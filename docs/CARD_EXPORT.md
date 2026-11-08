# 卡牌整图导出 · 技术方案

> 目标：把巫师的全部卡牌导出成**游戏里那样的完整卡面**——卡框 + 卡图 + 格式化描述 + 费用 + 标题 + 类型条。
> 用途：**Tier 表**与 **wiki**。升级前优先，升级后尽量也要。

---

## §0 结论

**✅ 完全可行，而且不需要重做任何 UI。**

游戏本身就有一个 `NCard` 组件负责"一张完整的卡"。我们的活儿只是：

> **造一张卡 → 塞进离屏视口 → 等它排版画完 → 把视口存成 PNG → 归还卡池。**

不需要复刻卡框、不需要复刻排版、不需要复刻关键字高亮——这些都由 `NCard` 自己完成。

**★ 推荐落点：在 mod 里加一个控制台命令**，在真实运行的游戏里批量导出。
理由见 §2：**游戏会主动扫描 mod 程序集里的控制台命令并注册**，而且**只要加载了 mod 就开放 Debug 命令**。

---

## §1 已验证的调用链（全部带出处）

| # | 事实 | 出处 |
|---|---|---|
| 1 | `NCard : Control, IPoolable`；`defaultSize = (300, 422)`；`GetCurrentSize() => defaultSize * Scale` | `NCard.cs:36` / `:502` / `:828` |
| 2 | **`NCard.Create(CardModel, ModelVisibility = Visible)`** → 从池里取一张并 `Model = card` | `NCard.cs:772` |
| 3 | `Model` 的 setter **内部会调 `Reload()`** → 整套视觉自动重建 | `NCard.cs:642-660` |
| 4 | ⚠️ `NCard.Create` 在 **`TestMode.IsOn` 时返回 `null`** | `NCard.cs:774` |
| 5 | `NodePool.Free<T>(T)` 归还，**但不会把节点从场景树里摘掉** | `NodePool.cs:36/51`、`Free` 主体 |
| 6 | ⚠️ `NodePool.Init` **重复调用会抛异常**（"already initialized"） | `NodePool.cs:15-24` |
| 7 | **`CardPoolModel.AllCards` 已经包含 mod 的卡**：`ModHelper.ConcatModelsFromMods(this, _allCards)` | `CardPoolModel.cs:44-53` |
| 8 | `CardModel.ToMutable()` 拿可变克隆；`CardCmd.Upgrade(card, style)` 是**同步 void** | `CardModel.cs:1191`、`CardCmd.cs:256` |
| 9 | **`NCard.ShowUpgradePreview()`** → `UpdateVisuals(..., CardPreviewMode.Upgrade)` → 用 `GetDescriptionForUpgradePreview()` | `NCard.cs:1354`、`:861-886` |
| 10 | ★★ **控制台会注册 mod 里的命令**：`ReflectionHelper.GetSubtypesInMods<AbstractConsoleCmd>()` | `DevConsole.cs:33` |
| 11 | ★★ **只要运行了 mod 就开放 Debug 命令**：`OS.HasFeature("editor") \|\| TestMode.IsOn \|\| ModManager.IsRunningModded() \|\| SettingsSave.FullConsole` | `NDevConsole.cs:359` |
| 12 | 游戏里已有离屏渲染的先例：`NMapShareButton` 用 `SubViewport` 出地图分享图；`MonsterModel.cs:63` 死亡时用 `SubViewport` 快照 | — |
| 13 | 已有"抓视口存图"的先例：`GetViewport().GetTexture().GetImage()` + `image.SavePngToBuffer()` | `GetLogsConsoleCmd.cs:93-100` |

---

## §2 方案对比

| 方案 | 做法 | 评价 |
|---|---|---|
| **A · 控制台命令 + 离屏渲染** | mod 里加 `wizardcards` 命令，内部用 `NCard` + `SubViewport` 批量出图 | ✅ **推荐**。像素级还原、能出升级版、可重复、可加参数 |
| **B · 手动截卡牌图书馆** | 进游戏开卡牌图书馆，逐屏截图裁切 | ❌ 65×2 张；卡在网格里是缩小态、背后有 UI 底、还要人工裁 |
| **C · Python/Pillow 复刻** | 自己画卡框、排文本、做关键字高亮 | ❌ 卡框是九宫格贴图、文本有 BBCode + 关键字着色 + 动态数值，复刻等于重写一遍 UI，且必然不准 |
| **D · 复用现有截图能力** | `NFeedbackScreenOpener` / `getlogs` 那套 | ⚠️ 只能截**当前画面**，不能逐张换卡 |

