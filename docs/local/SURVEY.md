# 参考文档探索结论

> 记录 2026-11-04 通读原版 localization 与 Mod eng/zhs 后得到的、需要长期保留的结论。
> 具体执行规范见 `ZHT_STYLE.md` / `JPN_STYLE.md`；术语见 `GLOSSARY.md`。

## 1. 结构

- Mod 本地化为 10 个 JSON：`characters`、`cards`、`relics`、`powers`、`potions`、`monsters`、`encounters`、`events`、`ancients`、`settings_ui`。
- key 以 `WIZARD-` 开头；先古对话为 `{Ancient}.talk.{Character}.{套}-{行}[r].{ancient|char|next}`。
- `.next` 是对话按钮文本；`r` 表示可重复套。
- 原版支持 16 种语言，目录用 `zht` / `jpn` 三字母码。

## 2. 硬约束

- BBCode 标签必须与英文“类型 + 数量”一致；不能自行增删。
- 占位符根名必须保留；`:diff()`、`:energyIcons()`、`:percentMore()` / `:percentLess()`、`{IfUpgraded:show:…}`、`{InCombat:…}` 不能改成固定值。
- `:plural:` 只用于英语；原版 zht/jpn **零使用**。中文/日文删除它，保留根变量。
- `description` 是静态描述（无具体数值），`smartDescription` 是动态描述（含 `{Amount}`）；不要互换。
- 原版 zht 规则文：数字/占位符两侧加空格；jpn 规则文：完全不加空格。

## 3. zht 与 zhs 的关键差异

| 概念 | zhs | zht |
|---|---|---|
| heal | 回复 | 恢復 |
| transform | 变化 | 轉化 |
| exhaust pile | 消耗牌堆 | 消耗堆 |
| plating | 覆甲 | 鍍甲 |
| intangible | 无实体 | 無形 |
| debuff | 负面效果 | 弱化效果 |
| skip | 跳过 | 略過 |
| next turn | 下个回合 | 下回合 |
| ascension | 进阶 | 晉階 |
| elite / boss | 精英 / Boss | 菁英 / 頭目 |

结论：**zht 不是 zhs 的繁体转换**；必须按原版 zht 的台湾用语与文学风格重写。

## 4. jpn 与 zhs 的关键差异

| 概念 | zhs | jpn |
|---|---|---|
| exhaust | 消耗 | 廃棄 |
| exhaust pile | 消耗牌堆 | 廃棄札 |
| draw pile | 抽牌堆 | 山札 |
| discard pile | 弃牌堆 | 捨て札 |
| hand | 手牌 | 手札 |
| block | 格挡 | ブロック |
| energy | 能量 | エナジー |
| weak | 虚弱 | 脱力 |
| vulnerable | 易伤 | 弱体 |
| frail | 脆弱 | 脆弱 |
| plating | 覆甲 | プレート |
| intangible | 无实体 | 霊体 |
| transform | 变化 | 変化 |
| debuff | 负面效果 | デバフ |

结论：jpn 语序、量词、片假名政策都独立；不能按中文语序直译。

## 5. 原版命名风格

- **zht**：2–4 字为主；文学化、克制；台湾用语；允许「！」。例：Bash 猛擊、Ascender's Bane 晉階者之禍、Ice Lance 冰槍。
- **jpn**：片假名写外来/招式名（ボールライトニング、アイスランス、ヘイルストーム）；汉字写本土概念（強打、氷河、寒波、神格化）；允许整句/感叹（叩き直してやる、失せよ！）。
- **职业名**：zht 用中文（鐵甲戰士、寂靜獵手、儲君、縛亡者）；jpn 用片假名（アイアンクラッド、サイレント、リージェント、ネクロバインダー）。所以本 Mod：zht「巫師」/ jpn「ウィザード」。
- **先古名**：zht 音译（達弗、涅奧、諾奴佩普、歐洛巴斯、佩爾、坦克斯、特茲卡塔拉、瓦庫、建築師）；jpn 片假名（ダーヴ、ネオー、ノヌペイペ、オロバス、パエル、タンクス、テスカタラ、ヴァクー、アーキテクト）。

## 6. 本 Mod 的命名风格

- 英文五类：炼金/秘术（Athanor、Tria Prima、Maleficarum、Magnum Opus）、科学（Alternating Current、Electromagnetic Wave、Superconduct）、诗意（Burning Soul、Skyfire Rampage、Clear World）、日常物（Bookmark、Endnote、Tidy Up）、招式（Fireball、Ice Arrow、Lightning Bolt、Blazing Punch）。
- zhs 风格：2–4 字、术/之/拳、4 字成语（天火浩荡、高温消毒、真理派对、禁书目录）、元素复合（炽焰、巨焱、寒冰、雷电），比原版 zht 更“网文/仙侠”。
- zht 重写原则：按原版 zht 的克制文学腔，不整体照搬 zhs；法术牌可用「術」，少堆 4 字成语。
- jpn 原则：招式/专名片假名，概念/状态汉字；避免与原版标题完全撞名。

## 7. zhs 与 eng 的冲突（已处理/待观察）

- 已修 eng：
  - `VOLCANO_SAGE.DEBATE` 的 Doubt：`[gold]` → `[red]`（塞诅咒用红色，参考原版 TRIAL）。
  - `VOLCANO_SAGE.DEFY` 的 “do not fear”：`[red]` → `[gold]`（对齐审核版 zhs）。
- 已改 zht 命名：
  - Burn 能力 → 「焚燒」，避让原版 INFLAME「燃燒」；Burn 状态牌仍为「灼傷」。
- 待观察：
  - `AMNESIA`：eng 有 `{AmnesiaPower:diff()}`，zhs 省略；zht 暂保留变量。
  - `EPIPHANY`：eng 比 zhs 多一处 `[gold]Hand[/gold]`；zht 暂对齐 eng。
- 原则：**格式/标签以 zhs 为审核参考；内容结构以 eng 为源；冲突时先判断是否为 eng 格式错误，再修 eng 并记录。**

## 8. 主要参考

- `reference/archive/game-recovery/localization/README.md`（校验规则）
- `reference/archive/game-recovery/localization/zht/{cards,powers,relics,events,ancients,characters,monsters}.json`
- `reference/archive/game-recovery/localization/jpn/{cards,powers,relics,events,ancients,characters,monsters}.json`
- `reference/docs/tutorials-src/Basics/05 - 变量与描述/README.md`
- `reference/docs/tutorials-src/Basics/06 - 先古对话/README.md`
- `src/Wizard/localization/eng|zhs/*.json`
