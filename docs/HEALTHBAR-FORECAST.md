# 血条预测覆盖层（Poison / Doom 那一段）—— 实现机制调查

> 2026-10-16 调查。目标：让「燃烧」也能在怪物血条上染出一段颜色表示"将要扣除的血"。
> **本文只记录已查证的事实，每条带 文件:行号。未施工。**

---

## 0. 结论速览

**能做，而且有官方扩展点，不需要改 `health_bar.tscn`、不需要注入节点。**

- 原版自己用**专用节点**（`%PoisonForeground` / `%DoomForeground`）实现，那两个能力是硬编码的；
- 但 **BaseLib 提供了一个通用接口 `IHealthBarForecastSource`**，任何**能力**只要重写一个方法
  就能渲染同款色段 —— 由 BaseLib 的 Harmony 补丁在运行时自动建容器/建节点/摆位置。
- ⚠️ **关键**：我们的 `BurnPower : CustomPowerModel`，而
  `CustomPowerModel` **已经实现了这个接口**
  （`BaseLib.Abstracts/CustomPowerModel.cs:8`），且默认实现返回空。
  ⇒ **只要重写一个方法即可，连接口都不用显式声明。**

---

## 1. 原版是怎么做的（硬编码，仅供理解）

### 1.1 节点结构（`scenes/combat/health_bar.tscn`）

层级（都在 `HpForegroundContainer/Mask` 之下，`Mask` 负责裁剪）：

```
HpBarContainer                        (Control)
├─ HpBackground        NinePatchRect   深青底
├─ BlockOutline        NinePatchRect
├─ HpForegroundContainer               clip_children = 1
│  └─ Mask             NinePatchRect   ← 裁剪区，所有"血量段"都在它下面
│     ├─ HpMiddleground NinePatchRect  橙 (1, 0.498, 0)      —— 掉血动画的"残影"
│     ├─ PoisonForeground NinePatchRect 绿 (0.4745, 0.7529, 0.2353)
│     ├─ HpForeground   NinePatchRect   红 (0.8, 0.047, 0.047)  ← 当前血量
│     └─ DoomForeground NinePatchRect   紫 (0.3529, 0.2588, 0.6471) + doom_bar.gdshader
├─ HpLabel             Label
├─ InfinityTex / BlockContainer ...
```

三层（poison / hp / doom）**共用同一张 `health_bar_fill.png` 九宫格**，
只是 `self_modulate` 颜色不同。注意 `DoomForeground` 额外挂了
`doom_bar.gdshader`（带噪声 + 渐变），所以灾厄那段看起来是"流动的紫"。

### 1.2 坐标约定

右边缘是 `OffsetRight == 0`，**向左为负**。
所有段的 `Control.OffsetLeft / OffsetRight` 都是相对 Mask 的偏移。

### 1.3 核心算法（`NHealthBar.RefreshForeground`，`NHealthBar.cs:417-504`）

```csharp
float offsetRight = GetFgWidth(_creature.CurrentHp) - MaxFgWidth;   // 当前血量段的左边界
_hpForeground.OffsetRight = offsetRight;

int doom   = _creature.GetPowerAmount<DoomPower>();
int poison = _creature.GetPower<PoisonPower>()?.CalculateTotalDamageNextTurn() ?? 0;

// ── 毒：从右边往左涂 ──
if (hasPoison && poison > 0) {
    if (IsPoisonLethal(poison)) {            // 毒致死 ⇒ 整条都是绿的，红条隐藏
        _poisonForeground.OffsetLeft  = 0f;
        _poisonForeground.OffsetRight = offsetRight;
        _hpForeground.Visible = false;
    } else {
        float fgWidth = GetFgWidth(_creature.CurrentHp - poison);   // ★ 扣完毒之后的血量宽度
        _hpForeground.OffsetRight = fgWidth - MaxFgWidth;           //   红条缩短
        _poisonForeground.OffsetLeft  = Math.Max(0f, fgWidth - patchMarginLeft);  // 绿段左界
        _poisonForeground.OffsetRight = offsetRight;                // 绿段右界 = 原血量左界
    }
}

// ── 灾厄：从左往右涂（DoomForeground 在 HpForeground 之上，先算少的一段）──
if (hasDoom && doom > 0) {
    float num2 = GetFgWidth(doom) - MaxFgWidth;
    if (IsDoomLethal(doom, poison)) {
        ... _hpForeground.Visible = false;
    } else {
        _doomForeground.OffsetRight = Math.Min(0f, num2 + patchMarginRight);
        _hpForeground.Visible = true;
    }
}
```
`GetFgWidth(amount)`（`NHealthBar.cs:631-644`）：
```csharp
float val = (float)amount / (float)_creature.MaxHp * maxFgWidth;
return Math.Max(val, (_creature.CurrentHp > 0) ? 12f : 0f);   // 至少 12px，避免看不见
```

