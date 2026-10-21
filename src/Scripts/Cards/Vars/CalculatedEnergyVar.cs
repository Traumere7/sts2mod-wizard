using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Cards.Vars;

/// <summary>
/// 「按条件获得若干点能量」的动态变量。
///
/// <para>
/// 卡面写法：<c>{CalculatedEnergy:energyIcons()}</c>
/// —— 必须带 <c>:energyIcons()</c>，否则会渲染成裸数字而不是能量图标。
/// </para>
///
/// <para>
/// 公式与 <see cref="CalculatedVar"/> 一致：<c>CalculationBase + CalculationExtra × 倍率</c>。
/// 卡牌的 <see cref="DynamicVars"/> 里必须同时提供
/// <c>CalculationBaseVar</c> 与 <c>CalculationExtraVar</c>，否则构造动态变量集会抛异常。
/// </para>
///
/// <para>
/// <b>为什么不像 <see cref="CalculatedDamageVar"/> 那样重写
/// <c>UpdateCardPreview</c>：</b>它那套逻辑是**伤害专有**的
/// （附魔加成、<c>Hook.ModifyDamage</c>、<c>ValueProp</c>），能量用不上。
/// 基类 <see cref="CalculatedVar.UpdateCardPreview"/> 做的
/// <c>PreviewValue = Calculate(target)</c> 已经够用，
/// 而「按住牌指向敌人时实时演算」正是靠 <c>Calculate(target)</c> 拿到目标来实现的。
/// </para>
///
/// <para>
/// <b>没有指定目标时（<c>target == null</c>）倍率函数应返回 0</b>，
/// 卡面就显示基础值 —— 与设计要求的"未指定敌人时 x 默认为 0"一致。
/// </para>
///
/// <para>
/// <b>只重写 <see cref="CalculatedVar.GetExtraVar"/>：</b>
/// <c>CalculatedVar</c> 默认取 <c>DynamicVars.CalculationExtra</c>，
/// 而 <c>CalculatedDamageVar</c> 改取 <c>DynamicVars.ExtraDamage</c>。
/// 我们走默认那一套，所以其实可以不重写；这里显式写出来是为了
/// <b>把这条隐含约定固定下来</b> —— 有人将来照抄 <c>CalculatedDamageVar</c>
/// 加一个 <c>ExtraEnergyVar</c> 时，能一眼看出这里依赖的是哪个变量名。
/// </para>
/// </summary>
public class CalculatedEnergyVar : CalculatedVar
{
    public const string DefaultName = "CalculatedEnergy";

    public CalculatedEnergyVar()
        : base(DefaultName)
    {
    }

    /// <summary>
    /// 显式绑定到 <c>CalculationExtra</c>。
    /// 卡牌若没提供这个变量，这里兜底返回自身，
    /// 以免把"少写一个变量"变成难查的崩溃。
    /// </summary>
    protected override DynamicVar GetExtraVar()
    {
        // DynamicVar 的持有者是 protected 字段 _owner（没有公共属性）。
        if (_owner is not CardModel card)
        {
            return this;
        }

        return card.DynamicVars.TryGetValue("CalculationExtra", out DynamicVar? extra)
            ? extra
            : this;
    }
}
