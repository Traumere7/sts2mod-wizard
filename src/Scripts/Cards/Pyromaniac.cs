using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 纵火狂 - 2 费 - 能力 - 罕见
/// **免疫燃烧。每当你给予燃烧时，获得 4(6) 点活力。**
///
/// <para>
/// 本牌给**两个**效果：
/// <list type="bullet">
///   <item>「燃烧免疫」<see cref="BurnImmunityPower"/> ——
///         获得时清除身上已有燃烧，并阻止后续对自己的燃烧施加。
///         这是**原版没有对应物**的效果，实现见该类的注释。</item>
///   <item>「纵火狂」<see cref="PyromaniacPower"/> ——
///         叠层 = 每次给的活力变多（4 → 6）。</item>
/// </list>
/// </para>
///
/// <para>
/// 「活力」<see cref="VigorPower"/> 是原版机制（下一次攻击加算这些伤害，
/// 之后清零），参考原版「地形改造」<c>Terraforming</c>。
/// </para>
///
/// <para>
/// <b>升级改的是"每次获得的活力"</b>（4 → 6），不是触发次数。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Pyromaniac : WizardCardModel
{
    private const int Cost = 2;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BurnImmunityPower>(),
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<BurnPower>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每次给予燃烧时获得的活力点数。两张纵火狂 = 引擎层数累加 → 8。
        new PowerVar<PyromaniacPower>(4m),
    ];

    public Pyromaniac()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 本牌给**两个**效果：
        //   ① 「燃烧免疫」—— 布尔状态，不叠层
        //   ② 「纵火狂」  —— 叠层 = 每次给的活力变多
        //
        // ⚠️ 顺序有讲究：**先**上免疫。
        //    免疫的 AfterApplied 会清除玩家身上已有的燃烧；
        //    若反过来，先上的纵火狂会先订阅 BurnApplied，逻辑上无碍，
        //    但"先免疫再开火"读起来更符合直觉，也避免未来加"获得时触发"
        //    类效果时踩到顺序坑。
        await PowerCmd.Apply<BurnImmunityPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        await PowerCmd.Apply<PyromaniacPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(PyromaniacPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 每次给的活力 4 → 6
        DynamicVars[nameof(PyromaniacPower)].UpgradeValueBy(2m);
    }
}