### 1.4 "致死"的高亮

`IsPoisonLethal(poison)` = `poison >= CurrentHp`（`NHealthBar.cs:603-610`）；
`IsDoomLethal(doom, poison)` = `doom >= CurrentHp - poison`（`:616-623`，**把毒的伤害也算进去**，
因为毒先结算、血量可能掉进灾厄范围）。致死时 `RefreshText` 会把血量数字改成特殊颜色
（`NHealthBar.cs:562-580`）。

---

## 2. ✅ 我们要用的：BaseLib 的 `IHealthBarForecastSource`

### 2.1 接口（`BaseLib.Hooks/IHealthBarForecastSource.cs`）

```csharp
namespace BaseLib.Hooks;

public interface IHealthBarForecastSource
{
    IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context);
}
```

### 2.2 ⚠️ 我们的 `BurnPower` **天生就实现了它**

```csharp
// BaseLib.Abstracts/CustomPowerModel.cs:8
public abstract class CustomPowerModel
    : PowerModel, ICustomPower, ICustomModel, ILocalizationProvider, IHealthBarForecastSource

// 默认实现（同文件，返回空）
public virtual IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    => Array.Empty<HealthBarForecastSegment>();
```
而本 mod 的 `BurnPower : CustomPowerModel`（`src/Scripts/Powers/BurnPower.cs:56`）。
⇒ **只需 `public override` 这一个方法，不需要注册、不需要改场景。**

### 2.3 自动发现（`BaseLib.Patches.UI/HealthBarForecastRegistry.cs:60-67`）

```csharp
foreach (IHealthBarForecastSource item in creature.Powers.OfType<IHealthBarForecastSource>())
    AppendTypedSegments(item, item.GetType().FullName ?? ..., context, num++, list, "creature power");
```
⇒ **挂在生物身上的能力会被自动扫到**（`creature.Powers`）。
只有"非能力的来源"才需要 `HealthBarForecastRegistry.Register(...)` 显式注册。

### 2.4 渲染由 BaseLib 的补丁接管（`HealthBarForecastPatch.cs`）

```csharp
[HarmonyPatch(typeof(NHealthBar), "RefreshForeground")]     // :66
[HarmonyPostfix] static void RefreshForegroundPostfix(NHealthBar __instance) => RunOverlayRefresh(__instance);

[HarmonyPatch(typeof(NHealthBar), "RefreshMiddleground")]   // :85
[HarmonyPatch(typeof(NHealthBar), "RefreshText")]           // :92
[HarmonyPatch(typeof(NHealthBar), "SetHpBarContainerSizeWithOffsetsImmediately")]  // :75
```
它把状态存在 `SpireField<NHealthBar, HealthBarForecastUiState?>` 里，
**自己创建 `RightContainer` / `LeftContainer` / `OvercapContainer` 和分段
`NinePatchRect`**（以原版 `PoisonForeground` 等作模板，见 `HealthBarForecastUiState`
的 `rightTemplate` / `leftTemplate`）—— 所以**我们完全不碰场景文件**。

### 2.5 `FromRight` 的语义（`HealthBarForecastPatch.cs:151-202`）

