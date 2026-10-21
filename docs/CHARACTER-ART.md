# 角色立绘：切换机制与资源清单

> 2026-10-13 调查 + 实装「死亡立绘」时整理。
> 涉及代码：`src/Scripts/Patches/WizardDeathSpritePatch.cs`、
> `src/Scripts/Character/WizardCharacter.cs`、`src/Wizard/scenes/wizard_character.tscn`。

---

## 1. 战斗立绘是怎么加载的

`wizard_character.tscn` 是**战斗中的角色场景**，结构：

```gdscript
[node name="WizardCharacter" type="Node2D"]
script = ExtResource("1_wizncv")      # res://src/Core/Nodes/Combat/NCreatureVisuals.cs
[node name="Visuals" type="Sprite2D" parent="."]
unique_name_in_owner = true            # ⇒ %Visuals
position = Vector2(42.2, -173)
scale = Vector2(0.6039, 0.6039)
texture = ExtResource("2_wizchar")     # res://Wizard/images/characters/wizard_battle.png
[node name="Bounds" type="Control" parent="."]      # 血条/意图定位，NCombatRoom 用它排站位
[node name="IntentPos" type="Marker2D" parent="."]
[node name="CenterPos" type="Marker2D" parent="."]
```

### ⚠️ 关键认知：场景里的 `script` 路径只是**类型提示**，不耦合 C# 状态

`res://src/Core/Nodes/Combat/NCreatureVisuals.cs` 指向的是**原版游戏自己的**
`MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals`
（mod 里根本没有这个文件；`reference/` 下的只是反编译副本）。

而且 `NCreatureVisuals._Ready()` 只做**实例级**节点查找
（`NCreatureVisuals.cs:251-272`）：

```csharp
public override void _Ready()
{
    _body = GetNode<Node2D>("%Visuals");
    _phobiaModeBody = GetNodeOrNull<Node2D>("%PhobiaModeVisuals");
    FormVfxHolder = GetNodeOrNull<Control>("%FormVfx");
    Bounds = GetNode<Control>("%Bounds");
    IntentPosition = GetNode<Marker2D>("%IntentPos");
    VfxSpawnPosition = GetNode<Marker2D>("%CenterPos");
    OrbPosition = HasNode("%OrbPos") ? GetNode<Marker2D>("%OrbPos") : IntentPosition;
    TalkPosition = HasNode("%TalkPos") ? GetNode<Marker2D>("%TalkPos") : null;
    if (IsSpineNode) { SpineBody = new MegaSprite(_body); ... }
    ...
}
```

**没有任何 C# 静态字段**。结论：**改/换立绘不需要动场景，也不需要写自定义
`NCreatureVisuals` 子类** —— 找到 `%Visuals` 节点改 `Texture` 就够了。

`IsSpineNode` 在我们这是 false（`%Visuals` 是普通 `Sprite2D`，不是 Spine），
所以 `SpineBody` 为 null，那条分支不走。

---

## 2. 死亡流程：唯一入口 `StartDeathAnim`

全游戏的死亡演出只有**一个**入口：

```csharp
// NCreature.cs:931
public float StartDeathAnim(bool shouldRemove)
```

- **唯一调用者**：`CreatureCmd.cs:545`
  ```csharp
  deathAnimLength = nCreature.StartDeathAnim(shouldRemoveFromCombat && creature.IsMonster);
  ```
- ⚠️ **玩家恒为 `false`**（玩家不是 monster），所以**不能**用 `shouldRemove` 判断"是不是玩家"。
- 引擎内部对玩家有专门分支（`NCreature.cs:951-955`）：
  ```csharp
  if (Entity.Player != null) { SfxCmd.PlayDeath(Entity.Player); Visuals.RemoveFormVfx(); }
  ```
  ⇒ **判断"是玩家"用 `Entity.Player != null`**，与引擎自身一致。
- 之后它启动 `AnimDie(shouldRemove, token)`（`NCreature.cs:1006`，`private async Task`）。

**为什么打 `StartDeathAnim` 而不是 `AnimDie`**：后者是 `private async Task`，
Harmony 打起来脆；而且它要先 `await` 动画结束，而换贴图要**立刻**发生。
挂在同步方法 `StartDeathAnim` 的 Postfix 上最稳。

> `StartDeathAnim` 开头有"已有死亡动画在跑就 `return 0`"的短路
> （`NCreature.cs:938-942`），但 **Postfix 照常执行** —— 对幂等的换图操作正好。

---

## 3. 立绘对齐：两张图的几何差异

