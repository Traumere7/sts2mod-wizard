using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 【临时诊断，定位「荆棘反伤为 0」后删除】
///
/// <para>
/// 原版 <see cref="ThornsPower.BeforeDamageReceived"/>：
/// <code>
/// if (target == base.Owner &amp;&amp; dealer != null &amp;&amp; (props.IsPoweredAttack() || cardSource is Omnislice))
///     await CreatureCmd.Damage(choiceContext, dealer, base.Amount, ValueProp.Unpowered | ValueProp.SkipHurtAnim, base.Owner, null, null);
/// </code>
/// 反伤值 = <c>base.Amount</c>（能力层数）。这里把"触发时的层数 / 目标 / 来源 / 传入伤害 / 标记位"
/// 全部打进日志，用来判断 0 点到底是「层数为 0」还是「被结算环节压成 0」。
/// </para>
/// </summary>
[HarmonyPatch(typeof(ThornsPower), nameof(ThornsPower.BeforeDamageReceived))]
internal static class ThornsDiagnosticPatch
{
    private static void Prefix(
        ThornsPower __instance,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        try
        {
            Entry.Logger.Info(
                $"[诊断/荆棘] owner={__instance.Owner?.Name} 层数={__instance.Amount} "
                + $"target={target?.Name}(block={target?.Block}) dealer={dealer?.Name} "
                + $"传入伤害={amount} props={props} 是强化攻击={props.IsPoweredAttack()}");
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[诊断/荆棘] 记录失败：{e.Message}");
        }
    }
}
