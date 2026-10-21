using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 冰之城墙（能力）：本回合内，敌人每攻击一次，就获得 1 层冻结和 2 层寒冷。
///
/// 触发点用 <c>AfterDamageReceived</c>：当自己被打中时反击上 debuff。
/// 层数用能力的 Amount 承载（1 层 = 冻结 1 层 / 寒冷 2 层），这样升级可以调。
/// 回合结束时移除（<c>PowerStackType.Single</c> + 在玩家回合结束时手动移除）。
/// </summary>
public class FrostWallPower : CustomPowerModel
{
    /// <summary>每层给予的冻结层数。</summary>
    public const decimal FreezePerStack = 1m;

    /// <summary>每层给予的寒冷层数。</summary>
    public const decimal ChillPerStack = 2m;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 计数型：<c>Amount</c> = 打了几张「冰之城墙」，效果按层数<b>缩放</b>。
    ///
    /// <para>
    /// ⚠️ 早期这里是 <see cref="PowerStackType.Single"/> 且硬编码 <c>1m</c>/<c>2m</c>，
    /// 于是"开两张仍只有一张的效果"（实测反馈）。机制本身就该随张数增长，
    /// 所以必须是 <c>Counter</c> + 用 <c>Amount</c> 缩放。
    /// </para>
    ///
    /// <para>
    /// 移除时机（敌人回合结束）不受影响：<c>Remove</c> 会连层数一起清掉。
    /// </para>
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/frost_wall_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/frost_wall_power_big.png";

    /// <summary>悬停文案展示用的"本回合实际给多少"变量名。</summary>
    private const string FreezeAmountKey = "FreezeAmount";
    private const string ChillAmountKey = "ChillAmount";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 悬停文案里的两个数字。它们 = 每层值 × 层数，
        // 随层数变化，所以必须在层数变化时同步（见 SyncDisplay）。
        new DynamicVar(FreezeAmountKey, FreezePerStack),
        new DynamicVar(ChillAmountKey, ChillPerStack),
    ];

    /// <summary>本回合实际给予的冻结层数（= 每层值 × 层数）。</summary>
    private decimal CurrentFreeze => FreezePerStack * Math.Max(Amount, 1);

    /// <summary>本回合实际给予的寒冷层数（= 每层值 × 层数）。</summary>
    private decimal CurrentChill => ChillPerStack * Math.Max(Amount, 1);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncDisplay();
        return Task.CompletedTask;
    }

    /// <summary>层数变化时刷新悬停文案里的数字（再打一张要立刻体现翻倍）。</summary>
    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            SyncDisplay();
        }

        return Task.CompletedTask;
    }

    private void SyncDisplay()
    {
        if (DynamicVars.TryGetValue(FreezeAmountKey, out DynamicVar? f))
        {
            f.BaseValue = CurrentFreeze;
        }

        if (DynamicVars.TryGetValue(ChillAmountKey, out DynamicVar? c))
        {
            c.BaseValue = CurrentChill;
        }
    }

    /// <summary>被打中时给攻击者上冻结与寒冷（按层数缩放）。</summary>
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || dealer == null || !dealer.IsAlive)
        {
            return;
        }

        if (!props.IsPoweredAttack() || result.TotalDamage <= 0)
        {
            return;
        }

        int stacks = Math.Max(Amount, 1);

        await PowerCmd.Apply<FreezePower>(
            choiceContext, dealer, FreezePerStack * stacks, Owner, null);

        // 寒冷走 SpellCmd.ApplyChill 统一收口（"每当施加寒冷时"的效果可响应）。
        await SpellCmd.ApplyChill(
            choiceContext, dealer, ChillPerStack * stacks, Owner, null);
    }

    /// <summary>
    /// 敌人回合结束时移除，实现"只在本回合（含敌人这一回合）生效"。
    ///
    /// ⚠️ **必须在敌人回合结束时移除，不能放在玩家回合结束。**
    /// 时序（<c>CombatManager.cs</c>）：
    /// <code>
    /// 玩家回合结束 → EndPlayerTurnPhaseTwoInternal:1771 → AfterSideTurnEnd(side = Player)
    /// 敌人回合开始 → 敌人攻击 → AfterDamageReceived      ← 我们就是在这里挂 debuff
    /// 敌人回合结束 → EndEnemyTurnInternal:1696          → AfterSideTurnEnd(side = Enemy)
    /// </code>
    /// 若在玩家回合结束时移除，能力会在敌人出手**之前**就消失，
    /// <see cref="AfterDamageReceived"/> 永远不会触发 —— 这正是之前"能力图标在但打不出 debuff"的原因。
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.Remove(this);
        }
    }
}
