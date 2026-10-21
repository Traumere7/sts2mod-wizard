using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 通灵：<b>在你的下 N 个回合开始时，你的第 1 张牌可免费打出</b>
/// （<c>Amount</c> = 剩余回合数）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 两层结构：本能力只负责"每回合发一次"，免费由载体能力承载
/// ══════════════════════════════════════════════════════════════════
/// 用户 2026-10-19 定的方案：
/// <list type="number">
///   <item>每个<b>玩家回合开始</b>，给玩家施加 <b>1 层</b>
///         <see cref="FreeThisTurnPower"/>（本回合第 1 张牌免费）；</item>
///   <item>随后<b>通灵自身层数 -1</b>。</item>
/// </list>
/// 这样通灵就<b>必然</b>在每回合开始时掉层（不会因"归零被移除"而丢失当回合效果），
/// 因为当回合的效果已经沉降到独立的载体能力上 —— 载体虽由通灵施加，
/// 但生命周期与通灵无关（打一张牌掉 1 层、回合结束必然移除）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结算时机：照抄原版「下回合获得能量」<c>EnergyNextTurnPower</c>
/// ══════════════════════════════════════════════════════════════════
/// <c>EnergyNextTurnPower.cs:23-31</c>：
/// <code>
/// public override async Task AfterEnergyReset(Player player)
/// {
///     if (player == base.Owner.Player)
///     {
///         await PlayerCmd.GainEnergy(base.Amount, player);
///         await PowerCmd.Remove(this);
///     }
/// }
/// </code>
/// ⇒ <b>回合开始（能量重置那一刻）就是标准结算点</b>。
/// 本能力同样在 <c>AfterEnergyReset</c> 里结算并掉层，
/// 与「整理」<c>TidyUp</c> 所用的那个原版效果保持同一节奏。
/// </para>
///
/// <para>
/// <b>顺序：先施加载体、再掉自己的层。</b>
/// 若顺序反过来，最后一层时本能力会先被移除 —— 虽然载体已经施加成功、
/// 效果其实不受影响，但"先给效果再扣代价"的顺序读起来更不容易出错。
/// </para>
///
/// <para>
/// ⚠️ <b>"下个回合"是自然成立的</b>：本牌在玩家回合内打出，
/// 而 <c>AfterEnergyReset</c> 在本回合早已跑过 ⇒ 最早下个回合才生效。
/// 不需要额外记"是否本回合"。
/// </para>
/// </summary>
public class TelepathyPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 还要影响几个回合。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/telepathy_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/telepathy_power_big.png";

    /// <summary>每回合施加几层「本回合免费」= 每回合几张免费牌。</summary>
    private const decimal FreeCardsPerTurn = 1m;

    // ══════════════════════════════════════════════════════════════════
    // 回合开始：发一次「本回合免费」，然后自己掉层
    // ══════════════════════════════════════════════════════════════════
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        Flash();

        // ① 把"本回合第 1 张牌免费"沉降到载体能力上。
        await PowerCmd.Apply<FreeThisTurnPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            FreeCardsPerTurn,
            Owner,
            null);

        // ② 自己掉层（归零时引擎自动移除）。
        //
        // ⚠️ 这里可以放心掉 —— 当回合的效果已经在 FreeThisTurnPower 上了，
        //    不会因为本能力被移除而消失。
        await PowerCmd.Decrement(this);
    }
}
