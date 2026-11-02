# 替换美术包（B 版）使用说明

本 mod 自带一套美术（下称 **A 版**，默认启用）。
另有一套**不在工坊公开包里**的替换美术（**B 版**），由作者私下分发。

---

## 一、安装

1. 把作者发给你的 `WizardAlt.pck` 放进**本 mod 的目录**，与 `Wizard.pck` 并排：

```
Slay the Spire 2\mods\Wizard\
├── Wizard.dll
├── Wizard.json
├── Wizard.pck          ← 原版美术（A）
└── WizardAlt.pck       ← 替换美术（B）——新加的这个
```

> 不知道 `mods\Wizard` 在哪？Steam → 库 → 右键《Slay the Spire 2》→ 管理 → 浏览本地文件，
> 然后进 `mods\Wizard`。
> （如果你是从创意工坊订阅的，游戏目录这边同样可以放——**本地 `mods` 目录优先于工坊**。）

2. 启动游戏 → 主菜单 → **设置（Settings）** → 找到 mod 设置里的 **巫师 / Wizard** →
   勾选 **「使用替换美术（勾选后需重启游戏）」**。
3. **完全退出游戏，再重新启动。**

看到卡图 / 头像 / 立绘变了就成功了。

---

## 二、切回原版美术

- 打开设置，**取消勾选**那一项 → **重启游戏**；
- 或者直接把 `WizardAlt.pck` 删掉 / 改名（这样即使勾着也只会用原版，日志里会有一条提示）。

两种方式都**不会**改动游戏本体的任何文件。

---

## 三、常见问题

**Q：勾了但没变化？**
- 99% 是**没重启**。这个开关只在游戏启动时读一次。
- 确认 `WizardAlt.pck` 与 `Wizard.pck` 在**同一个目录**（即 `mods\Wizard\`）。

**Q：日志里怎么看有没有生效？**
打开 `%APPDATA%\SlayTheSpire2\logs\godot.log`，搜索 `美术：`：
```
[Wizard] 美术：使用 mod 自带版本（设置里未开启替换美术）      ← A 版
[Wizard] 美术：已挂载替换美术包 ...\mods\Wizard\WizardAlt.pck  ← B 版
[Wizard] 美术：已开启替换美术，但没找到 ...\WizardAlt.pck       ← 勾了但文件不在，退回 A
```

**Q：会和联机冲突吗？**
不会。美术只影响你本地显示，卡牌 ID 与逻辑不变，双方谁用哪套都行。

**Q：创意工坊更新会不会把 B 版覆盖掉？**
不会。工坊更新的是工坊缓存目录，`mods\Wizard\` 是你自己的目录，`WizardAlt.pck` 不会被碰。
（但如果 A 版美术本身更新了，建议也找作者要一份新的 `WizardAlt.pck`。）

---

## 四、给作者：B 版怎么出包

B 版美术按下面的结构放好（路径必须与 mod 内 `res://` 一致）：

```
art_alt\
└── Wizard\
    ├── images\cards\*.png         所有卡图
    ├── images\ui\*.png            巫师图标：头像、地图标记、角色选择图、能量图标……
    └── images\characters\*.png    战斗 / 火堆 / 死亡 立绘
```

然后**直接跑 `\build.ps1` 就行**——第 5 步会自动出包并部署：

```
=== 5/6 打包替换美术包（B 版，可选） ===
  源：...\art_alt（76 张 png）
  WizardAlt.pck 生成并部署成功（108.2 MB，MD5 B62BCAC7）
```

细节：
- 源目录缺 / 里面没有 png / 缺 python / 缺打包工具 / 加 `-SkipAltArt` ⇒ **跳过**，
  并且**不会删除**已经装在 `mods\Wizard\` 里的 `WizardAlt.pck`。
- 产物先落 `build\WizardAlt.pck`（做哈希校验），再复制到 `mods\Wizard\`。
- 单独出包（不走整个构建）仍可用：
  `python reference\tools\make_alt_art_pck.py art_alt build\WizardAlt.pck`
- （`reference\tools\make_test_alt_art.py` 是"先用 A 版占位 + 几张明显替换"的测试版生成器。）

⚠️ 不要包含：遗物、事件、怪物的贴图（B 版范围不含它们）。
⚠️ 覆盖包里**不要**出现 `project.godot`、`.godot/uid_cache.bin`、`.godot/editor/`（脚本已自动剔除）。
