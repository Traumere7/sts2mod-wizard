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
/// 大寒波 - 1(0) 费 - 技能 - 罕见
/// 打出此牌后，你在本回合内每打出一张牌，就给予所有敌人 1 层寒冷。
///
/// <para>
/// 效果由 <see cref="ColdWavePower"/> 承载。升级只降费（1 → 0），
/// 所以 <c>OnUpgrade</c> 用 <c>EnergyCost.UpgradeBy(-1)</c>
/// （与「环流」<see cref="Circulation"/> 同一个写法）。
/// </para>
///
/// <para>
/// ⚠️ <b>时序是本牌的关键</b>，详见 <see cref="ColdWavePower"/> 的类注释：
/// 每张牌是在 <c>OnPlay</c>（伤害结算）**之前**挂寒冷的，
/// 所以本回合打出的火焰/雷电攻击**当次就能吃到寒冷 +50%**。
/// 这是"打出此牌后…每打出一张牌"的正确读法 ——
/// 寒冷先落地，紧接着的这次伤害才享受加成。
/// </para>
///
/// <para>
/// <b>它不是法术</b>（不造成伤害），所以继承 <see cref="WizardCardModel"/>
/// 而不是 <c>WizardSpellCard</c>，与「冰之城墙」「震慑」同一做法。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class ColdWave : WizardCardModel
{
    private const int Cost = 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每张牌给的寒冷层数。层数语义 = 每次给几层（见 ColdWavePower 注释）。
        new PowerVar<ColdWavePower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ChillPower>(),
    ];

    public ColdWave()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ColdWavePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(ColdWavePower)].BaseValue,
            Owner.Creature,
            this);
    }

    /// <summary>升级 1 费 → 0 费（照抄「环流」<c>Circulation</c>）。</summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
