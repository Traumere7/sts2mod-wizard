using System.IO;
using System.Reflection;
using BaseLib.Config;
using Godot;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using Wizard.Scripts.Config;
using Wizard.Scripts.Spells;

namespace Wizard;

/// <summary>
/// 巫师 mod 的入口点。
/// 字符串 "Initialize" 必须与下面的初始化函数命名一致。
/// </summary>
[ModInitializer(nameof(Initialize))]
public class Entry
{
    /// <summary>mod id，必须与 Wizard.json 中的 id 一致。</summary>
    public const string ModId = "Wizard";

    /// <summary>全局日志器。</summary>
    public static readonly MegaCrit.Sts2.Core.Logging.Logger Logger = new(ModId, LogType.Generic);

    private static Harmony? _harmony;

    public static void Initialize()
    {
        Logger.Info($"[{ModId}] 正在初始化……");

        var assembly = Assembly.GetExecutingAssembly();

        // 必须：让 Godot 能找到本程序集里的 C# 脚本（自定义场景会用到）。
        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        // 设置项（BaseLib 的 mod 配置页会自动生成 UI）

        ModConfigRegistry.Register(ModId, new WizardArtConfig());

        // 美术 A/B：必须在**任何美术资源被加载之前**挂载覆盖包
        MountAltArtPack();

        ApplyPatches(assembly);

        // 常驻订阅"给予燃烧"事件，供「巨焱拳」统计"本场战斗施加了多少层"。
        // 复位由 BurnTallyResetPatch 挂在 Hook.BeforeCombatStart 上，见该类注释。
        BurnTally.Subscribe();

        Logger.Info($"[{ModId}] 初始化完成。");
    }

    /// <summary>
    /// 若玩家在设置里勾了「使用替换美术」，就把 DLL 同目录的
    /// <c>WizardAlt.pck</c> 作为**覆盖包**挂载上去。
    ///
    /// <para>
    /// 原理（2026-11-02 实测）：游戏只挂 <c>mods\Wizard\Wizard.pck</c> 一个包
    /// （<c>ModManager.cs:822-828</c>），但 Godot 的
    /// <c>ProjectSettings.LoadResourcePack(path, replaceFiles: true)</c>
    /// 允许再挂一个包、<b>用同名 <c>res://</c> 路径覆盖已有资源</b>。
    /// 覆盖包里的路径与正式包完全一致 ⇒ 不需要改任何美术路径代码。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>调用时机是硬要求</b>：必须在任何美术资源被加载之前。
    /// <c>Entry.Initialize</c> 由 <c>ModManager</c> 在挂完本 mod 的 pck 后立刻调用，
    /// 此时还没有模型/UI 去读贴图，所以这里挂载是安全的
    /// （实测：挂载后 <c>CacheMode.Reuse</c> 读到的是覆盖包里的版本）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 任何异常都只记日志、<b>不中止初始化</b>：美术包缺失/损坏不该让 mod 起不来。
    /// </para>
    /// </summary>
    private static void MountAltArtPack()
    {
        try
        {
            if (!WizardArtConfig.UseAltArt)
            {
                Logger.Info("美术：使用 mod 自带版本（设置里未开启替换美术）");
                return;
            }

            string dir = Path.GetDirectoryName(typeof(Entry).Assembly.Location) ?? ".";
            string altPath = Path.Combine(dir, "WizardAlt.pck");
            if (!File.Exists(altPath))
            {
                Logger.Warn($"美术：已开启替换美术，但没找到 {altPath}；继续使用自带版本");
                return;
            }

            bool ok = ProjectSettings.LoadResourcePack(altPath, replaceFiles: true);
            if (ok)
            {
                Logger.Info($"美术：已挂载替换美术包 {altPath}");
            }
            else
            {
                Logger.Warn($"美术：替换美术包挂载失败 {altPath}；继续使用自带版本");
            }
        }
        catch (Exception e)
        {
            Logger.Warn($"美术：挂载替换美术包时异常，继续使用自带版本：{e}");
        }
    }

    /// <summary>
    /// 逐个应用补丁，任一失败只记日志、**绝不中止初始化**。
    ///
    /// <para>
    /// <b>为什么不能直接 <c>_harmony.PatchAll(assembly)</c>：</b>
    /// <c>PatchAll</c> 遇到一个无法解析目标的补丁类就会抛
    /// <c>HarmonyException</c>；而它被本方法调用，异常会一路上抛到
    /// <c>ModManager.CallModInitializer</c>，把<b>整个 <c>Initialize</c> 中断</b>。
    /// </para>
    ///
    /// <para>
    /// <b>实际踩到过：</b><c>[HarmonyPatch(typeof(NParticlesContainer), "_Ready")]</c>
    /// 解析不出目标，于是抛异常 —— 结果是<b>初始化在这里就断了</b>，
    /// 连它后面的代码都不执行。排查时表现为"改了、部署了，但游戏里毫无变化"，
    /// 极具误导性。日志里的特征是：
    /// <code>
    /// [ERROR] Exception thrown when calling mod initializer of type Wizard.Entry:
    ///   System.Reflection.TargetInvocationException
    ///    ---> HarmonyLib.HarmonyException: Patching exception in method null
    ///    ---> System.ArgumentException: Undefined target method for patch method ...
    /// </code>
    /// </para>
    ///
    /// <para>
    /// 现在改为按类逐个 <c>CreateClassProcessor</c> + <c>Patch()</c>：
    /// 失败的类只影响自己，其余补丁照常生效，并且日志会点名是谁挂了。
    /// <b>代价：日志里补丁的成功与否变成必须看的东西</b> ——
    /// 所以这里会逐个打印成功/失败。
    /// </para>
    ///
    /// <para>
    /// 顺带一条硬性检查：<c>Patch()</c> 返回<b>空列表</b>意味着"补丁类解析到了，
    /// 但一个方法都没打上"（目标为 null 或签名不匹配）。
    /// 这种情况不抛异常，<b>静默无效</b>，所以必须显式当成失败报出来。
    /// </para>
    /// </summary>
    private static void ApplyPatches(Assembly assembly)
    {
        _harmony = new Harmony($"sts2.{ModId}");

        int ok = 0;
        int failed = 0;

        foreach (Type type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<HarmonyPatch>() == null)
            {
                continue;
            }

            try
            {
                // 注意：Godot.Bridge 也有一个 MethodInfo，必须写全名。
                List<System.Reflection.MethodInfo> patched = _harmony.CreateClassProcessor(type).Patch();

                if (patched == null || patched.Count == 0)
                {
                    failed++;
                    Logger.Info($"[{ModId}] 补丁无效（0 个方法，目标未解析或签名不匹配）：{type.Name}");
                    continue;
                }

                ok++;
                Logger.Info($"[{ModId}] 补丁已应用：{type.Name} -> {patched.Count} 个方法");
            }
            catch (Exception ex)
            {
                failed++;
                Logger.Info($"[{ModId}] 补丁应用失败（已跳过，不影响其它补丁）：{type.Name} -> {ex.Message}");
            }
        }

        Logger.Info($"[{ModId}] 补丁汇总：成功 {ok} 个，失败 {failed} 个。");
    }
}
