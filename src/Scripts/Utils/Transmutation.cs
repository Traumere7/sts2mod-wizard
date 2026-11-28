using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Utils;

/// <summary>
/// 嬗变 / Transmutation —— 本 mod **自己重写**的"变化"（不再使用原版 <c>CardCmd.TransformToRandom</c> 的选牌规则）。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 与原版「变化」的两处关键不同（design.md 第 18~20 行）
/// ══════════════════════════════════════════════════════════════════
/// <list type="number">
///   <item><b>稀有度只升不降</b>：
///         普通/基础 → 普通/罕见/稀有；罕见 → 罕见/稀有；稀有 → 稀有；
///         先古 → 无色**稀有**（design 的特别要求）。</item>
///   <item><b>状态/诅咒</b> → 换成一个"正面、可打出"的池子：
///         全部颜色（含无色、巫师、愚者）+ 事件牌 + 衍生牌，
///         再排除 <c>Basic</c>（太弱）/ <c>Ancient</c>（太强太稀有）/ 状态 / 诅咒 / 不可打出。</item>
/// </list>
/// 其余机制**完全对齐原版**（见 <see cref="TransmuteMany"/> 的实现说明）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 实现方式：复用引擎的落位/演出，只替换"选谁"这一步
/// ══════════════════════════════════════════════════════════════════
/// 原版的选牌在 <c>CardFactory.GetDefaultTransformationOptions</c> +
/// <c>GetFilteredTransformationOptions</c>（内部还会按稀有度 2~4 过滤、查 <c>CanBeGeneratedInCombat</c>），
/// 这些规则与本 mod 的稀有度规则冲突，所以<b>不能直接喂 options</b>。
/// <para>
/// 做法：先**自己挑好替代牌**（<see cref="PickReplacement"/>），再用
/// <c>new CardTransformation(original, replacement)</c> 的"指定替代"构造 → 交给
/// <c>CardCmd.Transform(...)</c>。后者负责：
/// 从原牌堆移除、按原位置放回、<c>CardsTransformed</c> 历史、翻牌演出、
/// 以及 <c>Hook.ModifyCardBeingAddedToDeck</c>（牌组场景）。
/// 这样既保留了原版全部周边行为，又完全掌控候选池。
/// </para>
/// </para>
///
/// <para>
/// ⚠️ <b>"算作生成"</b>：design 明确要求嬗变被视作"生成卡牌"。
/// 原版 <c>CardFactory.CreateRandomCardForTransform</c> 走的是 <c>CardScope.CreateCard</c>，
/// <b>不会</b>记 <c>CardGenerated</c> 历史、也不发 <c>AfterCardGeneratedForCombat</c>；
/// 所以我们在变换完成后补上这两件事（「催化」「孔雀之尾」「结晶」都会因此响应）。
/// </para>
///
/// <para>
/// ⚠️ <b>升级/附魔不保留</b>：与原版一致 —— 替代牌是由卡池原型新建的牌（未升级、无附魔）。
/// </para>
/// </summary>
public static class Transmutation
{
    // ══════════════════════════════════════════════════════════════════
    // 对外 API
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 这张牌能否被嬗变 —— 与原版一致：<see cref="CardModel.IsTransformable"/>
    /// （<c>Eternal</c> 牌不可移除 ⇒ 也不可变化）。
    /// </summary>
    public static bool CanTransmute(CardModel card) => card.IsTransformable;

    /// <summary>嬗变一张牌，返回替代牌（失败/无候选时返回 <c>null</c>）。</summary>
    public static async Task<CardModel?> Transmute(
        CardModel original,
        Rng rng,
        CardPreviewStyle style = CardPreviewStyle.HorizontalLayout)
    {
        IReadOnlyList<CardModel> created = await TransmuteMany([original], rng, style);
        return created.Count > 0 ? created[0] : null;
    }

