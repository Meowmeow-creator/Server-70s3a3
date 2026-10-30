# FIX 2026-10-27 · 迷惘之塔进不去（选图界面不关闭）——FG363

玩家反馈：女格斗系（散打转职线）点迷惘之塔（11001）进不去。塔应当是多层结构，进场先和死神对话，
对话结束开始打怪，清完再对话一次，然后客户端自动把人推到下一层。

## 现场（`local-run/logs/channel-evidence.log`，2026-10-27 16:52）

```
16:52:32.562 FRAME opcode=16 seq=525                (客户端选图点“进入”)
16:52:32.613 SENT tower-entry16  (type1 {1})
16:52:32.613 SENT tower-entry3
16:52:32.613 SENT tower-entry28
16:52:32.614 SENT tower-info142  (11001/15 层/0/0x0b)
16:52:32.616 SENT tower-stage143 (stage1/31001/3 怪)
16:52:32.616 TOWER_ENTRY_COMMITTED dungeon=11001
16:52:32.716 FRAME opcode=16 seq=526                (100ms 后重发)
16:52:33.366 FRAME opcode=16 seq=527
16:52:35.777 FRAME opcode=16 seq=528
16:52:35.953 FRAME opcode=16 seq=529
16:52:36.108 FRAME opcode=16 seq=530
16:52:36.259 FRAME opcode=16 seq=531
16:53:10     PEER_CLOSED                             (客户端一直没发 op40)
```

客户端全程停在选图界面，从未发出 op40（楼层载入完成请求）。

## 三次实机抓包对比（决定性证据）

| 时间 | 服务端回了什么 | 客户端反应 |
|---|---|---|
| 2026-10-26 22:59（FG211 普通副本模型） | `16/3/28` **+ op29 房间包** | 1.5 秒后发 **op40**，客户端进图，能放技能、能杀怪（`ROOM_LOADED room=0`） |
| 2026-10-26 23:09（FG348 原生塔协议） | 只有 `142/143` | 14ms 后重发 op16，从不发 op40 |
| 2026-10-27 16:52（FG350 补丁后） | `16/3/28` + `142/143` | 100ms 后重发 op16，从不发 op40 |

⇒ **关闭选图对话框、触发楼层载入的是 op29（普通房间包）**；`16/3/28` 不够，`142/143` 在选图状态下
被客户端直接忽略。142/143 的字节与原生 `CDeathTower::handleStartGame` / `CStage::makeStagePacket`
逐字节一致（`local-run/driver/DecryptCapture-FG349` 解出 142=`f9 2a 00 00 0f 00 00 0b`，
143=stage1/seed/31001/3 怪），所以问题不是包体，而是**顺序/状态**。

## 修复

### 1. 进塔：先走已证明能载入的普通进场，载入确认后再给塔身份

`Tower-Run-FG348.cs`

- `TowerBeginFG348`：`16(type1 ack)/3/28` + **op29 房间包**（第 1 层，地图 31001）。
- `TowerLoadFG348`（客户端 op40）：回 `type1 op40 {1}` + `op30`；**第 1 层载入确认后**才发
  `op142`（dungeonIdx/总层数/bloodState/随机 buff）与 `op143`（stage/map/怪物行）——
  即塔身份与死神对话/楼层 HUD 在副本场景已经存在后再交给客户端。
- `TOWER_IDENTITY_SENT` 只发一次。

### 2. 楼层推进：op48 不再要求先收到 op162

原生 `handleMoveMap` 用 op162 设置的 stage state 做门；但实际情况是客户端可能整局都不发 op162
（2026-10-27 16:58 的正常副本里 op48 也是在没有任何 op162 的情况下发出的）。
现在以 **本层已清空（`RoomCleared`，含 [ai character] APC）** 为准，op162 仍照常记录。
成功推进时依次发：`type1 op48 ack` → **op29（下一层房间，载入触发）** → **op143（下一层 stage）**。
`dungeon.Move` 失败时不再先回成功 ack。

