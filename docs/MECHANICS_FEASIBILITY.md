# 巫师 Mod — 16 项机制技术可行性评估

> 依据：本地反编译源码（游戏 v0.111.0 / BaseLib v3.4.7 / 观者 v1.5.10）+ 本仓库现有代码。
> 所有结论均标注文件路径与精确签名；不确定处已显式标注。
> 目标框架：`net9.0`（`src/Wizard.csproj`，`LangVersion 14.0`，`Nullable enable`）。

---

## 0. 前置上下文：本仓库已有的实现（很重要）

`design.md` 里的卡池几乎逐条对应本次要评估的 16 项机制。仓库现状：

| 已存在 | 文件 | 与本次机制的关系 |
|---|---|---|
| `WizardCardModel` 卡牌基类（卡图路径约定） | `src/Scripts/Cards/WizardCardModel.cs` | 所有新卡的基类 |
| `WizardSpellCard : WizardCardModel, ISpellCard` | `src/Scripts/Cards/WizardSpellCard.cs` | 法术牌基类 |
| `SpellSchool` / `SpellUtil.IsSpell()/.SchoolOf()` | `src/Scripts/Spells/` | 判定"火焰/雷电/寒冰伤害"的唯一途径 |
| `SpellCmd.ApplyBurn / ApplyBurnToAll`（取高值语义） | `src/Scripts/Spells/SpellCmd.cs` | **机制 2/14 的关键漏斗，也是它们的头号风险源** |
| `BurnPower`（燃烧，Debuff） | `src/Scripts/Powers/BurnPower.cs` | 机制 1/2/7/8/12/14 的核心状态 |
| `SpellPowerPower`（法术增益，Buff） | `src/Scripts/Powers/SpellPowerPower.cs` | 机制 2/3/14 的"1 点法术增益" |
| `CurrentOverflowPower` + `EnableSelfSplash()` | `src/Scripts/Powers/CurrentOverflowPower.cs` | **机制 6 已实现** |
| `ChillPower` / `FreezePower` / `FrostWallPower` / `ParalysisPower` | `src/Scripts/Powers/` | 机制 1/5/7 会与之交互 |

两个必须先说清的事实：

1. **原版 `Burn` 是"状态牌"，不是 Debuff。**
   `game/MegaCrit.Sts2.Core.Models.Cards/Burn.cs`：`Unplayable` 的 `CardType.Status`，留在手里时回合结束对自己造成 2 点 `Unpowered|Move` 伤害。
   巫师 mod 的"燃烧"是**自己定义的 `Wizard.Scripts.Powers.BurnPower`（`PowerType.Debuff`）**。
   ⚠️ 任何同时 `using MegaCrit.Sts2.Core.Models.Cards;` 和 `using Wizard.Scripts.Powers;` 的文件都会 `Burn` 二义/误用。

2. **`Retain` / `Innate` / `Ethereal` / `Exhaust` 是引擎原生关键字**，不需要任何补丁：
   - `game/MegaCrit.Sts2.Core.Entities.Cards/CardKeyword.cs` 里已有全部 8 个值。
   - Retain：`CardModel.ShouldRetainThisTurn`（`CardModel.cs:591`）→ `CombatManager.FlushPlayerHand`（`CombatManager.cs:1794`）。
   - Innate：`CombatManager.cs:913-919`（第 1 回合把 `Innate` 牌移到抽牌堆顶并抬高起手抽牌数）。
   - Ethereal：`CombatManager.cs:1615`（回合结束 `Hook.ShouldEtherealTrigger` → `CardCmd.Exhaust(..., causedByEthereal: true)`）与 `CardModel.GetResultLocationForCardPlay`（`CardModel.cs:2090`）。

---

## 1. 汇总表

| # | 机制 | design.md 对应卡 | 可实现? | 难度 | 关键 API | 备注 |
|---|---|---|---|---|---|---|
| 1 | 本回合攻击额外造成等量火焰伤害 + 1 燃烧 | 燃烧之魂 | ✅ | 中 | `AbstractModel.AfterDamageGiven` + `CreatureCmd.Damage` + `SpellCmd.ApplyBurn` | 无限递归可用 `ValueProp.Unpowered` 天然阻断 |
| 2 | 每次施加燃烧 → +1 法术增益 | 纵火狂 | ✅（有坑） | 中 | `AbstractModel.AfterPowerAmountChanged` | **`SpellCmd.ApplyBurn` 抬高已有层数时不触发任何钩子**，需改漏斗 |
| 3 | 每次消耗牌 → +1 法术增益 | 凝神 | ✅ | 低 | `AbstractModel.AfterCardExhausted` | 打出 Exhaust 牌也会走 `CardCmd.Exhaust`（`CardModel.cs:1998`） |
| 4 | 0 费净赚 3 费 | 决心 | ✅ | 低 | `PlayerCmd.GainEnergy(decimal, Player)` + `EnergyVar` | 纯原版写法（`Adrenaline`/`Turbo`） |
| 5 | 雷电伤害额外施加 1 层电流外溢 | 交流电 | ✅ | 低 | `AfterDamageGiven` + `PowerCmd.Apply<CurrentOverflowPower>` | 与 `CurrentOverflowPower` 自身钩子有相互触发风险，需判定顺序 |
| 6 | 电流外溢可对目标自身结算 | 环流 | ✅ 已实现 | 低 | `CurrentOverflowPower.EnableSelfSplash()` | 只需一张 Power 卡调用它 |
| 7 | 全体 1 次 + 每有一种负面效果再 1 次 | 降雷 | ✅ | 中 | 逐敌循环 + `DamageCmd.Attack().WithHitCount()` + `PowerModel.TypeForCurrentAmount == PowerType.Debuff` | 每次数**因敌而异**，不能用 `ModifyAttackHitCount`（全局） |
| 8 | 目标每有一种负面效果 +2 费 | 魔力汲取 | ✅ | 低 | `target.Powers.Count(...)` + `PlayerCmd.GainEnergy` | 原版 `Rend`/`Misery` 直接照抄 |
| 9 | 回合开始把上回合最后 1~2 张牌回手 | 回忆 | ✅ | 中 | `CombatManager.Instance.History.CardPlaysFinished` + `HappenedLastPlayerTurn` | 原版 `HistoryCourse`（遗物）+ `Bolas`（卡）已示范 |
| 10 | 本次战斗结束额外 1 组卡牌奖励 | 修行 | ✅ | 低 | `CombatRoom.AddExtraReward` + `new CardReward(...)` | **原版 `TheHunt` 卡逐行示范**，无需奖励钩子 |
| 11 | 抽 3；选 1 技能消耗，否则弃全部手牌 | 占卜 | ✅ | 低-中 | `CardCmd.Draw` + `CardSelectCmd.FromHand(prefs(0,1))` + `CardCmd.Discard` | "否则"= `MinSelect = 0` 后直接确认 |
| 12 | 施加 2 燃烧，按场上最高燃烧层抽牌，选至多该数量消耗 | 真理派对 | ✅ | 中 | `Creature.GetPowerAmount<BurnPower>()` + 动态 `CardSelectorPrefs(0, n)` | 动态 MaxSelect 已验证（`Purity`） |
| 13 | 随机（升级）攻击/技能/能力各 1 张入手，获消耗、本回合 0 费 | 暗黑神秘学 | ✅ | 低 | `CardFactory.GetDistinctForCombat` + `CardCmd.Upgrade` + `CardCmd.ApplyKeyword(Exhaust)` + `SetToFreeThisTurn()` | 三段都有原版先例（`Splash`/`Largesse`/`WhiteNoise`/`Metamorphosis`） |
| 14 | 每次施加本回合未施加过的负面效果 → +1 费抽 1~2 | 全知形态 | ⚠️ | **高** | `AfterPowerAmountChanged` + `CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()` | 受机制 2 同一漏斗缺陷影响；且"负面效果类型"需自定口径 |
| 15 | 保留 Retain | 燃烧之魂 | ✅ 原生 | 极低 | `CanonicalKeywords => [CardKeyword.Retain]` | 引擎已实现 |
| 16 | 固有 Innate | 交流电 | ✅ 原生 | 极低 | `CanonicalKeywords => [CardKeyword.Innate]` | 引擎已实现 |

---

## 2. 卡牌编写规范（本项目的准确样板）

### 2.1 `[Pool]` 是硬性要求 —— 漏了会**中止整个游戏启动**

`BaseLib.Patches.Content.CustomContentDictionary.AddModel(Type)` 会抛：
`System.Exception: Model Xxx.Yyy must be marked with a PoolAttribute to determine which pool to add it to.`

- 每一个 `CustomCardModel` / `CustomRelicModel` / `CustomPotionModel` 子类都必须带 `[Pool(typeof(...))]`（**初始遗物也要**）。
- 卡池类（`CustomCardPoolModel` 等）与角色类不需要。
- 属性类型：`BaseLib.Utils.PoolAttribute`（`sealed class PoolAttribute : Attribute`，可传 `Type` 或池类名字符串，`Inherited = true`）。
- `CustomCardModel` 的构造函数在 `autoAdd: true`（默认）时调用 `CustomContentDictionary.AddModel(GetType())`（`BaseLib.Abstracts/CustomCardModel.cs:71-81`）；若直接继承 `CardModel`，必须自己写默认构造函数并调用它。

### 2.2 本项目的新卡样板（照抄即可）

