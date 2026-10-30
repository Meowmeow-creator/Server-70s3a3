# FIX 2026-10-29：敌人的噩梦之夜「与沙影贝利特会合」后副本里出现两只沙影贝利特 —— FG384 任务房重复放点

## 症状

主线「敌人的噩梦之夜」（5729，夜间袭击战 dungeon 83，勇士以上）与沙影·贝利特会合后，同一趟副本里还能在另一个房间再遇到一只沙影·贝利特（玩家 2026-10-29 反馈"分裂出了两个"）。

## 根因：同一张地图 16337 被放进了两个格子

16337（`map/nightassault/q(1,0)verict.map`）自带的 `[ai character] id=10628` 就是沙影·贝利特；服务端把房间的 APC 按 `CurrentMap` 查表生成（`Dungeon-QuestMaze-FG361.cs:90` → `Act4-Boss-Apc-FG176.cs:81`，表在 `Quest-Maze-FG361.cs:893`）。**一个 `CurrentMap==16337` 的房间就生成一只**，而这一趟里有两个格子解析成 16337：

1. **FG361 任务迷宫（正确的那一处）**：`Quest-Maze-FG361.cs:36` 的 d83 5729 变体（`.dgn` 第 3 个 `[maze info]` 块，maze index 2）把 (1,0) 的 16320 换成 16337。客户端按这个块画迷宫，所以这里才是"会合"该发生的房间。
2. **FG205 任务房改道（多余的那一处）**：`Dungeon-QuestCompanion-FG130.cs:73` 在 5729 处于已接状态时又把 (2,1)（普通房 16326）改判成 16337。`MapFG130` 里 FG205 分支排在 FG361 迷宫之前（同文件 `:104`），所以它压过任务块自己的格子表。

后果不只是两只贝利特：第二格的名册走 `MazeRowsFG361`（任务块自己的地图 16326，7 只普通怪），只有 APC 走 `CurrentMap`（16337），于是那一间看起来是普通房、怪是普通房的、却站着一只贝利特；另外因为两格都报 16337，`[clear map] 5729` 在普通房清掉也能完成。

FG205 这条改道是 2026-10-26 加的，当时 d83 还没有任务迷宫；批次一（`local-run/coord/FIX-2026-10-27-batch1-quest-mazes-FG361.md:187`）已经写明"d83 5729 块在 (1,0) 给 16337，批次二的沙影贝利特房可以直接用"，即 FG361 落地后它就成了重复放点的死代码。

## 修复（`Dungeon-QuestCompanion-FG130.cs`，3 处）

- 新增 `MazeDrawsRoomFG384(map)`：当选中的迷宫块（`MazeFG361.Maps`）里已经有这张图时返回 true。
- 两条 FG205 改道（d83/5729→16337、d50/1475→6220）都加上 `&&!MazeDrawsRoomFG384(...)`：**改道只做兜底，不再二次放点**。
- 兜底仍然成立：严格难度下（`DNF70_QUEST_DIFFICULTY_STRICT=1`）或选中更靠后的 d83 块（如 5737）时，该块本身没有 16337，改道照旧把它放到 (2,1)，仍然只有一只。
- d50/1475 的 6220 只有 FG205 一个来源（d50 不在 FG361 变体表里，`MazeFG361==null` → 判定 false），行为不变。

## 验证

- `python scripts/check_repo.py` → 429 文件 / 199 稳定源通过；`build.py --smoke` → 199 编译通过；已停服重编、WMI 分离重启（03:45，7002/17001 在听）。**旧客户端会话会断，重开客户端即可。**
- 新 driver `local-run/driver/BelitDoubleProbe-FG384.cs`（全源 rsp，`-main:BelitDoubleProbeFG384`）：用引擎自己的迷宫/名册/APC 表逐格走 d83 与 d87，**宽松与严格两种难度策略下各 10/10 PASS**：
  - d83 diff2 + 5729：`(1,0) drawn=16337 apc=[10628]`，`(2,1) drawn=16326 serverMap=16326 apc=[]`，roomsWithBelit=1（修复前为 2）。
  - d83 diff0 + 5729（宽松→5729 块 / 严格→基础块+改道）：两种都 roomsWithBelit=1。
  - d83 diff2 + 5729 与 5737 同时已接（选中 5737 块）：改道仍在 (2,1) 放 16337，roomsWithBelit=1。
  - d83 diff2 无任务：roomsWithBelit=0；d87 diff4 + 5911：15053 一只，不变。
- 回归（全源重编后跑）：`Batch2QuestDropsCheck-FG360` 78/78、`QuestLanding-FG207` 80/80、`GhentBatchCheck-FG362` 120/120 全绿。
- `QuestMazeCheck-FG360` 在 d61/3908（宽松难度策略）与 d140/4102（清条件）两处断言失败；用"撤掉本修复的同源副本"重编复跑结果**完全一致**，属该 driver 断言过时，与本次改动无关。

## 残留 / 注意

- 10628 走 APC 侧信道（role 5，与 4424/411 无谋的剑士等同型 `[character]` 同伴一致），不计入 `TotalCount`、无 `.mob` 掉落；本次未改。
- 其它"多套格子改道机制叠加"的位置（FG205 / FG361 任务迷宫 / FG362 随机房 / FG215 任务首领格 / FG211 剧情房）以后新增时都要守同一条：**先看选中的迷宫块里有没有这张图，再决定是否改判格子**，否则同一个 `[ai character]` 会在不同格子上各生成一次。
