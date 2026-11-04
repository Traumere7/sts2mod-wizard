using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Utils;
using Godot;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 全知形态 - 3 费 - 能力 - 稀有
/// 虚无。每当你给予敌人本回合未给予过的一种负面效果时，获得 1 费并抽 1(2) 张牌。
///
/// <para>
/// 效果由 <see cref="OmniscientFormPower"/> 承载。
/// 「虚无」是<b>初始就有的</b>词条，直接列在 <c>CanonicalKeywords</c>。
/// </para>
///
/// <para>
/// <b>升级改的是"每次抽几张"</b>（1 → 2），不是获得能量的数量。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class OmniscientForm : WizardCardModel
{
    private const int Cost = 3;

    private const string AmountKey = nameof(OmniscientFormPower);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<OmniscientFormPower>(1m),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
    ];

    public OmniscientForm()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 演出（G04）：「形态展开」——
        // 原版形态牌（恶魔/回响/虚空/收割者/蛇）用的是 NFormVfx 光环，
        // 但它按**原版角色的 spine 骨骼名**绑定（NFormVfx.Initialize 里要求
        // NCreatureVisuals.HasSpineAnimation），巫师是自绘 Sprite2D 立绘，绑不上。
        // 所以退而求其次：用原版「噩梦」的整屏紫烟暗角 + 「大风车」的扫掠线做一次登场演出。
        WizardVfx.SmokyVignette(
            new Color(0.45f, 0.22f, 0.8f, 0.55f),
            new Color(0.08f, 0f, 0.35f, 0.32f));
        WizardVfx.HorizontalLines(WizardVfx.ArcaneTint, 1.2);

        await PowerCmd.Apply<OmniscientFormPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[AmountKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[AmountKey].UpgradeValueBy(1m);
    }
}
