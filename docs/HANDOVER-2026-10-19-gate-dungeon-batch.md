# 交接（2026-10-19）：城镇副本门与缺口口径修正

依据 `local-run/reports/gap-report-2026-10-19.md`。本页只写本波做完的事、验到哪一步、以及没做完的确切原因。
所有数字都是**跑出来的**；未跑的一律标「未验出」。

## 1. 本波接入的副本（全部由 PVF 生成器派生，不手抄）

| 副本 | 名称 | 门区（客户端 `[dungeon gate]`） | 房间/名册证据 |
|---|---|---|---|
| 90 | 树精繁殖地 | town3 area4/area6（天帷巨兽，门已存在） | `dungeon/act3/breeding.dgn` + `map/gbl_breeding/` 12 房 |
| 91 | GBL女神殿 | 同上 | `dungeon/act3/goddesstemple.dgn` + `map/gbl_goddess_temple/` 13 房 |
| 100 | 南部溪谷 | town2 area7（新增门） | `dungeon/southerndale/grimseeker.dgn` + `map/grim/` |
| 140 | 月光酒馆 | town2 area4（新增门） | `dungeon/village/moonlightpub/aganzo.dgn` + `map/aganzo/` |
| 110/111/112 | GBL教阿拉德分部/研究所/孵化场 | town3 area9（新增门） | `dungeon/village/gbl/gbl_*.dgn` + `map/gbl1..3/` |
| 60/61 | 热血八番街/绿都格罗兹尼 | town3 area10（新增门） | `dungeon/village/cartelarad/odesa\|grozni.dgn` + `map/odesa2`、`map/groznyoutside`+`map/groznyinside`（61 跨两目录，生成器已支持多目录） |
| 86/87/92/93/1504 | 列车上的海贼/夺回西部线/雾都赫伊斯/阿登高地/幽灵列车 | **town9 鲁夫特悬空海港 area1**（新城镇） | `dungeon/act8/*.dgn`、`dungeon/ancient/ghosttrain.dgn` |
| 3503 | 逆袭之谷 | town5 area2/area3（雪山，门已存在） | `dungeon/act5/counterattackarea.dgn`；2 房 25039(3×1021)/25040(64518 首领)，逐行核对 |

1504 的 `[required item] 4421 1 1` + `[coin limit] 3` 与 1502/1506 同形，已进 `Entry-Cost-FG185.cs`（门票 4421 已在 `data/item-metadata.xml`，且 commerce 有售）。

## 2. 口径修正（gap_report.py）

- **原报告第三节"24 个可进副本没有怪物池"是错的**：`FloorCatalog.MonsterPools` 的键是**怪物 ID**，脚本拿它去比副本 ID。
  改成副本→房间→名册三级核对后：**66 个有迷宫表的可进副本，0 只怪缺掉落登记**（脚本 `local-run/pvf/gap_check_pools.py` 独立复算同结论）。
- `gates`/`selection` 原来是手抄快照，改成解析 `DungeonGate(...)` 与所有 `ProfileBody(new ushort[]{...})`；`TownSupported` 同样从源码求值。
- 物品登记改读 `data/item-metadata.xml`（原来读 `build/stable/`，工作树改了也看不见）。

## 3. 复跑结果（同一脚本，改前 / 改后）

| 指标 | 改前 | 改后 |
|---|---|---|
| 选副本集合 | 53 | **68** |
| 未接通正常缺口 | 29 | **14** |
| 缺 `DungeonGate` 门区 | 8 | **3** |
| 被副本缺口卡住的 supported 任务 | 226 | **171** |
| supported 任务缺物品登记 | 13 | **0** |
| 目录条数 / Supported=true | 3157 / 2025 | 3157 / 2025（13 条本来就是 true，只是被物品卡住） |

