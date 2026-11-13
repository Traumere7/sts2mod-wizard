using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Utils;

/// <summary>
/// 「即毁」<see cref="WizardKeywords.Obliterate"/> 的**行为实现**（引擎只管渲染词条，不管效果）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么改 <c>DeckVersion</c> 而不是打出的那张牌
/// ══════════════════════════════════════════════════════════════════
/// 战斗里打出的牌是**卡组牌的克隆**（<c>Player.PopulateCombatState</c> →
/// <c>card.DeckVersion = 卡组里那张</c>）。
/// <c>CardPileCmd.RemoveFromDeck</c> 明确要求
/// <c>card.Pile.Type == PileType.Deck</c>，否则抛
/// <c>InvalidOperationException("You cannot remove a card that is not in the deck.")</c> ——
/// 所以必须把 <c>DeckVersion</c> 传进去。
/// </para>
///
/// <para>
/// <b>战斗中也能用</b>：原版「顺手牵羊」<c>SwipePower</c> 与 <c>DeprecatedCard</c>
/// 就是战斗中调用 <c>RemoveFromDeck</c> 的先例（都用 <c>showPreview: false</c>）。
/// </para>
///
/// <para>
/// <b>临时牌（生成牌）无法被移除，也不需要移除</b>：它们 <c>DeckVersion == null</c>，
/// 本场战斗结束就消失，直接跳过。
/// </para>
/// </summary>
public static class CardObliteration
{
    /// <summary>该牌是否带「即毁」词条。</summary>
    public static bool HasObliterate(CardModel card) => card.Keywords.Contains(WizardKeywords.Obliterate);

    /// <summary>
    /// 对**刚打出并已正确结算**的牌执行「即毁」：把它的卡组原型永久移除。
    /// 卡组里没有它（生成牌 / 已在卡组外）时什么都不做。
    /// </summary>
    public static async Task Obliterate(CardModel playedCard)
    {
        CardModel? deckCard = playedCard.DeckVersion;
        if (deckCard == null)
        {
            // 生成牌：本场战斗结束就消失，没有"卡组中的原型"可移除。
            return;
        }

        if (deckCard.Pile?.Type != PileType.Deck)
        {
            // 极端情况（例如卡组牌已被别的效果移走）：不报错，静默跳过。
            Entry.Logger.Warn(
                $"即毁：{deckCard.Id} 的卡组原型不在卡组牌堆（Pile={deckCard.Pile?.Type}），跳过移除。");
            return;
        }

        await CardPileCmd.RemoveFromDeck(deckCard, showPreview: false);
    }
}
