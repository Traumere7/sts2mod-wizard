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
/// 净罪 / PurifySin - 1 费 - 技能 - 稀有（升级获得「固有」）
/// 消耗你所有的状态牌和诅咒牌。每消耗 1 张牌，获得 2 点[purple]法术增益[/purple]和 2 点[gold]敏捷[/gold]。消耗。
///
/// <para>
/// ⚠️ <b>升级口径改过三轮，当前是"1 费 + 升级加「固有」"</b>
/// （<c>docs/design.md:130</c> 现为 <c>1（0）</c> + <c>2点法术增益和2点敏捷</c>）：
/// <list type="bullet">
///   <item>第 1 版：<c>1（0）</c> + 固定 1 层 ⇒ 升级<b>降费</b>，数值固定 1。</item>
///   <item>第 2 版：<c>1-</c> + <c>1（2）</c> 层 ⇒ 费用不变，升级<b>加数值</b>。</item>
///   <item>第 3 版（2026-10-21）：<c>1（0）</c> + <b>固定 2 点</b> ⇒ 升级<b>降费</b>。</item>
///   <item><b>现版（2026-10-31 平衡性调整）</b>：<c>1</c> 费 + <b>固定 2 点</b> ⇒
///         升级<b>不再降费</b>（本就 0 费），改为 <c>AddKeyword(CardKeyword.Innate)</c>
///         获得「固有」。数值仍固定 2（<b>不随升级变化</b>）。</item>
/// </list>
/// ⇒ <c>OnUpgrade</c> 里是 <c>EnergyCost.UpgradeBy(-1)</c>，
/// <b>两个 <c>PowerVar</c> 都不调 <c>UpgradeValueBy</c></b>。
/// </para>
///
/// <para>
/// 📌 <b>为什么不干脆去掉 PowerVar、直接在文案里写 "2"：</b>
/// 保留 <c>PowerVar</c> 有两个好处 ——
/// ① 卡面数字会走 <c>PowerVar.UpdateCardPreview</c> 的
/// <c>Hook.ModifyPowerAmountGiven</c>，受"给予能力层数额外 +N"这类修正影响；
/// ② 与本 mod 其它卡（如「镇静」<see cref="CalmMind"/> 的
/// <c>PowerVar&lt;SpellPowerPower&gt;(1m)</c> 同样不随升级变化）保持一致。
/// </para>
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

    /// <summary>每消耗 1 张牌给予的法术增益层数（<b>固定值，不随升级变化</b>）。</summary>
    private const int SpellPowerPerCard = 2;

    /// <summary>每消耗 1 张牌给予的敏捷层数（<b>固定值，不随升级变化</b>）。</summary>
    private const int DexterityPerCard = 2;

    private const string SpellKey = nameof(SpellPowerPower);
    private const string DexKey = nameof(DexterityPower);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    /// <summary>
    /// 两个数值变量：每消耗 1 张牌给多少点法术增益 / 敏捷。
    /// 卡面文案用 <c>{SpellPowerPower:diff()}</c> / <c>{DexterityPower:diff()}</c>。
    /// ⚠️ <b>升级不改这两个变量</b>（升级只加「固有」），所以 <c>OnUpgrade</c> 里
    /// 没有 <c>UpgradeValueBy</c> —— 卡面在升级前后都显示 2。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SpellPowerPower>(SpellPowerPerCard),
        new PowerVar<DexterityPower>(DexterityPerCard),
    ];

    /// <summary>初始就有「消耗」（升级**降费** 1 → 0，数值不变）。</summary>
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

            // 每消耗 1 张，各给 DynamicVars 里的点数（固定 2，升级不变）。
            await PowerCmd.Apply<SpellPowerPower>(
                choiceContext, Owner.Creature, DynamicVars[SpellKey].BaseValue, Owner.Creature, this);

            await PowerCmd.Apply<DexterityPower>(
                choiceContext, Owner.Creature, DynamicVars[DexKey].BaseValue, Owner.Creature, this);
        }
    }

    /// <summary>
    /// 升级<b>降费</b> 1 → 0（<c>design.md</c> 写的是 <c>1（0）</c>）。
    /// <b>两个数值变量都不动</b> —— 升级前后都是「每张 2 点法术增益 + 2 点敏捷」。
    /// </summary>
    protected override void OnUpgrade()
    {
        // 2026-10-31 平衡性调整：升级不再降费（本就 1 费），改为获得「固有」。
        AddKeyword(CardKeyword.Innate);
    }
}
