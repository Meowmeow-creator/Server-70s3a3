# 2026-10-29 根特北门(d88) AT-5T 步行者不出现

## 现象

玩家报告：根特北门没有 AT-5T 步行者，没有它就无法打开通往下一张图的门。
实测确认：房间 15068/15069/15070/15071/15072 的东侧出口被
`10269 barr.obj` / `10274 barr2.obj`（`[is blocking obj]` + `[can beat index] 61235`）挡住，
只有 AT-5T（mob 61235）能破坏，玩家自己打不动。

## 取证（线上日志，非推测）

`local-run/logs/archive/20261029-032640/channel-evidence.log`（03:22:46 进 d88）：

| 行 | 含义 |
|---|---|
| `DUNGEON_BLOCK dungeon=88 difficulty=3 block=0 quest=0 rooms=7 boss=6,1` | 选中的是 d88 唯一迷宫块 |
| `SENT dungeon-room type=0 opcode=29 bytes=127` | 起始房 15063（6 怪）：15 头 + pad16(98) |
| `SENT next-room type=0 opcode=29 bytes=175` | 进入 (0,0)=15064（9 怪）：15 + pad16(140+17) |
| `ROOM_MOVE_ACCEPTED from=0 to=0` @03:23:00 / `from=0 to=1` @03:23:07 | 玩家确实进了两个 [ridable] 房 |
| 全程没有 `RIDABLE_OP`、没有 op130 | 客户端从未发起上骑（场上没有可骑物） |

175 = 15 + 160，而 9 怪裸房间体是 140 字节 ⇒ 包内确实带了
`RideableSectionFG361()` 的 17 字节（`[count=1][px][py][61235][200]`）。
**即：服务端按 TW1031(rednf) 格式发了可骑乘段，CN70 客户端不认。**

## 根因

CN70 客户端与本服房间协议不是同一代：

* 行动体行是 **14 字节**（`group u16, listIndex u32, uid u16, mobId u16, level u8, role u8, 0, 0`），
  rednf（TW1031）是 16 字节（`u32 mobIndex`）。见 `docs/HANDOVER-2026-10-15-act4-port.md` §16.1。
* 因而 rednf `BattleDungeonMapA2/MakePacket.inc` 里的尾段顺序
  `<hellPartyActive> <rideableCount> <4×int> <seat>` 不能当作本客户端的权威格式。
* 客户端自己会读 `.dgn`（op28 只发迷宫块序号 `MazeIndexFG361`，`EntryBody()` 见 `Dungeon-Flow-FG.cs:58`；
  `[pathgate object]` 也是客户端自己画），但它不会凭 `[ridable]` 自行生成步行者。

## 修复

默认改为**行动体方式**生成 AT-5T，TW 式房间段改为显式开关：

| 改动 | 文件 |
|---|---|
| `RideableNativeFG361` 默认 **false**，`DNF70_AT5T_RIDABLE=1` 才启用 TW 式段 | `Dungeon-QuestMaze-FG361.cs` |
| `QuestMazeRidablesFG361()` 生成 `{map, 61235, level=61, role=0}`（原来是 role 5） | 同上 |
| op130 上骑请求：只要该副本声明了 `[ridable]` 就回 ack 317 成功（原实现要求 native 模式 + 当前格有可骑物） | 同上 |
| 房间包组装时记 `RIDABLE_ROOM dungeon=.. map=.. objects=1 mode=actor-row` | `Object-Drops-FG167.cs` |

为什么 role=0：普通怪物行在 CN70 协议里就是 role 0（`Quest-Maze-FG361.Rows` 第三列），客户端按
`monster.lst` 解析 61235 = `NewMonsters/Riding/at_5t/at_5t.mob`。role 5 是 `[ai character]`，
客户端会去 `aicharacter.lst` 找 61235 —— 那里没有这个 id。61235 的 `.mob` 自带
`[ridableskill definition]`（格林机炮/勇猛突击/火焰喷射/德尔塔火箭/地毯式轰炸）与 `[int data] ... 200 ...`，
所以它是「带骑乘技能的中立载具」，不是普通敌人。

## 验收

* 新增 `local-run/driver/RidableCheck-FG361.cs`（离线）：
  * 默认模式 45 项 PASS：四格各 1 个 61235、role 0、房间包计数含它、段长度为 0、包内确实有 14 字节行；
  * `DNF70_AT5T_RIDABLE=1` 25 项 PASS：17 字节段（count/px/py/61235/200），不写行动体行。
* `QuestMazeCheck-FG360` 跑到 d88 段落全绿（之后在 d140/4102 因 FG372 清关语义漂移失败，与本修复无关）。
* 服务端已重编译并重启（`build/stable` 04:09）。

## 仍需实机确认

1. 客户端是否把 61235 行动体渲染成 AT-5T（理论上按 monster.lst 解析）。
2. 玩家靠近它时能否上骑（客户端骑乘判定是否只认房间包的可骑乘段）。若不能上骑，下一步就只剩
   客户端侧的格式取证：需要动态确认 op29 尾段/其它 op 的真实读法，再做客户端数据级兜底
   （把 barr.obj 的 `[can beat index]` 去掉，让普通攻击可以破坏路障）。
3. 日志判据：进入 (0,0)/(1,0)/(0,2)/(1,2) 会出现
   `RIDABLE_ROOM ... objects=1 mode=actor-row`；玩家上骑会出现 `RIDABLE_OP ... declares=True ok=True`。

## 修订（同日）：一台，不是四台