    /// <summary>
    /// 批量嬗变（一次调用 = 一次原版 <c>CardCmd.Transform</c>，顺序/位置由引擎处理）。
    /// 返回实际生成的替代牌列表（顺序与可嬗变的原牌一致）。
    /// </summary>
    public static async Task<IReadOnlyList<CardModel>> TransmuteMany(
        IEnumerable<CardModel> originals,
        Rng rng,
        CardPreviewStyle style = CardPreviewStyle.HorizontalLayout)
    {
        List<CardTransformation> transformations = [];
        List<CardModel> created = [];

        foreach (CardModel original in originals)
        {
            // 不可变化的牌跳过（原版在这里会抛异常，我们直接忽略）。
            if (!CanTransmute(original))
            {
                continue;
            }

            CardModel? replacement = PickReplacement(original, rng);
            if (replacement == null)
            {
                continue;
            }

            transformations.Add(new CardTransformation(original, replacement));
            created.Add(replacement);
        }

        if (transformations.Count == 0)
        {
            return created;
        }

        // 引擎负责移除/落位/历史/演出。
        await CardCmd.Transform(transformations, rng, style);

        // 设计口径：嬗变也算"生成卡牌" ⇒ 补上历史与钩子。
        foreach (CardModel replacement in created)
        {
            await RegisterAsGenerated(replacement);
        }

        return created;
    }

    /// <summary>
    /// 选牌界面的提示语（自定义键，见 <c>localization/{lang}/card_selection.json</c> 的
    /// <c>WIZARD-TO_TRANSMUTE</c>）。
    /// 不用原版 <c>CardSelectorPrefs.TransformSelectionPrompt</c>：那会显示"选择要变化的牌"。
    /// </summary>
    public static LocString SelectionPrompt =>
        new LocString("card_selection", "WIZARD-TO_TRANSMUTE");

    /// <summary>
    /// 嬗变的悬停提示（自定义文案，见 <c>localization/{lang}/static_hover_tips.json</c> 的
    /// <c>WIZARD-TRANSMUTATION.title/description</c>）。
    /// 不用 <c>StaticHoverTip.Transform</c>：那是原版"变化"的解释，与嬗变规则不同。
    /// </summary>
    public static IHoverTip HoverTip => new HoverTip(
        new LocString("static_hover_tips", "WIZARD-TRANSMUTATION.title"),
        new LocString("static_hover_tips", "WIZARD-TRANSMUTATION.description"));

    // ══════════════════════════════════════════════════════════════════
    // 候选池
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 状态/诅咒的"正面、可打出"候选池：
    /// 全部颜色（含无色 / 巫师 / 愚者）+ 事件牌 + 衍生牌。
    /// </summary>
    private static CardPoolModel[] AllColorAndSpecialPools =>
    [
        ModelDb.CardPool<IroncladCardPool>(),     // 红 - 铁甲战士
        ModelDb.CardPool<SilentCardPool>(),       // 绿 - 静默猎手
        ModelDb.CardPool<DefectCardPool>(),       // 蓝 - 故障机器人
        ModelDb.CardPool<RegentCardPool>(),       // 橙 - 储君
        ModelDb.CardPool<NecrobinderCardPool>(),  // 紫 - 亡灵契约师
        ModelDb.CardPool<FoolCardPool>(),         // 黄 - 愚者
        ModelDb.CardPool<ColorlessCardPool>(),    // 灰 - 无色牌
        ModelDb.CardPool<WizardCardPool>(),       // 本 mod
        ModelDb.CardPool<EventCardPool>(),        // 事件牌
        ModelDb.CardPool<TokenCardPool>(),        // 衍生牌
    ];

    /// <summary>这张牌是否按"状态/诅咒"的特殊规则处理。</summary>
    private static bool IsStatusOrCurse(CardModel card) =>
        card.Type is CardType.Status or CardType.Curse
        || card.Rarity is CardRarity.Status or CardRarity.Curse;

    /// <summary>
    /// 普通路径的"颜色"规则（照抄原版 <c>CardFactory.GetDefaultTransformationOptions</c>）：
    /// 非 Quest 且非 事件/先古/衍生的牌 ⇒ 用它自己的卡池；否则 ⇒ 无色牌池。
    /// </summary>
    private static CardPoolModel GetSourcePool(CardModel original) =>
        original.Type != CardType.Quest
        && original.Rarity is not (CardRarity.Event or CardRarity.Ancient or CardRarity.Token)
            ? original.Pool
            : ModelDb.CardPool<ColorlessCardPool>();

    /// <summary>
    /// 普通路径允许的目标稀有度（design：只升不降）。
    /// <list type="bullet">
    ///   <item>Basic / Common → Common / Uncommon / Rare</item>
    ///   <item>Uncommon → Uncommon / Rare</item>
    ///   <item>Rare → Rare</item>
    ///   <item>Ancient → Rare（特别要求：先古一定变**无色稀有**）</item>
    ///   <item>Event / Token → Common / Uncommon / Rare（沿用原版口径）</item>
    /// </list>
    /// </summary>
    private static CardRarity[] AllowedRarities(CardRarity source) => source switch
    {
        CardRarity.Basic or CardRarity.Common or CardRarity.Event or CardRarity.Token =>
            [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare],
        CardRarity.Uncommon => [CardRarity.Uncommon, CardRarity.Rare],
        CardRarity.Rare or CardRarity.Ancient => [CardRarity.Rare],
        _ => [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare],
    };

