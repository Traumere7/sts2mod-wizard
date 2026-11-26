using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Wizard.Scripts.Character;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 巫师 Godot 原生 2D 骨骼动画（<c>AnimationPlayer</c>）状态控制器。
///
/// <para>
/// 负责处理 BaseLib <c>CustomAnimation</c> 没覆盖到的五件实战刚需：
/// <list type="number">
///   <item><b>接管优先级（<c>Priority.First</c>）</b>：BaseLib 的
///         <c>CustomAnimationPatch.SendTriggerToOtherAnimators</c> 同样是
///         <c>NCreature.SetAnimationTrigger</c> 的 Prefix 且返回 <c>false</c>；
///         必须抢在 BaseLib 之前接管，否则本控制器的逻辑会被 BaseLib 静默短路。</item>
///   <item><b>全卡池单牌单动作规范（去重门控）</b>：原版及其它角色/Mod 卡池里，
///         不少卡牌会在同一张牌的 <c>OnPlay</c> 里连续触发多个动作（如死灵术士的
///         <c>Fear</c> 先 <c>Attack</c> 后 <c>Cast</c>、<c>Coda / CrushUnder / Stomp</c>
///         先 <c>Cast</c> 后 <c>Attack</c>、<c>SweepingBeam</c> 先 <c>Attack</c> 后 <c>Cast</c>、
///         <c>DramaticEntrance / DyingStar / FlickFlack</c> 连续两次 <c>Attack</c>，
///         以及 40+ 张跨职业多段攻击牌每段都触发 <c>Attack</c>）。
///         借助 <c>CombatHistory.CardPlayStarted / CardPlayFinished</c> 按玩家维护
///         <c>CardPlay</c> 栈，确保每次出牌最多触发一次主动动作（<c>attack / cast / powerup</c>），
///         同时不拦截被动状态（<c>hurt / die / revive / idle</c>），也不影响回响/重放或嵌套自动打出。</item>
///   <item><b>跨职业触发器归一化</b>：把静默猎手 <c>Shiv</c>、铁甲战士 <c>heavyAttack</c>、
///         愚者 <c>shootBow</c>、死灵术士 <c>summonTrigger</c>、摄政王 <c>sovereignBladeTrigger</c>
///         等所有职业专属触发器统一映射回巫师的 6 个标准动作。</item>
///   <item><b>离散显隐零残影硬复位（同时修复 A 版与 B 版）</b>：Godot 4 的
///         <c>AnimationPlayer</c> 在执行非零混合（<c>SetBlendTime &gt; 0</c>）切换时，
///         会跳过目标动画 <c>t = 0.0</c> 处的离散轨道关键帧（<c>update = 1</c>，如 <c>:visible</c>），
///         导致前一个动作的替换帧（B 版 <c>PoseAtk2</c> 与 <c>PoseCast2</c> 叠影、
///         A 版 <c>powerup</c> 被打断后 <c>RightArmP4</c> 僵死且法杖悬空）。
///         本控制器自动扫描每个动作是否含多关键帧 <c>:visible</c> 切换轨道：
///         纯连续骨骼过渡（A 版 <c>idle ↔ attack / cast / hurt</c>、B 版 <c>idle ↔ hurt</c>）
///         保留顺滑混合；任何涉及离散显隐切换的动作打断一律以 <c>blend = 0.0</c>
///         先 <c>RESET</c> 再起播目标动作，从根源杜绝双立绘叠影与残余特效。</item>
///   <item><b>死亡锁定与复活解锁</b>：进入 <c>die</c> 后忽略后续残余 <c>Hit / Idle</c> 触发，
///         且幂等防重复归零；<c>StartReviveAnim</c> 时清除锁并切回 <c>idle</c>。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ 仅对<b>骨骼版巫师场景</b>（<c>Visuals</c> 下同时含 <c>Parts</c>、<c>Skeleton2D</c>、
/// <c>AnimationPlayer</c>）生效；静态回退场景与其它角色完全放行，零副作用。
/// </para>
/// </summary>
internal static class WizardAnimController
{
    private const string ReadyMeta = "wizard_anim_ready";
    private const string DeadMeta = "wizard_anim_dead";
    private const string DiscreteAnimsMeta = "wizard_discrete_anims";

    private static readonly string[] OneShotAnims = ["attack", "cast", "powerup", "hurt"];

