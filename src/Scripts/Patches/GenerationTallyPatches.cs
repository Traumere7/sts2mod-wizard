using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 给 <see cref="GenerationTally"/> 计数：挂在
/// <c>Hook.AfterCardGeneratedForCombat</c> 的 **Prefix** 上。
///
/// <para>
/// 用 Prefix 而不是 Postfix：<c>UndyingPower</c> 自己也覆写了
/// <c>AfterCardGeneratedForCombat</c>，它需要在本钩子派发时读到**已经加过 1** 的值；
/// Prefix 先于所有模型的钩子执行，正好满足。
/// </para>
///
/// <para>
/// 口径：只统计"**玩家回合内、由玩家创建**"的生成（对应「不灭」卡面"你在本回合生成"）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardGeneratedForCombat))]
internal static class GenerationTallyCountPatch
{
    private static void Prefix(ICombatState combatState, CardModel card, Player? creator)
    {
        if (creator == null || combatState.CurrentSide != CombatSide.Player)
        {
            return;
        }

        GenerationTally.Bump(creator);
    }
}

/// <summary>
/// 每个**玩家回合开始**时清空 <see cref="GenerationTally"/>。
///
/// <para>
/// 挂在 <c>Hook.BeforeSideTurnStart</c> 的 Prefix：先清零、再开始本回合的一切生成。
/// 「不灭」在 <c>AfterSideTurnStart</c> 结算，读的是它上一回合存下的值，不受清零影响。
/// </para>
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart))]
internal static class GenerationTallyResetPatch
{
    private static void Prefix(CombatSide side)
    {
        if (side == CombatSide.Player)
        {
            GenerationTally.Reset();
        }
    }
}
