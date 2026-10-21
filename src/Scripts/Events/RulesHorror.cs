using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Localization;

namespace Wizard.Scripts.Events;

/// <summary>
/// 尖塔规则怪谈 / Rules Horror —— 第三阶段事件，单人专属。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 触发条件（design.md「事件稿」原文，三个分支是「或」）
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item>卡组 &lt; 20 张 <b>且</b> 存在至少 1 张可移除的牌；</item>
///   <item>卡组在 20–35 之间（<b>闭区间</b>）<b>且</b> 存在至少 1 张可升级的牌；</item>
///   <item>卡组 &gt; 35 张。</item>
/// </list>
/// 注意三段的边界：<c>&lt;20</c> / <c>[20,35]</c> / <c>&gt;35</c> ——
/// 恰好 20 和恰好 35 都归<b>第二段</b>（闭区间）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「锁定」选项的样式必须与原版一致
/// ══════════════════════════════════════════════════════════════════
/// 原版 <c>NEventOptionButton._Ready</c>（<c>NEventOptionButton.cs:290-298</c>）：
/// <code>
/// string value = (Option.IsLocked ? "red" : "gold");
/// _label.Text = $"[{value}][b]{formattedText}[/b][/{value}]\n{formattedText2}";
/// </code>
/// ⇒ 锁定项渲染成 <b>红标题 + 下方描述</b>，再由
/// <c>SetVisuallyLocked</c>（<c>:324-330</c>）整体降透明度并压暗 HSV。
///
/// <b>所以"已锁定 + 锁定理由"靠的是「标题填『已锁定』、描述填理由」</b>，
/// 不是靠某个特殊 API。原版每个锁定项都有一对独立的
/// <c>…_LOCKED.title</c>（= <c>已锁定</c>）/ <c>…_LOCKED.description</c>（= 理由）。
/// 参考 <c>GRAVE_OF_THE_FORGOTTEN.pages.INITIAL.options.CONFRONT_LOCKED</c>：
/// <code>
/// title       = 已锁定
/// description = 你没有可被[purple]附魔[/purple]的带有[gold]消耗[/gold]的卡牌。
/// </code>
///
/// <para>
/// ⇒ 本事件用 BaseLib 的 <c>LockedOption(locKey)</c>：
/// 它自己拼 <c>{Entry}.pages.INITIAL.options.{locKey}</c> 并
/// 以 <c>(Func&lt;Task&gt;)null</c> 构造 ⇒ <c>IsLocked = true</c>，
/// 正好对上原版那条渲染路径。
/// </para>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 只出现在第三阶段
/// ══════════════════════════════════════════════════════════════════
/// 用 <c>IsAllowed</c> 判 <c>runState.CurrentActIndex</c>：
/// <b>Act 1 = 0，Act 2 = 1，Act 3 = 2</b>（<c>IRunState.cs:29</c> 注释明确写了），
/// 且 <c>Glory.Index => 2</c> 就是第三阶段的 Act。
/// </para>
///
/// <para>
/// ⚠️ <b>多人合作：否</b> —— 不重写 <c>IsShared</c>（默认 <c>false</c>）。
/// </para>
/// </summary>
public sealed class RulesHorror : CustomEventModel
{
    // ── 阈值（design.md 原文：<20 / 20-35 / >35）──
    private const int LeanMax = 19;      // 段①：<= 19
    private const int BalancedMin = 20;  // 段②：[20, 35]
    private const int BalancedMax = 35;
    private const int AbundantMin = 36;  // 段③：>= 36

    // ── 各选项的数值 ──
    /// <summary>选项1「我追求精简」的生命代价。</summary>
    private const int LeanHpLoss = 6;

    /// <summary>选项2「我信奉均衡」的升级张数。</summary>
    private const int UpgradeCount = 2;

    /// <summary>选项3「我崇尚丰盈」的回复量与遗物个数。</summary>
    private const int AbundantHeal = 20;

    private const int RelicCount = 3;

    /// <summary>选项4「离开」的降级张数。</summary>
    private const int DowngradeCount = 2;

    /// <summary>本事件的本地化键前缀（= <c>ModelId.Entry</c>）。</summary>
    private const string Key = "WIZARD-RULES_HORROR";

