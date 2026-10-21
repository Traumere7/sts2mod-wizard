# 巫师 Mod — 五个自定义敌方减益 · 技术可行性评估

> 基于本地反编译源码（游戏 v0.111.0 / BaseLib 3.4.7 / 观者 mod 1.5.10），未使用网络资料。
> 所有引用均给出文件路径 + 行号 + 完整签名，可直接对照。
>
> 现状：`src/Scripts/Powers/` 下已有 `BurnPower.cs`、`ChillPower.cs`、`SpellPowerPower.cs`，
> 以及 `src/Scripts/Spells/{SpellSchool,SpellUtil,ISpellCard}.cs`。本报告在其基础上评估与修正。

---

## 1. 总表

| # | 减益 | 可实现？ | 难度 | 关键 Hook / API | 主要风险 |
|---|---|---|---|---|---|
| 1 | 燃烧 Burn | ✅ 可以 | **中** | `TryModifyPowerAmountReceived`（取最大值）+ `AfterSideTurnEnd` + `CreatureCmd.Damage(Unblockable\|Unpowered, dealer:null)` | 取最大值只能靠"把传入量当作绝对值"实现，与「增量式加层」语义冲突；但 vanilla 无任何增伤钩子能改到它（仅 `ModifyDamageCap` 能压低） |
| 2 | 麻痹 Paralysis | ✅ 可以 | **易** | `ModifyDamageMultiplicative`（照抄 `WeakPower`，0.75→0.5）+ `AfterSideTurnEnd` → `TickDownDuration` | 与 Weak 乘算叠加；只对 `IsPoweredAttack()` 生效 |
| 3 | 电流外溢 Current Overflow | ⚠️ 可以，但有硬伤 | **中** | `AfterDamageReceived` / `BeforeDamageReceived` + `CombatState.GetTeammatesOf` + `RunState.Rng.CombatTargets` | 「雷电伤害」在引擎里**不存在**，只能靠 `cardSource is ISpellCard{Lightning}` 判定 → 任何非卡牌来源的雷电伤害（充能球、能力牌、外溢溅射本身）都判不出来；单敌人战斗完全失效 |
| 4 | 冻结 Freeze | ✅ 可以，**无需 Harmony** | **难（但有解）** | `BeforeSideTurnStart(Player)` 抓 `Monster.NextMove` → `AfterSideTurnStart(Player)` 调 `MonsterModel.SetMoveImmediate(move, forceTransition: true)` | 必须自己处理 `SkipNextDurationTick`（否则 1 层冻结完全无效）；必须绕开 `"STUNNED"` 状态；多人「额外回合」会顺手改写意图 |
| 5 | 寒冷 Chill | ✅ 可以 | **易** | `ModifyDamageMultiplicative` + `cardSource.SchoolOf()` | 只认卡牌来源；层数只是持续时间，倍率固定；与易伤/超导乘算叠加 |

**整体结论：五个全部可在纯 C#（BaseLib + 无 Harmony）范围内实现。**
唯一"机制上不完整"的是 #3（雷电判定）和 #1（"不受任何加成"有两个 vanilla 例外）。

---

## 2. 跨切面 API

### 2.1 PowerModel 怎么定义

**BaseLib 基类**（`reference/decompiled/BaseLib/BaseLib.Abstracts/CustomPowerModel.cs`，全文 22 行）：

```csharp
public abstract class CustomPowerModel : PowerModel, ICustomPower, ICustomModel,
                                         ILocalizationProvider, IHealthBarForecastSource
{
    public virtual string? CustomPackedIconPath => null;      // 小图标（能力条上的）
    public virtual string? CustomBigIconPath => null;         // 大图标（tooltip）
    public virtual string? CustomBigBetaIconPath => null;
    public virtual List<(string, string)>? Localization => null;
    public virtual IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
        => Array.Empty<HealthBarForecastSegment>();
}
```

**强制 override 只有两个**（`MegaCrit.Sts2.Core.Models/PowerModel.cs`）：

| 成员 | 行 | 签名 |
|---|---|---|
| `Type` | 142 | `public abstract PowerType Type { get; }` |
| `StackType` | 236 | `public abstract PowerStackType StackType { get; }` |

**常用可选 override**（同文件）：

| 成员 | 行 | 说明 |
|---|---|---|
| `AllowNegative` | 238 | 默认 false。`StrengthPower` 用 true（降到 0 才移除） |
| `AmountLabelColor` | 224 | 默认：Debuff→红，其余→奶油色。`PoisonPower` 显式改成 `_normalAmountLabelColor` |
| `CanonicalVars` | 344 | `IEnumerable<DynamicVar>`，供本地化 `{DamageIncrease}` 之类使用 |
| `ExtraHoverTips` | 348 | 额外 tooltip |
| `DisplayAmount` | 218 | 显示值 ≠ Amount 时用（`SlowPower` 用它显示百分比） |
| `InstanceType` | 144 | `PowerInstanceType.{None,Instanced,InstancedPerApplier}` |
| `ShouldScaleInMultiplayer` | 342 | 多人层数缩放 |
| `InitInternalData()` | 512 | 私有数据容器（clone 时重置），配 `GetInternalData<T>()`（523） |
| `SkipNextDurationTick` | 246 | 见 §2.6 |
| `BeforeApplied` / `AfterApplied` | 608 / 619 | **仅在首次施加时**触发；加层不触发 |

**图标**：BaseLib 用 Harmony Prefix 接管（`BaseLib.Abstracts/PackedIconPath.cs:10`、`BigIconPath.cs:10`）：
`__result = customPower.CustomPackedIconPath; return __result == null;` → 返回 null 就回退原版路径。

**本地化**两条路，任选：
- **JSON（本项目已在用）**：`src/Wizard/localization/{zhs,eng}/powers.json`，键为 `WIZARD-BURN_POWER.title` / `.description` / `.smartDescription`。
  由 `BaseLib.Patches.Localization/DefaultLoc.cs`（Harmony 补丁 `ModManager.GetModdedLocTables`）按 `res://{modId}/localization/{lang}/{file}` 加载。
  ⚠️ **本项目目前没有 `powers.json`**，新增能力必须建这个文件（两个语言目录都要）。
- **代码内**：override `Localization` 返回 `List<(string key, string value)>?`，由 `BaseLib.Patches.Localization/ModelLocPatch.cs:81-95` 注入，`LocTable` 默认取 `PowerModel.locTable`（= `"powers"`，PowerModel.cs:23）。

**注意**：Power **不需要** `[Pool(...)]`。HANDOVER.md §3.3 那条"每个 Custom\*Model 必须带 Pool"只针对卡牌/遗物/药水（有卡池的模型）。

### 2.2 StackType 有哪些值 / 什么意思

`reference/decompiled/game/MegaCrit.Sts2.Core.Entities.Powers/PowerStackType.cs`（全文 14 行）：

```csharp
public enum PowerStackType
{
    None,
    /// Amount is visible, and must be manually incremented/decremented via an action.
    Counter,
    /// Amount is hidden, and is always 1.
    Single
}
```

**关键结论：枚举里没有"取最大值 / 不可叠加"这一档。**
- `Counter` = 层数可见、需手动加减（**燃烧/麻痹/冻结/寒冷全部用这个**）。
- `Single` = 层数隐藏、恒为 1。
- `None` = 层数隐藏但可变（`DampenPower.cs:22`、`MonologuePower.cs:37-39` 用）。

"持续时间"语义也**不是** StackType 给的，而是 `AfterSideTurnEnd` 里手动调 `PowerCmd.TickDownDuration(this)` 实现（见 §2.6）。

### 2.3 回合结束（敌方回合结束）的 Hook

`reference/decompiled/game/MegaCrit.Sts2.Core.Models/AbstractModel.cs`：

```csharp
// 行 1388
public virtual Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)

// 行 1406  ← 燃烧要用这个
public virtual Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)

// 行 1372 / 1354 / 1425 —— Early / VeryEarly / Late 变体
// 行 1247
public virtual Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)

// 行 1268  ← 注意：没有 choiceContext
public virtual Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
```

派发点（`MegaCrit.Sts2.Core.Hooks/Hook.cs`）：
`Hook.AfterSideTurnEnd(ICombatState, CombatSide, IEnumerable<Creature>)` @1279；
`Hook.BeforeSideTurnEnd(...)` @1244；
`Hook.AfterSideTurnStart(ICombatState, CombatSide, IReadOnlyList<Creature>)` @1175；
`Hook.BeforeSideTurnStart(ICombatState, CombatSide, IReadOnlyList<Creature>)` @1156。

调用现场（`MegaCrit.Sts2.Core.Combat/CombatManager.cs`）：

| 行 | 内容 |
|---|---|
| 1691 / 1696 | `EndEnemyTurnInternal`：`Hook.BeforeSideTurnEnd(state, CurrentSide, enemies)` / `Hook.AfterSideTurnEnd(...)`，`enemies = state.CreaturesOnCurrentSide.ToList()` |
| 1556 / 1771 | 玩家侧同样两个 hook |
| **721** | `Hook.BeforeSideTurnStart` |
| **741-745** | **`foreach (Creature enemy in state.Enemies) enemy.PrepareForNextTurn(state.PlayerCreatures);` ← 意图重掷发生在这里（仅玩家回合且非额外回合）** |
| **783** | `Hook.AfterSideTurnStart` ← **晚于重掷**，冻结靠这个顺序成立 |

