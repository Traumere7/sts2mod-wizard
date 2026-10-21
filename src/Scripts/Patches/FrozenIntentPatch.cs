using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Patches;

/// <summary>
/// 冻结的生效点：拦截 <see cref="MonsterModel.RollMove"/>，
/// 若该怪物已被冻结，则写回它上一回合的招，而不是重掷新招。
///
/// <para>
/// <b>为什么必须打补丁：</b><c>RollMove</c> 是唯一的选招入口，整段实现只有一行：
/// <code>
/// public void RollMove(IEnumerable&lt;Creature&gt; targets)
///     =&gt; NextMove = MoveStateMachine.RollMove(targets, Creature, RunRng.MonsterAi);
/// </code>
/// 而 <c>NextMove</c> 是 <c>{ get; private set; }</c>，外部无法写入。
/// </para>
///
/// <para>
/// <b>为什么不调用原方法：</b>原方法会把状态机游标 <c>_currentState</c> 往前推一格，
/// 那么下一次掷招就会<b>跳过</b>被冻结的那一招（冻结实际上是"多打一次"，
/// 不是"少打一次"）。所以这里让原方法完全不执行 ——
/// 状态机停在原地，冻结结束后照常从它后面一招继续，总招式数不变。
/// </para>
///
/// <para>
/// <b>为什么不能用 <c>SetMoveImmediate</c></b>（原版 <c>IllusionPower</c> 用的那个）：
/// 它内部会调用 <c>MoveStateMachine.ForceCurrentState(state)</c>，
/// 把状态机游标也拨回该招，下一次 <c>RollMove</c> 会从那里继续推进，覆盖当场失效。
/// 而 <c>MoveState.GetNextState</c> 返回只读的 <c>FollowUpStateId</c>，
/// 也无法让状态机自然自环。所以只能直接写 <c>NextMove</c>，<b>不动状态机</b>。
/// </para>
///
/// <para>
/// <b>登记时机</b>见 <see cref="FreezePower.AfterSideTurnEnd"/>：
/// 在玩家回合结束（掷招之后、敌人行动之前）写入 <see cref="FreezeRegistry"/>，
/// 因此本补丁消费到的永远是"本回合正在展示的那一招"，
/// 敌人下一回合就会把它原样重打一次。
/// </para>
///
/// <para>
/// <b>眩晕必须放行：</b><c>Creature.StunInternal</c> 通过 <c>SetMoveImmediate</c>
/// 构造一个临时的 <c>STUNNED</c> 状态（不注册进 <c>States</c> 字典）。
/// 若冻结在此处覆盖，会让被眩晕的怪物照常行动 —— 冻结不能取消眩晕。
/// </para>
/// </summary>
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove))]
internal static class FrozenIntentPatch
{
    private const string Tag = "[Wizard/Freeze]";

    /// <summary><c>MonsterModel.NextMove</c> 的私有 setter。</summary>
    private static readonly MethodInfo? NextMoveSetter =
        AccessTools.PropertySetter(typeof(MonsterModel), nameof(MonsterModel.NextMove));

    [HarmonyPrefix]
    private static bool Prefix(MonsterModel __instance, IEnumerable<Creature> targets)
    {
        MoveState? frozen = FreezeRegistry.Consume(__instance);
        if (frozen == null)
        {
            return true; // 未被冻结：完全走原逻辑
        }

        // 这里**不能**再检查 `HasPower<FreezePower>()`。
        // 层数在敌人回合结束递减，1 层时能力当场就被移除了，
        // 而拦截掷招发生在下一个玩家回合开始 —— 也就是说
        // **登记一定活得比能力久**。曾用它做"残留登记"防护，
        // 结果把每一个合法的首次登记都丢掉了，表现就是"1 层完全无效果"。
        // 现在改由 FreezeRegistry 的 Armed 标记负责，见 FreezeRegistry 的注释。

        // 眩晕的临时状态不在 States 字典里，放行以免取消眩晕。
        if (frozen.StateId == MonsterModel.stunnedMoveId)
        {
            Log.Info($"{Tag} {__instance.Id.Entry}: 登记的意图是眩晕，放行重掷");
            return true;
        }

        if (NextMoveSetter == null || __instance.MoveStateMachine == null)
        {
            Log.Info($"{Tag} {__instance.Id.Entry}: 拦截失败（setter={NextMoveSetter != null} 状态机={__instance.MoveStateMachine != null}），放行");
            return true;
        }

        // 只写 NextMove，不推进状态机：
        // 这样冻结结束后招式序列从原来那一招的下一招继续，总回合数不增不减。
        NextMoveSetter.Invoke(__instance, new object[] { frozen });
        Log.Info($"{Tag} {__instance.Id.Entry}: 掷招被拦截，意图保持为 '{frozen.StateId}'");

        __instance.Creature.GetCreatureNode()?.RefreshIntents();
        return false; // 跳过原方法（不重掷、不推进状态机）
    }
}
