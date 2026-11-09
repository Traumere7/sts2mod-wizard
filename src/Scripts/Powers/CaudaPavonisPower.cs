namespace Wizard.Scripts.Powers;

/// <summary>
/// 孔雀之尾（**未升级版**）：你每生成 1 张卡牌，就给予随机敌人 **2** 层随机负面效果，
/// 结算次数 = 本能力的层数（n）。
///
/// <para>
/// 与升级版 <see cref="CaudaPavonisUpgradedPower"/> 是**两个独立的模型**，
/// 因此两个 buff 图标各自叠层、互不干扰（用户 2026-11-09 定案）。
/// 两者本地化名称都叫「孔雀之尾」。
/// </para>
/// </summary>
public class CaudaPavonisPower : CaudaPavonisPowerBase
{
    /// <summary>未升级：每次给 2 层。</summary>
    public override decimal LayersPerTrigger => 2m;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/cauda_pavonis_power.png";

    public override string? CustomBigIconPath => "res://Wizard/images/powers/cauda_pavonis_power_big.png";
}
