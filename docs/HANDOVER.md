# 巫师 Mod — 交接文档

> 本文档只讲三件事：**要换哪些美术**、**要换哪些文案**、**内容做到哪一步了**。
> 环境、构建方式、代码设计见 `README.md`；接口细节见 `reference/ENVIRONMENT.md`。

---

## 一句话现状

**角色机制（含最难的「冻结」）已全部实现并实测通过；卡牌 17 / 32。**
角色能跑起来，但**美术仍是占位**——借用原版铁甲战士，文案是我写的临时稿。

---

# 第一部分：需要替换的美术素材

## 1.1 我生成的占位图（11 张，可直接覆盖同名文件）

这些是纯色几何图形的临时货，用来让资源路径能解析。**替换时直接覆盖同名文件即可，不用改代码。**

| # | 文件路径（相对 `src/Wizard/`） | 当前尺寸 | 建议正式尺寸 | 用途 / 引用位置 |
|---|---|---|---|---|
| 1 | `images/cards/StrikeWizard.png` | 250×190 | **1000×760** | 打击卡图 |
| 2 | `images/cards/DefendWizard.png` | 250×190 | **1000×760** | 防御卡图 |
| 3 | `images/relics/wizard_starter_relic.png` | 85×85 | **256×256** | 初始遗物图标 |
| 4 | `images/potions/wizard_blood_potion.png` | 85×85 | **256×256** | 药水图标 |
| 5 | `images/potions/wizard_blood_potion_outline.png` | 85×85 | **256×256** | 药水轮廓图（未知药水时的剪影） |
| 6 | `images/ui/char_icon_wizard.png` | 256×256 | **88×88** | 顶部面板头像、角色统计页头像 |
| 7 | `images/ui/char_select_wizard.png` | 256×256 | **132×195** | 角色选择界面立绘 |
| 8 | `images/ui/char_select_wizard_locked.png` | 256×256 | **132×195** | 同上，未解锁状态（剪影） |
| 9 | `images/ui/map_marker_wizard.png` | 128×128 | **49×64** | 地图上的角色标记、表情轮盘头像 |
| 10 | `images/ui/energy_wizard.png` | 24×24 | **24×24** ✅ | 描述文本里的能量图标 |
| 11 | `images/ui/energy_wizard_big.png` | 74×74 | **74×74** ✅ | tooltip / 卡牌左上角的能量图标 |

> **建议尺寸的依据**：3、4、5 号取自原版同类素材实测（`burning_blood.png` = 256×256）；
> 1、2 号取自原版卡图实测（`strike_ironclad.png` = 1000×760）；
> 6～9 号取自原版角色素材实测（88×88 / 132×195 / 49×64）；
> 10、11 号来自 BaseLib 教程文档的明确要求。
> **尺寸不必严格相等**——游戏会自动缩放，但保持宽高比能避免拉伸。
> 注意 6 号和 7 号原版是**不同宽高比**（正方形 vs 竖长方形），别混用同一张图。

### 1.1b 后补的占位图（8 张，2026-09 新增）

上表的 11 张是**最早**生成的。之后新增了卡牌与能力，又补了下面 8 张。
两批画风一致（深色底 + 同色几何符号），配色严格取自 `art/art_design.md`
§5.2（三系色）与 §5.3（状态色），底色统一 `#1C142E`。

| # | 文件路径（相对 `src/Wizard/`） | 尺寸 | 用途 |
|---|---|---|---|
| 1 | `images/cards/Determination.png` | 1000×760 | 决心 `Determination` 卡图 |
| 2 | `images/cards/Concentrate.png` | 1000×760 | 凝神 `Concentrate` 卡图 |
| 3 | `images/powers/burning_soul_power.png` | 64×64 | 燃烧之魂 小图标 |
| 4 | `images/powers/burning_soul_power_big.png` | 128×128 | 燃烧之魂 大图标 |
| 5 | `images/powers/circulation_power.png` | 64×64 | 环流 小图标 |
| 6 | `images/powers/circulation_power_big.png` | 128×128 | 环流 大图标 |
| 7 | `images/powers/concentrate_power.png` | 64×64 | 凝神 小图标 |
| 8 | `images/powers/concentrate_power_big.png` | 128×128 | 凝神 大图标 |

> **能力图标必须成对提供**：代码里 `CustomPackedIconPath`（64×64，
> 战斗中的状态条）与 `CustomBigIconPath`（128×128，悬停详情）是两个独立路径，
> 只给一个会让另一处显示空白。

生成脚本：`reference/tools/make_new_placeholders.py`（只生成上面这 8 张，
不会覆盖别人的卡图）。

> ⚠️ 卡图标签用的是**英文**：占位图里的 TTF 不含中文字形，
> 写中文会渲染成豆腐块。中文卡名本来由游戏在卡框上另外显示，不影响。

