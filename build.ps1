<#
.SYNOPSIS
    一键构建巫师 mod：编译 C#、打包 pck、复制到游戏 mods 目录。

.DESCRIPTION
    打包使用 GDRE Tools，因此不需要安装 Godot 导出模板。
    若你之后装了导出模板，也可以直接用 Godot 的 --export-pack（见 Wizard.csproj 的 ExportPck 目标）。

.EXAMPLE
    powershell -File build.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipPck,
    [switch]$NoCommit
)

$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'src'
$ProjectFile = Join-Path $ProjectDir 'Wizard.csproj'
$BuildDir = Join-Path $RepoRoot 'build'
$PckOut = Join-Path $BuildDir 'Wizard.pck'

$GameDir = 'D:\SteamLibrary\steamapps\common\Slay the Spire 2'

# ══════════════════════════════════════════════════════════════════
# ⚠️⚠️ 部署目标 = 游戏目录下的 **mods\Wizard** ⚠️⚠️
# ══════════════════════════════════════════════════════════════════
#
# 为什么最终选它（三处都试过，这一处最可靠）：
#
#   ① 游戏目录 mods\Wizard —— **游戏确实会加载，且优先级高于工坊**
#        实测（godot.log，两份同时存在时）：
#          Loading assembly DLL ...\common\Slay the Spire 2\mods\Wizard\Wizard.dll
#        ⇒ 它**胜过**工坊那份。而且它是普通本地目录，**Steam 不会碰它**。
#
#   ② 工坊目录 workshop\content\2868840\3807136195 —— ❌ 不可写
#        Steam 管理的缓存。实测：10:42 写入成功 → 10:46 被覆盖回已发布版本。
#
#   ③ 官方上传器 workspace ModUploader\WizardWorkspace\content —— 只是"素材源"
#        往里写**不会**让游戏看到，必须再手动跑 ModUploader.exe 上传、
#        等 Steam 同步回 ② 才生效。链路太长，不适合日常迭代。
#
# 所以：
#   * **日常开发 / 联机测试 → 部署到 ①（本脚本目标）**，改完立即可见。
#   * 正式对外发布时，才需要把产物同步到 ③ 并跑 ModUploader.exe 上传（见 CONTEXT.md §2.2）。
#
# ⚠️⚠️ ① 与 ② 的 mod id 相同，会同时被扫描。
#     ① 优先，所以 ② 落后**不影响本机游戏**；
#     但**联机双方必须都拿到同一份 ①**，否则会像 §6.23 那样状态分叉。
$ModDir = Join-Path $GameDir 'mods\Wizard'

# 工坊缓存目录（仅用于提示"已发布版本是否落后"，本脚本不往里写）
$WorkshopItemId = '3807136195'
$WorkshopDir = "D:\SteamLibrary\steamapps\workshop\content\2868840\$WorkshopItemId"

# 官方上传器 workspace（仅正式发布时用到）
$UploaderRoot = 'D:\Downloads\ModUploader-win-x64'
$UploaderDir = Join-Path $UploaderRoot 'WizardWorkspace\content'

$GdreExe = 'D:\Godot\GDRE_tools-v2.6.0-windows\gdre_tools.exe'
$GodotExe = 'D:\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe'

function Write-Step([string]$text) { Write-Host "=== $text ===" -ForegroundColor Cyan }

