using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 判定某个伤害来源是否属于法术、以及属于哪一系。
/// </summary>
public static class SpellUtil
{
    /// <summary>该卡牌是否为法术牌。</summary>
    public static bool IsSpell(this CardModel? card) => card is ISpellCard;

    /// <summary>
    /// 该卡牌的法术流派；非法术牌返回 <see cref="SpellSchool.None"/>。
    /// </summary>
    public static SpellSchool SchoolOf(this CardModel? card)
        => card is ISpellCard spell ? spell.School : SpellSchool.None;

    /// <summary>是否为火系法术。</summary>
    public static bool IsFire(this CardModel? card) => card.SchoolOf() == SpellSchool.Fire;

    /// <summary>是否为雷系法术。</summary>
    public static bool IsLightning(this CardModel? card) => card.SchoolOf() == SpellSchool.Lightning;

    /// <summary>是否为冰系法术。</summary>
    public static bool IsIce(this CardModel? card) => card.SchoolOf() == SpellSchool.Ice;
}
