using System.Collections.Generic;
using Godot;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using Wizard.Scripts.Monsters;

namespace Wizard.Scripts.Encounters;

/// <summary>
/// 火山巨兽遭遇战 —— **只在「火山贤者」事件里出现**，不参与常规地图生成。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ① 绝不会在常规战斗节点遭遇（2026-10-22 核实）
/// ══════════════════════════════════════════════════════════════════
/// BaseLib 把自定义遭遇战塞进某一幕的遭遇战池时，
/// 唯一判据就是 <c>IsValidForAct</c>（<c>AddActContent.cs:49-52</c>）：
/// <code>
/// foreach (CustomEncounterModel encounter in CustomContentDictionary.CustomEncounters)
///     if (!origResult.Any(e => e.Id.Equals(encounter.Id)) &amp;&amp; encounter.IsValidForAct(__instance))
///         yield return encounter;
/// </code>
/// 这里恒定返回 <c>false</c> ⇒ **任何一幕的池子里都不会有它**。
/// 而 <c>VolcanicBeast</c> 只被本遭遇战的 <see cref="GenerateMonsters"/> 引用，
/// <c>CustomMonsterModel</c> 也不会把自己登记进任何怪物池。
/// ⇒ 唯一入口就是事件里的 <c>EnterCombatWithoutExitingEvent&lt;VolcanicBeastEncounter&gt;</c>。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ② 房间类型必须是 <c>Elite</c>：音乐与它绑定
/// ══════════════════════════════════════════════════════════════════
/// 战斗音乐不是"一首曲子"，而是<b>本幕曲目 + 一个 Progress 参数</b>：
/// <c>NRunMusicController.UpdateTrack()</c> 读
/// <c>GetTrack(_runState.CurrentRoom.RoomType)</c>，
/// 再 <c>update_global_parameter("Progress", (float)track)</c>；
/// 而 <c>RoomType.Elite</c> / <c>Boss</c> ⇒ <c>MusicProgressTrack.Elite</c>（<c>:226-229</c>）。
///
/// <para>
/// <c>NonInteractiveMode</c> 之外**没有**公开 API 能单独改这个全局参数
/// （<c>UpdateMusicParameter</c> 走的是另一个 proxy 通道 <c>update_music_parameter</c>；
/// <c>CustomBgm</c> 只会换整首曲子、丢掉本幕的 Progress 变体）。
/// ⇒ 想要"第 2 幕的精英战斗音乐"，<b>只能把房间类型设成 <c>Elite</c></b>。
/// </para>
///
/// <para>
/// ⚠️ 代价：<c>RewardsSet.GenerateRewardsFor</c> 的 Elite 分支
/// （<c>RewardsSet.cs:232-238</c>）会**无条件**多给一件随机遗物、
/// 且卡牌用精英稀有度权重。所以奖励改成：
/// <list type="bullet">
///   <item>100 金币 —— 由房间发（<see cref="MinGoldReward"/>=<see cref="MaxGoldReward"/>=100）</item>
///   <item>一次卡牌奖励 —— 由房间发（权重是精英档）</item>
///   <item>一件随机遗物 —— 由房间发（**任意稀有度**，不保证稀有）</item>
///   <item>一件随机商店遗物（巫师且未拥有「三原素」时固定给三原素）—— 事件额外给</item>
/// </list>
/// 合计 **100 金币 + 2 件遗物 + 1 次卡牌**，件数与 design.md 一致；
/// 与 design 的两点细微差异已在总结里说明。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ③ 自定义战斗背景（本 mod 自带，不再是"借用原版目录"的实验）
/// ══════════════════════════════════════════════════════════════════
/// 机制（<c>EncounterModel.cs:288-308</c>）：
/// <code>
/// if (HasCustomBackground) _backgroundAssets = CreateBackgroundAssetsForCustom(rng);
/// else                     _backgroundAssets = parentAct.GenerateBackgroundAssets(rng);
/// // CreateBackgroundAssetsForCustom: new BackgroundAssets(Id.Entry.ToLowerInvariant(), rng)
/// </code>
/// 而 <c>BackgroundAssets(string title, Rng rng)</c> 会去读
/// <c>res://scenes/backgrounds/{title}/layers/</c> 目录（按 <c>_bg_</c> 分组随机选层 + <c>_fg_</c> 前景）
/// 与 <c>res://scenes/backgrounds/{title}/{title}_background.tscn</c>。
///
/// <para>
/// ⇒ BaseLib 的 <see cref="CustomEncounterBackground"/> 返回
/// <c>new BackgroundAssets("volcanic_beast", rng)</c>，指向<b>本 mod 自带</b>的：
/// <code>
/// src/scenes/backgrounds/volcanic_beast/volcanic_beast_background.tscn   ← 根 Control + NCombatBackground
/// src/scenes/backgrounds/volcanic_beast/layers/volcanic_beast_bg_00_a.tscn ← 居中的 TextureRect
/// src/Wizard/images/backgrounds/volcanic_beast/volcanic_beast_bg_00_a.png  ← 用户提供的 2560×1200 贴图
/// </code>
/// ⚠️ 目录名必须与 <c>BackgroundAssets</c> 的第一个参数一致（它会去拼
/// <c>res://scenes/backgrounds/{title}/…</c>），**不是** <c>res://Wizard/…</c>。
/// </para>
/// <para>
/// ⚠️ 目录不存在时 <c>BackgroundAssets</c> 的构造函数会**抛异常**，
/// 所以先用 <c>DirAccess.DirExistsAbsolute</c> 挡一层，缺了就返回 <c>null</c>
/// （= 回落本幕默认背景），不让整场战斗崩掉。
/// </para>
/// </summary>
public class VolcanicBeastEncounter : CustomEncounterModel
{
    /// <summary>
    /// 自定义背景的目录名 —— 对应本 mod 自带的
    /// <c>res://scenes/backgrounds/volcanic_beast/</c>（贴图来自用户提供的
    /// <c>art/VolcanicBackground.png</c>，2560×1200）。
    /// </summary>
    private const string BackgroundTitle = "volcanic_beast";

