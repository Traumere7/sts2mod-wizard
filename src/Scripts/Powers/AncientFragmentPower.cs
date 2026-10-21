using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 远古残章：**敌人每具有 1 种负面效果，你对其造成的伤害提升 25%（可叠加）。**
///
/// <para>
/// 触发点 <c>ModifyDamageMultiplicative</c>（返回<b>乘数</b>，1 表示不缩放）。
/// 与「寒冷」<see cref="ChillPower"/> 同一个钩子，但判据不同：
/// 寒冷看"伤害是不是火/雷法术"，本能力看"目标身上有几种负面效果"。
/// </para>
///
/// <para>
/// <b>叠层加算</b>：<c>倍率 = 1 + 0.25 × 张数 × 种类数</c>
/// <list type="bullet">
///   <item>1 张 · 1 种 → 1.25（+25%）</item>
///   <item>2 张 · 1 种 → 1.50（+50%）</item>
///   <item>1 张 · 4 种 → 2.00（+100%）</item>
/// </list>
/// <b>不是</b>复利 <c>1.25^N</c>（4 种时那会到 2.44 倍）。
/// </para>
///
/// <para>
/// <b>"1 种"的口径与「魔力汲取」「降雷」一致</b>：按 <c>PowerModel.Id</c> 去重，
/// 同一个 power 叠多少层都只算 1 种。
/// </para>
///
/// <para>
/// <b>只作用于"我打出去的伤害"</b>：<c>dealer != Owner</c> 直接返回 1。
/// 不限制伤害类型 —— 设计说的是"你对其造成的伤害"，不分法术与普通攻击。
/// </para>
///
/// <para>
/// <b>不担心递归</b>：<c>ModifyDamageMultiplicative</c> 是<b>伤害结算前</b>的查询，
/// 不会造成伤害，因此不会自我触发。
/// </para>
/// </summary>
public class AncientFragmentPower : CustomPowerModel
{
    /// <summary>每种负面效果的提升幅度（0.25 = +25%）。</summary>
    public const decimal BonusPerDebuff = 0.25m;

    /// <summary>
    /// 展示用变量名：当前**累计**的百分点数（1 张 = 25，2 张 = 50），
    /// 供能力文案 <c>{TotalPercent}%</c> 渲染。
    /// </summary>
    public const string TotalPercentKey = "TotalPercent";

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 计数型：<c>Amount</c> = 已拥有的「远古残章」张数，加成按张数<b>加算</b>。
    ///
    /// <para>
    /// ⚠️ 早期这里是 <see cref="PowerStackType.Single"/>，于是"开两张仍只有一张的效果"
    /// （实测反馈）。加成幅度本来就该随张数增长，所以必须是 <c>Counter</c> ——
    /// 引擎的层数累加天然给出"两张 = 2 层"。
    /// </para>
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/ancient_fragment_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/ancient_fragment_power_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // 累计百分点数（1 张 = 25，2 张 = 50），让悬停文案显示叠层后的真实幅度。
        new DynamicVar(TotalPercentKey, BonusPerDebuff * 100m),
    ];

    /// <summary>当前累计百分点数（1 张 = 25，2 张 = 50）。</summary>
    public decimal TotalPercent => BonusPerDebuff * 100m * Math.Max(Amount, 1);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncTotalPercent();
        return Task.CompletedTask;
    }

    /// <summary>层数变化时刷新展示值（再打一张要立刻体现 +50%）。</summary>
    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            SyncTotalPercent();
        }

        return Task.CompletedTask;
    }

    private void SyncTotalPercent()
    {
        if (DynamicVars.TryGetValue(TotalPercentKey, out DynamicVar? v))
        {
            v.BaseValue = TotalPercent;
        }
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        MegaCrit.Sts2.Core.ValueProps.ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        // 只加成"我方造成的"伤害。
        if (dealer != Owner)
        {
            return 1m;
        }

        if (target == null)
        {
            return 1m;
        }

        int kinds = CountDistinctDebuffs(target);
        if (kinds <= 0)
        {
            return 1m;
        }

        // 加算：1 + 每层加成 × 张数 × 负面效果种类数
        //
        //   1 张 · 1 种 → 1 + 0.25×1×1 = 1.25（+25%）
        //   2 张 · 1 种 → 1 + 0.25×2×1 = 1.50（+50%）
        //   1 张 · 4 种 → 1 + 0.25×1×4 = 2.00（+100%）
        //
        // 用 Max(Amount, 1) 兜底：万一层数因故为 0，也按 1 张算，不会退化成"无加成"。
        int stacks = Math.Max(Amount, 1);
        return 1m + BonusPerDebuff * stacks * kinds;
    }

    /// <summary>目标身上不同种类负面效果的数量（按 <c>Id</c> 去重）。</summary>
    private static int CountDistinctDebuffs(Creature target)
        => target.Powers
            .Where(p => p.Type == PowerType.Debuff)
            .Select(p => p.Id)
            .Distinct()
            .Count();
}
