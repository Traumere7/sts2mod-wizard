# 巫师 Mod — 战斗骨骼动画调研与设计方案

> 撰写日期：2026-11-22  
> 前置里程碑：A 版默认巫师已完成 12 分件拆层与 Godot 4.5 原生 `Skeleton2D` 待机呼吸动画 `idle`（见 `src/Wizard/scenes/wizard_character.tscn`），游戏内实测通过。  
> 调研依据：`reference/decompiled/game`（STS2 v0.111.0 源码）、`reference/decompiled/BaseLib`（BaseLib 3.4.8）、`D:\Godot\Slay the Spire 2\animations\characters\*_skel_data.tres`、本 Mod `src/Scripts/Cards/` 全部 82 张卡牌。

---

## 一、原版 STS2 角色战斗动作全量调研

### 1.1 通用底座（所有角色共享）

原版基类 `CharacterModel.cs`（`AnimationStates` 与 `GenerateAnimator`）为所有玩家角色定义了统一的动作状态机底座：

| 类别 | 触发器（Trigger） | 原版动画名 | 循环 | 自动回位 | 引擎调用入口 |
|---|---|---|---|---|---|
| **常规待机** | `"Idle"` | `idle_loop` | 是 | — | 进场默认；`CreatureCmd.Heal` 等回血后刷新 |
| **残血待机** | `"Idle"`（HP ≤ 25%） | `low_health_loop` | 是 | — | `CharacterModel.IsLowHealth` 条件分支 |
| **放松待机** | `"Relaxed"` | `relaxed_loop` | 是 | 遇 `"Idle"` 回待机 | 假商人事件（`NFakeMerchant`）、商店（`NMerchantCharacter`） |
| **普通攻击** | `"Attack"` | `attack` | 否 | 回待机 | `AttackCommand`（所有 `DamageCmd.Attack` 自动触发） |
| **施法 / 技能** | `"Cast"` | `cast` | 否 | 回待机 | 技能牌显式调 `CreatureCmd.TriggerAnim(..., "Cast", CastAnimDelay)` |
| **能力 / 强化** | `"PowerUp"` | 默认复用 `cast` | 否 | 回待机 | 能力牌显式调 `CreatureCmd.TriggerAnim(..., "PowerUp", PowerUpAnimDelay)` |
| **受击硬直** | `"Hit"` | `hurt` | 否 | 回待机 | `CreatureCmd.Damage` 造成 **未格挡伤害 > 0 且非自伤** 时自动触发 |
| **死亡倒地** | `"Dead"` | `die` | 否 | **停在末帧** | `NCreature.StartDeathAnim`、`NGameOverScreen`、`NMerchantCharacter` |
| **复活** | `"Revive"` | *无（全员未做）* | — | — | 原版 6 角色均未定义，统一走 `NCreature.AnimTempRevive()` 淡出→切 `Idle`→淡入 |

> **关键源码细节（`CreatureCmd.cs`）**：
> - `TriggerAnim` 在触发 `"Attack"` / `"Cast"` / `"PowerUp"` 时会**自动播放对应角色音效**（`AttackSfx` / `CastSfx` / `PowerUpSfx`），随后等待 `Cmd.CustomScaledWait(min(waitTime * 0.5f, 0.25f), waitTime)`（自动兼容加速模式）。
> - 自伤（`receiver == dealer`）与被格挡完全挡下的伤害**不会**触发 `"Hit"`。
> - 原版防御牌（如 `DefendIronclad`）只调 `CreatureCmd.GainBlock`，**不**调用 `TriggerAnim`；而带法术/增益/召唤/抽牌等明显施法意象的技能牌与所有能力牌才会显式调 `TriggerAnim("Cast")` / `TriggerAnim("PowerUp")`。

---

### 1.2 原版 6 名角色的「招牌特色动作」规律

