using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Potions;

/// <summary>
/// 禁忌之血 / ForbiddenBlood - Rare
/// 回复 15 点生命。当你的生命不足一半时，额外回复 15 点生命。
///
/// <para>
/// <b>照抄原版「血瓶」<c>BloodPotion</c></b>：<c>AnyTime</c> + <c>AnyPlayer</c> +
/// <c>AssertValidForTargetedPotion</c> + <c>PlaySplashVfx</c> + <c>CreatureCmd.Heal</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>"不足一半"用<b>严格小于</b></b>（<c>&lt; 0.5</c>）——
/// 恰好一半<b>不</b>触发额外回复，与设计文档「不足一半」的字面一致。
/// </para>
///
/// <para>
/// ⚠️ <b>在 <c>Heal</c> 之前读 <c>CurrentHp</c></b>：先回 15 再判定的话，
/// 满血边缘的情况会误判（例如 14/30 先回到 29/30，就不再是"不足一半"了，
/// 但设计意图是按<b>喝药那一刻</b>的血量算）。
/// </para>
/// </summary>
[Pool(typeof(WizardPotionPool))]
public class ForbiddenBlood : CustomPotionModel
{
    /// <summary>基础回复量。</summary>
    private const int BaseHeal = 15;

    /// <summary>生命不足一半时的额外回复量。</summary>
    private const int BonusHeal = 15;

    public override PotionRarity Rarity => PotionRarity.Rare;

    /// <summary>战斗内外都可用（回血药水，与原版「血瓶」一致）。</summary>
    public override PotionUsage Usage => PotionUsage.AnyTime;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(BaseHeal),
        new DynamicVar("BonusHeal", BonusHeal),
    ];

    public override string? CustomPackedImagePath =>
        "res://Wizard/images/potions/forbidden_blood.png";

    public override string? CustomPackedOutlinePath =>
        "res://Wizard/images/potions/forbidden_blood_outline.png";

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);

        // 深石榴红 —— 与药水美术的液体色一致。
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("7B1E2B"));

        // ★ 判定必须在回复之前（见类注释）。
        bool belowHalf = target.CurrentHp < target.MaxHp / 2;

        await CreatureCmd.Heal(target, DynamicVars.Heal.BaseValue);

        if (belowHalf)
        {
            await CreatureCmd.Heal(target, DynamicVars["BonusHeal"].BaseValue);
        }
    }
}
