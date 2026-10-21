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
/// 炽焰术 - 2 费 - 攻击 - 罕见
/// 造成 6(7) 点火焰伤害 3 次，给予 3(5) 层燃烧。
///
/// 多段伤害用 <c>DamageCmd.Attack(...).WithHitCount(3)</c> 一次结算，
/// 而不是循环调用 3 次 —— 前者走引擎的"多段攻击"通道，
/// 会正确触发"每段各算一次"的钩子（如电流外溢、火焰屏障）。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class BlazingFlame : WizardSpellCard
{
    private const int Cost = 2;

    private const int Hits = 3;

    private const string BurnKey = nameof(BurnPower);

    public override SpellSchool School => SpellSchool.Fire;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<BurnPower>(3m),
    ];

    public BlazingFlame()
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
            .WithHitCount(Hits)
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
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars[BurnKey].UpgradeValueBy(2m);
    }
}