逐一查阅原版反编译源码（含 Beta 第 6 角色 `Fool`），发现原版遵循一条极其克制的工业规范：**每位角色在通用底座之外，只多做 1 个强辨识度的招牌动作**。

| 原版角色 | `AttackAnimDelay` | `CastAnimDelay` | `PowerUpAnimDelay` | 专属特色动作（动画名 / Trigger） | 用途 |
|---|---|---|---|---|---|
| **铁甲战士** (`Ironclad`) | `0.15s` | `0.25s` | `0.25s` | `attack_heavy` / `"heavyAttack"`（延迟 `0.20s`） | 重击类大攻击（重锤、痛击等） |
| **静默猎手** (`Silent`) | `0.15s` | `0.25s` | `0.25s` | `shiv` / `"Shiv"` | 飞刀快速投掷 |
| **故障机器人** (`Defect`) | `0.15s` | `0.25s` | **`0.50s`** | `process` / `"PowerUp"` | 把能力牌从 `cast` 拆出，做独立充能演算动作 |
| **亡灵契约师** (`Necrobinder`) | `0.15s` | **`0.40s`** | `0.40s` | `cast_mighty`（Cast/PowerUp）+ `cast`（`"summonTrigger"`，`0.25s`） | 区分重型暗影施法与轻量召唤奥斯提 |
| **摄政王** (`Regent`) | `0.15s` | `0.25s` | `0.25s` | `attack_sovereign` / `"sovereignBladeTrigger"`（延迟 `0.25s`） | 君王之刃挥砍 |
| **愚者** (`Fool`) | `0.15s` | `0.25s` | `0.25s` | `shoot` / `"shootBow"`（延迟 `0.45s`） | 拉弓射箭 |

### 1.3 原版融合过渡（Mix）参数参考

查阅全部原版 `*_skel_data.tres` 的 `SpineAnimationMix` 配置，STS2 战斗动作节奏极干脆，**绝不拖泥带水**：
- 全局默认过渡：`default_mix = 0.05s`（极短，保留打击脆感）
- `idle_loop → attack`：`0.02s ~ 0.10s`
- `idle_loop → hurt`：`0.02s ~ 0.03s`（受击必须瞬间响应！）
- `hurt → idle_loop`：`0.10s`（回位略柔和）
- `attack → attack` / `hurt → hurt` / `hurt → die`：`0.00s`（连击或受击致死立刻切断重播，零粘滞！）

---

## 二、巫师 Mod 建议制作的战斗动作清单

结合巫师「以生命与理智换取三系法术（火/雷/冰）+ 以太禁忌奥术」的职业定位，以及我们 82 张卡牌的构成（19 张法术攻击、2 张物理攻击、36 张技能、19 张能力），建议按 **P0 / P1 / P2** 分级推进，**用最少的高质量动作覆盖 100% 战斗体验**：

