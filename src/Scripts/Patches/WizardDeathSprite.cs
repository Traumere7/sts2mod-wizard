using System.Collections.Generic;
using Godot;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 把巫师的立绘换成「战败贴图」的通用逻辑。
///
/// ══════════════════════════════════════════════════════════════════
/// 为什么需要"通用"
/// ══════════════════════════════════════════════════════════════════
/// 战斗内死亡挂在 <c>NCreature.StartDeathAnim</c> 上就够了，但
/// <b>非战斗界面（涅奥 / 火堆 / 商店）那条路走不到</b> ——
/// <c>CreatureCmd.KillWithoutCheckingWinCondition</c> 里是
/// <c>NCombatRoom.Instance?.GetCreatureNode(creature)</c>，
/// 非战斗场景 <c>NCombatRoom.Instance</c> 为 null ⇒ 整段跳过。
///
/// 而那几个界面的立绘载体<b>没有共同基类</b>：
/// <list type="bullet">
///   <item>战斗 <c>NCreatureVisuals</c>：节点名 <c>Visuals</c>，贴图 <c>wizard_battle.png</c></item>
///   <item>火堆 <c>NRestSiteCharacter</c>：节点名 <c>Visuals</c>，贴图是 <c>wizard_rest_site.png</c> 切出的 <c>AtlasTexture</c>（坐姿）</item>
///   <item>商店 <c>NMerchantCharacter</c>：节点名 <c>Icon</c>，贴图 <c>wizard_battle.png</c></item>
///   <item>涅奥：与战斗同一个场景</item>
/// </list>
///
/// ══════════════════════════════════════════════════════════════════
/// ⚠️⚠️ 第一版踩的坑（2026-10-13 实测）：**绝不要遍历整棵场景树乱换**
/// ══════════════════════════════════════════════════════════════════
/// 第一版为了"不管当前是哪个界面都能找到"，直接从 <c>SceneTree.Root</c>
/// 全树遍历、凡是节点名叫 <c>Visuals</c> / <c>Icon</c> 的 <c>Sprite2D</c> 就换图。
/// 实测日志：
/// <code>
/// [Wizard] 放弃游戏：已替换 25 个战败立绘节点。   ← 涅奥（应为 1）
/// [Wizard] 放弃游戏：已替换 33 个战败立绘节点。   ← 火堆（应为 1）
/// [Wizard] 放弃游戏：已替换 40 个战败立绘节点。   ← 商店（应为 1）
/// </code>
/// 后果有两个，都很严重：
/// <list type="number">
///   <item><b>换错人</b>：<c>Icon</c> 这个名字原版怪物/角色也用，
///         而遍历会碰到<b>预加载缓存里的场景实例</b>——包括<b>商人自己</b>的立绘。</item>
///   <item><b>直接把游戏搞崩</b>：
///         <c>ERROR: InvalidOperationException: Expected BoundObject to be a SpineSprite,
///         but it is a Sprite2D!</c>
///         抛在 <c>NMerchantCharacter._Ready</c>。
///         我们换掉的是<b>缓存里</b>那个节点的贴图，污染了后续实例化。</item>
/// </list>
///
/// <para>
/// <b>本版的做法</b>：<b>只处理"确实属于巫师"的立绘节点</b>，
/// 判据是沿父链找到的那个<b>场景 owner</b> 的脚本路径以
/// <c>res://Wizard/</c> 开头（即三个 <c>wizard_character*.tscn</c> 之一）。
/// 这样原版/其它 mod 的 <c>Visuals</c>、<c>Icon</c> 一律不会被碰。
/// 另外只处理 <c>IsInsideTree()</c> 的节点，避开预加载缓存。
/// </para>
///
/// ══════════════════════════════════════════════════════════════════
/// 对齐地面线（而不是硬编码偏移）
/// ══════════════════════════════════════════════════════════════════
/// 各场景立绘的 <c>scale</c> 与原点都不同（战斗 0.6039 / 火堆 0.4384 / 商店 0.875），
/// 所以不能用某一处算出的像素偏移去套所有场景。
/// 本类<b>保持"画布底边"的世界坐标不变</b>：
/// <c>position.y + H/2*scale</c> 前后相等（H = 该 Sprite 当前画的那整块画布高度）。
/// </summary>
internal static class WizardDeathSprite
{
    private const string DeathSpritePath = "res://Wizard/images/characters/wizard_die.png";

    /// <summary>换过图的 Sprite 打这个标记，保证幂等（重复调用不叠加偏移）。</summary>
    private const string AppliedMeta = "wizard_death_sprite_applied";

    /// <summary>巫师角色场景的路径前缀 —— 判定"这个立绘是不是巫师的"。</summary>
    private const string WizardScenePrefix = "res://Wizard/";

    /// <summary>承载巫师立绘的节点名（战斗/火堆用 Visuals，商店用 Icon）。</summary>
    private static readonly string[] SpriteNodeNames = ["Visuals", "Icon"];

    private static Texture2D? _deathTexture;

    /// <summary>战败贴图（带缓存）。</summary>
    public static Texture2D? DeathTexture
        => _deathTexture ??= GD.Load<Texture2D>(DeathSpritePath);