### 3. 空怪层 / 清完层的自动推进（塔不能卡层）

PVF 里迷惘塔 31003/31005/31010/31011、死亡塔大部分楼层**没有 [monster] 行**；原生用
`[apc random point]` 抽 AP 随机怪补满，本 build 未实现。空层清空条件恒真但客户端无事可做，
因此在载入确认后由维护循环等待 8 秒自动推进（`TOWER_EMPTY_FLOOR` / `TOWER_EMPTY_FLOOR_ADVANCED`）。
有 APC 的层（如 31005 的 15402）不算空层，仍要求击杀。

清完有怪的层后，正常路径是客户端在死神“on complete”对话结束后自己发 op48（塔模式）。
如果客户端仍停在普通副本场景（没有下一层传送门，FG211 时代就是这样卡在 1 层的），
服务端在清层后 15 秒兜底推进（`TOWER_CLEAR_ADVANCE_ARMED` / `TOWER_CLEAR_ADVANCE`），
保证多层的“打完对话→推到下一层”流程一定发生。
两种兜底都先做 `dungeon.Move` 再发包；已被自动跳过的楼层随后迟到的 op48 会被吞掉
（`TOWER_MOVE_AFTER_AUTO_IGNORED`，60 秒窗口），不会给新楼层回错误包。

### 4. 跨会话残留的塔状态（顺带修）

`towerFG348` 只在 `Town()`/op7 清理。玩家在塔里直接关客户端后重登，旧塔运行态还挂着：
2026-10-27 16:57 的日志里，普通副本 90 的 op40 被路由到 `TowerLoadFG348`、op48 被
`TOWER_MOVE_REJECTED` 连续拒绝（`floor=1 cleared=True`），玩家只能按 Esc 退出。

- `TowerActiveFG348` 增加身份绑定：`DungeonId` 与 `RunId` 都必须与当前副本一致。
- `World()`（登录进世界）与普通副本 op16 分支都会把 `towerFG348` 置空。

### 5. 房间包容错

`ObjectRoomBodyFG167()`（场景掉落物）失败时回退到 `dungeon.RoomBody()`，避免因为掉落规划异常
把已提交的进塔变成 `type1 op16 {0,0x15}` 毒 ack（10-26/10-27 卡死就是这个毒 ack 的形态）。

## 离线验收

```
python scripts/check_repo.py                 # 428 文件 / 186 稳定源 通过
python scripts/build.py --smoke              # rc0，双服编译
local-run/build-verify-fg348b/TowerFloors-FG348.exe
  -> TOWER_FG348 checks=119 failures=0
```

新增断言：逐层走完迷惘塔 15 层（每层地图必须命中 PVF `[death tower map indexes]` 行——
31001,31002,31003,31004,31005,31007,31008,31010,31011,31006,31019,31015,31020,31024,31025，
**不是连续编号**）、每层 op143 的 stage 号、每层怪物 + APC 全灭才算清层、空层策略
（31003 无怪无 APC、房间包为裸头；31005 有 APC 不算空层）、`IdentitySent` 初值。
原有 56 项塔包体断言不变。

## 实机验收

1. 女格斗（散打）从亡者峡谷进迷惘之塔：**应直接载入 1F**，日志顺序
   `SENT tower-entry16/3/28` → `SENT tower-room29` → `TOWER_ENTRY_COMMITTED` →
   客户端 `opcode=40` → `TOWER_LOADED` → `SENT tower-info142` + `tower-stage143` +
   `TOWER_IDENTITY_SENT`。
2. 1F 清怪 → op48 → 日志 `TOWER_FLOOR_ADVANCED ... source=op48` 且 floor 递增、地图按上表跳。
3. 空层（如 3F）应看到 `TOWER_EMPTY_FLOOR` 与 8 秒后的 `TOWER_EMPTY_FLOOR_ADVANCED`。
4. 15F 杀完 9 只怪 + APC 608 → `TOWER_FINISH_ARMED` → 排行/奖励/EPLP 菜单 → 回城。
5. 塔里直接关客户端再登录进普通副本：op40 必须走正常 `ROOM_LOADED`，不应出现
   `TOWER_LOADED`/`TOWER_MOVE_REJECTED`。

