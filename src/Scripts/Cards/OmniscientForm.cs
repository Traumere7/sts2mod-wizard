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
        await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);

        // 持续形态光环由 OmniscientFormPower.AfterApplied 挂载原版 NReaperFormVfx（主体）
        // 并叠加拆分染紫的 serpent_form 扩散脉动光环，每次触发回费抽牌时同步脉动爆发。
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