```csharp
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;

namespace Wizard.Scripts.Cards;

/// <summary>火苗 - 0 费 - 攻击 - 普通：造成 5(7) 点火焰伤害，给予 1 层燃烧。</summary>
[Pool(typeof(WizardCardPool))]           // ← 必须；漏了会 fatal error
public class FlameSpark : WizardSpellCard  // 法术牌用 WizardSpellCard；非伤害牌用 WizardCardModel
{
    public override SpellSchool School => SpellSchool.Fire;   // 法术牌必须实现

    // 基础数值。DamageVar 会自动提供 DynamicVars.Damage
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new DynamicVar("Burn", 1m),      // 自定义数值用字符串键，描述里写 {Burn}
    ];

    public FlameSpark()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)         // alpha/beta 均要求传 cardPlay
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_molten_fist")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}
```

### 2.3 可覆写的常用成员（精确签名）

来自 `game/MegaCrit.Sts2.Core.Models/CardModel.cs`：

| 成员 | 签名 | 说明 |
|---|---|---|
| 构造函数 | `CustomCardModel(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true, bool autoAdd = true)` | 基类 `CardModel` 的同名构造函数 |
| `CanonicalVars` | `protected virtual IEnumerable<DynamicVar> CanonicalVars` | 基础数值；`DynamicVars.Xxx` 按名字索引 |
| `CanonicalKeywords` | `public virtual IEnumerable<CardKeyword> CanonicalKeywords` | 静态关键字（`Array.Empty` 为默认） |
| `CanonicalTags` | `protected virtual HashSet<CardTag> CanonicalTags` | `[CardTag.Strike]` 等；`Tags` 属性读取它并缓存 |
| `GainsBlock` | `public virtual bool GainsBlock => false` | 防御类牌必须为 true，游戏据此识别格挡牌。`CustomCardModel` 已把它改成"含 `BlockVar`/`CalculatedBlockVar` 时自动 true"（`CustomCardModel.cs:25-29`） |
| `ExtraHoverTips` | `protected virtual IEnumerable<IHoverTip> ExtraHoverTips` | `HoverTipFactory.FromKeyword/FromPower<T>/FromCard<T>/Static`；`base.EnergyHoverTip` 是 protected |
| `OnPlay` | `protected virtual Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)` | 效果主体 |
| `OnUpgrade` | `protected virtual void OnUpgrade()` | 升级；用 `DynamicVars.X.UpgradeValueBy(n)` / `EnergyCost.UpgradeBy(-1)` / `AddKeyword` / `RemoveKeyword` |
| `HasTurnEndInHandEffect` + `OnTurnEndInHand` | `public virtual bool HasTurnEndInHandEffect` / `protected virtual Task OnTurnEndInHand(PlayerChoiceContext)` | 手牌中回合结束触发（原版 `Burn` 用） |
| `PortraitPath` / `CustomPortraitPath` | `public virtual string PortraitPath` / `public virtual string? CustomPortraitPath` | 见下 |

### 2.4 卡图路径

本项目基类已封装（`src/Scripts/Cards/WizardCardModel.cs:26`）：

```csharp
public override string? CustomPortraitPath => $"res://Wizard/images/cards/{GetType().Name}.png";
```

- **文件名必须等于类名**：`Fireball` → `src/Wizard/images/cards/Fireball.png`。
- 尺寸任意，官方普通卡 250×190（正式资源建议 1000×760），先古卡 250×351；不需要裁剪。

### 2.5 本地化键

`src/Wizard/localization/{zhs,eng}/cards.json`：

```json
{
  "WIZARD-FLAME_SPARK.title": "火苗",
  "WIZARD-FLAME_SPARK.description": "造成 {Damage:diff()} 点火焰伤害，给予 {Burn:diff()} 层燃烧。"
}
```

- ID 规则：`{命名空间首段大写}-{类名 UPPER_SNAKE}`。`FlameSpark` → `WIZARD-FLAME_SPARK`。
- `:diff()` 让数值在升级预览时变绿/红。
- 关键字（Exhaust/Retain 等）**不要写进描述**，引擎自动追加；自定义关键字要写 `card_keywords.json`（见 §3）。

---

## 3. 关键字参考

### 3.1 原版 `CardKeyword` 全量（`game/MegaCrit.Sts2.Core.Entities.Cards/CardKeyword.cs`）

| 值 | 序数 | 含义（来自源码注释与实现） |
|---|---|---|
| `None` | 0 | 占位，无意义 |
| `Exhaust` | 1 | 打出后进入消耗堆。`CardModel.GetResultLocationForCardPlay`（`CardModel.cs:2090`）→ `OnPlayWrapper`（`CardModel.cs:1998`）→ `CardCmd.Exhaust`，因此会走 `AfterCardExhausted` + 写 `CardExhaustedEntry` 历史 |
| `Ethereal` | 2 | 回合结束时若仍在手牌则消耗（`CombatManager.cs:1615`）；HoverTip 会自动附带 Exhaust |
| `Innate` | 3 | 战斗第 1 回合置于抽牌堆顶并抬高起手抽牌数（`CombatManager.cs:913`） |
| `Unplayable` | 4 | 无法打出（`CardCmd.AutoPlay` 会直接送进结果堆，`CardCmd.cs:58`） |
| `Retain` | 5 | 回合结束留在手牌（`CardModel.ShouldRetainThisTurn` / `CombatManager.cs:1794`） |
| `Sly` | 6 | 弃掉时自动打出（`CardCmd.DiscardAndDraw` 的 `slyCards` 分支） |
| `Eternal` | 7 | 不可移除（`CardModel.IsRemovable => !Keywords.Contains(CardKeyword.Eternal)`） |

`CardKeywordOrder.cs` 定义了卡面上关键字的书写顺序（Ethereal → Retain → Innate…）。
`CardModel` 另有一套**不入 enum** 的单回合机制：`GiveSingleTurnRetain()` / `GiveSingleTurnSly()`（`CardModel.cs:1348/1357`）与 `CardCmd.ApplySingleTurnRetain/Sly`。

**结论：机制 15（Retain）与 16（Innate）只是加一个枚举值，零额外工作。**

```csharp
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
// 或按升级给：protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
```

### 3.2 自定义关键字（BaseLib）

`reference/docs/baselib-wiki-src/docs/utilities/enums.md` + `BaseLib.Patches.Content/`：

```csharp
public static class WizardKeywords
{
    // 最终 ID = {前缀}-{自定义名}，例如 WIZARD-ARCANE
    [CustomEnum("ARCANE")]
    [KeywordProperties(AutoKeywordPosition.Before)]   // 自动追加到描述前；After 为之后
    public static CardKeyword Arcane;
}
```

- 属性签名：
  - `CustomEnumAttribute(string? name = null)`，`AttributeTargets.Field`。
  - `KeywordPropertiesAttribute(AutoKeywordPosition position)` / `(AutoKeywordPosition position, bool richKeyword = true)`；`AutoKeywordPosition { None, Before, After }`。
- 引用时**用定义它的类名**，不要用 `CardKeyword.Arcane`：
  `Keywords.Contains(WizardKeywords.Arcane)`。
- 需要 `src/Wizard/localization/{zhs,eng}/card_keywords.json`：

```json
{ "WIZARD-ARCANE.title": "奥术", "WIZARD-ARCANE.description": "……" }
```

- ⚠️ `[KeywordProperties]` 只负责**显示**，不提供任何行为逻辑。若关键字要有效果，需自己写钩子或 singleton（BaseLib 教程 `15 - 添加单例` 明确建议用 `CustomSingletonModel` 承载关键字逻辑）。
- ⚠️ `CardKeyword` 在 STS2 里只用于"无数字单词"。带数字的效果应该用 `DynamicVar` + tooltip，而不是关键字。

---

## 4. 逐机制详解

### 机制 1 — 燃烧之魂：本回合攻击额外造成等量火焰伤害 + 1 燃烧

**结论：可实现。** 用一张临时 Power（对手回合结束移除），挂 `AfterDamageGiven`。

关键签名（`game/MegaCrit.Sts2.Core.Models/AbstractModel.cs:593`）：

```csharp
public virtual Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer,
    DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
```

`Hook.AfterDamageGiven(...)` 在 `CreatureCmd.Damage` 里**每份伤害结算一次**（`CreatureCmd.cs:411`，条件只有 `combatState != null`）。

```csharp
public class BurningSoulPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;   // 只是"本回合存在"
    public override string? CustomPackedIconPath => "res://Wizard/images/powers/burning_soul_power.png";
    public override string? CustomBigIconPath    => "res://Wizard/images/powers/burning_soul_power_big.png";

    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner) return;                       // 只处理自己造成的
        if (!props.IsPoweredAttack()) return;              // ← 递归护栏（关键！）
        if (result.TotalDamage <= 0) return;
        if (!target.IsAlive) return;

        // 额外火焰伤害：等量、不受加成（cardSource 传 null）
        await CreatureCmd.Damage(ctx, target, result.TotalDamage,
            ValueProp.Unpowered, null, null);

        await SpellCmd.ApplyBurn(ctx, target, 1m, Owner, null);
    }

    // 本回合结束后移除（写法与 FrostWallPower 一致）
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player) await PowerCmd.Remove(this);
    }
}
```

卡：

```csharp
[Pool(typeof(WizardCardPool))]
public class BurningSoul : WizardCardModel
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Burn", 1m)];
    public BurningSoul() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        await PowerCmd.Apply<BurningSoulPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
}
```

