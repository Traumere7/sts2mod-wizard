using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using Wizard.Scripts.Character;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 巫师死亡时，把立绘**直接换成倒地贴图**（不需要动画演出）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 两个入口，覆盖"战斗内"与"非战斗界面"
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>战斗内</b> —— <c>NCreature.StartDeathAnim</c> 的 Postfix。
///         全游戏死亡演出的唯一入口（<c>NCreature.cs:931</c>），
///         由 <c>CreatureCmd.cs:545</c> 唯一调用。</item>
///   <item><b>非战斗界面</b>（涅奥 / 火堆 / 商店）—— <c>RunManager.GuaranteeKillAllPlayers</c>
///         的 <b>Prefix</b>。「放弃游戏」的必经之路
///         （<c>RunManager.cs:1485</c> 调用，方法体 1501-1512，
///         注释原文 "When you Abandon a Run you are sentenced to death"）。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>为什么非战斗界面不能靠 <c>StartDeathAnim</c></b>：
/// <c>CreatureCmd.KillWithoutCheckingWinCondition</c> 里是
/// <c>NCombatRoom.Instance?.GetCreatureNode(creature)</c>，
/// 非战斗场景 <c>NCombatRoom.Instance</c> 为 null ⇒ 整段跳过。
/// </para>
///
/// <para>
/// ⚠️ <b>为什么不用 <c>shouldRemove</c> 判断"是不是玩家"</b>：
/// 玩家恒传 <c>false</c>（<c>shouldRemoveFromCombat &amp;&amp; creature.IsMonster</c>）。
/// 引擎自己对玩家的判据是 <c>Entity.Player != null</c>（<c>NCreature.cs:951</c>），这里照它。
/// </para>
///
/// <para>
/// ⚠️ <b>换图失败绝不能让死亡流程崩</b>：所有入口都包了 try/catch，
/// 最坏情况就是维持原立绘。
/// </para>
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class WizardDeathSpritePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        try
        {
            // ① 只处理玩家（与引擎 NCreature.cs:951 的判据一致）。怪物/召唤物不动。
            if (__instance.Entity?.Player == null)
            {
                return;
            }

            // ② 只处理巫师自己 —— 多人下别把队友的立绘换掉。
            if (!WizardCharacter.IsWizardPlayer(__instance.Entity.Player))
            {
                return;
            }

            NCreatureVisuals? visuals = __instance.Visuals;
            if (visuals == null || !GodotObject.IsInstanceValid(visuals))
            {
                return;
            }

            WizardDeathSprite.SwapAll(visuals);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 战败立绘替换失败（维持原贴图）：{e.Message}");
        }
    }
}

