using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
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
/// 书签 / Bookmark - 1 费 - 技能 - 罕见
/// 获得 7(9) 点格挡。给手牌中的 1(2) 张牌添加[gold]保留[/gold]。
///
/// <para>
/// <b>「添加保留」照抄原版「响指」<c>Snap</c>：</b>
/// <code>
/// CardModel? chosen = (await CardSelectCmd.FromHand(
///     choiceContext, Owner,
///     new CardSelectorPrefs(SelectionScreenPrompt, count),
///     filter: c =&gt; !c.Keywords.Contains(CardKeyword.Retain),   // ← 已带保留的不再列出
///     source: this)).FirstOrDefault();
///
/// if (chosen != null) CardCmd.ApplyKeyword(chosen, CardKeyword.Retain);
/// </code>
/// 两个要点：
/// <list type="number">
///   <item><b><c>filter</c> 排除已有「保留」的牌</b> —— 否则选一张本来就保留的牌等于白选。
///         <c>Snap</c> 就是这么写的。</item>
///   <item><b>用 <c>CardCmd.ApplyKeyword</c></b>（不是直接改 <c>Keywords</c> 集合）——
///         它会正确处理"本场战斗内的临时词条"，且不会污染卡组里的原型。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>可多选（升级后 2 张）</b>：<c>Snap</c> 只选 1 张，所以它用了
/// <c>FirstOrDefault()</c>。本牌要选 1~2 张，所以直接遍历返回的可枚举，
/// 对每张都 <c>ApplyKeyword</c>。
/// <c>CardSelectorPrefs(prompt, count)</c> 的第二个参数会被自动注入成
/// 选牌界面的 <c>{Amount}</c>（见 <c>CardModel.SelectionScreenPrompt</c>
/// 里的 <c>DynamicVars.AddTo</c>），所以文案用 <c>{Amount}</c> 不用写死数字。
/// </para>
///
/// <para>
/// <b>先格挡、后加保留</b>：与设计文档一致（设计写的是"获得格挡。给…添加保留。"）。
/// </para>
///
/// <para>
/// ⚠️ <b>本地化必须提供 <c>.selectionScreenPrompt</c> 键</b>，
/// 否则 <c>CardModel.SelectionScreenPrompt</c> 会抛
/// <c>InvalidOperationException: No selection screen prompt for ...</c>。
/// 文案：<c>选择{Amount}张牌来添加[gold]保留[/gold]。</c>
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Bookmark : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>基础格挡（升级 +2 → 9）。</summary>
    private const decimal BaseBlock = 7m;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
        HoverTipFactory.Static(StaticHoverTip.Block),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(BaseBlock, ValueProp.Move),
        // 添加保留的张数（升级 1 → 2）
        new CardsVar(1),
    ];

    public Bookmark()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 先获得格挡。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // ② 给手牌中若干张牌添加「保留」。
        int count = DynamicVars.Cards.IntValue;
        if (count <= 0)
        {
            return;
        }

        IEnumerable<CardModel> chosen = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, count),
            // 照抄 Snap：已经带「保留」的牌不列出来（选了也是白选）。
            filter: c => !c.Keywords.Contains(CardKeyword.Retain),
            source: this);

        // 可多选，所以要遍历；Snap 只选 1 张才用 FirstOrDefault。
        foreach (CardModel card in chosen.ToList())
        {
            CardCmd.ApplyKeyword(card, CardKeyword.Retain);
        }
    }

    protected override void OnUpgrade()
    {
        // 7 → 9；张数 1 → 2
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
