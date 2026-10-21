using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 思索 - 0 费 - 技能 - 衍生（Token）
/// 保留。选择 1 张手牌消耗。抽 2(3) 张牌。消耗。
///
/// <para>
/// 部分结构照抄原版「燃烧契约」<c>BurningPact</c>：
/// <c>CardSelectCmd.FromHand(ExhaustSelectionPrompt, ...)</c> + <c>CardCmd.Exhaust</c>，
/// 再照抄它的 <c>CardPileCmd.Draw</c>。
/// </para>
///
/// <para>
/// <b>⚠️ 牌池必须是 <c>TokenCardPool</c>，稀有度必须是 <c>CardRarity.Token</c>。</b>
/// 这是"衍生牌"的正确归属 —— 原版「燃料」<c>Fuel</c>、「仆从打击」<c>MinionStrike</c>、
/// 观者 mod 的「奇迹」<c>Miracle</c> 都是这个组合
/// （<c>[Pool(typeof(TokenCardPool))]</c> + <c>CardRarity.Token</c>）。
///
/// <b>放进 <c>WizardCardPool</c> 会让它出现在常规卡池里</b> ——
/// 奖励、商店、卡牌图鉴都会随机刷出这张本该只由初始遗物生成的牌。
/// </para>
///
/// <para>
/// <b>关键字是「保留」+「消耗」两个。</b>
/// 保留让它不会被回合末弃掉（可以留着下回合再用），
/// 消耗保证它是真正的一次性牌 —— 与「燃料」「仆从打击」一致。
/// </para>
///
/// <para>
/// <b>可被升级</b>（「武装」<c>Armaments</c>、「神化」<c>Apotheosis</c> 都能作用到它）：
/// <c>CardRarity.Token</c> 不影响 <c>IsUpgradable</c>，
/// 只要 <c>MaxUpgradeLevel &gt; 0</c>（默认就是 1）即可。
/// 升级效果见 <c>OnUpgrade</c>。
/// </para>
///
/// <para>
/// <b>选牌为空时的处理</b>：手牌里没有可选的牌（或玩家取消）时会返回 null，
/// 此时<b>只跳过消耗，仍然抽牌</b> —— 与燃烧契约一致。
/// </para>
/// </summary>
[Pool(typeof(TokenCardPool))]
public class Ponder : WizardCardModel
{
    private const int Cost = 0;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Retain,
        CardKeyword.Exhaust,
    ];

    public Ponder()
        : base(Cost, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? chosen = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            filter: null,
            source: this)).FirstOrDefault();

        if (chosen != null)
        {
            await CardCmd.Exhaust(choiceContext, chosen);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    /// <summary>升级：抽 2 → 3 张。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
