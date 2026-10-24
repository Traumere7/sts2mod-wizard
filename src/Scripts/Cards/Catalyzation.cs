using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 催化 / Catalyzation - 1 费 - 能力 - 罕见
/// 你每生成 4(3) 张卡牌，就获得 1 费。
///
/// <para>
/// <b>效果全在 <see cref="CatalyzationPower"/></b> 里；本牌只负责施加能力，
/// 并把"阈值"（4 / 升级后 3）传进去。
/// </para>
///
/// <para>
/// ⚠️ <b>阈值必须先 <c>Apply</c> 再 <c>SetThreshold</c></b>：
/// <c>PowerCmd.Apply</c> 的 <c>amount</c> 参数是能力的 <c>Amount</c>，
/// 而 <c>Amount</c> 在本能力里表示"每次给几点能量"（固定 1），
/// **不是**阈值。阈值走单独的 setter
/// （同原版「夜魇」<c>Nightmare</c> → <c>NightmarePower.SetSelectedCard</c> 的做法）。
/// </para>
///
/// <para>
/// <b>每打一张都是独立能力实例</b>（<c>PowerInstanceType.Instanced</c>），
/// 所以打两张就是两个独立计数器 —— 详见 <see cref="CatalyzationPower"/> 的类注释。
/// </para>
///
/// <para>
/// <b>升级只降阈值 4 → 3</b>（design.md 写的是「4（3）」），费用不变。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Catalyzation : WizardCardModel
{
    private const int Cost = 1;

    /// <summary>未升级时的触发阈值。</summary>
    private const int CardsPerTrigger = 4;

    /// <summary>每次触发给多少能量（design 固定 1）。</summary>
    private const int EnergyPerTrigger = 1;

    private const string CardsKey = "Cards";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        base.EnergyHoverTip,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(CardsPerTrigger),
    ];

    public Catalyzation()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CatalyzationPower? power = await PowerCmd.Apply<CatalyzationPower>(
            choiceContext,
            Owner.Creature,
            EnergyPerTrigger,
            Owner.Creature,
            this);

        // ⚠️ 阈值不能走 amount（那是"每次给几点能量"），Apply 之后再塞进去。
        power?.SetThreshold(DynamicVars[CardsKey].IntValue);
    }

    /// <summary>升级：阈值 4 → 3（费用不变）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[CardsKey].UpgradeValueBy(-1m);
    }
}
