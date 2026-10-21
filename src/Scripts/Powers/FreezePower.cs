using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 冻结：**使其接下来 N 个回合的意图都与本次抓取到的招式相同**（N = 层数）。
/// 每个敌人回合结束（结算完成后）失去 1 层。
///
/// ══════════════════════════════════════════════════════════════════
/// 时序（依据反编译源码 CombatManager / Creature / MonsterModel 校正）
/// ══════════════════════════════════════════════════════════════════
///
/// <code>
/// 【玩家回合 N】StartTurn
///     ① :721   Hook.BeforeSideTurnStart
///     ② :743   enemy.PrepareForNextTurn() → Monster.RollMove()        ← 掷招
///     ③ :783   Hook.AfterSideTurnStart(side = Player)
///     ……玩家操作……
///     ── SwitchFromPlayerToEnemySide :1715 ──
/// 【敌人回合 N】ExecuteEnemyTurn :1402
///     ④ :1423  每个敌人依次 enemy.TakeTurn() → Monster.PerformMove()
///               ★ 冰之城墙在这里触发（敌人攻击玩家 → AfterDamageReceived → 挂冻结）
///     ⑤ :1084  EndEnemyTurnInternal()
///             └ :1696 Hook.AfterSideTurnEnd(side = Enemy)
///                  ① 登记：NextMove 仍是刚执行的招
///                  ② 掉 1 层（1 层时能力就地移除，登记保留）
///     ── SwitchSides :1089 → 敌人回合真正结束 ──
/// 【玩家回合 N+1】回到 ② → 掷招被 <see cref="Patches.FrozenIntentPatch"/> 拦下，重复刚抓到的招 ✔
/// </code>
///
/// ══════════════════════════════════════════════════════════════════
/// 为什么"登记 + 掉层"都在⑤，且顺序不能反
/// ══════════════════════════════════════════════════════════════════
///
/// <b>登记点必须是⑤（敌人出手之后、敌人回合结束之前）。</b>
/// <c>NextMove</c> 只在②被重新赋值，所以⑤读到的是**敌人刚刚打出的那一招**。
/// 对<b>冰之城墙</b>这类"敌人攻击后才挂上冻结"的效果，这是唯一正确的抓取点 ——
/// 冻结是在敌人回合中挂上的，此时玩家回合末早已过去，抓不到"刚打我的那一招"。
///
/// <b>掉层必须紧跟登记之后（仍在⑤），不能挪到玩家回合末。</b>
/// 层数语义是"还能重复几次"：本次登记已经买到了下一次重复，
/// 所以结算完成后立刻扣掉。这样 N 层恰好买 N 次重复。
///
/// <b>登记必须每回合重做。</b>
/// 每次掷招都会消费掉登记，所以第 2 层要生效就必须在下一个敌人回合末再登记一次。
/// **不要**加"已有登记就跳过"的优化 —— 那会让 N 层退化成 1 层。
///
/// ══════════════════════════════════════════════════════════════════
/// 两条必须记住的约束
/// ══════════════════════════════════════════════════════════════════
///
/// <b>登记一定活得比能力久。</b>
/// 掉层在⑤、而拦截掷招在下一个玩家回合的②。
/// 1 层时能力在⑤就被移除，之后②才去消费登记 ——
/// 所以**绝不能用"能力是否还在"判断登记是否有效**
/// （曾用 <c>HasPower</c> 做"残留登记"防护，结果把每一个合法登记都丢掉了）。
/// 有效性的判据放在 <see cref="FreezeRegistry"/> 的 <c>Armed</c> 标记里。
///
/// <b>不要判断 <c>participants.Contains(Owner)</c> 之外的东西。</b>
/// <c>participants</c> 是"当前回合那一方"的生物；判定回合归属请用 <c>side</c> 参数。
///
/// <b>眩晕（<c>MonsterModel.stunnedMoveId</c>）必须放行</b>，否则会把眩晕覆盖掉。
/// </summary>
public class FreezePower : CustomPowerModel
{
    private const string Tag = "[Wizard/Freeze]";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/freeze_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/freeze_power_big.png";

    private string Who => $"{Owner?.Monster?.Id.Entry ?? "?"}(a={Amount})";

    /// <summary>
    /// 只在<b>敌人回合结束</b>（敌人出手之后、回合真正结束之前）做两件事，顺序不能反：
    /// <list type="number">
    ///   <item><b>登记</b>：此刻 <c>NextMove</c> 仍是敌人刚刚执行的招，写进
    ///         <see cref="FreezeRegistry"/>，交给 <see cref="Patches.FrozenIntentPatch"/>
    ///         在下一次掷招时写回。</item>
    ///   <item><b>掉 1 层</b>：结算完成之后再掉层。1 层时能力就此移除，
    ///         但<b>登记还留着</b>（见 <see cref="FreezeRegistry"/> 的 <c>Armed</c> 注释），
    ///         所以下一个玩家回合的掷招照样会被拦下。</item>
    /// </list>
    ///
    /// <para>
    /// ⚠️ <b>这两步必须成对出现在同一个分支里。</b>
    /// 曾经写成 <c>if (side == Enemy) { Register(); return; }</c>
    /// 外加一个独立的 <c>if (side == Player) TickDown</c> ——
    /// 结果是<b>层数永远不掉</b>，再加上每个敌人回合都重新登记，
    /// 意图就被永久锁死（实测"冻结永不掉层、意图永远保持一种"）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>也不要"只在没有登记时才登记"。</b>
    /// 每回合都重新登记是<b>必需的</b>：每层只买一次重复，
    /// 而每次掷招都会消费掉登记 —— 不重新登记的话，第 2 层就无事可做。
    /// N 层 = N 次重复正是靠"登记 → 消耗 → 再登记"这个循环实现的。
    /// </para>
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy)
        {
            return;
        }

        // ① 先结算：抓取敌人刚刚执行的招，供下一个玩家回合的掷招写回。
        Register();

        // ② 再掉层。必须放在登记之后 —— 层数语义是"还能重复几次"，
        //    本次已经买到了一次重复，所以现在才扣。
        if (participants.Contains(Owner))
        {
            await PowerCmd.TickDownDuration(this);
        }
    }

    /// <summary>
    /// 敌人出手之后、敌人回合结束之前：抓取敌人刚刚执行的招式并登记。
    ///
    /// <para>
    /// 此刻层数一定还在（掉层是紧随其后的下一步），所以这次登记必然是有效的。
    /// </para>
    /// </summary>
    private void Register()
    {
        if (Amount <= 0)
        {
            // 理论上到不了这里（能力已移除，钩子不会执行）。
            // 但万一将来时序变动，必须保证不会留下一条会冻结下一回合的残留登记。
            ClearStale();
            return;
        }

        if (Owner == null || Owner.IsDead)
        {
            return;
        }

        MonsterModel? monster = Owner.Monster;
        if (monster == null || monster.MoveStateMachine == null)
        {
            return;
        }

        MoveState move = monster.NextMove;

        // 眩晕是临时构造、未注册进 States 的 MoveState，必须放行。
        if (move.StateId == MonsterModel.stunnedMoveId)
        {
            return;
        }

        FreezeRegistry.Set(monster, move);
        Log.Info($"{Tag} 登记 {Who}：敌人刚执行的招 = '{move.StateId}'，下次掷招时保留");
    }

    /// <summary>清掉本能力持有者身上的登记。</summary>
    private void ClearStale()
    {
        MonsterModel? monster = Owner?.Monster;
        if (monster != null)
        {
            FreezeRegistry.Clear(monster);
        }
    }
}
