using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 孔雀之尾 - 1 费 - 能力 - 稀有
/// 你每生成 1 张卡牌，就给予随机敌人 2(3) 层[随机负面效果]。
///
/// <para>
/// 「随机负面效果」的候选与"优先给未拥有的"逻辑见
/// <see cref="RandomDebuff"/>（悬停提示用 <see cref="WizardTips.RandomDebuff"/>）。
/// </para>
///
/// <para>
/// ⚠️ 两个数值各有归属（见 <see cref="CaudaPavonisPower"/> 的注释）：
/// <list type="bullet">
///   <item><c>PowerVar&lt;CaudaPavonisPower&gt;(1)</c> —— 每次打出 +1 层，层数 = <b>每次触发结算几次</b>（n）；</item>
///   <item><c>Layers</c> 变量（2 / 升级后 3）—— <b>每次给几层负面效果</b>，Apply 之后写进能力里。</item>
/// </list>
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class CaudaPavonis : WizardCardModel
{
    private const int Cost = 1;

    private const decimal BaseLayers = 2m;

    private const decimal LayersUpgrade = 1m;

    private const string LayersKey = CaudaPavonisPower.LayersKey;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 每次打出 +1 层（= "结算几次"里的 n）。
        new PowerVar<CaudaPavonisPower>(1m),

        // 每次给几层负面效果。
        new DynamicVar(LayersKey, BaseLayers),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WizardTips.RandomDebuff,
    ];

    public CaudaPavonis()
        : base(Cost, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CaudaPavonisPower? power = await PowerCmd.Apply<CaudaPavonisPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        // "每次给几层"不能走 Amount（那是 n），Apply 之后再塞进去。
        power?.SetLayers((int)DynamicVars[LayersKey].BaseValue);
    }

    protected override void OnUpgrade()
    {
        // 每次给 2 → 3 层（结算次数不变）。
        DynamicVars[LayersKey].UpgradeValueBy(LayersUpgrade);
    }
}
