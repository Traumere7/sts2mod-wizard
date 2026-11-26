using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 薪柴 / Kindling：**每当你造成火焰伤害时，将那名敌人已有的燃烧层数增加 <c>Amount</c> 层。**
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「增加」而不是「给予」（关键）
/// ══════════════════════════════════════════════════════════════════
/// 本 mod 的燃烧是"取高值、不叠加"的（见 <see cref="BurnPower"/>），
/// 所以这里必须走 <see cref="BurnPower.ApplyAbsolute"/>（现值 + 增量），
/// 并像「天火浩荡」<c>SkyfireRampage</c> 一样手动补发
/// <see cref="WizardEvents.RaiseBurnApplied"/> —— 否则「纵火狂」「全知形态」等
/// 订阅"给予燃烧"的效果收不到这次增长。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 触发口径
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item>只认**自己造成**的伤害（<c>dealer == Owner</c>）。</item>
///   <item>必须带 <see cref="SpellProps.Fire"/> 标记（法术火焰伤害都由
///         <c>SpellProps.For(School)</c> 打上；燃烧自身的结算伤害**没有**这个标记，
///         所以不会自己喂自己）。</item>
///   <item>必须真的造成伤害（<c>result.TotalDamage &gt; 0</c>）。</item>
///   <item>目标必须**已有**燃烧（层数 &gt; 0）—— 「将燃烧层数增加」的前提是它本来就有。</item>
/// </list>
/// </para>
/// </summary>
public class KindlingPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数器型：<c>Amount</c> = 每次火焰伤害增加几层燃烧（每张「薪柴」+1）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/kindling_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/kindling_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // ① 只认自己造成的伤害。
        if (dealer != Owner)
        {
            return;
        }

        // ② 必须真的打出了伤害（被格挡光的不算）。
        if (result.TotalDamage <= 0m || !target.IsAlive)
        {
            return;
        }

        // ③ 必须是火焰伤害（以太算，因为 Aether = Fire | Lightning | Ice）。
        if (!SpellProps.HasSchool(props, cardSource, SpellSchool.Fire))
        {
            return;
        }

        // ④ 目标必须已有燃烧 —— "增加层数"，不是"施加燃烧"。
        int current = target.GetPowerAmount<BurnPower>();
        if (current <= 0)
        {
            return;
        }

        int add = (int)Amount;
        if (add <= 0)
        {
            return;
        }

        // ⑤ 走「天火浩荡」同款的加算式施加，并补发"给予燃烧"事件。
        BurnApplicationResult applied = await BurnPower.ApplyAbsolute(
            choiceContext,
            target,
            current + add,
            Owner,
            null);

        if (applied.Landed)
        {
            await WizardEvents.RaiseBurnApplied(target, Owner, add, landed: true);
        }
    }
}
