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
/// 通灵 / Telepathy - 2 费 - 技能 - 罕见
/// 获得 11(15) 点格挡。下个回合你的第 1 张牌可免费打出。
///
/// <para>
/// <b>效果由 <see cref="TelepathyPower"/> 承载</b>，
/// 叠层语义 = "还要影响几个回合"（见该能力的类注释）。
/// 本牌只负责给格挡 + 施加 1 层。
/// </para>
///
/// <para>
/// <b>「下个回合」是自然成立的</b>：本牌在玩家回合内打出，
/// 而 <see cref="TelepathyPower"/> 只在
/// <c>AfterPlayerTurnStart</c> 时生效 —— 本回合那个钩子早已跑过，
/// 所以最早也要<b>下个回合开始</b>才生效。不需要额外记"是否本回合"。
/// </para>
///
/// <para>
/// <b>照抄原版「虚空形态」<c>VoidForm</c></b>（<c>VoidForm.cs:17-28</c>）
/// 的"卡负责施加能力"分工。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Telepathy : WizardCardModel
{
    private const int Cost = 2;

    private const int Block = 11;

    private const int BlockUpgrade = 4;

    /// <summary>施加几层「通灵」= 影响几个回合。</summary>
    private const int Turns = 1;

    private const string PowerKey = nameof(TelepathyPower);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<TelepathyPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(Block, ValueProp.Move),
        new PowerVar<TelepathyPower>(Turns),
    ];

    public Telepathy()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        await PowerCmd.Apply<TelepathyPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[PowerKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(BlockUpgrade);
    }
}
