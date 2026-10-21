using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 让 <c>TargetType.Self</c> 的**群体攻击牌**在拖拽时也能显示动态数值。
///
/// ══════════════════════════════════════════════════════════════════
/// 问题
/// ══════════════════════════════════════════════════════════════════
/// 原版「欺凌」<c>Bully</c>（<c>AnyEnemy</c>）手里显示基础值、拖出来才显示
/// <c>基础 + 倍率×状态</c>，原理是 <c>CalculatedVar.Calculate(target)</c> 里的
/// <c>target</c>：手里为 null → 倍率返回 0 → 显示 <c>CalculationBase</c>。
///
/// 而「落日」<c>Sunset</c> 是 <c>TargetType.Self</c>（"对所有敌人"靠
/// <c>TargetingAllOpponents</c>，不需要选目标），引擎<b>从不给它设预览目标</b>：
/// <list type="bullet">
///   <item><c>NMouseCardPlay.TargetSelection</c> 只对
///         <c>AnyEnemy</c>/<c>AnyAlly</c> 走 <c>SingleCreatureTargeting</c>，
///         那条路才会 <c>Connect(CreatureHovered)</c> → <c>SetPreviewTarget</c>；</item>
///   <item>其余（含 <c>Self</c>）走 <c>MultiCreatureTargeting</c>，
///         只调 <c>ShowMultiCreatureTargetingVisuals()</c>，**不设预览目标**。</item>
/// </list>
/// 于是落日的 <c>target</c> 恒为 null，倍率恒返回 0 → 卡面永远只有基础值。
///
/// ══════════════════════════════════════════════════════════════════
/// 修法
/// ══════════════════════════════════════════════════════════════════
/// 在拖拽真正进入"瞄准态"的那一刻（<c>ShowMultiCreatureTargetingVisuals</c>，
/// 鼠标拖进 Play Zone 时触发），替 <c>Self</c> 群体攻击牌补一个预览目标，
/// 让动态倍率算得出来。
///
/// <para>
/// <b>为什么不改卡牌本身：</b>「落日」的倍率是**全局量**（场上最高燃烧），
/// 与目标无关，所以在卡牌层面无法区分"手里"和"拖出来" ——
/// 只有 UI 层知道当前是不是在瞄准。
/// </para>
///
/// <para>
/// <b>补的目标取 <c>HittableEnemies[0]</c>：</b>原版对
/// <c>AllEnemies</c>/<c>RandomEnemy</c> 也是这么做的
/// （<c>NCardPlay.cs:336</c>，仅在只敌人数量为 1 时）。
/// 这里放宽到"任意数量"，因为这个目标只是个**让倍率有话可说的占位**，
/// 落日这类牌的倍率会忽略它、直接读全场状态。
/// </para>
///
/// <para>
/// <b>只补 <c>Self</c>，且只在"卡牌确实是群体攻击"时：</b>
/// 条件是 <c>TargetType.Self</c> 且 <c>CardType.Attack</c>。
/// 这避免影响纯自增益牌（如「镇静」「修行」）——
/// 它们的倍率若对 target 敏感，补目标反而会显示错误的预览。
/// </para>
///
/// <para>
/// ⚠️ 打了补丁后，<c>CalculatedVar</c> 的倍率函数可以**通过 <c>target != null</c>
/// 来判断"是否处于拖拽瞄准态"** —— 这正是 <c>Bully</c> 的天然行为。
/// 见 <c>FieldState.HighestBurnForPreview</c>。
/// </para>
/// </summary>
[HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
internal static class SelfTargetingPreviewPatch
{
    private const string Tag = "[Wizard/SelfPreview]";

    private static readonly PropertyInfo? CardNodeProperty =
        AccessTools.Property(typeof(NCardPlay), "CardNode");

    /// <summary>
    /// 前缀：给"自身目标的群体攻击牌"补一个预览目标。
    ///
    /// <para>
    /// 必须在前缀里做 —— 原方法内部紧接着就调
    /// <c>CardNode.UpdateVisuals(..., CardPreviewMode.MultiCreatureTargeting)</c>，
    /// 而预览值就是在那次刷新里算的，之后补目标就晚了。
    /// </para>
    /// </summary>
    [HarmonyPrefix]
    private static void Prefix(NCardPlay __instance)
    {
        try
        {
            if (CardNodeProperty?.GetValue(__instance) is not NCard cardNode)
            {
                return;
            }

            CardModel? card = cardNode.Model;
            if (card == null)
            {
                return;
            }

            // 只处理"自身目标 + 攻击牌"（即落日这类"自身目标的群体攻击"）。
            if (card.TargetType != TargetType.Self || card.Type != CardType.Attack)
            {
                return;
            }

            // 已经有了就别覆盖（其他路径设过的话）。
            if (card.CurrentTarget != null)
            {
                return;
            }

            ICombatState? combatState = card.CombatState;
            if (combatState == null)
            {
                return;
            }

            IReadOnlyList<Creature> hittable = combatState.HittableEnemies;
            if (hittable.Count == 0)
            {
                return;
            }

            cardNode.SetPreviewTarget(hittable[0]);
        }
        catch (Exception ex)
        {
            // 补丁绝不能因为异常而打断拖拽流程。
            Log.Info($"{Tag} 补预览目标失败（已忽略）：{ex.Message}");
        }
    }
}
