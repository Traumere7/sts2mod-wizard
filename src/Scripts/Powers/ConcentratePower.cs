using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 凝神：**每当你消耗 1 张牌时，获得 1 层法术增益。**
///
/// <para>
/// 触发点 <c>AfterCardExhausted</c>（范本 <c>FeelNoPainPower</c> 无痛、
/// <c>DarkEmbracePower</c> 黑暗拥抱）。
/// 注意这个钩子在 <c>CardCmd.cs:246</c> 触发，**因果与"回合末虚无"都会走到**
/// （<c>causedByEthereal</c> 参数区分二者）—— 本能力不区分，
/// 因为设计文档说的是"消耗 1 张牌"。
/// </para>
///
/// <para>
/// <b>只响应自己的牌：</b>多人环境下要确认 <c>card.Owner</c> 是自己的 Player，
/// 否则队友消耗牌也会给你加层。
/// </para>
/// </summary>
public class ConcentratePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 计数型：<c>Amount</c> = 打了几张「凝神」，
    /// 每消耗 1 张牌获得的法术增益<b>按层数缩放</b>。
    ///
    /// <para>
    /// ⚠️ 早期这里虽然声明了 <c>Counter</c>，但效果硬编码 <c>1m</c>、
    /// 完全没引用 <c>Amount</c> —— 于是"开两张凝神仍只有一张的效果"。
    /// 声明成 Counter 却不用 Amount，等于白白累加一个不起作用的数字。
    /// </para>
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/concentrate_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/concentrate_power_big.png";

    /// <summary>悬停文案展示用的"每次实际给多少"变量名。</summary>
    private const string GainAmountKey = "GainAmount";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 悬停文案里的数字 = 层数（每层给 1 点），随层数变化，
        // 所以在层数变化时同步（见 SyncDisplay）。
        new DynamicVar(GainAmountKey, 1m),
    ];

    /// <summary>每次消耗牌实际获得的法术增益层数（= 层数）。</summary>
    private decimal CurrentGain => Math.Max(Amount, 1);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncDisplay();
        return Task.CompletedTask;
    }

    /// <summary>层数变化时刷新悬停文案里的数字（再打一张要立刻体现）。</summary>
    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            SyncDisplay();
        }

        return Task.CompletedTask;
    }

    private void SyncDisplay()
    {
        if (DynamicVars.TryGetValue(GainAmountKey, out DynamicVar? v))
        {
            v.BaseValue = CurrentGain;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        // 只认自己的牌（多人环境下队友消耗牌不应给你加层）。
        if (card.Owner != Owner.Player)
        {
            return;
        }

        int stacks = Math.Max(Amount, 1);

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext, Owner, stacks, Owner, null);
    }
}
