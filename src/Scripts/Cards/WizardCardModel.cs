using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 巫师所有卡牌的公共基类。
/// 统一处理卡图路径，避免每张卡都重复写一遍。
/// </summary>
public abstract class WizardCardModel : CustomCardModel
{
    protected WizardCardModel(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    /// <summary>
    /// 卡图路径。约定为 <c>res://Wizard/images/cards/{类名}.png</c>。
    /// 官方普通卡尺寸 250x190，先古卡 250x351；任意尺寸都可，不需要裁剪。
    /// </summary>
    public override string? CustomPortraitPath => $"res://Wizard/images/cards/{GetType().Name}.png";

    /// <summary>
    /// 「选择消耗的卡牌」选牌界面打开时，这张牌是否**金边亮起** ——
    /// 表示"被消耗能带来额外收益"（仿原版「奇巧」<c>Sly</c> 在"选择丢弃"时亮起的体验）。
    ///
    /// <para>
    /// 由 <see cref="Wizard.Scripts.Patches.ExhaustSelectionGlowPatch"/> 统一接入：
    /// 当 <c>CardSelectCmd.FromHand</c> 的 prompt 是 <c>TO_EXHAUST</c> 时，
    /// 会给该次选牌注入 <c>ShouldGlowGold</c> 谓词。
    /// </para>
    ///
    /// <para>默认 <c>false</c>；「尾注」「心流」这类"被消耗时触发"的牌覆盖成 <c>true</c>。</para>
    /// </summary>
    public virtual bool GlowsOnExhaustSelection => false;
}
