using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;
using BaseLib.Extensions;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 以太箭 - 1 费 - 攻击 - 衍生（Token）
/// 保留。造成 9(12) 点以太伤害。消耗。
///
/// <para>
/// 由「第四元素」<see cref="TheFourthElement"/> 每回合生成，是**以太伤害**的样板牌。
/// </para>
///
/// <para>
/// ⚠️ <b>以太 = 火焰 | 雷电 | 寒冰 三位并集</b>（见 <see cref="SpellProps.Aether"/>）：
/// 这一击会同时吃到「寒冷」「超导」、触发「电流外溢」、并让「三原素」一次点亮三系。
/// 所以这里**必须**用 <c>SpellProps.For(SpellSchool.Aether)</c> 生成标记位，
/// 不能只靠 <c>School</c> 属性（那只用于卡面/接口展示）。
/// </para>
///
/// <para>
/// ⚠️ 牌池必须是 <see cref="TokenCardPool"/>、稀有度 <c>CardRarity.Token</c>
/// —— 与「思索」「顿悟」一致，否则会出现在奖励/商店/图鉴里。
/// </para>
/// </summary>
[Pool(typeof(TokenCardPool))]
public class AetherArrow : WizardSpellCard
{
    private const int Cost = 1;

    /// <summary>基础 9 点；升级后 12 点（设计稿：<c>造成9（12）点以太伤害</c>）。</summary>
    private const decimal Damage = 9m;

    /// <summary>升级加成：9 → 12。</summary>
    private const decimal UpgradeDamageBonus = 3m;

    public override SpellSchool School => SpellSchool.Aether;

    /// <summary>保留 + 消耗。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move | SpellProps.For(SpellSchool.Aether)),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WizardTips.Aether,
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public AetherArrow()
        : base(Cost, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 单体攻击牌没有目标属于引擎级异常 —— 直接抛，不要静默跳过。
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // 命中演出：**白+金版**星星冲击（自制的改色场景，加载失败会回退原版紫色）。
        WizardVfx.StarryImpactGold(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            // 命中音：Regent 的「引导之星」星光音（初版那声）。
            .WithHitFx(null, WizardVfx.GuidingStarSfx)
            .Execute(choiceContext);
    }

    /// <summary>
    /// 升级：伤害 9 → **12**（设计稿 <c>造成9（12）点以太伤害</c>）。
    ///
    /// <para>
    /// ⚠️ 虽然本牌是衍生牌（<see cref="CardRarity.Token"/>，不会出现在奖励/商店/图鉴），
    /// 但**仍可被局内效果升级**（例如「武装」类升级手牌的效果、遗物给的随机升级），
    /// 所以<b>必须实现</b>升级数值，不能留空。
    /// </para>
    /// </summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBonus);
    }
}