    /// <summary>design.md：固定 100 金币。</summary>
    private const int GoldReward = 100;

    public VolcanicBeastEncounter()
        : base(RoomType.Elite)
    {
    }

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
    [
        ModelDb.Monster<VolcanicBeast>(),
    ];

    /// <summary>
    /// ⚠️ 永远返回 false —— 本遭遇战是**事件限定**的，不进任何常规遭遇战池。
    /// （事件里由 <c>EnterCombatWithoutExitingEvent</c> 直接点名，不经过此筛选。）
    /// </summary>
    public override bool IsValidForAct(ActModel act) => false;

    /// <summary>design.md：100 金币（由房间的 Elite 奖励发，避免事件再给一次重复）。</summary>
    public override int MinGoldReward => GoldReward;

    public override int MaxGoldReward => GoldReward;

    /// <summary>
    /// 换成「瀑布巨兽」的专属战斗背景（实验）。
    /// 目录不存在则返回 null ⇒ 回落本幕默认背景。
    /// </summary>
    public override BackgroundAssets? CustomEncounterBackground(ActModel parentAct, Rng rng)
    {
        string layersDir = $"res://scenes/backgrounds/{BackgroundTitle}/layers";
        if (!DirAccess.DirExistsAbsolute(layersDir))
        {
            Entry.Logger.Warn("未找到背景目录 {layersDir}，回落本幕默认背景。");
            return null;
        }

        Entry.Logger.Info($"火山巨兽使用自定义战斗背景：{BackgroundTitle}");
        return new BackgroundAssets(BackgroundTitle, rng);
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        // 传 null 让引擎自动分配站位（单怪场景无需布阵）。
        (ModelDb.Monster<VolcanicBeast>().ToMutable(), null),
    ];
}
