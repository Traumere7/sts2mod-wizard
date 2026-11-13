# 2026-11-13 public-beta 更新 —— 兼容性评估报告

> 官方 3 个月来首次、也是有史以来最大规模 public-beta 更新。
> 本报告记录**评估方法、发现的问题、已做的修复**，以及**仍需人工验证的部分**。
> 反编译产物已在比对**之后**刷新到 `reference/decompiled/{game,BaseLib}`，
> 旧版本保留为 `game-2026-09-19` / `BaseLib-2026-09-19`。

---

## 1. 环境快照

| 项 | 值 |
|---|---|
| Steam buildid | `26625880`（beta 分支 `public-beta`，`LastUpdated` = 2026-11-13） |
| 游戏程序集 | `data_sts2_windows_x86_64\sts2.dll`，10,646,528 B，2026-11-13 09:26 |
| Godot 引擎 | **4.5.1（未变）** |
| 游戏资源包 | `SlayTheSpire2.pck` 2.15 GB，**pack format 3**，17,357 个资源 |
| 我们的 mod pck | pack format **2**（`build.ps1` 用 `--pck-version=2`）—— 引擎未升级，**仍能正常加载**（启动自检已证） |
| BaseLib | 创意工坊 `3737335127`，**v3.4.8**（dll 2026-11-12 21:19） |
| 本地构建 | `Wizard.json` 版本 → **0.5.4**（本地 beta 兼容构建，**未发布**） |

## 2. 反编译刷新

- 工具：`ilspycmd 11.2.0`（`dotnet tool install -g ilspycmd`），项目模式 `-p`。
- 新版 `sts2.dll` → 3,888 个 `.cs`；BaseLib 3.4.8 → 514 个。
- **先比对、后替换**：旧树备份为 `game-2026-09-19`（3,538 个）/ `BaseLib-2026-09-19`（521 个）。
- 差异清单落盘：`reference/decompiled/_diff_lists*.txt`、`_hook_bodies_{old,new}.txt`、`_our_overrides.txt`。
- ⚠️ 坑：两次反编译的 **ILSpy 版本不同**，`global::` 前缀与换行风格会制造大量"假变化"——
  归一化（去 `global::`、折叠空白）后，2614 个"变化"里有 104 个其实是纯格式差异。

## 3. 变更规模

| 指标 | 数值 |
|---|---|
| 旧 / 新 类型（.cs） | 3,537 → **3,887**（新增 **360**，删除 **10**） |
| 语义变化文件（归一化后） | **2,510** / 未变 1,017 |
| 新增内容（Top） | 卡牌 **+121**、能力 **+41**、商人 **+22**、遗物 **+17**、`Dread/Dominions` **+12**、VFX **+11** |

新玩法大类：**Fool（弓箭手，`ValueProp.FromArrow` / `AttackCommand.FromArrow()`）**、
`Dread/Dominions`、新商人体系、大量新卡/新能力/新事件。

## 4. 编译级破坏：3 处（已修）

用新版 `sts2.dll` + BaseLib 3.4.8 直接编译本 mod，只报 3 个错：

| # | 位置 | 报错 | 根因 | 修法 |
|---|---|---|---|---|
| 1 | `Cards/Vars/IceArmorVar.cs` | CS0115 无适合的方法可重写 | 新版 `DynamicVar` **删除了** `GetBaseValueForIConvertible()`，`IConvertible` 的取值改为 `ShownValue => (int)PreviewValue`（`GetSourceValue` 也返回它） | 删除该重写；我们本来就已把最终值写进 `PreviewValue`，所以显示不受影响 |
| 2 | `Cards/Vars/HealingSpellHealVar.cs` | 同上 | 同上 | 同上 |
| 3 | `Events/RulesHorror.cs` | CS1501 `PullNextRelicFromFront` 没有 1 参数重载 | 新版 `RelicFactory` **删掉了 `(Player)` 重载**，必须显式传 `Rng` | 改为 `PullNextRelicFromFront(player, Rng!)`（原版事件都传 `EventModel.Rng`） |

修完后**零编译错误**（`dll 311B96E1`）。

## 5. 资源路径：1 处失效（已修）

- 逐个校验本 mod 引用的 **39 个**游戏资源路径（自制 `.tscn` 里的 26 个 + C# 里 13 个 `vfx/...`）：
  - `vfx/vfx_rock_shards` **已从新版移除** ⇒ 改用新版的 `vfx/vfx_rock_shatter`
    （用在 `WizardCharacter.GetArchitectAttackVfx()` 的观赏性特效列表里，不影响机制）。
  - 其余 38 个全部存在。
- ⚠️ 校验时注意：导出包里 `.png` 以 `.godot/imported/*.ctex` + `.png.import` 形式存在，
  直接查 `.png` 会得到**假阴性**（本次 10 个"缺失"全是这个原因）。
- 🔶 **无法用静态方式验证**：`event:/sfx/...` 是 FMOD 事件路径，不在 pck 文件表里。
  本次未改动任何 SFX 路径，但若新版删了某些事件，只会在实机播放时才暴露。

## 6. 运行时挂钩与语义：**未发现破坏**

