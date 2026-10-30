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
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;

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
/// <b>选项上直接写出要给的遗物名字</b>（不再是"一件随机遗物"），
/// 拿到后展示后续文案并结束事件。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// ⚠️ 抽取顺序：<b>角色专属池优先，抽空了才回退通用池</b>
/// ══════════════════════════════════════════════════════════════════
/// <b>不能用 <c>RelicFactory.PullNextRelicFromFront</c> 做这件事</b> ——
/// 我第一版就是这么写的，结果玩家一件专属遗物都没有却总是拿到通用遗物。
/// 原因：它走的 <c>RelicGrabBag.Populate</c>（<c>RelicGrabBag.cs:69-90</c>）是
/// <code>
/// list = SharedRelicPool ... ToList();
/// list.AddRange(character.RelicPool ...);
/// list.RemoveAll(r =&gt; !_rarities.Contains(r.Rarity));
/// ... 分桶后 UnstableShuffle(rng)     // ★ 打乱
/// </code>
/// —— 两个池子是**混合后打乱**的，"从队首取"取到谁纯看运气，
/// <b>根本不存在"专属池优先"</b>。
/// </para>
///
/// <para>
/// 正确做法是分两步查，本类的 <see cref="RollRelic"/>：
/// <list type="number">
///   <item><c>Owner.Character.RelicPool.GetUnlockedRelics(...)</c>
///         里筛该稀有度 + 未拥有 + <c>IsAllowed(runState)</c>，非空则 <c>Rng.NextItem</c>；</item>
///   <item>否则 <c>ModelDb.RelicPool&lt;SharedRelicPool&gt;()</c> 同样筛一遍；</item>
///   <item>都没有 ⇒ <c>RelicFactory.FallbackRelic</c>（<c>Circlet</c>，原版兜底）。</item>
/// </list>
/// 随机源用 <c>EventModel.Rng</c>（每个事件独立、按玩家槽位播种，<c>EventModel.cs:134-138</c>）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 把遗物名字写进选项文案：<c>StringVar</c> + <c>{RelicFront}</c>
/// ══════════════════════════════════════════════════════════════════
/// 原版「滑脚木桥」就是这么做的（<c>SlipperyBridge.cs:78,128-129</c>：
/// <c>new StringVar("RandomCard")</c> + <c>StringValue = ...</c>）。
/// 关键在于 **<c>DynamicVars</c> 是 UI 在渲染按钮时才注入的**：
/// <c>NEventOptionButton.cs:285-286</c>
/// <code>
/// Event.DynamicVars.AddTo(Option.Description);
/// Event.DynamicVars.AddTo(Option.Title);
/// </code>
/// ⇒ 只要在 <see cref="GenerateInitialOptions"/> 里把 <c>StringValue</c> 设好，
/// 选项文案里的 <c>{RelicFront}</c> 就会被替换成遗物名。
/// ⚠️ <c>RelicModel.Title</c> 是 <c>LocString</c>（不是 string），所以取
/// <c>GetFormattedText()</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>必须 <c>.ToMutable()</c></b>：<c>RelicCmd.Obtain</c> 第一行就
/// <c>AssertMutable()</c>（<c>RelicCmd.cs:308</c>），传原型抛 <c>CanonicalModelException</c>；
/// <c>EventOption.WithRelic</c> 同样要求可变实例（<c>EventOption.cs:167-172</c>）。
/// 顺便：<c>ToMutable()</c> 出来的实例按 <c>Id</c> 比较，所以
/// <c>RelicCmd.Obtain</c> 内部的 <c>RelicGrabBag.Remove</c> 仍能正确把它从袋子里剔掉
/// （<c>RelicGrabBag.Remove</c> 用 <c>r.Id == relic.Id</c> 比对）。
/// </para>
/// </summary>
public sealed class LevelSecret : CustomEventModel
{
    /// <summary>本事件的本地化键前缀（= <c>ModelId.Entry</c>）。</summary>
    private const string Key = "WIZARD-LEVEL_SECRET";

    // ── 选项文案里用的三个遗物名变量（UI 渲染时才注入 DynamicVars）──
    private const string FrontVar = "RelicFront";
    private const string InnerVar = "RelicInner";
    private const string DeepVar = "RelicDeep";

    // ── 三个选项各自要给的遗物（生成选项时定好，玩家选哪个给哪个）──
    private RelicModel? _front;

    private RelicModel? _inner;

    private RelicModel? _deep;

    public override string? CustomInitialPortraitPath => "res://Wizard/images/events/level_secret.png";

