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
/// ⚠️ <b>升级牌与未升级牌挂的是两个不同的能力模型</b>（用户 2026-11-09 定案）：
/// <list type="bullet">
///   <item>未升级 → <see cref="CaudaPavonisPower"/>（每次 2 层）；</item>
///   <item>升级   → <see cref="CaudaPavonisUpgradedPower"/>（每次 3 层）。</item>
/// </list>
/// 两者是不同 <c>ModelId</c> 的独立 buff，各自叠层：打两张未升级 = 一个图标 n=2，
/// 打两张升级 = 另一个图标 n=2，各打一张 = 两个图标各 n=1。
/// 卡面上的 <c>{Layers:diff()}</c>（2→3）只是<b>展示</b>，真正生效的层数由能力类自己的
/// <c>LayersPerTrigger</c> 决定。
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
        // 每次给几层负面效果（仅用于卡面文案；实际层数由能力类决定）。
        new PowerVar<CaudaPavonisPower>(1m),
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
        // 每次打出 +1 层 = 结算次数 n；层数（每次给几层）由能力类自己定，
        // 升级过的牌挂的是另一个能力模型，因此两个 buff 各自叠层、互不干扰。
        if (IsUpgraded)
        {
            await PowerCmd.Apply<CaudaPavonisUpgradedPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }
        else
        {
            await PowerCmd.Apply<CaudaPavonisPower>(
                choiceContext,
                Owner.Creature,
                1m,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        // 每次给 2 → 3 层（结算次数不变）。
        DynamicVars[LayersKey].UpgradeValueBy(LayersUpgrade);
    }
}
