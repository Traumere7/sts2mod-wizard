using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 震慑 - 2 费 - 技能 - 普通
/// 获得 7(10) 点格挡。给予 1 层麻痹。
///
/// <para>
/// <b>麻痹是单体负面效果</b>（<see cref="ParalysisPower"/>），所以目标类型是
/// <c>TargetType.AnyEnemy</c>，而不是 <c>Self</c> —— 格挡给自己、麻痹给敌人，
/// 这种"混合目标"的牌在原版里也统一按"需要点敌人"处理
/// （对照本 mod 的「雷光」<see cref="LightningBolt"/>：伤害+负面效果都是点敌人）。
/// </para>
///
/// <para>
/// <b>结算顺序：先格挡、再上麻痹。</b>
/// 格挡用 <c>CreatureCmd.GainBlock</c>（照抄「观火」<see cref="WatchFire"/>、
/// 「冰之城墙」<see cref="IceWall"/>）。
/// </para>
///
/// <para>
/// <b>它本身不造成伤害，所以不实现 <c>ISpellCard</c></b>
/// （继承 <see cref="WizardCardModel"/> 而不是 <c>WizardSpellCard</c>），
/// 与「冰之城墙」「禁书目录」同一个做法 —— 法术流派标记只对"伤害"有意义。
/// </para>
///
/// <para>
/// <b>升级只涨格挡（7 → 10）</b>，麻痹层数不变 ——
/// 设计文档写的是"获得 7（10）格挡。给予 1 层麻痹"。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Intimidate : WizardCardModel
{
    private const int Cost = 2;

    private const string ParalysisKey = nameof(ParalysisPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move),
        new PowerVar<ParalysisPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ParalysisPower>(),
    ];

    public Intimidate()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // ① 先给自己格挡。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // ② 再给目标上麻痹。
        await PowerCmd.Apply<ParalysisPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[ParalysisKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 7 → 10（麻痹层数不变）
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
