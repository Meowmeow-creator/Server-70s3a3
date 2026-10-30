# 2026-10-29 疑惑之村（d53）电梯一直下降、走不出起始房（FG389）

部署状态：**未部署**。源码已改并通过离线编译（scratch 输出，未覆盖 `build/stable`），
`check_repo.py` 通过（200 源）。线上 channel/entrance 仍是旧二进制，重启频道服后生效。

## 1. 用户症状

疑惑之村（诺斯玛尔 d53）进图后起始房电梯一直往下降，无法进入下一房间。FG365 已把起始房的
独眼巨人 56716 补进房间怪表（`SuspiciousVillage/(2,5)start.map` 的
`[special passive object] 1112 -> [monster] 56716`），怪能刷出来，但电梯仍不结束。

## 2. 原版实现（rednf = 台服 1031 `df_game_r` 逐指令重建）

原版在服务端有专门的电梯计时与通知，和地图数据配套：

- `CParty::check_allmember_loading` 0x085b1ad4：一局的第一间房装载完成时
  `if (BF.m_dungeonIndex == 0x35 && BF.m_elevatorFlag != 0)` →
  `m_elevatorTimer=0, m_elevatorState=2, m_elevatorFlag=0`，并
  `InsertTimer(OBJ_TYPE_1, partyIndex, msg 0x28, 15 秒, key, 0)`。
  `m_elevatorFlag` 由 `reset_field` 置 1，所以只有一局的第一间房会开计时。
- `CBattle_Field::check_elevator_timer` 0x0830d902（`TimerElevatorDungeon`）：
  `++m_elevatorTimer`；`if (m_elevatorTimer < 4 && m_elevatorState == 2)` 再排 15 秒；
  `if (m_elevatorState == 2)` 发 `send_elevator_time_packet(0)`。
- `CBattle_Field::send_elevator_time_packet` 0x0830d848：notify 包体 2 字节
  `{ byte m_elevatorTimer, byte state }`。
- `kill_monster` 0x0830d082 段：击杀 `m_mobIndex == 0xdd8c`（56716 独眼巨人）时
  `m_elevatorState = (signed char)m_elevatorTimer <= 3 ? 1 : 2`，立即发
  `{ m_elevatorTimer, m_elevatorState }`。

即：电梯下降由服务端的 15 秒 tick 推进，最多 4 个 tick（15/30/45/60 秒）；提前击杀独眼巨人
（timer<=3）把 state 置 1 提前停下，超时后击杀是 state 2。服务端不做房间切换，客户端收到这些
字段后自己结束下降、开放出口。

## 3. CN70 客户端包号取证（不是台服的 253）

台服 `PacketNameTables` 里 `g_szNotiPacketName[253] = ENUM_NOTIPACKET_ELEVATOR_CLEAR_TIME_CHECK`，
但本机 CN70 客户端（`DNF70_v50.exe`）的编号不同：

- 客户端 notify 注册表在 file 0x9DD40..0x9F4xx，`push <handler>; push <opcode>; call 0x915E0`。
- 电梯项：file **0x9EB49** = `push 0x471290` / `push 0x109` / `call 0x915E0` ⇒ **op 265**。
- handler `0x471290`：先 `ReadByte` 两次，再遍历当前副本的被动对象表，找
  `[obj+0x238] == 0x459`（1113 = `MapObject/Obstacle/ElevatorControl.obj`），
  把第一个 byte 写 `[obj+0xCD4]`、第二个写 `[obj+0xCD0]`。
  正是 `{timer,state}` 两字节电梯包。
- 对照：CN70 的 op 253 handler（0x469A90）读 5 个 int，是别的通知，不能拿来发电梯包。

## 4. 修改

- `src/Tools/VmTestKit/Dungeon-Elevator-FG389.cs`（新增）：
  `ResetElevatorFG389`（Begin 时按 d53 置 armed）、`ArmElevatorFG389`（首次 MarkLoaded 启动）、
  `ElevatorTickFG389`（15 秒一个 tick、最多 4 个、state!=2 停止，时间参数可注入便于离线验收）、
  `ElevatorKillFG389`（timer<=3→1，否则→2）。
