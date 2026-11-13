# 新卡「水银 / 永恒回响」开工前调研（2026-11-13）

> 目标：确认 **战斗局内施加附魔**、**两种不同生命周期**、**「即毁」作为引擎渲染词条** 三件事是否可行，
> 以及原版有哪些可直接照抄的参考实现。

## 0. 结论（先看这个）

| 需求 | 可行性 | 依据 |
|---|---|---|
| 战斗局内给卡牌附魔 | ✅ **可行** | `CardCmd.Enchant` 不限制战斗内外；`BlitzMerchant*`（新版商人）就是在局外批量附魔的现成示范，而局内路径同样成立（见 §2） |
| 附魔只作用于本场战斗（水银） | ✅ **精确定义** | 生成牌 `DeckVersion == null`，且战斗牌是**克隆**，每张战斗牌持有**独立的附魔实例** |
| 附魔永久写进卡组（永恒回响） | ✅ **精确定义** | 改 `card.DeckVersion`（卡组里的那张），它会随卡组序列化（`CardModel.ToSerializable` 写 `Enchantment`） |
| 「复奏」= 重放 2 | ✅ **原版同款** | `Spiral`（涡旋）就靠 `EnchantmentModel.EnchantPlayCount` 实现；引擎 `playCount = GetEnchantedReplayCount() + 1` |
| 「即毁」作为引擎渲染词条 | ✅ **BaseLib 原生支持** | `[CustomEnum]` + `[KeywordProperties(AutoKeywordPosition.After)]`，BaseLib 自己就内置了同款词条 `Purge` |
| 「即毁」的**行为** | ⚠️ 需自己实现 | 引擎只内置 Exhaust/Ethereal 等；我们调用 `CardPileCmd.RemoveFromDeck`（战斗中可用，`SwipePower`/`DeprecatedCard` 是原版先例） |

**总评：三件事都能做，且原版/BaseLib 都有可直接照抄的样板。**

---

## 1. 附魔系统：关键结构

`EnchantmentModel`（`Models/EnchantmentModel.cs`）要点：

| 成员 | 作用 |
|---|---|
| `CanEnchant(card)` | **合法性总闸**：`CardType` 为 `Status(4)/Curse(5)/Quest(6)` 直接 false；卡组里 `Unplayable` 的牌 false；已有别的附魔 false。⇒ 设计要求的"排除状态和诅咒"**白送** |
| `OnPlay(ctx, cardPlay)` | 被附魔的牌**打出时**触发（`CardModel` 里 `await enchantment.OnPlay(...)`） |
| `AfterCardPlayed(...)` | 打出后触发（`Swift` 用它把自己置 `Disabled`） |
| `EnchantPlayCount(original)` | **重放**的唯一入口（`Spiral`/`Glam` 实现它） |
| `HasExtraCardText` / `ExtraCardText` | 卡面追加一行附魔说明（键 `enchantments.<id>.extraCardText`） |
| `IconPath` | `images/enchantments/<id>.png`，**缺失会自动回退** `missing_enchantment.png`（不会报错） |
| `Status`（Normal/Disabled） | 每场战斗的"一次"语义；注意：`CardModel.DeepCloneFields` 会克隆附魔，**每张战斗牌一份独立实例** |

### 原版参考实现

| 参考 | 位置 | 可抄什么 |
|---|---|---|
| **迅速 Swift** | `Models/Enchantments/Swift.cs` | `OnPlay` → `CardPileCmd.Draw(Amount)`；`AfterCardPlayed` → `Status = Disabled`（每场仅一次） |
| **涡旋 Spiral** | `Models/Enchantments/Spiral.cs` | `IntVar("Times", 1)` + `EnchantPlayCount(original) => original + Times` + `ExtraHoverTips = Static(ReplayDynamic)`；⚠️ 它把 `CanEnchant` 限制成"仅基础打击/防御"，我们**不抄**这条 |
| **华美手镯 / 涡旋事件** | `BeautifulBracelet` / `SpiralingWhirlpool` | 局外附魔的调用范式（`CardCmd.Enchant` + `SelectAndEnchant` 类流程） |
| **新版黑市商人** | `BlitzMerchant*` | 批量附魔 + `CardCmd.Enchant(...)` 的 UI/历史记录写法 |

---

