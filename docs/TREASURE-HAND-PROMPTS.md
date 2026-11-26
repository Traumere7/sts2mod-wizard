# 巫师宝箱房手臂（Multiplayer Hands）生图提示词指南

> 对应原版游戏 `images/ui/hands/multiplayer_hand_<char>_{point,rock,paper,scissors}.png`，用于单人/多人宝箱房选遗物伸手指向，以及多人抢遗物时的猜拳（石头/布/剪刀）演出。

---

## 一、素材清单与最终规格（共 4 张，逐张生成）

| 序号 | 动作 | 游戏内用途 | 目标文件名（交付后放至 `src/Wizard/images/ui/hands/`） | 最终尺寸 | 手指顶边大致位置 |
|---|---|---|---|---|---|
| 1 | **单指指物（Point）** | 宝箱房指向/拾取遗物（**最常用，先生成这张作为其余 3 张的图生图参考！**） | `multiplayer_hand_wizard_point.png` | `422 × 1200` RGBA | 顶部留白约 `18px` |
| 2 | **握拳（Rock）** | 多人猜拳「石头」 | `multiplayer_hand_wizard_rock.png` | `422 × 1200` RGBA | 顶部留白约 `135px`（拳头比伸指矮一截） |
| 3 | **张掌（Paper）** | 多人猜拳「布」 | `multiplayer_hand_wizard_paper.png` | `422 × 1200` RGBA | 顶部留白约 `14px` |
| 4 | **剪刀手（Scissors）** | 多人猜拳「剪刀」 | `multiplayer_hand_wizard_scissors.png` | `422 × 1200` RGBA | 顶部留白约 `18px` |

### 统一技术约束
- **画幅比例**：尽量选择最接近 `1:3` 的竖长画幅（例如 **9:16** 或 **2:5** / **1:2.8**；如果模型不支持 `1:3`，用 **9:16** 生成并要求手臂居中竖直贯穿画面即可，后续 PS 或脚本可精确等比贴入 `422 × 1200` 画布）。
- **背景**：**纯白 `#FFFFFF`**，无阴影、无光效、无文字、无边框（方便 PS 魔棒/主体抠图或脚本一键去白边）。
- **视角与朝向**：**手背朝向观众、垂直正上方伸出**（参考原版铁甲战士/死灵契约师手部资源：`D:\Godot\Slay the Spire 2\images\ui\hands\multiplayer_hand_ironclad_point.png` 与 `multiplayer_hand_necrobinder_point.png`）。
- **底部渐隐处理**：生成时让紫袍袖子直直向下延伸即可，**无需在生图阶段强求透明渐隐**——PS 抠图后在 `422 × 1200` 画布底部 `y = 956..1105` 加一条向下的透明羽化蒙版即可与原版丝滑一致（也可以直接给我白底原图，我用脚本批量抠图 + 对齐 + 羽化）。
- **强推荐工作流（保证 4 张袖子/绷带完全一致）**：
  1. **先生成第 1 张 `Point`**：垫图（参考图）同时上传 `src/Wizard/images/characters/wizard_battle.png`（取服装/肤色/绷带/画风）和原版 `multiplayer_hand_ironclad_point.png`（取竖直构图/手背视角/比例）。
  2. **再逐张生成第 2、3、4 张（`Rock` / `Paper` / `Scissors`）**：把**刚定稿的第 1 张 `Point` 作为核心参考图**（图生图/局部编辑），要求“手腕以下绷带与紫色星空法袍袖 100% 保持不动，仅改变顶部手势”，这样 4 张图切换时手腕和袖口严丝合缝！

---

## 二、视觉风格与巫师外观设定（全 4 张共用）

- **画风**：《杀戮尖塔 2 / Slay the Spire 2》暗黑奇幻手绘厚涂 + 清晰黑色漫画勾线（cel-shaded dark fantasy comic ink outlines），略带粗粝笔触与纸张质感，**不要写实 3D、不要二次元光滑赛璐璐**。
- **三段式结构（自下而上）**：
  1. **下段（底部约 55% 高度）— 紫色星空法袍袖**：深皇家紫（`#4B2485` ~ `#2C1354`）宽大布料长袖，带微弱深蓝紫纵纹高光与零星细小白色/淡紫星尘微点、自然布褶与深色暗影，与 `wizard_battle.png` 的星空法袍完全一致；
  2. **中段（手腕与前臂上段，约 20% 高度）— 泛黄旧布绷带**：略显陈旧的米灰/羊皮纸色布条（`#D2C6B4` ~ `#9A8D7D`）一圈圈紧缠在手腕及小臂前段，有清晰黑线勾勒缠绕缝隙与阴影，与 `wizard_battle.png` 手腕绷带一致；
  3. **上段（手掌，约 25% 高度）— 沧桑男性巫师手（手背视角）**：偏苍白暖米肤色（`#C9A992`）、指节分明、带细微关节折痕与暗影，无手套、无戒指发光特效。

