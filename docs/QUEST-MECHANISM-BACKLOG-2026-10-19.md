# 任务机制链待办清单（2026-10-19 实测）

口径与来源（全部可复算，命令见 §4）：
`data/quest-catalog.xml` **3157** 条，`Supported=true` **2025**、`false` **1132**；
仅存在于 `n_quest`、目录未收录 **425** 条（其中可支持 **150**、当前引擎不支持 **275**）。
`Reason` 是生成器 `local-run/pvf/gen_snow_quests.py:derive()` 打的规则标签，目录侧共 **80** 种字符串、current-only 侧 **42** 种。

结论先说：**转职链本身在 PVF 里不缺任务、不缺普通副本**；卡住它的是 5 个"修炼场"副本和 1 座塔。
`recon/new-job-quests-report.md` §7.3 的说法方向正确但口径不准，本文 §1 已更正（那份文档也补了更正行）。

---

## 1. 一转/觉醒任务链（最高优先，成本可控）

实测（`export/jobchain-objective-split.txt`）：

| 目标副本 | 受影响 supported 记录 | 其中 JobChange 环 | 任务 id |
|---|---|---|---|
| 500 修炼场 | 6 | 6 | 7829, 7830, 7837, 7868, 7869, 7876 |
| 503 修炼场 | 5 | 5 | 7824, 7834, 7873, 7884, 7894 |
| 504 修炼场 | 2 | 2 | 7859, 7889 |
| 506 修炼场 | 1 | 1 | 7865 |
| 514 修炼场 | 1 | 1 | 7864 |
| 11001 迷惘之塔 | 15 | 11 | 64, 65, 3200, 3506, 7815, 7819, 7827, 7840, 7847, 7857, 7860, 7879, 7887… |

- 合计 **30** 条 `Supported=true` 任务的目标永远拿不到；按前置做传递闭包是 **46** 条，其中 **40** 条是转职环。
- 500-514 这 15 个副本在 PVF 里**内容齐全**（实测房间与 `[monster]` 行）：
  500=3房/33行、501=2房/0行、502=11房/87行、503=3房/30行、504=3房/25行、505=3房/35行、
  506=3房/30行、507=3房/19行、508=3房/19行、509=3房/7行、510=3房/5行、511=2房/5行、
  512=4房/17行、513=4房/17行、514=3房/29行。全部 `[necessary party]` 无（可单进）、minLv 20/50/66。
- 它们被缺口统计漏掉的原因：客户端 worldmap 里这些行带 `[in progress]`，`gap_report.py` 把 15 个 `[in progress]` ID
  单独归一类、不计进"正常缺口"，所以看起来"没有缺口"。**11001 不同**：`dungeon/towers/towerofillusion.dgn`
  23883 字节、**无 `[maze info]` 块**、自身目录 0 房，是塔格式，属六塔未交付（`research/tower` 未晋升）。

机制（怎么判完成）：这 15 条一转任务都是 `Type=[condition under clear]`、`SubType=6`，
`Condition` 行是 `[dungeon, difficulty(-1), N]`；`Offline70ProgressionFG.Clear/Visit` 只要收到"通关过该副本 id"就推进，
`EntryDungeon`（`Batch-Service-FG.cs:260`）要求 `Forest.Supported(id) + SupportedInArea(town,area,id)`。
⇒ **不需要新机制**：把这 5 个修炼场按雪山/天帷同一套生成器落地（迷宫表+房间+名册+等级+掉落），再决定入口怎么给。
唯一未证的是**客户端侧**：worldmap 标了 `[in progress]` 的行，选择列表会不会显示、能不能进 —— 这条必须隔离实例实测，不能推定。

建议顺序：500 → 503 → 504 → 506 → 514（先做客户端会不会开门的验证，再做其余），完成后 **15** 条一转环 + 其 **8** 条直接依赖可交。

### 1.1 本轮已落地（5 个修炼场全移植，静态验证过，入口待真人）

- 表全部由 `gen_dungeon_content.py` 派生：500 `map/training`、503 `map/deadmoon`、504 `map/middleocean`、
  506 `map/deadmoon_avenger2`、514 `map/deadmoon_avenger1`，各 3 房；`patch` 后 `compare` 0 mismatch，
  独立第二解析器 `r3_gen_dimension.py gate 500 503 504 506 514` **0 mismatch**；等级/经验/champion 表同步；
  Floor 名册 +15 行、掉落池补 3 个（61820 有 champion 掉料，61250/61261 的 `.mob` 无掉率标签 → 保持空池，不编造）。
- profile 与放行：500 进 `PubBody`（town2 area4）、503/506/514 进 `ForestBody`（town2 area6）、504 进 `SkyBody`（town3 area3/5），
  `SupportedInArea` 同步；`check_repo` PASS、159 源编译 PASS。
- 机制侧**不需要新代码**（实测调用链）：`Batch-Service-FG.cs:275` op49 → `CaptureClearScore` → `:253`
  `quests.Clear(questPath, DungeonId, Difficulty, party=1, rank=clearScore[0], usedRevivalCoins, elapsed, technic=clearTechnicFG184, hit=clearHitFG184, receipt)`，
  而 `Progression-FG.cs:87` 已实现 sub6（通关即满足）/ sub7（`normalizedRank>=N`）/ sub10（`technic>=N`）。
  ⇒ 11 条 sub6 环（7837/7876/7824/7834/7873/7884/7894/7859/7889/7865/7864）与 2 条 sub10（7829/7868，technic≥50）判据成立。