**坑**

1. **递归**：额外伤害必须是 `ValueProp.Unpowered`（不带 `Move`）。`ValuePropExtensions.IsPoweredAttack()`（`ValuePropExtensions.cs`）要求 `Move` 且非 `Unpowered`，所以额外伤害不会再触发本钩子。这正是原版 `EnvenomPower` 的写法（`EnvenomPower.cs`：`dealer == Owner && props.IsPoweredAttack() && result.UnblockedDamage > 0`）。
2. **"等量"的口径**：`DamageResult.TotalDamage = BlockedDamage + UnblockedDamage`（`DamageResult.cs:63`）。多段攻击（如火球术 2 段）会**每段各触发一次**，即"每次攻击"被理解为"每份伤害"。若你要的是"每次攻击指令只加成一次"，改用 `AfterAttack` + `command.Results.SelectMany(...)`（`AttackCommand.Results`，`AttackCommand.cs:149`）。
3. **双重加成**：额外伤害若把攻击牌当 `cardSource` 传进去，`SpellPowerPower`（`cardSource.IsSpell()`）和 `ChillPower`（`cardSource.SchoolOf()`）会**再乘/再加一次**。传 `null` 是"等量、不吃加成"的字面实现；要让它吃加成则显式传 `cardSource`——两者语义不同，必须刻意选。
4. **致死**：`CreatureCmd.Damage` 内部会处理死亡；不要在循环里对已死目标继续操作，加 `if (!target.IsAlive)`。
5. **Power 也要 `[Pool]`？** 不需要 —— `[Pool]` 只针对会进卡池/遗物池/药水池的模型。Power 通过 `PowerCmd.Apply<T>` 使用。

---

### 机制 2 — 纵火狂：每次施加燃烧 → +1 法术增益

**结论：可实现，但当前 `SpellCmd.ApplyBurn` 会让它只触发第一次。这是本次评估里最需要先修的结构性问题。**

钩子签名（`AbstractModel.cs:1077`）：

```csharp
public virtual Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
    decimal amount, Creature? applier, CardModel? cardSource)
```

原版最接近的先例：`SleightOfFleshPower.cs`（"每次对敌人施加负面效果 → 造成伤害"），判据为
`power.GetTypeForAmount(amount) == PowerType.Debuff && power.Owner.IsEnemy && applier == base.Owner && !(power is ITemporaryPower)`。

```csharp
public class ArsonistPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext ctx, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0m) return;
        if (applier != Owner) return;
        if (power is not BurnPower) return;

        Flash();
        await PowerCmd.Apply<SpellPowerPower>(ctx, Owner, Amount, Owner, null);
    }
}
```

**坑（务必先修）**

- `src/Scripts/Spells/SpellCmd.cs:33-45` 现在的写法是：
  ```csharp
  BurnPower? existing = target.GetPower<BurnPower>();
  if (existing == null) { await PowerCmd.Apply<BurnPower>(...); return; }
  if (amount > existing.Amount) existing.SetAmount((int)amount);   // ← 不走任何钩子
  ```
  `PowerModel.SetAmount`（`PowerModel.cs:542`）只改数值，**不会**调用 `Hook.AfterPowerAmountChanged`，也**不会**写 `CombatManager.History.PowerReceived`，更不会触发 `Hook.AfterModifyingPowerAmountGiven/Received`。所以：
  - "首次给某敌人上燃烧"会触发；
  - "把已有燃烧从 2 层顶到 3 层"**不会**触发。
- **推荐修法**（保持"取高值"语义且恢复钩子）：把 `existing.SetAmount(...)` 换成
  ```csharp
  if (amount > existing.Amount)
      await PowerCmd.ModifyAmount(choiceContext, existing,
          amount - existing.Amount, applier, cardSource);
  ```
  `PowerCmd.ModifyAmount` 内部 `newAmount = power.Amount + offset`（`PowerCmd.cs`），偏移量取 `amount - existing.Amount` 正好得到 `amount`，且会依次触发 `Hook.BeforePowerAmountChanged` → `SetAmount` → `Hook.AfterPowerAmountChanged` → 写历史。这正是机制 14 也依赖的东西。
- **替代修法**：在 `SpellCmd` 里加一个自定义事件（或 `Action<Creature, int>` 委托），由 `WizardSubscriber`/Power 订阅。适用于你不想改动 `PowerCmd` 调用链的情况。观者 mod 用的是 `WatcherSubscriber` + `WatcherHook` 这一类自建事件（`Watcher.Code.Events/`），可以照抄。
- `AfterPowerAmountChanged` 是**战斗钩子**，所有战斗中的 `PowerModel` 都会收到，所以必须在钩子里按 `applier == Owner` 过滤，否则敌人给你上燃烧也会给自己加层。
- 若只想对"敌人身上的燃烧"计数，加 `power.Owner.IsEnemy`（对齐 `SleightOfFleshPower`）。

---

### 机制 3 — 凝神：每次消耗牌 → +1 法术增益

**结论：可实现，风险低。**

钩子签名（`AbstractModel.cs:447`）：

```csharp
public virtual Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
```

触发点只有一处（`CardCmd.Exhaust`，`CardCmd.cs:237-248`），而它被这些路径调用：

| 触发源 | 位置 |
|---|---|
| 打出带 `Exhaust` 关键字的牌 | `CardModel.OnPlayWrapper` → `CardModel.cs:1998` |
| 指定牌被效果消耗（`Purity`/`TrueGrit`/`Brand`…） | 显式调用 `CardCmd.Exhaust` |
| Ethereal 回合结束消耗 | `CombatManager.cs:1622`、`CombatManager.cs:1672` |
| 手牌中的回合结束效果牌被消耗 | `CombatManager.ResolveTurnEndCardEffects`（`CombatManager.cs:1672`） |

```csharp
public class ConcentrationPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardExhausted(PlayerChoiceContext ctx, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner.Player) return;      // 只要自己的牌
        Flash();
        await PowerCmd.Apply<SpellPowerPower>(ctx, Owner, Amount, Owner, null);
    }
}
```

**坑**

- `card.Owner` 是 `Player`；`Owner`（Power）是 `Creature`，比较要用 `Owner.Player`。
- 敌人的牌被消耗也会广播到你的 Power（战斗钩子是全局广播），必须过滤。
- `causedByEthereal` 可用来做"Ethereal 消耗不计"之类的细分。
- 原版先例：`Midnight.cs` 用 `AfterCardExhausted` 减费；`EvilEye.cs` 用历史 `CardExhaustedEntry` 判定"本回合是否消耗过"。**观者 mod 完全没有用过 `AfterCardExhausted`（0 处）**，所以只能照原版 `Midnight` 抄。

---

### 机制 4 — 决心：0 费净赚 3 费

**结论：完全原生，最低风险。**

```csharp
public static async Task GainEnergy(decimal amount, Player player)   // PlayerCmd.cs:29
```

```csharp
[Pool(typeof(WizardCardPool))]
public class Determination : WizardCardModel
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3),                     // 键名固定为 "Energy"，描述写 {Energy}
        new PowerVar<VulnerablePower>(1m),
        new DynamicVar("Frail", 1m),
        new DynamicVar("Burn", 3m),
    ];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [EnergyHoverTip];

    public Determination() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<VulnerablePower>(ctx, Owner.Creature, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<FrailPower>(ctx, Owner.Creature, DynamicVars["Frail"].BaseValue, Owner.Creature, this);
        await SpellCmd.ApplyBurn(ctx, Owner.Creature, DynamicVars["Burn"].BaseValue, Owner.Creature, this);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);    // ← 核心
    }

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1m);
}
```

**坑**

- 0 费 + 赚费 = 构造函数第一个参数写 `0`，费用用 `EnergyVar` 单独承载；二者互不冲突（原版 `Adrenaline` 0 费 `EnergyVar(1)` + 抽 2）。
- `GainEnergy` 会经过 `Hook.ModifyEnergyGain` / `Hook.AfterModifyingEnergyGain`（`PlayerCmd.cs:34-35`），所以"无法获得能量"类 Debuff（`NoEnergyGainPower`）会正确拦下。
- 战斗结束/结束中会静默不生效（`if (!(amount <= 0m) && !CombatManager.Instance.IsEnding)`）。
- 若想要"净赚"的显示效果，`ExtraHoverTips => [EnergyHoverTip]`（`CardModel.EnergyHoverTip` 是 `protected IHoverTip`，`CardModel.cs:257`）。
- 原版先例：`Adrenaline`、`Turbo`、`Offering`、`Bloodletting`、`DoubleEnergy`（`DoubleEnergy` 直接读 `Owner.PlayerCombatState.Energy`）。

---

### 机制 5 — 交流电：雷电伤害额外施加 1 层电流外溢

**结论：可实现，低风险，但与机制 6 的既有实现存在相互触发顺序问题。**

先看已有的 `CurrentOverflowPower`（`src/Scripts/Powers/CurrentOverflowPower.cs`）：它自己就挂在玩家的 `AfterDamageGiven` 上，判据是 `dealer == Owner && cardSource.SchoolOf() == SpellSchool.Lightning`，并对溅射伤害传 `cardSource: null`（注释明确说这是为了防无限递归）。

