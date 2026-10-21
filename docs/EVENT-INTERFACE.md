# 事件系统接口调查 + 「尖塔规则怪谈」实现记录

> 2026-10-17。用户要求「先充分调查接口」后实现。**本文只记录已查证的事实，每条带 文件:行号。**

---

## 0. 结论速览

**事件已实现并部署，运行时已被游戏登记（DevConsole 能识别 `WIZARD-RULES_HORROR`）。**
立绘已用用户提供的 `rules_horror.png`（2560×1200）。

| 项 | 值 |
|---|---|
| 类文件 | `src/Scripts/Events/RulesHorror.cs` |
| 基类 | `BaseLib.Abstracts.CustomEventModel` |
| `Id.Entry` | `WIZARD-RULES_HORROR` |
| 本地化 | `src/Wizard/localization/{zhs,eng}/events.json`（各 20 键） |
| 出现阶段 | 第三阶段（`CurrentActIndex == 2`） |
| 多人 | 否（`IsShared` 默认 false） |
| 选项 | 4 个（3 个按卡组张数互斥锁定 + 1 个「离开」永不锁定） |
| 立绘 | `src/Wizard/images/events/rules_horror.png`，**2560 × 1200** |

### 后续轮次的重要修正（见 §9）

1. **锁定时标题必须是「已锁定」**，理由写在描述里 —— 见 §9.1
2. **`Option(onChosen, string)` 那个重载会把字符串当 `pageKey`** —— 见 §9.2（这是个静默 bug）

---

## 1. 基类与注册（**不用写 pool、不用登记**）

```csharp
// BaseLib.Abstracts/CustomEventModel.cs:17-33
public abstract class CustomEventModel : EventModel, ICustomModel, ILocalizationProvider
{
    public virtual ActModel[] Acts => Array.Empty<ActModel>();
    public virtual string? CustomInitialPortraitPath => null;
    public virtual string? CustomBackgroundScenePath => null;
    public virtual string? CustomVfxPath => null;

    public CustomEventModel(bool autoAdd = true)
    {
        if (autoAdd) CustomContentDictionary.AddEvent(this);   // ← 自动注册
    }
}
```
⇒ **继承即注册**，不需要 `[Pool]` 之类的标记（与卡牌不同）。

### 1.1 `Acts` 留空 = 进「共享事件池」

```csharp
// BaseLib.Patches.Content/CustomContentDictionary.cs:89-102
public static void AddEvent(CustomEventModel eventModel)
{
    if (!RegisterType(eventModel.GetType())) return;
    if (eventModel.Acts.Length == 0)
        SharedCustomEvents.InsertSorted(eventModel);   // ← 所有 Act 共享
    else
        ActCustomEvents.InsertSorted(eventModel);      // ← 按 Act 过滤
}
```

**两条路径最终都会进 `ModelDb.AllEvents`**：
- `ModelDb.AllEvents`（`ModelDb.cs:194`）= `Acts.SelectMany(a => a.AllEvents)`
- `AddActContent.Patch`（`AddActContent.cs:28-33`）Harmony 了每个 Act 的
  **`AllEvents` getter**，Postfix `AddCustomEvents`（`:58-72`）会把
  **`ActCustomEvents` 里 `Acts` 命中的** 追加进去
- `CustomSharedEvents`（`CustomSharedEvents.cs`）用 Transpiler 往共享事件列表里
  `ConcatCustom` 追加 `SharedCustomEvents`

⇒ **本事件选择 `Acts` 留空 + `IsAllowed` 过滤**：语义直白、风险最低。
若要更严格的按 Act 分组，可改成 override `Acts => [ModelDb.Act<Glory>()]`。

---

## 2. 出现条件：`IsAllowed(IRunState)`

```csharp
// EventModel.cs:319-327（含官方注释）
/// <summary>
/// For example, RelicTrader can only be entered if you have 5+ relics and you're on act 2+.
/// </summary>
public virtual bool IsAllowed(IRunState runState) => true;
```

**官方示范**（`RelicTrader.cs:98-103`）：
```csharp
public override bool IsAllowed(IRunState runState)
{
    if (runState.CurrentActIndex == 0) return false;      // Act 1 不给
    return runState.Players.All(p => GetValidRelics(p).Count() >= 5);
}
```

**Act 索引**（`IRunState.cs:28-31` 注释明确）：
> `Act 1 has index 0, Act 2 has index 1, etc.`

对应原版 Act（各自 `Index` 属性）：
| Act | 类 | `Index` |
|---|---|---|
| 第一 | `Overgrowth` | `0` |
| 第二 | `Hive` | `1` |
| **第三** | **`Glory`** | **`2`** |