<#
    执行"会往 stderr 写内容"的原生命令，返回其退出码。

    为什么必须包一层：
      PowerShell 在 $ErrorActionPreference = 'Stop' 下，**原生命令只要往 stderr 写任何东西**，
      就会产生 NativeCommandError 并**直接终止脚本** —— 即使用 *> 把输出重定向到文件也拦不住。
      而 Godot 的 --import 在独立工程里必然往 stderr 报一堆
      "Cannot open file res://src/..."（原版游戏资源，独立运行时本来就不存在）。
      所以必须在这条命令执行期间临时把 ErrorActionPreference 降为 Continue。

    ⚠️⚠️ 为什么用 cmd /c 重定向，而不是 PowerShell 的 `*>`：⚠️⚠️
      **GDRE Tools 是 GUI 子系统程序**，PowerShell 的 `& $exe @args *> $log`
      对它**静默失败**：不报错、$LASTEXITCODE 为空、日志 0 字节，
      目标 pck **根本没被创建**。实测对照（同一条 GDRE 打包命令）：
          & $exe ... *> log    → 退出码(空)   生成=False  日志=0 字节
          cmd /c "... > log"   → 退出码=0     生成=True
      后果极其隐蔽：build\Wizard.pck 一直是**若干轮之前的旧文件**，
      而脚本读它的哈希、与同样陈旧的部署文件"校验通过"，
      于是**旧 pck 被当成新产物部署出去，脚本还报成功** ——
      这正是之前反复出现"改了但游戏里没变"的原因。
#>
function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory)][string]$Exe,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$LogPath
    )

    # 构造 cmd 命令行：每个参数加引号（路径可能含空格）。
    # 本脚本用到的参数（路径、枚举值）都不含双引号，故不做转义。
    $quoted = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $redirect = if ($LogPath) { " > `"$LogPath`" 2>&1" } else { " > nul 2>&1" }
    $cmdLine = "`"$Exe`" $quoted$redirect"

    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        cmd /c $cmdLine | Out-Null
        return $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $saved
    }
}

# ─────────────────────────────────────────────────────────────
Write-Step '1/5 编译 C#'

# 把工坊目录**传给 csproj**：csproj 的 CopyMod 目标负责复制 dll + json，
# 它的默认值与这里一致，但显式传参可保证"改一处就够"，不会两边漂移。
dotnet build $ProjectFile -c $Configuration -v minimal "-p:WorkshopDir=$ModDir"
if ($LASTEXITCODE -ne 0) { throw "dotnet build 失败（退出码 $LASTEXITCODE）" }

# ─────────────────────────────────────────────────────────────
Write-Step '2/5 让 Godot 导入/刷新资源'

# 说明：独立运行 Godot 时，游戏本体的 PCK 没有挂载，所以场景里指向原版资源的引用
# （res://src/Core/Nodes/... 、res://addons/mega_text/... 、res://themes/... 等）
# 一定会报 "Cannot open file / Failed loading resource"。
# 这些是**预期噪音**，不是错误 —— 游戏运行时这些路径由游戏自己的 PCK 提供。
#
# 因此这里：
#   1. 用 Invoke-NativeCapture 执行（它会在命令期间临时关闭 Stop 语义；
#      否则 Godot 写 stderr 会直接终止脚本 —— 这正是之前报错的原因）；
#   2. 用退出码判断成功与否；
#   3. 只把"非原版路径"的真实错误挑出来报告。
$importLog = Join-Path $env:TEMP 'wizard-godot-import.log'
$importExit = Invoke-NativeCapture -Exe $GodotExe `
    -Arguments @('--headless', '--path', $ProjectDir, '--import') `
    -LogPath $importLog

if ($importExit -ne 0) {
    Get-Content $importLog -Tail 30 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    throw "Godot 导入失败（退出码 $importExit），日志见 $importLog"
}

# 已知的预期噪音。分两类：
#   A. 原版游戏资源缺失 —— 这些路径由游戏本体 PCK 提供，独立 Godot 里必然读不到
#   B. Godot 编辑器自身的 headless 噪音 —— 与我们的工程无关
$expectedMissing = @(
    'res://src/',             # 原版 C# 脚本（NEnergyCounter / NCreatureVisuals / NMerchantCharacter / NRestSiteCharacter）
    'res://addons/mega_text/',# 原版 MegaLabel 脚本
    'res://themes/',          # 原版主题/字体
    'res://images/',          # 原版图片资源
    'res://scenes/',          # 原版场景
    'res://materials/',
    'res://shaders/',
    'res://fonts/',
    'res://animations/'
)
$expectedNoise = @(
    'HotReloadAssemblyWatcher',   # 编辑器热重载监视器（headless 下计时器不在场景树里）
    "not inside the scene tree",
    'EditorSettings::get_singleton',  # headless 下没有 EditorSettings 单例（2026-10-21 观测到）
    'CategoryInfo',               # PowerShell 对上述 stderr 的包装行
    'FullyQualifiedErrorId',
    'NativeCommandError'
)

