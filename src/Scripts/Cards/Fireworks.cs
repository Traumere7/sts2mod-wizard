using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 烟花 / Fireworks - 1 费 - 罕见 - 技能
/// 添加 3(4) 张「火花」到你的手牌。
///
/// <para>
/// 生成路径照抄原版「亮起来」<c>BrightenUp</c> / 「刀」<c>Shiv</c>：
/// <c>CombatState.CreateCard&lt;Spark&gt;(Owner)</c> 建战斗实例 →
/// <c>CardPileCmd.AddGeneratedCardsToCombat(..., PileType.Hand, Owner)</c>（自动记
/// <c>CardGenerated</c> 历史并发 <c>AfterCardGeneratedForCombat</c>）→ 飞牌演出。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Fireworks : WizardCardModel
{
    private const int Cost = 1;

    private const int SparkCount = 3;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(SparkCount),
    ];

    /// <summary>
    /// 文案里写了「火花」，挂上它的悬停（照「第四元素」<c>HoverTipFactory.FromCard&lt;AetherArrow&gt;()</c> 的写法）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Spark>(),
    ];

    public Fireworks()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);

        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        int count = DynamicVars.Cards.IntValue;
        if (count <= 0)
        {
            return;
        }

        List<CardModel> sparks = [];
        for (int i = 0; i < count; i++)
        {
            sparks.Add(combatState.CreateCard<Spark>(Owner));
        }

        // ⚠️ 不加 CardCmd.PreviewCardPileAdd —— 那会在屏幕中央生成"特写"卡牌再飞向手牌；
        // 用户 2026-11-28 明确：烟花生成火花时不需要这个特写，直接进手牌即可。
        await CardPileCmd.AddGeneratedCardsToCombat(
            sparks,
            PileType.Hand,
            Owner);
    }

    /// <summary>升级：火花数 3 → 4。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
    }
}
