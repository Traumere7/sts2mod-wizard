using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 失忆 / Amnesia - 1 费 - 技能 - **状态**
/// 打出后，你在下个回合少抽 1 张牌。消耗。
///
/// <para>
/// ⚠️ <b>这是状态牌，不是常规卡牌。</b>两道保险让它无法通过常规手段获得：
/// <list type="number">
///   <item><b>牌池是 <c>StatusCardPool</c></b>
///         （<c>[Pool(typeof(StatusCardPool))]</c>）——
///         与 <c>WizardCardPool</c> 分开，所以不会进巫师的卡池、奖励、商店、图鉴。</item>
///   <item><b><c>CanBeGeneratedInCombat => false</c></b>
///         （照抄原版 <c>MindRot</c>）—— 阻止"战斗中随机生成卡牌"的效果产出它。</item>
/// </list>
/// 唯一的产出来源是「勿忘我」<see cref="ForgetMeNot"/> 的
/// <c>CardPileCmd.AddGeneratedCardToCombat</c>。
/// 这与原版状态牌（<c>Burn</c>/<c>Dazed</c>/<c>MindRot</c>）的做法一致。
/// </para>
///
/// <para>
/// <b>费用是 1 而不是"不可打出"</b>：设计文档写的是"打出后，你在下个回合少抽 1 张牌。
/// 消耗。" —— 所以它是**可打出的**（花 1 费把这个负面清掉，同时承受少抽一张）。
/// 对照原版：<c>Burn</c> 是 <c>Unplayable</c>（费用 -1，留在手里每回合烧血），
/// <c>MindRot</c> 是从"知识恶魔"那里选出来直接生效（费用 -1）。
/// 本牌两种都不是，按设计做成 1 费可打出 + 消耗。
/// </para>
///
/// <para>
/// <b><c>MaxUpgradeLevel => 0</c></b>（照抄 <c>MindRot</c>/<c>Burn</c>）：
/// 状态牌不可升级。少了这个，卡牌图鉴/升级效果会把它当成可升级牌。
/// </para>
///
/// <para>
/// <b>效果用能力承载</b>（<see cref="AmnesiaPower"/>），因为"少抽牌"是
/// 抽牌阶段的钩子（<c>ModifyHandDraw</c>），卡牌本身在打出后就被消耗掉了，
/// 活不到下个回合的抽牌阶段。
/// </para>
/// </summary>
[Pool(typeof(StatusCardPool))]
public class Amnesia : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>
    /// 阻止"战斗中随机生成卡牌"的效果产出它（照抄原版 <c>MindRot</c>）——
    /// 状态牌只应由「勿忘我」指定产出，不该被随机生成池摸到。
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;

    /// <summary>状态牌不可升级。</summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// <b>「消耗」（设计明确要求）</b>——
    /// 打出后不进弃牌堆，避免这张负面牌反复循环污染牌库。
    /// 与「思索」「顿悟」同为 <c>CardKeyword.Exhaust</c>。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 少抽几张（当前设计固定 1 层）
        new PowerVar<AmnesiaPower>(1m),
    ];

    public Amnesia()
        : base(Cost, CardType.Status, CardRarity.Status, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AmnesiaPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(AmnesiaPower)].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 不可升级（<see cref="MaxUpgradeLevel"/> = 0），所以这里不会被调用；
    /// 但基类是 <c>abstract</c>，必须给个实现。
    /// </summary>
    protected override void OnUpgrade()
    {
    }
}