对 `Direction == FromRight` 的分段，按 `(Order, SequenceOrder)` 排序后**依次从中扣血**：
```csharp
int num3 = max(0, CurrentHp - poisonDamage);     // :150  ★ 起点已扣掉毒
float offsetRight = hpForeground.OffsetRight;
foreach (seg in fromRight) {
    if (num3 <= 0) break;
    int take = Math.Min(seg.Amount, num3);        // :167 被剩余血量截断
    num3 -= take;
    seg.OffsetLeft  = (num3 > 0) ? Math.Max(0f, GetFgWidth(num3) - patchMarginLeft) : 0f;  // :178
    seg.OffsetRight = GetFgWidth(amount = num3 + take) - maxFgWidth;                       // :179
    ...
}
if (num4 > 0) {
    if (num3 > 0) { hpForeground.Visible = true; hpForeground.OffsetRight = GetFgWidth(num3) - maxFgWidth; }
    else          { hpForeground.Visible = false; }        // 盖满 ⇒ 红条隐藏
    doomForeground.OffsetRight = Math.Min(doomForeground.OffsetRight, hpForeground.OffsetRight);
}
```
⇒ **多个 FromRight 来源会像"层叠切片"一样依次从右向左排**，各自被剩余血量截断，
盖满时红条自动隐藏 —— 这正是我们要的"右侧染成一段"。

### 2.6 可用类型一览

```csharp
// BaseLib.Hooks/HealthBarForecastSegment.cs —— 只读 record struct
public readonly record struct HealthBarForecastSegment(
    int Amount, Color Color, HealthBarForecastDirection Direction, int Order,
    Material? OverlayMaterial, Color? OverlaySelfModulate = null,
    HealthBarForecastLeftOriginLayout LeftOriginLayout = Chained,
    int LeftExclusiveZGroup = 0, bool AffectsHpLabel = true);

// BaseLib.Hooks/HealthBarForecastDirection.cs
public enum HealthBarForecastDirection { FromRight, FromLeft, OutwardFromCurrentHp, InwardFromMaxHp }

// BaseLib.Hooks/HealthBarForecastLeftOriginLayout.cs
public enum HealthBarForecastLeftOriginLayout { Chained, OverlapFromOrigin }

// BaseLib.Hooks/HealthBarForecastContext.cs —— 只读 record struct
public readonly record struct HealthBarForecastContext(Creature Creature)
{
    public CombatStateWrapper? CombatState { get; }
    public CombatSide? CurrentSide { get; }
}
```

**两种写法：**

```csharp
// (a) 极简：Single —— 已经内置了 amount <= 0 就返回空列表的保护
public static IEnumerable<HealthBarForecastSegment> Single(
    int amount, Color color, HealthBarForecastDirection direction, int order = 0)

// (b) 链式：先把"到时点会掉多少"排好序，BaseLib 会按真实结算顺序染色
HealthBarForecasts.FromRight(context, color)
    .AtSideTurnStart(CombatSide.Enemy, burnAmount)     // HealthBarForecastLaneBuilder.cs:47
    .ThenFromLeft(nextColor)                            // :65
    .Build();                                           // :77
```
`HealthBarForecastOrder.ForSideTurnStart(creature, triggerSide)`
（`HealthBarForecastOrder.cs`）返回 **1 = 本回合就结算 / 0 = 要等到对方回合**，
正是给 `AtSideTurnStart` 排序用的。

---

## 3. 事件链：改了层数血条会自己刷新吗

订阅情况（**这是本调查里唯一有风险的一点**）：

| 事件 | 订阅者 | 效果 |
|---|---|---|
| `Creature.PowerApplied`（`Creature.cs:335`） | `NPowerContainer.cs:91` | ✅ 能力**新增**时会刷新 |
| `Creature.PowerRemoved`（`:341`） | `NPowerContainer.cs:92` | ✅ |
| `Creature.PowerIncreased`（`:337`） | `NCreature.cs:542` → `OnPowerIncreased`（`:800`） | ⚠️ 只播 `NPowerAppliedVfx`，**没有**调 `RefreshValues` |
| `Creature.PowerDecreased`（`:339`） | 只有 `CombatStateTracker.cs:98` / `NOrbVfx` / `NMultiplayerPlayerState` | ⚠️ 同上 |
| `Creature.CurrentHpChanged` / `BlockChanged` | `NCreatureStateDisplay.cs:255-256` | ✅ |

