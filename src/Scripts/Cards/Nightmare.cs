using System.Collections.Generic;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 梦魇 / Nightmare —— **诅咒**。
/// 不可被打出。永恒。每当你抽到这张牌时，将此牌的一张复制品加入你的抽牌堆。
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
/// 抽到时的复制：有演出、即时刷新牌堆数字
/// ══════════════════════════════════════════════════════════════════
/// 照抄原版「愤怒」<c>Anger</c> / 「超频」<c>Overclock</c>：
/// <code>
/// CardModel copy = CreateClone();                                  // 战斗内克隆，不动卡组原型
/// CardPileAddResult r = await CardPileCmd.AddGeneratedCardToCombat(
///     copy, PileType.Draw, Owner, CardPilePosition.Random);
/// CardCmd.PreviewCardPileAdd(r);                                   // ★ 飞牌动画 + 抽牌堆计数即时刷新
/// </code>
/// <c>CardCmd.PreviewCardPileAdd</c>（<c>CardCmd.cs</c>）就是原版"加牌入堆"的演出入口，
/// 它会把牌飞向目标牌堆并立刻更新计数。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 关键：抽牌堆已空时必须"先等系统洗牌，再补复制品"
/// ══════════════════════════════════════════════════════════════════
/// <b>问题</b>（用户 2026-11-17 实测）：牌组里只剩 1 张「梦魇」时，
/// 回合开始抽 5 张会变成 —— 抽到梦魇 → 立刻塞一张回抽牌堆 → 引擎下一抽又抽到它 → 再塞……
/// 最终一次抽 5 张抽到 5 张梦魇。
/// </para>
///
/// <para>
/// <b>原因</b>：<c>CardPileCmd.DrawInternal</c> 的循环是
/// <code>
/// for i in 0..count:
///     await ShuffleIfNecessary(...)   // 抽牌堆空且弃牌堆有牌 ⇒ 洗牌
///     取牌堆顶 → 进手牌 → Hook.AfterCardDrawn(...)
/// </code>
/// 我们在 <c>AfterCardDrawn</c> 里往**空**抽牌堆塞的牌，就是下一次循环的牌堆顶，
/// 于是必然被立刻抽到。
/// </para>
///
/// <para>
/// <b>修法</b>：
/// <list type="number">
///   <item>抽到「梦魇」时若抽牌堆已是空 ⇒ <b>先挂起</b>（<see cref="_pendingCopies"/>），
///         这次不放牌；</item>
///   <item>引擎在下一次抽牌前会调 <c>ShuffleIfNecessary</c> →
///         <c>CardPileCmd.Shuffle</c>，它在**最后**才发
///         <c>Hook.AfterShuffle(choiceContext, shuffler)</c>（洗牌完成、牌已进牌堆之后）；
///   <item>我们在 <see cref="AfterShuffle"/> 里把挂起的复制品补进去 ——
///         洗牌已结束，所以它能落到**随机位置**（<c>CardPilePosition.Random</c>），
///         不会被立刻抽到；之后照常继续后续抽牌。</item>
/// </list>
/// 这样"一次抽 5 张 → 5 张梦魇"的连锁不会发生；
/// 复制品只是混进洗好的牌堆里，之后被抽到是随机的（设计要的"梦魇缠身"）。
/// </para>
///
/// <para>
/// 若本次抽牌后再也没有洗牌（抽牌堆和弃牌堆都空），复制品会一直挂起，
/// 直到下一次洗牌时补上 —— 这正是"必须等到系统洗牌之后"的字面语义。
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

    /// <summary>
    /// 因为"抽牌堆已空"而挂起、等待下一次系统洗牌后再补的复制品数量。
    ///
    /// <para>
    /// 普通字段即可：它只在<b>本场战斗</b>有意义（战斗结束牌全清），
    /// 不需要 <c>[SavedProperty]</c> 跨存档。
    /// </para>
    /// </summary>
    private int _pendingCopies;

    public Nightmare()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
    }

    // ══════════════════════════════════════════════════════════════════
    // 抽到这张牌
    // ══════════════════════════════════════════════════════════════════
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

        // 抽牌堆已空 ⇒ 挂起。引擎下一次抽牌前会 ShuffleIfNecessary →
        // Hook.AfterShuffle，到那时再补（见类注释）。
        if (!PileType.Draw.GetPile(Owner).Cards.Any())
        {
            _pendingCopies++;
            Entry.Logger.Info(
                $"[梦魇] 抽牌堆为空，复制品挂起（pending={_pendingCopies}），等系统洗牌后再补。");
            return;
        }

        // 抽牌堆还有牌：先把之前挂起的补上（理论上此时已洗过牌），再放这次的。
        await FlushPendingCopies();
        await AddCopyToDrawPile();
    }

    // ══════════════════════════════════════════════════════════════════
    // 系统洗牌完成 ⇒ 补上挂起的复制品
    // ══════════════════════════════════════════════════════════════════
    public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
    {
        if (shuffler != Owner || _pendingCopies <= 0)
        {
            return;
        }

        Entry.Logger.Info($"[梦魇] 系统洗牌完成，补上挂起的复制品（pending={_pendingCopies}）。");
        await FlushPendingCopies();
    }

    /// <summary>把挂起的复制品全部补进（已洗好的）抽牌堆。</summary>
    private async Task FlushPendingCopies()
    {
        // 先清零再循环：CreateClone 会 MemberwiseClone 复制本字段，
        // 不清零的话复制品会继承 pending 计数、日后又冒出来。
        int count = _pendingCopies;
        _pendingCopies = 0;

        for (int i = 0; i < count; i++)
        {
            await AddCopyToDrawPile();
        }
    }

    /// <summary>
    /// 造一张复制品，加到抽牌堆的**随机位置**，并播放"飞向牌堆"的动画（同时刷新计数）。
    /// </summary>
    private async Task AddCopyToDrawPile()
    {
        if (CombatState == null)
        {
            return;
        }

        CardModel copy = CreateClone();

        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            copy,
            PileType.Draw,
            Owner,
            CardPilePosition.Random);

        // ★ 演出 + 抽牌堆计数即时更新（原版 Anger / Overclock 同款）。
        CardCmd.PreviewCardPileAdd(result);
    }
}
