using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Dev;

/// <summary>
/// 把巫师的全部卡牌渲染成**游戏里那样的整图**（卡框 + 卡图 + 描述 + 费用 + 标题 + 类型条）并写成 PNG。
///
/// <para>
/// <b>原理</b>：不自己拼卡面，直接用游戏自己的 <see cref="NCard"/>：
/// 从卡池取空节点 → 先入树 → 绑 Model → <b>显式调 <c>UpdateVisuals</c></b> → 等几帧 → 抓图 → 归还卡池。
/// </para>
///
/// <para>⚠️ <b>只能在"真的跑起来的游戏里"执行</b>：字体、贴图、节点池都在游戏自己的 PCK 里。</para>
///
/// <para>
/// ★★ <b>坑 1：必须先入树、再绑 Model。</b>
/// <c>NCard.Model</c> 的 setter 会调 <c>Reload()</c>，而 <c>Reload()</c> 与 <c>UpdateVisuals()</c>
/// 第一行都是 <c>if (!IsNodeReady()) return;</c>。入树前绑 Model 会让它们<b>静默空转</b>。
/// </para>
///
/// <para>
/// ★★ <b>坑 2：<c>Reload()</c> 不负责文字，必须再显式调一次 <c>UpdateVisuals</c>。</b>
/// <c>Reload()</c> 只做四件事：换能量图标贴图、<c>UpdateTypePlaque()</c>、按稀有度切换远古卡外观、
/// 按 <c>Visibility</c> 上模糊材质。<b>标题 / 描述 / 费用数字 / 星标显隐全在 <c>UpdateVisuals</c> 里</b>，
/// 而它平时是由"卡被放进某个牌堆/手持槽"时调用的，我们手工造卡不会自动触发。
/// 症状：标题显示 <c>card.tscn</c> 的默认文本「Broken Card」、描述显示默认占位
/// 「If you can read this, there is a bug.」、费用恒为 0、左上角多出一枚星标。
/// </para>
///
/// <para>
/// ★★ <b>坑 3：卡面以节点原点为中心，不是左上角。</b>
/// <c>Frame</c> 是 300×422 居中，费用球伸到 x -166 / y -227，星标伸到 x -186。
/// 所以卡要放在视口中心，视口也要比 300×422 大一圈。
/// </para>
///
/// <para>
/// ⚠️ <b>卡池归还顺序</b>：<see cref="NodePool.Free{T}"/> 只放回池子、<b>不摘树</b>，必须先 <c>RemoveChild</c>。
/// </para>
///
/// <para>⚠️ <b>不要调 <c>NCard.InitPool()</c></b>：重复 <c>NodePool.Init</c> 会抛异常（游戏已初始化过）。</para>
/// </summary>
public static class WizardCardExporter
{
    /// <summary>默认输出目录（开发机上的仓库路径，见 docs/CARD_EXPORT.md）。</summary>
    public const string DefaultOutDir = @"D:\Projects\STS2\Wizard\art\cards_in_game";

    /// <summary>默认导出语言：简体中文。</summary>
    public const string DefaultLanguage = "zhs";

    /// <summary>视口尺寸：容纳伸出卡框的费用球 / 星标 / 标题横幅，保证每张图尺寸统一。</summary>
    private const int ViewWidth = 384;
    private const int ViewHeight = 480;

    /// <summary>卡节点摆放位置 = 视口中心（卡面以自身原点为中心）。</summary>
    private static readonly Vector2 CardCenter = new(ViewWidth / 2f, ViewHeight / 2f);

    /// <summary>绑完 Model 并调完 <c>UpdateVisuals</c> 后等几帧再抓图。</summary>
    private const int FramesBeforeCapture = 3;

