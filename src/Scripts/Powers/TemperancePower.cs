using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 节制：**本回合你只能再打出 <see cref="AllowedPlays"/> 张牌**（负面效果，可被「高温消毒」清除）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 照抄原版「安可」<c>EncorePower</c>（愚者牌的「安可」）
/// ══════════════════════════════════════════════════════════════════
/// 结构与 <c>EncorePower</c> 完全一致：
/// <list type="bullet">
///   <item><c>PowerStackType.Counter</c>，<c>Amount</c> = 允许打出的张数；</item>
///   <item>内部 <c>Data.numberOfCardsPlayedSinceApplied</c> 记录"施加之后打了几张"；</item>
///   <item><c>ShouldPlay</c> 返回 false ⇒ <c>Hook.ShouldPlay</c> 拦下这张牌
///         （引擎里 <c>CardCmd.Play:63</c> 与 <c>CardModel:1908</c> 都会问它），
///         所以打满之后<b>手牌上所有牌都不能再打出</b>；</item>
///   <item><c>AfterSideTurnEnd</c> 时移除自己。</item>
/// </list>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 与原版「安可」的两点**刻意差别**（用户 2026-11-14 明确）
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>不能靠再打一张「节制」叠加</b>：安可是可叠加的（两张 = 再打两张），
///         而节制<b>不叠加、也不刷新计数</b>。
///         实现方式：<see cref="Cards.Temperance"/> 在施加前检查
///         <c>HasPower&lt;TemperancePower&gt;()</c>，已经存在就<b>完全不施加</b> ——
///         既不增加 <c>Amount</c>，也不会重置 <see cref="Data.NumberOfCardsPlayedSinceApplied"/>。
///         另一个细节：再打出的那张「节制」<b>自己也算</b>"打出了一张牌"，
///         因为它同样会走 <see cref="AfterCardPlayed"/>（与「安可」一致）。</item>
///   <item><b>是负面效果</b>（<see cref="PowerType.Debuff"/>）⇒ 能被「高温消毒」这类
///         "移除自身负面效果"的牌清掉，从而解除限制 —— 这是设计留的出口。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <c>cardsBeingPlayed</c> 的作用（照抄原版注释）：记录"正在被打出途中"的牌。
/// 重放 / 多次结算时同一张牌会连续走 <c>BeforeCardPlayed</c>，先记进来再算，
/// 避免"这一张还没结算完就已经被判成下一张"的错位。
/// </para>
/// </summary>
public class TemperancePower : CustomPowerModel
{
    /// <summary>本回合还能打出的张数（设计固定 2，升级不改变这个值）。</summary>
    public const int AllowedPlays = 2;

    /// <summary>卡面/悬停里的动态变量名。</summary>
    private const string PlaysRemainingKey = "PlaysRemaining";

    /// <summary>内部可变状态（照抄 <c>EncorePower.Data</c>）。</summary>
    private sealed class Data
    {
        /// <summary>正在打出途中的牌（重放时同一张会连续进入）。</summary>
        public readonly List<CardModel> CardsBeingPlayed = new();

        /// <summary>施加之后已经打出了几张牌。</summary>
        public int NumberOfCardsPlayedSinceApplied;
    }

    public override PowerType Type => PowerType.Debuff;

    /// <summary>计数器型：<c>Amount</c> = 允许打出的总张数。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>图标上显示"还能再打几张"。</summary>
    public override int DisplayAmount => PlaysRemaining;

    private int PlaysRemaining =>
        Math.Max(0, (int)Amount - GetInternalData<Data>().NumberOfCardsPlayedSinceApplied);

    /// <summary>
    /// ⚠️ canonical 的默认值必须是 <see cref="AllowedPlays"/>（不是 0）：
    /// 卡面「节制」的悬停用的是 <b>canonical 能力</b>，写成 0 会显示"只能再打出 0 张牌"。
    /// 实际实例在 <see cref="AfterApplied"/> 里会重算成真实剩余数。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(PlaysRemainingKey, AllowedPlays),
    ];

    protected override object? InitInternalData() => new Data();

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/temperance_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/temperance_power_big.png";

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        DynamicVars[PlaysRemainingKey].BaseValue = PlaysRemaining;
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner)
        {
            GetInternalData<Data>().CardsBeingPlayed.Add(cardPlay.Card);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 打满之后拦下**任何**牌（<c>Hook.ShouldPlay</c> → <c>CardCmd.Play</c>）。
    /// </summary>
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (card.Owner.Creature != Owner)
        {
            return true;
        }

        Data data = GetInternalData<Data>();
        return data.NumberOfCardsPlayedSinceApplied + data.CardsBeingPlayed.Count < Amount;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        Data data = GetInternalData<Data>();

        // 只有"我们真的记过这张"才计数（照抄原版：防止别的来源的 AfterCardPlayed 混进来）。
        if (!data.CardsBeingPlayed.Remove(cardPlay.Card))
        {
            return Task.CompletedTask;
        }

        data.NumberOfCardsPlayedSinceApplied++;
        DynamicVars[PlaysRemainingKey].BaseValue = PlaysRemaining;
        InvokeDisplayAmountChanged();

        return Task.CompletedTask;
    }

    /// <summary>回合结束时移除（照抄原版 <c>EncorePower</c>）。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            Flash();
            await PowerCmd.Remove(this);
        }
    }
}
