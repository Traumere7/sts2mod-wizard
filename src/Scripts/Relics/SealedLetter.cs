using System.Linq;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 火漆密函 / SealedLetter - Rare
/// 在你的第 3 回合开始时，将 1 张随机能力牌加入你的手牌，那张牌的耗能变为 0。
///
/// <para>
/// <b>三段各照抄一个原版模板：</b>
/// <list type="number">
///   <item><b>"第 3 回合"</b> —— 原版「吊灯」<c>Chandelier</c>：
///         <c>AfterSideTurnStart</c> + <c>participants.Contains</c> +
///         <c>Owner.PlayerCombatState.TurnNumber == 3</c>（<c>Chandelier.cs:27</c>）。</item>
///   <item><b>随机能力牌</b> —— 原版卡「白噪」<c>WhiteNoise</c>（它<b>就是</b>
///         "加 1 张随机能力牌到手牌、本回合费用为 0"，<c>WhiteNoise.cs:23-25</c>）
///         与遗物「弩」<c>Crossbow</c>（同样的三次调用，只是过滤攻击牌）。</item>
///   <item><b>费用归 0</b> —— <c>CardModel.SetToFreeThisTurn()</c>（<c>CardModel.cs:1267</c>），
///         它内部是 <c>EnergyCost.SetThisTurnOrUntilPlayed(0)</c> +
///         <c>SetStarCostThisTurn(0)</c>。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>用 <c>TurnNumber</c> 而不是 <c>RoundNumber</c></b>：前者是<b>每个玩家各自</b>
/// 的回合序号（从 1 开始，额外回合会 +1，多人下各人不同），
/// 见 <c>PlayerCombatState.cs:29-37</c>。原版所有"第 N 回合"遗物都用它。
/// </para>
///
/// <para>
/// ⚠️ <b><c>CardFactory.GetDistinctForCombat</c> 已经替我们做了三件事</b>
/// （<c>CardFactory.cs:119-129</c>）：按人数过滤、<c>FilterForCombat</c>（排除
/// <c>Basic</c>/<c>Ancient</c>/<c>Event</c> 稀有度与 <c>CanBeGeneratedInCombat == false</c> 的牌）、
/// 以及<b>生成战斗副本</b>（<c>CombatState.CreateCard</c>）。
/// 所以<b>不需要</b>自己再调 <c>CreateCard</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>它可能返回空或少于请求数</b>，所以 <c>FirstOrDefault()</c> + null 检查是必需的
/// （<c>WhiteNoise.cs:26</c> / <c>PowerPotion.cs:29</c> 都这么做）。
/// 另外<b>先判空池</b>再调用（照抄 <c>Crossbow.cs:26-29</c>）——
/// 因为 <c>FilterForCombat</c> 可能把整个池子滤空。
/// </para>
///
/// <para>
/// ⚠️ <b>随机源必须用 <c>Owner.RunState.Rng.CombatCardGeneration</c></b>
/// （<c>RunRngSet.cs:51</c>），这样多人/回放才一致。
/// 想允许重复则用 <c>CardFactory.GetForCombat</c>（<c>CardFactory.cs:140-152</c>）。
/// </para>
///
/// <para>
/// <b>加到手牌用 <c>CardPileCmd.AddGeneratedCardToCombat</c></b>：它会写入战斗历史
/// （"生成牌"条目），这是「回忆」等依赖历史的效果能正确工作的前提。
/// ⚠️ 它<b>不检查手牌上限</b>（与本 mod「魔法卷轴」的注释一致）。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class SealedLetter : ArcaneRelicModel
{
    /// <summary>触发的回合序号。</summary>
    private const int TriggerTurn = 3;

    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    /// 触发回合数与加入张数 —— 都以变量承载，让工具提示能引用
    /// （原版遗物约定：数字一律 `[blue]{变量}[/blue]`）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turn", TriggerTurn),
        new CardsVar(CardsToAdd),
    ];

    /// <summary>加入的张数（固定 1）。</summary>
    private const int CardsToAdd = 1;

    // 没有额外悬停提示 —— 本遗物涉及的是"随机能力牌 + 临时免费"，
    // 两者都没有合适的既有 HoverTip 工厂（随机牌无法 FromCard，
    // 临时费用也不是关键字）。硬凑一个反而误导。

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        if (Owner.PlayerCombatState?.TurnNumber != TriggerTurn)
        {
            return;
        }

        // 先滤出"本角色卡池里的能力牌"。空池就别往下走 ——
        // GetDistinctForCombat 还会再滤掉 Basic/Ancient/Event，可能一个不剩。
        List<CardModel> pool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Type == CardType.Power)
            .ToList();

        if (pool.Count == 0)
        {
            return;
        }

        CardModel? card = CardFactory
            .GetDistinctForCombat(Owner, pool, 1, Owner.RunState.Rng.CombatCardGeneration)
            .FirstOrDefault();

        if (card == null)
        {
            return;
        }

        Flash();

        // ⚠️⚠️ <b>2026-10-12 实测修正（第二次）：要用 <c>SetToFreeThisCombat()</c>，
        // 让这张牌<b>整场战斗</b>保持 0 费。</b>
        //
        // 演变过程（两次都是我理解偏了）：
        // <list type="number">
        //   <item>初版用 <c>SetToFreeThisTurn()</c>（= <c>SetThisTurnOrUntilPlayed(0)</c>），
        //         实测"一直保持 0 费、不只本回合"。</item>
        //   <item>我据此改成 <c>EnergyCost.SetThisTurn(0)</c>，
        //         结果变成"<b>只有</b>本回合 0 费" —— 又反了。</item>
        //   <item>用户澄清期望：<b>本场战斗中一直是 0 费</b>。
        //         正解是 <c>SetToFreeThisCombat()</c>
        //         （= <c>EnergyCost.SetThisCombat(0)</c> + <c>SetStarCostThisCombat(0)</c>，
        //         过期标志 <c>EndOfCombat</c>，见 <c>CardEnergyCost.cs:238</c>）。</item>
        // </list>
        //
        // 参照：原版「宝石面具」<c>JeweledMask</c> 用的是 <c>SetToFreeThisTurn()</c>
        // （它只要本回合）；本遗物要的是整场，所以往上一档用 ThisCombat 版本。
        card.SetToFreeThisCombat();

        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}
