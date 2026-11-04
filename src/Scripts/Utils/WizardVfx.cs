using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx.Forms;

namespace Wizard.Scripts.Utils;

/// <summary>
/// 从<b>原版游戏</b>借用的演出片段（不新增任何美术/音频资源）。
///
/// <para>
/// 每个方法都对应原版某张卡/某个球的演出，注释里写明<b>出处文件</b>与原始代码，
/// 方便以后对照原版改动。完整候选清单见 <c>docs/VFX_SOURCES.md</c>。
/// </para>
///
/// <para>
/// ⚠️ 三条统一约定：
/// <list type="number">
///   <item>任何效果失败都只写一条 <c>演出：…</c> 日志，<b>绝不打断卡牌结算</b>；</item>
///   <item>节点 <c>Create</c> 返回 null（资源没加载上）时也会记日志，便于排查"没效果"；</item>
///   <item>除 <see cref="PlayLargeMagicMissile"/> / <see cref="GrandFinale"/> /
///         <see cref="EchoFormPulse"/> 这些<b>需要等演出播完</b>的以外，一律<b>不 await</b>，
///         避免拖慢战斗节奏。</item>
/// </list>
/// </para>
/// </summary>
public static class WizardVfx
{
    // ══════════════════════════════════════════════════════════════
    // 配色 / 音效 / 特效名
    // ══════════════════════════════════════════════════════════════

    /// <summary>原版「末日降临」<c>EndOfDays</c> 的深绯色。</summary>
    public static readonly Color EndOfDaysTint = new("8c2447");

    /// <summary>冰系染色（浅青蓝）。</summary>
    public static readonly Color IceTint = new("#8fd8ff");

    /// <summary>火焰系染色（橙红）。</summary>
    public static readonly Color EmberTint = new("#ff7a2f");

    /// <summary>奥术系染色（紫）。</summary>
    public static readonly Color ArcaneTint = new("#b06cff");

    /// <summary>金色（用于「真理派对」这类"审判/神圣"演出）。</summary>
    public static readonly Color GoldTint = new("#ffd76e");

    /// <summary>原版熔岩拳（我们火焰攻击一直在用）。</summary>
    public const string MoltenFistFx = "vfx/vfx_molten_fist";

    /// <summary>原版星光冲击（我们冰/雷杂项一直在用）。</summary>
    public const string StarryImpactFx = "vfx/vfx_starry_impact";

    /// <summary>
    /// 夸张刺击（原版「背刺」「暗杀」在用）。
    /// 出处：<c>Backstab.cs</c> / <c>Assassinate.cs</c> → <c>.WithHitFx("vfx/vfx_dramatic_stab")</c>。
    /// </summary>
    public const string DramaticStabFx = "vfx/vfx_dramatic_stab";

    /// <summary>重型钝击（原版「全力一击」AllForOne / Bludgeon 在用）。</summary>
    public const string HeavyBluntFx = "vfx/vfx_heavy_blunt";

    /// <summary>
    /// 火焰攻击的通用音效。
    /// 出处：<c>Brand.cs</c> / <c>Burn.cs</c> / <c>FiendFire.cs</c> →
    /// <c>"event:/sfx/characters/attack_fire"</c>。
    /// </summary>
    public const string FireHitSfx = "event:/sfx/characters/attack_fire";

    /// <summary>
    /// 原版故障机器人「闪电球」<b>激发</b>时的打击特效。
    /// 出处：<c>LightningOrb.ApplyLightningDamage</c> → <c>VfxCmd.PlayOnCreature(item, "vfx/vfx_attack_lightning")</c>。
    /// </summary>
    public const string LightningOrbImpact = "vfx/vfx_attack_lightning";

    /// <summary>
    /// 原版闪电球<b>激发</b>音效。
    /// 出处：<c>LightningOrb.EvokeSfx</c> → <c>"event:/sfx/characters/defect/defect_lightning_evoke"</c>。
    /// </summary>
    public const string LightningOrbEvokeSfx = "event:/sfx/characters/defect/defect_lightning_evoke";

    // ══════════════════════════════════════════════════════════════
    // 挂载基础设施
    // ══════════════════════════════════════════════════════════════

