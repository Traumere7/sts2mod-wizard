using System;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 失忆：<b>在你的下 N 个回合开始时，各少抽 1 张牌</b>（<c>Amount</c> = 剩余回合数）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 语义已改（2026-10-19，用户要求）
/// ══════════════════════════════════════════════════════════════════
/// <b>旧</b>：<c>Amount</c> = 少抽几张，<b>只影响下个回合一次</b>
/// （打 3 张 → 下回合一次少抽 3 张）。
/// <b>新</b>：<c>Amount</c> = 还要影响几个回合，每回合少抽 <b>1</b> 张
/// （打 3 张 → 下 3 个回合各少抽 1 张）。
/// 文案：<i>"在你的下 N 个回合开始时，少抽 1 张牌。"</i>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么用 <c>ModifyHandDraw</c> + <c>AfterPlayerTurnStart</c> 两个钩子
/// ══════════════════════════════════════════════════════════════════
/// <b>① 削减抽牌数 → <c>ModifyHandDraw</c></b>
/// （照抄原版「心灵腐化」<c>MindRotPower</c>，<c>MindRotPower.cs:14-20</c>）
///
/// <para>
/// ⚠️ <b><c>ModifyHandDraw</c> 只作用于「回合开始抽的那 5 张」</b>，
/// <b>不影响回合内的"抽 N 张"效果</b>。证据：
/// 它全游戏只有<b>一个</b>调用点 ——
/// <c>CombatManager.SetupPlayerTurn</c> 里的
/// <c>Hook.ModifyHandDraw(state, player, 5m, out ...)</c>
/// （<c>CombatManager.cs:902</c>，紧接 <c>:921</c> 的
/// <c>CardPileCmd.Draw(...)</c>）。回合内抽牌走别的地方，不经过这个钩子。
/// 所以"回合开始时少抽"这个语义天然成立，不需要额外判断时机。
/// </para>
///
/// <para>
/// <b>② 消耗一层 → <c>AfterPlayerTurnStart</c></b>
///
/// <para>
/// ⚠️ <b>不用 <c>AfterModifyingHandDraw</c>，因为它在"抽牌数没被改变"时不会被调用。</b>
/// <c>Hook.ModifyHandDraw</c>（<c>Hook.cs:1692-1716</c>）只在
/// <c>(int)num2 != (int)num</c>（数量真的变了）时才把该 modifier 收进
/// <c>modifiers</c>；而 <c>Hook.AfterModifyingHandDraw</c>（<c>:751-761</c>）
/// 只对 <c>modifiers</c> 里的模型回调。
/// 于是"基础抽牌数已经是 0"（例如同时中了原版「心灵腐化」）时，
/// 本能力把 0 削到 <c>Math.Max(0, 0-1) = 0</c> —— 数量没变 ⇒
/// <c>AfterModifyingHandDraw</c> 不触发 ⇒ 层数永远不减少 ⇒ <b>Debuff 卡在场上</b>。
/// </para>
///
/// <para>
/// 改用 <c>AfterPlayerTurnStart</c>（<c>AbstractModel.cs:1320</c>）：
/// 它在 <c>CombatManager.cs:923</c> 被调用，<b>正好在抽牌（<c>:921</c>）之后</b>，
/// 且<b>每个玩家回合恰好一次</b>，与"抽牌数有没有真的变化"无关。
/// 用它扣层最稳，语义也正是"这个回合的惩罚已经生效"。
/// </para>
///
/// <b>时序</b>（<c>CombatManager.SetupPlayerTurn</c>，行号已核对）：
/// <code>
/// :900  Hook.BeforeHandDraw
/// :902  Hook.ModifyHandDraw(5m)          ← ① 这里少抽 1
/// :903  Hook.AfterModifyingHandDraw
/// :921  CardPileCmd.Draw(handDraw)
/// :923  Hook.AfterPlayerTurnStart        ← ② 这里扣一层
/// </code>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 叠层与移除
/// ══════════════════════════════════════════════════════════════════
/// <c>StackType.Counter</c> ⇒ 每打一张「失忆」<c>Amount</c> +1，即多影响一个回合。
/// 扣层用 <c>PowerCmd.Decrement(this)</c>（<c>PowerCmd.cs:184-187</c>，
/// 内部 <c>ModifyAmount(-1)</c>），与「模糊」<c>BlurPower</c>、
/// 「缓冲」<c>BufferPower</c> 等"每回合掉一层"的能力同一写法。
/// <c>ModifyAmount</c> 末尾会
/// <c>if (power.ShouldRemoveDueToAmount()) await Remove(power);</c>
/// （<c>PowerCmd.cs:251-254</c>），而 <c>ShouldRemoveDueToAmount</c>
/// （<c>PowerModel.cs:478-489</c>）对非 <c>AllowNegative</c> 的计数型能力在
/// <c>Amount &lt;= 0</c> 时返回 true ⇒ <b>归零自动移除，无需手动 <c>Remove</c></b>。
/// </para>
///
/// <para>
/// ⚠️ <b>卡面上的「消耗」不在本能力里</b>，在 <c>Amnesia.CanonicalKeywords</c>。
/// 本能力只负责削减抽牌与扣层。
/// </para>
/// </summary>
public class AmnesiaPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    /// <summary>计数型：<c>Amount</c> = 还要影响几个回合（每回合少抽 1 张）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/amnesia_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/amnesia_power_big.png";

    /// <summary>每回合削减的抽牌数 —— 固定 1 张，与层数无关。</summary>
    private const decimal CardsPerTurn = 1m;

    /// <summary>
    /// 回合开始抽牌时少抽 <see cref="CardsPerTurn"/> 张
    /// （照抄 <c>MindRotPower.ModifyHandDraw</c> 的结构，但固定减 1 而非减 <c>Amount</c>）。
    /// </summary>
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        // 只影响自己；层数没了就不影响（正常情况下已被移除，这里是防御）。
        if (player != Owner.Player || Amount <= 0)
        {
            return count;
        }

        // 夹到 0 以上：抽牌数不能变成负数。
        return Math.Max(0m, count - CardsPerTurn);
    }

    /// <summary>
    /// 本回合的抽牌惩罚已经生效（抽牌之后触发）—— 扣掉一层"剩余回合数"。
    /// 归零时由 <c>PowerCmd.ModifyAmount</c> 自动移除，不必手动 <c>Remove</c>。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return;
        }

        Flash();

        await PowerCmd.Decrement(this);
    }
}