---

## §3 方案 A 的实现草图

```csharp
// ── 一个控制台命令 ────────────────────────────────────────
public sealed class WizardCardDumpConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "wizardcards";
    public override string Args    => "[upgraded|all] [scale:1|2|3]";
    public override string Description => "把巫师全部卡牌导出成整图 PNG（含卡框/描述/费用）。";
    public override bool IsNetworked => false;
    public override bool DebugOnly   => false;

    public override CmdResult Process(Player? p, string[] args)
    {
        // ⚠️ Process 是同步的，逐张等待帧不能用 await 直接写在这里
        //    → 起一个异步任务，立刻返回"已开始"
        WizardCardDumper.RunAsync(args);      // async void / Task，内部自己管生命周期
        return new CmdResult(true, "开始导出…（完成后看日志与输出目录）");
    }
}
```

```csharp
// ── 导出主循环（伪代码）─────────────────────────────────────
var viewport = new SubViewport {
    TransparentBg = true,                        // ★ 出带 alpha 的 PNG，便于贴到任何底色
    RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
};
root.AddChild(viewport);                          // 必须在树里才会渲染

var pool = ModelDb.CardPool<WizardCardPool>();

foreach (var canonical in pool.AllCards)          // ← 已含 mod 卡（§1 #7）
{
    // ① 升级前
    await DumpOne(canonical, viewport, upgraded: false);

    // ② 升级后（真升级，费用也会变）
    if (canonical.MaxUpgradeLevel > 0)
    {
        var m = canonical.ToMutable();            // ★ 不动原型（§1 #8）
        CardCmd.Upgrade(m, CardPreviewStyle.None);
        await DumpOne(m, viewport, upgraded: true);
    }
}

// ── 单张 ──────────────────────────────────────────────────
async Task DumpOne(CardModel model, SubViewport vp, bool upgraded)
{
    var card = NCard.Create(model, ModelVisibility.Visible);   // 池里取
    if (card == null) return;                                  // TestMode 下为 null

    if (card.GetParent() is { } p) p.RemoveChild(card);         // ★ 池里的节点可能还挂着
    vp.AddChild(card);

    card.Scale = Vector2.One * S;                              // S = 1 / 2 / 3
    vp.Size = new Vector2I((int)(300 * S), (int)(422 * S));

    // 等排版 + 等绘制
    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    await ToSignal(Engine.GetMainLoop(), "process_frame");      // 可能还不够，见 §5 风险 1

    var img = vp.GetTexture().GetImage();                       // §1 #13 同款
    img.SavePng($"user://card_exports/{pool}/{name}{(upgraded ? "_up" : "")}.png");

    vp.RemoveChild(card);                                       // ★ 必须先摘（§1 #5）
    NodePool.Free(card);                                        // 归还
}
```

**建议的输出结构**

```
user://card_exports/
└── wizard/
    ├── 001_ColdWave.png
    ├── 001_ColdWave_up.png
    ├── 002_...
```

- 文件名用**类名**（`ColdWave`），和 `images/cards/*.png` 一一对应，便于脚本配对
- 前缀序号按"费用 → 类型 → 名称"排序，方便直接拼 Tier 表

---

## §4 ★ 升级版的两种做法，差别很大

| 做法 | 效果 | 费用变不变 | 风险 |
|---|---|---|---|
| **`card.ShowUpgradePreview()`** | 只把**描述文本**换成升级后的（`GetDescriptionForUpgradePreview`） | ❌ **不变** | 无（不动模型） |
| **`ToMutable()` + `CardCmd.Upgrade()`** | **真升级**：描述、费用、词条全变 | ✅ 变 | 需确认 `CardCmd.Upgrade` 在非战斗环境安全 |

