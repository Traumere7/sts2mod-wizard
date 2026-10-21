using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 温暖蜡烛 / WarmCandle - Uncommon
/// 你每消耗 12 张牌，回复 3 点生命。（计数<b>跨战斗</b>累积）
///
/// <para>
/// <b>照抄原版「纸钱」<c>JossPaper</c></b> —— 它就是"每消耗 N 张牌做一件事"
/// 并带一个跨战斗持久计数器的现成范本（N=5，抽牌）。
/// 伤害化用「五轮书」<c>BookOfFiveRings</c> 的回复写法
/// （<c>BookOfFiveRings.cs:90</c> 的 <c>CreatureCmd.Heal</c>）。
/// </para>
///
/// <para>
/// <b>跨战斗持久的三件套</b>（<c>RelicModel</c> 的既有机制）：
/// <list type="number">
///   <item><c>ShowCounter =&gt; true</c>（<c>RelicModel.cs:347</c>）—— 显示图标下的数字</item>
///   <item><c>DisplayAmount</c>（<c>RelicModel.cs:349</c>）—— 那个数字是多少</item>
///   <item><c>[SavedProperty]</c> 打在真正持有状态的属性上，并在 setter 里
///         <c>AssertMutable()</c> + <c>InvokeDisplayAmountChanged()</c></item>
/// </list>
/// <b>跨战斗持久 = 不重置</b>：<c>AfterCombatEnd</c> 里<b>不</b>清 <c>CardsExhausted</c>
/// （<c>JossPaper.cs:144-148</c> 也是只清临时字段）。
/// </para>
///
/// <para>
/// ⚠️ <b><c>[SavedProperty]</c> 的 setter 必须调 <c>AssertMutable()</c></b> ——
/// 反序列化会在可变克隆上跑 setter，引擎靠这个断言保证安全
/// （每个原版例子都这么做，如 <c>JossPaper.cs:60-74</c>）。
/// </para>
///
/// <para>
/// <b>「虚无」消耗也计数</b>：<c>causedByEthereal</c> 为 true 时同样 +1。
/// <c>JossPaper</c> 出于"给牌时机"的考虑把虚无消耗延后到 <c>AfterSideTurnEnd</c>
/// 再算（见其注释 76-80 行），但本遗物只是计数+回血、没有给牌时机问题，
/// 所以<b>就地计数</b>，不需要那套延后。
/// </para>
/// </summary>
[Pool(typeof(WizardRelicPool))]
public class WarmCandle : ArcaneRelicModel
{
    /// <summary>触发阈值：每消耗这么多张牌回一次血。</summary>
    private const int Threshold = 12;

    private int _cardsExhausted;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool ShowCounter => true;

    public override int DisplayAmount => CardsExhausted;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(3m),
        new DynamicVar("Threshold", Threshold),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
    ];

    /// <summary>
    /// 已消耗张数（<b>跨战斗累积</b>）。
    ///
    /// <para>
    /// <c>SaveIfNotTypeDefault</c>：为 0 时不必写进存档，省体积
    /// （<c>JossPaper.cs:60</c> 用的就是这个条件）。
    /// </para>
    /// </summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsExhausted
    {
        get => _cardsExhausted;
        set
        {
            AssertMutable();
            _cardsExhausted = value;
            InvokeDisplayAmountChanged();
        }
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        // 只数自己的牌（多人下别数队友的）。
        if (card.Owner != Owner)
        {
            return;
        }

        CardsExhausted++;

        if (CardsExhausted < Threshold)
        {
            return;
        }

        // 达到阈值：回血并扣掉这批计数（余数保留，避免"溢出浪费"）。
        CardsExhausted -= Threshold;

        Flash();

        // ⚠️ 第三个参数（playAnim）留默认 —— CreatureCmd.cs:728-734 明确要求
        //    即使是战斗外回血也要保留动画。
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
    }
}
