using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 顿悟 - 0 费 - 技能 - 衍生（Token）
/// 保留。选择 1 张手牌消耗。从抽牌堆选 2(3) 张牌加入你的手牌。消耗。
///
/// <para>
/// 两部分分别照抄原版：
/// <list type="bullet">
///   <item>消耗手牌 → 「燃烧契约」<c>BurningPact</c>
///         （<c>FromHand(ExhaustSelectionPrompt, N)</c>）</item>
///   <item>从抽牌堆取牌 → 「许愿」<c>Wish</c> / 「疏浚」<c>Dredge</c>
///         （<c>CardSelectCmd.FromCombatPile(PileType.Draw)</c> + <c>CardPileCmd.Add(..., PileType.Hand)</c>）</item>
/// </list>
/// </para>
///
/// <para>
/// <b>⚠️ 与「思索」一样，牌池必须是 <c>TokenCardPool</c>、稀有度 <c>CardRarity.Token</c>。</b>
/// 放进 <c>WizardCardPool</c> 会让它出现在常规卡池（奖励/商店/图鉴）里。
/// </para>
///
/// <para>
/// <b>顺序是"先消耗、再从抽牌堆取"</b>：与设计文档一致，
/// 而且这样"消耗 2 张"不会把刚取回的那张又消耗掉。
/// </para>
///
/// <para>
/// <b>手牌上限</b>：先检查空位，满了就跳过取牌（照抄 <c>Dredge</c> 的
/// <c>Math.Min</c> 思路）。直接 <c>Add</c> 超限会丢牌。
/// </para>
///
/// <para>
/// <b>⚠️ 本地化必须用占位符，不能写死数字。</b>
/// 本牌升级改的是 <c>CardsVar</c>（2 → 3），文案要写
/// <c>{Cards:diff()}</c>，选牌提示要写 <c>{Amount}</c>
/// （<c>{Amount}</c> 由 <c>CardSelectorPrefs</c> 构造时自动注入 =
/// 上面传给它的 <c>take</c>）。
/// 早前这里把「1 张 / a card」和「选择1张牌」**写死在文案里**，
/// 于是升级后数字不变 —— 看起来就像"升级效果没做"。
/// </para>
/// </summary>
[Pool(typeof(TokenCardPool))]
public class Epiphany : WizardCardModel
{
    private const int Cost = 0;

    /// <summary>选择几张手牌消耗（2026-11-18 加强：2 → 1）。</summary>
    private const int ExhaustCount = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 从抽牌堆取几张（2026-11-18 加强：2 张，升级 3 张）
        new CardsVar(2),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust,
    ];

    public Epiphany()
        : base(Cost, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 选择 1 张手牌消耗（照抄 BurningPact）。
        IEnumerable<CardModel> toExhaust = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, ExhaustCount),
            filter: null,
            source: this);

        foreach (CardModel card in toExhaust.ToList())
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // ② 从抽牌堆取牌加入手牌（照抄 Dredge / Wish）。
        int want = DynamicVars.Cards.IntValue;
        int freeSlots = CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count;
        int take = Math.Min(want, freeSlots);
        if (take <= 0)
        {
            return;
        }

        List<CardModel> taken = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Draw.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, take))).ToList();

        if (taken.Count > 0)
        {
            await CardPileCmd.Add(taken, PileType.Hand);
        }
    }

    /// <summary>升级：从抽牌堆取 2 → 3 张（消耗手牌数固定 1，不随升级变化）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}