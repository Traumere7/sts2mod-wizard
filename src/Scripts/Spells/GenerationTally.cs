using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 「本回合中，某玩家**生成**了多少张卡牌」的累加器（供「不灭」<c>UndyingPower</c> 使用）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么需要常驻累加器（而不是读战斗历史 / 只靠能力自己数）
/// ══════════════════════════════════════════════════════════════════
/// 「不灭」要求 x = **本回合**生成的牌数，口径是"既溯前、又追后"：
/// <list type="bullet">
///   <item><b>溯前</b>：打出「不灭」之前就已经生成的牌也要算进去；</item>
///   <item><b>追后</b>：打出之后继续生成，数值要跟着涨。</item>
/// </list>
/// 能力实例只在"打出之后"才存在，所以计数必须由**常驻**逻辑维护；
/// 而战斗历史 <c>CardGeneratedEntry</c> 不暴露回合号（<c>RoundNumber</c> / <c>CurrentSide</c> 是私有），
/// 拿不到"本回合"的可靠切片，因此自行计数。
/// </para>
///
/// <para>
/// <b>数据来源</b>：<see cref="Patches.GenerationTallyCountPatch"/> 挂在
/// <c>Hook.AfterCardGeneratedForCombat</c> 的 Prefix 上（先计数、再派发给各能力，
/// 这样 <c>UndyingPower</c> 在自己的同钩子里立刻能读到最新值）。
/// 只有"玩家回合内、由玩家创造"的生成才计入 —— 对应卡面"你在本回合生成"。
/// </para>
///
/// <para>
/// <b>复位</b>：<see cref="Patches.GenerationTallyResetPatch"/> 挂在
/// <c>Hook.BeforeSideTurnStart</c>（玩家回合开始时）清零。
/// 必须在**能力结算之前**清零，但「不灭」结算读的是它在上一回合存下的
/// <c>_pendingEnergy</c>，不依赖当前计数，所以两者互不干扰。
/// </para>
/// </summary>
public static class GenerationTally
{
    /// <summary>按玩家 <c>NetId</c> 分开计数（多人各自独立）。</summary>
    private static readonly Dictionary<ulong, int> Counts = [];

    /// <summary>该玩家本回合已生成的牌数。</summary>
    public static int Get(Player? player)
        => player != null && Counts.TryGetValue(player.NetId, out int value) ? value : 0;

    /// <summary>该玩家生成了一张牌。</summary>
    public static void Bump(Player creator)
        => Counts[creator.NetId] = Get(creator) + 1;

    /// <summary>新回合开始，所有人清零。</summary>
    public static void Reset() => Counts.Clear();
}
