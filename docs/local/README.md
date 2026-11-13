# 本地化工作区（zht / jpn）

> 本目录只记录本地化相关结论、规范、术语、拍板决定与进度。
> 只改 `src/Wizard/localization` 下的本地化文件；不碰其他工程文件，也不动 `eng` / `zhs` 中未经授权的文案。

## 当前状态

| 语言 | 状态 | 说明 |
|---|---|---|
| zht（繁體中文） | **首轮完成** | 10 个文件已落盘；已补 Aether、Cauda Pavonis、Eternal Echo / Mercury 新内容，待全量校验 |
| jpn（日本語） | **首轮完成** | 10 个文件已落盘；已补 Aether、Cauda Pavonis、Eternal Echo / Mercury 新内容，待全量校验 |

## 权威顺序（重要）

1. **`zhs` 是人工审核过的版本**，格式、措辞、术语以它为准。
2. **`eng` 是内容来源，但未经审核**；发现格式/标签错误时，经授权可直接修 `eng`（见 `ENG_FIXES.md`）。
3. **zht/jpn 的硬性结构仍以英文 key / BBCode 标签数量为准**；zhs 只作措辞与术语参考。
   若 zhs 与 eng 的标签数量冲突，先判断是否为 eng 的标签错误；确认后修 eng，再让译文对齐。
4. 原版游戏 localization（`reference/archive/game-recovery/localization`）是术语与风格的最终依据；
   zht 用台湾用语，jpn 用原版日语惯用表达。

## 已拍板的决定

- **Spell Power = 魔法威力**（zht / jpn 相同）。
- **jpn 属性伤害写法**：`[orange]炎[/orange]属性のXダメージ`（标签只包属性词）。
- **Rules Horror**：zht「規則怪談」，jpn「ルール・ホラー」。
- **与原版撞名时一律改名**，具体译名可自定；已知冲突见 `GLOSSARY.md`。
  - 例：Burn 能力 zht 改「焚燒」（原版 INFLAME 已用「燃燒」）。
- **zht 卡名整体按 zht 风格重写**，不照搬 zhs 卡名。
- **VOLCANO_SAGE 的 Doubt 用 `[red]`**（塞诅咒语境；参考原版 `TRIAL`）。
- 授权修 `eng` 的格式错误；每笔修改记录在 `ENG_FIXES.md`。

## 文件索引

| 文件 | 用途 |
|---|---|
| `SURVEY.md` | 参考文档探索结论（zht/jpn/zhs 差异、命名风格、zhs-eng 冲突） |
| `GLOSSARY.md` | 英文 / zhs / zht / jpn 术语表、Mod 专有词、命名冲突 |
| `ZHT_NAMES.md` | zht 全量命名表（卡牌/能力/遗物/事件等） |
| `JPN_NAMES.md` | jpn 全量命名表（卡牌/能力/遗物/事件等） |
| `ZHT_STYLE.md` | 繁中格式、标点、句法、卡名风格 |
| `JPN_STYLE.md` | 日文格式、语序、量词、片假名政策、语气 |
| `VOICE.md` | 角色与九位先古的语气/人称表 |
| `ENG_FIXES.md` | eng 格式修正记录 |
| `QA.md` | 落盘后的自检清单 |
| `PROGRESS.md` | 各文件进度与待办 |

## 工作顺序

`characters → powers → cards → relics/potions/monsters/encounters/settings → events → ancients`

先定语气与术语，再铺量；每批完成后按 `QA.md` 自检。
