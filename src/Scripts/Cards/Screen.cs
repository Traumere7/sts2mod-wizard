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

namespace Wizard.Scripts.Cards;

/// <summary>
/// 屏蔽 - 1 费 - 技能 - 普通
/// 获得 5(7) 点格挡。给予所有敌人 1(2) 层电流外溢。
///
/// <para>
/// <b>电流外溢挂在"被打的敌人"身上</b>（对照本 mod 的「雷光」<see cref="LightningBolt"/>：
/// "电流外溢附着在被打的敌人身上，它之后再受到雷电伤害时溅射给其他敌人"）。
/// 所以本牌是**先给每个敌人各自挂 1 层**，而不是给玩家自己加个全局层数。
/// </para>
///
/// <para>
/// <b>为什么对每个敌人单独 <c>PowerCmd.Apply</c>，而不是 <c>Apply(…, IEnumerable&lt;Creature&gt;)</c>：</b>
/// 两者最终都会逐个 <c>Apply</c>（<c>PowerCmd.cs:36-52</c> 就是个 foreach），
/// 但逐个写能顺带处理"没有敌人 / 敌人中途死亡"的情况，
/// 且与本 mod 其它群体施加的写法（<c>SpellCmd.ApplyBurnToAll</c>）口径一致，便于阅读。
/// </para>
///
/// <para>
/// <b>结算顺序：先格挡、再群体上电流外溢。</b>
/// </para>
///
/// <para>
/// <b>升级只涨格挡与层数</b>：5 → 7 格挡、1 → 2 层电流外溢（按设计文档）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Screen : WizardCardModel
{
    private const int Cost = 1;

    private const string OverflowKey = nameof(CurrentOverflowPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<CurrentOverflowPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CurrentOverflowPower>(),
    ];

    public Screen()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 先给自己格挡。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        // ② 再给每个敌人各自挂电流外溢。
        List<Creature> enemies = combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .ToList();

        decimal overflow = DynamicVars[OverflowKey].BaseValue;

        foreach (Creature enemy in enemies)
        {
            await PowerCmd.Apply<CurrentOverflowPower>(
                choiceContext,
                enemy,
                overflow,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        // 5 → 7 格挡；1 → 2 层电流外溢
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars[OverflowKey].UpgradeValueBy(1m);
    }
}
