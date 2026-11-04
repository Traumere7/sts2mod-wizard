# 进度

## zht（繁體中文）

| 文件 | 状态 | 备注 |
|---|---|---|
| characters.json | ✅ 完成 | 15 条 |
| powers.json | ✅ 完成 | 87 条；29 个能力 |
| cards.json | ✅ 完成 | 136 条；65 张卡 |
| relics.json | ✅ 完成 | 27 条；9 个遗物 |
| potions.json | ✅ 完成 | 7 条 |
| monsters.json | ✅ 完成 | 4 条 |
| encounters.json | ✅ 完成 | 2 条 |
| events.json | ✅ 完成 | 46 条；3 个事件 |
| ancients.json | ✅ 完成 | 89 条；9 位先古 |
| settings_ui.json | ✅ 完成 | 2 条 |

## jpn（日本語）

| 文件 | 状态 | 备注 |
|---|---|---|
| characters.json | ✅ 完成 | 15 条 |
| powers.json | ✅ 完成 | 87 条；29 个能力 |
| cards.json | ✅ 完成 | 136 条；65 张卡 |
| relics.json | ✅ 完成 | 27 条；9 个遗物 |
| potions.json | ✅ 完成 | 7 条 |
| monsters.json | ✅ 完成 | 4 条 |
| encounters.json | ✅ 完成 | 2 条 |
| events.json | ✅ 完成 | 46 条；3 个事件 |
| ancients.json | ✅ 完成 | 89 条；9 位先古 |
| settings_ui.json | ✅ 完成 | 2 条 |

## 待办

- [x] zht key 数量、标签总数、术语与空格人工核对
- [x] jpn key 数量、标签总数、术语与空格人工核对
- [ ] zht / jpn 全量自动 JSON 解析 / 标签逐 key diff（建议在 CI 或本地脚本跑）
- [ ] zht / jpn 术语一致性抽查
- [ ] 确认 `Burn` 能力改名「焚燒」后的文本一致性（zht）
- [ ] 复核 jpn 命名（尤其 `見張り火`、`着火`、`大寒波`、`ダブルリコシェ`）

## 本轮关键决定

- Spell Power = 魔法威力（zht/jpn）。
- Burn 能力 zht = 焚燒（避让原版 INFLAME「燃燒」）；Burn 状态牌 zht = 灼傷。
- zht Rules Horror = 規則怪談；jpn = ルール・ホラー。
- zht 卡名整体重写，未照搬 zhs。
- `eng` 已修 Doubt/DEFY 两处标签（见 `ENG_FIXES.md`）。