# 只检查日志里**以 ERROR/Failed 开头**的行，避免把 PowerShell 的包装行当成真错误
$realErrors = Select-String -Path $importLog -Pattern '^(ERROR|SCRIPT ERROR|Failed)' -Encoding UTF8 -ErrorAction SilentlyContinue |
    Where-Object {
        $line = $_.Line
        -not ($expectedMissing | Where-Object { $line -like "*$_*" }) -and
        -not ($expectedNoise   | Where-Object { $line -like "*$_*" })
    }

if ($realErrors) {
    Write-Host "  发现 $($realErrors.Count) 条非预期的导入错误：" -ForegroundColor Yellow
    $realErrors | Select-Object -First 15 | ForEach-Object { Write-Host "    $($_.Line.Trim())" -ForegroundColor Yellow }
    Write-Host "  完整日志：$importLog" -ForegroundColor DarkGray
} else {
    Write-Host '  资源导入完成' -ForegroundColor Green
    Write-Host '    （原版资源引用缺失、Godot 编辑器 headless 噪音：均已忽略）' -ForegroundColor DarkGray
}

# ─────────────────────────────────────────────────────────────
if ($SkipPck) {
    Write-Step '3/5 已跳过 pck 打包'
} else {
    Write-Step '3/5 打包 pck（GDRE，无需导出模板）'
    New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

    # ⚠️⚠️ 必须**先删掉旧的 pck**。⚠️⚠️
    #
    # 踩过的坑（这次才查清）：GDRE 的 --pck-create 在某些情况下
    # **不会覆盖已存在的输出文件**，命令返回后 build\Wizard.pck 仍是旧内容。
    # 而脚本紧接着读它算哈希 → 拿到旧的哈希 → 与同样旧的部署文件"校验通过"，
    # 于是**旧的 pck 被当成新的部署出去，脚本还报成功**。
    #
    # 实测铁证：
    #   脚本自报 "pck B013A75B 校验通过"
    #   但独立复核 build\Wizard.pck = 1EAC1041、部署 = B013A75B（旧）
    # 也就是说 $srcHash 读到的根本不是本轮产物。
    #
    # 先删掉旧文件，GDRE 就必然要新建一个，配合下面的"mtime 必须是本轮的"校验，
    # 双重保证不会再把陈旧的 pck 当新产物。
    if (Test-Path $PckOut) {
        Remove-Item $PckOut -Force
    }

    $pckStart = Get-Date
    $gdreLog = Join-Path $env:TEMP 'wizard-gdre.log'
    $null = Invoke-NativeCapture -Exe $GdreExe `
        -Arguments @('--headless', "--pck-create=$ProjectDir", '--pck-version=2',
                     '--pck-engine-version=4.5.1', "--output=$PckOut") `
        -LogPath $gdreLog
    if (-not (Test-Path $PckOut)) {
        Get-Content $gdreLog -Tail 20 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
        throw "pck 未生成：$PckOut（日志见 $gdreLog）"
    }

    # 产物必须是"本轮刚写出来的"，否则说明又读到了陈旧内容。
    $pckInfo = Get-Item $PckOut
    if ($pckInfo.LastWriteTime -lt $pckStart.AddSeconds(-5)) {
        throw "pck 时间戳早于本次打包（$($pckInfo.LastWriteTime) < $pckStart）—— 产物可能是陈旧的"
    }

    $size = [math]::Round($pckInfo.Length / 1KB, 1)
    Write-Host "  Wizard.pck 生成成功（$size KB，$($pckInfo.LastWriteTime.ToString('HH:mm:ss'))）" -ForegroundColor Green
}

