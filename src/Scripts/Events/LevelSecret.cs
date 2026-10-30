using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Events;

/// <summary>
/// 秘仪之间 / LevelSecret —— 任意阶段事件，单人专属。
///
/// <para>
/// design.md 的口径：
/// <code>
/// 出现阶段：任意阶段       ⇒ 不重写 IsAllowed（默认 true，EventModel.cs:324-327）
/// 多人合作：否             ⇒ 不重写 IsShared（默认 false，EventModel.cs:81）
/// 触发条件：无
/// </code>
/// 三个选项各给一件遗物，稀有度依次为 普通 / 罕见 / 稀有；
/// 拿到后展示后续文案（「你满载而归…」）并结束事件。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「随机一件玩家没有的、该稀有度的遗物」= <c>RelicFactory</c> + 过滤器
/// ══════════════════════════════════════════════════════════════════
/// <c>RelicFactory.PullNextRelicFromFront(player, rarity, filter)</c>
/// （<c>RelicFactory.cs:45-50</c>）内部走
/// <c>player.RelicGrabBag.PullFromFront(rarity, filter, runState)</c>，
/// 而这个袋子在 <c>RelicGrabBag.Populate</c>（<c>:69-90</c>）里是这样装的：
/// <code>
/// var list = ModelDb.RelicPool&lt;SharedRelicPool&gt;().GetUnlockedRelics(player.UnlockState).ToList();
/// list.AddRange(player.Character.RelicPool.GetUnlockedRelics(player.UnlockState));
/// list.RemoveAll(r =&gt; !_rarities.Contains(r.Rarity));   // 只留 Common/Uncommon/Rare/Shop
/// ... 按稀有度分桶后 UnstableShuffle(rng)
/// </code>
/// ⇒ <b>天然就是"角色专属池 + 通用池"的合并随机</b>，正好是 design 说明里
/// 「先从角色专属池选；全都有了就退到通用池」的语义
/// （专属池空了，袋子里自然只剩通用的）。
///
/// <para>
/// 而「玩家没有的」这层由我们自己的过滤器补上：
/// <code>
/// r =&gt; !player.Relics.Any(owned =&gt; owned.Id == r.Id)
/// </code>
/// ⚠️ 用 <c>Id</c> 比而不是 <c>Relics.Contains(r)</c>：<c>Player.Relics</c> 里是
/// <b>可变实例</b>，而工厂给我们的是<b>原型</b>，引用相等永远为 false。
/// </para>
///
/// <para>
/// 该稀有度的遗物全拿完时 <c>PullFromFront</c> 返回 <c>null</c>，
/// 工厂兜底成 <c>FallbackRelic</c>（<c>Circlet</c>，"遗物已集齐"的占位遗物）——
/// 这是原版所有遗物奖励的标准行为，照旧。
/// </para>
/// </para>
///
/// <para>
/// ⚠️ <b>必须 <c>.ToMutable()</c> 再交给 <c>RelicCmd.Obtain</c></b>：
/// <c>Obtain</c> 第一行就 <c>relic.AssertMutable()</c>（<c>RelicCmd.cs:308</c>），
/// 传原型会抛 <c>CanonicalModelException</c>。
/// （同「尖塔规则怪谈」<c>RulesHorror.cs:436</c> 的写法。）
/// </para>
///
/// <para>
/// ⚠️ <b>选项文案必须用 <c>Option(onChosen, LocString title, LocString description)</c> 重载</b>：
/// 只传一个字符串的重载会把参数当 <b>pageKey</b>（<c>CustomEventModel.cs:41</c>），
/// 运行时报 "Missing localization key"。这是本项目踩过的坑。
/// </para>
/// </summary>
public sealed class LevelSecret : CustomEventModel
{
    /// <summary>本事件的本地化键前缀（= <c>ModelId.Entry</c>）。</summary>
    private const string Key = "WIZARD-LEVEL_SECRET";

    public override string? CustomInitialPortraitPath => "res://Wizard/images/events/level_secret.png";

    // 出现阶段「任意」、多人合作「否」都是 EventModel 的默认值，故不重写
    // IsAllowed / IsShared。详见类注释。

    // ══════════════════════════════════════════════════════════════════
    // 选项
    // ══════════════════════════════════════════════════════════════════
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            Option(TakeFront, FrontTitle, FrontDescription),
            Option(TakeInner, InnerTitle, InnerDescription),
            Option(TakeDeep, DeepTitle, DeepDescription),
        ];
    }

    // ══════════════════════════════════════════════════════════════════
    // 三个选项：分别给 普通 / 罕见 / 稀有 遗物
    // ══════════════════════════════════════════════════════════════════
    private Task TakeFront() => TakeRelic(RelicRarity.Common);

    private Task TakeInner() => TakeRelic(RelicRarity.Uncommon);

    private Task TakeDeep() => TakeRelic(RelicRarity.Rare);

    /// <summary>拿一件该稀有度的、玩家没有的遗物，然后展示后续文案并结束事件。</summary>
    private async Task TakeRelic(RelicRarity rarity)
    {
        Player? player = Owner;
        if (player == null)
        {
            return;
        }

        // 只从"玩家还没有的"里抽（袋子本身已合并了角色专属池与通用池）。
        RelicModel relic = RelicFactory.PullNextRelicFromFront(
            player,
            rarity,
            candidate => !player.Relics.Any(owned => owned.Id == candidate.Id));

        // ⚠️ 必须 ToMutable()：RelicCmd.Obtain 会 AssertMutable。
        await RelicCmd.Obtain(relic.ToMutable(), player);

        // 后续文案（三个选项共用），随后事件结束。
        SetEventFinished(L10NLookup($"{Key}.pages.RESULT.description"));
    }

    // ══════════════════════════════════════════════════════════════════
    // 文案键
    // ══════════════════════════════════════════════════════════════════
    private static LocString Opt(string name, string field) =>
        new("events", $"{Key}.pages.INITIAL.options.{name}.{field}");

    private static LocString FrontTitle => Opt("FRONT", "title");

    private static LocString FrontDescription => Opt("FRONT", "description");

    private static LocString InnerTitle => Opt("INNER", "title");

    private static LocString InnerDescription => Opt("INNER", "description");

    private static LocString DeepTitle => Opt("DEEP", "title");

    private static LocString DeepDescription => Opt("DEEP", "description");
}
