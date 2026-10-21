# 巫师（Wizard）—《杀戮尖塔 2》角色 Mod

一个基于 **BaseLib** 的全新角色 mod：为《杀戮尖塔 2》添加角色「巫师」，包含卡牌、专属遗物、药水、先古之民对话等一整套角色内容。

---

## 当前状态

**可运行骨架已就绪**：工程可以编译、打包并成功部署到游戏 `mods` 目录。
角色本体、三卡池、初始卡组（4 打击 + 4 防御）、初始遗物、一瓶药水、
**9 位先古之民的完整对话（各 3 套 + 可重复兜底）**、全套 Godot 场景均已就位（美术为占位素材）。

已通过的验证：

| 验证项 | 结果 |
|---|---|
| `dotnet build` | ✅ 0 警告 0 错误 |
| Godot headless 资源导入 | ✅ 12 个资源全部导入，无报错 |
| PCK 打包与内容结构 | ✅ 逐条核对 `res://` 路径正确 |
| 部署到游戏 mods 目录 | ✅ dll + json + pck 三件齐全 |
| 本地化 JSON 合法性 | ✅ 中英各 113 键，全部可解析 |
| 本地化键格式 | ✅ 已对照观者 mod 真实发布数据核实 |

**尚未验证**：还没有实际启动游戏跑过。下一步应该在游戏的「模组」界面确认 Wizard 已加载，
并进入角色选择界面看巫师能否正常出现。

### 最可能出问题的地方（首次实测时优先看这里）

1. **角色 ID 与本地化键的对应**：若角色名显示为 `WIZARD-WIZARD_CHARACTER.title`
   这类原始键名，说明 ID 推导与预期不符。可用控制台命令验证，或临时加 `[CustomID]` 固定 ID。
2. **能量表盘场景结构**：`wizard_energy_counter.tscn` 的节点名必须精确匹配
   `EnergyVfxBack` / `Layers`(含 `RotationLayers`) / `EnergyVfxFront` / `Label`，否则表盘不显示。
3. **卡图路径**：`CustomPortraitPath` 约定为 `res://Wizard/images/cards/{类名}.png`，
   新增卡牌时别忘了放同名图片。
4. **内容注册时机**：`Entry.Initialize()` 里已调用 `ScriptManagerBridge.LookupScriptsInAssembly`
   与 `Harmony.PatchAll`，BaseLib 的自动注册依赖它。

调试手段：游戏内按 `~` 打开控制台；`reference\docs\tutorials-src\Basics\07 - 快速调试&热重载\`
与 `Basics\09 - 控制台指令\` 有完整说明；`Basics\12 - 触发顺序\` 讲钩子执行顺序。


---

## 目录结构

```
D:\Projects\STS2\Wizard\
├── HANDOVER.md                  ★ 交接清单：要换的美术 / 文案 / 内容进度
├── build.ps1                    一键构建：编译 → 打包 pck → 部署到游戏
├── README.md                    本文件
├── src\                         ★ Godot 工程根目录（= res://）
│   ├── project.godot
│   ├── Wizard.csproj
│   ├── Wizard.json              mod 清单（id / 版本 / 依赖声明）
│   ├── icon.svg
│   ├── Scripts\                 C# 源码
│   │   ├── Entry.cs             mod 入口 [ModInitializer]
│   │   ├── Cards\               打击 / 防御 / 卡牌基类
│   │   ├── Character\           巫师角色定义
│   │   ├── Pools\               卡池 / 遗物池 / 药水池
│   │   ├── Relics\              初始遗物
│   │   └── Potions\             药水
│   └── Wizard\                  ★ mod 资源（文件夹名必须 = modid）
│       ├── scenes\              6 个 Godot 场景
│       ├── images\              卡图 / 图标 / 遗物图 / 药水图
│       └── localization\{zhs,eng}\   中英文文案
├── build\                       构建产物（Wizard.pck）
└── reference\                   ★ 开发知识库（不参与构建）
    ├── ENVIRONMENT.md           环境勘察报告
    ├── docs\                    官方文档原始 Markdown
    │   ├── tutorials-src\       教程站源码（190 篇 md，中英双语）
    │   └── baselib-wiki-src\    开发者 Wiki 源码（38 篇 md）
    ├── decompiled\              反编译源码
    │   ├── game\                原版游戏 v0.111.0（3538 个 .cs）
    │   ├── BaseLib\             BaseLib 3.4.7（521 个 .cs）
    │   └── Watcher\             观者 mod 1.5.10（208 个 .cs）★ 最佳范本
    ├── archive\                  解包/还原出的原始数据（只读参考）
    │   ├── game-recovery\        原版 PCK 还原出的完整工程
    │   │                         （3591 脚本 + 4029 资源，含 16 语言本地化文案）
    │   └── watcher-extract\      观者 mod 解包出的资源与**真实本地化文案** ★
    └── tools\make_placeholders.py  占位图生成脚本
```

> ⚠️ `src\Wizard\` 这一层文件夹名**必须**等于 `Wizard.json` 里的 `id`，
> 这是游戏与 BaseLib 查找 mod 资源（`res://Wizard/...`）的依据。

---

## 构建

```powershell
# 一键构建 + 部署
& .\build.ps1

# 只编译，不打包
& .\build.ps1 -SkipPck
```

