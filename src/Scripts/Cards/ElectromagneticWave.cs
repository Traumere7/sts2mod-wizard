using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 电磁波 - 1 费 - 攻击 - 普通
/// 对所有敌人造成 7(8) 点雷电伤害。抽 1(2) 张牌。
///
/// <para>
/// <b>群体伤害用 <c>TargetingAllOpponents(combatState)</c></b>，
/// 照抄本 mod 的「降雷」<see cref="LightningFall"/> 与「落日」<see cref="Sunset"/>。
/// 本牌只有 1 段，不涉及 <c>WithHitCount</c>（那是「降雷」按负面效果种类加段用的）。
/// </para>
///
/// <para>
/// <b>结算顺序：先伤害、再抽牌。</b>
/// 先抽牌没有意义反而有害 —— 抽到的牌里可能有"回合结束前必须打"的牌，
/// 而伤害是这张牌的主体效果，应当先落地
/// （对照「真理派对」<see cref="TruthParty"/>：先施加燃烧再按层数抽牌，
/// 抽牌量依赖前面的结果，所以只能后抽）。
/// </para>
///
/// <para>
/// <b>⚠️ 没有可行目标时，整张牌不生效（连抽牌也没有）。</b>
/// 这是**照抄原版「扫荡射线」<c>SweepingGaze</c> 的实践**：
/// <code>
/// if (!Osty.CheckMissingWithAnim(base.Owner))
/// {
///     await DamageCmd.Attack(...).TargetingRandomOpponents(...).Execute(...);
/// }
/// // ← 抽牌之类的前置不存在；整个效果体都在 if 里面
/// </code>
/// 原版把「无法结算」定义为整张牌的无效，而不是"伤害跳过、其它照常"。
/// 本牌遵循同一口径：<b>抽牌是攻击牌的附带收益，攻击没打出去就不给</b>。
/// </para>
///
/// <para>
/// <b>为什么用 <c>GetOpponentsOf(...).Any(c =&gt; c.IsAlive)</c> 作为判据：</b>
/// 这正是引擎自己判断"有没有可行目标"的口径 ——
/// <c>TargetingAllOpponents</c> → <c>GetPossibleTargets()</c> → <c>GetOpponentsOf(Attacker)</c>
/// （<c>AttackCommand.cs:163-180</c>），随后在 <c>:557-563</c> 过滤 <c>IsAlive</c>，
/// 数量为 0 且战斗仍在进行就 <c>break</c>（整段攻击直接不发生）。
/// 所以这里的判据与引擎**逐字同源**，不会出现"我判它有、它判没有"的错位。
/// </para>
///
/// <para>
/// <b>抽牌用 <c>CardPileCmd.Draw</c></b>（照抄「占卜」<see cref="Divination"/>、
/// 「思索」<see cref="Ponder"/>）。
/// </para>
///
/// <para>
/// <b>升级只涨伤害与抽牌数</b>：7 → 8 伤害、1 → 2 张牌（按设计文档）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ElectromagneticWave : WizardSpellCard
{
    private const int Cost = 1;

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move | SpellProps.For(School)),
        new CardsVar(1),
    ];

    public ElectromagneticWave()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        // 没有可行目标 ⇒ 整张牌不生效（抽牌也不给）。
        // 口径与引擎一致：对手活着的数量为 0 时，TargetingAllOpponents 那一击根本不会发生。
        // 见类注释里「扫荡射线」的对照说明。
        bool hasTarget = combatState
            .GetOpponentsOf(Owner.Creature)
            .Any(c => c.IsAlive);

        if (!hasTarget)
        {
            return;
        }

        // ① 先造成群体雷电伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        // ② 再抽牌。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        // 7 → 8 伤害；抽 1 → 2 张牌
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
