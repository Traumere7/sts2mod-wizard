using BaseLib.Abstracts;

namespace Wizard.Scripts.Pools;

/// <summary>
/// 巫师的专属遗物池。
/// </summary>
public class WizardRelicPool : CustomRelicPoolModel
{
    /// <summary>tooltip 和卡牌左上角使用的能量图标，尺寸 74x74。</summary>
    public override string? BigEnergyIconPath => "res://Wizard/images/ui/energy_wizard_big.png";

    /// <summary>描述文本中使用的能量图标，尺寸 24x24。</summary>
    public override string? TextEnergyIconPath => "res://Wizard/images/ui/energy_wizard.png";
}
