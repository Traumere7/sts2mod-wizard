using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 水银 - 1 费 - 能力 - 罕见
/// 你每回合生成的前 1(2) 张牌获得附魔：迅速 2。
///
/// <para>
/// 层数 = 每回合可附魔张数（<c>PowerStackType.Counter</c>，多张相加）。升级 +1 张。
/// 能力逻辑见 <see cref="MercuryPower"/>。
/// </para>
///
/// <para>
/// 悬停：只挂「迅速」附魔（<c>FromEnchantment&lt;Swift&gt;(2)</c>，原版工厂方法）；不挂本牌能力。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Mercury : WizardCardModel
{
    private const int Cost = 1;

    private const string PowerKey = nameof(MercuryPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MercuryPower>(1m),
    ];

    /// <summary>
    /// ⚠️ 只解释「迅速」附魔，**不挂本牌自己的能力**（用户 2026-11-13 明确：
    /// 卡面不需要「水银」buff 的解释，与「凝神/回忆」同一条口径）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        // FromEnchantment 返回的是 IEnumerable，用集合展开铺进来。
        .. HoverTipFactory.FromEnchantment<Swift>(MercuryPower.SwiftAmount),
    ];

    public Mercury()
        : base(Cost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<MercuryPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[PowerKey].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // 每回合 1 张 → 2 张
        DynamicVars[PowerKey].UpgradeValueBy(1m);
    }
}
