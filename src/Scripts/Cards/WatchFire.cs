using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 观火 - 1 费 - 技能 - 普通
/// 获得 8(11) 点格挡，给予全体敌人 1 层燃烧。
///
/// 这张牌用来验证"全体施加 + 取高值叠加"的组合是否正确。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class WatchFire : WizardCardModel
{
    private const int Cost = 1;

    private const string BurnKey = nameof(BurnPower);

    /// <summary>悬停时展示相关机制说明。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new PowerVar<BurnPower>(1m),
    ];

    public WatchFire()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        ICombatState? combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        List<Creature> enemies = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .ToList();

        await SpellCmd.ApplyBurnToAll(
            choiceContext,
            enemies,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
