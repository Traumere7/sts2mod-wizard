using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Cards;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 全知形态：**每当你给予敌人本回合尚未给予过的一种负面效果时，
/// 获得 1 点能量并抽 N 张牌**（N = 层数，升级后 2）。
///
/// <para>
/// 触发点用 <c>BeforePowerAmountChanged</c> 而<b>不是</b> <c>AfterPowerAmountChanged</c>。
/// 后者是"数值真的变了才发"：
/// <code>
/// // PowerCmd.cs:247
/// if ((int)modifiedOffset != 0)
///     await Hook.AfterPowerAmountChanged(...);
/// </code>
/// 而 <c>BeforePowerAmountChanged</c> 是<b>无条件</b>发的
/// （<c>PowerCmd.cs:231</c> 的 <c>ModifyAmount</c>、<c>PowerCmd.cs:124</c> 的 <c>Apply</c>），
/// 正好对应"<b>施加动作本身算数</b>"的语义 ——
/// 敌人已有 3 层易伤、本回合第一次再给 3 层（层数不变），也必须触发。
/// </para>
///
/// <para>
/// ⚠️ 两个分支的初始化时机不同：<c>Apply</c> 走的是新建实例，那时
/// <c>power.Owner</c> <b>还是 null</b>（要等 <c>ApplyInternal</c> 才赋值），
/// 所以<b>不能用 <c>power.Owner</c> 判断阵营</b>，要用钩子显式传进来的
/// <c>target</c>（它就是接受者）。
/// </para>
///
/// <para>
/// <b>种类去重</b>：按 <c>PowerModel.Id</c> 记录本回合已给予过的种类，
/// 与「魔力汲取」「降雷」的"1 种"口径一致。
/// </para>
///
/// <para>
/// <b>状态重置</b>：<b>敌我双方</b>的回合开始都清空已记录的种类
/// （"本回合" = <b>当前进行中的那一方的回合</b>）。
/// 2026-10-21 从"仅玩家回合开始"改成这样 —— 目的是让"敌人回合中被攻击后
/// 由「冰之城墙」施加的寒冷/冻结"也能算作本回合第一次 ⇒ 照常触发。
/// 详见 <see cref="AfterSideTurnStart"/> 的注释。
/// </para>
///
/// <para>
/// ⚠️ <b>存档后必须重置</b>：<c>_appliedThisTurn</c> 是普通字段，
/// 既不在存档里也不会自动清空。Save&amp;Load 会克隆能力实例，
/// 若不在 <c>AfterCloned</c> 里清掉，重进后那些种类仍被当成"本回合已给过"
/// → 本回合剩余时间再也无法触发。反过来说，重进时把记录清空是<b>安全</b>的：
/// 最坏情况只是同一种类在本回合被重复触发一次，而漏触发是硬失效。
/// </para>
///
/// <para>
/// <b>只认"我施加的、施在敌人身上的"</b>：
/// <c>applier != Owner</c> 直接跳过（别人上 debuff 不该让我获益）；
/// 且接受者必须在 <c>Owner</c> 的对立面（自己给自己上燃烧不算"给予敌人"）。
/// </para>
/// </summary>
public class OmniscientFormPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/omniscient_form_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/omniscient_form_power_big.png";

    /// <summary>本回合已经"给予过"的负面效果种类（按 ModelId 去重）。</summary>
    private readonly HashSet<ModelId> _appliedThisTurn = [];

    /// <summary>
    /// 存档读取 / 克隆后清空"本回合"记录。
    ///
    /// <para>
    /// 字段是 <c>readonly</c>，所以只能 <c>Clear()</c> 不能重新赋值。
    /// 详见类注释里"存档后必须重置"一段。
    /// </para>
    /// </summary>
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _appliedThisTurn.Clear();
    }

    /// <summary>是否已订阅燃烧事件（退订时的幂等保护）。</summary>
    private bool _subscribed;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 先退订再订阅，保证不会重复挂同一个委托。
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

    /// <summary>
    /// 战斗结束兜底退订。
    ///
    /// <para>
    /// 用 <c>AfterCombatEnd</c> 而<b>不是</b> <c>AfterCombatVictory</c>：
    /// <c>Player.AfterCombatEnd()</c> 会清空能力，而 <c>Hook.AfterCombatVictory</c>
    /// 在那之后才跑 —— 为 Power 重写的 <c>AfterCombatVictory</c> 永不触发
    /// （见 <c>CONTEXT.md</c> §6.4）。
    /// </para>
    /// </summary>
    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
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

    /// <summary>
    /// 燃烧被施加时触发。
    ///
    /// <para>
    /// <b>为什么光有 <see cref="BeforePowerAmountChanged"/> 不够：</b>
    /// 燃烧是"取高值"的，<c>BurnPower.ApplyAbsolute</c> 在
    /// <b>现值已不低于目标时直接 return，完全不碰引擎</b> ——
    /// 于是 <c>BeforePowerAmountChanged</c> 根本不会发。
    /// 表现就是"怪物身上已有高层数燃烧时，再施加低层数燃烧不触发全知形态"。
    /// </para>
    ///
    /// <para>
    /// <see cref="SpellCmd.ApplyBurn"/> 在<b>任何分支</b>都会触发本事件
    /// （"施加动作本身算数"），所以它才是覆盖完整的那条路。
    /// </para>
    ///
    /// <para>
    /// <b>不会与 <see cref="BeforePowerAmountChanged"/> 重复触发</b>：
    /// 两者都走 <see cref="_appliedThisTurn"/> 去重，第二次 <c>Add</c> 返回 false。
    /// </para>
    /// </summary>
    private async Task OnBurnApplied(Creature target, Creature? applier, decimal amount, bool landed)
    {
        if (applier != Owner)
        {
            return;
        }

        // ⚠️ 燃烧这条路径**已经**按 landed 过滤（2026-10-13 第二次修正），
        // 所以 ObserveDebuff 不需要再判 artifact —— 而且在那里判是**错的**：
        // 被抵消时引擎已把 artifact 那层减掉，"最后一层被打掉"会被误判成没抵消。
        if (!landed)
        {
            return;
        }

        await ObserveDebuff(target, ModelDb.GetId<BurnPower>());
    }
    /// <summary>
    /// <b>任一方的回合开始</b>都重置"本回合"记录（2026-10-21 用户要求）。
    ///
    /// <para>
    /// ⚠️ 旧实现只在<b>玩家</b>回合开始重置
    /// （<c>side == Player &amp;&amp; participants.Contains(Owner)</c>）。
    /// 那样"本回合"只覆盖玩家自己的回合，于是
    /// <b>敌人回合里由防守反击类效果施加的负面效果会被算作"本回合已给过"</b>
    /// 而不触发 —— 典型例子是「冰之城墙」<see cref="FrostWallPower"/>：
    /// 它在<b>敌人攻击时</b>才给敌人上冻结/寒冷，
    /// 若玩家自己回合已经给过寒冷，敌人回合这次就不再抽牌。
    /// </para>
    ///
    /// <para>
    /// 改成双方回合开始都清空后，"本回合"= <b>当前进行中的那一方的回合</b>：
    /// 敌人回合开始时会重新清空一次，于是敌人回合中途（被攻击后）施加的
    /// 寒冷/冻结算作"本回合第一次" ⇒ 「全知形态」照常给能量与抽牌。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>不能保留 <c>participants.Contains(Owner)</c> 这半个条件</b>：
    /// <c>participants</c> 是"<b>正在开始回合的那一方</b>的生物"，
    /// 敌人回合时里面全是敌人，永远不含 <c>Owner</c>（玩家），
    /// 保留它等于敌人回合仍然不重置。
    /// 这里可以无条件清空 —— <c>Hook.AfterSideTurnStart</c>
    /// （<c>Hook.cs</c>）会遍历<b>全部</b>战斗监听者（不分阵营），
    /// 而本方法只在战斗中触发，清空一个纯内存集合没有任何副作用。
    /// </para>
    ///
    /// <para>
    /// 📌 <b>「熔火审判」<see cref="PyroJudgmentPower"/> 不要跟着改</b>：
    /// 它的 <c>AfterSideTurnStart</c> 是"每回合把应施加的燃烧层数 +1"，
    /// 属于<b>自身的成长节奏</b>，与"本回合已给过哪些种类"的记忆无关。
    /// </para>
    /// </summary>
    public override Task AfterSideTurnStart(
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        IReadOnlyList<Creature> participants,
        MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        // 敌我双方回合开始都重置。
        _appliedThisTurn.Clear();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 非燃烧类的负面效果：靠引擎钩子观察。
    ///
    /// <para>
    /// ⚠️⚠️ <b>2026-10-13 第二次修正：必须用
    /// <see cref="AfterPowerAmountChanged"/>，不能用 <c>BeforePowerAmountChanged</c>。</b>
    /// </para>
    ///
    /// <para>
    /// <b>为什么：</b>「人工制品」的抵消发生在
    /// <c>Hook.ModifyPowerAmountReceived</c>（<c>PowerCmd.Apply:131</c>），
    /// 而 <c>BeforePowerAmountChanged</c> 在它<b>之前</b>就发了
    /// （<c>PowerCmd.Apply:124</c>），此时 <c>amount</c> 还是原始值、目标身上 artifact 也还在 ——
    /// 所以旧版会<b>把被抵消的施加也当成"给予了负面效果"</b>（用户实测报告的 bug #2/#3）。
    /// </para>
    ///
    /// <para>
    /// <b>为什么 <c>AfterPowerAmountChanged</c> 是对的：</b>
    /// <c>PowerCmd.Apply:157</c> 是
    /// <code>if (modifiedAmount != 0m) await Hook.AfterPowerAmountChanged(...);</code>
    /// —— 被抵消时 <c>modifiedAmount</c> 已被归零，<b>这一句根本不发</b>。
    /// 换成它在语义上等价于"这次施加真的被引擎接受了"。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>那"层数没变也算"的口径怎么办？</b>
    /// 那是为<b>燃烧</b>的"取高值"设计的（已有 4 层时再施加 1 层不涨层）。
    /// 而本节注释钉死了"<b>非燃烧类</b>"：本 mod 的非燃烧负面
    /// （寒冷/易伤/麻痹）都是普通累加型 <c>Counter</c>，**不存在层数没变的情况**，
    /// 所以用后置钩子不会漏触发。燃烧那条路走
    /// <c>WizardEvents.BurnApplied</c>（带 <c>landed</c>），见 <see cref="OnBurnApplied"/>。
    /// </para>
    /// </summary>
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 只认"我施加的"。
        if (applier != Owner)
        {
            return;
        }

        // 只认"负面效果"。
        if (power.Type != PowerType.Debuff)
        {
            return;
        }

        // 只认"施加"（正数）—— 移除/递减（负数）不该算"给予"。
        if (amount <= 0m)
        {
            return;
        }

        // ⚠️ 目标用 `power.Owner` 而不是参数 —— 这个钩子**没有 target 参数**，
        //    接受者就是该能力的拥有者（`PowerModel.Owner`）。
        //    （对比 `BeforePowerAmountChanged` 是有 `target` 参数的。）
        Creature? target = power.Owner;
        if (target == null)
        {
            return;
        }

        // 燃烧走它自己的路径（那边带 landed 过滤），这里跳过以免重复触发。
        if (power is BurnPower)
        {
            return;
        }

        await ObserveDebuff(target, power.Id);
    }

    /// <summary>
    /// 记录"本回合由我给某敌人施加了某种负面效果"，首次则给予奖励。
    ///
    /// <para>
    /// 两条触发路径（引擎钩子 + 燃烧事件）<b>共用这里</b>，
    /// 靠 <see cref="_appliedThisTurn"/> 去重，所以同一次施加不会给两份奖励。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 本方法<b>不</b>自己判「人工制品」—— 判据已上移到两条入口：
    /// 引擎钩子用 <c>AfterPowerAmountChanged</c>（被抵消时不发），
    /// 燃烧路径用 <c>landed</c>。
    /// <b>在这里查 <c>target.HasPower&lt;ArtifactPower&gt;()</c> 是错的</b>：
    /// 被抵消时引擎已把 artifact 那层减掉，查询必为 false。
    /// </para>
    /// </summary>
    private async Task ObserveDebuff(Creature target, ModelId powerId)
    {
        if (Amount <= 0)
        {
            return;
        }

        // 接受者必须在对立面：自己给自己上燃烧不算"给予敌人"。
        if (target.Side == Owner.Side)
        {
            return;
        }

        // 本回合已经给过这一种 ⇒ 不重复触发。
        if (!_appliedThisTurn.Add(powerId))
        {
            return;
        }

        await Grant();
    }

    private async Task Grant()
    {
        // Creature.Player 对**敌人**恒为 null（只有 Creature(Player, ...) 那个构造会赋值）。
        // 本能力只可能挂在玩家身上，所以这里必定非 null —— 但 nullable 是开启的，
        // 必须显式判空，否则 CS8604，而且一旦假设不成立就是开局即崩。
        Player? player = Owner.Player;
        if (player == null)
        {
            return;
        }

        Flash();

        await PlayerCmd.GainEnergy(1m, player);

        await CardPileCmd.Draw(
            new ThrowingPlayerChoiceContext(),
            Amount,
            player);
    }
}