---

## 3. 选项与「锁定」

### 3.1 `IsLocked` 的定义

```csharp
// EventOption.cs:24 / :62 / :84
public bool IsLocked { get; }
...
IsLocked = OnChosen == null;
```
⇒ **条件不满足时把委托传 `null` 即可**，UI 自动灰掉且不可点。不需要额外机制。

### 3.2 BaseLib 的三种 `Option` 重载

| 重载 | 何时用 |
|---|---|
| `Option(Func<Task>? onChosen, LocString title, LocString description, params IHoverTip[] tips)` | **本事件用这个** —— 键名自己控制，最可预测 |
| `Option(Func<Task>? onChosen, string pageKey, ...)` | 键名从方法名 `Slugify` 生成；**委托为 null 时会打 `Logger.Error` 并退化成 `UNKNOWN`** |
| `LockedOption(string locKey, ...)` | 需**手写** locKey 字符串 |

⚠️ 所以锁定时**不用** `LockedOption`（要手写键）也**不用** `pageKey` 重载（会 ERROR），
而是用第一个重载 + `null` 委托。

### 3.3 结束事件

```csharp
// EventModel.cs:478-481
protected void SetEventFinished(LocString description)
{
    SetEventState(description, Array.Empty<EventOption>());
}
```

---

## 4. 三个选项用到的原版 API（全部有原版先例）

### 4.1 移除 1 张牌 —— 照抄「光与暗的门扉」

```csharp
// DoorsOfLightAndDark.cs:33-37
List<CardModel> cards = (await CardSelectCmd.FromDeckForRemoval(
    prefs: new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1),
    player: base.Owner)).ToList();
await CardPileCmd.RemoveFromDeck(cards);
```

### 4.2 随机升级 N 张 —— 同样照抄它

```csharp
// DoorsOfLightAndDark.cs:24-31
IEnumerable<CardModel> picked = PileType.Deck.GetPile(Owner).Cards
    .Where(c => c.IsUpgradable)
    .ToList().StableShuffle(Rng).Take(N);
foreach (var c in picked) CardCmd.Upgrade(c);
```
⚠️ 用 `Rng`（事件的确定性随机源），**不要用 `Random.Shared`**，否则同种子重放不一致。
`StableShuffle` 在 `MegaCrit.Sts2.Core.Extensions`（`ListExtensions.cs:22/45`）。

### 4.3 获得随机遗物

```csharp
// RelicFactory.cs:26
public static RelicModel PullNextRelicFromFront(Player player)
// 内部：（RollRarity(player), filter）→ 掷稀有度 → 从池里取 → 标记本局不再出现
await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(player).ToMutable(), player);
```
⚠️ **不要手动读 `ModelDb.RelicPool<WizardRelicPool>()` 抽** ——
`PullNextRelicFromFront` 已正确处理「角色池 + 共享池」合并与「本局不再出现」状态。
原版先例：`RanwidTheElder.cs:113/121/131`、`ColossalFlower.cs:103`。

### 4.4 牌组张数 / 可移除 / 可升级判定

```csharp
int n = PileType.Deck.GetPile(player).Cards.Count;          // PileTypeExtensions.cs
bool canRemove = PileType.Deck.GetPile(player).Cards.Any(c => c.IsRemovable);
bool canUpgrade = PileType.Deck.GetPile(player).Cards.Any(c => c.IsUpgradable);
```
（`CookRestSiteOption.cs:65` 用的正是 `Count(c => c.IsRemovable)`）

---

## 5. ⚠️ 美术：需要补的贴图尺寸

### 5.1 立绘（**这是要你补的那张**）

| 项 | 值 |
|---|---|
| **尺寸** | **3440 × 1616**（PNG） |
| 依据 | 解包原版 pck，`images/events/` 下 **58 张立绘中 56 张都是 3440×1616**（唯一例外 `dense_vegetation` 那套是 2560×1200） |
| 应放路径 | `src/Wizard/images/events/rules_horror.png` |
| 代码要改成 | `CustomInitialPortraitPath => "res://Wizard/images/events/rules_horror.png";` |

> 原版命名约定（`EventModel.cs:198`）：
> `ImageHelper.GetImagePath("events/" + Id.Entry.ToLowerInvariant() + ".png")`
> ⇒ `WIZARD-RULES_HORROR` → `res://images/events/wizard-rules_horror.png`。
> 但我们的图放 mod 目录，所以**必须显式给 `CustomInitialPortraitPath`**
> （BaseLib 的 `InitialPortraitPath` Prefix 会优先用这个值，
> 见 `BaseLib/…/InitialPortraitPath.cs:10-16`）。
> mod 内文件名用 `rules_horror.png` 更清爽，不必带 `wizard-` 前缀。

