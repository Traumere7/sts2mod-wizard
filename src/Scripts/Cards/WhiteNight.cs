using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 白夜 / WhiteNight - 1 费 - 罕见 - 攻击
/// 造成 11(14) 点寒冰伤害。如果敌人有燃烧，则给予 2(3) 层虚弱和 2(3) 层寒冷。
///
/// <para>
/// 2026-11-26 改版（design.md）：原来"有燃烧再打一段"改为"有燃烧就上虚弱 + 寒冷"。
/// 条件在**出牌瞬间**判定（目标当时身上有燃烧）；
/// 目标被这一发打死则不再挂负面（尸体上挂减益没有意义）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class WhiteNight : WizardSpellCard
{
    private const int Cost = 1;

    private const string WeakKey = nameof(WeakPower);

    private const string ChillKey = nameof(ChillPower);

    /// <summary>寒冰法术的命中演出（与「冰矢」IceShard 同款）。</summary>
    private const string HitFx = "vfx/vfx_starry_impact";

    public override SpellSchool School => SpellSchool.Ice;

    /// <summary>
    /// 金边亮起条件（与「冰矢」「引燃」同一套写法）：
    /// <b>只要有敌人身上有燃烧</b>，手里的这张牌就亮 —— 提示"现在打出去能触发虚弱 + 寒冷"。
    /// </summary>
    protected override bool ShouldGlowGoldInternal
        => CombatState?.HittableEnemies.Any(e => e.GetPowerAmount<BurnPower>() > 0) ?? false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(11m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<WeakPower>(2m),
        new PowerVar<ChillPower>(2m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromPower<WeakPower>(),
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

        // ① 寒冰伤害（必定发生）。
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

        // ② 有燃烧 ⇒ 虚弱 + 寒冷（两层数各自随升级 +1）。
        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[WeakKey].BaseValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<ChillPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[ChillKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级：伤害 11 → 14（+3）、虚弱 2 → 3、寒冷 2 → 3。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars[WeakKey].UpgradeValueBy(1m);
        DynamicVars[ChillKey].UpgradeValueBy(1m);
    }
}