    private sealed class CardPlayAnimGate
    {
        public required CardPlay CardPlay { get; init; }
        public bool ActionPlayed { get; set; }
    }

    /// <summary>
    /// 按玩家 NetId 维护当前活跃的出牌栈（支持多人双巫师互不干扰、支持嵌套 AutoPlay、支持回响逐次出牌）。
    /// </summary>
    private static readonly Dictionary<ulong, List<CardPlayAnimGate>> ActiveCardPlays = new();

    /// <summary>尝试从战斗角色视觉节点中取出骨骼版 AnimationPlayer。</summary>
    public static bool TryGetRiggedAnimPlayer(Node? visuals, out AnimationPlayer anim)
    {
        anim = null!;
        if (visuals == null || !GodotObject.IsInstanceValid(visuals))
        {
            return false;
        }

        Node? body = visuals.Name == "Visuals"
            ? visuals
            : visuals.GetNodeOrNull<Node>("%Visuals") ?? visuals.GetNodeOrNull<Node>("Visuals");

        if (body == null || body is Sprite2D)
        {
            return false;
        }

        if (body.GetNodeOrNull<Node2D>("Parts") == null || body.GetNodeOrNull<Skeleton2D>("Skeleton2D") == null)
        {
            return false;
        }

        if (body.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is not { } player)
        {
            return false;
        }

        anim = player;
        return true;
    }

    /// <summary>登记一次巫师出牌开始（推入新的单动作门控帧）。</summary>
    public static void OnCardPlayStarted(CardPlay? cardPlay)
    {
        Player? player = cardPlay?.Player;
        if (cardPlay == null || player == null || !WizardCharacter.IsWizardPlayer(player))
        {
            return;
        }

        if (!ActiveCardPlays.TryGetValue(player.NetId, out List<CardPlayAnimGate>? stack))
        {
            stack = new List<CardPlayAnimGate>(4);
            ActiveCardPlays[player.NetId] = stack;
        }

        stack.Add(new CardPlayAnimGate { CardPlay = cardPlay });
    }

