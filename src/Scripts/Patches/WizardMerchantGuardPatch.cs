using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using Wizard.Scripts.Patches;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 自绘商人立绘（非 Spine）的**安全性兜底**。
///
/// ══════════════════════════════════════════════════════════════════
/// 病灶：`NMerchantCharacter` 的两个方法都**无条件**把第 0 个子节点当 SpineSprite
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// public override void _Ready()
///     => this.RunWhenSpineReady(new MegaSprite(GetChild(0)), ...);
///
/// public void PlayAnimation(string anim, bool loop = false)
///     => new MegaSprite(GetChild(0)).GetAnimationState().SetAnimation(anim, loop);
/// </code>
/// 我们的商人场景第 0 个子节点是 `Sprite2D`（`Icon`）⇒ 抛
/// <c>InvalidOperationException: Expected BoundObject to be a SpineSprite, but it is a Sprite2D!</c>
///
/// <para>
/// <b>实测崩溃栈（这是「商店点放弃游戏卡死」的真正原因）</b>：
/// <code>
/// at NMerchantCharacter.PlayAnimation_Patch1(NMerchantCharacter this, String anim, Boolean loop)
/// at NGameOverScreen.MoveCreaturesToDifferentLayerAndDisableUi()
/// at NGameOverScreen.AfterOverlayOpened()
/// at NRun.ShowGameOverScreen(SerializableRun)
/// </code>
/// 对应 <c>NGameOverScreen.cs</c> 里商店那条分支：
/// <code>
/// else if (NMerchantRoom.Instance != null)
///     foreach (NMerchantCharacter v in NMerchantRoom.Instance.PlayerVisuals)
///     {
///         v.PlayAnimation("die");          // ← 崩在这里
///         v.Reparent(_creatureContainer);
///     }
/// </code>
/// 所以**正常逛商店完全没事**，只有"放弃/结算"才会走到这里 —— 与用户观察一致。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>为什么不靠 BaseLib 的守卫</b>：
/// BaseLib 有 <c>MerchantCharacterAnimPatch</c>，但它的两道前提极易失配：
/// <list type="number">
///   <item><c>NodeFactory.CreatedFromFactory</c> —— 只对走工厂创建的节点为 true，
///         而 <c>NMerchantRoom.cs:249</c> 是**自己**
///         <c>PreloadManager.Cache.GetScene(path).Instantiate()</c>，绕过工厂；</item>
///   <item><c>CustomAnimation.HasCustomAnimation</c> —— 只保证"有个动画播放器"，
///         <b>不保证里面真有那个动画名</b>；随后它还会试
///         <c>PlayCustomAnimation(__instance, "Die", "die")</c>，
///         播放器里没有 <c>die</c> 就返回 false，<b>原始方法照样执行 ⇒ 崩</b>。</item>
/// </list>
/// 我们实测踩过：只放一个"空的 AnimationPlayer"等于没修。
/// <b>因此本补丁不再依赖任何守卫，直接把这两个方法拦掉。</b>
/// </para>
///
/// <para>
/// <b>Harmony 的补丁顺序</b>：本补丁的 Prefix 返回 <c>false</c> 时，
/// <b>后续（更内层）的 Prefix 与原始方法都不会执行</b> ——
/// 所以哪怕 BaseLib 的 Prefix 也在同一方法上，也不会再走到崩溃路径。
/// （HarmonyX 对同方法的多组补丁按注册顺序从外到内嵌套执行。）
/// </para>
///
/// <para>
/// <b>我们做什么代替</b>：
/// <list type="bullet">
///   <item><c>_Ready</c> → 什么都不做（我们不需要待机动画，立绘是静态图）。</item>
///   <item><c>PlayAnimation("die")</c> → 把立绘换成战败贴图（复用
///         <see cref="WizardDeathSprite"/> 的地面线对齐逻辑）。</item>
///   <item>其它动画名 → 忽略（没有 spine 可播，也不该崩）。</item>
/// </list>
/// </para>
/// </summary>
[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
internal static class WizardMerchantPlayAnimationPatch
{
    /// <summary>
    /// Prefix 永远返回 <c>false</c> —— 原始方法对我们一定是崩的，
    /// 没有"有时能安全跑"的情况。非巫师的商人角色直接放行（return true）。
    /// </summary>
    [HarmonyPrefix]
    private static bool Prefix(NMerchantCharacter __instance, string anim)
    {
        try
        {
            if (!WizardMerchantGuard.IsWizardMerchant(__instance))
            {
                // 别人的商人（原版/其它 mod）走原逻辑。
                return true;
            }

            if (anim is "die" or "Die")
            {
                // 结算界面要播的死亡动画 → 用战败贴图代替。
                WizardDeathSprite.SwapAll(__instance);
            }

            // 其余动画（relaxed_loop 等）忽略：静态立绘没有可播的东西。
            return false;
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 商人 PlayAnimation 兜底失败：{e.Message}");
            // 宁可不动，也不要让原始方法跑起来崩掉。
            return false;
        }
    }
}

/// <summary>
/// `NMerchantCharacter._Ready` 的兜底 —— 同样无条件把第 0 个子节点当 SpineSprite。
///
/// <para>
/// 实测这一处在<b>进入商店</b>时也会抛异常（日志里 `_Ready_Patch1` 那条），
/// 虽然它不像 `PlayAnimation` 那样直接卡死结算，但同样是错误来源，一并拦掉。
/// 我们不需要待机动画。
/// </para>
/// </summary>
[HarmonyPatch(typeof(NMerchantCharacter), "_Ready")]
internal static class WizardMerchantReadyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMerchantCharacter __instance)
    {
        try
        {
            return !WizardMerchantGuard.IsWizardMerchant(__instance);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 商人 _Ready 兜底失败：{e.Message}");
            return false;
        }
    }
}

