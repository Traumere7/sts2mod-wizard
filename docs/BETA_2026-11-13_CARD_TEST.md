# 2026-11-13 public-beta 更新 —— **卡牌受影响清单与复测建议**

> 配套报告：`docs/BETA_2026-11-13_COMPAT.md`（整体兼容性）与 `CONTEXT.md` §32。
> 结论：**75 张卡里，A 级（必须优先复测）23 张、B 级（顺路复测）49 张、C 级（纯数值、可不测）3 张。**

## 0. 判断依据（已完成的静态核查）

| 检查项 | 结果 |
|---|---|
| 39 个我们重写的钩子/接口方法 | 编译通过 ⇒ 签名都在 |
| `Hook.cs` 中与我们同名的 18 个钩子方法体 | **逐字节一致** |
| `ModifyDamage` / `HoverTips` / `Keywords` / `PowerCmd.Apply` / `CombatHistory.CardGenerated` | **一致** |
| `VigorPower` 私有结构（我们的活力补丁依赖） | **一致** |
| `CardPileCmd.Draw` / `AddGeneratedCardToCombat` / `Add` | 一致（`Add` 仅内部 tween 枚举写法变化） |
| `CardCmd.TransformToRandom` / `CardSelectCmd.FromCombatPile` / `CreatureCmd.GainBlock` / `PlayerCmd.GainEnergy` | **一致** |
| `CardSelectCmd.FromHand` | **重构**（改用 `CardSelectorStack`、玩家死亡早退）⇒ 选牌类卡要复测 |
| `DynamicVar` 取值路径 | **变了**（`GetBaseValueForIConvertible` 被删）⇒ 自定义动态变量预览要复测 |
| 我们借用的 24 个原版 VFX 类 + 内部场景路径 | **全部存在**（0 缺失） |
| 我们硬编码等待的演出时长常量 | 仅 `NGrandFinaleVfx` 多了一个 1.625（该 helper 目前没有卡在用） |
| 我们引用的 9 个原版能力 + 2 个原版卡/遗物 | 差异只有 `base.X` → `X` 写法，**语义与数值未变** |

## 1. A 级：优先复测（23 张）

