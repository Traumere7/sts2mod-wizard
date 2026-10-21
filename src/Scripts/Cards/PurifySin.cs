using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 净罪 / PurifySin - 1(0) 费 - 技能 - 稀有
/// 消耗你所有的状态牌和诅咒牌。每消耗 1 张牌，获得 1 层[purple]法术增益[/purple]和 1 层[gold]敏捷[/gold]。消耗。
///
/// <para>
/// <b>「消耗所有状态牌和诅咒牌」照抄原版「散射炮」<c>FlakCannon</c>：</b>
/// 它用 <c>owner.PlayerCombatState.AllCards</c> 拿到**全部牌堆**的牌，再按
/// <c>Type == CardType.Status</c> 过滤（并排除已在消耗堆的）：
/// <code>
/// owner.PlayerCombatState.AllCards
///     .Where(c =&gt; c.Type == CardType.Status &amp;&amp; c.Pile.Type != PileType.Exhaust)
/// </code>
/// <c>PlayerCombatState.AllPiles</c> 是
/// <c>{ Hand, DrawPile, DiscardPile, ExhaustPile, PlayPile }</c>
/// —— 所以 <c>AllCards</c> 天然<b>同时覆盖手牌、抽牌堆、弃牌堆</b>，
/// 这正是"包括手牌、抽牌堆和弃牌堆"的实现方式，不需要逐个堆去遍历。
/// </para>
///
/// <para>
/// ⚠️ <b>必须排除已在消耗堆的牌</b>（照抄 <c>FlakCannon</c> 的
/// <c>c.Pile.Type != PileType.Exhaust</c>）：那些牌早就是"已消耗"状态，
/// 再对它们调一次 <c>CardCmd.Exhaust</c> 既无意义、又可能重复计数。
/// </para>
///
/// <para>
/// ⚠️ <b>本牌自己现在位于 <c>PileType.Play</c></b>，<c>AllCards</c> 会把它也算进来 ——
/// 但它的 <c>Type</c> 是 <c>Skill</c>，不满足状态/诅咒过滤，所以天然被排除。
/// </para>
///
/// <para>
/// <b>「每消耗 1 张牌」的计数方式：边消耗边计数。</b>
/// 先 <c>ToList()</c> 快照（<c>CardCmd.Exhaust</c> 会改动牌堆，边遍历边改会抛异常，
/// 与「高温消毒」同一个坑），然后逐张消耗、每张各给 1 层法术增益与 1 层敏捷。
/// </para>
///
/// <para>
/// <b>法术增益加给自己</b>（<c>Owner.Creature</c>）——
/// 与本 mod「镇静」<see cref="CalmMind"/>「焚尽」<see cref="BurnToAshes"/> 同一施加对象。
/// </para>
///
/// <para>
/// <b>本牌自带「消耗」</b>，所以打完就进消耗堆（不会再被后续的「净罪」重复利用）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class PurifySin : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    /// <summary>
    /// 无卡面数值变量 —— 给多少完全取决于场上有几张状态/诅咒牌，
    /// 卡面文案也就没有占位符（与「魔力凝聚」同一情形）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>初始就有「消耗」（升级只降费）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public PurifySin()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ⚠️ 先快照再消耗：CardCmd.Exhaust 会改动牌堆，边遍历边改会抛异常。
        //    AllCards 覆盖 手牌 / 抽牌堆 / 弃牌堆 / 消耗堆 / Play 堆。
        List<CardModel> targets = (Owner.PlayerCombatState?.AllCards
                                   ?? Enumerable.Empty<CardModel>())
            .Where(c => (c.Type == CardType.Status || c.Type == CardType.Curse)
                        && c.Pile?.Type != PileType.Exhaust)
            .ToList();

        if (targets.Count == 0)
        {
            return;
        }

        foreach (CardModel card in targets)
        {
            await CardCmd.Exhaust(choiceContext, card);

            // 每消耗 1 张，各给 1 层。
            await PowerCmd.Apply<SpellPowerPower>(
                choiceContext, Owner.Creature, 1m, Owner.Creature, this);

            await PowerCmd.Apply<DexterityPower>(
                choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    /// <summary>升级 1 费 → 0 费（设计写的是「1（0）」）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
