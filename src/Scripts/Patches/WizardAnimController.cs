using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Wizard.Scripts.Character;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 巫师 Godot 原生 2D 骨骼动画（<c>AnimationPlayer</c>）状态控制器。
///
/// <para>
/// 负责处理 BaseLib <c>CustomAnimation</c> 没覆盖到的四件实战刚需：
/// <list type="number">
///   <item><b>平滑混合时间（SetBlendTime）</b>：复刻原版 STS2 的干脆节奏
///         （出招 0.05s、受击 0.02s 即时响应、回位 0.08~0.12s）。</item>
///   <item><b>一次性动作自动回位</b>：<c>attack / cast / powerup / hurt</c>
///         播完后自动接回循环态 <c>idle</c>。</item>
///   <item><b>连击/快打重播</b>：同一动作尚在播放时再次触发（如连打打击、多段受击），
///         立刻归零重播，不吞动作。</item>
///   <item><b>死亡锁定与复活解锁</b>：进入 <c>die</c> 后忽略后续残余 <c>Hit/Idle</c> 触发，
///         且幂等防重复归零；<c>StartReviveAnim</c> 时清除锁并切回 <c>idle</c>
///         （顺带绕开 BaseLib <c>UseCustomReviveAnim</c> 找不到 revive 却吞掉淡入的坑）。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ 仅对<b>骨骼版巫师场景</b>（<c>Visuals</c> 下同时含 <c>Parts</c>、<c>Skeleton2D</c>、
/// <c>AnimationPlayer</c>）生效；B 版静态回退场景与其它角色完全放行，零副作用。
/// </para>
/// </summary>
internal static class WizardAnimController
{
    private const string ReadyMeta = "wizard_anim_ready";
    private const string DeadMeta = "wizard_anim_dead";

    private static readonly string[] OneShotAnims = ["attack", "cast", "powerup", "hurt"];

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

    /// <summary>初始化混合时间与播完自动回位信号（幂等）。</summary>
    public static void EnsureInitialized(AnimationPlayer anim)
    {
        if (anim.HasMeta(ReadyMeta))
        {
            return;
        }

        anim.SetMeta(ReadyMeta, true);

        // 复刻原版 STS2 SpineAnimationMix 节奏：起手脆、受击快、回位柔
        SetBlendIfPresent(anim, "idle", "attack", 0.05);
        SetBlendIfPresent(anim, "attack", "idle", 0.08);
        SetBlendIfPresent(anim, "idle", "cast", 0.06);
        SetBlendIfPresent(anim, "cast", "idle", 0.10);
        SetBlendIfPresent(anim, "idle", "powerup", 0.06);
        SetBlendIfPresent(anim, "powerup", "idle", 0.12);
        SetBlendIfPresent(anim, "idle", "hurt", 0.02);
        SetBlendIfPresent(anim, "hurt", "idle", 0.10);

        // 动作间相互打断
        foreach (string from in OneShotAnims)
        {
            SetBlendIfPresent(anim, from, "hurt", 0.02);
            SetBlendIfPresent(anim, from, "attack", 0.04);
            SetBlendIfPresent(anim, from, "cast", 0.05);
            SetBlendIfPresent(anim, from, "powerup", 0.05);
            SetBlendIfPresent(anim, from, "die", 0.03);
        }

        SetBlendIfPresent(anim, "idle", "die", 0.03);

        // 一次性动作播完自动回到 idle；die 播完保持末帧定格
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
            if (name is "die" or "idle")
            {
                return;
            }

            if (anim.HasAnimation("idle"))
            {
                anim.Play("idle");
            }
        };
    }

    /// <summary>按引擎触发器名播放对应骨骼动画。</summary>
    public static void PlayTrigger(AnimationPlayer anim, string trigger)
    {
        EnsureInitialized(anim);

        string target = trigger switch
        {
            "Idle" or "idle" => "idle",
            "Attack" or "attack" => "attack",
            "Cast" or "cast" => "cast",
            "PowerUp" or "powerup" => "powerup",
            "Hit" or "hit" or "Hurt" or "hurt" => "hurt",
            "Dead" or "dead" or "Die" or "die" => "die",
            "Revive" or "revive" => "revive",
            _ => trigger.ToLowerInvariant(),
        };

        if (target == "revive")
        {
            Revive(anim);
            return;
        }

        if (target == "die")
        {
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

        // 同动作连击（如连续出打击或连续受击）立即归零重播，避免吞动作
        if (target != "idle" && anim.CurrentAnimation == target)
        {
            anim.Stop(keepState: false);
            anim.Play(target);
            anim.Seek(0.0, true);
            return;
        }

        anim.Play(target);
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
                anim.Play("die");
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

        anim.Play("die");
        if (jumpToEnd)
        {
            anim.Seek(anim.CurrentAnimationLength, true);
            anim.Pause();
        }

        return true;
    }

    /// <summary>复活解锁并回到待机循环（idle 首帧会自动恢复 Parts 可见与隐藏 DeathSprite）。</summary>
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
            anim.Play("idle");
            anim.Seek(0.0, true);
        }
    }

    private static void SetBlendIfPresent(AnimationPlayer anim, string from, string to, double seconds)
    {
        if (anim.HasAnimation(from) && anim.HasAnimation(to))
        {
            anim.SetBlendTime(from, to, seconds);
        }
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
    [HarmonyPrefix]
    private static bool Prefix(NCreature __instance, string trigger)
    {
        try
        {
            if (!WizardCharacter.IsWizardPlayer(__instance.Entity?.Player))
            {
                return true;
            }

            if (!WizardAnimController.TryGetRiggedAnimPlayer(__instance.Visuals, out AnimationPlayer anim))
            {
                return true;
            }

            WizardAnimController.PlayTrigger(anim, trigger);
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
            if (!WizardCharacter.IsWizardPlayer(__instance.Entity?.Player))
            {
                return;
            }

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
