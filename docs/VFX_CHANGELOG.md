# 原版演出接入清单（v0.4.6-dev，2026-11-03）

> 本次把 `docs/VFX_SOURCES.md` 里建议的演出**全部接上**了。
> 每一条都是独立改动，**编号（F01/L01/A01…）用于回退**：
> 实测后觉得哪条不好，直接说编号（例如"L02 和 A04 不要"），我就把那一处改回旧版。
>
> 共同点：画面/音效全部借用**原版游戏**已有资源，没有新增任何美术或音频文件。
> 演出失败只写一条 `演出：…` 日志，**不会**影响卡牌结算。

---

## 一览表

| 编号 | 卡牌 | 旧版（当前） | 新版（本次） |
|---|---|---|---|
| **F01** | 炽焰术、清净世界、弹射（火段）、火球术、火苗、闪爆、引燃、落日 | `vfx/vfx_molten_fist`（只有画面，音效是默认攻击音） | 同样画面 **+ 火焰音效** `event:/sfx/characters/attack_fire` |
| **F02** | 引燃 | 只有熔岩拳命中 | 追加：**目标脚下地火**（原版「燃烧」`NGroundFireVfx`） |
| **F03** | 燃烧之魂 | 无表现 | 追加：**自身燃烧光环**（原版「火焰屏障」`NFireBurningVfx`） |
| **F04** | 火刑 | 无表现 | 追加：**自身燃烧光环**（同上） |
| **F05** | 纵火狂 | 无表现 | 追加：**自身脚下地火**（`NGroundFireVfx`） |
| **L01** | 电磁波 | 星光冲击单发 | 追加：**横向扫射光束**贯穿全场（原版「横扫光束」`NSweepingBeamVfx`）+ 每个敌人命中点 |
| **L02** | 雷光 | 闪电球激发的打击特效 + 音效 | 追加：**施法者→目标的粗光束**（原版「超能光束」`NHyperbeamVfx` + 命中爆点），原闪电打击保留 |
| **I01** | 冰铠 | 无表现 | 追加：**身上一层冰壳**（`NLiquidOverlayVfx` 染冰蓝 `#8fd8ff`） |
| **I02** | 冰之城墙 | 无表现 | 追加：**冰蓝尖刺飞溅**（`NSpikeSplashVfx(Cyan)`） |
| **I03** | 冰矢 | 星光冲击命中 | 追加：**冰蓝细切痕**（`NThinSliceVfx(Cyan)`，原版「斩击」用的那个） |
| **A01** | 天火浩荡 | 只有末日降临大飞弹 | 追加：**整屏横向扫描线**（原版「大风车」`NHorizontalLinesVfx`，橙红） |
| **A02** | 真理派对 | 无表现 | 追加：**金色整屏扫描线**（同上，金色） |
| **A03** | 暗黑神秘学 | 无表现 | 追加：**整屏紫色烟雾暗角**（原版「噩梦」`NSmokyVignetteVfx`） |
| **A04** | 失忆 | 无表现 | 追加：**屏幕里伸出大量手**（原版「噩梦」`NNightmareHandsVfx`） |
| **A05** | 杰作 | 无表现 | 追加：**盛大收束光效**（原版「终幕」`NGrandFinaleVfx`，**带约 1 秒前摇**，会稍微推迟后续结算） |
| **P01** | 击穿 | `vfx/vfx_starry_impact` | **换成**夸张刺击 `vfx/vfx_dramatic_stab`（原版「背刺」「暗杀」） |
| **P02** | 巨焱拳 | `vfx/vfx_molten_fist` | **换成**重型钝击 `vfx/vfx_heavy_blunt` + 火焰音效 |

> 之前已经接入、这次**没有改动**的：落日 / 天火浩荡的「末日降临」大飞弹；雷光 / 降雷的闪电球激发特效+音效（含降雷逐目标）。

---

## 怎么测

随便开一局，把这 17 条对应的牌打出来看。建议一次测 3~5 张，记下编号。

**如果某个效果没出现**，看日志里有没有这一行（打开
`%APPDATA%\SlayTheSpire2\logs\godot.log` 搜索 `演出：`）：
```
[WARN] [Wizard] 演出：NGroundFireVfx 创建失败（资源可能没加载上）
[WARN] [Wizard] 演出：NGroundFireVfx 找不到战斗特效层，跳过
```
把日志行发我，我就能定位是"资源没加载"还是"节点挂错层"。

---

## 代码位置（回退用）

