# FG359 普通副本装备掉落接线 — 2026-10-27

> 前置：[INVESTIGATION-2026-10-27-hell-drop-wiring.md](INVESTIGATION-2026-10-27-hell-drop-wiring.md)
> 与 [FIX-2026-10-27-hell-drop-FG356.md](FIX-2026-10-27-hell-drop-FG356.md)。本批把普通副本缺失的
> **type2 通用装备、type4 creature 装备、special（领主神器/真·系列 + 88 本特殊配方）** 三条随机掉落
> 接上，并让已经实现的材料/配方池从 PVF 表取倍率，不再硬编码。

## 1. 之前的真实缺口（已纠正早期误判）

| 项 | 之前 | 现在 |
|---|---|---|
| `[drop prob]` 装备列（type2，309/230/209/230/198/162/135 /10000） | 从未消费 → 打怪不掉通用装备 | 每只怪 1 次 roll，按难度/怪物类型/队伍倍率缩放 |
| type4（creature 装备，30/15/15/10/5/5） | 从未消费 | 同上（41 件 creature 装备 + 通用窗口） |
| `itemdropinfo_monseter_extra.etc`（special） | 从未加载 | 282 件 `[special monster drop]` 装备 + 88 本特殊配方，**仅英雄难度**进池 |
| 材料/配方池（pool1/3）倍率 | 代码里硬编码数组 | 改从 FG359 目录取 `[dungeon difficulty drop bonusrate]` / `[monster type drop bonusrate]`，**数值不变** |
| 88 本特殊配方 | 不在 `item-metadata.xml`，无法掉落/拾取 | 新增 `special-item-metadata-FG359.xml`（Kind 1 / StackLimit 1000 / PVF 最低等级） |

**重要更正**：早前把 `[monster type drop bonusrate]` 的 `0.2/2/7/16.8` 行当成了“配方行”，实际它是
**type2（装备）行**；`1/2/3/4` 才是材料/配方/creature 行，而且第 0 行是金币。因此已接的 pool1/3
数字原本就是对的，本批没有改变它们的掉率——只是去掉硬编码并补上缺失的三条池子。

## 2. 数据与实现

- 生成器：`local-run/pvf/gen_equip_drops.py`（可重跑）→
  - `data/equip-drops-FG359.dat`（291KB）：两张表的概率/稀有度/倍率/等级窗口 +
    type2 319 表/33053 行、type4 95 表/320 行、special 43 表/2634 行（仅难度 4）。
  - `data/special-item-metadata-FG359.xml`：88 本特殊配方的物品定义。
  - 报告：`local-run/pvf/export/equip-catalog-report.json`。
- 运行时：`src/Tools/VmTestKit/Equip-Drops-FG359.cs`
  - `Offline70EquipCatalogFG359`：二进制校验（PVF sha/边界/累计权重）。
  - `Offline70EquipDropsFG359.Append`：先 special（type1..4，仅非“清空地狱组”的那一击），
    再 ordinary type2/type4；阈值 = `概率 × 难度倍率 × 怪物类型倍率 × 单人倍率`，
    稀有度走 `GetItemRarity`，物品从 `(rarity, 怪物等级)` 池里 lower_bound 抽取。
- 接入点：`Floor-Drops-FG119.Plan`（每次击杀，地面槽满时跳过而不是拒绝击杀）、
  `General-Stack-FG126.Threshold`（改为查目录）、服务构造器加载新目录与配方元数据。

## 3. 验收

| 项 | 结果 |
|---|---|
| `python scripts/check_repo.py` | 428 文件 / 181 源通过 |
| `python scripts/build.py --smoke` | 编译通过、无参拒绝通过 |
| `EquipCheck-FG359.exe`（build/stable） | **PASS checks=25**：池表计数、难度/怪物/队伍行、装备阈值 27（普通）/4535（英雄首领）、稀有度阶梯、特殊池仅英雄、27132 不在普通池、88 配方可注册 |
| `HellCheck-FG356.exe` | PASS checks=69（深渊链无回归） |
| `Act4MechanicsCheck` | ALL CHECKS PASSED |
| 服务启动（17098 一次性 store） | `LISTENING`，新目录/元数据加载无异常 |
| 线上服务 | 已用新产物重启（7002 / 17001 监听） |

复跑：

```bash
python local-run/pvf/gen_equip_drops.py --write
cd Server-70s3a3 && python scripts/build.py --smoke
cp local-run/driver/EquipCheck-FG359.exe Server-70s3a3/build/stable/
cd Server-70s3a3/build/stable && ./EquipCheck-FG359.exe
```

## 4. 已知边界

1. `getItemDropDungeonType()` 本地恒为 0（普通副本；没有维度/脚本 boss 图标记），所以 special 池只
   生成了 dungeon type 0。将来接维度副本时需重跑生成器（b=1/2 的池子同源）。
2. `[special monster drop]` 物品在本 PVF 全部是 `[hero]` + `[dungeon type all]`，所以特殊装备只在
   英雄难度掉；普通/勇士/王者难度没有 special 掉落，这是原版表本身的行为。
3. 幸运点（`CLuckPoint`）未建模，稀有度按无幸运用户的基础阶梯计算。
4. type4 只有 41 件 creature 装备，实际掉率很低（30/10000 × 倍率）。
5. 未做实机回归；建议进一次普通副本（普通难度）确认打怪会掉白/蓝装，再进英雄难度确认
   `[special monster drop]` 装备/配方会出现。