    // 出现阶段「任意」、多人合作「否」都是 EventModel 的默认值，故不重写
    // IsAllowed / IsShared。详见类注释。

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar(FrontVar),
        new StringVar(InnerVar),
        new StringVar(DeepVar),
    ];

    // ══════════════════════════════════════════════════════════════════
    // 选项
    // ══════════════════════════════════════════════════════════════════
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        Player? player = Owner;

        // ⚠️ 绝不能返回空列表 —— 那会让事件界面一个按钮都没有，等于软锁
        //    （EventModel.ReplaceNullOptions 只防 null 选项，不防"零选项"）。
        // Owner 在 EventModel.BeginEvent 里必先赋值，这条分支实际不可达；
        // 但降级成一个锁定选项比零选项安全得多（同「尖塔规则怪谈」的处理）。
        if (player == null)
        {
            return [LockedOption("LOCKED")];
        }

        _front = RollRelic(player, RelicRarity.Common);
        _inner = RollRelic(player, RelicRarity.Uncommon);
        _deep = RollRelic(player, RelicRarity.Rare);

        SetRelicName(FrontVar, _front);
        SetRelicName(InnerVar, _inner);
        SetRelicName(DeepVar, _deep);

        return
        [
            Option(TakeFront, FrontTitle, FrontDescription, HoverTipFactory.FromRelic(_front).ToArray())
                .WithRelic(_front),
            Option(TakeInner, InnerTitle, InnerDescription, HoverTipFactory.FromRelic(_inner).ToArray())
                .WithRelic(_inner),
            Option(TakeDeep, DeepTitle, DeepDescription, HoverTipFactory.FromRelic(_deep).ToArray())
                .WithRelic(_deep),
        ];
    }

    // ══════════════════════════════════════════════════════════════════
    // 三个选项
    // ══════════════════════════════════════════════════════════════════
    private Task TakeFront() => TakeRelic(_front);

    private Task TakeInner() => TakeRelic(_inner);

    private Task TakeDeep() => TakeRelic(_deep);

    /// <summary>给出该选项对应的那件遗物，然后展示后续文案并结束事件。</summary>
    private async Task TakeRelic(RelicModel? relic)
    {
        Player? player = Owner;
        if (player == null || relic == null)
        {
            return;
        }

        // relic 在 GenerateInitialOptions 里已经 ToMutable() 过，可直接交给 Obtain。
        await RelicCmd.Obtain(relic, player);

        // 后续文案（三个选项共用），随后事件结束。
        // SetEventFinished 内部 = SetEventState(desc, 空选项)（EventModel.cs:478-481）。
        SetEventFinished(L10NLookup($"{Key}.pages.RESULT.description"));
    }

    // ══════════════════════════════════════════════════════════════════
    // 抽遗物：角色专属池优先 ⇒ 通用池 ⇒ Circlet
    // ══════════════════════════════════════════════════════════════════
    private RelicModel RollRelic(Player player, RelicRarity rarity)
    {
        // ① 角色专属遗物池
        if (PickFrom(player.Character.RelicPool.GetUnlockedRelics(player.UnlockState),
                     player, rarity) is { } fromCharacter)
        {
            return fromCharacter;
        }

        // ② 专属池抽空了 ⇒ 回退通用池
        if (PickFrom(ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(player.UnlockState),
                     player, rarity) is { } fromShared)
        {
            return fromShared;
        }

        // ③ 该稀有度已全拿完 ⇒ 原版兜底（Circlet）
        return RelicFactory.FallbackRelic.ToMutable();
    }

    /// <summary>
    /// 从给定卡池里筛出「该稀有度 + 可给予」的遗物，随机取一件并转成可变实例。
    /// 池子里没有合适的就返回 <c>null</c>（由 <see cref="RollRelic"/> 决定下一步）。
    /// </summary>
    private RelicModel? PickFrom(IEnumerable<RelicModel> pool, Player player, RelicRarity rarity)
    {
        List<RelicModel> candidates = pool
            .Where(r => r.Rarity == rarity && CanOffer(player, r))
            .ToList();

        // Rng 必非 null：BeginEvent 先赋值 Owner/Rng（EventModel.cs:233-234），
        // 之后才 SetInitialEventState → GenerateInitialOptions（:251,267）。
        return candidates.Count == 0 ? null : Rng!.NextItem(candidates)?.ToMutable();
    }

    /// <summary>该遗物能不能作为选项出现：本局允许 + 玩家还没有。</summary>
    private static bool CanOffer(Player player, RelicModel relic)
    {
        return relic.IsAllowed(player.RunState)
            && !player.Relics.Any(owned => owned.Id == relic.Id);
    }

    /// <summary>把遗物名写进对应的 <c>StringVar</c>（渲染时由 DynamicVars 注入文案）。</summary>
    private void SetRelicName(string varName, RelicModel relic)
    {
        ((StringVar)DynamicVars[varName]).StringValue = relic.Title.GetFormattedText();
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
