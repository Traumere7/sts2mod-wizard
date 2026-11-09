using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 凝神 - 1(0) 费 - 能力 - 罕见
/// 每当你消耗 1 张牌时，获得 1 层法术增益。
///
/// 效果由 <see cref="ConcentratePower"/> 承载。升级只降费。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Concentrate : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>
    /// 卡面**要**解释「消耗」（buff 侧不解释）—— 原版「无惧疼痛」`FeelNoPain` 就是这么分的：
    /// 卡挂 `FromKeyword(CardKeyword.Exhaust)`，`FeelNoPainPower` 只挂 `Static(Block)`。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public Concentrate()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ConcentratePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
