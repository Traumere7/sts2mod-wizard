using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
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
/// 本场战斗中你每消耗 1 张牌，这张牌的耗能就减 1。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 动态费用
/// ══════════════════════════════════════════════════════════════════
/// 不缓存计数器，每次计算费用时**直接查战斗历史**
/// （<c>CombatManager.Instance.History.Entries.OfType&lt;CardExhaustedEntry&gt;()</c>）：
/// <list type="bullet">
///   <item>同名牌、克隆牌、以及**战斗中中途才生成**的副本，费用天然一致；</item>
///   <item>不需要订阅 <c>AfterCardExhausted</c> 去维护状态，也不会有"历史漏记"的漂移。</item>
/// </list>
/// 口径与「结晶」「永恒回响」读历史一致（<c>CardGeneratedEntry</c> / <c>CardExhaustedEntry</c>）。
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
    /// 本场战斗中每消耗 1 张牌，费用 -1（最低 0）。
    /// 用 <c>Late</c> 版本，保证在其它全局减费之后再扣减。
    /// </summary>
    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        if (!ReferenceEquals(card, this) || CombatState == null || originalCost < 0m)
        {
            return false;
        }

        int exhausted = CombatManager.Instance.History.Entries
            .OfType<CardExhaustedEntry>()
            .Count(e => e.Actor == Owner.Creature);

        if (exhausted <= 0)
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - exhausted);
        return true;
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

    /// <summary>升级：每段伤害 5 → 7（+2；段数与动态减费不变）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
