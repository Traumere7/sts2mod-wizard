using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 环流：**电流外溢现在可以对敌人自身结算**。
///
/// <para>
/// 原版语义见 <see cref="CurrentOverflowPower"/>：附着在敌人身上，
/// 该敌人受雷电伤害时把伤害溅射给**随机另一名敌人**；
/// 单体战斗中"另一名敌人"不存在，于是完全无事发生（设计如此）。
/// </para>
///
/// <para>
/// 本能力把候选池从"<b>除宿主 X 以外的其他敌人</b>"放宽为
/// "<b>包括 X 在内的所有敌人</b>"——
/// 于是单体战斗也能吃到外溢（此时必定弹回 X 自己），
/// 多体战斗则多了"可能弹回 X 自己"的随机项。
/// </para>
///
/// <para>
/// <b>⚠️ 候选池永远只含敌人，不会包含玩家。</b>
/// 本能力的语义不是"把玩家拉进候选池"，只是"把宿主自己放回去"。
/// 中间一版实现曾把 <c>PlayerCreatures</c> 也拼进候选池 —— 那是错的。
/// </para>
///
/// <para>
/// 判定方式是在 <see cref="CurrentOverflowPower"/> 里查
/// <c>combatState.PlayerCreatures.Any(c =&gt; c.HasPower&lt;CirculationPower&gt;())</c>
/// —— <b>不能</b>用 <c>Owner.HasPower&lt;CirculationPower&gt;()</c>：
/// <c>Owner</c> 是**敌人**，而能力挂在**玩家**身上。
/// 也<b>不能</b>用 <c>Owner.Player</c>：敌人的 <c>Creature.Player</c> 恒为 null。
/// </para>
///
/// <para>
/// 这是**能力**牌（<c>CardType.Power</c>），对商店卡位生成有正面作用 ——
/// 详见 <c>Superconduct</c> 的注释（商店需要池子里有至少一张能力牌）。
/// </para>
/// </summary>
///
/// <para>
/// 这是**能力**牌（<c>CardType.Power</c>），对商店卡位生成有正面作用 ——
/// 详见 <c>Superconduct</c> 的注释（商店需要池子里有至少一张能力牌）。
/// </para>
/// </summary>
public class CirculationPower : CustomPowerModel
{
    /// <summary>升级后的费用由卡牌自身处理，这里不需要额外数值。</summary>
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/circulation_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/circulation_power_big.png";
}
