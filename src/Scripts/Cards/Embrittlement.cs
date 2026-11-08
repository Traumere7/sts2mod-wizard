using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 脆化 - 1 费 - 技能 - 罕见
/// 给予所有敌人 1(2) 层[麻痹]。本回合内，你对具有[麻痹]的敌人造成的[寒冰伤害]变为 2 倍。
///
/// <para>
/// 两段效果：
/// <list type="number">
///   <item>群体上麻痹 —— 用 <c>CombatState.HittableEnemies</c>（照抄「天火浩荡」「降雷」），
///         逐个 <c>PowerCmd.Apply&lt;ParalysisPower&gt;</c>；</item>
///   <item>给自己挂 <see cref="EmbrittlementPower"/>（本回合的"寒冰伤害 ×2"），
///         它在**玩家回合结束时**自动移除（<c>AfterSideTurnEnd</c>）。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>必须先上麻痹、再挂能力</b>：能力只对"已经具有麻痹"的目标生效，
/// 顺序反了不会出错，但语义上应当先让敌人进入麻痹状态。
/// </para>
///
/// <para>
/// ⚠️ <b>"寒冰伤害"的判定用 <c>SpellProps.HasSchool(..., Ice)</c></b>，
/// 所以**以太伤害也算寒冰伤害**（design.md：以太同时被视作三系）——
/// 这是设计要求的正例，不是 bug。
/// </para>
///
/// <para>
/// <b>升级</b>：麻痹 1 → 2。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Embrittlement : WizardCardModel
{
    private const int Cost = 1;

    private const string ParalysisKey = nameof(ParalysisPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ParalysisPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ParalysisPower>(),
        HoverTipFactory.FromPower<EmbrittlementPower>(),
    ];

    public Embrittlement()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        // 先快照，避免在遍历中因为施加能力而改动集合。
        List<Creature> enemies = combatState.HittableEnemies.ToList();

        decimal paralysis = DynamicVars[ParalysisKey].BaseValue;

        // ① 群体麻痹。
        foreach (Creature enemy in enemies)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            await PowerCmd.Apply<ParalysisPower>(
                choiceContext,
                enemy,
                paralysis,
                Owner.Creature,
                this);
        }

        // ② 本回合的"寒冰伤害 ×2"（玩家回合结束时自动消失）。
        await PowerCmd.Apply<EmbrittlementPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 麻痹 1 → 2
        DynamicVars[ParalysisKey].UpgradeValueBy(1m);
    }
}
