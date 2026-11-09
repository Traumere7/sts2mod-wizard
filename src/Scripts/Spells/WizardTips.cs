using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 本 mod 自定义的悬停提示（不是原版的关键词/能力，所以自己拼 <c>HoverTip</c>）。
///
/// <para>
/// 文案放在 <c>localization/{lang}/cards.json</c> 里、表名 <c>cards</c>
/// （BaseLib 会把 mod 的 cards.json 合并进原版 cards 表）：
/// <c>WIZARD-AETHER.title</c> / <c>WIZARD-AETHER.description</c>。
/// </para>
/// </summary>
public static class WizardTips
{
    /// <summary>
    /// 「以太」：设计文档「特殊伤害类型」——
    /// 以太伤害**同时被视作火焰、雷电、寒冰**三种类型。
    ///
    /// <para>
    /// 挂在「第四元素」「以太箭」「魔力爆发」三张以太相关牌上，
    /// 让玩家能直接从卡面悬停知道这条规则，而不必去翻简介。
    /// </para>
    /// </summary>
    public static IHoverTip Aether => new HoverTip(
        new LocString("cards", "WIZARD-AETHER.title"),
        new LocString("cards", "WIZARD-AETHER.description"));

    /// <summary>
    /// 「随机负面效果」（「孔雀之尾」用）：
    /// 候选为燃烧、麻痹、电流外溢、寒冷、冻结、易伤、虚弱，优先给予敌人未拥有的。
    /// 文案见 <c>cards.json</c> 的 <c>WIZARD-RANDOM_DEBUFF.*</c>。
    /// </summary>
    public static IHoverTip RandomDebuff => new HoverTip(
        new LocString("cards", "WIZARD-RANDOM_DEBUFF.title"),
        new LocString("cards", "WIZARD-RANDOM_DEBUFF.description"));
}
