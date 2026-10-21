using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 快速咏唱 / QuickChant - 0 费 - 技能 - 普通
/// 在本回合获得 2(4) 点[purple]法术增益[/purple]。
///
/// <para>
/// 效果由 <see cref="TemporarySpellPowerPower"/> 承载 ——
/// 它**先真正加上法术增益，回合结束时再扣回去**（照抄原版
/// 「热修复」<c>Hotfix</c> / 「预判」<c>Anticipate</c> 的临时属性机制）。
/// </para>
///
/// <para>
/// ⚠️ <b>那颗临时标记是 <c>Debuff</c> 类型，这是有意的</b>：
/// 于是「高温消毒」可以把标记摘掉，"扣回法术增益"的代码随之消失，
/// **临时增益就变成了永久增益**。详见 <see cref="TemporarySpellPowerPower"/> 的类注释。
/// </para>
///
/// <para>
/// <b>本牌不加「消耗」</b>：设计文档没写，且 0 费 + 临时收益本身已经平衡
/// （想固化就得再花一张「高温消毒」的牌与费用）。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class QuickChant : WizardCardModel
{
    private const int Cost = 0;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 本回合临时获得的法术增益（升级 2 → 4）
        new PowerVar<SpellPowerPower>(2m),
    ];

    public QuickChant()
        : base(Cost, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<TemporarySpellPowerPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(SpellPowerPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 2 → 4
        DynamicVars[nameof(SpellPowerPower)].UpgradeValueBy(2m);
    }
}
