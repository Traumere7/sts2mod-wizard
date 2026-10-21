using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 雷光 - 2 费 - 攻击 - 罕见
/// 造成 8(10) 点雷电伤害，给予 1(2) 层麻痹，给予 1 层电流外溢。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class LightningBolt : WizardSpellCard
{
    private const int Cost = 2;

    private const string ParalysisKey = nameof(ParalysisPower);
    private const string OverflowKey = nameof(CurrentOverflowPower);

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<ParalysisPower>(1m),
        new PowerVar<CurrentOverflowPower>(1m),
    ];

    /// <summary>悬停时展示相关机制说明。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ParalysisPower>(),
        HoverTipFactory.FromPower<CurrentOverflowPower>(),
    ];

    public LightningBolt()
        : base(Cost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        await PowerCmd.Apply<ParalysisPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[ParalysisKey].BaseValue,
            Owner.Creature,
            this);

        // 电流外溢附着在**被打的敌人**身上：它之后再受到雷电伤害时溅射给其他敌人。
        await PowerCmd.Apply<CurrentOverflowPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[OverflowKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars[ParalysisKey].UpgradeValueBy(1m);
    }
}
