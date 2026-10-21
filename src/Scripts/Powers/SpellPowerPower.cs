using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 法术增益：所有**法术伤害**加算提升该增益层数。
/// 相当于法术伤害自己的"力量"。
///
/// 实现方式与 <c>StrengthPower</c> 完全一致，只是把"是否为正规攻击"的判断
/// 换成"伤害来源是否为法术牌"。
/// </summary>
public class SpellPowerPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/spell_power_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/spell_power_power_big.png";

    /// <summary>
    /// 命名变量，供卡面 <c>PowerVar&lt;SpellPowerPower&gt;</c> 与能力 tooltip 解析 <c>{Amount}</c>。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(nameof(SpellPowerPower), 0m),
    ];

    /// <summary>
    /// 加算伤害。返回 0 表示不加成。
    /// 只有"造成伤害的是本能力的拥有者"且"伤害来源是法术牌"时才生效。
    /// </summary>
    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Owner != dealer)
        {
            return 0m;
        }

        // 同时看 ValueProp 标记位与卡牌接口：前者覆盖所有来源，后者兼容未迁移的卡。
        if (!SpellProps.IsSpell(props, cardSource))
        {
            return 0m;
        }

        return Amount;
    }
}
