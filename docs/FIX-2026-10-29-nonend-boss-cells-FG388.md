# FG388 非终点的 [boss map] 格不该是首领房（2026-10-29 列车上的海贼）

玩家反馈：**列车上的海贼**（dungeon 86）朝着 boss 房间的**反方向**刷图，走到最后的房间时
**也出现了一个 boss**；把它打死后**不会结算**。

## 1. 根因：FG372 把"所有 [boss map] 格"都改成了首领房

d86 的 `.dgn` 是 11×1 的直线列车：

```
[greed]  `BBFFFFFFFFFFFFFFFFFFEE`
[start map] 5 0
[boss map]  0 0  10 0        <- 两个候选终点格：(0,0) 和 (10,0)
```

* (0,0) 的车头房是 `(0,0)Boss.map` = **15039**（怪物行含 61192 `[boss]`）
* (10,0) 的车尾房是 `(10,0)Boss.map` = **15025**（同样含一行 61192 `[boss]`）
* (10,0) 的普通房是 `(10,0)Normal.map` = **15028**（61191 / 56008 / 61191，全 normal）

rednf 原生只把**本次 run 抽中的终点格**当首领格：

* `CBattle_Field::SetGridPath`（`BattleDepsB2/SetGridPath.inc`）：
  `bossIdx = rand() % m_gridMaze->m_vec20.size(); bossPos = m_vec20[bossIdx];`
  → `m_endX/m_endY` 每局从 `[boss map]` 列表里抽**一个**，其余格只是普通格。
* `CBattle_Field::get_candidator_map`（`BattleMonsterDepsC2/get_candidator_map.inc`）：
  只有 `m_endX == x && m_endY == y` 才走 `[boss map specification]` / `ChooseRandomMap(1, ...)`
  的首领分支；其它格（包括另一个 `[boss map]` 格）走 `m_mapCandidates` / `ChooseRandomMap(0, ...)`
  的普通分支。
* `add_monsters_current_map`（`BattleMonsterDepsC2/add_monsters_current_map.inc:153`）
  里的 `mob.m_roleType == 3` 只针对**当前这张图**的怪；FG372 文档把它读成了"所有非 dummy
  首领房都会生成 kind-3"，于是把每个 `[boss map]` 格都换成 `[boss]` 房 —— 这正是本次回归的来源。

本服的终点是**静态**的（`Forest.BossX/BossY` = `[boss map]` 的第一对，= (0,0)），
但 (10,0) 也拿着 15025，于是车尾房刷出 kind-3 的 61192；`IsBossRoom` 只认 (0,0)，
击杀永远进不了 `BossKilled()` → **有 boss、不结算**。

## 2. 修复：只有静态终点用首领房，其余 [boss map] 格回到普通房

| 文件 | 改动 |
| --- | --- |
| `local-run/pvf/gen_dungeon_content.py` | `derive()`：`is_boss = (x,y) == boss`（第一对 = 本服静态终点），不再用 `boss_cells`；`[boss map specification]` 也只给终点格 |
| `local-run/pvf/gen_quest_mazes.py` | `derive_maze()` 同一规则；非终点格不再读 `[boss map specification]` |
| `local-run/pvf/apply_nonend_boss_normal_FG388.py`（新） | 只改 FG372 过修复的格子：当前是 `[boss]` 房、重算后是 `[normal]` 房才替换；d51 的手工追逐链跳过 |
| `src/Tools/VmTestKit/Forest-Content-FG75.cs` | 29 格 Map 改回普通房 + 3 条缺失的 Monsters 行（4418/4419/9209） |
| `src/Tools/VmTestKit/Quest-Maze-FG361.cs` | 重新生成：6 个 base 块的 7 格改回普通房，10002/10031 等不再被引用 |
| `src/Tools/VmTestKit/Floor-Catalog-FG119.cs` | 补 3 条 MapRolls（4418/4419/9209，16/16/12 行，按生成器规则） |

Forest 的 29 格（d32 两格，其余每副一格；终点格全部保持不变）：

```
d3   (2,3)->217    d4  (2,0)->416    d5  (3,2)->617    d6  (4,1)->877
d7   (3,2)->1031   d8  (4,2)->1243   d9  (4,2)->1417   d11 (3,2)->1616
d12  (2,1)->1811   d13 (1,0)->2004   d14 (4,2)->2217   d21 (5,0)->3020
d22  (3,0)->3246   d23 (4,3)->3422   d24 (2,0)->3629   d25 (2,2)->3815
d27  (4,2)->6016   d31 (3,0)->4234   d32 (5,3)->4418   d32 (4,4)->4419
d42  (3,0)->9006   d43 (3,0)->9209   d50 (3,2)->6212   d80 (4,2)->10064
d86  (10,0)->15028 d90 (3,3)->3910   d91 (2,0)->3710   d92 (4,0)->15184
d1000(3,2)->2825
```