| 卡牌 | 英文 | 风险来源 | 怎么测 | 异常长什么样 |
|---|---|---|---|---|
| **冰铠** | `IceArmor` | 新版 `DynamicVar` 删除了 `GetBaseValueForIConvertible()`，取值改走 `ShownValue = (int)PreviewValue` | 手里/卡组里各看一次：数值是否=「基础 + 敌人寒冷+冻结」；数字是否随敌人状态变绿 | 卡面停在基础值 / 一直不变绿 / 打出时实际给的覆甲与卡面数字不一致 |
| **治疗术** | `HealingSpell` | 同上（自定义 `CalculationBase/Extra` 预览） | 手里看数值是否=「基础 + 法术增益」；打出后实际回血是否一致 | 卡面数值不随法术增益变化 |
| **魔力爆发** | `ManaBurst` | `CalculatedDamageVar` + `ExtraDamageVar`；战斗内的「（造成 N 点伤害。）」行；以太伤害演出 | 卡组里只显示设计稿两句；进战斗后底部出现括号行且 N 随法术增益变化；打出时白+金星光冲击 + 星光音 | 括号行不出现 / N 不更新 / 特效或音效缺失 |
| **以太箭** | `AetherArrow` | 自制的白+金星星冲击场景（复制原版只改色）+ 星光音；由「第四元素」生成 | 打出：目标身上金色星光冲击（不是紫色）+ 星光音 | 完全没特效（查日志 `演出：白+金星星冲击`）/ 音效无声 |
| **结晶** | `Crystallization` | 依赖 `CombatHistory.CardGenerated` 与 `HappenedThisTurn` 判「本回合生成过牌」 | 第 2 回合起、本回合没生成过牌时打 = 8 格挡；先打一张生成牌（如「镜像」/「启示」）再打 = 16 | 永远 16 / 永远 8 |
| **孔雀之尾** | `CaudaPavonis` | 依赖 `AfterCardGeneratedForCombat`；未升级与升级版是两个独立能力、各自叠层 | 打一张未升级 + 一张升级，检查是否出现**两个**图标且各自 n=1；生成一张牌后应各触发一次（2 层与 3 层） | 两个 buff 合并成一个 / 生成牌不触发 / 优先给未拥有负面效果失效 |
| **催化** | `Catalyzation` | 依赖 `AfterCardGeneratedForCombat` 计数 | 打 1 张催化，生成 4 张牌 → 应得 1 费；打 2 张 → 两个独立计数器 | 不给费 / 计数不独立 |
| **暗黑神秘学** | `DarkMysticism` | 生成卡牌并加入手牌（`AddGeneratedCardToCombat`） | 打出后手牌多出对应牌，且被「催化」计数 | 牌没进手牌 / 不触发催化 |
| **决心** | `Determination` | 生成「灼伤」+ 燃烧施加计数 | 打出后弃牌堆出现灼伤；界面/能力上的燃烧计数正常 | 灼伤没出现 / 计数错乱 |
| **勿忘我** | `ForgetMeNot` | 消耗堆取牌 + 选牌（`CardSelectCmd` 已重构） | 选 1~3 张（至少 1 张、必须手动确认）；取消/选 0 张的行为；取回后往弃牌堆塞「失忆」 | 选牌界面不弹 / 无法取消 / 取回数量错 |
| **启示** | `Inspiration` | 生成卡牌 + 从池里选 3 张（选牌重构 + 卡池 API） | 打出后 3 张候选、只能选 1 张；生成的牌能被催化计数 | 候选为空 / 数量不对 |
| **镜像** | `Mirror` | 复制手牌（生成路径） | 打出后手牌每张（除镜像）各多一张复制品；复制品算「生成」 | 复制品缺失 / 不触发催化或结晶 |
| **燃烧之魂** | `BurningSoul` | ⚠ 活力联动核心：我们的 `VigorTrackingPatch` 依赖原版 `VigorPower` 私有结构 | 开「燃烧之魂」+「纵火狂」，连续打两张攻击牌 → 第二张**仍应吃到活力**；日志应出现 `已清空跟踪` | 第二张攻击伤害不加活力（= 老 bug 复发） |
| **纵火狂** | `Pyromaniac` | 同上（每次燃烧落地给活力） | 同上；并确认「燃烧之魂」追加的燃烧也会触发纵火狂 | 追加的燃烧不触发 / 活力不涨 |
| **顿悟** | `Epiphany` | `CardSelectCmd` 重构 + 加入手牌 | 选 2 张手牌消耗 → 再从抽牌堆选 1(2) 张加入手牌；手牌满时不该丢牌 | 选牌界面异常 / 手牌溢出丢牌 |
| **思索** | `Ponder` | `CardSelectCmd` 重构（选 1 张加入手牌） | 起始遗物「魔法卷轴」战斗开始给 1 张；打出后选 1 张牌加入手牌 | 选牌不弹 / 不加入 |
| **书签** | `Bookmark` | `CardSelectCmd` 重构（给手牌加保留） | 选 N 张手牌加「保留」；确认后卡面出现保留词条 | 选牌异常 / 未加保留 |
| **回忆** | `Recollection` | `CardSelectCmd` 重构（把上回合最后 N 张放回手牌） | 打出后上回合最后 N 张非消耗牌回到手牌 | 回手数量不对 |
| **创生** | `Quickening` | `CardSelectCmd` 重构 + `CardCmd.TransformToRandom` | 手上 1 张可变化牌时也能选 1 张；确认后牌真的变化 | 选牌界面无法确认 / 牌没变化 |
| **整理** | `TidyUp` | `CardSelectCmd` 重构 | 选牌流程正常、确认后效果生效 | 选牌异常 |
| **真理派对** | `TruthParty` | `CardSelectCmd` 重构（可 0 张） | 允许一张都不选并确认 | 强制选牌 / 无法确认 |
| **幻化** | `Transmogrify` | `CardSelectCmd` 重构 + 变形 | 选牌后变化结果正常 | 变化失效 |
| **占卜** | `Divination` | 选牌（抽牌堆）+ 抽牌 | 从抽牌堆选牌加入手牌后，抽牌数量正确 | 选牌异常 |

