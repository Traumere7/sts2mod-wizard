using System.Text;
using BaseLib.Abstracts;

namespace Wizard.Scripts.Relics;

/// <summary>
/// 巫师遗物的公共基类。存在的唯一理由是**修图标路径**。
///
/// ══════════════════════════════════════════════════════════════════
/// 问题：mod 的遗物图标默认永远找不到
/// ══════════════════════════════════════════════════════════════════
/// <c>RelicModel.IconBaseName =&gt; Id.Entry.ToLowerInvariant()</c>
/// （<c>RelicModel.cs:128</c>），于是图标路径是
/// <c>res://images/relics/{Id.Entry 小写}.png</c>。
///
/// 而 mod 模型的 <c>Id.Entry</c> 是
/// <c>{命名空间}-{类名 UPPER_SNAKE}</c> —— **带一个连字符**。
/// 于是引擎会去找 <c>wizard-wizard_starter_relic.png</c>，
/// 而我们的文件叫 <c>wizard_starter_relic.png</c>，**永远对不上**。
///
/// 症状是启动日志里刷：
/// <code>
/// [WARN] AtlasResourceLoader: Missing sprite 'wizard-wizard_starter_relic'
///        in relic_atlas (requested: res://images/atlases/relic_atlas.sprites/...)
/// </code>
/// 遗物栏里显示为空白/问号。而且这是**静默失败** —— 不报错、不崩溃，
/// 只是图标死活不出来。
///
/// ══════════════════════════════════════════════════════════════════
/// 解法（与观者 mod 的 <c>WatcherRelicModel</c> 完全一致）
/// ══════════════════════════════════════════════════════════════════
/// 把 <c>Id.Entry</c> 的命名空间前缀（第一个连字符之前的部分）去掉，
/// 用剩下的部分转小写作为文件名：
/// <list type="bullet">
///   <item><c>WIZARD-SPELL_SCROLL</c> → <c>spell_scroll</c></item>
///   <item>大图标 <c>res://Wizard/images/relics/spell_scroll.png</c></item>
///   <item>图集 <c>res://Wizard/images/atlases/relic_atlas.sprites/spell_scroll.tres</c></item>
///   <item>轮廓 <c>.../spell_scroll_outline.tres</c></item>
/// </list>
///
/// 也就是说：**遗物图片文件名 = 类名的 snake_case**，与卡图同一套约定。
/// </summary>
public abstract class ArcaneRelicModel : CustomRelicModel
{
    private const string ImagesRoot = "res://Wizard/images";

    /// <summary>去掉命名空间前缀后的名字，小写。例：<c>spell_scroll</c>。</summary>
    private string BaseName => SnakeCase(GetType().Name);

    protected override string BigIconPath => $"{ImagesRoot}/relics/{BaseName}.png";

    public override string PackedIconPath =>
        $"{ImagesRoot}/atlases/relic_atlas.sprites/{BaseName}.tres";

    protected override string PackedIconOutlinePath =>
        $"{ImagesRoot}/atlases/relic_atlas.sprites/{BaseName}_outline.tres";

    /// <summary><c>SpellScroll</c> → <c>spell_scroll</c>。</summary>
    private static string SnakeCase(string name)
    {
        StringBuilder sb = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                // 只有"小写/数字 → 大写"的边界才插下划线，
                // 这样连续大写（如 HTTP）不会被拆成 h_t_t_p。
                if (i > 0 && !char.IsUpper(name[i - 1]))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