> 📌 **历史遗留**：`make_placeholders.py` 里卡图仍是 250×190，
> 与现有 1000×760 不一致。现在 17 张已实现卡**全部有图**，
> 所以那个脚本的卡图分支已无实际用途，不必再跑。

### 占位图怎么来的

`reference/tools/make_placeholders.py`（Python + PIL）。改完脚本重跑一次就能重新生成全部占位图：

```powershell
python reference\tools\make_placeholders.py
```

## 1.2 场景里内嵌的占位贴图（6 个 .tscn）

所有场景目前都用 `res://icon.svg`（我画的紫色巫师帽）当贴图。**这些需要你在 Godot 编辑器里打开场景替换**。

| 场景文件 | 用途 | 需要做的事 |
|---|---|---|
| `scenes/wizard_character.tscn` | 战斗人物 | 把 `Visuals`(Sprite2D) 换成你的角色动画节点 |
| `scenes/wizard_energy_counter.tscn` | 能量表盘 | 替换 `Layers/Layer1` 的贴图；可增设旋转图层 |
| `scenes/wizard_character_merchant.tscn` | 商店里的巫师 | 换成你的动画节点 |
| `scenes/wizard_character_rest_site.tscn` | 篝火旁的巫师 | 换成你的动画节点 |
| `scenes/wizard_bg.tscn` | 角色选择背景 | 换 `Control/Icon` 贴图；可调 `ColorRect` 底色与两个粒子节点 |
| `scenes/wizard_icon.tscn` | 左上角头像场景 | 换贴图（当前是方块占位） |

### ⚠️ 场景结构有硬性约束，改的时候别动节点名

`wizard_character.tscn` 与 `wizard_energy_counter.tscn` 的节点名是 BaseLib 按名字查找的，
**改名或不勾选「作为唯一名称访问」(% 号) 就会失效**：

```
wizard_character.tscn        必须是以下结构，这 4 个节点不能改名、必须勾选 %
  WizardCharacter (Node2D)
  ├── Visuals   (可换成 SpineSprite / AnimatedSprite2D / Sprite2D / AnimationPlayer)
  ├── Bounds    (Control，= 血条长度和 hitbox 大小)
  ├── IntentPos (Marker2D)
  └── CenterPos (Marker2D)

wizard_energy_counter.tscn   同上
  WizardEnergyCounter (Control)
  ├── EnergyVfxBack (Node2D) %
  ├── Layers (Control) %
  │   ├── Layer1 (TextureRect)
  │   └── RotationLayers (Control) %   ← 要旋转的图层放这里
  ├── EnergyVfxFront (Node2D) %
  └── Label (Label)                    ← 数字文本，名字也不能改
```

`wizard_character_rest_site.tscn` 需要这 4 个节点名（勾选 %）：
`ControlRoot` / `SelectionReticle` / `Hitbox` / `ThoughtBubbleRight` / `ThoughtBubbleLeft`。

**这些场景目前都没有挂脚本**，资源由 BaseLib 自动转换，不需要你手动挂。

## 1.3 仍在借用铁甲战士的资源（看不到但存在）★ 最容易漏

角色继承的是 `PlaceholderCharacterModel`，**下面这些我没写代码，所以游戏会自动用铁甲战士的**。
它们不出现在你的文件列表里，容易漏掉。要替换就给 `WizardCharacter.cs` 加上对应的
`public override string? CustomXxx => "...";`。

| 资源 | 对应属性 | 现在的效果 |
|---|---|---|
| **Spine 骨骼动画** | （由 `wizard_character.tscn` 的 `Visuals` 节点类型决定） | 整个战斗动画都是铁甲战士的 |
| 卡牌拖尾 | `CustomTrailPath` | 铁甲战士的红色拖尾 |
| 音效：攻击 | `CustomAttackSfx` | 铁甲战士攻击音效 |
| 音效：施法 | `CustomCastSfx` | 铁甲战士施法音效 |
| 音效：死亡 | `CustomDeathSfx` | 铁甲战士死亡音效 |
| 音效：角色选择 | `CharacterSelectSfx` | 铁甲战士选择音效 |
| 音效：转场 | `CharacterTransitionSfx` | `wipe_ironclad`（**这个不能为空，当前是继承值**） |
| 多人模式：手指 | `CustomArmPointingTexturePath` | 铁甲战士的手 |
| 多人模式：石头 | `CustomArmRockTexturePath` | 同上 |
| 多人模式：布 | `CustomArmPaperTexturePath` | 同上 |
| 多人模式：剪刀 | `CustomArmScissorsTexturePath` | 同上 |
| 选人转场材质 | `CustomCharacterSelectTransitionPath` | `ironclad_transition_mat.tres` |
| 顶部头像描边 | `CustomIconOutlineTexturePath` | `character_icon_ironclad_outline.png`（**我还没提供对应文件**） |

> 想换 `PlaceholderID` 一次性借别的角色的全部资源也可以：加 `public override string PlaceholderID => "silent";`

### 关于 Spine 动画

