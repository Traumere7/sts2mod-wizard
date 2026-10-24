using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 镜像 / Mirror - 1(0) 费 - 技能 - 稀有
/// 将你所有手牌的复制品加入你的手牌。消耗。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 顺序口径（用户 2026-10-25 明确）：ABCD → ABCD<b>DCBA</b>
/// ══════════════════════════════════════════════════════════════════
/// "给当前手牌抓一个快照，然后从最右侧开始、把快照的复制品依次加进去"。
/// 实现上要注意 <c>CardPileCmd.Add</c> 只支持三种位置
/// （<c>CardPileCmd.cs:505-511</c>）：
/// <code>
/// CardPilePosition.Bottom => -1   // 追加到末尾（手牌的最右）
/// CardPilePosition.Top    =>  0   // 插到开头（手牌的最左）
/// CardPilePosition.Random => 随机索引
/// </code>
/// <b>没有"插入到指定索引"的重载</b>。所以做法是：
/// <b>倒序遍历快照（D、C、B、A），每张都 <c>Bottom</c> 追加到最右</b>：
/// <code>
/// 初始  ABCD
/// 加 D → ABCD D
/// 加 C → ABCD D C
/// 加 B → ABCD D C B
/// 加 A → ABCD D C B A   ✅ 正是 ABCD DCBA
/// </code>
/// ⚠️ 若正序遍历 + Bottom 会得到 <c>ABCD ABCD</c>（错）；正序遍历 + Top 会得到
/// <c>DCBA ABCD</c>（也错）。只有"倒序 + Bottom"这一种组合对。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 手牌满了怎么办：引擎已经帮我们处理好了
/// ══════════════════════════════════════════════════════════════════
/// <c>CardPile.MaxCardsInHand = 10</c>（<c>CardPile.cs:21</c>）。
/// <c>CardPileCmd.Add</c> 内部有（<c>:484-519</c>）：
/// <code>
/// bool isFullHandAdd = cardPile.Type == PileType.Hand &amp;&amp;
///                      cardPile.Cards.Count &gt;= CardPile.MaxCardsInHand;
/// if (isFullHandAdd) cardPile = CardPile.Get(PileType.Discard, card.Owner);   // 改投弃牌堆
/// ...
/// if (isFullHandAdd &amp;&amp; LocalContext.IsMe(card.Owner))
///     ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), ...);      // 弹"手牌已满"
/// </code>
/// ⇒ <b>"超出上限的复制品挤进弃牌堆"是引擎的既定行为，不需要自己判断</b>
/// （用户 2026-10-25 的要求正好与之一致）。
/// </para>
///
/// <para>
/// <b>复制品用 <c>CardModel.CreateClone()</c></b>（照抄原版「夜魇」<c>Nightmare</c>）：
/// 它会 <c>CardScope.CloneCard(this)</c> 复制整张牌（含升级等级），
/// 并要求原牌处于战斗牌堆中（手牌满足）。
/// 之后同样照抄夜魇调一次 <c>CardCmd.ClearAffliction(clone)</c> ——
/// 复制品不应继承原牌身上的"折磨"类临时减益。
/// </para>
///
/// <para>
/// <b>复制品算"生成卡牌"</b>：走的是
/// <c>CardPileCmd.AddGeneratedCardToCombat</c>，会触发
/// <c>Hook.AfterCardGeneratedForCombat</c> —— 所以「催化」会数到它们，
/// 「创世之柱」也会响应。这是刻意的（复制品确实是生成的牌）。
/// </para>
///
/// <para>
/// ⚠️ <b>快照必须在加牌之前取</b>：否则会把自己刚加进去的复制品再复制一遍，
/// 变成指数爆炸（10 张手牌 → 10 + 20 + 40 …）。
/// </para>
///
/// <para>
/// <b>本牌自带「消耗」</b>，所以打完就进消耗堆；打出时它自己在
/// <c>PileType.Play</c>、不在手牌里，因此<b>不会复制自己</b>。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Mirror : WizardCardModel
{
    private const int Cost = 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public Mirror()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ⚠️ 先快照：不然后面会把新加的复制品也复制进去（指数爆炸）。
        List<CardModel> snapshot = PileType.Hand.GetPile(Owner).Cards.ToList();

        // 倒序遍历 + 每张追加到最右 ⇒ ABCD → ABCD DCBA。
        for (int i = snapshot.Count - 1; i >= 0; i--)
        {
            CardModel clone = snapshot[i].CreateClone();

            // 复制品不继承"折磨"类临时减益（同原版「夜魇」）。
            CardCmd.ClearAffliction(clone);

            // Bottom = 追加到末尾 = 手牌最右；手牌满时引擎自动改投弃牌堆。
            await CardPileCmd.AddGeneratedCardToCombat(
                clone, PileType.Hand, Owner, CardPilePosition.Bottom);
        }
    }

    /// <summary>升级 1 费 → 0 费（design.md 写的是「1（0）」）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