`RefreshValues` 的调用点（`NCreatureStateDisplay.cs`）：`:248`（SetCreature）、
`:272`（SetCreatureBounds）、`:286`（OnCombatStateChanged）、`:404`（Block 变化）。

⚠️ **推论（需实测确认）**：把`燃烧层数从 2 加到 10`时，走的是 `PowerIncreased`，
按上表**不保证**立刻重绘 —— 可能要到下一次血量/格挡变化、悬停、或回合切换才更新。
原版的毒大概也有同样的滞后（`CalculateTotalDamageNextTurn` 是"下回合伤害"，
本来就不要求同帧精确）。

> 如果实测发现确实滞后，可以在 `BurnPower` 的应用路径里
> （`ApplyAbsolute` 写完层数之后）主动触发一次刷新 —— 但**先实测再决定**，
> 不要凭猜测加补丁。

---

## 4. 拟实现的写法（**未施工**，仅草案）

`BurnPower` 里加一个 override：

```csharp
/// <summary>
/// 血条预测段：在右侧用橙红染出"下回合开始时将要被烧掉的血量"。
/// 走 BaseLib 的 IHealthBarForecastSource（CustomPowerModel 已实现该接口）。
/// </summary>
public override IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(
    HealthBarForecastContext context)
{
    // 燃烧在"拥有者所在阵营的回合开始"结算 ⇒ 用 ForSideTurnStart 排序
    int amount = context.Creature.GetPowerAmount<BurnPower>();
    if (amount <= 0)
    {
        return [];
    }

    // ⚠️ 颜色待定稿：设计上燃烧是橙红，但 HpMiddleground 已经是橙 (1, 0.498, 0)
    //    与 HpForeground 是红 (0.8, 0.047, 0.047)，要挑一个能区分开的橙红。
    Color burnColor = new(1f, 0.35f, 0.1f);

    return HealthBarForecasts.Single(
        amount,
        burnColor,
        HealthBarForecastDirection.FromRight,
        HealthBarForecastOrder.ForSideTurnStart(
            context.Creature, CombatSide.Enemy));
}
```

**与毒的交互**：毒从 `CurrentHp - poisonDamage` 之后开始排（`HealthBarForecastPatch.cs:150`），
所以两者不会重叠，会**依次往左接**（谁在前取决于 `Order`）。
设计上要不要规定"毒先烧、燃烧后烧"的顺序，需要拍板。

---

## 5. 需要拍板 / 待实测的点

1. **颜色**：要能同时区分于
   - `HpForeground` 红 `(0.8, 0.047, 0.047)`
   - `HpMiddleground` 橙 `(1, 0.498, 0)`
   - `PoisonForeground` 绿 `(0.4745, 0.7529, 0.2353)`
   - `DoomForeground` 紫 `(0.3529, 0.2588, 0.6471)`
   → 建议橙红偏亮（如 `(1, 0.35, 0.1)`）或加个自发光材质，需要实机看效果。
2. **顺序**：与毒同时存在时谁先结算？燃烧在**该生物回合开始**结算，
   毒用 `CalculateTotalDamageNextTurn()`（也是下回合）。
   `HealthBarForecastOrder.ForSideTurnStart` 正是为这个排序准备的。
3. **是否 `affectsHpLabel`**：默认 `true`，表示"这段会真的把血扣光时，
   血量数字也跟着变色"。毒/灾厄都是这个行为，建议保持一致。
4. **刷新时机**（见 §3）需要实测。

---

## 6. 相关文件索引

