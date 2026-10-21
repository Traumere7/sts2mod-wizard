# 巫师（Wizard）—《杀戮尖塔 2》角色 Mod

为 **Slay the Spire 2** 添加一名全新角色「巫师」：以生命与理智为代价，换取毁天灭地的奥术之力。

基于 [**BaseLib**](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127) 开发。

| | |
|---|---|
| **创意工坊** | [巫师 / Wizard（id 3807136195）](https://steamcommunity.com/sharedfiles/filedetails/?id=3807136195) |
| **当前版本** | `0.3.6` |
| **游戏版本** | 0.111.0（`min_game_version` 0.107.1） |
| **依赖** | BaseLib **≥ 3.4.7** |
| **语言** | 简体中文 / English |

---

## 内容量

| 项 | 数量 |
|---|---|
| 卡牌 | **60** 张（含初始「打击」「防御」、衍生牌与状态牌） |
| 能力 | 26 个 |
| 遗物 | 9 个 |
| 药水 | 3 瓶 |
| 专属事件 | 1 个（「尖塔规则怪谈」） |
| 先古之民对话 | 全部 **9 位**，每位 3 套 + 可重复兜底 |

**三系法术**：火焰（9 张）、雷电（6 张）、寒冰（2 张），共 17 张法术牌。
伤害结算与增益走 [SpellSchool](src/Scripts/Spells/SpellSchool.cs)（「法术增益」提升伤害，
「寒冷」提升火焰与雷电，雷电另受「超导」影响）。

---

## 快速开始

### 方式一：直接下载构建产物

从 [**Releases**](https://github.com/Traumere7/sts2mod-wizard/releases) 下载当前版本的压缩包，
解压到游戏目录下的 `mods\Wizard\`，确保三个文件齐全：

```
Slay the Spire 2\mods\Wizard\
├── Wizard.dll
├── Wizard.json
└── Wizard.pck
```

同时还需要 `mods\BaseLib\`（创意工坊订阅即可）。

> 提交 `.pck` 到 git 会超过 GitHub 的单文件 100 MB 上限，所以二进制产物走 Release 附件
> （附件单文件上限 2 GiB）。仓库里只存源码与资源。

### 方式二：从源码构建

```powershell
# 一键构建 + 部署 + 自动提交
.\build.ps1

# 只编译，不打包 pck
.\build.ps1 -SkipPck

# 不在构建后自动 git 提交
.\build.ps1 -NoCommit
```

`build.ps1` 依次做五件事：

1. `dotnet build` 编译 C#（`net9.0`，与游戏运行时一致）
2. `godot --headless --import` 刷新资源导入
3. 用 **GDRE Tools** 打包 `Wizard.pck`（**不需要装 Godot 导出模板**）
4. 部署到 `…\Slay the Spire 2\mods\Wizard\`，并逐个文件做哈希校验
5. **自动提交**：把源码/资源/文档的改动提交并推送到本仓库（可用 `-NoCommit` 关闭）

### 环境依赖

| 项 | 路径 / 版本 |
|---|---|
| 游戏 | `D:\SteamLibrary\steamapps\common\Slay the Spire 2`（v0.111.0） |
| BaseLib | 创意工坊 `3737335127`，**≥ 3.4.7**（放到游戏 `mods\BaseLib\`） |
| .NET SDK | 10.x（工程 target `net9.0`） |
| Godot | `D:\Godot\Godot_v4.5.1-stable_mono_win64\`（4.5.1 stable mono） |
| GDRE Tools | `D:\Godot\GDRE_tools-v2.6.0-windows\gdre_tools.exe` |

路径都在 `build.ps1` 顶部的变量里，换机器时改那里即可。

---

## 目录结构

```
.
├── README.md                 本文件
├── build.ps1                 一键构建：编译 → 导入 → 打包 → 部署 → 提交
├── docs/                     ★ 设计与技术文档（design.md 是内容权威来源）
│   ├── design.md             卡牌 / 遗物 / 药水 / 事件的**唯一权威**设计文档
│   ├── CONTEXT.md            项目记忆：约定、已踩的坑、接口调研结论
│   ├── HANDOVER.md           交接清单
│   ├── EVENT-INTERFACE.md    事件接口调研与实现记录
│   ├── HEALTHBAR-FORECAST.md 血条预测叠加层
│   ├── RELICS-REFERENCE.md   遗物参考
│   ├── CARD_POOL_REVIEW.md / DEBUFF-FEASIBILITY.md / MECHANICS_FEASIBILITY.md / CHARACTER-ART.md
│   └── …
└── src/                      ★ Godot 工程根目录（= res://）
    ├── project.godot
    ├── Wizard.csproj
    ├── Wizard.json           mod 清单（id / 版本 / 依赖）
    ├── icon.svg
    ├── Scripts/              C# 源码
    │   ├── Entry.cs          mod 入口（[ModInitializer]）
    │   ├── Cards/            60 张卡牌
    │   ├── Powers/           26 个能力
    │   ├── Relics/ Potions/ 遗物与药水
    │   ├── Events/           事件
    │   ├── Spells/           法术流派与伤害结算
    │   ├── Character/ Pools/ 角色定义与三个内容池
    │   └── …
    └── Wizard/               mod 资源（文件夹名**必须**等于 mod id）
        ├── images/           卡图 / 能力图标 / 遗物图 / 药水图 / 场景图
        ├── localization/
        │   └── {zhs,eng}/    cards / powers / relics / potions / events / ancients
        └── scenes/           Godot 场景
```

**不入版本库**（已在 `.gitignore` 中）：`art/`（原始美术素材与 prompt）、
`logs/`、`reference/`（约 3.2 GB 的离线知识库）、`build/`（含 100 MB+ 的 `Wizard.pck`）、
`src/.godot/`（Godot 导入缓存）。

---

## 关键约定

### 内容 ID 与本地化键

BaseLib 把 `{命名空间 UPPER_SNAKE 前缀}-{类名 UPPER_SNAKE}` 作为 `ModelId.Entry`，
**连字符属于 Entry 本身**：

| 类 | Entry | 本地化键 |
|---|---|---|
| `Wizard.Scripts.Cards.Animism` | `WIZARD-ANIMISM` | `WIZARD-ANIMISM.title` / `.description` / `.flavor` |
| `Wizard.Scripts.Powers.BurnPower` | `WIZARD-BURN_POWER` | `WIZARD-BURN_POWER.description` |
| `Wizard.Scripts.Events.RulesHorror` | `WIZARD-RULES_HORROR` | `WIZARD-RULES_HORROR.*` |

> ⚠️ 原版游戏的本地化键**没有前缀**（如 `IRONCLAD.title`），前缀与连字符是 BaseLib 给 mod 加的。
> 先古对话的键同样用带连字符的 Entry 原文。
> **改名一张卡要同步改三处**：类名、卡图文件名、本地化键。

### 卡面文案格式

- **数字不加颜色标记**（`[blue]` 不是给数字用的）
- `[blue]` 只用于两个**关键字**：`雷电伤害` / `寒冰伤害`（英文 `Lightning damage` / `Ice damage`）
- 专有名词/词条用 `[gold]`，法术增益用 `[purple]`，火焰伤害用 `[orange]`
- 数值跟随升级用 `{Var:diff()}`；能量一律用图标 `{Energy:energyIcons()}`，不写「费」字
- 句号后换行
- 词条（消耗/保留/虚无…）由 `CanonicalKeywords` 自动渲染，**不要**在描述里重复写

### 设计文档是权威来源

**卡牌效果与文案以 `docs/design.md` 为准**，不要擅自改动；
`docs/design.md` 里括号是**数字**表示升级数值，是**词条名**表示"升级后获得该词条"。

---

## 未完成 / 已知缺口

- 角色本体美术仍是**占位**（`PlaceholderCharacterModel`，未替换的资源回退到原版铁甲战士）
- 遗物 `PurifySin` 的图仍是占位
- 部分能力图标是占位图

---

## 开发知识库

开发期用过一套离线知识库（反编译源码、官方教程与 Wiki 快照），约 3.2 GB，
**不入版本库**，需要时在本地 `reference/` 保留即可。在线地址：

- 教程站 <https://tutorials.sts2modding.com/>
- BaseLib Wiki <https://alchyr.github.io/BaseLib-Wiki/>

排查问题主要看 `%APPDATA%\SlayTheSpire2\logs\godot.log`（游戏内按 `~` 开控制台）。