---

## 三、4 张逐张生图提示词（可直接复制）

> **说明**：每张同时提供 **英文主 Prompt**（多数主流生图模型对英文结构指令遵循最稳）与 **中文 Prompt**（适配中文生图模型），二选一或按你所用工具粘贴即可。

---

### 第 1 张：单指指物（`Point`）— 优先生成！

- **建议参考图**：`wizard_battle.png`（角色风格/紫袍/绷带） + `multiplayer_hand_ironclad_point.png`（竖直手背构图比例）

**英文 Prompt：**
```text
2D dark-fantasy game UI illustration in the exact art style of Slay the Spire 2, painterly cel-shading with crisp dark ink outlines, isolated on a pure flat solid white background (#FFFFFF), no drop shadow, no glow, no particles, no text.

A single vertical human male wizard's right arm and hand reaching straight upward from the bottom center of the frame, viewed strictly from the back of the hand (dorsal view), matching the tall slender proportions of a vertical arm icon.

- Top (hand): Weathered pale-tan human male hand viewed from the back, with the index finger pointing straight vertically upward and the thumb, middle, ring, and pinky fingers curled naturally into a closed fist. Crisp comic ink lines on knuckles and finger creases.
- Middle (wrist & upper forearm): Worn off-white parchment-colored cloth bandages wrapped snugly around the wrist and upper forearm, with inked overlap lines and soft shadows.
- Bottom (lower forearm & sleeve, bottom 55% of arm): Deep royal purple wizard robe sleeve with subtle dark cosmic blue vertical highlight, natural cloth folds, and tiny scattered white star specks (matching the wizard's starry purple robe), extending straight downward to the bottom center.

Centered vertically with generous pure white #FFFFFF margin on left, right, and top edges. Nothing clipped at the fingertip or sides.
```

**中文 Prompt：**
```text
《杀戮尖塔2》暗黑奇幻手绘游戏 UI 素材风格，厚涂赛璐璐上色配清晰黑色漫画勾线，纯平纯白背景（#FFFFFF），无阴影、无发光特效、无粒子、无文字。

画面正中绘制一条从底部垂直正上方伸出的男性巫师手臂，严格为【手背朝向观众】的正面俯视视角，整体修长竖直：
- 顶部（手掌，占高度约 25%）：略显沧桑的偏苍白暖肤色男性手背，【食指笔直竖直向上指】，拇指、中指、无名指与小指自然蜷握成拳，指节与指纹折痕用清晰黑线勾勒。
- 中段（手腕与前臂上段，占高度约 20%）：泛黄做旧的羊皮纸米白色布条绷带紧密缠绕手腕与小臂前段，带清晰的缠绕折线与暗影。
- 下段（小臂中下段，占高度约 55%）：深皇家紫色星空法袍长袖，带柔和深蓝紫纵向亮面、自然布料褶皱与零星细小白色星点，笔直向下延伸。

手臂居中竖直摆放，左右与指尖上方留足纯白边距，绝不裁切手指。
```

---

### 第 2 张：握拳（`Rock` — 石头）

- **建议参考图**：**把第 1 张定稿的 `Point` 图作为核心参考图**（可附原版 `multiplayer_hand_ironclad_rock.png` 参考拳头轮廓）

**英文 Prompt：**
```text
Edit the reference wizard arm image to change ONLY the hand gesture at the top while keeping everything below the wrist 100% identical.

Keep the exact same art style (Slay the Spire 2 dark-fantasy cel-shaded comic ink), exact same pure flat solid white background (#FFFFFF), exact same centered vertical placement, exact same deep royal purple starry robe sleeve, and exact same wrapped parchment cloth bandages on the wrist and forearm.

- Top (hand): Change the pointing hand into a tightly clenched closed fist viewed from the back of the hand (rock gesture in rock-paper-scissors). All four fingers and thumb are curled firmly into a compact fist with knuckles facing upward and clear dark ink linework. Because no finger is extended, leave slightly more pure white space above the knuckles than in the pointing pose.

No glow, no sparks, no drop shadow, no text, pure solid #FFFFFF white background.
```

**中文 Prompt：**
```text
基于参考图中的巫师手臂，【手腕以下的泛黄绷带、深紫色星空法袍袖、粗细比例、画风与居中位置 100% 保持不变】，仅修改顶部的手势：

- 将原本竖直食指改为【紧握的拳头（猜拳“石头”手势）】，严格保持手背朝向观众，四指与拇指紧凑收拢握拳，指关节朝上，漫画黑线清晰刻画指节起伏；因为没有伸出手指，拳头顶部上方比指物版多留出一些纯白空间。

纯平纯白背景（#FFFFFF），无阴影、无特效、无文字。
```