| 优先级 | 动作名 | 对应 Trigger | 总时长 | 生效延迟（`AnimDelay`） | 定位与覆盖范围 |
|---|---|---|---|---|---|
| **已完成** | `idle` | `"Idle"` | `2.80s`（循环） | — | 战斗默认待机呼吸循环（已实装） |
| **P0 必做** | `attack` | `"Attack"` | `0.55s` | `0.15s` | **挥杖迅击**：物理攻击（打击/黑暗神秘学）及常规直伤法术（火球/电击/冰矢/以太箭等），节奏利落适合高频连打 |
| **P0 必做** | `cast` | `"Cast"` | `0.70s` | `0.25s` | **单手引魔 / 咏唱施法**：绝大多数技能牌（法术准备、护盾、抽牌、法术增益类）与非直伤法术 |
| **P0 必做** | `hurt` | `"Hit"` | `0.40s` | `0s` | **受击后仰硬直**：受到敌人未格挡伤害时瞬间后退缩肩、帽檐掀动、宝石猛晃 |
| **P0 必做** | `die` | `"Dead"` | `1.10s`（停末帧） | `DeathAnimTime = 1.20s` | **力竭倒地**：战斗死亡 + 商店/火堆/涅奥放弃游戏结算画面通用 |
| **P1 招牌** | `powerup` | `"PowerUp"` | `0.95s` | `0.35s` | **巫师专属大施法 / 禁忌仪式**（对标故障机器人 `process` / 亡灵契约师 `cast_mighty`）：19 张能力牌（全知形态、烈焰狂人、大图书馆、伟大法袍等）以及高费终结大法术（天火浩荡、降雷、清界、燃尽灰烬） |
| **P1 复用** | `relaxed` | `"Relaxed"` | `3.20s`（循环） | — | **放松待机**：比战斗态更舒缓、杖身微斜放松，直接复用于**假商人事件**与 **A 版商店场景**（零额外拆件！） |
| **P2 远期** | `low_health` | 条件待机 | `2.00s`（循环） | — | 残血急促喘息（HP ≤ 25%）；BaseLib 原生不处理低血待机切换，需写额外补丁，可等核心动作全齐后再决定是否补 |
| **不做** | `revive` | `"Revive"` | — | — | 与原版 6 角色一致，直接沿用引擎 `AnimTempRevive` 淡入淡出 |

> **为什么把 `powerup` 定为巫师的专属招牌动作？**  
> 巫师拥有高达 **19 张能力牌**（占卡池近 1/4）以及极具仪式感的「全知形态 / 禁忌大法术」。普通技能用轻量 `cast`（0.25s 出手，不拖回合节奏），而打出能力牌或核弹级大法术时触发双手举杖+法典悬浮/共鸣+红宝石高亮的 `powerup`（0.35s 出手），职业辨识度与爽感直接拉满，同时又严格保持与原版同等的维护体量（共 **6 个核心战斗动作 + 1 个复用放松态**）。

---

## 三、各战斗动作的逐帧编排设计（适配当前 13 根骨骼）

> 以下设计基于当前 A 版骨骼树（`BoneRoot` → `BonePelvis` → `BoneRobeLower` / `BoneTorso` → `BoneHead`→`BoneHat`、`BoneSpellbook`、`BoneRightArm`→`BoneRightHand`、`BoneLeftArm`→`BoneLeftForearm`→`BoneStaff`→`BoneGem`），所有动作均遵循动画十二原则中的 **预备（Anticipation）→ 爆发（Action）→ 缓冲与次级跟随（Follow-through & Recovery）**，确保**动作有重量、关节不露缝、回位顺滑**。

### 3.1 `attack` — 挥杖迅击（总长 `0.55s`，打击点 `0.15s`）

- **动作意象**：手握长杖迅速蓄力后撤半步，随即向前猛推/点出杖首释放奥术冲击，红宝石剧烈甩动。
- **时间轴拆解**：
  - **`0.00s → 0.08s`（蓄力预备）**：`BonePelvis` / `BoneTorso` 微向左后收（`x -6px, rot -5°`），`BoneLeftArm` / `BoneLeftForearm` 回拉抬肘，将木杖微提离地并向后蓄势（`BoneStaff rot -12°`）；`BoneRightArm` 向后展开平衡。
  - **`0.08s → 0.16s`（爆发击发 — 对齐 `AttackAnimDelay = 0.15s`）**：身体猛向右前倾（`BoneTorso x +12px, rot +9°`），`BoneLeftForearm` 迅速前伸推杖、`BoneStaff` 向前探出倾斜 `+18°`（杖首指向敌人！），`BoneHead` 压低凝视前方、`BoneHat` 略滞后前压；`BoneGem` 因惯性向后甩起 `-25°`。
  - **`0.16s → 0.28s`（冲击余震 + 宝石回甩）**：躯干与手臂在极点微顿（2~3 帧打击定格感），`BoneGem` 惯性向前过冲甩到 `+32°`！`BoneSpellbook` 与 `BoneRightArm` 随前冲略向前摆。
  - **`0.28s → 0.55s`（回位归地）**：木杖稳稳落回地面原位，全身各骨骼带微小阻尼回到 `idle` 起始姿态，`BoneGem` 衰减晃动。
