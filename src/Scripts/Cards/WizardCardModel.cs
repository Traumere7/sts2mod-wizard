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
}
