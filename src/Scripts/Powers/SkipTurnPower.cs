using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 跳过回合：<b>在你的下 N 个回合开始时，结束你的回合</b>（<c>Amount</c> = 剩余回合数）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结构照抄「失忆」<see cref="AmnesiaPower"/>
/// ══════════════════════════════════════════════════════════════════
/// 两者语义完全同构（"在你的下 N 个回合开始时做某事，每回合掉 1 层"），
/// 所以钩子、扣层、owner 判定的写法保持一致。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结束回合必须走<b>官方路径</b> <c>PlayerCmd.EndTurn</c>
/// ══════════════════════════════════════════════════════════════════
/// 照抄原版「虚空形态」<c>VoidForm.OnPlay</c>（<c>VoidForm.cs:26</c>）：
/// <code>
/// PlayerCmd.EndTurn(base.Owner, canBackOut: false);
/// </code>
/// <b>不要</b>自己手动跑"弃牌 → 敌人回合"那套 —— 只有官方路径才会：
/// <list type="bullet">
///   <item>正常触发 <c>BeforeSideTurnEnd</c> / <c>AfterSideTurnEnd</c> 等回合结束钩子；</item>
///   <item>正常走 <c>EndPlayerTurnPhaseOne/Two</c>，回合数照常 +1；</item>
///   <item>多人下只把自己标记为"已结束回合"，<b>不影响</b>其他玩家继续行动。</item>
/// </list>
/// 用户的技术口径就是这样要求的：
/// <i>"被跳过回合效果影响下，玩家的回合开始时和回合结束时效果依旧可正常结算，
/// 回合数也正常增加。"</i>
/// </para>
///
/// <para>
/// ⚠️ <b>为什么在 <c>AfterPlayerTurnStart</c> 里结束回合是安全的</b>
/// （时序证据，<c>CombatManager.cs</c>）：
/// <code>
/// StartTurn():                       // :727-733 先建好信号源
///     turnState.EndTurnSignalSource = new TaskCompletionSource&lt;EndTurnSignal&gt;();
///     ...
///     await SetupPlayerTurn(...);    // :921 抽牌 → :923 Hook.AfterPlayerTurnStart(...)
/// AwaitTurnEndAndSwitchSides():      // :635
///     EndTurnSignal s = await endTurnSignalSource.Task;   // :656
///     await AfterAllPlayersReadyToEndTurn(turnState, s);  // :672
/// </code>
/// 也就是说 <c>AfterPlayerTurnStart</c> 是 <c>SetupPlayerTurn</c> 的<b>最后一句</b>，
/// 而"等待回合结束"的 <c>Task</c> 在它<b>之后</b>才 await。
/// 我们在这里调 <c>EndTurn</c> ⇒ 只是提前把那个 TCS 设好结果，
/// 等到 await 时立刻返回 ⇒ 正常进入回合结束流程，<b>不会</b>重入或跳过结算。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>为什么 <c>Type</c> 是 <c>None</c> 而不是 <c>Debuff</c></b>
/// 「跳过回合」显然是负面效果，但它<b>不能</b>标成 <c>Debuff</c>：
/// 原版「人工制品」<c>ArtifactPower.TryModifyPowerAmountReceived</c> 的判定是
/// <code>
/// if (canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff) return false;
/// modifiedAmount = 0m;   // 直接归零
/// </code>
/// —— <b>只看目标、完全不看施加者</b>。所以标成 Debuff 的话，
/// 玩家身上只要还有人工制品，自己打出的「杰作」就会把「跳过回合」整层吃掉，
/// 代价凭空消失（等于一个可以稳定白嫖的 bug）。
/// 用 <c>None</c> 既躲开人工制品，也符合它"这是自己安排的待办事项"的语义。
/// </para>
/// </summary>
public class SkipTurnPower : CustomPowerModel
{
    /// <summary>⚠️ 必须是 <c>None</c>，理由见类注释（人工制品会吃掉 Debuff）。</summary>
    public override PowerType Type => PowerType.None;

    /// <summary>计数型：<c>Amount</c> = 还要跳过几个回合。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/skip_turn_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/skip_turn_power_big.png";

    /// <summary>
    /// 回合开始 ⇒ 立刻结束回合，并掉 1 层。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 只影响自己；层数没了就不用做事（防御）。
        if (player != Owner.Player || Amount <= 0)
        {
            return;
        }

        // 自己已经死了就别再折腾回合（防御，正常不会走到）。
        if (!player.Creature.IsAlive)
        {
            return;
        }

        Flash();

        // ⚠️ 先掉层、再结束回合。
        //
        // 反过来的话，万一 EndTurn 之后有同步流程重新进入"回合开始"，
        // 身上还挂着 1 层就会再跳一次 —— 无限跳过。
        // 先掉层则"这次跳过已经用掉了"，任何重入都不会重复生效。
        // 归零时由 PowerCmd 自动移除，不必手动 Remove。
        await PowerCmd.Decrement(this);

        // 官方结束回合路径（同原版「虚空形态」VoidForm.cs:26）。
        // 多人下只把自己标记为已结束，其他玩家照常行动。
        PlayerCmd.EndTurn(player, canBackOut: false);
    }
}