- **B 版（伊蕾娜）对应演绎**：同为 `0.55s` / `0.15s` 打击点，改为手腕迅捷挥动短魔杖向前点刺、长银发与外袍后摆随前冲大幅飘起。

### 3.2 `cast` — 咏唱施法（总长 `0.70s`，生效点 `0.25s`）

- **动作意象**：不同于 `attack` 的前冲推杖，`cast` 强调**「右手起诀 + 抬杖引魔」**的优雅施法姿态，让玩家一眼区分攻击与技能！
- **时间轴拆解**：
  - **`0.00s → 0.14s`（起手聚能）**：`BoneTorso` 微微挺直微仰（`y -8px, rot -4°`），**画面左侧垂下的宽袖右手 `BoneRightArm` 向前上方抬起约 `-35°`、`BoneRightHand` 张掌上扬**（露出宽大星空袖口），同时左手微抬木杖离地约 `12px`；`BoneSpellbook` 随魔力微震轻浮摆动。
  - **`0.14s → 0.28s`（咒语释放 — 对齐 `CastAnimDelay = 0.25s`）**：躯干微向前送气，右手向前上方微推一顿、木杖顺势轻顿点地（或微前倾 `+8°`），`BoneGem` 晃动并可配合**杖尖/宝石闪过一次奥术紫微光**（可选 `Sprite2D` 叠加微光节点，随关键帧调 `modulate.a`，零额外性能开销！）。
  - **`0.28s → 0.70s`（收势落袖）**：`BoneRightArm` 优雅垂回体侧，`BoneRightHand` 滞后轻垂，木杖轻落回地，帽檐与法袍下摆柔和复位。
- **B 版（伊蕾娜）对应演绎**：同为 `0.70s` / `0.25s`，高举魔杖划一小弧、另一只手轻扶帽檐或展开，长发微浮。

### 3.3 `powerup` — 禁忌觉醒 / 大施法（总长 `0.95s`，生效点 `0.35s`）

- **动作意象**：巫师最具戏剧张力的招牌动作 —— **高举法杖顿地 / 仰首聚魔、法袍鼓荡、红宝石剧烈共鸣**，专门配给能力牌与终结大法术！
- **时间轴拆解**：
  - **`0.00s → 0.22s`（蓄力下压）**：`BonePelvis` 下沉蓄力（`y +6px`），`BoneTorso` 略含胸低头压帽，双手同时收拢聚势，`BoneLeftArm`/`BoneLeftForearm` 把法杖高高提起（离地 ~35px！），`BoneRightArm` 抬至胸前。
  - **`0.22s → 0.38s`（爆发顿杖 / 魔力冲天 — 对齐 `PowerUpAnimDelay = 0.35s`）**：
    - `BoneTorso` 猛然挺拔上拔（`y -12px, rot -7°`），`BoneHead` 昂首、`BoneHat` 微向后掀；
    - `BoneLeftForearm` 干脆有力地将长杖**向下一顿砸地**（或高举顶点定格），`BoneRightArm` 向外大幅张开（`-55°`）；
    - `BoneRobeLower` 轻微横向鼓荡拉伸（`scale (1.06, 1.03)`）营造魔力气浪吹动法袍的视觉张力！
    - `BoneGem` 与 `BoneSpellbook` 强烈晃动，叠加一层奥术紫/红宝石高光脉冲（`modulate` 提亮 0.2s）。
  - **`0.38s → 0.62s`（余威悬停）**：高张姿态略作滞留（0.2s 呼吸微颤），让大牌的 VFX 与屏幕特效有充足的仪式感空间。
  - **`0.62s → 0.95s`（缓步收功）**：各部件依次沉回待机位，次级部件衰减摆动。
