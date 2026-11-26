# 替换美术包（B 版）使用说明

本 mod 自带一套美术（下称 **A 版**，默认启用）。
另有一套**替换美术**（**B 版**）—— 从 v0.6.1 起，**B 版已经随创意工坊条目一起分发**
（工坊条目里除了 `Wizard.pck` 之外还有 `WizardAlt.pck`，约 131 MB）。

> B 版**默认不启用**，需要玩家自己在 mod 设置里勾选；两套美术都不影响卡牌逻辑与联机。

---

## 一、启用 B 版（工坊用户）

1. 订阅工坊条目并等待下载完成（条目总大小约 295 MB，已含 B 版）。
2. 启动游戏 → 主菜单 → **设置（Settings）** → mod 设置里的 **巫师 / Wizard** →
   勾选 **「使用替换美术（勾选后需重启游戏）」**。
3. **完全退出游戏，再重新启动**（这个开关只在启动时读一次，之后改美术不重启不生效）。

看到卡图 / 头像 / 立绘变了就成功了。

> 本地安装（不用工坊）的玩家：把 `WizardAlt.pck` 放在 `mods\Wizard\` 里（与 `Wizard.pck` 同目录）即可，其余步骤相同。

---

## 二、切回 A 版

- 打开设置，**取消勾选**那一项 → **重启游戏**；
- 或者直接把 `WizardAlt.pck` 删掉 / 改名（这样即使勾着也只会用 A 版，日志里会有一条提示）。

两种方式都**不会**改动游戏本体的任何文件。

---

## 三、常见问题

**Q：勾了但没变化？**
- 99% 是**没重启**。这个开关只在游戏启动时读一次。
- 看一眼日志（见下）确认包是否被找到、挂载是否成功。

**Q：日志里怎么看有没有生效？**
打开 `%APPDATA%\SlayTheSpire2\logs\godot.log`，搜索 `美术：`：
```
[Wizard] 美术：使用 mod 自带版本（设置里未开启替换美术）
[Wizard] 美术：已挂载替换美术包 ...\workshop\content\2868840\3807136195\WizardAlt.pck   ← 工坊订阅 + B 版
[Wizard] 美术：已挂载替换美术包 ...\mods\Wizard\WizardAlt.pck                            ← 本地安装 + B 版
[Wizard] 美术：已开启替换美术，但下列位置都没有 WizardAlt.pck ...                        ← 勾了但文件不在，退回 A
```

**Q：会和联机冲突吗？**
不会。美术只影响你本地显示，卡牌 ID 与逻辑不变，双方谁用哪套都行。

**Q：工坊更新会不会把 B 版弄没？**
只要作者每次更新条目时都把 `WizardAlt.pck` 一起上传（当前发布流程就是这样），就会一直保留在条目里。
如果某次更新后日志里提示"没找到 WizardAlt.pck"，取消勾选即可正常游玩，等作者补传。

**Q：新卡的 B 版？**
B 版只覆盖 `art_alt\` 里放过的资源；后续新增卡牌若还没画 B 版，使用 B 版时会自动回退到 A 版对应卡图（不报错）。

**Q：B 版能只放一部分吗？**
可以，覆盖包是按文件覆盖的；缺哪张就用 A 版。

---

## 四、给作者：B 版怎么出包 / 发布

素材放 `art_alt\Wizard\`，路径必须与 mod 内 `res://` 一致：

```
art_alt\
└── Wizard\
    ├── images\cards\*.png         所有卡图
    ├── images\ui\*.png            巫师图标：头像、地图标记、角色选择图、能量图标……
    └── images\characters\*.png    战斗 / 火堆 / 死亡 立绘
```

**直接跑 `build.ps1`（不加 `-SkipAltArt`）**，第 5 步会自动出包并部署：

```
=== 5/6 打包替换美术包（B 版，可选） ===
  源：...\art_alt（512 张 png）
  ✅ 覆盖包已生成：build\WizardAlt.pck  137,304,004 B
  WizardAlt.pck 生成并部署成功（130.9 MB，MD5 0E2B7376）
```

细节：
- 源目录缺 / 没有 png / 缺 python / 缺打包工具 / 加 `-SkipAltArt` ⇒ **跳过**，且**不会删除**已部署的 `WizardAlt.pck`。
- 产物先落 `build\WizardAlt.pck`（做哈希校验），再复制到 `mods\Wizard\`。
- 单独出包：`python reference\tools\make_alt_art_pck.py art_alt build\WizardAlt.pck`

### 发布到创意工坊（2026-11-26 起）

`WizardAlt.pck` **随主条目一起上传**：

```powershell
# 1) 把 4 个文件都复制进上传器 content（含 B 包）
Copy-Item D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Wizard\Wizard{.dll,.pck,.json} `
          D:\Downloads\ModUploader-win-x64\WizardWorkspace\content\ -Force
Copy-Item D:\Projects\STS2\Wizard\build\WizardAlt.pck `
          D:\Downloads\ModUploader-win-x64\WizardWorkspace\content\ -Force

# 2) 上传
cd D:\Downloads\ModUploader-win-x64
.\ModUploader.exe upload -w .\WizardWorkspace
```

⚠️ **每次更新都必须把 `WizardAlt.pck` 继续放在 `content\` 里**：
Steam 用新内容替换条目的文件清单，漏传一次订阅者的 B 包就会被移除。
⚠️ 条目总大小 ≈ 295 MB（`Wizard.pck` 172 MB + `WizardAlt.pck` 131 MB）。
⚠️ 上传完成后可用 Steam Web API 核对 `file_size` 是否包含这 131 MB。

### 加载顺序（为什么工坊条目里放一份就能用）

`Entry.MountAltArtPack()` 依次查找：
1. **DLL 同目录** → 本地安装是 `mods\Wizard\`；工坊订阅是 `workshop\content\2868840\3807136195\`
2. `%APPDATA%\SlayTheSpire2\WizardAlt.pck`（手动放置的兜底）

游戏本体 `ModManager` 只挂 `modId + ".pck"`（即 `Wizard.pck`），
同目录多余的 `WizardAlt.pck` **不会**被自动挂载 —— 只有勾选设置后，
我方代码才用 `ProjectSettings.LoadResourcePack(..., replaceFiles: true)` 把它盖上去。

⚠️ 不要包含：遗物、事件、怪物的贴图（B 版范围不含它们）。
⚠️ 覆盖包里**不要**出现 `project.godot`、`.godot/uid_cache.bin`、`.godot/editor/`（脚本已自动剔除）。
