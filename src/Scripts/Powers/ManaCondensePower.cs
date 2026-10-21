using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 魔力凝聚：**在你的回合结束时，获得等同于你的[purple]法术增益[/purple]的格挡。**
///
/// <para>
/// <b>照抄观者 mod 的「如水」<c>LikeWaterPower</c>：</b>它用
/// <c>BeforeSideTurnEndEarly</c> 在"回合结束的早期阶段"给格挡。
/// 用这个时机（而不是 <c>AfterSideTurnEnd</c>）能让格挡赶在本回合结算完之前到手。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>2026-10-06 修：这张能力牌原本在卡上直接写 <c>BeforeSideTurnEndEarly</c>，
/// 且 <c>OnPlay</c> 是空的 —— 结果"打出后没有能力图标、效果也不生效"（实测反馈）。</b>
/// 原因是**能力牌的钩子必须由它施加的 <c>PowerModel</c> 承载**：
/// 卡打出后进弃牌堆，<c>CardModel</c> 上的回合结束钩子不再参与派发。
/// 正确姿势是"卡 <c>OnPlay</c> 施加一个能力 → 能力挂回合结束钩子"
/// （本 mod 其余 11 张能力牌都是这么写的，只有这一张漏了）。
/// </para>
///
/// <para>
/// <b>不叠层语义</b>：打第二张只是让层数 +1，但效果是"获得等同于法术增益的格挡"，
/// 与层数无关 —— 所以 <see cref="StackType"/> 用 <see cref="PowerStackType.Single"/>
/// 反而更诚实？<b>不。</b>这里仍用 <c>Counter</c>：
/// 一是与「修行」那次教训一致（"有没有更强"要看设计意图，目前设计未定义多层效果），
/// 二是 <c>Counter</c> 不会隐藏层数、便于以后扩展成"每层各结算一次"。
/// 若将来确认要"多层各给一次"，把 <c>BeforeSideTurnEndEarly</c> 改成按 <c>Amount</c> 循环即可。
/// </para>
/// </summary>
public class ManaCondensePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 计数型：<c>Amount</c> = 回合结束时**结算几次**。
    ///
    /// <para>
    /// ⚠️⚠️ <b>2026-10-08 修：原来声明了 <c>Counter</c> 却完全不读 <c>Amount</c>，
    /// 于是"空有叠层变量而无叠层逻辑"（实测反馈）。</b>
    /// 打第二张「魔力凝聚」时图标数字会变成 2、看起来叠上了，
    /// 但实际效果与只有 1 层时完全一样 —— 正是 <c>CONTEXT.md</c> §6.24.1
    /// 里"② 读了层数却硬编码常量"那一类。
    /// </para>
    ///
    /// <para>
    /// 现在的语义：**有几层就结算几次**（每次各按当时的法术增益给一份格挡），
    /// 与「燃烧之魂」<see cref="BurningSoulPower"/> 的按层循环同一写法。
    /// </para>
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath =>
        "res://Wizard/images/powers/mana_condense_power.png";

    public override string? CustomBigIconPath =>
        "res://Wizard/images/powers/mana_condense_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    /// <summary>
    /// 回合结束时按法术增益层数给格挡 —— **结算 <c>Amount</c> 次**
    /// （照抄「如水」<c>LikeWaterPower</c> 的时机，加上按层循环）。
    ///
    /// <para>
    /// ⚠️ 这里是 <c>PowerModel</c>，<c>Owner</c> 就是 <c>Creature</c>，
    /// 所以直接 <c>participants.Contains(Owner)</c> ——
    /// 与「快速咏唱」的临时能力同写法。
    /// </para>
    ///
    /// <para>
    /// <b>每次都用同一个法术增益值</b>（循环里不重算）——
    /// 因为法术增益在本回合结束这一瞬间是固定的，
    /// 而"结算 N 次"指的是给 N 份格挡，不是把法术增益翻倍。
    /// </para>
    /// </summary>
    public override async Task BeforeSideTurnEndEarly(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        int spellPower = Owner.GetPowerAmount<SpellPowerPower>();
        if (spellPower <= 0)
        {
            return;
        }

        // ★ 有几层就结算几次。
        int times = Math.Max(Amount, 1);

        Flash();

        for (int i = 0; i < times; i++)
        {
            await CreatureCmd.GainBlock(
                Owner,
                spellPower,
                ValueProp.Move,
                null,
                false);
        }
    }
}