/// <summary>
/// 结算界面（`NGameOverScreen`）里"既不在战斗、也不在商店、也不在火堆"那条分支
/// —— 即**涅奥开局界面**等场景。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么涅奥界面"不崩但也不换图"
/// ══════════════════════════════════════════════════════════════════
/// `NGameOverScreen.cs:1005-1015` 那条 <c>else</c> 分支做的是：
/// <code>
/// NCreatureVisuals v = player.Creature.CreateVisuals();   // ← 重新实例化我们的战斗场景
/// _creatureContainer.AddChildSafely(v);
/// v.SpineAnimation.SetAnimation("die", loop: false);      // ← 想播死亡动画
/// </code>
/// 而 <c>SpineAnimationAccess</c> 是**显式 null-safe** 的
/// （其类注释原文："Null-safe wrapper ... No-ops when the underlying MegaSprite is null"）：
/// <code>
/// public void SetAnimation(string name, bool loop = true, int track = 0)
///     => _sprite?.GetAnimationState().SetAnimation(name, loop, track);
/// </code>
/// 我们的立绘是普通 <c>Sprite2D</c>，<c>SpineBody</c> 为 null ⇒
/// <b>这一句静默什么都不做，不抛异常</b>。
/// 所以涅奥界面的表现是"<b>不崩，但立绘保持站姿</b>" —— 与用户观察完全一致。
/// </para>
///
/// <para>
/// ⚠️ 注意与商店的区别：商店走的是
/// <c>NMerchantCharacter.PlayAnimation</c>，那里是
/// <c>new MegaSprite(GetChild(0))</c> —— **没有 null 保护** ⇒ 抛异常 ⇒ 卡死。
/// 同一个结算流程，两条分支的健壮性不同，这就是"商店卡死、涅奥只是不换图"的原因。
/// </para>
///
/// <para>
/// <b>本补丁的做法</b>：Postfix 里对"刚被放进结算界面的那些立绘"再跑一次
/// <see cref="WizardDeathSprite.SwapAll"/>。因为
/// <c>CreateVisuals()</c> 是**新实例化**的场景，立绘是原样的
/// <c>wizard_battle.png</c>，正好符合换图条件。
/// </para>
///
/// <para>
/// 对商店/火堆分支也一样安全：那两条分支要么没有 <c>NCreatureVisuals</c>，
/// 要么 <c>SwapAll</c> 会因"已经换过"（meta 标记）或"贴图不是战斗姿态"而跳过 —— 幂等。
/// </para>
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), "MoveCreaturesToDifferentLayerAndDisableUi")]
internal static class WizardGameOverDeathSpritePatch
{
    [HarmonyPostfix]
    private static void Postfix(NGameOverScreen __instance)
    {
        try
        {
            // 结算界面会把立绘挂到 %CreatureContainer 下，直接扫它最准。
            if (__instance.GetNodeOrNull<Control>("%CreatureContainer") is not { } container)
            {
                return;
            }

            int swapped = WizardDeathSprite.SwapAll(container);

            if (swapped > 0)
            {
                Entry.Logger.Info($"[Wizard] 结算界面：已替换 {swapped} 个战败立绘节点。");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 结算界面替换战败立绘失败：{e.Message}");
        }
    }
}

///
/// <para>
/// 多重证据，任一命中即认定（顺序从最可靠到最宽松）：
/// <list type="number">
///   <item>场景文件路径以 <c>res://Wizard/</c> 开头（最权威）；</item>
///   <item>子树里有我们那三个场景的立绘节点（节点名 <c>Icon</c>/<c>Visuals</c>）
///         且贴图来自本 mod。</item>
/// </list>
/// 第一条依赖 Godot 的 <c>SceneFilePath</c>；实测我们的商人场景节点上它是
/// 空串，所以必须有第二条兜底。
/// </para>
/// </summary>
internal static class WizardMerchantGuard
{
    /// <summary>我们商人场景的立绘节点名（见 <c>wizard_character_merchant.tscn</c>）。</summary>
    private const string MerchantSpriteName = "Icon";

    public static bool IsWizardMerchant(NMerchantCharacter node)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return false;
        }

        // 证据 ①：场景归属
        for (Node? n = node; n != null; n = n.GetParent())
        {
            string scenePath = n.SceneFilePath;
            if (!string.IsNullOrEmpty(scenePath))
            {
                if (scenePath.StartsWith("res://Wizard/", StringComparison.Ordinal))
                {
                    return true;
                }

                break;
            }
        }

        // 证据 ②：立绘节点名 + 贴图来自本 mod
        //   ⚠️ 用 GetNodeOrNull（不是递归查找）：我们的场景里 Icon 是直接子级。
        if (node.GetNodeOrNull<Sprite2D>(MerchantSpriteName) is { } sprite)
        {
            Texture2D? tex = sprite.Texture;
            if (tex != null)
            {
                string path = tex is AtlasTexture atlas && atlas.Atlas != null
                    ? atlas.Atlas.ResourcePath
                    : tex.ResourcePath;

                if (path.Contains("/Wizard/"))
                {
                    return true;
                }
            }

            // 贴图路径拿不到时，退一步看"有没有我们那个 AnimationPlayer"
            // （我们为修这个 bug 专门加的，原版商人不会有）。
            if (node.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") != null)
            {
                return true;
            }
        }

        return false;
    }
}