构建脚本做四件事：
1. `dotnet build` 编译 C#（编译后 csproj 会自动把 dll/json 复制到游戏 mods 目录）
2. `godot --headless --import` 刷新资源导入
3. 用 **GDRE Tools** 打包 `Wizard.pck`（**不需要安装 Godot 导出模板**）
4. 把 pck 部署到 `D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Wizard\`

### 为什么用 GDRE 而不用 Godot 官方导出？

官方 `--export-pack` 需要约 1.2 GB 的 mono 导出模板，而本机 `export_templates` 目录为空。
GDRE Tools 的 `--pck-create` 可以直接从工程目录生成 PCK，**实测可用且产物结构正确**。

若之后你装了导出模板，`Wizard.csproj` 里已预留 `ExportPck` 目标，`dotnet publish` 即可改用官方导出。

---

## 环境依赖

| 项 | 路径 / 版本 |
|---|---|
| 游戏 | `D:\SteamLibrary\steamapps\common\Slay the Spire 2`（v0.111.0） |
| BaseLib | 创意工坊 `3737335127`，**v3.4.7**（已复制到游戏 `mods\BaseLib\`） |
| .NET SDK | 10.0.302（工程 target `net9.0`，与游戏一致） |
| Godot | `D:\Godot\Godot_v4.5.1-stable_mono_win64\`（4.5.1 stable mono） |
| GDRE Tools | `D:\Godot\GDRE_tools-v2.6.0-windows\gdre_tools.exe` |

游戏 `mods\` 目录下需要同时存在 `BaseLib` 和 `Wizard` 两个文件夹。

---

## 关键设计说明

### 角色资源全部走占位继承

`WizardCharacter` 继承 `PlaceholderCharacterModel`，默认借用原版铁甲战士的全部资源
（地图标记、多人模式手臂、音效、卡牌拖尾、选人转场等）。

已替换为巫师自己场景的部分：战斗人物、能量表盘、商店人物、篝火人物、选人背景、顶部头像。
其余未替换的资源会**自动回退到原版铁甲战士**，不需要写任何代码。

等美术就绪后，只要重写对应的 `Custom*` 属性即可平滑替换。

### 内容 ID 生成规则（已实证核实）

BaseLib 的 `PrefixIdPatch` 把 `{命名空间的 UPPER_SNAKE 前缀}-{类名的 UPPER_SNAKE}` 作为 **`ModelId.Entry`**。
关键点：**连字符属于 Entry 本身**，而不是仅在 ID 的字符串表示里出现。
这一点已用创意工坊观者 mod 的真实发布数据交叉验证
（`reference\archive\watcher-extract\` 里有解包出来的原文）。

| 类 | 生成的 Entry | 本地化键 |
|---|---|---|
| `Wizard.Scripts.Cards.StrikeWizard` | `WIZARD-STRIKE_WIZARD` | `WIZARD-STRIKE_WIZARD.title` |
| `Wizard.Scripts.Cards.DefendWizard` | `WIZARD-DEFEND_WIZARD` | `WIZARD-DEFEND_WIZARD.title` |
| `Wizard.Scripts.Relics.WizardStarterRelic` | `WIZARD-WIZARD_STARTER_RELIC` | `…​.title` |
| `Wizard.Scripts.Potions.WizardBloodPotion` | `WIZARD-WIZARD_BLOOD_POTION` | `…​.title` |
| `Wizard.Scripts.Character.WizardCharacter` | `WIZARD-WIZARD_CHARACTER` | `…​.title` |

**先古对话的键同样使用这个带连字符的 Entry 原文**（不是下划线形式）：

```
DARV.talk.WIZARD-WIZARD_CHARACTER.0-0.ancient
```

> ⚠️ 这是个很容易踩的坑：原版游戏的本地化键**完全没有前缀**（例如 `IRONCLAD.title`），
> 前缀与连字符是 BaseLib 为 mod 内容额外加的。写文案时务必对齐上表。

> 也可以在类上加 `[CustomID("...")]` 自定义 ID，此时前缀不会被添加，
> 本地化键要相应改为该自定义值。它同时也会影响先古对话的键。


---

## 待办

- [ ] **实测**：启动游戏，确认模组加载、角色可选、初始卡组正确
- [ ] 替换巫师角色美术（Spine 骨骼或帧动画）与音效
- [ ] 设计并实现巫师的核心机制与完整卡池
- [ ] 补全遗物池与药水池内容
- [ ] 补全先古对话的 `firstVisitEver` 与 `ANY` 兜底文案
- [ ] 上传创意工坊

---

## 开发知识库

`reference\` 下是完整的离线知识库，开发时优先查这里：

1. **`reference\docs\tutorials-src\BaseLib\`** — 通俗教程，16 章，覆盖卡牌/遗物/药水/能力/先古/人物等
2. **`reference\docs\baselib-wiki-src\`** — 开发者 Wiki，接口级细节
3. **`reference\decompiled\Watcher\`** — 观者 mod，**完整角色 mod 的最佳范本**，从角色定义到 VFX 一应俱全
4. **`reference\decompiled\BaseLib\`** — BaseLib 自身实现，可确认确切的行为与可重写成员
5. **`reference\decompiled\game\`** — 原版实现，写卡牌效果时找相似的卡参考

在线地址（也可直接访问）：
- <https://tutorials.sts2modding.com/>
- <https://alchyr.github.io/BaseLib-Wiki/>
