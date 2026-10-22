# 巫师 Mod — 上下文快照

> **用途**：新会话或上下文压缩后，读这一份即可接手，不必回翻对话历史。
>
> **配套文档**：
> - `reference/README.md` —— **知识库说明**：反编译产物/教程/Wiki/资源解包各自的来源与用途，
>   以及"写新内容时该去哪查"的流程
> - `HANDOVER.md` —— 美术与文案交接（要给美术/文案看的东西）
> - `README.md` —— 环境与构建；`design.md` —— 卡牌设计定稿
>
> 最后更新：第四阶段（**卡牌 34/34 全部实现**）。

---

## 0. 一句话现状

框架与角色机制（含最难的「冻结」）已全部完成并实测通过；**60 张卡全部实现并已构建部署**；
26 个能力、**9 个遗物**、**3 瓶药水**、**60 张卡牌**已实现。卡图与遗物/药水图标均已补齐真美术。
已发布 **v0.3.7** 到创意工坊（item 3807136195）。
剩余：先古对话补充键、以及若干待清理的诊断日志。
遗物实现要点见 `RELICS-REFERENCE.md`。

---

## 1. 工程位置

| 项 | 值 |
|---|---|
| 工作区 | `D:\Projects\STS2\Wizard` |
| 游戏 | `D:\SteamLibrary\steamapps\common\Slay the Spire 2`（v0.111.0） |
| BaseLib | 创意工坊 3.4.7 |
| 观者 mod（参考范本） | 创意工坊 1.5.10 |
| 反编译产物 | `reference/decompiled/{game,BaseLib,Watcher}` |
| 构建 | `.\build.ps1` |

Shell 是 **Windows PowerShell 5.1**（不是 pwsh 7）。

---

## 2. ⚠️ 构建链路（**先读这一节**）

### 2.1 曾经的"改了但游戏里没变"——两个根因，都极隐蔽

**根因 A：GDRE 是 GUI 子系统程序，PowerShell 重定向对它静默失败。**

```powershell
& gdre_tools.exe ... *> $log    # → 退出码(空)  生成=False  日志=0字节
cmd /c "... > $log"             # → 退出码=0    生成=True
```

后果：**`--pck-create` 从来没真正执行过**，`build\Wizard.pck` 一直是若干轮之前的旧文件；
脚本读它的哈希、又和同样陈旧的部署文件"校验通过" → **旧 pck 被当成新产物部署，脚本还报成功**。

**已修**：`Invoke-NativeCapture` 改用 `cmd /c` 重定向。

**根因 B：GDRE 不覆盖已存在的输出文件。**

**已修**：打包前先 `Remove-Item` 旧 pck，并在打包后校验**时间戳必须是本轮**。

### 2.2 部署目标 = 游戏目录 **`mods\Wizard`**（优先级高于工坊，且不受 Steam 影响）

**三处候选全部实测过，只有这一处适合日常迭代：**

| # | 目录 | 游戏会加载？ | 稳定性 | 用途 |
|---|---|---|---|---|
| ① | `...\common\Slay the Spire 2\mods\Wizard` | ✅ **会，且优先级高于工坊** | 普通本地目录，**Steam 不碰** | ✅ **本脚本部署到这里** |
| ② | `...\workshop\content\2868840\3807136195` | ✅ 会（无 ① 时） | ❌ Steam 管理的缓存，**写入会被覆盖回已发布版本** | 订阅者下载到的内容 |
| ③ | `ModUploader\WizardWorkspace\content` | ❌ 不会 | ✅ 稳定 | 官方上传器的"素材源"，正式发布时用 |

**① 优先于 ② 的实测依据**（`godot.log`，两份同时存在时）：
```
Found mod manifest file ...\common\Slay the Spire 2\mods\Wizard\Wizard.json
Looking for mods to load from Steam Workshop mod 3807136195 in ...\3807136195
...
Loading assembly DLL ...\common\Slay the Spire 2\mods\Wizard\Wizard.dll   ← 用了 mods 这份
```

**② 会被覆盖的实测证据**：
```
10:42  脚本写入 ② 成功，三道哈希校验全过   dll 919E1607 / pck 9FAF7D05
10:46  Steam 把它覆盖回"已发布版本"        dll 52C6DBBB / pck 082ACCE8
10:49  启动游戏 ⇒ 加载的是旧包 ⇒ 修复"没生效"
```

> ⚠️ **这三种形态共同点：脚本自报的哈希校验全部通过。**
> 哈希只证明"目标文件等于构建产物"，**从不证明游戏会加载它**。
> 判断真正生效，**唯一权威依据是 `godot.log` 的 `Loading assembly DLL` 那一行**。

**正式发布流程**（只在需要让订阅者/联机队友拿到新版本时走）：
```
build.ps1 → 复制到 ③ → 运行 ModUploader.exe → Steam 同步到 ②
```

### 2.2b 同名 mod 两份并存：**`mods` 那份胜出**（这是优点，不是坑）

这是"改了却没生效"的**第三个**根因，比前两个更隐蔽 ——
因为这个目录游戏**确实会读**，只是**优先级高于工坊**。

**实测证据**（`godot.log`，两份同时存在时）：
```
15: Found mod manifest file ...\common\Slay the Spire 2\mods\Wizard\Wizard.json
16: Looking for mods to load from Steam Workshop mod 3807136195 in ...\3807136195
17: Found mod manifest file ...\3807136195\Wizard.json
...
83: Loading assembly DLL ...\common\Slay the Spire 2\mods\Wizard\Wizard.dll   ← 用了这份
84: Loading Godot PCK    ...\common\Slay the Spire 2\mods\Wizard\Wizard.pck
94: Finished mod initialization for '巫师' (Wizard).      ← 只初始化一次
```

两份同名 mod（同 `id`）**不会被跳过或报错**，游戏静默挑「游戏目录」那份。

**这个优先级是我们要利用的特性**：正因为 `mods` 那份胜出，
把开发版放进 `mods\Wizard` 就能**盖住工坊上的已发布版本**，
改完立即生效、不必上传 —— 这就是 §2.2 选它作为部署目标的原因。

> ⚠️ 反过来说：**如果 `mods\Wizard` 里留的是一份陈旧副本，
> 它就会盖住工坊上的新版本**。这正是曾经踩过的坑 ——
> 当时部署目标被改到别处，`mods\Wizard` 变成了"永久陈旧的孤儿"，
> 于是"新美术/新代码不生效，而三道哈希校验全部通过"。
> **现在部署目标就是它，所以不会再有孤儿。**

> 📌 **通用教训（比具体路径更重要）**：
> **"部署成功"≠"生效"**。已踩过三种形态：
> ① 写到没人读的地方；
> ② 写对了地方但**优先级输给另一份**（同名抢占）；
> ③ 写对了地方但**被 Steam 覆盖回已发布版本**（工坊缓存，见 §2.2）。
>
> 判断部署是否真正生效，**唯一权威依据是 `godot.log` 的 `Loading assembly DLL` 那一行**——
> 它明明白白写出游戏最终加载的是哪个文件。改完部署链路后，
> **务必启动一次游戏并把这一行读出来核对**，不要只看脚本自报的哈希。
> （路径写错会静默造出一个空目录，而 MakeDir+Copy 一路绿灯）。

### 2.2c 用 IL 验证"修复真的编译进去了"

比读源码更硬的证据：**直接反编译部署出去的那个 dll**，
确认改动确实在 IL 里（源码对 ≠ 产物对，构建链此前已多次坑过）。

本机可用 `Mono.Cecil` 做一次性校验（临时工程，用完即删）：

```powershell
# 建临时工程
mkdir D:\Projects\STS2\_ilcheck; cd D:\Projects\STS2\_ilcheck
dotnet new console -o . --force
dotnet add package Mono.Cecil
# Program.cs 里用 AssemblyDefinition.ReadAssembly(dll) 打印目标方法的 IL
dotnet run -- <dll路径> <类型名> <方法名>
```

实例（交流电叠层修复的验收）：

```
--- Wizard.Scripts.Powers.AlternatingCurrentPower::get_StackType ---
  0000: ldc.i4.1  <<< 常量 1        ← PowerStackType.Counter = 1（Single = 2）
  0001: ret

--- .../<AfterDamageGiven>d__11::MoveNext ---
  call PowerModel::get_Amount()          ★
  ldc.i4.1
  call System.Math::Max(Int32,Int32)     ★
  call PowerCmd::Apply<CurrentOverflowPower>(...)  ★
```

> `PowerStackType`：`None=0, Counter=1, Single=2`。
> 所以 `ldc.i4.1` 就是 `Counter` —— 一眼可判。

### 2.3 部署校验（三道，缺一不可）

```
① Wizard.dll  ：.godot\mono\temp\bin\Debug\Wizard.dll  vs  mods\Wizard\Wizard.dll
② Wizard.pck  ：build\Wizard.pck                        vs  mods\Wizard\Wizard.pck
③ pck 时间戳  ：必须晚于本次打包开始时间
```

> ⚠️ 曾把 dll 源路径写成 `bin\Debug\Wizard.dll`（那里**没有文件**），
> 于是 `Test-Path` 为假、**整段校验被静默跳过**。实际产物在
> `.godot\mono\temp\bin\Debug\Wizard.dll`。
>
> ⚠️ **这三道只证明"写对了目标"**，不证明"游戏真的加载了它"。
> 最终确认要么读 `godot.log` 的 `Loading assembly DLL` 那一行，
> 要么用 §2.2c 的 IL 校验直接看产物。

### 2.4 硬性纪律

- **构建前必须关闭游戏**：`Wizard.dll` 与 `Wizard.pck` 会被游戏进程锁住，
  复制失败 → 出现"pck 是新的、dll 是旧的"这种**最难排查**的半更新状态。
- **构建后必须独立复核**（不要只信脚本自报）：

  ```powershell
  $mods = 'D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Wizard'
  $b  = 'build\Wizard.pck'; $d = "$mods\Wizard.pck"
  $bd = 'src\.godot\mono\temp\bin\Debug\Wizard.dll'; $dd = "$mods\Wizard.dll"
  (Get-FileHash $b).Hash  -eq (Get-FileHash $d).Hash
  (Get-FileHash $bd).Hash -eq (Get-FileHash $dd).Hash
  ```

- **只有想让订阅者/联机队友拿到新版本时才需要发布**（§2.2）：
  复制到 `ModUploader\WizardWorkspace\content` → 跑 `ModUploader.exe`。
  日常自测**不需要**上传 —— 游戏读的是 `mods\Wizard`。

- **`build.ps1` 的 UTF-8 BOM：现在只是"兼容保险"，不再是硬性要求。**
  （2026-09-28 起终端已升级到 **PowerShell 7**，`$PSVersionTable.PSVersion` = `7.6.6`。
  PS7 默认就按 UTF-8 读脚本，BOM 有无都能正确解析中文。）

  > 📌 **历史**：此前用 PowerShell **5.1**，它按 ANSI 读无 BOM 的脚本，
  > 于是中文全部乱码、脚本直接解析失败（`edit` 工具每次改这个文件都会吃掉 BOM，
  > 累计踩坑 5 次）。那个时代必须先补回 BOM 才能跑。
  >
  > 现在 `.\build.ps1` 直接跑即可，**不必**再套 `powershell -File`。
  > BOM 保留着也无害（PS7 会跳过它），所以没特意删。

  只有在**明确要退回 `powershell.exe`(5.1)** 时才需要补 BOM：

  ```powershell
  $p = Resolve-Path build.ps1
  $t = [IO.File]::ReadAllText($p, [Text.UTF8Encoding]::new($false))
  [IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($true)))
  ```

- **不要单独删 `src\.godot\imported`**：PCK 里装的就是那些 `*.ctex` 导入产物。
  删了 GDRE 会打包失败；要清连 `build\` 一起清，然后重跑一次 `--import`。

### 2.5 清缓存要重跑两步

```powershell
Remove-Item build -Recurse -Force
& $GodotExe --headless --path src --import     # 重建 .godot/imported
.\build.ps1
```

---

## 3. 内容进度

### 3.1 总览

| 类别 | 已完成 | 目标 |
|---|---|---|
| 卡牌 | **60**（全部） | 60 |
| 遗物 | **9**（魔法卷轴 / 死海古卷 / 恒温炉 / 铅咒板 / 火漆密函 / 温暖蜡烛 / 巫术百科 / 鎏金天平 / 三原素） | 9 |
| 药水 | 1 个占位 | 4 |
| 能力 (Power) | 22 | — |
| 先古对话 | 9 位 × 3 套 | — |
| 角色机制 | 法术伤害 / 燃烧 / 麻痹 / 电流外溢 / 冻结 / 寒冷 / 法术增益 | 已定稿 |

### 3.2 已实现卡牌（54 = 全部）

基础 4：`StrikeWizard` `DefendWizard` `Fireball` `CalmMind`
火焰 12：`FlameSpark` `BlazingFlame` `SterilizeByFire` `BurningSoul` `WatchFire` `Determination`
　　　　`PyroJudgment` `Pyromaniac` `Flashburst` `BurnToAshes` `BlazingPunch` `DoubleRicochet`
雷电 8：`LightningBolt` `Circulation` `AlternatingCurrent` `LightningFall` `StaticCharge`
　　　　`Screen` `ElectromagneticWave` `Puncture`
寒冰 6：`IceWall` `Superconduct` `IceShard` `IceArmor` `ClearWorld` `ColdWave`
奥术 19：`ManaDrain` `Recollection` `Asceticism` `MagicalIndex` `Concentrate`
　　　　`HealingSpell` `DarkMysticism` `Divination` `TruthParty` `OmniscientForm` `Intimidate`
　　　　`Bookmark` `Endnote` `ForgetMeNot` `TidyUp` `QuickChant` `ManaCondense` `MindFlow`
　　　　`PurifySin`
状态 1：`Amnesia`（`StatusCardPool` + `CardRarity.Status`，**不进巫师卡池**）
先古 2：`Sunset`（火焰）`AncientFragment`（奥术）
衍生 2：`Ponder` `Epiphany`（`TokenCardPool` + `CardRarity.Token`）

> 分类口径：**先古 2 张、状态 1 张单列**（不重复计入火焰/奥术），
> 因此 4+12+8+6+19+1+2+2 = **54**。与 `design.md` 的统计表一致。

> 📌 **2026-10-04 新增 4 张**：`TidyUp`（整理）`DoubleRicochet`（弹射）
> `QuickChant`（快速咏唱）`ManaCondense`（魔力凝聚），
> 另有新能力 `TemporarySpellPowerPower`。
> **2026-10-08 新增 1 张**：`MindFlow`（心流，X 费）。
> 这 5 张的卡图后来都已换成真美术。

**全部 60 张都有卡图**（`images/cards/{类名}.png`，1000×760）；

> 📌 **换卡图后必须让 Godot 重新导入**（2026-10-01 踩到）：
> 直接覆盖同名 png 后，`src\.godot\imported\<名>.png-<hash>.ctex` 可能**仍是旧图** ——
> 判据是 `ctex` 大小：占位图只有几 KB，真图是数百 KB。
> 若构建后 pck 没变大、`ctex` 还是小的，就**删掉这对 `.ctex`/`.md5`** 再重跑 `build.ps1`：
>
> ```powershell
> Get-ChildItem "src\.godot\imported" |
>   Where-Object { $_.Name -match "^<卡名>\." } | Remove-Item -Force
> ```
>
> 参照值：`Screen.png`(1.47MB 源) → 1121552 B；`ColdWave.png`(0.94MB) → 782476 B。

> ⚠️ **禁书目录已改名**：`IndexOfForbiddenBooks` → **`MagicalIndex`**（英文名 "Magical Index"）。
> 改名连带三处：类名、卡图文件名（`MagicalIndex.png`）、本地化键（`WIZARD-MAGICAL_INDEX.*`）。
> 漏改任何一处都会表现为"卡图丢失"或"标题显示成键名"。

### 3.3 未实现内容

**卡牌已全部实现（41/41）。** 剩余工作见 §3.1 表格的非卡牌行：
遗物还差 6 个、药水还差 3 个、先古对话的 `firstVisitEver` / `ANY` 键。

### 3.3b 本批（第三批 4 张新增 + 冰霰调整）的实现要点

| 卡 | 关键决策 |
|---|---|
| `Flashburst` | 「虚无」直接进 `CanonicalKeywords`（**不参与升级**，所以不像「高温消毒」那样用 `RemoveKeyword`）。**纯伤害牌，不挂 `BurnPower` 悬停**——它不给燃烧，挂了会误导 |
| `Intimidate` | 格挡给自己 + 麻痹给敌人 ⇒ `TargetType.AnyEnemy`。**不实现 `ISpellCard`**（不造成伤害，流派标记无意义），与「冰之城墙」「禁书目录」同做法 |
| `Screen` | 电流外溢挂在**被打的敌人**身上（对照「雷光」），所以对每个敌人各自 `PowerCmd.Apply`；`TargetType.Self`（只是选目标不需要点敌人） |
| `ElectromagneticWave` | 群体伤害用 `TargetingAllOpponents`（1 段，不涉及 `WithHitCount`）。**先伤害后抽牌**；**没有可行目标时整张牌不生效（抽牌也不给）** —— 照抄原版「扫荡射线」`SweepingGaze`，判据 `GetOpponentsOf(...).Any(IsAlive)` 与引擎 `AttackCommand` 同源 |
| `IceShard` | 段数 2(3) → **3(4)**，每段伤害与寒冷层数不变（`RepeatVar` 基数 2→3，升级仍 `UpgradeValueBy(1)`） |

**升级效果（第三批）**：

| 卡 | 升级 |
|---|---|
| `Flashburst` | 伤害 18 → 24 |
| `Intimidate` | 格挡 6 → 8（麻痹层数不变） |
| `Screen` | 格挡 5 → 7；电流外溢 1 → 2 |
| `ElectromagneticWave` | 伤害 7 → 8；抽牌 1 → 2 |

> **命名冲突检查**：4 个类名（`Flashburst` / `Intimidate` / `Screen` / `ElectromagneticWave`）
> 均已对 `reference/decompiled/game/` 全量检索，**无同名原版类型**。
> ⚠️ `Screen` 一词在原版出现 500+ 次，但**全是 `ScreenContext` / `NScreen*` 之类的前缀**，
> 没有独立的 `Screen` 类型 —— 我们的 `Wizard.Scripts.Cards.Screen` 不会产生歧义。

### 3.4 本批（第二批 9 张）的实现要点

| 卡 | 关键决策 |
|---|---|
| `PyroJudgment` | `AfterSideTurnStart(side==Player && participants.Contains(Owner))`；`SetAmount(Amount+1)` 不走 `ModifyAmount`（避免惊动层数变更钩子） |
| `Pyromaniac` | 订阅静态 `WizardEvents.BurnApplied`；`AfterApplied` 里 `-=` 再 `+=` 保证幂等；`AfterRemoved` **与** `AfterCombatEnd` 都要退订 |
| `AlternatingCurrent` | `AfterDamageGiven` + `SpellProps.Resolve(props, cardSource)==SpellSchool.Lightning` + `TotalDamage>0` + `_resolving` 防重入 |
| `LightningFall` | 段数**按敌人各自算**，所以逐个敌人单独 `DamageCmd`，**不能用** `TargetingAllOpponents`（它只接受一个共享 HitCount） |
| `TruthParty` | 先上燃烧再读最高层数（否则自己刚上的 2 层永远不算）；张数 = **最高燃烧的一半（向上取整）**；选牌用 `FromHand(prompt, 0, max)` 的 **min=0** 表达"最多"（**可以一张都不选**） |
| `ForgetMeNot` | 从**消耗牌堆**取牌（`PileType.Exhaust`）；选牌用 `FromCombatPile(prompt, **min=1**, max)` —— 与「真理派对」相反，**至少选 1 张** |
| `Divination` | 「否则」实现为**先判定有无技能牌**：有→强制选 1（min=max=1），无→直接弃光手牌，不给选择界面 |
| `OmniscientForm` | `AfterPowerAmountChanged` 里用 `HashSet<ModelId>` 记录本回合已给过的负面效果种类，`AfterSideTurnStart(side==Player)` 清空 |
| `Sunset` | **先伤害后上燃烧**（否则本次伤害会吃到自己刚施加的 5 层）；每段伤害 = `CalculationBase + ExtraDamage × 施放瞬间场上最高燃烧` |
| `AncientFragment` | `ModifyDamageMultiplicative` 返回 `1 + 0.25×种类数`（**加算**：1.25/1.5/1.75/2.0） |
| `LightningFall` | 段数 = `1 + 全体敌人负面效果种类的并集`（对每个敌人同一段数） |

**升级效果（第二批）**：

| 卡 | 升级 |
|---|---|
| `AlternatingCurrent` | 获得「固有」词条（`AddKeyword`）。能力叠层 = 每次给的电流外溢层数（见 §6.24） |
| `AncientFragment` | 获得「固有」词条（`AddKeyword`）。**费用恒为 2，不降费** |
| `Sunset` | 段数 3 → 4（`Repeat.UpgradeValueBy(1)`） |
| `PyroJudgment` | 燃烧 3 → 4（`PowerVar` 默认升级 +1） |
| `Pyromaniac` | 法术增益 1 → 2 |
| `LightningFall` | 伤害 8 → 11 |
| `Divination` | 抽牌 3 → 4 |
| `TruthParty` | 燃烧 2 → 3 |
| `OmniscientForm` | 抽牌 1 → 2 |

> ⚠️ **「升级加词条」的写法必须照抄原版**（「杂耍」`Juggling` / 「独白」`Monologue`）：
> 初始**不**在 `CanonicalKeywords` 里声明该关键字，`OnUpgrade()` 里
> `AddKeyword(CardKeyword.Xxx)`。
> **本地化文案里不要手写「（固有）」「（保留）」** —— 关键字由引擎渲染成卡面词条，
> 升级后标签自动出现，手写会重复。
>
> 反面教材：`AlternatingCurrent` / `AncientFragment` 初版把「固有」写进了
> `CanonicalKeywords`（当成"初始就有"），于是变成**未升级也有固有、升级反而无事发生**；
> `AncientFragment` 还错用了 `EnergyCost.UpgradeBy(-1)` 降费
> （那是普通卡「修行」`Asceticism` 的处理，先古卡不该降费）。

> 两处设计歧义（如需改动都是单行）：`AncientFragmentPower` 用复利 `1.25^N`
> （备选线性 `1 + 0.25×N`）；`OmniscientFormPower` 在负面效果**层数未变化**时
> 不触发（`PowerCmd` 只在偏移非 0 时发钩子，属已知限制）。

---

## 4. 已实现资产清单

### 4.1 能力（`src/Scripts/Powers/`，共 21 个）

机制类：`BurnPower` `ChillPower` `ParalysisPower` `SpellPowerPower` `CurrentOverflowPower`
　　　　`FreezePower` + `FreezeRegistry`（非 Power，是冻结查询表）

卡牌授予类（第一批）：`FrostWallPower` `SuperconductPower` `CirculationPower`
　　　　`BurningSoulPower` `ConcentratePower` `RecollectionPower` `AsceticismPower`

卡牌授予类（第二批 9 张卡带来的 5 个）：
`PyroJudgmentPower` `PyromaniacPower` `AlternatingCurrentPower`
`OmniscientFormPower` `AncientFragmentPower`

另有 `BurnImmunityPower`（「纵火狂」附带，与 PyromaniacPower 一同实装）、
`ColdWavePower`（「大寒波」，用 `BeforeCardPlayed`，见 §6.26）、
`AmnesiaPower`（「失忆」，用 `ModifyHandDraw` 削抽牌，见 §6.32）。

> 全部 21 个都有 64 + 128 图标（`images/powers/{snake_case}_power[_big].png`）。
> 注意 **Power 不需要 `[Pool]`**（只有卡/遗物/药水必需），
> 与已正常工作的一批保持了一致 —— 别"顺手补上"。

### 4.2 遗物（`src/Scripts/Relics/`）

- `ArcaneRelicModel` —— 基类，**修了 mod 遗物图标路径**（见 §6.6）
- `SpellScroll` 魔法卷轴 —— 初始遗物，战斗开始时把 1 张「思索」加入手牌
- `DeadSeaScrolls` 死海古卷 —— `Ancient`，把 1 张「顿悟」加入手牌

### 4.3 遗物资源结构（照抄观者 mod）

```
images/relics/{name}.png                                    85×85 大图标
images/relics/{name}_outline.png                            轮廓版
images/atlases/relic_atlas.png                              图集（85×85 网格）
images/atlases/relic_outline_atlas.png
images/atlases/relic_atlas.sprites/{name}.tres              AtlasTexture + Rect2
images/atlases/relic_atlas.sprites/{name}_outline.tres
```

### 4.4 本地化键数（中英各一份，数量一致）

| 文件 | 键数 |
|---|---|
| `localization/{zhs,eng}/cards.json` | 65 |
| `localization/{zhs,eng}/powers.json` | 54 |
| `localization/{zhs,eng}/relics.json` | 6 |

> ⚠️ 每新增一张卡/一个能力/一个遗物，**中英两边都要加**，键名规则见 §9。
> 漏加的表现是卡名/效果栏显示成原始键名（如 `cards.WIZARD-XXX.title`）。
>
> 快速自检（应输出「仅zhs=无 仅eng=无」）：
>
> ```powershell
> foreach ($f in @("cards","powers")) {
>   $z = Get-Content "src\Wizard\localization\zhs\$f.json" -Raw -Encoding UTF8 | ConvertFrom-Json
>   $e = Get-Content "src\Wizard\localization\eng\$f.json" -Raw -Encoding UTF8 | ConvertFrom-Json
>   $zk = $z.PSObject.Properties.Name; $ek = $e.PSObject.Properties.Name
>   "[$f] zhs=$($zk.Count) eng=$($ek.Count) 仅zhs=$(($zk|?{$ek -notcontains $_}) -join ',') 仅eng=$(($ek|?{$zk -notcontains $_}) -join ',')"
> }
> ```

---

## 5. 调试入口

- 日志：`%APPDATA%\SlayTheSpire2\logs\godot.log`
- 游戏内控制台（`~`）：`card <ID>` · `power <ID> <amount> <target>`（**0 = 玩家，1+ = 敌人**）
- 已植入诊断日志前缀（**定型后应清理**）：
  `[Wizard/Burn]` `[Wizard/Asceticism]` `[Wizard/Freeze]` `[Wizard/Particles]`

---

## 6. 引擎行为速查（**全是踩过的坑**）

### 6.1 升级改「词条」→ 用 AddKeyword / RemoveKeyword

```csharp
// 升级【添加】词条 —— 原版「独白」Monologue、原版「杂耍」Juggling
public override void OnUpgrade() => AddKeyword(CardKeyword.Retain);

