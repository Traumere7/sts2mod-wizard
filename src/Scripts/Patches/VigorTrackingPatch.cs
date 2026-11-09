using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 「活力」<see cref="VigorPower"/> 的**跟踪状态复用**修正 + 临时探针。
///
/// ══════════════════════════════════════════════════════════════════
/// 问题（2026-11-09 用户复现「法术伤害牌不享受活力」）
/// ══════════════════════════════════════════════════════════════════
/// 原版 <c>VigorPower</c> 用一份私有状态 `Data { AttackCommand? commandToModify; int
/// amountWhenAttackStarted; }` 只"认一次攻击"：
///
/// <list type="number">
///   <item><c>BeforeAttack(command)</c>：<b>若 <c>commandToModify != null</c> 直接 return</b>
///         ⇒ 这次攻击**不会被跟踪**；</item>
///   <item><c>ModifyDamageAdditive(...)</c>：<b>若 <c>commandToModify != null</c> 且
///         本次 <c>cardSource</c> 不是那条命令的来源牌 ⇒ 返回 0</b>；</item>
///   <item><c>AfterAttack(command)</c>：只做 <c>ModifyAmount(-amountWhenAttackStarted)</c>，
///         <b>从不把 <c>commandToModify</c> 清回 null</b>。</item>
/// </list>
///
/// ⇒ 只要"这一次攻击结束后活力还有剩余层数"，<c>commandToModify</c> 就会一直指着
/// **上一张牌**，之后所有攻击都被第 1/2 条挡掉 —— 表现为"有活力层数，但伤害完全不加"。
///
/// 原版里活力通常由卡牌/药水在**攻击之外**给予，所以"打完还剩层数"很少见，这个坑不显形。
/// 而本 mod 的「纵火狂」<c>PyromaniacPower</c> 会在**攻击结算过程中**
/// （「燃烧之魂」每段追加燃烧时）继续给活力 ⇒ 必然踩中：
/// 有活力、有层数、就是不加伤害。
///
/// ══════════════════════════════════════════════════════════════════
/// 修法
/// ══════════════════════════════════════════════════════════════════
/// 在 <c>AfterAttack</c> 的 Postfix 里：**当本次命令就是被跟踪的那条时，把跟踪状态清空**
/// （<c>commandToModify = null</c>、<c>amountWhenAttackStarted = 0</c>）。
/// 这样"还剩层数"的活力会在下一次攻击时重新被正常跟踪。
///
/// ⚠️ 安全性：`AfterAttack` 是 <c>async Task</c>，Harmony 的 Postfix 在状态机**第一次挂起**
/// （第一个 await 让出）时执行；而 <c>-amountWhenAttackStarted</c> 是在进入 await **之前**
/// 就求值进实参的，所以清空该字段不会影响本次扣减。
///
/// ⚠️ 依赖反射访问原版私有字段（<c>PowerModel._internalData</c> → <c>VigorPower.Data.*</c>）。
/// 一旦原版改字段名，反射会失败 —— 失败时**只记一条警告并放弃补丁**，绝不影响游戏运行。
///
/// ══════════════════════════════════════════════════════════════════
/// 探针（定位完成后可删）
/// ══════════════════════════════════════════════════════════════════
/// 三处各打一行 <c>[活力诊断]</c>，能一眼区分：
/// "钩子没被调用 / 被跟踪状态挡了 / 加算被闸门挡了 / 真的加了"。
/// 只在"施法者 == 活力拥有者 且 该伤害是一次攻击"时打印，避免刷屏。
/// </summary>
[HarmonyPatch(typeof(VigorPower))]
internal static class VigorTrackingPatch
{
    private const string LogTag = "[活力诊断]";

    private static readonly FieldInfo? InternalDataField =
        AccessTools.Field(typeof(PowerModel), "_internalData");

