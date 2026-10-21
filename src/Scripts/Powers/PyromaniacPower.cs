using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 纵火狂：**每当你给予燃烧时，获得 N 点活力**（N = 层数，2 / 升级后 4）。
///
/// <para>
/// 「活力」<see cref="VigorPower"/> 是原版机制：下一次攻击牌**加算**这些点伤害，
/// 结算后清零。参考原版「地形改造」<c>Terraforming</c>。
/// </para>
///
/// <para>
/// <b>叠层 = 每次给的活力变多</b>：<c>StackType.Counter</c>，
/// <c>Amount</c> 直接就是每次给的活力点数（2 或 4），
/// 引擎的层数累加天然实现"两张纵火狂 = 每次给 4 点"。
/// 不需要任何自己维护的变量 —— 这是本 mod 反复踩坑后总结出的最稳模式
/// （同「超导」）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么必须用 mod 自己的事件，而不是引擎钩子
/// ══════════════════════════════════════════════════════════════════
/// 游戏在 <c>PowerCmd.ModifyAmount</c> 里是这么派发的：
/// <code>
/// if ((int)modifiedOffset != 0)
///     await Hook.AfterPowerAmountChanged(...);   // PowerCmd.cs:247
/// </code>
/// <b>数值没变化时钩子根本不发。</b>
///
/// 而设计语义是「**施加动作本身算数**」：敌人已有 4 层燃烧，再施加 1 层，
/// 结果仍是 4 层，但这**算作一次有效的"给予燃烧"**，本能力必须响应。
/// 引擎钩子表达不了"无变化的施加"，所以
/// <see cref="SpellCmd.ApplyBurn"/> 在两个分支都会显式触发
/// <see cref="WizardEvents.BurnApplied"/>。
/// </para>
///
/// <para>
/// <b>⚠️ 静态事件的订阅生命周期</b>
/// <see cref="WizardEvents.BurnApplied"/> 是 <b>static</b> 事件，
/// 订阅后若不退订，能力实例会被事件一直引用 —— 跨战斗泄漏，
/// 而且<b>已结束战斗里的旧能力仍会响应新战斗的燃烧</b>。
///
/// 因此：
/// <list type="bullet">
///   <item><c>AfterApplied</c> 里先 <c>-=</c> 再 <c>+=</c>（幂等，避免重复订阅）</item>
///   <item><c>AfterRemoved</c> 与<b>战斗结束时</b>都要退订</item>
/// </list>
/// 注意 <c>Player.AfterCombatEnd()</c> 会直接清空能力、<b>不保证</b>逐个走
/// <c>AfterRemoved</c>，所以额外挂了 <c>AfterCombatEnd</c> 做兜底退订。
/// </para>
///
/// <para>
/// <b>只认"我给敌人上的"燃烧</b>：<c>applier != Owner</c> 直接跳过；
/// 并且目标必须<b>不是自己</b>（给自己上燃烧不该赚活力 ——
/// 何况带上「燃烧免疫」后自己根本烧不起来）。
/// </para>
/// </summary>
public class PyromaniacPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 每次给予燃烧时获得的活力点数。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/pyromaniac_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/pyromaniac_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    /// <summary>是否已订阅（也用于退订时的幂等保护）。</summary>
    private bool _subscribed;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 先退订再订阅，保证不会重复挂同一个委托。
        WizardEvents.BurnApplied -= OnBurnApplied;
        WizardEvents.BurnApplied += OnBurnApplied;
        _subscribed = true;
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 战斗结束兜底退订。
    ///
    /// <para>
    /// 用 <c>AfterCombatEnd</c> 而<b>不是</b> <c>AfterCombatVictory</c>：
    /// <c>Player.AfterCombatEnd()</c> 会 <c>RemoveAllPowersInternalExcept()</c> 清空能力，
    /// 而 <c>Hook.AfterCombatVictory</c> 在那之后才跑 —— 为 Power 重写的
    /// <c>AfterCombatVictory</c> 永远不会触发（详见 <c>CONTEXT.md</c> §6.4）。
    /// </para>
    /// </summary>
    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        WizardEvents.BurnApplied -= OnBurnApplied;
        _subscribed = false;
    }

    /// <summary>
    /// 只响应"由本能力拥有者施加给别人的"燃烧 ——
    /// 别人给敌人上燃烧不该让我获益，自己给自己上也不算。
    ///
    /// <para>
    /// ⚠️ 判据用事件传来的 <c>landed</c>，<b>不要</b>自己查 artifact ——
    /// 被抵消时引擎已把 artifact 那层减掉，查询必为 false。
    /// </para>
    /// </summary>
    private async Task OnBurnApplied(Creature target, Creature? applier, decimal amount, bool landed)
    {
        if (applier != Owner || Amount <= 0)
        {
            return;
        }

        // ★ 只有真的落上才给活力（2026-10-13 第二次修正）。
        //
        // ⚠️ 这里**不能**自己查 `target.HasPower<ArtifactPower>()`：
        //    被抵消时引擎已经把 artifact 那一层减掉了，查询必为 false，
        //    于是"最后一层 artifact 被打掉"这种情况会被误判成"没被抵消"
        //    —— 这正是用户实测到的 bug。判据必须用施加方传来的 landed。
        if (!landed)
        {
            return;
        }

        // 给自己上燃烧不算"纵火"（且带免疫时压根不会发生）。
        if (target == Owner)
        {
            return;
        }

        Flash();

        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            Amount,
            Owner,
            null);
    }
}