## 2. B 级：顺路复测（49 张，按依赖分组）

这些卡依赖的东西**已确认未变**，或只是演出/时序，正常玩时留意即可。

### 选牌/变形（其余）

| 卡牌 | 英文 | 备注 |
|---|---|---|
| 泛灵论 | `Animism` | 变化/变形；抽牌 |
| 尾注 | `Endnote` | 抽牌 |
| 电磁波 | `ElectromagneticWave` | 抽牌；多段/全target |
| 速读 | `SpeedRead` | 抽牌；能量 |

### 借用原版 VFX（演出/时序）

| 卡牌 | 英文 | 备注 |
|---|---|---|
| 清净世界 | `ClearWorld` | 重锤雨（先播 0.2s 再结算） |
| 天火浩荡 | `SkyfireRampage` | 金色横线 + 大魔法飞弹 |
| 落日 | `Sunset` | 大魔法飞弹（先播后结算） |
| 全知形态 | `OmniscientForm` | 全屏烟雾暗角 + 金色横线 |
| 杰作 | `MagnumOpus` | 紫色烟雾暗角 |
| 冰之城墙 | `IceWall` | 尖刺水花 |
| 冰矢 | `IceArrow` | 薄切 |
| 击穿 | `Puncture` | 戏剧性刺击 |
| 雷光 | `LightningBolt` | 闪电球激发特效 + 音效 |
| 降雷 | `LightningFall` | 闪电球激发 + 逐目标 |
| 引燃 | `Ignite` | 地面火 + 火焰拳 |
| 炽焰术 | `BlazingFlame` | 火焰拳 + 火焰音 |
| 弹射 | `DoubleRicochet` | 火焰拳 + 星光冲击 |
| 火球术 | `Fireball` | 火焰拳 |
| 火苗 | `FlameSpark` | 火焰拳 |
| 闪爆 | `Flashburst` | 火焰拳 |
| 高温消毒 | `SterilizeByFire` | 地面火 |

### 自定义动态变量（预览）

| 卡牌 | 英文 | 备注 |
|---|---|---|
| 巨焱拳 | `BlazingPunch` | 自定义动态变量(预览) |
| 魔力汲取 | `ManaDrain` | 自定义动态变量(预览)；能量 |

### 格挡/数值类

| 卡牌 | 英文 | 备注 |
|---|---|---|
| 防御 | `DefendWizard` | 格挡 |
| 镇静 | `CalmMind` | 原版能力；格挡 |
| 置换 | `Displacement` | 原版能力；格挡 |
| 震慑 | `Intimidate` | 原版能力；格挡 |
| 心流 | `MindFlow` | 格挡；能量 |
| 迷思 | `Myth` | 格挡；能量 |
| 屏蔽 | `Screen` | 原版能力；格挡 |
| 通灵 | `Telepathy` | 原版能力；格挡 |
| 观火 | `WatchFire` | 格挡 |
| 书签 | `Bookmark` | CardSelectCmd 重构（给手牌加保留） |

### 原版能力交互（已确认代码未变）

| 卡牌 | 英文 | 备注 |
|---|---|---|
| 交流电 | `AlternatingCurrent` | 原版能力 |
| 失忆 | `Amnesia` | 生成卡牌(历史/钩子)；原版能力 |
| 远古残章 | `AncientFragment` | 原版能力 |
| 修行 | `Asceticism` | 原版能力 |
| 焚尽 | `BurnToAshes` | 原版能力 |
| 环流 | `Circulation` | 原版能力 |
| 大寒波 | `ColdWave` | 原版能力 |
| 凝神 | `Concentrate` | 原版能力 |
| 脆化 | `Embrittlement` | 原版能力 |
| 图书馆 | `GreatLibrary` | 原版能力 |
| 禁书目录 | `MagicalIndex` | 原版能力 |
| 魔力凝聚 | `ManaCondense` | 原版能力 |
| 净罪 | `PurifySin` | 原版能力 |
| 火刑 | `PyroJudgment` | 原版能力 |
| 快速咏唱 | `QuickChant` | 原版能力 |
| 超导 | `Superconduct` | 原版能力 |
| 第四元素 | `TheFourthElement` | 原版能力 |

