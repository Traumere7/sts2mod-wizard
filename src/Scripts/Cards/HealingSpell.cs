using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Cards.Vars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 治疗术 - 1 费 - 技能 - 罕见
/// 回复 5(7) 点生命。额外回复等同于你的[法术增益]的生命。消耗。
///
/// <para>
/// 基础结构照抄原版「时候未到」<c>NotYet</c>：
/// <c>HealVar</c> + <c>CreatureCmd.Heal</c> + <c>Exhaust</c>。
/// 差别只在于额外加上了法术增益层数。
/// </para>
///
/// <para>
/// <b>法术增益直接读 <c>GetPowerAmount&lt;SpellPowerPower&gt;()</c></b>，
/// 而不是放进 <c>DynamicVar</c> —— 因为卡面文案里"额外回复等量于法术增益的生命"
/// 不需要显示具体数字（层数会随战斗变化，显示出来反而要处理预览刷新）。
/// 如果以后想让卡面显示总额，就该换成 <c>CalculatedVar</c> 那套
/// （见 <see cref="ManaDrain"/> 的写法）。
/// </para>
///
/// <para>
/// <b>治疗上限由引擎处理</b>：<c>CreatureCmd.Heal</c> 自己会夹到最大生命，
/// 不需要我们判断。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class HealingSpell : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>
    /// ⚠️ <b>不能被战斗内生成</b>（<c>CardModel.cs:643</c> 的虚属性，默认 <c>true</c>）。
    ///
    /// <para>
    /// <b>为什么</b>：本牌是**回复生命**，属于原版明确排除的"产生局外收益"类。
    /// 原版同类的 <c>NotYet</c>（时候未到，回复 10 点生命）就设了
    /// <c>CanBeGeneratedInCombat => false</c>，本牌照此对齐。
    /// </para>
    ///
    /// <para>
    /// 原版这一类的完整名单（均 <c>CanBeGeneratedInCombat => false</c>）：
    /// 回复生命 <c>NotYet</c>、获得生命上限 <c>Feed</c>、
    /// 获得药水 <c>Alchemize</c>、获得金币 <c>HandOfGreed</c> / <c>Royalties</c>、
    /// 获得卡牌奖励 <c>TheHunt</c>。
    /// </para>
    ///
    /// <para>
    /// 该标记只影响 <c>CardFactory.FilterForCombat</c>（<c>CardFactory.cs:159-162</c>）
    /// 与"战斗内变形"（<c>:203</c>）—— <b>不影响</b>普通卡牌奖励、商店、牌库。
    /// </para>
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 基础治疗量：**用 CalculationBaseVar**，它就是 Heal 算式的基数。
        // 升级改它（5 → 7），最终值随之整体上移。
        new CalculationBaseVar(5m),
        new CalculationExtraVar(1m),

        // 卡面显示值（变量名 "Heal"）：
        //   手里就直接显示"基础 + 法术增益"，有加成时数字变绿。
        //   结构同原版「重锤」BodySlam（倍率读自己、忽略 target）。
        // 完整说明见 HealingSpellHealVar 的类注释。
        new HealingSpellHealVar(),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public HealingSpell()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // 结算直接算实际值，不读卡面展示变量 ——
        // 展示变量只在卡面刷新时更新，与"真正治多少"应当各自独立可验证。
        int bonus = Owner.Creature.GetPowerAmount<SpellPowerPower>();
        decimal total = DynamicVars.CalculationBase.BaseValue + bonus;

        await CreatureCmd.Heal(Owner.Creature, total);
    }

    protected override void OnUpgrade()
    {
        // 5 → 7（最终值整体上移，法术增益仍照常叠加）
        DynamicVars.CalculationBase.UpgradeValueBy(2m);
    }
}
