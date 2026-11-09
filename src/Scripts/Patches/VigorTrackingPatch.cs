using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 修正原版「活力」<see cref="VigorPower"/> 的**跟踪状态复用**问题
/// （2026-11-09 定位并实测确认修复）。
///
/// ══════════════════════════════════════════════════════════════════
/// 原版的坑
/// ══════════════════════════════════════════════════════════════════
/// <c>VigorPower</c> 用一份私有状态只"认一次攻击"：
/// <code>
/// Data { AttackCommand? commandToModify; int amountWhenAttackStarted; }
///
/// BeforeAttack:      if (commandToModify != null) return;          // ② 后续攻击不跟踪
/// ModifyDamageAdditive: commandToModify != null &amp;&amp; cardSource != tracked.ModelSource ⇒ 0
/// AfterAttack:       ModifyAmount(-amountWhenAttackStarted);       // ① 只扣层数，不清跟踪
/// </code>
/// ⇒ 只要一次攻击结束时活力**还有剩余层数**，<c>commandToModify</c> 就一直指着上一张牌，
/// 之后所有攻击都吃不到活力（表现为"有活力层数但伤害不加"）。
///
/// 原版不会在攻击过程中给活力，所以这个坑在 vanilla 里不显形；
/// 本 mod 的「纵火狂」在「燃烧之魂」追加燃烧时给活力，必然踩中。
///
/// ══════════════════════════════════════════════════════════════════
/// 修法
/// ══════════════════════════════════════════════════════════════════
/// 在 <c>AfterAttack</c> 的 Postfix 里：本次命令就是被跟踪那条时，把
/// <c>commandToModify = null</c>、<c>amountWhenAttackStarted = 0</c> 清空，
/// 让"还剩层数"的活力在下一次攻击时重新被正常跟踪。
///
/// <list type="bullet">
///   <item><c>AfterAttack</c> 是 <c>async Task</c>：Harmony 的 Postfix 在状态机**第一次挂起**时执行，
///         而 <c>-amountWhenAttackStarted</c> 在进 await **之前**已求值进实参，
///         所以清空该字段不会影响本次扣减。</item>
///   <item>依赖反射访问原版私有字段（<c>PowerModel._internalData</c> →
///         <c>VigorPower.Data.*</c>）。反射失败**只记一条警告并放弃补丁**，绝不影响游戏运行。</item>
/// </list>
///
/// <para>
/// 2026-11-09 实测：一场战斗里出现 7 次"AfterAttack 时仍有剩余层数（12/36）"，
/// 修复后下一张攻击牌（含「魔力爆发」这种以太法术）都能正常加算；之后再无"被挡"记录。
/// 定位用的 <c>[活力诊断]</c> 探针已按用户确认删除。
/// </para>
/// </summary>
[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.AfterAttack))]
internal static class VigorTrackingPatch
{
    private static readonly FieldInfo? InternalDataField =
        AccessTools.Field(typeof(PowerModel), "_internalData");

    private static readonly Type? DataType =
        typeof(VigorPower).GetNestedType("Data", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? CommandField =
        DataType?.GetField("commandToModify", BindingFlags.Public | BindingFlags.Instance);

    private static readonly FieldInfo? AmountField =
        DataType?.GetField("amountWhenAttackStarted", BindingFlags.Public | BindingFlags.Instance);

    private static bool _reflectionBroken;

    [HarmonyPostfix]
    private static void Postfix(VigorPower __instance, AttackCommand command)
    {
        if (_reflectionBroken || command.Attacker != __instance.Owner)
        {
            return;
        }

        if (InternalDataField == null || DataType == null || CommandField == null || AmountField == null)
        {
            _reflectionBroken = true;
            Entry.Logger.Warn(
                "[活力] 反射定位 VigorPower 私有状态失败（原版可能改了字段名）；"
                + "本次不修正活力跟踪状态。若再出现「有活力层数但不加伤害」，请报告。");
            return;
        }

        object? data = InternalDataField.GetValue(__instance);
        if (data == null || CommandField.GetValue(data) is not AttackCommand tracked || tracked != command)
        {
            return;
        }

        // 本次攻击就是被跟踪的那条 ⇒ 跟踪结束，清空状态。
        // 剩余层数（纵火狂在攻击中补的）留给下一次攻击正常使用。
        CommandField.SetValue(data, null);
        AmountField.SetValue(data, 0);
    }
}
