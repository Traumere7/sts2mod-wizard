using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using Wizard.Scripts.Monsters;

namespace Wizard.Scripts.Encounters;

/// <summary>
/// 火山巨兽遭遇战 —— **只在「火山贤者」事件里出现**，不参与常规地图生成。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 2026-10-22 修：<b>绝不能把 <c>ShouldGiveRewards</c> 设成 false</b>
/// ══════════════════════════════════════════════════════════════════
/// 第一版为了让"奖励完全按 design 给"，把这里设成了 <c>false</c>，结果
/// <b>打完 BOSS 什么都不弹、直接回地图</b>。原因在
/// <c>RewardsCmd.cs:26</c>：
/// <code>
/// if (encounter != null &amp;&amp; !encounter.ShouldGiveRewards)
///     rewardsSet = new RewardsSet(player).EmptyForRoom(room);   // 空集合
/// else
///     rewardsSet = new RewardsSet(player).WithRewardsFromRoom(room);
/// </code>
/// 而事件通过 <c>EnterCombatWithoutExitingEvent</c> 传的 <c>extraRewards</c>
/// 是挂在 <c>CombatRoom.ExtraRewards</c> 上、**只由 <c>WithRewardsFromRoom</c> 合并**的
/// （<c>RewardsSet.cs:99-102</c>）。
/// ⇒ <c>ShouldGiveRewards = false</c> 会把<b>连 extraRewards 一起</b>吃掉。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 为什么房间类型是 <c>Monster</c> 而不是 <c>Elite</c>
/// ══════════════════════════════════════════════════════════════════
/// design.md 写「类型：精英（事件限定）」，但它的**奖励清单**是
/// 「100 金币 + 随机商店遗物 + 随机稀有遗物 + 一次<b>普通</b>卡牌奖励」——
/// 而 <c>RoomType.Elite</c> 的默认奖励是
/// <c>金币 + 药水? + 卡牌(精英权重) + 随机遗物</c>（<c>RewardsSet.cs:232-238</c>），
/// 会**多一件随机遗物**、且卡牌用精英稀有度权重，与设计清单冲突。
///
/// <para>
/// <c>RoomType.Monster</c> 的默认奖励只有
/// <c>金币 + 药水? + 卡牌(普通权重)</c>，
/// 正好对上设计的"一次普通卡牌奖励"；金币用
/// <see cref="CalculateGoldProportion"/> 返回 0 抑制掉
/// （Monster 分支有 <c>if (combatRoom.GoldProportion &gt; 0f)</c> 的判断，
/// <c>RewardsSet.cs:225</c>），改由事件精确给 100。
/// </para>
///
/// <para>
/// ⇒ 最终奖励 = <b>100 金币（事件给）+ 一次普通卡牌奖励（房间给）+
/// 随机商店遗物 + 随机稀有遗物（事件给）</b>，与 design.md 逐项一致。
/// 唯一多出来的是**药水掉落判定**（普通战斗本来就有，没有按遭遇战关闭的开关）。
/// </para>
/// </summary>
public class VolcanicBeastEncounter : CustomEncounterModel
{
    public VolcanicBeastEncounter()
        : base(RoomType.Monster)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<VolcanicBeast>(),
    ];

    /// <summary>
    /// ⚠️ 永远返回 false —— 本遭遇战是**事件限定**的，不进常规遭遇战池。
    /// （事件里由 <c>EnterCombatWithoutExitingEvent</c> 直接点名，不经过此筛选。）
    /// </summary>
    public override bool IsValidForAct(ActModel act) => false;

    /// <summary>
    /// 金币奖励比例设为 0 ⇒ 房间不再发默认金币，由事件精确给 100
    /// （<c>RewardsSet.cs:225</c> 只有 Monster 分支检查这个值）。
    /// </summary>
    public override float CalculateGoldProportion(CombatState combatState) => 0f;

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        // 传 null 让引擎自动分配站位（单怪场景无需布阵）。
        (ModelDb.Monster<VolcanicBeast>().ToMutable(), null),
    ];
}
