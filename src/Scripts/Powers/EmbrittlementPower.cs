using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 脆化（本回合效果）：**你对具有「麻痹」的敌人造成的寒冰伤害变为 2 倍**。
///
/// <para>
/// 触发点 <c>ModifyDamageMultiplicative</c>（返回<b>乘数</b>，1 表示不缩放），
/// 与「寒冷」<see cref="ChillPower"/>、「超导」<see cref="SuperconductPower"/> 是同一个钩子。
/// 判定三件套：
/// <list type="number">
///   <item><c>dealer == Owner</c> —— 只放大"我方造成的"伤害；</item>
///   <item>目标身上必须有 <see cref="ParalysisPower"/>；</item>
///   <item>该伤害必须算寒冰系 —— 用 <c>SpellProps.HasSchool(..., Ice)</c>，
///         所以**以太伤害也会被放大**（design.md：以太同时被视作三系）。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>"本回合内"</b>：玩家回合结束时自动移除
/// （<c>AfterSideTurnEnd(side = Player)</c>，与「大寒波」<see cref="ColdWavePower"/> 同一条）。
/// </para>
///
/// <para>
/// ⚠️ <b>不叠层</b>：<c>PowerStackType.Single</c> —— 打两张「脆化」仍是 ×2（不是 ×4）。
/// </para>
/// </summary>
public class EmbrittlementPower : CustomPowerModel
{
    /// <summary>寒冰伤害的放大倍率。</summary>
    private const decimal Multiplier = 2m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/embrittlement_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/embrittlement_power_big.png";

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        // 只放大"我方造成的"伤害。
        if (dealer != Owner)
        {
            return 1m;
        }

        // 目标必须具有麻痹。
        if (target == null || !target.HasPower<ParalysisPower>())
        {
            return 1m;
        }

        // 只放大寒冰伤害（以太伤害也算寒冰）。
        if (!SpellProps.HasSchool(props, cardSource, SpellSchool.Ice))
        {
            return 1m;
        }

        return Multiplier;
    }

    /// <summary>玩家回合结束时移除（"本回合内"）。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}