### 5.2 目前用的占位符

```csharp
public override string? CustomInitialPortraitPath => "res://images/events/battleworn_dummy.png";
```
直接借用原版的「饱经战损的假人」。**不占我们 pck 的体积**（引用原版资源）。

### 5.3 可选的另外两张（**本事件不需要**）

| 用途 | 尺寸/命名 | 说明 |
|---|---|---|
| 恐惧模式立绘 | `events/{entry}_phobia_mode.png` | 可选，`HasPhobiaModePortrait` 检测存在才用（`EventModel.cs:200-202`） |
| 背景场景 | `scenes/events/background_scenes/{entry}.tscn` | 可选，`CustomBackgroundScenePath` 保持 null 就走通用动态背景。原版也只有少数事件有专属背景 |
| 特效 | `scenes/vfx/events/{entry}_vfx.tscn` | 可选（`EventModel.cs:206-208`） |

---

## 6. 本地化键结构

`{MOD}/localization/{lang}/events.json`，键前缀 = `Id.Entry`：

```
WIZARD-RULES_HORROR.title
WIZARD-RULES_HORROR.pages.INITIAL.description
WIZARD-RULES_HORROR.pages.INITIAL.options.{LEAN|BALANCED|ABUNDANT}.title / .description
WIZARD-RULES_HORROR.pages.{LEAN|BALANCED|ABUNDANT}.description      ← 选项后的"后续"文本
```

⚠️ **页面键（`INITIAL` / `LEAN` / `BALANCED` / `ABUNDANT`）由我们自己定**，
但选项键必须与代码传的 `LocString` 一致。
本事件两边都显式写死（`Key` 常量 + 6 个 `LocString` 属性），没有隐式生成。

---

## 7. 设计稿 → 实现的口径映射

`design.md:350-361`「事件稿」：

| 设计稿 | 实现 |
|---|---|
| 出现阶段：第三阶段 | `IsAllowed`: `CurrentActIndex != 2` → false |
| 多人合作：否 | 不 override `IsShared`（默认 false） |
| 触发①卡组<20 且存在可移除牌 | `deckSize <= 19 && Cards.Any(IsRemovable)` |
| 触发②卡组 20-35（闭区间）且存在可升级牌 | `20 <= deckSize <= 35 && Cards.Any(IsUpgradable)` |
| 触发③卡组>35 | `deckSize >= 36` |
| 选项1 锁定条件：<20 | `lean = deckSize <= 19 && canRemove` |
| 选项2 锁定条件：20-35 | `balanced = 20<=n<=35 && canUpgrade` |
| 选项3 锁定条件：>35 | `abundant = deckSize >= 36` |
| 移除1张 / 升级2张 / 3个随机遗物 | §4.1 / §4.2 / §4.3 |

⚠️ **锁定判据用「本档位」而不是「任意档位」** ——
否则 18 张卡组时三个选项都能点，玩家会挑收益最高的（3 个遗物）。
三个档位互斥（`<=19` / `[20,35]` / `>=36`），所以任意时刻**恰好只有一个选项可点**。

⚠️ **触发条件①和②附带"存在可操作对象"**，是为了避免
「事件出现了但三个选项全锁着」的死局。

---

## 8. 验证记录

```
dotnet build                       0 警告 0 错误
build.ps1                          dll 6AA3C7BE / pck 31CF6A07 均校验通过
IL 核实                            Wizard.Scripts.Events.RulesHorror : CustomEventModel
                                   方法 get_CustomInitialPortraitPath / IsAllowed / GenerateInitialOptions
pck 内容                           events.json 已打包；中英 title 都在
运行时                             DevConsole 输出 "event WIZARD-RULES_HORROR" ⇒ 已被登记
                                   无 ERROR、无 "Missing localization key" 告警
                                   events.json 被当作 loc table 合并（中英各一条 INFO）
```

---

## 9. 第二轮修正（2026-10-17 晚）

### 9.1 ⚠️ 锁定样式：标题必须写「已锁定」，理由写在描述里

**用户反馈**：上一版锁定项显示的是"选项本名 + 效果描述"（只是变红变灰），
与原版不一致。原版是**上面「已锁定」、下面写锁定理由**。

**查证**（`NEventOptionButton.cs:290-298`）：

```csharp
string formattedText  = Option.Title.GetFormattedText();
string formattedText2 = Option.Description.GetFormattedText();
string value = (Option.IsLocked ? "red" : "gold");
_label.Text = $"[{value}][b]{formattedText}[/b][/{value}]\n{formattedText2}";
```