玩家实机反馈「根特北门刷出了两台 AT-5T 步行者」。核对 PVF 与 rednf 后确认这是第一版修复引入的
偏差：`[ridable]` 的四个格是**候选点**，不是四个步行者。

PVF 原文（`dungeon/act7/gentnorthgate.dgn`）：

```
[ridable] 1 1 0 0 61235 206 277 0 2 61235 177 279 1 0 61235 187 398 1 2 61235 222 312
```

头两个字是 RidableScript 的 `[select]` / `[regenerate]`（rednf `CDungeon.h` RidableScript +
`STDungeonScript/ImportDungeonScript.inc` case 0x3a5 子 tag `[select]`/`[regenerate]`），
`[select] 1` 表示这一局只存在 **1** 个可骑物。`CRidable::SetRidableObject`
（`MonsterAbilityLeafC3/SetRidableObject.inc`）的语义是：

* `select >= rows` → 顺序生成全部候选；
* `select < rows` → 复制候选池，每次 `random_shuffle` 后 `back()` 弹出，共生成 `select` 个；
* `m_objects` 以 `m_field0c`（步行者 mob id 61235）为 key，所以同一个 mob 本来也只能存在一台。

第一版把四个格都当成一台来生成，玩家从 (0,0) 走到 (1,0) 就会看到第二台；骑着一台进下一个
`[ridable]` 房时，坐骑和该房自己那台会同屏。

### 改动

| 改动 | 文件 |
|---|---|
| 新增 `RidableSelect`（d88 = 1），`[ridable]` 头一个字入表 | `Quest-Maze-FG361.cs`（生成器 `local-run/pvf/gen_quest_mazes.py` 同步） |
| `SelectedRidablesFG361()`：按 `[select]` 从候选池取，未选中格不生成 | `Dungeon-QuestMaze-FG361.cs` |
| `CurrentRidablesFG361()` = 选中格与当前房间的交集；行动体与 TW 段两条路径共用 | 同上 |
| 选点用 `RunId` 做 `StableDraws`（等价原生每局一次 shuffle），一局内固定 | 同上 |
| 驱动改为「四格中恰好一格有步行者 + 未选中房包计数不变」 | `local-run/driver/RidableCheck-FG361.cs`、`QuestMazeCheck-FG360.cs` |

### 验收

* `RidableCheck-FG361`（默认 26 项 / `DNF70_AT5T_RIDABLE=1` 24 项）全 PASS：
  四格中只有一格声明 61235；该房包计数 = 怪物数 + 1 且含 14 字节行动体行；其余 `[ridable]`
  房不出现行动体、不出现可骑乘段；8 个不同 RunId 抽到 3 个不同格。
* `QuestMazeCheck-FG360` 的 d88 段全绿（`d88 one [ridable] walker per run ... = 1`）；
  d140/4102 仍在 FG372 清关语义漂移处失败，为本修复之前就有的既有漂移。
* `scripts/check_repo.py` 通过（199 源），`scripts/build.py --smoke` 通过；已按 WMI 分离方式
  重启频道/入口服（14:42），客户端重登即可。

### 日志判据变化

同一次跑图里 `RIDABLE_ROOM ... objects=1 mode=actor-row` 只应出现一次（选中的那一格）；
其它 `[ridable]` 房进入时应完全没有该行。

## 小地图标记：op28 尾部的可骑物格列表

玩家要求「步行者应该像 boss 房一样在小地图上标出来」。查证：原版进图包 0x1c
（`BattleDungeonMapA2/MakePacket.inc` case 0x1c）在 `m_endX/m_endY` 之后依次写
`m_hellPosX`、`m_hellPosY`、`m_bloodState`、`m_ridable.GetObjectCnt()`、每个可骑物的
`(m_x, m_y)`，再写 `put_short(todayLayer)`。而 CN70 客户端有专用贴图
`Sprite/Interface/Common/Ridable/RidableMinimap.img`（strings-xrefs 0x000bd995），
本服 `EntryBody()` 原来固定 12 字节、尾部全 0 —— 正好等于「无可骑物」时的原生前缀
（12 = 6 + hellX/hellY/blood/count + todayLayer 2 字节，与 Lorien 的实机抓包长度一致）。

### 改动

* `Dungeon-Flow-FG.cs` 的 `EntryBody()`：按 `SelectedRidablesFG361()`（本局选中的那一格）
  写 `[9]=count`、`[10..]=cellX/cellY`，todayLayer 顺延；无 `[ridable]` 的副本仍是 12 字节，
  字节级不变。校验和由 `BatchPacketFG(...,integrity:true)` 对整段重算，自动跟着变。
* 驱动断言：`RidableCheck-FG361` 49 项（native 47 项）含「op28 = 14 字节 / count=1 /
  cell 等于选中格 / 四格视图读到同一格 / 无 `[ridable]` 副本仍 12 字节」；
  `QuestMazeCheck-FG360` d88 段也校验 op28 的可骑物格。

### 判据与回退

* 进 d88 后小地图上被选中格应出现可骑物图标（专用图，非 boss 骷髅）。
* 如果实机发现「房间里同时出现两台」，说明该客户端也会按 op28 列表自行生成步行者：
  此时把 `QuestMazeRidablesFG361()` 的行动体行关掉即可（保留 op28 列表 + op130 ack），
  原生就是「列表管标记、房间包管生成」两条通道。
* 如果图标没出现，说明该客户端不读 op28 这一段，下一手是客户端侧取证
  （`RidableMinimap.img` 的绘制点与它读的数据源）。
