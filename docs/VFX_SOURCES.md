# 原版演出 / 音效 可借用清单（巫师 Mod 对照表）

> 目的：**不新增任何美术与音频资源**，直接调用原版已有的特效与音效，
> 给巫师卡牌换/加演出。本文件是候选池，勾选后再实现。
> 数据来源：`reference/decompiled/game`（游戏 0.111.0 反编译），扫描脚本
> `reference/tools/scan_vanilla_vfx.py`（输出 `build/vfx-scan.txt`）。
>
> 统计：596 张原版卡里 **221 张**有演出；卡牌用到 **17 种** vfx 资源、
> **33 种**脚本化特效节点。

---

## 0. 三种接入方式（按成本从低到高）

| 方式 | 代码 | 适用 |
|---|---|---|
| **A. 命中特效** | `DamageCmd.Attack(...).WithHitFx("vfx/xxx", "event:/sfx/xxx")` | 攻击类，最省事 |
| **B. 主动播特效** | `VfxCmd.PlayOnCreature(target, "vfx/xxx")` / `PlayOnSide(side, ...)` / `PlayFullScreenInCombat(...)` | 非攻击、或要在指定时机播 |
| **C. 脚本化节点** | `NXxxVfx.Create(...)` → `NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(vfx)` → 可选 `await Cmd.Wait(vfx.WaitTime)` | 有位移/持续时间/染色的大演出 |

⚠️ 共同注意：
1. **特效和音效要一起给** —— `WithHitFx(vfx)` 只改画面，音效仍是默认攻击音（我们踩过）。
2. 多目标攻击**默认只在敌方半场中心播一次**特效；要逐目标必须加
   `.SpawningHitVfxOnEachCreature()`。
3. 所有演出都要 `try/catch`，且 `NCombatRoom.Instance == null`（卡牌库预览）时直接返回。
4. 多人游戏：演出只在本地播，结算走主机 —— 不影响同步，但别把演出当作结算时机。

---

## 1. 命中特效资源（17 种，字符串直接用）

| vfx 资源 | 观感 | 原版代表卡 | 巫师候选卡 |
|---|---|---|---|
| `vfx/vfx_attack_slash` | 普通斩击 | 打击、快斩 | 击穿、弹射 |
| `vfx/vfx_attack_blunt` | 普通钝击 | 重击类 | 巨焱拳 |
| `vfx/vfx_heavy_blunt` | 重型钝击（大） | 全力一击、Bludgeon | 清净世界 |
| `vfx/vfx_starry_impact` | 星光冲击（紫） | 星辰类、Defect 多数 | 冰矢、冰霰、静电、电磁波、幻化（**现用**） |
| `vfx/vfx_bloody_impact` | 血花四溅 | 铁甲战士流血类 | 火刑（若想更狠） |
| `vfx/vfx_dramatic_stab` | 夸张刺击 | 背刺、暗杀 | 击穿、Puncture 类 |
| `vfx/vfx_flying_slash` | 飞出的斩击 | 飞刀类 | 弹射 |
| `vfx/vfx_giant_horizontal_slash` | 超宽横斩 | 大范围斩 | 清净世界、真理派对 |
| `vfx/vfx_molten_fist` | 熔岩拳（橙红） | 熔岩类 | 火球、火苗、炽焰、引燃、晴天火（**现用**） |
| `vfx/vfx_bite` | 撕咬 | 野兽类 | 怪物用，卡牌一般不用 |
| `vfx/vfx_gaze` | 凝视（眼） | 视线类 | 通灵、暗黑神秘学 |
| `vfx/vfx_rock_shatter` | 岩石碎裂 | 落石类 | 天火浩荡（备选） |
| `vfx/vfx_thrash` | 鞭打 | 树精类 | 震慑 |
| `vfx/vfx_adrenaline` | 兴奋/药效 | 肾上腺素 | 快速咏唱、顿悟 |
| `vfx/vfx_coin_explosion_regular` | 金币爆开 | 贪婪类 | 杰作（很贴"杰作=宝物"） |
| `vfx/vfx_blood_wall` | 血色屏障 | 血墙 | 冰之城墙（换色不现实，慎用） |
| `vfx/vfx_dramatic_entrance_fullscreen` | **全屏**登场特效 | DramaticEntrance | 落日、天火浩荡（备选） |