```csharp
public class AlternatingCurrentPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner) return;
        if (cardSource.SchoolOf() != SpellSchool.Lightning) return;   // 复用 SpellUtil 扩展
        if (result.TotalDamage <= 0) return;

        await PowerCmd.Apply<CurrentOverflowPower>(ctx, Owner, Amount, Owner, null);
    }
}
```

**坑**

1. **顺序敏感**：溅射伤害本身 `cardSource` 为 `null`（`SchoolOf()` → `SpellSchool.None`），所以不会再次触发本钩子 —— 与 `CurrentOverflowPower` 的防递归约定一致，两条链不会互相点火。
2. 但**同一份雷电伤害**会让 `CurrentOverflowPower.AfterDamageGiven` 与 `AlternatingCurrentPower.AfterDamageGiven` 都在同一钩子轮次里被调用；`AlternatingCurrentPower` 加的层数**不会**让本次伤害多溅射一次（因为 `CurrentOverflowPower` 里 `Amount` 是在它自己执行时读取的）。这在语义上是好事（避免"先加层再放大本次"的循环），但**顺序依赖 `Hook` 的遍历顺序**（`Hook.IterateCombatHookListeners` 按模型排序），行为不稳定。若要求严格确定性，应在钩子里快照 `Amount` 或显式延迟到 `AfterAttack`。
3. "雷电伤害"的判定依赖 `ISpellCard.School`，不是 `ValueProp`，所以**非卡牌来源**（Power/遗物造成的雷电伤害）无法被识别 —— 这是当前架构的固有局限。
4. 该卡 design.md 标注 `固有`，直接 `CanonicalKeywords => [CardKeyword.Innate]`。

---

### 机制 6 — 环流：电流外溢可对目标自身结算

**结论：已经实现。** `src/Scripts/Powers/CurrentOverflowPower.cs`：

```csharp
private class OverflowData { public bool AllowSelfSplash; }
protected override object? InitInternalData() => new OverflowData();
public void EnableSelfSplash() => GetInternalData<OverflowData>().AllowSelfSplash = true;
```

卡只需要：

```csharp
[Pool(typeof(WizardCardPool))]
public class Circulation : WizardCardModel
{
    public Circulation() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        await PowerCmd.Apply<CurrentOverflowPower>(ctx, Owner.Creature, 1m, Owner.Creature, this);
    // 若环流自身也是"开关"，则在 AfterApplied 里调用 (Owner.GetPower<CurrentOverflowPower>())?.EnableSelfSplash()
}
```

**坑**

- 目前 `EnableSelfSplash()` 改的是**电泳外溢 Power 实例**上的内部数据，而不是一个全局开关。如果 `CurrentOverflowPower` 因任何原因被移除/重建（例如层数归零、`ShouldRemoveDueToAmount`），标志位会丢失。若"环流"是永久性能力，建议把开关放到 `SpireField<PlayerCombatState, bool>` 或让 `CurrentOverflowPower` 在 `AfterApplied` 时查一次。
- 多实例：`CurrentOverflowPower` 的 `InstanceType` 是默认 `PowerInstanceType.None`，`PowerCmd.Apply` 会走 `FindExistingInstanceForStacking` 复用同一实例，所以标志位通常不会丢。
- `InitInternalData()` / `GetInternalData<T>()`（`PowerModel.cs:512/523`）是官方推荐的"私有数据"承载方式（注释明确说克隆时会重置）。

---

### 机制 7 — 降雷：全体 1 次 + 敌每有 1 种负面效果再 1 次

**结论：可实现。关键点是"每种效果只算一次"，且重复次数因敌而异 —— 不能用 `ModifyAttackHitCount`。**

可用的原件：

- `AbstractModel.ModifyAttackHitCount(AttackCommand attack, int hitCount) → int`（`AbstractModel.cs:1436`）：**对整次攻击统一生效**，无法按目标区分。`AttackCommand.Execute` 里 `decimal attackCount = Hook.ModifyAttackHitCount(combatState, this, _hitCount)`（`AttackCommand.cs:550`），对所有目标同一份计数。
- `AttackCommand.WithHitCount(int)`（`AttackCommand.cs:488`）。
- 去重计数的原版先例：`Rend.cs`
  ```csharp
  new CalculatedDamageVar(ValueProp.Move).WithMultiplier(
      (CardModel card, Creature? target) => target?.Powers.Count(ShouldCountPower) ?? 0)
  ...
  private static bool ShouldCountPower(PowerModel power)
      => power.TypeForCurrentAmount == PowerType.Debuff && !(power is ITemporaryPower);
  ```
  注意 `Rend` 数的是**实例数**，不是**去重后的种类数**；你要的是种类数，需要 `GroupBy(p => p.Id)`。

```csharp
protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
{
    ICombatState? cs = CombatState;
    if (cs == null) return;

    foreach (Creature enemy in cs.HittableEnemies.ToList())
    {
        if (!enemy.IsAlive) continue;

        int kinds = enemy.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff && p is not ITemporaryPower)
            .Select(p => p.Id)              // ← 按 ModelId 去重 = "不同种类"
            .Distinct()
            .Count();

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .Targeting(enemy)               // ← 逐敌单目标
            .WithHitCount(1 + kinds)
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(ctx);
    }
}
```

**坑**

1. **语义取舍**：design.md 写"对全体敌人造成 8 点雷电伤害。敌人具有的负面效果每有 1 种，就再结算 1 次"。逐敌循环 = 每个敌人独立计算次数（符合字面）。若想"一次 AOE 打 N 遍"，则用 `.TargetingAllOpponents(cs).WithHitCount(1 + maxKinds)` —— 但那样次数对全体相同。
2. **逐敌循环 = N 次独立攻击**：`BeforeAttack`/`AfterAttack` 会各触发 N 次。这会放大 `CurrentOverflowPower` 的溅射（每敌每次雷电伤害都触发）、`GigantificationPower`（`BeforeAttack` 里给 `commandToModify` 打标记，`AfterAttack` 里消耗层数 —— 逐敌循环会让它在第一敌后就把层数扣掉）。**这是本机制最实际的副作用。**
3. **去重口径**：`ITemporaryPower`（临时力量/敏捷等）原版 `Rend`/`Misery` 都不计入；`PowerInstanceType.Instanced` 的 Power（一个 Boss 多份）会共享 `Id`，`Distinct()` 会正确合并。
4. **`TypeForCurrentAmount` 而非 `Type`**：`PowerModel.TypeForCurrentAmount`（`PowerModel.cs:240`）会处理"层数为负时 Buff 视为 Debuff"（如负力量），比 `Type` 更准。
5. **目标死亡**：`AttackCommand.Execute` 内部每段都会重新过滤 `c.IsAlive`；但外层循环请自行 `ToList()` 快照并检查 `IsAlive`，避免 `InvalidOperationException`。

---

### 机制 8 — 魔力汲取：目标每有 1 种负面效果 +2 费

**结论：可实现，低风险（`Rend` + `PlayerCmd.GainEnergy` 的组合）。**

```csharp
protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
{
    ArgumentNullException.ThrowIfNull(play.Target, nameof(play.Target));

    int kinds = play.Target.Powers
        .Where(p => p.TypeForCurrentAmount == PowerType.Debuff && p is not ITemporaryPower)
        .Select(p => p.Id).Distinct().Count();

    if (kinds > 0)
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue * kinds, Owner);
}
```

**坑**

- 费用是"每存在 1 种"而不是"每层"，所以不能直接用 `PowerVar`；描述里建议用 `{Energy:diff()}` 并写"每有一种负面效果，获得 {Energy} 点能量"。
- `PlayerCmd.GainEnergy` 同样过 `Hook.ModifyEnergyGain`。
- 战斗钩子时序：`OnPlay` 内的 `GainEnergy` 在卡牌结算过程中调用没有任何问题（`Adrenaline` 就是这样）。
- 若想让它参与"本卡消耗的能量"相关的钩子（例如某些"本回合已花费能量"统计），注意 `GainEnergy` 不会写 `EnergySpentEntry`（那是 `CardModel.SpendEnergy` 写的），行为符合预期。

---

### 机制 9 — 回忆：回合开始把上回合最后 1~2 张牌回手

**结论：可实现。原版有两个精确先例。**

- `game/MegaCrit.Sts2.Core.Models.Relics/HistoryCourse.cs`（遗物，回合开始的 `AutoPrePlay` 阶段恢复上回合最后一张攻击）：
  ```csharp
  public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
  {
      if (player == base.Owner && base.Owner.PlayerCombatState.TurnNumber != 1)
      {
          CardModel? card = CombatManager.Instance.History.CardPlaysFinished
              .LastOrDefault(e => e.CardPlay.Player == base.Owner
                               && e.HappenedLastPlayerTurn(base.Owner)
                               && e.CardPlay.Card.Type == CardType.Attack
                               && !e.CardPlay.Card.IsDupe)?.CardPlay.Card;
          if (card != null) { Flash(); await CardCmd.AutoPlay(choiceContext, card.CreateDupe(player), null); }
      }
  }
  ```
- `game/MegaCrit.Sts2.Core.Models.Cards/Bolas.cs`（卡牌，`BeforeHandDraw` 里把自己从弃牌堆捞回手）：
  ```csharp
  public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
  {
      if (player == base.Owner && CombatManager.Instance.History.CardPlaysFinished
              .Any(e => e.HappenedLastPlayerTurn(base.Owner) && e.CardPlay.Card == this))
      {
          CardPile? pile = base.Pile;
          if (pile == null || pile.Type != PileType.Hand)
              await CardPileCmd.Add(this, PileType.Hand);
      }
  }
  ```

