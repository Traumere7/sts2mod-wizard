namespace Wizard.Scripts.Powers;

/// <summary>
/// 怪物版「燃烧免疫」—— <b>复用 <see cref="BurnImmunityPower"/> 的全部效果，只换一套文案</b>。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么要单开一个子类，而不是在原类里按拥有者切换文案
/// ══════════════════════════════════════════════════════════════════
/// 第一版试过重写 <c>PowerModel.Title</c> / <c>Description</c>，在里面判断
/// "<c>Owner</c> 是不是玩家"。结果<b>直接崩</b>（2026-10-22 实测）：
/// <code>
/// CanonicalModelException: Canonical model of type BurnImmunityPower used in incorrect place
///   at PowerModel.get_Owner()            ← ★ Owner 的 getter 自身就是 AssertMutable()
///   at BurnImmunityPower.get_OwnedByMonster()
///   at BurnImmunityPower.get_Description()
///   at PowerModel.GetDumbHoverTip(...)
///   at HoverTipFactory.FromPower[BurnImmunityPower]()
///   at Pyromaniac.get_ExtraHoverTips()   ← 「纵火狂」的卡面悬停
/// </code>
/// 原因是<b>卡牌悬停提示读的是 canonical（不可变）实例</b>，而
/// <c>PowerModel.Owner</c> 的 getter 第一行就是 <c>AssertMutable()</c>
/// （<c>PowerModel.cs</c> 的 <c>public Creature Owner</c>）——
/// 只要碰 <c>Owner</c> 就会抛。所以"按拥有者切文案"这条路走不通。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 子类方案的三个关键点
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>效果完全继承</b>：不清燃烧的 <c>AfterApplied</c> 等一切逻辑照旧。</item>
///   <item><b>拦截依然有效</b>：<c>SpellCmd.ApplyBurn</c> 的判据是
///         <c>target.HasPower&lt;BurnImmunityPower&gt;()</c>（<c>SpellCmd.cs:63</c>），
///         而 <c>Creature.HasPower&lt;T&gt;</c> 内部是 <c>_powers.Any(p =&gt; p is T)</c>
///         —— <b>子类实例也算命中</b>，所以怪物照样免疫燃烧。</item>
///   <item><b>文案自动独立</b>：<c>PowerModel.Description</c> 用
///         <c>Id.Entry + ".description"</c> 取键，子类的 Entry 是
///         <c>WIZARD-MONSTER_BURN_IMMUNITY_POWER</c>，于是天然读另一套键 ——
///         不需要任何重写。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ 文案里**写死了「火山巨兽」**（用户要求"火山巨兽不会受到燃烧影响"）。
/// 若以后别的怪物也想复用这个能力，要么再开一个子类，
/// 要么把文案改成"该敌人…"这类中性说法。
/// </para>
/// </summary>
public class MonsterBurnImmunityPower : BurnImmunityPower
{
}
