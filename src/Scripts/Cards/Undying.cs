using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 不灭 / Undying - 1(0) 费 - 罕见 - 技能
/// 下回合获得等同于你在本回合生成卡牌数量的能量。
///
/// <para>
/// 本卡只负责"施加 1 层 <see cref="UndyingPower"/>"；计数、动态数值与结算全在能力里
/// （见 <see cref="UndyingPower"/> 的类注释）。
/// 升级只降费 1 → 0，不加层数。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Undying : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>每张「不灭」施加 1 层（多张叠加 = 多结算几次）。</summary>
    private const decimal StacksPerPlay = 1m;

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>文案里写了"获得能量"，挂能量悬停（与「决心」等一致）。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        base.EnergyHoverTip,
    ];

    public Undying()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        await PowerCmd.Apply<UndyingPower>(
            choiceContext,
            Owner.Creature,
            StacksPerPlay,
            Owner.Creature,
            this);
    }

    /// <summary>升级：费用 1 → 0（层数不变）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
