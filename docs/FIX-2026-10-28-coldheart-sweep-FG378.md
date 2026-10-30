# FIX 2026-10-28 冰心少年击败查理不结算（FG378 残余怪清扫）

## 玩家现象

冰心少年（d41，`theboywithcoldheart.dgn`）杀掉最终领主查理后副本不结算：
门不开（`REJECTED opcode=48 reason=Dungeon room admission`），玩家只能回城。

## 实锤（logs/channel-evidence.log 2026-10-28 23:36）

boss 房 5802 名册 6 条：50080/50081/50083/50082（hiddena-d 玩具兵）、**1034 冰之心**、
1030 查理（kind-3）。当局 KILL_COMMITTED：uid 105/100/101/104（四玩具）+ uid 103（查理）。
**uid 102 = 冰之心全程零 op42**——它 `.mob` 是 `[HP MAX] * 4000`、无 `[attack kind]` 的底座
道具怪，客户端打不死也从不_report 死亡。服务端 `RoomCleared` 要求整房清空 → 死锁。

## native 对照（rednf）

原生根本不会死锁，靠的是第二条结算通道：

- `Dispatchers/process_40.inc`（Dispatcher_BossDieCheck = op120）：客户端上报 boss 死亡 →
  从 `m_apcList` 划掉；**列表为空就挂 1s 定时器**（注意：find 未命中 0x15 也照样判空挂表）。
- `CPartyDepsD2/OnTimeoutBossDie.inc`（ORIG 0x085aa5d4）：定时器到点把当前图
  **所有 `m_dropCnt != 0` 的活怪逐个 `kill_monster` 强制清场**（走完整奖励管线），残怪死后
  `prepare_dungeon_clear` 的 `check_grid_clear && check_end_point` 自然通过 → ClearDungeon。
- `add_monsters_current_map.inc:144-147`：`m_field13=ref.m_champion`、`m_dropCnt=ref.m_hpPct`
  （`[fixed]`=100）。所以原生 check_grid_clear 也拦"满血普通怪"，但 op120 通道先把它清了。

本客户端**从不上报 op120**（全部归档日志 0 条，本场亦然），所以原通道不可观测。

## 修复

以 op42（客户端击杀上报）里"**终点房最后一条 kind-3 行死亡**"作为 OnTimeoutBossDie 的等价触发点：

- `Dungeon-Flow-FG.cs`
  - `Kind3SweepPendingFG378(uid)`：仅当 Active/Loaded/未结算、非塔（TowerFlowFG211）、
    非 `[clear condition]` 迷宫（FG372）、非 APC boss 房（FG176/FG361）、当前房是终点房
    （IsBossRoom）、被杀 uid 是真名册行且 kind==3、房内再无其它活 kind-3、且 `!RoomCleared`
    （有待清残怪）时返回 true。d51 假身房天然不触发：Kill() 先推进追假身链，
    BossX/BossY 已移到下一格 → IsBossRoom=false。
  - `SweepLeftoversFG378()`：把房内全部活怪（真名册 + 条件怪 + 事件怪）标死并返回；
    FG376 友方场景怪（`*_CharacterTeam`）跳过——与 RoomCleared 的豁免口径一致。
- `Batch-Service-FG.cs` `KillMonster`：`Kill(uid)` 之后、`RoomCleared` 判定之前插入：
  sweep 命中则对每个残怪发 `boss-minion-cleanup`（op38 死亡包，无掉落），并记
  `BOSS_LEFTOVER_SWEEP_FG378` 日志。随后既有链路自动走通：
  RoomCleared → MapClearFG130 → ReadyToClear → op127 boss-state → op31 clear-request。

## 有意保留的偏差（对齐 native 的取舍）

1. **清扫怪不掉落、不给经验、不推进任务击杀**。native 的 OnTimeoutBossDie 走完整
   kill_monster 管线（会 roll 掉落/经验）；本实现沿用 CleanupAfterBoss（FG372 APC 房清扫）
   的既有先例，改动面最小。触发条件本身要求"房内已无活 kind-3"，正常打法（先清小怪再打
   boss）下清扫只清打不死道具怪，损失面≈0。
2. **非终点 boss 格不清扫**：杀小领主房仍需清完整间才开门（native 里非终点格本就是普通房，
   不存在这个场景；见 FG372 第三轮偏差记录）。
3. 冰心少年无"冰之心先破、查理才可打"的原版机制门（服务端不模拟无敌门）；查理可直接击杀，
   心跟着清扫一起死，玩家可见结果与原版一致。

## 验收