---

## 2. 脚本化特效节点（33 种，按观感分组）

### 2.1 火焰系（火流派最值得用）

| 节点 | 签名 | 观感 / 原版出处 | 巫师候选 |
|---|---|---|---|
| `NGroundFireVfx` | `Create(Creature target, VfxColor color = Red)` | 目标脚下燃起地火；Brand / **Burn（燃烧）** / Conflagration / Inflame | **燃烧之魂、引燃、纵火狂**（持续火）/ 火刑 |
| `NFireBurningVfx` | `Create(Creature, float scaleFactor, bool goingRight)` | 目标身上持续燃烧；FlameBarrier / Pyre / Overclock | **燃烧之魂、火刑、高温消毒** |
| `NFireBurstVfx` | `Create(Creature, float scaleFactor)` | 爆燃一团；Cinder / InfernoPower | 闪爆、天火浩荡（每段） |
| `NFireSmokePuffVfx` | `Create(Creature target)` | 熄灭的烟；InfernoPower / TheBombPower | 焚尽成灰 |
| `NLargeMagicMissileVfx` | `Create(Vector2 floorPos, Color tint)` | **大魔法飞弹砸地**；EndOfDays / MeteorStrike / Bombardment | ✅ **落日、天火浩荡 已用** |
| `NSmallMagicMissileVfx` | `Create(Vector2 centerPos, Color tint)` | 小魔法飞弹；Comet / GuidingStar | 火球术、火苗（可做得更"法术"） |

### 2.2 雷电系

| 节点 | 签名 | 观感 / 出处 | 巫师候选 |
|---|---|---|---|
| `vfx/vfx_attack_lightning` | 字符串 | ✅ 闪电球激发打击（**雷光/降雷 已用**，含音效） | — |
| `NHyperbeamVfx` + `NHyperbeamImpactVfx` | `Create(owner, target)` / `Create(owner, target)` | **从施法者射出的粗光束** + 命中爆点；Defect Hyperbeam | **雷光（升级版光束感）、降雷、电磁波** |
| `NSweepingBeamVfx` + `NSweepingBeamImpactVfx` | `Create(owner, List<Creature>)` / `Create(creature)` | **横向扫射光束**，可一次扫过多个目标；SweepingBeam | **电磁波、降雷（全体）** |
| `NLineBurstVfx` | `Create(Creature target)` | 目标处线条炸开；祭典兽 | 静电、超导 |
| `NThinSliceVfx` | `Create(Creature? target, VfxColor color = Cyan)` | 极细的一闪切痕（可染色）；Slice / Neutralize / FlashOfSteel | 击穿、冰矢（Cyan） |
| `NBigSlashVfx` + `NBigSlashImpactVfx` | `Create(Creature)` ×2 | 巨型斩击 + 命中爆点；PerfectedStrike | 清净世界、真理派对 |

### 2.3 冰霜系

| 节点 | 签名 | 观感 / 出处 | 巫师候选 |
|---|---|---|---|
| `NThinSliceVfx(Cyan)` | 同上 | 冰蓝色切痕 | 冰矢、冰霰 |
| `NSplashVfx` | `Create(Vector2 pos, Color tint)` | 液体溅开（可染色）；BouncingFlask | 冰霰（浅蓝）、寒冰牌 |
| `NLiquidOverlayVfx` | `Create(Creature? target, Color tint)` | 目标身上覆盖一层液体/冰壳；BouncingFlask | **冰铠、冰之城墙** |
| `NSpikeSplashVfx` | `Create(Creature, VfxColor color = Red)` | 尖刺/碎冰飞溅；CrushUnder / Stomp | 冰霰、冰之城墙 |

### 2.4 奥术 / 精神系（巫师"法术感"的主力）

