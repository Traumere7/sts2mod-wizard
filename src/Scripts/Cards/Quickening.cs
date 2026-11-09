using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 创生 - 1(0) 费 - 技能 - 罕见
/// 从你的手牌中选最多 3 张牌[变化]。
///
/// <para>
/// <b>选卡口径与「勿忘我」一致</b>：区间构造 <c>new CardSelectorPrefs(prompt, 1, max)</c>
/// —— 至少选 1 张（min=1）、最多 3 张（max=3），区间构造会自动置
/// <c>RequireManualConfirmation</c>（玩家必须手动确认，不会一选就走）。
/// 手牌里可变化的牌少于 3 张时，max 自动降为实际张数。
/// </para>
///
/// <para>
/// ⚠️ <b>必须按 <c>IsTransformable</c> 过滤</b>：<c>CardCmd.Transform</c> 遇到不可变化的牌
/// （状态牌里的「失忆」其实可变化，但「先古卡」等不行）会**抛异常**
/// （见「灵魂」<see cref="Animism"/> 的同一条注释）。
/// 候选为空时直接跳过，不弹选牌界面。
/// </para>
///
/// <para>
/// <b>变化用 <c>CardCmd.TransformToRandom</c></b>，随机源 <c>RunState.Rng.CombatCardSelection</c>
/// —— 与「变化」<see cref="Transmogrify"/>、「灵魂」<see cref="Animism"/> 完全一致。
/// </para>
///
/// <para>
/// 升级只降费 1 → 0（"最多 3 张"不变）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Quickening : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>最多选几张（设计固定 3，不随升级变化）。</summary>
    private const int MaxSelect = 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(MaxSelect),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Transform),
    ];

    public Quickening()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState == null)
        {
            return;
        }

        // ① 先按"可变化"过滤出候选（含刚打出的这张吗？—— 不会：它已经在出牌区）。
        List<CardModel> candidates = PileType.Hand
            .GetPile(Owner)
            .Cards
            .Where(c => c.IsTransformable)
            .ToList();

        if (candidates.Count == 0)
        {
            // 没有可变化的牌 → 不弹选牌界面（"至少选 1 张"无从谈起）。
            return;
        }

        int max = Math.Min(MaxSelect, candidates.Count);

        // ② 选 1 ~ max 张。
        List<CardModel> selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1, max),
            c => c.IsTransformable,
            this)).ToList();

        // ③ 逐张变化（先 ToList 快照：变化会替换手牌里的元素）。
        foreach (CardModel card in selected)
        {
            await CardCmd.TransformToRandom(card, Owner.RunState.Rng.CombatCardSelection);
        }
    }

    protected override void OnUpgrade()
    {
        // 费用 1 → 0
        EnergyCost.UpgradeBy(-1);
    }
}
