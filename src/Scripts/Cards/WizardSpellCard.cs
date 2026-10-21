using MegaCrit.Sts2.Core.Entities.Cards;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 巫师**法术牌**的基类。
/// 继承它即自动获得 <see cref="ISpellCard"/> 标记，
/// 从而受到法术增益、寒冷、超导等效果的影响（且不触发非法术的加成）。
/// </summary>
public abstract class WizardSpellCard : WizardCardModel, ISpellCard
{
    protected WizardSpellCard(
        int baseCost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    /// <summary>法术流派，由子类指定。</summary>
    public abstract SpellSchool School { get; }
}
