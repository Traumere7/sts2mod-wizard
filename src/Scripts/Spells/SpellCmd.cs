using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Spells;

/// <summary>
/// 法术相关的常用操作。
/// </summary>
public static class SpellCmd
{
    /// <summary>
    /// 施加燃烧，遵循"不可叠加、取层数最高值"的规则。
    ///
    /// <para>
    /// <b>层数换算交给 <see cref="BurnPower.ApplyAbsolute"/>，本方法不碰层数。</b>
    /// 那里在锁内完成"读现状 → 算差值 → 写入"，因此并发调用（例如
    /// 「观火」给两只怪各施加 1 层、紧接着「火球术」施加 2 层）
    /// 不会互相覆盖。
    /// </para>
    ///
    /// <para>
    /// <b>为什么不能只靠 <see cref="BurnPower.TryModifyPowerAmountReceived"/>：</b>
    /// <c>PowerCmd.ModifyAmount</c> 是赋值（<c>SetAmount(Amount + delta)</c>）而非累加，
    /// 同一批里被并发调用时会各自读到旧值，后算出的 delta=0 会把先算的 delta=1 覆盖掉。
    /// 实测日志见 <see cref="BurnPower.TryModifyPowerAmountReceived"/> 的注释。
    /// </para>
    ///
    /// <para>
    /// 结算完层数后，<b>无条件</b>触发一次 <see cref="WizardEvents.BurnApplied"/>：
    /// 设计的语义是"施加动作本身算数" —— 敌人已有 4 层时再施加 1 层，结果仍是 4 层，
    /// 但这算一次有效的"给予燃烧"，「纵火狂」等效果必须响应。
    /// 引擎的 <c>AfterPowerAmountChanged</c> 做不到这点（它在 <c>offset == 0</c> 时不发，
    /// 见 <c>PowerCmd.cs:247</c>）。
    /// </para>
    /// </summary>
    public static async Task ApplyBurn(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount <= 0m || !target.IsAlive)
        {
            return;
        }

        // ⚠️ 燃烧免疫：拥有者不会获得燃烧，**直接整个跳过**（连"施加动作"都不算）。
        //
        // ⚠️ 这里是**所有**燃烧来源的唯一收口（「高温消毒」「决心」、火球术系列、
        // 以及一切未来来源都走它），所以一处拦截即可覆盖全部。
        //
        // 为什么不在 BurnPower 的 TryModifyPowerAmountReceived 里拦：
        // 那个钩子只能**改数值**、没有否决语义，表达不了"这次施加不发生"。
        //
        // "连施加动作都不算"是有意的：免疫期间燃烧根本不存在，
        // 相关的"每当给予燃烧"效果自然也不该被自己触发。
        if (target.HasPower<BurnImmunityPower>())
        {
            return;
        }

        // ⚠️⚠️⚠️ **绝不要在这里拦「人工制品」**（2026-10-13 第二次修正）。
        //
        // 上一版就是在这里 `if (DebuffUtil.BlockedByArtifact(target)) return;`，
        // 结果同时造成三个 bug（用户实测报告）：
        //   ① 燃烧**不减人工制品层数** —— 因为"减层"是引擎在
        //      `Hook.AfterModifyingPowerAmountReceived` 里做的
        //      （见 `PowerCmd.Apply:156` / `PowerCmd.ModifyAmount:246`），
        //      我们提前 return 就整条路径都没跑；
        //   ② 禁书目录四种类只减 2 层 —— 同理，我拦掉的那些没走到引擎；
        //   ③ 纵火狂照样触发 —— 因为 `BlockedByArtifact` 判的是"现在有没有 artifact"，
        //      而"这一击把最后一层 artifact 打掉"时它返回 false，事件就发了。
        //
        // ✅ 正解：**放手让引擎自己抵消**（它会正确地减一层 artifact、
        //    并把 amount 归零），我们只在"是否要发事件"上做判断。
        //    判据见下面 `Landed`。
        BurnApplicationResult result =
            await BurnPower.ApplyAbsolute(choiceContext, target, (int)amount, applier, cardSource);

