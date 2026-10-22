using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using Wizard.Scripts.Monsters;

namespace Wizard.Scripts.Encounters;

/// <summary>
/// 火山巨兽遭遇战 —— **只在「火山贤者」事件里出现**，不参与常规地图生成。
///
/// <para>
/// ⚠️ <b><see cref="IsValidForAct"/> 返回 <c>false</c> 是刻意的</b>：
/// 它让这个遭遇战<b>不会被塞进第二阶段的普通遭遇战池</b>
/// （<c>docs/design.md</c>：类型「精英（事件限定）」）。
/// 事件里用 <c>EnterCombatWithoutExitingEvent&lt;VolcanicBeastEncounter&gt;</c>
/// 直接点名进入，**不经过** <c>IsValidForAct</c> 的筛选，所以这样写不影响事件。
/// </para>
///
/// <para>
/// ⚠️ <b><see cref="ShouldGiveRewards"/> = <c>false</c></b>：
/// design.md 对这场战斗的奖励是**逐项写死**的
/// （100 金币 + 随机商店遗物 + 随机稀有遗物 + 一次普通卡牌奖励），
/// 不是精英战的默认奖励。所以关掉遭遇战自带奖励，
/// 由事件通过 <c>extraRewards</c> 精确给出 ——
/// 否则会额外多出一份精英奖励。
/// </para>
///
/// <para>
/// <b>房间类型用 <see cref="RoomType.Elite"/></b>：与 design.md 的「类型：精英」一致，
/// 影响战斗 UI（精英血条装饰）与背景，不参与奖励计算（奖励已关）。
/// </para>
///
/// <para>
/// <b>没有自定义场景</b>：<c>Slots</c> 为空、<c>CustomScenePath</c> 为 null 时，
/// 单怪会自动被放到默认位置 —— 占位阶段不需要额外做遭遇战布阵场景。
/// </para>
/// </summary>
public class VolcanicBeastEncounter : CustomEncounterModel
{
    public VolcanicBeastEncounter()
        : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<VolcanicBeast>(),
    ];

    /// <summary>
    /// ⚠️ 永远返回 false —— 本遭遇战是**事件限定**的，不进常规遭遇战池。
    /// </summary>
    public override bool IsValidForAct(ActModel act) => false;

    /// <summary>奖励全部由「火山贤者」事件精确给出，这里关掉默认奖励。</summary>
    public override bool ShouldGiveRewards => false;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        // 传 null 让引擎自动分配站位（单怪场景无需布阵）。
        (ModelDb.Monster<VolcanicBeast>().ToMutable(), null),
    ];
}
