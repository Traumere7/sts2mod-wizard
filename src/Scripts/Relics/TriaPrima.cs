using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 三原素 / TriaPrima - Shop
/// 每当你在同一回合内将[orange]火焰[/orange]、[blue]雷电[/blue]、[blue]寒冰[/blue]
/// 伤害都造成过后触发：获得 1 层[gold]无实体[/gold]，该遗物在本回合和下回合失效。
///
/// <para>
/// ⚠️ 上面的 `[blue]寒冰[/blue]` 是从文案里照抄的 —— **`[ice]` 不是合法 BBCode 标签**，
/// 游戏不会渲染、会原样显示 `[ice]`。本 mod 的 `cards.json` 里寒冰伤害统一用
/// `[blue]`（与雷电一致）。注释里也一并避免写错，因为它会被编进 pck。
/// </para>
/// ══════════════════════════════════════════════════════════════════
/// 口径（2026-10-12 用户拍板）
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>必须"攻击命中"</b>：三种元素都要<b>实实在在打到怪身上</b>才触发。
///         所以判定门用 <c>props.IsPoweredAttack()</c> 且 <c>result.UnblockedDamage &gt; 0</c>
///         （照抄原版「涂毒」<c>EnvenomPower.cs:21</c> 的写法）。</item>
///   <item><b>"本回合和下回合失效"</b> = 给出无实体后，直到<b>下下回合</b>才重新亮起。</item>
///   <item><b>每场战斗开始时一定亮起</b>（所有状态在 <c>BeforeCombatStart</c> 归零）。</item>
/// </list>
///
/// <para>
/// ⚠️⚠️ <b><c>RelicStatus.Disabled</c> 只是外观，不会禁用任何行为。</b>
/// 已核实：<c>Status</c> 的 setter 只发事件（<c>RelicModel.cs:330-345</c>），
/// 唯一消费者是图标 shader（<c>RelicModel.cs:473-504</c>）；
/// 而钩子派发时<b>只</b>过滤 <c>IsMelted</c>、从不读 <c>Status</c>
/// （<c>CombatState.cs:432</c> / <c>Hook.cs:53-63</c>）。
/// 所以必须自己用 <see cref="DisabledUntilTurn"/> 守卫<b>每个</b>钩子体。
/// </para>
///
/// <para>
/// <b>伤害类型识别用本 mod 的 <see cref="SpellProps.Resolve"/></b>：
/// <c>ValueProp</c> 随伤害本身传递，任何来源（卡牌/能力/遗物）都可靠；
/// 而 <c>cardSource</c> 在非卡牌来源时是 null、靠它会静默失效。
/// 本 mod 已有 5 处这么用（<c>AlternatingCurrentPower</c> / <c>BurningSoulPower</c> /
/// <c>SuperconductPower</c> / <c>CurrentOverflowPower</c> / <c>ChillPower</c>）。
/// </para>
///
/// <para>
/// ⚠️ <b>原版 300 个遗物里没有任何一个重写 <c>AfterDamageGiven</c></b>（已 grep 确认）——
/// 这个钩子的用法来自能力（<c>EnvenomPower</c> / <c>PaperCutsPower</c> 等）。
/// 钩子本身对遗物可用（<c>Hook.cs:401</c> 走 <c>IterateHookListeners</c>，
/// 遗物在其中；且它<b>刻意绕过</b>"战斗已结束"守卫，见 <c>Hook.cs:398-400</c>，
/// 因为击杀的那一刀也要能触发）。
/// </para>
///
/// <para>
/// <b>回合编号用 <c>PlayerCombatState.TurnNumber</c></b>（从 1 开始，额外回合会 +1）。
/// "本回合 + 下回合"失效 ⇒ <c>DisabledUntilTurn = TurnNumber + 2</c>：
/// 当回合与下一回合的 <c>TurnNumber</c> 都 &lt;= 它，到 <c>+2</c> 那一回合才恢复。
/// </para>
///
/// <para>
/// <b>无实体</b>：<c>IntangiblePower</c> 在<b>敌人回合结束</b>才减层
/// （<c>IntangiblePower.cs:66-72</c>），所以 1 层 = "本回合 + 敌人回合"，
/// 正是要的效果。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class TriaPrima : ArcaneRelicModel
{
    /// <summary>
    /// 触发后失效**额外**多少回合。
    ///
    /// <para>
    /// ⚠️⚠️ <b>2026-10-12 实测修正：1，不是 2。</b>
    /// 初版取 2，实测"多了一回合 —— 到无实体回合之后的<b>第二</b>回合才亮"。
    /// 期望是：<b>无实体那一回合不亮 → 下一回合也不亮 → 再下一回合就可用</b>。
    /// </para>
    ///
    /// <para>
    /// 推演（<c>TurnNumber</c> 从 1 开始，且 <c>BeforeSideTurnStart</c> 在
    /// 回合号自增<b>之后</b>才派发）：
    /// <code>
    /// 回合 N   ：三系齐 → _disabledUntilTurn = N + 1，得无实体（不亮）
    /// 回合 N+1 ：N+1 &lt;= N+1 → 仍不亮          ← 期望的"下一回合不亮"
    /// 回合 N+2 ：N+2 &gt;  N+1 → 恢复亮起          ← 期望的"再下回合可用"
    /// </code>
    /// </para>
    /// </summary>
    private const int DisabledExtraTurns = 1;

    /// <summary>本回合已命中的元素。</summary>
    private bool _hitFire;
    private bool _hitLightning;
    private bool _hitIce;

    /// <summary>失效到哪一回合（含）。<c>-1</c> = 未失效。</summary>
    private int _disabledUntilTurn = -1;

    public override RelicRarity Rarity => RelicRarity.Shop;

    // 无计数器：失效状态靠图标置灰表达，不需要数字。
    public override bool ShowCounter => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<IntangiblePower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IntangiblePower>(),
    ];

    /// <summary>
    /// 当前是否失效（**每个钩子体的第一行都要查它** —— <c>Status</c> 不会替我们挡）。
    /// </summary>
    private bool IsDisabled =>
        Owner.PlayerCombatState != null
        && _disabledUntilTurn >= 0
        && Owner.PlayerCombatState.TurnNumber <= _disabledUntilTurn;

    /// <summary>
    /// 每场战斗开始时一定亮起（用户拍板）。
    /// </summary>
    public override Task BeforeCombatStart()
    {
        ResetTurnFlags();
        _disabledUntilTurn = -1;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    /// <summary>回合开始：清本回合元素标记；到期则重新亮起。</summary>
    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        ResetTurnFlags();

        if (_disabledUntilTurn >= 0
            && Owner.PlayerCombatState != null
            && Owner.PlayerCombatState.TurnNumber > _disabledUntilTurn)
        {
            _disabledUntilTurn = -1;
            Status = RelicStatus.Normal;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    /// <summary>战斗结束清理（原版所有改 Status 的遗物都会做，如 <c>StoneCalendar.cs:108-113</c>）。</summary>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTurnFlags();
        _disabledUntilTurn = -1;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // ★ 失效守卫（Status 不会替我们挡）。
        if (IsDisabled)
        {
            return;
        }

        // ① 只认自己造成的（照抄 EnvenomPower.cs:21）。
        if (dealer != Owner.Creature)
        {
            return;
        }

        // ② 必须是一次"攻击"（Move 且非 Unpowered）。
        if (!props.IsPoweredAttack())
        {
            return;
        }

        // ③ 必须"实实在在打到"（被格挡光的不算）。
        if (result.UnblockedDamage <= 0)
        {
            return;
        }

        // ④ 按伤害自带的学派标记归类。
        switch (SpellProps.Resolve(props, cardSource))
        {
            case SpellSchool.Fire:
                _hitFire = true;
                break;
            case SpellSchool.Lightning:
                _hitLightning = true;
                break;
            case SpellSchool.Ice:
                _hitIce = true;
                break;
            default:
                return;
        }

        if (!_hitFire || !_hitLightning || !_hitIce)
        {
            return;
        }

        // ⑤ 三系齐了 → 给 1 层无实体并进入失效。
        _disabledUntilTurn = (Owner.PlayerCombatState?.TurnNumber ?? 0) + DisabledExtraTurns;

        Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        Flash();

        await PowerCmd.Apply<IntangiblePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(IntangiblePower)].BaseValue,
            Owner.Creature,
            null);
    }

    private void ResetTurnFlags()
    {
        _hitFire = false;
        _hitLightning = false;
        _hitIce = false;
    }
}
