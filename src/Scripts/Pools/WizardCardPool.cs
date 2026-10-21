using BaseLib.Abstracts;
using Godot;

namespace Wizard.Scripts.Pools;

/// <summary>
/// 巫师的专属卡池。
/// 所有巫师卡牌通过 <c>[Pool(typeof(WizardCardPool))]</c> 注册到这里。
/// </summary>
public class WizardCardPool : CustomCardPoolModel
{
    /// <summary>卡池 ID（同时决定卡池的美术资源查找名）。</summary>
    public override string Title => "Wizard";

    // ── 卡框颜色：以 HSV 描述，三个值共同构成卡池主题色 ──
    // 奥术紫：色相 0.75
    public override float H => 0.75f;
    public override float S => 0.45f;
    public override float V => 0.95f;

    /// <summary>卡池主题色，用于牌堆入口等 UI。</summary>
    public override Color DeckEntryCardColor => new(0.55f, 0.35f, 0.95f);

    /// <summary>是否为无色卡池。</summary>
    public override bool IsColorless => false;

    // ── 能量图标（占位，替换为巫师自己的能量图标即可）──
    /// <summary>tooltip 和卡牌左上角使用的能量图标，尺寸 74x74。</summary>
    public override string? BigEnergyIconPath => "res://Wizard/images/ui/energy_wizard_big.png";

    /// <summary>描述文本中使用的能量图标，尺寸 24x24。</summary>
    public override string? TextEnergyIconPath => "res://Wizard/images/ui/energy_wizard.png";
}
