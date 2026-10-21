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

namespace Wizard.Scripts.Cards;

/// <summary>
/// 冰之城墙 - 2 费 - 技能 - 罕见
/// 获得 14(18) 点格挡。本回合敌人每攻击一次，就获得 1 层冻结和 2 层寒冷。
///
/// 这张牌用来验证：受伤触发型能力 + 新异常状态（冻结/寒冷）的组合。
/// 注意它本身不是法术（不造成伤害），所以不实现 ISpellCard。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class IceWall : WizardCardModel
{
    private const int Cost = 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(14m, ValueProp.Move),
        new PowerVar<FrostWallPower>(1m),
    ];

    /// <summary>
    /// 悬停提示只给**机制关键词**（冻结/寒冷），不给「冰之城墙」自己的能力提示。
    ///
    /// 原因：<c>HoverTipFactory.FromPower&lt;T&gt;()</c> 走的是 <c>GetDumbHoverTip</c>，
    /// 它按设计**不注入自定义变量**，能力文案里的变量会原样显示成占位符。
    /// 原版同类型的效果（如「火焰屏障」<c>FlameBarrierPower</c>，数值同样随升级变化）
    /// 也是**不给自己的效果做卡面提示**。此处遵循同一实践。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<FreezePower>(),
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    public IceWall()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        await PowerCmd.Apply<FrostWallPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
