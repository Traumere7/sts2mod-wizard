using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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

        IReadOnlyList<CardPileAddResult> results = await CardPileCmd.AddGeneratedCardsToCombat(
            sparks,
            PileType.Hand,
            Owner);

        // ★ 飞牌演出 + 手牌计数即时刷新（与「梦魇」同款；这里是一次多张，用列表重载）。
        CardCmd.PreviewCardPileAdd(results);
    }

    /// <summary>升级：火花数 3 → 4。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
    }
}
