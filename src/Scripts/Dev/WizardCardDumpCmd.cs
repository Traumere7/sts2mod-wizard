using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Wizard.Scripts.Dev;

/// <summary>
/// 开发用控制台命令：把巫师全部卡牌导出成整图 PNG（卡框 + 卡图 + 描述 + 费用）。
///
/// <para>
/// <b>怎么用</b>：游戏里按 <c>`</c>（反引号 / 波浪键）打开控制台，输入：
/// <code>
/// wizardcards
/// wizardcards D:\some\other\dir
/// </code>
/// 图片默认写到 <c>art\cards_in_game\{类名}.png</c>（见 <see cref="WizardCardExporter.DefaultOutDir"/>）。
/// </para>
///
/// <para>
/// <b>为什么 mod 能加控制台命令</b>：游戏在创建控制台时会
/// <c>AbstractConsoleCmdSubtypes.All.Concat(ReflectionHelper.GetSubtypesInMods&lt;AbstractConsoleCmd&gt;())</c>
/// （<c>DevConsole.cs:33</c>），也就是说它<b>会扫描 mod 程序集里的命令并注册</b>。
/// 另外 <c>NDevConsole.cs:359</c> 的判定里有 <c>ModManager.IsRunningModded()</c>，
/// 所以<b>只要加载了 mod，调试命令就一律可用</b>。
/// </para>
///
/// <para>
/// <b>为什么返回 <see cref="CmdResult"/> 的 Task 重载</b>：控制台拿到非空 <c>result.task</c> 会
/// <c>TaskHelper.RunSafely(result.task)</c>（<c>DevConsole.cs:288</c>），
/// 也就是<b>异常由游戏兜底</b>，不会把控制台搞崩。逐张导出又要等帧，所以必须走异步。
/// </para>
/// </summary>
public sealed class WizardCardDumpCmd : AbstractConsoleCmd
{
    public override string CmdName => "wizardcards";

    public override string Args => "[out:string] [lang:string]";

    public override string Description =>
        "把巫师全部卡牌渲染成整图 PNG（卡框+卡图+描述+费用），默认 zhs、输出到 art/cards_in_game。";

    public override bool IsNetworked => false;

    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        string? outDir = args.Length > 0 ? args[0] : null;
        string? lang = args.Length > 1 ? args[1] : null;

        return new CmdResult(
            WizardCardExporter.RunAsync(outDir, lang),
            success: true,
            msg: "开始导出巫师卡牌…（完成后看日志里的 [卡牌导出] 汇总）");
    }
}