    private static readonly Type? DataType =
        typeof(VigorPower).GetNestedType("Data", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? CommandField =
        DataType?.GetField("commandToModify", BindingFlags.Public | BindingFlags.Instance);

    private static readonly FieldInfo? AmountField =
        DataType?.GetField("amountWhenAttackStarted", BindingFlags.Public | BindingFlags.Instance);

    private static bool _reflectionBroken;

    /// <summary>读取被跟踪的命令（反射失败返回 null，并只警告一次）。</summary>
    private static AttackCommand? TrackedCommand(VigorPower vigor)
    {
        if (_reflectionBroken)
        {
            return null;
        }

        if (InternalDataField == null || DataType == null || CommandField == null || AmountField == null)
        {
            _reflectionBroken = true;
            Entry.Logger.Warn(
                $"{LogTag} 反射定位 VigorPower 私有状态失败（原版可能改了字段名）；" +
                "本次不修正活力跟踪状态 —— 若再次出现「有活力但不加伤害」，请报告。");
            return null;
        }

        object? data = InternalDataField.GetValue(vigor);
        return data == null ? null : CommandField.GetValue(data) as AttackCommand;
    }

    // ══════════════════════════════════════════════════════════════
    // 探针 ①：BeforeAttack —— 看"这次攻击有没有被跟踪"
    // ══════════════════════════════════════════════════════════════
    [HarmonyPrefix]
    [HarmonyPatch(nameof(VigorPower.BeforeAttack))]
    private static void BeforeAttackPrefix(VigorPower __instance, AttackCommand command)
    {
        if (command.Attacker != __instance.Owner)
        {
            return;
        }

        AttackCommand? tracked = TrackedCommand(__instance);
        bool powered = command.DamageProps.IsPoweredAttack();
        string source = command.ModelSource == null ? "null" : command.ModelSource.GetType().Name;

        Entry.Logger.Info(
            $"{LogTag} BeforeAttack 牌={source} powered={powered} 层数={__instance.Amount} "
            + $"跟踪中={(tracked != null ? tracked.ModelSource?.GetType().Name : "无")} "
            + $"⇒ {((tracked != null || !powered) ? "本次不会吃到加成" : "本次将被跟踪")}");
    }

    // ══════════════════════════════════════════════════════════════
    // 探针 ②：ModifyDamageAdditive —— 看"实际加了多少"
    // ══════════════════════════════════════════════════════════════
    [HarmonyPostfix]
    [HarmonyPatch(nameof(VigorPower.ModifyDamageAdditive))]
    private static void ModifyDamageAdditivePostfix(
        VigorPower __instance,
        Creature? dealer,
        ValueProp props,
        CardModel? cardSource,
        ref decimal __result)
    {
        if (dealer != __instance.Owner || !props.IsPoweredAttack())
        {
            return;
        }

        Entry.Logger.Info(
            $"{LogTag} ModifyDamageAdditive 牌={(cardSource == null ? "null" : cardSource.GetType().Name)} "
            + $"层数={__instance.Amount} 加算={__result}");
    }

    // ══════════════════════════════════════════════════════════════
    // 修正 + 探针 ③：AfterAttack —— 清空"已完成的跟踪"
    // ══════════════════════════════════════════════════════════════
    [HarmonyPostfix]
    [HarmonyPatch(nameof(VigorPower.AfterAttack))]
    private static void AfterAttackPostfix(VigorPower __instance, AttackCommand command)
    {
        if (command.Attacker != __instance.Owner)
        {
            return;
        }

        AttackCommand? tracked = TrackedCommand(__instance);
        if (tracked == null || tracked != command || _reflectionBroken)
        {
            return;
        }

        object? data = InternalDataField!.GetValue(__instance);
        if (data == null)
        {
            return;
        }

        CommandField!.SetValue(data, null);
        AmountField!.SetValue(data, 0);

        Entry.Logger.Info(
            $"{LogTag} AfterAttack 已清空跟踪（剩余层数={__instance.Amount}）"
            + " ⇒ 下次攻击可以正常吃到活力");
    }
}