| 编号 | 文件:行 |
|---|---|
| F01 | `BlazingFlame.cs:60`、`ClearWorld.cs:121`、`DoubleRicochet.cs:141`、`Fireball.cs:84`、`FlameSpark.cs:54`、`Flashburst.cs:81`、`Ignite.cs:149`、`Sunset.cs:144`（另 `BlazingPunch.cs:157` 见 P02） |
| F02 | `Ignite.cs:153` |
| F03 | `BurningSoul.cs:64` |
| F04 | `PyroJudgment.cs:80` |
| F05 | `Pyromaniac.cs:87` |
| L01 | `ElectromagneticWave.cs:104` |
| L02 | `LightningBolt.cs:61` |
| I01 | `IceArmor.cs:99` |
| I02 | `IceWall.cs:57` |
| I03 | `IceArrow.cs:97` |
| A01 | `SkyfireRampage.cs:143` |
| A02 | `TruthParty.cs:91` |
| A03 | `DarkMysticism.cs:77` |
| A04 | `Amnesia.cs:93` |
| A05 | `MagnumOpus.cs:103` |
| P01 | `Puncture.cs:91` |
| P02 | `BlazingPunch.cs:157` |

所有效果方法集中在 `src/Scripts/Utils/WizardVfx.cs`，每条注释都写明了**原版出处**。

---

## 已知风险（测的时候留意）

| 项 | 风险 | 表现 |
|---|---|---|
| **A05** 杰作 | `NGrandFinaleVfx` 有前摇，我 `await` 了 `totalAnticipationDuration` | 打出去后约 1 秒才继续结算（抽牌/能量） |
| **A03/A04** | 挂的是**全局 UI 层**（`NRun.GlobalUi`） | 若与 B 版美术色系冲突、或盖住 UI，请告诉我；多人时也只在本地播 |
| **F03/F04** `NFireBurningVfx` | 原版是"火焰屏障期间常驻"，我们只播一次 | 可能只闪一下就没了（属正常，不是 bug） |
| **L02** 雷光 | 光束 + 原闪电打击**同时**存在 | 可能觉得"太满"，可只保留一个 |
| **I01** `NLiquidOverlayVfx` | 原版是毒液瓶的"覆盖层" | 蓝色可能偏"粘液"而不像冰 |
| **A01/A02** 整屏扫描线 | 全屏层 | 可能挡视线或太吵 |

---

## 第二轮修订（2026-11-03 晚，依据实测）

| 编号 | 处理 | 说明 |
|---|---|---|
| **F04** 火刑 | **修改** | 去掉卡面的"自身燃烧光环"（理由：不该烧自己）；改成**每回合结算时给每个敌人脚下地火**（`NGroundFireVfx`，在 `PyroJudgmentPower.AfterSideTurnStart` 的施加循环里） |
| **L01** 电磁波 | **回退** | 横扫光束是从眼睛射出的，太夸张、只适配原版机器人 |
| **L02** 雷光 | **回退** | 同上；只保留电球激发的打击特效+音效 |
| **I01** 冰铠 | **修改** | 原来是 `NLiquidOverlayVfx`，**实测完全没效果** —— 见下方"踩坑记录"；改成**脚下蓝色寒气圈**（`NGroundFireVfx` + `VfxColor.Blue`） |
| **A02** 真理派对 | **回退** | 不需要 |
| **A04** 失忆 | **回退** | 不需要 |
| **A05** 杰作 | **修改** | `NGrandFinaleVfx`（带前摇）不合适；改成**与 A03 相同的紫色烟雾暗角** |
| **P02** 巨焱拳 | **回退** | 不合适，恢复 `vfx/vfx_molten_fist` |
| I02 冰之城墙 | 微调 | `Cyan` → `Blue`（`NGroundFireVfx` 的 switch 对 Cyan 直接 `throw`，统一用映射好的 Blue） |

### 踩坑记录：`NLiquidOverlayVfx` 对自绘立绘无效

```csharp
// NLiquidOverlayVfx.PlayVfx()
(NCombatRoom.Instance?.GetCreatureNode(_targetCreature))?.Visuals.TryApplyLiquidOverlay(_tint);
```
它只是去调**角色立绘上的"液体槽"**（原版 spine 才有的东西）。
巫师是自绘立绘，那个槽不存在 ⇒ 整段**静默无效**，节点自己 0.5 秒后 `QueueFree`，
所以日志里连错误都没有。以后凡是 `CreatureNode.Visuals.*` 系列（液体覆盖、染色等）
都不能直接用在巫师身上。

### 防呆

`WizardVfx.GroundFire` 加了一层颜色白名单（Red/Green/Blue/Purple/Black/White），
不认识的颜色一律回落 Blue —— 因为 `NGroundFireVfx.ApplyColor` 的 `default` 分支是
`throw new ArgumentOutOfRangeException()`，传 Cyan 会炸在 `_Ready` 里。

### 当前仍然生效的条目

F01、F02、F03、F05、I01（新方案）、I02、I03、A01、A03、A05（新方案）、P01
（加上之前就有的：落日/天火浩荡的大飞弹、雷光/降雷的电球激发特效+音效）