    /// <summary>挑一张替代牌并创建它的战斗/牌组实例；没有合法候选时返回 <c>null</c>。</summary>
    private static CardModel? PickReplacement(CardModel original, Rng rng)
    {
        bool isInCombat = original.CombatState != null;
        CardModel[] candidates = GetCandidates(original, isInCombat).ToArray();

        if (candidates.Length == 0)
        {
            Entry.Logger.Warn(
                $"[嬗变] {original.Id.Entry} 没有合法候选（稀有度={original.Rarity}，类型={original.Type}），跳过。");
            return null;
        }

        CardModel canonical = rng.NextItem(candidates)!;
        return CreateCardFor(original, canonical);
    }

    /// <summary>候选牌（未实例化，都是卡池原型）。</summary>
    private static IEnumerable<CardModel> GetCandidates(CardModel original, bool isInCombat)
    {
        IRunState runState = original.Owner.RunState;
        UnlockState unlockState = original.Owner.UnlockState;
        CardMultiplayerConstraint constraint = runState.CardMultiplayerConstraint;

        if (IsStatusOrCurse(original))
        {
            // ── 状态 / 诅咒：跨颜色 + 事件 + 衍生，只留"正面且可打出"的 ──
            return AllColorAndSpecialPools
                .SelectMany(p => p.GetUnlockedCards(unlockState, constraint))
                // 只留 攻击 / 技能 / 能力（排除状态、诅咒、任务、事件里的特殊类型）
                .Where(c => c.Type is CardType.Attack or CardType.Skill or CardType.Power)
                // 排除基础牌（太弱）与先古牌（太强/太稀有，用户 2026-11-22 明确）
                .Where(c => c.Rarity is not (CardRarity.Basic or CardRarity.Ancient))
                // 一定可打出
                .Where(c => !c.Keywords.Contains(CardKeyword.Unplayable))
                .Where(c => !isInCombat || c.CanBeGeneratedInCombat)
                .Where(c => c.IsAllowedInRun(runState))
                .Where(c => c.Id != original.Id)
                .DistinctBy(c => c.Id);
        }

        // ── 普通路径：同颜色（事件/先古/衍生 ⇒ 无色）+ 只升不降的稀有度 ──
        CardRarity[] allowed = AllowedRarities(original.Rarity);
        return GetSourcePool(original)
            .GetUnlockedCards(unlockState, constraint)
            .Where(c => allowed.Contains(c.Rarity))
            .Where(c => !isInCombat || c.CanBeGeneratedInCombat)
            .Where(c => c.IsAllowedInRun(runState))
            .Where(c => c.Id != original.Id);
    }

    /// <summary>按原牌所在作用域创建替代牌实例（战斗内 → <c>CombatState</c>；否则 → <c>RunState</c>）。</summary>
    private static CardModel CreateCardFor(CardModel original, CardModel canonical)
    {
        Player owner = original.Owner;
        return original.CombatState != null
            ? original.CombatState.CreateCard(canonical, owner)
            : owner.RunState.CreateCard(canonical, owner);
    }

    /// <summary>
    /// 把一张"由其它效果凭空造出来的牌"登记为**生成**：
    /// 写 <c>CardGenerated</c> 历史 + 发 <c>AfterCardGeneratedForCombat</c>。
    /// （与 <c>CardPileCmd.AddGeneratedCardsToCombat</c> 的顺序一致。）
    ///
    /// <para>
    /// 供"变化/替换成某张特定牌"的效果复用（例如「点灯」把手牌变成「火花」）——
    /// 这类效果走的是 <c>CardCmd.Transform</c> 的指定替代路径，引擎不会自动记生成。
    /// </para>
    /// </summary>
    public static async Task RegisterAsGenerated(CardModel replacement)
    {
        ICombatState? combatState = replacement.CombatState;
        if (combatState == null)
        {
            return;
        }

        Player? creator = replacement.Owner;
        CombatManager.Instance.History.CardGenerated(combatState, replacement, creator);
        await Hook.AfterCardGeneratedForCombat(combatState, replacement, creator);
    }
}
