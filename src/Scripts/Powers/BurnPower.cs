using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;
using MegaCrit.Sts2.Core.Models.Powers;
using BaseLib.Hooks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using System;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 燃烧：**不可叠加**，每次施加时取层数最高值；**每回合不衰减**。
/// **在附着者所在阵营的回合开始时**受到等同于燃烧层数的伤害
/// （敌人身上即"它出手之前"；玩家身上即"自己回合开始"，两者等效 ——
/// 实现见下方的 <see cref="AfterSideTurnStart"/>）。
///
/// 取高值由 <see cref="TryModifyPowerAmountReceived"/> 实现：
/// 把传入的 <c>amount</c> 当作"期望的绝对层数"，换算成差值返回，
/// 因此 <c>PowerCmd.Apply</c> 的默认加法叠加会被纠正为"取高值"。
/// 这样任何调用方（包括其它 mod）走标准 API 都能得到正确行为。
///
/// 伤害**不受任何加成影响**（力量、易伤、法术增益、寒冷都不生效）：
/// 用 <c>Unblockable | Unpowered</c> 让所有以 <c>IsPoweredAttack()</c> 为条件的加成都失效，
/// 且 dealer 传 null，因此不触发吸血、击杀回能等需要伤害来源的效果。
/// 这与原版毒的写法一致（<c>PoisonPower.Trigger</c>）。
///
/// ⚠️ 已知例外：原版的 <c>IntangiblePower</c> / <c>HardToKillPower</c>（伤害上限）
/// 与 <c>HardenedShellPower</c>（HP 损失修正）不看 <c>ValueProp</c>，仍会削减燃烧伤害。
/// 它们只会压低、不会放大。
/// </summary>
/// <summary>
/// <c>ApplyAbsolute</c> 的结果。
///
/// <para>
/// <b>为什么需要这个返回值</b>（2026-10-13 用户报告的 bug）：
/// 调用方 <see cref="Spells.SpellCmd.ApplyBurn"/> 要决定"是否发
/// <c>WizardEvents.BurnApplied</c>"，而
/// <b>"被人工制品抵消"和"取高值导致层数没变"必须区分开</b>：
/// </para>
/// <list type="bullet">
///   <item>被 artifact 抵消 ⇒ <b>不算</b>给予燃烧（纵火狂/巨焱拳/全知形态都不该触发）；</item>
///   <item>目标已有更高层燃烧、这次取高值没变 ⇒ <b>仍然算</b>一次有效的施加
///         （这是 <c>BurnTally</c> 类注释里写明的设计口径）。</item>
/// </list>
/// <para>
/// 两者都可能表现为"层数没变"，所以判据是"<b>这次写入是否真的被引擎接受</b>"。
/// </para>
/// </summary>
public readonly record struct BurnApplicationResult(bool Landed);