关键 API：

| 成员 | 位置 | 签名 |
|---|---|---|
| `CombatManager.Instance.History` | `CombatManager.cs` | `CombatHistory` |
| `.CardPlaysFinished` | `CombatHistory.cs:28` | `IEnumerable<CardPlayFinishedEntry>` |
| `.CardPlaysStarted` | `CombatHistory.cs:26` | `IEnumerable<CardPlayStartedEntry>` |
| `.Entries` | `CombatHistory.cs:24` | `IEnumerable<CombatHistoryEntry>` |
| `CardPlayFinishedEntry.CardPlay` | `Entries/CardPlayFinishedEntry.cs:13` | `CardPlay`（含 `.Card`/`.Player`/`.Target`/`.PlayIndex`/`.IsAutoPlay`） |
| `CombatHistoryEntry.HappenedLastPlayerTurn(Player)` | `CombatHistoryEntry.cs:99` | `bool` —— 按 `_playerTurnNumbers[netId] == 当前 TurnNumber - 1` 判定 |
| `CombatHistoryEntry.HappenedThisTurn(ICombatState?)` | `CombatHistoryEntry.cs:62` | `bool` |

```csharp
public class MemoryPower : CustomPowerModel
{
    private const string CountKey = "RecallCount";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(CountKey, 1m)];

    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext ctx, Player player)
    {
        if (player != Owner.Player) return;

        int want = (int)DynamicVars[CountKey].BaseValue;
        List<CardModel> lastTurn = CombatManager.Instance.History.CardPlaysFinished
            .Where(e => e.CardPlay.Player == Owner.Player
                     && e.HappenedLastPlayerTurn(Owner.Player)
                     && !e.CardPlay.IsAutoPlay
                     && !e.CardPlay.Card.IsDupe)
            .Select(e => e.CardPlay.Card)
            .Distinct()
            .TakeLast(want)        // 上回合"最后 N 张"
            .ToList();

        foreach (CardModel card in lastTurn)
        {
            if (card.Pile == null) continue;
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }
}
```

**坑**

1. **实例身份**：`CardPlaysFinished` 里的 `CardPlay.Card` 是**当时那张可变实例**。它可能已经在弃牌堆/消耗堆，甚至已被移出战斗（消耗堆仍在）。`CardPileCmd.Add(card, PileType.Hand)` 没有"必须有 pile"的限制，但从**消耗堆**捞回来会让"消耗"语义失效（若你不想要，加 `if (card.Pile?.Type == PileType.Exhaust) continue;`）。
2. **`AddGeneratedCardToCombat` 不能用于已存在的牌**：`CardPileCmd.cs:292` 会抛 `InvalidOperationException("You are not allowed to generate cards that already have a pile")`。要用 `CardPileCmd.Add`。若想给一张"复制品"，用 `card.CreateClone()`（要求原牌在战斗堆里，`CardModel.cs:2184`）或 `card.CreateDupe(player)`（`CardModel.cs:2215`，会移除 `Exhaust` 关键字并把 `IsDupe` 置真）。
3. **去重**：同一张牌在一回合内被打出两次会产生两条 `CardPlayFinishedEntry`（`PlayIndex` 区分），`Distinct()` 会合并。
4. **时机选择**：`AfterAutoPrePlayPhaseEntered`（回合开始自动出牌阶段之后，`AbstractModel.cs:280`）与 `BeforeHandDraw`（`AbstractModel.cs:779`，抽牌前，`Bolas` 用它把牌塞回手再抽）语义不同。
   - 想"进手 + 参与本回合"→ 用 `BeforeHandDraw`/`BeforeHandDrawLate`。
   - 想"进手但不被本回合抽牌数影响"→ 用 `AfterPlayerTurnStartEarly` / `AfterAutoPrePlayPhaseEntered`。
5. **`TurnNumber != 1` 守卫**：第 1 回合没有"上回合"，`HappenedLastPlayerTurn` 会返回 false，但显式守卫更省事（照抄 `HistoryCourse`）。
6. **多人**：`HappenedLastPlayerTurn` 按 `NetId` 判定，天然支持联机；但每张牌在每个客户端读取的历史必须一致 —— 所以**不能用本地时间/本地随机数**决定恢复哪张。

---

### 机制 10 — 修行：本次战斗结束额外获得 1 组卡牌奖励

**结论：可实现，且原版有一张卡逐行示范 —— 不需要 `TryModifyRewards`。**

**原版 `TheHunt`（`game/MegaCrit.Sts2.Core.Models.Cards/TheHunt.cs`）**：

```csharp
AbstractRoom currentRoom = base.CombatState.RunState.CurrentRoom;
if (currentRoom is CombatRoom combatRoom)
{
    // ... 造成伤害、判定击杀 ...
    combatRoom.AddExtraReward(base.Owner,
        new CardReward(CardCreationOptions.ForRoom(base.Owner, combatRoom.RoomType), 3, base.Owner));
    await PowerCmd.Apply<TheHuntPower>(choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
}
```

相关签名：

| 成员 | 位置 | 签名 |
|---|---|---|
| `CombatRoom.AddExtraReward` | `Rooms/CombatRoom.cs:185` | `public void AddExtraReward(Player player, Reward reward)` |
| `CombatRoom.ExtraRewards` | `CombatRoom.cs:53` | `IReadOnlyDictionary<Player, List<Reward>>` |
| `CardReward` ctor | `Rewards/CardReward.cs:110` | `public CardReward(CardCreationOptions options, int cardCount, Player player, PlayerChoiceSynchronizer? synchronizer = null)` |
| `CardCreationOptions.ForRoom` | `Runs/CardCreationOptions.cs:80` | `public static CardCreationOptions ForRoom(Player player, RoomType roomType)` |
| 汇入奖励界面 | `Rewards/RewardsSet.cs` `WithRewardsFromRoom` | `if (Room is CombatRoom combatRoom && combatRoom.ExtraRewards.TryGetValue(Player, out var value)) Rewards.AddRange(value);` |
| 序列化 | `CombatRoom.ToSerializable/FromSerializable` | `ExtraRewards` 会存盘，读档后依然有效 |

```csharp
[Pool(typeof(WizardCardPool))]
public class Training : WizardCardModel
{
    public Training() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.RunState.CurrentRoom is CombatRoom room)
            room.AddExtraReward(Owner,
                new CardReward(CardCreationOptions.ForRoom(Owner, room.RoomType), 3, Owner));
        // Power 卡还会被 PowerCmd.Apply 放进能力栏
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
```

**坑**

1. **不需要 `TryModifyRewards`。** 那条路（`AbstractModel.TryModifyRewards(Player, List<Reward>, AbstractRoom?)`，`AbstractModel.cs:2140`，由 `Hook.ModifyRewards` 用 `runState.IterateHookListeners(null)` 驱动）确实对**牌库里的牌**也生效（`RunState.IterateHookListeners` 会把 `player.Deck.Cards` 加进监听者，`RunState.cs:554-561`），但**对战斗中的手牌不生效**（`childCombatState == null` 时不会遍历战斗堆）。所以对"能力牌"这条路线不可行。
2. 若坚持用钩子路线，唯一可行形态是 `CustomSingletonModel(HookType.Run)`（`ModHelper.IterateAllRunStateSubscribers` 会把它加入 `IterateHookListeners`，`RunState.cs:584`）。但 singleton 是**整个进程一份 canonical 实例**（`ModelDb.Singleton<T>()`），在上面存可变状态会跨局泄漏，必须把标志放到 `SpireField<Player, bool>`。既然 `TheHunt` 已有现成方案，不建议走这条。
3. **`ExtraRewards` 在战斗失败时也会留在房间里**，但 `RewardsSet.Offer()` 会 `if (Player.Creature.IsDead) return;`，所以不会出问题。
4. 若同时想要"能力牌本身在能力栏显示"，保留 `CardType.Power` 即可；原版 `TheHunt` 是攻击牌，另外补了一个 `TheHuntPower` 作为计数器。对"修行"这张一次性的牌，可以把"是否已发过奖励"也放在 `ExtraRewards` 的天然幂等上（每次打出加一组，符合设计）。

---

### 机制 11 — 占卜：抽 3；选 1 张技能消耗，否则弃掉所有手牌

**结论：可实现。关键是"否则"用 `MinSelect = 0` 表达。**

签名：

```csharp
// CardPileCmd.cs:985
public static Task<IEnumerable<CardModel>> Draw(PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw = false)

// CardSelectCmd.cs:817
public static async Task<IEnumerable<CardModel>> FromHand(PlayerChoiceContext context, Player player,
    CardSelectorPrefs prefs, Func<CardModel, bool>? filter, AbstractModel source)

// CardCmd.cs:157 / 237
public static async Task Discard(PlayerChoiceContext choiceContext, IEnumerable<CardModel> cards)
public static async Task<CardPileAddResult?> Exhaust(PlayerChoiceContext choiceContext, CardModel card,
    bool causedByEthereal = false, bool skipVisuals = false)
```

`CardSelectorPrefs`（`CardSelection/CardSelectorPrefs.cs`，**是 struct**）：