要换成真正的巫师骨骼动画，需要：`Visuals` 节点类型改为 `SpineSprite`，
且骨骼里要有这些动画名：`idle_loop`、`attack`、`cast`、`hurt`、`die`。
若用帧动画则设 `AnimatedSprite2D`，动画名同上。若只有一张静态图，`Sprite2D` 即可。

参考：`reference/docs/tutorials-src/Visuals/01 - 卡图&Spine/` 与
`reference/docs/tutorials-src/BaseLib/14 - 添加新人物/`，
以及观者 mod 的 `reference/decompiled/Watcher/Watcher.Code.Nodes/WatcherNCreatureVisuals.cs`（20KB，最完整的实现）。

## 1.4 还没有的资源

| 缺失项 | 说明 |
|---|---|
| `images/ui/char_icon_wizard_outline.png` | 顶部头像描边，88×88。当前继承铁甲战士的 |
| 正式卡图 | 每新增一张卡都需要 `images/cards/{类名}.png`（见 §3.3 的约定） |
| 正式遗物图 | 每新增一个遗物需要 `images/relics/{id}.png`，256×256 |
| 正式药水图 | 每新增一瓶药水需要图 + 轮廓图两张，256×256 |

---

# 第二部分：需要替换的个性化文案

所有文案都在 `src/Wizard/localization/{zhs,eng}/` 下，中英双语结构完全对应。
**改文案不需要懂代码，但键名不能改**。

## 2.1 全部文案清单

| 文件 | 键数(每语言) | 内容 | 状态 |
|---|---|---|---|
| `characters.json` | 15 | 角色名、描述、代词、开局独白、多人闲聊 | 我写的临时稿 |
| `cards.json` | 4 | 打击 / 防御 两张卡的名字与描述 | 跟随原版数值 |
| `relics.json` | 3 | 初始遗物名 / 描述 / 风味文本 | **对应临时机制，必须重写** |
| `potions.json` | 2 | 药水名 / 描述 | **对应临时机制，必须重写** |
| `ancients.json` | 89 | 9 位先古之民 × 3 套对话 | 我原创，见 §2.3 |

## 2.2 必须随机制一起重写的文案 ★

**这两个是占位机制，文案改得再漂亮也没用，得先定机制。**

### 初始遗物（`relics.json` + `Relics/WizardStarterRelic.cs`）

- **当前机制**：战斗开始时获得 8 点格挡（直接抄的原版「锚」）
- **当前文案**：「奥术核心」/「战斗开始时，获得 8 点格挡」
- **要做的**：设计巫师自己的初始遗物机制 → 改代码 `BeforeCombatStart()` → 再写文案

### 药水（`potions.json` + `Potions/WizardBloodPotion.cs`）

- **当前机制**：恢复最大生命 20%（直接抄的原版「血瓶」）
- **当前文案**：「巫师之血」/「恢复最大生命值 20% 的生命」
- **要做的**：同上，先定机制再写文案

## 2.3 先古之民对话（`ancients.json`，89 键 × 2 语言）

**这部分需要你重点审阅** —— 内容是我原创的，不是官方文案。

已覆盖 **9 位先古之民**，每位 3 套对话：

| 先古之民 ID | 名字 | 套数 |
|---|---|---|
| `NEOW` | 涅奥 | `0-0` / `1-0r` / `2-0..2-2` |
| `DARV` | 达弗 | 同上 |
| `NONUPEIPE` | 诺努佩佩 | 同上 |
| `OROBAS` | 欧洛巴斯 | 同上 |
| `PAEL` | 佩尔 | 同上 |
| `TANX` | 坦克斯 | 同上 |
| `TEZCATARA` | 特斯卡特拉 | 同上 |
| `VAKUU` | 瓦库 | 同上 |
| `THE_ARCHITECT` | 建筑师 | 同上（仅 `r` 变体） |

**我写的角色设定**（写文案时的依据，**需要你确认或推翻**）：
一个为了知识烧掉自己藏书的学者，代价是记得每一本的内容却再也写不出来。
傲慢、疲惫、好奇多于残忍。对话里多次提及「烧书」这一意象。

**还没写的键**（可选，不写会走游戏默认行为）：

| 键 | 作用 | 现状 |
|---|---|---|
| `{先古}.talk.firstVisitEver.*` | 全游戏首次遇见该先古时的通用对话 | 未写，走原版 |
| `{先古}.talk.ANY.*` | 角色不被该先古认识时的兜底 | 未写，走原版 |
| `{先古}.talk.{角色}.N-visit` | 指定第几次遇见触发 | 未用，走默认递增 |
| `{先古}.talk.{角色}.N-attack` | 攻击建筑师动画，值可为 `Both`/`Architect`/`Player`/`None` | 未写 |

## 2.4 键名格式硬约束 ★ 已踩过坑

先古对话的键里**角色部分必须写成 `WIZARD-WIZARD_CHARACTER`**（带连字符，不是下划线）：

