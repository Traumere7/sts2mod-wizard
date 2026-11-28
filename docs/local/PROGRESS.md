# 进度

## zht（繁體中文）

| 文件 | 状态 | 备注 |
|---|---|---|
| characters.json | ✅ 完成 | 15 条 |
| powers.json | ✅ 完成 | 114 条；38 个能力 |
| cards.json | ✅ 完成 | 181 条；87 张卡 |
| card_keywords.json | ✅ 完成 | 2 条；1 个关键词 |
| enchantments.json | ✅ 完成 | 4 条；2 个附魔 |
| card_selection.json | ✅ 完成 | 1 条 |
| static_hover_tips.json | ✅ 完成 | 2 条；嬗变机制 |
| relics.json | ✅ 完成 | 30 条；10 个遗物 |
| potions.json | ✅ 完成 | 7 条 |
| monsters.json | ✅ 完成 | 4 条 |
| encounters.json | ✅ 完成 | 2 条 |
| events.json | ✅ 完成 | 54 条；4 个事件 |
| ancients.json | ✅ 完成 | 89 条；9 位先古 |
| settings_ui.json | ✅ 完成 | 2 条 |

## jpn（日本語）

| 文件 | 状态 | 备注 |
|---|---|---|
| characters.json | ✅ 完成 | 15 条 |
| powers.json | ✅ 完成 | 114 条；38 个能力 |
| cards.json | ✅ 完成 | 181 条；87 张卡 |
| card_keywords.json | ✅ 完成 | 2 条；1 个关键词 |
| enchantments.json | ✅ 完成 | 4 条；2 个附魔 |
| card_selection.json | ✅ 完成 | 1 条 |
| static_hover_tips.json | ✅ 完成 | 2 条；嬗变机制 |
| relics.json | ✅ 完成 | 30 条；10 个遗物 |
| potions.json | ✅ 完成 | 7 条 |
| monsters.json | ✅ 完成 | 4 条 |
| encounters.json | ✅ 完成 | 2 条 |
| events.json | ✅ 完成 | 54 条；4 个事件 |
| ancients.json | ✅ 完成 | 89 条；9 位先古 |
| settings_ui.json | ✅ 完成 | 2 条 |

## 待办

- [x] zht key 数量、标签总数、术语与空格人工核对
- [x] jpn key 数量、标签总数、术语与空格人工核对
- [x] Aether 版本新增 8 卡 + 2 能力已补 zht/jpn
- [x] Cauda Pavonis 版本新增 4 卡 + 3 能力已补 zht/jpn
- [x] Eternal Echo / Mercury 版本新增 2 卡 + 1 能力已补 zht/jpn
- [x] Temperance / Magus Robe 版本新增 2 卡 + 2 能力已补 zht/jpn
- [x] Unknown Sea / Nightmare / Aqua Heart / Immortal 已补 zht/jpn（含 card_keywords、enchantments 新文件）
- [x] Transmute（嬗變 / 変成）机制及 Animism / Quickening / Transmogrify 已同步
- [x] Electric Arc / Flicker / Kindling / White Night / Wildfire 及 Kindling 能力已补；Skyfire Rampage 改为触发 2 次
- [ ] zht / jpn 全量自动 JSON 解析 / 标签逐 key diff（建议在 CI 或本地脚本跑）
- [ ] zht / jpn 术语一致性抽查
- [ ] 确认 `Burn` 能力改名「焚燒」后的文本一致性（zht）
- [ ] 复核 jpn 命名（尤其 `見張り火`、`着火`、`大寒波`、`ダブルリコシェ`）

## 本轮关键决定