| 用途 | 路径 |
|---|---|
| 原版血条脚本 | `reference/decompiled/game/MegaCrit.Sts2.Core.Nodes.Combat/NHealthBar.cs`（`RefreshForeground` :417） |
| 原版血条场景 | `reference/archive/game-recovery/scenes/combat/health_bar.tscn` |
| 原版灾厄着色器 | `res://scenes/combat/doom_bar.gdshader`（在游戏 pck 内） |
| 接口 | `reference/decompiled/BaseLib/BaseLib.Hooks/IHealthBarForecastSource.cs` |
| 能力基类（已实现接口） | `reference/decompiled/BaseLib/BaseLib.Abstracts/CustomPowerModel.cs:8` |
| 类型定义 | `reference/decompiled/BaseLib/BaseLib.Hooks/HealthBarForecast{Segment,Context,Direction,LeftOriginLayout,Order}.cs` |
| 自动发现 | `reference/decompiled/BaseLib/BaseLib.Patches.UI/HealthBarForecastRegistry.cs:60` |
| 渲染补丁 | `reference/decompiled/BaseLib/BaseLib.Patches.UI/HealthBarForecastPatch.cs` |
| 官方教程 | `reference/docs/tutorials-src/.../RitsuLib/02 - Gameplay Foundation/01 - Health Bar Overlay/README.md` |
| 我们的能力 | `src/Scripts/Powers/BurnPower.cs` |

> ⚠️ 注意：那份教程的目录名是 **RitsuLib**，但接口实际定义在 **BaseLib**
> （`BaseLib.Hooks`），且 `CustomPowerModel` 已实现它 ——
> **我们用 BaseLib 就够，不需要引入 RitsuLib 依赖。**

---

## 7. ✅ 实施记录（2026-10-16）

**用户拍板**：
1. 颜色 —— **橙黄、偏黄一点**，与血条的红区分；
2. 与毒的顺序 —— **不必专门规定，按默认的来**。用户实测"怪身上的异常效果都是从左到右结算，
   燃烧和毒的顺序只取决于**玩家给予的顺序**"；
3. `affectsHpLabel` —— **true**；
4. 开工。

### 7.1 落地写法（`src/Scripts/Powers/BurnPower.cs`）

```csharp
public override IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(
    HealthBarForecastContext context)
{
    int amount = context.Creature.GetPowerAmount<BurnPower>();
    if (amount <= 0) return [];

    return HealthBarForecasts.Single(
        amount,
        BurnForecastColor,                      // (1, 0.78, 0.12) 橙黄偏黄
        HealthBarForecastDirection.FromRight);  // order 用默认 0
}

private static readonly Color BurnForecastColor = new(1f, 0.78f, 0.12f);
```

> ### 颜色的试错过程（留档，避免以后又调回去）
>
> | 轮次 | 值 | RGB | 结果 |
> |---|---|---|---|
> | 1 | `(1, 0.78, 0.12)` | 255, 199, 31 | 用户觉得"太黄" |
> | 2 | `(0.980, 0.541, 0.157)` | 250, 138, 40 | **用户发现与残影橙太像,回滚** |
>
> ⚠️ 第 2 轮与「掉血残影」橙 `HpMiddleground` `(1, 0.498, 0)` ≈ RGB(255, 127, 0)
> 只差 `(5, 11, 40)`（欧氏距离 **0.164**），而第 1 轮离它是 **0.307**。
> 同屏容易误读 ⇒ **最终定为第 1 轮的偏黄值**。
>
> **血条调色余量已经很小**：红（当前血）与橙（掉血残影）占掉了红橙区间，
> 绿是毒、紫是灾厄。可区分的方向只剩"更黄"或"更亮/加自发光材质"。

**只改了这一个文件、只加了这一个 override。** 不加补丁、不碰场景、不加依赖。

### 7.2 IL 核实（确认方向常量真的是 `FromRight`）

```
ldloc.0      ; amount
ldsfld       Godot.Color BurnPower::BurnForecastColor
ldc.i4.0     ; direction = 0 = HealthBarForecastDirection.FromRight   ✅
ldc.i4.0     ; order = 0（默认，不特殊规定）                            ✅
call  BaseLib.Hooks.HealthBarForecasts::Single(Int32, Color, Direction, Int32)
```
> C# 把**枚举常量在编译期内联成 int**，所以反编译里看不到 `FromRight` 这个名字 ——
> 一开始用"字符串里有没有 FromRight"去核实得到了假阴性，改成 dump 完整 IL 才对。

