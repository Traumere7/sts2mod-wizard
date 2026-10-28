using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 杰作 / MagnumOpus - 0 费 - 技能 - 稀有
/// 获得 3 费。抽 3（5）张牌。跳过你的下个回合。消耗。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// "跳过你的下个回合" = 给自己 1 层 <see cref="SkipTurnPower"/>
/// ══════════════════════════════════════════════════════════════════
/// 用户定的实现口径：<i>"每次使用给自己施加 1 层效果『跳过回合』，
/// 描述是『在你的下 n 个回合开始时，结束你的回合。』，然后每回合开始时结算并掉层。"</i>
/// ⇒ 本牌<b>不</b>在这里直接结束回合（那是原版「虚空形态」<c>VoidForm</c> 的做法：
/// 打出即结束当前回合）；本牌要跳的是<b>下个</b>回合，
/// 所以交给 <see cref="SkipTurnPower"/> 在回合开始时结算。
/// 打两张「杰作」= 2 层 = 跳 2 个回合。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 卡面的「跳过」是<b>自定义关键词</b>（[gold] 修饰 + 悬停解释）
/// ══════════════════════════════════════════════════════════════════
/// 「跳过」不是原版的 <c>CardKeyword</c> 枚举成员，所以：
/// <list type="bullet">
///   <item>卡面里手写 <c>[gold]跳过[/gold]</c> 做视觉强调；</item>
///   <item>悬停解释通过 <c>ExtraHoverTips</c> 里手搓一个 <see cref="HoverTip"/> 提供 ——
///         <c>HoverTip(LocString title, LocString description)</c> 是 public 构造函数
///         （原版 <c>HoverTipFactory.Static</c> 内部也是这么 new 的，
///         见 <c>HoverTipFactory.cs:153</c>）。</item>
/// </list>
/// 文案键放在 <c>cards</c> 表里（本牌专属，不污染通用关键词表）：
/// <list type="bullet">
///   <item><c>WIZARD-MAGNUM_OPUS.skipKeyword.title</c> = 跳过；</item>
///   <item><c>WIZARD-MAGNUM_OPUS.skipKeyword.description</c> = 在你的回合开始时，结束你的回合。</item>
/// </list>
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class MagnumOpus : WizardCardModel
{
    private const int Cost = 0;

    private const int EnergyGain = 3;

    private const int Draw = 3;

    /// <summary>3 → 5。</summary>
    private const int DrawUpgrade = 2;

    /// <summary>每次打出施加几层「跳过回合」。</summary>
    private const int SkipsPerPlay = 1;

    private const string EnergyKey = "Energy";

    private const string SkipKey = nameof(SkipTurnPower);

    /// <summary>自定义关键词「跳过」的悬停解释（见类注释）。</summary>
    private const string LocTable = "cards";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        base.EnergyHoverTip,

        // 「跳过」的自定义悬停解释。
        // ⚠️ HoverTip 的构造会立刻 GetFormattedText()，所以两个键必须在 cards 表里存在，
        //    否则悬停时会拿不到文本（zhs / eng 两侧都要有）。
        new HoverTip(
            new LocString(LocTable, "WIZARD-MAGNUM_OPUS.skipKeyword.title"),
            new LocString(LocTable, "WIZARD-MAGNUM_OPUS.skipKeyword.description")),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(EnergyGain),
        new CardsVar(Draw),
        new PowerVar<SkipTurnPower>(SkipsPerPlay),
    ];

    public MagnumOpus()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 获得能量。
        await PlayerCmd.GainEnergy(DynamicVars[EnergyKey].BaseValue, Owner);

        // ② 抽牌（照抄「占卜」Divination 的写法）。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        // ③ 安排"跳过下个回合"（回合开始时由能力结算，见 SkipTurnPower）。
        await PowerCmd.Apply<SkipTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[SkipKey].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级：抽牌 3 → 5（能量与跳过层数不变）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(DrawUpgrade);
    }
}