    /// <summary>登记一次巫师出牌结束（按引用安全弹栈，防止异常/中途死亡导致栈错位）。</summary>
    public static void OnCardPlayFinished(CardPlay? cardPlay)
    {
        Player? player = cardPlay?.Player;
        if (cardPlay == null || player == null || !ActiveCardPlays.TryGetValue(player.NetId, out List<CardPlayAnimGate>? stack))
        {
            return;
        }

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(stack[i].CardPlay, cardPlay))
            {
                stack.RemoveRange(i, stack.Count - i);
                break;
            }
        }

        if (stack.Count == 0)
        {
            ActiveCardPlays.Remove(player.NetId);
        }
    }

    /// <summary>清空所有出牌门控栈（战斗切换/历史清空时调用）。</summary>
    public static void ClearAllCardPlayGates()
    {
        ActiveCardPlays.Clear();
    }

    /// <summary>清空单个玩家的出牌门控栈（死亡/复活时调用）。</summary>
    public static void ClearPlayerCardPlayGate(ulong? playerNetId)
    {
        if (playerNetId.HasValue)
        {
            ActiveCardPlays.Remove(playerNetId.Value);
        }
    }

    /// <summary>
    /// 把原版/跨职业/第三方 Mod 的动画触发器名归一化为巫师的 6 个标准动作名。
    /// </summary>
    internal static string NormalizeTrigger(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            return string.Empty;
        }

        switch (trigger)
        {
            case "Idle" or "idle":
                return "idle";
            case "Attack" or "attack" or "Shiv" or "shiv" or "heavyAttack" or "shootBow":
                return "attack";
            case "Cast" or "cast" or "summonTrigger" or "sovereignBladeTrigger":
                return "cast";
            case "PowerUp" or "powerup" or "process":
                return "powerup";
            case "Hit" or "hit" or "Hurt" or "hurt":
                return "hurt";
            case "Dead" or "dead" or "Die" or "die":
                return "die";
            case "Revive" or "revive":
                return "revive";
        }

        string lower = trigger.ToLowerInvariant();
        if (lower.Contains("dead") || lower.Contains("die"))
        {
            return "die";
        }

        if (lower.Contains("revive"))
        {
            return "revive";
        }

        if (lower.Contains("hit") || lower.Contains("hurt"))
        {
            return "hurt";
        }

        if (lower.Contains("power") || lower.Contains("buff") || lower.Contains("charge"))
        {
            return "powerup";
        }

        if (lower.Contains("cast") || lower.Contains("summon") || lower.Contains("chant"))
        {
            return "cast";
        }

        if (lower.Contains("attack") || lower.Contains("slash") || lower.Contains("strike")
            || lower.Contains("shoot") || lower.Contains("shiv") || lower.Contains("poke"))
        {
            return "attack";
        }

        if (lower.Contains("idle"))
        {
            return "idle";
        }

        return lower;
    }

    private static bool IsCardActionAnim(string target)
        => target is "attack" or "cast" or "powerup";

    /// <summary>
    /// 扫描 AnimationPlayer 中所有会在播放过程中切换 <c>:visible</c> 的动画
    /// （如 A 版的 <c>powerup / die</c>、B 版的 <c>attack / cast / powerup / die</c>）。
    /// </summary>
    private static HashSet<string> DetectDiscreteVisibilityAnims(AnimationPlayer anim)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string animName in anim.GetAnimationList())
        {
            if (animName == "RESET")
            {
                continue;
            }

            Animation? resource = anim.GetAnimation(animName);
            if (resource == null)
            {
                continue;
            }

            int trackCount = resource.GetTrackCount();
            for (int i = 0; i < trackCount; i++)
            {
                string path = resource.TrackGetPath(i).ToString();
                if (!path.EndsWith(":visible", StringComparison.Ordinal))
                {
                    continue;
                }

                if (resource.TrackGetKeyCount(i) > 1)
                {
                    result.Add(animName);
                    break;
                }
            }
        }

        return result;
    }

    private static bool IsDiscreteVisibilityAnim(AnimationPlayer anim, string animName)
    {
        if (string.IsNullOrEmpty(animName) || !anim.HasMeta(DiscreteAnimsMeta))
        {
            return false;
        }

        string packed = anim.GetMeta(DiscreteAnimsMeta).AsString();
        return packed.Contains($"|{animName}|", StringComparison.Ordinal);
    }

    /// <summary>初始化混合时间与播完自动回位信号（幂等）。</summary>
    public static void EnsureInitialized(AnimationPlayer anim)
    {
        if (anim.HasMeta(ReadyMeta))
        {
            return;
        }

        anim.SetMeta(ReadyMeta, true);

        HashSet<string> discreteAnims = DetectDiscreteVisibilityAnims(anim);
        string packedDiscrete = discreteAnims.Count > 0
            ? "|" + string.Join("|", discreteAnims) + "|"
            : string.Empty;
        anim.SetMeta(DiscreteAnimsMeta, packedDiscrete);

        // 仅在【起止双方均不切换 :visible】的纯连续骨骼过渡上启用平滑混合时间：
        //   - A 版：idle ↔ attack / cast / hurt 与 attack / cast / hurt 互切保持平滑混合；
        //           凡涉及 powerup 或 die 的切换全部保持 0.0s 硬切（避免 t=0 离散显隐被吞）。
        //   - B 版：idle ↔ hurt 保持平滑混合；
        //           凡涉及 attack / cast / powerup / die 的切换全部保持 0.0s 硬切。
        SetSafeBlend(anim, discreteAnims, "idle", "attack", 0.05);
        SetSafeBlend(anim, discreteAnims, "attack", "idle", 0.08);
        SetSafeBlend(anim, discreteAnims, "idle", "cast", 0.06);
        SetSafeBlend(anim, discreteAnims, "cast", "idle", 0.10);
        SetSafeBlend(anim, discreteAnims, "idle", "powerup", 0.06);
        SetSafeBlend(anim, discreteAnims, "powerup", "idle", 0.12);
        SetSafeBlend(anim, discreteAnims, "idle", "hurt", 0.02);
        SetSafeBlend(anim, discreteAnims, "hurt", "idle", 0.10);

        foreach (string from in OneShotAnims)
        {
            SetSafeBlend(anim, discreteAnims, from, "hurt", 0.02);
            SetSafeBlend(anim, discreteAnims, from, "attack", 0.04);
            SetSafeBlend(anim, discreteAnims, from, "cast", 0.05);
            SetSafeBlend(anim, discreteAnims, from, "powerup", 0.05);
            SetSafeBlend(anim, discreteAnims, from, "die", 0.03);
        }

        SetSafeBlend(anim, discreteAnims, "idle", "die", 0.03);
        SetSafeBlend(anim, discreteAnims, "relaxed_loop", "die", 0.04);

        // 一次性动作自然播完后自动回到 idle；die 播完保持末帧定格。
        // 注意：自然播完时离散显隐轨道已回到与 idle 完全一致的静息态，此时显式传 0.08s 混合可平滑接回呼吸。
        anim.AnimationFinished += (StringName finishedName) =>
        {
            if (!GodotObject.IsInstanceValid(anim))
            {
                return;
            }

            if (anim.HasMeta(DeadMeta))
            {
                return;
            }

            string name = finishedName.ToString();
            if (name is "die" or "idle" or "RESET")
            {
                return;
            }

            if (anim.HasAnimation("idle"))
            {
                anim.Play("idle", customBlend: 0.08);
            }
        };
    }

    /// <summary>按引擎触发器名播放对应骨骼动画（带单牌单动作去重与零残影硬复位）。</summary>
    public static void PlayTrigger(AnimationPlayer anim, string trigger, ulong? playerNetId = null)
    {
        EnsureInitialized(anim);

        string target = NormalizeTrigger(trigger);
        if (string.IsNullOrEmpty(target))
        {
            return;
        }

        if (target == "revive")
        {
            ClearPlayerCardPlayGate(playerNetId);
            Revive(anim);
            return;
        }

        if (target == "die")
        {
            ClearPlayerCardPlayGate(playerNetId);
            PlayDie(anim);
            return;
        }

        // 死亡锁定期间屏蔽后续残余 Hit/Idle 等触发
        if (anim.HasMeta(DeadMeta))
        {
            return;
        }

        if (!anim.HasAnimation(target))
        {
            return;
        }

        // 全卡池单牌单动作去重：
        // 若当前正处于某张牌的 CardPlay 执行期内，且该次出牌已经播放过一次主动动作
        // （attack / cast / powerup），则忽略同一次出牌后续重复/冲突的主动动作触发
        // （例如 Fear 的 Attack→Cast、Coda/CrushUnder/Stomp 的 Cast→Attack、
        // SweepingBeam 的 Attack→Cast、多段跨职业攻击的连续 Attack）。
        // 被动状态（hurt / die / revive / idle）绝不受此门控限制。
        if (IsCardActionAnim(target)
            && playerNetId.HasValue
            && ActiveCardPlays.TryGetValue(playerNetId.Value, out List<CardPlayAnimGate>? stack)
            && stack.Count > 0)
        {
            CardPlayAnimGate top = stack[^1];
            if (top.ActionPlayed)
            {
                return;
            }

            top.ActionPlayed = true;
        }

        string current = anim.CurrentAnimation.ToString();
        bool isSameOneShot = target != "idle" && current == target && anim.IsPlaying();
        bool involvesDiscrete = IsDiscreteVisibilityAnim(anim, current) || IsDiscreteVisibilityAnim(anim, target);

        if (isSameOneShot || involvesDiscrete)
        {
            PlayWithHardReset(anim, target);
            return;
        }

        anim.Play(target);
    }

    /// <summary>
    /// 硬复位播放：先停播并立刻应用 <c>RESET</c> 清空所有离散显隐与残余特效，
    /// 再以 <c>blend = 0.0</c> 从第 0 帧精准起播目标动画。
    /// </summary>
    private static void PlayWithHardReset(AnimationPlayer anim, string target)
    {
        anim.Stop(keepState: false);
        if (anim.HasAnimation("RESET"))
        {
            anim.Play("RESET", customBlend: 0.0);
            anim.Advance(0.0);
        }

        anim.Play(target, customBlend: 0.0);
        anim.Seek(0.0, update: true);
    }

    /// <summary>幂等播放死亡倒地动画并锁定死亡状态。</summary>
    public static bool PlayDie(AnimationPlayer anim, bool jumpToEnd = false)
    {
        EnsureInitialized(anim);

        if (!anim.HasAnimation("die"))
        {
            return false;
        }

        if (anim.HasMeta(DeadMeta))
        {
            if (jumpToEnd)
            {
                PlayWithHardReset(anim, "die");
                anim.Seek(anim.CurrentAnimationLength, true);
                anim.Pause();
            }

            return true;
        }

        anim.SetMeta(DeadMeta, true);
        if (!jumpToEnd && anim.CurrentAnimation == "die" && anim.IsPlaying())
        {
            return true;
        }

        PlayWithHardReset(anim, "die");
        if (jumpToEnd)
        {
            anim.Seek(anim.CurrentAnimationLength, true);
            anim.Pause();
        }

        return true;
    }

    /// <summary>复活解锁并回到待机循环（RESET + idle 首帧会自动恢复 Parts 可见与隐藏 DeathSprite）。</summary>
    public static void Revive(AnimationPlayer anim)
    {
        EnsureInitialized(anim);

        if (anim.HasMeta(DeadMeta))
        {
            anim.RemoveMeta(DeadMeta);
        }

        if (anim.GetParent() is Node visuals && visuals.HasMeta("wizard_death_sprite_applied"))
        {
            visuals.RemoveMeta("wizard_death_sprite_applied");
        }

        if (anim.HasAnimation("idle"))
        {
            PlayWithHardReset(anim, "idle");
        }
    }

    private static void SetSafeBlend(
        AnimationPlayer anim,
        HashSet<string> discreteAnims,
        string from,
        string to,
        double seconds)
    {
        if (!anim.HasAnimation(from) || !anim.HasAnimation(to))
        {
            return;
        }

        double effectiveBlend = (discreteAnims.Contains(from) || discreteAnims.Contains(to))
            ? 0.0
            : seconds;
        anim.SetBlendTime(from, to, effectiveBlend);
    }
}

