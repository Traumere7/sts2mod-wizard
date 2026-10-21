using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 迷思 / Myth - 1 费 - 技能 - 普通
/// 获得 4(6) 格挡。获得 1(2) 费。随机化你所有手牌的耗能。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「随机化耗能」的语义：与「混乱」<c>ConfusedPower</c> **不同**
/// ══════════════════════════════════════════════════════════════════
/// 原版「异蛇之眼」<c>SneckoEye</c> 施加「混乱」<c>ConfusedPower</c>，
/// 其实现是 <c>AfterCardDrawn</c> —— <b>每次被抽到时</b>才随机一次
/// （<c>ConfusedPower.cs:33-45</c>）：
/// <code>
/// if (card.EnergyCost.Canonical &lt; 0) return;      // X 费牌跳过
/// card.EnergyCost.SetThisCombat(RunState.Rng.CombatEnergyCosts.NextInt(4));
/// NCard.FindOnTable(card)?.PlayRandomizeCostAnim();
/// </code>
///
/// <para>
/// <b>本牌（用户 2026-10-19 明确要求）</b>：立刻把<b>当前手牌</b>逐张重新赋予 0–3 的耗能，
/// 且<b>本场战斗内持续维持</b>那些数值 —— 除非被其它效果再次改写
/// （包括再打一次「迷思」）。
/// 所以直接复用同一组 API，只是<b>触发时机</b>从"抽到时"改成"打出时遍历手牌"。
/// </para>
///
/// <para>
/// ⚠️ <b><c>SetThisCombat</c> 为什么就够"持续维持"</b>：
/// 它往 <c>CardEnergyCost._localModifiers</c> 里加一个
/// <c>LocalCostModifier(cost, Absolute, EndOfCombat)</c>
/// （<c>CardEnergyCost.cs:238-244</c>）。而
/// <c>GetWithModifiers</c>（<c>:109-115</c>）会<b>按加入顺序</b>依次应用这些 modifier，
/// <c>Absolute</c> 的语义是"把费用设为该值"（见 <c>LocalCostModifier</c> 的类注释示例），
/// ⇒ <b>后加入的覆盖先加入的</b>。所以：
/// <list type="bullet">
///   <item>再打一次「迷思」⇒ 新 modifier 覆盖旧的 ✓</item>
///   <item>其它改费效果（加费/减费）⇒ 按其自身语义叠加或覆盖 ✓</item>
///   <item>换回合 ⇒ <c>EndOfCombat</c> 不到期，数值保持不变 ✓</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>X 费牌（<c>Canonical &lt; 0</c>）必须跳过</b> —— 与 <c>ConfusedPower</c> 一致。
/// 给 X 费牌设绝对值会把"X"变成定值，破坏牌面语义。
/// </para>
///
/// <para>
/// ⚠️ <b>随机数源用 <c>RunState.Rng.CombatEnergyCosts</c></b>（与 <c>ConfusedPower</c> 同一个），
/// 保证"战斗内耗能随机"这条流是可复现的。
/// </para>
///
/// <para>
/// <b>结算顺序：格挡 → 获得能量 → 随机化耗能。</b>
/// 先把玩家自己的收益结算完，再动手牌费用，读感更自然。
/// </para>
///
/// <para>
/// <b>费用文案用 <c>{Energy:energyIcons()}</c></b>（动态能量图标）：
/// 与「整理」<c>TidyUp</c>（同样 `EnergyVar` + 升级 +1）一致 ——
/// 升级后图标数会从 1 个变 2 个，所以不能用写死个数的静态写法。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Myth : WizardCardModel
{
    private const int Cost = 1;

    private const int Block = 4;

    private const int BlockUpgrade = 2;

    private const int Energy = 1;

    private const int EnergyUpgrade = 1;

    /// <summary>随机耗能的上界（不含）：<c>NextInt(4)</c> ⇒ 0–3。</summary>
    private const int EnergyCostExclusiveMax = 4;

    private const string EnergyKey = "Energy";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        base.EnergyHoverTip,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(Block, ValueProp.Move),
        new EnergyVar(Energy),
    ];

    public Myth()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 格挡。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        // ② 获得能量。
        await PlayerCmd.GainEnergy(DynamicVars[EnergyKey].BaseValue, Owner);

        // ③ 随机化所有手牌的耗能（照抄 ConfusedPower 的写法）。
        RandomizeHandCosts();
    }

    /// <summary>
    /// 把当前手牌逐张的耗能重设为 0–3 之间的随机值，并<b>维持到战斗结束</b>。
    /// </summary>
    private void RandomizeHandCosts()
    {
        foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.ToList())
        {
            // ⚠️ X 费牌跳过：Canonical < 0 表示"费用为 X"。
            if (card.EnergyCost.Canonical < 0)
            {
                continue;
            }

            int cost = Owner.RunState.Rng.CombatEnergyCosts.NextInt(EnergyCostExclusiveMax);
            card.EnergyCost.SetThisCombat(cost);

            // 与 ConfusedPower 同一套视觉反馈。
            NCard.FindOnTable(card)?.PlayRandomizeCostAnim();
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(BlockUpgrade);
        DynamicVars[EnergyKey].UpgradeValueBy(EnergyUpgrade);
    }
}
