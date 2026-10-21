using System.Linq;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 引燃 / Ignite - 1 费 - 攻击 - 罕见
/// 造成 6（8）点火焰伤害。给予 1 层燃烧。
/// 若敌人意图为攻击，则获得 1 点法术增益和 1（2）费。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 关键点：「意图为攻击」的判据 = <c>MonsterModel.IntendsToAttack</c>
/// ══════════════════════════════════════════════════════════════════
/// <b>这是原版自带的属性，语义与设计文案逐字一致，不要自己写判据：</b>
/// <code>
/// // MonsterModel.cs:242-246
/// public bool IntendsToAttack => NextMove.Intents.Any(intent =>
///     intent.IntentType == IntentType.Attack
///  || intent.IntentType == IntentType.DeathBlow);
/// </code>
/// <b>注意它把 <c>DeathBlow</c>（致死一击）也算作"攻击"</b> ——
/// 这正是设计想要的（死亡一击当然是攻击）。
/// </para>
///
/// <para>
/// ⚠️ <b>必须判<b>玩家选定的那个目标</b>，不是"任意敌人"。</b>
/// 本牌是 <c>TargetType.AnyEnemy</c>（单体），所以用
/// <c>cardPlay.Target.Monster.IntendsToAttack</c>。
/// </para>
///
/// <para>
/// <b>照抄原版同款卡牌「刺骨凝视」<c>GoForTheEyes</c> 的写法</b>
/// （它也是"造成伤害 → 若目标意图为攻击则追加效果"）：
/// <code>
/// await DamageCmd.Attack(...).FromCard(this, cardPlay).Targeting(cardPlay.Target)...Execute(choiceContext);
/// if (cardPlay.Target.Monster.IntendsToAttack)
///     await PowerCmd.Apply&lt;WeakPower&gt;(choiceContext, cardPlay.Target, ..., base.Owner.Creature, this);
/// </code>
/// <b>用户指定参考的观者 mod「不惧妖邪」<c>FearNoEvil</c> 是同一套</b>
/// （其文案：<i>"造成伤害。如果目标敌人的意图是攻击，则进入平静。"</i>）。
/// </para>
///
/// <para>
/// ⚠️ <b><c>cardPlay.Target</c> 是 <c>Creature?</c></b>：虽然本牌
/// <c>TargetType.AnyEnemy</c> 理论上必定有目标，但签名可空，
/// 直接解引用会 <c>CS8602</c>；且一旦假设不成立就是运行时崩溃。
/// 所以用 <c>ArgumentNullException.ThrowIfNull</c> 收窄（与 <c>GoForTheEyes</c> 一致）
/// —— <b>这比用 <c>?.</c> 静默跳过更好</b>：单体攻击牌没有目标属于引擎级异常，
/// 应该立刻暴露而不是静默不结算。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 伤害与燃烧都走本 mod 的收口
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item><b>伤害</b>用 <c>ValueProp.Move | SpellProps.For(School)</c> ——
///         后者打上"火系法术"标记，使其吃<b>法术增益</b>而不吃力量
///         （见 <see cref="SpellProps"/> 与 <c>SpellPowerPower</c>）。</item>
///   <item><b>燃烧</b>走 <see cref="SpellCmd.ApplyBurn"/>，不是直接
///         <c>PowerCmd.Apply</c> —— 本 mod 的燃烧有"取高值、不叠加"的特殊语义，
///         且需要触发「纵火狂」「巨焱拳」「全知形态」等订阅了
///         <c>WizardEvents.BurnApplied</c> 的效果。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>结算顺序：伤害 → 燃烧 → 条件奖励。</b>
/// 条件奖励放最后是有意的：它读的是<b>攻击前</b>的意图
/// （意图在本回合内不会因这次攻击而改变），
/// 但把"额外收益"排在伤害之后，读感上更自然（先打，再结算奖励）。
/// </para>
///
/// <para>
/// ⚠️ <b>「获得 1（2）费」用 <c>EnergyVar</c></b>，卡面用
/// <c>{Energy:energyIcons()}</c> 渲染<b>能量图标</b>而不是"费"字
/// （用户明确要求）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Ignite : WizardSpellCard
{
    private const int Cost = 1;

    private const int Damage = 6;

    private const int DamageUpgrade = 2;

    private const int Burn = 1;

    /// <summary>意图为攻击时获得的法术增益（升级不增加）。</summary>
    private const int SpellPowerOnAttack = 1;

    private const int EnergyOnAttack = 1;

    private const int EnergyOnAttackUpgrade = 1;

    private const string BurnKey = nameof(BurnPower);

    private const string SpellPowerKey = nameof(SpellPowerPower);

    private const string EnergyKey = "Energy";

    public override SpellSchool School => SpellSchool.Fire;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromPower<SpellPowerPower>(),
        base.EnergyHoverTip,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<BurnPower>(Burn),
        new PowerVar<SpellPowerPower>(SpellPowerOnAttack),
        new EnergyVar(EnergyOnAttack),
    ];

    public Ignite()
        : base(Cost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 单体攻击牌没有目标属于引擎级异常 —— 直接抛，不要静默跳过。
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // ① 火焰伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        // ② 给予燃烧（走统一收口）。
        await SpellCmd.ApplyBurn(
            choiceContext,
            cardPlay.Target,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);

        // ③ 若目标意图为攻击 ⇒ 额外收益。
        //
        // ⚠️ 判据是**原版属性** MonsterModel.IntendsToAttack
        //    （Attack 或 DeathBlow 都算，见类注释）。
        if (cardPlay.Target.Monster?.IntendsToAttack != true)
        {
            return;
        }

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[SpellPowerKey].BaseValue,
            Owner.Creature,
            this);

        // ⚠️ 不能在 GainEnergy 之后再读能量（它会跑 ModifyEnergyGain 再入账），
        //    这里加的是 DynamicVars 里声明的固定值，所以没这个问题。
        await PlayerCmd.GainEnergy(DynamicVars[EnergyKey].BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(DamageUpgrade);
        DynamicVars[EnergyKey].UpgradeValueBy(EnergyOnAttackUpgrade);
    }
}
