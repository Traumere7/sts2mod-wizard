using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 本回合免费：<b>本回合你打出的第 <c>Amount</c> 张牌免费</b>。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 这是一个"一次性、每回合重建"的载体能力（2026-10-19 用户设计）
/// ══════════════════════════════════════════════════════════════════
/// 它<b>不自己计回合</b>，只负责一件事：让本回合的若干张牌免费。
/// 生命周期完全由外部决定：
/// <list type="bullet">
///   <item>由 <see cref="TelepathyPower"/> 在<b>玩家回合开始</b>施加 1 层；</item>
///   <item>每打出一张牌 <b>-1 层</b>（归零由引擎自动移除）；</item>
///   <item><b>回合结束必然移除</b>（哪怕一次都没用掉）。</item>
/// </list>
/// 这样拆开的好处：<b>施加方</b>（通灵）可以在回合开始就放心掉层，
/// 不会因为"层数归零被移除"而丢掉当回合的效果 —— 效果已经沉降到这个载体上。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「免费」的实现：照抄原版「虚空形态」<c>VoidFormPower</c>
/// ══════════════════════════════════════════════════════════════════
/// 重写 <c>TryModifyEnergyCostInCombatLate</c> 与 <c>TryModifyStarCost</c>
/// 两个改费钩子，都把费用改成 <c>0</c>（<c>VoidFormPower.cs:85-109</c>）。
/// 两者都要 —— "免费"应同时免掉能量与星星。
/// ⚠️ 只对 <c>PileType.Hand</c> / <c>PileType.Play</c> 的牌生效
/// （照抄 <c>VoidFormPower.ShouldSkip</c>）：牌堆里的牌不该显示成 0 费。
/// </para>
///
/// <para>
/// ⚠️ <b>扣层在 <c>AfterCardPlayed</c>，且要排除自动打出</b>：
/// 能力触发的免费牌（<c>IsAutoPlay</c>）不该消耗玩家的机会。
/// 与「通灵」原先同一套判据：只看"
/// 玩家主动打出的、属于这个玩家的、且是同一次连击的最后一张"。
/// </para>
///
/// <para>
/// ⚠️ <b>回合结束必须 <c>Remove</c> 而不能只用 <c>Decrement</c></b>：
/// <c>Amount</c> 可能还剩 1 层没用掉，若只靠"归零自动移除"，
/// 它会跨回合残留到下一个回合 —— 那就变成了"跨回合累计"，
/// 与"本回合"的语义直接冲突。
/// </para>
/// </summary>
public class FreeThisTurnPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>计数型：<c>Amount</c> = 本回合还剩几张免费牌。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/free_this_turn_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/free_this_turn_power_big.png";

    // ══════════════════════════════════════════════════════════════════
    // ① 改费用：把手牌里的牌变 0 费
    // ══════════════════════════════════════════════════════════════════
    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        return TryMakeFree(card, originalCost, out modifiedCost);
    }

    public override bool TryModifyStarCost(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        return TryMakeFree(card, originalCost, out modifiedCost);
    }

    private bool TryMakeFree(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;

        if (Amount <= 0 || ShouldSkip(card))
        {
            return false;
        }

        modifiedCost = 0m;
        return true;
    }

    /// <summary>
    /// 是否不该受本能力影响 —— 照抄 <c>VoidFormPower.ShouldSkip</c>
    /// （<c>VoidFormPower.cs:186-210</c>）。
    /// </summary>
    private static bool ShouldSkip(CardModel card)
    {
        // 只有在手牌 / 正在打出的牌才显示 0 费。
        PileType? pileType = card.Pile?.Type;
        return pileType is not (PileType.Hand or PileType.Play);
    }

    // ══════════════════════════════════════════════════════════════════
    // ② 打出一张牌 ⇒ 掉 1 层
    // ══════════════════════════════════════════════════════════════════
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Amount <= 0)
        {
            return;
        }

        // 只看"玩家主动打出的、这个玩家的、且属于同一次连击的最后一张"。
        // 自动打出（能力触发的免费牌）不该消耗这次机会。
        if (cardPlay.IsAutoPlay
            || !cardPlay.IsLastInSeries
            || cardPlay.Card.Owner?.Creature != Owner)
        {
            return;
        }

        // 归零时由 ModifyAmount 内部 ShouldRemoveDueToAmount 自动移除。
        await PowerCmd.Decrement(this);
    }

    // ══════════════════════════════════════════════════════════════════
    // ③ 回合结束：必然移除
    // ══════════════════════════════════════════════════════════════════
    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        // 不能只靠 Decrement —— 若有没用掉的层数，它会残留到下一个回合。
        await PowerCmd.Remove(this);
    }
}
