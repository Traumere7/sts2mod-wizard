using System.Reflection;
using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 修一个会让<b>战斗开局不抽牌、整场卡死</b>的恶性 bug。
///
/// ══════════════════════════════════════════════════════════════════
/// 症状与日志
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// [ERROR] Combat #1 turn loop died while its combat is in progress;
///         the combat is stuck until the room is restarted:
///         System.NullReferenceException
///   at NParticlesContainer.Restart()
///   at NEnergyCounter.OnEnergyChanged(Int32, Int32)
///   at PlayerCombatState.set_Energy(Int32)
///   at PlayerCombatState.ResetEnergy()
///   at CombatManager.SetupPlayerTurn(...)
/// </code>
///
/// ══════════════════════════════════════════════════════════════════
/// 根因
/// ══════════════════════════════════════════════════════════════════
/// <c>NParticlesContainer</c> 的字段
/// <code>private Array&lt;GpuParticles2D&gt;? _particles;</code>
/// 在 C# 里<b>没有初始化式</b>，只能靠场景序列化赋值。
/// 而它的两个方法都**无条件解引用**：
/// <code>
/// public void Restart()                  { for (int i = 0; i &lt; _particles.Count; i++) ... }
/// public void SetEmitting(bool emitting) { for (int i = 0; i &lt; _particles.Count; i++) ... }
/// </code>
/// 只要场景没给 <c>_particles</c> 赋值，<c>_particles.Count</c> 本身就抛 NRE。
/// 注意 <c>_backVfx?.Restart()</c> 里的 <c>?.</c> **保护不了这种情况** ——
/// 容器对象是好的，坏的是它内部的数组。
///
/// ══════════════════════════════════════════════════════════════════
/// 为什么这会弄死整个回合
/// ══════════════════════════════════════════════════════════════════
/// <c>ResetEnergy()</c> 在 <c>CombatManager.SetupPlayerTurn</c> 里、
/// <b>抽牌之前</b>执行，且 <c>EnergyChanged</c> 是<b>同步</b>触发的。
/// 异常直接掀掉 turn loop ⇒ 牌永远抽不出来 ⇒ 战斗永久卡住。
/// 这就是"战斗开局不抽牌"的真正原因，与抽牌逻辑本身无关。
///
/// ══════════════════════════════════════════════════════════════════
/// 为什么原版角色没事、我们中招
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item>原版角色的能量表盘场景自己序列化了 <c>_particles</c> —— 所以没事</item>
///   <item>BaseLib 的 <c>NEnergyCounterFactory.SetParticles</c> 有反射兜底：
///         <c>ParticlesField?.SetValue(container, CollectParticles(...))</c></item>
/// </list>
/// 我们的场景<b>自带</b> <c>EnergyVfxBack</c>/<c>EnergyVfxFront</c> 节点
/// （为了让 <c>NEnergyCounter._Ready</c> 的 <c>GetNode("%...")</c> 能解析成功），
/// 于是工厂认为节点已齐备、不生成、<b>也就不会调用 SetParticles</b> ——
/// 两条路都绕过了，<c>_particles</c> 保持 null。
///
/// ══════════════════════════════════════════════════════════════════
/// 修法
/// ══════════════════════════════════════════════════════════════════
/// 给**会崩的那两个方法本身**加 Prefix：进方法前若 <c>_particles</c> 为空，
/// 就递归收集 <c>GpuParticles2D</c> 子节点填入（逻辑与 BaseLib
/// <c>NEnergyCounterFactory.CollectParticles</c> 一致），至少保证它是
/// <b>空数组</b>而不是 null。然后<b>放行原方法</b> —— 不改变任何行为。
///
/// <b>⚠️ 为什么不补丁 <c>_Ready</c>（踩过的坑）：</b>
/// 最初写的是 <c>[HarmonyPatch(typeof(NParticlesContainer), "_Ready")]</c>，
/// 结果 <c>AccessTools.Method</c> <b>解析不出这个目标</b>，<c>PatchAll</c> 抛
/// <c>HarmonyException: Undefined target method</c>。
/// 更糟的是这个异常会**中断 <c>Entry.Initialize()</c>**，
/// 导致后面所有初始化（含其它补丁）全部不执行，而崩溃照旧 ——
/// 表现为"改了但完全没效果"。
///
/// <b>根因（已用 MetadataLoadContext 反射核实）：</b><c>_Ready</c> 是 <b>virtual</b> 方法 ——
/// <code>
/// System.Void _Ready()                     virtual=True   private=False
/// System.Void Restart()                    virtual=False  private=False
/// System.Void SetEmitting(System.Boolean)  virtual=False  private=False
/// </code>
/// 所以补丁改用后两个 —— 类型唯一、<b>非虚</b>、签名无歧义，解析可靠得多。
/// 好在 <c>Restart</c> 正好就是崩溃栈的顶点。
///
/// <b>为什么用反射读字段：</b>无论该字段是 <c>private</c> 还是别的可见性都能工作，
/// 不依赖反编译结果。已核实它是
/// <c>private Godot.Collections.Array&lt;Godot.GpuParticles2D&gt;</c>，
/// <c>GetField</c> 能正常拿到。用 <c>GetField</c> 而不是 <c>AccessTools.Field</c>，
/// 是为了不让"Harmony 能否解析私有字段"成为新的失败点。
/// </summary>
internal static class ParticlesContainerGuard
{
    public const string Tag = "[Wizard/Particles]";

