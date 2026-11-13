using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
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

        // ② 卡组那份：永久生效（临时牌没有 DeckVersion，天然跳过）。
        if (picked.DeckVersion != null)
        {
            CardCmd.Enchant(
                ModelDb.Enchantment<Reprise>().ToMutable(),
                picked.DeckVersion,
                Reprise.ReplayCount);
        }

        // ③ 即毁：真的结算成功了，才把这张牌从卡组里移除。
        await CardObliteration.Obliterate(this);
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
