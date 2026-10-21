using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Potions;

/// <summary>
/// 真理药水 / TruthPotion - Uncommon
/// 抽 3 张牌，选最多 3 张牌消耗。
///
/// <para>
/// <b>两个动作各有原版模板：</b>
/// <list type="bullet">
///   <item><b>抽 3 张</b> —— 原版「迅捷药水」<c>SwiftPotion</c>：
///         <c>CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, target.Player)</c></item>
///   <item><b>选最多 3 张消耗</b> —— 原版「灰水」<c>Ashwater</c>：
///         <c>CardSelectCmd.FromHand(new CardSelectorPrefs(SelectionScreenPrompt, 0, 3), ...)</c>
///         然后逐张 <c>CardCmd.Exhaust</c>。</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>必须 <c>CombatOnly</c></b>：<c>CardPileCmd.Draw</c> 与手牌选择都只存在于战斗中，
/// 在地图上喝会出问题。
/// </para>
///
/// <para>
/// ⚠️ <b>选择数量是 <c>(0, 3)</c> 而不是 <c>(3, 3)</c></b>：设计文档写的是
/// "<b>选最多</b> 3 张"，即**可以少选、也可以一张都不选**。
/// <c>CardSelectorPrefs(prompt, minCount, maxCount)</c> 里 <c>minCount = 0</c> 表示允许空选。
/// （对比原版 <c>Ashwater</c> 用 <c>(0, 999999999)</c> 表示"任意张"。）
/// </para>
///
/// <para>
/// ⚠️ <b>先抽牌、再开选择界面</b> —— 设计文档的语序就是"抽3张牌，选最多3张牌消耗"，
/// 而且刚抽到的牌也应该能被消耗。
/// </para>
///
/// <para>
/// ⚠️ <b><c>selectionScreenPrompt</c> 本地化键是必需的</b>：
/// <c>PotionModel.SelectionScreenPrompt</c> =
/// <c>new LocString("potions", Id.Entry + ".selectionScreenPrompt")</c>。
/// <b>缺键会抛异常</b>（本 mod 在卡牌侧踩过同样的坑），所以
/// <c>potions.json</c> 里必须有
/// <c>WIZARD-TRUTH_POTION.selectionScreenPrompt</c>。
/// </para>
/// </summary>
[Pool(typeof(WizardPotionPool))]
public class TruthPotion : CustomPotionModel
{
    /// <summary>抽牌数。</summary>
    private const int CardsToDraw = 3;

    /// <summary>最多可消耗的张数。</summary>
    private const int MaxExhaust = 3;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(CardsToDraw),
        new DynamicVar("MaxExhaust", MaxExhaust),
    ];

    // ⚠️ PotionModel 的这个属性是 **public** virtual（PotionModel.cs:184），
    //    与卡牌/遗物（protected）不同 —— 写成 protected 会 CS0507。
    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    public override string? CustomPackedImagePath =>
        "res://Wizard/images/potions/truth_potion.png";

    public override string? CustomPackedOutlinePath =>
        "res://Wizard/images/potions/truth_potion_outline.png";

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);

        // AssertValidForTargetedPotion 在运行时保证 target 非空且是玩家，
        // 但它的签名没有 [NotNull] 标注（原版 PowerPotion 直接解引用是靠编译器
        // 不做流分析）。这里显式收窄：既消掉 CS8600/CS8604，
        // 也把"玩家为空"这种不可能情况变成明确的提前返回，而不是 NRE。
        if (target?.Player is not { } player)
        {
            return;
        }

        // 亮蓝偏青 —— 与药水美术的液体色一致（刻意避开雷电的蓝紫）。
        NCombatRoom.Instance?.PlaySplashVfx(target, new Color("3FB8D8"));

        // ① 先抽牌，让新抽到的牌也能参与下面的选择。
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);

        // ② 选最多 MaxExhaust 张消耗（minCount = 0 ⇒ 允许空选）。
        IEnumerable<CardModel> chosen = await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(
                SelectionScreenPrompt,
                minCount: 0,
                maxCount: MaxExhaust),
            filter: null,
            source: this);

        foreach (CardModel card in chosen)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }
    }
}
