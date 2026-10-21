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

namespace Wizard.Scripts.Powers;

/// <summary>
/// 麻痹：依据层数，使若干回合内敌人造成的伤害减半。
/// 敌人回合结束时失去 1 层。
///
/// 写法与 <c>WeakPower</c>（虚弱，造成 75% 伤害）完全一致，只是把倍率改成 0.5。
/// 注意：倍率是"造成伤害的比例"，不是"减少的比例"。
/// </summary>
public class ParalysisPower : CustomPowerModel
{
    private const string DamageDecreaseKey = "DamageDecrease";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/paralysis_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/paralysis_power_big.png";

    /// <summary>减伤倍率 0.5 = 造成 50% 伤害。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(nameof(ParalysisPower), 0m),
        new DynamicVar(DamageDecreaseKey, 0.5m),
    ];

    /// <summary>拥有者造成的伤害减半。</summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner)
        {
            return 1m;
        }

        if (!props.IsPoweredAttack())
        {
            return 1m;
        }

        return DynamicVars[DamageDecreaseKey].BaseValue;
    }

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
