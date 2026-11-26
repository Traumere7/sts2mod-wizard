# 巫师 Mod — 工程交接文档

> **2026-11-26 整理。** 写给接手这个项目的开发者 / AI agent。
>
> 本文档只讲三件最重要的事：
> 1. **怎么写一张卡**（工程实践、代码骨架、踩坑点）
> 2. **怎么调 bug**（方法论 + 排障速查表 + 经典案例）
> 3. **已经定死的规范**（哪些必须做、哪些绝对不能做）
>
> 环境搭建、发布流程、美术替换等属于"按需查阅"，都放在文末索引里。
>
> **事实来源优先级**：
> `docs/design.md`（卡面文案 / 效果，**唯一事实来源**）
> → `docs/CONTEXT.md`（工程细节与逐日踩坑日志，含全部"为什么"）
> → 本文档（提炼与索引）
> → `README.md`（对外介绍）。

---

## 目录

- [0. 30 秒速览](#0-30-秒速览)
- [1. 环境与构建（最小必要）](#1-环境与构建最小必要)
- [2. 写一张卡：标准流程](#2-写一张卡标准流程)
- [3. 已定死的规范](#3-已定死的规范)
- [4. Bug 调试手册](#4-bug-调试手册)
- [5. 工程结构与关键文件](#5-工程结构与关键文件)
- [6. 交接清单 / 下次游戏更新后](#6-交接清单--下次游戏更新后)
- [7. 命令速查](#7-命令速查)

---

## 0. 30 秒速览

| 项 | 值 |
|---|---|
| 项目 | 《杀戮尖塔 2》**角色 mod「巫师 / Wizard」**，基于 [BaseLib](https://github.com/Alchyr/BaseLib-StS2/) |
| 仓库 | `D:\Projects\STS2\Wizard`（<https://github.com/Traumere7/sts2mod-wizard>） |
| 当前版本 | `src/Wizard.json` → `0.6.0`；BaseLib `min_version 3.4.8`；`min_game_version 0.107.1` |
| 目标游戏 | 公共测试版，buildid **26625880**（2026-11-13），Godot 4.5.1 |
| 内容量 | **80 张卡** / 36 个能力 / 10 个遗物 / 3 瓶药水 / 2 个附魔 / 4 个事件 / 1 个怪物 / 9 位先古之民对话 |
| 语言 | `zhs` + `eng`（我维护）；`zht` + `jpn`（本地化团队维护） |
| 构建 | `.\build.ps1`：编译 → Godot import → GDRE 打包 `.pck` → 部署到游戏 `mods\Wizard` → 可选 B 版美术 |
| 部署目标 | `D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Wizard`（**优先级高于工坊**，日常迭代只认这里） |
| 日志 | `%APPDATA%\SlayTheSpire2\logs\godot.log`（历史轮转：`godot2026-11-22T23.17.32.log` 这种） |
| 游戏存档 | `%APPDATA%\SlayTheSpire2\steam\<SteamID64>\`，**modded / 未改装是两套独立目录** |
| 未提交内容 | `reference/`（反编译树、工具）、`build/`、`art_alt/` 都在 `.gitignore` 或未纳入版本库 |

**协作方式**：用户在游戏里实测并反馈；agent 负责实现、构建、部署、自检、写文档。
**规矩：启动游戏前必须先告知用户**（人机协作，别偷偷启动抢存档/抢窗口）。

---

## 1. 环境与构建（最小必要）

### 1.1 构建链路

```powershell
# 日常迭代（推荐）：编译 + 打包 + 部署，不提交、不处理 B 版美术
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -SkipAltArt -NoCommit

# 完整发布产物（含 B 版美术覆盖包）
.\build.ps1
```

| 参数 | 作用 |
|---|---|
| `-NoCommit` | 不自动 `git add/commit`（**日常必须带**，否则会把别人的改动一起提交） |
| `-SkipPck` | 只编译 dll，不重打 pck（只改 C# 时可用；但资源改动必须重打） |
| `-SkipAltArt` | 跳过 B 版美术覆盖包（`WizardAlt.pck`） |

### 1.2 构建链路的三个隐蔽根因（改代码前先读）

1. **`dotnet build` 的输出不一定在游戏加载的目录** —— 部署目标固定为游戏目录 `mods\Wizard`，
   工坊目录是只读缓存（会被 Steam 覆盖回已发布版本），上传器 workspace 只是"素材源"。
2. **pck 必须用 GDRE 以 `--pck-version=2` 重打**，否则游戏读不到新资源。
3. **`build.ps1` 必须保持 Windows PowerShell 5.1 兼容**：UTF-8 **含 BOM**、CRLF，
   不能依赖 PS7 语法（`??`、`-Parallel` 等）。

### 1.3 两道验证（每次改完都做）

```powershell
# ① 哈希核对（部署目录 vs 构建产物）
Get-FileHash 'D:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\Wizard\Wizard.dll' -Algorithm MD5
# ② pck 字节核对（确认文案/资源真的进了包）
python -c "from pathlib import Path; b=Path('build/Wizard.pck').read_bytes(); print('文案在包里:', '要做中文原文'.encode() in b)"
```

启动自检（**先告知用户**）：

```powershell
Start-Process 'steam://rungameid/2868840'; Start-Sleep 100
Get-Content "$env:APPDATA\SlayTheSpire2\logs\godot.log" | Select-String '补丁已应用|Missing localization|Exception|must be marked'
# 期望：补丁汇总 15/15（或当前数量）、无缺失文案、无异常
```

---

## 2. 写一张卡：标准流程

### 2.1 落盘清单（少一样就显示不出来）

| 内容 | 位置 / 约定 |
|---|---|
| 卡牌类 | `src/Scripts/Cards/<ClassName>.cs`，继承 `WizardCardModel`（法术牌继承 `WizardSpellCard`） |
| 卡图 | `src/Wizard/images/cards/<ClassName>.png`（基类自动拼路径；普通 250×190，先古 250×351，尺寸宽松） |
| 卡牌文案 | `src/Wizard/localization/{zhs,eng}/cards.json`，键 = `WIZARD-<CLASS_SNAKE_UPPER>.description`（`title` 同理） |
| 能力（如有） | `src/Scripts/Powers/<ClassName>Power.cs` + `powers.json` 的 `WIZARD-<...>_POWER.description` / `.smartDescription` |
| 能力图标 | `images/powers/<snake>_power.png`(64×64) 与 `_big.png`(128×128) —— **两个都要**，是两条独立路径 |
| 关键词（如有） | 自定义词条走 BaseLib `[CustomEnum]`，见 §2.7 |
| 悬停 | `ExtraHoverTips`：文案里提到的机制都要挂，**同名 buff 除外**（§3.2） |

> 类上**必须**有 `[Pool(typeof(WizardCardPool))]`；漏了会在启动时直接崩（§3.6）。

### 2.2 最小骨架（照抄 `CalmMind.cs` 的结构）

```csharp
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Wizard.Scripts.Pools;
using Wizard.Scripts.Powers;

namespace Wizard.Scripts.Cards;

/// <summary>
/// 镇静 - 2 费 - 技能 - 初始卡组
/// 获得 10(13) 点格挡，获得 1 层法术增益。
/// </summary>
[Pool(typeof(WizardCardPool))]
public class CalmMind : WizardCardModel
{
    private const int Cost = 2;

    private const string SpellPowerKey = nameof(SpellPowerPower);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(10m, ValueProp.Move),
        new PowerVar<SpellPowerPower>(1m),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SpellPowerPower>(),
    ];

    public CalmMind()
        : base(Cost, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        await PowerCmd.Apply<SpellPowerPower>(
            choiceContext, Owner.Creature, DynamicVars[SpellPowerKey].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
```

**三条硬性要求**：

1. `CanonicalVars` 里所有"会变动的数值"都要声明 —— 卡面占位符、升级、悬停全靠它。
2. 实际效果读 `DynamicVars.X.BaseValue` / `DynamicVars[key].BaseValue`（**不要**写死常量）。
3. 升级只写在 `OnUpgrade()`；卡面文案不写死数字（§2.6）。

### 2.3 常用 `DynamicVar` 对照表

| 需求 | 写法 | 卡面占位符 |
|---|---|---|
| 格挡（吃敏捷/脆弱） | `new BlockVar(10m, ValueProp.Move)` | `{Block:diff()}` |
| 格挡（**能力给的**，不吃敏捷/脆弱） | `new BlockVar(n, ValueProp.Unpowered)` | `{Block:diff()}` |
| 伤害（吃力量） | `new DamageVar(6m, ValueProp.Move)` | `{Damage:diff()}` |
| 法术伤害 | `new DamageVar(4m, ValueProp.Move \| SpellProps.For(School))` | `{Damage:diff()}` |
| 能力层数 | `new PowerVar<T>(1m)` | `{<T 类名去掉 Power>:diff()}` |
| 抽牌数 | `new CardsVar(2)` | `{Cards:diff()}` |
| 能量 | `new EnergyVar(3)` | `{Energy:energyIcons()}` |
| 任意文本 | `new StringVar("Amount", "x")` | `{Amount}` |

- **升级**：`DynamicVars.X.UpgradeValueBy(2m)`、`DynamicVars.Cards.UpgradeValueBy(1)`、`DynamicVars["Key"].UpgradeValueBy(...)`。
- **动态键**：需要按名字取时用 `private const string XxxKey = nameof(XxxPower);` + `DynamicVars[XxxKey]`。
- **自定义伤害计算**（如"每有 1 层 X 造成 Y"）用 `CalculatedDamageVar` + `ExtraDamageVar`，
  ⚠️ 不是 `CalculationExtraVar`（CONTEXT §6.29，写错不报错但数值不动）。

### 2.4 `OnPlay` 常用命令（都是 async）

| 效果 | API |
|---|---|
| 打演出 | `await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);` |
| 获得格挡 | `await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);` |
| 伤害 | `await DamageCmd.Attack(v).WithValueProp(...).FromCard(this, cardPlay).Targeting(t).WithHitCount(n).Execute(choiceContext);` |
| 上能力 | `await PowerCmd.Apply<T>(choiceContext, target, amount, source, this);` |
| 抽牌 | `await CardPileCmd.Draw(choiceContext, n, Owner);` |
| 获得能量 | `await PlayerCmd.GainEnergy(n, Owner);` |
| 给关键词 | `CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);` |
| 附魔 | `CardCmd.Enchant<T>(card, amount);`（异种附魔会抛异常，见 §3.5） |
| 加牌进战斗 | `CardPileCmd.AddGeneratedCardsToCombat([card], PileType.Discard, Owner);`（见 §3.4） |
| 选牌 | `await CardSelectCmd.FromHand(choiceContext, Owner, new CardSelectorPrefs(prompt, min, max), filter, this)` |
| 结束回合 | `await PlayerCmd.EndTurn(choiceContext, Owner, canBackOut: false);` |

### 2.5 法术牌（火焰 / 雷电 / 寒冰 / 以太）

继承 `WizardSpellCard`，并实现：

```csharp
public override SpellSchool School => SpellSchool.Fire;   // 自动获得 ISpellCard 标记

// 卡面数值必须带法术 props，否则不吃「法术增益 / 寒冷 / 超导」
new DamageVar(4m, ValueProp.Move | SpellProps.For(School)),
await DamageCmd.Attack(v).WithValueProp(ValueProp.Move | SpellProps.For(School)).FromCard(this, cardPlay)...
```

- 「以太」不是第四系，而是**三系并集**（CONTEXT §28.1）——`SpellSchool` 里要按"问某一系"的接口处理。
- 卡面「雷电伤害 / 寒冰伤害」用 `[blue]`；其他伤害不许上 `[blue]`（§3.1）。

### 2.6 升级的两种形态（选错就是 bug）

| 升级改变的是 | 正确写法 | 卡面 |
|---|---|---|
| **数值**（伤害/格挡/层数/抽牌） | `DynamicVars.X.UpgradeValueBy(n)` | 写 `{X:diff()}`，自动显示 `1(2)` |
| **词条**（保留/固有/消耗） | `AddKeyword(...)` / `RemoveKeyword(...)` | 词条不写进文案，由引擎渲染 |
| 费用 | `EnergyCost.UpgradeBy(-1)` | 引擎自动显示 `1(0)` |

⚠️ 卡面文案**不要**为了升级写 `{IfUpgraded:show:...}` 来改数字 —— 那是给"只在升级后出现的整句特效"用的（§2.6 例外：整句机制差异才用）。

### 2.7 自定义关键词（BaseLib）

```csharp
[CustomEnum(null)]
[KeywordProperties(AutoKeywordPosition.After)]
public static CardKeyword Obliterate;

// 本地化：card_keywords.json
// "WIZARD-OBLITERATE.title": "即毁"
// "WIZARD-OBLITERATE.description": "打出后从你的牌组中永久移除。"
```

- 卡面要用 `[gold]即毁[/gold]` 并在 `ExtraHoverTips` 里 `HoverTipFactory.FromKeyword(WizardKeywords.Obliterate)`。
- 自定义"非词条"机制（如「嬗变」「跳过」）用**自定义 HoverTip**：
  `new HoverTip(new LocString("static_hover_tips", "WIZARD-TRANSMUTATION.title"), new LocString(..., ".description"))`。

### 2.8 特殊稀有度 / 牌池

| 类型 | Pool | Rarity | 注意 |
|---|---|---|---|
| 普通卡 | `WizardCardPool` | Basic/Common/Uncommon/Rare | 进角色卡池 |
| 衍生牌（Token） | `TokenCardPool` | `CardRarity.Token` | `[Pool(typeof(TokenCardPool))]` |
| 状态牌 | `StatusCardPool` | `CardRarity.Status` | `Unplayable` 之类 |
| 诅咒牌 | `CurseCardPool` | `CardRarity.Curse` | `Eternal` = 不能移除 |
| 先古牌 | `WizardCardPool`（原版先古也在角色池） | `CardRarity.Ancient` | 嬗变时→无色稀有；「超越」走 `ITranscendenceCard` |

- **局外收益牌**（修行 / 治疗术 / 永恒回响这类"战斗中不该被生成"的）必须
  `public override bool CanBeGeneratedInCombat => false;`（§3.4）。

### 2.9 文案规范（zhs / eng）

- 键：`WIZARD-<CLASS_SNAKE_UPPER>.title` / `.description`。
  规则是 BaseLib 的「模型前缀 + Entry 全大写」（类名 → 蛇形大写，如 `CalmMind` → `CALM_MIND`），
  前缀来自 `[Pool]`/模型注册，最终形如 `WIZARD-CALM_MIND`；自定义关键词同理（`WIZARD-OBLITERATE`，见 `WizardKeywords.cs` 注释）。
- **有升级值写成 `8（10）` 这种自动括号**：用占位符 `{Damage:diff()}`，不要手写"（10）"。
- 换行用 `\n`；关键词/专有名词 `[gold]`；雷电/寒冰伤害 `[blue]`；负面状态 `[purple]`（少量）。
- 卡名里的数字/费用：费用由引擎显示，文案里**不写**费用。
- **只改 `zhs` 和 `eng`**；`zht` / `jpn` 是本地化团队的范围（§3.7）。

### 2.10 新卡 checklist（提交前逐条过）

- [ ] 类上有 `[Pool(...)]`，继承 `WizardCardModel` / `WizardSpellCard`
- [ ] `CanonicalVars` 声明了所有会变的数值；`OnPlay` 读 `BaseValue`
- [ ] `OnUpgrade()` 覆盖了设计稿里所有升级变化（含"升级给词条"）
- [ ] `ExtraHoverTips` 覆盖文案里提到的**所有**关键词/机制（同名 buff 除外）
- [ ] 卡图存在（`images/cards/<类名>.png`）
- [ ] `zhs` + `eng` 两张文案齐全（含 `.title`），无缺失键
- [ ] 需要时实现 `CanBeGeneratedInCombat` / `IsTransformable` 语义
- [ ] 生成类牌走 `AddGeneratedCardsToCombat`；永久移除走 `DeckVersion`
- [ ] `dotnet build` 无 error；`build.ps1` 部署成功；启动日志无异常
- [ ] 更新 `docs/CONTEXT.md`（新 §）+ 需要时 `design.md` 由用户维护

---

## 3. 已定死的规范

> 以下每条都有踩坑来源，详见 `CONTEXT.md` 括号里的章节。**改之前先读那一节。**

### 3.1 内容与文案

1. **`docs/design.md` 是唯一事实来源**：卡面文案、数值、效果、稀有度、费用以它为准。
   用户明确要求「卡牌效果和文案请不要随意修改！」—— 发现不一致时**先问**，不要"顺手修正"。
2. 升级值用 `1（2）` 形式（design.md 里就是这么写的）。
3. `[blue]` **只用于「雷电伤害 / 寒冰伤害」**；其他颜色（红/紫/金）各有语义，别乱加（§10.5）。
4. 中文标点用全角 `，。：；`；英文用半角；中英之间留空格。
5. 卡名与本地化键必须和类名对应，改类名要同步改键与卡图。

### 3.2 悬停（HoverTip）

1. **卡面文案里提到的每个关键词/机制都要能悬停解释** —— 包括「消耗」（2026-11-13 beta 起引擎不再自动给）。
2. **例外**：与卡牌**同名**的 buff 不挂（避免"伟大法袍"悬停自己）。
3. 卡面提到「格挡」但卡本身不获得格挡时，手动挂 `HoverTipFactory.Static(StaticHoverTip.Block)`。
4. 能量图标悬停用 `base.EnergyHoverTip`；能力用 `HoverTipFactory.FromPower<T>()`；关键词 `FromKeyword`。
5. 自定义机制（嬗变 / 跳过 / 即毁）用自定义 HoverTip + 自己的本地化表键。

### 3.3 数值实现

1. **能力给的格挡必须 `ValueProp.Unpowered`** —— 只有 `Move && !Unpowered` 的格挡才会被敏捷/脆弱/快速/紧绷修正（`ValuePropExtensions.IsPoweredCardOrMonsterMoveBlock()`）。
2. 伤害默认带 `ValueProp.Move`；"不该吃力量"的伤害用 `Unpowered`。
3. X 费卡：`HasEnergyCostX` + `ResolveEnergyXValue()`（§6.25）。
4. 「攻击/格挡加成」类能力改数值时，用 `AfterPowerAmountChanged` / `ModifyDamage` 等**现成钩子**，不要手改 `Amount`。
5. 玩家自己身上的原版减益会"多留一回合"，这是引擎行为（§6.30），别去修。

### 3.4 生成 / 牌堆 / 移除

1. **"生成一张牌"必须走 `CardPileCmd.AddGeneratedCardsToCombat`**：它会写 `CombatHistory.CardGenerated` 并发 `AfterCardGeneratedForCombat`，「催化」「孔雀之尾」「结晶」都靠这个。
   - 例外：**嬗变**用 `CardCmd.Transform`（引擎历史不同），所以 `Transmutation` 手动补了这两件事（`Utils/Transmutation.cs`）。
2. 「抽到即复制」类效果（如「梦魇」诅咒）**必须防连锁**：生成物直接进弃牌堆，或用 `_pendingCopies` 之类标记，否则抽 5 张会复制出 5 张（CONTEXT §40.8）。
3. **永久移除**：`CardPileCmd.RemoveFromDeck(card.DeckVersion)`，且要求 `card.Pile.Type == PileType.Deck`；战斗中打出的牌要拿 `DeckVersion`（§6.43 / `Utils/CardObliteration.cs`）。
4. 战斗内加牌牢记"牌是克隆"：`DeckVersion` 才是本体；临时费用/临时词条不写回本体。

### 3.5 附魔

1. 自定义附魔：`Scripts/Enchantments/<Name>.cs : CustomEnchantmentModel`，图标 `images/enchantments/<snake>.png`（64×64 透明底），文案在 `enchantments.json`。
2. 战斗中的牌是**深克隆**，附魔实例独立；要写回必须 `DeckVersion`。
3. `OnEnchant()` 在读档（`FromSerializable`）时**会重跑**，所以词条移除/修改必须可重复执行（幂等）。
4. 异种附魔覆盖会抛异常；同种附魔才叠层。
5. 结算顺序与"重放/复奏"类效果用 `EnchantPlayCount` 扩展（见 `Reprise.cs`）。

### 3.6 模型注册（不做就崩）

1. **每个自定义模型（卡/遗物/药水/能力/附魔/事件/怪物）都必须带 `[Pool(...)]`** —— 漏了启动直接崩：
   `Model ... must be marked with a PoolAttribute to determine which pool to add it to.`
2. 能力图标必须**成对**：`CustomPackedIconPath`（64×64）+ `CustomBigIconPath`（128×128）。
3. 遗物图标要进**图集 `.tres`**（`relic_atlas.sprites/<name>.tres` 与 `_outline.tres`，85×85），不能只放 PNG。
4. 事件 / 怪物 / 先古对话各有固定文件与接口，见 `docs/EVENT-INTERFACE.md`。

### 3.7 协作边界（**别越界**）

| 范围 | 负责人 | 说明 |
|---|---|---|
| `docs/design.md` | **用户** | 卡面文案/数值的唯一来源，改动必须用户确认 |
| `src/Wizard/localization/zhs` + `eng` | **我（agent）** | 新增/修改文案 |
| `src/Wizard/localization/zht` + `jpn` | **本地化团队** | 不要碰；只在他们要求时提供英文/中文原文 |
| `docs/local/**` | **本地化团队** | 术语表、风格指南、进度 |
| 美术（`art_alt/`、卡图、立绘） | 用户 / 美术 | 我只做占位与接线 |

### 3.8 Git / 发布纪律

1. **`git add` 只加自己改的文件**；仓库里常有用户未提交的美术/动画工作，`git add -A` 会把它们卷进我的提交（已发生过一次，见 CONTEXT §47）。
2. 仓库级 `core.autocrlf=false` 是**刻意的**，别改。
3. 推送：SSH 偶尔被网络阻断（`Connection closed by ... port 22`），改用 HTTPS 推送：
   `git push https://github.com/Traumere7/sts2mod-wizard.git HEAD:main`。
4. 发布流程：改 `src/Wizard.json` 版本 → `build.ps1` → 复制 dll/pck/json 到上传器 workspace `content\` →
   `ModUploader.exe upload -w .\WizardWorkspace`（在 `D:\Downloads\ModUploader-win-x64` 下跑）→
   Steam Web API 核对 file_size/更新时间/简介 → zip（**只含 dll/json/pck**）→ `gh release create`。
5. 工坊简介上限 **8000 字节**（不是字符）；超了从「新增卡牌」最旧条目删。
6. 发布前 `docs/releases/v<ver>.md` 必须写好；`changeNote` 同步更新。

---

## 4. Bug 调试手册

### 4.1 标准调试循环

```
复现（用户描述 / 日志） → 抓日志 → 反编译查证引擎行为 → 最小修复 → 构建部署 → 启动自检 → 写 CONTEXT
```

**不要**跳过"反编译查证"直接猜 —— 这个项目 80% 的坑都在引擎/BaseLib 的时序与语义里。

### 4.2 日志怎么读

```powershell
$log = "$env:APPDATA\SlayTheSpire2\logs\godot.log"
# 启动异常 / 补丁数 / 文案缺失
Select-String -Path $log -Pattern '补丁已应用|Exception|Missing localization|must be marked'
# 只看自己 mod 的行
Select-String -Path $log -Pattern '\[Wizard\]|\[BaseLib\]' | Select-Object -Last 30
```

常见日志信号：

| 日志 | 含义 |
|---|---|
| `[Wizard] 补丁已应用：XxxPatch -> N 个方法` | Harmony 补丁生效；**补丁数突然变少 = 目标方法被游戏更新改了** |
| `Model ... must be marked with a PoolAttribute` | 漏 `[Pool]`，启动崩溃（§3.6） |
| `Missing localization key` | 文案键缺失（只在用到时警告，不崩） |
| `Unknown character ID / Unknown card ID` | 存档里有未安装 mod 的 id（只警告，可忽略） |
| `Deleting ... because it does not exist on remote` | **Steam 云同步在删本地存档**，立刻关游戏 + 关云同步（这不是 bug，是引擎行为） |

### 4.3 反编译查证（最常用的手段）

- 反编译树：`reference/decompiled/game/`（2026-11-13 快照，3,888 个 .cs）与 `reference/decompiled/BaseLib/`。
- 旧快照：`reference/decompiled/game-2026-09-19/`、`BaseLib-2026-09-19/`（用于对比游戏更新前后的签名变化）。
- 查具体 API：直接 `Select-String -Path reference\decompiled\game\**\*.cs -Pattern 'class CardCmd'`，
  或按类型名 `rglob("<Type>.cs")`。
- 验证"修复真的编译进去了"：对部署后的 dll 用 `ilspycmd`/`ildasm` 看方法体（CONTEXT §2.2c）。

### 4.4 运行时诊断注入（临时 patch，用完删）

怀疑"某个值/钩子没按预期走"时，加一个临时 Harmony postfix 打印：

```csharp
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
internal static class TempHoverDumpPatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        Entry.Logger.Info("=== TEMP DUMP ===");
        foreach (var card in ModelDb.AllCards)
            Entry.Logger.Info($"{card.Id.Entry} | tips={string.Join(",", card.HoverTips.Select(t => t.Title))}");
    }
}
```
跑一次游戏、抓日志、**删掉补丁**。这类诊断补丁绝不能留在发布包里。

### 4.5 离线验证技巧（不用启动游戏）

1. **pck 字节检索**：确认文案/资源进了包（§1.3）。
2. **离屏渲染成 PNG**：`Godot --headless` 加载场景截图，用于验证 UI/立绘（CONTEXT §22.3）。
3. **资源可加载性**：用 Godot 离屏 `ResourceLoader.load` 检查 `.tres`/贴图路径（曾用它发现美术包 id 被改写）。
4. **JSON 校验**：本地化文件、`Wizard.json` 改动后先 `json.load` 一遍。

### 4.6 症状 → 根因 → 修法 速查表

| 症状 | 常见根因 | 修法 |
|---|---|---|
| 游戏启动崩，日志 `must be marked with a PoolAttribute` | 新模型漏 `[Pool]` | 加 `[Pool(typeof(...))]` |
| 卡图/能力图标不显示 | 路径拼错 / 图标不成对 | 对齐 `CustomPortraitPath`、`CustomPackedIconPath` + `CustomBigIconPath` |
| 卡面数字不随升级变 | 用了常量而非 `BaseValue`，或漏 `UpgradeValueBy` | 改 `DynamicVars` |
| 卡面显示 `{X:diff()}` 原文 | 键名和 `CanonicalVars` 里的名字不一致 | 对齐 var 名 |
| 升级后没有「固有/保留」 | 升级只写数值、忘了 `AddKeyword` | `OnUpgrade` 补词条 |
| 能力层数不叠加 / 显示错 | `StackType` 与 `Amount` 脱节 | 见 CONTEXT §6.24 |
| 打出的能力牌不进弃牌堆 | 能力牌默认 `PileType.None` | `ModifyCardPlayResultLocation` 映射到 `Discard`（`Immortal.cs` 先例） |
| 生成的牌不触发「催化/孔雀之尾」 | 没用 `AddGeneratedCardsToCombat`（或嬗变没补钩子） | §3.4 |
| 抽到即复制 → 无限连锁 | 生成物进了抽牌堆/手牌 | 改弃牌堆 + 防重入标记 |
| 格挡被敏捷/脆弱加成 | 用了 `ValueProp.Move` 而非 `Unpowered` | §3.3 |
| 一次性伤害变成"法术伤害"多扣血 | 附魔自伤带了 `SpellProps` | 非攻击伤害不要带法术 props（§27） |
| 悬停缺关键词 | 文案提到但没挂 `ExtraHoverTips` | §3.2 |
| 战斗外 UI（商店/火堆）崩溃 | 把非 `NCreature` 当战斗对象处理 | CONTEXT §6.38–6.40 |
| 多人模式效果串到队友 | 漏了 `ShouldSkip` / 没按 `Owner` 过滤 | 参考 CONTEXT §15.1 |
| mod 更新后编译失败 | 游戏 beta 删/改了 API | 刷反编译树，按新签名改（§6） |
| 改了代码但游戏没变化 | 部署目录不对 / pck 没重打 | §1.2 |
| 存档/角色进度异常 | Steam 云同步删除本地文件 | 关云同步 + 备份（§4.2） |

### 4.7 经典案例复盘（每个都值得记住）

**① 「本回合免费」串到别人身上（§15.1）**
- 现象：多人模式里"本回合打出的下一张牌免费"对队友也生效。
- 根因：补丁/能力没有按 `Owner` 过滤，且漏了 `ShouldSkip` 的第一条。
- 教训：**所有"本回合/本场"效果都必须按玩家隔离**；写多人相关逻辑前先看 `ShouldSkip`。

**② 能力"不可叠层"但层数乱跳（§6.24）**
- 根因：`StackType`（不叠）与 `Amount` 语义脱节 —— 每次 `Apply` 都改 `Amount`，但显示/结算按另一套走。
- 教训：让 `Amount` 成为**唯一真相**，UI 读同一个值；不要另开 `DisplayAmount` 之类影子字段去"补"。

**③ 生成牌计数口径不一致（§6.27 / §6.34）**
- 同一个"每当施加 X"在"实际生效"和"施加动作"两种口径下结果不同（人工制品会抵消但动作发生）。
- 教训：先想清楚需求是"动作"还是"实际生效"，再选 `CombatHistory` 还是累加器；统一收口（一个入口函数）。

**④ 「梦魇」抽 5 张变成 5 张复制（§40.8）**
- 根因：生成物进了手牌/抽牌堆 → 再次触发"抽到即复制"。
- 修法：生成物直接进**弃牌堆**，并在生成时打标记；借鉴 `AfterShuffle` 时序处理洗牌。
- 教训：任何"复制/生成"都要问一句"它会不会再次进入触发源"。

**⑤ 能力牌不进弃牌堆 / 「不朽」附魔失效（§40）**
- 根因：能力牌默认 `PileType.None`（打出后不留在任何牌堆）。
- 修法：`ModifyCardPlayResultLocation` 里把 `Power + None` 映射到 `Discard`；并在 `CanEnchant` 里明确允许条件。
- 教训：附魔的生命周期跟**牌**走，但牌的"落点"是 `CardCmd` 的职责，两者要分别验证。

**⑥ 能力给的格挡被敏捷加成（§44）**
- 根因：`ValueProp.Move` 才会被 `DexterityPower/FrailPower` 修正；能力给的格挡必须是 `Unpowered`。
- 教训：任何"非卡牌直接给的格挡"一律 `Unpowered`，并在卡面挂 `StaticHoverTip.Block`。

**⑦ 附魔自伤被算成法术伤害（§27）**
- 根因：附魔触发的一次性伤害沿用了卡的 `SpellProps`，导致「法术增益」多扣血。
- 修法：非攻击性/非施法伤害一律不带法术 props。
- 教训：**伤害来源分类**（攻击/法术/能力）决定吃哪些加成，传递 props 时要显式区分。

**⑧ mod 更新后 API 删除（beta 兼容）**
- 例：`GetBaseValueForIConvertible` 被移除、`PullNextRelicFromFront` 换签名、`vfx_rock_shards` 资源被删。
- 流程：刷反编译树 → `dotnet build` 看报错 → 按新签名改 → 启动自检。**不要**为了修编译错误去改卡牌设计。

### 4.8 别急着"修"的原版行为

- 荆棘反伤 0 点：原版行为（§26）。
- 人工制品抵消减益，但"施加动作"仍发生（§6.37/§6.41）。
- 给玩家自己上原版减益会多留一回合（§6.30）。
- 能力牌默认不留在牌堆（§40）。

---

## 5. 工程结构与关键文件

```
src/
├── Wizard.json                 # 清单：版本 / 依赖 / pck+dll 标记
├── Wizard.csproj               # 引用 sts2.dll + BaseLib.dll
├── Scripts/
│   ├── Entry.cs                # 入口：初始化、日志、AltArt 挂载
│   ├── WizardKeywords.cs       # 自定义关键词注册（BaseLib CustomEnum）
│   ├── Cards/                  # 82 个文件 = 80 张卡 + WizardCardModel + WizardSpellCard
│   ├── Powers/                 # 39 个文件 = 36 个能力 + 基类/变体/注册表
│   ├── Relics/                 # 11 = 10 遗物 + ArcaneRelicModel
│   ├── Potions/                # 3
│   ├── Events/                 # 4
│   ├── Enchantments/           # 2（复奏 Reprise / 不朽 Immortal）
│   ├── Monsters/               # 1（火山巨兽三件套）
│   ├── Spells/                 # 法术体系：ISpellCard / SpellSchool / SpellProps / SpellCmd / SpellUtil /
│   │                           #   WizardEvents / WizardTips / DebuffUtil / BurnTally / FieldState
│   ├── Utils/
│   │   ├── CardObliteration.cs # 「即毁」：从牌组永久移除
│   │   ├── Transmutation.cs    # 「嬗变」：自选候选池的 transform（2026-11-22）
│   │   ├── RandomDebuff.cs     # 随机减益（统一排除燃烧等）
│   │   └── WizardVfx.cs        # 演出/音效常量
│   ├── Patches/                # 11 个 Harmony 补丁（死亡立绘、商人生成、动画等）
│   ├── Character/              # 角色定义（WizardCharacter）
│   └── Pools/                  # WizardCardPool / WizardRelicPool / WizardPotionPool
└── Wizard/
    ├── localization/{zhs,eng,zht,jpn}/*.json   # 14 张表
    ├── images/…                # 卡图 / 能力图标 / 遗物图集 / 附魔 / 事件 / 角色
    └── scenes/…                # 角色场景（含骨骼动画版）
```

**关键文件说明**：

| 文件 | 作用 |
|---|---|
| `Cards/WizardCardModel.cs` | 卡牌基类：统一卡图路径 `res://Wizard/images/cards/{类名}.png` |
| `Cards/WizardSpellCard.cs` | 法术牌基类：`ISpellCard` + `School` |
| `Utils/Transmutation.cs` | 嬗变：自选候选 → `new CardTransformation(original, replacement)` → `CardCmd.Transform`，再补生成钩子 |
| `Utils/CardObliteration.cs` | 即毁（`Obliterate` 关键词）：`RemoveFromDeck(DeckVersion)` |
| `Powers/CaudaPavonisPowerBase.cs` | 孔雀之尾：基础版/升级版共享逻辑的范例 |
| `Patches/WizardDeathSprite.cs` | 死亡立绘（限定只改巫师自己的节点） |
| `docs/EVENT-INTERFACE.md` | 事件/怪物/先古对话的接口与文件约定 |

**文档地图**：

| 文档 | 内容 |
|---|---|
| `docs/design.md` | 卡牌/遗物/事件设计稿（**用户维护，唯一事实来源**） |
| `docs/CONTEXT.md` | 逐日工程日志与踩坑（**最长、最全，遇事不决先搜这里**） |
| `docs/HANDOVER.md` | 本文档：写卡实践 / 调试 / 规范 |
| `docs/EVENT-INTERFACE.md` | 事件系统接口 |
| `docs/BETA_2026-11-13_COMPAT.md` | beta 兼容性改动清单 |
| `docs/releases/v*.md` | 每个版本的发布说明 |
| `docs/local/**` | 本地化团队文档（zht/jpn） |
| `docs/CARD_EXPORT.md` / `CARD_POOL_REVIEW*.md` / `card_pool_audit_v3/**` | 卡池审计与平衡分析（历史资料） |

---

## 6. 交接清单 / 下次游戏更新后

### 6.1 每次游戏更新后

1. 刷新反编译树（`reference/decompiled/game-<date>`），保留旧快照。
2. `dotnet build` 看有没有 API 删除/签名变化 → 修编译。
3. 跑 `build.ps1` → 启动自检（补丁数、异常、缺失文案）。
4. 重点回归：**自定义关键词、`CardCmd.Transform`、生成钩子、附魔生命周期、角色场景**（这几处最容易受引擎改动影响）。
5. 更新 `docs/BETA_<date>_COMPAT.md` 与 CONTEXT。

### 6.2 当前待验证 / 已知风险

- 嬗变（§45）实机验证清单见 CONTEXT §45.5。
- Steam 云同步有"删本地档"的危险行为：**演示/测试前先备份**（`D:\Backups\STS2\backup-sts2-saves.ps1`）。
- `BetterSaves`（若启用）会把 modded ↔ vanilla 互同步，可能覆盖手动编辑的存档。
- 工坊简介余量很小（约 86 B），下次加内容需先删「新增卡牌」旧条目。
- SSH 推送不稳定，必要时走 HTTPS。

### 6.3 交接第一周建议顺序

1. 读 `docs/design.md` 的卡牌列表 → 随机挑 3 张卡对照代码读（推荐：`CalmMind` / `Fireball` / `MagusRobe`）。
2. 跑一次完整构建 + 启动自检，确认环境无问题。
3. 读本档 §3（规范）与 §4.6（速查表）。
4. 接一个小的平衡调整（改数值 + 文案）走完整流程：build → deploy → 自检 → 写 CONTEXT。

---

## 7. 命令速查

```powershell
# ── 构建 / 部署 ──
.\build.ps1 -SkipAltArt -NoCommit          # 日常迭代
.\build.ps1                                 # 完整（含 B 版美术）
dotnet build .\src\Wizard.csproj -c Debug -v minimal --no-incremental

# ── 日志 ──
Get-Content "$env:APPDATA\SlayTheSpire2\logs\godot.log" -Tail 80
Select-String -Path "$env:APPDATA\SlayTheSpire2\logs\godot.log" -Pattern '补丁已应用|Exception|Missing localization'

# ── 启动（先告知用户！）──
Start-Process 'steam://rungameid/2868840'

# ── 反编译查证 ──
Get-ChildItem reference\decompiled\game -Recurse -Filter 'CardCmd.cs'
Select-String -Path 'reference\decompiled\game\**\*.cs' -Pattern 'class CardCmd'

# ── 本地化键检查（两语言必须一致）──
python -c "import json,glob; ks=[set(json.load(open(f,encoding='utf-8'))) for f in glob.glob('src/Wizard/localization/eng/*.json')+glob.glob('src/Wizard/localization/zhs/*.json')]; print('键数:', [len(k) for k in ks])"

# ── 存档备份 ──
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\Backups\STS2\backup-sts2-saves.ps1'

# ── 发布（详见 §3.8）──
cd D:\Downloads\ModUploader-win-x64
.\ModUploader.exe upload -w .\WizardWorkspace
gh release create v<ver> <zip> --title <ver> --notes-file docs/releases/v<ver>.md
```

---

*本文档由 2026-11-26 的交接整理产生；日常遇到新坑请继续写进 `docs/CONTEXT.md`，不要只改代码不写文档。*