⚠️ 官方在 `AbstractModel.cs:1393-1397` 明确写了：
> "Note: Enemy-damaging effects (Bedlam Beacon, The Bomb, etc) should NOT go in here. Put them in BeforeSideTurnEnd instead. Self-damaging effects (Acid Dust, Magic Bomb, etc) can go in here."

但 vanilla 的 `ConstrictPower.AfterSideTurnEnd`（`MegaCrit.Sts2.Core.Models.Powers/ConstrictPower.cs:19-25`）就是**在 `AfterSideTurnEnd` 里对自己造成伤害**：

```csharp
public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
{
    if (participants.Contains(base.Owner))
        await CreatureCmd.Damage(choiceContext, base.Owner, base.Amount, ValueProp.Unpowered, base.Owner);
}
```

这是**燃烧最贴切的原版先例**（"持有者在自己阵营回合结束时自伤"）。若实测发现敌人已死/战斗结束时结算异常，退路是改用 `BeforeSideTurnEnd`（代价：燃烧会在敌人行动**之前**结算）。

### 2.4 无加成伤害怎么做

**命令签名**（`MegaCrit.Sts2.Core.Commands/CreatureCmd.cs:221`）：

```csharp
public static async Task<IEnumerable<DamageResult>> Damage(
    PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props,
    Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
```

**`ValueProp` 全枚举**（`MegaCrit.Sts2.Core.ValueProps/ValueProp.cs`，全文 18 行）：

```csharp
[Flags]
public enum ValueProp
{
    Unblockable = 2,   // HP loss like Poison
    Unpowered   = 4,   // Damage from relics, potions, and powers
    Move        = 8,   // Attack damage from Attack cards and Enemy creatures attacking
    SkipHurtAnim= 0x10
}
```
（没有 `None = 0`，默认值就是 0。）

**判定真身**（`ValuePropExtensions.cs:5`）：

```csharp
public static bool IsPoweredAttack(this ValueProp props)
    => props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
```

**PoisonPower 全文**（`MegaCrit.Sts2.Core.Models.Powers/PoisonPower.cs`，82 行）——核心两段：

```csharp
public override PowerType Type => PowerType.Debuff;
public override PowerStackType StackType => PowerStackType.Counter;
public override Color AmountLabelColor => PowerModel._normalAmountLabelColor;

// 行 42-53：伤害预览
public int CalculateTotalDamageNextTurn()
{
    decimal num = 0m;
    int num2 = Math.Min(base.Amount, TriggerCount);
    for (int i = 0; i < num2; i++)
    {
        decimal damage = base.Amount - i;
        damage = Hook.ModifyDamage(base.Owner.CombatState.RunState, base.Owner.CombatState, base.Owner, null,
                                   damage, ValueProp.Unblockable | ValueProp.Unpowered, null, null,
                                   ModifyDamageHookType.All, CardPreviewMode.None, out IEnumerable<AbstractModel> _);
        num += damage;
    }
    return (int)num;
}

// 行 55-61：在【拥有者所在阵营】回合【开始】时触发
public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
{
    if (participants.Contains(base.Owner)) await Trigger();
}

// 行 66-81：真正的伤害
public async Task Trigger()
{
    int iterations = TriggerCount;
    for (int i = 0; i < iterations; i++)
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), base.Owner, base.Amount,
                                 ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        if (base.Owner.IsAlive) await PowerCmd.Decrement(this);
        else await Cmd.CustomScaledWait(0.1f, 0.25f);
    }
}
```

- `ValueProp.Unblockable` → `Creature.DamageBlockInternal`（Creature.cs:431-436）里 `num = props.HasFlag(Unblockable) ? 0m : ...`，完全不消耗格挡。
- `Unpowered`（无 `Move`）→ `IsPoweredAttack()` 为 false → **力量、易伤、虚弱、缓慢、被包围、法术增益全部返回原值**（逐个核对过：`StrengthPower.cs:22`、`VulnerablePower.cs:34`、`WeakPower.cs:31`、`SlowPower.cs:46`、`SurroundedPower.cs:48-51`、`TrackingPower.cs:21`、`ColossusPower.cs:33` …）。
- `dealer = null` → `SurroundedPower` 直接 `if (dealer == null) return 1m;`；吸血/击杀回能类需要 dealer 的效果也不会触发。

### 2.5 从卡牌施加能力

**引擎层**（`MegaCrit.Sts2.Core.Commands/PowerCmd.cs`）：

```csharp
// 行 36
public static async Task<IReadOnlyList<T>> Apply<T>(PlayerChoiceContext choiceContext, IEnumerable<Creature>? targets,
        decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel

// 行 71  ← 单目标
public static async Task<T?> Apply<T>(PlayerChoiceContext choiceContext, Creature target,
        decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
```

内部流程（`PowerCmd.cs:71-93`）：先 `FindExistingInstanceForStacking`（169）找已有实例；
- 找不到 → `powerModel.ToMutable()` → 新实例走 `Apply(...)`（105）；
- 找到 → `ModifyAmount(ctx, power, amount, ...)`（219）—— **注意：此时 `amount` 被当作"层数增量"，最终 `newAmount = power.Amount + (int)modifiedOffset`（第 240 行）**。

**BaseLib 糖**（`BaseLib.Utils/CommonActions.cs`，推荐在卡里用）：

```csharp
// 行 400
public static async Task<T?> Apply<T>(PlayerChoiceContext context, Creature target, CardModel? card,
                                      decimal amount, bool silent = false) where T : PowerModel
// 行 387：层数从 card.DynamicVars.Power<T>().BaseValue 取
public static async Task<T?> Apply<T>(PlayerChoiceContext context, Creature target, CardModel card, bool silent = false)
// 行 374：多目标，层数从 dynVarSource 取
public static async Task<IReadOnlyList<T>> Apply<T>(PlayerChoiceContext context, IEnumerable<Creature> targets,
                                      DynamicVarSource dynVarSource, bool silent = false)
// 行 512
public static async Task<IReadOnlyList<T>> Apply<T>(PlayerChoiceContext ctx, CardModel card, CardPlay? cardPlay, bool silent = false)
// 行 415：给自己
public static async Task<T?> ApplySelf<T>(PlayerChoiceContext context, CardModel card, decimal amount, bool silent = false)
```

**观者实战写法**（`Watcher.Code.Cards.Common/SashWhip.cs:48`）：

```csharp
await CommonActions.Apply<WeakPower>(ctx, cardPlay.Target, this, false);
```

**先例**：
```csharp
// Watcher.Code.Powers/WaveOfTheHandPower.cs:33
await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), this.CombatState.HittableEnemies, amount2, this.Owner, null, false);
```

### 2.6 读 / 改层数

| API | 位置 | 说明 |
|---|---|---|
| `public int Amount { get; private set; }` | `PowerModel.cs:187` | **setter 私有** |
| `public void SetAmount(int amount, bool silent = false)` | `PowerModel.cs:542` | 公开；内部 `Owner.InvokePowerModified` → **未挂载的实例会 NRE** |
| `public int AmountOnTurnStart { get; set; }` | `PowerModel.cs:205` | 回合开始时快照（`Creature.BeforeTurnStart`，Creature.cs:678-684） |
| `public virtual int DisplayAmount => Amount;` | `PowerModel.cs:218` | |
| `public async Task<int> PowerCmd.ModifyAmount(PlayerChoiceContext, PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)` | `PowerCmd.cs:219` | 返回新层数 |
| `PowerCmd.Decrement(PowerModel)` | `PowerCmd.cs:184` | = `ModifyAmount(-1)` |
| **`PowerCmd.TickDownDuration(PowerModel)`** | `PowerCmd.cs:194` | 时长型减益专用，尊重 `SkipNextDurationTick` |
| `PowerCmd.Remove(PowerModel?)` | `PowerCmd.cs:291` | |
| `Creature.GetPower<T>()` / `HasPower<T>()` / `GetPowerAmount<T>()` | `Creature.cs:571 / 561 / 596` | `GetPowerAmount<T>() => GetPower<T>()?.Amount ?? 0` |

`SkipNextDurationTick`（`PowerModel.cs:246`）语义见 `PowerCmd.cs:194-204`：

```csharp
public static async Task TickDownDuration(PowerModel power)
{
    if (power.SkipNextDurationTick) power.SkipNextDurationTick = false;
    else await Decrement(power);
}
```
`PowerCmd.Apply` 只在 **`target.Side == CombatSide.Player && power.Type == Debuff`** 时自动置 true（`PowerCmd.cs:148-151`）。
**敌人身上的减益不会自动跳过第一跳** —— 冻结必须自己置位（见 §5）。

`Creature.ApplyPowerInternal`（Creature.cs:606-613）在同类型同 `InstanceType == None` 已有实例时**抛异常**，所以同种能力永远只会有一个实例（`Instanced` 除外）。

### 2.7 受伤倍率（ModifyDamageMultiplicative 约定）

`AbstractModel.cs` 三个钩子：

```csharp
// 行 1579 —— 加算，返回"要加多少"，默认 0
public virtual decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) => 0m;

// 行 1613 —— 乘算，返回"乘多少"，默认 1  ← 易伤/虚弱/寒冷都用这个
public virtual decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) => 1m;

// 行 1595 —— 单次伤害上限，默认 decimal.MaxValue
public virtual decimal ModifyDamageCap(Creature? target, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) => decimal.MaxValue;
```

