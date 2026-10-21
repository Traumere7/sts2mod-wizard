using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Cards;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 死海古卷
/// 战斗开始时，将 1 张「顿悟」加入你的手牌。
///
/// <para>
/// 这是「魔法卷轴」的**替换版本**：由先古之民欧洛巴斯（OROBAS）获得。
/// 效果结构与 <see cref="SpellScroll"/> 完全一致，只是换成更强的「顿悟」。
/// </para>
///
/// <para>
/// ✅ <b>OROBAS 的替换机制已接通</b>（早期这里写着"需要 Harmony 补丁"，已过时）：
/// 原版 <c>TouchOfOrobas.RefinementUpgrades</c> 确实是一张**硬编码的静态表**，
/// 只认原版 5 个初始遗物，查不到会回退成 <c>Circlet</c> ——
/// 但 <b>BaseLib 已经自己给 <c>GetUpgradedStarterRelic</c> 打了 Prefix</b>，
/// 只要 <see cref="SpellScroll"/> 重写
/// <c>CustomRelicModel.GetUpgradeReplacement()</c> 返回本遗物即可，
/// **mod 侧不需要写任何 Harmony**。详见 <c>CONTEXT.md</c> §6.17。
/// </para>
///
/// <para>
/// 仍可用控制台 <c>relic WIZARD-DEAD_SEA_SCROLLS</c> 直接测试效果。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class DeadSeaScrolls : ArcaneRelicModel
{
    /// <summary>先古遗物，不参与普通稀有度抽取。</summary>
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>
    /// 加入的张数 —— 用变量承载，让卡面/工具提示能以 `{Cards}` 引用
    /// （原版遗物约定：数字一律 `[blue]{变量}[/blue]`）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(CardsToAdd),
    ];

    /// <summary>加入的张数（固定 1，原版同类遗物的一致做法）。</summary>
    private const int CardsToAdd = 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Epiphany>(),
    ];

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        await CardPileCmd.AddGeneratedCardToCombat(
            combatState.CreateCard<Epiphany>(Owner),
            PileType.Hand,
            Owner,
            CardPilePosition.Top);

        Flash();
    }
}
