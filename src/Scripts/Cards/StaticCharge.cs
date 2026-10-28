using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 静电 - 1 费 - 攻击 - 普通
/// 造成 5(7) 点雷电伤害 2 次。
///
/// <para>
/// <b>两段用 <c>WithHitCount</c> 一次结算</b>，而不是手动循环两次 <c>DamageCmd</c>：
/// 前者走引擎的多段攻击通道，能让"每段各算一次"的钩子
/// （电流外溢、燃烧之魂的插入伤害）按段正确触发。
/// 段数用 <c>RepeatVar</c> 声明，与原版「飞剑回旋镖」<c>SwordBoomerang</c>
/// 和本 mod 的「火球术」<c>Fireball</c> 一致。
/// </para>
///
/// <para>
/// <b>升级涨的是每段伤害（5 → 7），段数恒为 2</b> ——
/// 设计写的是"造成 5（7）点雷电伤害 2 次"，
/// 括号内是伤害值，不是段数。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class StaticCharge : WizardSpellCard
{
    private const int Cost = 1;

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move | SpellProps.For(School)),
        new RepeatVar(2),
    ];

    public StaticCharge()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 5 → 7（段数不变）
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