派发：`Hook.ModifyDamage(...)`（Hook.cs:1495）→ `ModifyDamageInternal`（Hook.cs:2519）。
执行顺序（2519-2566）：**先遍历所有 listener 做 Additive（累加），再遍历一次做 Multiplicative（连乘），最后 Cap（取最小值）**。
乘算**互相相乘**，不取最大：易伤 1.5 × 寒冷 1.5 = 2.25。

**`VulnerablePower` 全文**（67 行）核心：

```csharp
public override PowerType Type => PowerType.Debuff;
public override PowerStackType StackType => PowerStackType.Counter;
protected override IEnumerable<DynamicVar> CanonicalVars => [ new DynamicVar("DamageIncrease", 1.5m) ];

public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
{
    if (target != base.Owner) return 1m;
    if (!props.IsPoweredAttack()) return 1m;              // ← 只对"正规攻击"生效
    decimal num = base.DynamicVars["DamageIncrease"].BaseValue;
    if (dealer != null) {
        var paperPhrog = dealer.Player?.GetRelic<PaperPhrog>();
        if (paperPhrog != null) num = paperPhrog.ModifyVulnerableMultiplier(target, num, props, dealer, cardSource);
        var cruelty = dealer.GetPower<CrueltyPower>() ?? dealer.PetOwner?.Creature.GetPower<CrueltyPower>();
        if (cruelty != null) num = cruelty.ModifyVulnerableMultiplier(target, num, props, dealer, cardSource);
    }
    var debilitate = target.GetPower<DebilitatePower>();
    if (debilitate != null) num = debilitate.ModifyVulnerableMultiplier(target, num, props, dealer, cardSource);
    return num;
}

public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
{
    if (side == CombatSide.Enemy) await PowerCmd.TickDownDuration(this);   // ← 时长语义
}
```

**约定**：倍率**不随层数变化**，层数 = 持续回合数；`AmountLabelColor` 默认红色（Debuff）。

⚠️ **注意易伤与力量用的是同一个判定 `IsPoweredAttack()`**，所以设计文档里"法术伤害享受易伤、不享受力量"**无法只靠 ValueProp 实现** —— 见 §8。

### 2.8 施加能力时改层数（Burn 需要的钩子）

`AbstractModel.cs:2093`：

```csharp
/// <param name="canonicalPower">Canonical power that will be applied.</param>
/// <param name="target">Creature who the power will be applied to.</param>
/// <param name="amount">Original amount of the power to be applied.</param>
/// <param name="modifiedAmount">New amount of the power to be applied.</param>
public virtual bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
        decimal amount, Creature? applier, out decimal modifiedAmount)
{ modifiedAmount = amount; return false; }
```

**语义（很关键，容易踩坑）**：
- 派发：`Hook.ModifyPowerAmountReceived(combatState, power, target, amount, applier, out modifiers)`（Hook.cs:1925）。
  它遍历 `combatState.IterateHookListeners()`，**每个 listener 依次改写 `num`**（不是取最值）。
- 调用点 1 —— 新实例：`PowerCmd.cs:131`，`amount` = 要施加的层数，`canonicalPower` = **尚未挂到 owner 上的新实例**（`Owner == null`）。
- 调用点 2 —— 已有实例：`PowerCmd.cs:238`，`amount` = **层数增量（offset）**，`canonicalPower` = **目标身上那个活的实例**（来自 `FindExistingInstanceForStacking`）。
  随后 `int newAmount = power.Amount + (int)modifiedOffset;`（`PowerCmd.cs:240`）。

相关钩子：
```csharp
// AbstractModel.cs:1057 —— 只通知，不能改
public virtual Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
// AbstractModel.cs:1077
public virtual Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
// AbstractModel.cs:948 / 958
public virtual Task AfterModifyingPowerAmountReceived(PowerModel power)   // ArtifactPower 在这里扣自己
public virtual Task AfterModifyingPowerAmountGiven(PowerModel power)
// AbstractModel.cs:1845 / 1861 —— ModifyPowerAmountGivenAdditive / Multiplicative（施加方侧）
```

**vanilla 先例**：
```csharp
// ArtifactPower.cs:17 —— 把减益层数直接改成 0
public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? _, out decimal modifiedAmount)
{
    if (target != base.Owner) { modifiedAmount = amount; return false; }
    if (canonicalPower.GetTypeForAmount(amount) != PowerType.Debuff) { modifiedAmount = amount; return false; }
    if (!canonicalPower.IsVisible) { modifiedAmount = amount; return false; }
    modifiedAmount = 0m; return true;
}
// RuinedHelmet.cs:32 —— 力量施加翻倍（modifiedAmount *= 2m）
```

`should Remove DueToAmount` 判定：`PowerModel.ShouldRemoveDueToAmount()`（PowerModel.cs:478）：
非 `AllowNegative` 的能力 `< 0` 时移除。

### 2.9 伤害后 / 伤害前钩子

```csharp
// AbstractModel.cs:593
public virtual Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)

// AbstractModel.cs:608 —— 拿到的是"改完倍率、扣格挡之前"的 amount
public virtual Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)

// AbstractModel.cs:623  ← 电流外溢要用这个
public virtual Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)

// AbstractModel.cs:640
public virtual Task AfterDamageReceivedLate(...)   // 同参数，晚于上一者
```

派发：`Hook.AfterDamageReceived`（Hook.cs:429，走 `runState.IterateHookListeners(combatState)`，
**所有能力/遗物/怪物都能收到**，不只是承受者的）、`Hook.BeforeDamageReceived`（Hook.cs:415）、`Hook.AfterDamageGiven`（Hook.cs:401）。

`DamageResult`（`MegaCrit.Sts2.Core.Entities.Creatures/DamageResult.cs`）只有：
`Receiver`、`Props`、`BlockedDamage`、`UnblockedDamage`、`OverkillDamage`、`TotalDamage`、`WasBlockBroken`、`WasFullyBlocked`、`WasTargetKilled`。
**没有原始伤害值，也没有"元素/流派"字段。**

⚠️ **`AfterDamageReceived` 在"这一击打死目标"时不会被调用**（`CreatureCmd.cs:413-420`：`if (!WasTargetKilled || !IsDead) await Hook.AfterDamageReceived(...); else killedCreatures.Add(...)`）。
`AfterDamageGiven`（411 行）不受此限制。

### 2.10 随机选敌 & 队友

```csharp
// ICombatState.cs:23/28/90/174/179/184
IReadOnlyList<Creature> Allies { get; }
IReadOnlyList<Creature> Enemies { get; }
IReadOnlyList<Creature> HittableEnemies { get; }
IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side);
IReadOnlyList<Creature> GetOpponentsOf(Creature creature);
IReadOnlyList<Creature> GetTeammatesOf(Creature creature);

// Creature.cs:286
public bool IsHittable { get; }   // 活着 && Hook.ShouldAllowHitting

// RunRngSet.cs:72 / 77
public Rng CombatTargets { get; }
public Rng MonsterAi { get; }
// Rng.cs:289
public T? NextItem<T>(IEnumerable<T> items)
```
vanilla 用法（`LightningOrb.cs:52`）：
```csharp
base.Owner.RunState.Rng.CombatTargets.NextItem(list)
```

---

## 3. 燃烧 Burn

**可行性：可以。难度：中。关键点全在"取最大值"。**

### 3.1 取最大值怎么实现

vanilla 没有 `ModifyPowerAmountReceivedAdditive/Multiplicative` 这种"加法/乘法"式的施加侧钩子，
只有 §2.8 那个**顺序改写**的 `TryModifyPowerAmountReceived`。所以实现思路是：
**在能力自己身上 override 它，把传入量当作"期望的绝对层数"，返回 `max(0, 期望值 - 当前值)`。**

这样可以工作，因为**所有巫师侧的燃烧来源都应走 `PowerCmd.Apply<BurnPower>`（绝对值语义）**。

> ⚠️ **重要约束**：这条实现同时意味着 `PowerCmd.ModifyAmount(burn, +n, ...)`（增量式加层）会被曲解 ——
> 例如"每回合开始给 3 层燃烧，然后这个数值 +1"。若该来源用 `PowerCmd.Apply` 传绝对值就没问题；
> 若用 `ModifyAmount` 传 +1，会被 `max` 吃掉。**所以：凡是给燃烧，一律用 `PowerCmd.Apply`。**

### 3.2 Skeleton

`src/Scripts/Powers/BurnPower.cs`（替换现有实现）：