## 仍未做（与本 bug 无关的已知简化）

- 塔内 AP 随机怪 / `[ai character]` 之外的随机布点（空层因此只能跳过，不能真的打）。
- 塔牌/金币结算（op145 四组空）、排行榜记录（op144 空记录）、11008 绝望之塔（CBossTower）。
- 客户端若最终仍不进入“塔模式”（无死神对话 HUD、不自动推层），则缺的是客户端侧对
  142/143 的接收链，需要按本次日志的 `TOWER_IDENTITY_SENT` 之后客户端行为继续定位。

## 2026-10-27 17:43 实机回归与第二轮修复

进塔已经通了（`TOWER_ENTRY_COMMITTED` → 客户端 op40 → `TOWER_LOADED` → 能打 1F）。
但清怪后不推层、也没有死神对话。日志：

```
17:43:43.846 TOWER_FLOOR_CLEARED ... floor=1 uid=102
17:43:43.846 TOWER_CLEAR_ADVANCE_ARMED ... delaySeconds=15
17:43:58.906 TOWER_CLEAR_ADVANCE_REJECTED ... reason=Dungeon room admission
```

**推层失败的根因：楼层号当成了格子 Y。** 塔是 1 宽竖列迷宫，第 N 层是格子 `(0, N-1)`；
`TowerAdvanceFG348` 里写的是 `dungeon.Move(0, (byte)(Floor+1))`，从 1 层（Y=0）直接跳到
Y=2，`dungeon.Move` 的相邻性校验抛 `Dungeon room admission`（离线 driver 当时用的是正确写法，
所以没暴露）。修复：新增 `Offline70TowerRunFG348.CellYForFloor(floor)`（`floor-1`），
推进改用 `dungeon.Move(0, CellYForFloor(next))`；driver 增加 1/2/15 层的格子断言并把逐层走查
改成调用同一个 helper（**122/122 通过**）。

**死神对话仍然没有出现**，且客户端从不发 op162（塔模式标志）；说明客户端没有进入塔场景。
第二轮把 142/143 改回**原生时机**：先发 `142/143`（选图状态、客户端自己的 tower 模块启动点），
再发 `16/3/28` + `op29`；载入确认后只补发一次 `143`（同 stage 幂等），日志 `TOWER_STAGE_SENT`。
这是唯一没试过的组合（FG348=只有 142/143；FG350=16/3/28 后 142/143；FG363 第一轮=载入后 142/143）。

若仍无对话：142/143 的客户端消费者在 VMProtect 段（op142 槽位执行入口 0x5D3490 →
跳板 0x1E77C16 落在混淆区，静态不可读），下一步只能做客户端运行时追踪/补丁，不再属于服务端协议问题。

## 2026-10-27 18:00 第二次实机回归：推进成功、3 层闪退

```
17:59:52 TOWER_FLOOR_ADVANCED floor=2 map=31002 source=clear-timer
17:59:53 客户端 op40 → TOWER_LOADED floor=2      (2 层正常打怪)
18:00:52 TOWER_FLOOR_ADVANCED floor=3 map=31003
18:00:52 客户端 BugTrace（0x80000003 @0x463790）→ 连接断开
```

- 推层已修好（1F→2F 正常，玩家在 2F 打了怪）。
- **闪退发生在 2F→3F 那一刻**。PVF 里 `map/illusiontower/003f.map` 没有 `[monster]` 行，
  只有 `[apc random point] 100`；旧实现给客户端发了“0 个 actor 的房间包”，客户端崩在空塔层。
  原生服务端 `consistMap` 第二段会按 AP 点数从 aicharacter.lst 抽 AI 角色补满，所以原生客户端
  永远收不到空层。

