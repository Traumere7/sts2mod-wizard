using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 冰霰 - 1 费 - 攻击 - 普通
/// 随机对敌人造成 3 点寒冰伤害 3(4) 次，每次命中给予 1 层寒冷。消耗。
///
/// <para>
/// <b>随机多段照抄原版「飞剑回旋镖」<c>SwordBoomerang</c>：</b>
/// <c>TargetType.RandomEnemy</c> + <c>TargetingRandomOpponents(combatState)</c>
/// + <c>WithHitCount(Repeat)</c> —— 引擎会**为每一段单独随机**一个目标
/// （<c>AttackCommand.cs:618</c>：<c>Rng.CombatTargets.NextItem(validTargets)</c>）。
/// </para>
///
/// <para>
/// <b>⚠️ 为什么不能只用 <c>TargetingRandomOpponents</c>：</b>
/// 那只能造成伤害，无法"每次命中各给 1 层寒冷" ——
/// 多段攻击没有"每段回调"可供注入。所以这里**自己循环**，
/// 每段依次做「随机选一个敌人 → 造成伤害 → 给它 1 层寒冷」。
/// </para>
///
/// <para>
/// <b>随机数用 <c>Owner.RunState.Rng.CombatTargets</c></b>，
/// 与引擎内部选随机目标用的是**同一个 Rng 流**
/// （<c>AttackCommand.cs:617</c>），也与本 mod 的「电流外溢」一致 ——
/// 这样随机结果可复现、且不会污染其他随机。
/// </para>
///
/// <para>
/// <b>每段独立随机</b>：允许重复命中同一个敌人（引擎的
/// <c>_doesRandomTargetingAllowDuplicates</c> 默认也是 true）。所以 3 段
/// 可能全打在同一个敌人身上、也可能分散，这正是设计要的"随机"。
/// </para>
///
/// <para>
/// <b>寒冷走 <c>PowerCmd.Apply</c></b>（与「禁书目录」一致）——
/// 本 mod 只有燃烧有专门的 <c>SpellCmd.ApplyBurn</c>
/// （因为燃烧需要"取高值"语义 + 自己的事件）。
/// </para>
///
/// <para>
/// <b>升级只加段数（3 → 4）</b>，每段伤害与寒冷层数都不变 ——
/// 与参考卡「飞剑回旋镖」的升级方式一致。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class IceShard : WizardSpellCard
{
    private const int Cost = 1;

    private const string ChillKey = nameof(ChillPower);

    public override SpellSchool School => SpellSchool.Ice;

    /// <summary>「消耗」是初始就有的词条，直接声明即可（不涉及升级）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move | SpellProps.For(School)),
        new RepeatVar(3),
        new PowerVar<ChillPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ChillPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public IceShard()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.RandomEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        List<Creature> candidates = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        int hits = DynamicVars.Repeat.IntValue;
        decimal damage = DynamicVars.Damage.BaseValue;
        decimal chill = DynamicVars[ChillKey].BaseValue;

        for (int i = 0; i < hits; i++)
        {
            // 每段单独随机；候选可能因上一段击杀而变少，所以每段都重新过滤一次。
            List<Creature> alive = candidates.Where(c => c.IsAlive).ToList();
            if (alive.Count == 0)
            {
                break;
            }

            int index = Owner.RunState.Rng.CombatTargets.NextInt(alive.Count);
            Creature victim = alive[index];

            await DamageCmd.Attack(damage)
                .WithValueProp(ValueProp.Move | SpellProps.For(School))
                .FromCard(this, cardPlay)
                .Targeting(victim)
                .WithHitFx("vfx/vfx_starry_impact")
                .Execute(choiceContext);

            // 每次命中各给 1 层寒冷（与"每段伤害"配对）。
            // 走 SpellCmd.ApplyChill 统一收口 —— 这样"每当施加寒冷时"的效果
            // （如「弹射」DoubleRicochet）才能响应到。
            await SpellCmd.ApplyChill(
                choiceContext,
                victim,
                chill,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        // 3 → 4 段（每段伤害与寒冷层数不变）
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