```
DARV.talk.WIZARD-WIZARD_CHARACTER.0-0.ancient     ✅
DARV.talk.WIZARD_WIZARD_CHARACTER.0-0.ancient     ❌ 不生效
```

原因：BaseLib 把 `{命名空间前缀}-{类名}` 整个作为 `ModelId.Entry`，
而先古对话的键直接拼接这个 Entry。**我已用创意工坊观者 mod 的真实发布数据交叉验证**：

```
"THE_ARCHITECT.talk.WATCHER-WATCHER.0-0.char"     ← 观者的真实键
```

同理，卡牌/遗物/药水的键见 §3.3。**改键名会导致文案不显示（游戏回退成原始键名）。**

## 2.5 不建议改的文案

- 先古之民自己的名字（`DARV.title` / `DARV.epithet`）—— 那是游戏本体的内容，不属于本 mod
- 打击 / 防御 的描述 —— 跟随原版数值，改了反而容易和实际效果不一致

---

# 第三部分：游戏内容实现进度

## 3.1 总览

| 内容类别 | 已完成 | 未开始 | 目标规模 |
|---|---|---|---|
| 卡牌 | **17 / 32** | 15 张 | 32 张（`design.md` 定稿） |
| 能力(Power) | **11 个** | — | 随卡牌增长 |
| 遗物 | 1 个临时 | 初始遗物系统 / 稀有度分级 | 8 个 |
| 药水 | 1 瓶临时 | — | 4 瓶 |
| 先古对话 | 9 位 × 3 套 | `firstVisitEver`/`ANY` | — |
| 角色机制 | 法术伤害 / 燃烧 / 麻痹 / 电流外溢 / 冻结 / 寒冷 / 法术增益 | — | 已定稿 |

## 3.2 框架部分（已完成 ✓）

- ✅ `Entry.cs` — mod 入口，`[ModInitializer]` + Godot 脚本注册 + **逐类应用 Harmony 补丁**（见下方注意事项）
- ✅ `WizardCharacter.cs` — 角色定义（75 血 / 99 金币 / 中立性别 / 奥术紫主题色）
- ✅ 三卡池 `WizardCardPool` / `WizardRelicPool` / `WizardPotionPool`（含主题色与能量图标路径）
- ✅ `WizardCardModel` / `WizardSpellCard` — 卡牌基类，统一卡图路径约定
- ✅ 6 个 Godot 场景 + 完整本地化目录结构
- ✅ 构建链路（编译 / 打包 / 部署）+ 中英双语本地化

### ⚠️ 补丁应用的注意事项（踩过坑）

`Entry.Initialize` **不能**直接用 `harmony.PatchAll(assembly)`：

- `PatchAll` 遇到一个解析不出目标的补丁类会**抛异常**，一路上抛到
  `ModManager.CallModInitializer`，把**整个 `Initialize` 中断** ——
  后面的代码全不执行，表现为"改了、编译了、部署了，游戏里毫无变化"，极具误导性。
- 现在改为**逐类** `CreateClassProcessor(type).Patch()` + 各自 try/catch，
  失败的只跳过自己，并逐个打印成功/失败。
- ⚠️ `Patch()` 返回**空列表**表示"类解析到了但一个方法都没打上"（目标为 null
  或签名不匹配）—— 这种情况**不抛异常、静默无效**，代码里已显式当失败报出。

**启动后先看日志确认补丁状态**：

```text
[Wizard] 补丁已应用：FrozenIntentPatch -> 1 个方法
[Wizard] 补丁已应用：ParticlesContainerRestartPatch -> 1 个方法
[Wizard] 补丁已应用：ParticlesContainerSetEmittingPatch -> 1 个方法
[Wizard] 补丁已应用：SpellIgnoresStrengthPatch -> 1 个方法
[Wizard] 补丁汇总：成功 4 个，失败 0 个。
```

另外：**Harmony 无法解析 Godot 的 virtual 生命周期方法**（如 `_Ready`）。
补丁目标要挑**类型唯一、非虚、签名无歧义**的方法 ——
详见 `reference/FEASIBILITY.md` §4.6.8。

## 3.3 卡牌（当前 17 / 32）

> ⚠️ **加新内容时最容易犯的错（已实际踩过，会导致游戏启动致命错误）**
>
> **每一个** `CustomCardModel` / `CustomRelicModel` / `CustomPotionModel` 的子类
> **都必须带 `[Pool(typeof(对应的池子))]`**，否则游戏在 `ModelDb.Init` 阶段实例化它时会抛异常，
> **整个游戏启动被中止**，弹出致命错误对话框。
>
> 关键点：**初始遗物也必须带**（即使 `Rarity = Starter` 根本不会被随机抽取）。
> 只有池子类（`CustomXxxPoolModel`）和角色类（`CustomCharacterModel`）不需要。
>
> 报错长这样：
> ```
> System.Exception: Model Xxx.Yyy must be marked with a PoolAttribute
>     to determine which pool to add it to.
>     at BaseLib.Patches.Content.CustomContentDictionary.AddModel(Type modelType)
> ```
> 日志位置：`%APPDATA%\SlayTheSpire2\logs\godot.log`
>
> **注意这个错误会中止模型扫描**，所以一次只报一个——修完一个可能暴露下一个。
> 加完新内容后建议对照检查所有模型类都有 `[Pool]`。

