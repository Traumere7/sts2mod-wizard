using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Potions;

/// <summary>
/// 魔力药水 / ManaPotion - Common
/// 获得 3 点法术增益。
///
/// <para>
/// <b>照抄本 mod「静心」<c>CalmMind.cs:47</c> 施加法术增益的写法</b>
/// （<c>PowerCmd.Apply&lt;SpellPowerPower&gt;(ctx, 目标, 层数, applier, cardSource)</c>），
/// 外壳沿用原版药水惯例（<c>AssertValidForTargetedPotion</c> + <c>PlaySplashVfx</c>）。
/// </para>
///
/// <para>
/// ⚠️ <b><c>CombatOnly</c></b>：法术增益是<b>战斗内</b>的能力，地图上喝了会立刻消失，
/// 与设计意图不符。原版所有"给能力"的药水也都是 <c>CombatOnly</c>。
/// </para>
///
/// <para>
/// ⚠️ <b><c>cardSource</c> 传 <c>null</c></b> —— 药水不是卡牌，没有 <c>CardModel</c>。
/// 这会影响"法术牌才加成"之类的判据（<c>SpellPowerPower</c> 本身只在
/// <b>造成伤害</b>时读 cardSource，施加层数不受影响）。
/// </para>
/// </summary>
[Pool(typeof(WizardPotionPool))]
public class ManaPotion : CustomPotionModel
{
    /// <summary>获得的法术增益层数。</summary>
    private const int SpellPowerAmount = 3;

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SpellPowerPower>(SpellPowerAmount),
    ];

    // ⚠️ PotionModel 的这个属性是 **public** virtual（PotionModel.cs:184），
    //    与卡牌/遗物（protected）不同 —— 写成 protected 会 CS0507。
    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public override string? CustomPackedImagePath =>
        "res://Wizard/images/potions/mana_potion.png";

    public override string? CustomPackedOutlinePath =>
        "res://Wizard/images/potions/mana_potion_outline.png";

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);

        // 奥术靛紫 —— 与药水美术的液体色一致。
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("7C52D6"));

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext,
            target,
            DynamicVars[nameof(SpellPowerPower)].BaseValue,
            target,
            null);
    }
}
