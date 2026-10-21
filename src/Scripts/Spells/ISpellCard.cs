namespace Wizard.Scripts.Spells;

/// <summary>
/// 标记接口：实现此接口的卡牌造成的伤害被视为**法术伤害**。
///
/// 伤害钩子（<c>ModifyDamageAdditive</c> / <c>ModifyDamageMultiplicative</c>）能拿到
/// <c>cardSource</c>，因此可以用这个接口判断"本次伤害是不是法术、是哪一系"。
/// 这样无需任何 Harmony 补丁即可让法术增益、寒冷等效果只作用于法术。
/// </summary>
public interface ISpellCard
{
    /// <summary>本法术牌的流派。</summary>
    SpellSchool School { get; }
}