```csharp
public CardSelectorPrefs(LocString prompt, int selectCount)
public CardSelectorPrefs(LocString prompt, int minCount, int maxCount)
public bool RequireManualConfirmation { get; init; }   // 构造时 = MinSelect != MaxSelect
public bool Cancelable { get; init; }                  // 仅牌库类选择界面读取；手牌选择不用它
public bool PretendCardsCanBePlayed { get; init; }
public Func<CardModel, bool>? ShouldGlowGold { get; set; }
public static LocString ExhaustSelectionPrompt / DiscardSelectionPrompt / TransformSelectionPrompt / ...
```

手牌选择的确认按钮判定在 `NPlayerHand.RefreshSelectModeConfirmButton()`（`NPlayerHand.cs:1259-1269`）：`count >= MinSelect && count <= MaxSelect` 才可点 → **`MinSelect = 0` 就是"可以一张都不选后确认"**，即"否则"分支。

```csharp
protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
{
    await CardPileCmd.Draw(ctx, DynamicVars.Cards.BaseValue, Owner);

    if (PileType.Hand.GetPile(Owner).Cards.Count == 0) return;

    var prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 1)
    {
        PretendCardsCanBePlayed = true,
    };

    CardModel? chosen = (await CardSelectCmd.FromHand(
        ctx, Owner, prefs,
        c => c.Type == CardType.Skill && !c.Keywords.Contains(CardKeyword.Unplayable),
        this)).FirstOrDefault();

    if (chosen != null)
        await CardCmd.Exhaust(ctx, chosen);
    else
        await CardCmd.Discard(ctx, PileType.Hand.GetPile(Owner).Cards);   // 内部已 ToList()
}
```

**坑**

1. **过滤函数**必须是 `Func<CardModel, bool>`：`c => c.Type == CardType.Skill`。原版同类写法见 `DecisionsDecisions.cs:37`（技能 + 非 Unplayable）、`HandTrick.cs:30`（技能 + 非 Sly）。
2. **"全部手牌"可能包含占卜自己**。此时占卜已在 `PileType.Play`（`OnPlayWrapper` 第 1866 行把牌移到 Play 堆），所以不在手牌里。安全。
3. **`Cancelable` 对手牌选择无效**：`CardSelectCmd.FromHand` 不会读它（`Cancelable` 只在 `NDeckCardSelectScreen` / `NDeckUpgradeSelectScreen` 等牌库界面里用）。用 `MinSelect = 0`。
4. **注意 `RequireManualConfirmation`**：`(0, 1)` 时 `0 != 1` → true，UI 一定会弹出来；若用 `(1, 1)` 且手牌里只有 1 张可选项，`FromHand` 的快捷路径会**直接返回**而不弹 UI（`CardSelectCmd.cs:840`）。这正好是"只有 1 张技能牌时自动消耗"的合理行为。
5. 若"否则弃掉所有手牌"应该包含刚抽到的 3 张 —— 是的，`PileType.Hand.GetPile(Owner).Cards` 是当前手牌快照；`CardCmd.DiscardAndDraw` 内部会 `ToList()`（`CardCmd.cs:178`），所以直接传活集合是安全的。
6. `CardCmd.Exhaust` 会触发 `Hook.AfterCardExhausted`（→ 机制 3 会因此加层），这是特性不是 bug。

---

### 机制 12 — 真理派对：施加 2 燃烧；按场上最高燃烧层抽牌；选至多该数量消耗

**结论：可实现。动态 `MaxSelect` 已被原版验证（`Purity`）。**

```csharp
protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
{
    ICombatState? cs = CombatState;
    if (cs == null) return;

    // 1) 施加燃烧（全场敌人）
    List<Creature> enemies = cs.GetOpponentsOf(Owner.Creature).Where(c => c.IsAlive).ToList();
    await SpellCmd.ApplyBurnToAll(ctx, enemies, DynamicVars["Burn"].BaseValue, Owner.Creature, this);

    // 2) 场上最高燃烧层
    int highest = cs.Creatures.Select(c => c.GetPowerAmount<BurnPower>()).DefaultIfEmpty(0).Max();
    if (highest <= 0) return;

    // 3) 抽等量张
    await CardPileCmd.Draw(ctx, highest, Owner);

    // 4) 选至多 highest 张消耗
    var prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, highest);
    foreach (CardModel c in await CardSelectCmd.FromHand(ctx, Owner, prefs, null, this))
        await CardCmd.Exhaust(ctx, c);
}
```

关键 API：`Creature.GetPowerAmount<T>()`（`Creature.cs:596`，返回 `GetPower<T>()?.Amount ?? 0`）、`ICombatState.Creatures` / `.HittableEnemies`（`ICombatState.cs:33/90`）。

**坑**

1. **`CardPileCmd.Draw` 的 `count` 是 `decimal`**，内部 `(int)Math.Ceiling(count)`（`CardPileCmd.cs:1025`）；手牌上限 10 时超出会静默截断（`Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count)`，`CardPile.cs:21` → `MaxCardsInHand = 10`）。
2. **燃烧取高值语义**：`SpellCmd.ApplyBurn` 是"取最高值"。若敌人已有 3 层，本次"施加 2 层"不会降到 2，也不会升到 5。抽牌数应在施加**之后**计算（如上）。
3. **"场上"的口径**：`cs.Creatures` 含玩家与宠物。若只想算敌人，用 `cs.HittableEnemies`。**只有敌人会带 BurnPower** 时两者等价，但明确写出来更安全。
4. `highest` 可能远大于手牌数；`CardSelectorPrefs(prompt, 0, highest)` 的 `MaxSelect` 大于可选项数是可以的（`FromHand` 只在 `list.Count <= prefs.MinSelect` 时走快捷路径，`MinSelect = 0` 时不会误触发）。
5. **原版先例**：`Purity.cs:28`（`new CardSelectorPrefs(base.SelectionScreenPrompt, 0, base.DynamicVars.Cards.IntValue)` + 逐个 `CardCmd.Exhaust`）、`Guards.cs:26`（`(0, 999999999)`）、`Ashwater.cs:31` 同形。

---

### 机制 13 — 暗黑神秘学：随机（升级）攻击/技能/能力各 1 张入手，获消耗、本回合 0 费

**结论：可实现，三段都有原版先例。**

| 需求 | API | 原版先例 |
|---|---|---|
| 随机抽 1 张指定类型 | `CardFactory.GetDistinctForCombat(Player, IEnumerable<CardModel>, int, Rng)`（`CardFactory.cs:119`） | `WhiteNoise.cs`（`where c.Type == CardType.Power`）、`Distraction.cs`（`CardType.Skill`） |
| 升级 | `CardCmd.Upgrade(CardModel card, CardPreviewStyle style = HorizontalLayout)`（`CardCmd.cs:256`，**void**） | `Largesse.cs`（`if (base.IsUpgraded) CardCmd.Upgrade(cardModel)`）、`Splash.cs` |
| 加入手牌 | `CardPileCmd.AddGeneratedCardToCombat(CardModel, PileType, Player?, CardPilePosition = Bottom)`（`CardPileCmd.cs:267`） | `Discovery.cs`、`JackOfAllTrades.cs` |
| 获得消耗 | `CardCmd.ApplyKeyword(CardModel, params CardKeyword[])`（`CardCmd.cs:680`）或 `card.AddKeyword(CardKeyword.Exhaust)`（`CardModel.cs:1330`） | `SculptingStrike.cs`（给 Ethereal）、`Snap.cs`（给 Retain） |
| 本回合 0 费 | `CardModel.SetToFreeThisTurn()`（`CardModel.cs:1267`） | `Discovery.cs`、`WhiteNoise.cs`、`Splash.cs`、`Distraction.cs`、`Metamorphosis.cs` |

```csharp
protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
{
    Rng rng = Owner.RunState.Rng.CombatCardGeneration;
    IEnumerable<CardModel> pool = Owner.Character.CardPool
        .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint);

    foreach (CardType type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
    {
        CardModel? card = CardFactory
            .GetDistinctForCombat(Owner, pool.Where(c => c.Type == type), 1, rng)
            .FirstOrDefault();
        if (card == null) continue;

        if (IsUpgraded && card.IsUpgradable)
            CardCmd.Upgrade(card);                       // 随机"升级过"的

        CardCmd.ApplyKeyword(card, CardKeyword.Exhaust); // 获得消耗
        card.SetToFreeThisTurn();                        // 本回合 0 费
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner));
    }
}
```

**坑**

1. **`WithHitCount`/`SetToFreeThisTurn` 的顺序无所谓**，但 `CardCmd.Upgrade` 要求卡可升级（内部 `if (!card.IsUpgradable) continue;`，`CardCmd.cs:275`），并对 `PileType.Deck` 的牌写 run history —— 战斗中新生成的牌不入此分支。
2. **`GetDistinctForCombat` 已过滤**：`FilterForCombat` 会剔除 `!CanBeGeneratedInCombat`、`Rarity == Basic/Ancient/Event`（`CardFactory.cs:161`）。所以**基础牌（打击/防御）和先古牌不会被抽到** —— 这正是你要的，但如果巫师的基础牌想被抽到，需要给它们 `CanBeGeneratedInCombat => true` 并绕开 `FilterForCombat`（用 `Owner.Character.CardPool.GetUnlockedCards(...)` + 自己的随机）。
3. **`SetToFreeThisTurn()` = `EnergyCost.SetThisTurnOrUntilPlayed(0)` + `SetStarCostThisTurn(0)`**（`CardModel.cs:1267-1271`），即"本回合或直到打出"。战斗中新生成的牌还没进任何 pile，`SetToFreeThisTurn` 直接可用。
4. **`AddGeneratedCardToCombat` 会写 `CardGeneratedEntry` 历史**（`CardPileCmd.cs:308`）并触发 `Hook.AfterCardGeneratedForCombat`（`CardPileCmd.cs:311`）。如果以后做"每次生成卡牌时……"的效果，这里是入口。它也要求卡**还没有 pile**（`CardPileCmd.cs:292`），所以不能用它把已有牌搬来搬去。
5. 若希望"随机"在所有客户端一致，必须用 `Owner.RunState.Rng.CombatCardGeneration`（同步 RNG），**不要用 `new Random()`**。
6. `CardType.Power` 的牌进手牌后打出来会进入能力栏（`CardModel.GetResultLocationForCardPlay`：`Type == CardType.Power → PileType.None`），与 `SetToFreeThisTurn` 不冲突。