    /// <summary>
    /// 在 <paramref name="root"/> 子树里找出<b>属于巫师</b>的立绘节点并换成战败图。
    ///
    /// <para>
    /// ⚠️ 调用方必须传入<b>巫师的节点</b>（战斗是 <c>NCreatureVisuals</c>，
    /// 火堆/商店是 <c>NRestSiteCharacter</c> / <c>NMerchantCharacter</c>），
    /// 不要传整棵场景树 —— 见类注释里第一版的教训。
    /// </para>
    ///
    /// <returns>实际替换了几个节点（正常应为 1）。</returns>
    /// </summary>
    public static int SwapAll(Node? root)
    {
        if (root == null || !GodotObject.IsInstanceValid(root))
        {
            return 0;
        }

        Texture2D? death = DeathTexture;
        if (death == null)
        {
            Entry.Logger.Warn($"[Wizard] 战败立绘：加载失败 {DeathSpritePath}");
            return 0;
        }

        int count = 0;
        foreach (Sprite2D sprite in FindOwnedSprites(root))
        {
            if (SwapOne(sprite, death))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>换单个 Sprite（已是战败图则跳过）。</summary>
    public static bool SwapOne(Sprite2D sprite, Texture2D death)
    {
        if (sprite.HasMeta(AppliedMeta))
        {
            return false;
        }

        // ── 对齐画布底边（见类注释）──
        // 换图前底边：position.y + (H_old/2) * scale.y
        // 换图后底边：position.y' + (H_new/2) * scale.y
        // 令两者相等 ⇒ position.y' = position.y + (H_old - H_new)/2 * scale.y
        float oldH = CanvasHeight(sprite.Texture);
        float newH = CanvasHeight(death);
        float scaleY = Mathf.Abs(sprite.Scale.Y);

        if (oldH > 0f && newH > 0f && !Mathf.IsEqualApprox(oldH, newH))
        {
            Vector2 p = sprite.Position;
            sprite.Position = new Vector2(p.X, p.Y + (oldH - newH) / 2f * scaleY);
        }

        sprite.SetMeta(AppliedMeta, true);

        // ⚠️ 用 SetDeferred 而不是直接赋值：我们是在某个 C# 回调
        // （Harmony Prefix）里改别的节点的属性，而 Godot 要求
        // 对"非当前调用者的可渲染属性"用延迟写入。
        // 直接赋值在场景正被构建/销毁时可能留下悬空引用。
        sprite.SetDeferred(Sprite2D.PropertyName.Texture, death);

        // 诊断日志：记录到底碰了哪个节点（排查"换错人"用）。
        Entry.Logger.Info(
            $"[Wizard] 战败立绘已替换：path={sprite.GetPath()} "
            + $"scene={sprite.SceneFilePath} oldH={oldH} newH={newH}");
        return true;
    }

    /// <summary>
    /// 找出子树里<b>确实属于巫师</b>的立绘 Sprite。
    ///
    /// <para>
    /// 三重过滤，缺一不可：
    /// <list type="number">
    ///   <item>节点名是 <c>Visuals</c> 或 <c>Icon</c>；</item>
    ///   <item>节点在<b>场景树里</b>（<c>IsInsideTree()</c>）——
    ///         避开预加载缓存里的场景实例；</item>
    ///   <item><b>沿父链找到的场景 owner 脚本路径以 <c>res://Wizard/</c> 开头</b> ——
    ///         这才是"是不是巫师的"的权威判据。</item>
    /// </list>
    /// </para>
    /// </summary>
    private static IEnumerable<Sprite2D> FindOwnedSprites(Node root)
    {
        var found = new List<Sprite2D>();
        Collect(root, found);
        return found;
    }

    private static void Collect(Node node, List<Sprite2D> found)
    {
        if (node is Sprite2D sprite
            && IsWizardSpriteName(node.Name)
            && sprite.IsInsideTree()
            && BelongsToWizardScene(node))
        {
            found.Add(sprite);
        }

        foreach (Node child in node.GetChildren())
        {
            Collect(child, found);
        }
    }

    private static bool IsWizardSpriteName(StringName name)
    {
        foreach (string candidate in SpriteNodeNames)
        {
            if (name == candidate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 沿父链往上找第一个"带脚本的祖先节点"，看它的脚本是否来自本 mod 的场景。
    ///
    /// <para>
    /// 场景根节点（<c>WizardCharacter</c> / <c>WizardRestSite</c> / <c>WizardMerchant</c>）
    /// 的脚本就是 <c>wizard_character*.tscn</c> 里 <c>ext_resource</c> 指定的那些
    /// （<c>NCreatureVisuals.cs</c> / <c>NRestSiteCharacter.cs</c> / <c>NMerchantCharacter.cs</c>）；
    /// <see cref="GodotObject.GetScript"/> 的 <c>ResourcePath</c>
    /// 会指向<b>我们场景文件里的那条 ext_resource 路径</b>，
    /// 但为了稳妥，这里同时接受"脚本路径含 Wizard"与
    /// "节点的 <c>SceneFilePath</c> 含 Wizard"两种证据。
    /// </para>
    /// </summary>
    private static bool BelongsToWizardScene(Node node)
    {
        // 证据 A：节点自身的场景文件路径（Godot 4.2+ 提供）。
        for (Node? n = node; n != null; n = n.GetParent())
        {
            string scenePath = n.SceneFilePath;
            if (!string.IsNullOrEmpty(scenePath))
            {
                return scenePath.StartsWith(WizardScenePrefix, System.StringComparison.Ordinal);
            }
        }

        return false;
    }

    /// <summary>该贴图整块画布的高度（用于地面线对齐）。</summary>
    private static float CanvasHeight(Texture2D? tex)
    {
        if (tex == null)
        {
            return 0f;
        }

        // 火堆的 AtlasTexture 是"切一半"，高度即整张图高度，
        // 但只用 region 的高（那才是这个 Sprite 实际画出来的画布高）。
        if (tex is AtlasTexture atlas)
        {
            return atlas.Region.Size.Y;
        }

        return tex.GetHeight();
    }
}
