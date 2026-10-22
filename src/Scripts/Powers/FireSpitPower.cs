using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 喷火：「火山巨兽」的专属计数能力。
///
/// <para>
/// <b>层数 = 它每次喷火攻击会给玩家多少层燃烧</b>
/// （<c>docs/design.md</c> 特殊怪物一节：初始 3，第 3 个技能后 +2）。
/// </para>
///
/// <para>
/// ⚠️ <b>本能力自己没有任何逻辑</b> —— 它是一个**纯计数器**，
/// 由 <c>VolcanicBeast</c> 在出招时读 <c>Creature.GetPowerAmount&lt;FireSpitPower&gt;()</c>。
/// 这样设计的好处与「超导」「纵火狂」一致：
/// <b>叠层语义天然由引擎的 Counter 累加实现</b>，不需要自己维护任何变量。
/// </para>
///
/// <para>
/// <b>类型是 Buff</b>：它挂在怪物自己身上，表示"它的喷火威力"。
/// 玩家看到的层数就是下一次会吃到的燃烧层数。
/// </para>
///
/// <para>
/// <b>不叠能力图标上限</b>：<see cref="PowerStackType.Counter"/> 让层数直接显示，
/// 与"喷火 3 → 5"的成长感一致。
/// </para>
/// </summary>
public class FireSpitPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/fire_spit_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/fire_spit_power_big.png";
}