- `local-run/driver/RoomsBossCheck-FG372.exe` 新增 [I] 段 27 条断言，全量 **122/122 PASS**
  （原 95 条零回归）：清扫只移除冰之心（102:1034）、玩具杀不触发、非终点格（d3 (2,3)）
  不触发、d140 clear-condition 不触发、d51 假身不触发、清扫一次性。
- 回归 `GhentBatchCheck-FG362` **120/120 PASS**。
- 部署：停服 → `check_repo.py`（197 源）→ `build.py --smoke` → driver → restart-detached
  （7002/17001 已监听）。

## 工具备忘

- 全源 rsp 生成必须用**绝对反斜杠路径**（老 .rsp 同款格式）；相对/正斜杠路径会得到
  CS2001/CS1504。路径含 `\70` 时用 `chr(92)` 拼接，防工具层八进制转义。
- csc 全路径：`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（Git Bash 无 csc）。

## 第二轮修正（FG379，玩家纠错：冰之心是保护目标）

玩家指出：进 boss 房时系统提示"**若冰之心被破坏，则任务失败**"——冰之心是保护目标，
不是可清扫的敌对残怪。任务文本直接证实：史诗任务
`n_quest/stormpass/theboywithcoldheart/epic_43_theboywithcoldheart_1_5.qst` 的
depend message 写着"**虽然守住了查理的心脏 - 冰之心**，但下次还是会出现同样狂暴的查理"。

结论：
1. **结算逻辑本身没错**——native 的有效规则就是"终点房最后一条 kind-3 死亡 → 系统清场 →
   结算"（FG378 对齐的就是它）。
2. **错的是第一轮把冰之心当成了清扫对象**。原版"冰之心被毁=任务失败"是客户端本地判定
   （服务端无对应通道；全场日志零冰之心 op42；native 的 OnTimeoutBossDie 确实会把它一起
   kill_monster——此处按 align-with-native 协议判客户端规则优先，偏差记录在案）。

修复：`Dungeon-Flow-FG.cs` 新增 `ProtectObjectiveFG379(monster)`（当前仅 1034，全库只有
5802 一张房引用）：
- `RoomCleared` 不再计入保护目标（同 FG376 友方怪的豁免口径）→ 冰之心活着不拦门；
- `SweepLeftoversFG378` 跳过保护目标 → 清扫永远不碰它；正常顺序下杀完玩具+查理后
  RoomCleared 直接为真，连清扫都不触发，冰之心完整保留到结算画面。

验收：`RoomsBossCheck-FG372` [I] 段重写为双序断言（先玩具后查理=无清扫直通结算；
先查理后玩具=清扫恰好移除四玩具、冰之心存活），**126/126 PASS**；GhentBatchCheck-FG362
**120/120**。已重新部署（7002/17001）。

服务端不实现"冰之心被毁→任务失败"：那是客户端本地流程（客户端自己播失败并中断），
服务端只需不做任何与之矛盾的事。

## 第三轮修正（同晚，实机复测仍见冰之心死亡）

**实锤**（logs/channel-evidence.log 2026-10-29 01:59:41）：查理死亡包之后 1 tick 发出了
`SENT boss-minion-cleanup type=0 opcode=38`。FG378 清扫没有触发（无 BOSS_LEFTOVER_SWEEP_FG378
日志、RoomCleared 已为真），发包的是更老的 `CleanupAfterBoss()` 兜底路径——它在
`ReadyToClear` 之后无差别把真名册全部标死并发死亡包，**没有任何豁免**。第一轮只修了
FG378 清扫，漏了这条。

修复（对齐"清扫必须服从 RoomCleared 同一套豁免"的不变式）：
- `Dungeon-Flow-FG.cs` `CleanupAfterBoss()`：跳过 FG376 友方怪与 FG379 保护目标
  （该路径原本服务于 APC boss 房的普通怪兜底，APC 房行为不变）。
- `Batch-Service-FG.cs` `TestSweepRoom()`（GM 一击必杀测试清扫）：同样跳过豁免怪，
  防止测试模式杀掉保护目标/友方怪。

验收：RoomsBossCheck-FG372 **130/130**（[I] 两序各加 CleanupAfterBoss 空结果断言；
修前该断言会返回 {102:1034} 正好复现实机日志）、GhentBatchCheck-FG362 **120/120**。
已重新部署（7002/17001）。

**不变式（今后所有"批量标死+发死亡包"的路径都必须遵守）**：
RoomCleared 豁免的怪（FG376 友方、FG379 保护目标）绝不接收清扫/兜底死亡包。
目前有三个实现点：SweepLeftoversFG378、CleanupAfterBoss、TestSweepRoom。