// 升级【移除】词条 —— 原版「蜃景」Mirage
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
public override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
```

> **文案里一个字都不要写升级提示** —— 关键字由引擎渲染成卡面词条，升级后标签自动出现/消失。
>
> ❌ 我曾自造 `private bool _upgraded` + `CanonicalKeywords` 返回不同集合，
> 行为虽对但偏离惯例，还逼得往描述里手写「（升级移除消耗）」—— 那才是多余且易错。

**⚠️ 别把「升级才有的词条」写成"初始就有"** —— 这是本 mod 实际犯过的错：

```csharp
// ❌ 错：写进 CanonicalKeywords → 未升级就已经有固有，升级反而无事发生
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];
protected override void OnUpgrade() { }   // 空实现

// ✅ 对：初始什么都不声明，升级时才加
protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
```

自查：若某张非"初始套牌"的卡在 `CanonicalKeywords` 里列了 `Innate` / `Retain`，
先确认它**未升级时就该有**该词条；否则一律挪进 `OnUpgrade()`。
（`Sunset` 的 `Ethereal`、`OmniscientForm` 的 `Ethereal` 属"初始就有"，留在 `CanonicalKeywords` 是对的。）

### 6.2 升级改「非词条」→ 才用 `{IfUpgraded}`

```
"WIZARD-DARK_MYSTICISM.description": "将随机的{IfUpgraded:show:升级过的|}攻击、技能、能力牌各1张…"
```

### 6.3 `PowerCmd.ModifyAmount` 是**赋值**，不是累加

```csharp
power.SetAmount(power.Amount + (int)modifiedOffset);
```

同一批结算里若被**并发**调用多次，各自读到同一个旧 `Amount`，
后算出的 delta 会**覆盖**先算的 —— 这就是「燃烧锁层」的真凶。

**判据来自实测日志**（两怪战斗、同回合观火→火球术）：
```
施加前: 层数=1 请求=2
  取高值: 实例#1C4385A 现有=1 期望=2 delta=1   ← 算对了
  取高值: 实例#1C4385A 现有=1 期望=1 delta=0   ← 又算一次，读数仍旧
施加后: 层数=1（期望 2）                        ← delta=0 覆盖了 delta=1
```

**解法**：`BurnPower.ApplyAbsolute` —— 在 `SemaphoreSlim` 内完成「读现状 → 算差值 → 写入」，
后进入者读到新值、算出 0 差值后**直接返回不写入**。
`SpellCmd.ApplyBurn` 一律走这个入口。

### 6.4 `AfterCombatVictory` 对 **Power 不触发**

`CombatManager.EndCombatInternal` 的真实顺序：

```
:1315  Hook.AfterCombatEnd        ← 能力还在 ✓
:1324  player2.AfterCombatEnd()   ← 内部 RemoveAllPowersInternalExcept()，能力被清空
:1326  Hook.AfterCombatVictory    ← 能力已没了，为 Power 重写的钩子永不触发
```

引擎注释写"通常应该用 `AfterCombatVictory`"，但对**能力**是陷阱。
**要在战斗结束时做事的能力，用 `AfterCombatEnd`。**

### 6.5 弃牌堆弹回手牌 → `CardPileCmd.Add`（移动）

```csharp
await CardPileCmd.Add(card, PileType.Hand);   // 移动 —— 原版「好勇斗狠」AggressionPower
```

`CreateDupe()` 是**复刻**，用于"自动打出一张副本、原件留在弃牌堆"（原版「历史课」HistoryCourse）。
用错会变成"手牌和弃牌堆各一张"，洗牌后弃牌堆那张被洗回抽牌堆 → 看起来"重复的牌莫名消失"。

### 6.6 mod 遗物图标默认永远找不到

`RelicModel.IconBaseName => Id.Entry.ToLowerInvariant()`，而 mod 的 Entry 是
`{命名空间}-{类名}`（**带连字符**）→ 引擎去找 `wizard-spell_scroll.png`，文件名对不上。

**解法**（`ArcaneRelicModel` 基类，与观者 `WatcherRelicModel` 一致）：
去掉命名空间前缀，用**类名 snake_case** 作为文件名。

症状是启动日志刷 `Missing sprite 'wizard-wizard_starter_relic'`，遗物栏图标空白。

### 6.7 能力/卡牌图标路径约定

- 卡图：`res://Wizard/images/cards/{类名}.png`（`WizardCardModel` 自动推导）
- 能力图标**必须成对提供**：`CustomPackedIconPath`(64×64) + `CustomBigIconPath`(128×128)
  —— 是两个独立路径，只给一个另一处空白
- 能力图集：`res://Wizard/images/powers/{name}_power.png` / `_big.png`

### 6.8 每个模型类都必须带 `[Pool]`

`CustomCardModel` / `CustomRelicModel` / `CustomPotionModel` 的**每一个**子类都要
`[Pool(typeof(对应池子))]`，否则游戏在 `ModelDb.Init` 抛异常、**启动中止**（致命弹窗）。
初始遗物也必须带。池子类和角色类不需要。

### 6.9 衍生牌用 TokenCardPool

```csharp
[Pool(typeof(TokenCardPool))]
public class Ponder : ... : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
```

原版「燃料」`Fuel`、「仆从打击」`MinionStrike`、观者「奇迹」`Miracle` 都是这个组合。
放进自己的卡池会让它出现在奖励/商店/图鉴里。

### 6.10 卡面动态值

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars =>
[
    new CalculationBaseVar(0m),
    new CalculationExtraVar(1m),
    new CalculatedEnergyVar().WithMultiplier((CardModel _, Creature? target) => 按目标算),
];
```

- 值 = `CalculationBase + CalculationExtra × 倍率`
- **未指定目标时倍率函数应返回 0** → 卡面显示基础值
- 引擎在悬停/拖拽目标时调 `DynamicVar.UpdateCardPreview`（`CardModel.cs:1479`）
- 能量版 `CalculatedEnergyVar` 是自造的（引擎没有），在 `src/Scripts/Cards/Vars/`
- ⚠️ `{CalculatedEnergy:energyIcons()}` **≠** `{Energy:energyIcons()}`，后者与目标无关

#### 6.10.1 让卡面那个数字**实时算**（伤害）

需求形如「造成 5 点伤害，每段额外造成等量于场上最高燃烧的伤害」，
要让卡面的「5」变成算完的最终值。**照抄原版「欺凌」`Bully` / 「撕碎」`Rend`**：

```csharp
new DamageVar(5m, ValueProp.Move | SpellProps.For(School)),   // 卡面基础值 + 携带流派标记
new CalculationBaseVar(5m),                                    // ← 真正的基础伤害
new ExtraDamageVar(1m),                                        // ← 每 1 单位倍率加几点
new CalculatedDamageVar(ValueProp.Move | SpellProps.For(School))
    .WithMultiplier(FieldState.HighestBurn),                   // ← 倍率 = 场上最高燃烧
```

- 文案用 **`{CalculatedDamage:diff()}`**（不是 `{Damage:diff()}`），卡面即最终值
- 「额外造成 N 点」的 N 就是 `ExtraDamageVar`；`CalculationBase + ExtraDamage × 倍率`
  —— 即"**直接加进前一句的数值**"，不是独立第二段伤害
- ⚠️ **`DamageVar` 里那个数字从此是"死"的**，升级时若只改它、
  不改 `CalculationBase`，卡面与实际伤害就会脱节。两处都要改
- ⚠️ `ExtraDamageVar` 是 `CalculatedDamageVar.GetExtraVar()` 的**硬依赖**
  （缺了会 `KeyNotFoundException`），即使文案里不显示它也必须声明
- ⚠️ 不要自己写 `new CalculatedVar(...)`：`CalculatedDamageVar` 额外做了
  附魔加成与 `Hook.ModifyDamage` 的卡面预览，自己写会丢这些

#### 6.10.2 ⚠️ `Attack(CalculatedDamageVar)` 会丢法术流派标记

`DamageCmd` 有两个重载：

| 重载 | 行为 |
|---|---|
| `Attack(decimal damagePerHit)` | `DamageProps` 默认 `ValueProp.Move`，可用 `WithValueProp(...)` 覆盖 |
| `Attack(CalculatedDamageVar)` | 走 `_calculatedDamageVar`，**不设置 `DamageProps`** |

`AttackCommand`（`AttackCommand.cs`）里 `DamageProps` 与 `_calculatedDamageVar` 是**两个独立字段**，
且 `DamageProps` 没有公开 setter、只能靠 `WithValueProp`。

于是本 mod 的 `SpellProps.Fire/Lightning/Ice` 标记位**过不去**
（`ChillPower` / `SuperconductPower` / `SpellPowerPower` 全读执行期的 `props`）→ 法术加成静默失效。

**结论**：需要法术流派标记时，**用 `Attack(decimal)` 重载 + `WithValueProp`**，
把值自己算出来传进去：

```csharp
decimal damagePerHit = DynamicVars.CalculatedDamage.Calculate(null);
await DamageCmd.Attack(damagePerHit)
    .WithValueProp(ValueProp.Move | SpellProps.For(School))
    .FromCard(this, cardPlay)      // ← 会自动填 Attacker（AttackCommand.cs:229）
    ...
```

效果与重载版一致，但流派标记不丢。代价是绕开了重载里的"中间值取整"逻辑，
所以**伤害值必须保证是整数**（本 mod 的数值都是整数，安全）。

### 6.10.3 「场上的 X」= 含玩家自己

统一走 `src/Scripts/Spells/FieldState.cs`。

⚠️ **踩过的坑**：写"场上最高燃烧"时直觉会用
`CombatState.GetOpponentsOf(Owner.Creature)` —— 那只是**敌人**。
但「高温消毒」会给玩家自己上燃烧，所以玩家身上的层数也是"场上的"。
正确取法是 **`ICombatState.Creatures`**（`CombatState.cs`：`_allies.Concat(_enemies)`）。

倍率函数在**非战斗**场合（卡牌库预览）也会被调用，那时没有棋局，
必须返回 0 让卡面显示基础值 —— 所以 `FieldState.HighestBurn` 返回 `decimal`
（`WithMultiplier` 委托签名是 `Func<CardModel, Creature?, decimal>`，
返回 `int` 会 **CS0407**），另提供 `TryHighestBurn(out int)` 给需要整数层数的场合。

### 6.11 只在战斗中显示的卡面括号

引擎已注入条件变量（`CardModel.cs:1379-1387`）：

| 变量 | 含义 |
|---|---|
| `InCombat` | 战斗中 |
| `IsTargeting` | 已指定目标 |
| `OnTable` | 在 Hand/Play 牌堆 |
| `TargetType` | 目标类型 |

```
"…获得1{energyPrefix:energyIcons(1)}。{InCombat:\n（获得{CalculatedEnergy:energyIcons()}）|}"
```

原版 15+ 张卡都用这个（`BARRAGE` `BODY_SLAM` `FINISHER` `GOLD_AXE`…）。

### 6.12 百分比格式化

| 指令 | 公式 | 配对值 | 适用 |
|---|---|---|---|
| `percentMore()` | `(v-1)×100` | `1.5` | 受伤/伤害**增加** |
| `percentLess()` | `(1-v)×100` | `0.5` | 受伤/伤害**减少** |

- **`%` 号要自己写在文本里**：原版是 `[blue]{DamageDecrease:percentLess()}%[/blue]`
- 选错方向会显示成 `-50`
- 例：`SplashRatio = 0.5`（造成 50% 伤害）→ 用 `percentLess()`

### 6.13 燃烧特例

- **不可叠加、取层数最高值**、每回合不衰减
- 施加一律走 `SpellCmd.ApplyBurn`（内部走 `BurnPower.ApplyAbsolute`），
  **不要**直接 `PowerCmd.Apply<BurnPower>`
- 应用动作本身算数 → 无论层数是否变化都无条件触发 `WizardEvents.BurnApplied`
  （引擎的 `AfterPowerAmountChanged` 在 `offset == 0` 时不发，见 `PowerCmd.cs:247`）

### 6.14 冻结的实现要点

- 登记点：**敌人回合结束**（敌人出手后、回合真正结束前），读到的 `NextMove` 是刚执行的招
- 掉层点：同一分支、**紧跟登记之后**
- 拦截：Harmony Prefix on `MonsterModel.RollMove`，反射写 `NextMove`，`return false`（不推进状态机）
- **登记活得比能力久** → 绝不能用 `HasPower` 判断登记有效性（曾因此让 1 层完全失效）
- **必须每回合重新登记** → 加"已有登记就跳过"的优化会让 N 层退化成 1 层
- 眩晕 `STUNNED` 必须放行

### 6.15 Harmony 补丁应用

`Entry.ApplyPatches` **逐个类** `CreateClassProcessor(type).Patch()` + 各自 try/catch。
**不能直接 `PatchAll`** —— 一个补丁解析失败会抛异常、中断整个 `Initialize`
（表现为"改了但毫无变化"）。

- `Patch()` 返回**空列表**表示"类解析到了但一个方法都没打上"（静默无效），要当失败报出
- **Harmony 无法解析 Godot 的 virtual 生命周期方法**（如 `_Ready`）→ 补丁目标要挑
  **类型唯一、非虚、签名无歧义**的方法
- 启动日志里的 `[Wizard] 补丁已应用：…` 与"补丁汇总"必须看

### 6.16 能量表盘 `_particles` 为 null → 战斗开局不抽牌

`NParticlesContainer._particles` 是 `[Export]` 字段、C# 里没初始化式，
而 `Restart()`/`SetEmitting()` 无条件访问 `_particles.Count` → NRE。
该 NRE 出现在 `CombatManager.SetupPlayerTurn` 的 `ResetEnergy()` 里、**抽牌之前**，
掀掉整个 turn loop。

**已修**：`Patches/ParticlesContainerGuard.cs`（Prefix on `Restart`/`SetEmitting`，
反射补空数组）。

### 6.17 先古替换：**用 BaseLib 的扩展点，不要自己写 Harmony**

⚠️ **本节早期结论（"需要给两张静态表写 Harmony 补丁"）已被推翻。**
BaseLib 3.4.7 已经自己给那两张表打好补丁了，mod 侧只要实现三个扩展点：

| 需求 | 扩展点 | 写在哪 |
|---|---|---|
| 初始**牌**换先古牌（古老牙齿） | `ITranscendenceCard` | **被替换的那张初始牌**上 |
| 初始**遗物**换升级遗物（欧洛巴斯之触） | `CustomRelicModel.GetUpgradeReplacement()` | **初始遗物**类上 |
| 尘封魔典给哪张卡 | `ITomeCard` | **被给予的那张先古牌**上 |

```csharp
// ① 古老牙齿：火球术 → 落日
public class Fireball : WizardSpellCard, ITranscendenceCard
{
    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<Sunset>();
}

// ② 欧洛巴斯之触：魔法卷轴 → 死海古卷
public class SpellScroll : ArcaneRelicModel
{
    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<DeadSeaScrolls>();
}

// ③ 尘封魔典给「远古残章」
public class AncientFragment : WizardCardModel, ITomeCard
{
    public CharacterModel TomeCharacter => ModelDb.Character<WizardCharacter>();
}
```

**BaseLib 内部是怎么接的**（读 BaseLib 反编译产物确认）：

| BaseLib 补丁 | 手法 |
|---|---|
| `ArchaicToothTranscendenceUpgradesPatch` | 对原版 `ArchaicTooth.TranscendenceUpgrades` 静态属性加 **`[HarmonyPostfix]`**，扫描 `ModelDb.AllCards` 里所有 `ITranscendenceCard` 并进 `__result` |
| `StarterUpgradePatches` | 对 `TouchOfOrobas.GetUpgradedStarterRelic` 加 **`[HarmonyPrefix]`**：初始遗物若是 `CustomRelicModel`，取 `GetUpgradeReplacement()`（**返回 null 才回落**到原版硬编码表 → `Circlet`） |
| `DustyTomePatch` | 对 `DustyTome.SetupForPlayer` 加 **`[HarmonyPrefix]`**：扫所有 `ITomeCard` 按 `TomeCharacter` 归组，**该角色有一组就直接接管**（返回 false，原版逻辑整段跳过） |

> **「尘封魔典只给远古残章」正是靠 ③ + ① 一起成立的**：
> ③ 让巫师这组里只有「远古残章」，随机池退化成唯一解；
> ① 让「落日」进了 `TranscendenceCards`，从而被原版筛选条件
> `!ArchaicTooth.TranscendenceCards.Contains(c)` 排除。
> 两条路径互不干扰，**不需要单独去"把落日移出魔典池"**。

> 📌 **缓存时机不成问题**：`TranscendenceUpgrades` 那段扫描有 `if (_customTranscendence == null)` 的一次性缓存，
> 但它只在**游戏内**（先古选项 / 魔典生成）首次访问，那时 `ModelDb` 早已初始化完毕。
> 若日后发现扫不到新卡，才需要考虑这个缓存。

**原版那两张表的原始结构**（供排查用，`ArchaicTooth.cs:26`、`TouchOfOrobas.cs:29`）：
两者都是**表达式体属性**（每次访问新建字典，不是 `static readonly` 字段）——
所以补丁必须挂 getter 或改写返回值，不能"拿引用改一次"。
`ArchaicTooth` 的判定链是
`GetTranscendenceStarterCard`（卡组里第一个命中该表的牌）→ `AfterObtained` → `CardCmd.Transform`，
且替换时会**继承初始牌的升级状态**（`if (starterCard.IsUpgraded) CardCmd.Upgrade(...)`）与 `Enchantment`。

> ⚠️ 由此推得：初始套牌里是**未升级**火球术，所以这样换出来的「落日」**不带升级**。
> 要改的话得在自己的 `GetTranscendenceTransformedCard` 里主动 `CardCmd.Upgrade`。

---

### 6.18 施加动作 vs 数值变化：用 `BeforePowerAmountChanged`

两个钩子的**实际**行为（别只看名字）：

| 钩子 | 何时发 | 位置 |
|---|---|---|
| `BeforePowerAmountChanged` | **无条件**发（施加动作本身） | `PowerCmd.cs:231`（ModifyAmount）、`PowerCmd.cs:124`（Apply） |
| `AfterPowerAmountChanged` | **仅当 `modifiedOffset != 0`** | `PowerCmd.cs:247`（有 `if` 守卫） |

⚠️ `AfterPowerAmountChanged` 的 XML 注释写的是"after a power's amount is changed"，
看起来像"每次施加都发"，但**实现里有 `if ((int)modifiedOffset != 0)`** ——
"敌人已有 3 层易伤、再给 3 层"这种层数不变的施加**不会**触发它。

**所以凡语义是"施加动作本身算数"的效果**（例：「全知形态」按**种类**计数，
同种类只需种一次），都必须用 `BeforePowerAmountChanged`。

**两个分支的初始化时机不同，这是最坑的地方**：

```csharp
// Apply 分支（新建实例）：此时 power.Owner 还是 null！
//   要等 ApplyInternal(target, ...) 才赋值
await Hook.BeforePowerAmountChanged(combatState, power, amount, target, applier, cardSource);