物品侧：`gen_item_metadata.py write --source quest-catalog.xml --allow-legacy-exceptions --apply` 追加 50 条，
`item-metadata.xml` 7113 → **7163** 条；`quests` 模式复验 `quest item references 1430, unregistered 0`，VERIFY PASS。
仍有 **9** 个 id（1039-1046、7427）**PVF 里没有物品文件**，只被 Supported=false 的任务引用，属原版数据缺失，登记救不了。

## 4. 剩余 14 个缺口是判死的阻塞，不是漏做

- **62-67**（哥布林王国/蠕动之城/兰蒂卢斯的鹰犬/巴卡尔之城/虚无之境/黑色大地）：PVF `[necessary party]=3`、`dimension 1..6`、`coinLimit 8`，
  且 `pathgate`/`evil` 机制全仓无实现（`grep -r pathgate src/` 0 命中）。单机客户端不放行，开成"可进"= 假成功。
- **120/121**（黄龙/青龙大会）：`.dgn` 迷宫房 0 间，且属比武场 —— `docs/PLAN-single-player-priority-2026-10-16.md` §1.4 明确排除 PVP。
- **3505/11000/11001**：`dungeon/towers/*.dgn` 无 `[maze info]` 块（塔格式不同）；**11006/11007/11008**：名册怪物 0 行。六塔在 `FEATURE_STATUS` 明确未交付、`research/tower` 未晋升。
- 因此 town3 area7（亡者峡谷）、town4 area4（异次元裂缝）、town12 area2（武斗大会）三个门**保持关闭**。
- 3503 已接，但 `[pathgate object] 601..610` 与 `[village attack revenge dungeon]` 波次机制未实现，`[gold card use] 0` 交给客户端自己的 PVF；房间只有 2 间，清怪即通关 —— 这是与原版的行为差异，已写进 `Dungeon-Selection-Protocol.cs` 注释。

## 5. 验证到哪一步

- `python scripts/check_repo.py` → PASS，`stableSources: 159`。
- 159 源编译 → 两个 exe 通过；无参数退出码 2（`build/stable/*.exe` 被线上服务占用会 CS0016，所以编到 `local-run/tmp/compilecheck/`）。
- 独立第二解析器 `r3_gen_dimension.py gate` 对 90/91、60/61/100/110/111/112、86/87/92/93/140/1504 全部 **0 mismatch**。
  3503 无 `[map specification]`，第二解析器取不到房间，改为手工逐行核对（上面表格）。
- Forest 表相对 HEAD 做超集校验：**0 条丢失、0 条改值、+864 条新增**。
- `python local-run/pvf/reverify_quest_flips.py` → **PASS**。
- **未做**：隔离实例真人/驱动走查（进副本打一遍）。需要停线上 7002 频道做完整 `build.py --smoke` + `restart-detached.ps1` + 起客户端，未经授权没有动。

## 6. 本波顺带修的复验工具问题

- `reverify_quest_flips.py` 的 invariant 3 原来只要"翻绿计划里有任务不再是 true"就 FAIL。
  实测 14 条（388-396 等）是 2026-10-19 05:50 那次 verdict 重导**带着 `extra:[target character]` 原因**主动回红的
  （`PLAN §3` 把"目标角色 125 条"列为待拍板）。现在区分"有记录原因的重新退休"与"无原因丢失"，后者才 FAIL；实测 unexplained=0。
- `HuntEnemyCheck.cs` 的两处快照钉死已换成不变量：目录条数改 3051..3300（引擎同边界），
  hunt enemy 记录数 333 → **323**（HEAD 333，工作树把 15 条重派生成 `[meet npc]/[seeking]/[clear map]` 等，**没有丢记录**：id 集合 3157，HEAD 的 3051 全在）。
  行为夹具 4007 已不再是 `[hunt enemy]`（现在是 `[meet npc]`），改用 **3831**（112 本、难度≥1、怪 56104、无前置、NPC 10），30 条断言全过。
- `local-run/driver/HuntEnemyCheck.rsp` 原来写死 155 个源（缺本波依赖的 4 个新文件），改成按 glob 生成 159 源、正斜杠路径。
