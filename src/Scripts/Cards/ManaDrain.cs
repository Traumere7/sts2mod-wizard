using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Cards.Vars;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 魔力汲取 - 1(0) 费 - 技能 - 罕见
/// 目标敌人具有的负面效果每存在 1 种，获得 1 点能量。
///
/// <para>
/// <b>卡面用 <c>{CalculatedEnergy:energyIcons()}</c>，不是 <c>{Energy:energyIcons()}</c>。</b>
/// 前者是动态量（随目标变化），后者是固定量。
/// 写成 <c>{Energy:...}</c> 会永远显示 <c>EnergyVar</c> 的固定值，与目标无关。
/// </para>
///
/// <para>
/// <b>"1 种"的口径</b>：按 <c>PowerModel.Id</c> 去重后计数。
/// 引擎没有"负面效果种类"这个现成概念，只有一个个 power 实例，
/// 所以过滤 <c>PowerType.Debuff</c> 再按 Id 去重 ——
/// 同一个 power 无论叠了多少层，都只算 1 种。
/// </para>
///
/// <para>
/// <b>未指定目标时倍率为 0</b>（<c>target?.… ?? 0</c>），
/// 卡面于是显示基础值 0，符合"未指定敌人时 x 默认为 0"的要求。
/// 按住牌指向敌人时，引擎会调
/// <c>DynamicVar.UpdateCardPreview(card, mode, target, …)</c>，
/// <c>CalculatedVar</c> 据此重算 <c>PreviewValue</c>，卡面实时变成
/// "（获得 x 能量）"。参考原版「蜃景」<c>Mirage</c> / 「撕裂」<c>Rend</c>。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ManaDrain : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>能量图标需要的字符色前缀由 EnergyIconsFormatter 自动取角色色，这里不用管。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // CalculatedVar 依赖这两个变量：值 = Base + Extra × 倍率
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedEnergyVar().WithMultiplier(
            (CardModel _, Creature? target) => CountDistinctDebuffs(target)),
    ];

    public ManaDrain()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));

        // 用与卡面完全相同的计算，保证"显示多少就真给多少"。
        int energy = CountDistinctDebuffs(cardPlay.Target);
        if (energy <= 0)
        {
            return;
        }

        await PlayerCmd.GainEnergy(energy, Owner);
    }

    /// <summary>目标身上不同种类负面效果的数量（按 <c>Id</c> 去重）。</summary>
    private static int CountDistinctDebuffs(Creature? target)
    {
        if (target is null)
        {
            return 0;
        }

        return target.Powers
            .Where(p => p.Type == PowerType.Debuff)
            .Select(p => p.Id)
            .Distinct()
            .Count();
    }

    protected override void OnUpgrade()
    {
        // 1 费 → 0 费。费用变化由卡面自动显示，不需要 DynamicVar。
        EnergyCost.UpgradeBy(-1);
    }
}