再配合 `SetVisuallyLocked()`（`:324-330`，降透明度 + 压暗 HSV）。

⇒ **没有"锁定专用渲染"，就是"标题填『已锁定』、描述填理由"**。
原版每个锁定项都有**独立的一对键**，例如
`GRAVE_OF_THE_FORGOTTEN.pages.INITIAL.options.CONFRONT_LOCKED`：

```
title       = 已锁定
description = 你没有可被[purple]附魔[/purple]的带有[gold]消耗[/gold]的卡牌。
```

本事件照此改为三对独立键：
```
LEAN_LOCKED.title       = 已锁定
LEAN_LOCKED.description = 需要牌组数量少于[blue]20[/blue]张。
BALANCED_LOCKED.description = 需要牌组数量在[blue]20-35[/blue]之间。
ABUNDANT_LOCKED.description = 需要牌组数量多于[blue]35[/blue]张。
```
代码用 BaseLib 的 `LockedOption("LEAN_LOCKED")` —— 它自己拼
`{Entry}.pages.INITIAL.options.{locKey}` 并以 `(Func<Task>)null` 构造，
正好对上原版那条 `IsLocked` 渲染路径。

### 9.2 ⚠️⚠️ 一个**编译器不会报**的重载陷阱

BaseLib 有三个 `Option` 重载（`CustomEventModel.cs:35-107`）：

```csharp
Option(Func<Task>? onChosen, LocString title, LocString description, params IHoverTip[] tips)
Option(Func<Task>? onChosen, string pageKey = "INITIAL", params IHoverTip[] tips)          // ← 陷阱
Option(Func<Task>? onChosen, IEnumerable<IHoverTip> tips, string pageKey = "INITIAL")
```

**写成 `Option(SomeMethod, "WIZARD-RULES_HORROR.pages.INITIAL.options.LEAVE")` 会绑定到第二个**
—— 那个字符串被当成 **`pageKey`**，然后拼成

```
{Entry}.pages.{你传的整串}.options.{方法名 Slugify}
```

键完全错位，**只会在运行时打一条 `Missing localization key` 警告**，界面显示空白键名，
而编译器一声不响（因为确实存在匹配的重载）。

⇒ **必须显式给 `Title` / `Description` 两个 `LocString`**，走第一个重载。
本事件为此写了 8 个 `LocString` 属性 + 一个 `Opt(name, field)` 帮助方法。

**IL 核实**（部署后的 `Wizard.dll`）：
```
GenerateInitialOptions  call CustomEventModel::Option(Func`1<Task>, LocString, LocString, IHoverTip[])   ×4
GenerateInitialOptions  call CustomEventModel::LockedOption(String, String, IHoverTip[])                 ×3
GenerateInitialOptions  callvirt EventOption::ThatDoesDamage(Decimal)                                   ×1
```
4 个选项全部走 `LocString` 重载，**没有**错误绑定的调用。

### 9.3 选项 4「离开」—— 永不锁定

design.md 没给锁定条件，且这是玩家的**退出路径**：
若也按卡组张数锁定，条件不满足时会**卡在事件里出不去**。所以无条件可点。

效果「随机降级 2 张牌」与选项 2「升级 2 张」完全镜像：
```csharp
PileType.Deck.GetPile(player).Cards.Where(c => c.IsUpgraded)
    .ToList().StableShuffle(Rng).Take(2)
    → CardCmd.Downgrade(card)
