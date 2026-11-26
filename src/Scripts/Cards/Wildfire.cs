using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 流火 / Wildfire - 3 费 - 罕见 - 攻击
/// 对所有敌人造成 5(7) 点火焰伤害 3 次。
/// 每当你消耗 1 张牌，此牌的耗能在**下次打出前**减少 1 费。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 减费口径：照抄原版「火箭飞拳」<c>RocketPunch</c>
/// ══════════════════════════════════════════════════════════════════
/// 原版写法是在钩子里直接给**本牌**的局部费用修正：
/// <code>
/// EnergyCost.AddUntilPlayed(-1);   // LocalCostModifierExpiration.UntilPlayed
/// </code>
/// 「UntilPlayed」会在打出该牌时由引擎清除（<c>AfterCardPlayedCleanup</c>），
/// 所以语义正好是"下次打出前减少 1 费"——打出去之后就恢复原价，需要重新攒。
/// </para>
///
/// <para>
/// ⚠️ 每张副本各自累计：钩子对**所有**在战力牌堆里的牌都会触发，
/// 所以手里/牌堆里的每一张「流火」都独立记自己的减费（与火箭飞拳一致）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Wildfire : WizardSpellCard
{
    private const int Cost = 3;

    private const int Hits = 3;

    public override SpellSchool School => SpellSchool.Fire;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move | SpellProps.For(School)),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        // 文案里写了「消耗」，词条解释要挂上。
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public Wildfire()
        : base(Cost, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    /// <summary>
    /// 每当你消耗 1 张牌 ⇒ 本牌费用 −1，直到它被打出（照抄原版「火箭飞拳」）。
    /// </summary>
    public override Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        // 只认自己的牌被消耗（队友/敌人被消耗的牌不算）。
        if (card.Owner != Owner)
        {
            return Task.CompletedTask;
        }

        EnergyCost.AddUntilPlayed(-1);
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // 群体 3 段：一次 WithHitCount 结算（每段同值），照抄「落日」Sunset。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitCount(Hits)
            .WithHitFx(WizardVfx.MoltenFistFx, WizardVfx.FireHitSfx)
            .Execute(choiceContext);
    }

    /// <summary>升级：每段伤害 5 → 7（+2；段数与减费机制不变）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