`Sprite2D.centered` 默认 `true` ⇒ **贴图中心**锚在节点原点 `(0,0)`。
tscn 的 `position = (42.2, -173)` 是按 `wizard_battle.png` 调的。

| 文件 | 画布 | 内容 bbox（alpha） | 内容尺寸 | 内容中心 | 相对画布中心 |
|---|---|---|---|---|---|
| `wizard_battle.png` | 512×768 | x 74..443 / y 97..671 | 369 × 574 | (258.5, 384.0) | **(+2.5, +0.0)** |
| `wizard_die.png` | 512×768 | x 28..493 / y 326..592 | 465 × 266 | (260.5, 459.0) | **(+4.5, +75.0)** |

- **站立**：脚底在 `y=671`，内容中心≈画布中心（偏移 0）。
- **倒地**：躺姿中心比画布中心低 **75px**。

⇒ **直接换图会让巫师"沉"下去 75px**。解法：换图时把 `position.y` 加
`DeathYOffset`（当前 **75**，见补丁里的常量）。

> 用 `sprite.SetMeta("wizard_death_offset_applied", true)` 标记，防止重复调用累加偏移。
>
> ⚠️ 75 这个值来自"两侧内容中心偏移之差"。
> 若实测觉得贴地位置不对（偏高/偏低），**只改 `DeathYOffset` 这一个常量**即可。

X 方向：`+2.5` vs `+4.5`，只差 2px（世界坐标 ≈1.2），**不需要补偿**。

---

## 4. 多人游戏：必须确认"是巫师"

`creature.Entity.Player != null` 只说明"这是某个玩家" —— **队友也是玩家**。
所以凡是"只改巫师自己外观"的逻辑都要再过一层。
已提供统一判据：

```csharp
// WizardCharacter.cs
public const string CharacterEntry = "WIZARD-WIZARD_CHARACTER";
public static bool IsWizardPlayer(Player? player)
    => player?.Character?.Id.Entry == CharacterEntry;
```

`Id.Entry` 形如 `{命名空间根}-{类名的 UPPER_SNAKE}`。
⚠️ 它不是 `CharacterId`（那个是 `"Wizard"`）—— 与 `characters.json` 的键前缀
`WIZARD-WIZARD_CHARACTER.*` 一致。

> 比 `player.Character is WizardCharacter` 更稳：`Id.Entry` 是引擎的稳定标识，
> 不受将来换基类 / 换占位实现影响。

---

## 5. 新增/更换角色立绘的清单

| 用途 | 路径 | 场景里怎么引用 |
|---|---|---|
| **战斗立绘** | `images/characters/wizard_battle.png`（512×768 RGBA） | `wizard_character.tscn` 的 `%Visuals.texture` |
| **死亡立绘** | `images/characters/wizard_die.png`（512×768 RGBA） | **不在场景里** —— 由 `WizardDeathSpritePatch` 运行时换上 |
| **营火立绘** | `images/characters/wizard_rest_site.png`（2048×1536，一图两坐姿） | `wizard_character_rest_site.tscn`，用 `AtlasTexture` 切左右 |
| **商人立绘** | 复用 `wizard_battle.png` | `wizard_character_merchant.tscn` |
| **角色图标** | `images/ui/char_icon_wizard.png`（88×88） | `wizard_icon.tscn` |
| **选人立绘** | `images/ui/char_select_wizard.png`（132×195） | `CharacterModel` 属性 |
| **选人背景** | `images/ui/char_select_bg_wizard.png`（2560×1200） | `wizard_bg.tscn` |
| **能量计数器** | `images/ui/energy_wizard_counter.png`（128×128） | `wizard_energy_counter.tscn` |
| **地图标记** | `images/ui/map_marker_wizard.png`（49×64） | `CustomMapMarkerPath` |

### 加新立绘的流程

1. 素材放进 `images/characters/`
2. 引用它：
   - 场景已有节点 → 改 `.tscn` 的 `texture`
   - 运行时按条件切换 → Harmony 补丁 + `GD.Load<Texture2D>(path)`（**做静态缓存**，
     别每次事件都从磁盘加载）
3. `.\build.ps1`（Godot 自动导入并生成 `.import`）

> ⚠️ `char_select_bg_wizard.png` **本身就是一张带角色的完整构图**，
> `wizard_bg.tscn` 里**不要**再叠一层角色 Sprite —— 曾经叠过 `wizard_battle.png`，
> 结果画面出现两个巫师（见该 tscn 的注释）。