---

### 机制 14 — 全知形态：每次施加"本回合未施加过"的负面效果 → +1 费抽 1~2

**结论：可实现，但是 16 项里风险最高的一项。** 两个独立难点：**(a) 事件源覆盖不全**（同机制 2 的漏斗缺陷）；**(b) "种类"的口径与去重。**

面向对象的选择：

**方案 A（推荐）：钩子 + 历史双查，天然按回合自动重置。**

```csharp
public class OmnisciencePower : CustomPowerModel
{
    private const string DrawKey = "DrawCount";
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar(DrawKey, 1m)];

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext ctx, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0m) return;
        if (applier != Owner) return;
        if (power.GetTypeForAmount(amount) != PowerType.Debuff) return;
        if (power is ITemporaryPower) return;
        if (power.Owner == Owner) return;              // 不要给自己上的 Debuff 计数

        // 是否"本回合已经施加过这个种类"？
        bool already = CombatManager.Instance.History.Entries
            .OfType<PowerReceivedEntry>()
            .Any(e => e.Applier == Owner
                   && e.Amount > 0m
                   && e.Power.Id == power.Id
                   && e.HappenedThisTurn(CombatState)
                   && e != /* 当前这条 */ null);

        if (already) return;

        Flash();
        await PlayerCmd.GainEnergy(1m, Owner.Player);
        await CardPileCmd.Draw(ctx, DynamicVars[DrawKey].BaseValue, Owner.Player);
    }
}
```

`PowerReceivedEntry`（`History/Entries/PowerReceivedEntry.cs`）：`Power`、`Amount`、`Applier`。
历史条目在 `PowerCmd.Apply` 与 `PowerCmd.ModifyAmount` 里被写入（`PowerCmd.cs`：`CombatManager.Instance.History.PowerReceived(combatState, power, modifiedAmount, applier)`）。
`HappenedThisTurn(ICombatState?)`（`CombatHistoryEntry.cs:62`）同时比对 `RoundNumber`、`CurrentSide` 和每名玩家的 `TurnNumber` —— 这正是"本回合"的官方定义。

**方案 B：`SpireField<PlayerCombatState, HashSet<ModelId>>` 显式记账**，在 `AfterSideTurnEnd`/`AfterPlayerTurnStart` 清空。写法见 `reference/docs/baselib-wiki-src/docs/utilities/spirefield.md`：

```csharp
public static class WizardCombatState
{
    public static readonly SpireField<PlayerCombatState, HashSet<ModelId>> DebuffTypesThisTurn =
        new(() => new HashSet<ModelId>());
}
// 用法：WizardCombatState.DebuffTypesThisTurn[Owner.Player.PlayerCombatState].Add(power.Id);
```

**坑（按严重度排序）**

1. **漏斗覆盖**：`SpellCmd.ApplyBurn` 抬高已有燃烧层数时，既不触发 `AfterPowerAmountChanged` 也不写 `PowerReceivedEntry`（见机制 2）。**不修 `SpellCmd` 的话，"燃烧"这个种类永远只在首次施加时算一次**，第二次起完全不计 —— 这会直接违背卡牌描述。
2. **`AfterPowerAmountChanged` 传进来的 `amount` 是"本次变化的量"，不是"最终层数"**。用 `amount <= 0` 判正负即可，不要拿它跟层数比。
3. **读历史的时机**：`CombatManager.History.PowerReceived` 在 `Hook.AfterPowerAmountChanged` **之前**写入（`PowerCmd.Apply` 里先 `power.ApplyInternal` → `History.PowerReceived` → 再 `Hook.AfterPowerAmountChanged`；`ModifyAmount` 里 `SetAmount` → `PowerReceived` → `Hook.AfterPowerAmountChanged`）。所以钩子里查历史会**查到本次这一条本身**，必须排除它。排除方式建议：先查"本回合该 Id 的条目数 > 1"或改用方案 B。
4. **"负面效果种类"的口径**要定死：按 `power.Id`（同名叠加算一种）还是按 `power.GetType()`？按 `Id` 更符合"种类"直觉。
5. **`ITemporaryPower`**：临时力量/敏捷等原版会通过 `TypeForCurrentAmount` 报 Debuff，需要按 `Rend`/`SleightOfFleshPower` 的先例排除。
6. **抽牌上限**：一回合内抽牌可能超过手牌上限 10 而被静默丢弃（正常）。
7. **该卡 design.md 标注 `虚无`（Ethereal）** → `CanonicalKeywords => [CardKeyword.Ethereal]`。注意它是能力牌，能力牌打出后进"能力栏"（`PileType.None`）而**不留在手牌**，所以 Ethereal 永远不会触发 —— 这是设计上的矛盾点，需要确认（可能你想写的是"消耗"或者只是文案遗留）。
8. 不用 `SpireField` 也可以用历史 —— 历史天然随回合滚动，**不需要手动清理**，这是方案 A 相对 B 的最大优势。

---

### 机制 15 — 保留（Retain）

**结论：原生，零实现成本。**

```csharp
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
// 单回合临时保留（例如"本回合保留手牌"）：
//   CardCmd.ApplySingleTurnRetain(card)   // CardCmd.cs:712 → card.GiveSingleTurnRetain()
//   card.ShouldRetainThisTurn             // CardModel.cs:591
```

- 回合结束判定：`CombatManager.FlushPlayerHand`（`CombatManager.cs:1789-1808`）按 `card.ShouldRetainThisTurn` 分流到 `cardsToRetain` / `cardsToFlush`，然后触发 `Hook.AfterFlush(choiceContext, player, flushedCards, retainedCards)`。
- 与 Retain 联动的原版钩子写法：`AbstractModel.AfterFlush(PlayerChoiceContext, Player, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)`（`AbstractModel.cs:758`）。
- **文案不要写"保留"**，引擎自动追加（`CardModel.HoverTips` 会对每个关键字加 tooltip，`CardModel.cs:979-986`）。
- 原版先例：`Purity.cs`（Retain + Exhaust 同时拥有）、`Protect`、`Perseverance`、`SandsOfTime`、`WindmillStrike`（在 `AfterFlush` 里读 `retainedCards` 成长）。

---

### 机制 16 — 固有（Innate）

**结论：原生，零实现成本。**

```csharp
public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];
```

- 实现：`CombatManager.cs:905-920`，第 1 回合把 `Keywords.Contains(CardKeyword.Innate)` 的牌移到抽牌堆顶，并把起手抽牌数抬到 `Math.Max(handDraw, list2.Count)`（上限 10）。
- 原版先例：`Apotheosis.cs`、`Assassinate.cs`、`Backstab.cs`、`BootSequence.cs`、`MindBlast.cs`、`Suppress.cs`。
- **坑**：Innate 只在 `TurnNumber == 1` 生效；且它影响的是**"起手抽牌数"**，即有多张固有牌会一起进手（数量受 10 张上限约束）。若牌不在抽牌堆（例如通过 `AddGeneratedCardToCombat` 放到弃牌堆），Innate 无效。
- 与"保留"组合时注意：`CardKeywordOrder` 规定了卡面关键字顺序。

---

## 5. 观者 mod 先例对照（可抄的最近邻）

观者 92 张卡的完整目录与能力清单（见调研报告），按机制映射：

