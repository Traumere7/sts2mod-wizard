using BaseLib.Config;

namespace Wizard.Scripts.Config;

/// <summary>
/// 巫师 mod 的设置项（显示在游戏的 mod 设置页里）。
///
/// <para>
/// 框架来自 BaseLib：<see cref="SimpleModConfig"/> 会按<b>静态属性</b>自动生成设置行
/// （<c>bool</c> ⇒ 复选框），落地文件是
/// <c>%APPDATA%\SlayTheSpire2\mod_configs\Wizard.cfg</c>
/// —— 文件名取自<b>根命名空间</b>（这里是 <c>Wizard</c>）。
/// </para>
///
/// <para>
/// 标签文案的查找口径（<c>ModConfig.GetLabelText</c>）：
/// <code>
/// LocString.GetIfExists("settings_ui", ModPrefix + Slugify(属性名) + ".title")
/// </code>
/// <c>ModPrefix</c> = 根命名空间大写 + <c>-</c> ⇒ 本类属性对应的键是
/// <c>settings_ui</c> 表里的 <c>WIZARD-USE_ALT_ART.title</c>
/// （表文件：<c>localization/{lang}/settings_ui.json</c>）。
/// 键不存在时会退化成属性名本身 —— 所以文案键必须补。
/// </para>
///
/// <para>
/// ⚠️ <b>本选项只在"游戏启动时"被读取一次</b>（见 <c>Entry.MountAltArtPack</c>）——
/// 美术资源一旦被 Godot 加载就进了缓存，换路径不会自动重读。
/// 所以勾选/取消后<b>必须重启游戏</b>，标签文案里写明了这一点。
/// </para>
/// </summary>
public class WizardArtConfig : SimpleModConfig
{
    /// <summary>
    /// 是否使用玩家私下分发的替换美术包（<c>mods\Wizard\WizardAlt.pck</c>）。
    /// 默认 <c>false</c> ⇒ 用 mod 自带的美术。
    /// </summary>
    [ConfigSection("Art")]
    public static bool UseAltArt { get; set; } = false;
}