### 已完成（17 张）

| 卡牌 | 类名 | 费 | 类型 | 稀有度 | 效果 |
|---|---|---|---|---|---|
| 打击 | `StrikeWizard` | 1 | Attack | Basic | 造成 6 伤害（升级 +3） |
| 防御 | `DefendWizard` | 1 | Skill | Basic | 获得 5 格挡（升级 +3） |
| 火球术 | `Fireball` | 1 | Attack | Basic | 3(4) 火焰伤害 ×2，2 层燃烧 |
| 镇静 | `CalmMind` | 2 | Skill | Basic | 9(12) 格挡，1 层法术增益 |
| 火苗 | `FlameSpark` | 0 | Attack | Common | 6(9) 火焰伤害，1 层燃烧 |
| 观火 | `WatchFire` | 1 | Skill | Common | 8(11) 格挡，全体 1 层燃烧 |
| 炽焰术 | `BlazingFlame` | 2 | Attack | Uncommon | 6(7) 火焰伤害 ×3，3(5) 层燃烧 |
| 高温消毒 | `SterilizeByFire` | 0 | Skill | Uncommon | 移除自身所有负面，自加 1 燃烧，消耗（升级移除消耗） |
| 燃烧之魂 | `BurningSoul` | 1 | Skill | Uncommon | 每段攻击结算后插入等量火焰伤害 + 1 燃烧（不可递归）。升级＝加「保留」 |
| 决心 | `Determination` | 0 | Skill | Uncommon | 自加 1 易伤/1 脆弱/3 燃烧，获得 3(4) 费 |
| 雷光 | `LightningBolt` | 2 | Attack | Uncommon | 8(10) 雷电伤害，1(2) 麻痹，1 电流外溢 |
| 冰之城墙 | `IceWall` | 2 | Skill | Uncommon | 14(18) 格挡，本回合敌人每攻击一次获得 1 冻结 + 2 寒冷 |
| 超导 | `Superconduct` | 1 | Power | Uncommon | 对寒冷敌人的雷电伤害 +50%(75%) |
| 环流 | `Circulation` | 1(0) | Power | Uncommon | 电流外溢可对敌人自身结算 |
| 魔力汲取 | `ManaDrain` | 1(0) | Skill | Uncommon | 目标每有 1 种负面效果，获得 1 费 |
| 禁书目录 | `IndexOfForbiddenBooks` | 0 | Skill | Rare | 2 易伤/2 燃烧/2 麻痹/2 寒冷，消耗（升级移除消耗） |
| 凝神 | `Concentrate` | 1(0) | Power | Uncommon | 每消耗 1 张牌，获得 1 层法术增益 |
| 火刑 | `PyroJudgment` | 1 | Power | Uncommon | 回合开始给全体 3(4) 层燃烧，然后该数值 +1 |
| 纵火狂 | `Pyromaniac` | 2 | Power | Uncommon | 每当你给予燃烧，获得 1(2) 层法术增益 |
| 真理派对 | `TruthParty` | 1 | Skill | Uncommon | 给 2(3) 层燃烧；按场上最高燃烧层数抽牌，选至多该数量的牌消耗 |
| 占卜 | `Divination` | 1 | Skill | Uncommon | 抽 3(4) 张；选 1 张技能牌消耗，否则弃掉所有手牌 |
| 交流电 | `AlternatingCurrent` | 1 | Power | Rare | 每次雷电伤害额外给 1 层电流外溢。升级＝加「固有」 |
| 降雷 | `LightningFall` | 1 | Attack | Rare | 对全体 7(10) 雷电伤害；所有敌人负面效果每有 1 种类就再结算 1 次 |
| 静电 | `StaticCharge` | 1 | Attack | Common | 4(6) 雷电伤害 ×2 |
| 冰霰 | `IceShard` | 1 | Attack | Common | 随机对敌人 3 寒冰伤害 ×2(3)，每次命中给 1 层寒冷。消耗 |
| 全知形态 | `OmniscientForm` | 3 | Power | Rare | 虚无。给敌人本回合未给过的一种负面效果时，获得 1 费并抽 1(2) 张 |
| 落日 | `Sunset` | 1 | Attack | Ancient | 虚无。对全体 5 火焰伤害 ×3（升级 ×4）；每段额外加等量于场上最高燃烧的伤害；给全体 5 层燃烧 |
| 远古残章 | `AncientFragment` | 2 | Power | Ancient | 敌人每有 1 种负面效果，你对其伤害 +25%。升级＝加「固有」 |