### 7.3 层数即伤害（为什么 `amount` 不用任何扣减）

燃烧每次结算造成**等同于当前层数**的伤害（`BurnPower.Trigger`），
且是 `Unblockable | Unpowered` —— 格挡拦不住。
所以"预测掉血量"就是层数本身。这也是为什么 `affectsHpLabel`（= 这段能烧光时血量数字变色）
用默认 `true` 是对的。

### 7.4 ⚠️ 与毒的真实关系（比调查阶段更准确）

- **毒不走 forecast 段**：它由原版脚本用 `%PoisonForeground` 单独画
  （`NHealthBar.RefreshForeground`），BaseLib 只是把毒的伤害**预先扣掉**当作
  `FromRight` 段的起点（`HealthBarForecastPatch.cs:150`）。
- ⇒ **毒永远在最右边，「燃烧」段紧接在毒的左边。**
- 多个 forecast 来源之间按 `sequenceOrder` 排，而它按 `creature.Powers` 的下标给
  （`Registry.cs` 的 `num++`），那个列表是**按施加顺序 append** 的
  （`Creature.cs` 的 `_powers.Add(power)`）。
  ⇒ **顺序自然等于"玩家给予的顺序"**，与用户观察一致，无需干预。

### 7.5 ✅ 层数变化时立刻重绘（2026-10-16 用户拍板"要立刻重绘"）

**问题成因**：血条预测段的**数据**由我们提供，但**渲染**由 BaseLib 的
`HealthBarForecastPatch` 挂在 `NHealthBar.RefreshForeground` 的 Postfix 上完成。
而 `NHealthBar.RefreshValues()` 的调用点只有四处
（`NCreatureStateDisplay.cs` 的 `SetCreature` / `SetCreatureBounds` /
`OnCombatStateChanged` / Block 变化），**「燃烧层数增加」这条路径一个都不经过** ——
`NCreature.OnPowerIncreased`（`NCreature.cs:800`）**只播 VFX**，没调 `RefreshValues`。
⇒ 不补的话，色段要等下次掉血 / 格挡变化 / 悬停才更新。

**修法**：在 `ApplyAbsolute` 写完层数之后**主动刷一次血条**。

```csharp
if (after != before)
{
    RefreshHealthBar(target);
}
```

调用路径（`BurnPower.RefreshHealthBar`）：

```
Creature.GetCreatureNode()                     // Creature.cs:758 → NCreature
→ node.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")   // NCreature.cs:464
→ display.GetNodeOrNull<NHealthBar>("%HealthBar")            // NCreatureStateDisplay.cs:198
→ .RefreshValues()                             // NHealthBar.cs:381（public）
```

**为什么放在 `ApplyLock` 内、写完层数之后**：这样重绘读到的一定是**刚写入的新层数**，
不会闪出旧值。
**为什么只在 `after != before` 时刷**：取高值没变时色段本来就不用动。
⚠️ 但**返回值语义不变**（仍是 `after > before`）—— 那是给「纵火狂」「巨焱拳」用的，
与刷不刷血条无关。

⚠️ 整个 `RefreshHealthBar` **吞掉异常**：刷血条纯属表现层，
绝不能因为找不到节点把战斗逻辑带崩。非战斗场景 `GetCreatureNode()` 返回 null，
静默跳过即可（燃烧本来也只在战斗里存在）。

**IL 核实**（重绘路径与调用点都在）：
```
ApplyAbsolute 状态机:
    callvirt  Creature::GetPowerAmount<BurnPower>()     ; 读 after
    call      BurnPower::RefreshHealthBar(Creature)     ; ★ 重绘
    newobj    BurnApplicationResult::.ctor(bool)

RefreshHealthBar:
    callvirt  Creature::GetCreatureNode()
    callvirt  Node::GetNodeOrNull<NCreatureStateDisplay>(NodePath)
    callvirt  Node::GetNodeOrNull<NHealthBar>(NodePath)
    call      NHealthBar::RefreshValues()
```

> 与 `FrozenIntentPatch.cs:95` 的
> `__instance.Creature.GetCreatureNode()?.RefreshIntents()` 是同一套写法。