> **建议：优先用第二种**（wiki 需要"升级后到底几费"）。第一种只作为对照/兜底。
>
> ⚠️ **绝对不要对 `AllCards` 里的原型直接升级**——那是全局单例，会把整个 mod 的卡都变成升级版。

---

## §5 风险 / 必须先验证的点

| # | 风险 | 应对 |
|---|---|---|
| 1 | **文本要几帧才排好**（`MegaRichTextLabel`） | 先试 1 帧，不对就加到 2–3 帧；或等 `frame_post_draw` **两次** |
| 2 | **1× 只有 300×422**，对 wiki 偏小；放大到 2×/3× 时**九宫格卡框可能糊** | ★ 打小样对比 1×/2×/3× 再定档 |
| 3 | **卡池的回收顺序**：`Free` 不摘树 | 先 `RemoveChild` 再 `NodePool.Free` |
| 4 | `NodePool.Init` 重复调用会抛异常 | ★ **不要调 `NCard.InitPool()`**，直接用 `Create`（正常启动时池已初始化） |
| 5 | `TestMode.IsOn` 时 `Create` 返回 null | 正常游玩不受影响 |
| 6 | **"手牌态"与"图书馆态"的视觉可能不同**（费用颜色、`DisplayingPile`） | 如需固定的图书馆外观，可能要额外设 `DisplayingPile`；先出样看 |
| 7 | 字体 / BBCode 依赖游戏 PCK | ✅ 运行时自然可用（这点是"必须在真游戏里跑"的核心理由） |
| 8 | 65 张 × 2 = 130 次渲染 | 每张几帧，合计几秒；可加进度日志 |

---

## §6 ★ 建议的第一步：只打一张小样

**只导 `ColdWave`（大寒波）一张，1×，不透明底。**

验收清单：

1. 卡框完整、九宫格没缺口
2. 卡图 `ColdWave.png` 正确贴进去
3. **标题、类型条、费用数字**都在
4. **描述文本完整且没有缺字/乱码**
5. 边界没有多余背景（视口尺寸正好等于卡面）

这张对了，剩下就是把参数变成 130 张循环。

---

## §7 待拍板