**卡牌已全部实现（34/34）。**

初始卡组 = 4× 打击 + 4× 防御 + 1× 火球术 + 1× 镇静。

**卡池现状**：17 张。`CustomCardPoolModel` 的 `GenerateAllCards()` 被 BaseLib 覆盖，
卡牌靠 `[Pool(typeof(WizardCardPool))]` 特性自动注册进池子。所以**卡池会随你新增卡牌自动增长**，
不需要手动维护列表。

> **商店需要卡池里至少有 1 张能力牌**：`MerchantInventory._coloredCardTypes`
> 固定为 `[Attack, Attack, Skill, Skill, Power]`，池子里没有 Power 会抛
> `Can't generate valid rarity for merchant card type Power` → **进商店黑屏**。

**新增一张卡的完整步骤**：

1. 在 `src/Scripts/Cards/` 建 `类名.cs`，继承 `WizardCardModel`（法术牌则继承 `WizardSpellCard`），加 `[Pool(typeof(WizardCardPool))]`
2. 放卡图 `src/Wizard/images/cards/{类名}.png`（路径由基类自动推导，**文件名必须等于类名**）
3. 在 `localization/{zhs,eng}/cards.json` 加两个键：
   `WIZARD-{类名UPPER_SNAKE}.title` 和 `.description`

**ID 生成规则**（务必对照，键写错就不显示）：

| 类名 | 生成的键前缀 |
|---|---|
| `StrikeWizard` | `WIZARD-STRIKE_WIZARD` |
| `DefendWizard` | `WIZARD-DEFEND_WIZARD` |
| `IndexOfForbiddenBooks` | `WIZARD-INDEX_OF_FORBIDDEN_BOOKS` |

**法术伤害的写法**（`WizardSpellCard` 子类）：

```csharp
public override SpellSchool School => SpellSchool.Fire;

protected override IEnumerable<DynamicVar> CanonicalVars =>
[
    new DamageVar(6m, ValueProp.Move | SpellProps.For(School)),
];

// 出伤时也要带上同样的 ValueProp，否则钩子认不出它是法术
await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
    .WithValueProp(ValueProp.Move | SpellProps.For(School))
    .FromCard(this, cardPlay)
    .Targeting(cardPlay.Target)
    .WithHitCount(3)              // 多段用 WithHitCount，不要循环调用 3 次
    .WithHitFx("vfx/vfx_molten_fist")
    .Execute(choiceContext);
```

**燃烧一律走 `SpellCmd.ApplyBurn`**，不要直接 `PowerCmd.Apply<BurnPower>` ——
燃烧有"不可叠加、取高值"的特殊规则，且需要无条件触发"给予燃烧时"的效果
（如「纵火狂」），这些逻辑都封在 `ApplyBurn` 里。

**「按目标动态演算」的卡面（参考原版「蜃景」`Mirage` / 「撕裂」`Rend`）**：