### 修复：AP 随机怪补层（`Tower-ApFill-FG363.cs`）

- 每张塔图的 `[apc random point]`（PVF 实测）：迷惘塔 31003/31011=100；死亡塔
  30007=150、30013=250、30019=300、30024=400、30035=600、30038/39/40=400、30041=500、
  30042=600、30043=700、30044=800、30045=900。
- 抽取算法照原生：`budget` 内取随机点→落到 ≤ 它的最大档（100/150/170/200/250/350/500）→
  从档内随机取一个 AI 角色（id/等级表沿用 FG171 塔快照的真实分档）→ 去重后扣点继续，
  直到预算不足最小档。结果按 (RunId, map) 确定性生成并**缓存**，保证房间包与 op143 描述同一批 actor。
- 这些 actor 走现有 side-channel APC 通道（roleType 5、uid=100+TotalCount+i）：客户端会渲染、能击杀，
  `RoomCleared` 要求它们全死；击杀回 op38，清层逻辑不变。

离线：`TowerFloors-FG348` **138/138**（新增 AP 档位/预算/确定性/3 层房间非空/15F 混排断言）。

### 仍未解决：死神对话

客户端仍未进入“塔模式”（从不发 op162；142/143 在载入前后两种时机都没触发 HUD/对话）。
地图本身每层都有 `[NPC] 1001`（死神）与 `[tower dialog]` 文案，说明对话是客户端读自己 PVF 播的；
op142 的客户端执行入口在混淆段，服务端已无可调项，下一步只能做客户端运行时追踪/补丁。

## 死神对话：客户端塔场景不启动 → 用 op12 系统行播 PVF 原文（FG363d）

2026-10-27 18:39 实机（客户端正在塔内）做了只读内存取证（`local-run/scan_tower_objects.py`，
ReadProcessMemory 遍历 463MB 私有堆）：**没有 op142/op143 处理对象**（vtable `0x01458F7C` /
`0x014534B4`），并且整局客户端从不发 op162。结论：这个 CN70 客户端在离线进场下不会进入塔场景，
142/143 的消费者在混淆段（槽执行 `0x5D3490 → 0x1E77C16`），服务端侧没有可调项。

兜底实现（内容忠实、通道换用客户端一定渲染的系统消息）：

- `Tower-Dialog-FG363.cs`（生成器 `local-run/pvf/gen_tower_dialog_fg363.py`）：从
  `dungeon/towers/towerofillusion.dgn` / `towerofdeath.dgn` 读出全部 `[tower dialog]`
  行（迷惘 15×2 条、死亡 45×2 条，PVF 原文，GBK 传输），去掉 `<apc::-1>` 标记并加「死神：」前缀。
- 发法：op12 系统行（`{type 0,uid 0,group 0,len,GBK text}`，与 FG353 版本公告同一通道）。
- 时机：楼层载入确认（op40）后播该层 `on start`；该层清空（`TOWER_FLOOR_CLEARED`）时播
  `on complete`；按层号做写一次性回执，重复击杀不重播。日志 `TOWER_DIALOG floor=N phase=start|complete`。
- 离线：`TowerFloors-FG348` **149/149**（新增 30/90 条对话存在性、起止行不相同、越界为空、
  非塔副本为空、op12 body type 0 断言）。

这不是客户端的模态对话框：原生塔场景里的头像对话气泡无法在塔场景未启动时出现；
但台词内容、顺序、触发时机与 PVF 原文一致。若以后客户端塔场景打通，应把这条兜底按
`op162` 是否出现自动关闭，避免双份。

## 备注（非本修复）

部署时 `scripts/check_repo.py` 报 `Death-Tower-Drops-FG364.cs: unreleased tower implementation
in stable sources`（18:38/18:44 由另一并行会话写入的塔内掉落实现，已随本次编译进入 build/stable）。
该文件名命中仓库的未发布塔实现黑名单，需要该改动的作者按流程改名/登记后再提交。
