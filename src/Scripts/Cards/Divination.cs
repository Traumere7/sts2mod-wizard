using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 占卜 - 1 费 - 技能 - 罕见
/// 抽 3(4) 张牌。选 1 张技能牌消耗，否则丢弃所有手牌。
///
/// <para>
/// <b>「否则」是一个二选一的强制分支</b>：
/// <list type="bullet">
///   <item>手牌里有技能牌 → 必须选 1 张消耗，手牌保留其余</item>
///   <item>手牌里没有技能牌 → 无法完成消耗 → <b>弃掉全部手牌</b></item>
/// </list>
///
/// <para>
/// <b>实现要点：不要用"取消选择"来表达"否则"。</b>
/// <c>CardSelectorPrefs</c> 的 <c>Cancelable</c> / <c>RequireManualConfirmation</c>
/// 涉及 UI 交互，玩家"取消"与"无牌可选"是两回事，语义会混。
/// 这里改成<b>先判断有没有技能牌</b>：
/// 有就强制选 1 张（<c>min = max = 1</c>，玩家必须选）；
/// 没有就直接弃光手牌，根本不给选择界面。这样两个分支互斥且无歧义。
/// </para>
///
/// <para>
/// <b>"弃掉手牌"用 <c>CardPileCmd</c> 的成批弃牌</b>，并<b>先快照再逐个移</b> ——
/// 边遍历边改牌堆会抛 <c>InvalidOperationException</c>（集合已修改），
/// 这个坑在「高温消毒」清负面时也踩过。
/// </para>
///
/// <para>
/// 结算顺序：<b>先抽牌、再判定</b>。因为抽到的技能牌也可以被选中消耗，
/// 所以判定必须发生在抽牌之后。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Divination : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3),
    ];

    public Divination()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 先抽牌（抽到的技能牌也能参与下面的选择）。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        var hand = PileType.Hand.GetPile(Owner);
        List<CardModel> skills = hand.Cards
            .Where(c => c.Type == CardType.Skill)
            .ToList();

        // ② 有技能牌 → 强制选 1 张消耗。
        if (skills.Count > 0)
        {
            CardModel? chosen = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
                filter: c => c.Type == CardType.Skill,
                source: this)).FirstOrDefault();

            if (chosen != null)
            {
                await CardCmd.Exhaust(choiceContext, chosen);
            }

            return;
        }

        // ③ 没有技能牌 → 弃掉所有手牌。先快照，再逐个弃。
        List<CardModel> toDiscard = hand.Cards.ToList();
        foreach (CardModel card in toDiscard)
        {
            await CardPileCmd.Add(card, PileType.Discard.GetPile(Owner));
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
