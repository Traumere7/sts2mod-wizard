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
/// 冰矢 / IceArrow - 1 费 - 攻击 - 普通
/// 造成 9（12）点寒冰伤害。如果敌人的意图是攻击，则给予 1（2）层寒冷。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结构照抄「引燃」<see cref="Ignite"/>
/// ══════════════════════════════════════════════════════════════════
/// 两者是同一套骨架，只是"条件达成后的收益"不同
/// （引燃给法术增益 + 费，本牌给寒冷）：
/// <list type="number">
///   <item>单体法术伤害（<c>DamageCmd.Attack(...).WithValueProp(...)</c>）；</item>
///   <item>无条件的那部分效果（引燃是燃烧，本牌没有）；</item>
///   <item><b>意图判定</b>：<c>cardPlay.Target.Monster?.IntendsToAttack != true</c>
///         ⇒ 直接 return。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>意图判据用原版属性 <c>MonsterModel.IntendsToAttack</c></b>，
/// 它把 <c>Attack</c> 与 <c>DeathBlow</c>（大招/处决）都算作"要攻击"，
/// 与玩家的直觉一致。不要自己拿 <c>Intent</c> 的枚举去比 ——
/// 那会漏掉 <c>DeathBlow</c>（引燃当年就是这么定的口径）。
/// 对非怪物（理论上不会出现）取 <c>null != true</c> ⇒ 不触发追加效果。
/// </para>
///
/// <para>
/// <b>寒冰伤害靠 <c>ValueProp</c> 的 <see cref="SpellProps.For"/> 标记</b>识别，
/// 这样「法术增益」「超导」「寒冷」等一切按法术学派结算的效果都能吃到，
/// 与其它寒冰牌口径一致。
/// </para>
///
/// <para>
/// <b>寒冷走统一收口 <see cref="SpellCmd.ApplyChill"/></b>（不是裸
/// <c>PowerCmd.Apply</c>）—— 那里会处理"是否被人工制品抵消"、
/// 以及「超导」「冰甲」等订阅者的计数。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class IceArrow : WizardSpellCard
{
    private const int Cost = 1;

    private const int Damage = 9;

    /// <summary>9 → 12。</summary>
    private const int DamageUpgrade = 3;

    private const int Chill = 1;

    /// <summary>1 → 2。</summary>
    private const int ChillUpgrade = 1;

    private const string ChillKey = nameof(ChillPower);

    public override SpellSchool School => SpellSchool.Ice;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move | SpellProps.For(School)),
        new PowerVar<ChillPower>(Chill),
    ];

    public IceArrow()
        : base(Cost, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 单体攻击牌没有目标属于引擎级异常 —— 直接抛，不要静默跳过（同引燃）。
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // ① 寒冰伤害。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        // ② 若目标意图为攻击 ⇒ 给予寒冷。
        if (cardPlay.Target.Monster?.IntendsToAttack != true)
        {
            return;
        }

        await SpellCmd.ApplyChill(
            choiceContext,
            cardPlay.Target,
            DynamicVars[ChillKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级：伤害 9 → 12，寒冷 1 → 2（两段都升，对应 design 的「9（12）」「1（2）」）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(DamageUpgrade);
        DynamicVars[ChillKey].UpgradeValueBy(ChillUpgrade);
    }
}
