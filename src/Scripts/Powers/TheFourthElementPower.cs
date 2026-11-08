using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using Wizard.Scripts.Cards;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 第四元素：**在你的回合开始时，将 <c>Amount</c> 张「以太箭」加入你的手牌**。
///
/// <para>
/// 触发点用 <c>AfterSideTurnStart(side = Player)</c> + <c>participants.Contains(Owner)</c>
/// —— 与「火刑」<see cref="PyroJudgmentPower"/>、「观火」等本 mod 其它"回合开始"能力一致
/// （<c>participants</c> 是**当前回合那一方**的生物）。
/// </para>
///
/// <para>
/// 生成牌走 <c>CombatState.CreateCard&lt;AetherArrow&gt;(player)</c> +
/// <c>CardPileCmd.AddGeneratedCardToCombat(..., PileType.Hand, player)</c> ——
/// 与本 mod「暗黑神秘学」「决心」同一条"生成"路径，
/// 因此会被「催化」等订阅 <c>AfterCardGeneratedForCombat</c> 的效果正确数到。
/// </para>
///
/// <para>
/// ⚠️ <b>层数 = 每回合给几张</b>（<c>PowerStackType.Counter</c>）。
/// 打两张「第四元素」就是每回合 2 张以太箭。
/// </para>
/// </summary>
public class TheFourthElementPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/the_fourth_element_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/the_fourth_element_power_big.png";

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        // 只在"自己这方的回合开始"触发（即玩家回合）。
        if (side != CombatSide.Player || !participants.Contains(Owner))
        {
            return;
        }

        Player? player = Owner.Player;
        if (player == null)
        {
            return;
        }

        int count = (int)Amount;
        if (count <= 0)
        {
            return;
        }

        Flash();

        for (int i = 0; i < count; i++)
        {
            CardModel arrow = combatState.CreateCard<AetherArrow>(player);
            await CardPileCmd.AddGeneratedCardToCombat(arrow, PileType.Hand, player);
        }
    }
}
