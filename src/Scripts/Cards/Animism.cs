using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Wizard.Scripts.Pools;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 泛灵论 / Animism - 1（0）费 - 技能 - 稀有
/// 抽 1 张牌。变化你的所有手牌，这些牌获得虚无。
///
/// <para>
/// ⚠️ <b>数值已改（2026-10-31 平衡性调整，design.md 同步）</b>：
/// <b>旧</b>：0 费，升级<b>加「保留」</b>。
/// <b>新</b>：<b>1（0）费</b>，<b>没有「保留」</b>了 —— 升级只降费。
/// ⇒ <c>CanonicalKeywords</c> 保持空，<c>OnUpgrade()</c> 里
/// <c>EnergyCost.UpgradeBy(-1)</c>，**不再** <c>AddKeyword(Retain)</c>。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「变化你的所有手牌，这些牌获得虚无」
/// ══════════════════════════════════════════════════════════════════
/// 两步，顺序不能反：
/// <list type="number">
///   <item><b>变化</b>：逐张 <c>CardCmd.TransformToRandom</c>（同「熵」<c>EntropyPower</c>），
///         拿返回的 <c>CardPileAddResult.cardAdded</c> 作为<b>变化后的新牌</b>；</item>
///   <item><b>给新牌上虚无</b>：<c>CardCmd.ApplyKeyword(newCard, CardKeyword.Ethereal)</c>
///         （同「雕琢打击」<c>SculptingStrike</c>）。</item>
/// </list>
/// 必须先变化再上词条 —— 给旧牌上词条没意义，旧牌会被替换掉。
/// </para>
///
/// <para>
/// ⚠️ <b>必须先 <c>ToList()</c> 快照手牌</b>：变化会把手牌里的牌替换掉，
/// 直接 <c>foreach</c> 活动集合会在枚举途中被改。
/// </para>
///
/// <para>
/// ⚠️ <b>必须过滤 <c>IsTransformable</c></b>：<c>CardCmd.Transform</c>
/// （<c>CardCmd.cs:374-409</c>）在 <c>!original.IsTransformable</c> 时<b>抛异常</b>
/// （"Can't transform … because it's un-transformable."）。
/// 本牌是"变化<b>所有</b>手牌"，不筛就会在有不可变化牌时整张卡报错。
/// </para>
///
/// <para>
/// <b>抽牌用 <c>CardPileCmd.Draw</c></b>（照抄「占卜」<c>Divination</c>），
/// 且<b>先抽再变化</b> —— 文案顺序就是"抽1张牌。变化你的所有手牌"，
/// 抽到的那张也该被变化到。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Animism : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>抽几张（design.md 未标注升级值，升级只降费）。</summary>
    private const int DrawCount = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Transform),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
    ];

    public Animism()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 抽 1 张。
        await CardPileCmd.Draw(choiceContext, DrawCount, Owner);

        // ② 变化所有手牌，并给变化后的新牌上「虚无」。
        //
        // ⚠️ 先快照：变化会替换手牌里的元素。
        List<CardModel> hand = PileType.Hand.GetPile(Owner).Cards.ToList();

        foreach (CardModel original in hand)
        {
            // 不可变化的牌跳过 —— 否则 CardCmd.Transform 会抛异常。
            if (!original.IsTransformable)
            {
                continue;
            }

            CardPileAddResult result = await CardCmd.TransformToRandom(
                original, Owner.RunState.Rng.CombatCardSelection);

            // 给"变化后"的新牌上「虚无」。
            if (result.success && result.cardAdded != null)
            {
                CardCmd.ApplyKeyword(result.cardAdded, CardKeyword.Ethereal);
            }
        }
    }

    /// <summary>
    /// 升级只降费 1 → 0（2026-10-31 起不再加「保留」词条）。
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