    /// <summary>导出全部巫师卡牌的未升级版本。</summary>
    /// <param name="outDirOverride">可选：覆盖输出目录。</param>
    /// <param name="languageOverride">可选：覆盖导出语言（如 <c>zhs</c> / <c>eng</c>），默认 <see cref="DefaultLanguage"/>。</param>
    /// <returns>成功导出的张数。</returns>
    public static async Task<int> RunAsync(string? outDirOverride = null, string? languageOverride = null)
    {
        string outDir = ResolveOutDir(outDirOverride);
        Directory.CreateDirectory(outDir);

        var tree = (SceneTree)Engine.GetMainLoop();

        var viewport = new SubViewport
        {
            Size = new Vector2I(ViewWidth, ViewHeight),
            // ★ 透明底：卡框圆角之外完全透明。
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Disable3D = true,
        };
        tree.Root.AddChild(viewport);

        LocManager? loc = LocManager.Instance;
        string? restoreLanguage = null;

        try
        {
            // ── 临时切到目标语言（默认简体中文）──────────────────
            string wanted = string.IsNullOrWhiteSpace(languageOverride) ? DefaultLanguage : languageOverride!;
            if (loc != null && loc.Language != wanted)
            {
                if (!LocManager.Languages.Contains(wanted))
                {
                    Entry.Logger.Warn(
                        $"[卡牌导出] 不支持的语言 '{wanted}'，沿用当前语言 '{loc.Language}'。" +
                        $"可选：{string.Join(", ", LocManager.Languages)}");
                }
                else
                {
                    restoreLanguage = loc.Language;
                    loc.SetLanguage(wanted);
                    Entry.Logger.Info($"[卡牌导出] 语言 {restoreLanguage} → {wanted}");
                    // 让语言切换的回调跑完
                    for (int i = 0; i < 2; i++)
                    {
                        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    }
                }
            }

            CardModel[] cards = ModelDb.CardPool<WizardCardPool>().AllCards
                .OrderBy(c => c.GetType().Name, StringComparer.Ordinal)
                .ToArray();

            Entry.Logger.Info($"[卡牌导出] 共 {cards.Length} 张，视口 {ViewWidth}x{ViewHeight}，输出目录：{outDir}");

            int ok = 0;
            int failed = 0;

            foreach (CardModel model in cards)
            {
                string fileName = model.GetType().Name;
                NCard? node = null;

                try
                {
                    // ★★ 坑 1：拿空模型节点，Model 要等入树之后再绑。
                    node = NodePool.Get<NCard>();

                    if (node.GetParent() is { } oldParent)
                    {
                        oldParent.RemoveChild(node);
                    }

                    node.Scale = Vector2.One;
                    node.Rotation = 0f;
                    node.Position = CardCenter;   // ★★ 坑 3：卡面以原点为中心
                    node.Visible = true;

                    viewport.AddChild(node);      // ← _Ready 跑完，IsNodeReady() 为真

                    node.Visibility = ModelVisibility.Visible;
                    node.Model = model;           // ← Reload() 生效（卡框/类型条/能量图标）

                    // ★★ 坑 2：Reload() 不管文字，必须显式再刷一次。
                    // 这一步才会设置：标题、描述正文、费用数字、星标显隐、卡图。
                    node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);

                    // 悬浮高亮是一张 759x951 的发光贴图，出图时不要。
                    if (node.CardHighlight is CanvasItem highlight)
                    {
                        highlight.Visible = false;
                    }

                    for (int i = 0; i < FramesBeforeCapture; i++)
                    {
                        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    }

                    Image? image = viewport.GetTexture()?.GetImage();
                    if (image == null)
                    {
                        Entry.Logger.Warn($"[卡牌导出] {fileName}：抓图得到 null，跳过");
                        failed++;
                        continue;
                    }

                    File.WriteAllBytes(Path.Combine(outDir, fileName + ".png"), image.SavePngToBuffer());
                    ok++;

                    if (ok % 10 == 0)
                    {
                        Entry.Logger.Info($"[卡牌导出] 进度 {ok}/{cards.Length}…");
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    Entry.Logger.Warn($"[卡牌导出] {fileName} 导出失败：{e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    if (node != null)
                    {
                        if (node.GetParent() is { } p)
                        {
                            p.RemoveChild(node);
                        }

                        try
                        {
                            NodePool.Free(node);
                        }
                        catch (Exception e)
                        {
                            Entry.Logger.Warn($"[卡牌导出] {fileName} 归还卡池失败：{e.Message}");
                        }
                    }
                }
            }

            Entry.Logger.Info($"[卡牌导出] 完成：成功 {ok} 张，失败 {failed} 张 → {outDir}");
            return ok;
        }
        finally
        {
            // ── 恢复原语言 ──────────────────────────────────
            if (restoreLanguage != null && loc != null)
            {
                loc.SetLanguage(restoreLanguage);
                Entry.Logger.Info($"[卡牌导出] 语言已恢复为 {restoreLanguage}");
            }

            viewport.QueueFree();
        }
    }

    /// <summary>解析输出目录；指定的目录建不出来时退回到 <c>user://card_exports</c>。</summary>
    private static string ResolveOutDir(string? overrideDir)
    {
        string wanted = string.IsNullOrWhiteSpace(overrideDir) ? DefaultOutDir : overrideDir!;

        try
        {
            Directory.CreateDirectory(wanted);
            return wanted;
        }
        catch (Exception e)
        {
            string fallback = ProjectSettings.GlobalizePath("user://card_exports");
            Entry.Logger.Warn($"[卡牌导出] 输出目录 {wanted} 不可用（{e.Message}），改用 {fallback}");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
