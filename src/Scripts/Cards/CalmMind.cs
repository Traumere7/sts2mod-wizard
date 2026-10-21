using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 镇静 - 2 费 - 技能 - 初始卡组
/// 获得 10(13) 点格挡，获得 1 层法术增益。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class CalmMind : WizardCardModel
{
    private const int Cost = 2;

    private const string SpellPowerKey = nameof(SpellPowerPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new PowerVar<SpellPowerPower>(1m),
    ];

    /// <summary>悬停时展示相关机制说明。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public CalmMind()
        : base(Cost, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[SpellPowerKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