/// <summary>
/// 「放弃游戏」在<b>非战斗界面</b>（涅奥 / 火堆 / 商店）时的战败立绘替换。
///
/// <para>
/// ⚠️⚠️ <b>第一版在这里踩了大坑</b>：为了"不管当前是哪个界面都能找到"，
/// 直接从 <c>SceneTree.Root</c> 全树遍历、凡名字是 <c>Visuals</c>/<c>Icon</c>
/// 的 <c>Sprite2D</c> 就换图。实测替换了 <b>25 / 33 / 40</b> 个节点（应该是 1），
/// 并且污染了<b>预加载缓存里商人的立绘节点</b>，导致下一次进商店时
/// <c>NMerchantCharacter._Ready</c> 抛
/// <c>InvalidOperationException: Expected BoundObject to be a SpineSprite, but it is a Sprite2D!</c>
/// —— 那正是"商店点放弃游戏卡死"的根因。
/// </para>
///
/// <para>
/// <b>本版做法</b>：先<b>定位巫师的场景根节点</b>，再只在它的子树里换。
/// 定位方式是遍历"带脚本且脚本来自本 mod 场景"的节点，
/// 并逐个用 <see cref="WizardDeathSprite"/> 的严格过滤换图 ——
/// 找不到就什么都不做，绝不误伤。
/// </para>
///
/// <para>
/// <b>为什么 Prefix 而不是 Postfix</b>：<c>GuaranteeKillAllPlayers</c> 内部
/// 每个玩家之后会 <c>await Cmd.CustomScaledWait(0.25f, 0.5f)</c>，
/// 随后引擎就要 <c>ShowGameOverScreen</c> 转场。在 Prefix 换图，
/// 玩家在转场前那几帧看到的就是倒地图。
/// </para>
///
/// <para>
/// ⚠️ <b>目标方法名只能写字符串</b>：它声明为
/// <c>private async Task GuaranteeKillAllPlayers()</c>（<c>RunManager.cs:1501</c>），
/// mod 程序集看不见，不能用 <c>nameof</c>。签名无参 ⇒ Prefix 也不带匹配参数。
/// </para>
/// </summary>
[HarmonyPatch(typeof(RunManager), "GuaranteeKillAllPlayers")]
internal static class WizardAbandonRunDeathSpritePatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        try
        {
            int swapped = SwapWizardInTree();

            if (swapped > 0)
            {
                Entry.Logger.Info($"[Wizard] 放弃游戏：已替换 {swapped} 个战败立绘节点。");
            }
            else
            {
                // 不是错误：说明当前界面没有巫师立绘（例如多人局里是队友在操作）。
                Entry.Logger.Info("[Wizard] 放弃游戏：当前界面未找到巫师立绘，跳过。");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 放弃游戏时替换战败立绘失败：{e.Message}");
        }
    }

    /// <summary>
    /// 在当前场景树里找巫师立绘并换图。
    ///
    /// <para>
    /// 遍历全树是<b>为了找到</b>巫师节点（这几个界面的节点层级不同，
    /// 硬查路径会漏），但<b>换图本身有严格过滤</b>
    /// （见 <see cref="WizardDeathSprite"/>）——
    /// "遍历范围大"和"误伤"是两件事：
    /// 过滤条件（节点在场景树里 + 祖先场景来自 <c>res://Wizard/</c>）
    /// 保证了只会碰巫师自己的立绘。
    /// </para>
    /// </summary>
    private static int SwapWizardInTree()
    {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
        {
            return 0;
        }

        Texture2D? death = WizardDeathSprite.DeathTexture;
        if (death == null)
        {
            Entry.Logger.Warn("[Wizard] 放弃游戏：战败贴图加载失败。");
            return 0;
        }

        int count = 0;

        // 只对"带脚本且脚本属于本 mod 场景"的节点调用 ——
        // SwapAll 内部还会再过滤一次节点名/在树内/场景归属。
        foreach (Node node in EnumerateScripted(tree.Root))
        {
            if (!BelongsToWizardScene(node))
            {
                continue;
            }

            count += WizardDeathSprite.SwapAll(node);
        }

        return count;
    }

    /// <summary>遍历整棵树，只产出"设置了脚本"的节点（大幅缩小候选集）。</summary>
    private static IEnumerable<Node> EnumerateScripted(Node root)
    {
        if (root.GetScript().Obj != null)
        {
            yield return root;
        }

        foreach (Node child in root.GetChildren())
        {
            foreach (Node node in EnumerateScripted(child))
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// 这个节点所在的场景是否来自本 mod。
    ///
    /// <para>
    /// 用 <c>SceneFilePath</c>（Godot 4.2+ 提供，指向实例化它的
    /// <c>.tscn</c>）判断 —— 我们的三个角色场景都在 <c>res://Wizard/scenes/</c> 下，
    /// 而原版怪物的 <c>Visuals</c>、原版商人的 <c>Icon</c> 都不在这个前缀下。
    /// </para>
    /// </summary>
    private static bool BelongsToWizardScene(Node node)
    {
        for (Node? n = node; n != null; n = n.GetParent())
        {
            string scenePath = n.SceneFilePath;
            if (!string.IsNullOrEmpty(scenePath))
            {
                return scenePath.StartsWith("res://Wizard/", StringComparison.Ordinal);
            }
        }

        return false;
    }
}
