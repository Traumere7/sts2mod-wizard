using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 燃烧免疫：拥有者**不会获得燃烧**，且获得本效果时会清除身上已有的燃烧。
///
/// <para>
/// <b>这是原版没有对应物的效果</b>，两条需求分别用两种手段实现：
/// <list type="number">
///   <item><b>获得时清除已有燃烧</b> —— 在 <see cref="AfterApplied"/> 里
///         直接 <c>PowerCmd.Remove&lt;BurnPower&gt;()</c>。</item>
///   <item><b>阻止后续施加</b> —— 在 <see cref="SpellCmd.ApplyBurn"/> 入口处拦截。
///         那里是**所有**燃烧施加的唯一收口（「高温消毒」「决心」以及未来的
///         任何来源都走它），所以一处拦截即可覆盖全部来源。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ 为什么必须在 <c>SpellCmd</c> 层拦、而不能靠已有的
/// <c>TryModifyPowerAmountReceived</c>（<see cref="BurnPower"/> 用那个实现"取高值"）：
/// 那个钩子只能**改数值**、没有否决语义，无法表达"这次施加完全不发生"。
/// 详见 <c>CONTEXT.md</c> §6.13 关于燃烧特例的说明。
/// </para>
///
/// <para>
/// <b>不叠层</b>：<see cref="PowerStackType.Single"/> ——
/// 免疫是布尔状态，重复获得没有意义。
/// </para>
///
/// <para>
/// <b>只拦"给拥有者自己"的施加</b>：敌人身上的燃烧照常。所以「纵火狂」
/// 依旧可以给敌人上燃烧来赚活力，只是玩家自己烧不起来了。
/// </para>
/// </summary>
public class BurnImmunityPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    /// <summary>不叠层：免疫是布尔状态。</summary>
    public override PowerStackType StackType => PowerStackType.Single;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/burn_immunity_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/burn_immunity_power_big.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    // ══════════════════════════════════════════════════════════════════
    // 按"挂在谁身上"切换文案（2026-10-22 用户要求）
    // ══════════════════════════════════════════════════════════════════
    //
    // 同一个能力既可能挂在**玩家**身上（「纵火狂」），也可能挂在**怪物**身上
    // （「火山巨兽」）。默认文案是第二人称「你不会受到燃烧影响」，
    // 出现在怪物身上就变成"你说怪物"了，读着很怪。
    //
    // ⇒ 重写 `PowerModel` 的两个 `virtual` 属性：
    //      Title   (PowerModel.cs:49)
    //      Description (PowerModel.cs:51)
    //   以及 `SmartDescriptionLocKey`（`:87`，带 {Amount} 的那份），
    //   在"拥有者是怪物"时换成 `.monster*` 前缀的另一套键。
    //
    // ⚠️ 判定必须写 `Owner is { Player: null }`（**Owner 非 null 且不是玩家**）：
    //   · canonical 实例的 Owner 是 null —— 卡牌悬停提示
    //     （`HoverTipFactory.FromPower<BurnImmunityPower>()`）走的是它，
    //     必须仍然拿玩家版文案；
    //   · 挂在玩家身上 → Owner.Player != null → 玩家版 ✓；
    //   · 挂在怪物身上 → Owner != null 且 Owner.Player == null → 怪物版 ✓。

    /// <summary>本能力是否挂在**怪物**身上（canonical 实例不算）。</summary>
    private bool OwnedByMonster => Owner is { Player: null };

    public override LocString Title =>
        OwnedByMonster
            ? new LocString("powers", Id.Entry + ".monsterTitle")
            : base.Title;

    public override LocString Description =>
        OwnedByMonster
            ? new LocString("powers", Id.Entry + ".monsterDescription")
            : base.Description;

    protected override string SmartDescriptionLocKey =>
        OwnedByMonster
            ? Id.Entry + ".monsterSmartDescription"
            : base.SmartDescriptionLocKey;

    /// <summary>
    /// 获得本效果时，清除拥有者身上已有的燃烧。
    ///
    /// <para>
    /// <c>PowerCmd.Remove&lt;T&gt;</c> 内部是 <c>Remove(creature.GetPower&lt;T&gt;())</c>，
    /// 而 <c>GetPower</c> 在没有该能力时返回 null、<c>Remove</c> 对 null 直接返回 ——
    /// 所以"身上没燃烧"时是安全的空操作。
    /// </para>
    /// </summary>
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await PowerCmd.Remove<BurnPower>(Owner);
    }
}
