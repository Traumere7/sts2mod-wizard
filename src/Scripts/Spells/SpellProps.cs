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

        // ⚠️⚠️ 回退到"看卡牌接口"之前，必须先排除**非攻击性伤害**。⚠️⚠️
        //
        // 2026-11-04 实测 bug：一张法术牌被附魔「腐化」Corrupted 之后，
        // 该附魔的 OnPlay 会这样扣自己的血：
        //     CreatureCmd.Damage(ctx, Owner.Creature, 2m,
        //         ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
        //         base.Card, cardPlay);          // ← cardSource 就是那张法术牌
        // 它的 props 里**没有**我们的法术标记位，但 cardSource 是法术牌，
        // 于是这里的回退把"附魔自伤 2 点"误判成法术伤害 ⇒
        // 「法术增益」SpellPowerPower 的 ModifyDamageAdditive 再加上 Amount
        // ⇒ 玩家扣 2 + 法术增益 层数 的血。
        //
        // 修法：这类伤害一律视为"非法术"——
        //   · Unpowered  ：能力/遗物/附魔/药水等非卡牌攻击来源（Corrupted 就带这个）
        //   · Unblockable：直接扣血（中毒等），不可能是法术攻击
        //   · 不含 Move  ：不是攻击
        // 我们自己的法术伤害一律显式带 SpellProps 标记位，前面三个 HasFlag 就返回了，
        // 根本走不到这里，所以这条限制不会影响任何正常法术。
        if (props.HasFlag(ValueProp.Unpowered)
            || props.HasFlag(ValueProp.Unblockable)
            || !props.HasFlag(ValueProp.Move))
        {
            return SpellSchool.None;
        }

        return cardSource.SchoolOf();
    }

    /// <summary>该伤害是否为法术伤害。</summary>
    public static bool IsSpell(ValueProp props, CardModel? cardSource)
        => Resolve(props, cardSource) != SpellSchool.None;
}
