using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Cards;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 「本回合获得 N 点法术增益」的临时载体：**回合结束时把这 N 点扣回去**。
///
/// <para>
/// <b>照抄原版「热修复」<c>Hotfix</c> / 「预判」<c>Anticipate</c> 的机制</b>
/// （真身是 <c>TemporaryFocusPower</c> / <c>TemporaryDexterityPower</c>）：
/// <list type="number">
///   <item><c>BeforeApplied</c> 里**先把属性真正加上**（这里加 <c>SpellPowerPower</c>）；</item>
///   <item><c>AfterSideTurnEnd</c> 里移除自己，再把同样的量**扣回去**。</item>
/// </list>
/// 引擎里 <c>PowerCmd.Apply</c> 是 <c>BeforeApplied</c> → <c>ApplyInternal</c> 的顺序
/// （已核实），所以"先加属性、再挂上自己"是成立的。
/// </para>
///
/// <para>
/// ⚠️⚠️ <b><see cref="Type"/> 必须是 <c>Debuff</c> —— 这是设计要的，不是笔误。</b>
/// 因为「高温消毒」<see cref="SterilizeByFire"/> 的实现是
/// "遍历 <c>Owner.Creature.Powers</c> 找出所有 <c>Type == PowerType.Debuff</c> 的逐个移除"。
/// 把它标成 <c>Debuff</c>，玩家就能用「高温消毒」**提前摘掉这颗临时标记** ——
/// 而"扣回属性"的代码就在 <c>AfterSideTurnEnd</c> 里、随它一起消失，
/// 于是**临时的法术增益变成了永久的**。
/// </para>
///
/// <para>
/// <b>这是刻意设计的玩法</b>（"用净化把临时增益固化"），
/// 所以别因为"效果明明是纯收益却标 Debuff"而改掉它。
/// 文案上也按负面效果的口径写（见 <c>powers.json</c> 的
/// <c>WIZARD-TEMPORARY_SPELL_POWER_POWER.*</c>）。
/// </para>
///
/// <para>
/// ⚠️ 注意别和「高温消毒」自身给的 1 层燃烧冲突：那是另一张牌的代价，与本能力无关。
/// </para>
/// </summary>
public class TemporarySpellPowerPower : CustomPowerModel
{
    /// <summary>
    /// <b>刻意是 Debuff</b> —— 让「高温消毒」能移除它，从而固化临时法术增益。
    /// 详见类注释。
    /// </summary>
    public override PowerType Type => PowerType.Debuff;

    /// <summary>计数型：<c>Amount</c> = 本回合临时获得的法术增益层数。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath =>
        "res://Wizard/images/powers/temporary_spell_power_power.png";

    public override string? CustomBigIconPath =>
        "res://Wizard/images/powers/temporary_spell_power_power_big.png";

    /// <summary>
    /// 先把法术增益真正加上（照抄 <c>TemporaryFocusPower.BeforeApplied</c>）。
    ///
    /// <para>
    /// 用 <c>ThrowingPlayerChoiceContext</c> 是为了在"施加能力的流程里再施加另一个能力"
    /// —— 与 <c>TemporaryFocusPower</c> 完全一致，别改成传进来的那个 context。
    /// </para>
    /// </summary>
    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        await PowerCmd.Apply<SpellPowerPower>(
            new ThrowingPlayerChoiceContext(),
            target,
            amount,
            applier,
            cardSource,
            silent: true);
    }

    /// <summary>
    /// 层数变化时同步补上/补扣（照抄 <c>TemporaryFocusPower.AfterPowerAmountChanged</c>）——
    /// 覆盖"本回合内又打了一张「快速咏唱」"这种叠加情形。
    ///
    /// <para>
    /// <c>amount == Amount</c> 时要跳过：那是 <c>BeforeApplied</c> 刚加完之后自己触发的回调，
    /// 已经由 <c>BeforeApplied</c> 处理过了，再补一次会重复。
    /// </para>
    /// </summary>
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this || amount == Amount)
        {
            return;
        }

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext, Owner, amount, applier, cardSource, silent: true);
    }

    /// <summary>
    /// 回合结束时**扣回**临时法术增益，然后移除自己。
    ///
    /// <para>
    /// 用 <c>participants.Contains(Owner)</c> 判定（照抄 <c>TemporaryFocusPower</c>），
    /// 而不是判 <c>side == CombatSide.Player</c> —— 这样玩家与敌人两侧都通用。
    /// </para>
    /// </summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        Flash();

        int amount = Amount;

        // ⚠️ 顺序：先移除自己，再扣属性。
        //    反过来的话，"扣属性"可能又触发 AfterPowerAmountChanged 造成重复扣减。
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext, Owner, -amount, Owner, null);
    }
}
