# eng 格式修正记录

> 规则：只修格式/标签/占位符错误，不擅自改英文措辞。
> 依据：`zhs` 为人工审核版本；`eng` 未经审核。发现冲突时先对照 `zhs` 与原版同类文本，再决定是否修 `eng`。

## 2026-11-04

### 1. `WIZARD-VOLCANO_SAGE.pages.INITIAL.options.DEBATE.description`

- 原：`Add [blue]1[/blue] [gold]Doubt[/gold] to your [gold]deck[/gold].`
- 改：`Add [blue]1[/blue] [red]Doubt[/red] to your [gold]deck[/gold].`
- 理由：塞诅咒语境用 `[red]`；`zhs` 已用 `[red]疑虑[/red]`；原版 `TRIAL` 事件塞诅咒同样用 `[red]`。

### 2. `WIZARD-VOLCANO_SAGE.pages.DEFY.description`

- 原：`... you [red]do not fear[/red] the fight.`
- 改：`... you [gold]do not fear[/gold] the fight.`
- 理由：`zhs` 用 `[gold]无惧[/gold]`；`[red]` 留给“何人/危险/代价”等负面语义，正面意志用 `[gold]`。

## 待观察（暂不改）

- `WIZARD-AMNESIA.description`：`eng` 保留 `{AmnesiaPower:diff()}`，`zhs` 省略。两者语义都通；暂不改 `eng`，zht 保留变量。
- `WIZARD-EPIPHANY.description`：`eng` 的 `[gold]Hand[/gold]` 比 `zhs` 多一处。zht 对齐 `eng`；若后续确认 `zhs` 的“少一处高亮”是审核意图，可再统一。
