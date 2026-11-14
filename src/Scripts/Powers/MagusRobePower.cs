using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 伟大法袍：**你每给予敌人 1 层负面效果，就获得 1 点格挡**（无触发上限）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 两条数据来源，缺一不可
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>引擎钩子 <see cref="AfterPowerAmountChanged"/></b> —— 覆盖"任何负面效果"
///         （易伤、虚弱、寒冷、麻痹、电流外溢…包括原版/其它 mod 的负面）。
///         判定条件照抄「铅咒板」<c>CurseTablet</c>（那套口径是踩过坑才定下来的）：
///         施加者必须是自己、目标必须是敌人、必须是可见的、必须真的是负面
///         （用 <c>GetTypeForAmount(amount)</c> 按带符号量判定，不能用 <c>Type</c>）、
///         排除 <c>ITemporaryPower</c>（临时属性会在内部再施加一次真实能力，会重复计数）。</item>
///   <item><b>本 mod 的 <see cref="WizardEvents.BurnApplied"/></b> —— 覆盖**燃烧**。
///         燃烧是"取高值"的（<see cref="BurnPower"/>），给已有高层燃烧的敌人再加低层燃烧时
///         层数不变 ⇒ 引擎那条 <c>if ((int)modifiedOffset != 0) await Hook.AfterPowerAmountChanged(...)</c>
///         （<c>PowerCmd.cs:247</c>）**根本不发**。
///         但设计口径是"施加动作本身算数"，且格挡要按**请求层数**给 ——
///         所以燃烧一律走事件，并且在引擎钩子里 <c>power is BurnPower</c> 时直接跳过，
///         避免"燃烧真的涨了"时两条路都触发导致双倍格挡。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>为什么燃烧用"请求层数"而不是层数增量</b>：用户 2026-11-14 明确
/// "给予低层数的燃烧依旧算有效的给予负面效果动作，这张牌也此时能够起那个给予的燃烧层数的格挡"。
/// 事件参数里的 <c>amount</c> 正是<b>请求层数</b>（<see cref="SpellCmd.ApplyBurn"/> 无条件带上），
/// 而 <c>landed</c> 为 false 表示被「人工制品」抵消 —— 那种情况给 0 格挡。
/// </para>
///
/// <para>
/// <b>没有触发上限</b>：每次施加都触发，一张牌给多种/多层负面就触发多次。
/// 两张「伟大法袍」⇒ <c>Amount = 2</c> ⇒ 每层给 2 点格挡（线性叠加，见 <see cref="PerStackBlock"/>）。
/// </para>
/// </summary>
public class MagusRobePower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数器型：<c>Amount</c> = 每层负面给几倍格挡（每张「伟大法袍」+1）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/magus_robe_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/magus_robe_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    /// <summary>每 1 层负面给多少格挡（至少 1；两张法袍就是 2）。</summary>
    private decimal PerStackBlock => Math.Max(1m, Amount);

    /// <summary>用于退订幂等保护的标志。</summary>
    private bool _subscribed;

    // ══════════════════════════════════════════════════════════════════
    // 订阅 / 退订（照抄「纵火狂」PyromaniacPower 的生命周期）
    // ══════════════════════════════════════════════════════════════════
    // 能力每场战斗都会新建、战斗结束被清空，所以 AfterApplied 订阅；
    // 而 Player.AfterCombatEnd() 会直接清空能力、不保证逐个走 AfterRemoved，
    // 所以 AfterCombatEnd 还要兜底退订一次 —— 否则静态事件会留下悬挂委托。
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        WizardEvents.BurnApplied -= OnBurnApplied;
        WizardEvents.BurnApplied += OnBurnApplied;
        _subscribed = true;
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        WizardEvents.BurnApplied -= OnBurnApplied;
        _subscribed = false;
    }

    // ══════════════════════════════════════════════════════════════════
    // 来源 ①：引擎钩子 —— 除燃烧以外的所有负面
    // ══════════════════════════════════════════════════════════════════
    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 只认"我们自己施加的"。
        if (applier != Owner)
        {
            return Task.CompletedTask;
        }

        // 只认"落在敌人身上"的（这个钩子没有 target 参数，用 power.Owner）。
        if (!power.Owner.IsEnemy)
        {
            return Task.CompletedTask;
        }

        // 跳过隐藏的内部能力（如 TemporaryXxx 的内层）。
        if (!power.IsVisible)
        {
            return Task.CompletedTask;
        }

        // 燃烧走事件（见类注释）：这里跳过，避免双计。
        if (power is BurnPower)
        {
            return Task.CompletedTask;
        }

        // 排除临时属性能力：它们会在内部再施加一次真实能力，导致重复计数。
        if (power is ITemporaryPower)
        {
            return Task.CompletedTask;
        }

        // 必须是负面 —— 用 GetTypeForAmount(带符号量) 判定，不能用 power.Type。
        if (power.GetTypeForAmount(amount) != PowerType.Debuff)
        {
            return Task.CompletedTask;
        }

        // 负增量（例如被清除）不算"给予"。
        if (amount <= 0m)
        {
            return Task.CompletedTask;
        }

        return GainBlockForStacks(amount);
    }

    // ══════════════════════════════════════════════════════════════════
    // 来源 ②：本 mod 的燃烧事件 —— 按"请求层数"给格挡
    // ══════════════════════════════════════════════════════════════════
    private Task OnBurnApplied(Creature target, Creature? applier, decimal amount, bool landed)
    {
        // 静态事件可能比能力活得久：旧局的实例可能还挂着委托。
        if (Owner == null)
        {
            return Task.CompletedTask;
        }

        // 只认"我们自己施加给敌人的"。
        if (applier != Owner || !target.IsEnemy)
        {
            return Task.CompletedTask;
        }

        // 被「人工制品」抵消 ⇒ 没有真的给予，不给格挡。
        if (!landed)
        {
            return Task.CompletedTask;
        }

        return GainBlockForStacks(amount);
    }

    /// <summary>
    /// 按"给予的层数 × 每层格挡"发格挡。无上限、每次施加都触发。
    /// </summary>
    private async Task GainBlockForStacks(decimal stacks)
    {
        if (stacks <= 0m)
        {
            return;
        }

        Flash();

        await CreatureCmd.GainBlock(
            Owner,
            stacks * PerStackBlock,
            ValueProp.Unpowered,
            null);
    }
}