# ─────────────────────────────────────────────────────────────
Write-Step '4/5 部署到游戏目录 mods\Wizard'

# 目标目录先做一次存在性校验：路径写错会静默创建出一个没人读的目录，
# 而哈希校验依然"通过"（MakeDir 成功 + Copy 成功 ⇒ 一路绿灯，实际却写错了地方）。
if (-not (Test-Path $GameDir)) {
    throw "游戏目录不存在：$GameDir。请修正脚本顶部的 `$GameDir。"
}
New-Item -ItemType Directory -Force -Path $ModDir | Out-Null
Write-Host "  目标：$ModDir" -ForegroundColor DarkGray

# ⚠️⚠️ 先校验 dll —— 这是长期被忽视的坑。⚠️⚠️
#
# csproj 的 CopyMod 目标会在 dotnet build 结束时把 Wizard.dll 复制到 mods\Wizard。
# **游戏正在运行时 Wizard.dll 被锁住**，复制会重试 10 次后失败，
# 使 dotnet build 返回非 0，脚本会在第 1 步就中止。
#
# 但若复制失败被吞掉，后果极具迷惑性：
#   **pck（资源/本地化/卡图）更新了，dll（代码逻辑）却没更新**。
# 表现就是"新卡面配旧逻辑"、"改了代码游戏里毫无变化"，
# 而且 pck 哈希校验完全正常，完全看不出问题 —— 实测就是这样被误导了好几轮。
#
# 注意产物路径是 `.godot\mono\temp\bin\Debug\Wizard.dll`，
# **不是** `bin\Debug\Wizard.dll`（那里没有文件）。
# 之前这里写错路径，导致 Test-Path 为假、整段校验被静默跳过。
$builtDll = Join-Path $ProjectDir '.godot\mono\temp\bin\Debug\Wizard.dll'
$deployedDll = Join-Path $ModDir 'Wizard.dll'

if (-not (Test-Path $builtDll)) {
    throw "找不到构建产物 $builtDll（Godot 的 C# 产物路径可能变了）"
}

$builtDllHash = (Get-FileHash $builtDll -Algorithm MD5).Hash
$deployedDllHash = if (Test-Path $deployedDll) { (Get-FileHash $deployedDll -Algorithm MD5).Hash } else { '' }

if ($builtDllHash -ne $deployedDllHash) {
    Write-Host "  ✗ Wizard.dll 未正确部署（构建 $($builtDllHash.Substring(0,8)) vs 部署 $($deployedDllHash.Substring(0,8))）" -ForegroundColor Red
    Write-Host "    最可能的原因：**游戏正在运行，锁住了 Wizard.dll**。" -ForegroundColor Yellow
    Write-Host "    ⚠️ 此时 pck 可能是新的、dll 却是旧的 —— 表现为「新卡图 + 旧逻辑」，极难排查。" -ForegroundColor Yellow
    Write-Host "    请关闭游戏后重跑本脚本。" -ForegroundColor Yellow
    throw "Wizard.dll 部署失败：与构建产物哈希不一致"
}

Write-Host "  Wizard.dll 校验通过（$($builtDllHash.Substring(0,8))）" -ForegroundColor Green

# 目录已在上面的存在性校验里确认过，这里不再 MakeDir ——
# 若目标不存在就直接报错，避免"悄悄建出一个没人读的目录"。
Copy-Item $PckOut $ModDir -Force

# ⚠️ 同样必须校验 pck，不能只信 Copy-Item 不报错。
# 游戏运行时 Wizard.pck 也会被锁住，复制可能失败却不抛异常，
# 于是脚本照样打印"已部署"，而游戏里跑的是**旧包**。
$srcHash = (Get-FileHash $PckOut -Algorithm MD5).Hash
$dstPck = Join-Path $ModDir (Split-Path $PckOut -Leaf)
$dstHash = if (Test-Path $dstPck) { (Get-FileHash $dstPck -Algorithm MD5).Hash } else { '' }

