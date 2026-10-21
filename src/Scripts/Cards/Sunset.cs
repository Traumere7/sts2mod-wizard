using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
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
/// 落日 - 1 费 - 攻击 - 先古
/// 虚无。对全体敌人造成 6 点火焰伤害 3(4) 次。
/// 每段额外造成等量于场上最高燃烧层数的伤害。给予全体敌人 5 层燃烧。
///
/// <para>
/// <b>触发来源</b>：欧洛巴斯（OROBAS）选项三「古老牙齿」<c>ArchaicTooth</c>
/// 把初始卡组的「火球术」替换成本牌。
/// </para>
///
/// <para>
/// <b>伤害用动态变量而不是自己算十进制</b>，照抄原版「欺凌」<c>Bully</c> /
/// 「撕碎」<c>Rend</c> 的写法：
/// <list type="bullet">
///   <item><c>CalculationBaseVar</c> = 6（每段基础伤害）</item>
///   <item><c>ExtraDamageVar</c> = 1（每 1 层燃烧多加几点）</item>
///   <item><c>CalculatedDamageVar</c> 倍率 = <see cref="FieldState.HighestBurnForPreview"/></item>
/// </list>
/// 最终每段伤害 = <c>CalculationBase + ExtraDamage × 最高燃烧</c> = <c>5 + 最高燃烧</c>。
/// <b>卡面那个数字就是它算出来的实时值</b>，不再是写死的 5。
/// </para>
///
/// <para>
/// ⚠️ <c>DamageCmd.Attack(CalculatedDamageVar)</c> 这个重载<b>不设置</b>
/// <c>AttackCommand.DamageProps</c>（它默认 <c>ValueProp.Move</c>），
/// 而本 mod 的火焰流派标记位 <c>SpellProps.Fire</c> 恰恰要靠执行期的 props 传递
/// （<c>ChillPower</c> / <c>SuperconductPower</c> / <c>SpellPowerPower</c>
/// 都读它）。所以这里<b>改用 <c>Attack(decimal)</c> 重载 + <c>WithValueProp</c></b>，
/// 自己把 <c>CalculatedDamage.Calculate(null)</c> 算出来传进去 ——
/// 效果与重载版一致，但流派标记不会丢。
/// </para>
///
/// <para>
/// <b>"场上最高燃烧"含玩家自己</b>（见 <see cref="FieldState"/>）——
/// 「高温消毒」会给玩家上燃烧，那也属于"场上"。
/// </para>
///
/// <para>
/// <b>结算顺序：先造成伤害、再上燃烧。</b>
/// 若先上燃烧，本次伤害就会吃到自己刚施加的 5 层加成，强度与设计不符。
/// </para>
///
/// <para>
/// 段数用 <c>WithHitCount</c> 一次结算（每段同值），而不是循环调用 ——
/// 前者走引擎的多段攻击通道，能让"每段各算一次"的钩子（电流外溢、火焰屏障）正确触发。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Sunset : WizardSpellCard
{
    private const int Cost = 1;

    private const string BurnKey = nameof(BurnPower);

    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>虚无 + 打出生效 3(4) 段。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 卡面展示用（也是 CalculationBase 的来源），并携带火焰流派标记。
        new DamageVar(6m, ValueProp.Move | SpellProps.For(School)),
        new CalculationBaseVar(6m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move | SpellProps.For(School))
            .WithMultiplier(FieldState.HighestBurnForPreview),
        new RepeatVar(3),
        new PowerVar<BurnPower>(5m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
    ];

    public Sunset()
        : base(Cost, CardType.Attack, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        List<Creature> enemies = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .ToList();

        if (enemies.Count == 0)
        {
            return;
        }

        // 每段伤害 = CalculationBase + ExtraDamage × 场上最高燃烧。
        // 取的是**施放瞬间**的层数，且该值同时加到每一段上（不是逐段递增）。
        //
        // ⚠️ 这里**不能**用 DynamicVars.CalculatedDamage.Calculate(null)：
        // 那个倍率是 HighestBurnForPreview，它靠 target != null 来区分
        // "手里预览"与"拖出来预览"，而 Self 卡打出时 target 也是 null，
        // 会算出 0 → 伤害退化成基础值。结算直接读场上的真实层数。
        decimal highestBurn = FieldState.HighestBurn(this, null);
        decimal damagePerHit = DynamicVars.CalculationBase.BaseValue
            + DynamicVars.ExtraDamage.BaseValue * highestBurn;
        int hits = DynamicVars.Repeat.IntValue;

        // ① 先造成伤害（此时燃烧还没被本牌加上）。
        await DamageCmd.Attack(damagePerHit)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitCount(hits)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        // ② 再给全体上燃烧。
        await SpellCmd.ApplyBurnToAll(
            choiceContext,
            enemies,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 升级多打一段（3 → 4 段），<b>每段伤害不变</b>（按设计文档）。
    /// </summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