| 节点 | 签名 | 观感 / 出处 | 巫师候选 |
|---|---|---|---|
| `NSmokyVignetteVfx` | `Create(Color tint, Color highlight)` | **整屏烟雾暗角**；Haze / Nightmare / Whirlwind | **暗黑神秘学、真理派对、启示** |
| `NHorizontalLinesVfx` | `Create(Color color, double duration = 2.0, bool movingRightwards = true)` | **整屏横向扫描线**（Whirlwind 大招那种）；祭典兽 | **天火浩荡、真理派对、启示、落日** |
| `NFullscreenTextVfx` | `Create(string text)` | **整屏大字** | 大招/觉醒类（如"启示""全知形态"）—— 慎用，看是否违和 |
| `NNightmareHandsVfx` | `Create()` | 屏幕里伸出大量手；Nightmare | 失忆、暗黑神秘学 |
| `NGrandFinaleVfx` | `Create(Creature)` | 盛大收束光效；GrandFinale | **杰作、真言派对、尾注** |
| `NFanOfKnivesVfx` | `Create(Creature)` | 扇形飞刀展开；FanOfKnives | 弹射（多发） |
| `NDaggerSprayFlurryVfx` + `NDaggerSprayImpactVfx` | `Create(Creature, Color tint, bool goingRight)` ×2 | 飞刀连发 + 命中；DaggerSpray | 弹射、冰霰（染浅蓝） |
| `NShivThrowVfx` | `Create(Creature owner, Creature? target, Color tint)` | 掷出一把小刀；Shiv | 击穿 |
| `NBolasVfx` | `Create(owner, target)` | 绳索/束缚；Bolas | 震慑、屏蔽 |
| `NItemThrowVfx` | `Create(Vector2 src, Vector2 dst, Texture2D? tex, Vector2? scale)` | 把任意贴图从 A 扔到 B | 书签、禁书目录（扔书）—— 需自制贴图则不算"原版素材" |
| `NScratchVfx` | `Create(Creature, bool goingRight)` | 三道爪痕；Claw / Maul | 通灵、击穿 |
| `NStabVfx` | `Create(Creature? target, bool facingEnemies = false, VfxColor color = Red)` | 一记刺入；Finisher / Skewer | 击穿、镜像 |
| `NMonsterDeathVfx` | `Create(NCreature, CancellationToken)` | 怪物死亡爆散 | 不清算，纯表现（不推荐） |
| `NHealNumVfx` | `Create(Creature target, decimal amount)` | 回血数字飘出 | **治疗术** |

### 2.5 毒/粘液/孢子（一般不用，列出备查）

`NGaseousImpactVfx(side, state, tint)`、`NPoisonImpactVfx(creature)`、
`NWormyImpactVfx(creature)`、`NGoopyImpactVfx(creature)`、`NSmokePuffVfx(creature, color)`、
`NSporeImpactVfx(creature, color)`。

---

## 3. 形态光环（能力牌专用，需实测）

原版"形态"类能力会在角色身上常驻一层光环，靠 `NFormVfx` 派生类实现：

| 节点 | 创建 | 原版归属 | 巫师可考虑 |
|---|---|---|---|
| `NDemonFormVfx` | `Create(Creature)` | DemonFormPower（恶魔形态） | 通用"变身/强化"感 |
| `NEchoFormVfx` | `Create(Creature)` | EchoFormPower（回声形态） | **全知形态**（最贴） |
| `NVoidFormVfx` | `Create(Creature)` | VoidFormPower | 暗黑神秘学 |
| `NReaperFormVfx` | `Create(Creature)` | ReaperFormPower | — |
| `NSerpentFormVfx` | `Create(Creature)` | SerpentFormPower | — |

用法（照抄 `DemonFormPower`）：
```csharp
_vfx = NDemonFormVfx.Create(Owner);          // 能力挂上时
_vfx?.SetActive(true);
_vfx?.OnEffectTriggered();                    // 每次触发时闪一下
_vfx?.SetActive(false);                       // 能力移除时
```
⚠️ 这几个光环是**按原版角色的 spine 骨骼绑定的**，挂在巫师自绘立绘上可能错位/报错 —— **必须先实测**一个再推广。

---

## 4. 音效（比特效更稀缺，重点看这里）

### 4.1 五系球的三段音效（Defect）

