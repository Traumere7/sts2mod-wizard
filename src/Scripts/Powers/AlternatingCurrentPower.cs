using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 交流电：**你的每次雷电伤害，额外给予该目标 1 层电流外溢。**
///
/// <para>
/// 触发点 <c>AfterDamageGiven</c>（范本 <c>EnvenomPower</c>）：
/// 判定"这是雷电伤害"用 <c>SpellProps.Resolve(props, cardSource)</c>，
/// 与「寒冷」「电流外溢」等保持一致 —— 优先看 <c>ValueProp</c> 标记位
/// （任何来源都可靠），回退到卡牌接口。
/// </para>
///
/// <para>
/// <b>⚠️ 防重入是必须的</b>：本能力施加的 <see cref="CurrentOverflowPower"/> 会让
/// 目标在被雷电击中时溅射。虽然溅射本身不带学派（刻意设计成不吃任何加成、
/// 也不会再次触发本能力），但仍加一道 <c>_resolving</c> 保险 ——
/// 一旦将来有人改了溅射的 props，也不至于无限递归。
/// </para>
///
/// <para>
/// <b>冷却按"每次伤害"而不是"每张牌"</b>：多段雷电攻击（如未来的多段雷电牌）
/// 每一段都会各触发一次，符合"每次雷电伤害"的字面语义。
/// </para>
///
/// <para>
/// <b>叠层 = 每次给的电流外溢层数变多</b>（<c>StackType.Counter</c>，
/// <c>Amount</c> 直接就是每次施加的层数）：
/// 2 张「交流电」⇒ 每次雷电伤害给 2 层，而
/// <see cref="CurrentOverflowPower"/> 是<b>每层独立结算一次</b>
/// （见其 <c>AfterDamageReceived</c> 里的 <c>for (i &lt; Amount)</c>），
/// 所以叠层后每次雷电伤害会各溅射一次。
/// </para>
///
/// <para>
/// ⚠️ <b>早期这里是 <c>Single</c> 且硬编码 <c>1m</c></b>，
/// 于是"开两张交流电仍只有一张的效果"—— 这是本 mod 反复踩过的同一个坑
/// （同「远古残章」「冰之城墙」「凝神」「纵火狂」）：
/// <b>声明成 Counter/Single 却不用 <c>Amount</c>，等于白白累加一个不起作用的数字</b>。
/// 最稳的模式就是"把值直接放 <c>Amount</c>，让引擎的 <c>ModifyAmount</c> 天然累加"。
/// </para>
/// </summary>
public class AlternatingCurrentPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 每次雷电伤害额外施加的电流外溢层数。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/alternating_current_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/alternating_current_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CurrentOverflowPower>(),
    ];

    /// <summary>防重入：自己施加的电流外溢若再次触发本能力会无限递归。</summary>
    private bool _resolving;

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (_resolving || dealer != Owner || !target.IsAlive)
        {
            return;
        }

        // 只响应雷电法术伤害。
        if (SpellProps.Resolve(props, cardSource) != SpellSchool.Lightning)
        {
            return;
        }

        // 只为"真的造成了伤害"的那一段触发（全被格挡则不算）。
        if (result.TotalDamage <= 0)
        {
            return;
        }

        _resolving = true;
        try
        {
            // 每次给 Amount 层（叠层就是"每次给得更多"）。
            // Amount 为 0 时不该发生；但 PowerCmd.Apply 对 amount==0 会直接返回，
            // 这里用 Max(Amount, 1) 兜底，避免因故为 0 时退化成"完全不触发"。
            await PowerCmd.Apply<CurrentOverflowPower>(
                choiceContext,
                target,
                Math.Max(Amount, 1),
                Owner,
                null);
        }
        finally
        {
            _resolving = false;
        }
    }
}