[HarmonyPatch(typeof(NCreature), "_Ready")]
internal static class WizardAnimReadyPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        try
        {
            if (!WizardCharacter.IsWizardPlayer(__instance.Entity?.Player))
            {
                return;
            }

            if (!WizardAnimController.TryGetRiggedAnimPlayer(__instance.Visuals, out AnimationPlayer anim))
            {
                return;
            }

            WizardAnimController.EnsureInitialized(anim);

            if (__instance.Entity is { IsDead: true })
            {
                WizardAnimController.PlayDie(anim, jumpToEnd: true);
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 骨骼动画初始化失败：{e.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class WizardAnimTriggerPatch
{
    // ⚠️ 必须高于 BaseLib.Patches.UI.CustomAnimationPatch.SendTriggerToOtherAnimators（默认 Normal=400），
    //    否则 BaseLib 会先调用 AnimationPlayer.Play 并返回 false 把本补丁彻底短路！
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(NCreature __instance, string trigger)
    {
        try
        {
            Player? player = __instance.Entity?.Player;
            if (!WizardCharacter.IsWizardPlayer(player))
            {
                return true;
            }

            if (!WizardAnimController.TryGetRiggedAnimPlayer(__instance.Visuals, out AnimationPlayer anim))
            {
                return true;
            }

            WizardAnimController.PlayTrigger(anim, trigger, player?.NetId);
            return false;
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 播放骨骼动画触发器 {trigger} 失败：{e.Message}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
internal static class WizardAnimRevivePatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        try
        {
            Player? player = __instance.Entity?.Player;
            if (!WizardCharacter.IsWizardPlayer(player))
            {
                return;
            }

            WizardAnimController.ClearPlayerCardPlayGate(player?.NetId);

            if (WizardAnimController.TryGetRiggedAnimPlayer(__instance.Visuals, out AnimationPlayer anim))
            {
                WizardAnimController.Revive(anim);
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 复活恢复骨骼待机失败：{e.Message}");
        }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayStarted))]
internal static class WizardAnimCardPlayStartedPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardPlay cardPlay)
    {
        try
        {
            WizardAnimController.OnCardPlayStarted(cardPlay);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 记录出牌动画门控开始失败：{e.Message}");
        }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayFinished))]
internal static class WizardAnimCardPlayFinishedPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardPlay cardPlay)
    {
        try
        {
            WizardAnimController.OnCardPlayFinished(cardPlay);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 记录出牌动画门控结束失败：{e.Message}");
        }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.Clear))]
internal static class WizardAnimCombatHistoryClearPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        try
        {
            WizardAnimController.ClearAllCardPlayGates();
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[Wizard] 清理出牌动画门控栈失败：{e.Message}");
        }
    }
}
