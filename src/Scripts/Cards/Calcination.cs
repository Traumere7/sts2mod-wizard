using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 煅烧 / Calcination - 1 费 - 普通 - 攻击
/// 造成 8(11) 点火焰伤害。升级手牌中所有**临时加入战斗**的牌。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// "局内临时升级"照抄原版「军备」<c>Armaments</c>
/// ══════════════════════════════════════════════════════════════════
/// <c>CardCmd.Upgrade(card)</c> 只升级**传进去的那个实例**（<c>CardModel.UpgradeInternal</c>），
/// 战斗中的手牌是卡组的克隆 ⇒ 升级天然只持续本场战斗，不需要额外写回/回滚逻辑
/// （Armaments 升级手牌也是这个语义）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// "临时加入战斗的牌"怎么判定
/// ══════════════════════════════════════════════════════════════════
/// 查**战斗历史** <c>CardGeneratedEntry</c>，用**引用相等**判断（与「永恒回响」
/// <c>EternalEcho.IsGeneratedThisCombat</c>、「结晶」同一套口径）。
/// 不用 <c>DeckVersion == null</c>：某些生成路径会让牌带着 <c>DeckVersion</c> 进来，
/// 那种牌一旦被误升级，**会通过 DeckVersion 泄漏到卡组**。
/// </para>
///
/// <para>
/// ⚠️ 排除**状态牌**（<c>CardType.Status</c>）：用户 2026-11-28 明确要求。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Calcination : WizardSpellCard
{
    private const int Cost = 1;

    public override SpellSchool School => SpellSchool.Fire;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move | SpellProps.For(School)),
    ];

    public Calcination()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // ① 火焰伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(WizardVfx.MoltenFistFx, WizardVfx.FireHitSfx)
            .Execute(choiceContext);

        // ② 升级手牌里"本场战斗临时加入"的牌（排除状态牌）。
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        List<CardGeneratedEntry> generatedEntries = CombatManager.Instance.History.Entries
            .OfType<CardGeneratedEntry>()
            .ToList();

        List<CardModel> targets = PileType.Hand.GetPile(Owner).Cards
            .Where(c => c.IsUpgradable
                        && c.Type != CardType.Status
                        && generatedEntries.Any(e => ReferenceEquals(e.Card, c)))
            .ToList();

        foreach (CardModel card in targets)
        {
            CardCmd.Upgrade(card);
        }
    }

    /// <summary>升级：伤害 8 → 11（升级范围机制不变）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