- `Dungeon-Flow-FG.cs`：`Begin(...)` 尾部 `ResetElevatorFG389(id)`；`MarkLoaded()` 尾部
  `ArmElevatorFG389()`。
- `Batch-Service-FG.cs`：主循环加 `MaintainElevatorFG389()`（到点发 `BatchPacketFG(0,265,{timer,state})`）；
  `KillMonster` 在 `dungeon.Kill(uid)` 后对 56716 发击杀包。

## 5. 验收

- 离线 driver：`local-run/driver/ElevatorFG389.exe`（rsp 同目录），24/24 ALL PASS：
  起始图 16408 + 56716 在怪表；装载前不计时；15/30/45/60 秒 → `{1,0}/{2,0}/{3,0}/{4,0}`；
  第 5 个 tick 不发；早杀 `{1,1}` 后停链；0 tick 杀 `{0,1}`；超时杀 `{4,2}`；
  其它副本不启动；op265 帧 type 0 / 长度 15+16 / 解密回 `{2,1}`。
- 编译：`csc` 全量 200 源到 `local-run/build-verify-fg389/`（Channel + Entrance）通过；
  `python scripts/check_repo.py` 通过。

## 6. 残余 / 风险

- **实机复测（2026-10-29 16:21-16:27）**：电梯部分已生效——日志出现
  `ELEVATOR_TIME_FG389 timer=1/2/3 state=0`、`ELEVATOR_KILL_FG389 uid=101 timer=3 state=1`，
  客户端击杀后开始发 op48 房间移动请求。剩下的卡点不是电梯，而是 **op48 每次都被
  `REJECTED opcode=48 reason=Dungeon room admission` 拒绝**：起始房刷出了两只 56716，
  玩家只杀掉一只，`RoomCleared=false`，`Move()` 直接拒绝。
- 若客户端仍需更早的反馈（例如装载后立刻要一包），当前实现按原版从 15 秒开始；先按原版对齐，
  实机不符再补 t=0 包。
- 电梯房 (`(4,1)` 的 16415–16418) 与本次问题无关：那是同格随机房，不是楼层切换。

## 7. 复测补充（2026-10-29 16:27）：起始房重复刷怪

**症状**：电梯能停、能走到门口，但门一直不开；日志每秒一条
`REJECTED opcode=48 reason=Dungeon room admission`。

**根因**：起始房 16408 的怪表有**两条** 56716：

- `Dungeon-QuestCompanion-FG130.cs` 里 FG365 的硬编码
  `if(DungeonId==53&&CurrentMap==16408) return {{56716,67,0}}`；
- 后来 FG375 从 PVF `[special passive object]` 生成的
  `special-spawns-FG375.dat` 里 16408 → `[(56716,67,100,0)]`（native `CMap::set_map`
  的 insert_monster 路径）。

于是房间 2/2 只怪，uid 100 和 101 都是独眼巨人。玩家杀一只（uid=101）后 uid=100 还活着，
`RoomCleared` 为 false，`Move()` 的 `!RoomCleared` 分支抛出同一个错误。GM killall 两只都杀
的那次能走到下一房，也印证了这一点。

**修复**：删除 FG365 的硬编码行（保留 FG375，因为它带 native 的 area/object ordinal）。
FG365 验收 driver 补 `Offline70SpecialSpawnsFG375.Load(...)`；FG389 driver 增加
“只有一只 56716 / 杀掉后 RoomCleared / op48 (3,5) 放行”三项。

**验收**：`ElevatorFG389.exe` 27/27 ALL PASS；`Fg365AcceptanceCheck.exe` 50/50 ALL PASS；
200 源编译通过；`check_repo.py` 通过。部署后重进 d53，起始房应只有一只独眼巨人，
击杀后可直接走进下一房。
