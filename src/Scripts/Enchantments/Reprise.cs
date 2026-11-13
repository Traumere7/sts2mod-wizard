using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Wizard.Scripts.Enchantments;

/// <summary>
/// 复奏（Reprise）——**本 mod 新增附魔**：这张牌获得[gold]重放[/gold] 2。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 直接照抄原版「涡旋」<c>Spiral</c> 的重放实现
/// ══════════════════════════════════════════════════════════════════
/// 引擎里 "重放" 只有一个入口：
/// <code>
/// public virtual int EnchantPlayCount(int originalPlayCount);   // EnchantmentModel
/// 引擎内部：int playCount = GetEnchantedReplayCount() + 1;     // CardPlay 注释：Replay 2 ⇒ playCount = 3
/// </code>
/// 所以这里只需要 <see cref="EnchantPlayCount"/> 加上 <c>Times</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>合法性沿用基类</b>（不复制 Spiral 的"仅基础打击/防御"限制）：
/// 设计口径是"选择你的 1 张手牌"，只排除状态 / 诅咒 / 任务牌 ——
/// 而 <c>EnchantmentModel.CanEnchant</c> 已经天然排除
/// <c>CardType.Status/Curse/Quest</c>、卡组里不可打出的牌、以及已有异种附魔的牌。
/// </para>
///
/// <para>
/// 文案（<c>localization/{lang}/enchantments.json</c>）：
/// <c>WIZARD-REPRISE.title</c> / <c>.description</c>。官方句式参照
/// <c>SPIRAL.description = "这张牌获得[gold]重放[/gold][blue]1[/blue]。"</c>。
/// 图标走 BaseLib 的 <see cref="CustomEnchantmentModel"/> 挂载（64×64，缺失会自动回退空图标，不报错）。
/// </para>
/// </summary>
public class Reprise : CustomEnchantmentModel
{
    /// <summary>动态变量名（卡面/悬停里的"重放 N"用）。</summary>
    private const string TimesKey = "Times";

    /// <summary>重放次数（设计固定 2，不可通过升级改变）。</summary>
    public const int ReplayCount = 2;

    /// <summary>官方附魔图标尺寸是 64×64（见 <c>images/enchantments/*.png</c>）。</summary>
    protected override string? CustomIconPath => "res://Wizard/images/enchantments/reprise.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar(TimesKey, ReplayCount),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, DynamicVars[TimesKey]),
    ];

    /// <summary>重放：在原有次数上再加 <c>Times</c>（2）。</summary>
    public override int EnchantPlayCount(int originalPlayCount)
        => originalPlayCount + DynamicVars[TimesKey].IntValue;
}
