using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
/// 白夜 / WhiteNight - 1 费 - 罕见 - 攻击
/// 造成 11(13) 点寒冰伤害。如果敌人有燃烧，则再造成一次伤害，并给予 2(3) 层寒冷。
///
/// <para>
/// 两段伤害是**两次独立的 DamageCmd**（不是 <c>WithHitCount(2)</c>）：
/// 设计上是"先打一下，若有燃烧再打一下"—— 第二段是否发生取决于目标打完第一段后
/// 是否仍存活、以及**出牌瞬间**的燃烧判定（先算条件，再决定段数，
/// 避免第一段把目标打死后再往尸体上打）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class WhiteNight : WizardSpellCard
{
    private const int Cost = 1;

    private const string ChillKey = nameof(ChillPower);

    /// <summary>寒冰法术的命中演出（与「冰矢」IceShard 同款）。</summary>
    private const string HitFx = "vfx/vfx_starry_impact";

    /// <summary>
    /// 金边亮起条件（与「冰矢」「引燃」同一套写法）：
    /// <b>只要有敌人身上有燃烧</b>，手里的这张牌就亮 —— 提示"现在打出去能触发额外一段 + 寒冷"。
    /// </summary>
    protected override bool ShouldGlowGoldInternal
        => CombatState?.HittableEnemies.Any(e => e.GetPowerAmount<BurnPower>() > 0) ?? false;

    public override SpellSchool School => SpellSchool.Ice;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(11m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<ChillPower>(2m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    public WhiteNight()
        : base(Cost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // 先读条件：出牌瞬间目标身上有没有燃烧。
        bool burning = cardPlay.Target.GetPowerAmount<BurnPower>() > 0;

        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // ① 第一段寒冰伤害（必定发生）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(HitFx)
            .Execute(choiceContext);

        if (!burning || !cardPlay.Target.IsAlive)
        {
            return;
        }

        // ② 有燃烧 ⇒ 再打一段。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(HitFx)
            .Execute(choiceContext);

        // ③ 并给予寒冷（目标已死就不必再挂）。
        if (cardPlay.Target.IsAlive)
        {
            await PowerCmd.Apply<ChillPower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars[ChillKey].BaseValue,
                Owner.Creature,
                this);
        }
    }

    /// <summary>升级：伤害 11 → 13、寒冷 2 → 3。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars[ChillKey].UpgradeValueBy(1m);
    }
}
