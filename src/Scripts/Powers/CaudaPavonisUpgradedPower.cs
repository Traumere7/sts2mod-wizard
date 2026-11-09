namespace Wizard.Scripts.Powers;

/// <summary>
/// 孔雀之尾（**升级版**）：你每生成 1 张卡牌，就给予随机敌人 **3** 层随机负面效果，
/// 结算次数 = 本能力的层数（n）。
///
/// <para>
/// 与未升级版 <see cref="CaudaPavonisPower"/> 是**两个独立的模型**（不同 <c>ModelId</c>），
/// 所以：
/// <list type="bullet">
///   <item>打两张未升级 ⇒ 只有「2 层」那个图标，n=2；</item>
///   <item>打两张升级 ⇒ 只有「3 层」那个图标，n=2；</item>
///   <item>各打一张 ⇒ 两个图标各 n=1，各自独立结算。</item>
/// </list>
/// 名称在本地化里同样叫「孔雀之尾」（两个 buff 都能看出来源，不会被合并掉）。
/// </para>
/// </summary>
public class CaudaPavonisUpgradedPower : CaudaPavonisPowerBase
{
    /// <summary>升级版：每次给 3 层。</summary>
    public override decimal LayersPerTrigger => 3m;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/cauda_pavonis_upgraded_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/cauda_pavonis_upgraded_power_big.png";
}