// ModifyAmount 分支（已存在的实例）：power.Owner 已有值
await Hook.BeforePowerAmountChanged(combatState, power, offset, owner, applier, cardSource);
```

→ **不要用 `power.Owner` 判断阵营**，要用钩子显式传进来的 `target`（接受者）。
签名：`BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)`。

另外记得滤掉 `amount <= 0`：递减/移除也会走这个钩子，那不算"施加"。

### 6.19 普通字段要自己处理存档与克隆

`OmniscientFormPower._appliedThisTurn` 是个 `HashSet<ModelId>` 普通字段 ——
**不在存档里，也不会自动清空**。Save&Load 会克隆能力实例，
于是重进后那些种类仍被认为"本回合已给过" → 本回合剩余时间再也无法触发。

修法：重写 `AfterCloned()` 里 `Clear()`（字段是 `readonly`，只能 `Clear()` 不能重新赋值）。

```csharp
protected override void AfterCloned()
{
    base.AfterCloned();
    _appliedThisTurn.Clear();
}
```

> 取舍：清空是**安全**方向 —— 最坏情况只是同一种类在本回合被重复触发一次，
> 而"该触发却不触发"是硬失效。凡"每回合/每场战斗"的临时状态都按这个原则处理。

### 6.20 让卡面显示「额外结算 N 次」

照抄原版「终结技」`Finisher`：

```csharp
// ① 声明一个 CalculatedVar（不是 CalculatedDamageVar —— 这个不是伤害）
new CalculatedVar("CalculatedHits")
    .WithMultiplier((CardModel card, Creature? _) => 数出次数),

// ② 读取
int hits = (int)((CalculatedVar)DynamicVars["CalculatedHits"]).Calculate(target);

// ③ 文案：包在 InCombat 里，非战斗不显示
// zhs: …{InCombat:\n（额外结算{CalculatedHits:diff()}次）|}
// eng: …{InCombat:\n(Resolves {CalculatedHits:diff()} additional {CalculatedHits:plural:time|times})|}
```

`{CalculatedHits:plural:time|times}` 是英文复数（见 §6.12）。
与 `{CalculatedDamage:diff()}` 的区别：这个渲染的是**普通数字**，不是伤害值。

⚠️ **`CalculatedVar` 有硬前置变量，漏了会在"创建卡牌"时崩**：

```
ERROR: KeyNotFoundException: The given key 'CalculationBase' was not present in the dictionary.
  at DynamicVarSet.get_CalculationBase()
  at CalculatedVar.GetBaseVar()
  at CalculatedVar.UpdateValues()
  at CalculatedVar.SetOwner()
  at DynamicVarSet.InitializeWithOwner()
  at CardModel.DeepCloneFields()
  at AbstractModel.MutableClone_Patch1()
  at CardModel.ToMutable()
  at CombatState.CreateCard(...)
  at DevConsole...CardConsoleCmd.Process_Patch1(...)
```

`CalculatedVar.SetOwner` → `UpdateValues` → `GetBaseVar()` 会去取
`DynamicVars.CalculationBase`，所以**即使卡面根本不显示这两个值，也必须声明**：

```csharp
new CalculationBaseVar(0m),     // ← 倍率算式的"基数"
new CalculationExtraVar(1m),    // ← 每单位倍率加多少
new CalculatedVar("CalculatedHits").WithMultiplier(...),
```

原版「终结技」`Finisher` 同样声明了这两个。

> ⚠️ 这个崩溃**只在 `ToMutable()` 时爆**（所以是"控制台加卡/进战斗时崩"，
> 而游戏启动时不崩）—— 因为 `InitializeWithOwner` 是在 `DeepCloneFields` 里跑的。
> 静态检查看不出来，必须实际加一张进战斗。

### 6.21 `TargetType.Self` 的群体攻击牌拿不到预览目标

**症状**：卡面动态值（`CalculatedVar` 倍率）在手里和拖拽时**都**算不出来，
永远显示 `CalculationBase` —— 而实际伤害是对的。

**根因**（`NCard.cs:872` 取 `_previewTarget ?? Model.CurrentTarget`）：

| 路径 | 触发条件 | 会设预览目标吗 |
|---|---|---|
| `SingleCreatureTargeting` | `AnyEnemy` / `AnyAlly` | ✅ `Connect(CreatureHovered)` → `SetPreviewTarget` |
| `MultiCreatureTargeting` | 其余（含 **`Self`**、`AllEnemies`） | ❌ 只调 `ShowMultiCreatureTargetingVisuals()` |

原版「欺凌」`Bully` 是 `AnyEnemy`，所以"手里显示基础值、拖出来显示动态值"
是天然的；而「落日」是 `Self`（"对所有敌人"靠 `TargetingAllOpponents`，
不需要选目标），**引擎从不给它设预览目标** → 倍率恒返回 0。

**修法**：`Patches/SelfTargetingPreviewPatch.cs` —— 前缀拦
`NCardPlay.ShowMultiCreatureTargetingVisuals`（鼠标拖进 Play Zone 时触发），
对 `TargetType.Self` **且** `CardType.Attack` 的牌补
`SetPreviewTarget(HittableEnemies[0])`。

- 必须用**前缀**：原方法紧接着就 `UpdateVisuals(...)`，之后补目标就晚了
- 只补 `Self + Attack`，避免影响纯自增益牌（它们的倍率若对 target 敏感，
  补目标反而会显示错误预览）
- `CardNode` 是 `protected` 属性，用 `AccessTools.Property` 取

> 打了这个补丁后，`CalculatedVar` 的倍率函数就能**用 `target != null`
> 判断"是否处于拖拽瞄准态"** —— 即 `FieldState.HighestBurnForPreview` 的写法。

### 6.22 自定义角色场景：`GetNode<T>` 的**类型**不匹配会静默毁掉 `_Ready`

**症状**（联机专属）：火堆选完选项后卡住、**选项不消耗 → 可无限重复选**。
单人局完全正常。

**日志里有两个异常，第二个才是误导人的那个**：

```
① ERROR: System.InvalidCastException: Unable to cast object of type 'Godot.Control'
          to type 'MegaCrit.Sts2.Core.Nodes.Combat.NSelectionReticle'.
     at Godot.Node.GetNode[T](NodePath path)
     at NRestSiteCharacter._Ready()

② ERROR: System.NullReferenceException
     at NRestSiteCharacter.ShowSelectedRestSiteOption(RestSiteOption option)
     at NRestSiteRoom.OnAfterPlayerSelectedRestSiteOption(...)