## 3. C 级：可以不专门测（3 张）

| 卡牌 | 英文 | 说明 |
|---|---|---|
| 冰霰 | `IceShard` | 纯数值/纯格挡，无外部 API 或资源依赖 |
| 静电 | `StaticCharge` | 纯数值/纯格挡，无外部 API 或资源依赖 |
| 打击 | `StrikeWizard` | 纯数值/纯格挡，无外部 API 或资源依赖 |

## 4. 非卡牌但同类风险（也建议过一遍）

- **角色层**：`GetArchitectAttackVfx()` 里 `vfx/vfx_rock_shards` 已改为 `vfx/vfx_rock_shatter`（观赏性）。
- **能力牌图标/能力悬停**：「孔雀之尾」「图书馆」新图标；「随机负面效果」悬停。
- **3 张先古卡 + 9 位先古对话**：`GetUpgradeReplacement` / 对话键查找未变，但建议各看一次。
- **3 个事件**（含新版规则怪谈）、**1 个特殊怪物**、**9 个遗物**、**3 瓶药水**：与卡牌无关，但同属本 mod 内容。
- **商店/奖励**：新版商人体系新增 22 个类型，注意本 mod 卡牌/遗物在商店与奖励中是否正常出现。

## 5. 全局（与具体卡无关）

1. **旧存档**能否继续（新版带 24 条存档迁移）；
2. **卡图库/卡池**：75 张是否都在、稀有度与描述正确；
3. **音效**：本 mod 只用 3 个 FMOD 事件 —— `attack_fire`、`defect_lightning_evoke`、`regent_guiding_star`，各听一次即可；
4. **多人/联机**（如果测）；
5. 新版新增原版状态牌 **`Amnesia`** 与我们「失忆」**英文名重复**（id 前缀不同、不会冲突），是否改名待定。

## 6. 附：75 张完整分级表