| 球 | passive | evoke | channel |
|---|---|---|---|
| 闪电 | `.../defect_lightning_passive` | **`.../defect_lightning_evoke`** ✅ 已用于雷光/降雷 | `.../defect_lightning_channel` |
| 冰霜 | （无独立字段） | （无） | `.../defect_frost_channel` ★ 可用于冰系**施法**声 |
| 暗影 | — | — | `.../defect_dark_channel` ★ 暗黑神秘学 |
| 等离子 | — | — | `.../defect_plasma_channel` ★ 奥术/能量 |
| 玻璃 | — | — | `.../defect_glass_channel` ★ 幻化/镜像 |

完整路径前缀：`event:/sfx/characters/defect/`

### 4.2 卡牌里出现过的其它音效

| 事件路径 | 原版出处 | 用途 |
|---|---|---|
| `event:/sfx/characters/attack_fire` | Brand / Burn / FiendFire | **火系攻击通用**（我们完全没用，值得接） |
| `event:/sfx/characters/ironclad/ironclad_whirlwind` | Whirlwind | 大风车式多段 |
| `event:/sfx/characters/ironclad/ironclad_bloodwall` | BloodWall | 屏障类（冰之城墙？） |
| `event:/sfx/characters/regent/regent_guiding_star` | GuidingStar | 星光类 |
| `event:/sfx/characters/regent/regent_sovereign_blade` | SovereignBlade | 召唤剑 |
| `event:/sfx/characters/silent/silent_dagger_spray` | DaggerSpray | 飞刀连发 |
| `event:/sfx/byrdpip/byrdpip_attack` | ByrdSwoop | 小怪攻击（示例：`WithHitFx` 第二参用法） |

> 更全的音效库可以扫 `event:/sfx/...` 字符串（FMOD 事件名），需要的话我再做一次全量清单。

---

## 5. 巫师现状（已接入）

| 卡 | 现在用的 |
|---|---|
| 落日 / 天火浩荡 | `NLargeMagicMissileVfx`（末日降临，色 `8c2447`） |
| 雷光 / 降雷 | `vfx/vfx_attack_lightning` + `defect_lightning_evoke`；降雷逐目标 |
| 火球 / 火苗 / 炽焰 / 引燃 / 巨焱拳 / 闪爆 / 清净世界 / 落日 | `vfx/vfx_molten_fist` |
| 冰矢 / 冰霰 / 静电 / 电磁波 / 击穿 / 幻化 / 弹射 | `vfx/vfx_starry_impact` |
| 打击（初始） | `vfx/vfx_attack_slash` |

---

## 6. 我的推荐优先级（等你勾选）

**第一梯队（改动小、观感提升明显）**
1. **火焰系持续表现**：燃烧之魂/火刑 → `NFireBurningVfx`；引燃/纵火狂 → `NGroundFireVfx`
2. **火系音效补全**：所有火焰攻击加 `event:/sfx/characters/attack_fire` 作 `WithHitFx` 第二参
3. **电磁波 → `NSweepingBeamVfx`**（横向扫射，最贴"波"）
4. **雷光 → `NHyperbeamVfx` + 命中爆点**（比现有闪电打击更"法术"）

**第二梯队（大演出，适合稀有/先古卡）**
5. 天火浩荡/真理派对 → `NHorizontalLinesVfx`（整屏扫描线）
6. 暗黑神秘学/失忆 → `NSmokyVignetteVfx`
7. 杰作 → `NGrandFinaleVfx`
8. 冰铠/冰之城墙 → `NLiquidOverlayVfx` / `NSpikeSplashVfx`（染冰蓝）

**第三梯队（需先实测风险）**
9. 全知形态 → `NEchoFormVfx`（骨骼绑定可能不兼容巫师立绘）
10. 治疗术 → `NHealNumVfx`

---

## 7. 已知风险

| 风险 | 说明 |
|---|---|
| 骨骼/换色绑定 | `NFormVfx` 系列、`VfxColor` 染色都假定原版骨架；巫师是自绘立绘，**必须先实测一个** |
| 全屏遮罩 | `NSmokyVignetteVfx` / `NHorizontalLinesVfx` 是整屏层，可能挡住 UI 或与 B 版美术色系冲突 |
| 音效枯竭 | 原版卡牌里显式写的 SFX 只有 7 条；大部分卡依赖角色音效库，需要另做 FMOD 事件全量扫描 |
| 多人同步 | 演出纯本地播放，不影响结算；但**不要**把演出时长当作结算等待（`await` 会拖慢实际节奏） |