    /// <summary>字段解析一次即可；没找到就一直是 null，Prefix 会直接放行。</summary>
    public static readonly FieldInfo? ParticlesField = typeof(NParticlesContainer).GetField(
        "_particles",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static bool _loggedMissing;

    /// <summary>
    /// 确保 <paramref name="container"/> 的 <c>_particles</c> 不是 null。
    /// 已经赋过非空值就尊重它，不覆盖。
    /// </summary>
    public static void EnsureInitialized(NParticlesContainer container)
    {
        try
        {
            if (ParticlesField == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    Log.Info($"{Tag} 找不到 _particles 字段，无法修补");
                }

                return;
            }

            if (ParticlesField.GetValue(container) is Array<GpuParticles2D> existing && existing.Count > 0)
            {
                return;
            }

            var particles = new Array<GpuParticles2D>();
            CollectParticles(container, particles);
            ParticlesField.SetValue(container, particles);

            Log.Info($"{Tag} {container.Name}: _particles 已修补为 {particles.Count} 个粒子节点");
        }
        catch (Exception ex)
        {
            // 绝不把异常抛出去 —— 那正是我们要修的 bug 的形态。
            Log.Info($"{Tag} 修补 _particles 失败（已忽略）：{ex.Message}");
        }
    }

    /// <summary>与 BaseLib <c>NEnergyCounterFactory.CollectParticles</c> 一致：递归收集。</summary>
    private static void CollectParticles(Node node, Array<GpuParticles2D> particles)
    {
        foreach (Node child in node.GetChildren(false))
        {
            if (child is GpuParticles2D gpu)
            {
                particles.Add(gpu);
            }

            CollectParticles(child, particles);
        }
    }
}

/// <summary>崩溃点 ①：<c>OnEnergyChanged</c> 加能量时调它 → 三个报错栈的顶点。</summary>
[HarmonyPatch(typeof(NParticlesContainer), nameof(NParticlesContainer.Restart))]
internal static class ParticlesContainerRestartPatch
{
    [HarmonyPrefix]
    private static void Prefix(NParticlesContainer __instance)
        => ParticlesContainerGuard.EnsureInitialized(__instance);
}

/// <summary>崩溃点 ②：另一个同样会解引用 <c>_particles</c> 的方法，一并修掉。</summary>
[HarmonyPatch(typeof(NParticlesContainer), nameof(NParticlesContainer.SetEmitting))]
internal static class ParticlesContainerSetEmittingPatch
{
    [HarmonyPrefix]
    private static void Prefix(NParticlesContainer __instance)
        => ParticlesContainerGuard.EnsureInitialized(__instance);
}