```csharp
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 燃烧：不可叠加，重复施加取层数较高者。
/// 敌人回合结束时，受到等同于层数的伤害，且不受任何增伤影响。
/// </summary>
public sealed class BurnPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 燃烧不吃易伤类红色高亮，跟毒一样用常态色
    public override Godot.Color AmountLabelColor => _normalAmountLabelColor;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/burn_power.png";
    public override string? CustomBigIconPath   => "res://Wizard/images/powers/burn_power.png";

    /// <summary>
    /// 「取较高值」的实现。
    /// 走 PowerCmd.Apply 时，amount 是"本次想施加到的层数"（绝对值）；
    /// 走 ModifyAmount 时，amount 是"增量" —— 本 mod 约定禁止对燃烧用 ModifyAmount。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;

        // 只在"目标身上已有燃烧、且正在给它加层"这条路径上生效。
        // 首次施加时 canonicalPower.Owner 还是 null，落到 return false，按原值施加，正确。
        if (canonicalPower is not BurnPower burn) return false;
        if (target != burn.Owner) return false;
        if (amount <= 0m) return false;                 // 负值 / 0 不介入

        int desired = (int)amount;                      // 期望达到的层数
        int delta   = System.Math.Max(0, desired - burn.Amount);
        if (delta == (int)amount) return false;         // 本来就是要加这么多，不介入

        modifiedAmount = delta;                         // 可能要压低（含压到 0）
        return true;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;      // 只在"自己阵营"的回合结束结算
        if (Amount <= 0 || Owner.IsDead) return;

        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,        // dealer = null：不归属任何伤害来源
            null,        // cardSource = null：不吃附魔
            null);       // cardPlay = null
    }
}
```

本地化（**新建** `src/Wizard/localization/{zhs,eng}/powers.json`）：

```json
{
  "WIZARD-BURN_POWER.title": "燃烧",
  "WIZARD-BURN_POWER.description": "敌人回合结束时，受到等同于燃烧层数的伤害。不可叠加，重复施加时取层数较高者。",
  "WIZARD-BURN_POWER.smartDescription": "敌人回合结束时，受到[blue]{Amount}[/blue]点伤害。不可叠加。"
}
```

### 3.3 边界 / 例外

| 场景 | 行为 |
|---|---|
| 首次施加 5 层（身上没有） | `canonicalPower.Owner == null` → 钩子返回 false → 正常施加 5 层 ✔ |
| 身上 3 层，再施加 5 层 | 钩子返回 `5-3=2` → 新层数 = 3+2 = 5 ✔ |
| 身上 7 层，再施加 5 层 | 钩子返回 0 → 新层数 = 7+0 = 7 ✔（`ModifyAmount` 返回 7 ≠ 0，能力不会被误删） |
| 身上 7 层，再施加 -3 层 | `amount <= 0` → 不介入 → 变成 4 层。**这是 bug 还是特性取决于设计**；若"燃烧不可被减层"，改成 `return true; modifiedAmount = 0;` |
| 敌人有神器 Artifact | `ArtifactPower.TryModifyPowerAmountReceived` 把层数改成 0 + `AfterModifyingPowerAmountReceived` 扣神器。与燃烧钩子的先后取决于 `creature.Powers` 顺序（= 施加顺序） |
| 无加成是否真成立 | **增伤类全部成立**（均以 `IsPoweredAttack()` 或 `dealer==null` 为门槛）。**两个例外**：<br>① `ModifyDamageCap` —— `IntangiblePower.cs:47`/`HardToKillPower.cs:15` 无条件按 `target == Owner` 截断，燃烧伤害也会被压到 1 / Amount；<br>② `ModifyHpLost*` —— `HardenedShellPower.cs:32`（按 `Amount` 截断该回合累计受伤）也会削燃烧。两者都只**压低**不放大，可接受，但写文案时别写"无视任何减伤" |
| 格挡 | `Unblockable` → `DamageBlockInternal` 恒返回 0，不消耗格挡 ✔ |
| 结算时机 | `AfterSideTurnEnd(Enemy)` 在**敌人全部行动完之后**。所以燃烧打不死"即将出手"的敌人，只会补刀。若要"敌人出手前烧死它"，改用 `BeforeSideTurnEnd`（注意 `CombatManager.cs:1691` 是其调用点，早于 1696） |
| 反伤 / 死亡触发 | `dealer = null` → `CreatureCmd.cs:344` 会播放 Hit 动画（`receiver != dealer`），但 `AfterDamageGiven` 拿到的 dealer 是 null；`ExtraFields.DamageDealt` 不增加（405 行）；历史记录 `DamageReceivedEntry.Dealer == null` |
| 燃烧致死后 | `AfterDamageReceived` 不会为此触发（不影响燃烧本身），但会影响别的"受伤时"能力 |
| 多人 | 敌人身上的减益，`ShouldScaleInMultiplayer` 不需要 |

### 3.4 与毒的差异（用户问的对比）

| | 毒 PoisonPower | 燃烧 BurnPower |
|---|---|---|
| 触发时机 | `AfterSideTurnStart`（拥有者阵营回合**开始**） | `AfterSideTurnEnd`（拥有者阵营回合**结束**） |
| 伤害 props | `Unblockable \| Unpowered` | 同 |
| dealer | `null` | `null` |
| 结算后层数 | **递减 1**（`PowerCmd.Decrement`） | **不变**（燃烧是持续 debuff，不递减） |
| 层数语义 | 既是伤害量也是持续回合数 | 只是伤害量 |
| 叠加 | 相加 | 取最大值（自定义） |
| 额外机制 | `AccelerantPower` 加触发次数 | 无 |

⚠️ **现有 `src/Scripts/Powers/BurnPower.cs` 就是"毒改成回合结束、且不递减"** —— 结构没问题，只缺 §3.2 的取最大值钩子和 `dealer/cardSource/cardPlay` 显式传 null。

---

## 4. 麻痹 Paralysis

**可行性：可以。难度：易（照抄 WeakPower）。**

### 4.1 机制

"基于层数让敌人在若干回合内伤害减半" —— 这正是原版虚弱，只是 0.75 → 0.5。

**`WeakPower` 全文**（`MegaCrit.Sts2.Core.Models.Powers/WeakPower.cs`，56 行）：

```csharp
public sealed class WeakPower : PowerModel
{
    private const string _damageDecrease = "DamageDecrease";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [ new DynamicVar("DamageDecrease", 0.75m) ];

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
            Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != base.Owner) return 1m;             // ← 只看"造成伤害的是不是自己"
        if (!props.IsPoweredAttack()) return 1m;         // ← 只削弱正规攻击
        decimal num = base.DynamicVars["DamageDecrease"].BaseValue;
        var paperKrane = target?.Player?.GetRelic<PaperKrane>();
        if (paperKrane != null) num = paperKrane.ModifyWeakMultiplier(target, num, props, dealer, cardSource);
        var debilitate = dealer.GetPower<DebilitatePower>();
        if (debilitate != null) num = debilitate.ModifyWeakMultiplier(dealer, num, props, dealer, cardSource);
        return num;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy) await PowerCmd.TickDownDuration(this);
    }
}
```

"改成 -50%"= 把 `DamageDecrease` 从 `0.75m` 改成 `0.5m`。

**敌方攻击确实走 `ValueProp.Move`**：`AttackCommand.cs:127` `public ValueProp DamageProps { get; private set; } = ValueProp.Move;`，
且 `AttackCommand.cs:257 FromMonster(MonsterModel monster)`（`Unpowered()` 是显式调用才加，349 行）。所以 `IsPoweredAttack() == true`，麻痹对敌人攻击生效。

### 4.2 Skeleton

```csharp
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 麻痹：层数 = 持续回合数；期间该敌人造成的伤害减半。
/// 敌人回合结束时失去 1 层。
/// </summary>
public sealed class ParalysisPower : CustomPowerModel
{
    private const string DamageDecreaseKey = "DamageDecrease";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/paralysis_power.png";
    public override string? CustomBigIconPath   => "res://Wizard/images/powers/paralysis_power.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [ new DynamicVar(DamageDecreaseKey, 0.5m) ];

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
            Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        return DynamicVars[DamageDecreaseKey].BaseValue;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && participants.Contains(Owner))
            await PowerCmd.TickDownDuration(this);
    }
}
```

### 4.3 边界

- **与虚弱叠加是乘算**：虚弱 0.75 × 麻痹 0.5 = 0.375（`ModifyDamageInternal` 连乘，Hook.cs:2535-2546）。设计上要确认这是想要的。
- **`side == Enemy` 就够**：`EndEnemyTurnInternal` 的 `participants` 就是敌方全体，所以敌人身上的麻痹一定在敌方回合结束时跳。`participants.Contains(Owner)` 是更保险的写法（对多个敌人）。
- **`IsPoweredAttack()` 的副作用**：敌人的**非攻击伤害**（例如某种"直接造成 X 点伤害"的 `Unpowered` 技能）不吃麻痹。原版虚弱也是这个行为，一致。
- **`PaperKrane`（纸鹤）只改虚弱**，不影响麻痹 —— 符合"新增独立减益"的预期。
- **`DebilitatePower` 只改虚弱**（`WeakPower.cs:41-45`）。如果希望"虚弱类"统一被它影响，需要在自己的能力里也调一次；默认不调更干净。
- **能力自身不应该在层数 0 时继续生效**：`ShouldRemoveDueToAmount()` 保证 `Amount < 0` 即移除，所以一个能力不会以 0 层留在身上。
- **`SkipNextDurationTick`**：玩家对敌人施加时不会自动置位（`PowerCmd.cs:148` 只对玩家侧 debuff 生效），所以"麻痹 2" = 影响敌人接下来 2 个回合的输出。与虚弱一致。

---

## 5. 冻结 Freeze

**可行性：可以，且无需 Harmony。难度：难（时序必须精确）。**

### 5.1 敌人意图系统调研结果

