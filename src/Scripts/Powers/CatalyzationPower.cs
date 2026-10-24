using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 催化：<b>你每生成 4(3) 张卡牌，就获得 1 点能量</b>。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「生成卡牌」的触发钩子：<c>AfterCardGeneratedForCombat</c>
/// ══════════════════════════════════════════════════════════════════
/// 照抄原版「创世之柱」<c>PillarOfCreationPower</c>：
/// <code>
/// public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
/// {
///     if (creator != null &amp;&amp; creator.Creature == base.Owner) { ... }
/// }
/// </code>
/// <c>CardPileCmd.AddGeneratedCardToCombat</c> 会发这个钩子，
/// 所以本 mod 里所有走"生成"路径的加牌（「镜像」的复制品、
/// 怪物塞的「灼伤」等）都会被数到。
/// ⚠️ 走普通 <c>CardPileCmd.Add</c>（非 generated）的加牌**不算**生成。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 叠层口径：<c>PowerInstanceType.Instanced</c> —— 每打一张就是独立的一个计数器
/// ══════════════════════════════════════════════════════════════════
/// 用户 2026-10-25 明确："这种循环触发的效果**不设置叠层逻辑**，
/// 而是每开一张就再给予玩家一个该效果的 clone，每个该种效果具有独立计数器。"
/// 这正是原版「环绕轨道」<c>OrbitPower</c> 的做法：
/// <list type="number">
///   <item><c>public override PowerInstanceType InstanceType =&gt; PowerInstanceType.Instanced;</c>
///         —— 每次 <c>PowerCmd.Apply</c> 都新建一个<b>独立实例</b>（不会合并成一叠）；</item>
///   <item><c>protected override object InitInternalData() =&gt; new Data();</c>
///         —— 每个实例自带一份私有计数；</item>
///   <item><c>DisplayAmount</c> 改成"还差几张触发"，配合
///         <c>InvokeDisplayAmountChanged()</c> 实时刷新。</item>
/// </list>
/// ⇒ 打两张「催化」会有两个图标，各自独立数 4 张；
/// 生成第 4 张时两个都各给 1 点能量（共 2 点），与设计一致。
/// </para>
///
/// <para>
/// ⚠️ <b>阈值（4 / 升级后 3）是"每实例"的，所以不能放在 <c>Amount</c> 里</b>
/// —— <c>Amount</c> 被当作"每次给多少能量"（=1）。
/// 阈值由卡牌在 <c>PowerCmd.Apply</c> 之后调用 <see cref="SetThreshold"/> 写入
/// （照抄原版「夜魇」<c>NightmarePower.SetSelectedCard</c> 的"Apply 后再塞参数"模式）。
/// </para>
/// </summary>
public class CatalyzationPower : CustomPowerModel
{
    /// <summary>每个实例的私有计数。</summary>
    private sealed class Data
    {
        /// <summary>已经生成过多少张卡牌。</summary>
        public int Generated;

        /// <summary>已经触发过多少次（用来算"这次该给几份能量"）。</summary>
        public int Triggered;

        /// <summary>每多少张触发一次（4 / 升级后 3）。</summary>
        public int Threshold = DefaultThreshold;
    }

    private const int DefaultThreshold = 4;

    private const string CardsKey = "Cards";

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>⚠️ 每打一张就是独立实例（各自计数），不叠层。</summary>
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/catalyzation_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/catalyzation_power_big.png";

    /// <summary>
    /// 图标上显示"还差几张触发"（同「环绕轨道」的 <c>4 - spent % 4</c>）。
    /// 刚打完还没生成任何牌时显示满额（4 或 3）。
    /// </summary>
    public override int DisplayAmount
    {
        get
        {
            Data data = GetInternalData<Data>();
            int threshold = Math.Max(1, data.Threshold);
            int remainder = data.Generated % threshold;
            return remainder == 0 ? threshold : threshold - remainder;
        }
    }

    /// <summary>供文案与悬停提示使用：<c>{Cards}</c> = 触发阈值。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(DefaultThreshold),
    ];

    protected override object InitInternalData() => new Data();

    /// <summary>
    /// 由卡牌在 <c>PowerCmd.Apply</c> 之后调用，写入本实例的阈值
    /// （4，升级后 3），并同步 <c>{Cards}</c> 让描述文本跟着变。
    /// </summary>
    public void SetThreshold(int threshold)
    {
        Data data = GetInternalData<Data>();
        data.Threshold = Math.Max(1, threshold);

        // 让 {Cards} 显示成实际阈值（升级后是 3 而不是 4）。
        DynamicVars[CardsKey].BaseValue = data.Threshold;

        InvokeDisplayAmountChanged();
    }

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只认"自己生成的牌"：别人（或怪物）造的牌不该给我能量。
        if (creator == null || creator.Creature != Owner)
        {
            return;
        }

        // 本能力只会挂在玩家身上；这里挡一层空引用（编译器也要求）。
        Player? player = Owner.Player;
        if (player == null)
        {
            return;
        }

        Data data = GetInternalData<Data>();
        data.Generated++;

        int threshold = Math.Max(1, data.Threshold);
        int shouldHaveTriggered = data.Generated / threshold;

        if (shouldHaveTriggered > data.Triggered)
        {
            int times = shouldHaveTriggered - data.Triggered;
            Flash();

            // Amount = 每次触发给多少能量（design 固定 1）。
            await PlayerCmd.GainEnergy(Amount * times, player);

            data.Triggered = shouldHaveTriggered;
        }

        InvokeDisplayAmountChanged();
    }
}
