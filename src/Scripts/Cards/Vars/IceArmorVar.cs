using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards.Vars;

/// <summary>
/// 「冰铠」卡面显示的**最终覆甲层数** =
/// 基础覆甲 + 场上所有敌人的（寒冷层数 + 冻结层数）之和。
/// 在手里就显示最终值（有加成时变绿），卡组/卡牌库里显示基础值。
///
/// <para>
/// 结构照抄原版「岩石铠甲」<c>StoneArmor</c> 与「重锤」<c>BodySlam</c>：
/// 倍率函数<b>读自己的状态、完全忽略 <c>target</c></b>，所以手里就能算出来。
/// </para>
///
/// <para>
/// <b>卡面变绿的机制</b>（<c>DynamicVar.ToHighlightedString</c>）：
/// <code>
/// value  = (int)PreviewValue      // 显示的数字
/// value2 = (int)EnchantedValue    // 由 BaseValue 决定
/// green  = PreviewValue &gt; EnchantedValue
/// </code>
/// 所以本类把 <c>BaseValue</c> 写成<b>基础覆甲</b>（比较基准）、
/// <c>PreviewValue</c> 写成<b>最终值</b>（显示 + 变绿）。
/// </para>
///
/// <para>
/// ⚠️ <b>必须重写 <see cref="DynamicVar.GetBaseValueForIConvertible"/>：</b>
/// 引擎渲染 <c>{IceArmor:diff()}</c> 走 <c>DynamicVarSet.AddTo</c>
/// → <c>LocString.Add(DynamicVar)</c> → <c>GetBaseValueForIConvertible()</c>。
/// 基类把它重写成了 <c>Calculate(null)</c> —— 只写 <c>BaseValue</c> 是没用的。
/// </para>
///
/// <para>
/// <b>「卡组里显示原始值」怎么实现的：</b>倍率在<b>非战斗</b>时返回 0
/// （卡组/卡牌库/奖励界面没有棋局，读数无意义），
/// 于是 <c>Calculate(null) = CalculationBase + 1 × 0 = 基础值</c>。
/// </para>
///
/// <para>
/// ⚠️ <b>冻结也算入</b>（2026-09-25 增强）：<c>CalculationExtra</c> 仍是 1，
/// 所以"每 1 层寒冷或冻结 = +1 覆甲"，两种状态<b>等价</b>而非加权。
/// 改动只需动 <see cref="GetTotalChillAndFreeze"/> 一处，
/// 但**别忘了 <see cref="IceArmor.OnPlay"/> 里还有一份等价的结算求和** ——
/// 卡面展示与实结算刻意各自独立（见 IceArmor 类注释），两处必须同步改。
/// </para>
/// </summary>
public class IceArmorVar : CalculatedVar
{
    /// <summary>卡面文案用的变量名。</summary>
    public const string DisplayName = "IceArmor";

    public IceArmorVar()
        : base(DisplayName)
    {
    }

    /// <summary>
    /// 卡面取值：给出算式结果
    /// <c>CalculationBase + CalculationExtra × (寒冷 + 冻结) 总数</c>。
    /// 显式写出来是为了把"卡面显示的就是算式结果"这条约定固定住。
    /// </summary>
    protected override decimal GetBaseValueForIConvertible() => Calculate(null);

    public override void UpdateCardPreview(
        CardModel card,
        CardPreviewMode previewMode,
        Creature? target,
        bool runGlobalHooks)
    {
        decimal baseArmor = GetBaseVar().BaseValue;
        decimal bonus = GetTotalChillAndFreeze(card);
        decimal total = baseArmor + bonus;

        // BaseValue 决定比较基准（EnchantedValue），必须是"基础值"，
        // 否则最终值永远等于基准、不会变绿。
        BaseValue = baseArmor;

        // 显示的数字与变绿都靠 PreviewValue。
        PreviewValue = total;
    }

    /// <summary>
    /// 场上**所有敌人**的（寒冷 + 冻结）层数之和；非战斗时为 0。
    ///
    /// <para>
    /// 用 <c>CombatManager.Instance.IsInProgress</c> 兜底而不是直接读棋局：
    /// 卡组/卡牌库/奖励界面也会刷新卡面，那时没有棋局。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 用 <c>GetOpponentsOf</c>（敌人）而不是全场 —— 设计写的是
    /// "所有敌人"的层数，玩家自己身上的不算。
    /// </para>
    /// </summary>
    private static decimal GetTotalChillAndFreeze(CardModel card)
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return 0m;
        }

        if (card.Owner?.Creature is not { } self)
        {
            return 0m;
        }

        ICombatState? combatState = card.CombatState ?? self.CombatState;
        if (combatState == null)
        {
            return 0m;
        }

        // 两种能力都是 Counter，直接读 Amount 即可。
        return combatState
            .GetOpponentsOf(self)
            .Where(c => c.IsAlive)
            .Sum(c => c.GetPowerAmount<ChillPower>() + c.GetPowerAmount<FreezePower>());
    }
}