```
⚠️ **必须过滤 `IsUpgraded`**（`CardModel.cs:784` = `CurrentUpgradeLevel > 0`）：
`DowngradeInternal`（`CardModel.cs:2149`）会无条件把等级置 0 并重建费用/关键词，
对未升级的牌调用虽不崩，但会白跑一遍重建、且污染 `DowngradedCards` 统计。

### 9.4 生命代价 / 回复

| 效果 | API | 依据 |
|---|---|---|
| 失去 6 点生命 | `CreatureCmd.Damage(..., HpLoss, ValueProp.Unblockable \| Unpowered, ...)` | 原版 `Bloodletting.cs:40` |
| 回复 20 点生命 | `CreatureCmd.Heal(creature, amount)` | `CreatureCmd.cs:735`；事件先例 `AbyssalBaths.cs:60` |

选项 1 额外挂了 `.ThatDoesDamage(HpLoss)`（`EventOption.cs:200`）——
原版 `BrainLeech.Rip` 同写法，会在按钮上提示"将受到 N 点伤害"。
声明用 `new HpLossVar(6)` / `new HealVar(20)`，
`NEventOptionButton._Ready:285` 会 `Event.DynamicVars.AddTo(Option.Description)`，
所以选项描述里的 `{HpLoss}` / `{Heal}` 能正常解析。

### 9.5 文案特效：标签对照

`MegaRichTextLabel` 注册了 14 个自定义特效
（`MegaRichTextLabel.cs:184-200`）：
`aqua blue fadein flyin gold green jitter orange pink purple red scramble sine thinkydots`

| design.md 要求 | 实现 |
|---|---|
| 「辉煌的中庭」金色 | `[gold]辉煌的中庭[/gold]` |
| 「规则公告」紫色 | `[purple]规则公告[/purple]` |
| 「不可抗力」红色 | `[red]不可抗力[/red]` |
| 「未知的声音」金色 | `[gold]未知的声音[/gold]` |
| 「你信奉理念为何？」轻微摆动 + 「理念」紫色 | `[sine]你信奉[purple]理念[/purple]为何？[/sine]` |
| 「拼命挣扎」红色 + 轻微摆动 | `[sine][red]拼命挣扎[/red][/sine]` |
| 「禁锢魔力」紫色 | `[purple]禁锢魔力[/purple]` |

**「轻微摆动」= `[sine]`**，依据 `RichTextSine.cs`：
`amplitude = 0.8`、`frequency = 0.5`、`speed = 1.5`；
对比 `RichTextJitter.cs` 的 `amplitude = 3f`、`speed = 600f` —— 那才是剧烈抖动。

⚠️ **颜色可嵌套在 `[sine]` 内**：`RichTextSine._ProcessCustomFX` 只在
`env` 含 `colorKey` 时才覆盖颜色，所以内层 `[purple]` / `[red]` 优先生效。

⚠️ **`[sine]` 受玩家设置控制**：`AbstractMegaRichTextEffect.ShouldTransformText()`
返回 `SaveManager.Instance.PrefsSave.TextEffectsEnabled`。
若玩家关掉了"文字特效"，`[sine]` 会退化成普通文字（颜色仍生效）—— 这与原版行为一致。

### 9.6 立绘尺寸

用户提供 **2560 × 1200**（比例 2.133），原版主流是 3440 × 1616（比例 2.129）。

`EventModel.CreateInitialPortrait()`（`EventModel.cs:351-354`）只是
`PreloadManager.Cache.GetTexture2D(InitialPortraitPath)` —— **不校验尺寸**，
由事件界面按容器等比缩放。两者比例几乎一致，不会变形或裁掉关键内容。

### 9.7 本轮验证

```
dotnet build                 0 警告 0 错误
dll 0B567595 / pck 1958C8D7  哈希独立复核一致
IL 核实                      4× LocString 重载 / 3× LockedOption / 1× ThatDoesDamage
pck                          rules_horror.png 已打包；中英各 20 键齐全
运行时                       无 ERROR；无 "Missing localization key"；
                             events.json 被合并进 loc table；
                             rules_horror.png 已生成 .ctex（Godot 导入成功）
```

---

## 10. 第三轮：边界情况核查 + 3 处新文案特效（2026-10-17 晚）

### 10.1 核查结论总表

| # | 要求 | 结论 | 证据 |
|---|---|---|---|
| 1 | 三个选项都不可行 ⇒ 事件不遇到 | ✅ 已满足 | `TriggersFor` 三支互斥且各自带条件；`RoomSet.EnsureNextEventIsValid:118` 会 **skip** `IsAllowed == false` 的事件 |
| 2 | 恰好 20 / 35 张落在中间选项 | ✅ 已满足 | 常量经 IL 核实 = `LeanMax 19` / `BalancedMin 20` / `BalancedMax 35` / `AbundantMin 36` |
| 3 | 选项2：**玩家自选**升级，可升级牌 2/1/0 张 | ✅ 已满足（2026-10-17 改为自选） | `CardSelectCmd.FromDeckForUpgrade`：内部先过滤 `IsUpgradable`（`:539`）；0 张返回空（`:540-543`）；**≤MinSelect 且不要求手动确认时自动全选不弹界面**（`:545-548`） |
| 4 | 选项4：可降级 2/1/0 张 | ✅ 已满足 | `Where(IsUpgraded).Take(DowngradeCount)` 饱和；0 张时无事发生 |
| 5 | 选项1 致死时红色警告 / 单人可选 / 多人拦截 | ✅ **引擎自动处理** | 见 §10.3 |

### 10.2 边界真值表（镜像真实条件，跨 1–79 张 × 可移除/可升级 全部组合）

```
牌组 | 有可移除 | 有可升级 || 会遇到 | 选项1 | 选项2 | 选项3 | 选项4
  19 |    ●     |    ○     ||   ●    |   ●   |   ○   |   ○   |   ●（永不锁）
  19 |    ○     |    ○     ||   ○    |   ○   |   ○   |   ○   |   ●
  20 |    ○     |    ●     ||   ●    |   ○   |   ●   |   ○   |   ●
  20 |    ○     |    ○     ||   ○    |   ○   |   ○   |   ○   |   ●
  27 |    ○     |    ●     ||   ●    |   ○   |   ●   |   ○   |   ●
  35 |    ○     |    ●     ||   ●    |   ○   |   ●   |   ○   |   ●
  35 |    ○     |    ○     ||   ○    |   ○   |   ○   |   ○   |   ●
  36 |    ○     |    ○     ||   ●    |   ○   |   ○   |   ●   |   ●
  60 |    ○     |    ○     ||   ●    |   ○   |   ○   |   ●   |   ●