## 2. 生命周期：为什么"局内附魔"能精确分两种

1. 战斗开始时 `Player.PopulateCombatState`：`foreach (CardModel item in Deck.Cards) { card = state.CloneCard(item); card.DeckVersion = item; ... }`
   ⇒ 战斗牌是卡组牌的**克隆**，`DeckVersion` 指回卡组里的原牌。
2. `CombatState.CloneCard` → `CardModel.ClonePreservingMutability()` → `CardModel.DeepCloneFields()` 里有：
   ```csharp
   if (Enchantment != null) { var e = (EnchantmentModel)Enchantment.ClonePreservingMutability();
                              Enchantment = null; EnchantInternal(e, e.Amount); }
   ```
   ⇒ **每张战斗牌一份独立附魔实例**，改战斗牌 ≠ 改卡组牌。
3. `CardModel.ToSerializable()` 里 `Enchantment = Enchantment?.ToSerializable()` ⇒ **卡组牌**的附魔会进存档。
4. 生成牌（`CardPileCmd.AddGeneratedCardToCombat`）不进卡组 ⇒ `DeckVersion == null`，天生"本场战斗"。

**因此：**

| 卡 | 操作 | 生命周期 |
|---|---|---|
| 水银 | 只对**生成的战斗牌** `CardCmd.Enchant(Swift, 2)` | 严格本场战斗（连卡组都没有它） |
| 永恒回响（目标是卡组牌） | 手牌那张 + `target.DeckVersion` 各附魔一次 | 永久（每场战斗都生效） |
| 永恒回响（目标是临时牌） | 只有手牌那张（`DeckVersion == null`） | 仅本场战斗 |

⚠️ 两个附魔要**分别**施加（两份独立实例）：只改手牌 → 本场有效、下场没有；只改 `DeckVersion` → 下场才有、本场打出的仍是没附魔的克隆。

---

## 3. 重放（复奏 = 重放 2）

- `CardModel`：`public int GetEnchantedReplayCount() => Enchantment?.EnchantPlayCount(BaseReplayCount + ExtraReplayThisTurn) ?? (...)`。
- 引擎内部 `int playCount = GetEnchantedReplayCount() + 1;`（`CardPlay` 注释：Replay 2 ⇒ playCount=3）。
- ⇒ 我们的 `Reprise` 只需 `override int EnchantPlayCount(int original) => original + Times`，`Times = 2`。
- 官方文案口径：`SPIRAL.description = "这张牌获得[gold]重放[/gold][blue]1[/blue]。"` ⇒ 复奏写"重放 2"。

## 4. 「即毁」：BaseLib 支持自定义关键词

BaseLib 的注册机制（`GenEnumValues` + `CustomKeywords` + `AutoKeywordText`）：

```csharp
public static class WizardKeywords
{
    [CustomEnum(null)]
    [KeywordProperties(AutoKeywordPosition.After)]   // 词条显示在描述之后，同「消耗」
    public static CardKeyword Obliterate;
}
```

- BaseLib 在 `ModelDb.Init` 前：分配枚举位 → 记录 `CustomKeywords.KeywordIDs[value] = { Key = "WIZARD-OBLITERATE" }`
  → 把词条加进卡面词条行（`AutoKeywordText.AdditionalAfterKeywords`）。
- `CardKeywordExtensions.GetLocKeyPrefix` 被 BaseLib patch 成上面的 Key ⇒ 本地化键 = **`card_keywords` 表里的
  `WIZARD-OBLITERATE.title` / `.description`**（不是 `OBLITERATE.*`）。
- 官方词条文案格式（照抄）：`EXHAUST.title = "消耗"` / `EXHAUST.description = "在战斗结束前移除。"`
- BaseLib 自带同款词条 **`Purge`**（`BaseLibKeywords`，`AutoKeywordPosition.After`）——活的样板。
  `Purge` 的行为是"打出后把结果牌堆改成 `PileType.None`"（`PurgePatch`），**不等于**我们要的"从卡组永久移除"。

**「即毁」的行为**（我们自己实现）：
```csharp
if (playedCard.DeckVersion != null)
    await CardPileCmd.RemoveFromDeck(playedCard.DeckVersion, showPreview: false);
```
- 战斗中可用：原版 `SwipePower`（偷牌）与 `DeprecatedCard` 就是战斗中调 `RemoveFromDeck`。
- `RemoveFromDeck` 要求 `card.Pile.Type == Deck`（卡组牌满足）；会写地图历史 `CardsRemoved` 并触发 `Hook.BeforeCardRemoved`。

