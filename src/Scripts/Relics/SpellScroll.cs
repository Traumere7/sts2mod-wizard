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
/// 魔法卷轴（巫师初始遗物）
/// 战斗开始时，将 1 张「思索」加入你的手牌。
///
/// <para>
/// <b>结构照抄观者 mod 的初始遗物「纯净水」<c>PureWater</c></b>
/// （原版「奇迹」/「燃料」都是同一套模式），三处关键点：
/// </para>
///
/// <para>
/// <b>① 钩子用 <c>BeforeHandDraw</c> + <c>TurnNumber == 1</c>。</b>
/// 它发生在<b>抽初始手牌之前</b>，所以这张牌加进去之后引擎还会照常抽满 5 张，
/// 玩家第一回合手上是「思索 + 5 张」。
/// <b>不要用 <c>BeforeCombatStart</c></b> —— 那时虽然也在抽牌前，
/// 但 <c>CombatManager.cs:902</c> 的抽牌数是固定的 <c>5m</c>
/// （只有 <c>Math.Min(handDraw, MaxCardsInHand)</c> 这个上限，不扣已有手牌），
/// 两者其实等价；而 <c>BeforeHandDraw</c> 是原版同类遗物的既有选择，语义更明确。
/// </para>
///
/// <para>
/// <b>② 用 <c>combatState.CreateCard&lt;T&gt;(owner)</c> 生成指定牌。</b>
/// 它内部是 <c>CreateCard(ModelDb.Card&lt;T&gt;(), owner)</c>，
/// 得到的是**属于本次战斗的可变副本**，不会污染原型。
/// </para>
///
/// <para>
/// <b>③ 用 <c>AddGeneratedCardToCombat</c> 而不是普通 <c>Add</c>。</b>
/// 只有前者会写入战斗历史（"生成牌"条目），这也是「回忆」等
/// 依赖历史的卡能正确工作的前提。
/// </para>
///
/// <para>
/// <b>手牌上限</b>：这两条路径都<b>不检查</b> <c>MaxCardsInHand</c>
/// （观者 mod 与 <c>BigHat</c> 也都没查）。第一回合手牌不可能满，
/// 所以此处不额外防御 —— 若将来改成"每回合都给"，就必须补上限判断。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class SpellScroll : ArcaneRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    /// <summary>
    /// 欧洛巴斯之触 <c>TouchOfOrobas</c> 的替换目标：本遗物 → 「死海古卷」。
    ///
    /// <para>
    /// <b>不需要写 Harmony。</b> BaseLib 已有 <c>StarterUpgradePatches</c> ——
    /// 它对原版的 <c>TouchOfOrobas.GetUpgradedStarterRelic</c> 加了
    /// <c>[HarmonyPrefix]</c>：若初始遗物是 <c>CustomRelicModel</c> 子类，
    /// 就取 <see cref="GetUpgradeReplacement"/> 作为结果
    /// （返回 null 时才回落到原版的硬编码表 → <c>Circlet</c>）。
    /// 所以 mod 侧只要重写这个虚方法即可，不重写就会拿到「圆环」兜底。
    /// </para>
    /// </summary>
    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<DeadSeaScrolls>();

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
        HoverTipFactory.FromCard<Ponder>(),
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
            combatState.CreateCard<Ponder>(Owner),
            PileType.Hand,
            Owner,
            CardPilePosition.Top);

        Flash();
    }
}
