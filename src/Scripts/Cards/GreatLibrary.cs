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
/// 图书馆 - 3(2) 费 - 能力 - 稀有
/// 保留。在你的回合开始时，额外抽 2 张牌。
///
/// <para>
/// 效果直接照抄原版「机器学习」<c>MachineLearning</c>：
/// 给玩家挂 <see cref="GreatLibraryPower"/>，它重写 <c>ModifyHandDraw</c> 增加每回合手牌数。
/// 升级只降费 3 → 2（与原版"升级加「固有」"不同 —— 本牌设计要求的是<b>保留</b>，且升级前后都有）。
/// </para>
///
/// <para>
/// ⚠️ 「保留」写在 <see cref="CanonicalKeywords"/> 里，所以<b>升级前后都有</b>
/// （升级只动费用，不 AddKeyword / RemoveKeyword）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class GreatLibrary : WizardCardModel
{
    private const int Cost = 3;

    private const int DrawCount = 2;

    /// <summary>「保留」（升级前后都有）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(DrawCount),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    public GreatLibrary()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(
            Owner.Creature,
            "PowerUp",
            Owner.Character.PowerUpAnimDelay);

        await PowerCmd.Apply<GreatLibraryPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Cards.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 费用 3 → 2
        EnergyCost.UpgradeBy(-1);
    }
}
