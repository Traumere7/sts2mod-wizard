using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 火刑：**每回合开始时给全体敌人「燃烧量」层燃烧，然后燃烧量按「成长值」递增**。
///
/// <para>
/// <b>两个量分开存，这是本能力的核心设计：</b>
/// </para>
/// <list type="table">
///   <item>
///     <term><b>成长值</b></term>
///     <description>本能力的 <c>Amount</c>。每施加一次「火刑」+1
///       （引擎对已存在实例走 <c>ModifyAmount</c>，层数天然累加）。</description>
///   </item>
///   <item>
///     <term><b>燃烧量</b></term>
///     <description><see cref="BurnRateKey"/> 变量。卡牌施加时写入初值（3，升级 4），
///       之后每回合 +成长值。</description>
///   </item>
/// </list>
///
/// <para>
/// <b>为什么让 <c>Amount</c> 当成长值，而不是"<c>Amount</c>=燃烧量、另存成长"：</b>
/// <c>PowerStackType.Counter</c> 已经把"每张 +1"这个语义免费给出来了 ——
/// 一张 = 成长 1、两张 = 成长 2。
/// 反过来若把燃烧量放 <c>Amount</c>，第二张会让燃烧量翻倍（3→6），与设计不符；
/// 而且还得从 <c>Amount</c> 反推施加次数，那个除法在升级后会算错。
/// <b>早期实现就是那样，实测反馈"两个都加、很混乱"。</b>
/// </para>
///
/// <para>
/// <b>期望数值</b>：
/// <list type="bullet">
///   <item>单张：每回合给 3 层燃烧；燃烧量每回合 +1</item>
///   <item>两张：每回合给 3 层燃烧；燃烧量每回合 +2</item>
///   <item>升级：只提高初始燃烧量（3 → 4），不影响成长</item>
/// </list>
/// </para>
///
/// <para>
/// 触发点用 <c>AfterSideTurnStart(side = Player)</c> —— 理由与
/// <see cref="BurnPower"/> 的伤害时点直接相关：
/// <list type="bullet">
///   <item>燃烧伤害发生在<b>被附着者所在阵营的回合开始</b>（即敌人回合开始）</item>
///   <item>本能力在<b>玩家回合开始</b>施加燃烧，早于敌人回合开始</item>
/// </list>
/// 于是"本回合上的燃烧"必定能赶上"本回合敌人的结算"，不会晚一拍。
/// </para>
///
/// <para>
/// <b>参与者判定</b>：<c>AfterSideTurnStart</c> 的 <c>participants</c> 是
/// <b>当前回合那一方</b>的生物（<c>CombatManager.cs:709</c>）。
/// 本能力挂在玩家身上，所以 <c>side == Player</c> 时
/// <c>participants.Contains(Owner)</c> 才成立 —— 与
/// <c>DrawCardsNextTurnPower</c> 等原版能力的写法一致。
/// </para>
///
/// <para>
/// <b>只作用于敌人</b>：用 <c>combatState.GetOpponentsOf(Owner)</c> 取对立面并过滤存活，
/// 与「观火」<c>WatchFire</c> 的写法一致。
/// </para>
/// </summary>
public class PyroJudgmentPower : CustomPowerModel
{
    /// <summary>「每回合施加的燃烧层数」变量名。</summary>
    public const string BurnRateKey = "BurnRate";

    /// <summary>每次施加本能力给成长值加的增量（= 每张「火刑」+1）。</summary>
    public const int GrowthPerApplication = 1;

    /// <summary>未升级「火刑」写入的初始燃烧量。</summary>
    public const int InitialBurnRate = 3;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 计数型，<c>Amount</c> = <b>成长值</b>（= 已施加的次数）。
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/pyro_judgment_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/pyro_judgment_power_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每回合施加的燃烧层数。初值 = 未升级卡牌的初始燃烧量，
        // 卡牌施加时会把真实初值写进来（升级后是 4）。
        new DynamicVar(BurnRateKey, InitialBurnRate),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    /// <summary>本回合应施加的燃烧层数（当前值）。</summary>
    public decimal BurnRate
        => DynamicVars.TryGetValue(BurnRateKey, out DynamicVar? v) ? v.BaseValue : InitialBurnRate;

    /// <summary>卡牌施加时写入初始燃烧量（按卡牌的升级状态）。</summary>
    public void SetInitialBurnRate(decimal value)
    {
        if (DynamicVars.TryGetValue(BurnRateKey, out DynamicVar? v))
        {
            v.BaseValue = value;
        }
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncDisplay();
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        // 只在"自己这方的回合开始"触发（即玩家回合）。
        if (side != CombatSide.Player || !participants.Contains(Owner))
        {
            return;
        }

        if (Amount <= 0)
        {
            return;
        }

        List<Creature> enemies = combatState
            .GetOpponentsOf(Owner)
            .Where(c => c.IsAlive)
            .ToList();

        if (enemies.Count == 0)
        {
            return;
        }

        Flash();

        decimal burnRate = BurnRate;

        // "给予燃烧"的动作本身算数，所以走 SpellCmd（内部会触发 WizardEvents.BurnApplied）。
        foreach (Creature enemy in enemies)
        {
            await SpellCmd.ApplyBurn(new ThrowingPlayerChoiceContext(), enemy, burnRate, Owner, null);
        }

        // 燃烧量按**成长值**（= Amount）递增。成长值本身由施加次数决定，不在这里改。
        SetInitialBurnRate(burnRate + Amount);
    }

    /// <summary>
    /// 保证展示值有效。
    /// 目前只做"从未初始化时兜回初值"，不做别的纠正 ——
    /// 燃烧量的递增完全由 <see cref="AfterSideTurnStart"/> 驱动。
    ///
    /// <para>
    /// 说明：<c>PowerModel.HoverTips</c> 是<b>普通属性、不可重写</b>，
    /// 所以无法在渲染前拦截。好在卡牌施加时会把初值写好
    /// （<c>PyroJudgment.OnPlay</c> 调 <see cref="SetInitialBurnRate"/>），
    /// 之后每次 <see cref="AfterSideTurnStart"/> 都维护它，不会失真。
    /// </para>
    /// </summary>
    private void SyncDisplay()
    {
        if (DynamicVars.TryGetValue(BurnRateKey, out DynamicVar? v) && v.BaseValue <= 0m)
        {
            v.BaseValue = InitialBurnRate;
        }
    }
}