- **B 版（伊蕾娜）对应演绎**：魔杖高举画圆、全身微微浮空离地几像素、裙摆与长发向上飘起，随后轻盈落回地面。

### 3.4 `hurt` — 受击硬直（总长 `0.40s`，即时响应 `0s`）

- **动作意象**：干脆利落的受击冲击反馈，首帧立刻后甩，随后迅速稳住重心。
- **时间轴拆解**：
  - **`0.00s → 0.06s`（瞬间受创）**：**极快起势**！`BonePelvis`/`BoneTorso` 猛向左后退仰（`x -14px, y +4px, rot -11°`），`BoneHead` 后仰 `+14°`，`BoneHat` 因惯性剧烈向后翘起并滞后滑移（`-16°`）；双臂向两侧微甩，`BoneStaff` 向后倾斜；`Visuals` 或 `Parts` 可附带 1 帧极短受击泛白/偏红闪光（`modulate` 瞬间闪 `Color(1.35, 0.85, 0.85)` 后 0.12s 回白）。
  - **`0.06s → 0.16s`（惯性余波）**：`BoneHat`、`BoneRightHand`、`BoneSpellbook`、`BoneGem` 到达最大甩动幅度（红宝石猛甩 `-35°`）。
  - **`0.16s → 0.40s`（重稳重心）**：躯干稳健前移归位，法杖重新撑实地面。

### 3.5 `die` — 力竭倒地（总长 `1.10s`，不循环停末帧，`DeathAnimTime = 1.20s`）

- **核心工程巧思（兼顾动画连贯性与低成本）**：  
  如前次调研结论，强行用站姿 12 分件拗出完全侧趴透视会严重穿帮且耗时极高；利用我们已经画好的高质量倒地立绘 `wizard_die.png`，采用 **「骨骼受创跪沉 + 淡入切换倒地态 + 落地微震」** 两段式演出：
  - **`0.00s → 0.40s`（骨骼阶段：受创踉跄下沉）**：
    - `0.00s → 0.12s`：剧烈后仰受创（类似加大版 `hurt`），帽檐掀翻、双臂脱力垂落、木杖向右倾斜滑脱；
    - `0.12s → 0.40s`：`BonePelvis` 整体快速向地面下沉（`y +85px`）并向左前倾倒（`rot -32°`），`Parts.modulate.a` 在 `0.28s → 0.42s` 快速交叉淡出。
  - **`0.30s → 0.55s`（衔接阶段：倒地落地）**：
    - `DeathSprite`（`wizard_die.png`）在 `0.28s` 开启显示并从 `y -18px, rot -6°, alpha 0` 落向 `y = 0, rot = 0°, alpha 1`；
    - `0.45s → 0.58s` 落地瞬间做一次轻微压扁回弹（`scale (1.03, 0.96) → (1.0, 1.0)`）增强触地重量感！
  - **`0.58s → 1.10s`（静止定格）**：完全静止在倒地姿态并停在末帧（`loop_mode = 0`）。
- **额外收益**：一旦 `die` 动画落入 `wizard_character.tscn`，战斗死亡、火堆/涅奥放弃游戏（`NGameOverScreen` 实例化 `wizard_character.tscn` 后播 `die`）将全部自动获得完整倒地演出！

### 3.6 `relaxed` — 放松待机（总长 `3.20s`，循环）

- **动作意象**：呼吸周期从 `2.8s` 放缓到 `3.2s`，胸腔起伏更轻柔，头略微低垂放松，木杖更安稳地斜倚，红宝石轻缓晃动。
- **复用价值**：可直接作为 A 版商店场景 `wizard_character_merchant.tscn` 的默认循环动画，以及假商人事件的 `"Relaxed"` 动画。

---

## 四、技术接线与防坑设计（Godot 4.5 + BaseLib + 卡牌池）

