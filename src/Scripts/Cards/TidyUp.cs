using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 整理 / TidyUp - 1 费 - 技能 - 普通
/// 将弃牌堆中的 1 张牌放入你的手牌。
/// 将你手牌中的 1 张牌放到抽牌堆顶部。下回合获得 1(2) 费。
///
/// <para>
/// <b>三段效果各照抄一张原版牌：</b>
/// <list type="number">
///   <item><b>弃牌堆 → 手牌</b>：照抄「全息影像」<c>Hologram</c> ——
///         <c>CardSelectCmd.FromCombatPile(PileType.Discard.GetPile(Owner), …)</c>
///         + <c>CardPileCmd.Add(card, PileType.Hand)</c>。</item>
///   <item><b>手牌 → 抽牌堆顶</b>：照抄「微光」<c>Glimmer</c> ——
///         注意<b>必须带 <c>CardPilePosition.Top</c></b>，否则会插到底部。</item>
///   <item><b>下回合加费</b>：照抄「侧步」<c>Sidestep</c> ——
///         <c>PowerCmd.Apply&lt;EnergyNextTurnPower&gt;(…, Energy.IntValue, …)</c>。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>结算顺序：先取回、再放回、最后给费。</b>
/// 先取回保证了"放回抽牌堆顶"选的是**取回之后**的手牌
/// （若先放回，刚取回的那张可能又立刻被放回去，等于白拿）。
/// </para>
///
/// <para>
/// <b>两个"1 张"都是固定的</b>（设计文档写的是"1张"，升级只加费用 1 → 2），
/// 所以它们是常量、不进 <c>CanonicalVars</c>；
/// 只有下回合的费用用 <c>EnergyVar</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>本地化必须提供 <c>.selectionScreenPrompt</c></b>，否则
/// <c>CardModel.SelectionScreenPrompt</c> 抛
/// <c>InvalidOperationException: No selection screen prompt for …</c>。
/// </para>
///
/// <para>
/// <b>两个牌堆都要防空</b>：弃牌堆为空时跳过取回、手牌为空时跳过放回 ——
/// 否则选牌界面会要求选不出来的张数。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class TidyUp : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>基础下回合费用（升级 +1 → 2）。</summary>
    private const int BaseEnergy = 1;

    private const string EnergyKey = "Energy";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(BaseEnergy),
    ];

    public TidyUp()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 弃牌堆 → 手牌（照抄 Hologram）。
        await TakeFromDiscard(choiceContext);

        // ② 手牌 → 抽牌堆顶（照抄 Glimmer）。
        await PutBackOnTop(choiceContext);

        // ③ 下回合加费（照抄 Sidestep）。
        await PowerCmd.Apply<EnergyNextTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[EnergyKey].IntValue,
            Owner.Creature,
            this);
    }

    private async Task TakeFromDiscard(PlayerChoiceContext choiceContext)
    {
        CardPile discard = PileType.Discard.GetPile(Owner);
        if (discard.IsEmpty)
        {
            return;
        }

        // 手牌已满就没法再拿（Add 超限会丢牌）。
        int freeSlots = CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count;
        if (freeSlots <= 0)
        {
            return;
        }

        CardModel? taken = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            discard,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();

        if (taken != null)
        {
            await CardPileCmd.Add(taken, PileType.Hand);
        }
    }

    private async Task PutBackOnTop(PlayerChoiceContext choiceContext)
    {
        CardPile hand = PileType.Hand.GetPile(Owner);
        if (hand.IsEmpty)
        {
            return;
        }

        // ⚠️ 必须 CardPilePosition.Top —— 抽牌堆的"顶部"才是下一张要抽的牌。
        CardModel? put = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            filter: null,
            source: this)).FirstOrDefault();

        if (put != null)
        {
            await CardPileCmd.Add(put, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade()
    {
        // 下回合费用 1 → 2（两个"1 张"不变）
        DynamicVars[EnergyKey].UpgradeValueBy(1m);
    }
}
