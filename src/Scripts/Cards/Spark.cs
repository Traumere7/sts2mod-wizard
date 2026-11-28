using BaseLib.Extensions;
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
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 火花 / Spark - 0 费 - 攻击 - 衍生（无色）
/// 造成 2(4) 点火焰伤害。消耗。
///
/// <para>
/// 本 mod 的「打击/防御」级衍生牌：由「点灯」「烟花」生成。
/// 走 <see cref="TokenCardPool"/>（无色衍生池），与「以太箭」「沉思」「顿悟」同一口径。
/// </para>
/// </summary>
[Pool(typeof(TokenCardPool))]
public class Spark : WizardSpellCard
{
    private const int Cost = 0;

    public override SpellSchool School => SpellSchool.Fire;

    /// <summary>「消耗」是固有词条（打出去就进消耗堆）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move | SpellProps.For(School)),
    ];

    /// <summary>文案里写了「消耗」，词条解释要挂上。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public Spark()
        : base(Cost, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithValueProp(ValueProp.Move | SpellProps.For(School))
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx(WizardVfx.MoltenFistFx, WizardVfx.FireHitSfx)
            .Execute(choiceContext);
    }

    /// <summary>升级：伤害 2 → 4。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
