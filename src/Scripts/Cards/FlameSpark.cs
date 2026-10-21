using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 火苗 - 0 费 - 攻击 - 普通
/// 造成 6(9) 点火焰伤害，给予 1 层燃烧。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class FlameSpark : WizardSpellCard
{
    private const int Cost = 0;

    private const string BurnKey = nameof(BurnPower);

    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>悬停时展示相关机制说明。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<BurnPower>(1m),
    ];

    public FlameSpark()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        await SpellCmd.ApplyBurn(
            choiceContext,
            cardPlay.Target,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 6 → 9
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
