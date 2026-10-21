using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;
using Wizard.Scripts.Spells;

// ⚠️ 原版状态牌「灼伤」的类名就叫 Burn，与本 mod 的 BurnPower（燃烧，减益）不是一回事。
//    用别名避免歧义：BurnCard = 卡牌「灼伤」，BurnPower = 减益「燃烧」。
using BurnCard = MegaCrit.Sts2.Core.Models.Cards.Burn;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 决心 / Determination - 0 费 - 技能 - 罕见
/// 获得 3(4) 费。给予自身 2 层燃烧。将 1 张灼伤加入你的抽牌堆。
///
/// <para>
/// ⚠️ <b>2026-10-21 整张重做</b>（design.md:44）。旧版是
/// "给予自身 1 层易伤、1 层脆弱、3 层燃烧。获得 3(4) 费。"
/// 新版<b>去掉了易伤与脆弱</b>、燃烧 3 → <b>2</b> 层、
/// 并新增"把 1 张灼伤放进抽牌堆"。
/// </para>
///
/// <para>
/// ⚠️ 因为易伤/脆弱没了，旧版那个
/// <c>MakeDropNextTurn</c>（把 <c>SkipNextDurationTick</c> 撤销回 false，
/// 让自伤减益"下个自己回合开始就掉层"）<b>整段删除</b> ——
/// 燃烧不是 duration 型能力，不吃那套掉层规则。
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「灼伤」是**原版状态牌** <c>Burn</c>，不是本 mod 的燃烧减益
/// ══════════════════════════════════════════════════════════════════
/// 原版 <c>Burn.cs</c>：<c>CardType.Status</c> / <c>CardRarity.Status</c> /
/// 费用 -1 / <c>Unplayable</c> / <b>回合结束时若在手牌中则受 2 点伤害</b>。
/// 中文名「灼伤」，英文名「Burn」。
///
/// <para>
/// ⚠️ <b>英文侧存在同名：</b>本 mod 的减益「燃烧」<see cref="BurnPower"/>
/// 英文名也是 "Burn"。中文靠「灼伤 / 燃烧」区分，英文两者同为 "Burn" ——
/// 这是原版卡名与 mod 命名的固有冲突，靠悬停提示区分：
/// 卡牌给 <c>HoverTipFactory.FromCard&lt;BurnCard&gt;()</c>，
/// 减益给 <c>HoverTipFactory.FromPower&lt;BurnPower&gt;()</c>
/// （原版「超频」<c>Overclock</c> 就是这么做的）。
/// </para>
/// </para>
///
/// <para>
/// ══════════════════════════════════════════════════════════════════
/// 「加入抽牌堆」照抄原版「超频」<c>Overclock</c>
/// ══════════════════════════════════════════════════════════════════
/// <c>Overclock.cs:31-33</c>：
/// <code>
/// CardModel card = base.CombatState.CreateCard&lt;Burn&gt;(base.Owner);
/// CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, base.Owner));
/// await Cmd.Wait(0.5f);
/// </code>
/// 三个要点：
/// <list type="number">
///   <item><b>用 <c>CombatState.CreateCard&lt;T&gt;</c> 造牌</b>
///         （<c>ICombatState.cs:103</c>）—— 得到的是属于本次战斗的实例；</item>
///   <item><b>用 <c>CardPileCmd.AddGeneratedCardToCombat</c></b>
///         （<c>CardPileCmd.cs:267</c>）而不是普通 <c>Add</c> ——
///         它会把"生成了这张牌"记进战斗历史，且会拒绝已经属于某个牌堆的牌；</item>
///   <item><c>CardCmd.PreviewCardPileAdd</c> + <c>Cmd.Wait(0.5f)</c> 给玩家看得见的反馈。</item>
/// </list>
/// 牌位用默认的 <c>CardPilePosition.Bottom</c>（design.md 只说"加入抽牌堆"，未指定顶部）。
/// </para>
///
/// <para>
/// <b>结算顺序按 design.md 文案：获得费用 → 给予自身燃烧 → 加入灼伤。</b>
/// </para>
/// </summary>
[Pool(typeof(WizardCardPool))]
public class Determination : WizardCardModel
{
    private const int Cost = 0;

    private const int BurnStacks = 2;

    private const int BurnCards = 1;

    private const string BurnKey = nameof(BurnPower);
    private const string EnergyKey = "Energy";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EnergyHoverTip,
        HoverTipFactory.FromPower<BurnPower>(),
        HoverTipFactory.FromCard<BurnCard>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BurnPower>(BurnStacks),
        new EnergyVar(3),
    ];

    public Determination()
        : base(Cost, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature self = Owner.Creature;

        ICombatState? combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        // ① 获得费用。
        await PlayerCmd.GainEnergy(DynamicVars[EnergyKey].BaseValue, Owner);

        // ② 给予自身燃烧。
        //
        // ⚠️ 必须走 SpellCmd.ApplyBurn（不是裸 PowerCmd.Apply）：
        //    本 mod 的燃烧是"取高值、不叠加"的特殊语义，且需要触发
        //    WizardEvents.BurnApplied ——「纵火狂」「巨焱拳」「全知形态」
        //    都订阅它。applier 传 self 表示"自己给自己的"。
        await SpellCmd.ApplyBurn(
            choiceContext, self, DynamicVars[BurnKey].BaseValue, self, this);

        // ③ 把 1 张「灼伤」（原版状态牌）加入抽牌堆。
        for (int i = 0; i < BurnCards; i++)
        {
            CardModel burnCard = combatState.CreateCard<BurnCard>(Owner);

            CardCmd.PreviewCardPileAdd(
                await CardPileCmd.AddGeneratedCardToCombat(
                    burnCard, PileType.Draw, Owner));
        }

        await Cmd.Wait(0.5f);
    }

    /// <summary>升级只加费用 3 → 4（燃烧层数与灼伤张数固定）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars[EnergyKey].UpgradeValueBy(1m);
    }
}
