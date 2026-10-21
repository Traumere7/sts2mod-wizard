# 遗物实现参考（原版 + BaseLib 接口调查）

> 2026-10-10 调查产出。用途：实现 `design.md` §遗物 里新增的 9 个遗物。
> **本文只记录已查证的事实，每条都带 文件:行号 证据。** 未查证的推测会显式标注。

原版遗物目录（⚠️ 注意文件夹名里是**点号**，且没有 `Models\Relics\` 嵌套）：
```
reference/decompiled/game/MegaCrit.Sts2.Core.Models.Relics/     ← 300 个 .cs
```

---

## 0. 结论速查：9 个遗物各抄哪个原版模板

| # | 设计（design.md） | 模板 | 主钩子 | 关键行 |
|---|---|---|---|---|
| 1 | 恒温炉 +1 法术增益 | **`Brimstone`** | `AfterSideTurnStart` + `participants.Contains` | `Brimstone.cs:31,33,36` |
| 2a | 铅咒板 每回合首次上负面 | **`DemonTongue`** | `BeforeSideTurnStart` 重置标志 | `DemonTongue.cs:15,23,29-36` |
| 2b | 同上，检测"施加了负面" | `UnsettlingLamp` + `ViciousPower`/`SleightOfFleshPower` | `AfterPowerAmountChanged` | `SleightOfFleshPower.cs:23` |
| 3a | 火漆密函 第 3 回合 | **`Chandelier`** | `TurnNumber == 3` | `Chandelier.cs:27` |
| 3b | 同上，随机能力牌 | **`WhiteNoise`**（卡）/ `Crossbow`（遗物） | `CardFactory.GetDistinctForCombat` | `WhiteNoise.cs:23-25` |
| 3c | 同上，费用归零 | `CardModel.SetToFreeThisTurn()` | — | `CardModel.cs:1267` |
| 4a | 温暖蜡烛 消耗计数 | **`JossPaper`** | `AfterCardExhausted` | `JossPaper.cs:102` |
| 4b | 同上，跨战斗持久计数 | `JossPaper` / `BookOfFiveRings` | `ShowCounter`+`DisplayAmount`+`[SavedProperty]` | `JossPaper.cs:32,60-74` |
| 4c | 同上，回复生命 | `BookOfFiveRings` | `CreatureCmd.Heal` | `BookOfFiveRings.cs:90` |
| 5 | 巫术百科 3 种新负面 → 3 费 | 组合 `SleightOfFleshPower` + `Nunchaku` + `UnsettlingLamp` | `AfterPowerAmountChanged` | `PlayerCmd.cs:29` |
| 6 | 鎏金天平 获得力量→等量法术增益 | **`PossessStrengthPower`** / `RuinedHelmet` | `AfterPowerAmountChanged` | `PossessStrengthPower.cs:31,37,41,47` |
| 7 | 三原素 三系同回合 → 无实体 + 自我禁用 | 组合 `EnvenomPower` + `DemonTongue` + `Apparition` + `StoneCalendar` | `AfterDamageGiven` | `Hook.cs:401`；`IntangiblePower.cs:66-72` |

---

## 1. 三个必须知道的坑

### 1.1 ⚠️ `RelicStatus.Disabled` 只是**外观**，不会禁用任何行为

这是本次调查最重要的一条。**设计里的「三原素」(#7) 依赖它，直接用会失效。**

| 证据 | 位置 |
|---|---|
| `Status` 的 setter 只触发一个事件 | `RelicModel.cs:330-345` |
| 唯一消费者是图标 shader（`Disabled` → `_is_used=1`） | `RelicModel.cs:473-504` |
| `ShouldReceiveCombatHooks => true`，无条件 | `RelicModel.cs:422` |
| 派发时**只**过滤 `IsMelted`，从不读 `Status` | `CombatState.cs:432`、`Hook.cs:53-63` |

**正解**：自定义一个 `_disabledUntilTurn` 字段，并在**每个钩子体开头**自己 `if (IsDisabled) return;`。
原版也是这么做的 —— `BoneTea.cs:50-53` 在设 `Status` 之外还自己复查 `IsUsedUp`。

三个状态语义（都只是视觉）：
- `Active` = "已就绪/即将触发"的脉冲
- `Normal` = 默认 / 本回合已用完
- `Disabled` = 置灰"已耗尽"

### 1.2 ⚠️ `AfterPowerAmountChanged` 在**增量为 0 时不会触发**

- `PowerCmd.Apply` 里 `if (modifiedAmount != 0m)` — `PowerCmd.cs:157-161`
- `PowerCmd.ModifyAmount` 里 `if ((int)modifiedOffset != 0)` — `PowerCmd.cs:247-250`

影响 #2、#5、#6：如果"**施加动作本身**要算数"（例如敌人已有 4 层燃烧、再施加 1 层），
这个钩子**不发**。本 mod 已为此建了 `src/Scripts/Spells/WizardEvents.cs`（燃烧/寒冷事件），
其注释与引擎行为一致 —— 这类需求走自己的事件，别指望这个钩子。

另外：**这个钩子没有 `target` 参数**，目标要读 `power.Owner`（`PowerModel.cs:264`）。

### 1.3 ⚠️ `TemporaryStrengthPower` 等"临时属性"能力会让 #6 触发两次

`TemporaryStrengthPower` 在 `BeforeApplied` 里施加真实 `StrengthPower`，
又在自己层数变化时再施加一次（`TemporaryStrengthPower.cs`）。
原版两种规避写法：
- `!(power is ITemporaryPower)` — `SleightOfFleshPower.cs:23`
- `HasDoubledTemporaryPowerSource()` — `UnsettlingLamp.cs:169-172`

---

## 2. 注册与资源（本 mod 已就绪）

### 2.1 注册：加 `[Pool]` 特性即可

`CustomContentDictionary.AddModel` 会读 `[Pool(typeof(WizardRelicPool))]` 并调
`ModHelper.AddModelToPool`（`CustomContentDictionary.cs:64-69`）。
`CustomRelicPoolModel.GenerateAllRelics()` 返回空数组、由
`ModHelper.ConcatModelsFromMods(this, _relics)` 合并进来（`RelicPoolModel.cs:29-31`）。

> 所以**新增遗物只需**：类上写 `[Pool(typeof(WizardRelicPool))]`、继承 `ArcaneRelicModel`。
> 不需要改池子代码。

**必须在游戏初始化前完成** —— `AddModelToPool` 在 `isFrozen` 时会抛
`InvalidOperationException`（`ModHelper.cs`）。`CustomRelicModel` 的构造函数默认
`autoAdd: true` 就是干这个的。

### 2.2 图标：BaseLib 不管，`ArcaneRelicModel` 已修好路径

`RelicModel.IconBaseName => Id.Entry.ToLowerInvariant()`（`RelicModel.cs:128`），
而 mod 的 `Id.Entry` 形如 `WIZARD-SPELL_SCROLL`（带连字符）⇒ 找不到图标。
本 mod 的 `ArcaneRelicModel` 已重写三个路径为 `SnakeCase(类名)`，
与卡图同一套约定。**新遗物只要继承它、并按类名 snake_case 放图即可。**

需要的 5 个文件（本 mod 的 `make_new_placeholders.py` 已有生成器）：
```
images/relics/{name}.png              ← 大图标（85×85）
images/relics/{name}_outline.png      ← 轮廓
images/atlases/relic_atlas.png        ← 图集（N×85 宽）
images/atlases/relic_outline_atlas.png
images/atlases/relic_atlas.sprites/{name}.tres + {name}_outline.tres
```
- `relic_icon_pair(name, color, accent, symbol)` — `make_new_placeholders.py:348`
- `build_relic_atlas(entries)` — `make_new_placeholders.py:369`（**会整体重写图集**，
  新增遗物时必须把所有遗物一起传进去）

⚠️ **本 mod 现有 2 个遗物的图标是占位图**（286 / 287 字节），
与"53 张卡图全是真美术"不同。新遗物若也用占位图，视觉上是一致的。

#### 2.2.1 ⚠️⚠️ 遗物有**两套**图标，换素材必须同时处理（2026-10-12 实测踩到）

| 用途 | 属性 | 读哪个文件 |
|---|---|---|
| **缩略图**（遗物栏里的小图标、置灰态） | `PackedIconPath` / `PackedIconOutlinePath` | **图集** `atlases/relic_atlas.png` 里的一格（经 `.tres` 的 `region`） |
| **大图**（点开遗物描述时的大图标） | `BigIconPath` | `images/relics/{name}.png` |

**实测症状**：只把 `images/relics/*.png` 换成真美术后，
"**只有点开描述的大图是新素材，缩略图还是老的**"。

**原因**：缩略图根本不读 `images/relics/`，它读**图集**。图集没重建就还是旧图。

**正确做法（顺序不能错）**：
1. 真美术放到 `images/relics/{name}.png` 与 `{name}_outline.png`
   （建议 256×256 RGBA、正方形；非正方形会被居中裁方）
2. **重建图集**：`python reference/tools/rebuild_relic_atlas_from_art.py`
   —— 它把 `images/relics/*.png` 缩放成 85×85 拼成图集。
   ⚠️ 别用 `make_relic_art.py`（那个是**生成占位图**，会覆盖真美术）
3. **删图集的导入缓存**：`src/.godot/imported/relic_atlas.png-*` 与
   `relic_outline_atlas.png-*`，否则 Godot 可能继续用旧 `.ctex`
4. `.\build.ps1`

**健康检查**：真美术的图集 `.ctex` 应远大于占位版 ——
本次实测 `1,776 B`（占位）→ `72,810 B`（真美术）。
若 `atlases/relic_atlas.png` 只有 2~3 KB，说明里面仍是占位图。

**为什么是 85×85**：本 mod 既有约定（`make_relic_art.py` 用 `cell = 85`），
且已工作。原版图集用**逐格不等尺寸**（如 `Rect2(2644, 704, 81, 69)`），
但那是原版自己的打包结果，mod 侧统一方格完全可行。

### 2.3 本地化键
`relics.json`，键 `{Id.Entry}.title` / `.description` / `.flavor`
（本 mod 已有 `WIZARD-SPELL_SCROLL.*`、`WIZARD-DEAD_SEA_SCROLLS.*`）。

工具提示渲染：`DynamicDescription`（`RelicModel.cs:91-109`）会
`DynamicVars.AddTo(description)`，然后注入 `energyPrefix` 与 `singleStarIcon`。
所以 `CanonicalVars` + `{VarName}` 占位符可用，**与卡牌同一套机器**。

`PowerVar<T>` 的名字自动取 `typeof(T).Name`（`PowerVar.cs`），
即 `new PowerVar<SpellPowerPower>(1m)` → 文案里写 `{SpellPowerPower}`。
**同名会抛异常**，所以两个同类型变量必须显式命名（见 `Brimstone.cs:23-27`）。

### 2.4 已废弃资源

`wizard_starter_relic.png`（+ `_outline`）是 `WizardStarterRelic` → `SpellScroll`
改名后的**孤儿文件**，无类引用。启动日志里 `RELIC.WIZARD-WIZARD_STARTER_RELIC`
的告警来自**旧存档**，不是当前代码问题。可清理。

### 2.5 ⚠️⚠️ `[SavedProperty]` 只支持少数几种类型（实测踩到）

**启动日志实测**（`巫术百科` 初版用了 `HashSet<Type>`）：
```
[WARN] [BaseLib] SavedProperty does not support values of type
       System.Collections.Generic.HashSet`1[System.Type];
       change Maleficarum.CountedEver to a SavedSpireField for BaseLib to save it.
```

已核实 `BaseLib.Utils.SavePatchUtils.SupportedTypes` 的完整清单：

| 支持 | 不支持 |
|---|---|
| `int` / `bool` / `string` / `ModelId` | `HashSet<T>`、`List<T>`（除下面那个） |
| `int[]` | `Type`、`DateTime`… |
| `SerializableCard` / `SerializableCard[]` / `List<SerializableCard>` | |
| **任何枚举** 与 **枚举数组**（`t.IsEnum`，数组看 `GetElementType().IsEnum`） | |

判定函数是 `IsStoreTypeBaseSupported`，另外还要求宿主类型是
`RelicModel` / `CardModel` / `EnchantmentModel` / `ModifierModel`
（`IsHolderTypeBaseSupported`）。

**⚠️ 这个告警只在启动时打印、不会抛异常** —— 即"看起来能用，其实静默不存档"。
所以**新增任何 `[SavedProperty]` 后都要跑一次游戏、看日志有没有这条**。

**超出清单时的两条出路：**
1. **改用受支持类型做持久层**（推荐，零配置）——
   本次做法：存 `int[]`（存 `Type.GetHashCode()`），运行时再还原成 `HashSet<Type>`
   供 O(1) 查询。见 `Maleficarum.CountedEverHashes`。
   代价：哈希不可逆，还原时要反向枚举候选类型匹配
   （`typeof(PowerModel).Assembly.GetTypes()`）。
2. **按文档注册自定义类型**：`ExtendedSaveTypes.RegisterListSaveType` /
   `RegisterDictionarySaveType` / `RegisterObjectSaveType`，或改用
   `SavedSpireField<TKey, TVal>`（可自定义 `Serializer`/`Deserializer`）。
   见 `reference/.../spirefield.md`。

---

## 3. 钩子总览（`RelicModel` → `AbstractModel`，共 182 个可重写钩子）

**继承链只有两级**：`RelicModel : AbstractModel`，中间没有别的类。
`RelicModel` 自己只重写了 3 个：`ShouldReceiveCombatHooks`（=true）、
`DeepCloneFields`、`AfterCloned`（后两个必须 `base.` 调用）。

`AbstractModel` 上 **182 个钩子全是 `public virtual`**，`RelicModel` **一个都没重写**，
所以遗物子类可以自由重写任意一个。派发入口：`Hook.cs`（179 个），
另外 3 个在别处（`AfterAddToDeckPrevented` @ `CardPileCmd.cs:460`、
`AfterModifyingCardPlayResultLocation` @ `CardModel.cs:1885`、
`AfterTargetingBlockedVfx` @ `NTargetManager.cs:607`）。

### 3.1 本批 9 个遗物用得到的钩子

| 签名 | 时机 | 声明处 |
|---|---|---|
| `Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)` | 某方回合开始后 | `AbstractModel.cs:1268` |
| `Task BeforeSideTurnStart(PlayerChoiceContext, CombatSide, IReadOnlyList<Creature>, ICombatState)` | 某方回合开始前（**每回合重置标志用这个**） | `AbstractModel.cs:1247` |
| `Task AfterPlayerTurnStart(PlayerChoiceContext, Player)` | 玩家回合开始后（**需要玩家选择时用**） | `AbstractModel.cs:1320` |
| `Task AfterCardExhausted(PlayerChoiceContext, CardModel card, bool causedByEthereal)` | 任意牌被消耗后 | `AbstractModel.cs:447` |
| `Task AfterPowerAmountChanged(PlayerChoiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)` | 能力层数变化后 | `AbstractModel.cs:1077` |
| `Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)` | 能力层数变化前（**有 target**） | `AbstractModel.cs:1057` |
| `Task AfterDamageGiven(PlayerChoiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)` | 造成伤害后（**每段各一次**） | `AbstractModel.cs:593` |
| `Task AfterCardPlayed(PlayerChoiceContext, CardPlay)` | 打出牌后 | `AbstractModel.cs:477` |
| `Task AfterCombatEnd(CombatRoom room)` | 战斗结束后（**清理字段/Status 的地方**） | `AbstractModel.cs:520` |
| `Task BeforeHandDraw(Player, PlayerChoiceContext, ICombatState)` | 回合开始抽牌前 | `AbstractModel.cs:779` |
| `decimal ModifyPowerAmountGivenAdditive/Multiplicative(...)` | 修改要施加的能力层数 | `AbstractModel.cs:1845/1861` |
| `bool TryModifyPowerAmountReceived(PowerModel, Creature, decimal, Creature?, out decimal)` | 修改目标收到多少 | `AbstractModel.cs:2093` |
| `bool TryModifyEnergyCostInCombat(CardModel, decimal, out decimal)` | 战斗中改卡牌费用 | `AbstractModel.cs:2034` |

### 3.2 遗物子类常重写的属性

| 属性 | 说明 | 行 |
|---|---|---|
| `abstract RelicRarity Rarity` | **必须实现** | `RelicModel.cs:172` |
| `virtual IEnumerable<DynamicVar> CanonicalVars` | 工具提示变量 | `RelicModel.cs:302` |
| `virtual IEnumerable<IHoverTip> ExtraHoverTips` | 额外悬停提示 | `RelicModel.cs:376` |
| `virtual bool ShowCounter` | 显示数字角标 | `RelicModel.cs:347` |
| `virtual int DisplayAmount` | 角标数值 | `RelicModel.cs:349` |
| `virtual bool IsAllowed(IRunState)` / `IsAllowedAtNeow(Player)` | 抽池门槛 | `RelicModel.cs:434/443` |
| `virtual bool IsStackable` | 可否重复获得 | `RelicModel.cs:251` |
| `virtual bool IsUsedUp` | "次数用尽"（工具提示改写） | `RelicModel.cs:236` |
| `virtual bool HasUponPickupEffect` | 有拾取效果（影响可否交易） | `RelicModel.cs:241` |
| `virtual int MerchantCost` | 商店价（默认按稀有度） | `RelicModel.cs:304` |
| `virtual RelicModel? GetUpgradeReplacement()` | 欧洛巴斯替换（**BaseLib 已打补丁**） | `CustomRelicModel.cs` |

**本 mod 不需要自己写 Harmony** —— BaseLib 的 `StarterUpgradePatches` 已对
`TouchOfOrobas` 打前缀，遗物侧只要重写 `GetUpgradeReplacement()`。

### 3.3 枚举

```csharp
enum RelicRarity { None=0, Starter, Common, Uncommon, Rare, Shop, Event, Ancient }
enum RelicStatus { Normal=0, Active, Disabled }   // 全是视觉
```

### 3.4 ⚠️ 不存在的 API（别去找）

- **没有 `ShouldAppear`**（只有无关的 `MonsterState.ShouldAppearInLogs`）
- **没有卡牌升级/转化的钩子**（`AfterCardUpgraded` / `AfterCardTransformed` 都不存在）；
  转化只能间接通过 `TryModifyCardBeingAddedToDeck` 观察
- **没有 `AfterRelicObtained`**（遗物只有自己的 `AfterObtained` / `AfterRemoved`）
- **没有公开可调用的 `LocalCostModifier` 构造**；改费用走
  `CardModel.SetToFreeThisTurn()` 或 `CardEnergyCost.Set*`

---

## 4. 持久化计数器（#4 温暖蜡烛要用）

三个成员配合：

```csharp
public override bool ShowCounter => true;              // RelicModel.cs:347
public override int DisplayAmount => <你的计数>;        // RelicModel.cs:349

[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
public int CardsExhausted
{
    get => _cardsExhausted;
    set { AssertMutable(); _cardsExhausted = value; InvokeDisplayAmountChanged(); }
}
```

- **跨战斗持久 = 不重置**。`JossPaper.AfterCombatEnd`（`JossPaper.cs:144-148`）
  只重置临时字段 `EtherealCount`，`CardsExhausted` 留着。
- ⚠️ **`[SavedProperty]` 的 setter 必须 `AssertMutable()`** —— 反序列化时会在可变克隆上跑。
- `SerializationCondition` 选项：`AlwaysSave`(默认) / `SaveIfNotPropertyDefault` /
  `SaveIfNotTypeDefault` / `SaveIfNotCollectionEmptyOrNull`。
- **让工具提示数字跟随持久计数器**：在 setter 里改 `DynamicVars["X"].BaseValue`
  （`BoneTea.cs:27-44` 的写法）。
- 阈值取模的两种写法：`BookOfFiveRings.cs:65` 用
  `CardsAdded % Cards.IntValue`；`JossPaper.cs:132` 用 `CardsExhausted %= 5`。

---

## 5. 各遗物的具体要点

### #1 恒温炉 —— 照抄 `Brimstone`

```csharp
public override async Task AfterSideTurnStart(CombatSide side,
    IReadOnlyList<Creature> participants, ICombatState combatState)
{
    if (participants.Contains(Owner.Creature))          // ★ 判断"是我的回合"
    {
        Flash();
        await PowerCmd.Apply<SpellPowerPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature, 1m, Owner.Creature, null);
    }
}
```
- 用 `participants.Contains(Owner.Creature)` 判回合，**不需要** `side == Player`
  （`AbstractModel.cs:1262-1266` 定义了 participants 的语义，已含额外回合的注意）
- `new ThrowingPlayerChoiceContext()` 是"不需要玩家选择"的原版惯例
- 若需要玩家选择，改用 `AfterPlayerTurnStart`（`AbstractModel.cs:1313-1315` 明确说明）

### #2 铅咒板 —— `DemonTongue`（重置）+ `AfterPowerAmountChanged`（检测）

判"是我给敌人上了负面"的完整条件（来自 `UnsettlingLamp` + `SleightOfFleshPower`）：
```csharp
if (amount == 0m) return;                                   // SleightOfFleshPower.cs:23
if (applier != Owner.Creature) return;                       // UnsettlingLamp.cs:86
if (!power.Owner.IsEnemy) return;                            // SleightOfFleshPower.cs:23（无 target，用 power.Owner）
if (!power.IsVisible) return;                                // UnsettlingLamp.cs:94
if (power.GetTypeForAmount(amount) != PowerType.Debuff) return;  // UnsettlingLamp.cs:98
if (power.Owner.HasPower<ArtifactPower>()) return;           // UnsettlingLamp.cs:102（会被神器挡掉）
```
⚠️ 用 `power.GetTypeForAmount(amount)`（**按带符号的量**判定），不要用 `power.Type` ——
后者对"负力量"这类会判错。
⚠️ 原版**没有遗物**用过这个钩子（只有 3 个成就、1 个徽章、10 个能力用），
模式本身是原版的（`ViciousPower.cs:19`），但我们是第一个这么用的遗物。

### #3 火漆密函 —— `Chandelier` + `WhiteNoise`

```csharp
// 生成随机能力牌（照抄 WhiteNoise.cs:23-25 / Crossbow.cs:23-31）
var pool = Owner.Character.CardPool
    .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
    .Where(c => c.Type == CardType.Power).ToList();
if (pool.Count == 0) return;                    // ★ Crossbow.cs:26-29 的空池防御
var card = CardFactory.GetDistinctForCombat(
    Owner, pool, 1, Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
if (card == null) return;
card.SetToFreeThisTurn();                        // CardModel.cs:1267
await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
```
- `GetDistinctForCombat`（`CardFactory.cs:119-129`）内部已做：
  测试覆盖注入 → 按人数过滤 → `FilterForCombat`（**排除 Basic/Ancient/Event 且
  `CanBeGeneratedInCombat == false` 的**）→ `TakeRandom` → **`CreateCard` 生成战斗副本**
  ⇒ **不需要**自己再调 `CreateCard`
- 它可能返回**少于请求数或空**，所以 `FirstOrDefault()` + null 检查是必需的
- 想允许重复用 `CardFactory.GetForCombat`（`CardFactory.cs:140-152`）
- `AddGeneratedCardToCombat` **不检查手牌上限**（与 `SpellScroll` 的注释一致）

### #4 温暖蜡烛 —— 照抄 `JossPaper`，阈值 12，改为回复 3 点生命

```csharp
public override async Task AfterCardExhausted(PlayerChoiceContext ctx, CardModel card, bool causedByEthereal)
{
    if (card.Owner != Owner) return;             // JossPaper.cs:104
    CardsExhausted++;
    if (CardsExhausted >= 12) { CardsExhausted -= 12; await CreatureCmd.Heal(Owner.Creature, 3m); }
}
```
- `CreatureCmd.Heal(Creature, decimal, bool playAnim = true)` — `CreatureCmd.cs:735`；
  **第三个参数留默认**（`CreatureCmd.cs:728-734` 明确要求）
- 用 `new HealVar(3m)` 声明，让提示能渲染 `{Heal}`（`BookOfFiveRings` 的做法）
- ⚠️ **要决定"虚无"消耗算不算**：`JossPaper.cs:106-110` 把 `causedByEthereal`
  的消耗**延后到 `AfterSideTurnEnd`** 再计数（原因见其注释 76-80 行）。
  如果算，照抄这个延后；不算就 `if (causedByEthereal) return;`

### #5 巫术百科 —— 无单一模板，需组合

- "种别"的键：原版用 `power.GetType()`（`UnsettlingLamp.cs:171`）；
  等价可用 `power.Id`（`ModelId` 是 `record`，有值相等）
- "本场未给过"的记忆必须**跨战斗**⇒ `[SavedProperty]` 的 `HashSet`
  （`SerializationCondition.SaveIfNotCollectionEmptyOrNull`）
- 加费：`PlayerCmd.GainEnergy(decimal, Player)`（`PlayerCmd.cs:29`）；
  **内部没有回合检查**，要自己保证时机
- ⚠️ 语义待定：`_seenDebuffKinds` 要不要重置？原版无先例，**需要设计拍板并写进卡面**

### #6 鎏金天平 —— 照抄 `PossessStrengthPower` / `RuinedHelmet`

```csharp
if (power is not StrengthPower) return;      // PossessStrengthPower.cs:41
if (power.Owner != Owner.Creature) return;   // SwordSagePower.cs:34
if (amount <= 0m) return;                    // RuinedHelmet.cs:43（判定"获得"）
Flash();
await PowerCmd.Apply<SpellPowerPower>(ctx, Owner.Creature, amount, Owner.Creature, cardSource);
```
- **无状态**，不需要标志/计数器/重置
- ⚠️ 会被 `TemporaryStrengthPower` 重复触发（见 §1.3）；也要决定
  "遗物给的永久力量算不算"（`Brimstone.cs:36` 那种），通常算

### #7 三原素 —— 最难，无单一模板

- **伤害类型识别**：用本 mod 的 `SpellProps.Resolve(props, cardSource)`
  （`src/Scripts/Spells/SpellProps.cs`）—— `ValueProp` 随伤害传递，
  不依赖 `cardSource` 非空，正是为此设计的。本 mod 已有 5 处这么用
  （`AlternatingCurrentPower` / `BurningSoulPower` / `SuperconductPower` /
  `CurrentOverflowPower` / `ChillPower`）
- ⚠️ **原版 300 个遗物里没有任何一个重写 `AfterDamageGiven`**（已 grep 确认），
  模式来自能力（`EnvenomPower.cs:20` 等）
- 判定门（`EnvenomPower.cs:21`）：`dealer == Owner`、`props.IsPoweredAttack()`、
  `result.UnblockedDamage > 0`
- 无实体：`PowerCmd.Apply<IntangiblePower>(ctx, Owner.Creature, 1m, Owner.Creature, null)`；
  `IntangiblePower` 在**敌人回合结束**才减层（`IntangiblePower.cs:66-72`）
  ⇒ 1 层 = "本回合 + 敌人回合"，正是想要的
- 自我禁用：`_disabledUntilTurn = TurnNumber + 2` + **每个钩子体开头自己 guard**（见 §1.1）
- 回合编号：`Owner.PlayerCombatState.TurnNumber`（**从 1 开始**，
  `PlayerCombatState.cs:37`），额外回合会 +1，多人各自独立
  ⇒ 用 `TurnNumber` 而非 `RoundNumber`

---

## 6. 下一步（等指令）

1. 需要用户确认 #5 的语义（"未给过"的记忆是否跨战斗/是否重置）
2. 需要用户确认 #7 的判定门（是否要求 `IsPoweredAttack()`——
   `SpellProps` 标记的伤害可能是 `Unpowered`）
3. 需要用户提供 9 个遗物的定位：稀有度已给，但**中文名/英文类名/图标**是否已备好？
   design.md 给了中文名 + 英文名（如 `Athanor` / `CurseTablet` …）
4. 图标：本 mod 的 `make_new_placeholders.py` 已能生成，
   但 9 个新遗物需要**整体重建图集**（所有遗物一起传进 `build_relic_atlas`）