要让卡面在"按住牌指向敌人"时实时算出数值，用 `CalculatedVar` 系列：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars =>
[
    new CalculationBaseVar(0m),      // 基础值
    new CalculationExtraVar(1m),     // 每单位倍率的价值
    new CalculatedEnergyVar()        // 卡面写 {CalculatedEnergy:energyIcons()}
        .WithMultiplier((CardModel _, Creature? target) => 按目标算出的倍率),
];
```

- 值 = `CalculationBase + CalculationExtra × 倍率`
- **未指定目标时倍率函数应返回 0**（`target?.… ?? 0`），卡面即显示基础值
- 引擎在悬停/拖拽目标时会调 `DynamicVar.UpdateCardPreview(card, mode, target, …)`
  （`CardModel.cs:1479`），`CalculatedVar` 据此重算 `PreviewValue`
- 能量版实现在 `src/Scripts/Cards/Vars/CalculatedEnergyVar.cs`（引擎没有现成的）

> ⚠️ **能量必须用 `{CalculatedEnergy:energyIcons()}`，不能写 `{Energy:energyIcons()}`**。
> `{Energy}` 指向固定的 `EnergyVar`，与目标无关，永远显示同一个数。
> 另外 `:energyIcons()` 这个格式指令不能省，否则渲染成裸数字而不是能量图标。

> 📌 **本地化里 `{Xxx}` 的格式指令别漏**。漏掉指令会把原始值直接打出来 ——
> 电流外溢曾显示成 `受到[SplashRatio]倍率的伤害`，第一版又改成 `-50`。
>
> **百分比有两个方向，别选错**（`reference/decompiled/.../Formatters/`）：
>
> | 指令 | 公式 | 配对的值 | 适用 |
> |---|---|---|---|
> | `percentMore()` | `(v-1)×100` | `1.5` | 受伤 / 伤害**增加** |
> | `percentLess()` | `(1-v)×100` | `0.5` | 受伤 / 伤害**减少** |
>
> 电流外溢的 `SplashRatio = 0.5` 表示"造成 50% 的伤害"，
> 所以要用 `percentLess()` → `50`；用 `percentMore()` 会算出 `-50`。
>
> **`%` 号要自己写在文本里**：原版是 `[blue]{DamageDecrease:percentLess()}%[/blue]`，
> 单写 `{...:percentLess()}` 只会得到 `50` 而不是 `50%`。
>
> 注意 `{Amount}` 是例外：它由能力基类注入，本来就不带格式指令。

### 卡面「只在战斗中显示」的括号动态值

需求：静态描述常驻，**括号内的动态演算只在战斗中出现在卡面上**
（卡组、选牌、商店等局外场合不显示）。

引擎**已经注入了现成的条件变量**（`CardModel.cs:1379-1387`），
用它们把括号包起来即可，不需要自己写判断：

| 变量 | 含义 |
|---|---|
| `InCombat` | 是否在战斗中（`CombatManager.IsInProgress` 且是战斗牌堆） |
| `IsTargeting` | 是否已指定目标 |
| `OnTable` | 是否在 `Hand` / `Play` 牌堆 |
| `TargetType` | 目标类型名 |

写法（**照抄原版 `BARRAGE` / `BODY_SLAM` / `FINISHER` 等 15+ 张卡**）：

```
"WIZARD-MANA_DRAIN.description": "目标敌人每具有1种[gold]负面效果[/gold]，获得1{energyPrefix:energyIcons(1)}。{InCombat:\n（获得{CalculatedEnergy:energyIcons()}）|}"
```

- `{InCombat:真值文本|假值文本}` 是 SmartFormat 的条件语法（假值留空即不显示）
- 静态段用 `{energyPrefix:energyIcons(1)}`（固定 1 个图标），
  而**不是** `{CalculatedEnergy:...}` —— 后者才是随目标变化的动态量
- `energyPrefix` 由引擎注入（`CardModel.cs:1388`），会取到当前角色的能量图标

参考实现：`reference/decompiled/Watcher/Watcher.Code.Cards.{Basic,Common,Uncommon,Rare}/`（共 92 张）
写效果时先在原版找相似的卡：`reference/decompiled/game/MegaCrit.Sts2.Core.Models.Cards/`（596 张）。

## 3.4 遗物（当前 1 个占位）

| 遗物 | 类名 | 稀有度 | 效果 | 状态 |
|---|---|---|---|---|
| 奥术核心 | `WizardStarterRelic` | Starter | 战斗开始获得 8 格挡 | **占位，需重做** |

缺：普通 / 罕见 / 稀有 / 商店 / 事件 / 先古 各稀有度的遗物，一个都没有。

初始遗物还支持「先古升级」机制（原版「欧洛巴斯之触」用）：
重写 `GetUpgradeReplacement()` 返回升级后的遗物即可。

参考：`reference/decompiled/Watcher/Watcher.Code.Relics/`（8 个，含初始遗物）
教程：`reference/docs/tutorials-src/BaseLib/03 - 添加新遗物/`

## 3.5 药水（当前 1 瓶占位）

| 药水 | 类名 | 稀有度 | 使用时机 | 效果 | 状态 |
|---|---|---|---|---|---|
| 巫师之血 | `WizardBloodPotion` | Common | 随时 | 恢复最大生命 20% | **占位，需重做** |

参考：`reference/decompiled/Watcher/Watcher.Code.Potions/`（4 瓶）
教程：`reference/docs/tutorials-src/BaseLib/06 - 添加新药水/`

## 3.6 先古之民对话（已完成文本，待审阅）

见 §2.3。结构上已完成 9 位 × 3 套 × 中英双语 = 178 条。

## 3.7 未开始的内容

| 内容 | 说明 |
|---|---|
| ~~剩余 15 张卡~~ | **已完成**：32/32 全部实现并部署（卡图也已补齐） |
| ~~初始遗物系统~~ | **已完成**：魔法卷轴 `SpellScroll` / 死海古卷 `DeadSeaScrolls` |
| ~~衍生无色牌~~ | **已完成**：思索 `Ponder` / 顿悟 `Epiphany`（`TokenCardPool` + `CardRarity.Token`） |
| ~~先古替换项~~ | **已完成**（走 BaseLib 扩展点，无需 Harmony）：古老牙齿 火球术→落日；欧洛巴斯之触 魔法卷轴→死海古卷；尘封魔典→远古残章。见 `CONTEXT.md` §6.17 |
| 剩余遗物 6 个 | 目标 8 个，现有 2 个 |
| 遗物稀有度分级 | 待定 |
| 剩余 3 瓶药水 | 目标 4 瓶，现有 1 瓶占位 |
| 先古对话的 `firstVisitEver` / `ANY` | 现有 9 位 × 3 套 |
| 清理诊断日志 | `[Wizard/Burn]` `[Wizard/Asceticism]` `[Wizard/Freeze]` `[Wizard/Particles]` |

**其余可选项（角色 mod 通常不需要）**：

| 内容 | 教程章节 |
|---|---|
| 自定义卡牌属性（例如「奥术」关键词） | `BaseLib/04 - 添加卡牌属性/` |
| 自定义关键词（自定义枚举 + tooltip） | Wiki `utilities/enums` |
| 事件 / 遭遇 / 怪物 | `BaseLib/12 - 添加新事件/`、`11 - 添加新怪物/` |
| 附魔 | `BaseLib/13 - 添加新附魔/` |
| 时间线 / 纪元解锁 | `RitsuLib/09 - 添加时间线/`（当前角色设为「不需要时间线」） |
| 创意工坊上传 | `Basics/11 - 上传工坊/` |

---

## 3.8 已澄清的两条规则（原先我理解错了）

### 3.8.1 「升级加词条」而非「升级加数值」

有两张牌的升级是**改词条**，不是改数字：

| 卡牌 | 升级效果 |
|---|---|
| `BurningSoul` 燃烧之魂 | **获得「保留」** |
| `SterilizeByFire` 高温消毒 | **移除「消耗」** |

实现方式：关键字必须**可变** —— 用 `_upgraded` 标记 +
`CanonicalKeywords` 返回不同集合，**不要**用 `CardKeyword.Retain` 硬写。

### 3.8.2 燃烧之魂的精确语义（我原先理解错了）

**正确：** 你的**每一段**攻击结算后，**立刻插入**一段**等量**的火焰伤害，并给予 1 层燃烧。

- 「每一段」：火球术（3 伤 ×2 段）→ 结算为 3伤 → 3火焰 → 3伤 → 3火焰，共 4 段 ✅
- 「等量」取该段的 `DamageResult.TotalDamage`（含被格挡部分的理论伤害），
  所以**打格挡怪也不会缩水**
- **不可递归**：燃烧之魂不响应自己造成的伤害。两道保险 ——
  ① 插入的伤害走 `ValueProp.Unpowered`（不带 `Move`），`IsPoweredAttack()` 天然为 false；
  ② `_resolving` 标志兜底。**两层都要留着**，因为第 ① 条依赖 ValueProps 的传播正确
- 插入的伤害不带学派、dealer/cardSource 为 null → **不吃法术增益 / 寒冷 / 超导**

> 原先我误以为「等量」指"卡面基础伤害"，并给了一张"升级 +1"的错误设计。
> 现已按上述语义实现，`BurningSoulPower` 也**不再承载数值**（`StackType.Single`）。

### 3.8.3 决心已改名为 `Determination`

改名历程：`Resolve` → `Resolution` → **`Determination`**（最终定名）。
同步改动：C# 类名 / 文件名、卡图文件名、本地化键（`WIZARD-RESOLVE` →
`WIZARD-RESOLUTION` → `WIZARD-DETERMINATION`）、`design.md` 等。

> ⚠️ **注意别误改 `SpellProps.Resolve`** —— 那是 `SpellProps` 上的一个真实方法
> （判定伤害的法术流派），与这张牌无关。

---

## 3.9 建议的推进顺序

1. ~~阶段 2：出牌历史追踪 4 张~~ ✅
2. ~~阶段 3：动态层数 / 能力钩子 5 张~~ ✅
3. ~~阶段 4：复杂效果 5 张~~ ✅
4. ~~阶段 5：衍生牌 + 初始遗物系统~~ ✅
5. ~~先古替换~~ ✅（BaseLib 扩展点，见 `CONTEXT.md` §6.17）
6. **同步做**：遗物 8 个（现有 2）、药水 4 瓶（现有 1）
7. 补 `firstVisitEver` / `ANY` 兜底对话
8. 清理诊断日志（`[Wizard/Burn]` 等）
9. **最后统一替换美术** —— 现剩 10 张占位图：
   `AlternatingCurrent` `Asceticism` `Concentrate` `HealingSpell` `LightningFall`
   `OmniscientForm` `PyroJudgment` `Pyromaniac` `Determination` `TruthParty`
   （判断方法：文件 < 100 KB 的是占位图，真美术都是 600–1000 KB）
9. 上传工坊

---

# 附：常用操作速查

```powershell
# 构建 + 打包 + 部署（一条龙）
& D:\Projects\STS2\Wizard\build.ps1

# 只编译不打包
& D:\Projects\STS2\Wizard\build.ps1 -SkipPck

# 重新生成占位图
python D:\Projects\STS2\Wizard\reference\tools\make_placeholders.py

# 游戏内控制台（按 ~ 打开）
card WIZARD-STRIKE_WIZARD       # 战斗中获得一张打击
```

**改完代码/资源后一定要跑 `build.ps1`**，否则游戏加载的还是旧文件。

**游戏 mods 目录**（部署目标）：
`D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Wizard\`
里面有 `Wizard.dll` + `Wizard.json` + `Wizard.pck` 三个文件，缺一不可。
同级的 `mods\BaseLib\` 是依赖，也不能删。


