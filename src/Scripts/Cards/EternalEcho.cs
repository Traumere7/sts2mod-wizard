using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Enchantments;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 永恒回响 - 1 费 - 能力 - 稀有
/// 选择你的 1 张手牌附魔：复奏（重放 2）。即毁。升级后获得「保留」。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 附魔的两种生命周期（用户 2026-11-13 明确）
/// ══════════════════════════════════════════════════════════════════
/// <list type="bullet">
///   <item>目标是**卡组里存在的牌** ⇒ 手牌那份 + <c>DeckVersion</c> 那份各附魔一次
///         ⇒ 永久（每场战斗都生效，随卡组存档）；</item>
///   <item>目标是**临时加入的牌**（<c>DeckVersion == null</c>）⇒ 只有手牌那份 ⇒ 仅本场战斗。</item>
/// </list>
/// 为什么必须分别附魔：战斗牌是卡组牌的**克隆**，且
/// <c>CardModel.DeepCloneFields</c> 会给每张战斗牌**独立的附魔实例**
/// （见 <c>docs/RESEARCH_MERCURY_ETERNALECHO.md</c> §2）——
/// 只改手牌 → 本场有效、下场没有；只改卡组 → 下场才有。
/// </para>
///
/// <para>
/// ⚠️ <b>2026-11-13 bug 修复</b>：判"是否临时牌"不能只看 <c>DeckVersion == null</c>。
/// 用户实测出现过"给临时生成的牌附魔，结果卡组里同名牌也带上了复奏"——
/// 说明某些生成路径会让牌**带着 DeckVersion 进来**（或用户难以分辨手牌来源）。
/// 现在改为查**战斗历史**：只要这张牌出现在本场战斗的
/// <c>CardGeneratedEntry</c> 里，就一律按"临时牌"处理，**绝不写回卡组**。
/// 这样即使引擎的 <c>DeckVersion</c> 语义有偏差，也不会把附魔泄漏到卡组。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 边界（用户明确要求）
/// ══════════════════════════════════════════════════════════════════
/// 手里只有「永恒回响」自己、或**没有任何合法附魔目标**时：
/// 这张牌**照样可以打出**（扣费、作为能力牌进 <c>PileType.None</c>），
/// 但**不触发「即毁」** —— 即毁只在"正确结算（真的附魔成功）之后"才移除卡组里的这张牌。
/// </para>
///
/// <para>
/// 合法目标由 <c>Reprise.CanEnchant</c> 判定：基类已排除 状态 / 诅咒 / 任务牌、
/// 卡组里不可打出的牌、以及**已有其它附魔**的牌（引擎限制：异种附魔会抛异常）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class EternalEcho : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>「即毁」是引擎级自定义词条（见 <see cref="WizardKeywords.Obliterate"/>）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        WizardKeywords.Obliterate,
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        .. HoverTipFactory.FromEnchantment<Reprise>(Reprise.ReplayCount),
    ];

    public EternalEcho()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState == null)
        {
            return;
        }

        EnchantmentModel template = ModelDb.Enchantment<Reprise>();

        // 手牌里除自己以外的合法目标。
        List<CardModel> candidates = PileType.Hand
            .GetPile(Owner)
            .Cards
            .Where(c => c != this && template.CanEnchant(c))
            .ToList();

        if (candidates.Count == 0)
        {
            // 允许打出；不触发即毁（卡组里这张牌保留，之后还能再用）。
            return;
        }

        // 强制选 1 张（Cancelable 默认为 false，区间构造会要求手动确认）。
        CardModel? picked = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1, 1),
            c => c != this && template.CanEnchant(c),
            this)).FirstOrDefault();

        if (picked == null)
        {
            // 理论不可达（Cancelable=false）；保守处理：当成"未结算"。
            return;
        }

        // ① 手牌这份：本场战斗立即生效。
        CardCmd.Enchant(ModelDb.Enchantment<Reprise>().ToMutable(), picked, Reprise.ReplayCount);

        // ② 卡组那份：**只有"确实来自卡组"的牌**才永久生效。
        //
        //    ⚠️ 不能只看 DeckVersion != null：用户实测过"临时牌被附魔 → 卡组里同名牌也带复奏"。
        //    判据改用战斗历史（与「结晶」「催化」同一套 CardGeneratedEntry）：
        //    只要这张牌是本场战斗**生成**出来的，就一律当临时牌，绝不写回卡组。
        bool generatedThisCombat = IsGeneratedThisCombat(picked);
        bool deckOrigin = !generatedThisCombat && picked.DeckVersion != null;

        Entry.Logger.Info(
            $"[永恒回响] 目标={picked.Id.Entry}｜本场生成={generatedThisCombat}"
            + $"｜DeckVersion={picked.DeckVersion?.Id.Entry ?? "null"}｜写回卡组={deckOrigin}");

        if (generatedThisCombat && picked.DeckVersion != null)
        {
            // 引擎层面不该出现；真出现就说明有生成路径带上了 DeckVersion，记录一条便于排查。
            Entry.Logger.Warn(
                $"[永恒回响] 临时牌 {picked.Id.Entry} 竟然带 DeckVersion={picked.DeckVersion.Id.Entry}，"
                + "已按临时牌处理（不写回卡组）。");
        }

        if (deckOrigin)
        {
            CardCmd.Enchant(
                ModelDb.Enchantment<Reprise>().ToMutable(),
                picked.DeckVersion!,
                Reprise.ReplayCount);
        }

        // ③ 即毁：真的结算成功了，才把这张牌从卡组里移除。
        await CardObliteration.Obliterate(this);
    }

    /// <summary>
    /// 这张牌是不是**本场战斗生成出来的**（= 临时牌）。
    /// 与「结晶」<c>Crystallization.GeneratedCardThisTurn</c>、「催化」同一套历史：
    /// <c>CombatManager.Instance.History.Entries.OfType&lt;CardGeneratedEntry&gt;()</c>。
    /// 用引用相等判断，避免同名牌（多张观火）互相误判。
    /// </summary>
    private bool IsGeneratedThisCombat(CardModel card)
    {
        if (CombatState == null)
        {
            return false;
        }

        return CombatManager.Instance.History.Entries
            .OfType<CardGeneratedEntry>()
            .Any(e => ReferenceEquals(e.Card, card));
    }

    /// <summary>
    /// 升级：获得「保留」（与「镜像」「燃烧之魂」同样处理 —— 升级不加数值，只给词条）。
    /// ⚠️ 词条由引擎渲染，**不写进文案**。
    /// </summary>
    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
