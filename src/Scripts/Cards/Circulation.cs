using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 环流 - 1(0) 费 - 能力 - 罕见
/// 电流外溢现在可以对敌人自身结算。
///
/// 效果本身完全由 <see cref="CirculationPower"/> 承载 ——
/// 判定点在 <see cref="CurrentOverflowPower"/> 里，本卡只负责施加能力。
/// 升级只降费用（1 → 0），所以 <c>OnUpgrade</c> 用
/// <c>UpgradeBaseCost</c> 而不是改任何变量。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Circulation : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CurrentOverflowPower>(),
    ];

    public Circulation()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CirculationPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 1 费 → 0 费。费用升级走 EnergyCost.UpgradeBy（原版 60 张牌都是这个写法）。
        EnergyCost.UpgradeBy(-1);
    }
}