        // ⚠️ **总是**发事件，把 `Landed` 一起传出去（2026-10-13 第二次修正）。
        //
        // `result.Landed` 由 `ApplyAbsolute` 用"施加前快照"算出：
        //   · 被 artifact 抵消 ⇒ false；
        //   · 取高值导致层数没变（目标已有更高层燃烧）⇒ true（仍算一次有效施加）；
        //   · 正常施加 ⇒ true。
        //
        // ⚠️ 为什么不让本方法替订阅者过滤掉 landed == false 的情况：
        //    订阅者在事件回调里**查不出**这个事实 —— 引擎此时已经把 artifact
        //    那一层减掉了（减层在 `PowerCmd.Apply:156`），
        //    `target.HasPower<ArtifactPower>()` 必为 false。
        //    所以必须由施加方在这里算好并传出去。
        await WizardEvents.RaiseBurnApplied(target, applier, amount, result.Landed);
    }
    /// <summary>对多个目标施加燃烧。</summary>
    public static async Task ApplyBurnToAll(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        foreach (Creature target in targets)
        {
            await ApplyBurn(choiceContext, target, amount, applier, cardSource);
        }
    }

    /// <summary>
    /// 施加寒冷的**统一收口**（2026-10-04 新增）。
    ///
    /// <para>
    /// <b>为什么需要它：</b>在此之前寒冷有 4 个各自为政的施加点
    /// （<c>IceShard</c> / <c>MagicalIndex</c> / <c>ColdWavePower</c> / <c>FrostWallPower</c>），
    /// 都是直接 <c>PowerCmd.Apply&lt;ChillPower&gt;</c>。于是**任何
    /// "每当施加寒冷时"的效果都无从挂载** —— 「弹射」<c>DoubleRicochet</c> 正是第一个这种效果。
    /// 这与燃烧那边的 <see cref="ApplyBurn"/> + <see cref="WizardEvents.BurnApplied"/> 是同一套设计。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>与燃烧的关键区别：寒冷没有"取高值"语义。</b>
    /// <c>ChillPower</c> 是普通的可累加 <c>Counter</c>，直接 <c>PowerCmd.Apply</c> 即可，
    /// 不需要 <c>ApplyAbsolute</c> 那种"锁内读改写"。所以本方法只做两件事：
    /// 施加层数、然后无条件触发事件。
    /// </para>
    ///
    /// <para>
    /// <b>事件"无条件"触发是有意的</b>：语义是"施加动作本身算数"，
    /// 与燃烧那边一致（那里是为了让"取高值导致层数没变"也算一次）。
    /// </para>
    /// </summary>
    public static async Task ApplyChill(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount <= 0m || !target.IsAlive)
        {
            return;
        }

        // ⚠️⚠️⚠️ **绝不要在这里拦「人工制品」**（2026-10-13 第二次修正）。
        // 上一版在这里 `if (DebuffUtil.BlockedByArtifact(target)) return;`，
        // 后果与燃烧那边完全一样：引擎的"减一层 artifact"路径
        // （`PowerCmd.Apply:156` 的 `AfterModifyingPowerAmountReceived`）压根没跑。
        //
        // ✅ 正解：让引擎自己抵消 + 减层，我们只在"是否真落上"上做判断。
        bool hadArtifact = target.HasPower<ArtifactPower>();

        await PowerCmd.Apply<ChillPower>(choiceContext, target, amount, applier, cardSource);

        // ⚠️ **总是**发事件，把 `landed` 一起传出去（与燃烧那边同一口径）。
        //
        // 寒冷是累加型（不存在"取高值导致层数没变"），所以
        // `landed == false` **只可能**是因为被人工制品抵消。
        // 「弹射」等订阅者据此决定是否触发。
        await WizardEvents.RaiseChillApplied(target, applier, amount, !hadArtifact);
    }

    /// <summary>对多个目标施加寒冷（照抄 <see cref="ApplyBurnToAll"/> 的结构）。</summary>
    public static async Task ApplyChillToAll(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        foreach (Creature target in targets)
        {
            await ApplyChill(choiceContext, target, amount, applier, cardSource);
        }
    }
}
