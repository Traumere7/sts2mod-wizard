using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 每场战斗开始时清空 <see cref="BurnTally"/>。
///
/// ══════════════════════════════════════════════════════════════════
/// 为什么需要一个补丁（而不是像「纵火狂」那样用能力生命周期）
/// ══════════════════════════════════════════════════════════════════
///
/// 「纵火狂」<c>PyromaniacPower</c>、「全知形态」<c>OmniscientFormPower</c> 订阅
/// <see cref="WizardEvents.BurnApplied"/> 时，可以在能力的
/// <c>AfterRemoved</c> / <c>AfterCombatEnd</c> 里退订 —— 能力本身就是订阅的宿主。
///
/// 但 <see cref="BurnTally"/> 是**常驻的单例累加器**：它必须在整局里一直订阅，
/// 否则"这张牌还没入手时施加的燃烧"就漏了。而常驻订阅就必须有人负责**复位** ——
/// 否则计数会跨战斗累积，"本场战斗"的语义就没了。
///
/// 卡牌侧<b>拿不到</b>可靠的"新战斗开始"钩子（<c>AfterCombatEnd</c> 只对
/// <c>AbstractModel</c> 子类开放，见 <c>CONTEXT.md</c> §6.4），
/// 所以这里在引擎的 <see cref="Hook.BeforeCombatStart"/> 上挂一个 Postfix。
///
/// <para>
/// <b>为什么挂 <c>BeforeCombatStart</c> 而不是别的：</b>
/// 它是引擎里唯一的"战斗开始"入口（<c>Hook.cs:311</c>），
/// 在它之前不可能有本场战斗的燃烧施加，清在这里既不会漏也不会误清。
/// </para>
///
/// <para>
/// <b>幂等且安全：</b>Postfix 只做 <c>Reset()</c>，
/// 不清也不影响"只晚不清"以外的任何行为（最坏情况是计数偏多，
/// 而不是崩溃或数据损坏）。
/// </para>
///
/// <para>
/// ⚠️ <c>Harmony</c> 补丁类必须带 <c>[HarmonyPatch]</c> ——
/// <c>Entry.ApplyPatches</c> 是按这个特性扫描的，
/// 且会逐个打印成功/失败（失败的类不影响其它补丁）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class BurnTallyResetPatch
{
    private static void Postfix()
    {
        BurnTally.Reset();
        Entry.Logger.Info($"[{Entry.ModId}] 新战斗开始：燃烧施加计数已清零。");
    }
}