    /// <summary>
    /// 立绘：用户提供的 <b>2560 × 1200</b>。
    ///
    /// <para>
    /// ⚠️ 原版事件立绘的主流尺寸是 <b>3440 × 1616</b>（58 张里 56 张），
    /// 但 <c>EventModel.CreateInitialPortrait</c> 只是
    /// <c>PreloadManager.Cache.GetTexture2D(InitialPortraitPath)</c>，
    /// <b>不校验尺寸</b>，由事件界面按容器等比缩放。
    /// 本图 2560×1200（比例 2.133）与原版 3440×1616（比例 2.129）几乎一致，
    /// 所以不会变形或裁掉关键内容。
    /// </para>
    /// </summary>
    public override string? CustomInitialPortraitPath => "res://Wizard/images/events/rules_horror.png";

    // ══════════════════════════════════════════════════════════════════
    // 数值变量（选项文案里用 {HpLoss} / {Heal} 引用）
    // ══════════════════════════════════════════════════════════════════
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(LeanHpLoss),
        new HealVar(AbundantHeal),
    ];

    // ══════════════════════════════════════════════════════════════════
    // 出现条件：第三阶段 + 三条触发条件之一
    // ══════════════════════════════════════════════════════════════════
    public override bool IsAllowed(IRunState runState)
    {
        // Act 1 = 0 / Act 2 = 1 / Act 3 = 2。
        if (runState.CurrentActIndex != 2)
        {
            return false;
        }

        // ⚠️ 多人时逐个玩家判定（原版 RelicTrader 用 .All(...)，语义是"人人都满足"）。
        //    本事件设计为单人，用 All 只是保守取交集；单人局等价于判自己。
        return runState.Players.All(player =>
        {
            int deckSize = PileType.Deck.GetPile(player).Cards.Count;
            return TriggersFor(player, deckSize);
        });
    }

    /// <summary>
    /// 三条触发条件（「或」关系）。
    ///
    /// <para>
    /// ⚠️ 第①条和第②条除了张数，还各自要求"确实存在可操作对象" ——
    /// 否则会出现"事件出现了，但三个选项全锁着"的死局。
    /// </para>
    /// </summary>
    private static bool TriggersFor(Player player, int deckSize)
    {
        // ① < 20 且存在可移除的牌
        if (deckSize <= LeanMax)
        {
            return PileType.Deck.GetPile(player).Cards.Any(c => c.IsRemovable);
        }

        // ② [20, 35] 且存在可升级的牌
        if (deckSize <= BalancedMax)
        {
            return PileType.Deck.GetPile(player).Cards.Any(c => c.IsUpgradable);
        }

        // ③ > 35（第三条无附加要求）
        return deckSize >= AbundantMin;
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项生成
    // ══════════════════════════════════════════════════════════════════
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        Player? player = Owner;

        // ⚠️ <b>绝不能返回空列表</b> —— 那会让事件界面一个按钮都没有，等于**软锁**。
        // （`EventModel.ReplaceNullOptions`（`EventModel.cs:287-301`）只防 null 选项，
        //   <b>不防"零选项"</b>。）
        //
        // 实际上 `Owner` 在 `EventModel.BeginEvent:233` 必被赋值，
        // 且早于 `SetInitialEventState` → 本方法，所以这条分支不可达；
        // 但万一日后调用路径变了，降级成"三个锁定 + 离开可用"比零选项安全得多。
        if (player == null)
        {
            return
            [
                LockedOption("LEAN_LOCKED"),
                LockedOption("BALANCED_LOCKED"),
                LockedOption("ABUNDANT_LOCKED"),
                Option(ChooseLeave, LeaveTitle, LeaveDescription),
            ];
        }

        int deckSize = PileType.Deck.GetPile(player).Cards.Count;
        bool canRemove = PileType.Deck.GetPile(player).Cards.Any(c => c.IsRemovable);
        bool canUpgrade = PileType.Deck.GetPile(player).Cards.Any(c => c.IsUpgradable);

        // ⚠️ 锁定判据用「与本选项**对应档位**一致」，不是"任意档位满足"。
        //    例：卡组 18 张时，选项②③ 都必须锁 —— 否则玩家能从几个里随便挑收益最高的。
        bool lean = deckSize <= LeanMax && canRemove;
        bool balanced = deckSize >= BalancedMin && deckSize <= BalancedMax && canUpgrade;
        bool abundant = deckSize >= AbundantMin;

        return
        [
            // 选项 1：我追求精简 —— 失去 6 点生命，移除 1 张牌
            lean
                ? Option(ChooseLean, LeanTitle, LeanDescription)
                    .ThatDoesDamage(DynamicVars.HpLoss.BaseValue)
                : LockedOption("LEAN_LOCKED"),

            // 选项 2：我信奉均衡 —— 升级 2 张牌
            balanced
                ? Option(ChooseBalanced, BalancedTitle, BalancedDescription)
                : LockedOption("BALANCED_LOCKED"),

            // 选项 3：我崇尚丰盈 —— 回复 20 点生命，获得 3 个随机遗物
            abundant
                ? Option(ChooseAbundant, AbundantTitle, AbundantDescription)
                : LockedOption("ABUNDANT_LOCKED"),

            // 选项 4：离开 —— 随机降级 2 张牌
            //
            // ⚠️ **永不锁定**（design.md 没给锁定条件）：
            //    这是玩家的"退出"路径，任何情况下都必须能点，
            //    否则条件不满足时会卡在事件里出不去。
            Option(ChooseLeave, LeaveTitle, LeaveDescription),
        ];
    }

    // ══════════════════════════════════════════════════════════════════
    // 选项文案：必须用 LocString 重载
    // ══════════════════════════════════════════════════════════════════
    //
    // ⚠️⚠️ <b>这里有个极容易踩的坑</b>：BaseLib 有三个 `Option` 重载
    // （<c>CustomEventModel.cs:35-102</c>）：
    //
    // <code>
    // Option(Func<Task>? onChosen, LocString title, LocString description, params IHoverTip[] tips)
    // Option(Func<Task>? onChosen, string pageKey = "INITIAL", params IHoverTip[] tips)
    // Option(Func<Task>? onChosen, IEnumerable<IHoverTip> tips, string pageKey = "INITIAL")
    // </code>
    //
    // <b>写成 <c>Option(SomeMethod, "某个完整键字符串")</c> 会绑定到<b>第二个</b>重载，
    // 那个 string 会被当成 <b>pageKey</b>，再拼成
    // <c>{Entry}.pages.{该字符串}.options.{方法名 Slugify}</c> —— 完全错位，
    // 而且只会在运行时打一条 "Missing localization key" 警告、界面显示空键名。</b>
    // 编译器不会报错（因为确实有匹配的重载）。
    //
    // ⇒ 所以<b>必须显式给 Title/Description 两个 LocString</b>，走第一个重载。
    // 下面 4 个属性就是这么用的。

    private static LocString LeanTitle => Opt("LEAN", "title");

    private static LocString LeanDescription => Opt("LEAN", "description");

    private static LocString BalancedTitle => Opt("BALANCED", "title");

    private static LocString BalancedDescription => Opt("BALANCED", "description");

    private static LocString AbundantTitle => Opt("ABUNDANT", "title");

    private static LocString AbundantDescription => Opt("ABUNDANT", "description");

    private static LocString LeaveTitle => Opt("LEAVE", "title");

    private static LocString LeaveDescription => Opt("LEAVE", "description");

    /// <summary>拼出 <c>{Entry}.pages.INITIAL.options.{name}.{field}</c> 的 LocString。</summary>
    private static LocString Opt(string name, string field) =>
        new("events", $"{Key}.pages.INITIAL.options.{name}.{field}");

    // ══════════════════════════════════════════════════════════════════
    // 四个选项的效果
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 选项 1：失去 6 点生命，然后从牌组移除 1 张牌。
    ///
    /// <para>
    /// 生命损失照抄原版 <c>Bloodletting</c>（<c>Bloodletting.cs:40</c>）：
    /// <c>CreatureCmd.Damage(... HpLoss ... ValueProp.Unblockable | ValueProp.Unpowered ...)</c>
    /// —— 用<b>不可格挡 + 不受加成</b>的真实生命损失，
    /// 而不是"受到伤害"（否则会被力量/易伤等影响，与"失去生命"语义不符）。
    /// </para>
    ///
    /// <para>
    /// 移除牌照抄原版「光与暗的门扉」<c>DoorsOfLightAndDark.Dark()</c>
    /// （<c>DoorsOfLightAndDark.cs:33-37</c>）。
    /// </para>
    /// </summary>
    private async Task ChooseLean()
    {
        Player? player = Owner;
        if (player == null)
        {
            return;
        }

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

        // ⚠️⚠️ <b>扣血致死后必须立刻收手，不能再弹选牌界面。</b>
        //
        // <para>
        // 原因：<c>CreatureCmd.Damage</c> 末尾会 <c>await Kill(killedCreatures)</c>
        // （<c>CreatureCmd.cs:430</c>），而 <c>Kill</c> 在"所有玩家都死了"时会
        // <b>同步</b>执行 <c>RunManager.Instance.OnEnded(...)</c> +
        // <c>NRun.Instance.ShowGameOverScreen(...)</c>（<c>CreatureCmd.cs:475-489</c>）。
        // 此时 run 已经结束，再调 <c>CardSelectCmd.FromDeckForRemoval</c> 就会
        // 把选牌弹窗盖在结束画面上 —— 轻则视觉错乱，重则流程卡死。
        // </para>
        //
        // <para>
        // 单人**允许**选致死项（design.md 明确要求），所以这里不是"阻止死亡"，
        // 只是"死后不再做后续结算"。
        // 多人**根本走不到这** —— 引擎在
        // <c>NEventOptionButton.OnRelease</c>（<c>:366-385</c>）就把致命选项拦下了，
        // 只会弹角色台词（<c>eventDeathPrevention</c>）而不执行。
        // </para>
        if (player.Creature.IsDead)
        {
            return;
        }

        List<CardModel> cards = (await CardSelectCmd.FromDeckForRemoval(
            prefs: new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1),
            player: player)).ToList();

        await CardPileCmd.RemoveFromDeck(cards);

        SetEventFinished(L10NLookup($"{Key}.pages.LEAN.description"));
    }

    /// <summary>
    /// 选项 2：<b>由玩家自选</b>升级 2 张牌。
    ///
    /// <para>
    /// ⚠️ <b>不是随机升级。</b> 用 <c>CardSelectCmd.FromDeckForUpgrade</c>
    /// （<c>CardSelectCmd.cs:537</c>）—— 它会弹 <c>NDeckUpgradeSelectScreen</c>
    /// （带升级预览的选牌屏），与休息处「锻造」、原版事件「审判」<c>Trial</c> 同一条路径。
    /// </para>
    ///
    /// <para>
    /// <b>照抄原版 <c>Trial.MerchantInnocent()</c></b>（<c>Trial.cs:154-162</c>）——
    /// 它也是"选 2 张升级"，写法完全同构：
    /// <code>
    /// foreach (CardModel item in await CardSelectCmd.FromDeckForUpgrade(
    ///     prefs: new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 2),
    ///     player: base.Owner))
    /// {
    ///     CardCmd.Upgrade(item);
    /// }
    /// </code>
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>为什么不用"过滤 + 随机取 2"</b>（那是「光与暗的门扉」<c>DoorsOfLightAndDark</c>
    /// 的做法）：那个事件的设计本就是"随机升级 2 张"。本事件按 design.md 是玩家自选，
    /// 两者语义不同，不能互相照抄。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>几条边界由 <c>FromDeckForUpgrade</c> 自己处理好了</b>：
    /// <list type="bullet">
    ///   <item>它内部先 <c>Where(c =&gt; c.IsUpgradable)</c>（<c>:539</c>），
    ///         所以**不需要**自己过滤；</item>
    ///   <item>可升级牌为 0 张时直接返回空（<c>:540-543</c>）⇒ 无事发生；</item>
    ///   <item>可升级牌 ≤ <c>MinSelect</c> 且不要求手动确认时**自动全选、不弹界面**
    ///         （<c>:545-548</c>）⇒ 正好满足“只有 1 或 2 张可升级时直接升级”。</item>
    /// </list>
    /// <c>CardSelectorPrefs(prompt, selectCount)</c> 会令 <c>MinSelect == MaxSelect == 2</c>
    /// 且 <c>RequireManualConfirmation == false</c>（<c>CardSelectorPrefs.cs:77</c>），
    /// 所以只有"可升级牌 &gt; 2 张"时才会真正弹选牌屏。
    /// </para>
    /// </summary>
    private async Task ChooseBalanced()
    {
        Player? player = Owner;
        if (player == null)
        {
            return;
        }

        IEnumerable<CardModel> picked = await CardSelectCmd.FromDeckForUpgrade(
            prefs: new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, UpgradeCount),
            player: player);

        foreach (CardModel card in picked)
        {
            CardCmd.Upgrade(card);
        }

        SetEventFinished(L10NLookup($"{Key}.pages.BALANCED.description"));
    }

    /// <summary>
    /// 选项 3：回复 20 点生命，并获得 3 个随机遗物。
    ///
    /// <para>
    /// 回复用 <c>CreatureCmd.Heal(creature, amount)</c>
    /// （<c>CreatureCmd.cs:735</c>；原版事件 <c>AbyssalBaths.cs:60</c> 同写法）。
    /// </para>
    ///
    /// <para>
    /// 遗物用原版 <c>RelicFactory.PullNextRelicFromFront(player)</c>
    /// （<c>RelicFactory.cs:26</c>）—— 它就是"掷稀有度 + 从池里取 + 标记已见过"，
    /// 与精英战奖励、<c>RanwidTheElder</c> 用的是同一条路径。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>不要去读 <c>ModelDb.RelicPool&lt;WizardRelicPool&gt;()</c> 手动抽</b>：
    /// <c>PullNextRelicFromFront</c> 已经正确处理了"玩家角色池 + 共享池"的合并
    /// 与"本次运行不再出现"的状态，绕过它会导致遗物重复或该出现的不出现。
    /// </para>
    /// </summary>
    private async Task ChooseAbundant()
    {
        Player? player = Owner;
        if (player == null)
        {
            return;
        }

        await CreatureCmd.Heal(player.Creature, DynamicVars.Heal.BaseValue);

        for (int i = 0; i < RelicCount; i++)
        {
            await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(player).ToMutable(), player);
        }

        SetEventFinished(L10NLookup($"{Key}.pages.ABUNDANT.description"));
    }

    /// <summary>
    /// 选项 4：离开 —— 随机降级 2 张牌。
    ///
    /// <para>
    /// 与选项 2「升级」完全镜像：过滤 <c>IsUpgraded</c>
    /// （<c>CardModel.cs:784</c> 的 <c>CurrentUpgradeLevel &gt; 0</c>）→
    /// <c>StableShuffle(Rng)</c> → <c>Take(2)</c> → <c>CardCmd.Downgrade</c>
    /// （<c>CardCmd.cs:212</c>）。
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>必须过滤 <c>IsUpgraded</c></b>：<c>DowngradeInternal</c>（<c>CardModel.cs:2149</c>）
    /// 会无条件把 <c>CurrentUpgradeLevel</c> 置 0 并重置费用/关键词，
    /// 对<b>未升级</b>的牌调用虽然不会崩，但会白白跑一遍重建
    /// （且会污染 <c>DowngradedCards</c> 统计）。所以先筛再抽。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 若牌组里升级过的牌不足 2 张，就只降级现有的那几张
    /// （<c>Take</c> 天然饱和），不会报错。
    /// </para>
    /// </summary>
    private Task ChooseLeave()
    {
        Player? player = Owner;
        if (player == null)
        {
            return Task.CompletedTask;
        }

        IEnumerable<CardModel> picked = PileType.Deck.GetPile(player).Cards
            .Where(c => c.IsUpgraded)
            .ToList()
            .StableShuffle(Rng)
            .Take(DowngradeCount);

        foreach (CardModel card in picked)
        {
            CardCmd.Downgrade(card);
        }

        SetEventFinished(L10NLookup($"{Key}.pages.LEAVE.description"));
        return Task.CompletedTask;
    }
}
