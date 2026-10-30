using System.Linq;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 启示 / Inspiration - 0 费 - 技能 - 罕见
/// 从 3 张随机（升级过的）其他颜色的技能牌中选择 1 张加入你的手牌。消耗。
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 结构照抄原版「类星体」<c>Quasar</c>
/// ══════════════════════════════════════════════════════════════════
/// <c>Quasar.cs</c> 的写法（本牌只是把"无色牌"换成"其他颜色的技能牌"）：
/// <code>
/// List&lt;CardModel&gt; cards = CardFactory.GetDistinctForCombat(
///     Owner, pool, 3, Owner.RunState.Rng.CombatCardGeneration).ToList();
/// if (IsUpgraded) CardCmd.Upgrade(cards, CardPreviewStyle.HorizontalLayout);
/// CardModel card = await CardSelectCmd.FromChooseACardScreen(ctx, cards, Owner, canSkip: true);
/// if (card != null) await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
/// </code>
/// 两点照抄的细节：
/// <list type="bullet">
///   <item><b>取牌用 <c>GetDistinctForCombat</c></b>：它内部会
///         <c>CombatState.CreateCard</c>，拿到的是<b>属于本次战斗的新实例</b>；
///         并且已经替我们做了"按人数过滤 + <c>FilterForCombat</c>"两件事。</item>
///   <item><b>升级要升级"生成出来的实例"</b>（<c>if (IsUpgraded)</c> 那段），
///         不是改原型。这里用 <c>CardPreviewStyle.None</c> 静默升级
///         （同「暗黑神秘学」），否则会连弹三张升级预览。</item>
/// </list>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「其他颜色」= 红/绿/蓝/橙/紫/灰 <b>六个池子，显式列出</b>
/// ══════════════════════════════════════════════════════════════════
/// 用户 2026-10-30 的口径：*"除了巫师以外的任意常规可获得卡池，
/// 包括红（铁甲战士）/绿（静默猎手）/蓝（故障机器人）/橙（储君）/紫（亡灵契约师）/灰（无色牌）"*。
/// <code>
/// IroncladCardPool   红  铁甲战士
/// SilentCardPool     绿  静默猎手
/// DefectCardPool     蓝  故障机器人
/// RegentCardPool     橙  储君
/// NecrobinderCardPool 紫 亡灵契约师
/// ColorlessCardPool  灰  无色牌
/// </code>
/// ⚠️ <b>显式列池子，而不是"用 <c>ModelDb.AllCardPools</c> 减去自己"</b>：
/// 后者会把 <c>CurseCardPool</c> / <c>StatusCardPool</c> / <c>EventCardPool</c> /
/// <c>TokenCardPool</c> / <c>QuestCardPool</c> / <c>DeprecatedCardPool</c> 一起混进来
/// （诅咒、状态、事件专属、衍生物、弃用牌都不该出现在"选择"里）。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「生成守则」的排除<b>不用自己写</b> —— <c>FilterForCombat</c> 已经做了
/// ══════════════════════════════════════════════════════════════════
/// <c>CardFactory.FilterForCombat</c>（<c>CardFactory.cs:159-162</c>）：
/// <code>
/// cards.Where(c =&gt; c.CanBeGeneratedInCombat
///               &amp;&amp; c.Rarity != CardRarity.Basic
///               &amp;&amp; c.Rarity != CardRarity.Ancient
///               &amp;&amp; c.Rarity != CardRarity.Event).Distinct();
/// </code>
/// 而 <c>GetDistinctForCombat</c> 内部就调它（<c>:127</c>）。
/// 用户点名要排除的几张牌，原版<b>自己就标了 <c>CanBeGeneratedInCombat =&gt; false</c></b>：
/// <list type="bullet">
///   <item>贪婪之手 <c>HandOfGreed</c></item>
///   <item>炼制药水 <c>Alchemize</c></item>
///   <item>狂宴 <c>Feed</c>（获得生命上限）</item>
///   <item>时候未到 <c>NotYet</c>（回复生命）</item>
///   <item>王国资产 <c>Royalties</c></item>
/// </list>
/// 所以只要走 <c>GetDistinctForCombat</c> 就自动符合守则，**不要再手写一份黑名单**
/// （原版以后新增的同类牌也会自动被排除）。
/// </para>
///
/// <para>
/// ⚠️ <b>蓝（故障机器人）的球体系列牌拿到手不会崩，但也不会有用</b>：
/// <c>PlayerCombatState.OrbQueue</c> 对每个玩家都会创建
/// （<c>PlayerCombatState.cs:137-139</c>，容量 = <c>player.BaseOrbSlotCount</c>），
/// 而 <c>CharacterModel.BaseOrbSlotCount</c> 默认是 <c>0</c>（<c>:101</c>），
/// <c>OrbQueue.TryEnqueue</c> 在容量 0 时<b>直接返回 false</b>（<c>OrbQueue.cs:52-55</c>）
/// ⇒ 充能类牌安全地什么都不做，不会抛异常。
/// </para>
///
/// <para>
/// <b><c>canSkip: true</c></b> 照抄「类星体」：-design 没提能不能跳过，
/// 原版同类"从 N 张里选 1 张"的牌都可跳过。
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Inspiration : WizardCardModel
{
    private const int Cost = 0;

    /// <summary>备选张数。</summary>
    private const int OfferCount = 3;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
    ];

    public Inspiration()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState == null)
        {
            return;
        }

        List<CardModel> pool = GetOtherColorSkillPool().ToList();
        if (pool.Count == 0)
        {
            return;
        }

        // ① 取 3 张互不相同的备选（内部已完成"按人数过滤 + FilterForCombat + CreateCard"）。
        List<CardModel> offered = CardFactory
            .GetDistinctForCombat(Owner, pool, OfferCount, Owner.RunState.Rng.CombatCardGeneration)
            .ToList();

        if (offered.Count == 0)
        {
            return;
        }

        // ② 升级过的「启示」⇒ 备选牌也是升级过的（照抄 Quasar）。
        //    静默升级，避免连弹三张预览（同「暗黑神秘学」）。
        if (IsUpgraded)
        {
            CardCmd.Upgrade(offered, CardPreviewStyle.None);
        }

        // ③ 三选一（可跳过，照抄 Quasar）。
        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext, offered, Owner, canSkip: true);

        if (chosen == null)
        {
            return;
        }

        // ④ 加入手牌（走"生成"路径 ⇒ 会触发「催化」「创世之柱」等生成类效果）。
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    /// <summary>
    /// 「其他颜色」的<b>技能牌</b>候选池：红/绿/蓝/橙/紫/灰六个卡池的并集，
    /// 只取 <see cref="CardType.Skill"/>。池子为什么显式列出见类注释。
    /// </summary>
    private IEnumerable<CardModel> GetOtherColorSkillPool()
    {
        CardPoolModel[] otherColorPools =
        [
            ModelDb.CardPool<IroncladCardPool>(),     // 红 - 铁甲战士
            ModelDb.CardPool<SilentCardPool>(),       // 绿 - 静默猎手
            ModelDb.CardPool<DefectCardPool>(),       // 蓝 - 故障机器人
            ModelDb.CardPool<RegentCardPool>(),       // 橙 - 储君
            ModelDb.CardPool<NecrobinderCardPool>(),  // 紫 - 亡灵契约师
            ModelDb.CardPool<ColorlessCardPool>(),    // 灰 - 无色牌
        ];

        return otherColorPools
            .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(c => c.Type == CardType.Skill);
    }

    /// <summary>
    /// 升级不改变数值，而是让生成出来的备选牌变成<b>升级过的</b>
    /// （design 里「随机（升级过的）」的写法就是 <c>{IfUpgraded:show:...}</c> 的语义）。
    /// 所以这里不需要改任何 <c>DynamicVar</c>。
    /// </summary>
    protected override void OnUpgrade()
    {
    }
}