if ($srcHash -ne $dstHash) {
    Write-Host "  ✗ pck 复制未生效（源 $($srcHash.Substring(0,8)) vs 目标 $($dstHash.Substring(0,8))）" -ForegroundColor Red
    Write-Host "    最可能的原因：**游戏正在运行，锁住了 Wizard.pck**。" -ForegroundColor Yellow
    Write-Host "    请关闭游戏后重跑本脚本。" -ForegroundColor Yellow
    throw "pck 部署失败：目标文件与构建产物哈希不一致"
}

Write-Host "  已部署到 $ModDir（pck $($srcHash.Substring(0,8)) + dll $($builtDllHash.Substring(0,8)) 均校验通过）" -ForegroundColor Green

Get-ChildItem $ModDir | Select-Object Length, Name | Format-Table -AutoSize

# ══════════════════════════════════════════════════════════════════
# ✅ 部署到这里**就已经生效**：mods\Wizard 优先级高于工坊，且不受 Steam 影响。
# ══════════════════════════════════════════════════════════════════
#
# 下面只是**发布提醒**：工坊上那份（别人订阅时下载到的东西）更新，需要走上传。
# 你自己的机器跑的是 mods\Wizard，所以不必上传也能立刻测。
$wsDll = Join-Path $WorkshopDir 'Wizard.dll'
$wsPck = Join-Path $WorkshopDir 'Wizard.pck'
if ((Test-Path $wsDll) -and (Test-Path $wsPck)) {
    $sameDll = (Get-FileHash $wsDll -Algorithm MD5).Hash -eq $builtDllHash
    $samePck = (Get-FileHash $wsPck -Algorithm MD5).Hash -eq $srcHash
    Write-Host ''
    if ($sameDll -and $samePck) {
        Write-Host '   ✔ 工坊上那份也与本次构建一致。' -ForegroundColor Green
    } else {
        Write-Host '   ℹ 工坊上那份是旧版本（不影响你本机测试）。' -ForegroundColor DarkGray
        Write-Host "     工坊 dll=$((Get-FileHash $wsDll -Algorithm MD5).Hash.Substring(0,8))  pck=$((Get-FileHash $wsPck -Algorithm MD5).Hash.Substring(0,8))" -ForegroundColor DarkGray
        Write-Host "     本次 dll=$($builtDllHash.Substring(0,8))  pck=$($srcHash.Substring(0,8))" -ForegroundColor DarkGray
        Write-Host '     要让订阅者/联机队友拿到同一份，才需要走发布流程：' -ForegroundColor DarkGray
        Write-Host "       1) 把产物复制到 $UploaderDir" -ForegroundColor DarkGray
        Write-Host "       2) 运行 $UploaderRoot\ModUploader.exe（item id $WorkshopItemId）" -ForegroundColor DarkGray
    }
}