- Spell Power = 魔法威力（zht/jpn）。
- Burn 能力 zht = 焚燒（避让原版 INFLAME「燃燒」）；Burn 状态牌 zht = 灼傷。
- zht Rules Horror = 規則怪談；jpn = ルール・ホラー。
- zht 卡名整体重写，未照搬 zhs。
- jpn 命名修正：Myth → **迷妄**（不译「神話」）；Omniscient Form → **全知化**（原版 FORM 系均译「〜化」）；Clear World → **氷結世界**、Purify Sin → **祓罪**（避免两张都带「浄化」）。
- `eng` 已修 Doubt/DEFY 两处标签（见 `ENG_FIXES.md`）。
- Aether 版本新增 8 卡 + 2 能力：zht/jpn 已按 zhs 对齐；`Aether` zht = 乙太，jpn = エーテル。
- Aether 相关高亮统一为 `[gold]`（eng/zhs/zht/jpn）；不再用 `[blue]` 修饰 Aether / 以太 / 乙太 / エーテル。
- 新内容 eng 标签对齐：`SPEED_READ` 补 `[gold]Hand[/gold]`；`THE_FOURTH_ELEMENT` 补 `[gold]Aether Arrow[/gold]`（见 `ENG_FIXES.md`）。
- `MANA_BURST` 新文案已同步 zht/jpn（Aether 伤害改为按魔法威力结算，并加 `InCombat` 预览）；Aether 仍用 `[gold]`。
- Cauda Pavonis 版本新增：`Cauda Pavonis`、`Great Library`、`Quickening`、`Random debuff` 四卡，以及 `Cauda Pavonis` / `Cauda Pavonis+` / `Great Library` 三能力；zht/jpn 已补。
- Eternal Echo / Mercury 版本新增：`Eternal Echo`、`Mercury` 两卡，以及 `Mercury` 能力；zht/jpn 已补。Swift 采用原版附魔译名：zht **迅捷** / jpn **迅速**。
- `zhs` 的 `MERCURY_POWER.description` 补上 `[blue]{Swift}[/blue]` 标签，与 eng 的标签数对齐。
- Temperance / Magus Robe 版本新增：`Temperance`、`Magus Robe` 两卡，以及对应两个能力；zht/jpn 已补。`Magus Robe` zht = 偉大法袍，jpn = マグス・ローブ。
- `eng` 的 `MAGUS_ROBE` 卡面与 `MAGUS_ROBE_POWER` 描述/smart 补上 `[gold]debuff[/gold]`，与 zhs 的两处 gold 标签对齐。
- Unknown Sea：zht **未知海洋** / jpn **未知の海**；新增 8 条事件文本。
- Nightmare：zht **夢魘** / jpn **悪夢**；按用户指定保留，与原版 Silent 的 NIGHTMARE 同名（见 `GLOSSARY.md` 冲突表）。
- Aqua Heart：zht **水藍之心** / jpn **アクアハート**。
- 新文件：`card_keywords.json`（Obliterate zht 即毀 / jpn 抹消）、`enchantments.json`（Immortal 不朽；Reprise zht 復奏 / jpn リプリーズ）。
- Transmute：zht **嬗變** / jpn **変成**；新增 `card_selection.json`、`static_hover_tips.json`，并更新 `Animism`、`Quickening`、`Transmogrify`。
- `Quickening` 的 `selectionScreenPrompt` 在 eng/zhs 仍写 Transform，已改为 Transmute / 嬗变；zht/jpn 同步为嬗變 / 変成。
- `Magus Robe` 卡面改为 `{MagusRobePower:diff()}` 变量；zht/jpn 已补占位符。
- `Magnum Opus`（杰作）：当前 eng/zhs 效果文本与 zht/jpn 一致，未发现可见文本 diff；本次未改其文案。
- 新增 5 卡：Electric Arc（電弧）、Flicker（靈光 / 明滅）、Kindling（薪柴 / 焚き付け）、White Night（白夜）、Wildfire（流火 / 野火）；新增 `KINDLING_POWER`（薪柴 / 焚き付け）。
- `SKYFIRE_RAMPAGE`（天火浩蕩 / 天火暴走）：立刻触发 1 次 → **2 次**，zht/jpn 已同步。