### 4.1 播放器架构：`AnimationPlayer` + 轻量状态控制器 vs `AnimationTree`

对比 BaseLib `CustomAnimation.cs` 源码（`L115-L162`），存在两个极关键的工程细节：

1. **纯 `AnimationPlayer` 的局限**：播放一次性动画（如 `attack`）结束后**不会自动回到 `idle`**，会停在 `attack` 最后一帧；且 `AnimationPlayer.Play()` 默认硬切（0s 混合），除非代码调用 `SetBlendTime` 或改用 `AnimationTree`。
2. **BaseLib `UseAnimationTree` 的隐蔽坑**：
   - `CustomAnimation.cs:133` 调用的是 `stateMachine.Travel(name, resetOnTeleport: true)`；
   - 在 Godot 4 中，当玩家**连续快速打出两张攻击牌**（当前已处于 `attack` 状态）时，`Travel("attack", true)` **不会从头重播当前状态**，必须显式用 `Start("attack", true)` 或 `AnimationPlayer.Stop() + Play()`；
   - 此外 `AnimationTree` 的 `AnimationNodeStateMachine` 在纯文本 `.tscn` 里定义大量节点与过渡较冗长。

**推荐落地方案（最稳、最简、可 100% 精确控制过渡与连击重播）**：
- 场景内保留简洁清晰的 **`AnimationPlayer`**（包含 `RESET`、`idle`、`attack`、`cast`、`powerup`、`hurt`、`die`、`relaxed` 全部轨道，易于 Godot 编辑器预览与脚本生成）；
- 在 C# 侧加一个极简、专门服务于巫师场景的 **~60 行辅助模块 `WizardAnimController`**（通过 Harmony 监听或挂在 `NCreature` / `AnimationPlayer` 回调上，甚至不改场景根脚本 `NCreatureVisuals.cs`）：
  1. **初始化时一次性注册混合时间（`SetBlendTime`）**，100% 复刻原版 STS2 手感：
     - `idle ↔ attack / cast / powerup`：`0.05s ~ 0.08s`
     - `idle → hurt`：`0.02s`，`hurt → idle`：`0.10s`
     - `任意 → die`：`0.03s`
     - `同动作连击`（如 `attack → attack`、`hurt → hurt`）：立即 `Seek(0, true)` 从头重播，绝不吞动作！
  2. **自动回位**：订阅 `AnimationPlayer.AnimationFinished` —— 凡是 `attack` / `cast` / `powerup` / `hurt` 播完，自动平滑切回 `idle`；唯独 `die` 播完保持末帧定格，且进入 `die` 后屏蔽后续 `idle`/`hurt` 覆盖（除非触发 `Revive`）。
  3. **完全兼容 BaseLib 命名**：统一映射触发器：
     - `"Idle"` → `idle`
     - `"Attack"` → `attack`
     - `"Cast"` → `cast`
     - `"PowerUp"` → `powerup`
     - `"Hit"` / `"Hurt"` → `hurt`
     - `"Dead"` / `"Die"` → `die`
     - `"Relaxed"` → `relaxed`
  4. **非战斗战败自动接入**：把 `WizardGameOverDeathSpritePatch` 更新为优先对骨骼场景播放 `die`（A 版播完整倒地动画，B 版静态回退场景仍走原换图逻辑，互不干扰）。

### 4.2 巫师 82 张卡牌的动作触发映射策略

当前卡池排查结果：
- **21 张攻击牌**（19 张法术攻击 + 2 张物理攻击）：均走 `DamageCmd.Attack`，引擎已自动触发 `"Attack"`；
- **36 张技能牌**：仅 2 张显式调了 `TriggerAnim`；
- **19 张能力牌**：仅 1 张（`GreatLibrary`）显式调了 `TriggerAnim`。

