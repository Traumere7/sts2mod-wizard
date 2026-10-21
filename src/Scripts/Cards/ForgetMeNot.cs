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
/// 勿忘我 / Forget-me-not - 1 费 - 技能 - 稀有
/// 从你的[gold]消耗牌堆[/gold]选最多 2(3) 张牌加入你的手牌。
/// 将 1 张「失忆」<see cref="Amnesia"/> 加入你的[gold]弃牌堆[/gold]。
///
/// <para>
/// <b>⚠️ 2026-10-03 口径改动：加"最多"二字，且<b>至少选 1 张</b>（1 ~ 2(3) 张）。</b>
/// 这与「真理派对」<see cref="TruthParty"/> 的"<b>可以一张都不选</b>（0 ~ n）"不同 ——
/// 两者都用区间构造 <c>CardSelectorPrefs(prompt, min, max)</c>，
/// 差别只在 <c>min</c>：本牌 <c>min = 1</c>，真理派对 <c>min = 0</c>。
/// </para>
///
/// <para>
/// <b>✅「从消耗牌堆取牌」原版与 BaseLib 都支持，已实现。</b>
/// 关键是引擎把六个牌堆统一抽象成 <c>PileType</c> 枚举
/// （<c>None/Draw/Hand/Discard/Exhaust/Play/Deck</c>，见 <c>PileType.cs</c>，
/// 其中 <c>Exhaust</c> 的注释就是 "Where cards are exhausted to"），
/// 所以"取牌"这件事与牌堆无关，只换一个 <c>PileType</c> 就行：
/// <code>
/// // 取消耗牌堆
/// await CardSelectCmd.FromCombatPile(
///     choiceContext,
///     PileType.Exhaust.GetPile(Owner),      // ← 只把 Draw 换成 Exhaust
///     Owner,
///     new CardSelectorPrefs(SelectionScreenPrompt, min, take));   // min=1（至少选1张）
///
/// // 放回手牌
/// await CardPileCmd.Add(taken, PileType.Hand);
/// </code>
/// 这一段与「顿悟」<see cref="Epiphany"/>「从抽牌堆取牌」是**同一套调用**，
/// 只是牌堆不同；<c>Epiphany</c> 的写法又照抄了原版
/// 「许愿」<c>Wish</c> / 「疏浚」<c>Dredge</c>。
/// </para>
///
/// <para>
/// <b>为什么用 <c>CardPileCmd.Add</c> 而不是 <c>AddGeneratedCardToCombat</c>：</b>
/// 后者是给"凭空生成的牌"用的（它会往战斗历史里写一条 generated 记录，
/// 见 <c>CardPileCmd.cs</c> 的注释）。消耗牌堆里的牌是**已存在的实例**，
/// 只是换了个牌堆，所以用普通 <c>Add</c>。
/// </para>
///
/// <para>
/// <b>「失忆」用 <c>AddGeneratedCardToCombat</c></b>：它确实是凭空造出来的新牌，
/// 照抄原版「黏糊糊」<c>GunkUp</c>（往弃牌堆塞一张 <c>Slimed</c>）：
/// <code>
/// CardModel card = CombatState.CreateCard&lt;Amnesia&gt;(Owner);
/// CardCmd.PreviewCardPileAdd(
///     await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, Owner));
/// </code>
/// <c>PreviewCardPileAdd</c> 让玩家看见那张牌飞进弃牌堆 —— 否则"凭空多了一张状态牌"
/// 玩家会莫名其妙，所以别省这一步。
/// </para>
///
/// <para>
/// <b>手牌上限</b>：与「顿悟」一样先算空位再取，<c>Add</c> 超限会丢牌。
/// 但**即使空位为 0，「失忆」照给** —— 设计写的是两件事并列，
/// 前者受手牌上限约束（物理限制），后者是固定惩罚，不该被手牌满而吞掉。
/// </para>
///
/// <para>
/// ⚠️ <b>本地化必须提供 <c>.selectionScreenPrompt</c></b>，否则
/// <c>CardModel.SelectionScreenPrompt</c> 抛
/// <c>InvalidOperationException: No selection screen prompt for ...</c>
/// （见 <c>CardModel.cs:129-139</c>）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ForgetMeNot : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>从消耗牌堆取回几张（升级 2 → 3）。</summary>
    private const int TakeCount = 2;

    /// <summary>往弃牌堆塞几张「失忆」（固定 1 张，升级不变）。</summary>
    private const int AmnesiaCount = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromCard<Amnesia>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(TakeCount),
    ];

    public ForgetMeNot()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 从消耗牌堆取牌加入手牌（结构与 Epiphany 的"从抽牌堆取牌"完全一致，
        //    只把 PileType.Draw 换成 PileType.Exhaust）。
        await TakeFromExhaust(choiceContext);

        // ② 往弃牌堆塞「失忆」（照抄 GunkUp 往弃牌堆塞 Slimed）。
        await AddAmnesiaToDiscard();
    }

    private async Task TakeFromExhaust(PlayerChoiceContext choiceContext)
    {
        CardPile exhaust = PileType.Exhaust.GetPile(Owner);
        if (exhaust.IsEmpty)
        {
            // 消耗牌堆空 → 没有可选的牌，跳过选牌（"至少选1张"无从谈起）。
            return;
        }

        int freeSlots = CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count;
        int take = Math.Min(DynamicVars.Cards.IntValue, freeSlots);
        if (take <= 0)
        {
            // 手牌已满 → 取不进来，跳过（「失忆」照给，见 OnPlay）。
            return;
        }

        // 选牌数不能超过消耗牌堆里现有的牌数，否则选牌界面会要求选不出来的张数。
        take = Math.Min(take, exhaust.Cards.Count);

        // ⚠️ 2026-10-03：口径是"选**最多** 2(3) 张"，但**至少要选 1 张**
        //    （区别于「真理派对」的"可以一张都不选"）。
        //    用区间构造 (min, max)：min=1 强制至少选 1，max=take 允许少选。
        //    区间构造会自动置 RequireManualConfirmation（见 CardSelectorPrefs），
        //    所以玩家必须手动确认，不会一选就走。
        //    若消耗牌堆只有 1 张，min 也随之降为 1（不会要求选不出来的张数）。
        int min = Math.Min(1, exhaust.Cards.Count);

        List<CardModel> taken = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            exhaust,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, min, take))).ToList();

        if (taken.Count > 0)
        {
            await CardPileCmd.Add(taken, PileType.Hand);
        }
    }

    private async Task AddAmnesiaToDiscard()
    {
        if (CombatState == null)
        {
            return;
        }

        for (int i = 0; i < AmnesiaCount; i++)
        {
            CardModel card = CombatState.CreateCard<Amnesia>(Owner);
            CardCmd.PreviewCardPileAdd(
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, Owner));
        }

        await Cmd.Wait(0.5f);
    }

    protected override void OnUpgrade()
    {
        // 从消耗牌堆取回 2 → 3 张（「失忆」数量不变）
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