public class BurnPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/burn_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/burn_power_big.png";

    // ══════════════════════════════════════════════════════════════════
    // 血条预测覆盖层：把"下次结算将要烧掉的血"染成橙黄色一段
    // ══════════════════════════════════════════════════════════════════
    //
    // <para>
    // <b>为什么只要重写这一个方法：</b>
    // <c>CustomPowerModel</c> 的声明里<b>已经</b>带了
    // <c>IHealthBarForecastSource</c> 接口（<c>BaseLib.Abstracts/CustomPowerModel.cs:8</c>），
    // 默认实现返回空列表。所以这里 <c>override</c> 一下就生效 ——
    // <b>不需要显式声明接口，也不需要注册</b>：
    // <c>HealthBarForecastRegistry</c> 会自动扫
    // <c>creature.Powers.OfType&lt;IHealthBarForecastSource&gt;()</c>
    // （<c>HealthBarForecastRegistry.cs:60</c>）。
    // </para>
    //
    // <para>
    // <b>渲染全部由 BaseLib 的补丁接管</b>（<c>HealthBarForecastPatch.cs</c>）：
    // 它 Harmony 了 <c>NHealthBar.RefreshForeground</c> / <c>RefreshMiddleground</c> /
    // <c>RefreshText</c>，并**自己创建容器与分段节点**（拿原版
    // <c>%PoisonForeground</c> 当模板）。所以本 mod
    // **不碰 <c>health_bar.tscn</c>、不注入任何节点**。
    // </para>
    //
    // <para>
    // ⚠️ <b><c>FromRight</c> 的排布语义</b>（<c>HealthBarForecastPatch.cs:151-202</c>）：
    // 从"扣掉毒之后的血量"起，按 <c>(Order, SequenceOrder)</c> 依次<b>从右往左切片</b>，
    // 每段被剩余血量<b>截断</b>；盖满时红条自动隐藏。
    // ⇒ 毒由原版脚本用 <c>%PoisonForeground</c> 单独画在最右边
    //   （BaseLib 只是把它预先扣掉当起点，<c>:150</c>），
    //   **「燃烧」段紧接在毒的左边**。
    // </para>
    //
    // <para>
    // ⚠️ <b>为什么 <c>Order</c> 传默认 0（不做特殊规定）</b>：
    // 多个 forecast 来源之间按 <c>sequenceOrder</c> 排，而它是按
    // <c>creature.Powers</c> 的枚举下标给的（<c>Registry.cs:60-67</c> 的 <c>num++</c>），
    // 那个列表是 <b>按施加顺序 append</b> 的（<c>Creature.cs</c> 的 <c>_powers.Add(power)</c>）。
    // ⇒ <b>顺序自然等于"玩家给予的顺序"</b>，正是期望的口径，不需要额外干预。
    // </para>
    //
    // <para>
    // <b>颜色选择</b>：血条上已有四种颜色，必须都能区分开 ——
    // <list type="bullet">
    //   <item>当前血量 <c>HpForeground</c> 红 <c>(0.8, 0.047, 0.047)</c></item>
    //   <item>掉血残影 <c>HpMiddleground</c> 橙 <c>(1, 0.498, 0)</c></item>
    //   <item>毒 <c>PoisonForeground</c> 绿 <c>(0.4745, 0.7529, 0.2353)</c></item>
    //   <item>灾厄 <c>DoomForeground</c> 紫 <c>(0.3529, 0.2588, 0.6471)</c></item>
    // </list>
    // ⇒ 取<b>橙黄、明显偏黄</b>的 <c>(1, 0.78, 0.12)</c>：
    // 与红的色相拉开约 47°，与残影橙也拉得开（那个 G 只有 0.498）。
    // </para>
    //
    // <para>
    // <b><c>affectsHpLabel</c> 用默认的 <c>true</c></b>：这段能烧光时血量数字也跟着变色，
    // 与毒/灾厄的行为一致（<c>HealthBarForecastSegment</c> 的构造默认值）。
    // <c>Single(int, Color, Direction, int order = 0)</c> 那条重载已经是
    // <c>affectsHpLabel: true</c>，所以直接用它即可。
    // </para>

    /// <summary>
    /// 血条预测段：在右侧把"下次结算将要被烧掉的血量"染成橙黄色。
    ///
    /// <para>
    /// 层数即伤害 —— 燃烧每次结算造成等同于当前层数的伤害
    /// （见 <see cref="Trigger"/>），所以 <c>Amount</c> 直接取层数。
    /// 且该伤害是 <c>Unblockable | Unpowered</c>，格挡拦不住，
    /// 所以"预测掉血量"就是层数本身，不需要任何扣减。
    /// </para>
    /// </summary>
    public override IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(
        HealthBarForecastContext context)
    {
        int amount = context.Creature.GetPowerAmount<BurnPower>();

        // Amount <= 0 时给空列表。
        // （BaseLib 侧还会再滤一次 Amount > 0，见 Registry.cs 的 Where；
        //   这里早退只是省掉一次构造。）
        if (amount <= 0)
        {
            return [];
        }

        return HealthBarForecasts.Single(
            amount,
            BurnForecastColor,
            HealthBarForecastDirection.FromRight);
    }

    /// <summary>
    /// 燃烧预测段的颜色：橙黄、明显偏黄 —— <c>(1, 0.78, 0.12)</c> ≈ <c>RGB(255, 199, 31)</c>。
    ///
    /// <para>
    /// <b>取值经过一轮试错</b>（2026-10-16）：
    /// <list type="number">
    ///   <item>初版就是本值 —— 用户反馈"有点太黄了，稍微调偏红一点"，
    ///         并给出 <c>RGB(250, 138, 40)</c>；</item>
    ///   <item>改用 <c>RGB(250, 138, 40)</c> 后<b>用户自己发现</b>该值与
    ///         「掉血残影」橙 <c>HpMiddleground</c> <c>(1, 0.498, 0)</c> ≈ <c>RGB(255, 127, 0)</c>
    ///         只差 <c>(5, 11, 40)</c>（欧氏距离 <b>0.164</b>，而本值是 <b>0.307</b>），
    ///         同屏容易误读 ⇒ <b>决定回滚成偏黄的本值</b>。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>不要再往红/橙方向调。</b> 这条血条上已有四种颜色，可用的余地很小：
    /// <list type="bullet">
    ///   <item>当前血量 <c>HpForeground</c> 红 <c>(0.8, 0.047, 0.047)</c></item>
    ///   <item>掉血残影 <c>HpMiddleground</c> 橙 <c>(1, 0.498, 0)</c> ← <b>红橙区间已被它占了</b></item>
    ///   <item>毒 <c>PoisonForeground</c> 绿 <c>(0.4745, 0.7529, 0.2353)</c></item>
    ///   <item>灾厄 <c>DoomForeground</c> 紫 <c>(0.3529, 0.2588, 0.6471)</c></item>
    /// </list>
    /// 可区分的方向只剩"更黄"或"更亮/加自发光"。
    /// </para>
    /// </summary>
    private static readonly Color BurnForecastColor = new(1f, 0.78f, 0.12f);

    /// <summary>
    /// 能力自身文案里 <c>{Amount}</c> 的来源（由 <c>PowerModel</c> 基类自动注入层数）。
    /// 这里保留一个与类型同名的变量，便于卡面 <c>PowerVar&lt;BurnPower&gt;</c> 与悬停提示对齐。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(nameof(BurnPower), 0m),
    ];

    /// <summary>
    /// 把"绝对层数"语义纠正为"取高值"。
    ///
    /// 仅在"目标身上已有燃烧"这条路径上被调用，且转换后：
    /// <c>newAmount = Amount + delta = max(Amount, 期望值)</c>。
    ///
    /// <para>
    /// ⚠️ <b>这个钩子不能作为唯一实现</b>（见 <see cref="ApplyAbsolute"/>）。
    /// 原因：<c>PowerCmd.ModifyAmount</c> 是
    /// <c>SetAmount(power.Amount + delta)</c> —— <b>赋值，不是累加</b>。
    /// 而同一批结算里这个钩子可能被<b>并发</b>调用多次（都还没写入），
    /// 于是它们读到同一个旧 <c>Amount</c>，后算出的 <c>delta</c> 会把先算的覆盖掉。
    ///
    /// 实测日志（两怪战斗，同回合「观火」→「火球术」）：
    /// <code>
    /// 施加前: 层数=1 请求=2 来源=WIZARD-FIREBALL
    ///   取高值: 实例#1C4385A 现有=1 期望=2 delta=1   ← 火球术，算对了
    ///   取高值: 实例#1C4385A 现有=1 期望=1 delta=0   ← 同一实例又算一次（读数仍是旧值）
    /// 施加后: 层数=1（期望 2）                        ← delta=0 覆盖了 delta=1
    /// </code>
    /// 结论：<b>单靠"换算 delta"无法在并发调用下保证取高值</b>。
    /// </para>
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;

        if (canonicalPower is not BurnPower burn)
        {
            return false;
        }

        // ApplyAbsolute 在锁内自行处理换算，避免与这里重复。
        if (burn.SuppressReceivedHook)
        {
            return false;
        }

        // 首次施加时 Owner 尚未绑定，直接放行。
        if (burn.Owner == null || burn.Owner != target)
        {
            return false;
        }

        if (amount <= 0m)
        {
            return false;
        }

        int desired = (int)amount;
        int delta = Math.Max(0, desired - burn.Amount);

        Log.Info($"[Wizard/Burn] 取高值: 实例#{burn.GetHashCode():X} 现有={burn.Amount} 期望={desired} delta={delta} " +
                 $"target={target.Monster?.Id.Entry ?? target.Player?.Character.Id.Entry ?? "?"}");

        if (delta == (int)amount)
        {
            return false;
        }

        // 差值可能被压到 0（新层数不高于现有层数），此时不允许移除能力。
        modifiedAmount = delta;
        return true;
    }

    /// <summary>
    /// ApplyAbsolute 在"读现状 → 算差值 → 写入"期间置为 true，
    /// 让 <see cref="TryModifyPowerAmountReceived"/> 不再重复换算。
    /// </summary>
    private bool SuppressReceivedHook { get; set; }

    /// <summary>把 ApplyAbsolute 的"读-算-写"变成原子段，避免并发互相覆盖。</summary>
    private static readonly SemaphoreSlim ApplyLock = new(1, 1);



    /// <summary>
    /// <b>推荐的施加入口</b>：以"绝对值 + 取高值"语义设置燃烧层数。
    ///
    /// <para>
    /// 与 <see cref="TryModifyPowerAmountReceived"/> 的区别是它
    /// <b>在锁内完成"读现状 → 算差值 → 写入"</b>，
    /// 因此并发调用不会互相覆盖：后进入者读到的是已写入的新值，
    /// 算出 0 差值后<b>直接返回、不做任何写入</b>，不会把层数压回低值。
    /// </para>
    ///
    /// <para>
    /// <b>为什么必须上锁：</b><c>PowerCmd.ModifyAmount</c> 内部会
    /// <c>SetAmount(power.Amount + delta)</c>。若两个调用在"算差值"与"写入"之间交错，
    /// 后写入者会基于陈旧读数计算，从而把层数压回低值 —— 这正是实测到的 bug。
    /// </para>
    ///
    /// <para>
    /// ⚠️⚠️ <b>绝不在这里拦「人工制品」</b>（2026-10-13 第二次修正）。
    /// 减 artifact 层数是<b>引擎</b>在 <c>Hook.AfterModifyingPowerAmountReceived</c>
    /// 里做的（<c>PowerCmd.Apply:156</c> / <c>PowerCmd.ModifyAmount:246</c>）。
    /// 上一版在 <see cref="Spells.SpellCmd.ApplyBurn"/> 里提前 <c>return</c>，
    /// 导致引擎那条路径根本没跑 ⇒ <b>燃烧不减 artifact 层数</b>（用户实测报告）。
    /// 正解是<b>让引擎跑到</b>，再按返回值决定是否算"给予燃烧"。
    /// </para>
    /// </summary>
    public static async Task<BurnApplicationResult> ApplyAbsolute(
        PlayerChoiceContext choiceContext,
        Creature target,
        int desired,
        Creature? applier,
        CardModel? cardSource)
    {
        if (desired <= 0 || !target.IsAlive)
        {
            return new BurnApplicationResult(false);
        }

        string who = target.Monster?.Id.Entry ?? target.Player?.Character.Id.Entry ?? "?";

        await ApplyLock.WaitAsync();
        try
        {
            // 锁内重读权威状态（前一次可能刚写入）。
            int before = target.GetPowerAmount<BurnPower>();

            // 现状已经不低于目标 ⇒ 等价的"取高值"结果，不做任何写入。
            //
            // ⚠️ 判据分两种（这是"取高值"与"被抵消"唯一能区分的地方）：
            //   · **有 artifact** ⇒ 这次施加会被抵消 ⇒ Landed = false
            //     （artifact 会吃掉这一层，不该算"给予了燃烧"）；
            //   · **无 artifact** ⇒ 只是层数没升上去，**仍然算**一次有效施加
            //     （BurnTally 的设计口径：目标已有 2 层时再施加 1 层要计数）。
            if (before >= desired)
            {
                bool blocked = target.HasPower<ArtifactPower>();
                Log.Info($"[Wizard/Burn] 取高值(锁内): target={who} 现值={before} 请求={desired} "
                         + $"无需改动；artifact={blocked}");
                return new BurnApplicationResult(!blocked);
            }

            BurnPower? existing = target.GetPower<BurnPower>();
            if (existing != null)
            {
                existing.SuppressReceivedHook = true;
                try
                {
                    await PowerCmd.ModifyAmount(
                        choiceContext, existing, desired - before, applier, cardSource);
                }
                finally
                {
                    existing.SuppressReceivedHook = false;
                }
            }
            else
            {
                // 首次施加：这一条会完整走引擎的 `PowerCmd.Apply`，
                // 因此 artifact 的 `TryModifyPowerAmountReceived` 会把 amount 归零，
                // 并在 `AfterModifyingPowerAmountReceived` 里**减掉一层 artifact**
                // —— 这正是"燃烧要能消耗人工制品"所依赖的路径。
                await PowerCmd.Apply<BurnPower>(
                    choiceContext, target, desired, applier, cardSource);
            }

            int after = target.GetPowerAmount<BurnPower>();
            Log.Info($"[Wizard/Burn] 取高值(锁内): target={who} {before} -> {after}（请求 {desired}）");

            // ⚠️⚠️ 层数变了就**立刻重绘血条**（2026-10-16 用户拍板）。
            //
            // <para>
            // <b>为什么必须手动补这一下：</b>
            // 血条预测段的数据是我们在
            // <see cref="GetHealthBarForecastSegments"/> 里提供的，
            // 但**渲染**由 BaseLib 的 <c>HealthBarForecastPatch</c> 挂在
            // <c>NHealthBar.RefreshForeground</c> 的 Postfix 上完成。
            // 而 <c>NHealthBar.RefreshValues()</c> 的调用点只有四处
            // （<c>NCreatureStateDisplay.cs</c> 的 <c>SetCreature</c> /
            //   <c>SetCreatureBounds</c> / <c>OnCombatStateChanged</c> / Block 变化），
            // 「燃烧层数增加」这条路径**一个都不经过** ——
            // <c>NCreature.OnPowerIncreased</c>（<c>NCreature.cs:800</c>）只播 VFX，
            // 没有调 <c>RefreshValues</c>。
            // ⇒ 不补的话，色段要等下次掉血 / 格挡变化 / 悬停才更新。
            // </para>
            //
            // <para>
            // <b>为什么放在 <c>ApplyLock</c> 里、写完层数之后：</b>
            // 这样重绘读到的一定是<b>刚写入的新层数</b>，不会闪出旧值。
            // </para>
            //
            // <para>
            // ⚠️ <b>只在层数真的变了才刷</b>（<c>after != before</c>）：
            // 取高值没变时色段本来就不需要动，白刷一次纯属浪费。
            // 但**仍要保留"取高值也算落上"的返回值语义**（见下方判据）——
            // 那是给「纵火狂」「巨焱拳」用的，与刷不刷血条无关。
            // </para>
            if (after != before)
            {
                RefreshHealthBar(target);
            }

            // ★ 判据：这次写入是否真的被引擎接受。
            //   · 正常施加 ⇒ after 上升 ⇒ true；
            //   · 被 artifact 抵消 ⇒ amount 被归零、after 不变 ⇒ false。
            //
            // ⚠️ 取高值语义下层数只会上升或不变，所以 `after > before` 足够；
            //    不必用 `!=`（那会把"被抵消"和"没变化"混在一起）。
            return new BurnApplicationResult(after > before);
        }
        finally
        {
            ApplyLock.Release();
        }
    }

    /// <summary>
    /// 立刻重绘该生物的血条，让燃烧预测段同步到当前层数。
    ///
    /// <para>
    /// 路径：<c>Creature.GetCreatureNode()</c>（<c>Creature.cs:758</c>）
    /// → <c>NCreature</c> 下唯一名 <c>%HealthBar</c> 的
    /// <c>NCreatureStateDisplay</c>（<c>NCreature.cs:464</c>）
    /// → 它内部 <c>%HealthBar</c> 的 <c>NHealthBar</c>（<c>NCreatureStateDisplay.cs:198</c>）
    /// → <c>NHealthBar.RefreshValues()</c>（<b>public</b>，<c>NHealthBar.cs:381</c>）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 与 <c>FrozenIntentPatch.cs:95</c> 的
    /// <c>__instance.Creature.GetCreatureNode()?.RefreshIntents()</c> 同一套写法。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>只在战斗内有效</b>：<c>GetCreatureNode()</c> 内部走
    /// <c>NCombatRoom.Instance?.GetCreatureNode(this)</c>，非战斗场景返回 null
    /// （见 <c>WizardDeathSprite</c> 的类注释）。
    /// 但燃烧本身也只在战斗里存在，所以非战斗场景静默跳过即可。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>整个方法吞掉异常</b>：刷血条纯属表现层，
    /// 绝不能因为找不到节点而把战斗逻辑带崩。
    /// </para>
    /// </summary>
    private static void RefreshHealthBar(Creature creature)
    {
        try
        {
            NCreature? node = creature.GetCreatureNode();
            if (node == null || !GodotObject.IsInstanceValid(node))
            {
                return;
            }

            // NCreature._stateDisplay 就是 %HealthBar（NCreature.cs:464）。
            if (node.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar") is not { } display)
            {
                return;
            }

            // NCreatureStateDisplay 内部那颗 NHealthBar（NCreatureStateDisplay.cs:198）。
            display.GetNodeOrNull<NHealthBar>("%HealthBar")?.RefreshValues();
        }
        catch (Exception e)
        {
            Log.Warn($"[Wizard/BurnForecast] 重绘血条失败（不影响战斗逻辑）：{e.Message}");
        }
    }

    /// <summary>
    /// <b>被附着者在其回合开始时</b>受到等同于燃烧层数的伤害。
    ///
    /// 时点是"拥有者所在阵营的回合**开始**"，因此对玩家和敌人**完全等效**：
    /// 敌人身上的燃烧在敌人回合开始时烧它（出手之前）；
    /// 玩家身上的燃烧（「决心」「高温消毒」会给自己上）在玩家回合开始时烧自己。
    ///
    /// 与 <c>PoisonPower</c> 用同一个钩子，区别只有两点：
    /// <list type="bullet">
    ///   <item>燃烧**每回合不衰减**（毒每次结算后 <c>Decrement</c>），层数只是伤害量</item>
    ///   <item>燃烧一次结算全额（毒按 <c>TriggerCount</c> 分次结算）</item>
    /// </list>
    ///
    /// <para>
    /// ⚠️ 伤害本体在 <see cref="Trigger"/> 里，本方法只是个"到点了就结算"的触发器
    /// —— 这样「天火浩荡」<c>SkyfireRampage</c> 的"立刻触发一次燃烧结算"
    /// 就能直接复用同一条路径，不必抄一遍伤害公式。
    /// 结构与原版 <c>PoisonPower</c>（<c>AfterSideTurnStart</c> → <c>Trigger()</c>）一致。
    /// </para>
    /// </summary>
    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        await Trigger();
    }

    /// <summary>
    /// <b>立刻结算一次燃烧</b>：拥有者受到等同于当前层数的伤害。
    ///
    /// <para>
    /// 照抄原版 <c>PoisonPower.Trigger()</c>（<c>Outbreak</c>「毒性爆发」
    /// 就是循环调它来"立刻触发"）：
    /// <list type="bullet">
    ///   <item>层数 <b>不</b>因此减少（燃烧与毒的关键区别：毒结算后 <c>Decrement</c>）；</item>
    ///   <item>伤害是 <c>Unblockable | Unpowered</c> —— 无视格挡、不吃任何加成
    ///         （与回合开始那次结算完全一致）。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>调用前请自行确认 <c>Amount &gt; 0</c> 且 <c>!Owner.IsDead</c></b> ——
    /// 本方法在无效状态下直接返回，不会抛异常，但调用方通常还想跳过后续步骤。
    /// </para>
    /// </summary>
    public async Task Trigger()
    {
        if (Amount <= 0 || Owner == null || Owner.IsDead)
        {
            return;
        }

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner,
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
    }
}