| 组件 | 位置 | 关键成员 |
|---|---|---|
| `MonsterModel` | `MegaCrit.Sts2.Core.Models/MonsterModel.cs` | `public const string stunnedMoveId = "STUNNED";`(34)<br>`public MonsterMoveStateMachine? MoveStateMachine { get; private set; }`(223)<br>`public MoveState NextMove { get; private set; }`(240)<br>`public void RollMove(IEnumerable<Creature> targets)`(416)<br>**`public void SetMoveImmediate(MoveState state, bool forceTransition = false)`(421)** |
| `MonsterMoveStateMachine` | `MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine/MonsterMoveStateMachine.cs` | `public Dictionary<string, MonsterState> States { get; }`(16)<br>`public List<MonsterState> StateLog { get; }`(18)<br>`public MoveState RollMove(IEnumerable<Creature>, Creature owner, Rng rng)`(34)<br>`public void ForceCurrentState(MonsterState state)`(44) |
| `MoveState : MonsterState` | 同目录 `MoveState.cs` | `public IReadOnlyList<AbstractIntent> Intents { get; private set; }`(17)<br>`public string StateId { get; }`(19)<br>`public bool MustPerformOnceBeforeTransitioning { get; set; }`(21)<br>`public string? FollowUpStateId { get; init; }`(23)<br>`public override bool CanTransitionAway`(27) |
| 选招 | `Creature.PrepareForNextTurn`（Creature.cs:547）→ `Monster.RollMove` | 调用点 **`CombatManager.cs:743`**（玩家回合开始、非额外回合） |
| 出招 | `MonsterModel.PerformMove()`（435） | 用 `NextMove`，之后 `MoveStateMachine.OnMovePerformed(move)` |
| 意图显示 | `NCreature.UpdateIntent`（`Nodes.Combat/NCreature.cs:617-636`） | 读 **`Entity.Monster.NextMove.Intents`**；`RefreshIntents()`(663) |

**「让敌人下回合重复本回合意图」能不能直接做？—— 能，但必须用 hook 组合，不能用单一 API。**

可行的关键事实链：
1. `CombatManager.cs:721` `Hook.BeforeSideTurnStart(...)` **早于** 741-745 的 `PrepareForNextTurn`/`RollMove`。
   所以 `BeforeSideTurnStart(Player)` 里读到的 `Monster.NextMove` **仍是上一回合已出手的那一招**。✔ 抓取点。
2. `CombatManager.cs:783` `Hook.AfterSideTurnStart(...)` **晚于** 重掷。
   所以 `AfterSideTurnStart(Player)` 里调 `SetMoveImmediate(抓到的那招, forceTransition: true)` 可以把新意图顶回旧的。✔ 覆写点。
3. `SetMoveImmediate` 内部 `MoveStateMachine.ForceCurrentState(state)` 会把状态机的 `_currentState` 也拨回去，
   于是下一回合 `RollMove` 从**同一个招**重新推进 → 状态机不会被卡死，冻结结束后自然衔接。✔
   （`MonsterModel.cs:421-433`，`NCreature.RefreshIntents()` 会刷新显示的意图）

**没有** `SleepPower`/`StunPower` 之外的现成"强制意图"能力。已排查：
- `AsleepPower`（Lagavulin）：唤醒时 `CreatureCmd.Stun(Owner, WakeUpMove, "SLASH_MOVE")`。
- `SlumberPower`（SlumberingBeetle）：`AfterDamageReceived` 递减 + `CreatureCmd.Stun`。
- `BurrowedPower`（Tunneler）：`AfterBlockBroken` → `CreatureCmd.Stun`。
- `IllusionPower`（Parafright）：**这是 vanilla 里唯一一个"由能力设置非 STUNNED 移动状态"的先例**：
  `IllusionPower.cs:83-88` 构造一个 `new MoveState("REVIVE_MOVE", ReviveMove, new HealIntent()) { FollowUpStateId = ..., MustPerformOnceBeforeTransitioning = true }` 然后 `base.Owner.Monster.SetMoveImmediate(state)`。
- `ReattachPower.cs:62`：`base.Owner.Monster.SetMoveImmediate(decimillipedeSegment.DeadState)`。
- `HibernatePower` 与意图无关（只给寒冰充能球读的标记）。

**所以：不需要 Harmony 补丁。** 唯一需要 patch 的假设场景是"必须阻止 `RollMove` 本身发生"（比如要阻止 RNG 消耗），
那才需要 patch `MonsterModel.RollMove(IEnumerable<Creature>)` 或 `MonsterMoveStateMachine.FindNextMoveState`（private）。
按下面的实现则完全不需要。

### 5.2 Skeleton

```csharp
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 冻结：使敌人下一个回合重复本回合的意图。层数 = 重复的回合数。
/// 敌人回合结束时失去 1 层。
///
/// 时序（CombatManager.BeginSideTurn 内）：
///   721  Hook.BeforeSideTurnStart   ← 此刻 NextMove 仍是"刚出手的那招"，在这里抓
///   743  enemy.PrepareForNextTurn   ← 内部 RollMove 重掷意图
///   783  Hook.AfterSideTurnStart    ← 在这里把意图顶回去
/// </summary>
public sealed class FreezePower : CustomPowerModel
{
    /// <summary>本回合要强制重复的招式。只在内存里，不参与存档。</summary>
    private MoveState? _frozenMove;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/freeze_power.png";
    public override string? CustomBigIconPath   => "res://Wizard/images/powers/freeze_power.png";

    /// <summary>
    /// 首次施加时跳过"第一次时长跳"，否则 1 层冻结会在生效之前就归零。
    /// PowerCmd.Apply 只会给【玩家侧】debuff 自动置位（PowerCmd.cs:148），敌人侧必须自己来。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SkipNextDurationTick = true;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // 只关心玩家回合开始（敌人意图就是这时重掷的）
        if (side != CombatSide.Player) return Task.CompletedTask;
        _frozenMove = null;

        if (Amount <= 0 || Owner.IsDead) return Task.CompletedTask;
        var monster = Owner.Monster;
        var move = monster?.NextMove;
        if (monster == null || move == null) return Task.CompletedTask;

        // 不要冻结眩晕：STUNNED 是 Creature.StunInternal 临时 new 出来的 MoveState，
        // 它没有注册进 MoveStateMachine.States，冻结它没有意义。
        if (move.StateId == MonsterModel.stunnedMoveId) return Task.CompletedTask;

        _frozenMove = move;
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player) return Task.CompletedTask;

        var move = _frozenMove;
        _frozenMove = null;
        if (move == null || Amount <= 0 || Owner.IsDead) return Task.CompletedTask;

        var monster = Owner.Monster;
        if (monster?.MoveStateMachine == null) return Task.CompletedTask;

        // RollMove 之后 NextMove 已经换人；"STUNNED" 表示这回合敌人本来就该被跳过，别顶掉它。
        if (monster.NextMove.StateId == MonsterModel.stunnedMoveId) return Task.CompletedTask;
        if (ReferenceEquals(monster.NextMove, move)) return Task.CompletedTask;   // 没重掷（额外回合等），无需处理

        // forceTransition: true —— 忽略 NextMove.CanTransitionAway 的限制
        monster.SetMoveImmediate(move, forceTransition: true);
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy) return;
        if (!participants.Contains(Owner)) return;
        await PowerCmd.TickDownDuration(this);
    }
}
```

### 5.3 时序推演（验证过）

设敌人在玩家回合 N 显示的意图是 M。玩家在回合 N 打出"冻结 1"。

| 时刻 | 事件 | 冻结层数 |
|---|---|---|
| 玩家回合 N | 施加冻结 1，`SkipNextDurationTick = true` | 1 |
| 敌方回合 N | 执行 M（本来就会执行） | 1 |
| 敌方回合 N 结束 | `AfterSideTurnEnd(Enemy)` → `TickDownDuration` → **跳过**，只清 flag | 1 |
| 玩家回合 N+1 开始 | `BeforeSideTurnStart(Player)`：`_frozenMove = M` | 1 |
| （同回合）`PrepareForNextTurn` | `RollMove` → `NextMove = M'` | 1 |
| （同回合）`AfterSideTurnStart` | `SetMoveImmediate(M, forceTransition:true)` → 显示/待执行又变回 **M** | 1 |
| 敌方回合 N+1 | 执行 **M**（重复成功） | 1 |
| 敌方回合 N+1 结束 | `TickDownDuration` → 1 → 0，能力移除 | 0 |
| 玩家回合 N+2 | 无冻结，`RollMove` 正常 | — |

**层数 = 重复的回合数**，与设计文档"敌人回合结束时失去 1 层"一致。✔

### 5.4 边界 / 已知缺陷