| 检查项 | 结论 |
|---|---|
| 我们重写的 39 个方法（`reference/decompiled/_our_overrides.txt`） | 编译通过 ⇒ 基类/接口签名全部仍在 |
| `Hook.cs` 中与本 mod 重写同名的 **18 个静态方法体**（`AfterCardGeneratedForCombat`、`ModifyHandDraw`、`AfterDamageGiven/Received`、`AfterSideTurnStart/End`、`BeforeHandDraw`、`AfterPowerAmountChanged`…） | **逐字节一致**（归一化后） |
| `Hook.ModifyDamage`（伤害加算/乘算的聚合入口） | **一致** |
| `CardModel.HoverTips`（自动追加 Block / 关键字解释的逻辑） | **一致** |
| `CardModel.Keywords`、`PowerCmd.Apply<T>`、`PowerModel.SetAmount` | **一致** |
| `CombatHistory.CardGenerated`（「结晶」判定"本回合生成过牌"的依据） | **一致** |
| `VigorPower` 的私有结构 `Data { commandToModify, amountWhenAttackStarted }` 与 `AfterAttack/BeforeAttack/ModifyDamageAdditive` | **全部仍在** ⇒ 我们的 `VigorTrackingPatch` 继续有效 |
| `AddGeneratedCardToCombat` | 仅 ILSpy 命名差异，语义一致 |
| `DynamicVar` | **有破坏性变化**（见 §4 #1/#2，已修） |

原版本交互能力（代码级 diff，去掉注释后）：

- `VigorPower` / `StrengthPower` / `VulnerablePower` / `WeakPower` / `PlatingPower` /
  `ArtifactPower` / `MachineLearningPower`：差异**只有 `base.X` → `X` 这种写法简写**，语义不变。
- 数值不变：易伤 `DamageIncrease = 1.5`、虚弱 `DamageDecrease = 0.75`、镀甲 `Decrement = 1`。
- `ThornsPower`：**去掉了 `|| cardSource is Omnislice` 分支**（新机制专用），与本 mod 无关
  （我们不用 Omnislice，荆棘的既有行为已在 CONTEXT §26 定性）。

## 7. 枚举面：1 个新位（**必须**注意的那种）

| 枚举 | 变化 | 影响 |
|---|---|---|
| **`ValueProp`** | 新增 **`FromArrow = 0x20`** | ⭐ 我们的 `SpellProps` 三个 `[CustomEnum]` 位由 BaseLib 在运行时按"现有最大位"分配：0x20 已被占用 ⇒ 自动上移为 **Fire=0x40 / Lightning=0x80 / Ice=0x100**。<br>**启动日志实证**：`Generated KeyGenerator for enum ... ValueProp with starting value 64 | IsFlag: True` ✅<br>我们从未硬编码位值，所以无需改动。 |
| `VfxColor` | 新增 `Fool`（末尾追加） | 无影响（我们只用 Red/White/Gold/Cyan 等既有值） |
| `CardKeyword` / `CardRarity` / `CardType` / `TargetType` / `PowerStackType` / `PowerInstanceType` / `PowerType` | **未变** | 无影响 |

## 8. 模组清单 / BaseLib

- `ModManifest` record 字段**完全未变**（`id/name/author/description/version/has_pck/has_dll/dependencies/affects_gameplay/min_game_version`），
  我们的 `Wizard.json` 继续合规；`dependencies` 已是新式对象数组（带 `min_version`），无需迁移。
- BaseLib 3.4.8：`CustomPowerModel` / `CustomEventModel` / `SimpleModConfig` / `PoolAttribute` /
  `CustomEnumAttribute` **签名未变**；`CustomCardModel.GainsBlock` 只是 lambda 写法差异（语义同：有 `BlockVar`/`CalculatedBlockVar` 即为真）。
- 启动日志：`Applied 286 patches successfully, 0 failed`；我们 `12/12` 补丁全部应用成功。

## 9. 运行时自检（本次已做）

启动一次新版游戏（未进战斗），日志结论：

- `[Wizard] 初始化完成`；`补丁汇总：成功 12 个，失败 0 个` ✅
- `BaseLib: Starting custom enum generation for 1547 modded types` ✅
- 四个语言的本地化表全部 `Merging with base loc table` ✅
- **无任何异常/ERROR**（除一条与本 mod 无关的 `NCard` 对象池警告）✅

## 10. 仍需人工在实机验证

1. **实跑一局**：开局、卡池、遗物、事件、先古对话是否都正常；
2. **以太牌**：「以太箭」「魔力爆发」的**白+金星光冲击**（自制场景）在新版是否正常显示；
3. **音效**：FMOD 事件（`attack_fire`、`regent_guiding_star`、`defect_lightning_evoke` 等）是否仍存在；
4. **旧存档**：新版带存档迁移（`Registered 24 migrations`），旧巫师存档能否继续；
5. **新内容交互**：新版新增的 `FromArrow`、`Dread`、新商人体系与本 mod 是否有意料之外的叠加；
6. **重名内容**：新版新增了一张原版状态牌 `Amnesia`（我们也有「失忆」状态牌，
   id 前缀不同 `WIZARD-` 不会冲突，但**英文名重复**，玩家可能混淆；是否改名待定）。

## 11. 结论

**兼容性总体良好**：破坏面集中在 **1 个 `DynamicVar` 虚方法删除** + **1 个 `RelicFactory` 重载删除** + **1 个特效资源移除**，
均已修复并部署（`dll 311B96E1 / pck 6E8C0AD1`，清单版本 0.5.4，**未上传工坊**）。
所有我们依赖的**运行时钩子语义**未变，`ValueProp` 新位由 BaseLib 动态规避，风险主要在"**实机观感/音效/存档**"这类静态分析看不到的地方。
