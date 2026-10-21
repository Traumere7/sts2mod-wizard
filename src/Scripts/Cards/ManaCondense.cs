using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 魔力凝聚 / ManaCondense - 1(0) 费 - 能力 - 罕见
/// 在你的回合结束时，获得等同于你的[purple]法术增益[/purple]的[gold]格挡[/gold]。
///
/// <para>
/// ⚠️⚠️ <b>2026-10-06 修：本卡初版把 <c>BeforeSideTurnEndEarly</c> 直接写在卡上、
/// 而 <c>OnPlay</c> 是空的 —— 实测"打出后没有能力图标、效果也不生效"。</b>
/// 能力牌的钩子**必须由它施加的 <c>PowerModel</c> 承载**（卡打出后就进弃牌堆了），
/// 所以正确姿势是"卡 <c>OnPlay</c> 施加能力 → 能力挂回合结束钩子"。
/// 见 <see cref="ManaCondensePower"/>。
/// </para>
///
/// <para>
/// <b>格挡数值 = 法术增益层数</b>，完全动态、不写死于卡面 ——
/// 所以 <c>CanonicalVars</c> 为空，卡面文案里也没有数值占位符。
/// </para>
///
/// <para>
/// <b>升级只降费（1 → 0）</b>。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ManaCondense : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public ManaCondense()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>
    /// 打出的唯一动作就是施加 <see cref="ManaCondensePower"/> ——
    /// 效果全在那个能力的 <c>BeforeSideTurnEndEarly</c> 里。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ManaCondensePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    /// <summary>升级 1 费 → 0 费。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
