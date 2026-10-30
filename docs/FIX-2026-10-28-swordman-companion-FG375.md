# FIX 2026-10-28 《新手冒险家》无谋的剑士不可达 + 进图死胡同 (FG375)

玩家反馈两段：

1. 蜘蛛洞穴做史诗任务《新手冒险家》(quest 4408) 全图打完也没见到“无谋的剑士”，任务无法完成。
2. 第一版改动（把本局起点放到同伴房 4424）实机后变成“一进图就在死胡同、没有门出不去”。

第二段现场直接定位到真根因，下面按 rednf 原版逐条给出证据。**第一版的起点改动已撤销。**

## 根因：进图包 0x1c(客户端 op28) 的 m_mazeType 一直在发 0

### rednf 原版

* `CBattle_Field::SelectDungeon`（`BattleDungeonMapA2/SelectDungeon.inc`，ORIG 0x082ff486）：
  ```
  MazeScript* maze = GetAppropriateMaze(diff);
  m_mazeType = maze->m_mazeIndex;            // +0x1ac
  setQuestMazeInfo();
  m_clearCondition.SetDungeonClearConditionVector(maze->m_clearCondition);
  SetGridPath(maze, dungeon->GetDummyAppearCount());
  ```
  即**本局用哪一块 `[maze info]` 由服务端选定，并写入 m_mazeType 发给客户端**。
* `m_mazeIndex` 的来源（`STDungeonScript/ImportDungeonScript.inc` case 0x228 `[maze info]`）：
  `mazeLocal.m_mazeIndex = script->m_mazeList170.size();` 再 push_back ⇒ **该块在 .dgn 里的
  0 基序号**（0=第一块，1=第二块…）。d32 的三块依次是 base / `[quest connection] 0 4408 1` /
  `[quest connection] 0 4421 3` ⇒ 4408 块 index=1、4421 块 index=2。
* 进图包（`BattleDungeonMapA2/MakePacket.inc`）：`CParty/dungeon_start.inc` 先
  `put_header(0,0x1c); put_short(dungeonIdx); put_byte(type);` 再 `MakePacket(0x1c)`，而
  case 0x1c 的**第一个字段**就是 `put_byte(m_mazeType)`，紧接 `m_endX`/`m_endY`。
  之后的换房包 0x1d 只带坐标/怪物，**不再带迷宫号**（`CPartyDepsC2/move_map.inc`）——
  所以客户端整套迷宫网格（哪些格是房间、哪面有门、小地图）完全由进图包这一个字节决定。

### 本机客户端

* 客户端 op28 的 case（0x78FC3E）`push 0x18c` 后调用构造函数 0x782AF0，而 0x782AF0 里
  `push 0x1c; push ctx; call 0x410A40` —— 基类构造函数收到 **opcode 0x1c**，因此
  **客户端 op28 = 原版 0x1c 进图包**（与 op29 = 0x1d 同一套证据口径）。
* 我方 `EntryBody` 的 12 字节正好对齐原版前缀：
  `[u16 dungeonIdx][u8 type][u8 m_mazeType][u8 endX][u8 endY]`（bytes4/5 早已被实机证实是
  小地图首领坐标）。**但 byte3 从 FG175 基线起写的是 `GsdRunFG130?1:0`**，不是 m_mazeType。

### 后果（与两次玩家反馈逐条对上）

* 所有普通进图都告诉客户端“用第 0 块迷宫”，而服务端按 `[quest connection]` 选了第 1/2 块 ⇒
  客户端按 base 迷宫画门和小地图。
* 4408 的同伴房 4424 在 base 迷宫 (1,2) 是空格 ⇒ 客户端 (1,1) 没有南门 ⇒ 无谋的剑士永远
  进不去（“全图打完也没见到”）。第一版把起点硬放到 4424 后，客户端仍按 base 迷宫认为该格
  无房间 ⇒ **没有门、出不去**（第二段反馈），这同时证明了客户端确实是按 byte3 选块的。
* 该字段从 FG175 起就是占位值，所以 FG361/FG362 引入的所有任务迷宫（d60/61/81/84/89/140…）
  在实机上一直是“服务端任务迷宫 + 客户端 base 迷宫”的错位状态；本次一并修好。

## 改动

* `src/Tools/VmTestKit/Dungeon-QuestMaze-FG361.cs`：删除第一版起点覆盖；新增
  `MazeIndexFG361`（按 `Variants[d]` 列表位置取块序号；该列表由生成器按 .dgn 顺序逐块输出，
  位置即 `m_mazeIndex`），并注释引用上面的 rednf 证据。
* `src/Tools/VmTestKit/Dungeon-Flow-FG.cs`：`EntryBody` 的 byte3 改为 `(byte)MazeIndexFG361`
  （原 `GsdRunFG130?1:0` 删除）。基础块=0、4408 块=1、4421 块=2，与 .dgn 完全一致。
* 起点恢复原生 `[start map] 0 1`；GSD 4307/4308/4312 仍是手写覆盖（不在 QuestMaze 表内 →
  发 0 = base 块，与它们的几何一致）。

## 验收

`local-run/driver/SwordmanCompanionCheck-FG375.cs`（65 断言 ALL PASS）：

* 4408 条件 `[4421,411]`；难度 0/1/3/4 都选 4408 块；**op28 byte3==1**；起点 (0,1)=4401；
* 走到 (1,2) 首次进入房间包含 1 条 `code=411 level=25 role=5 runtime=100+TotalCount`；
  (5,3)=4421 房间同样带该 APC 行；每房掉落规划不抛异常；`MapVisit(4421)` 把任务剩余推到 0；
* 无任务时 base 块 **byte3==0**；接 4421 任务时选第 3 块（**byte3==2**）且起点为 4426。

回归：`RoomsBossCheck-FG372` 67/67、`GhentBatchCheck-FG362` 120/120；
`python scripts/check_repo.py` 428 文件 / 195 稳定源；双 exe 全源编译 + 无参 smoke。

## 残留

* 实机复验：进蜘蛛洞穴（任意难度）后应能按小地图看到 (1,1) 南侧的同伴房，进去即见无谋的剑士；
  4408 的 `[clear map]` 结算仍按“清空 4421”判定（同伴存活不硬性拦截，避免把任务锁死）。
* 原生“剑士招募后全程跟随”未实现；他固定在 4424，首领房 4421 仍保留 FG215 的旁路 APC 行作双保险。
* 其他任务迷宫（d60 等）现在客户端/服务端选块一致，但各自的实机表现未逐一复验；若某副本
  仍有门/房间不对，优先查该 dgn 块序号与 `Variants` 顺序是否一致。
