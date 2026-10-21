using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 真理派对 - 1 费 - 技能 - 罕见
/// 给予 2(3) 层燃烧。依照场上最高的燃烧层数的一半抽牌，选最多那个数量的牌消耗。
///
/// <para>
/// 两段效果分别照抄原版：
/// <list type="bullet">
///   <item>抽牌 → <c>CardPileCmd.Draw</c>（「燃烧契约」<c>BurningPact</c>）</item>
///   <item>选牌消耗 → <c>CardSelectCmd.FromHand(ExhaustSelectionPrompt, n)</c></item>
/// </list>
/// </para>
///
/// <para>
/// <b>结算顺序很重要：先上燃烧、再读最高层数。</b>
/// 本牌自己施加的燃烧也要计入"场上最高燃烧层数"，
/// 否则 2 层燃烧在小数值场合永远不会被算进去。
/// </para>
///
/// <para>
/// <b>"场上"含玩家自己身上的燃烧</b>，不只是敌人 ——
/// 统一走 <see cref="FieldState.TryHighestBurn"/>。
/// 先前这里用的是 <c>GetOpponentsOf</c>（仅敌人），
/// 会在「高温消毒」给自己上燃烧后算出偏低的数值。
/// </para>
///
/// <para>
/// <b>⚠️ 2026-10-03 平衡改动：抽牌与消耗的张数从"最高燃烧层数"改为"它的一半"。</b>
/// 口径是<b>向上取整</b>（<c>(highest + 1) / 2</c>）：
/// <c>1→1、2→1、3→2、4→2、5→3</c>。
/// 这样 1 层燃烧也至少能抽/消耗 1 张，不会出现"上了燃烧却什么都没发生"。
/// （该口径是技术实现细节，<b>不写进卡面文案</b>。）
/// </para>
///
/// <para>
/// <b>"选最多到那个数量"</b>用 <c>CardSelectorPrefs(prompt, minCount, maxCount)</c> 的
/// 区间构造：<c>min = 0</c>（<b>可以一张都不选</b>）、<c>max = 半数</c>。
/// 用单值构造会强制恰好选那么多张，与"最多"不符。
/// </para>
///
/// <para>
/// <b>抽牌与消耗的张数都用同一个"半数"值</b>，且该数值可能很大（燃烧无上限增长），
/// 所以两个数都要夹到合理范围：抽牌由引擎的手牌上限兜底，
/// 消耗张数则夹到当前手牌数（否则 <c>FromHand</c> 可能等不到足够的牌）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class TruthParty : WizardCardModel
{
    private const int Cost = 1;

    private const string BurnKey = nameof(BurnPower);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BurnPower>(2m),
    ];

    public TruthParty()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // ① 先上燃烧 —— 本牌自己给的燃烧也要参与下面的"最高层数"。
        await SpellCmd.ApplyBurn(
            choiceContext,
            cardPlay.Target,
            DynamicVars[BurnKey].BaseValue,
            Owner.Creature,
            this);

        if (CombatState == null)
        {
            return;
        }

        // ② 读场上最高燃烧层数（含刚施加的，也含玩家自己身上的 —— 见 FieldState）。
        if (!FieldState.TryHighestBurn(this, out int highest) || highest <= 0)
        {
            return;
        }

        // ③ 取一半（向上取整）。1 层也能拿到 1，不会归零。
        int half = (highest + 1) / 2;

        // ④ 抽牌（张数 = 半数；超出抽牌堆/手牌上限由引擎自行处理）。
        await CardPileCmd.Draw(choiceContext, half, Owner);

        // ⑤ 选**最多** half 张手牌消耗（min=0 表示可以一张都不选）。
        int handCount = PileType.Hand.GetPile(Owner).Cards.Count;
        int maxSelect = Math.Min(half, handCount);
        if (maxSelect <= 0)
        {
            return;
        }

        IEnumerable<CardModel> toExhaust = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, maxSelect),
            filter: null,
            source: this);

        foreach (CardModel card in toExhaust.ToList())
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[BurnKey].UpgradeValueBy(1m);
    }
}
