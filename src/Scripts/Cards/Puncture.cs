using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 击穿 - 1 费 - 攻击 - 罕见
/// 去除敌人身上的所有格挡值。造成 7(9) 点雷电伤害，给予 1(2) 层易伤。
///
/// <para>
/// <b>「去除所有格挡」照抄原版「暴露」<c>Expose</c>：</b>
/// <code>
/// await CreatureCmd.LoseBlock(choiceContext, cardPlay.Target,
///                             cardPlay.Target.Block, base.Owner.Creature);
/// </code>
/// 要点是<b>把当前格挡值作为数量传进去</b>（读 <c>Target.Block</c> 自己），
/// 而不是调什么"清空格挡"的专用 API —— 引擎里没有那种 API，
/// <c>LoseBlock</c> 是标准入口，它会走正常的格挡减少流程与相关钩子。
/// </para>
///
/// <para>
/// <b>结算顺序：先去格挡 → 再造成伤害 → 最后上易伤。</b>
/// <list type="number">
///   <item><b>去格挡必须在伤害之前</b>，否则这次伤害会被格挡吃掉，
///         "击穿"的语义就不成立了。</item>
///   <item><b>易伤放在伤害之后</b>：若先上易伤，本次伤害会吃到自己刚施加的
///         ×1.5，与设计不符 —— 与「炽焰术」「闪爆」"先伤害后上负面"同一原则。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>去格挡对自己人无效也无害</b>：本牌是 <c>TargetType.AnyEnemy</c>，
/// 只能点敌人，所以不存在"误清队友格挡"的问题。
/// </para>
///
/// <para>
/// <b>升级：伤害 7 → 9、易伤 1 → 2</b>（设计文档里两个数字都有括号）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Puncture : WizardSpellCard
{
    private const int Cost = 1;

    private const string VulnerableKey = nameof(VulnerablePower);

    public override SpellSchool School => SpellSchool.Lightning;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<VulnerablePower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<VulnerablePower>(),
    ];

    public Puncture()
        : base(Cost, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        var target = cardPlay.Target;

        // ① 先去除目标当前的全部格挡（照抄「暴露」Expose）。
        //    数量直接读 Target.Block —— 引擎没有"清空格挡"的专用 API。
        await CreatureCmd.LoseBlock(choiceContext, target, target.Block, Owner.Creature);

        // ② 再造成雷电伤害（此时已无格挡可挡）。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        // ③ 最后上易伤（放在伤害之后，避免本次伤害吃到自己刚施加的易伤）。
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            target,
            DynamicVars[VulnerableKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 7 → 9；易伤 1 → 2
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars[VulnerableKey].UpgradeValueBy(1m);
    }
}
