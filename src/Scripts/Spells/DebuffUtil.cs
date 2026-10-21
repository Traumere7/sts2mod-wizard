using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 「这次施加负面效果，到底有没有真的落到对方身上」的统一判据。
///
/// ══════════════════════════════════════════════════════════════════
/// 为什么需要它（2026-10-13 用户报告的 bug）
/// ══════════════════════════════════════════════════════════════════
/// 引擎的「人工制品」<see cref="ArtifactPower"/> 会**抵消**一次负面效果：
///
/// <code>
/// // ArtifactPower.cs
/// public override bool TryModifyPowerAmountReceived(
///     PowerModel canonicalPower, Creature target, decimal amount, ..., out decimal modifiedAmount)
/// {
///     if (target != base.Owner) { modifiedAmount = amount; return false; }
///     if (canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff) { ...return false; }
///     if (!canonicalPower.IsVisible) { ...return false; }
///     modifiedAmount = 0m;      // ★ 层数直接归零
///     return true;
/// }
/// public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
///     => await PowerCmd.Decrement(this);      // ★ 消耗一层人工制品
/// </code>
///
/// ⚠️ <b>但"施加动作"本身照样会发生</b> ——
/// <c>BeforePowerAmountChanged</c> 在抵消<b>之前</b>就已派发，
/// 而本 mod 自建的 <c>WizardEvents.BurnApplied</c> 更是<b>完全绕过引擎</b>、无条件触发。
/// 于是所有这些"每当给予负面效果/燃烧时"的效果<b>都会被 artifact 骗到</b>：
/// <list type="bullet">
///   <item>「全知形态」<c>OmniscientFormPower</c> —— 靠 <c>BeforePowerAmountChanged</c> + <c>BurnApplied</c></item>
///   <item>「纵火狂」<c>PyromaniacPower</c> —— 靠 <c>BurnApplied</c></item>
///   <item>「巨焱拳」的燃烧计数 <see cref="BurnTally"/> —— 靠 <c>BurnApplied</c></item>
/// </list>
/// 用户期望：<b>被 artifact 抵消就不该触发</b>。
///
/// <para>
/// ⚠️ <b>本判据只回答"会不会被 artifact 挡"</b>，不回答"层数有没有变化"。
/// 两者是不同的问题：
/// 「巨焱拳」的设计口径是<b>"施加动作"计数</b>（见 <see cref="BurnTally"/> 类注释）——
/// 目标已有 2 层燃烧时再施加 1 层，层数不变<b>仍要计 1</b>；
/// 但被 artifact 抵消时<b>不该计</b>。所以判据是"artifact"而不是"层数变了"。
/// </para>
///
/// <para>
/// <b>与 <c>UnsettlingLamp</c> 一致</b>：原版「不祥之灯」也是用
/// <c>target.HasPower&lt;ArtifactPower&gt;()</c> 来跳过被抵消的施加
/// （<c>UnsettlingLamp.cs:102</c>）。本类是把这个判据收口出来，避免各处各写一遍。
/// </para>
/// </summary>
public static class DebuffUtil
{
    /// <summary>
    /// ⚠️⚠️⚠️ <b>2026-10-13 第二次修正后，本类只在"施加之前、且施加后不再需要判断"时使用。</b>
    ///
    /// <para>
    /// <b>血的教训</b>：本类最初被用来在 <c>SpellCmd.ApplyBurn</c> / <c>ApplyChill</c>
    /// 里<b>提前 return</b>，结果同时造成三个 bug：
    /// <list type="number">
    ///   <item><b>燃烧/寒冷不再消耗「人工制品」层数</b> ——
    ///         因为减层是引擎在 <c>Hook.AfterModifyingPowerAmountReceived</c> 里做的
    ///         （<c>PowerCmd.Apply:156</c> / <c>PowerCmd.ModifyAmount:246</c>），
    ///         提前 return 让整条路径都没跑；</item>
    ///   <item><b>禁书目录四种类只减 2 层</b> —— 同理，被我们拦掉的那些没走到引擎；</item>
    ///   <item><b>"打掉最后一层 artifact"时仍会触发收益</b> ——
    ///         因为本判据查的是"现在有没有 artifact"，而那一刻引擎<b>已经把它减掉了</b>，
    ///         查询返回 false ⇒ 被误判成"没被抵消"。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>正确做法：让引擎自己抵消</b>（它会正确地减一层 artifact 并把 amount 归零），
    /// 然后按"这次施加是否被引擎接受"来判断收益是否成立：
    /// <list type="bullet">
    ///   <item><b>非燃烧负面</b> ⇒ 用 <c>AbstractModel.AfterPowerAmountChanged</c>。
    ///         被抵消时 <c>PowerCmd.Apply:157</c> 的
    ///         <c>if (modifiedAmount != 0m)</c> 不成立，钩子<b>根本不发</b>。
    ///         见 <c>OmniscientFormPower.AfterPowerAmountChanged</c>。</item>
    ///   <item><b>燃烧</b> ⇒ 用 <c>WizardEvents.BurnApplied</c> 的 <c>landed</c> 参数。
    ///         判据必须由<b>施加方</b>在施加那一刻用快照算出（那时还看得见 artifact），
    ///         再传给订阅者。见 <c>SpellCmd.ApplyBurn</c> 与 <c>BurnPower.ApplyAbsolute</c>。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>判断"能不能用本类"的准则</b>：
    /// 只有当<b>调用点本身</b>就是"施加动作的发起处"、
    /// 且<b>不需要让引擎去消耗 artifact 层数</b>时才可以用（例如"某个效果整个失效，连尝试都不该发生"）。
    /// 凡是"要被 artifact 抵消并减层"的施加，<b>一律不要在这里拦</b>。
    /// </para>
    /// </summary>
    public static bool BlockedByArtifact(Creature? target)
        => target == null || target.HasPower<ArtifactPower>();

    /// <summary>
    /// 这次负面效果施加是否<b>真的会落到</b>目标身上
    /// （存活 且 不被人工制品抵消）。
    ///
    /// <para>
    /// ⚠️ 刻意<b>不</b>检查"燃烧免疫"之类的具体免疫能力：
    /// 那些应该在各自的施加收口里拦（例如
    /// <see cref="SpellCmd.ApplyBurn"/> 已经拦了 <c>BurnImmunityPower</c> 并直接 return，
    /// 连事件都不发）。本判据只负责引擎级的通用抵消 —— 人工制品。
    /// </para>
    ///
    /// <para>⚠️ 使用限制见 <see cref="BlockedByArtifact"/> 的注释。</para>
    /// </summary>
    public static bool DebuffLandsOn(Creature? target)
        => target != null && target.IsAlive && !BlockedByArtifact(target);
}
