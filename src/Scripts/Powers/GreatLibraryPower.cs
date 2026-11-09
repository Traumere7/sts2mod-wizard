using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 图书馆：**在你的回合开始时，额外抽 <c>Amount</c> 张牌**。
///
/// <para>
/// 照抄原版「机器学习」<c>MachineLearningPower</c>：不改抽牌流程，只重写
/// <c>ModifyHandDraw(player, count)</c> 把本回合的手牌数加上 <c>Amount</c>。
/// 这是引擎"追加抽牌"的标准钩子 —— 比在 <c>BeforeHandDraw</c> 里再补一次
/// <c>CardPileCmd.Draw</c> 更正确（后者会被"抽牌数被削减"之类的效果绕过）。
/// </para>
///
/// <para>
/// <c>StackType = Counter</c>：打两张就是每回合 +4 张（与原版同款）。
/// </para>
/// </summary>
public class GreatLibraryPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/great_library_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/great_library_power_big.png";

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player)
        {
            return count;
        }

        return count + Amount;
    }
}
