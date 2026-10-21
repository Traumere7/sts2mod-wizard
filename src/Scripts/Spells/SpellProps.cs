using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 法术伤害在 <see cref="ValueProp"/> 上的自定义标记位。
///
/// 为什么用 ValueProp 而不是只靠 <c>cardSource is ISpellCard</c>：
/// 伤害钩子里的 <c>cardSource</c> 在**非卡牌来源**时会是 null
/// （能力触发、遗物触发、回合末结算等），届时靠接口判断会静默失效。
/// 而 <c>ValueProp</c> 是随伤害本身传递的，任何来源都可靠。
///
/// <b>位值是运行时分配的</b>：BaseLib 的 <c>GenEnumValues</c> 会扫描 mod 全部类型的
/// <c>[CustomEnum]</c> 字段，对 flags 枚举发出"下一个 2 的幂"
/// （见 <c>BaseLib.Patches.Content.CustomEnums.KeyGenerator</c>）。
/// <see cref="ValueProp"/> 现有最大位是 0x10，所以首个字段会拿到 0x20。
/// <b>切勿硬编码位值</b>，始终通过这些字段引用。
///
/// 注意：字段按**名称字母序**分配（<c>GenEnumValues</c> 会排序），
/// 所以重命名字段会改变位值。ValueProp 不进存档，因此不影响存档兼容性。
/// </summary>
public static class SpellProps
{
    [CustomEnum] public static ValueProp Fire;
    [CustomEnum] public static ValueProp Lightning;
    [CustomEnum] public static ValueProp Ice;

    /// <summary>所有法术标记位的合集。</summary>
    public static ValueProp All => Fire | Lightning | Ice;

    /// <summary>取某个法术流派对应的标记位。</summary>
    public static ValueProp For(SpellSchool school) => school switch
    {
        SpellSchool.Fire => Fire,
        SpellSchool.Lightning => Lightning,
        SpellSchool.Ice => Ice,
        _ => default,
    };

    /// <summary>
    /// 判定一次伤害的法术流派。
    /// 优先看 <paramref name="props"/>（对任何来源都可靠），
    /// 其次回退到卡牌接口（兼容尚未迁移、只实现了 <see cref="ISpellCard"/> 的卡）。
    /// </summary>
    public static SpellSchool Resolve(ValueProp props, CardModel? cardSource)
    {
        if (props.HasFlag(Fire)) return SpellSchool.Fire;
        if (props.HasFlag(Lightning)) return SpellSchool.Lightning;
        if (props.HasFlag(Ice)) return SpellSchool.Ice;

        return cardSource.SchoolOf();
    }

    /// <summary>该伤害是否为法术伤害。</summary>
    public static bool IsSpell(ValueProp props, CardModel? cardSource)
        => Resolve(props, cardSource) != SpellSchool.None;
}