---

### 第 3 张：五指张开（`Paper` — 布）

- **建议参考图**：**把第 1 张定稿的 `Point` 图作为核心参考图**（可附原版 `multiplayer_hand_ironclad_paper.png` 参考张掌轮廓）

**英文 Prompt：**
```text
Edit the reference wizard arm image to change ONLY the hand gesture at the top while keeping everything below the wrist 100% identical.

Keep the exact same art style (Slay the Spire 2 dark-fantasy cel-shaded comic ink), exact same pure flat solid white background (#FFFFFF), exact same centered vertical placement, exact same deep royal purple starry robe sleeve, and exact same wrapped parchment cloth bandages on the wrist and forearm.

- Top (hand): Change the pointing hand into a fully open flat hand with all five fingers stretched straight upward and slightly spread apart (paper gesture in rock-paper-scissors), viewed strictly from the back of the hand (dorsal view, thumb on the left side). Show fingernails and subtle knuckle creases with crisp dark ink outlines. Ensure generous pure white margin around the thumb and all fingertips with zero clipping.

No glow, no sparks, no drop shadow, no text, pure solid #FFFFFF white background.
```

**中文 Prompt：**
```text
基于参考图中的巫师手臂，【手腕以下的泛黄绷带、深紫色星空法袍袖、粗细比例、画风与居中位置 100% 保持不变】，仅修改顶部的手势：

- 将原本单指手势改为【五指完全伸直向上并微微张开的平掌（猜拳“布”手势）】，严格保持手背朝向观众（大拇指位于左侧），清晰画出指甲、指节与手背骨节的漫画黑线细节。拇指外侧与所有指尖四周均留足纯白边距，绝不裁切。

纯平纯白背景（#FFFFFF），无阴影、无特效、无文字。
```

---

### 第 4 张：剪刀手（`Scissors` — 剪刀）

- **建议参考图**：**把第 1 张定稿的 `Point` 图作为核心参考图**（可附原版 `multiplayer_hand_ironclad_scissors.png` 参考剪刀手轮廓）

**英文 Prompt：**
```text
Edit the reference wizard arm image to change ONLY the hand gesture at the top while keeping everything below the wrist 100% identical.

Keep the exact same art style (Slay the Spire 2 dark-fantasy cel-shaded comic ink), exact same pure flat solid white background (#FFFFFF), exact same centered vertical placement, exact same deep royal purple starry robe sleeve, and exact same wrapped parchment cloth bandages on the wrist and forearm.

- Top (hand): Change the pointing hand into a V-shaped scissors gesture (peace sign in rock-paper-scissors) viewed strictly from the back of the hand (dorsal view): index finger and middle finger extended straight upward in a clear V shape, while the thumb, ring finger, and pinky finger are curled neatly into the fist. Crisp comic ink outlines on nails and knuckles. Generous pure white margin around both fingertips with zero clipping.

No glow, no sparks, no drop shadow, no text, pure solid #FFFFFF white background.
```

**中文 Prompt：**
```text
基于参考图中的巫师手臂，【手腕以下的泛黄绷带、深紫色星空法袍袖、粗细比例、画风与居中位置 100% 保持不变】，仅修改顶部的手势：

- 将原本单指手势改为【食指与中指向上伸直张成 V 字的剪刀手（猜拳“剪刀”手势）】，严格保持手背朝向观众，大拇指、无名指与小指自然蜷握在掌中，清晰勾勒指甲与指节黑线。两根指尖上方留足纯白边距，绝不裁切。

纯平纯白背景（#FFFFFF），无阴影、无特效、无文字。
```

---

## 四、生成完成后的接入方式

你生成好这 4 张图（无论是白底 JPG/PNG 还是你用 PS 抠好透明底的 PNG）后，随便丢进工作区任意目录告诉我路径，我来自动完成收尾：
1. 统一去白边 / 对齐到原版精确画布 `422 × 1200` RGBA，并按原版参数在底部 `y = 956..1105` 加上柔和透明渐隐；
2. 存入 `src/Wizard/images/ui/hands/multiplayer_hand_wizard_{point,rock,paper,scissors}.png`；
3. 在 `src/Scripts/Character/WizardCharacter.cs` 中覆写 `CustomArmPointingTexturePath` / `CustomArmRockTexturePath` / `CustomArmPaperTexturePath` / `CustomArmScissorsTexturePath` 并编译打包部署。
