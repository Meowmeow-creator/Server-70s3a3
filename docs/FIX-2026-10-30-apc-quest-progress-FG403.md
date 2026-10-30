# FIX 2026-10-30：主线《扰乱轰炸目标》打死 TMH通信兵不提示任务完成（FG403，APC 击杀不推任务进度）

状态：**已编译部署，双服重启**。待实机确认。

## 1. 现象（用户 2026-10-30 报告，2 次实测）

主线任务《扰乱轰炸目标》要求"前往安特贝鲁峡谷的『根特东门』，除掉 TMH通信兵，勇士级以上"。
把 TMH通信兵 打死后**任务进度不动、没有『任务完成』提示**，回去找泽丁也交不了。

## 2. 任务与目标到底是什么

PVF 原文 `n_quest/gent/genteastgate/epic_56_genteastgate_5.qst`（客户端字符串 id 33235）：

```
[grade] [epic]      [difficulty] W      [level] 56 99      [pre required quest] 4215
[npc index] 59      [complete npc index] 58（泽丁）
[type] [hunt enemy]
[int data] 81  2  10607  2  1
```

catalog 行（`data/quest-catalog.xml`）：`Id=4216`，`Type=[hunt enemy]`，`Condition=[81,2,10607,2,1]`，
`Supported=true`。`[hunt enemy]` 是 5 列一组 = `[副本, 难度阈值, 目标 id, enemyType 列, 目标数]`，
所以这一行是「副本 81（根特东门）/ 难度≥2 / 目标 10607 / 1 只」。

10607 **不是 .mob**：

- `aicharacter.lst:10607 -> gunner/TMH_Signalman/TMHSignalMan.aic`，`ai_character_name_630 = TMH通信兵`；
- 声明它的房间图是 genteastgate 的 `04normal(3,1).map`、`(2,0)maze1.map`，以及任务迷宫图 10008/10043
  （`[ai character] 10607 ... [monster] [normal]`）；
- 副本 81 的基础 `[quest connection]` 块把 map 10008 放在格 (3,1)，`Quest-Maze-FG361.cs` 的
  `Apcs` 表里 `{10008, {10008,10607,58,5}}`，所以这间房真的会通过 FG186 侧通道把这个 APC 刷出来。

## 3. 根因：服务端记了账，但只在塔里才告诉客户端

`Batch-Service-FG.cs` 的 `Kill()` 把 APC（`[ai character]`）死亡单独分流：APC 的 uid 在
`100+TotalCount` 之后、不在 `ValidateKill` 的怪物区间里，所以它永远走不到 `KillMonster` 的任务累加。
分支里已经补了 `quests.KillWithAssetsFG171(..., apcId, ...)`，**服务端计数是对的**；但进度回包写的是

```
if(dungeon.TowerFlowFG211) SendQuestWireDeltaFG(apcQuestWireBefore,"tower-apc-quest-progress");
```

——`TowerFlowFG211` 是当年为塔任务 64/65 加的，普通副本的 APC 目标被杀之后**一个 op35 进度包都不发**。
客户端任务栏因此一直停在 0/1，也就不会播"任务完成"、不会把任务标成可交。

对比另外两条击杀路径：`KillMonster` 和 `PassiveObjectKillFG360` 都是**无条件** `SendQuestWireDeltaFG`。
同一类"客户端本地解不出来的目标"，APC 这条却漏了，属于分支漏改而不是设计差异。

离线取证（`local-run/driver/Quest4216ApcCredit-FG401.cs`，13/13 通过）直接驱动引擎跑这条链路：

```
catalog 4216: type=[hunt enemy] supported=True condition=[81,2,10607,2,1] npc=59 completeNpc=58
room: dungeon=81 difficulty=2 map=10008 totalCount=9
apc: uid=109 (100+TotalCount=109) id=10607 role=5
PASS the spawn'd actor is APC 10607 / death routes to the APC branch
PASS KillApcFG186: fresh death once, replayed frame rejected
after APC kill: progress=1 remaining=0 wireRemaining=0
```

即：**击杀当场服务端就已经 1/1、`Remaining`=0**，唯一缺的是那份通知客户端的 op35。

## 4. 修复（FG403）

`src/Tools/VmTestKit/Batch-Service-FG.cs`，APC 分支一行：

```diff
-if(dungeon.TowerFlowFG211)SendQuestWireDeltaFG(apcQuestWireBefore,"tower-apc-quest-progress");
+SendQuestWireDeltaFG(apcQuestWireBefore,dungeon.TowerFlowFG211?"tower-apc-quest-progress":"apc-quest-progress");
```

`SendQuestWireDeltaFG` 自带"只有打包值真的变了才发"的快照比较（见 `QuestWireSnapshotFG` 与 FG350
的连杀重复音说明），所以：

- 重复 op42 不会多发（`KillApcFG186` 本来也只认第一次死亡）；
- 塔内行为完全不变（label 仍是 `tower-apc-quest-progress`）；
- 其它 APC 目标任务一并修好：`3504`（d37/1301）、`607@d43`、`609@d44`、`1400@d23`、`11400@d26`、
  `20607@d141`、`21400@d21`——它们此前都只能在副本结算包（`clear-quest-progress`）里被动刷新。

## 5. 验收

离线（不碰存档）：

```
python scripts/check_repo.py        # 549 文件 / 203 稳定源，通过
python scripts/build.py --smoke      # compiled 203, smoke=true
local-run/build-verify-fg401/Quest4216ApcCredit-FG401.exe  # PASS=13 FAIL=0
```

部署校验：`build/stable/DNF70-Channel-FG.exe` 内含新字面量 `apc-quest-progress`（UTF-16 字符串堆），
双服已用 `local-run/one-click.ps1` 重启，7002/17001 就绪。

实机：接 4216 进根特东门 → (3,1) 房打死 TMH通信兵 → 击杀瞬间应弹"任务完成"、任务栏变可交；
回城找泽丁(58) 交任务。

**如果存档里进度已经是 1/1**（旧版服务端已经记上账、只是没通知）：登录时 `BatchSelectFG`（op4）会按
`{id, WireRemaining}` 逐条重发当前任务，所以之前白打的那一次不会丢，重新登录就应看到"可完成"，不必重跑副本。

注：本机 `local-run/DNF70-batch-test-FG` 里没有任何角色带 4216 的 Active/Completed 记录（r8 只到 4201/4202，
主线前置 4215 未做），也没有对应日志留下——这两次实测大概率来自另一份部署/存档。诊断本身不依赖那份存档：
第 3 节的离线驱动已经用引擎把"击杀 → 1/1"钉死，缺的只是通知。

## 6. 残留 / 未做

- `[hunt enemy]` 的第 4 列 `enemyType` 仍未参与计杀门禁（`Progression-FG.cs` 里有明确注释：rednf 的
  `ENUM_QUEST_ENEMY_TYPE` 是占位 stub，原版又是客户端 trigger 模型，静态证据定不死）。本条修复不依赖它。
- 客户端不会为 APC 目标发自己的 op35（否则它本地就会先跳完成），所以服务端推送是唯一通道；
  若后续抓包发现客户端其实会发、只是被拒，再按 `QUEST_TRIGGER_COMMITTED` 日志另案处理。