    /// <summary>把节点挂到战斗特效层（<c>NCombatRoom.CombatVfxContainer</c>）。</summary>
    private static void Attach(Node? node, string what)
    {
        if (node == null)
        {
            Entry.Logger.Warn($"演出：{what} 创建失败（资源可能没加载上）");
            return;
        }

        try
        {
            Control? container = NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null)
            {
                node.QueueFree();
                Entry.Logger.Warn($"演出：{what} 找不到战斗特效层，跳过");
                return;
            }

            container.AddChildSafely(node);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：{what} 挂载失败：{e.Message}");
        }
    }

    /// <summary>把节点挂到全局 UI 层（整屏特效：烟雾暗角、屏幕手等）。</summary>
    private static void AttachToGlobalUi(Node? node, string what)
    {
        if (node == null)
        {
            Entry.Logger.Warn($"演出：{what} 创建失败（资源可能没加载上）");
            return;
        }

        try
        {
            Node? ui = NRun.Instance?.GlobalUi;
            if (ui == null)
            {
                node.QueueFree();
                Entry.Logger.Warn($"演出：{what} 找不到全局 UI 层，跳过");
                return;
            }

            ui.AddChildSafely(node);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：{what} 挂载失败：{e.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 火焰系
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 目标脚下燃起地火（持续一小会儿）。
    /// 出处：原版「燃烧」<c>Burn</c> / 「烙印」<c>Brand</c> / <c>Conflagration</c> / <c>Inflame</c>。
    ///
    /// <para>
    /// ⚠️ <b>只支持 Red / Green / Blue / Purple / Black / White</b>：
    /// <c>NGroundFireVfx.ApplyColor</c> 的 <c>switch</c> 里
    /// <c>default: throw new ArgumentOutOfRangeException()</c>
    /// —— 传 <c>Cyan</c> 会直接抛异常（<c>_Ready</c> 里炸，特效不显示还刷错误）。
    /// 这里做一层防呆：不认识的颜色一律回落成 Blue（寒气色）。
    /// </para>
    /// </summary>
    public static void GroundFire(Creature target, VfxColor color = VfxColor.Red)
    {
        VfxColor safe = color switch
        {
            VfxColor.Red or VfxColor.Green or VfxColor.Blue or VfxColor.Purple
                or VfxColor.Black or VfxColor.White => color,
            _ => VfxColor.Blue,
        };

        Attach(NGroundFireVfx.Create(target, safe), nameof(NGroundFireVfx));
    }

    /// <summary>
    /// 目标身上持续燃烧的光效。
    /// 出处：原版「火焰屏障」<c>FlameBarrier</c>（<c>Create(creature, 0.75f, goingRight: false)</c>）。
    /// </summary>
    public static void FireBurning(Creature target, float scale = 0.75f)
        => Attach(NFireBurningVfx.Create(target, scale, goingRight: false), nameof(NFireBurningVfx));

    /// <summary>
    /// 熄灭的一团烟。
    /// 出处：<c>InfernoPower</c> / <c>TheBombPower</c> / <c>ExplosiveAmpoule</c>。
    /// </summary>
    public static void FireSmoke(Creature target)
        => Attach(NFireSmokePuffVfx.Create(target), nameof(NFireSmokePuffVfx));

    // ══════════════════════════════════════════════════════════════
    // 雷电系
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 从施法者射向目标的粗光束 + 命中爆点。
    /// 出处：原版「超能光束」<c>Hyperbeam</c> →
    /// <c>NHyperbeamVfx.Create(owner, target)</c> + <c>NHyperbeamImpactVfx.Create(owner, target)</c>。
    /// </summary>
    public static void Hyperbeam(Creature owner, Creature target)
    {
        Attach(NHyperbeamVfx.Create(owner, target), nameof(NHyperbeamVfx));
        Attach(NHyperbeamImpactVfx.Create(owner, target), nameof(NHyperbeamImpactVfx));
    }

    /// <summary>
    /// 横向扫射光束（可一次扫过多个目标）。
    /// 出处：原版「横扫光束」<c>SweepingBeam</c> →
    /// <c>NSweepingBeamVfx.Create(owner, List&lt;Creature&gt;)</c>。
    /// </summary>
    public static void SweepingBeam(Creature owner, IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        Attach(NSweepingBeamVfx.Create(owner, targets.ToList()), nameof(NSweepingBeamVfx));
        foreach (Creature target in targets)
        {
            Attach(NSweepingBeamImpactVfx.Create(target), nameof(NSweepingBeamImpactVfx));
        }
    }

    /// <summary>
    /// 目标处线条炸开。
    /// 出处：原版祭典兽 <c>CeremonialBeast</c>（<c>NLineBurstVfx.Create(target)</c>）。
    /// </summary>
    public static void LineBurst(Creature target)
        => Attach(NLineBurstVfx.Create(target), nameof(NLineBurstVfx));

    // ══════════════════════════════════════════════════════════════
    // 冰霜系
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 重锤从"举起"到"砸到地面"的时间。
    ///
    /// <para>
    /// 出处：<c>NHeavyBluntVfx.PlaySequence</c>（重锤特效自己的脚本）：
    /// <code>
    /// 重启 _anticipationParticles（前摇粒子：举起的烟尘/预兆）
    /// await WaitForSeconds(0.2f);                 // ← 0.2 秒后才"落下"
    /// NGame.ScreenShake(Strong, Short);           // 震屏
    /// 重启 _impactParticles（冲击粒子）
    /// </code>
    /// 所以特效的"砸到"发生在生成后 <b>0.2 秒</b>。
    /// </para>
    /// </summary>
    public const float HeavyBluntFallDuration = 0.2f;

    /// <summary>
    /// 让一组目标先各自挨一记「重锤」特效（原版 Bludgeon / AllForOne / Break 用的
    /// <c>vfx/vfx_heavy_blunt</c>），并<b>等它真的砸到</b>之后再返回。
    ///
    /// <para>
    /// 为什么不直接 <c>.WithHitFx("vfx/vfx_heavy_blunt")</c>：
    /// 那样特效是在**伤害结算那一刻**才开始播的，于是"伤害数字/闪光先出、锤子后落"，
    /// 与用户的观感要求相反。这里改为**先播特效、等 0.2 秒、再结算伤害**。
    /// </para>
    ///
    /// <para>
    /// 挂载位置用 <c>VfxCmd.PlayOnCreature</c>（= 生物的**脚下**），
    /// 与 Bludgeon 的 <c>WithHitVfxSpawnedAtBase()</c> 一致。
    /// </para>
    /// </summary>
    public static async Task HeavyBluntRain(IReadOnlyList<Creature> targets)
    {
        bool played = false;

        foreach (Creature target in targets)
        {
            try
            {
                VfxCmd.PlayOnCreature(target, VfxCmd.heavyBluntPath);
                played = true;
            }
            catch (Exception e)
            {
                Entry.Logger.Warn($"演出：重锤特效播放失败（不影响结算）：{e.Message}");
            }
        }

        if (!played)
        {
            return;
        }

        try
        {
            await Cmd.Wait(HeavyBluntFallDuration);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：等待重锤落地时异常（不影响结算）：{e.Message}");
        }
    }

    /// <summary>
    /// 小型魔法飞弹从**生成到砸到**的总时长。
    ///
    /// <para>
    /// 实测（对照 <c>reference/archive/game-recovery/scenes/vfx/vfx_small_magic_missile.tscn</c>）：
    /// 场景**没有**覆写这两个值，所以用类里的默认值：
    /// <code>
    /// private float _anticipationDuration = 0.2f;   // 前摇（天空射线 + 落点预兆）
    /// public  float WaitTime            = 0.2f;    // 弹体飞行
    /// </code>
    /// 合计 <b>0.4 秒</b>后冲击粒子才开始、屏幕才震 —— 这时才算"砸到"。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 原版「彗星」「指引之星」只 <c>await Cmd.Wait(vfx.WaitTime)</c>（0.2 秒），
    /// 也就是**它们的伤害早于落地**。我们按用户要求改成等满 0.4 秒，
    /// 让"烟尘/冲击"与命中闪光、伤害结算同时发生。
    /// </para>
    /// </summary>
    public const float SmallMissileFallDuration = 0.4f;

    /// <summary>
    /// 给一组目标同时落小魔法飞弹，并**等它们真的砸到**再返回。
    ///
    /// <para>
    /// 出处：原版「彗星」<c>Comet</c> / 「指引之星」<c>GuidingStar</c> →
    /// <c>NSmallMagicMissileVfx.Create(nCreature.GetBottomOfHitbox(), tint)</c>；
    /// 这里改用 <c>NCreature.VfxSpawnPosition</c>（原版「泡泡」用的那个），
    /// 对自绘立绘的巫师也有效，不依赖 spine。
    /// </para>
    /// </summary>
    public static async Task SmallMissileRain(IReadOnlyList<Creature> targets, Color tint)
    {
        bool spawned = false;

        foreach (Creature target in targets)
        {
            try
            {
                NCreature? node = NCombatRoom.Instance?.GetCreatureNode(target);
                if (node == null)
                {
                    Entry.Logger.Warn($"演出：{nameof(NSmallMagicMissileVfx)} 取不到 {target?.Name} 的节点，跳过");
                    continue;
                }

                NSmallMagicMissileVfx? vfx = NSmallMagicMissileVfx.Create(node.VfxSpawnPosition, tint);
                if (vfx == null)
                {
                    Entry.Logger.Warn($"演出：{nameof(NSmallMagicMissileVfx)} 创建失败（资源可能没加载上）");
                    continue;
                }

                Attach(vfx, nameof(NSmallMagicMissileVfx));
                spawned = true;
            }
            catch (Exception e)
            {
                Entry.Logger.Warn($"演出：{nameof(NSmallMagicMissileVfx)} 播放失败（不影响结算）：{e.Message}");
            }
        }

        if (!spawned)
        {
            return;
        }

        try
        {
            await Cmd.Wait(SmallMissileFallDuration);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：等待小魔法飞弹落地时异常（不影响结算）：{e.Message}");
        }
    }

    /// <summary>
    /// 极细的一闪切痕（可染色；冰系用 <see cref="VfxColor.Cyan"/>）。
    /// 出处：原版「斩击」<c>Slice</c> → <c>NThinSliceVfx.Create(target, VfxColor.Red)</c>。
    /// </summary>
    public static void ThinSlice(Creature? target, VfxColor color = VfxColor.Cyan)
        => Attach(NThinSliceVfx.Create(target, color), nameof(NThinSliceVfx));

    /// <summary>
    /// 尖刺/碎冰飞溅。
    /// 出处：原版 <c>CrushUnder</c> / <c>Stomp</c> → <c>NSpikeSplashVfx.Create(target, color)</c>。
    ///
    /// <para>
    /// 默认色 **Cyan**（冰蓝）—— 与 <see cref="GroundFire"/> 不同，
    /// <c>NSpikeSplashVfx</c> 只是把颜色往下传给
    /// <c>NFgGroundSpikeVfx</c>/<c>NBgGroundSpikeVfx</c> 的 <c>_vfxColor</c>，
    /// **没有会 throw 的 switch**，所以 Cyan 可以安全使用。
    /// </para>
    /// </summary>
    public static void SpikeSplash(Creature target, VfxColor color = VfxColor.Cyan)
        => Attach(NSpikeSplashVfx.Create(target, color), nameof(NSpikeSplashVfx));

    /// <summary>
    /// 在目标身上盖一层液体/冰壳。
    /// 出处：原版「弹跳药瓶」<c>BouncingFlask</c>（<c>NLiquidOverlayVfx.Create(target, tint)</c>）。
    ///
    /// <para>
    /// ⚠️ <b>只对原版 spine 角色有效</b>：它内部只是
    /// <c>CreatureNode.Visuals.TryApplyLiquidOverlay(tint)</c>，
    /// 去找立绘上的"液体槽"。巫师是**自绘立绘**，没有那个槽 ⇒
    /// 实测**完全没有效果**（2026-11-03「冰铠」踩过，已改用 <see cref="GroundFire"/> 蓝色地火）。
    /// </para>
    /// </summary>
    public static void LiquidOverlay(Creature? target, Color tint)
        => Attach(NLiquidOverlayVfx.Create(target, tint), nameof(NLiquidOverlayVfx));

    // ══════════════════════════════════════════════════════════════
    // 奥术 / 整屏演出
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 整屏横向扫描线（Whirlwind 大招那种）。
    /// 出处：原版「大风车」<c>Whirlwind</c> →
    /// <c>CombatVfxContainer.AddChildSafely(NHorizontalLinesVfx.Create(color, 0.8 + …))</c>。
    /// </summary>
    public static void HorizontalLines(Color color, double duration = 1.2)
        => Attach(NHorizontalLinesVfx.Create(color, duration), nameof(NHorizontalLinesVfx));

    /// <summary>
    /// 整屏烟雾暗角。
    /// 出处：原版「噩梦」<c>Nightmare</c>（挂 <c>NRun.Instance.GlobalUi</c>）。
    /// </summary>
    public static void SmokyVignette(Color tint, Color highlight)
        => AttachToGlobalUi(NSmokyVignetteVfx.Create(tint, highlight), nameof(NSmokyVignetteVfx));

    /// <summary>
    /// 屏幕里伸出大量手。
    /// 出处：原版「噩梦」<c>Nightmare</c>（<c>NNightmareHandsVfx.Create()</c>，挂 GlobalUi）。
    /// </summary>
    public static void NightmareHands()
        => AttachToGlobalUi(NNightmareHandsVfx.Create(), nameof(NNightmareHandsVfx));

    /// <summary>
    /// 盛大收束光效（有前摇，需要等）。
    /// 出处：原版「终幕」<c>GrandFinale</c> →
    /// <c>NGrandFinaleVfx.Create(owner)</c> + <c>await Cmd.Wait(totalAnticipationDuration)</c>。
    /// </summary>
    public static async Task GrandFinale(Creature owner)
    {
        try
        {
            NGrandFinaleVfx? vfx = NGrandFinaleVfx.Create(owner);
            if (vfx == null)
            {
                Entry.Logger.Warn($"演出：{nameof(NGrandFinaleVfx)} 创建失败（资源可能没加载上）");
                return;
            }

            Control? container = NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null)
            {
                vfx.QueueFree();
                return;
            }

            container.AddChildSafely(vfx);
            await Cmd.Wait(NGrandFinaleVfx.totalAnticipationDuration);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：{nameof(NGrandFinaleVfx)} 播放失败（不影响结算）：{e.Message}");
        }
    }

    /// <summary>
    /// 原版「回声形态」的光环，**一次性**播 2 秒后自动移除。
    ///
    /// <para>
    /// 出处：<c>EchoFormPower</c> → <c>NEchoFormVfx.Create(base.Owner)</c>。
    /// 原版是**常驻**光环（能力在就一直在），但那是绑在原版角色 spine 骨骼上的；
    /// 巫师是自绘立绘，先做成一次性脉冲最安全 —— 若实测没问题，再考虑改成常驻。
    /// </para>
    /// </summary>
    public static async Task EchoFormPulse(Creature owner, float seconds = 2f)
    {
        NEchoFormVfx? vfx = null;
        try
        {
            vfx = NEchoFormVfx.Create(owner);
            if (vfx == null)
            {
                Entry.Logger.Warn($"演出：{nameof(NEchoFormVfx)} 创建失败（资源可能没加载上）");
                return;
            }

            Control? container = NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null)
            {
                vfx.QueueFree();
                return;
            }

            container.AddChildSafely(vfx);

            await Cmd.Wait(seconds);

            if (vfx.IsValid())
            {
                vfx.QueueFree();
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：{nameof(NEchoFormVfx)} 播放失败（不影响结算）：{e.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 大型魔法飞弹（末日降临）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 原版「末日降临」<c>EndOfDays</c> 的演出：
    /// 在<b>敌方半场中心的地面</b>砸下一枚大型魔法飞弹，等它演完再继续结算。
    ///
    /// <para>
    /// 出处：<c>EndOfDays.OnPlay</c>
    /// <code>
    /// Vector2? sideCenterFloor = VfxCmd.GetSideCenterFloor(CombatSide.Enemy, base.CombatState);
    /// if (sideCenterFloor.HasValue) {
    ///     NLargeMagicMissileVfx vfx = NLargeMagicMissileVfx.Create(sideCenterFloor.Value, new Color("8c2447"));
    ///     if (vfx != null) {
    ///         NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(vfx);
    ///         await Cmd.Wait(vfx.WaitTime);
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="combatState">当前战斗状态；为 null 时不做任何事。</param>
    /// <param name="tint">飞弹配色；默认用末日降临那一档深绯。</param>
    public static async Task PlayLargeMagicMissile(ICombatState? combatState, Color? tint = null)
    {
        if (combatState == null)
        {
            return;
        }

        try
        {
            NCombatRoom? room = NCombatRoom.Instance;
            if (room == null)
            {
                return;
            }

            Vector2? floor = VfxCmd.GetSideCenterFloor(CombatSide.Enemy, combatState);
            if (floor is not { } position)
            {
                return;
            }

            NLargeMagicMissileVfx? vfx = NLargeMagicMissileVfx.Create(position, tint ?? EndOfDaysTint);
            if (vfx == null)
            {
                return;
            }

            room.CombatVfxContainer.AddChildSafely(vfx);
            await Cmd.Wait(vfx.WaitTime);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"演出：大型魔法飞弹播放失败（不影响结算）：{e}");
        }
    }
}
