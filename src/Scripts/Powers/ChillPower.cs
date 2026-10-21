using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 寒冷：提升敌人受到的**火焰与雷电**伤害。
/// 基础倍率 1.5（即 +50%），由 <see cref="CanonicalVars"/> 的 DamageIncrease 提供，
/// 因此升级/增强只需改这个变量。
/// 敌人回合结束时失去 1 层。
/// </summary>
public class ChillPower : CustomPowerModel
{
    private const string DamageIncreaseKey = "DamageIncrease";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/chill_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/chill_power_big.png";

    /// <summary>倍率变量，供本地化文本 <c>{DamageIncrease}</c> 使用。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(DamageIncreaseKey, 1.5m),
    ];

    /// <summary>
    /// 受伤乘算。返回 1 表示不缩放。
    /// 只对"目标是自己"且"伤害来自火/雷法术"生效。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner)
        {
            return 1m;
        }

        SpellSchool school = SpellProps.Resolve(props, cardSource);
        if (school != SpellSchool.Fire && school != SpellSchool.Lightning)
        {
            return 1m;
        }

        return DynamicVars[DamageIncreaseKey].BaseValue;
    }

    /// <summary>敌人回合结束时失去 1 层。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(this);
        }
    }
}
