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
using BaseLib.Extensions;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 魔力爆发 - 2(1) 费 - 攻击 - 稀有
/// 对所有敌人造成等同于你的[法术增益]层数的以太伤害。将你的[法术增益]层数翻倍。
///
/// <para>
/// <b>伤害值直接读「法术增益」的层数</b>（<see cref="SpellPowerPower"/>），
/// 卡面用 <c>CalculatedDamageVar</c> 实时显示（照抄「终结技」<c>Finisher</c> /
/// 「降雷」<see cref="LightningFall"/> 的写法）：
/// <code>
/// new CalculationBaseVar(0m),
/// new ExtraDamageVar(1m),           // ← CalculatedDamageVar 的硬前置（不是 CalculationExtraVar！）
/// new CalculatedDamageVar(props).WithMultiplier((card, _) =&gt; 法术增益层数)
/// </code>
/// </para>
///
/// <para>
/// ⚠️ <b>结算顺序：先伤害、再翻倍。</b>
/// 若先翻倍，本次伤害就会按翻倍后的层数算 —— 强度与设计文案的语序不符。
/// </para>
///
/// <para>
/// ⚠️ <b>翻倍用"再施加一份等量层数"实现</b>：
/// <c>PowerCmd.Apply&lt;SpellPowerPower&gt;(…, stacks, …)</c> 会走
/// <c>ModifyAmount</c> 在现有层数上累加 ⇒ stacks → 2×stacks。
/// 不要用 <c>SetAmount</c> 直接写 2×，那会绕过「给予法术增益」的钩子。
/// </para>
///
/// <para>
/// <b>升级</b>：费用 2 → 1（设计文档的「2（1）」）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ManaBurst : WizardSpellCard
{
    private const int Cost = 2;

    public override SpellSchool School => SpellSchool.Aether;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // ⚠️ CalculatedDamageVar 的硬前置是 **CalculationBaseVar + ExtraDamageVar**
        //    （不是 CalculationExtraVar —— 那个是给 CalculatedVar 用的）。
        //    写错会在卡牌实例化时抛 KeyNotFoundException 'ExtraDamage'，
        //    表现为"连控制台加牌都加不出来"。
        new CalculationBaseVar(0m),
        new ExtraDamageVar(1m),

        new CalculatedDamageVar(ValueProp.Move | SpellProps.For(SpellSchool.Aether))
            .WithMultiplier((CardModel card, Creature? _) => SpellPowerStacks(card)),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WizardTips.Aether,
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public ManaBurst()
        : base(Cost, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    /// <summary>当前「法术增益」层数（卡面预览与结算共用同一个口径）。</summary>
    private static decimal SpellPowerStacks(CardModel card)
        => card.Owner?.Creature is { } self ? self.GetPowerAmount<SpellPowerPower>() : 0m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        bool hasEnemy = combatState
            .GetOpponentsOf(Owner.Creature)
            .Any(c => c.IsAlive);

        if (!hasEnemy)
        {
            return;
        }

        // ① 按"当前层数"结算以太伤害（此时还没翻倍）。
        decimal stacks = SpellPowerStacks(this);

        // 命中演出：**白+金版**星星冲击（每个目标各一个，加载失败回退原版紫色）。
        foreach (Creature enemy in combatState.HittableEnemies.ToList())
        {
            WizardVfx.StarryImpactGold(enemy);
        }

        await DamageCmd.Attack(stacks)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .Execute(choiceContext);

        // ② 再翻倍：额外施加"当前层数"份 ⇒ 2×。
        if (stacks > 0m)
        {
            await PowerCmd.Apply<SpellPowerPower>(
                choiceContext,
                Owner.Creature,
                stacks,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        // 费用 2 → 1
        EnergyCost.UpgradeBy(-1);
    }
}
