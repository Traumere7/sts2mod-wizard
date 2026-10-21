using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 超导：**你对**寒冷状态的敌人造成的**雷电伤害**提升。
/// 每张卡把自己那份额度**直接累加到 <c>Amount</c>** 上，<b>加算</b>。
///
/// <list type="bullet">
///   <item>1 张未升级：<c>Amount = 50</c> → 倍率 <c>1 + 50/100 = 1.5</c>（+50%）</item>
///   <item>2 张未升级：<c>Amount = 100</c> → <c>2.0</c>（+100%）</item>
///   <item>两张未升级 + 一张升级：<c>50+50+75 = 175</c> → <c>2.75</c>（+175%）</item>
/// </list>
///
/// <para>
/// <b>结构照抄原版「残酷」<c>Cruelty</c> / 「跟踪」<c>Tracking</c>：</b>
/// 那两张牌也把 <c>25</c> / <c>50</c> 这样的<b>百分点数直接记在玩家身上的 buff 里</b>
/// （<c>PowerVar&lt;T&gt;(25m)</c> + <c>PowerStackType.Counter</c>），
/// 计算时 `1 + Amount/100`。这样：
/// <list type="bullet">
///   <item>叠层天然正确 —— 引擎的 <c>ModifyAmount</c> 会把每张卡的值加起来</item>
///   <item>升级天然正确 —— 升级只是把本张卡贡献的数值从 50 改成 75</item>
///   <item><b>混合升级也不会错</b> —— 每张卡各贡献各的</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>曾经踩过的两种错法（都别再走）：</b>
/// <list type="number">
///   <item>把"倍率 1.5"存进能力的 <c>DynamicVars</c>，再乘层数。
///         引擎的层数只会累加 <c>Amount</c>，累加不到 <c>DynamicVars</c>，
///         所以要么不叠层、要么得自己维护 —— 后者在混合升级时会算成
///         <c>3 × 0.75 = 2.25</c>（实测就是显示 225%）。</item>
///   <item>把"增量 0.5"交给 <c>percentMore()</c>。
///         那个格式化器算的是 <c>(v-1)×100</c>，传 0.5 会得到 <b>-50</b>。
///         正解是根本不用它 —— 直接存百分点数，文案写 <c>{Amount}%</c>。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>这是独立的乘算区</b>，与寒冷、易伤相乘。
/// 例如同时有易伤(1.5) + 寒冷(1.5) + 超导(1.5) → 3.375 倍。
/// 所谓"加算"只发生在<b>本乘区内部</b>（多层超导之间）。
/// </para>
///
/// <para>
/// 挂在**玩家**身上（与电流外溢不同）—— 语义是主动的
/// "你打寒冷目标更疼"，而不是"寒冷目标挨打更疼"，因此卡面文案用主动句式。
/// </para>
/// </summary>
public class SuperconductPower : CustomPowerModel
{
    /// <summary>未升级卡牌贡献的百分点数。</summary>
    public const decimal BaseIncreasePercent = 50m;

    /// <summary>升级后每张卡多贡献的百分点数（50 → 75）。</summary>
    public const decimal UpgradeStep = 25m;

    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> 直接就是累计的百分点数（50 = +50%）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/superconduct_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/superconduct_power_big.png";

    /// <summary>
    /// 受伤乘算。返回 1 表示不缩放。
    /// 条件：造成伤害的是自己 + 目标是寒冷状态 + 伤害学派是雷电。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner)
        {
            return 1m;
        }

        if (target == null || !target.HasPower<ChillPower>())
        {
            return 1m;
        }

        if (SpellProps.Resolve(props, cardSource) != SpellSchool.Lightning)
        {
            return 1m;
        }

        // 与「残酷」CrueltyPower / 「跟踪」TrackingPower 完全一致：
        //   1 + Amount / 100
        // Amount 已经由引擎按每张卡贡献的数值累加好了。
        return 1m + Amount / 100m;
    }
}