| 问题 | 说明 / 处理 |
|---|---|
| **1 层冻结完全无效（必须处理）** | 如果不自己置 `SkipNextDurationTick = true`，`AfterSideTurnEnd(Enemy)` 会在**玩家回合 N 后的那个敌方回合**就把它跳到 0，冻结从未生效。这是最容易踩的坑 |
| **重复施加不会重置 flag** | `AfterApplied` 只在首次施加时触发（`PowerModel.cs:613-622` 文档）。所以"冻结 1 → 冻结 1"变成 2 层时，`SkipNextDurationTick` 保持已消费状态，行为正确（2 层 = 2 个重复回合） |
| **`STUNNED` 状态** | `Creature.StunInternal`（Creature.cs:525-545）**临时 `new MoveState("STUNNED", ...)`**，`Id` 是 `"STUNNED"` 且**没有注册进 `MoveStateMachine.States`**。所以绝不能按 ID 去 `States[...]` 查（会 `KeyNotFoundException`），必须持有对象引用。代码里用 `MonsterModel.stunnedMoveId`（= `"STUNNED"`，public const）双向过滤 |
| **冻结期间状态机仍在推进** | `ForceCurrentState(M)` 把 `_currentState` 拨回 M，下一回合 `RollMove` 从 M 重新推进。对 `RandomBranchState`（`RandomBranchState.cs:115-128`）会**多消耗一次 RNG**（`RunRng.MonsterAi`）。多人下所有客户端跑同样代码，不会不同步，但随机序列与不冻结时不同。对 `ConditionalBranchState` 无副作用 |
| **额外回合（多人）会误伤** | `CombatManager.cs:739` `if (!isExtraPlayerTurn)` 才 `PrepareForNextTurn`。额外玩家回合里 `NextMove` 不变，我们的 `ReferenceEquals` 短路会跳过（已在代码里防住）。但 `_frozenMove` 仍会在额外回合被重置 —— 已在 `BeforeSideTurnStart` 开头无条件 `_frozenMove = null`，行为是"额外回合不参与冻结"，可接受 |
| **敌人使用 `MustPerformOnceBeforeTransitioning` 的招** | `SetMoveImmediate(..., forceTransition: true)` 会绕过该限制。若该招本应只出一次（例如"蓄力"后必须接"大招"），强制重复会让它连续蓄力。**需要按怪物白名单/黑名单处理**，或者接受这个行为 |
| **BOSS 的脚本化阶段转换** | 有些 BOSS 通过 `SetMoveImmediate` 在特定 HP 阈值切换阶段；冻结只是顶掉玩家回合开始时的 `RollMove` 结果，阶段切换（在其它钩子里）仍会覆盖冻结。表现为"冻结偶尔无效"，属于可接受 |
| **不攻击的意图** | 冻结对 buff/debuff 类意图同样有效（意图就是 `MoveState.Intents`） |
| **显示刷新** | `SetMoveImmediate` 会 `TaskHelper.RunSafely(creatureNode.RefreshIntents())`；无 Godot 节点（headless 测试）时为 null 安全 |
| **`MoveStateMachine == null`** | `MonsterModel.MoveStateMachine` 有 `private set`，`ResetStateMachine()` 会置 null。已判空 |
| **死亡/离场** | `Owner.IsDead` 已判；能力随 owner 死亡被移除（`ShouldPowerBeRemovedAfterOwnerDeath` 默认 true） |
| **和麻痹/寒冷同时存在** | 三者完全正交（一个改意图、一个改输出、一个改受伤），无冲突 |

**结论：能强制"下回合重复同一意图"，纯 C# 可实现，不需要 Harmony。**
需要 Harmony 的唯一情形是"必须阻止 `RollMove` 消耗 RNG"或"必须阻止状态机推进"，
那要 patch `MonsterModel.RollMove(IEnumerable<Creature>)`（MonsterModel.cs:416）或
`MonsterMoveStateMachine.FindNextMoveState`（MonsterMoveStateMachine.cs:54，private）。

---

## 6. 电流外溢 Current Overflow

**可行性：可以，但"雷电伤害"在引擎层面不存在，必须靠 mod 自己的标记 + 有硬伤。难度：中。**

### 6.1 「雷电伤害」怎么判定

**引擎没有伤害流派/元素概念。** `ValueProp` 只有 `Unblockable / Unpowered / Move / SkipHurtAnim`（§2.4），
`DamageResult` 里也没有元素字段（§2.9）。

现有 mod 的判定手段（`src/Scripts/Spells/SpellUtil.cs`）：

```csharp
public static bool IsSpell(this CardModel? card) => card is ISpellCard;
public static SpellSchool SchoolOf(this CardModel? card) => card is ISpellCard spell ? spell.School : SpellSchool.None;
public static bool IsLightning(this CardModel? card) => card.SchoolOf() == SpellSchool.Lightning;
```

即：**`cardSource` 是实现了 `ISpellCard` 且 `School == SpellSchool.Lightning` 的卡牌**。

⚠️ **必须在 `Damage` 调用时传 `cardSource`**：`AttackCommand.cs:669` 已经自动传了
`cardSource: ModelSource as CardModel`，而 `ModelSource` 由 `FromCard(card, cardPlay)`（214）设置；
BaseLib 的 `CommonActions.CardAttack(...)` 会设置它。所以只要用标准攻击流程，`cardSource` 就有值。✔

**判不出来的来源**（设计上要明确接受）：
- 充能球（`LightningOrb.cs:62` 传 `cardSource: null`）
- 任何能力牌/遗物/药水造成的伤害
- 电流外溢自己产生的溅射（**必须人为排除，否则无限递归**）

若以后要支持这些，需要一个共享的"雷电伤害上下文"（例如 `AsyncLocal<SpellSchool?>` 之类），
或者一个 Harmony patch 在 `CreatureCmd.Damage` 上注入元素标记。**不建议现在做。**

### 6.2 「基础倍率 50%」取哪个数

`AfterDamageReceived` 只给 `DamageResult`，**没有原始伤害**。三个可选口径：

| 口径 | 取法 | 优点 | 缺点 |
|---|---|---|---|
| **A. 卡牌基础伤害（推荐）** | `cardSource.DynamicVars.Damage.BaseValue * 0.5m` | 与"基础倍率"的字面意思一致；不受增益影响，数值稳定可预期 | 需要卡有 `"Damage"` 这个 `DynamicVar`（`DynamicVarSet.Damage` 会直接索引，缺键会抛异常 → 用 `ContainsKey("Damage")` 防） |
| B. 改后、扣格挡前的实际伤害 | 在 `BeforeDamageReceived` 里缓存 `amount`，在 `AfterDamageReceived` 里用 | 一定拿得到，兼容任何来源 | 会被法术增益/寒冷/易伤放大 → 溅射跟着膨胀，可能超模 |
| C. 实际掉血 | `result.UnblockedDamage * 0.5m` | 最简单 | 会被格挡/减伤吃掉，且高血量时数值离谱 |

**建议用 A**，并在设计文档里写明"溅射 = 该法术卡基础伤害 × 50%"。

### 6.3 Skeleton

```csharp
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 电流外溢：每当此敌人受到雷电伤害，随机另一个敌人受到「该法术基础伤害 50%」的伤害。
/// 每层独立结算（N 层 = N 次溅射，可重复命中同一目标）。
/// </summary>
public sealed class CurrentOverflowPower : CustomPowerModel
{
    private const decimal SplashRatio = 0.5m;

    /// <summary>防重入：溅射自身不能再次触发任何电流外溢。</summary>
    private bool _resolving;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/current_overflow_power.png";
    public override string? CustomBigIconPath   => "res://Wizard/images/powers/current_overflow_power.png";

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_resolving) return;
        if (target != Owner) return;
        if (Owner.IsDead) return;
        if (Amount <= 0) return;

        // 只认本 mod 的雷系法术卡
        if (cardSource.SchoolOf() != SpellSchool.Lightning) return;

        // 口径 A：卡牌基础伤害 × 50%（向下取整，至少 1）
        if (cardSource.DynamicVars is null || !cardSource.DynamicVars.ContainsKey("Damage")) return;
        decimal splash = System.Math.Floor(cardSource.DynamicVars.Damage.BaseValue * SplashRatio);
        if (splash < 1m) return;

        _resolving = true;
        try
        {
            for (int i = 0; i < Amount; i++)
            {
                var others = Owner.CombatState.GetTeammatesOf(Owner)
                                  .Where(c => c != Owner && c.IsHittable)
                                  .ToList();
                if (others.Count == 0) break;             // 单敌人战斗：无"另一敌人"

                Creature? pick = Owner.CombatState.RunState.Rng.CombatTargets.NextItem(others);
                if (pick == null) break;

                await CreatureCmd.Damage(
                    choiceContext,
                    pick,
                    splash,
                    ValueProp.Unpowered,   // 不加 Move → 不触发 IsPoweredAttack 类效果
                    null,                  // dealer = null
                    null,                  // cardSource = null ← 关键：保证不会被自己再触发
                    null);
            }
        }
        finally
        {
            _resolving = false;
        }
    }
}
```

### 6.4 边界 / 已知缺陷

