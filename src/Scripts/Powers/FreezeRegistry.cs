using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 冻结用的登记表：记录"某只怪物下一次掷招时要保留哪一招"。
///
/// <para>
/// 用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 而不是普通字典：
/// 键是 <see cref="MonsterModel"/> 实例，用弱引用可以避免战斗结束后残留引用。
/// </para>
///
/// <para>
/// <b>⚠️ <see cref="Entry.Armed"/> 是必须的，不能靠"能力是否还在"来判断：</b>
/// 层数在<b>敌人回合结束</b>递减（紧跟在登记之后），1 层时那一刻能力就被移除了；
/// 而拦截掷招发生在<b>下一个玩家回合开始</b>。
/// 也就是说 <b>登记一定活得比能力久</b>。
/// </para>
///
/// <para>
/// 曾经在 <c>FrozenIntentPatch</c> 里用 <c>Creature.HasPower&lt;FreezePower&gt;()</c>
/// 做二次确认，本意是丢弃"残留登记"，结果把<b>每一个合法的首次登记都丢掉了</b> ——
/// 表现就是"1 层完全无效果"，N 层只能重复 N-1 次。
/// 正确做法是：登记时明确标记"这一条可以拦截下一次掷招"。
/// 登记发生在敌人回合结束、掉层之前，此时层数一定还在，所以这个标记是可靠的。
/// </para>
/// </summary>
internal static class FreezeRegistry
{
    private sealed class Entry
    {
        public MoveState? Move;

        /// <summary>可以拦截下一次掷招。正常流程下恒为 true，仅作防御。</summary>
        public bool Armed;
    }

    private static readonly ConditionalWeakTable<MonsterModel, Entry> Table = new();

    /// <summary>
    /// 登记"该怪物下一次掷招要保留这一招"。
    /// 只在<b>敌人回合结束（掉层之前）</b>调用 ——
    /// 此刻 <c>NextMove</c> 仍是敌人刚刚执行的那一招。
    /// </summary>
    public static void Set(MonsterModel monster, MoveState move)
    {
        Table.Remove(monster);
        Table.Add(monster, new Entry { Move = move, Armed = true });
    }

    /// <summary>
    /// 取走"要保留的招"并**消费掉**登记（只在一次掷招生效）。
    ///
    /// <para>
    /// 未 <see cref="Entry.Armed"/> 的条目会被丢弃并返回 null ——
    /// 正常流程下不会出现，留作防御。
    /// </para>
    /// </summary>
    public static MoveState? Consume(MonsterModel monster)
    {
        if (!Table.TryGetValue(monster, out Entry? entry))
        {
            return null;
        }

        Table.Remove(monster);
        return entry.Armed ? entry.Move : null;
    }

    /// <summary>清掉该怪物的登记（能力被移除时调用）。</summary>
    public static void Clear(MonsterModel monster) => Table.Remove(monster);

    /// <summary>查询但不消费（调试用）。</summary>
    public static MoveState? Peek(MonsterModel monster)
        => Table.TryGetValue(monster, out Entry? entry) && entry.Armed ? entry.Move : null;
}
