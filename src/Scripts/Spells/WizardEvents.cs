using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 巫师 mod 自己的内容事件。
///
/// <para>
/// <b>为什么不复用引擎钩子</b>：游戏在 <c>PowerCmd.ModifyAmount</c> 里用
/// <c>if ((int)modifiedOffset != 0) await Hook.AfterPowerAmountChanged(...)</c>
/// （<c>PowerCmd.cs:247</c>）—— <b>数值没变化时钩子根本不发</b>。
/// 而设计的语义要求是"<b>施加动作</b>本身算数，与数值是否变化无关"：
/// 敌人已有 4 层燃烧，再施加 1 层，结果仍是 4 层，但这<b>算一次有效的"给予燃烧"</b>，
/// 「纵火狂」「巨焱拳」这类效果必须响应。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>但"施加动作算数"不等于"被抵消也算数"</b>（2026-10-13 第二次修正）。
/// 引擎的「人工制品」<c>ArtifactPower</c> 会把这次施加<b>整个抵消</b>
/// （把 amount 归零并减掉自己一层）。那种情况<b>不该</b>触发这些效果。
/// </para>
///
/// <para>
/// 难点在于<b>「被抵消」与「取高值导致层数没变」在事后看起来一模一样</b>，
/// 而按下面两种时序都会判错：
/// <list type="bullet">
///   <item>在事件处理里查 <c>target.HasPower&lt;ArtifactPower&gt;()</c> ——
///         <b>太晚</b>：引擎此时已经把那一层 artifact 减掉了
///         （减层发生在 <c>Hook.AfterModifyingPowerAmountReceived</c>，
///         即 <c>PowerCmd.Apply:156</c>），查询结果必为 false。
///         这正是用户实测到的"最后一层 artifact 被打掉时纵火狂照样触发"。</item>
///   <item>只看层数变化 —— <b>两种情况的层数都没变</b>，分不开。</item>
/// </list>
/// 所以判据必须由<b>施加方</b>在施加的那一刻算出来（那里还能看到快照），
/// 再通过 <see cref="BurnAppliedLanded"/> 事件参数传给订阅者。
/// </para>
/// </summary>
public static class WizardEvents
{
    /// <summary>
    /// 一次"给予燃烧"动作。
    ///
    /// <para>
    /// 参数：目标、施加者、本次请求的层数（不是最终层数）、
    /// <b>是否真的落到了目标身上</b>（见 <see cref="BurnAppliedLanded"/>）。
    /// </para>
    /// </summary>
    public static event Func<Creature, Creature?, decimal, bool, Task>? BurnApplied;

    /// <summary>
    /// 触发"给予燃烧"。
    ///
    /// <para>
    /// ⚠️ <b>无论是否被抵消都会触发</b>（这样才能把"被抵消"这个事实告诉订阅者）。
    /// 订阅者必须自己判断 <paramref name="landed"/>：
    /// 为 <c>false</c> 表示这次被「人工制品」抵消了，不该产生任何收益。
    /// </para>
    ///
    /// <para>
    /// <b>为什么不让本方法直接不发</b>：那样订阅者就永远看不到"被抵消"这件事，
    /// 也就无法在需要时做别的处理（例如播放一个"被挡下"的表现）。
    /// 把判断权交给订阅者更灵活，且语义显式。
    /// </para>
    /// </summary>
    public static async Task RaiseBurnApplied(
        Creature target, Creature? applier, decimal amount, bool landed)
    {
        Func<Creature, Creature?, decimal, bool, Task>? handlers = BurnApplied;
        if (handlers == null)
        {
            return;
        }

        // 逐个 await，保证顺序确定（不并行），与游戏自身的钩子派发风格一致。
        foreach (Func<Creature, Creature?, decimal, bool, Task> handler in handlers.GetInvocationList()
                     .Cast<Func<Creature, Creature?, decimal, bool, Task>>())
        {
            await handler(target, applier, amount, landed);
        }
    }

    /// <summary>
    /// 「这次施加是否真的落到了目标身上」的定义 —— 所有订阅者共用同一口径。
    ///
    /// <list type="bullet">
    ///   <item><c>true</c>：
    ///     <list type="bullet">
    ///       <item>燃烧层数真的上升了；<b>或</b></item>
    ///       <item>目标已有更高层燃烧、这次"取高值"没升上去，
    ///             但<b>没有被抵消</b> —— 按设计口径这仍算一次有效的施加。</item>
    ///     </list></item>
    ///   <item><c>false</c>：被目标的「人工制品」抵消（层数没变且 artifact 吃掉了这次施加）。</item>
    /// </list>
    /// </summary>
    public const string BurnAppliedLanded = "landed";

    /// <summary>
    /// 一次"给予寒冷"动作。
    /// 参数：目标、施加者、本次施加的层数、<b>是否真的落上</b>。
    ///
    /// <para>
    /// 与 <see cref="BurnApplied"/> 同样由 <see cref="SpellCmd.ApplyChill"/> 触发。
    /// ⚠️ <b>寒冷没有"取高值"语义</b>（<c>ChillPower</c> 是可累加的 <c>Counter</c>），
    /// 所以 <c>landed</c> 为 false <b>只可能</b>是因为被人工制品抵消。
    /// 「弹射」<c>DoubleRicochet</c> 的"每当施加寒冷时返回手牌"就订阅这个事件。
    /// </para>
    /// </summary>
    public static event Func<Creature, Creature?, decimal, bool, Task>? ChillApplied;

    /// <summary>触发"给予寒冷"。</summary>
    public static async Task RaiseChillApplied(
        Creature target, Creature? applier, decimal amount, bool landed)
    {
        Func<Creature, Creature?, decimal, bool, Task>? handlers = ChillApplied;
        if (handlers == null)
        {
            return;
        }

        foreach (Func<Creature, Creature?, decimal, bool, Task> handler in handlers.GetInvocationList()
                     .Cast<Func<Creature, Creature?, decimal, bool, Task>>())
        {
            await handler(target, applier, amount, landed);
        }
    }
}