| 问题 | 说明 |
|---|---|
| **单敌人战斗完全失效** | "随机**另一**敌人"在只有一个敌人时无目标。BOSS 战、大部分精英战都是单敌人 → 这个减益在那些战斗里是废牌。设计文档里的「环流」卡（"电流外溢现在可以对敌人自身结算"）正是为此，需要给它一个开关（例如一个 `bool IncludeSelf`，用 `GetInternalData` 或一个独立的标记能力承载） |
| **无限递归** | 溅射必须 **`cardSource: null` + `props` 不含 `Move`**。这样它既不会被判定成"雷电伤害"（`SchoolOf(null) == None`），也不会触发 `IsPoweredAttack()` 类效果。代码里还加了 `_resolving` 兜底 |
| **致死一击不出溅射** | `AfterDamageReceived` 在击杀时**不触发**（`CreatureCmd.cs:413-420`）。若希望"打死也溅射"，得改用 `AfterDamageGiven`（Hook.cs:401，不受击杀限制），但那个钩子的语义是"造成伤害"而非"受到伤害"，需要额外判 `target side == enemy` |
| **同一张 AOE 法术打多个敌人** | 每个敌人各自触发自己身上的电流外溢 —— 符合"每次敌人受到雷电伤害时"的字面语义，但 N 个敌人叠加会爆量，设计上要确认 |
| **"每层独立结算"的两种做法** | ①（上面）`Counter` + for 循环，N 次独立随机；②`InstanceType = Instanced`（`PowerInstanceType.cs:7-11`，`TheBombPower` 就是），每次施加产生独立实例，每个实例 `Amount == 1`，天然"各自结算"。②的显示是多个图标，①是一个带层数的图标。**推荐 ①** |
| **重复命中同一目标** | 上面每轮重新取 `others` 但**不做去重**，所以 3 层可能全打同一个敌人（"随机另一敌人"的字面解）。若要"尽量分散"，需要在循环里移除已命中者 |
| **敌人中途死亡** | 每轮重新计算 `others` 并检查 `IsHittable`，已死敌人不会被选中；`CreatureCmd.Damage` 对已死目标也会提前返回（CreatureCmd.cs:278-281） |
| **重入安全** | `Hook.AfterDamageReceived` 里的 `IterateHookListeners` 返回的是**预先物化好的 `List<AbstractModel>`**（`CombatState.cs:411-417`），在钩子里再造成伤害不会破坏迭代。安全 ✔ |
| **多人** | 随机数用 `RunState.Rng.CombatTargets`（`RunRngSet.cs:72`），与 `LightningOrb` 同源，多人同步 ✔ |
| **寒冷加成** | 溅射 `cardSource: null` → 不吃寒冷。若设计希望溅射也吃寒冷，就要给个"伤害时携带流派"的机制 —— 目前没有，只能让溅射调用 `CreatureCmd.Damage` 时传 `cardSource`（但那会同时触发本能力的递归！）。**建议：溅射不吃任何加成**，并把文案写成"造成 X 点伤害"而不是"雷电伤害" |

---

## 7. 寒冷 Chill

**可行性：可以。难度：易。现状代码基本可用，但要修 3 个点。**

### 7.1 倍率约定

见 §2.7。要点：
- `ModifyDamageMultiplicative` 返回**乘数**，默认 1；多个来源**连乘**（Hook.cs:2535-2546）。
- `VulnerablePower` 用 `DynamicVars["DamageIncrease"].BaseValue = 1.5m`，并在 helper 里允许遗物/能力（纸蛙、残酷）二次调整。
- 层数**不乘倍率**，只当持续时间。

### 7.2 Skeleton（修正版）

```csharp
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Spells;

namespace Wizard.Scripts.Powers;

/// <summary>
/// 寒冷：提升该敌人受到的火焰与雷电伤害（基础 ×1.5）。层数 = 持续回合数。
/// 敌人回合结束时失去 1 层。
/// </summary>
public sealed class ChillPower : CustomPowerModel
{
    private const string DamageIncreaseKey = "DamageIncrease";

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string? CustomPackedIconPath => "res://Wizard/images/powers/chill_power.png";
    public override string? CustomBigIconPath   => "res://Wizard/images/powers/chill_power.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [ new DynamicVar(DamageIncreaseKey, 1.5m) ];

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
            Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner) return 1m;

        // 【修正 1】只对"真实伤害"生效：非攻击且无 Move 的来源（例如燃烧结算）不吃寒冷。
        //          注意不能用 IsPoweredAttack()，否则火/雷法术如果将来标了 Unpowered 就失效。
        if (!props.HasFlag(ValueProp.Move)) return 1m;

        // 【修正 2】来源必须是本 mod 的火系 / 雷系法术卡。
        //          cardSource 为 null 时不成立（SchoolOf(null) == None），天然安全。
        SpellSchool school = cardSource.SchoolOf();
        if (school != SpellSchool.Fire && school != SpellSchool.Lightning) return 1m;

        return DynamicVars[DamageIncreaseKey].BaseValue;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 【修正 3】加上 participants 判空，多敌人时每层只跳一次
        if (side != CombatSide.Enemy || !participants.Contains(Owner)) return;
        await PowerCmd.TickDownDuration(this);
    }
}
```

**与现有实现（`src/Scripts/Powers/ChillPower.cs`）的差异**：
1. 缺 `props` 判定 —— 现有代码只查 `cardSource`。理论上一个"造成 X 点伤害"的非 `Move` 法术也会被加成，
   而 `ValueProp.Move` 才是原版"攻击伤害"的标记（`AttackCommand` 默认就是 `Move`）。加上更严谨。
2. `AfterSideTurnEnd` 缺 `participants.Contains(Owner)`。`side == Enemy` 时 participants 就是全体敌人，
   多敌人混战时每个敌人的寒冷都会在同一个 hook 里各跳一次 —— 数量上是对的，但显式判 owner 更清晰。
   （**注意**：这里和原版 `VulnerablePower` 写法不同 —— 原版就是只判 `side == Enemy`。两者等价，可选。）
3. `CanonicalVars` 可以顺手加 `ExtraHoverTips` 提示火焰/雷电，非必需。

### 7.3 边界

| 问题 | 说明 |
|---|---|
| **易伤 × 寒冷 = 2.25** | 连乘。这是原版约定（易伤 × 残酷 × 纸蛙也是连乘）。要和设计确认 |
| **超导 × 寒冷** | 设计文档里「超导」是"寒冷状态的敌人受到的雷电伤害提升 50%（75%）"。若它也做成 `ModifyDamageMultiplicative`，1.5 × 1.5 = 2.25，1.75 × 1.5 = 2.625。**建议超导不要用倍率**，改成读 `ChillPower` 的倍率做**加算**（`1.5 + 0.5 = 2.0`），或者直接在 `ChillPower` 里加一个 `dynamicValue` 项。原版 `CrueltyPower`/`PaperPhrog` 正是"读取并替换倍率"的模式（`VulnerablePower.cs:41-50`），可以照抄 |
| **层数只是时长** | 3 层寒冷 = 3 回合 ×1.5，**不是** ×1.5³。`DisplayAmount` 默认显示 `Amount`（3），玩家可能误以为叠得越多越强。要么写清文案，要么把 `DisplayAmount` 改成显示百分比（参考 `SlowPower.DisplayAmount`，SlowPower.cs:24） |
| **卡牌预览** | `DamageVar.UpdateCardPreview`（DamageVar.cs:28-46）会带 `cardSource` 调 `Hook.ModifyDamage(..., ModifyDamageHookType.All, previewMode, ...)`，所以**卡面数字会自动显示加成后的值** ✔。但这些是 `PreviewValue`，实际结算仍用 `BaseValue` |
| **`Target == null` 的预览** | 多目标预览时 `target` 为 null → `target != Owner` → 返回 1，预览显示未加成值。原版易伤同样如此 |
| **玩家侧** | 若将来把寒冷也给玩家（例如自伤流派），`ModifyDamageMultiplicative` 里应加 `dealer` 判断区分敌我，参考 `WeakPower` |
| **幻影敌人 / 免伤** | `IllusionPower.ShouldAllowHitting` 返回 false 时 `IsHittable == false`，但寒冷是目标侧受伤倍率，不受影响 |

---

## 8. 最高风险项

### 🥇 最高风险：#4 冻结（Freeze）—— 时序正确性

**为什么最危险**：它依赖 4 个互不相关的事实同时成立
（`BeforeSideTurnStart` 早于 `RollMove`、`AfterSideTurnStart` 晚于 `RollMove`、
`SetMoveImmediate(forceTransition:true)` 未被 `CanTransitionAway` 拦下、
`ForceCurrentState` 能把状态机拨回去），
而且**失败模式是静默的**：「1 层冻结什么都不做」你不会看到报错，只会看到意图没变。

**已在 §5.4 列出的具体崩点**：
1. 忘记 `SkipNextDurationTick = true` → **1 层冻结 100% 无效**（最容易踩）。
2. 按 ID 去 `MoveStateMachine.States["STUNNED"]` 查 → `KeyNotFoundException`（STUNNED 未注册）。
3. 不判 `STUNNED` 就强制 → 会**取消敌人的眩晕**，等于给敌人解控。
4. 多人额外回合 → 会顺手改写意图（已用 `_frozenMove = null` + `ReferenceEquals` 短路规避）。

**具体退路（按代价从低到高）**：

- **退路 A（推荐，0 成本）**：把"冻结"重新定义为**一次性减益**——`StackType = Single`，
  只重复 1 个回合，`AfterSideTurnEnd` 里直接 `PowerCmd.Remove(this)`。这样彻底绕开时长/跳过/多次捕获的所有坑，
  只保留"抓 + 顶"两步。设计文档改成"下回合重复本回合意图"，去掉"层数"。
- **退路 B（低风险，改机制）**：改成**"延迟一回合"** —— 抓取当前意图，让敌人这回合不出手（`CreatureCmd.Stun` 语义），
  下回合再执行。vanilla 有现成实现（`CreatureCmd.Stun(creature, stunMove, nextMoveId)`，CreatureCmd.cs:936），
  代价是玩家看到的意图是"眩晕"而不是原意图。
