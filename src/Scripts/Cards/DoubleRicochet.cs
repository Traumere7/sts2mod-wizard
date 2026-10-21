using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 弹射 / Double Ricochet - 0 费 - 攻击 - 普通
/// 造成 2(3) 点[orange]火焰伤害[/orange]。造成 2(3) 点[blue]雷电伤害[/blue]。
/// 每当你施加[gold]寒冷[/gold]时，从弃牌堆返回你的手牌。
///
/// <para>
/// <b>「从弃牌堆返回手牌」照抄观者 mod 的「疾风连击」<c>FlurryOfBlows</c>：</b>
/// 它是在触发时机里判 <c>this.Pile.Type == PileType.Discard</c>，
/// 满足才 <c>CardPileCmd.Add(this, PileType.Hand, …)</c>。
/// 那正是本牌需要的判据 —— 见下面"为什么不会无限"。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b>为什么加了「大寒波」也不会无限连</b>（这是本条设计的核心）：
/// 打出的牌在结算期间位于 <c>PileType.Play</c>，**不是** <c>Discard</c>。
/// 「大寒波」<see cref="ColdWavePower"/> 用的是 <c>BeforeCardPlayed</c>，
/// 它在本牌 <c>OnPlay</c> **之前**触发 —— 那一刻本牌还在 <c>Play</c> 堆里，
/// 所以 <c>Pile.Type == Discard</c> 为假，**不会**立刻弹回。
/// 只有等本牌结算完、进了弃牌堆之后，**再打一张牌**触发寒冷时，才会满足判据而弹回。
/// 于是形成"打一张 → 弹回 → 再打一张 → 再弹回"的循环，而不是同一张牌自我循环。
/// </para>
///
/// <para>
/// <b>伤害分两段、且各自带自己的法术流派</b>（火焰一段、雷电一段）：
/// 这样「寒冷」只加成火焰段、「超导」只加成雷电段，
/// 各自按自己的流派规则结算 —— 用两次独立的 <c>DamageCmd.Attack</c> 实现。
/// </para>
///
/// <para>
/// <b>事件订阅与退订</b>：本牌进弃牌堆后需要一直听着"施加寒冷"，
/// 所以订阅挂在 <c>AfterApplied</c>？不 —— 本牌是 <b>卡</b>不是能力，
/// 生命周期按"是否在战斗中"算。这里用
/// <see cref="AfterCardPlayed"/> 之后仍然订阅、并在
/// <c>AfterCombatEnd</c> 退订的方式（见实现）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class DoubleRicochet : WizardSpellCard
{
    private const int Cost = 0;

    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>雷电段伤害的变量键（火焰段复用 <c>DynamicVars.Damage</c>）。</summary>
    private const string LightningKey = "DoubleRicochetLightning";

    /// <summary>防止"同一个寒冷事件里重复弹回"。</summary>
    private bool _returning;

    /// <summary>
    /// **本回合是否已经弹回过了**（每回合限 1 次）。
    ///
    /// <para>
    /// ⚠️ <b>2026-10-06 新增：设计加了一句"（每回合限 1 次）"。</b>
    /// 目的是堵住"**1 张「大寒波」+ 2 张「弹射」**"的无限：
    /// 「大寒波」在每张牌打出前各给一次寒冷，两张「弹射」就能靠
    /// "A 弹回 → 打出 A → B 弹回 → 打出 B → A 弹回 → …"无限循环。
    /// 加上"每张牌每回合只能弹回 1 次"后，两张「弹射」每回合合计只能弹 2 次，
    /// 循环被截断。
    /// </para>
    ///
    /// <para>
    /// 注意这是**每张牌各自**一次（不是整场战斗每回合共 1 次）——
    /// 所以 2 张「弹射」每回合仍然可以各弹 1 次，只是无法形成无限。
    /// </para>
    /// </summary>
    private bool ReturnedThisTurn { get; set; }

    /// <summary>是否已订阅（幂等 + 退订保护）。</summary>
    private bool _subscribed;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    /// <summary>
    /// ⚠️⚠️ <b>2026-10-06 修：雷电段必须是 <see cref="DamageVar"/>，不能是普通 <c>DynamicVar</c>。</b>
    ///
    /// <para>
    /// 实测反馈："牌面文本只有火焰伤害使用了实时计算伤害的动态值，
    /// 雷电伤害那句是写死的静态值（实际结算是正确的，只是文本问题）"。
    /// </para>
    ///
    /// <para>
    /// <b>根因</b>：<c>{X:diff()}</c> 渲染的是 <c>DynamicVar.PreviewValue</c>，
    /// 而**只有 <see cref="DamageVar"/> 会重写 <c>UpdateCardPreview</c>**
    /// —— 它在里面跑 <c>Hook.ModifyDamage</c>（力量、易伤、寒冷、法术增益等）
    /// 并把结果写进 <c>PreviewValue</c>。普通 <c>DynamicVar</c> 的
    /// <c>PreviewValue</c> 永远等于 <c>BaseValue</c>，所以卡面只会显示基础值 2/3，
    /// 不随任何加成变化。
    /// </para>
    ///
    /// <para>
    /// <b>修法</b>：<see cref="DamageVar"/> 有带名字的重载
    /// <c>DamageVar(string name, decimal damage, ValueProp props)</c>，
    /// 所以第二段也能是真正的伤害变量，只是换个键名（默认键是 <c>"Damage"</c>）。
    /// </para>
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 火焰段（吃「寒冷」加成）。用默认键 "Damage"。
        new DamageVar(2m, ValueProp.Move | SpellProps.For(SpellSchool.Fire)),

        // 雷电段（吃「超导」加成、不吃「寒冷」）。换键名以免与上面撞车。
        // ⚠️ 必须也是 DamageVar，否则卡面不会实时计算（见上面注释）。
        new DamageVar(LightningKey, 2m, ValueProp.Move | SpellProps.For(SpellSchool.Lightning)),
    ];

    public DoubleRicochet()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // ① 火焰段（吃「寒冷」加成）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(SpellSchool.Fire))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        // ② 雷电段（吃「超导」加成，不吃「寒冷」）。
        await DamageCmd.Attack(LightningVar.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(SpellSchool.Lightning))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);
    }

    /// <summary>
    /// 雷电段的伤害变量（独立于火焰段，升级两者同涨）。
    /// 取出为 <see cref="DamageVar"/> 便于升级时调 <c>UpgradeValueBy</c>。
    /// </summary>
    private DamageVar LightningVar => (DamageVar)DynamicVars[LightningKey];

    /// <summary>
    /// 打出后开始监听"施加寒冷"（打出的那一刻才需要它；
    /// 但订阅是幂等的，多调无妨）。
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        WizardEvents.ChillApplied -= OnChillApplied;
        WizardEvents.ChillApplied += OnChillApplied;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        WizardEvents.ChillApplied -= OnChillApplied;
        _subscribed = false;
    }

    /// <summary>
    /// 每当施加寒冷时：若本牌**此刻在弃牌堆里**且**本回合还没弹过**，则返回手牌。
    ///
    /// <para>
    /// ⚠️ <c>Pile.Type == PileType.Discard</c> 这个判据<b>同时</b>承担了两件事：
    /// ① 只在"已经进弃牌堆"时才回手；② 结算期间（在 <c>Play</c> 堆）天然不触发，
    /// 所以不会与「大寒波」形成自我循环。详见类注释。
    /// </para>
    ///
    /// <para>
    /// <b>再加一道"每回合限 1 次"</b>（见 <see cref="ReturnedThisTurn"/> 的注释）：
    /// 防止"1 张「大寒波」+ 2 张「弹射」"直接构成无限。
    /// </para>
    /// </summary>
    private async Task OnChillApplied(Creature target, Creature? applier, decimal amount, bool landed)
    {
        if (_returning)
        {
            return;
        }

        // ⚠️ 2026-10-13 修正：被「人工制品」抵消的寒冷不算"施加了寒冷"。
        // 判据用事件传来的 landed。寒冷是累加型、不存在"取高值层数没变"，
        // 所以 landed == false 只可能是被抵消。
        if (!landed)
        {
            return;
        }

        // 只响应"我们自己施加的寒冷"（别人给敌人上寒冷不该让我回手）。
        if (applier != Owner.Creature)
        {
            return;
        }

        // ★ 每回合限 1 次。
        if (ReturnedThisTurn)
        {
            return;
        }

        if (Pile is not { Type: PileType.Discard })
        {
            return;
        }

        // 手牌已满就不弹（Add 超限会丢牌）。
        int freeSlots = CardPile.MaxCardsInHand - PileType.Hand.GetPile(Owner).Cards.Count;
        if (freeSlots <= 0)
        {
            return;
        }

        _returning = true;
        try
        {
            await CardPileCmd.Add(this, PileType.Hand);

            // 成功回手之后才记数 —— 上面任何一条提前返回都不该消耗这次机会。
            ReturnedThisTurn = true;
        }
        finally
        {
            _returning = false;
        }
    }

    /// <summary>战斗结束退订，避免跨战斗残留（静态事件的经典坑，见 CONTEXT.md §5）。</summary>
    public override Task AfterCombatEnd(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        Unsubscribe();
        return Task.CompletedTask;
    }

    /// <summary>
    /// **玩家回合开始时重置"本回合已弹回"**（每回合限 1 次的另一半）。
    ///
    /// <para>
    /// 用 <c>AfterSideTurnStart(side = Player)</c>，与本 mod 其它"每回合一次"
    /// 的实现同源（<c>OmniscientFormPower</c> / <c>PyroJudgmentPower</c>）。
    /// </para>
    ///
    /// <para>
    /// <b>参与者判定要用 <c>Owner.Creature</c></b> —— <c>CardModel.Owner</c> 是
    /// <c>Player</c>，而 <c>participants</c> 装的是 <c>Creature</c>。
    /// </para>
    /// </summary>
    public override Task AfterSideTurnStart(
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        IReadOnlyList<Creature> participants,
        MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player
            && participants.Contains(Owner.Creature))
        {
            ReturnedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ⚠️ <b>存档/读档后必须重置"本回合已弹回"。</b>
    ///
    /// <para>
    /// 与 <c>OmniscientFormPower</c> 同一个坑（见其类注释）：这类普通字段
    /// 既不在存档里、也不会自动清空；Save&amp;Load 会克隆卡牌实例，
    /// 若不清掉，重进后本回合**再也弹不回来**。
    /// </para>
    ///
    /// <para>
    /// 反过来清空是<b>安全</b>的：最坏情况只是本回合多弹一次，
    /// 而"漏弹"是硬失效（牌卡在弃牌堆里直到回合结束）。
    /// </para>
    /// </summary>
    protected override void AfterCloned()
    {
        base.AfterCloned();
        ReturnedThisTurn = false;
        _returning = false;
    }

    protected override void OnUpgrade()
    {
        // 火焰段 2 → 3；雷电段 2 → 3
        DynamicVars.Damage.UpgradeValueBy(1m);
        LightningVar.UpgradeValueBy(1m);
    }
}