---

## 5. 两张卡的具体实现设计

### 5.1 水银 `Mercury`（1 费 · 罕见 · 能力）

- 卡：`[Pool(WizardCardPool)]`，`PowerVar<MercuryPower>(1m)`；升级：每回合张数 1 → 2。
- 能力 `MercuryPower`：
  - `PowerStackType.Counter`，`Amount` = 每回合可附魔的**张数**（多张水银相加；升级牌给 2）。
  - `BeforeSideTurnStart(side == Player)`：把"本回合已用张数"清零。
  - `AfterCardGeneratedForCombat(card, creator)`：只认自己生成的牌；跳过**不可附魔**的牌
    （`CanEnchant == false`，例如状态牌）；对前 `Amount` 张可附魔的牌执行
    `CardCmd.Enchant(ModelDb.Enchantment<Swift>().ToMutable(), card, 2m)`。
  - 时机安全：该钩子触发时卡**已经在牌堆里**（`AddGeneratedCardsToCombat` 的顺序是
    `History.CardGenerated` → `Add(card, pile)` → `Hook.AfterCardGeneratedForCombat`）。
- 官方附魔名是 **「迅速」**（`SWIFT.title = 迅速`，`description = 你第一次打出这张牌时，抽{Amount}张牌。`）。
  设计稿卡面写的"迅捷"是笔误，**卡面用「迅速 2」**。

### 5.2 永恒回响 `EternalEcho`（1 费 · 稀有 · 能力）

- 关键词：`Retain` + 我们新增的 `Obliterate`（即毁）。
- 结算：
  1. 过滤合法目标：`hand.Where(c => c != this && reprise.CanEnchant(c))`
     （`CanEnchant` 已排除状态/诅咒/任务牌、卡组中不可打出、已有其它附魔）。
  2. 无合法目标 ⇒ **正常打出、不触发即毁**（结果牌堆对 Power 牌天然是 `PileType.None`）。
  3. 有合法目标 ⇒ 强制选 1 张（`CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1, 1)`，
     `Cancelable` 默认 false）。
  4. `CardCmd.Enchant(reprise.ToMutable(), handCard, 2m)`；
     若 `handCard.DeckVersion != null` 再给 `DeckVersion` 附魔一份。
  5. 结算成功 ⇒ `CardPileCmd.RemoveFromDeck(DeckVersion, showPreview: false)`（即毁）。
- 升级：设计稿没有升级数值 ⇒ 暂定**无升级效果**（同「失忆」）。

## 6. 需要新增的素材 / 本地化

- 素材：`images/enchantments/reprise.png`（**64×64**，与官方附魔图标同尺寸；程序绘制，不缩卡图）；
  `images/powers/mercury_power.png` + `_big`（程序绘制）。
- 本地化（只写 **zhs / eng**，繁中/日文交本地化团队）：
  - `cards.json`：`WIZARD-MERCURY` / `WIZARD-ETERNAL_ECHO`（title + description）
  - `powers.json`：`WIZARD-MERCURY_POWER`
  - **新增 `enchantments.json`**：`WIZARD-REPRISE.title/.description/.extraCardText`
  - **新增 `card_keywords.json`**：`WIZARD-OBLITERATE.title/.description`

## 7. 待确认的默认决策（已按最合理方式取默认值）

| # | 决策点 | 默认取值 |
|---|---|---|
| 1 | 多张水银的叠层 | `Amount` **相加**（两张未升级 = 每回合前 2 张；一张升级 = 前 2 张） |
| 2 | "生成的第一张牌"不可附魔时 | **不占名额**，顺延到第一张可附魔的生成牌 |
| 3 | 永恒回响升级 | 无升级效果（设计稿未给数值） |
| 4 | 复奏是否允许附魔到**能力牌** | 允许（合法对象只排除状态/诅咒/任务，按设计口径）；重放能力牌会重复触发其效果 |
| 5 | 已有其它附魔的牌 | 不在合法目标内（引擎限制：`CardCmd.Enchant` 遇异种附魔会抛错） |