```

**根因链**：`NRestSiteCharacter._Ready()` 开头是**无保护**的一串取节点：

```csharp
_controlRoot      = GetNode<Control>("ControlRoot");
Hitbox            = GetNode<Control>("%Hitbox");
_selectionReticle = GetNode<NSelectionReticle>("%SelectionReticle");  // ← ①抛在这
_leftThoughtAnchor  = GetNode<Control>("%ThoughtBubbleLeft");         // ← 没执行到
_rightThoughtAnchor = GetNode<Control>("%ThoughtBubbleRight");        // ← 没执行到
```

① 抛异常 → **`_Ready` 整段中止** → 两个思维气泡锚点保持 `null`；
② 处 `GetRestSiteOptionAnchor()` 返回 null → `null.GlobalPosition` → NRE。

**为什么只有联机暴露**：`OnAfterPlayerSelectedRestSiteOption` 第一行就是
`if (_runState.Players.Count <= 1) return;` —— 单人局根本不走这条路径。

**修法**：`%SelectionReticle` **必须是原版场景的实例**，不能是裸 `Control`：

```
[ext_resource type="PackedScene" path="res://scenes/ui/selection_reticle.tscn" id="3_wizreticle"]
[node name="SelectionReticle" parent="ControlRoot" instance=ExtResource("3_wizreticle")]
```

原版 `scenes/rest_site/characters/*.tscn` 的 5 个角色场景全都是 `instance=` 写法。

> ⚠️ **引用时不要带 uid**。原版那条是
> `uid="uid://bgy88bk44xxvk" path="res://scenes/ui/selection_reticle.tscn"`，
> 但那个 uid 的映射在**原版 pck** 里，mod 的 pck 没有 ——
> Godot 解析 `ExtResource` 时 uid 优先于 path，跨 pck 可能解析失败。
> 观者 mod 引用同一场景时用的是**纯 path**，照它写。

> 📌 **通用教训**：给自定义角色写 `CustomXxxPath` 场景时，
> 凡是 `_Ready` 里以 `GetNode<T>("%X")` 取用的节点，
> **只要 T 是具体的 C# 类型（而不是 `Control`）就必须 `instance=` 原版场景**。
> 而且 `_Ready` 没有 try/catch —— 任何一处类型不符都会让**其后所有**初始化静默失效，
> 报错点与出错点可能隔着几十行，极易误判。

---

### 6.23 多人不同步事故（2026-09-24，**已定位，非代码 bug**）

> **✅ 根因（用户确认）：两台机器的 mod 版本不一致。**
> 当天最后一次改动动过「凝神」`ConcentratePower`，
> 一方跑了新代码、另一方跑了旧代码，于是两人的 `凝神` 效果不同 ——
> 客户端每次消耗给的法术增益比主机多 1。
>
> 这正好解释了下面所有"代码上说不通"的现象：
> **同一份静态代码不可能产生 ±1 的差值**，
> 因为 `ConcentratePower.Amount = 2` 时它一次给 +2、给不出 +1。
> 差值只能是"两侧跑的不是同一份实现"。
>
> 📌 **通用教训**：`HandshakeManager` 只校验它认为"gameplay relevant"的 mod
> **版本号 / 哈希**；两边的 mod 只要版本号相同（或校验口径不一致），
> 代码内容不同也不会被拦下。**改完代码后必须确认联机双方都换成新包。**

> 日志：`%APPDATA%\SlayTheSpire2\logs\godot.log`。事故行：15931 / 15945（首次）、
> 18668 / 18753（复现）。结果都是主机 `SteamHost` 主动踢掉客户端，
> `Disconnecting peer 76561199508841737, reason: StateDivergence`，回主菜单。

#### 6.23.1 引擎的校验和机制

- `ChecksumTracker.GenerateChecksum(context, action)`：每次动作结束（以及回合阶段切换）
  生成一个**递增 id** 的校验和。客户端发给主机，主机逐 id 比对，
  不等就 `StateDivergence` → 踢人（`ChecksumTracker.cs:203-215,151`）。
- 校验和的内容是 `NetFullCombatState`：**生物 HP/格挡/能力(id+Amount)**、
  玩家 能量/星星/金币/5 个牌堆/**遗物/药水/RNG 计数器**、
  `nextChoiceIds` / `nextRewardIds` / `lastExecutedActionId` / `lastExecutedHookId`。
- ⚠️ **能力只算 `Amount`，不算 `DynamicVars`**（`NetFullCombatState.cs:409-416`）。
  所以"只改了展示用 DynamicVar"不会报不同步，**改了 `Amount` 才会**。
- 主机会把**双方的完整状态转储**打进日志（`LOCAL STATE DUMP` / `REMOTE STATE DUMP`），
  这是定位此类问题的第一手证据 —— **先读转储再猜**。

#### 6.23.2 两次事故的确证事实

| 项 | 第一次 | 第二次（稳定复现） |
|---|---|---|
| 分叉点校验和 id | **375** | **83** |
| 上下文 | `PlayCardAction CARD.WIZARD-WATCH_FIRE index 54` | `PlayCardAction CARD.WIZARD-WATCH_FIRE index 37` |
| 本地(主机)哈希 | `3298334217` | `889416099` |
| 远端(客户端)哈希 | `4083867009` | `261472741` |
| 前一个 id | 374 双方一致（凝神） | 81/82 双方一致（凝神 / 防御） |

| 项 | 值 |
|---|---|
| 主机 / 客户端 | `76561199385623943` / `76561199508841737` |
| 双方角色 | **都是** `CHARACTER.WIZARD-WIZARD_CHARACTER`（双巫师） |
| 游戏 / 分支 | `v0.111.0` / `PublicBeta`，Hash `2211597704`（**双方一致**） |
| 模组差异 | 主机多一个 `QuickSLButton-0.1.0`（非 gameplay），`HandshakeManager` 明确放行 |

**两次都是两份状态转储逐行比对后，唯一差异只有一行**（脚本 diff，153 行全等）：

```
第一次  主机 : Power POWER.WIZARD-SPELL_POWER_POWER Amount: 19
        客户端: Power POWER.WIZARD-SPELL_POWER_POWER Amount: 20
第二次  主机 : Power POWER.WIZARD-SPELL_POWER_POWER Amount: 12
        客户端: Power POWER.WIZARD-SPELL_POWER_POWER Amount: 13
```

**完全相同**的部分（因此可以排除）：

- 双方 HP / MaxHP / 格挡
- 全部其它能力（含 `CONCENTRATE_POWER Amount: 2`、`PYROMANIAC_POWER`、
  `VIGOR_POWER`、`ANCIENT_FRAGMENT_POWER` …）
- 所有敌人的全部能力（含 `BURN_POWER`、`CHILL_POWER`、`PARALYSIS_POWER`）
- 5 个牌堆的**每一张牌**（含 `Keywords`、`Energy Cost` 覆盖、`Floor`）
- 遗物、药水、金币、能量
- **全部 RNG 计数器** → **不是 RNG 分叉**
- `Choice IDs` / `Reward IDs` / `Last executed action ID`

#### 6.23.3 为什么"±1"这个数字就是根因的指纹

`法术增益` 只有两个来源：

1. `CalmMind.OnPlay` → `Apply<SpellPowerPower>(…, 1)` —— 固定 **+1**
2. `ConcentratePower.AfterCardExhausted` → `Apply<SpellPowerPower>(…, max(Amount,1))`

事故当下 `CONCENTRATE_POWER Amount = 2`（两份转储都证实），
**一次触发应该给 +2**。可是两边只差 **1**。

> 这个 ±1 无法用"同一份代码在两侧跑出不同结果"解释：
> 要么它给 2、要么它不给，**给不出 1**。
> 唯一自洽的解释就是 **两侧执行的是不同版本的 `凝神`**
> —— 旧版每次给 1、新版按层数给 2。用户随后确认正是如此。

`Hook.AfterCardExhausted` 的唯一调用点是 `CardCmd.Exhaust`（`CardCmd.cs:246`）；
打出带 `Exhaust` 词条的牌会走到它（`CardModel.cs:1992-1999`）。
`ConcentratePower` 当时确实正常触发（否则 `SpellPower` 不会同时增长）。

#### 6.23.4 排查过程中排除掉的方向（留作参考，**均非本次根因**）

- **RNG 分叉** —— 全部 RNG 计数器逐位相同，排除。
- **`SpellCmd.ApplyBurn` 的"条件短路"** —— `BurnPower.ApplyAbsolute` 在
  `现值 >= 请求值` 时直接 return、不碰引擎（日志里就是"现值=3 请求=1 无需改动"）。
  这确实会让**引擎钩子的派发次数取决于当前层数**，是 lockstep 的通用隐患，
  但本次两侧层数相同、短路行为对称，所以**不是本次根因**。
- **`WizardEvents.BurnApplied` 是 static 事件、订阅者是能力实例** ——
  退订漏一次会让调用列表长度两侧不同。本次两侧 `VIGOR_POWER` 完全一致，排除。
- **`SpellIgnoresStrengthPatch` 等 Harmony 补丁** —— 本次日志未见相关差异。

#### 6.23.5 结案结论

**这不是 mod 的代码 bug，是联机双方 mod 版本不一致。**
不需要改任何游戏逻辑。

> 📌 **操作要求**：改动任何卡牌/能力代码后，
> **联机双方必须同步替换 `Wizard.dll` + `Wizard.pck`**，
> 否则 `凝神` 这类"实现被改过的效果"会以 ±1 的形式累积成不同步并断线。
> 排查此类问题的最快路径：**先 diff 两份状态转储**（`LOCAL` / `REMOTE STATE DUMP`），
> 差异落在哪个 `Power` / 哪个牌堆上，就直接指向对应的那部分代码。

### 6.24 ⚠️⚠️ 能力「不可叠层」的固定病根：`StackType` 与 `Amount` 脱节

**这是本 mod 出现频率最高的一类 bug（已犯 5 次）。**
症状都是同一句话：**"开两张 XX，效果还是只有一张。"**

#### 6.24.1 三种病态写法（都表现为"不可叠层"）

```csharp
// ① 声明 Single —— 引擎直接拒绝累加，永远只有 1 层
public override PowerStackType StackType => PowerStackType.Single;

// ② 声明 Counter（层数确实在累加），但效果里硬编码常量、从不读 Amount
public override PowerStackType StackType => PowerStackType.Counter;
...
await PowerCmd.Apply<SomePower>(ctx, target, 1m, Owner, null);   // ← 写死 1m
```

② 最阴险：**能力图标上的数字会从 1 变成 2、看起来"叠上了"，
但实际效果完全没变** —— 白白累加一个不起作用的数字。

```csharp
// ③ 【2026-10-04 新增】读了 Amount、循环也对，但循环里施加的是"取高值"型效果，
//    且每次只请求 1 —— 于是 N 次请求被取高值吃成 1 次。
public override PowerStackType StackType => PowerStackType.Counter;
int hits = Amount;                       // ✅ 读了 Amount
for (int i = 0; i < hits; i++)           // ✅ 循环 N 次
{
    await CreatureCmd.Damage(...);        // ✅ 伤害叠加正确
    await SpellCmd.ApplyBurn(ctx, target, 1m, Owner, null);   // ❌ 每次只请求 1 层
}
```

③ 的隐蔽之处：**伤害是对的**（所以"伤害没问题"会让人以为整体没问题），
只有"取高值"型的效果（燃烧）不对 ——
`BurnPower.ApplyAbsolute` 里 `current >= desired` 时直接返回，
所以**请求 1 层 N 次，结果仍是 1 层**。

> 📌 **实测记录**：3 张「燃烧之魂」+「火球术」，伤害正确但燃烧只到 2 层。
> 日志逐条对得上：
> ```
> 第1段 ① 请求1 → 写入1    ② 请求1 → 无需改动  ③ 请求1 → 无需改动
> 第2段 ④⑤⑥ 请求1 → 全部"无需改动"（现值仍 1）
> 火球术 ⑦ 请求2 → 1 -> 2    ← 火球术自己的 2 层把结果盖成 2
> ```
> 修法：循环里改成请求 `hits` 层。

> 🔍 **判据**：循环里"每次施加"的那个量，必须**随 `Amount` 缩放**。
> 若某效果是"取高值/不叠加"型（燃烧、以及一切 `ApplyAbsolute` 语义的东西），
> 那么"循环 N 次、每次 +1"**等价于只加 1 次** ——
> 此时应当**一次请求 N**，而不是循环 N 次各请求 1。
> 换句话说：**叠加要靠"请求量"承载，不能靠"请求次数"承载。**

#### 6.24.2 唯一正确写法：把值直接放进 `Amount`，让引擎累加

```csharp
public override PowerStackType StackType => PowerStackType.Counter;
private decimal PerTrigger => Math.Max(Amount, 1);   // 兜底，避免为 0 时退化成"不触发"

await PowerCmd.Apply<SomePower>(ctx, target, PerTrigger, Owner, null);
```

引擎的叠加本来就是**加算 offset**（`PowerCmd.ModifyAmount` → `SetAmount(Amount + offset)`，
见 §6.3），所以"打第二张"天然让 `Amount` 由 1 变 2，**不需要自己维护任何变量**。

> ⚠️ 不要另建一个 `DynamicVar` 当"真实值"、再在 `AfterPowerAmountChanged` 里同步 ——
> 那是 §6.10 那套"展示用变量"的做法，用在**效果值**上会引入两类新问题：
> 存档/克隆后不同步（§6.19）、以及"图标数字和实际效果对不上"。
> **效果值就该是 `Amount` 本身。**

#### 6.24.3 已修复清单（改动都是单点：`Single`→`Counter` 或 `1m`→`Amount`）

| 能力 | 原病态 | 修法 |
|---|---|---|
| `AncientFragmentPower` | `Single` | → `Counter`，`1 + 0.25×Amount×种类数` |
| `FrostWallPower` | `Single` | → `Counter`，冻结/寒冷按 `Amount` 缩放 |
| `ConcentratePower` | `Counter` 但写死 `1m` | → 按 `Amount` 给层 |
| `PyromaniacPower` | `Counter` 但写死 `2m` | → `Amount` = 每次给的活力 |
| `AlternatingCurrentPower` | `Single` **且**写死 `1m` | → `Counter`，每次给 `Amount` 层 |
| `AsceticismPower` | `Single`（**2026-10-04 才修**，陈年 bug） | → `Counter`，每层各追加 1 组卡牌奖励 |
| `BurningSoulPower` | `Single`（**2026-10-04 修**，曾被我判为"合法的 Single"） | → `Counter`，每次攻击追加 `Amount` 段火焰伤害 + `Amount` 层燃烧 |

> 📌 **`AsceticismPower` 是第 6 次、也是最久没被发现的一次。**
> 它不涉及数值，只是"战斗结束后多给一组卡牌奖励"，
> 所以没有"数字对不上"这种显眼症状 —— 表现仅仅是
> **打两张「修行」也只给一组奖励**，很不容易注意到。
>
> **教训**：这个坑不只发生在"数值型"能力上。
> **只要效果的"量"随层数变化**（哪怕量是"几次"、"几组"、"几张"），
> 就必须 `Counter` + 读 `Amount`。
> 判据是 **"有没有'更强'的概念"**，而不是"有没有数字"。

#### 6.24.4 后续纪律

- 新增任何"可叠层"能力时，**先写 `StackType`，再立刻确认效果表达式里出现了 `Amount`**。
- 单纯"层数只影响显示、不影响效果"的能力，**不应该**声明 `Counter` ——
  那只会让人误以为它可叠层（用 `Single` 或 `None`）。
- **`Single` 的合法用途只有"二元开关"**：有/没有，没有"更强"的概念。
  本项目当前合法的 `Single` **只剩两个**（2026-10-04 起），
  改代码时若发现别的能力也想用 `Single`，先回答"它变强的方式是什么"：
  | 能力 | 为什么 `Single` 是对的 |
  |---|---|
  | `BurnImmunityPower` | 免疫是布尔状态，重复获得没有意义 |
  | `CirculationPower` | 效果固定（扩大电流外溢的候选池），不随层数缩放 |

  > 📌 **`BurningSoulPower` 曾是这张表里的第三个，2026-10-04 被移出。**
  > 当时的理由是"效果固定（每次攻击追加等量火焰伤害），不随层数缩放" ——
  > 听起来成立，但那是**把"当前实现"当成了"设计上限"**。
  > 用户随后明确："每次攻击追加 n 段，打一张为 1，后续打多张可叠层"
  > ⇒ 它有"更强"的概念，属于 `Counter`。
  >
  > **教训**：用 `Single` 前别只问"它现在的实现会不会随层数变"，
  > 要问 **"它是否**<b>应该</b>**随层数变"**。
  > 前者的答案会被现有代码带偏，后者才是设计意图。
- 回归验证：`grep` 所有 `StackType => PowerStackType.Counter` 的能力，
  逐个确认其效果路径读到了 `Amount`。可用下面这条粗筛：

  ```powershell
  Get-ChildItem src\Scripts\Powers\*.cs |
    Select-String -Pattern 'StackType => PowerStackType.Counter' -List |
    ForEach-Object { $_.Path }
  # 再对每个文件人工确认 Amount 是否出现在效果里（不是只在注释/Display 变量里）
  ```

---

### 6.25 X 费卡：`HasEnergyCostX` + `ResolveEnergyXValue()`

「焚尽」`BurnToAshes` 是本 mod 第一张 X 费卡。写法照抄原版
「串刺」`Skewer`（X 费攻击）、「挽歌」`Dirge`（X 费技能）、
「萎靡」`Malaise`（X 费 + 升级 X+1）。

**三件套，缺一不可：**

```csharp
// ① 声明 X 费 —— 引擎才会把费用显示成 X，并让 ResolveEnergyXValue() 可用
protected override bool HasEnergyCostX => true;

// ② 构造函数 baseCost 传 0（这是 X 费卡的"基数"，不是实际费用）
public BurnToAshes() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

// ③ OnPlay 里取值
int x = ResolveEnergyXValue();
```

> ⚠️⚠️ **绝不要自己去读能量**（如 `Owner.PlayerCombatState.Energy`）。
> `ResolveEnergyXValue()` 内部走的是
> `Hook.ModifyXValue(CombatState, this, EnergyCost.CapturedXValue)`
> （`CardModel.cs:1113-1124`），因此「化学X」`ChemicalX` 这类
> **"X 值 +N"的遗物会自动生效**。自己读能量会静默漏掉这些修正。
> 另外它捕获的是**打出那一刻**的 X，所以不要缓存到字段里跨回合用。

**升级写成 "X+1"**，用 `IsUpgraded` 分支而不是 DynamicVar：

```csharp
int x = ResolveEnergyXValue();
if (IsUpgraded) { x++; }      // 照抄 Malaise.cs:34-38
...
protected override void OnUpgrade() { }   // 无 DynamicVar 可升，留空
```

对应本地化写 `X{IfUpgraded:show:+1}`（原版「萎靡」就是这个写法）：

| | 文案 |
|---|---|
| zhs | `给予全体敌人X{IfUpgraded:show:+1}层[gold]燃烧[/gold]。` |
| eng | `Apply X{IfUpgraded:show:+1} [gold]Burn[/gold] to ALL enemies.` |

> 为什么不用 `{IfUpgraded:show:X+1\|X}` 那种写法：
> 两种在引擎里都能工作，但 `X{IfUpgraded:show:+1}` 与「萎靡」逐字一致，
> 升级前后都有 X，读者一眼能看出"X 在，只是 +1"。

**验收方式**：用 §2.2c 的 IL 校验直接确认产物里
`get_HasEnergyCostX` 返回 `ldc.i4.1`（true），
且 `OnPlay` 状态机里依次出现
`ResolveEnergyXValue()` → `get_IsUpgraded()` → 两个效果调用。

---

### 6.26 打出牌时的钩子时序：`BeforeCardPlayed` **先于**伤害结算

决定"某个效果是**先挂上**还是**后挂上**"的唯一依据。
「大寒波」<see cref="ColdWave"/> 就是靠这个让本回合的攻击**当次**吃到寒冷加成。

**引擎里"打出一张牌"的顺序**（`CardModel.Play`）：
```
await Hook.BeforeCardPlayed(combatState, cardPlay);   // CardModel.cs:1926
...
Task task = OnPlay(branchingPlayerChoiceContext, cardPlay);  // :1933  ← 伤害在这里结算
await branchingPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion(task);
...
await Hook.AfterCardPlayed(combatState, choiceContext, cardPlay);   // :1965
```

| 钩子 | 相对伤害 | 典型用途 |
|---|---|---|
| **`BeforeCardPlayed`** | **之前** | 需要"当次就生效"的增益/减益（「大寒波」挂寒冷） |
| `AfterCardPlayed` | 之后 | 需要读"这张牌结算结果"的效果（原版「湮灭」`Oblivion` 结算末日） |
| `AfterCardPlayedLate` | 更晚 | 要排在所有 `AfterCardPlayed` 之后 |

> ⚠️ **别用 `AfterCardPlayed` 去挂"本次伤害要吃到的加成"** ——
> 那时伤害已经结算完了，只有**之后**的牌能吃到，与设计预期差一张牌。
> 这是"看着对、实际差一拍"的典型错误，光看牌面文案看不出来。

**参考实现**：
- 原版「腐蚀波」`CorrosiveWavePower` → `AfterCardDrawn`
  （抽牌即上毒，同样在"牌的结算"之前，所以当次也吃得到）
- 原版「湮灭」`OblivionPower` → `BeforeCardPlayed` **记计划** +
  `AfterCardPlayed` **结算**（用内部 `Dictionary<CardModel,int>` 记住
  "这张牌打出前有几层"）
- 本 mod「大寒波」`ColdWavePower` → `BeforeCardPlayed` 里**立刻 apply**。
  **不需要** `Oblivion` 那本字典，因为 apply 没有延后：
  在 `OnPlay` 之前读到的 `Amount` 就是"这张牌生效前"的层数。

#### 6.26.1 ⚠️⚠️ "这张牌不吃自己提供的加成" ≠ "完全跳过这张牌"

这是「大寒波」实测踩到的坑，**很容易想当然写错**。

**错误写法**（第一版）：
```csharp
if (cardPlay.Card is ColdWave) return;   // ❌ 让「大寒波」完全不触发寒冷
```
想当然的理由是"防止自触发"，但语义错了。

**正确语义**：卡牌提供的加成对它**自己**不生效，但**此前已生效的层数仍然生效**。
期望时序（经用户实测确认）：

| 动作 | 应触发寒冷 | 之后能力层数 |
|---|---|---|
| 打大寒波 A | **0 层**（此时能力还不存在） | 1 |
| 打大寒波 B | **1 层**（来自 A） | 2 |
| 打大寒波 C | **2 层**（来自 A+B） | 3 |

**实现上不需要任何特殊分支**：因为 `BeforeCardPlayed` 在该牌的 `OnPlay`
（把层数加进 `Amount`）**之前**执行，直接读 `Amount` 读到的就是"打之前"的层数 ——
"排除自己、保留过往"自动成立。

> 📌 **通用教训**：需要"排除自身贡献"时，
> 优先找**读取时机天然就在自身生效之前**的钩子，
> 而不是加一个"遇到自己就整个跳过"的分支 ——
> 后者会把"自身贡献"和"自身这个动作"一起丢掉。

---

### 6.27 「本场战斗实际生效了多少」→ 读 `CombatHistory`（⚠️ 要数"施加动作"见 §6.27.1）

「巨焱拳」<see cref="BlazingPunch"/> 一开始要"本场战斗中给予的燃烧层数总和"。
如果口径是"**实际生效的层数**"，那**引擎已经在记录了**，直接读历史即可：

```csharp
using MegaCrit.Sts2.Core.Combat.History.Entries;

CombatManager.Instance.History.Entries
    .OfType<PowerReceivedEntry>()
    .Where(e => e.Power is BurnPower && e.Applier == self)   // 只算"我给的"
    .Sum(e => e.Amount);                                     // 只含实际写入的层数
```

**范本**：原版「死亡之门」<c>DeathsDoor</c>（同一个姿势，只是用 `Any` 问
"本回合给过末日吗"）：

```csharp
CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
    .Any(e => e.HappenedThisTurn(base.CombatState) && e.Power is DoomPower
           && e.Applier == base.Owner.Creature);
```

**`PowerReceivedEntry` 的字段**（`Entries/PowerReceivedEntry.cs`）：
| 字段 | 含义 |
|---|---|
| `Power` | 被施加的能力**实例**（可用 `is BurnPower` 判类型） |
| `Amount` | 本次**实际写入**的层数 |
| `Applier` | 施加者（`Creature`），用它筛"我给的" |
| （基类）`HappenedThisTurn(state)` | 是否发生在当前回合 |
| （基类）`HappenedLastPlayerTurn(player)` | 是否发生在该玩家上个回合 |

> ✨ **`Amount` 天然只含"实际生效"的层数**：`PowerCmd.ModifyAmount` 只在
> `modifiedOffset != 0` 时才调 `History.PowerReceived`（<c>PowerCmd.cs:242-243</c>）。
> 所以燃烧"取高值"导致没有真正加层时，历史里**不会**有那一条 ——
> 正是设计要的"给予的燃烧层数"口径（没加上去的不算）。

> ⚠️⚠️ **但"只含实际生效"正是「巨焱拳」最终**不**用历史表的原因**（2026-10-01 改）：
> 设计要的口径是"**施加动作**计数"——
> 怪身上已有 2 层燃烧，再打「观火」施加 1 层，层数不变，**但必须计 1**。
> 历史表里没有这条（`offset == 0` 不记录），事后根本无法区分
> "没施加"与"施加了但被取高值吃掉"。
> **所以"取高值导致无变化"的场景必须用累加器，不能用历史表。**
> 详见 §6.27.1。

**卡面直接显示这个总值，不再另起括号**（2026-10-01 改动）：
用引擎自带的 `CalculatedDamageVar`（`CalculatedVar` 的子类），
它的算式就是通用形式 `CalculationBase + ExtraDamage × 倍率`：

```csharp
new CalculationBaseVar(4m),          // 印刷伤害
new ExtraDamageVar(1m),              // ⚠️ 不是 CalculationExtraVar！见 §6.29
new CalculatedDamageVar(ValueProp.Move | SpellProps.For(School))
    .WithMultiplier((CardModel card, Creature? _) => TotalBurnAppliedThisCombat(card)),
```

于是 `n = 4 + 本场累计**施加**的燃烧`，手里直接看到计算后的值，
文案写 `{CalculatedDamage:diff()}`，**结算也走同一个**
`DynamicVars.CalculatedDamage.Calculate(target)` ——
展示与结算天生一致，不会出现"显示一个数、打出来另一个数"。

> ⚠️ **`CalculatedVar` 在非战斗时倍率自动归零**
> （`Calculate` 里有 `CombatManager.Instance.IsInProgress` 判断），
> 所以卡组/奖励界面显示的就是印刷伤害，不需要自己写兼容分支。

---

### 6.27.1 需要"施加动作"计数（而非"实际生效"）时的正解：常驻累加器 + 战斗开始清零

`WizardEvents.BurnApplied` **已经在语义上做对了**：
它由 `SpellCmd.ApplyBurn` 在**所有分支上无条件**触发，
携带的是**请求层数**（不是最终层数），且 `ApplyBurn` 是全部燃烧来源的唯一收口
（`SpellCmd.cs:67-70`）。引擎的 `AfterPowerAmountChanged` 反而做不到
（`offset == 0` 时不发）。

**难的不是"怎么计数"，而是"在哪里复位"。** 两步：

**① 常驻订阅**（不能像「纵火狂」那样靠能力生命周期，因为
"这张牌还没入手时施加的燃烧"也要算）：

```csharp
// Entry.Initialize() 里调一次
BurnTally.Subscribe();          // -= 再 +=，幂等（照抄 PyromaniacPower.AfterApplied）
```

**② 战斗开始清零** —— 卡牌侧拿不到这个钩子，所以用 Harmony 补丁挂在
引擎唯一的"战斗开始"入口上：

```csharp
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class BurnTallyResetPatch
{
    private static void Postfix() => BurnTally.Reset();
}
```

> 💡 **为什么挂 `BeforeCombatStart`**：它是引擎里唯一的战斗开始入口
> （`Hook.cs:311`），在它之前不可能有本场战斗的燃烧施加 ——
> 清在这里既不会漏也不会误清。
>
> 💡 **失败模式是安全的**：Postfix 只做 `Reset()`。
> 万一补丁没打上（`Entry.ApplyPatches` 会逐个打印成功/失败），
> 最坏结果是**计数跨战斗偏多**，而不是崩溃或数据损坏。
> 所以改完务必看日志里的 `补丁已应用：BurnTallyResetPatch -> 1 个方法`。

**按施加者分表，用 `ConditionalWeakTable` 而不是 `Dictionary`**：
后者会**永久持有** `Creature` 引用，把已离场的敌人/玩家留在内存里。
弱表让条目随对象回收自动消失（值用 `StrongBox<decimal>` 包一层，
因为 `decimal` 是值类型、不能直接当 `TValue`）。

> 📌 **一句话选型**：
> - 要"**实际生效了多少**" → 读 `CombatHistory`（`PowerReceivedEntry`）
> - 要"**施加了多少次/多少层（含被取高值吃掉的）**" → 常驻累加器 + 战斗开始清零

---

### 6.28 「升级 = 多个数值各 +1」（不改词条、不加费用）

「禁书目录」<see cref="MagicalIndex"/> 2026-09-28 的设计改动：
升级**不再移除「消耗」**，而是四种负面效果各 +1 层。

```csharp
protected override void OnUpgrade()
{
    DynamicVars[VulnerableKey].UpgradeValueBy(1m);
    DynamicVars[BurnKey].UpgradeValueBy(1m);
    DynamicVars[ParalysisKey].UpgradeValueBy(1m);
    DynamicVars[ChillKey].UpgradeValueBy(1m);
}
```

> ⚠️⚠️ **不要以为 `PowerVar` 会自己涨。**
> `PowerVar<T>` 继承 `DynamicVar`，而 `DynamicVar` **没有任何**
> "升级步长"机制 —— `UpgradeValueBy` 就是个纯加法
> （`DynamicVar.cs:143-147`：`BaseValue += addend`）。
> 原版也**没有**"一张牌多个 PowerVar 一起升级"的范本可抄。
>
> 所以多值升级必须**逐个显式列出**。要核对升级是否漏项，
> 最省事的办法是数 IL 里 `UpgradeValueBy` 的调用次数：
> 见 §2.2c 的 Mono.Cecil 校验法（`OnUpgrade` 应有 N 次调用，N = 变量个数）。

**"改词条" vs "改数值"是两条不同的路**，别混：
| 升级内容 | 写法 | 范本 |
|---|---|---|
| 加/去**关键字** | `AddKeyword` / `RemoveKeyword` | 原版「杂耍」「蜃景」，见 §6.1 |
| 改**数值** | `UpgradeValueBy` | 绝大多数卡 |
| 改**费用** | `EnergyCost.UpgradeBy(±n)` | 原版 60 张牌 |

---

### 6.29 ⚠️⚠️ `CalculatedDamageVar` 要 `ExtraDamageVar`，**不是** `CalculationExtraVar`

**同一个词根、不同子类，配套变量就不同 —— 声明错了游戏能正常启动、构建也全绿，
只在"卡面刷新"那一刻爆炸。** 2026-10-01 实际踩到。

| 用什么 | 必须配套声明 | 运行时读的键 |
|---|---|---|
| `CalculatedVar`（基类，「降雷」的 `CalculatedHits`） | `CalculationBaseVar` + **`CalculationExtraVar`** | `CalculationExtra` |
| `CalculatedDamageVar`（伤害卡，「巨焱拳」「落日」） | `CalculationBaseVar` + **`ExtraDamageVar`** | `ExtraDamage` |
| `CalculatedEnergyVar`（自定义，见 `ManaDrain`） | `CalculationBaseVar` + `CalculationExtraVar` | `CalculationExtra` |

**原因**：`CalculatedVar.GetExtraVar()` 默认返回 `DynamicVars.CalculationExtra`，
但 **`CalculatedDamageVar` 把它重写成了 `DynamicVars.ExtraDamage`**
（`CalculatedDamageVar.cs` 末尾）。

**症状**（只在把卡放进手牌/拖拽时触发，因为那时才 `UpdateCardPreview`）：
```
System.Collections.Generic.KeyNotFoundException:
  The given key 'ExtraDamage' was not present in the dictionary.
   at DynamicVarSet.get_ExtraDamage()
   at CalculatedDamageVar.GetExtraVar()
   at CalculatedVar.Calculate(Creature target)
   at CalculatedDamageVar.UpdateCardPreview(...)
   at CardModel.UpdateDynamicVarPreview(...)
   at NCard.UpdateVisuals(...)
```
**表现**：控制台 `card WIZARD-XXX hand` 加不进去、卡牌库里点开就报错，
而**其他所有牌都正常** —— 极具误导性，容易怀疑 `[Pool]`、注册或 pck。

> 📌 **为什么构建期发现不了**：`CalculationExtra`/`ExtraDamage` 的取值只在
> `UpdateCardPreview` 里发生，编译与 IL 校验都看不到"运行时字典里有没有这个键"。
> **静态能查的只有"声明了哪些变量"** —— 所以新增/改动 `Calculated*Var` 卡时，
> 务必用 §2.2c 的 IL 法确认 `CanonicalVars` 里的变量**类型名**对不对：
> ```
> newobj ...CalculationBaseVar
> newobj ...ExtraDamageVar          ← 伤害卡要的是这个
> newobj ...CalculatedDamageVar
> ```
>
> 🔍 **最省事的自查**：`new CalculatedDamageVar(` 旁边必须有 `new ExtraDamageVar(`；
> `new CalculatedVar(` 或 `new CalculatedEnergyVar(` 旁边必须有 `new CalculationExtraVar(`。

---

### 6.30 ⚠️ 给**玩家自己**上原版减益 → 引擎会自动"多留一个回合"

**症状**：打出「决心」<c>Determination</c> 后，自身的易伤/脆弱**下个回合不掉层**，
要等到**下下个回合**才掉 1 层。

**根因**在 `PowerCmd.Apply`（<c>PowerCmd.cs:148-151</c>）：

```csharp
if (target.Side == CombatSide.Player && power.Type == PowerType.Debuff)
    power.SkipNextDurationTick = true;      // ← 引擎自动吞掉第一次掉层
```

而 `VulnerablePower` / `FrailPower` / `WeakPower` 都只在
`AfterSideTurnEnd(side == Enemy)` 里 `TickDownDuration`（后者会看这个 skip 标志）。
两个"一回合"叠加，实际就是**两个回合**：

| 时点 | 易伤层数 |
|---|---|
| 回合 N 打出「决心」 | 1（引擎已置 `skip = true`） |
| **回合 N+1 开始** | **1** ← 玩家察觉"没掉层" |
| 回合 N+1 敌方回合结束 | skip 吞掉 → 仍 1 |
| **回合 N+2 开始** | **1** |
| 回合 N+2 敌方回合结束 | → 0 |

**这是引擎的刻意设计**（`SkipNextDurationTick` 的注释：
*"skipping the first tick if a monster applied the power to the player"*），
原版诅咒「羞耻」`Shame` / 「怀疑」`Doubt` 也**显式**把实例的
`SkipNextDurationTick` 再设一次 `true`，等于确认这个语义。

**要让减益"下个自己回合开始就掉"**（= 只覆盖本回合），
把那个标志撤销回 `false` 即可 —— 让"本回合之后的那个敌方回合结束"正常 tick 一次：

```csharp
VulnerablePower? vulnerable = await PowerCmd.Apply<VulnerablePower>(...);
FrailPower?      frail      = await PowerCmd.Apply<FrailPower>(...);

// 撤销引擎对"玩家侧减益"自动加的那次跳过
vulnerable.SkipNextDurationTick = false;
frail.SkipNextDurationTick = false;
```

改完的时序：

| 时点 | 易伤层数 |
|---|---|
| 回合 N 打出「决心」 | 1 |
| 回合 N 敌方回合结束 | tick → 0 |
| **回合 N+1 开始** | **0** ✅ 正是设计期望 |

> 📌 **要点**：`PowerCmd.Apply<T>()` **有返回值**（`Task<T?>`），
> 拿它就能直接改实例标志 —— 不需要 `GetPower<T>()` 再查一次，也不用反射。
>
> ⚠️ **只影响"给自己上减益"的牌**。给**敌人**上易伤/虚弱
> （如「清净世界」`ClearWorld`、「击穿」`Puncture`、「禁书目录」`MagicalIndex`）
> **不受影响** —— 那个自动 skip 只在 `target.Side == Player` 时加，
> 而且敌人侧的减益本来就该在自己的回合结束时正常 tick。
>
> 🔍 **想数"到底几个回合"**时，别只看 `SkipNextDurationTick`，
> 要连 `AfterSideTurnEnd(side == Enemy)` 这个**触发时机**一起算 ——
> 两者相乘才是玩家感知到的持续回合数。

#### 6.30.1 为什么这里**必须**和 `Shame`/`Doubt` 反着做（设计口径）

⚠️ **不要"为了跟原版一致"把它改回去。** 两者表面同类，实际语义相反。
判据是**这张牌是什么时候打出的**：

| | 「羞耻」`Shame` / 「怀疑」`Doubt` | 「决心」`Determination` |
|---|---|---|
| 牌的类型 | **诅咒**（凭空塞进卡组） | 普通技能，**玩家主动打出** |
| 打出的时机 | **自己回合结束时**才结算 | **自己回合内**打出 |
| 若沿用 skip（多留一回合） | ✅ 必要 —— 见下 | ❌ 惩罚会多延续一整个"能感知"的回合 |
| 正确口径 | **继承到下回合** | **下回合开始就掉** |

**诅咒为什么必须 skip**：`Shame` 给脆弱、`Doubt` 给虚弱，而
**脆弱/虚弱只在"玩家回合"才有意义**（它们影响玩家打出的牌）。
诅咒是在**回合结束时**才生效的 —— 若这 1 层不继承到下个玩家回合，
那"打出的当回合"已经过去、下回合又没有，这层效果<b>等于根本不存在</b>。
所以 skip 是让诅咒**从"没用"变成"有用"**。

**决心为什么必须不 skip**：「决心」在**自己回合内**打出，玩家**当回合接下来的
出牌就已经吃到脆弱**了（脆弱影响的是玩家打出的牌）——
也就是说这层惩罚**在打出的那一刻就已经兑现**，不需要靠"继承"来体现价值。
而它给的**易伤**又与脆弱/虚弱不同：**易伤在怪物回合才有意义**。
所以：

- 打出当回合：玩家自己后续的牌吃**脆弱** → 惩罚已兑现
- 紧接着的**敌方回合**：玩家吃**易伤** → 惩罚继续兑现
- 到**下个自己回合开始**：这一层的价值已经用尽 → 该掉了

**"复数张决心会顺延"正是这套口径的自然结果**：
每打 1 张就多 1 层，而每层各自需要一个回合边界才会掉一层 ——
所以连打 N 张就是 N 个回合的惩罚尾巴。
**这不是 bug，是设计**：想只吃一回合的惩罚，就只打一张。

> 📌 **一句话判据**：自伤减益该不该"下回合开始就掉"，
> 看的是**这份惩罚在打出的当回合是否已经兑现**。
> - 已兑现（决心：当回合的脆弱 + 紧随的敌方回合易伤）→ **不 skip**
> - 未兑现，且效果只在玩家回合有意义（Shame/Doubt 的脆弱/虚弱）→ **要 skip**
>
> 另注：脆弱/虚弱/易伤**三者触发时机不同** ——
> 脆弱与虚弱影响"玩家打出的牌"，易伤影响"怪物打出的牌"。
> 判断持续回合数时，必须把**效果的生效侧**和**掉层的触发侧**分开算。

---

### 6.31 操作**任意**牌堆的通用姿势（含消耗牌堆）

**结论：引擎把六个牌堆统一抽象成 `PileType`，所以"操作弃牌堆/消耗牌堆"
与"操作抽牌堆"是同一套调用，只需换一个枚举值。原版与 BaseLib 都支持。**

```csharp
public enum PileType { None, Draw, Hand, Discard, Exhaust, Play, Deck }
//                            1     2      3        4
```
（枚举值已用 IL 实测确认：`ldc.i4.4` 就是 `Exhaust`。）

**取牌 → 放回手牌**（「勿忘我」`ForgetMeNot` 用的就是这套）：

```csharp
// ① 拿到牌堆对象（扩展方法，任何 PileType 都能取）
CardPile exhaust = PileType.Exhaust.GetPile(owner);

// ② 从该牌堆选牌（CardSelectCmd.FromCombatPile 收的是 CardPile，与牌堆种类无关）
List<CardModel> taken = (await CardSelectCmd.FromCombatPile(
    choiceContext, exhaust, owner,
    new CardSelectorPrefs(SelectionScreenPrompt, take))).ToList();

// ③ 放回手牌
await CardPileCmd.Add(taken, PileType.Hand);
```

> 📌 **范本**：这一段与「顿悟」`Epiphany`「从抽牌堆取牌」**完全同构**，
> 而 `Epiphany` 照抄的是原版「许愿」`Wish` / 「疏浚」`Dredge`。
> 换句话说：**原版没有"直接操作弃牌堆的卡"不等于做不到** ——
> 引擎层面牌堆是统一抽象的，只是没有原版卡去用而已。

**往任意牌堆塞新牌**（凭空生成的牌，如状态牌）：

```csharp
CardModel card = CombatState.CreateCard<Amnesia>(owner);   // 造实例
CardCmd.PreviewCardPileAdd(                                // 让玩家看见它飞过去
    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, owner));
```
范本：原版「黏糊糊」`GunkUp`（往弃牌堆塞 `Slimed`）、「愤怒」`Anger`。

> ⚠️ **`AddGeneratedCardToCombat` vs `CardPileCmd.Add` 别用混**：
> - **凭空造的**新牌 → `AddGeneratedCardToCombat`（会往战斗历史写 generated 记录）
> - **已存在**的牌换牌堆（如从消耗牌堆取回手牌）→ 普通 `CardPileCmd.Add`
>
> 判定标准是"这个实例是不是刚造出来的"，不是"目标牌堆是哪个"。

> ⚠️ 取回手牌**必须先算手牌空位**（`CardPile.MaxCardsInHand - Hand.GetPile(owner).Cards.Count`），
> 并 `Math.Min` 到牌堆现有张数 —— `Add` 超限会丢牌，选牌数超过牌堆张数会卡住选牌界面。

---

### 6.32 状态牌 / 诅咒牌怎么注册（不进角色卡池）

**两道保险，缺一不可：**

```csharp
[Pool(typeof(StatusCardPool))]                       // ① 牌池用 StatusCardPool
public class Amnesia : WizardCardModel
{
    public override bool CanBeGeneratedInCombat => false;   // ② 禁掉战斗内随机生成
    public override int MaxUpgradeLevel => 0;               // ③ 状态牌不可升级

    public Amnesia() : base(1, CardType.Status, CardRarity.Status, TargetType.Self) { }
}
```

| 机制 | 作用 |
|---|---|
| `[Pool(typeof(StatusCardPool))]` | 不进 `WizardCardPool`，所以**不出现在奖励/商店/图鉴**（原版 `Burn`/`Dazed`/`MindRot` 都在这个池） |
| `CanBeGeneratedInCombat => false` | 阻止"战斗中随机生成卡牌"的效果产出它（照抄原版 `MindRot`） |
| `MaxUpgradeLevel => 0` | 状态牌不可升级（照抄 `Burn`/`MindRot`） |
| `CardType.Status` + `CardRarity.Status` | 卡面边框与稀有度显示 |

> ⚠️ **不要给状态牌挂 `[Pool(typeof(WizardCardPool))]`** —— 那会让它进奖励池。
>
> 📌 **`CardRarity.Token` 是给"衍生无色牌"的**（`Ponder`/`Epiphany`，用 `TokenCardPool`），
> 与状态牌不是一回事：Token 牌是角色机制的一部分（初始遗物产出），
> 状态牌是负面牌。两者的池、稀有度、能否生成都不同。

> 🔍 **费用给多少**：本站的状态牌是**可打出的**（设计写"打出后…消耗"），
> 所以给 1 费 + `CardKeyword.Exhaust`。
> 对照原版：`Burn` 是 `Unplayable`（费用 `-1`），`MindRot` 是被选中后直接生效（费用 `-1`）。
> **照设计写，别照抄原版的费用。**
>
> ⚠️ **`ExtraHoverTips` 里挂 `FromKeyword(Exhaust)` 不等于卡牌拥有该词条。**
> 2026-10-03 实际踩到：`Amnesia` 只写了 hover tip、忘了
> `CanonicalKeywords`，于是卡面**不显示「消耗」**、打出后也不消耗。
> 两者是**两件事**：
> - `CanonicalKeywords` → 真正的词条（引擎会据此消耗该牌，并把词条画在卡面上）
> - `ExtraHoverTips` → 只是鼠标悬停时弹出的说明条目

---

### 6.33 从消耗牌堆捞回来的牌，**词条保留**、但**临时费用已经用掉了**

（2026-10-03 查证 + 用户实测修正）

问：「暗黑神秘学」印出来的牌是 0 费 + 带「消耗」，
若把它**打出去**（于是进了消耗牌堆），再用「勿忘我」捞回来，那些特性还在吗？

**分两种特性，结论不同：**

| 特性 | 捞回来还有吗 | 为什么 |
|---|---|---|
| **「消耗」词条** | ✅ **有** | 词条挂在实例上，且**没有过期机制** |
| **0 费** | ❌ **没了，恢复原费** | 费用修饰符的过期条件含 `WhenPlayed`，而这张牌**已经打出过了** |

**链路上"实例不克隆"这一点是确定的**（三段证据）：

1. **消耗时进堆的是原实例，不是克隆**
   `CardCmd.Exhaust` → `CardPileCmd.Add(card, PileType.Exhaust, …)`（`CardCmd.cs:244`）
2. **`CardPileCmd.Add` 不克隆**——核心重载只是校验后把**同一批实例**挪进新牌堆。
   全文件唯一的克隆分支是 `PileType.Deck`（还要 `RunState.ContainsCard` 判定），
   战斗内牌堆之间的移动**不经过**它。
3. 所以实例上的一切（词条、升级、附魔、费用修饰符）**都随实例走**。

**那 0 费为什么还是会没？**因为问题不在"保不保留"，而在**修饰符已经被消费掉了**：

```csharp
// SetToFreeThisTurn() → EnergyCost.SetThisTurnOrUntilPlayed(0)
_localModifiers.Add(new LocalCostModifier(cost, LocalCostModifierAbsolute,
    LocalCostModifierExpiration.EndOfTurn | LocalCostModifierExpiration.WhenPlayed, reduceOnly));
//                                                      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//                                                      EndOfTurn 或 WhenPlayed，先到先得
```
（`CardEnergyCost.cs:197-203`）

⚠️ **关键：`WhenPlayed` 在"打出的那一刻"就触发** —— 也就是
**在它被消耗、进消耗牌堆之前**，那个 0 费修饰符就已经被清掉了。
所以捞回来的实例，费用早就恢复原样了，跟"跨不跨回合"无关。

> ❌ **不要写成"同回合捞回来仍是 0 费"** —— 那是错的（曾是这里的错误结论）。
> 正确说法：**已经打出过的牌，其 `UntilPlayed` 类费用修饰符一律已失效。**

> 📌 **设计口径（用户 2026-10-03 确认，刻意维持现状）**：
> 「暗黑神秘学」**就是只给一次免费打出的机会**；
> 想捞回来复用，就得付原本的费用。这在逻辑上是自洽的，
> **不是 bug，不要"顺手修"**。

> 📌 **启示**：做"从某个牌堆取回"的效果前，先确认**牌堆存的是实例还是原型**。
> 战斗内的六个牌堆都是实例；只有 `Deck`（房间之间的卡组）走克隆，
> 所以"战斗内的改动不会污染卡组里的原型"（`PileType.cs` 的 `Deck` 注释明确写了这点）。
>
> 但**实例保留 ≠ 效果保留** —— 还要看那个效果**自己有没有过期/消费机制**
> （`UntilPlayed`、`ThisTurn`、`ThisCombat` 等）。两者要分开判断。

---

### 6.34 「每当施加 X 时」必须走**统一收口**（2026-10-04，为「弹射」而建）

**症状**：想做"每当施加寒冷时……"的效果，却发现无从挂载。

**病因**：寒冷当时有 **4 个各自为政的施加点**，全都直接
`PowerCmd.Apply<ChillPower>(...)`：
`IceShard` / `MagicalIndex` / `ColdWavePower` / `FrostWallPower`。
没有任何单一位置能代表"发生了一次施加"。

**正解**：像燃烧那样建一个收口 + 一个事件。

```csharp
// SpellCmd
public static async Task ApplyChill(ctx, Creature target, decimal amount, Creature? applier, CardModel? source)
{
    if (amount <= 0m || !target.IsAlive) return;
    await PowerCmd.Apply<ChillPower>(ctx, target, amount, applier, source);
    await WizardEvents.RaiseChillApplied(target, applier, amount);   // 无条件
}
public static async Task ApplyChillToAll(...)   // 逐个 await

// 订阅方（「弹射」的返回手牌逻辑）
WizardEvents.ChillApplied -= OnChillApplied;   // 先减后加 = 幂等
WizardEvents.ChillApplied += OnChillApplied;
```

**关键点：**
- **事件无条件触发**，与 `BurnApplied` 一致。语义是"**施加动作**本身算数"，
  与层数最终变成多少无关。引擎的 `AfterPowerAmountChanged` 做不到
  （`offset == 0` 时不发，见 §6.24 与 `PowerCmd.cs:247`）。
- **燃烧与寒冷在此处不同**：燃烧是"取高值"（要走 `BurnPower.ApplyAbsolute` 的锁内读改写），
  寒冷是可累加的 `Counter`，直接 `Apply` 即可。**别照抄那边的层数逻辑。**
- **改施加点时务必改成走收口**，否则新的施加点会静默地不触发事件。
  现存 4 处已全部改完（`ApplyChill` ×3、`ApplyChillToAll` ×1）。

### 6.35 「从弃牌堆返回手牌」的判据：`Pile.Type == PileType.Discard`

观者 mod「疾风连击」`FlurryOfBlows` 的写法，「弹射」`DoubleRicochet` 照抄：

```csharp
if (Pile is not { Type: PileType.Discard }) return;   // ★ 这一个判据管两件事
await CardPileCmd.Add(this, PileType.Hand);
```

这一个判据**同时**解决两个问题：

1. **只在"已经进弃牌堆"时才回手** —— 手牌里/已消耗的都不该被拉回来。
2. **不会与「大寒波」形成无限连** —— 这是设计明确要求的（用户原话：
   "不能刚打完就立刻弹回手，而必须再打一张牌才会弹回手"）。
   打出的牌在结算期间位于 **`PileType.Play`**，不是 `Discard`；
   而「大寒波」用 `BeforeCardPlayed`（§6.26：**先于** `OnPlay` 与伤害结算），
   那一刻本牌还在 `Play` 堆里 ⇒ 判据为假 ⇒ **不弹回**。
   只有本牌结算完进了弃牌堆、**再打一张牌**触发寒冷时才满足判据。

> ✅ **IL 已核实**：`get_Pile` → `get_Type` → `ldc.i4.3`(Discard) → `brfalse`；
> 返回目标 `ldc.i4.2`(Hand) → `CardPileCmd::Add`。

#### 6.35.1 ⚠️ 只有上面那条判据**还不够**：必须再加"每回合限 1 次"

**2026-10-06 补充。** 上面那条判据只能挡住"**同一张牌**自我循环"，
挡不住"**多张牌接力循环**"：

> **1 张「大寒波」+ 2 张「弹射」= 无限。**
> 「大寒波」在**每张牌打出前**都给一次寒冷（`BeforeCardPlayed`），于是：
> 打出 A → A 进弃牌堆 → 打出 B → *B 的 `BeforeCardPlayed` 触发寒冷* ⇒ **A 弹回** →
> 打出 A → *A 的 `BeforeCardPlayed` 触发寒冷* ⇒ **B 弹回** → 打出 B → …
> 两张牌互相把对方捞回来，判据每次都为真。

所以设计加了一条限制（`design.md`："（每回合限1次）"），实现是**每张牌各自**
一个"本回合是否已弹回"的标志：

```csharp
private bool ReturnedThisTurn { get; set; }

// 触发点里
if (ReturnedThisTurn) return;            // ★ 限次
...
await CardPileCmd.Add(this, PileType.Hand);
ReturnedThisTurn = true;                 // 只在**成功回手之后**才记数

// 玩家回合开始时重置
public override Task AfterSideTurnStart(CombatSide side,
    IReadOnlyList<Creature> participants, ICombatState combatState)
{
    if (side == CombatSide.Player && participants.Contains(Owner.Creature))
        ReturnedThisTurn = false;
    return Task.CompletedTask;
}

// ⚠️ 存档/读档后也要重置（与 OmniscientFormPower 同一个坑）
protected override void AfterCloned()
{
    base.AfterCloned();
    ReturnedThisTurn = false;
    _returning = false;
}
```

**三个要点：**
- **是"每张牌各自一次"，不是"整场战斗每回合共 1 次"** ——
  2 张「弹射」每回合仍可各弹 1 次，只是无法互相接力成无限。
- **记数要放在 `CardPileCmd.Add` 成功之后**。放在前面的话，
  手牌满等提前 `return` 的分支会白白消耗掉这次机会。
- **`AfterCloned` 必须清**（§6.19 那一类坑）：普通字段不进存档、
  克隆后保留，不清就会导致"读档后本回合再也弹不回来"——
  这是**硬失效**；而多弹一次只是小失衡，所以清空是安全的方向。

> ✅ **IL 已核实**：`AfterSideTurnStart` 里有
> `Player::get_Creature` → `Enumerable::Contains` → `set_ReturnedThisTurn`；
> `AfterCloned` 里也有 `set_ReturnedThisTurn`。

**另外两道保险**（都要留）：
- `applier != Owner.Creature` 时跳过 —— 别人给敌人上寒冷不该让我回手。
- `_returning` 重入标志 + 手牌已满（`MaxCardsInHand`）则跳过。

### 6.36 临时属性 + **可被净化固化**：`Type` 故意设成 `Debuff`

「快速咏唱」（本回合获得 N 点法术增益）照抄原版
「热修复」`Hotfix` / 「预判」`Anticipate` 的机制（真身是
`TemporaryFocusPower` / `TemporaryDexterityPower`）：

```csharp
// ① BeforeApplied：先把属性真正加上
await PowerCmd.Apply<SpellPowerPower>(new ThrowingPlayerChoiceContext(),
    target, amount, applier, cardSource, silent: true);

// ② AfterSideTurnEnd：移除自己，再把同样的量扣回去
int amount = Amount;
await PowerCmd.Remove(this);                              // ⚠️ 先移除自己
await PowerCmd.Apply<SpellPowerPower>(ctx, Owner, -amount, Owner, null);
```

**引擎顺序已核实**：`PowerCmd.Apply` 里是
`await power.BeforeApplied(...)` → `power.ApplyInternal(...)`，
所以"先加属性、再挂上自己"成立。

> ⚠️ **扣回时必须先 `PowerCmd.Remove(this)` 再扣属性**。
> 反过来的话，"扣属性"可能又触发 `AfterPowerAmountChanged` 造成重复扣减。

#### 6.36.1 ⚠️ `Type => PowerType.Debuff` 是**刻意的**，别"修正"它

按直觉，一个**纯收益**的临时增益应该标 `Buff`。但这里必须标 `Debuff`：

「高温消毒」`SterilizeByFire` 的实现是
```csharp
List<PowerModel> debuffs = self.Powers.Where(p => p.Type == PowerType.Debuff).ToList();
foreach (var d in debuffs) await PowerCmd.Remove(d);
```
—— **"移除所有负面效果"= 移除所有 `Type == Debuff` 的能力**。

于是把它标成 `Debuff`，玩家就能用「高温消毒」**提前摘掉这颗临时标记**；
而"扣回属性"的代码就在 `AfterSideTurnEnd` 里、随它一起消失 ——
**临时的法术增益就变成了永久的**。这是设计要的玩法（"用净化把临时增益固化"），
所以改代码时不要因为"效果明明是纯收益"而把它改成 `Buff`。

> 同类注意：「燃烧」「寒冷」这些 debuff 也是 `Debuff`，
> 「高温消毒」会一并摘掉（那是它本来的用途）。

---

## 7. 未决事项

1. **诊断日志待清理** —— 见 §5（现仅剩 `[Wizard/Burn]`、`[Wizard/Freeze]`、`[Wizard/Particles]`）。
2. **若干数值待用户确认** —— 见 `HANDOVER.md`。
3. **遗物还差 6 个、药水还差 3 瓶**。
4. **先古对话的 `firstVisitEver` / `ANY` 键**。
5. **🅿️ 已封存：「法术伤害是否享受活力」** —— 见 §7.1（作者与报错玩家**均无法复现**，2026-09-25 暂停）。
6. **🅿️ 已封存：CombatSolver 无法模拟「降雷」** —— 见 §7.2（外部 mod 兼容性问题）。

### 7.1 已封存：「法术伤害不享受活力」报告

**状态：暂停，不处理。** 作者本地与报错玩家**都无法复现**。

**静态分析结论（重要，避免以后重复排查）：**
mod 侧**没有任何**阻止活力生效的代码 ——
- 4 个 Harmony 补丁里只有 `SpellIgnoresStrengthPatch` 涉及伤害加算，
  而它**只拦 `StrengthPower.ModifyDamageAdditive`**，不碰 `VigorPower`；
- `VigorPower.ModifyDamageAdditive` 的闸门是 `props.IsPoweredAttack()`
  （= `Move && !Unpowered`），而本 mod 所有伤害牌都带 `ValueProp.Move`
  （`ValueProp.Move | SpellProps.For(School)`）⇒ **判定为真**；
- `dealer` = `card.Owner.Creature`（`FromCard` 设置），与能力拥有者相同；
- 加算钩子会被调用（`CreatureCmd.Damage` 传 `ModifyDamageHookType.All`）。

即：**代码上活力应当生效**。游戏自身文案也是"你的下一张**攻击牌**伤害增加"，
而火苗/降雷/电磁波都确实是 `CardType.Attack`。

**唯一已证实的异常**见 §7.2（CombatSolver 报错），但**无法证明**二者相关。

**若将来要重启排查**，需要的材料：
1. 报错玩家的**日志级别调到 Debug** 后再复现一次
   —— 现有日志是 INFO 级，**完全没有伤害数值**
   （`DamageResult` / `TotalDamage` / `damage dealt` 命中数均为 0），
   所以"是否吃到活力"从日志**无法判定**；
2. 或临时重新加回诊断补丁：`[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.ModifyDamageAdditive))]`
   的 Prefix/Postfix，各打一行含
   `card / props / isSpell / isPoweredAttack / owner==dealer / vigor层数 / 实际加算值`
   —— 能一眼区分"没被调用 / 被闸门挡了 / 确实生效了"。
   （该补丁曾作为 `VigorProbePatch.cs` 存在，2026-09-25 已删除。）

### 7.2 已封存：CombatSolver 无法模拟「降雷」

**状态：暂停。** 这是**外部 mod 的兼容性问题**，不是本 mod 的 bug
（本 mod 单机、联机均正常）。

**现象**（报错玩家日志 `godot2026-09-25T12.35.27.log`，共 **112 次**）：
```
CombatSolver.IncompatibleGameplayModException:
  Unsupported gameplay combat extension card WIZARD-LIGHTNING_FALL:
  calculated variable has no branch-local specification from mod 巫师（Wizard）.
  位于 CombatPredictionDynamicVarExtensions.InvokeCalculate(CalculatedVar, ...)
配套 CombatSolver.SearchTransitionException: 搜索动作回放失败 ... → 整条路线搜索中止
（同一个 GameAction 以 AggregateException 结束）
```

**精确定位**：112 次报错**全部**指向 `WIZARD-LIGHTNING_FALL` 一张卡
（用精确匹配核对过；早前用宽正则得到的"31 张卡"是**误判** ——
那个正则在捕获 CombatSolver 的**镜像登记**日志）。

**推断的成因**：「降雷」的 `CalculatedVar("CalculatedHits")` 倍率函数是
`CountDebuffKindsAcross(card)` —— 本 mod 的**私有静态方法**，内部走
`card.CombatState` / `GetOpponentsOf` 读**实时棋局**。
CombatSolver 要在**它自己的预测分支**里求值这个公式，解析不到，于是抛错。

旁证：CombatSolver 自带的
[COMBAT_HOOK_COVERAGE.md](https://github.com/Torch1230/CombatSolver/blob/main/docs/COMBAT_HOOK_COVERAGE.md)
有一节就叫「**分支内计算变量**」，记着 `含 CalculatedVar 的卡牌：43 / 缺少分支内公式：0`
—— 说明它**明确要求** mod 卡的 `CalculatedVar` 提供"分支内公式"。

**若将来要修**：需要先拿到 CombatSolver 对"分支内公式"的接口约定
（其 `docs/` 里没写，可能要直接问作者 <https://github.com/Torch1230/CombatSolver>）。
届时对照的同名原版卡：`Finisher` / `Flechettes` / `Barrage` / `FlakCannon`
（它们同样用 `CalculatedHits` + `WithMultiplier`）。

> 顺带：`WIZARD-BURN_TO_ASHES`（焚尽）在那局里被打了 8 次、工作正常，
> 说明新卡本身没问题。

> ✅ 已完成（不再列在此处）：
> - 先古替换 —— 走 BaseLib 扩展点，见 §6.17（**不需要 Harmony**）
> - 先古卡池归属 —— 「落日」由 `ITranscendenceCard` 自动排除出尘封魔典池

---

## 8. 建议的推进顺序

> 卡牌与卡图**均已完结（41/41）**，下面只剩非卡牌工作。

1. **遗物**：还差 6 个（现有：魔法卷轴 / 死海古卷）；含 OROBAS 注入补丁
2. **药水**：还差 3 瓶（现有 1 个占位）
3. **先古对话**：补 `firstVisitEver` / `ANY` 键
4. **清理诊断日志**（`[Wizard/Burn]` `[Wizard/Freeze]` `[Wizard/Particles]`）
5. 可选：§7.2 的 CombatSolver 兼容性（需先拿到对方的"分支内公式"约定）

---

## 9. 内容 ID 与文件名约定（写错就不显示）

`Id.Entry = "{命名空间首段大写}-{类名 UPPER_SNAKE}"`（**连字符是 Entry 的一部分**）

| 项 | 约定 |
|---|---|
| 卡图 | `images/cards/{类名}.png` |
| 遗物图标 | `images/relics/{类名 snake_case}.png` |
| 本地化键 | `WIZARD-{类名UPPER_SNAKE}.title` / `.description` |
| 例 | `MagicalIndex` → `WIZARD-MAGICAL_INDEX.title` |

> ⚠️ **改卡牌英文名（类名）时要同步改三处**，漏一处就会出问题：
> ① 类名 —— 决定 `ModelId.Entry`
> ② 卡图文件名 —— `CustomPortraitPath` 是按 `GetType().Name` 拼的（`WizardCardModel`）
> ③ 本地化键 —— `WIZARD-{类名UPPER_SNAKE}.*`
>
> 实例：`IndexOfForbiddenBooks` → `MagicalIndex` 用了这三步，
> 并顺手删掉旧 `.import` 与 `.godot/imported/` 下的旧 `ctex`
> （`.import` 里写死了旧的 `source_file`/`dest_files`，留着会被 `--import` 重新生成，
>   但删掉更干净、也避免误读）。

> ⚠️ 重命名卡牌类时，`WIZARD-RESOLVE` 这类键里的连字符**不是词边界**，
> 正则批量替换会漏掉，要单独处理。
> 另注意别误改 `SpellProps.Resolve()` —— 那是判定伤害法术流派的真实方法。

---

### 6.37 ⚠️⚠️ 「人工制品」会抵消施加，但**"施加动作"照样发生**（2026-10-13）

**症状（用户报告）**：怪物身上有 `ArtifactPower` 时，负面效果被抵消，
但「全知形态」「纵火狂」照样触发、「巨焱拳」照样计燃烧层数。
期望：**只有真的上到了才触发**。

**根因** —— 引擎的抵消是"把层数改成 0"，**不是**"让施加不发生"：

```csharp
// ArtifactPower.cs
public override bool TryModifyPowerAmountReceived(
    PowerModel canonicalPower, Creature target, decimal amount, ..., out decimal modifiedAmount)
{
    if (target != base.Owner) { modifiedAmount = amount; return false; }
    if (canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff) { ... }
    if (!canonicalPower.IsVisible) { ... }
    modifiedAmount = 0m;                          // ★ 层数归零
    return true;
}
public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
    => await PowerCmd.Decrement(this);            // ★ 消耗一层
```

两个钩子都**照样派发**：
- `BeforePowerAmountChanged` 在抵消**之前**就发了；
- 本 mod 自建的 `WizardEvents.BurnApplied` / `ChillApplied` **完全绕过引擎**、无条件触发。

⇒ 所有"每当施加 X"的效果都会被骗。

**唯一正确判据**（收口在 `src/Scripts/Spells/DebuffUtil.cs`）：

```csharp
public static bool BlockedByArtifact(Creature? target)
    => target == null || target.HasPower<ArtifactPower>();
```

原版「不祥之灯」`UnsettlingLamp.cs:102` 也是这么判的（`target.HasPower<ArtifactPower>()`）。

**已加的拦截点（缺一不可）**：

| 位置 | 拦什么 |
|---|---|
| `SpellCmd.ApplyBurn` | 直接 return，**连 `BurnApplied` 事件都不发**（与"燃烧免疫"同一处理） |
| `SpellCmd.ApplyChill` | 同上（保护「弹射」的"施加寒冷时返回手牌"） |
| `BurnTally.Record` | 「巨焱拳」记账层再判一次（常驻单例，防绕过 `ApplyBurn` 的调用） |
| `OmniscientFormPower.ObserveDebuff` | **两条触发路径的唯一汇合点**，一处收口覆盖全部 |
| `PyromaniacPower.OnBurnApplied` | 「纵火狂」给活力前 |

**⚠️ 判据是"artifact"，不是"层数变了"** —— 两者是不同问题：
燃烧"取高值"，目标已有 2 层时再施加 1 层**层数不变但仍要计数**
（见 `BurnTally` 类注释）；被 artifact 抵消则**不计**。

> 若将来新增"每当施加 X"的效果，**必须**在收口处或自身入口加这道判据。

---

### 6.38 非战斗界面（火堆 / 商店 / 涅奥）**不是 `NCreature`**（2026-10-13）

做战败立绘时踩到：挂在 `NCreature.StartDeathAnim` 上的补丁在非战斗界面**永远不会触发**。

**为什么**：`CreatureCmd.KillWithoutCheckingWinCondition` 里是

```csharp
NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(creature);
if (nCreature != null) { deathAnimLength = nCreature.StartDeathAnim(...); }
```

非战斗场景 `NCombatRoom.Instance` 为 **null** ⇒ 整段跳过。

**三个界面的立绘载体毫无共同基类**：

| 界面 | 节点类 | 立绘节点名 | 当前贴图 |
|---|---|---|---|
| 战斗 | `NCreatureVisuals : Node2D` | `Visuals` | `wizard_battle.png` |
| 火堆 | `NRestSiteCharacter : Node2D` | `Visuals` | `wizard_rest_site.png` 的 `AtlasTexture`（坐姿） |
| 商店 | `NMerchantCharacter : Node2D` | `Icon` | `wizard_battle.png` |
| 涅奥 | （同战斗场景） | `Visuals` | `wizard_battle.png` |

⇒ 只能靠**节点名 + 当前贴图**识别。

**「放弃游戏」的唯一收口**：

```
RunManager.AbandonRun()  →  RunManager.GuaranteeKillAllPlayers()   // private async Task，无参
                          →  CreatureCmd.Kill(player.Creature, force: true)
                          →  KillWithoutCheckingWinCondition → StartDeathAnim（仅战斗房间）
```

⚠️ `GuaranteeKillAllPlayers` 是 **private**，`[HarmonyPatch]` 的目标名只能写**字符串**，
不能 `nameof`。

**对齐地面线用"画布底边"而不是硬编码像素偏移**：各场景 `scale` 不同
（战斗 0.6039 / 火堆 0.4384 / 商店 0.875），硬编码会各错各的。
保持 `position.y + H/2*scale` 不变即可（H = 该 Sprite 当前画的那整块画布高度）。

---

### 6.39 ⚠️⚠️ 自绘商人立绘（非 Spine）必须拦掉 `NMerchantCharacter` 的两个方法（2026-10-13）

**症状**：商店里点「放弃游戏」→ 立绘变成战败图，但**游戏卡死**。
⚠️ **正常逛商店完全没问题** —— 出问题的不是进入商店，是**结束游戏**。
（这条区分信息是定位的关键：它把范围从"进入"缩到了"结算"。）

#### 崩溃栈（实测日志）
```
ERROR: InvalidOperationException: Expected BoundObject to be a SpineSprite, but it is a Sprite2D!
   at NMerchantCharacter.PlayAnimation_Patch1(NMerchantCharacter this, String anim, Boolean loop)
   at NGameOverScreen.MoveCreaturesToDifferentLayerAndDisableUi()
   at NGameOverScreen.AfterOverlayOpened()
   at NRun.ShowGameOverScreen(SerializableRun serializableRun)
```

#### 根因：`NMerchantCharacter` 这两个方法**无条件**把第 0 个子节点当 SpineSprite
```csharp
// NMerchantCharacter.cs:45
public override void _Ready()
    => this.RunWhenSpineReady(new MegaSprite(GetChild(0)), ...);

// NMerchantCharacter.cs:53  —— 没有任何 null 保护
public void PlayAnimation(string anim, bool loop = false)
    => new MegaSprite(GetChild(0)).GetAnimationState().SetAnimation(anim, loop);
```

而结算界面**商店分支**会调它（`NGameOverScreen.cs`）：
```csharp
else if (NMerchantRoom.Instance != null)
    foreach (NMerchantCharacter v in NMerchantRoom.Instance.PlayerVisuals)
    {
        v.PlayAnimation("die");          // ← 崩在这里
        v.Reparent(_creatureContainer);
    }
```

#### ⚠️⚠️ 为什么"加个 `die` 动画"或"靠 BaseLib 守卫"都不可靠

BaseLib 有 `MerchantCharacterAnimPatch`，但两道前提**极易失配**：
```csharp
// ①
if (!NodeFactory.CreatedFromFactory(__instance)) return true;   // 放行原始方法
// ②
if (CustomAnimation.HasCustomAnimation(__instance)) return false;
```
- **①** `CreatedFromFactory` 只对走 `NodeFactory.CreateFromScene` 的节点为 true；
  而 `NMerchantRoom.cs:249` 是**自己**
  `PreloadManager.Cache.GetScene(path).Instantiate()`，**绕过了工厂** ⇒ false；
- **②** `HasCustomAnimation` 只保证"**有**个动画播放器"，
  **不保证里面真有那个动画名**。随后它还会试
  `PlayCustomAnimation(__instance, "Die", "die")`，
  播放器里没有 `die` 就返回 false ⇒ **原始方法照样执行 ⇒ 崩**。

> **实测踩坑**：第一版只放了一个"**空的** `AnimationPlayer`"（空 `AnimationLibrary`）——
> 等于没修。补一个真的叫 `die` 的动画**理论上**能修，但这条链路依赖 BaseLib
> 内部多个条件同时成立，**不可验证、不可靠**。

#### ✅ 正解：直接拦掉这两个方法，不依赖任何守卫
见 `src/Scripts/Patches/WizardMerchantGuardPatch.cs`：

```csharp
[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
internal static class WizardMerchantPlayAnimationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMerchantCharacter __instance, string anim)
    {
        if (!WizardMerchantGuard.IsWizardMerchant(__instance))
            return true;                      // 别人的商人走原逻辑
        if (anim is "die" or "Die")
            WizardDeathSprite.SwapAll(__instance);   // 用战败贴图代替死亡动画
        return false;                          // ★ 永不执行原始方法
    }
}
```
`_Ready` 同理拦掉（我们不需要待机动画）。

⚠️ **Harmony 的 Prefix 返回 `false` 时，后续（更内层）的 Prefix 与原始方法都不执行** ——
所以哪怕 BaseLib 的 Prefix 也在同一方法上，也不会再走到崩溃路径。

#### ⚠️ 顺带弄清：为什么**涅奥界面不崩、只是不换图**
`NGameOverScreen` 里"既不在战斗、也不在商店、也不在火堆"那条 `else` 分支：
```csharp
NCreatureVisuals v = player.Creature.CreateVisuals();
_creatureContainer.AddChildSafely(v);
v.SpineAnimation.SetAnimation("die", loop: false);   // ← 不崩
```
因为 `SpineAnimationAccess` 是**显式 null-safe** 的（其类注释原文：
"Null-safe wrapper ... No-ops when the underlying MegaSprite is null"）：
```csharp
public void SetAnimation(string name, bool loop = true, int track = 0)
    => _sprite?.GetAnimationState().SetAnimation(name, loop, track);
```
我们的立绘是普通 `Sprite2D` ⇒ `SpineBody` 为 null ⇒
**这一句静默什么都不做** ⇒ 涅奥界面"不崩但保持站姿"。

**同一个结算流程，两条分支健壮性不同**，这就是"商店卡死、涅奥只是不换图"的原因。
修法是给 `MoveCreaturesToDifferentLayerAndDisableUi` 加 Postfix，
对 `%CreatureContainer` 里的立绘再跑一次 `SwapAll`（`CreateVisuals()` 是新实例，
立绘还是 `wizard_battle.png`，正好符合换图条件）。

> **方法论教训**：这一处前后错了 4 轮，根因都是**"看到日志里有 ERROR 就当成卡死的原因"**，
> 而没先验证"这个错误是否真在卡死的那条执行路径上"。
> 用户提供的**区分性观察**（"不点放弃就一切正常"）才是决定性的 ——
> 定位问题时应当主动去问这类能把范围一刀切开的观察，而不是急着宣布找到根因。

---

### 6.40 战败立绘：换图必须**严格限定只改巫师自己的节点**（2026-10-13）

第一版为了"不管当前是哪个界面都能找到立绘"，直接从 `SceneTree.Root` 全树遍历，
凡节点名叫 `Visuals`/`Icon` 的 `Sprite2D` 就换图。实测日志：
```
[Wizard] 放弃游戏：已替换 25 个战败立绘节点。   ← 涅奥（应为 1）
[Wizard] 放弃游戏：已替换 33 个战败立绘节点。   ← 火堆（应为 1）
[Wizard] 放弃游戏：已替换 40 个战败立绘节点。   ← 商店（应为 1）
```
后果：
1. **换错人** —— `Visuals`/`Icon` 这两个名字原版怪物/角色也用，
   且遍历会碰到**预加载缓存里的场景实例**（包括商人自己的立绘）；
2. **污染缓存** —— 换掉的是缓存里那个节点的贴图，影响后续实例化。

✅ **正解：三重过滤，缺一不可**（`src/Scripts/Patches/WizardDeathSprite.cs`）
```csharp
节点名 ∈ { "Visuals", "Icon" }
&& sprite.IsInsideTree()                        // 排除预加载缓存
&& 沿父链的 SceneFilePath 以 "res://Wizard/" 开头   // 权威判据：是不是巫师自己的场景
```
修好后日志每次都是 `已替换 1 个`，且路径正确。

**另外两点**：
- 换贴图用 `sprite.SetDeferred(Sprite2D.PropertyName.Texture, death)`，
  而不是直接赋值 —— 我们是在 Harmony 回调里改别的节点的可渲染属性。
- **地面线对齐要用"画布底边"而不是硬编码像素偏移**：
  各场景 `scale` 不同（战斗 0.6039 / 火堆 0.4384 / 商店 0.875），
  保持 `position.y + H/2*scale` 不变即可（H = 该 Sprite 当前画的那整块画布高度）。

---

### 6.41 ⚠️⚠️⚠️ 「人工制品」只能由**引擎**抵消 —— 绝不要在施加前自己拦（2026-10-13）

**症状（用户实测）**：
1. 「燃烧」不能使「人工制品」减少层数；
2. 「禁书目录」4 种负面效果只减 2 层人工制品；
3. 「禁书目录」打掉最后一层人工制品、只上了最后的寒冷时，仍会触发「纵火狂」。

**根因：三个现象是同一个错误造成的** ——
上一轮我在 `SpellCmd.ApplyBurn` / `ApplyChill` 里加了
`if (DebuffUtil.BlockedByArtifact(target)) return;`，想"避免被骗触发"。
但**「减一层人工制品」是引擎做的**：
```
PowerCmd.Apply:131   modifiedAmount = Hook.ModifyPowerAmountReceived(...)   // artifact 把 amount 改成 0
PowerCmd.Apply:156   await Hook.AfterModifyingPowerAmountReceived(...)      // ★ artifact 在这里 Decrement 自己
PowerCmd.ModifyAmount:246  同上（叠层路径）
```
我们**提前 return** ⇒ 整条路径都没跑 ⇒ ① 层数不减。
禁书目录 4 种里被我拦掉的那些同样没走到引擎 ⇒ ② 只减 2 层。

而 ③ 更微妙：`BlockedByArtifact` 判的是"**现在**有没有 artifact"，
但引擎在判定之后<b>立刻把那一层减掉了</b> ⇒
"打掉最后一层"这种情况查询返回 false ⇒ 被误判成"没被抵消" ⇒ 照样触发。

#### ✅ 正解：让引擎自己抵消，我们只判断"这次施加是否被引擎接受"

| 场景 | 用什么判据 | 为什么 |
|---|---|---|
| **非燃烧负面**（寒冷/易伤/麻痹…） | `AbstractModel.AfterPowerAmountChanged` | `PowerCmd.Apply:157` 是 `if (modifiedAmount != 0m)` —— 被抵消时**根本不发**。旧版用 `BeforePowerAmountChanged`（在抵消**之前**发）所以会误触发。 |
| **燃烧** | `WizardEvents.BurnApplied` 的 **`landed`** 参数 | 燃烧有"取高值"语义，`AfterPowerAmountChanged` 在"层数没变"时也不发，会漏触发。而 landed 必须由**施加方**在施加那一刻用快照算出 —— 那时还看得见 artifact。 |

**关键实现细节**（`BurnPower.ApplyAbsolute`）：
```csharp
int before = target.GetPowerAmount<BurnPower>();
bool blocked = target.HasPower<ArtifactPower>();     // ★ 在施加前抓快照
// …写入…
int after = target.GetPowerAmount<BurnPower>();
return new BurnApplicationResult(after > before);    // 被抵消 ⇒ false
```
⚠️ 注意"目标已有更高层燃烧、这次取高值没升上去"要返回 **true**
（那是设计口径："施加动作本身算数"，见 `BurnTally` 类注释）。

⚠️ `AfterPowerAmountChanged` **没有 `target` 参数**，接受者是 `power.Owner`。

#### ⚠️ 顺带发现：`BurnPower.TryModifyPowerAmountReceived` 的 early-return 会漏掉 artifact
```csharp
// 首次施加时 Owner 尚未绑定，直接放行。
if (burn.Owner == null || burn.Owner != target) { return false; }
```
`return false` = "不改数值"，但 artifact 的
`Hook.ModifyPowerAmountReceived` **照样会被调用**（它是独立的 Hook 遍历），
所以这一条不影响 artifact 减层 —— **前提是我们不要在更外层提前 return**。

#### 📌 判断准则（写进 `DebuffUtil` 的类注释）
> 只有调用点本身就是"施加动作的发起处"、且**不需要让引擎消耗 artifact 层数**时，
> 才可以用 `DebuffUtil.BlockedByArtifact`。
> 凡是"要被 artifact 抵消并减层"的施加，**一律不要提前拦**。

> **方法论教训（第二次同类错误）**：上一轮我把 `BlockedByArtifact` 当成"通用的
> artifact 判据"到处加，却没先弄清"**artifact 的层数是谁减的、在哪一步减**"。
> 加拦截之前必须先确认：**我要拦掉的那条路径里，还夹带了什么我需要的副作用。**

---

### 6.42 遗物要订阅自定义事件时，生命周期跟**遗物**（不是跟战斗）（2026-10-13）

**症状**：遗物「铅咒板」在目标已有高层数燃烧、再施加**低层数**燃烧时不触发。
期望触发。用户建议"与「全知形态」「纵火狂」对齐"。

**根因**：燃烧是"取高值"的（<c>BurnPower</c>），低层数施加<b>不改变层数</b> ⇒
引擎的 <c>if ((int)modifiedOffset != 0) await Hook.AfterPowerAmountChanged(...)</c>
（<c>PowerCmd.cs:247</c>）**根本不发**。
而铅咒板当时<b>只</b>挂了 <c>AfterPowerAmountChanged</c>，所以收不到通知。

**修法**：额外订阅 <c>WizardEvents.BurnApplied</c>（它就是为"施加动作本身算数"
而存在的，在 <c>SpellCmd.ApplyBurn</c> 里无条件触发），
并按事件带来的 <c>landed</c> 过滤。

> ⚠️ 两条路都会到（层数上升时引擎钩子 + 事件），靠同一个去重标志收敛成一次。

#### ⚠️⚠️ 但订阅的生命周期与能力**不同** —— 照抄会出 bug

| | 能力（`PyromaniacPower` / `OmniscientFormPower`） | 遗物（`CurseTablet`） |
|---|---|---|
| 生命周期 | 每场战斗**新建**、战斗结束被清空 | **持久**，一直在 <c>Owner.Relics</c> 里 |
| 订阅时机 | <c>AfterApplied(Creature?, CardModel?)</c> | <c>AfterObtained()</c> |
| 退订时机 | <c>AfterCombatEnd</c> + <c>AfterRemoved(Creature)</c> | **只在** <c>AfterRemoved()</c> |
| 钩子签名 | 带参数 | **无参** |

⚠️ **若照抄能力那样在 <c>AfterCombatEnd</c> 退订，第二场战斗就再也收不到事件了。**
遗物的订阅必须跟遗物本身同生共死。

⚠️ <c>RelicModel</c> 的钩子是
<c>AfterObtained()</c> / <c>AfterRemoved()</c>（<b>无参</b>），
不是能力那套带参签名 —— 写错了会 <c>CS0115: 没有找到适合的方法来重写</c>。

⚠️ 常驻订阅要防御"遗物还在、但 Owner 已不成立"（旧局结束、实例未回收、新局开始）：
在回调第一行判 <c>Owner?.Creature == null</c> 直接返回。

#### 验证证据
- <c>RelicCmd.cs:53</c> 与 <c>RunManager.cs:735</c> 都会调 <c>relic.AfterObtained()</c>
  ⇒ 拾起时订阅**可靠**（不必再挂 <c>AfterCombatStart</c> 兜底）。
- IL 核实：<c>CurseTablet::AfterObtained</c> 含 <c>add_BurnApplied</c>、
  <c>Unsubscribe</c> 含 <c>remove_BurnApplied</c>、
  <c>&lt;OnBurnApplied&gt;d__14</c> 读 <c>landed</c> 字段。

---

### 6.43 `CanBeGeneratedInCombat`：哪些牌不能战斗内生成（2026-10-19）

**机制**：`CardModel.cs:643` 的虚属性，默认 `true`；置 `false` 即从"战斗内生成"池里排除。

**唯一两个使用点**：
| 位置 | 作用 |
|---|---|
| `CardFactory.FilterForCombat`（`CardFactory.cs:159-162`） | 被 `GetDistinctForCombat` / `GetForCombat` 调用 |
| `CardFactory` 变形选项（`:203`，仅 `isInCombat == true`） | 战斗内变形 |

⇒ **不影响**普通卡牌奖励、商店、牌库、局外池。

**原版全名单**（26 处，其中卡牌 23 张 + 药水 3 种）：

| 类别 | 卡牌 |
|---|---|
| 局外收益·回复生命 | `NotYet`（时候未到） |
| 局外收益·生命上限 | `Feed`（狂宴） |
| 局外收益·药水 | `Alchemize`（炼制药水） |
| 局外收益·金币 | `HandOfGreed`（贪婪之手）、`Royalties`（王国资产） |
| 局外收益·卡牌奖励 | `TheHunt`（狩猎） |
| 其他功能牌 | `HiddenGem`（未掘宝石）、`Abundance`（富足）、`Nightmare`（夜魇）、`Eidolon`（幻景）、`Transfigure`（重构）、`NeowsFury`（涅奥之怒）、`FranticEscape`（狂乱逃离） |
| 不可打出的状态/诅咒类 | `AscendersBane`、`Disintegration`、`MindRot`、`Sloth`、`Soot`、`WasteAway` |
| 药水 | `FairyInABottle`、`FruitJuice`、`RegenPotion` |

> ⚠️ wiki 说法**大体正确但有两处偏差**：
> ① 「王国资产」是 `Royalties`（不是 `Abundance`，后者中文是「富足」）；
> ② 名单里还有 `HiddenGem`/`Abundance`/`Nightmare`/`Eidolon`/`Transfigure`/
> `NeowsFury`/`FranticEscape` 等"功能牌"，以及 6 张不可打出的状态牌 ——
> wiki 只列了"局外收益"那 7 张，其余未提。

**本 mod 对齐**：给「修行」`Asceticism`（战斗结束给卡牌奖励）与
「治疗术」`HealingSpell`（回复生命）加 `CanBeGeneratedInCombat => false`。

**受影响的生成源**（都走 `FilterForCombat`）：
- 「暗黑神秘学」`DarkMysticism`（`CardFactory.GetDistinctForCombat`，按 Attack/Skill/Power 各生成 1 张）
- 「封印信」`SealedLetter`（同上，只生成 Power 牌）

排除后池子仍充足：Skill 23→22、Power 11→10（Attack 无变化）。

---

### 6.44 「失忆」：`ModifyHandDraw` 只作用于**回合开始的 5 张**（2026-10-19）

**语义变更**：`Amount` 从"少抽几张（一次性）"改成"还要影响几个回合"，
每回合固定少抽 **1** 张。

**关键事实：`ModifyHandDraw` 全游戏只有一个调用点** ——
`CombatManager.SetupPlayerTurn` 里的
`Hook.ModifyHandDraw(state, player, 5m, out ...)`（`CombatManager.cs:902`），
紧接 `:921` 的 `CardPileCmd.Draw(handDraw)`。
**回合内的"抽 N 张"效果不经过这个钩子** ⇒ "回合开始时少抽"天然成立。

**时序**（行号已核对）：
```
:900  Hook.BeforeHandDraw
:902  Hook.ModifyHandDraw(5m)        ← 削减抽牌数
:903  Hook.AfterModifyingHandDraw
:921  CardPileCmd.Draw(handDraw)
:923  Hook.AfterPlayerTurnStart      ← 扣层（每回合恰一次）
```

⚠️ **扣层不能用 `AfterModifyingHandDraw`**：
`Hook.ModifyHandDraw`（`Hook.cs:1692-1716`）只在
`(int)num2 != (int)num`（数量真的变了）时才把 modifier 收进 `modifiers`；
`Hook.AfterModifyingHandDraw`（`:751-761`）只对 `modifiers` 里的模型回调。
于是"基础抽牌数已经是 0"（如同时中了原版「心灵腐化」）时
`Math.Max(0, 0-1) == 0` 没变 ⇒ 回调不触发 ⇒ **层数永远不减少，Debuff 卡在场**。

⇒ 改用 `AfterPlayerTurnStart`（`AbstractModel.cs:1320`）：在抽牌**之后**、
**每回合恰一次**、与数量是否变化无关。

**扣层方式**：`PowerCmd.Decrement(this)`（`PowerCmd.cs:184-187`），
与「模糊」`BlurPower`、「缓冲」`BufferPower` 同一写法。
`ModifyAmount` 末尾 `if (power.ShouldRemoveDueToAmount()) await Remove(power);`
（`:251-254`），而 `ShouldRemoveDueToAmount`（`PowerModel.cs:478-489`）
对非 `AllowNegative` 的计数型能力在 `Amount <= 0` 时返回 true
⇒ **归零自动移除，不需要手动 `Remove`**。

**IL 核实**（部署后 dll）：
```
ModifyHandDraw:            owner.Player 判定 → Amount>0 判定
                           → Math.Max(Decimal.Zero, count - Decimal.One)   ← 减 1，不是减 Amount
.cctor:                    CardsPerTurn = Decimal.One
<AfterPlayerTurnStart>:    get_Player → get_Amount → Flash → PowerCmd::Decrement
```

> ⚠️ `design.md:173` 仍是旧文案（"你在下个回合少抽1张牌"），
> 已在本轮报告中标注待用户自行更新。

---

### 6.45 战斗内改费：`SetThisCombat` 是"持续维持"的正确 API（2026-10-19）

**问题**：`Myth`「迷思」要把手牌耗能随机成 0–3 并**维持整场战斗**（可被后续效果覆盖）。

**答案**：`card.EnergyCost.SetThisCombat(cost)`（`CardEnergyCost.cs:238-244`），
它往 `_localModifiers` 里加
`LocalCostModifier(cost, Absolute, EndOfCombat)`。

**为什么够"持续"**：
- `GetWithModifiers`（`:109-115`）**按加入顺序**依次应用 `_localModifiers`
- `Absolute` 的语义是"把费用设为该值"（见 `LocalCostModifier` 类注释的分步示例）
  ⇒ **后加入的覆盖先加入的**
- 过期时机是 `EndOfCombat`

⇒ 结果正好是：换了回合仍在；再打一次「迷思」会被新值覆盖；
其它加/减费效果按其自身 modifier 语义参与计算。**不需要任何自定义状态字段。**

⚠️ **X 费牌必须跳过**：`EnergyCost.Canonical < 0` 表示费用为 X
（`ConfusedPower.cs:40-43` 就是这么判的）。给 X 费牌设绝对值会把 X 变成定值。

⚠️ 随机源用 `RunState.Rng.CombatEnergyCosts.NextInt(4)` —— 与「混乱」`ConfusedPower` 同一个流。
视觉反馈也照抄：`NCard.FindOnTable(card)?.PlayRandomizeCostAnim()`。

#### 与「混乱」`ConfusedPower` 的区别（用户明确要求）
| | 混乱（异蛇之眼） | 迷思 |
|---|---|---|
| 触发时机 | `AfterCardDrawn`（**每次抽到**时） | 打出时**遍历当前手牌** |
| 结果 | 之后每抽一次都可能再变 | 本次设定后固定，除非被再次改写 |

---

### 6.46 「每回合一张免费牌」：拆成**载体能力**才能真正在回合开始掉层（2026-10-19 定稿）

**用户方案（优于我最初的两版）**：

1. 新增**载体能力** `FreeThisTurnPower`（「本回合免费」，`Amount` = 本回合还剩几张免费牌）
   - 描述：`本回合你打出的第{Amount}张牌免费。`
   - **每打出一张牌 -1 层**
   - **玩家回合结束必然移除**（`Remove`，不能只靠归零自动移除）
2. `TelepathyPower`（通灵）的 `AfterEnergyReset` 里：
   - 先给玩家施加 **1 层** `FreeThisTurnPower`
   - **然后通灵自身 -1 层**

⇒ 通灵就**必然**每回合开始掉层 —— 当回合的效果已经沉降到独立载体上，
   载体被移除也轮不到通灵，反之亦然。

#### 为什么这个拆分是必须的

「本回合第 1 张牌免费」是**贯穿整个回合**的被动效果，而
`PowerCmd.Decrement` → `ModifyAmount(-1)` 末尾
`if (power.ShouldRemoveDueToAmount()) await Remove(power)`
（`PowerCmd.cs:251-254`），计数型能力 `Amount <= 0` 即移除
（`PowerModel.cs:478-489`）。
因此**同一个能力不可能既"回合开始掉层"又"整回合持续生效"** ——
最后一层掉的瞬间它就消失了。

拆成两个能力后两个诉求各自成立：
| | 通灵 | 载体「本回合免费」 |
|---|---|---|
| 寿命单位 | 回合 | **本回合内** |
| 掉层时机 | 回合开始（`AfterEnergyReset`） | 每打一张牌 |
| 移除时机 | 归零即移除 | **回合结束必然移除** |
| 作用 | 计时器 | 真正提供 0 费 |

#### 结算点：`AfterEnergyReset`（与「整理」一致）

```csharp
// EnergyNextTurnPower.cs:23-31（「整理」TidyUp 用的原版效果）
public override async Task AfterEnergyReset(Player player)
{
    if (player == base.Owner.Player)
    {
        await PlayerCmd.GainEnergy(base.Amount, player);
        await PowerCmd.Remove(this);
    }
}
```
⇒ 回合开始（能量重置那一刻）就是标准结算点。

#### 载体的实现要点

- **免费**：照抄 `VoidFormPower.cs:85-109`，重写
  `TryModifyEnergyCostInCombatLate` + `TryModifyStarCost`，都返回 `0m`。
  只对 `PileType.Hand` / `PileType.Play` 生效（同 `VoidFormPower.ShouldSkip`）。
- **掉层**：`AfterCardPlayed` → `PowerCmd.Decrement(this)`；
  **排除自动打出**（`IsAutoPlay`）与非本人的牌，只看同一次连击的最后一张。
- **回合结束移除**：`BeforeSideTurnEnd`（`participants.Contains(Owner)`）→ `PowerCmd.Remove(this)`。
  ⚠️ **不能只 `Decrement`**：若有没用掉的层数，它会残留到下一个回合，
  变成"跨回合累计"，与"本回合"语义冲突。

#### 时序

```
:898  Hook.AfterEnergyReset     ← 通灵：施加 1 层载体 → 自己 -1 层
:902  Hook.ModifyHandDraw(5m)
:921  CardPileCmd.Draw(...)
:923  Hook.AfterPlayerTurnStart
…出牌…                          ← 载体 TryModify*Cost → AfterCardPlayed 掉 1 层
回合结束                         ← 载体 Remove（无论用没用掉）
```

### 6.47 变化（Transform）的两个前置约束（2026-10-19）

`CardCmd.Transform`（`CardCmd.cs:374-409`）在下面两种情况会**抛异常**：

```csharp
if (!item.Original.IsTransformable)
    throw new InvalidOperationException("Can't transform … because it's un-transformable.");
if (replacement == null)
    throw new InvalidOperationException($"Attempting to transform un-transformable card …");
```

⇒ **调用前必须过滤 `IsTransformable`**（`CardModel.cs` 里：带 `Eternal` 且
在 `Deck` 堆时才是 false；手牌里的牌通常可变化）。
「泛灵论」是"变化**所有**手牌"，不筛会在遇到不可变化牌时整张卡报错。

**RNG 用 `RunState.Rng.CombatCardSelection`**（与 `EntropyPower` 一致）。

**"变化后给新牌上词条"**：`TransformToRandom` 返回 `CardPileAddResult`，
取 `.cardAdded` 才是新牌；顺序必须是"先变化、再 `CardCmd.ApplyKeyword`"。

**遍历前必须 `ToList()` 快照手牌** —— 变化会替换手牌元素。

---

### 6.48 「（保留）」在 design.md 里可以表示"升级才获得该词条"（2026-10-19）

`泛灵论 - 0 - 稀有 - 技能 （保留）抽1张牌。…`
这里的 `（保留）` **不是**升级数值，而是"升级后获得「保留」"。
⇒ `CanonicalKeywords` 留空，在 `OnUpgrade()` 里
`AddKeyword(CardKeyword.Retain)`（与「熵」`Entropy` 升级加「固有」同一写法）。
卡面文案**不写「保留」**（基础版没有该词条）。

> ⚠️ 读 design.md 时注意区分：括号里是**数字**就是升级数值，
> 是**词条名**就是"升级后获得该词条"。

---

### 6.49 「灼伤」（原版状态牌 `Burn`）≠ 本 mod 的「燃烧」减益（2026-10-21）

**命名冲突**：两者英文名**都是 "Burn"**，中文才区分。

| | 中文 | 英文 | 类型 | 代码 |
|---|---|---|---|---|
| 减益 | 燃烧 | Burn | `PowerModel` | `Wizard.Scripts.Powers.BurnPower` |
| 状态牌 | 灼伤 | Burn | `CardModel`（Status） | `MegaCrit.Sts2.Core.Models.Cards.Burn` |

⇒ 写代码时**必须用别名**区分，否则 `Burn` 到底指哪个完全靠猜：
```csharp
using BurnCard = MegaCrit.Sts2.Core.Models.Cards.Burn;
```
⇒ 悬停提示也要分开给（原版「超频」`Overclock` 就是这么做的）：
`HoverTipFactory.FromCard<BurnCard>()` + `HoverTipFactory.FromPower<BurnPower>()`。

**原版 `Burn`（灼伤）的规格**（`Burn.cs`）：
`CardType.Status` / `CardRarity.Status` / 基础费用 **-1** / `CardKeyword.Unplayable` /
`MaxUpgradeLevel => 0` / **回合结束时若在手牌中则受 2 点伤害**。

---

### 6.50 战斗中往牌堆塞牌：`AddGeneratedCardToCombat`（2026-10-21）

「决心」重做后要把 1 张「灼伤」放进**抽牌堆**。照抄原版「超频」`Overclock.cs:31-33`：

```csharp
CardModel burnCard = combatState.CreateCard<BurnCard>(Owner);
CardCmd.PreviewCardPileAdd(
    await CardPileCmd.AddGeneratedCardToCombat(burnCard, PileType.Draw, Owner));
await Cmd.Wait(0.5f);
```

三个要点：
1. **造牌用 `CombatState.CreateCard<T>(Player)`**（`ICombatState.cs:103`）——
   得到属于本次战斗的新实例。
2. **加牌用 `CardPileCmd.AddGeneratedCardToCombat`**（`CardPileCmd.cs:267`），
   **不要**用普通 `Add`：
   - 它会把"生成了这张牌"记进战斗历史；
   - 它会校验"牌不能已经属于某个牌堆"（否则抛 `InvalidOperationException`）；
   - 它要求是**战斗内牌堆**（`newPileType.IsCombatPile()`）。
3. `CardCmd.PreviewCardPileAdd(result)` + `Cmd.Wait(0.5f)` 给玩家看得见的反馈。

⚠️ `CardPilePosition` 枚举 = `{None, Bottom, Top, Random}`；
design.md 只写"加入你的抽牌堆"、未指定顶部 ⇒ 用默认 `Bottom`。

---

### 6.51 「决心」重做时删掉的旧机制（2026-10-21）

旧版「决心」（`给予自身 1 层易伤、1 层脆弱、3 层燃烧`）里有一段
`MakeDropNextTurn`：把 `VulnerablePower` / `FrailPower` 的
`SkipNextDurationTick` 从引擎默认的 `true` 改回 `false`，
让"自己给自己的减益"在下个自己回合开始就掉层。

**新版去掉了易伤与脆弱 ⇒ 这段整段删除。**
燃烧 `BurnPower` 不是 duration 型能力（`StackType.Counter`，回合开始结算伤害但**不衰减**），
不吃 `TickDownDuration` 那套规则，所以**不需要任何掉层修正**。

> 📌 保留这条记录是为了避免以后有人"重新实现"时把那段 hack 一起搬回来 ——
> 它只对 `Vulnerable`/`Frail`/`Weak` 这类 duration 能力有意义。

---

### 6.52 「全知形态」的"本回合"记忆改为**敌我双方回合开始都重置**（2026-10-21）

**旧行为**：只在**玩家**回合开始清空 `_appliedThisTurn`
（`side == Player && participants.Contains(Owner)`）。
⇒ "本回合"只覆盖玩家自己的回合，**敌人回合里施加的负面效果会被算作"已给过"**。

**问题场景**（用户提出）：
「冰之城墙」<see cref="FrostWallPower"/> 是"本回合敌人每攻击一次，
就给敌人 1 层冻结 + 2 层寒冷"—— 它的施加**发生在敌人回合**（敌人攻击时）。
若玩家在自己回合已经给过寒冷，敌人回合这次就不会触发「全知形态」的抽牌。

**新行为**：`AfterSideTurnStart` 里**无条件** `_appliedThisTurn.Clear()`。
"本回合" = **当前进行中的那一方的回合**。

⇒ 敌人回合开始会重新清空一次，于是敌人回合中途（被攻击后）施加的寒冷/冻结算作
"本回合第一次" ⇒ 照常给能量与抽牌。

#### ⚠️ 两个易错点

1. **不能保留 `participants.Contains(Owner)` 这半个条件**：
   `participants` 是"**正在开始回合的那一方**的生物"，
   敌人回合时里面全是敌人，永远不含玩家 ⇒ 保留它等于敌人回合仍不重置。
2. **可以无条件清空**：`Hook.AfterSideTurnStart`（`Hook.cs`）遍历
   **全部**战斗监听者（不分阵营），而本方法只在战斗中触发，
   清空一个纯内存 `HashSet` 没有任何副作用。

> 📌 **「熔火审判」`PyroJudgmentPower` 不要跟着改**：
> 它的 `AfterSideTurnStart(side = Player)` 是"每回合把应施加的燃烧层数 +1"，
> 属于**自身成长节奏**，与"本回合已给过哪些种类"的记忆无关。

**卡面文案无需改动**：能力文案是"每当你给予敌人**本回合**未给予过的一种负面效果时…"，
"本回合"在新口径下依然成立（只是从"玩家回合"扩展为"当前方回合"）。

**IL 核实**（部署后 dll）：
```
OmniscientFormPower::AfterSideTurnStart:
    ldarg.0
    ldfld  HashSet`1<ModelId> _appliedThisTurn
    callvirt HashSet`1::Clear()
    call   Task::get_CompletedTask()
```
—— 没有任何 `get_side` / `Contains` 调用。

---

### 6.53 build.ps1 的导入噪音过滤补一条 `EditorSettings`（2026-10-21）

`build.ps1` 会扫描 Godot `--import` 日志里以 `ERROR/Failed` 开头的行，
按 `$expectedMissing`（原版资源缺失，如 `res://src/`、`res://addons/mega_text/`）
与 `$expectedNoise`（headless 噪音）过滤。

本次构建冒出一条此前没见过的：

```
ERROR: Condition "!EditorSettings::get_singleton() || !EditorSettings::get_singleton()->has_setting(p_setting)" is true. Returning: Variant()
```

这是 **headless 模式下没有 `EditorSettings` 单例**导致的编辑器侧噪音，
与工程内容无关（pck 仍正常生成）。已在 `$expectedNoise` 补
`'EditorSettings::get_singleton'`，重新构建后恢复输出"资源导入完成"。

---

## 10.5 卡面文案的 `[blue]` 只用于「雷电伤害 / 寒冰伤害」（2026-10-21 用户纠正）

**用户原话**：「无论中英文，卡面文本本地化中的数字都不需要使用 `[blue]` 修饰，
用 `[blue]` 修饰的关键字只有"雷电伤害""寒冰伤害"。
用 `[blue]` 修饰数字是**悬停 buff 标识变量**的习惯，不要扩延到卡面文本。」

| 位置 | 数字要不要 `[blue]` |
|---|---|
| `localization/*/cards.json`（**卡面**） | ❌ **不要** |
| `localization/*/powers.json`（**悬停 / 能力文案**） | ✅ 要（那是标识变量的习惯） |

卡面里保留 `[blue]` 的只有两个**关键字**：
`[blue]雷电伤害[/blue]` / `[blue]寒冰伤害[/blue]`（英文 `[blue]Lightning damage[/blue]` / `[blue]Ice damage[/blue]`）。

> ⚠️ 我在 v0.3.5 及之前写过 `[blue]1[/blue]张手牌`、`[blue]2[/blue]层` 这类，
> 已由用户于 2026-10-21 手工全部修正（`cards.json` 中现为 0 处）。
> **以后的卡面文案：数字直接写，不加任何颜色标记。**

核查方法（应输出 0）：
```python
re.findall(r"\[blue\]\d", cards_json_text)
```

---

## 10.6 改工坊描述时，断言别硬编码引号样式（2026-10-21）

给 v0.3.6 写描述脚本时，一条"原有修复仍在"的断言写成
`"修复了遗物“铅咒板”" in desc` 而**失败** —— 实际文案用的是**直角引号**
`修复了遗物「铅咒板」在给予「燃烧」效果时结算异常的问题。`。

真正的安全性来自**行级多重集合比对**（`Counter(old) - Counter(new)` 必须为空、
`Counter(new) - Counter(old)` 必须恰等于本次要加的行）—— 那条断言当时是 **OK** 的，
证明没有任何行被删改。硬编码引号只是我自己写错的无关断言。

> 📌 经验：**内容正确性靠多重集合比对**；关键字断言只用来抽查语义，
> 别把不确定的**标点样式**写死进断言（中文弯引号 / 直角引号 / 全角括号都可能）。

---

## 11. 线上仓库（2026-10-21 建立）

**https://github.com/Traumere7/sts2mod-wizard**（**public**，默认分支 `main`）

首个提交 `9a310fb`「chore: 初始存档（v0.3.6）」，**607 个文件**
（src 594 / docs 10 / build.ps1 / design.md / .gitignore）。

### 11.1 未纳入版本库的内容

| 路径 | 原因 |
|---|---|
| `art/` `logs/` `reference/` | 用户指定排除（reference 有 3.2 GB） |
| `build/` | 内含 `Wizard.pck` **~106 MB**，**超过 GitHub 单文件 100 MB 硬上限**，推送必失败 |
| `src/.godot/` | Godot 导入缓存（~46 MB），可完全重新生成 |
| `*.pck` | 打包产物 |

### 11.2 ⚠️ 仓库级 `core.autocrlf=false` 是刻意设的

系统级 gitconfig（`C:/Program Files/Git/etc/gitconfig`）设了 `core.autocrlf=true`，
会把换行统一转换。而本工程**换行本来就混用**：
`.cs`/`.json`/`.md`(docs 里新建的) 是 LF，`build.ps1`/`design.md` 是 CRLF。
开着 autocrlf 会导致**每次 checkout 后一堆文件显示"已修改"**（假改动）。

⇒ 已设 `git config --local core.autocrlf false`，**存档保字节原样**。
验证方法：`git ls-files --eol <file>`，应满足 `i/…` 与 `w/…` 相同。

### 11.3 推送工具链

`gh` CLI 已登录（账号 **Traumere7**，token scope 含 `repo`），
走 HTTPS 推送，**不需要 SSH key、也不需要输入密码**。
仓库创建命令：`gh repo create sts2mod-wizard --private --source . --remote origin --push`

> 📌 根目录**没有 README.md** —— 所有原根目录文档（含 `README.md`）都已收进 `docs/`。
> 用户原本的说法是"readme 以外收进 docs"，若希望 README 回到根目录，把它移出来即可。

### 11.4 目录调整（2026-10-21 用户操作）

· `design.md` 移入 `docs/`（内容权威来源不变，路径变为 `docs/design.md`）
· `README.md` 放在**仓库根目录**（GitHub 首页渲染），已按项目现状重写

### 11.5 构建产物走 GitHub Release 附件（二进制不进仓库）

| | git 仓库 | Release 附件 |
|---|---|---|
| 单文件上限 | **100 MB**（硬限制，超了推送直接失败） | **2 GiB** |
| 克隆成本 | 计入 `git clone` | 不计入 |

⇒ `Wizard.pck`（~106 MB）**只能**作为 Release 附件分发。

> 📌 **发版刻意不自动化**（用户 2026-10-21 明确否决把它加进 `build.ps1`）：
> 代码通常要迭代好几次才打包发布一次，发版是**独立的一环**，
> 只由人判断时机后手动执行。**不要再提议给 `build.ps1` 加 `-Release` 之类的参数。**
**发布流程**（每次发版）：
1. 构建并部署（`build.ps1` 会自动提交源码）
2. 从 `…\Slay the Spire 2\mods\Wizard\` 取 `Wizard.dll` / `Wizard.json` / `Wizard.pck`
3. 打包：`Compress-Archive -Path <三件> -DestinationPath Wizard-v<ver>.zip`
4. 说明写到 `docs/releases/v<ver>.md`
5. `gh release create v<ver> Wizard-v<ver>.zip --title "v<ver>" --notes-file docs/releases/v<ver>.md`

⚠️ `gh release create --target <sha>` 会报 `Release.target_commitish is invalid` ——
**省略 `--target`** 即可（默认取默认分支当前提交）。

首个 release：**v0.3.6**，附件 `Wizard-v0.3.6.zip`（104.7 MB），
dll `BBCF409E` / json `0EA48CCB` / pck `12A4D7C7`。
已实测：下载回读 → 解压 → 三个文件 MD5 与部署目录完全一致。

### 11.6 build.ps1 第 5 步：自动提交

构建完成后自动 `git add -A` → `commit` → `push`：
· 参数 `-NoCommit` 可跳过
· 工作区干净则跳过
· 不是 git 仓库则跳过
· **commit/push 失败只打警告，绝不让构建失败**（`$ErrorActionPreference` 临时降为 Continue），
  因为 `$ErrorActionPreference = 'Stop'` 会把 git 的 stderr 包装成终止性错误
· 提交信息格式：`build: v<版本>（dll <8位> / pck <8位>）` + 变更清单

⚠️ **每次构建 dll/pck 的哈希都会变**（编译与打包含时间戳），
所以「工坊上那份是旧版本」的提示在没发版的构建里必然出现，属正常现象。

### 11.7 ⚠️ HTTPS 推送被网络阻断 ⇒ remote 改用 SSH（2026-10-21 实测）

构建时自动提交后推送失败，报错：
```
fatal: unable to access 'https://github.com/...':
  TLS connect error: error:0A000126:SSL routines::unexpected eof while reading
```
**排查结论**：
| 检查 | 结果 |
|---|---|
| `gh api`（Go 的 TLS 栈） | ✅ 正常 |
| `Invoke-WebRequest https://github.com` | ✅ HTTP 200 |
| `git push` HTTPS（OpenSSL） | ❌ TLS connect error |
| `git push` HTTPS（`http.sslBackend=schannel`） | ❌ schannel: failed to receive handshake |
| `ssh -T git@github.com` | ✅ `Hi Traumere7!` |

⇒ 是**网络层对 git 的 HTTPS 连接**做了干扰（其他客户端的 TLS 都正常），
不是 git 配置问题，重试 3 次均失败。**SSH key 已注册在 GitHub 上，可用**。

**解决办法**：
```powershell
git remote set-url origin git@github.com:Traumere7/sts2mod-wizard.git
git push --set-upstream origin main      # 已设为跟踪分支
```
已把 remote 永久改成 SSH。`build.ps1` 的推送失败提示里也加了这个兜底提示。

> 📌 以后若再遇到 `git push` 失败，**先试 SSH**，不要浪费时间调 `http.version` / `sslBackend`。

### 11.8 发版记录

| 版本 | 日期 | 主要改动 | 附件 |
|---|---|---|---|
| v0.3.6 | 2026-10-21 | 「决心」重做；「全知形态」记忆在敌人回合也重置 | `Wizard-v0.3.6.zip` |
| v0.3.7 | 2026-10-21 | 「净罪」每张给 2 点法术增益 + 2 点敏捷；升级改为降费 1→0 | `Wizard-v0.3.7.zip` |

每个版本的说明归档在 `docs/releases/v<版本>.md`（同时用作 `gh release create --notes-file`）。

#### 📌 附件校验：用 API 的 `digest` 代替“下载回来比对”

本机网络对 github 的 HTTPS **大文件下载**会中途断
（`wsarecv: An established connection was aborted by the software in your host machine`），
104 MB 的附件经常只下到 20 MB 左右。

⇒ 更稳的校验方式（**无需下载**）：

```powershell
gh api repos/<owner>/<repo>/releases/tags/v<ver> --jq '.assets[] | "\(.name) \(.size) \(.digest)"'
Get-FileHash <本地 zip> -Algorithm SHA256
```

GitHub 会返回附件的 `sha256:<hex>`；与本地哈希 + **精确字节数**一并比对，
即可证明远端与本地字节完全一致。v0.3.7 实测一致：
`sha256:0465552384…f036`，109,753,922 B。

> 小文件（如通过 `gh api .../contents/...` 取源码）不受影响，只有大附件下载会断。

---

## 12. 怪物系统（2026-10-22 首次实现：火山巨兽 + 火山贤者）

本项目第一个怪物与第一个"事件内战斗"。以下结论都经实测/IL 核实。

### 12.1 三件套：Monster / Encounter / Event

| 层 | BaseLib 基类 | 关键重写 |
|---|---|---|
| 怪物 | `CustomMonsterModel` | `MinInitialHp` / `MaxInitialHp` / `CreateCustomVisuals()` / `AfterAddedToRoom()` / `GenerateMoveStateMachine()` |
| 遭遇战 | `CustomEncounterModel` | `AllPossibleMonsters` / `IsValidForAct` / `GenerateMonsters()` / `ShouldGiveRewards` |
| 事件 | `CustomEventModel`（继承游戏 `EventModel`） | `GenerateInitialOptions()` / `IsShared` / `IsAllowed` |

注册是**自动**的：`CustomMonsterModel` / `CustomEncounterModel` / `CustomEventModel`
的构造函数都会往 `CustomContentDictionary` 注册，而
`Entry.Initialize()` 里的 `ScriptManagerBridge.LookupScriptsInAssembly` 负责实例化扫描到它们。
**不需要任何 `[Pool]` 之类的特性。**

### 12.2 单贴图怪物：场景与贴图尺寸

怪物视觉是一个 `.tscn`，**四个节点名必须完全一致且勾选 Access as Unique Name**：

```
VolcanicBeast (Node2D)
├── Visuals    Sprite2D     ← 怪物本体贴图
├── Bounds     Control      ← 命中框（宽度参与多怪排布：Sum(Bounds.Size.X)）
├── CenterPos  Marker2D     ← 血条/特效锚点
└── IntentPos  Marker2D     ← 意图图标位置
```

`CreateCustomVisuals()` 用 `NodeFactory<NCreatureVisuals>.CreateFromScene(path)`
（若场景自带脚本则改用 `CustomVisualPath`）。

⚠️ **`.tscn` 里不要写 `;` 注释** —— 原版 126 个怪物场景里一个都没有，
`load_steps` / `format` 之外的注释没有可靠先例，不值得冒险。

#### 贴图该多大（用户问过）

- 游戏基准分辨率 **1920×1080**（`project.godot` 的 `window/size/viewport_*`），
  且 `stretch/mode = canvas_items`
- `Sprite2D` **默认 1:1 按贴图像素绘制**（不缩放）
- 原版 **123 个怪物场景**的 `Bounds` 实测：
  - 高度 **中位 300 px**、25% 分位 201、75% 分位 367、范围 94~642
  - 宽度中位 274 px

⇒ **只给一张贴图时，直接画成"最终屏幕尺寸"最省事**：
**512×512 PNG**，怪物主体占画面 ~70–80%（≈360–400 px 高），四周留透明边，
场景里 `scale = 1` 即可 —— 本项目的占位贴图就是这么做的。

若要高分辨率源图：**1024×1024 + 场景里 `scale ≈ 0.4`**
（原版 `the_spear` 就是 1000×1000 @ scale 0.2755）。

Godot 4 不强制 2 的幂，但用 512/1024 对 mipmap 与压缩更友好。

### 12.3 怪物 AI：意图 + 状态机

```csharp
MoveState claw = new("FLAME_CLAW", FlameClawMove,
    new MultiAttackIntent(3, 4),   // 3 伤 × 4 段
    new DebuffIntent());
claw.FollowUpState = smash;
return new MonsterMoveStateMachine([claw, smash, eruption], claw);
```

- 意图构造：`SingleAttackIntent(int)`、`MultiAttackIntent(int dmg, int repeat)`、
  `DebuffIntent(bool strong=false)`、`BuffIntent()`、`StatusIntent(int count)`
- 招式方法签名固定 `async Task XxxMove(IReadOnlyList<Creature> targets)`
- 招式名进 `monsters.json` 的 `{ENTRY}.moves.{MOVE_ID}.title`

**动态数值要现读**：本项目的「喷火」层数决定给玩家多少燃烧，
出招时用 `Creature.GetPowerAmount<FireSpitPower>()`（`Creature.cs:596`），
不要缓存 —— 第 3 招会把它 +2。

### 12.4 战斗内塞状态牌进弃牌堆

照抄原版「超频」`Overclock`：

```csharp
CardModel burn = CombatState.CreateCard<Burn>(player);
await CardPileCmd.AddGeneratedCardToCombat(burn, PileType.Discard, player);
```

### 12.5 事件内触发战斗

```csharp
public override bool IsShared => true;              // ⚠️ 必须
EnterCombatWithoutExitingEvent<VolcanicBeastEncounter>(extraRewards, shouldResumeAfterCombat: false);
```

- ⚠️ **`IsShared` 必须为 true**：`EventModel.cs:573-576` 在非共享事件里直接抛
  `InvalidOperationException`。design.md 也写了"多人合作：是"。
- ⚠️ **不要把 `LayoutType` 设成 `EventLayoutType.Combat`**：
  `EventModel.cs:577-580` 禁止"Combat 布局 + 战后返回"，
  而本事件前面还有 3 个选项要显示。保持默认布局。
- 「后续文案 → 战斗按钮 → 进战斗」两步结构照抄 `DenseVegetation`：
  `SetEventState(文案, [Option(Fight, ...)])`。
- 关掉遭遇战自带奖励（`ShouldGiveRewards => false`），全部奖励走 `extraRewards`，
  否则会多出一份精英奖励。

### 12.6 ⚠️ `RelicReward(RelicModel, Player)` 必须传可变实例（2026-10-22 实测崩过）

```csharp
// ❌ 抛 CanonicalModelException: Canonical model of type TriaPrima used in incorrect place.
new RelicReward(ModelDb.Relic<TriaPrima>(), player);

// ✅
new RelicReward(ModelDb.Relic<TriaPrima>().ToMutable(), player);
```

`RelicReward..ctor(RelicModel, Player)` 内部会 `AssertMutable()`。
同一坑对 `RelicCmd.Obtain(...)` 也成立（`CONTEXT` 里「规则怪谈」已经是 `.ToMutable()`）。

> 判据：**凡把 `ModelDb.Xxx<T>()` 的结果交给"运行期数据"接口，先 `.ToMutable()`。**
> 但 `Monster<T>()` 在 `AllPossibleMonsters` 里**不要** ToMutable（那是静态声明），
> 只在 `GenerateMonsters()` 里 ToMutable —— 两者容易看反。

### 12.7 事件/怪物的本地化文件

新增两个此前不存在的文件（与 `cards.json` 同级）：

```
localization/{zhs,eng}/monsters.json      {ENTRY}.name / .moves.{ID}.title / .moves.{ID}.banter
localization/{zhs,eng}/encounters.json    {ENTRY}.title / {ENTRY}.loss
```

`{ENTRY}.loss` 是**死因文案**，`{character}` 由引擎注入。
事件仍在 `events.json`，键形如
`{ENTRY}.title` / `.pages.INITIAL.description` / `.pages.INITIAL.options.{OPT}.{title|description}`
/ `.pages.{OPT}.description` / `.loss`。

### 12.9 ⚠️ `ShouldGiveRewards = false` 会把事件的 `extraRewards` 一起吃掉

**现象**：事件战斗打赢后**什么都不弹、直接回地图**。

**根因**（`RewardsCmd.cs:20-36`）：

```csharp
if (encounter != null && !encounter.ShouldGiveRewards)
    rewardsSet = new RewardsSet(player).EmptyForRoom(room);   // 空集合
else
    rewardsSet = new RewardsSet(player).WithRewardsFromRoom(room);
```

而 `EnterCombatWithoutExitingEvent` 的 `extraRewards` 是挂在
`CombatRoom.ExtraRewards` 上、**只由 `WithRewardsFromRoom` 合并**的
（`RewardsSet.cs:99-102`、`EventCombatSynchronizer.cs:154-160`）。
⇒ 关掉 `ShouldGiveRewards` 等于**连自定义奖励一起关掉**。

**正确做法**：`ShouldGiveRewards` 保持默认 `true`，
改用下面的手段剔除不想要的默认奖励。

### 12.10 事件战斗的默认奖励怎么"精确控制"

`RewardsSet.GenerateRewardsFor`（`:206`）按 `RoomType` 决定默认奖励：

| RoomType | 默认奖励 |
|---|---|
| Monster | 金币（**仅当 `GoldProportion > 0`**）+ 药水判定 + 卡牌（普通权重） |
| Elite | 金币（**无条件**）+ 药水判定 + 卡牌（精英权重）+ **随机遗物** |
| Boss | 金币 + 药水判定 + 卡牌 |

⇒ 想要"事件精确给、不要默认遗物"时：
- 用 **`RoomType.Monster`**（没有默认遗物；卡牌是普通权重，正好对上
  design 的"一次普通卡牌奖励"）
- 重写 **`CalculateGoldProportion(CombatState) => 0f`** 抑制默认金币
  （只有 Monster 分支检查这个值，`:225`；原版 `GremlinMercNormal` 用过同一钩子）

⚠️ **药水判定无法按遭遇战关闭**（用的是玩家全局 `PlayerOdds.PotionReward`），
普通战斗本来就有，属于可接受残留。

### 12.11 ⚠️ 怪物视觉必须**同时**实现两个钩子

BaseLib 对 `MonsterModel` 有**两个** Harmony 补丁：

| 补丁 | 目标 | 读的自定义成员 |
|---|---|---|
| `BaseLib.Abstracts.CreateVisuals` | `MonsterModel.CreateVisuals` | `CreateCustomVisuals()` |
| `BaseLib.Abstracts.VisualsPath` | `MonsterModel.VisualsPath` | `CustomVisualPath` |

只实现 `CreateCustomVisuals()` 时，**战斗内**能正常显示，
但预加载/图鉴走 `VisualsPath` 会回落成默认路径
`res://scenes/creature_visuals/{modid}-{entry}.tscn`，日志里刷
`ERROR: Failed to load resource synchronously: ...`。

⇒ **两个都指到同一个 tscn**：
```csharp
public override string? CustomVisualPath => VisualsScenePath;
public override NCreatureVisuals? CreateCustomVisuals() =>
    NodeFactory<NCreatureVisuals>.CreateFromScene(VisualsScenePath);
```

### 12.12 ⚠️ `PowerModel.Owner` 的 getter 会 `AssertMutable()` —— 不能靠它切文案

**需求**：同一个「燃烧免疫」挂在玩家身上要写"你"，挂在怪物身上要写"火山巨兽"。

**失败的做法**：重写 `Title` / `Description`，在里面判 `Owner.Player == null`。
运行时直接崩：

```
CanonicalModelException: Canonical model of type BurnImmunityPower used in incorrect place
  at PowerModel.get_Owner()          ← ★ Owner 的 getter 第一行就是 AssertMutable()
  at BurnImmunityPower.get_Description()
  at PowerModel.GetDumbHoverTip(...)
  at HoverTipFactory.FromPower[BurnImmunityPower]()
  at Pyromaniac.get_ExtraHoverTips()  ← 卡牌悬停读的是 canonical 实例
```

**可用的做法**：**开子类**（本项目用 `MonsterBurnImmunityPower : BurnImmunityPower`）。

| 关注点 | 为什么子类可行 |
|---|---|
| 效果复用 | 全部继承，一行逻辑都不用重写 |
| 拦截仍然生效 | `SpellCmd.cs:63` 判的是 `target.HasPower<BurnImmunityPower>()`，而 `Creature.HasPower<T>` 是 `_powers.Any(p => p is T)` ⇒ **子类实例命中** |
| 文案自动独立 | `PowerModel.Description` 用 `Id.Entry + ".description"` 取键，子类有自己的 Entry ⇒ 天然另一套键，无需任何重写 |

> 📌 若确实要"同一个类两种文案"，只能用 `AbstractModel.IsMutable` 挡一层
> （canonical 时 `IsMutable == false`，不要去碰 `Owner`），但子类方案更干净。

### 12.13 怪物往玩家牌堆塞牌要有"演出"

裸 `AddGeneratedCardToCombat` 塞进去了但**玩家看不到**。参考原版：

```csharp
// 「史莱姆狂战士」SlimedBerserker.cs:70
await CardPileCmd.AddToCombatAndPreview<Slimed>(targets, PileType.Discard, 10, null);
```

`AddToCombatAndPreview`（`CardPileCmd.cs:1214-1252`）内部会逐张
`AddGeneratedCardToCombat` 后调 `CardCmd.PreviewCardPileAdd` + `Cmd.Wait(1f)`，
即"牌飞进牌堆 + 停留展示"的完整演出。
（「无厌沙虫」`TheInsatiable.cs:133-146` 是同一套做法的多目标手写版。）

### 12.14 事件选项"只显示一行"的写法

参考原版「灯火钥匙」`THE_LANTERN_KEY.pages.KEEP_THE_KEY.options.FIGHT`：

- eng：`.title = 'Fight'`、`.description = ''`（**空串**）
- zhs：只有 `.title = '战斗'`，**没有** description 键

`NEventOptionButton._Ready` 的渲染是
`[{gold|red}][b]{title}[/b][/…]\n{description}` ——
**description 为空 ⇒ 只剩一行**（标题本身已被引擎上成金色加粗）。

> 本项目在此基础上把标题也写成 `[gold]战斗[/gold]`（用户明确要求带 gold 修饰），
> 效果等同于引擎自动着色，多余但无害。