**两种接线方式对比**：
- **方式 A（推荐，遵循原版规范 + 零漏网）**：
  - 在卡牌分类上做精细映射，并给高费/终结法术显式指定动作：
    - **物理攻击 & 常规法术攻击**（如打击、火球术、电击、冰锥、以太箭等）：走默认 `"Attack"`（`0.15s`，打击干脆利落）；
    - **终结型重型大法术**（如天火浩荡 `SkyfireRampage`、降雷 `LightningFall`、清界 `ClearWorld`、燃尽灰烬 `BurnToAshes`、落日 `Sunset` 等）：用 `.WithAttackerAnim("PowerUp", Owner.Character.PowerUpAnimDelay)` 或在施放前触发 `"PowerUp"`，演出气势直接与小法术拉开层次！
    - **全部 19 张能力牌**：统一在 `OnPlay` 开头加 `await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);`（与原版所有能力牌写法 100% 一致）；
    - **施法类技能牌**（除纯防御 `DefendWizard` 外，或视手感决定 `DefendWizard` 是否也播轻微 `Cast`）：统一加 `await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);`。
- **方式 B（集中兜底钩子）**：也可在出牌基类/补丁里对「未手动发过动画的巫师技能/能力牌」按 `CardType` 自动补发 `Cast` / `PowerUp`，但注意 `await` 延迟时机应在出牌效果之前（而不是之后），因此**批量规范化写入各卡 `OnPlay` 首行**（配合小脚本核查全覆盖）效果最精准、与原版最一致。

> **关于「防御牌是否播 `cast`」**：原版 `DefendIronclad` 等基础防御不播 `Cast`（只弹盾牌特效，保证出防御零延迟）；但巫师的护盾牌（如镇静、水幕、冰甲等法术防御）播 `Cast` 会很有法师感。建议：**纯基础 `DefendWizard` 保持原版零延迟（不播或极短 `0.1s`），其余功能性技能牌统一播 `Cast`**。

### 4.3 角色音效（SFX）顺手提醒

目前 `WizardCharacter` 继承 `PlaceholderCharacterModel`（默认占位 `"ironclad"`），一旦给所有能力/技能牌补齐 `TriggerAnim("Cast"/"PowerUp")`，游戏会高频播放 `ironclad_cast`（铁甲战士的战吼/重声）。后续可按需在 `WizardCharacter` 中将 `CustomCastSfx` / `CustomAttackSfx` 替换为更贴合法师的音效（原版其他角色或自定义音频均可）。

---

## 五、分阶段施工顺序建议

确认本设计方案后，建议按以下 4 步稳步实施，每步都跑离屏渲染帧图质检 + 游戏实测：

1. **第 1 步（动作基础设施）**：
   - 在 `WizardCharacter` 设置 `PowerUpAnimDelay = 0.35f`、`DeathAnimTime = 1.20f`；
   - 实现 `WizardAnimController`（混合时间 `SetBlendTime`、一次性动作播完自动回 `idle`、连击重播、死亡锁定）并接通 `WizardGameOverDeathSpritePatch`。
2. **第 2 步（制作 P0 四大核心动作）**：
   - 在 `rig_wizard_a_scene.py` 中基于同一套 13 根骨骼依次实装 `attack`、`cast`、`hurt`、`die`；
   - 离屏渲染每个动作的 4~5 个关键帧逐帧质检（核查关节不露缝、遮挡自然、倒地衔接顺滑）。
3. **第 3 步（制作 P1 招牌动作 + 全卡池动作接线）**：
   - 实装 `powerup`（禁忌大施法，附带红宝石微光脉冲）与 `relaxed`（顺带可接入 A 版商店场景）；
   - 给巫师 19 张能力牌、技能牌及终结大法术补齐 `TriggerAnim` / `WithAttackerAnim` 触发。
4. **第 4 步（向其他场景 & B 版扩展）**：
   - 将这套经过实战检验的动作契约与脚本管线平滑复制到 **A 版商店/火堆** 以及 **B 版伊蕾娜**。
