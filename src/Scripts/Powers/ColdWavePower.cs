using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 大寒波：**本回合内，你每打出一张牌，就给予所有敌人 N 层寒冷**（N = 层数）。
/// 玩家回合结束时移除。
///
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 时序：用 <c>BeforeCardPlayed</c>，不是 <c>AfterCardPlayed</c>
/// ══════════════════════════════════════════════════════════════════
///
/// <b>设计要的是「先寒冷、后伤害」</b>，这样本回合打出的火焰/雷电攻击
/// **当次就能吃到寒冷加成**。引擎里"打出牌"的顺序
/// （<c>CardModel.Play</c>，见 <c>CardModel.cs:1926/1933</c>）是：
///
/// <code>
/// await Hook.BeforeCardPlayed(...)      // :1926  ← 寒冷挂在这里
/// Task task = OnPlay(...)               // :1933  ← 伤害在这里结算
/// ... AfterCardPlayed / AfterCardPlayedLate
/// </code>
///
/// 因此：
/// <list type="bullet">
///   <item><b><c>BeforeCardPlayed</c>（本实现）</b>：
///         寒冷 → 该牌自己的伤害结算 ⇒ **当次攻击就享受 +50%** ✅ 设计要的</item>
///   <item><c>AfterCardPlayed</c>：该牌伤害 → 寒冷挂上
///         ⇒ 当次**吃不到**，只有之后的牌受益 ❌</item>
/// </list>
///
/// <para>
/// <b>范本对照</b>：原版「腐蚀波」<c>CorrosiveWavePower</c> 用的是
/// <c>AfterCardDrawn</c>（抽牌即上毒，同样在"牌的结算"之前，因此当次也吃得到）；
/// 「湮灭」<c>OblivionPower</c> 用 <c>BeforeCardPlayed</c> 记计划 +
/// <c>AfterCardPlayed</c> 结算。本能力与「腐蚀波」的思路一致：
/// 在牌结算**之前**就 apply，所以不需要「湮灭」那本回查字典。
/// </para>
///
/// <para>
/// <b>「大寒波」自己也要触发寒冷</b>（只是不吃自己那一层）：
/// 期望时序见 <see cref="BeforeCardPlayed"/> 的注释。
/// ⚠️ 不要加"跳过所有「大寒波」"的守卫 —— 那是错的，
/// 会让这张牌完全拿不到此前累计层数的收益。
/// </para>
///
/// <para>
/// <b>层数语义</b>（与「凝神」「纵火狂」等同一套）：<c>Amount</c> = **每张牌给几层寒冷**，
/// 打第二张「大寒波」就变成每张牌 2 层。设计只写了"1 层"，
/// 但把值直接放 <c>Amount</c> 是本 mod 反复验证过的最稳模式
/// （见 <c>CONTEXT.md</c> §6.24）。
/// </para>
/// </summary>
public class ColdWavePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 每打出一张牌给几层寒冷。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/cold_wave_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/cold_wave_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    /// <summary>
    /// 每打出一张牌之前：把"本能力**此刻**的层数"作为寒冷挂给所有敌人。
    ///
    /// <para>
    /// ⚠️ <b>必须是 <c>BeforeCardPlayed</c></b>：它在 <c>OnPlay</c>（伤害结算）
    /// 之前执行，这样这一张牌自己的火焰/雷电伤害就能吃到寒冷加成。
    /// 改成 <c>AfterCardPlayed</c> 会让"当次攻击吃不到"，与设计不符。
    /// </para>
    ///
    /// <para>
    /// ⚠️⚠️ <b>不要加"跳过所有「大寒波」"的守卫。</b>
    /// 曾经写成 <c>if (cardPlay.Card is ColdWave) return;</c> —— 那是<b>错的</b>：
    /// 它让「大寒波」**完全**不触发寒冷，而正确语义是
    /// "<b>只排除它自己这一张贡献的层数，仍要享受此前已生效层数的加成</b>"。
    /// 由于本方法在该牌的 <c>OnPlay</c>（把层数加进 <c>Amount</c>）之前执行，
    /// 直接读 <c>Amount</c> 就自动做到了这件事，<b>无需任何特殊分支</b>。
    /// </para>
    ///
    /// <para>
    /// <b>为什么不需要原版「湮灭」<c>OblivionPower</c> 那本
    /// <c>Dictionary&lt;CardModel,int&gt;</c>：</b>
    /// 它之所以要记，是因为它把 <c>apply</c> 推迟到了 <c>AfterCardPlayed</c> ——
    /// 那时 <c>Amount</c> 已经被该牌自己加过了，只能回查"打之前是几层"。
    /// 本实现在 <c>BeforeCardPlayed</c> 里<b>立刻 apply</b>，
    /// 读到的就是打之前的层数，用完即弃，所以不需要留任何记录。
    /// </para>
    ///
    /// <para>
    /// 预期时序（经用户实测确认）：
    /// <code>
    /// 打 A：能力尚不存在 ⇒ 不给寒冷；A 的 OnPlay 把能力设为 1 层
    /// 打 B：读到 1 层 ⇒ 给 1 层寒冷；B 的 OnPlay 把能力加到 2 层
    /// 打 C：读到 2 层 ⇒ 给 2 层寒冷；C 的 OnPlay 把能力加到 3 层
    /// </code>
    /// </para>
    /// </summary>
    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        // 只响应"自己能拥有的、打出的牌"（判据同原版「湮灭」）。
        if (cardPlay.Card.Owner != Owner.Player)
        {
            return;
        }

        ICombatState? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        // ⚠️ 此刻仍是"这张牌生效前"的层数（OnPlay 还没跑），
        //    所以直接用它 —— 既排除了这张牌自己的贡献，又保留了此前的累计。
        //    Amount 为 0 时不该发生（能力会被移除）；兜底避免退化成"完全不触发"。
        int stacks = Math.Max(Amount, 1);

        Flash();

        // 立刻 apply（不留到 AfterCardPlayed）——
        // 这样紧接着的 OnPlay 里的伤害就能吃到这次寒冷加成。
        //
        // 走 SpellCmd.ApplyChillToAll 统一收口：这样"每当施加寒冷时"的效果
        // （「弹射」DoubleRicochet 的返回手牌）才能响应到。
        await SpellCmd.ApplyChillToAll(
            new ThrowingPlayerChoiceContext(),
            combatState.HittableEnemies,
            stacks,
            Owner,
            null);
    }

    /// <summary>玩家回合结束时移除（"本回合内"）。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}
