using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Utils;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 点灯 / LightTheLamp - 1 费 - 普通 - 技能
/// 获得 6(8) 点格挡。将一张手牌变化为「火花」。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 选 1 张手牌 → 变化成「火花」
/// ══════════════════════════════════════════════════════════════════
/// 用 <c>CardCmd.Transform</c> 的"指定替代"路径（与「嬗变」同一套落位/演出机制）：
/// <code>
/// CardTransformation(picked, spark)   // spark 是 CombatState.CreateCard&lt;Spark&gt;(Owner) 新建的战斗实例
/// </code>
/// **生成语义由引擎负责**：<c>CardCmd.Transform</c> 会写 <c>CardGenerated</c> 历史，
/// 并在末尾对战斗内替代牌发 <c>AfterCardGeneratedForCombat</c>（<c>CardCmd.cs:541</c>），
/// 不要再手动补一次（2026-11-28 修正：那会双倍计数）。
/// </para>
///
/// <para>
/// ⚠️ 候选过滤用 <see cref="Transmutation.CanTransmute"/>（= <c>IsTransformable</c>）：
/// 永恒/不可移除的牌不能变；候选为空时只拿格挡、不弹选牌界面。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class LightTheLamp : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>「选择一张手牌变化为火花」的提示语（<c>card_selection</c> 表）。</summary>
    private static LocString SparkSelectionPrompt =>
        new("card_selection", "WIZARD-TO_SPARK");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move),
    ];

    /// <summary>
    /// 文案里写了「火花」，挂上它的悬停（照「第四元素」<c>HoverTipFactory.FromCard&lt;AetherArrow&gt;()</c> 的写法）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Spark>(),
    ];

    public LightTheLamp()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        // ① 先获得格挡（即使后面没有可变化的牌，格挡也照拿）。
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        // ② 没有合法候选就不弹选牌界面。
        bool anyCandidate = PileType.Hand.GetPile(Owner).Cards.Any(Transmutation.CanTransmute);
        if (!anyCandidate)
        {
            return;
        }

        CardModel? picked = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SparkSelectionPrompt, 1),
            Transmutation.CanTransmute,
            this)).FirstOrDefault();

        if (picked == null || !picked.IsTransformable)
        {
            return;
        }

        // ③ 变化成火花（新建战斗实例，指定替代）。
        Spark spark = combatState.CreateCard<Spark>(Owner);
        // CardCmd.Transform 自带 History.CardGenerated + AfterCardGeneratedForCombat
        // （CardCmd.cs:541），不需要再手动补"生成"——补了会双倍计数。
        await CardCmd.Transform(
            [new CardTransformation(picked, spark)],
            Owner.RunState.Rng.CombatCardSelection);
    }

    /// <summary>升级：格挡 6 → 8。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