- **两条要说清的风险**：
  1. 7830/7869 是 sub7，要求 `normalizedRank≥80`，而 `CaptureClearScore` 里 `rank=b[0]` 与 `hit=min(100,b[0])` **读同一个字节**
     （`PLAN §3` 已记为"需真人抓包判定 b[0] 到底是评分还是连击占比"）⇒ 这两条在拿到真人抓包前不能声称可交。
  2. 客户端 worldmap 把这些行标了 `[in progress]`：选择列表会不会显示、op15/op16 会不会被客户端自己拦下，
     **未验出**，必须隔离实例点一次。进不去就记成"内容已备、入口未通"。
- `party=1` 是写死的：sub5（按队伍人数）在本构建里永远不推进，属单机语义选择，不是 bug。


---

## 2. 需要新机制、以后一定会用上的（按影响条数排）

| 机制 | 条数 | 说明（PVF 证据 + 引擎现状） |
|---|---|---|
| `[condition under clear]` 其它子类型 | **306**（16 个 sub：5=73、11=31、13=22、9=20、14/8/15=19、10/3=16、2/1/16=15/12/12、0=11、4=9、7=8、12=4） | 生成器只放行 sub 1/2/3/10 且行宽=3（`gen_snow_quests.py:357`）。sub 5/9/11/13… 的第 3 列语义未取证。`PLAN §3` 已列"sub 1 的 `hit` 字段含义需真人抓包"。→ 每个 sub 单独取证一轮，收益最大（306 条） |
| `condition row width` 等结构不合 | **233**（width 190 + reward pair 41 + invalid 2） | 条件/奖励数组宽度不符合引擎列宽。属取证类：逐种宽度对齐原版语义，不涉及新协议 |
| `extra:[target character]` | **198**（+current-only 8） | 任务只对**指定角色**可见/可接。现在的任务存档是账号级、无 per-character 维度。要做得先定角色维度语义（远征 388-396 就是这一类，2026-10-19 05:50 那次 verdict 重导把它们回红了） |
| `grade:[training]` | **310** | 实测这 310 条 minLv 在 40..70（另有 2 条 minLv=0）、`Job` 全 `[all]`、272 条是 `[condition under clear]`，且**没有一条**引用 500-514 —— 与 §1 的一转修炼场不是同一件事，是 40-70 级的"修炼/修行"重复线（`BAD_GRADES={"[training]"}`，注释写"the training system does not exist here"）。要接得先取证的不是副本，而是这条线本身的完成条件与奖励语义 |
| `reward:[...]` 奖励类型 | **79**（title 38、hero 12、creature evolution 7、hell challenge 7、grow type 5、ridable 5、awakening type 3、slot expansion 2） | `TitleBook-FG157` 已有称号机制，38 条 title 奖励主要是接线；hero/grow/awakening 属转职层（`PLAN §5` 已审计：`s.Job` 无写入点、二觉被前置挡住）；creature/ridable/slot 是宠物/坐骑/背包槽扩展，属批次 4 |
| `extra:[effect after clear quest]` | **56** | 完成任务后的地图/NPC 副作用（开图、NPC 出现）。生成器只在值≠0 时拒绝，非 0 值的具体效果未取证 |
| `objective:[hunt enemy]` 列语义 | **44**（sub 0=26、-1=18） | 波次 2 已接五列计杀链（`Progression-FG.HuntStride/HuntGoal`）；剩下的 44 条是 sub 0/-1 的列宽或目标怪无表（APC/条件怪）。与 §3 的 APC 线合并做 |
| `extra:[first exposed by npc]` / `delete npc index` / `condition data` / `check count` / `limit showing msg` | 33 / 11(+122) / —(+52) / —(+43) / —(+20) | 都属**任务可见性与列表控制**族：哪个 NPC 首次暴露、完成后删 NPC、显示次数限制、按条件显示。单机语义下多数可以忽略（当普通任务常驻），但忽略=偏离原版，需要一次性拍板 |
| `extra:[event]` | **170**（+current-only 139） | 活动/限时任务。`PLAN §3` 列为待拍板：按普通任务开放 / 保持关闭 / 等价单机语义 |
| `extra:[mail title/contents/time/enclose]` | 11（+20） | 邮件附件奖励：需要邮件投递与领取，本仓无邮件系统（`Outbox` 只到任务奖励出件） |
| `extra:[creature kind/level]` + `reward:[creature evolution]` | 18 + 7（+10） | 宠物进化链，宠物战斗/饥饿本身也是历史未完项 |

## 3. 不接（原版废案 / 单机无意义）—— 已核实，不要为它们开机制

| 类别 | 条数 | 判定依据 |
|---|---|---|
| `disabled name` | **231** | PVF 里 `name` 以 `((` 开头（`gen_snow_quests.py:340`）= **原版自己关闭**的任务，不是我们缺功能 → 废案 |
| `missing client item` | **62 条记录**，只涉及 **9** 个 id（1039-1046、7427） | 这些 id 在 PVF `stackable.lst` / `equipment.lst` 里**没有物品文件**（`gen_item_metadata.py` 实测 refused 9），原版数据缺失，登记救不了 |
| `objective:[pvp rank] / [powerwar point] / [pvp match]` | 11（+3） | PVP/比武，`PLAN §1.4` 明确排除 |
| 11001 迷惘之塔等六塔 | 见 §1 | 塔格式无 `[maze info]`，`FEATURE_STATUS` 未交付、研究未晋升 —— 属独立工程，不是任务侧能顺手修的 |

---

## 4. 复算方式

```
# 缺口总表（含 15 个 [in progress] 的单独分类）
python local-run/pvf/gap_report.py
# 机制标签分组计数
python local-run/pvf/gen_item_metadata.py quests          # 物品闭包与 gate
python local-run/recon 相关：export/jobchain-objective-split.txt、export/reason-groups.txt、
       export/training-census.txt、export/mechanism-audit.txt（本波脚本输出）
```

本文所有条数都是上面脚本跑出来的；未跑的一律写"未验出"。
