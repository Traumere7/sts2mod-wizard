using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;

namespace Wizard.Scripts.Enchantments;

/// <summary>
/// 不朽 / Immortal —— **本 mod 新增附魔**（未知海洋事件专属）。
///
/// <para>
/// 效果（design.md）：
/// <list type="bullet">
///   <item>这张牌<b>失去「消耗」和「即毁」词条</b>；</item>
///   <item>如果这张牌是<b>能力牌</b>，则打出后<b>不会消失</b>，而是进入<b>弃牌堆</b>。</item>
/// </list>
/// 合法目标（<see cref="CanEnchant"/>）：带「消耗」词条、带「即毁」词条、或者<b>是能力牌</b>。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 去词条：照抄原版附魔「灵魂之力」<c>SoulsPower</c>
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// public override bool CanEnchant(CardModel card)   // 只对本地带 Exhaust 的牌生效
///     => base.CanEnchant(card) &amp;&amp; card.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Exhaust);
///
/// protected override void OnEnchant() => CardCmd.RemoveKeyword(Card, CardKeyword.Exhaust);
/// </code>
/// <c>OnEnchant</c> 的时机很重要：<c>CardCmd.Enchant</c> 会调 <c>ModifyCard()</c> → <c>OnEnchant()</c>；
/// <b>读档</b>时 <c>CardModel.FromSerializable</c> 也会调 <c>Enchantment.ModifyCard()</c>
/// （<c>CardModel.cs</c> 的 FromSerializable 分支），所以去掉的词条<b>能跨存档保留</b>。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 能力牌"不消失"：用引擎钩子，不用 Harmony
/// ══════════════════════════════════════════════════════════════════
/// 引擎给能力牌的结算落点是 <c>PileType.None</c>（打出来就消失，只留下能力本身）：
/// <code>
/// // CardModel.GetResultLocationForCardPlay()
/// if (IsDupe || Type == CardType.Power) return new CardLocation(Owner, PileType.None, Bottom);
/// </code>
/// 官方留的扩展点是 <c>AbstractModel.ModifyCardPlayResultLocation</c>
/// （<c>Hook.ModifyCardPlayResultLocation</c> 汇总；<c>CardModel.cs:2044</c> 调用），
/// 而 <c>EnchantmentModel.ShouldReceiveCombatHooks</c> 会跟着自己那张牌走
/// ⇒ <b>附魔自己就能改落点</b>。把 <c>None</c> 改成 <c>Discard</c> 即可。
/// </para>
///
/// <para>
/// ⚠️ 注意 <c>location.pileType</c> 是 <c>CardLocation</c> 这个 <b>record struct</b> 的字段，
/// 直接改字段再返回（原版 <c>CorruptionPower</c> 就是这么写的）。
/// </para>
/// </summary>
public class Immortal : CustomEnchantmentModel
{
    protected override string? CustomIconPath => "res://Wizard/images/enchantments/immortal.png";

    /// <summary>描述里提到「消耗」「即毁」两个词条，按规矩都要挂解释。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(WizardKeywords.Obliterate),
    ];

    /// <summary>
    /// 合法目标：带「消耗」、带「即毁」、或本身是能力牌。
    ///
    /// <para>
    /// ⚠️ 用 <c>GetKeywordsWithSources(KeywordSources.Local)</c> 而不是 <c>card.Keywords</c>：
    /// 前者只算<b>这张牌自己带的</b>词条（与 <c>SoulsPower</c> 一致），
    /// 不会被外部临时加上的词条误判。
    /// 基类 <c>CanEnchant</c> 已经排除了 状态 / 诅咒 / 任务牌、卡组里不可打出的牌、已有异种附魔的牌。
    /// </para>
    /// </summary>
    public override bool CanEnchant(CardModel card)
    {
        if (!base.CanEnchant(card))
        {
            return false;
        }

        IReadOnlySet<CardKeyword> local = card.GetKeywordsWithSources(KeywordSources.Local);

        return local.Contains(CardKeyword.Exhaust)
            || local.Contains(WizardKeywords.Obliterate)
            || card.Type == CardType.Power;
    }

    /// <summary>附上时立刻去掉这两个词条（读档时引擎会重跑本方法，见类注释）。</summary>
    protected override void OnEnchant()
    {
        CardCmd.RemoveKeyword(Card, CardKeyword.Exhaust);
        CardCmd.RemoveKeyword(Card, WizardKeywords.Obliterate);
    }

    /// <summary>
    /// 能力牌打出后：本来是 <c>PileType.None</c>（消失），改成 <c>PileType.Discard</c>（进弃牌堆）。
    /// </summary>
    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        // 只管自己附魔的那张牌。
        if (Card == null || card != Card)
        {
            return location;
        }

        if (card.Type != CardType.Power)
        {
            return location;
        }

        if (location.pileType == PileType.None)
        {
            location.pileType = PileType.Discard;
        }

        return location;
    }
}
