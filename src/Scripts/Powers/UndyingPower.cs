using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 不灭 / Undying：**下个回合开始时，获得 x 点能量 n 次**（n = 层数，x = 本回合生成的牌数）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// x 的口径："既溯前、又追后"
/// ══════════════════════════════════════════════════════════════════
/// x 由常驻累加器 <see cref="GenerationTally"/> 维护（本回合玩家生成的牌数）：
/// <list type="bullet">
///   <item>打出「不灭」时 <c>AfterApplied</c> 立刻抓一次 ⇒ **溯前**；</item>
///   <item>之后每次生成，<c>AfterCardGeneratedForCombat</c> 再抓一次 ⇒ **追后**；</item>
///   <item>抓到的值存进实例字段 <c>_pendingEnergy</c>（能力悬停里的 <c>{Generated}</c> 同步更新）。</item>
/// </list>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结算：玩家回合开始时一次性结算并清除（不管多少层）
/// ══════════════════════════════════════════════════════════════════
/// <c>AfterSideTurnStart</c>（玩家侧）里：先 <c>PowerCmd.Remove(this)</c>，再
/// `for i in 0..Amount: PlayerCmd.GainEnergy(_pendingEnergy, player)`。
/// 先移除可避免"获得能量"过程再次触发本能力。
/// </para>
///
/// <para>
/// ⚠️ <b>锁定</b>：玩家回合结束时把 <c>_locked</c> 置 true，
/// 之后（下一回合开始前的任何钩子）都不再改写 <c>_pendingEnergy</c> ——
/// 防止回合开始阶段的其它生成把"上一回合的终值"覆盖成新回合的 0/小值。
/// </para>
/// </summary>
public class UndyingPower : CustomPowerModel
{
    /// <summary>悬停里"获得多少能量"的变量名（<c>{Generated}</c>）。</summary>
    public const string GeneratedKey = "Generated";

    public override PowerType Type => PowerType.Buff;

    /// <summary>层数 = 打出了几张「不灭」（每张 +1 层），结算时按层数重复。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/undying_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/undying_power_big.png";

    /// <summary>上一回合（= 打出「不灭」的那回合）结束时的生成数。</summary>
    private int _pendingEnergy;

    /// <summary>玩家回合结束后锁定，不再改写 <see cref="_pendingEnergy"/>。</summary>
    private bool _locked;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(GeneratedKey, 0m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Energy),
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        Capture();
        return Task.CompletedTask;
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        // 只认"本能力的拥有者生成的牌"。
        Player? owner = Owner?.Player;
        if (owner != null && creator != null && creator.NetId == owner.NetId)
        {
            Capture();
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner != null && participants.Contains(Owner))
        {
            _locked = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Player || Owner == null || !participants.Contains(Owner))
        {
            return;
        }

        int stacks = (int)Amount;
        int energy = _pendingEnergy;

        // 先移除，再给能量 —— 避免"获得能量"相关钩子再次触发本能力。
        await PowerCmd.Remove(this);

        Player? player = Owner?.Player;
        if (player == null || stacks <= 0 || energy <= 0)
        {
            return;
        }

        for (int i = 0; i < stacks; i++)
        {
            await PlayerCmd.GainEnergy(energy, player);
        }
    }

    /// <summary>把当前累计值抓进 <see cref="_pendingEnergy"/>，并同步悬停数字。</summary>
    private void Capture()
    {
        if (_locked)
        {
            return;
        }

        Player? owner = Owner?.Player;
        if (owner == null)
        {
            return;
        }

        _pendingEnergy = GenerationTally.Get(owner);

        if (DynamicVars.TryGetValue(GeneratedKey, out DynamicVar? var))
        {
            var.BaseValue = _pendingEnergy;
        }
    }
}
