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
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 电弧 / ElectricArc - 1 费 - 罕见 - 攻击
/// 给予 3(4) 层电流外溢，然后造成 7(9) 点雷电伤害。
///
/// <para>
/// 顺序是设计的一部分：**先挂电流外溢、再打雷伤** ——
/// 这样这一发雷伤本身就能吃到「电流外溢」的溅射（它是"受到雷电伤害时"触发）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ElectricArc : WizardSpellCard
{
    private const int Cost = 1;

    private const string OverflowKey = nameof(CurrentOverflowPower);

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<CurrentOverflowPower>(3m),
        new DamageVar(7m, ValueProp.Move | SpellProps.For(School)),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CurrentOverflowPower>(),
    ];

    public ElectricArc()
        : base(Cost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // ① 先挂电流外溢。
        await PowerCmd.Apply<CurrentOverflowPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[OverflowKey].BaseValue,
            Owner.Creature,
            this);

        // ② 再打雷电伤害（这一发能触发刚挂上的电流外溢）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(WizardVfx.LightningOrbImpact)
            .Execute(choiceContext);
    }

    /// <summary>升级：电流外溢 3 → 4、伤害 7 → 9。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[OverflowKey].UpgradeValueBy(1m);
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
