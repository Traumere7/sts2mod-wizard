using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 「本场战斗中，某生物**施加**（attempt）了多少层燃烧」的累加器。
///
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 语义：**"施加动作"计数，不是"实际写入"计数**
/// ══════════════════════════════════════════════════════════════════
///
/// 燃烧是"取高值、不叠加"的（见 <see cref="Powers.BurnPower"/>）：
/// 目标已有 2 层时再施加 1 层，结果**仍是 2 层**。
/// 但设计上这**算一次有效的"给予燃烧"**，「巨焱拳」<c>BlazingPunch</c> 必须计 1 层。
///
/// 例：怪身上 2 层燃烧 → 打「观火」（施加 1 层）→ 层数不变，
/// 但本累加器 +1。
///
/// <para>
/// <b>为什么不能读战斗历史 <c>PowerReceivedEntry</c>：</b>
/// 那本表只在**数值真的变了**时才记录 ——
/// <c>PowerCmd.ModifyAmount</c> 里有 <c>if ((int)modifiedOffset != 0)</c>
/// 判断（<c>PowerCmd.cs:242-243</c>），取高值导致没变化时那条记录**根本不存在**，
/// 事后无法区分"没施加"和"施加了但没生效"。
/// 所以必须<strong>在施加的那一刻</strong>自己累加。
/// </para>
///
/// <para>
/// <b>数据来源是 <see cref="WizardEvents.BurnApplied"/>：</b>
/// 它由 <see cref="SpellCmd.ApplyBurn"/> 在所有分支上<b>无条件</b>触发，
/// 携带的是<b>请求层数</b>（不是最终层数），正是本累加器要的口径。
/// 而 <c>ApplyBurn</c> 是全部燃烧来源的唯一收口，所以这里不会漏。
/// （燃烧免疫时 <c>ApplyBurn</c> 直接 return，连事件都不发 ——
/// 那时"施加动作"确实不存在，不计也符合设计。）
/// </para>
///
/// <para>
/// <b>为什么用累加器而不是引擎钩子：</b>
/// 引擎的 <c>AfterPowerAmountChanged</c> 在 <c>offset == 0</c> 时不发，
/// 表达不了"无变化的施加" —— 这也是 <see cref="WizardEvents"/> 存在的原因。
/// </para>
///
/// <para>
/// <b>复位</b>：由 <see cref="Patches.BurnTallyResetPatch"/> 挂在
/// <c>Hook.BeforeCombatStart</c> 上清零（见该类注释说明为何必须显式复位）。
/// </para>
/// </summary>
public static class BurnTally
{
    /// <summary>
    /// 按施加者分别累计（key 为施加者 <c>Creature</c>）。
    ///
    /// <para>
    /// 用 <c>ConditionalWeakTable</c> 而不是 <c>Dictionary</c>：
    /// 后者会<b>永久持有</b> <c>Creature</c> 引用，把已经离场的敌人/玩家留在内存里。
    /// 弱表让随从与敌人被回收时条目自动消失。
    /// </para>
    /// </summary>
    private static readonly ConditionalWeakTable<Creature, StrongBox<decimal>> Totals = new();

    /// <summary>
    /// 记录一次由 <paramref name="applier"/> 施加的燃烧（<paramref name="amount"/> = 请求层数）。
    ///
    /// <para><paramref name="applier"/> 为 null 时忽略：那表示"非生物来源"，
    /// 与"我给予的燃烧"无关。</para>
    ///
    /// <para>
    /// ⚠️ <b>2026-10-13 修正：被「人工制品」抵消的施加不计。</b>
    /// 用户期望"只有实际真的给怪物身上上到了燃烧才计数"。
    /// 这里虽然与 <see cref="SpellCmd.ApplyBurn"/> 里那道拦截重复，
    /// 但<b>刻意保留双层</b> —— 本累加器是常驻单例、订阅的是全局事件，
    /// 任何绕过 <c>ApplyBurn</c> 直接 <c>RaiseBurnApplied</c> 的调用都会漏过第一层；
    /// 在"记账"这一层再判一次 artifact 最稳。
    /// </para>
    /// </summary>
    public static void Record(Creature? applier, decimal amount)
    {
        if (applier == null || amount <= 0m)
        {
            return;
        }

        Totals.GetOrCreateValue(applier).Value += amount;
    }

    /// <summary>该施加者在本场战斗中累计**施加**的燃烧层数。</summary>
    public static decimal TotalFor(Creature? applier)
    {
        if (applier == null)
        {
            return 0m;
        }

        return Totals.TryGetValue(applier, out StrongBox<decimal>? box) ? box.Value : 0m;
    }

    /// <summary>清空所有累计（新战斗开始时调用）。</summary>
    public static void Reset() => Totals.Clear();

    /// <summary>是否已订阅（幂等保护）。</summary>
    private static bool _subscribed;

    /// <summary>
    /// 常驻订阅 <see cref="WizardEvents.BurnApplied"/>（由 <c>Entry.Initialize</c> 调用一次）。
    ///
    /// <para>
    /// <b>"先退订再订阅"</b>是照抄「纵火狂」<c>PyromaniacPower.AfterApplied</c> 的写法：
    /// 保证即使被调用多次也不会挂上重复委托（否则一次施加会被计两次）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 这里**不退订**，是刻意的：本累加器必须在整局里一直待命，
    /// 包括"这张牌还没入手"的时候。复位靠 <see cref="Reset"/>（每场战斗开头）。
    /// </para>
    /// </summary>
    public static void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        WizardEvents.BurnApplied -= OnBurnApplied;
        WizardEvents.BurnApplied += OnBurnApplied;
        _subscribed = true;
    }

    private static Task OnBurnApplied(Creature target, Creature? applier, decimal amount, bool landed)
    {
        // ⚠️ 只有真的落上才记账（2026-10-13 第二次修正）。
        // 被「人工制品」抵消时 landed == false ⇒ 不计。
        // 判据由 SpellCmd.ApplyBurn 在施加那一刻算好传进来 ——
        // 在这里查 target.HasPower<ArtifactPower>() 是**错的**，
        // 引擎此时已经把 artifact 那一层减掉了。
        if (!landed)
        {
            return Task.CompletedTask;
        }

        Record(applier, amount);
        return Task.CompletedTask;
    }
}