| 机制 | 观者 mod 有对应的吗 | 最有价值的参考 |
|---|---|---|
| 1 攻击 rider | ✅ | `Watcher.Code.Powers/BlockReturnPower.cs`（`BeforeAttack`/`AfterAttack` + `command.Results` 计数）；`WreathOfFlame` → 原版 `VigorPower`（一次性加成） |
| 2 施加 Debuff 触发 | ⭕ 部分 | `Watcher.Code.Powers/MantraPower.cs` 用 `AfterPowerAmountChanged`；原版 `SleightOfFleshPower` 才是最佳范本 |
| 3 消耗触发 | ❌ **完全没有**（全 mod 0 处 `AfterCardExhausted`） | 只能抄原版 `Midnight.cs` |
| 4 中途获得能量 | ✅ | `Watcher.Code.Cards.Token/Miracle.cs`（**完整源码**：0 费 Skill、`EnergyVar(1)+1`、Retain+Exhaust）；`Cards/Common/FollowUp.cs`（`Owner.PlayerCombatState.GainEnergy(1m)` 同步版） |
| 5 雷电→电流外溢 | ❌ | 无。参考 `CurrentOverflowPower` 自身 + `EnvenomPower` |
| 6 开关式行为改写 | ❌ | 无 |
| 7 按 X 重复 | ✅ | `Ragnarok.cs`/`Tantrum.cs`（`RepeatVar` + `.WithHitCount`）；`BowlingBash.cs`（**用实时状态算 hitCount**）；`Wallop.cs`（从 `Execute()` 返回的 `Results` 反推数值） |
| 8 按 Debuff 数量给资源 | ❌ | `PressurePoints.cs`（读 `GetPowerAmount<MarkPower>()` 后逐敌结算）；原版 `Rend.cs`/`Misery.cs` |
| 9 上回合记忆 | ⭕ 部分 | `Foresight`/`Study` 用 `BeforeHandDraw`；`FollowUp.cs`/`CrushJoints.cs`/`SashWhip.cs` 用 `CombatManager.Instance.History.CardPlaysStarted.LastOrDefault(...)` 读"上一张打出的牌"；`SandsOfTime`/`Perseverance`/`WindmillStrike` 用 `AfterFlush` |
| 10 奖励 | ❌ **全 mod 0 处** | 只能抄原版 `TheHunt.cs` |
| 11 选择/消耗 | ⭕ 部分 | `Commands/ScryCmd.cs`（`FromSimpleGrid` + `CardSelectorPrefs(prompt, 0, n)` + 逐个 `CardCmd.Discard`）；`Meditate.cs`（`FromSimpleGrid` + 移到手牌 + `GiveSingleTurnRetain`）；`Omniscience.cs`（选 1 张后 `AutoPlay` 再 `Exhaust`）。**"选择手牌并直接消耗"没有先例** |
| 12 动态抽牌 + 选择 | ⭕ 部分 | `ScrawlWatcher.cs`（`CardPileCmd.Draw(ctx, 10 - Hand.Cards.Count, Owner)`）；`SpiritShield.cs`（用 `CalculatedBlock` 替代存储计数） |
| 13 生成 + 临时改装 | ✅ | `Commands/WatcherCmd.cs` 的 `GiveCard<T>`/`GiveCards<T>`（`combatState.CreateCard` + `CardPileCmd.AddGeneratedCardToCombat` + `CardCmd.Upgrade`）；`ForeignInfluence.cs`（按稀有度权重随机生成 + `FromChooseACardScreen`）；`Powers/MasterRealityPower.cs`（`AfterCardGeneratedForCombat` 里统一升级）；`ForeignInfluence.cs:70` `SetToFreeThisTurn()`。**注意：观者 mod 全 mod 0 处 `AddKeyword` —— 动态加 Exhaust 没有先例，要抄原版 `SculptingStrike.cs`（加 Ethereal）** |
| 14 每回合首次 Debuff | ❌ | 无。`Brilliance.cs` 展示了"用历史求和"的写法（`History.Entries.OfType<PowerReceivedEntry>()`） |
| 15 Retain | ✅ | `Protect`/`FlyingSleeves`/`Perseverance`/`SandsOfTime`/`WindmillStrike`；`EstablishmentPower.cs` 读 `ShouldRetainThisTurn` |
| 16 Innate | ✅ | `Establishment.cs`（`WithKeyword(Innate, UpgradeType.1)`）、`Alpha.cs`、`BattleHymn.cs` |

另外值得抄的观者基础设施：`Watcher.Code.Commands/WatcherCmd.cs`（生成卡的统一入口）、`Watcher.Code.Compatibility/CardCmdCompatibility.cs`（跨游戏版本 API 兼容层 —— 用 `Expression` 编译委托绕开签名变化，本项目如果只支持单一版本可以不做）、`Watcher.Code.Keywords/WatcherKeywords.cs`（**全 mod 唯一一个 `[CustomEnum]`，正是你要的自定义关键字样板**）。

---

## 6. 最高风险机制 Top 3 + 兜底方案

### 🥇 第 1 名：机制 14（全知形态）+ 机制 2（纵火狂）—— 同一根因

**风险**：`SpellCmd.ApplyBurn` 的"取高值"路径（`existing.SetAmount`）绕过了 `PowerCmd`，导致
- `Hook.AfterPowerAmountChanged` 不触发 → 机制 2/14 在"已有燃烧被顶高"时全部失效；
- `CombatManager.History.PowerReceived` 不写 → 机制 14 基于历史的判定也一起失效。

**判定方式**：先在游戏里造一个"已有 2 层燃烧的敌人"，再打一张施加 3 层燃烧的牌，看纵火狂是否加层。大概率不加。

**兜底/修法**（按推荐度）：
1. **改 `SpellCmd.ApplyBurn`**（`src/Scripts/Spells/SpellCmd.cs`）：把 `existing.SetAmount((int)amount)` 换成
   `await PowerCmd.ModifyAmount(choiceContext, existing, amount - existing.Amount, applier, cardSource)`。
   同时恢复钩子与历史，且"取高值"语义不变。**改动量最小、收益最大。**
2. 在 `SpellCmd` 里自建事件（`public static event Func<...>`），由 `WizardSubscriber` 分发给订阅者。适合不想依赖 `PowerCmd` 链路的场合，但需要自己保证联机确定性。
3. 若 `ModifyAmount` 的副作用（触发 `AfterModifyingPowerAmountGiven/Received`、`ShouldRemoveDueToAmount` 移除检查）不可接受，则退化为方案 2。

**并且机制 14 还有第二个独立风险**：钩子里查历史会查到"本次这一条"，必须排除；`ITemporaryPower`、`TypeForCurrentAmount`、按 `Id` 还是按 `GetType()` 去重都需要明确口径。若历史路线不稳定，改用 `SpireField<PlayerCombatState, HashSet<ModelId>>` 显式记账（`spirefield.md` 明确推荐 `PlayerCombatState` 作为"每玩家每场战斗"的键，因为它天然随战斗重置）。

---

### 🥈 第 2 名：机制 1（燃烧之魂）—— 递归 / 双重加成 / 多段语义

**风险**：
- 额外伤害若带上 `Move` 标志，`AfterDamageGiven` 会递归触发 → 栈溢出或无限伤害。
- 额外伤害若传 `cardSource`，`SpellPowerPower`/`ChillPower` 会再乘一次 → 数值爆炸。
- 多段攻击（火球术 2 段）"每次攻击额外等量"会变成"每段额外等量"，总伤害翻 4 倍而非 3 倍。
- 与 `CurrentOverflowPower` 叠加时，逐段触发会放大溅射。

**兜底**：
- 强制 `ValueProp.Unpowered`（不带 `Move`），并在钩子首行 `if (!props.IsPoweredAttack()) return;` —— 双保险。
- 明确 `cardSource: null`（不吃加成）或 `cardSource`（吃加成），并写进注释。
- 若要"每次攻击指令只加一次"，改挂 `AfterAttack(PlayerChoiceContext, AttackCommand)`，用
  `command.Results.SelectMany(r => r).GroupBy(r => r.Receiver).Sum(g => g.Sum(r => r.TotalDamage))` 汇总。
- 额外伤害加一个"本回合已触发过"的标志也没用（要按目标/按次），最稳妥的是**用 `ValueProp.Unpowered` 从机制上断链**。

---

### 🥉 第 3 名：机制 9（回忆）—— 历史条目到"可回手的牌"的映射

**风险**：
- `CardPlayFinishedEntry.CardPlay.Card` 是**当时那张可变实例**，可能已进消耗堆、已被 `CreateDupe`、甚至已 `IsDupe`。直接 `CardPileCmd.Add(card, PileType.Hand)` 会把"消耗"的语义破坏掉。
- 用 `AddGeneratedCardToCombat` 会抛异常（卡已有 pile，`CardPileCmd.cs:292`）。
- `HappenedLastPlayerTurn` 依赖 `PlayerCombatState.TurnNumber` 与 `RoundNumber`/`CurrentSide` 三方一致，若在错误的钩子里读（例如 `AfterPlayerTurnStart` 时 `TurnNumber` 已 +1），会读到"本回合"而不是"上回合"。
- 遗物与能力牌的钩子时机不同：`HistoryCourse` 用 `AfterAutoPrePlayPhaseEntered`，`Bolas` 用 `BeforeHandDraw`。

**兜底**：
- 明确时机：能力牌建议 `AfterAutoPrePlayPhaseEntered`（对齐 `HistoryCourse`），若希望牌参与本回合抽牌则改 `BeforeHandDraw`。
- 加 `TurnNumber != 1` 守卫。
- 过滤：`!e.CardPlay.IsAutoPlay && !e.CardPlay.Card.IsDupe`，并对消耗堆里的牌选择"跳过"或"用 `CreateClone()` 复制一份"。
- 用 `card.Pile?.Type` 判断来源堆，写进日志先跑通再调手感。
- 若历史路线与预期不符，退化为**显式记账**：在 `AfterCardPlayed` 里把 `cardPlay.Card.Id` push 进 `SpireField<PlayerCombatState, Queue<ModelId>>`（`CopyOnClone` 不需要，`PlayerCombatState` 每场战斗一份），回合边界 `Clear()`。

---

## 7. 一句话结论

16 项机制**全部可实现**：15/16 是引擎原生零成本；4/5/6/7/8/10/13 有原版或现有代码的直接先例；1/3/9/11/12 需要正确选择钩子与 API 但无结构性障碍；**只有 2 与 14 依赖"修补 `SpellCmd.ApplyBurn` 的事件漏斗"这一前置改动**，这是开工前必须做的第一件事。
