using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 回忆：**每个回合开始时，把你上个回合打出的牌从后往前回溯，
/// 取最后 N 张「非消耗且非能力」的牌拿回手牌**（N = 层数）。
///
/// <para>
/// ⚠️⚠️ <b>2026-10-08 口径修改：计数方式变了。</b>
///
/// <list type="bullet">
///   <item><b>旧口径</b>：先按打出顺序取尾部 N 张，再逐张判断能不能回来 ——
///         <b>被消耗的牌照样占名额</b>（"沉没的名额不顺延"）。</item>
///   <item><b>新口径</b>：从后往前回溯，<b>只数够条件的牌</b>，
///         直到凑满 N 张为止 —— 消耗牌/能力牌<b>不占名额</b>，直接跳过。</item>
/// </list>
///
/// 用户原话："从上回合打出牌的序列从后往前回溯，找到最后 1（2）个
/// 非消耗且非能力的（等效于：存在于弃牌堆或抽牌堆中的）牌弹回手牌
/// （如果不存在这样的牌仍允许返回 0 张）"。
/// </para>
///
/// <para>
/// <para>
/// <b>判据只有一条：此刻在不在抽牌堆/弃牌堆</b>（用户 2026-10-10 拍板）。
/// 理由与各情形的落点见 <see cref="CanReturn"/> 的注释。
/// <b>不要</b>再往里加词条判断 —— 曾经加过「永恒」（语义是"不能从牌组移除"，
/// 与本问题无关）与「消耗」（会把"消耗牌一定被消耗了"写死）。
/// </para>
/// </para>
///
/// <para>
/// <b>触发点用 <c>BeforeHandDraw</c></b> —— 引擎里"回合开始、抽牌之前"的标准钩子
/// （<c>CombatManager.cs:900</c>）。原版「如此甚好」<c>MakeItSo</c> 也用它。
/// </para>
///
/// <para>
/// <b>取"上个回合打出的牌"用战斗历史</b>（照抄遗物「历史课」<c>HistoryCourse</c>）：
/// <code>
/// CombatManager.Instance.History.CardPlaysFinished
///     .Where(e => e.CardPlay.Player == player &amp;&amp; e.HappenedLastPlayerTurn(player))
/// </code>
/// <c>HappenedLastPlayerTurn</c> 是 <c>CombatHistoryEntry</c> 上的现成判定 ——
/// 不是"上一回合轮次"，而是"**该玩家自己**上个回合"。
/// </para>
///
/// <para>
/// <b>① 必须是"移动"，不是"复刻"。</b>
/// 用 <c>CardPileCmd.Add(card, PileType.Hand)</c> —— 它会<b>把牌从原牌堆移走</b>。
/// 这正是原版「好勇斗狠」<c>AggressionPower</c> 的做法。
///
/// 一开始我写的是 <c>CreateDupe()</c> + <c>AddGeneratedCardToCombat</c>，
/// 那是「历史课」（**自动打出一张**副本，原件留在弃牌堆）的写法，**用在这里是错的**：
/// 结果是手牌和弃牌堆**同时存在两张同名牌**。
/// 更诡异的是，洗牌时弃牌堆会被洗回抽牌堆，于是那张重复的又"消失"了 ——
/// 表现为"手里有一张、弃牌堆也有一张，洗牌后却只剩一张"。
/// </para>
///
/// <para>
/// <b>手牌上限</b>：先算空位，最多拿到满为止（与 <c>Dredge</c> 的 <c>Math.Min</c> 同思路）。
/// </para>
/// </summary>
public class RecollectionPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/recollection_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/recollection_power_big.png";

    /// <summary>
    /// 这张牌是否"非消耗且非能力"（能不能被「回忆」捞回来）。
    ///
    /// <para>
    /// ⚠️⚠️ <b>2026-10-10 定案：判据【只看堆】，不掺任何词条。</b>
    /// </para>
    ///
    /// <para>
    /// <b>为什么删掉「永恒」</b>：<c>Eternal</c> 的语义是
    /// "不能从牌组中移除"，与"这回合有没有被消耗"毫无关系 ——
    /// 把它写进这个判断是我加错了。
    /// </para>
    ///
    /// <para>
    /// <b>为什么连「消耗」词条也一起删掉</b>（用户拍板）：
    /// 一代杀戮尖塔有遗物「奇怪的勺子」（打出消耗牌时 50% 概率不消耗），
    /// 即<b>"带消耗词条的牌也可能没被消耗、仍留在堆里"</b>。
    /// 二代原版与本 mod 目前都没有这种设计，但为了不把这个可能性写死，
    /// 判据收敛为<b>唯一一条</b>：
    /// </para>
    ///
    /// <code>
    /// 合格 ⟺ 此刻位于 抽牌堆(Draw) 或 弃牌堆(Discard)
    /// </code>
    ///
    /// <para>
    /// 于是：
    /// <list type="bullet">
    ///   <item>正常结算完的牌 → 在弃牌堆 → <b>合格</b></item>
    ///   <item>被消耗的牌 → 在消耗牌堆 → 不合格</item>
    ///   <item>能力牌 → 打出后进能力区（<c>PileType.None</c>）→ 不合格</item>
    ///   <item>「保留」而留在手牌的牌、以及「虚无」在回合末被消耗的牌 →
    ///         都不在抽牌/弃牌堆 → 不合格</item>
    ///   <item>（假想的）"消耗牌没被消耗" → 仍在弃牌堆 → <b>合格</b>，
    ///         这正是要保住的灵活性</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// ⚠️ 判据时机是 <c>BeforeHandDraw</c>，发生在<b>回合末清理之后</b>，
    /// 保留/虚无/能力牌都已各就各位，牌堆处于稳定终态 ——
    /// 所以"看堆"是可靠的。
    /// </para>
    /// </summary>
    private static bool CanReturn(CardModel card)
    {
        PileType? pile = card.Pile?.Type;
        return pile is PileType.Draw or PileType.Discard;
    }

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner.Player || Amount <= 0)
        {
            return;
        }

        int freeSlots = CardPile.MaxCardsInHand - PileType.Hand.GetPile(player).Cards.Count;
        if (freeSlots <= 0)
        {
            return;
        }

        // 上回合打出的牌，按打出顺序排列。
        List<CardModel> played = CombatManager.Instance.History.CardPlaysFinished
            .Where(e => e.CardPlay.Player == player && e.HappenedLastPlayerTurn(player))
            .Select(e => e.CardPlay.Card)
            .ToList();

        if (played.Count == 0)
        {
            return;
        }

        // ★ 从后往前回溯，只数**够条件**的牌，凑满 take 张为止。
        //   消耗牌/能力牌**不占名额**，直接跳过（这是本次口径修改的核心）。
        int take = Math.Min(Amount, freeSlots);

        List<CardModel> picked = [];
        for (int i = played.Count - 1; i >= 0 && picked.Count < take; i--)
        {
            if (CanReturn(played[i]))
            {
                picked.Add(played[i]);
            }
        }

        if (picked.Count == 0)
        {
            return;
        }

        Flash();

        // ⚠️ 必须用 Add（移动），不能用 CreateDupe（复刻）。
        //
        // 按打出顺序（正序）放回，让最后打出的那张排在最后 ——
        // 手牌顺序与"打出的先后"一致，读感更自然。
        picked.Reverse();

        foreach (CardModel card in picked)
        {
            // 再次确认它**此刻**还在弃牌堆才搬。
            //
            // 为什么还要复查：`CanReturn` 是在挑牌那一刻判的，
            // 而 `CardPileCmd.Add` 是 await 的 —— 两张牌之间可能有别的钩子
            // 把它挪走（虽然极罕见）。移动一张已经不在弃牌堆的牌是错的，
            // 所以逐张移动前各查一次。
            //
            // 位于抽牌堆的牌同样合格，但**不需要移动**（它已经在牌堆里等着被抽），
            // 所以这里只处理弃牌堆的。
            if (card.Pile?.Type != PileType.Discard)
            {
                continue;
            }

            await CardPileCmd.Add(card, PileType.Hand);
        }
    }
}
