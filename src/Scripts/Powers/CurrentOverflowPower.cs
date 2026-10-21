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
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 电流外溢：附着在**敌人**身上。
/// 该敌人每次受到**雷电伤害**时，随机另一名敌人受到**该次伤害最终结算值** 50% 的伤害。
/// 每层独立结算一次（各自随机选目标）。
///
/// <para>
/// <b>候选池（永远只含敌人，绝不含玩家侧）</b>：
/// <list type="bullet">
///   <item><b>无「环流」</b>：候选 = <b>除宿主 X 以外的其他存活敌人</b>。
///         单体战斗时候选为空 ⇒ 本效果无事发生（设计如此）。</item>
///   <item><b>有「环流」</b>：候选 = <b>所有存活敌人（含 X 自己）</b>。
///         于是可能弹给 X、也可能弹给别的敌人；只有 1 名敌人时必定是 X。</item>
/// </list>
/// 换句话说「环流」的唯一作用就是<b>把宿主自己放回候选池</b>，
/// 而<b>不是</b>把玩家拉进来。
/// </para>
///
/// <para>
/// 触发点用 <c>AfterDamageReceived</c>（而非 <c>AfterDamageGiven</c>）：
/// 因为状态挂在"被打的那个敌人"身上，要在它自己受伤时响应。
/// 范本参考 <c>EnvenomPower</c>（涂毒）与 <c>FlameBarrierPower</c>（火焰屏障）。
/// </para>
///
/// <para>
/// <b>伤害基数取 <c>DamageResult.TotalDamage</c></b>（= 被格挡 + 实际掉血），
/// 即"这一下最终打出去多少"。所以易伤、法术增益、寒冷、超导等
/// 一切增伤都会如实传导 —— 见 <c>AfterDamageReceived</c> 里的注释。
/// </para>
///
/// <para>
/// 注意：<c>AfterDamageReceived</c> 在"这一击击杀目标"时不会触发
/// （见 <c>CreatureCmd.cs:413-420</c>），因此被雷电一击打死的敌人不会溅射。
/// </para>
/// </summary>
public class CurrentOverflowPower : CustomPowerModel
{
    private const string SplashRatioKey = "SplashRatio";

    /// <summary>防重入：溅射过程中不再响应任何伤害事件。</summary>
    private bool _resolving;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/current_overflow_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/current_overflow_power_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(nameof(CurrentOverflowPower), 0m),
        new DynamicVar(SplashRatioKey, 0.5m),
    ];

    /// <summary>自己受到雷电伤害时，把伤害溅射给随机另一名敌人。</summary>
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (_resolving || target != Owner || Owner.IsDead || Amount <= 0)
        {
            return;
        }

        // 只响应雷电法术伤害。
        if (SpellProps.Resolve(props, cardSource) != SpellSchool.Lightning)
        {
            return;
        }

        ICombatState? combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 取**本次伤害的最终结算值**（易伤/法术增益/寒冷/超导等全部算完之后的值）。
        //
        // ⚠️ 用 result.TotalDamage 而不是卡面的 DynamicVars.Damage.BaseValue。
        //    DamageResult 明确写着 TotalDamage = BlockedDamage + UnblockedDamage，
        //    即"这一下实际打出去的伤害"（含被格挡吃掉的部分），
        //    与"原始数值×0.5"的旧口径不同 —— 旧口径会漏掉所有增伤/减伤，
        //    表现为"超导/寒冷/法术增益堆起来后，外溢伤害还是很小"。
        //
        //    也不要改用 UnblockedDamage：那是扣掉格挡之后的，
        //    敌人有格挡时外溢会莫名缩水（Amount 的语义是伤害量，不是掉血量）。
        decimal splash = Math.Floor(result.TotalDamage * DynamicVars[SplashRatioKey].BaseValue);
        if (splash < 1m)
        {
            return;
        }

        _resolving = true;
        try
        {
            // 「环流」：拥有它之后，**候选池扩大**——
            // 「环流」：把**宿主自己放回候选池**。
            //
            // ⚠️ 判定必须走 combatState.PlayerCreatures，**不能用 Owner.Player**。
            // Owner 是**敌人**，而 Creature.Player 只在
            // `Creature(Player player, ...)` 那个构造里被赋值
            // （见 Creature.cs:364-370），敌人走的是 Monster 构造，
            // 它的 .Player **恒为 null** —— 用它判断会永远为 false，
            // 表现为"环流完全没效果"。
            bool circulation = combatState.PlayerCreatures
                .Any(c => c.HasPower<CirculationPower>());

            // 每层独立结算一次：各自随机选一名"溅射目标"。
            for (int i = 0; i < Amount; i++)
            {
                // ⚠️⚠️ **候选池永远只含敌人，绝不包含玩家侧。**
                //
                // 用 GetTeammatesOf（同阵营）而不是 GetOpponentsOf：
                // GetOpponentsOf(Owner) 返回的是"拥有者的对立面"，
                // 对敌人而言就是**玩家侧** —— 那恰恰是必须排除的。
                //
                // 「环流」的唯一作用是**把宿主自己放回池子**：
                //   case1 无环流：候选 = 其他敌人（排除 X）→ 单怪时为空 → 效果无效
                //   case2 有环流：候选 = 全部敌人（含 X）→ 单怪时必为 X
                List<Creature> candidates = combatState
                    .GetTeammatesOf(Owner)
                    .Where(c => c.IsAlive && (circulation || c != Owner))
                    .ToList();

                if (candidates.Count == 0)
                {
                    // 单体战斗且没有「环流」：没有"另一名敌人"，本效果无事发生（设计如此）。
                    break;
                }

                // ⚠️⚠️ 随机数必须走 **ICombatState.RunState**，不能走 `Owner.Player?.RunState`。
                //
                // Owner 是**敌人**，而 `Creature.Player` 对敌人**恒为 null**
                // （只有 `Creature(Player, ...)` 那个构造会赋值，敌人走 Monster 构造）
                // —— 于是 `Owner.Player?.RunState.Rng...` 整个表达式求值为 null，
                // 配合 `?? 0` 就**永远取候选池的第 0 个**。
                // 实测表现：溅射目标永远是可选池里最左边的那名敌人，"随机"形同虚设。
                //
                // 注意 `Creature` **没有** `RunState` 属性（卡牌侧写的 `Owner.RunState`
                // 是 `CardModel.Owner`，那是 Player），所以这里从 ICombatState 取。
                //
                // 每层各自调一次 NextInt ⇒ 每层的目标都是**独立重新随机**的。
                int index = combatState.RunState.Rng.CombatTargets.NextInt(candidates.Count);
                Creature victim = candidates[index];

                // 溅射刻意不带学派、不带 Move、dealer/cardSource 均为 null：
                // 既不吃任何加成，也不会再次触发电流外溢，从根本上杜绝递归。
                await CreatureCmd.Damage(
                    choiceContext,
                    victim,
                    splash,
                    ValueProp.Unpowered,
                    null,
                    null);
            }
        }
        finally
        {
            _resolving = false;
        }
    }
}