| # | 事项 | 建议 |
|---|---|---|
| 1 | 输出倍率 | 先 1× 打样，再定 2×（600×844）或 3×（900×1266） |
| 2 | 透明底 | **要**（`TransparentBg = true`），贴任何底色都不露白 |
| 3 | 升级版 | **要**，用"真升级"（`ToMutable` + `CardCmd.Upgrade`） |
| 4 | 文件名 | **类名**（`ColdWave` / `ColdWave_up`） |
| 5 | 落盘位置 | `user://card_exports/` → `%APPDATA%\SlayTheSpire2\`，导出后拷走 |
| 6 | 命令名 | `wizardcards` |

---

## §8 ★★ 实测踩到的两个坑（2026-11-05，第一版跑出来的问题）

第一版导出后，**每张图都只截到右下角，描述位置写着英文「If you can read this, there is a bug.」**。
两个原因都不显然，记在这里：

### 坑 1 · 必须先入树、再绑 Model

`NCard.Model` 的 setter 会调 `Reload()`，而 **`Reload()` 和 `UpdateVisuals()` 的第一行都是**：

```csharp
if (!IsNodeReady()) return;
```

用 `NCard.Create(model)` 时，Model 是**入树之前**绑的 → `Reload()` **静默空转** →
标题、卡图、费用、描述**全部没画**，留在场景里的默认值上。

> **「If you can read this, there is a bug.」** 就是 `card.tscn` 里 `DescriptionLabel` 的**默认 text**。

**正确顺序**：

```csharp
var node = NodePool.Get<NCard>();   // 拿空模型节点
viewport.AddChild(node);            // 先入树 → _Ready 跑完 → IsNodeReady() 为真
node.Visibility = ModelVisibility.Visible;
node.Model = model;                 // 再绑 → Reload() 这次真的生效
```

（`_Ready()` 只做 `GetNode` 缓存、不解引用 `Model`，所以空模型入树是安全的。）

### 坑 2 · 卡面以节点原点为中心，不是左上角

`card.tscn` 的几何是**居中**的：

| 节点 | 矩形（相对卡节点原点） | 说明 |
|---|---|---|
| `Frame` | `x -150..150, y -211..211` | 300×422，**居中** |
| `Portrait` | `x -125..125, y -168..22` | 250×190 |
| `EnergyIcon` | `x -166..-102, y -227..-163` | **伸出边框左上 16px** |
| `StarIcon` | `x -186..-128` | **伸出左边 36px** |
| `TitleBanner` | `x -163..164, y -207..-124` | 比卡框宽 |
| `Shadow` | `x -138..162, y -199..223` | 伸出右下 12px |
| `Highlight` | `759 × 951` | 悬浮发光，**出图要手动隐藏** |

所以把卡放在 `Position = 0` 只会截到**右下四分之一**。

**做法**：视口放大到 **384 × 480**，卡节点放**视口中心** `(192, 240)`。
这样卡框恒定居中于 `(192, 240)`，且费用球 / 星标 / 标题横幅都完整收进来，**每张图尺寸统一**。

### 附 · 顺带

- `Highlight`（759×951 的悬浮发光）要在出图前 `Visible = false`，否则边缘会带一圈光
- 抓图前等 **3 帧**（`process_frame` 在绘制之前触发，第三帧才保证已完整画过一次）
- 第一版 65 张里只出了 **62** 张 —— 3 张失败，等第二版跑完再看日志里的失败名单

---

## §9 第二轮：`Reload()` 不负责文字，必须显式调 `UpdateVisuals`

第二版卡框、卡图、类型条都对了，但：

- 标题显示 **「Broken Card」**
- 描述显示 **「If you can read this, there is a bug.」**
- 费用恒为 **0**
- 左上角多出一枚 **蓝色星标**

四个症状，**一个根因**：

> 那两句英文分别是 `card.tscn` 里 `TitleLabel` 和 `DescriptionLabel` 的**默认文本**。
> 说明这两处从来没被赋值。

而 `Reload()` 只管四件事：

```csharp
_energyIcon.Texture = Model.EnergyIcon;   // 能量图标贴图
UpdateTypePlaque();                        // 类型条
... 按稀有度切换远古卡外观 ...
... 按 Visibility 上模糊材质 ...
```

**它一次都没有碰文字、费用数字和星标显隐。** 那些全在 `UpdateVisuals(pileType, previewMode)` 里：

| 症状 | 由 `UpdateVisuals` 里的谁修 |
|---|---|
| 标题 | `UpdateTitleLabel()` |
| 描述正文 | `Model.GetDescriptionForPile(...)` |
| 费用 0 | `UpdateEnergyCostVisuals(pileType)` |
| 蓝色星标 | `UpdateStarCostVisuals(pileType)` → `Visible = GetStarCostWithModifiers() >= 0`（巫师卡是 −1，自动隐藏） |
| 卡图 | `UpdatePortrait()` |

平时这张卡被塞进手牌/牌堆/展示槽时，游戏会替我们调 `UpdateVisuals`；**我们手工造卡，不会有任何人替我们调**。

**修法**：绑完 Model 之后再补一次显式调用。

```csharp
node.Visibility = ModelVisibility.Visible;
node.Model = model;
node.UpdateVisuals(PileType.None, CardPreviewMode.Normal);   // ★ 必须
```

> 用 `PileType.None` 是有意的：`Hand` 分支会额外做"能不能打出"的判断并给费用上色，`None` 得到的是**中性的卡库外观**。

## §10 导出语言（zhs）

`LocManager` 是公开单例：

```csharp
LocManager.Instance.Language                 // 当前语言，如 "zho"… 实际是 "zhs"
LocManager.Instance.SetLanguage("zhs")       // 换语言（和设置界面同一套）
LocManager.Languages                         // 支持列表：eng / zhs / zht / jpn / kor …
```

导出前后包一层 `try/finally`：**先切到 `zhs`，导完在 finally 里切回原语言**，避免影响玩家设置。
切换后要等 **2 帧**让语言变更回调跑完再开始导。

> 也可以不改代码——**把游戏语言设成中文再导出**，效果一样。