任务迷宫表（`Quest-Maze-FG361`）的 base 块同步改回：d25 (2,2)=3815、d31 (3,0)=4234、
d32 (5,3)/(4,4)=4416、d81 (1,2)=10000、d82 (2,0)=10022、d92 (4,0)=15184。
d81 的 (1,2) 原来是 10002（首领房，61112）；61112 同时也在终点房 10003 里，任务 4228 不受影响。

d86 修复后的实际内容：

* 终点 (0,0) = 15039，名册 61219 / **61192(kind 3)** / 61219 → 击杀后正常结算
* 车尾 (10,0) = 15028，名册 61191 / 56008 / 61191（全 normal）→ 不再出现"打不结算的 boss"
* 15028 的 `[greed]` 与 15025 同为 `EE EE`，门方向与服务端迷宫 mask 一致，客户端不会出现假门

## 3. 验收

* 静态不变量：`Forest` 29 个非终点 [boss map] 格 **kind-3 行数 = 0**；28 个终点格 kind-3 ≥ 1
  （已知例外：d89 17045 的 boss 是 role-8 APC、d140 16030 走 `[clear map]`、d507 15122 名册为空（首领是 NPC/APC），
  三者都不是 kind-3 结算）。`Quest-Maze-FG361` 所有 variant 的非终点 [boss map] 格同样 0 个 kind-3。
* `RoomsBossCheck-FG388.exe`：**126/126 ALL PASS**（原 FG372 的 [B] 段整体改写为
  "非终点格必须是普通房且无 kind-3" + 新增 d81/d82 base 块检查 + 全图非终点格扫描）。
* `Fg365Acceptance` 48/48（d86 (0,0)=15039 / (10,0)=15028）、`GhentBatchCheck-FG362` 120/120、
  `SwordmanCompanionCheck-FG375` 65/65、`BwangaChainCheck-FG377` 308/308、`MoonlightPubEntryCheck` ALL PASS。
* `scripts/check_repo.py`：430 文件 / 200 稳定源通过；`scripts/build.py --smoke` 通过（200 源，两 exe）。
* `restart-detached.ps1` 已重启：channel 7002、entrance 17001 均在监听。

已知与本修复无关的旧 driver 断言：`QuestMazeCheck-FG360` 仍按 FG361 之前的语义断言
（d141 不走任务迷宫、d140 没有 `[clear condition]` 等），跑它需要
`DNF70_QUEST_DIFFICULTY_STRICT=1`，且会在这些历史断言处停下；`Triage-FG352-Check` 的
2 条失败是徽章目录（FG370 之后）的旧断言。两者都不是本次改动引入的。

## 4. 保留的偏差（写进文档，避免下次再被当 bug）

原生每局在 `[boss map]` 列表里**随机**抽终点（d86 可能是车头也可能是车尾），本服固定取第一对。
所以：

* d86 现在**永远**在 (0,0) 车头打 boss；车尾是普通房。要还原随机终点需要把 `BossX/BossY`
  做成 per-run 状态（并在 op28 里下发抽中的那一格），属于独立的后续工作。
* d4 这类"两个终点格的 boss 房内容不同"的副本（(0,0)=b413/427、 (2,0)=b416/428，
  b416 才带 `[special passive object]` 召唤怪）目前只会走第一格；第二格的 boss 房数据仍在表里
  （`Special-Spawns-FG375` 的 428 行），等随机终点落地后自然启用。

## 5. 为什么不再担心"客户端自己把 [boss map] 都画成首领房"

FG372 当时留了一条未验证的担心（其交接文档第 357 行）：客户端是否按自己 `.dgn` 的
`[boss map]` 列表给所有候选格画首领标记。本轮按现有证据判定**不是**：

* 进图包 op28 的 body 第 4/5 字节就是终点坐标（`m_endX/m_endY`），服务端每条进图包都带它；
  如果客户端能从自己的 `.dgn` 推出首领位置，这两个字段没有存在意义。
* d51 哈穆林的实测链路：三个候选格 (1,0)/(0,0)/(0,1)，客户端小地图上只有**一个**会移动的首领
  标记；假皮特死亡包的尾部（`flag34` 时追加 `m_endX/m_endY`）把新坐标带给客户端，标记才挪到下一格。
  这只能解释为"标记 = 服务端下发的终点"，而不是"所有 [boss map] 格都画标记"。
* 玩家本次的描述也吻合：d86 是一条直线，如果两端都画首领标记，玩家不会用"朝 boss 房间的反方向
  刷图，走到最后的房间**却**出现 boss"这种说法——他看到的是**一个**首领标记。

结论：终点固定 (0,0) 时，车尾 (10,0) 在客户端只是一间普通房，加载 15028 后也没有 kind-3 演员，
不会出现首领血条/首领音乐。若后续实机反馈"车尾仍有首领标记但没 boss"，按第 4 节的随机终点
方案处理（让被标记的格一定是本局终点），而不是把 28 格改回首领房。
