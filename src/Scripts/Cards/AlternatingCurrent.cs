using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 交流电 - 1 费 - 能力 - 稀有
/// 你的每次雷电伤害额外给予 1 层电流外溢。**升级后获得「固有」。**
///
/// <para>
/// 效果由 <see cref="AlternatingCurrentPower"/> 承载。
/// </para>
///
/// <para>
/// <b>升级添加词条完全照抄原版「杂耍」<c>Juggling</c>：</b>
/// <list type="bullet">
///   <item>初始<b>不</b>在 <c>CanonicalKeywords</c> 里声明任何关键字</item>
///   <item><c>OnUpgrade</c> 调 <c>AddKeyword(CardKeyword.Innate)</c></item>
/// </list>
/// 原版 <c>Juggling</c> 正是"<c>OnPlay</c> 施加一个能力 + 升级只给固有"，
/// 与本卡结构完全同型，所以直接沿用它的写法。
/// </para>
///
/// <para>
/// <b>本地化文案里不要写「（固有）」</b> —— 关键字由引擎渲染成卡面词条，
/// 升级后标签自动出现（同「燃烧之魂」的「保留」）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class AlternatingCurrent : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CurrentOverflowPower>(),
    ];

    public AlternatingCurrent()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AlternatingCurrentPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    /// <summary>升级获得「固有」（照抄原版「杂耍」<c>Juggling</c>）。</summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
