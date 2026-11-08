using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 结晶 - 1 费 - 技能 - 罕见
/// 获得 8(10) 点[格挡]。如果你在本回合中生成过卡牌，则再结算一次。
///
/// <para>
/// <b>「本回合生成过卡牌」的判定</b>照抄原版「超巨」<c>Supermassive</c>：
/// <c>CombatManager.Instance.History.Entries.OfType&lt;CardGeneratedEntry&gt;()</c>
/// 配合 <c>HappenedThisTurn(CombatState)</c>，并限定"创建者是本牌的主人"。
/// 这条口径与本 mod「催化」<c>CatalyzationPower</c> 数的完全一致
/// （同一个 <c>AfterCardGeneratedForCombat</c> 钩子落进历史）。
/// </para>
///
/// <para>
/// ⚠️ 判定放在**第一次给格挡之前**读历史也可以，但这里先给第一次格挡再判定 ——
/// 两者结果相同（本牌自己不会生成卡牌），顺序不影响。
/// </para>
///
/// <para>
/// <b>升级</b>：格挡 8 → 10。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Crystallization : WizardCardModel
{
    private const int Cost = 1;

    private const decimal Block = 8m;

    private const decimal BlockUpgrade = 2m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(Block, ValueProp.Move),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    public Crystallization()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 先结算一次格挡。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // ② 本回合生成过卡牌 ⇒ 再结算一次。
        if (GeneratedCardThisTurn())
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    /// <summary>本回合内，本牌的主人是否生成过卡牌。</summary>
    private bool GeneratedCardThisTurn()
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return false;
        }

        return CombatManager.Instance.History
            .Entries
            .OfType<CardGeneratedEntry>()
            .Any(e => e.Creator == Owner && e.HappenedThisTurn(combatState));
    }

    protected override void OnUpgrade()
    {
        // 8 → 10
        DynamicVars.Block.UpgradeValueBy(BlockUpgrade);
    }
}