| 级 | 卡牌 | 英文 | 风险/备注 |
|---|---|---|---|
| A | 以太箭 | `AetherArrow` | 自制「白+金星星冲击」场景 + 星光音 |
| A | 书签 | `Bookmark` | CardSelectCmd 重构（给手牌加保留） |
| A | 燃烧之魂 | `BurningSoul` | 活力联动核心（VigorTrackingPatch 依赖原版私有结构） |
| A | 催化 | `Catalyzation` | 依赖 AfterCardGeneratedForCombat 计数 |
| A | 孔雀之尾 | `CaudaPavonis` | 依赖 AfterCardGeneratedForCombat（两版能力各自叠层） |
| A | 结晶 | `Crystallization` | 依赖 CombatHistory.CardGenerated 判本回合生成 |
| A | 暗黑神秘学 | `DarkMysticism` | 生成卡牌入牌堆 |
| A | 决心 | `Determination` | 生成「灼伤」+ 燃烧施加计数 |
| A | 占卜 | `Divination` | 选牌（抽牌堆） |
| A | 顿悟 | `Epiphany` | CardSelectCmd 重构 + 加入手牌 |
| A | 勿忘我 | `ForgetMeNot` | 消耗堆取牌（选牌重构影响） |
| A | 治疗术 | `HealingSpell` | 同 IceArmor（自定义 CalculationBase/Extra 预览） |
| A | 冰铠 | `IceArmor` | Custom DynamicVar 预览路径（新版改取 ShownValue=PreviewValue）——看数字与变绿 |
| A | 启示 | `Inspiration` | 生成卡牌 + 选牌池 |
| A | 魔力爆发 | `ManaBurst` | CalculatedDamageVar 实时伤害行 + 以太伤害演出 |
| A | 镜像 | `Mirror` | 复制手牌（生成路径） |
| A | 思索 | `Ponder` | CardSelectCmd 重构（选 1 张加入手牌） |
| A | 纵火狂 | `Pyromaniac` | 同燃烧之魂（每次燃烧给活力） |
| A | 创生 | `Quickening` | CardSelectCmd 重构 + TransformToRandom |
| A | 回忆 | `Recollection` | CardSelectCmd 重构（放回手牌） |
| A | 整理 | `TidyUp` | CardSelectCmd 重构 |
| A | 幻化 | `Transmogrify` | CardSelectCmd 重构 + 变形 |
| A | 真理派对 | `TruthParty` | CardSelectCmd 重构 |
| B | 交流电 | `AlternatingCurrent` | 原版能力 |
| B | 失忆 | `Amnesia` | 生成卡牌(历史/钩子)；原版能力 |
| B | 远古残章 | `AncientFragment` | 原版能力 |
| B | 泛灵论 | `Animism` | 变化/变形；抽牌 |
| B | 修行 | `Asceticism` | 原版能力 |
| B | 炽焰术 | `BlazingFlame` | 火焰拳 + 火焰音 |
| B | 巨焱拳 | `BlazingPunch` | 自定义动态变量(预览) |
| B | 焚尽 | `BurnToAshes` | 原版能力 |
| B | 镇静 | `CalmMind` | 原版能力；格挡 |
| B | 环流 | `Circulation` | 原版能力 |
| B | 清净世界 | `ClearWorld` | 重锤雨（先播 0.2s 再结算） |
| B | 大寒波 | `ColdWave` | 原版能力 |
| B | 凝神 | `Concentrate` | 原版能力 |
| B | 防御 | `DefendWizard` | 格挡 |
| B | 置换 | `Displacement` | 原版能力；格挡 |
| B | 弹射 | `DoubleRicochet` | 火焰拳 + 星光冲击 |
| B | 电磁波 | `ElectromagneticWave` | 抽牌；多段/全target |
| B | 脆化 | `Embrittlement` | 原版能力 |
| B | 尾注 | `Endnote` | 抽牌 |
| B | 火球术 | `Fireball` | 火焰拳 |
| B | 火苗 | `FlameSpark` | 火焰拳 |
| B | 闪爆 | `Flashburst` | 火焰拳 |
| B | 图书馆 | `GreatLibrary` | 原版能力 |
| B | 冰矢 | `IceArrow` | 薄切 |
| B | 冰之城墙 | `IceWall` | 尖刺水花 |
| B | 引燃 | `Ignite` | 地面火 + 火焰拳 |
| B | 震慑 | `Intimidate` | 原版能力；格挡 |
| B | 雷光 | `LightningBolt` | 闪电球激发特效 + 音效 |
| B | 降雷 | `LightningFall` | 闪电球激发 + 逐目标 |
| B | 禁书目录 | `MagicalIndex` | 原版能力 |
| B | 杰作 | `MagnumOpus` | 紫色烟雾暗角 |
| B | 魔力凝聚 | `ManaCondense` | 原版能力 |
| B | 魔力汲取 | `ManaDrain` | 自定义动态变量(预览)；能量 |
| B | 心流 | `MindFlow` | 格挡；能量 |
| B | 迷思 | `Myth` | 格挡；能量 |
| B | 全知形态 | `OmniscientForm` | 全屏烟雾暗角 + 金色横线 |
| B | 击穿 | `Puncture` | 戏剧性刺击 |
| B | 净罪 | `PurifySin` | 原版能力 |
| B | 火刑 | `PyroJudgment` | 原版能力 |
| B | 快速咏唱 | `QuickChant` | 原版能力 |
| B | 屏蔽 | `Screen` | 原版能力；格挡 |
| B | 天火浩荡 | `SkyfireRampage` | 金色横线 + 大魔法飞弹 |
| B | 速读 | `SpeedRead` | 抽牌；能量 |
| B | 高温消毒 | `SterilizeByFire` | 地面火 |
| B | 落日 | `Sunset` | 大魔法飞弹（先播后结算） |
| B | 超导 | `Superconduct` | 原版能力 |
| B | 通灵 | `Telepathy` | 原版能力；格挡 |
| B | 第四元素 | `TheFourthElement` | 原版能力 |
| B | 观火 | `WatchFire` | 格挡 |
| C | 冰霰 | `IceShard` | 纯数值，无外部依赖 |
| C | 静电 | `StaticCharge` | 纯数值，无外部依赖 |
| C | 打击 | `StrikeWizard` | 纯数值，无外部依赖 |