```

程序化断言（全组合遍历，**0 违例**）：

- 「三选项全锁 ⇒ 事件不出现」：违例 **0**
- 「事件出现 ⇒ 至少一选项可用」（不会卡成无解）:违例 **0**
- 「选项1/2/3 恒互斥（恰好 1 个可用）」:违例 **0**

> ⚠️ 这条"恰好一个可用"很重要：若按"任意档位满足就全开"实现，
> 18 张卡组时三个选项都能点，玩家会挑收益最高的（3 个遗物）。

### 10.3 ⚠️ 选项 5：致死警告 / 单人可选 / 多人拦截 —— **全部由引擎处理**

**用户记忆正确，但机制不是"文字变红"，而是"红色闪光 + 脉冲"**。

原版 `NEventOptionButton.OnRelease`（`NEventOptionButton.cs:360-389`）：

```csharp
if (Option.IsLocked) return;
if (WillKillPlayer())
{
    if (Event.Owner.RunState.Players.Count > 1)          // ── 多人
    {
        LocString line = Event.Owner.Character.EventDeathPreventionLine;
        _deathPreventionVfx = NThoughtBubbleVfx.Create(line.GetFormattedText(), DialogueSide.Right, null);
        ... TaskHelper.RunSafely(RumbleDeathVfx());       //   屏幕/气泡抖动
        ... TaskHelper.RunSafely(ExpireDeathPreventionVfx()); // 2.5s 后消失
        return;                                            // ★ 不执行选项
    }
    ShowPersistentKillGlow();                              // ── 单人：红色辉光，但仍执行
}
NEventRoom.Instance.OptionButtonClicked(Option, Index);     // ★ 单人真的会执行 → 可以死
```

`WillKillPlayer()` = `Option.WillKillPlayer?.Invoke(Event.Owner)`（`:—`），
而 `ThatDoesDamage(d)` 就是
`ThatWillKillPlayerIf(p => p.Creature.CurrentHp <= d)`（`EventOption.cs:200-203`）——
注意是 `<=`，**恰好剩 6 血也会警告**。

⇒ **本事件已经调用了 `.ThatDoesDamage(DynamicVars.HpLoss.BaseValue)`，
所以这套行为全部自动生效，不需要额外代码。**

台词来源：`CharacterModel.EventDeathPreventionLine`
（`CharacterModel.cs:89`）= `{CHARACTER}.eventDeathPrevention`。
**巫师已有这条**（`characters.json`）：
```
zhs: "不……我的研究还没有完成……"
eng: "No... my research is not finished..."
```
⇒ 多人点下去会弹**巫师自己的**台词（不是摄政的"为了我的队伍，我必须活下去！！"）。

### 10.4 ⚠️ 本轮修掉的一个真实隐患：扣血致死后继续弹选牌界面

`CreatureCmd.Damage` 末尾会 `await Kill(killedCreatures)`（`CreatureCmd.cs:430`），
而 `Kill` 在"所有玩家都死"时会**同步**：

```csharp
SerializableRun run = RunManager.Instance.OnEnded(isVictory: false);   // :486
NRun.Instance.ShowGameOverScreen(run);                                 // :487
```

此时 run 已经结束。若接着调 `CardSelectCmd.FromDeckForRemoval`，
选牌弹窗会盖在结束画面上 —— 轻则视觉错乱，重则流程卡死。

**修法**：扣血后立刻判 `player.Creature.IsDead`，死了就 `return`（不再选牌、不再 `SetEventFinished`）。

**IL 核实**（部署后 dll 的 `ChooseLean` 状态机，顺序正确）：
```
callvirt CreatureCmd::Damage(...)
callvirt Creature::get_IsDead()          ← 守卫
call     CardSelectCmd::FromDeckForRemoval(...)
call     CardPileCmd::RemoveFromDeck(...)
call     EventModel::SetEventFinished(...)
```

### 10.5 ⚠️ 另一个隐患：零选项 = 软锁

原实现里 `Owner == null` 时返回 `Array.Empty<EventOption>()`。
`EventModel.ReplaceNullOptions`（`EventModel.cs:287-301`）只防 **null 选项**，
**不防"零选项"** —— 那会渲染出一个没有任何按钮的事件页，等于软锁。

查证：`Owner` 在 `EventModel.BeginEvent:233` 必被赋值，且早于
`SetInitialEventState` → `GenerateInitialOptions`，所以该分支**不可达**。
但仍改为安全降级：**三个锁定 + 「离开」可用**（始终留一条出路）。

### 10.6 3 处新文案特效

| design.md 位置 | 实现 |
|---|---|
| 选项一后续「某人」红色 | `你似乎令[red]某人[/red]深为不满？` |
| 选项二后续「赐福」金色 | `你获得了小小的[gold]赐福[/gold]。…` |
| 选项三后续「认可」「丰盈的财宝」金色 | `尖塔似乎[gold]认可[/gold]了你的精神，你也收获了[gold]丰盈的财宝[/gold]（？）` |

英文同步：`[red]someone[/red]` / `[gold]blessing[/gold]` /
`[gold]acknowledge[/gold]` / `[gold]an abundance of treasure[/gold]`。

### 10.7 本轮验证

```
dotnet build                 0 警告 0 错误
dll 8FD820A8 / pck B1D0D9EC  哈希独立复核一致
本地化校验                    24 项全 OK（含 3 处新特效、无缺键）
IL 核实                      常量 19/20/35/36/6/2/20/3/2；Take 存在；
                             ChooseLean 的 IsDead 守卫位于 Damage 与选牌之间
