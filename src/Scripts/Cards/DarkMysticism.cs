using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 暗黑神秘学 - 3 费 - 技能 - 稀有
/// 将随机（升级过的）攻击、技能、能力牌各 1 张加入你的手牌，
/// 这些牌获得「消耗」，在本回合可免费打出。
///
/// <para>
/// 生成方式照抄原版「新生之喜」<c>BundleOfJoy</c> 与「飞溅」<c>Splash</c>：
/// <c>CardFactory.GetDistinctForCombat</c> 取牌（它内部会
/// <c>CombatState.CreateCard</c>，拿到的是<b>属于本次战斗的新实例</b>，
/// 不会污染原型的 <c>IsUpgraded</c>），再
/// <c>CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner)</c>。
/// </para>
///
/// <para>
/// <b>「消耗」只加给攻击/技能，不加给能力牌</b> —— 能力牌打出后进能力区、
/// 本来就不回牌堆循环，标「消耗」是冗余且会让卡面多一个误导性标签。
/// 这是设计文档的定案（「甲」案）。
/// </para>
///
/// <para>
/// <b>升级要升级生成出来的实例，不是原型。</b>
/// <c>CardCmd.Upgrade</c> 默认会在屏幕上做卡片预览动画，
/// 这里用 <c>CardPreviewStyle.None</c> 静默升级 —— 否则会连弹三张预览。
/// 它内部会检查 <c>IsUpgradable</c>，重复调用是安全的。
/// </para>
///
/// <para>
/// <b>免费打出用 <c>SetToFreeThisTurn()</c></b>（照抄 <c>Splash</c>），
/// 只在<b>本回合</b>有效；若这些牌本回合没打出去，下回合恢复原费。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class DarkMysticism : WizardCardModel
{
    private const int Cost = 3;

    /// <summary>要生成的三类牌。</summary>
    private static readonly CardType[] GeneratedTypes =
    [
        CardType.Attack,
        CardType.Skill,
        CardType.Power,
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public DarkMysticism()
        : base(Cost, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState == null)
        {
            return;
        }

        // 卡池快照：巫师的已解锁卡，供按类型筛选。
        List<CardModel> pool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .ToList();

        foreach (CardType type in GeneratedTypes)
        {
            CardModel? generated = CardFactory
                .GetDistinctForCombat(
                    Owner,
                    pool.Where(c => c.Type == type),
                    1,
                    Owner.RunState.Rng.CombatCardGeneration)
                .FirstOrDefault();

            if (generated == null)
            {
                // 该类型没有任何可生成牌（理论上不会发生）。
                continue;
            }

            // 升级效果作用于"生成出来的实例"。
            if (IsUpgraded)
            {
                CardCmd.Upgrade(generated, CardPreviewStyle.None);
            }

            // 能力牌不加「消耗」（它打出后进能力区，本来就不回牌堆）。
            if (type != CardType.Power)
            {
                generated.AddKeyword(CardKeyword.Exhaust);
            }

            generated.SetToFreeThisTurn();

            await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级效果是"生成的牌也是升级过的"，没有可升级的数值。
        // 实际分支在 OnPlay 里读 IsUpgraded。
    }
}