- **退路 C（需要 Harmony，成本最高）**：patch `MonsterModel.RollMove(IEnumerable<Creature>)`（MonsterModel.cs:416）
  或 `MonsterMoveStateMachine.FindNextMoveState`（MonsterMoveStateMachine.cs:54，private），
  在重掷时直接返回上一招。这是"真正的冻结"，但引入版本脆弱性（signature 随游戏更新变），
  且该方法是 `private`，需要 `AccessTools`/`HarmonyPatch` 反射。**只有 A/B 都不可接受时才用。**

### 🥈 次高风险：#3 电流外溢 —— 单敌人战斗完全失效

不是"实现不了"，是"实现了也几乎没用"。BOSS 战/精英战多为单敌人，
"随机**另一**敌人"无目标。**这是设计问题不是技术问题**，建议：
把「环流」的开关**默认打开**（允许溅射到自己），或把文案改成"随机一名敌人（可以是自己）"。
明确决定这一点比任何代码优化都重要。

### 🥉 第三：#1 燃烧 —— "不受任何加成"有两个真实例外

`IntangiblePower`（无敌）、`HardToKillPower`（`ModifyDamageCap`）、`HardenedShellPower`（`ModifyHpLost*`）
**都不看 ValueProp**，会实实在在削掉燃烧伤害。
这不是 bug，是原版设计（它们是"减伤/上限"，不是"加成"）。
文案写"不受任何加成"可以，别写"无视一切减伤"。
另外 `TryModifyPowerAmountReceived` 的"最大值"语义与"增量式加层"冲突 —— 必须在团队内立规矩：**燃烧只用 `PowerCmd.Apply`**。

---

## 9. 设计文档里的一个隐藏矛盾（顺带发现）

`design.md` 第 4 行：**"法术伤害……享受易伤加成，不享受力量加成。"**

但引擎里 `VulnerablePower.cs:34` 和 `StrengthPower.cs:22` 用的是**同一个判定** `props.IsPoweredAttack()`
（= `Move && !Unpowered`，ValuePropExtensions.cs:5）。所以：

- 让法术走标准 `ValueProp.Move` → **易伤生效，力量也生效**（矛盾）。
- 让法术走 `Move | Unpowered` → 力量不生效，**易伤也不生效**（矛盾）。
- 让法术走 `Unpowered` → 两者都不生效（矛盾）。

**唯一解**：`ValueProp.Move` + 一个 Harmony 补丁，在 `StrengthPower.ModifyDamageAdditive` 里
（`MegaCrit.Sts2.Core.Models.Powers/StrengthPower.cs:16`）对 `cardSource.IsSpell()` 返回 0。
参考现有写法：

```csharp
[HarmonyPatch(typeof(StrengthPower), nameof(StrengthPower.ModifyDamageAdditive))]
internal static class StrengthPower_NoSpellPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel? cardSource, ref decimal __result)
    {
        if (cardSource.IsSpell()) __result = 0m;
    }
}
```

实际影响面很小（巫师只能通过药水/遗物/事件拿到原版力量），但**如果要严格实现设计文档，这个补丁是必须的**。
项目已有 `[ModInitializer]` + Harmony 扫描（`src/Scripts/Entry.cs`），加补丁零额外基建。

---

## 10. 观者 Mod 先例对照表

`reference/decompiled/Watcher/Watcher.Code.Powers/`（25 个）。直接可抄的：

| 巫师需求 | 观者先例 | 文件 : 行 | 抄什么 |
|---|---|---|---|
| **回合结束造成伤害** | `OmegaPower` | `OmegaPower.cs:21-38` | `AfterSideTurnEnd` + `Owner.Side != side` 提前返回 + `CreatureCmd.Damage(ctx, targets, Amount, Unpowered, Owner)` |
| **回合结束自伤 + 时长递减** | `BlasphemerPower` | `BlasphemerPower.cs:19-27` | `CreatureCmd.Damage(ctx, Owner, 99999m, Unpowered, Owner)` + `PowerCmd.TickDownDuration(this)` |
| **时长型减益递减** | `SimmeringRagePower` | `SimmeringRagePower.cs:26-33` | `BeforeHandDraw` 里 `PowerCmd.TickDownDuration(this)`（换成 `AfterSideTurnEnd`） |
| **受伤时递减 / 受伤时触发** | `PlatedArmorPower` | `PlatedArmorPower.cs:27-33` | `AfterDamageReceived` + `target == Owner && result.UnblockedDamage != 0` |
| **能力基类 + 图标路径约定** | `WatcherPowerModel` | `Abstract/WatcherPowerModel.cs`（全文 13 行） | 用 `Id.Entry.RemovePrefix().ToLowerInvariant()` + `.PowerImagePath()` 推导图标路径（巫师可写成 `WizardPowerModel` 基类） |
| **能力向敌方群体施加 debuff** | `WaveOfTheHandPower` | `WaveOfTheHandPower.cs:26-36` | `PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), CombatState.HittableEnemies, amount, Owner, null, false)` |
| **卡牌施加 debuff** | `SashWhip` / `CrushJoints` / `Indignation` | `Cards.Common/SashWhip.cs:48` 等 | `await CommonActions.Apply<WeakPower>(ctx, cardPlay.Target, this, false);` |
| **卡牌声明能力层数动态变量** | `SashWhip` 构造 | `SashWhip.cs:36-41` | `((ConstructedCardModel)this).WithPower<WeakPower>(1, 1);` （施加减益的标准声明方式） |
| **能力在受伤时闪一下** | `PlatedArmorPower.ForceFlash` / `WaveOfTheHandPower.AfterSideTurnEnd` | — | `this.Flash()`；观者大量用 `BeforeSideTurnEnd` 里 `((PowerModel)this).Flash()` |
| **多目标 end-of-turn 伤害** | `OmegaPower` | 同上 | `CombatState.GetOpponentsOf(Owner).Where(c => c.IsAlive)` |

⚠️ **观者的源码是从编译后的 DLL 反编译的**（注意 `(PowerType)1`、`(ValueProp)4`、`((PowerModel)this)` 这类痕迹）。
抄结构可以，**抄语法不行** —— 巫师的源码是真正的 C#，要写成 `PowerType.Debuff` / `ValueProp.Unpowered` / `Owner`。

**观者没有覆盖到的两块**：
1. **没有任何"取最大值"式的叠加控制能力**（25 个能力全是标准的 Counter/Single）→ 燃烧的 §3.2 钩子是新东西。
2. **没有任何操控敌人意图的能力**（唯一的意图读取是 `Cards.Uncommon/FearNoEvil.cs:37-47`，只读 `monster.NextMove.Intents.Any(...)` 判断"是否攻击"）→ 冻结是新东西。但它读 `Monster.NextMove.Intents` 的方式可以作为"意图结构"的参考。

---

## 11. 建议的落地顺序

1. **麻痹、寒冷** —— 照抄 `WeakPower`/`VulnerablePower`，半天搞定，先验证能力注册与本地化链路（**新建 `powers.json`**、**新建 `Wizard/images/powers/` 目录 + 图标**）。
2. **燃烧** —— 现有实现只差取最大值钩子；同时把 `dealer/cardSource/cardPlay` 显式传 `null`。
3. **电流外溢** —— 先决定"单敌人是否允许溅射自己"，再写。
4. **冻结** —— 最后做，先按 §5.2 实现，用 `OmegaPower` 之外唯一没先例的那一步（`SetMoveImmediate`）单独验证：
   - 测试用例 1：1 层冻结 → 敌人下回合意图是否与上回合相同（**必须**在日志里打 `NextMove.StateId`）。
   - 测试用例 2：对 `LagavulinMatriarch` 施加冻结，确认没有覆盖掉 `STUNNED`。
   - 测试用例 3：连续 3 回合观察状态机是否被卡死（应正常推进）。

---

## 12. 明确"做不到 / 不确定"的清单

| 项 | 结论 |
|---|---|
| 引擎层面识别"雷电伤害" | ❌ **不存在**。只能靠 `cardSource is ISpellCard{Lightning}`。充能球 / 能力 / 遗物 / 药水来源判不出来 |
| 燃烧"不受任何加成" | ⚠️ **增伤类完全成立**；但 `ModifyDamageCap`（无敌/难杀）与 `ModifyHpLost*`（硬化外壳）会压低它 |
| 敌人意图"真正冻结"（阻止 `RollMove`） | ❌ 不 patch 做不到。但**不阻止也能达到设计效果**（§5.2），无需 patch |
| 一次伤害事件里拿到"原始伤害值" | ❌ `DamageResult` 没有；只能从 `cardSource.DynamicVars.Damage.BaseValue` 反推，或在 `BeforeDamageReceived` 缓存 |
| "法术吃易伤但不吃力量" | ❌ 纯 ValueProp 做不到（§9），需要 1 个 Harmony postfix |
| 让溅射也吃寒冷 / 被判定成雷电伤害 | ❌ 目前没有"伤害携带流派"的机制。硬做会无限递归。**建议不做** |
| `PowerStackType` 里找"取最大值"档 | ❌ 只有 `None / Counter / Single` |
| 冻结对 `MustPerformOnceBeforeTransitioning` 招式 | ⚠️ 会被强制重复（`forceTransition: true`），行为可能不理想，需要按怪物白名单处理 |
| 冻结期间的 RNG 消耗 | ⚠️ 每次重复会多消耗一次 `RunRng.MonsterAi`，随机序列与不冻结时不同（多人下仍同步） |

---

*评估完成。所有引用均来自本地反编译源码；未使用任何网络资料。*
