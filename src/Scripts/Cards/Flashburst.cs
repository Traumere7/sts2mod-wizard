using BaseLib.Extensions;
using BaseLib.Utils;
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
/// 闪爆 - 2 费 - 攻击 - 普通
/// 虚无。造成 17(23) 点火焰伤害。给予 3 层燃烧。
///
/// <para>
/// <b>「虚无」<c>Ethereal</c> 是初始就有的词条</b>，直接在
/// <see cref="CanonicalKeywords"/> 里声明即可（照抄本 mod 的「落日」<see cref="Sunset"/>
/// 与「全知形态」<see cref="OmniscientForm"/>）。
/// 回合结束时若还在手里，引擎会自动把它消耗掉
/// （走 <c>CardCmd.Exhaust(…, causedByEthereal: true)</c>，见 <c>CombatManager.cs</c>）。
/// </para>
///
/// <para>
/// <b>升级只涨伤害（17 → 23）</b>，燃烧层数恒为 3 ——
/// 设计文档写的是"虚无。造成 17（23）点火焰伤害。给予 3 层燃烧"，
/// 括号只覆盖伤害，燃烧没有括号。
/// </para>
///
/// <para>
/// <b>结算顺序：先伤害、再上燃烧。</b>
/// 若先上燃烧，本次伤害就会吃到自己刚施加的燃烧加成（「寒冷」不吃燃烧，
/// 但「纵火狂」会给活力、活力会加算到这次攻击上），强度与设计不符 ——
/// 与「火苗」「炽焰术」「落日」同一条原则。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Flashburst : WizardSpellCard
{
    private const int Cost = 2;

    private const string BurnKey = nameof(BurnPower);

    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>「虚无」是初始就有的词条，直接声明即可（不涉及升级）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(17m, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<BurnPower>(3m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
    ];

    public Flashburst()
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
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);

        // 燃烧走 SpellCmd.ApplyBurn（不是直接 PowerCmd.Apply）——
        // 本 mod 的燃烧有"不可叠加、取最高值"的特殊语义，
        // 且需要无条件触发"给予燃烧时"的效果（「纵火狂」「全知形态」），
        // 这些全封在那个收口里，不能绕过。
        await SpellCmd.ApplyBurn(
            choiceContext,
            cardPlay.Target,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 17 → 23（燃烧层数不变）
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