# ─────────────────────────────────────────────────────────────
# === 5/5 自动提交 ===
# ─────────────────────────────────────────────────────────────
# 构建完成后把源码 / 资源 / 文档的改动提交并推送到线上仓库，避免攒成一大坨。
# · build/ 与 src/.godot/ 已在 .gitignore 里，不会被提交
# · 提交或推送失败**不影响构建结果**，只打印警告
# · 用 -NoCommit 关闭本次自动提交
if ($NoCommit) {
    Write-Host ''
    Write-Step '5/5 自动提交（-NoCommit，跳过）'
} elseif (-not (Test-Path (Join-Path $RepoRoot '.git'))) {
    Write-Host ''
    Write-Step '5/5 自动提交（非 git 仓库，跳过）'
} else {
    Write-Host ''
    Write-Step '5/5 自动提交'

    $savedEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        Push-Location $RepoRoot

        $dirty = @(& git status --porcelain 2>&1)
        if ($dirty.Count -eq 0) {
            Write-Host '   工作区干净，无需提交' -ForegroundColor DarkGray
        } else {
            $changed = $dirty.Count
            $manifestPath = Join-Path $ProjectDir 'Wizard.json'
            # ⚠️ 必须显式 -Encoding UTF8：Windows PowerShell 5.1 的 Get-Content
            #    默认按 ANSI/GBK 解码 UTF-8 文件 ⇒ 中文变乱码 ⇒ JSON 解析报
            #    "传入的对象无效，应为':'或'}'"（2026-10-22 实测复现）。
            #    这里也刻意不用 ConvertFrom-Json，直接正则取 version，
            #    少一层对编码与 JSON 格式的敏感性。
            $manifestText = Get-Content $manifestPath -Raw -Encoding UTF8
            $version = [regex]::Match($manifestText, '"version"\s*:\s*"([^"]+)"').Groups[1].Value
            $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm'

            $msgLines = @(
                "build: v$version（dll $($builtDllHash.Substring(0,8)) / pck $($srcHash.Substring(0,8))）"
                ''
                "自动提交于 $stamp，共 $changed 项变更："
            ) + @($dirty | Select-Object -First 20 | ForEach-Object { "  $_" })
            if ($changed -gt 20) { $msgLines += "  …（其余 $($changed - 20) 项略）" }

            $msgFile = [System.IO.Path]::GetTempFileName()
            # ⚠️ 用 .NET 写 UTF-8 **无 BOM**：PS 5.1 的 Set-Content -Encoding UTF8 会写 BOM，
            #    BOM 会跟着进 git 提交信息（首行出现看不见的字符）。
            [System.IO.File]::WriteAllText(
                $msgFile,
                ($msgLines -join "`n"),
                (New-Object System.Text.UTF8Encoding($false)))

            & git add -A 2>&1 | Out-Null
            $commitOut = & git commit -q -F $msgFile 2>&1
            $commitCode = $LASTEXITCODE
            Remove-Item $msgFile -Force -ErrorAction SilentlyContinue

            if ($commitCode -ne 0) {
                Write-Host "   ⚠ commit 失败（退出码 $commitCode），改动已 add 但未提交" -ForegroundColor Yellow
                $commitOut | Select-Object -First 5 | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkGray }
            } else {
                $head = (& git log --oneline -1 2>&1 | Out-String).Trim()
                $remote = (& git remote 2>&1 | Out-String).Trim()
                if ($remote) {
                    & git push 2>&1 | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "   ✔ 已提交并推送：$head" -ForegroundColor Green
                    } else {
                        Write-Host '   ⚠ 已提交，但推送失败（本地已留档，稍后手动 git push）' -ForegroundColor Yellow
                        Write-Host '     若 HTTPS 被网络阻断（TLS connect error），把 remote 换成 SSH 再推：' -ForegroundColor DarkGray
                        Write-Host '       git remote set-url origin git@github.com:Traumere7/sts2mod-wizard.git' -ForegroundColor DarkGray
                    }
                } else {
                    Write-Host "   ✔ 已提交（无远端，未推送）：$head" -ForegroundColor Green
                }
            }
        }
    } catch {
        Write-Host "   ⚠ 自动提交异常（不影响构建结果）：$($_.Exception.Message)" -ForegroundColor Yellow
    } finally {
        Pop-Location
        $ErrorActionPreference = $savedEap
    }
}

Write-Host ''
Write-Host '启动游戏即可（本机加载 mods\Wizard，优先级高于工坊）。' -ForegroundColor Green
Write-Host '核对加载路径：' -ForegroundColor DarkGray
Write-Host '  Select-String -Path "$env:APPDATA\SlayTheSpire2\logs\godot.log" -Pattern "Loading (assembly DLL|Godot PCK).*Wizard"' -ForegroundColor DarkGray