pck                          3 处新特效已打包（中英）
运行时                        无 ERROR、无 "Missing localization key"
                             （⇒ LockedOption 的键存在性检查通过）
```

> ⚠️ 日志里有一条 `[WARN] [EventSynchronizer] Beginning new event EVENT.WIZARD-RULES_HORROR,
> but event EVENT.SLIPPERY_BRIDGE ... is not yet finished!` ——
> 那是**用 DevConsole 强制跳转**时、上一事件（滑桥）还没关掉造成的，
> 与事件实现无关（`EventSynchronizer.cs:109`）。

### 10.8 ⚠️ 选项 2 是**玩家自选**升级，不是随机（2026-10-17 修正）

**用户指出**：design.md 写的是「升级2张牌」，语义是玩家自选；我错按「随机升级 2 张」实现。

**错在哪**：我照抄了「光与暗的门扉」`DoorsOfLightAndDark.Light()`
（`DoorsOfLightAndDark.cs:24-31`）的 `Where(IsUpgradable) → StableShuffle(Rng) → Take(2)`。
但那个事件的设计**本身就是"随机升级 2 张"** —— 两个事件的语义不同，不能互相照抄。

**正确做法**：`CardSelectCmd.FromDeckForUpgrade`
（`CardSelectCmd.cs:537`）—— 弹 `NDeckUpgradeSelectScreen`（**带升级预览的选牌屏**），
与休息处「锻造」`SmithRestSiteOption`、原版事件「审判」`Trial.MerchantInnocent()`
（`Trial.cs:154-162`，也是"选 2 张升级"）同一条路径。

```csharp
foreach (CardModel card in await CardSelectCmd.FromDeckForUpgrade(
    prefs: new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, UpgradeCount),
    player: player))
{
    CardCmd.Upgrade(card);
}
```

**边界由它自己处理好了**（比手写更稳）：
- `:539` 内部先 `Where(c => c.IsUpgradable)` ⇒ **不用自己过滤**
- `:540-543` 可升级 0 张 ⇒ 返回空 ⇒ 无事发生
- `:545-548` 可升级 ≤ `MinSelect` 且 `!RequireManualConfirmation` ⇒ **自动全选、不弹界面**
  （`CardSelectorPrefs(prompt, selectCount)` 令 `MinSelect == MaxSelect == 2`
   且 `RequireManualConfirmation == false`，见 `CardSelectorPrefs.cs:77`）
  ⇒ 正好满足"只有 1 或 2 张可升级时直接升级"
- 可升级 > 2 张时才真正弹选牌屏

**文案同步**（去掉"随机"）：

| | 改前 | 改后 |
|---|---|---|
| 中文 | 随机[gold]升级[/gold][blue]2[/blue]张牌。 | [gold]升级[/gold][blue]2[/blue]张牌。 |
| 英文 | [gold]Upgrade[/gold] [blue]2[/blue] random cards. | [gold]Upgrade[/gold] [blue]2[/blue] cards. |

措辞对齐原版 `TRIAL.pages.MERCHANT.options.INNOCENT.description`
（zh: `[gold]升级[/gold][blue]2[/blue]张牌。` / en: `[gold]Upgrade[/gold] [blue]2[/blue] cards.`），
它用的正是同一个 `FromDeckForUpgrade(..., 2)`。

**IL 核实**（部署后 dll）：
```
ChooseBalanced:
    call  CardSelectorPrefs::get_UpgradeSelectionPrompt()
    newobj CardSelectorPrefs::.ctor(LocString, Int32)
    call  CardSelectCmd::FromDeckForUpgrade(Player, CardSelectorPrefs)
    call  CardCmd::Upgrade(CardModel, CardPreviewStyle)
