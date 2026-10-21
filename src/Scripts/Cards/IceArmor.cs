using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Cards.Vars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
namespace Wizard.Scripts.Cards;

/// <summary>
/// 冰铠 - 1 费 - 能力 - 稀有
/// 获得 6(9) 层[覆甲]，额外获得等同于**所有敌人（寒冷 + 冻结）层数之和**的覆甲。
///
/// <para>
/// 结构照抄原版「岩石铠甲」<c>StoneArmor</c>：
/// <c>PowerVar&lt;PlatingPower&gt;</c> + <c>PowerCmd.Apply&lt;PlatingPower&gt;</c>，
/// 升级用 <c>UpgradeValueBy</c>。
/// </para>
///
/// <para>
/// <b>「覆甲」<c>PlatingPower</c> 是原版能力</b>：回合结束时获得等同层数的格挡，
/// 回合开始时减少 1 层。所以本牌给的是<b>持续若干回合的护甲</b>，不是一次性格挡。
/// </para>
///
/// <para>
/// <b>卡面动态值</b>见 <see cref="IceArmorVar"/>：手里显示"基础 + 寒冷与冻结总数"的最终值
/// （有加成时变绿），卡组/卡牌库里显示基础值。
/// <b>但结算不读它</b> —— 见 <see cref="OnPlay"/> 的注释。
/// </para>
///
/// <para>
/// ⚠️ <b>冻结也算入</b>（2026-09-25 增强）：寒冷与冻结<b>等价</b>，每层各 +1 覆甲。
/// 本类的 <see cref="GetTotalEnemyChillAndFreeze"/> 与
/// <see cref="IceArmorVar"/> 里的同名求和是**两份独立实现**（刻意如此），
/// 改口径时两处都要改。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class IceArmor : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>基础覆甲层数（升级 +3 → 9）。</summary>
    private const decimal BasePlating = 6m;

    /// <summary>升级时基础覆甲的增量。</summary>
    private const decimal UpgradeStep = 3m;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<PlatingPower>(),
        HoverTipFactory.FromPower<ChillPower>(),
        HoverTipFactory.FromPower<FreezePower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 算式：IceArmor = CalculationBase + CalculationExtra × 寒冷总数
        new CalculationBaseVar(BasePlating),
        new CalculationExtraVar(1m),

        // 卡面显示值（变量名 "IceArmor"）：手里是最终值、卡组里是基础值。
        new IceArmorVar(),

        // 实际施加的量：基础覆甲。结算专用，不参与卡面展示。
        new PowerVar<PlatingPower>(BasePlating),
    ];

    public IceArmor()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ⚠️ 结算**自己算**，不读卡面展示变量 ——
        // 展示变量只在卡面刷新时更新，与"真正给多少"应当各自独立可验证
        // （这与「治疗术」HealingSpell 同一条原则）。
        decimal plating = DynamicVars[nameof(PlatingPower)].BaseValue + GetTotalEnemyChillAndFreeze();

        if (plating <= 0m)
        {
            return;
        }

        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Owner.Creature,
            plating,
            Owner.Creature,
            this);
    }

    /// <summary>
    /// 场上所有存活敌人的（寒冷 + 冻结）层数之和。
    ///
    /// <para>
    /// ⚠️ 与 <see cref="IceArmorVar"/> 里的求和是两份独立实现 ——
    /// 这是刻意的（卡面展示 vs 实际结算各自独立可验证），
    /// 所以<b>改口径时必须两处同步改</b>。
    /// </para>
    /// </summary>
    private decimal GetTotalEnemyChillAndFreeze()
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState == null)
        {
            return 0m;
        }

        return combatState
            .GetOpponentsOf(Owner.Creature)
            .Where(c => c.IsAlive)
            .Sum(c => c.GetPowerAmount<ChillPower>() + c.GetPowerAmount<FreezePower>());
    }

    protected override void OnUpgrade()
    {
        // 基础覆甲 6 → 9。两个变量都要改：
        //   CalculationBase 决定卡面算式，PlatingPower 决定实际施加量。
        DynamicVars.CalculationBase.UpgradeValueBy(UpgradeStep);
        DynamicVars[nameof(PlatingPower)].UpgradeValueBy(UpgradeStep);
    }
}
