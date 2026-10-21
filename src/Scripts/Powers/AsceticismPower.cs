using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 修行：**本次战斗结束时额外获得 <c>Amount</c> 组卡牌奖励**（每层 1 组）。
///
/// <para>
/// 触发点 <c>AfterCombatEnd</c>（<c>AbstractModel.cs:520</c>）。
/// </para>
///
/// <para>
/// ⚠️ <b>不能用 <c>AfterCombatVictory</c></b> —— 这是踩过的坑，而且很隐蔽：
/// 引擎注释写着"通常应该用 <c>AfterCombatVictory</c>"，听起来更对，
/// 但 <c>EndCombatInternal</c>（<c>CombatManager.cs:1297</c>）的真实顺序是：
/// <code>
/// :1315  Hook.AfterCombatEnd        ← 此时能力还在 ✓
/// :1324  player2.AfterCombatEnd()   ← 内部 RemoveAllPowersInternalExcept()，能力被清空
/// :1326  Hook.AfterCombatVictory    ← 能力已经没了，为 Power 重写的这个钩子永不触发
/// </code>
/// <c>Player.AfterCombatEnd</c>（<c>Player.cs:837-840</c>）第一件事就是
/// <c>Creature.RemoveAllPowersInternalExcept()</c>。
/// 而 <c>Hook.AfterCombatVictory</c> 遍历的是
/// <c>runState.IterateHookListeners(combatState)</c>，其中战斗监听者来自各个
/// <c>creature.Powers</c> —— 已清空，于是<b>能力的 <c>AfterCombatVictory</c> 静默不执行</b>。
///
/// 实测症状：日志里连钩子里的任何输出都没有（证明函数没被调用），
/// 而奖励确实没给。
/// </para>
///
/// <para>
/// <b>为什么 <c>AfterCombatEnd</c> 时机够用：</b>
/// 额外奖励存在 <c>CombatRoom._extraRewards</c> 里，直到
/// <c>RewardsSet</c> 生成时才被读取（<c>RewardsSet.cs:96</c>），
/// 而那发生在 <c>OfferRoomEndRewards</c>（由 UI 触发，远晚于这里）。
/// 所以只要在战斗结束流程中写入即可。
/// </para>
///
/// <para>
/// 奖励构造照抄原版「狩猎」<c>TheHunt</c>：
/// <c>new CardReward(CardCreationOptions.ForRoom(owner, room.RoomType), 3, owner)</c>，
/// 其中 <c>3</c> 是"一组卡牌奖励"的候选张数（与原版常规奖励一致）。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>2026-10-04 修：原来声明的是 <see cref="PowerStackType.Single"/>，
/// 于是"修行不能叠层"（陈年 bug）。</b>
/// <c>Single</c> 的语义是"只表示存在、不承载层数"，引擎不会累加 ——
/// 打第二张「修行」时 <c>Amount</c> 仍是 1，奖励也只有 1 组。
/// </para>
///
/// <para>
/// <b>判据（本项目已踩到第 6 次，见 <c>CONTEXT.md</c> §6.24）：</b>
/// 能力的强度<b>取决于"有几层"</b> ⟺ 必须 <c>Counter</c> + 读 <c>Amount</c>。
/// <list type="bullet">
///   <item><c>Single</c> 是给「壁垒」<c>Barricade</c> 这类<b>二元开关</b>用的
///         —— 有/没有，没有"更强"的概念。</item>
///   <item>「修行」的强度是<b>可数的量</b>（奖励组数），
///         所以每层就该各给一组，属于 <c>Counter</c>。</item>
/// </list>
/// </para>
/// </summary>
public class AsceticismPower : CustomPowerModel
{
    /// <summary>一组卡牌奖励的候选张数，与原版常规战斗奖励一致。</summary>
    private const int CardsPerReward = 3;

    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 额外奖励的<b>组数</b>。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/asceticism_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/asceticism_power_big.png";

    /// <summary>
    /// 悬停文案用的层数（<c>{Amount}</c>）。
    /// <c>Math.Max(Amount, 1)</c> 兜底：层数为 0 时不该发生，但也不该显示"0 组"。
    /// </summary>
    private int RewardGroups => Math.Max(Amount, 1);

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Player? player = Owner.Player;
        Log.Info($"[Wizard/Asceticism] AfterCombatEnd 被调用，Owner.Player={(player != null)} "
                 + $"Amount={Amount} room={room.RoomType}");

        if (player == null)
        {
            return Task.CompletedTask;
        }

        Flash();

        // 每层各追加一组卡牌奖励（AddExtraReward 内部是 List.Add，可重复调用）。
        int groups = RewardGroups;
        for (int i = 0; i < groups; i++)
        {
            room.AddExtraReward(
                player,
                new CardReward(
                    CardCreationOptions.ForRoom(player, room.RoomType),
                    CardsPerReward,
                    player));
        }

        Log.Info($"[Wizard/Asceticism] 已追加 {groups} 组卡牌奖励（每组 {CardsPerReward} 张）");

        return Task.CompletedTask;
    }
}
