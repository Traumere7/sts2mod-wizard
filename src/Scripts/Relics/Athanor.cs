using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 恒温炉 / Athanor - Rare
/// 在你的回合开始时，获得 1 点[purple]法术增益[/purple]。
///
/// <para>
/// <b>照抄原版「硫磺」<c>Brimstone</c></b>（唯一一个每回合无条件给能力的原版遗物）：
/// <code>
/// public override async Task AfterSideTurnStart(CombatSide side,
///     IReadOnlyList&lt;Creature&gt; participants, ICombatState combatState)
/// {
///     if (participants.Contains(Owner.Creature)) { ... }
/// }
/// </code>
/// </para>
///
/// <para>
/// <b>"是不是我的回合"用 <c>participants.Contains(Owner.Creature)</c> 判定</b>，
/// <b>不需要</b>再判 <c>side == CombatSide.Player</c> ——
/// <c>AbstractModel.cs:1262-1266</c> 定义了 <c>participants</c> 的语义
/// （"参与本回合的生物"；多人下额外回合时其他玩家可能不在列表里，
/// 所以他们应该自行退出）。
/// </para>
///
/// <para>
/// <b>用 <c>new ThrowingPlayerChoiceContext()</c></b>：这是原版"不需要玩家选择"的
/// 回合开始类效果的统一写法（<c>Brimstone.cs:36</c>）。
/// 若将来改成需要玩家选择，必须改用 <c>AfterPlayerTurnStart</c>
/// （<c>AbstractModel.cs:1313-1315</c> 明确要求，否则与抽牌类效果交互会出问题）。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class Athanor : ArcaneRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SpellPowerPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        Flash();

        await PowerCmd.Apply<SpellPowerPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            DynamicVars[nameof(SpellPowerPower)].BaseValue,
            Owner.Creature,
            null);
    }
}
