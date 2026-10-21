using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 让**法术伤害不吃力量**。
///
/// 为什么必须是补丁：游戏用同一个闸门 <c>ValuePropExtensions.IsPoweredAttack()</c>
/// （= <c>Move &amp;&amp; !Unpowered</c>）同时控制力量与易伤：
/// <list type="bullet">
///   <item><c>StrengthPower.ModifyDamageAdditive</c>（力量 +N）</item>
///   <item><c>VulnerablePower.ModifyDamageMultiplicative</c>（易伤 ×1.5）</item>
/// </list>
/// 而 <c>ValueProp</c> 只有 4 个位、没有中间态，因此"吃易伤但不吃力量"
/// **无法通过纯 hook 实现**。
///
/// 加算钩子没有否决机制（<c>Hook.cs:2527</c> 是纯累加），
/// 且 <c>ModifyDamageHookType</c> 是调用方参数、模型无法影响，
/// 所以只能在这里前置返回。
///
/// <b>影响范围被刻意限制到最小</b>：只否决"伤害来源是法术"的情形，
/// 其他一切（荆棘、吸血、毒刃、双倍伤害等约 60 个共用该闸门的效果）保持不变。
/// </summary>
[HarmonyPatch(typeof(StrengthPower), nameof(StrengthPower.ModifyDamageAdditive))]
internal static class SpellIgnoresStrengthPatch
{
    /// <summary>
    /// 前缀返回 false 即跳过原方法，直接把结果定为 0（不加算力量）。
    /// </summary>
    [HarmonyPrefix]
    private static bool Prefix(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        ref decimal __result)
    {
        if (SpellProps.IsSpell(props, cardSource))
        {
            __result = 0m;
            return false;
        }

        return true;
    }
}
