using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 不灭 / Undying - 1(0) 费 - 罕见 - 技能
/// 下回合获得等同于你在本回合生成卡牌数量的能量。
///
/// <para>
/// 本卡只负责"施加 1 层 <see cref="UndyingPower"/>"；计数、动态数值与结算全在能力里
/// （见 <see cref="UndyingPower"/> 的类注释）。
/// 升级只降费 1 → 0，不加层数。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Undying : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>卡面"本回合已生成 x 张牌"的变量名（文案写 <c>{GeneratedCards:diff()}</c>）。</summary>
    private const string GeneratedKey = "GeneratedCards";

    /// <summary>每张「不灭」施加 1 层（多张叠加 = 多结算几次）。</summary>
    private const decimal StacksPerPlay = 1m;

    /// <summary>
    /// 卡面额外一行「（本回合已生成 x 张牌）」：
    /// x 用 <see cref="CalculatedVar"/> 现算（= <see cref="GenerationTally"/>），
    /// 与「降雷」「魔力汲取」同一套写法；<c>{InCombat:...}</c> 保证只在战斗内显示。
    ///
    /// <para>
    /// ⚠️ <c>CalculationBaseVar</c> + <c>CalculationExtraVar</c> 是
    /// <see cref="CalculatedVar"/> 的硬前置（缺了会在 <c>ToMutable()</c> 抛），
    /// 值 = Base + Extra × 倍率（这里 Base=0、Extra=1，倍率就是"生成数"）。
    /// </para>
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar(GeneratedKey)
            .WithMultiplier(static (CardModel card, Creature? _) => GenerationTally.Get(card.Owner)),
    ];

    /// <summary>文案里写了"获得能量"，挂能量悬停（与「决心」等一致）。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        base.EnergyHoverTip,
    ];

    public Undying()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    /// <summary>
    /// 本回合每生成一张牌就刷新一次卡面（手牌中的这张牌要实时显示新数字）。
    /// </summary>
    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator == Owner)
        {
            RefreshDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>新回合开始（计数清零）时刷新一次卡面。</summary>
    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player)
        {
            RefreshDisplay();
        }

        return Task.CompletedTask;
    }

    /// <summary>找手牌里的这张牌并重绘（找不到/不在战斗内就静默跳过）。</summary>
    private void RefreshDisplay()
    {
        try
        {
            if (Pile?.Type != PileType.Hand)
            {
                return;
            }

            NCard.FindOnTable(this, PileType.Hand)?.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        }
        catch (Exception e)
        {
            Log.Warn($"[Wizard/Undying] 刷新卡面失败（不影响结算）：{e.Message}");
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        await PowerCmd.Apply<UndyingPower>(
            choiceContext,
            Owner.Creature,
            StacksPerPlay,
            Owner.Creature,
            this);
    }

    /// <summary>升级：费用 1 → 0（层数不变）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
