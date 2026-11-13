using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Wizard.Scripts;

/// <summary>
/// 本 mod 新增的**引擎级卡牌词条**（由 BaseLib 注册进 <see cref="CardKeyword"/>）。
///
/// <para>
/// ⚠️ 注册机制（BaseLib 3.4.8，已核对其反编译源码）：
/// <list type="bullet">
///   <item><c>[CustomEnum(null)]</c> 让 BaseLib 在 <c>ModelDb.Init</c> 之前为本字段分配一个枚举位
///         （见 <c>GenEnumValues</c> → <c>CustomEnums.GenerateKey</c>）；</item>
///   <item><c>[KeywordProperties(AutoKeywordPosition.After)]</c> 让词条**自动出现在卡面词条行**的
///         描述之后（与「消耗」同位置），并登记进 <c>CustomKeywords.KeywordIDs</c>；</item>
///   <item>本地化键 = <c>GetPrefix() + 字段名.ToUpperInvariant()</c> = <b><c>WIZARD-OBLITERATE</c></b>，
///         所以文案要写在 <c>card_keywords.json</c> 的
///         <c>WIZARD-OBLITERATE.title</c> / <c>WIZARD-OBLITERATE.description</c>（**不是** <c>OBLITERATE.*</c>）。</item>
/// </list>
/// BaseLib 自己也内置了一个同款词条 <c>BaseLibKeywords.Purge</c>，可作参照。
/// </para>
///
/// <para>
/// ⚠️ <b>词条只是"渲染 + 语义标记"，引擎不会自动执行它</b>（引擎只内置 Exhaust / Ethereal / Innate …）。
/// 行为由 <see cref="Wizard.Scripts.Utils.CardObliteration"/> 提供，卡牌在**成功结算之后**主动调用。
/// </para>
/// </summary>
public static class WizardKeywords
{
    /// <summary>
    /// 即毁：**打出后，将这张牌从你的卡组中移除**（不是「消耗」——消耗只是本场战斗离场）。
    /// 移除动作见 <see cref="Wizard.Scripts.Utils.CardObliteration.Obliterate"/>。
    /// </summary>
    [CustomEnum(null)]
    [KeywordProperties(AutoKeywordPosition.After)]
    public static CardKeyword Obliterate;
}
