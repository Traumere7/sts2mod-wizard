using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 水银：**你每回合生成的前 <c>Amount</c> 张（可附魔的）牌获得附魔：迅速 2**。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 生命周期（为什么是"本场战斗"）
/// ══════════════════════════════════════════════════════════════════
/// 只对 <c>AfterCardGeneratedForCombat</c> 送来的牌附魔 —— 那一定是**战斗中生成**的新牌，
/// 不进卡组（<c>DeckVersion == null</c>），所以附魔随这张牌在本场战斗后一起消失，
/// 不需要额外清理。详见 <c>docs/RESEARCH_MERCURY_ETERNALECHO.md</c> §2。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 时机：钩子触发时卡**已经在牌堆里**
/// ══════════════════════════════════════════════════════════════════
/// <c>CardPileCmd.AddGeneratedCardsToCombat</c> 的顺序是：
/// <c>History.CardGenerated</c> → <c>Add(card, pile)</c> → <c>Hook.AfterCardGeneratedForCombat</c>，
/// 所以这里可以直接 <c>CardCmd.Enchant</c>，卡面/牌堆 UI 会立刻刷新。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 计数口径（用户 2026-11-13 确认）
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item><c>Amount</c> = 每回合可附魔的**张数**（多张水银相加；升级牌一次给 2）；</item>
///   <item>每回合在 <c>BeforeSideTurnStart(玩家)</c> 清零；</item>
///   <item>遇到**不可附魔**的生成牌（例如状态牌）**不占名额**，顺延到下一张可附魔的。</item>
/// </list>
/// </para>
///
/// <para>
/// 「迅速」<see cref="Swift"/> 是**原版附魔**：第一次打出该牌时抽 <c>Amount</c> 张牌，
/// 之后置 Disabled（每场战斗只触发一次）；层级 <c>2</c> 由我们的卡牌传入。
/// </para>
/// </summary>
public class MercuryPower : CustomPowerModel
{
    /// <summary>每张"迅速"的层数（第一次打出时抽 2 张）。</summary>
    public const int SwiftAmount = 2;

    /// <summary>本回合已经附魔了几张。</summary>
    private int _usedThisTurn;

    public override PowerType Type => PowerType.Buff;

    /// <summary>层数 = 每回合可附魔张数（多张水银叠加）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/mercury_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/mercury_power_big.png";

    /// <summary>玩家回合开始时重置本回合名额。</summary>
    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
        {
            _usedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只认"自己生成的牌"（与「催化」「孔雀之尾」同一条口径）。
        if (creator == null || creator.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        if (_usedThisTurn >= (int)Amount)
        {
            return Task.CompletedTask;
        }

        EnchantmentModel template = ModelDb.Enchantment<Swift>();

        // CanEnchant 已排除 状态 / 诅咒 / 任务牌、卡组里不可打出的牌、已有异种附魔的牌。
        if (!template.CanEnchant(card))
        {
            // 不占名额：顺延到下一张可附魔的生成牌。
            return Task.CompletedTask;
        }

        CardCmd.Enchant(ModelDb.Enchantment<Swift>().ToMutable(), card, SwiftAmount);
        _usedThisTurn++;

        Flash();

        return Task.CompletedTask;
    }
}
