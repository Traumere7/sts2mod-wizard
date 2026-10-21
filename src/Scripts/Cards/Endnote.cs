using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 尾注 / Endnote - 1 费 - 技能 - 罕见
/// 抽 1 张牌。这张牌<b>被消耗时</b>，抽 2(3) 张牌。
///
/// <para>
/// <b>「被消耗时触发」照抄原版「战鼓」<c>DrumOfBattle</c>：</b>
/// 重写 <c>AbstractModel.AfterCardExhausted</c>
/// （<c>AbstractModel.cs:447</c>），并<b>判断被消耗的是不是自己</b>：
/// <code>
/// public override async Task AfterCardExhausted(
///     PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
/// {
///     if (card == this &amp;&amp; CombatState != null) { ... }
/// }
/// </code>
/// <c>DrumOfBattle</c> 还额外用 <c>GeneratePlayCount</c> 处理"这张牌被打出多次"
/// 的情况；本牌设计上没有"重复打出"的机制，所以不需要。
/// </para>
///
/// <para>
/// <b>两个数值是分开的变量</b>：
/// <list type="bullet">
///   <item>打出时抽 <b>1</b> 张 —— 固定值，升级不变，所以是常量不是变量</item>
///   <item>被消耗时抽 <b>2(3)</b> 张 —— 升级 +1，用 <c>CardsVar(2)</c></item>
/// </list>
/// ⚠️ 别把两者合成一个 <c>CardsVar</c>：它们升级行为不同
/// （一个不变、一个 +1），合并了就没法只升其中一个。
/// </para>
///
/// <para>
/// <b>这个效果怎么触发</b>：本牌自己不带「消耗」，所以正常打出时不会触发。
/// 它要靠<b>别的</b>手段被消耗 —— 例如「思索」「顿悟」的"选择手牌消耗"、
/// 「高温消毒」、或任何带「消耗」的联动。也就是说它是一张
/// <b>需要主动喂给消耗体系的牌</b>，这是设计意图而非缺陷。
/// </para>
///
/// <para>
/// <c>causedByEthereal</c>（因「虚无」在回合结束时被消耗）也会走到这个钩子，
/// 本牌不带「虚无」，所以无需区分；保持与 <c>DrumOfBattle</c> 相同的处理方式
/// （不判断该标志，一律触发）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Endnote : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>打出时抽的牌数（固定，不随升级变化）。</summary>
    private const decimal DrawOnPlay = 1m;
    /// <summary>被消耗时抽的牌数基数（升级 +1 → 3）。</summary>
    private const int DrawOnExhaust = 2;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 被消耗时抽几张（升级 2 → 3）
        new CardsVar(DrawOnExhaust),
    ];

    public Endnote()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DrawOnPlay, Owner);
    }

    /// <summary>
    /// 这张牌**被消耗时**抽牌（照抄「战鼓」<c>DrumOfBattle</c>）。
    ///
    /// <para>
    /// ⚠️ 必须判 <c>card == this</c>：这个钩子对**任何**卡被消耗都会触发
    /// （打在每一张 <c>CardModel</c> 上），不判就会"别人被消耗、我也抽牌"。
    /// </para>
    /// </summary>
    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card != this || CombatState == null)
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);    }

    protected override void OnUpgrade()
    {
        // 被消耗时抽 2 → 3（打出时抽的 1 张不变）
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