ChooseLeave:
    call  ListExtensions::StableShuffle<CardModel>(List, Rng)   ← 仍随机，符合设计
```

⇒ **`StableShuffle` 现在只剩 `ChooseLeave` 在用** —— 选项 4「随机降级 2 张」按 design.md
本来就是随机的，保持不变。

```
dotnet build                 0 警告 0 错误
dll EF8D6ED5 / pck 3CE41FA6  哈希独立复核一致
pck 文案                     中英均无"随机/random"
运行时                       无 ERROR、无缺键告警
```

---

## 11. 事件死亡专属文案（`events` 表 `{Entry}.loss`，2026-10-17）

### 11.1 现象

选项 1（失去 6 点生命）**致死**时，结算界面显示的是**兜底调试文案**：

```
巫师正在试图修复游戏漏洞。如果你并非MegaCrit的程序员，但是你看见了这条信息，请告诉我们！
```

### 11.2 取值链（已查证）

`NRunHistory` 的 `GameOverType.EventDeath` 分支（`NRunHistory.cs:668-688`）：

```csharp
eventModel  = ModelDb.GetById<EventModel>(history.KilledByEvent);
LocString ls = LocString.GetIfExists(eventModel.LocTable, eventModel.Id.Entry + ".loss");
if (ls == null) ls = GetDeathDebugMessage();     // ← 那句"修复游戏漏洞"
characterModel.AddDetailsTo(ls);                 // ← 注入 {character} 等
ls.Add("event", eventModel.Title);               // ← 注入 {event}
```

- `EventModel.LocTable` 默认 `"events"`（`EventModel.cs:60`）
- `Id.Entry` = `WIZARD-RULES_HORROR`
  ⇒ **键 = `events` 表里的 `WIZARD-RULES_HORROR.loss`**
- 兜底文案来自 `run_history` 表的 `MAP_POINT_HISTORY.debug`

**为什么会走这个分支**：`RunHistoryUtilities.CreateRunHistoryEntry`
（`RunHistoryUtilities.cs:30-33`）里
`roomType == RoomType.Event ⇒ killedByEvent = mapPointRoomHistoryEntry.ModelId`。
所以在事件房里死亡**确实**会被判定为"死于事件" —— 缺的只是文案本身。

### 11.3 修复

给 `localization/{zhs,eng}/events.json` 各加一条：

| | 值 | 渲染结果 |
|---|---|---|
| zhs | `{character}被规则怪谈的魔力摧毁了意识...` | 巫师被规则怪谈的魔力摧毁了意识... |
| eng | `{character}'s mind was destroyed by the magic of the Rules Horror...` | The Wizard's mind was destroyed by the magic of the Rules Horror... |

⚠️ **用 `{character}` 而不是硬写"巫师"**：
`CharacterModel.AddDetailsTo`（`CharacterModel.cs:270-279`）注入
`{character}` = 角色 `Title`，而本 mod 的
`WIZARD-WIZARD_CHARACTER.title` 就是 `巫师`（无任何标记）
⇒ 渲染结果与用户给的文字**逐字相同**，但角色名日后若改动也不会失配。
（脚本里有断言 `zh_render == 用户原文`。）

⚠️ 没有用 `{event}`：用户指定的是「规则怪谈」，
而 `{event}` = 事件标题「**尖塔**规则怪谈」，会多出"尖塔"两字。

### 11.4 验证

```
本地化校验           9 项全 OK（含"中文渲染 == 用户原文"）
pck 内               WIZARD-RULES_HORROR.loss 中英都在
dotnet build         0 警告 0 错误
dll EF8D6ED5 / pck F9B06A26   哈希独立复核一致
运行时               无 ERROR、无缺键告警
```

> ⚠️ 这条文案只在**新的**死亡记录上生效；已存在的 run history 记录是快照，不会回溯改写。
