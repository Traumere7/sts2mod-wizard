using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 火刑 - 1 费 - 能力 - 罕见
/// 回合开始时，给予全体敌人 3(4) 层燃烧，然后把这个数值增加 1。
///
/// <para>
/// 效果由 <see cref="PyroJudgmentPower"/> 承载，本卡只负责施加能力。
/// 层数用 <c>PowerVar&lt;PyroJudgmentPower&gt;</c> 表达，
/// **升级改的就是这个初始值**（3 → 4）—— 能力后续会自行递增，
/// 所以升级只影响起点。
/// </para>
///
/// <para>
/// <b>这是能力牌</b>（<c>CardType.Power</c>），对商店卡位生成有正面作用
/// （见 <c>Superconduct</c> 的注释：商店需要池子里有至少一张能力牌）。
/// </para>
/// <para>
/// <b>叠加语义（成长期用独立变量）</b>：
/// <list type="bullet">
///   <item><c>{PyroJudgmentPower}</c> → 每回合施加的燃烧层数，<b>每次施加累加</b></item>
///   <item><c>{Growth}</c> → 每回合的成长量，也随施加次数累加</item>
/// </list>
/// k 张火刑 = 每回合给 <c>3k</c> 层、成长 <c>+k</c>。
/// 单张 = 3 层/+1；两张 = 6 层/+2。
/// 详见 <see cref="PyroJudgmentPower.GrowthKey"/> 的说明 ——
/// 若把成长塞进 <c>Amount</c> 的增量，第二张就会变成"初值 6、每回合 +1"，与设计不符。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class PyroJudgment : WizardCardModel
{
    private const int Cost = 1;

    private const string BurnRateKey = PyroJudgmentPower.BurnRateKey;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 卡面显示的「给予 N 层燃烧」：初值 3，升级 4。
        // 这个值在 OnPlay 里写入能力，之后由能力自己递增。
        new DynamicVar(BurnRateKey, PyroJudgmentPower.InitialBurnRate),
    ];

    public PyroJudgment()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ⚠️ 施加的数量传的是**成长增量**（1），不是燃烧量。
        //
        // 能力的 Amount 是"成长值"，靠引擎的层数累加实现"每张 +1"：
        //   单张 → Amount = 1（成长 +1/回合）
        //   两张 → Amount = 2（成长 +2/回合）
        // 若这里传燃烧量，第二张就会把 Amount 加成 6，燃烧量翻倍 —— 与设计不符。
        await PowerCmd.Apply<PyroJudgmentPower>(
            choiceContext,
            Owner.Creature,
            PyroJudgmentPower.GrowthPerApplication,
            Owner.Creature,
            this);

        // 燃烧量的初值由卡牌决定（3，升级 4），
        // 必须在施加之后写，否则会被新建实例的默认值覆盖。
        if (Owner.Creature.GetPower<PyroJudgmentPower>() is { } power)
        {
            power.SetInitialBurnRate(DynamicVars[BurnRateKey].BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        // 只提高初始燃烧量（3 → 4），不影响成长。
        DynamicVars[BurnRateKey].UpgradeValueBy(1m);
    }
}
