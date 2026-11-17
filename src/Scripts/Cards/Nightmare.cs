using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 梦魇 / Nightmare —— **诅咒**。
/// 不可被打出。每当你抽到这张牌时，将此牌的一张复制品加入你的抽牌堆。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结构照抄原版诅咒 <c>Decay</c> / 状态牌 <c>Void</c>
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item><c>CardType.Curse</c> + <c>CardRarity.Curse</c> + 费用 <c>-1</c>
///         （-1 = 不可打出；<c>Decay.cs</c> 的写法）；</item>
///   <item><c>MaxUpgradeLevel =&gt; 0</c>（诅咒不可升级，同 <c>Decay</c> / <c>Void</c>）；</item>
///   <item><c>CanonicalKeywords =&gt; [Unplayable]</c> —— 卡面的「不可被打出」文案由词条自动生成，
///         <b>不要</b>再写进 description。</item>
/// </list>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// "抽到这张牌时"：<c>AfterCardDrawn</c> 钩子（照抄 <c>Void</c>）
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// // Void.cs
/// public override async Task AfterCardDrawn(PlayerChoiceContext ctx, CardModel card, bool fromHandDraw)
/// {
///     if (card == this) { ... }
/// }
/// </code>
/// ⚠️ 这个钩子对<b>每一次抽牌</b>都会发，必须靠 <c>card == this</c> 过滤，
/// 且它只在自己处于战斗牌堆时才会被派发（<c>CardModel.ShouldReceiveCombatHooks</c>）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// "把复制品加入抽牌堆"：照抄「愤怒」<c>Anger</c>
/// ══════════════════════════════════════════════════════════════════
/// <code>
/// CardModel copy = CreateClone();                                   // 战斗内克隆，不动卡组原型
/// await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Draw, Owner);
/// </code>
/// <c>CreateClone()</c> 会走 <c>CloneCard</c>（<c>AfterCloned</c> 里把 <c>DeckVersion</c> 清空），
/// 所以复制品是<b>本场战斗的临时牌</b>，不会污染玩家卡组。
/// 复制品入手后再被抽到时仍会再复制一张 —— 这正是设计要的"梦魇缠身"。
/// </para>
/// </summary>
[Pool(typeof(CurseCardPool))]
public class Nightmare : WizardCardModel
{
    /// <summary>诅咒不可升级（同原版 <c>Decay</c> / <c>Void</c>）。</summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>不可被打出 —— 卡面「不可被打出」由该词条自动生成。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
    ];

    public Nightmare()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
    }

    /// <summary>抽到这张牌时：把一张复制品塞回抽牌堆。</summary>
    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        // 这个钩子对每次抽牌都会触发，先过滤掉"抽的不是我"。
        if (card != this || CombatState == null)
        {
            return;
        }

        // 和 Void 一样给一点停顿，让玩家看清"抽到了梦魇"。
        await Cmd.Wait(0.25f);

        CardModel copy = CreateClone();
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Draw, Owner);
    }
}
