using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards.Vars;

/// <summary>
/// 「治疗术」卡面显示的**最终治疗量** = 基础治疗量 + 法术增益层数。
/// 在手里就直接显示最终值；有加成时数字<b>变绿</b>（与原版力量/敏捷同款）。
///
/// <para>
/// 结构照抄原版「重锤」<c>BodySlam</c> / 「金斧」<c>GoldAxe</c>：
/// 倍率函数<b>读自己的状态、完全忽略 <c>target</c></b>，所以手里就能算出来。
/// （对比「欺凌」<c>Bully</c> 的倍率读 <c>target</c>，那才必须拖出来才有值。）
/// </para>
///
/// <para>
/// <b>卡面变绿的机制</b>（<c>DynamicVar.ToHighlightedString</c>）：
/// <code>
/// value  = (int)PreviewValue      // 显示的数字
/// value2 = (int)EnchantedValue    // 由 BaseValue 决定（ResetToBase 时同步）
/// green  = PreviewValue &gt; EnchantedValue
/// </code>
/// 所以本类只需：
/// <list type="bullet">
///   <item><c>BaseValue = 基础治疗量</c> ⇒ <c>EnchantedValue</c> 跟着变成基础值（比较基准）</item>
///   <item><c>PreviewValue = 基础 + 法术增益</c> ⇒ 显示最终值，且大于基准时自动变绿</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ <b>为什么必须重写 <see cref="DynamicVar.GetBaseValueForIConvertible"/>：</b>
/// 引擎渲染 <c>{Heal:diff()}</c> 走 <c>DynamicVarSet.AddTo</c>
/// → <c>LocString.Add(DynamicVar)</c> → <c>GetBaseValueForIConvertible()</c>。
/// 而基类把它<b>重写成了 <c>Calculate(null)</c></b> ——
/// "只写 BaseValue/PreviewValue"是不够的，卡面会显示 <c>Calculate(null)</c>。
/// 上一版就是漏了这点，卡组里直接显示 0。
/// </para>
///
/// <para>
/// <b>非战斗时返回 0</b>：卡组/卡牌库里不该凭空多出法术增益
/// （那时没有棋局，读数没有意义），也避免任何 <c>Owner</c> 未绑定的空引用。
/// </para>
///
/// <para>
/// ⚠️ <b>结算不读这个变量</b>：<c>HealingSpell.OnPlay</c> 自己算实际治疗量。
/// </para>
/// </summary>
public class HealingSpellHealVar : CalculatedVar
{
    /// <summary>卡面文案用的变量名（与 <c>HealVar</c> 默认名一致，故文案无需改）。</summary>
    public const string DisplayName = "Heal";

    /// <summary>基础治疗量变量名（用 <c>CalculationBase</c>，见卡牌的变量声明）。</summary>
    public const string BaseHealKey = "CalculationBase";

    public HealingSpellHealVar()
        : base(DisplayName)
    {
    }

    /// <summary>
    /// 卡面取值：直接给出 <c>Calculate(null)</c>，与基类语义一致。
    /// 显式写出来是为了把这条约定固定住 —— 卡面显示的就是算式结果。
    /// </summary>
    protected override decimal GetBaseValueForIConvertible() => Calculate(null);

    public override void UpdateCardPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature? target,
        bool runGlobalHooks)
    {
        // 基数（基础治疗量）。CalculationBase 的 BaseValue 就是升级后的值。
        decimal baseHeal = GetBaseVar().BaseValue;
        decimal bonus = GetSpellPower(card);
        decimal total = baseHeal + bonus;

        // BaseValue 决定比较基准（EnchantedValue），必须是"基础值"而不是最终值，
        // 否则最终值永远等于基准、不会变绿。
        BaseValue = baseHeal;

        // 显示的数字与变绿都靠 PreviewValue。
        PreviewValue = total;
    }

    /// <summary>
    /// 拥有者的法术增益层数；非战斗时为 0。
    ///
    /// <para>
    /// 用 <c>CombatManager.Instance.IsInProgress</c> 兜底而不是直接读玩家能力：
    /// 卡组/卡牌库/奖励界面也会刷新卡面，那时没有棋局，
    /// 读数无意义且可能拿到未绑定的对象。
    /// </para>
    /// </summary>
    private static decimal GetSpellPower(CardModel card)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return 0m;
        }

        return card.Owner?.Creature?.GetPowerAmount<SpellPowerPower>() ?? 0;
    }
}
