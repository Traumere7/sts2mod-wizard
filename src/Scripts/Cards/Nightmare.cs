using System.Collections.Generic;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 梦魇 / Nightmare —— **诅咒**。
/// 不可被打出。每当你抽到这张牌时，将此牌的一张复制品加入你的**弃牌堆**。永恒。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 词条：<c>Unplayable</c> + <c>Eternal</c>
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item><c>Unplayable</c>：不可打出（费用 <c>-1</c>，同原版 <c>Decay</c>）；</item>
///   <item><c>Eternal</c>：**永恒**（原版词条，引擎渲染）——
///         <c>CardModel.IsRemovable =&gt; !Keywords.Contains(Eternal)</c>
///         ⇒ 牌组里<b>无法被移除/变化</b>，所以「梦魇」一旦吃下就永久留在卡组。</item>
/// </list>
/// 两个词条的卡面文案都由引擎自动生成，<b>不要</b>写进 description。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 为什么进**弃牌堆**而不是抽牌堆（用户 2026-11-18 改稿）
/// ══════════════════════════════════════════════════════════════════
/// 早先的版本是"复制品加入**抽牌堆**"，即使加了"抽牌堆为空时等系统洗牌再补"的特判，
/// 实战里仍然会大量复制（抽牌堆非空时塞进去的牌很快就会被同回合的后续抽牌抽到）。
/// 改为**弃牌堆**后，复制品只能等下一次 `CardPileCmd.Shuffle`
/// （洗牌会把弃牌堆并进抽牌堆并打乱）或者洗牌类效果才回到抽牌堆，
/// 天然不会形成"抽到 → 立刻再抽到"的连锁。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 加牌演出 + 弃牌堆计数即时刷新
/// ══════════════════════════════════════════════════════════════════
/// 照抄原版「愤怒」<c>Anger</c>（它就是"把复制品加进弃牌堆"）：
/// <code>
/// CardModel copy = CreateClone();                                  // 战斗内克隆，不动卡组原型
/// CardPileAddResult r = await CardPileCmd.AddGeneratedCardToCombat(
///     copy, PileType.Discard, Owner);
/// CardCmd.PreviewCardPileAdd(r);                                   // ★ 飞牌动画 + 弃牌堆计数即时刷新
/// </code>
/// <c>CardCmd.PreviewCardPileAdd</c>（<c>CardCmd.cs</c>）是原版"加牌入堆"的演出入口，
/// 会把牌飞向目标牌堆并立刻更新计数（「超频」<c>Overclock</c> 也是这么用的）。
/// </para>
/// </summary>
[Pool(typeof(CurseCardPool))]
public class Nightmare : WizardCardModel
{
    /// <summary>诅咒不可升级（同原版 <c>Decay</c> / <c>Void</c>）。</summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>不可被打出 + 永恒（不能从牌组移除/变化）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Eternal,
    ];

    public Nightmare()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
    }

    /// <summary>抽到这张牌时：把一张复制品丢进弃牌堆（带飞牌演出）。</summary>
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

        // 战斗内克隆（CreateClone 会清空 DeckVersion，不污染卡组原型）。
        CardModel copy = CreateClone();

        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            copy,
            PileType.Discard,
            Owner);

        // ★ 演出 + 弃牌堆计数即时更新（原版 Anger / Overclock 同款）。
        CardCmd.PreviewCardPileAdd(result);
    }
}
