using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 薪柴 / Kindling - 1 费 - 稀有 - 能力
/// 每当你造成火焰伤害时，将敌人具有的燃烧层数增加 1(2)。
///
/// <para>
/// 触发与加算逻辑全在 <see cref="KindlingPower"/> 里；
/// 本类只负责施加能力与升级数值（1 → 2）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Kindling : WizardCardModel
{
    private const int Cost = 1;

    private const string PowerKey = nameof(KindlingPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<KindlingPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    public Kindling()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        await PowerCmd.Apply<KindlingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[PowerKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级：每次火焰伤害增加的燃烧层数 1 → 2。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[PowerKey].UpgradeValueBy(1m);
    }
}
