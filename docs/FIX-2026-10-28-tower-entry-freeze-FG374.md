# FIX 2026-10-28 · 女格斗进入迷妄之塔卡死（选图/载入阶段全交互冻结）——FG374

玩家反馈：女格斗（散打线，存档 role 14「sadas」）点迷妄之塔（11001）后客户端卡死，只能杀进程。
10-27 修复后的版本曾实测能进 1F，本批是**回归**。

## 1. 现场与取证

- 客户端 `start/TenProtect/BugTrace.log` 最后一次记录是 **19:38:13 `0x80000003 @0x00463790`**。
  该地址是 CRT 的终止桩（0x463790 落在 int3 填充区，栈帧是 `0x1225520/0x121abd0/0xE75D20` 的
  异常展开链）——`Thread2026-10-28-19-38-13.dmp.z` 里 37 条线程、异常码 0x80000003、`Eip=0x463790`，
  且**同目录 08:08 / 20:49 的 dump 是同一签名**（每次被强制结束都会落这一份）。
  ⇒ 19:38 不是客户端自己抛异常崩溃（没有新的 BugTrace 业务异常），而是**被用户手动杀掉**，
  与“全交互卡死”一致。
- 19:38 的频道日志在 20:42/20:45 重启时被覆盖，无法回放；因此改用**离线复跑**取证。
- 客户端 exe 校验 `md5 21bb2486259c246b30389944d8fa9f77`，与 10-28 08:03 的“干净版”备份逐字节一致，
  排除客户端补丁残留。

## 2. 离线复跑（新增工具）

`local-run/tower-entry-check/TowerEntryCheck-FG374.cs`：反射构造真实的 `Offline70BatchServiceFG`
（存档用 `local-run/DNF70-batch-test-FG` 的副本，服务端数据文件用 `build/stable` 的同名副本），
绑定 **role 14 / 女格斗 / Lv70**，把 `keys[i]=i` 下 Blowfish 加密的 8 字节 op16 直接喂给 `Handle`，
并用 `deathSendFG170=MemoryStream` 截获全部下行包、按 opcode 解密。

结论：**服务端侧完全正常**——无异常、无 `REJECTED`，下行字节与 10-27 已验收抓包逐字节一致：

```
op142 plain = F9 2A 00 00 0F 00 00 0B        (11001 / 15 层 / blood 0 / 无随机buff)
op143 plain = 01 00 | seed | 19 79 | 03 | {mobIdx,uid=100..102,monster=4,level=30,role=0}.. | 00
op29  plain = 房间头 + 3 行怪（uid 100..102，怪 4，30 级）+ 物件/骑乘段
```

所以卡死不是“服务端没发包”或“包体坏了”，只能是**发送时机/顺序在客户端侧触发了 UI 状态问题**。

## 3. 根因（两处时机回归）

### 3.1 塔身份包被提前到选图对话框仍在的窗口

FG363 第一轮（10-27 17:43，实测能进 1F）的顺序是：`16/3/28` + `op29` 先发，`op40` 之后再给
`142/143`。17:43 之后为尝试让客户端进入“塔模式”，第二轮把 `142/143` 移到了 `16/3/28` **之前**。
18:39 的堆取证已经证明这组包在选图状态下**根本没被消费**（内存在不到 op142/143 处理器对象、
全pro从不发 op162）。留在前面只有风险：

- `16/3/28` 才是关闭选图对话框的包（FG350 结论），`op29` 才是楼层载入触发（FG363 结论）；
- op142 的处理器会把对象挂到全局 UI 管理器（`0x9D2F40` 槽：`[0x18AFF54]+0x3b50` →
  `0x422E80/0x422E90`，并调 `0xD95210` 渲染），在**模态选图框还开着**时创建/操作塔 HUD，
  存在模态竞态：同样是这个组合，开发机在 18:00 能进，玩家在 19:38 卡死，符合“时序相关”。

修复：回到已实测的组合——先 `16(type1 ack)/3/28` + `op29`，`op40` 载入确认后再补 `op142+op143`
（只一次，`TOWER_IDENTITY_SENT`）。

### 3.2 死神台词的 op12 紧贴载入确认

`FG366b/FG370` 把 op12 的发送者 uid 从 0 改成 `role.Id`（uid=0 会被本客户端丢弃）。但该行是在
`TowerLoadFG348` 里**紧接着 op40 的 ack 立即发送**的：此时副本场景和玩家 actor 正在创建，
op12 的 uid 需要在场景里解析。10-27 能正常玩的那版正是 uid=0（被丢弃、等于没发），
这是 18:39（可玩）→ 19:38（卡死）之间**唯一**触碰塔流程的改动。

修复：台词改为**武装**（`DialogPending/DialogFloor/DialogDueUtc`），由维护循环在载入后 2 秒播放
（`TOWER_DIALOG_ARMED`），此时场景已建立、uid 可解析；台词文本、层号、去重规则不变。
楼层清空时（`TOWER_FLOOR_CLEARED`）先取消未播的 start 行，避免“先 complete 后 start”。

## 4. 顺带加固（同类卡死的防线）

1. `TowerLoadFG348`：op40 的 body 不是 8 字节时降级为 0 摘要（`TOWER_LOAD_BODY_IGNORED`），
   `Sync()` 失败只记 `TOWER_LOAD_SYNC_PENDING`——**绝不因为摘要/Sync 抛异常而丢掉载入 ack**
   （丢 ack 就是载入画面永久卡死）。
2. `TowerRoomBodyFG348`：兜底从只捕 `InvalidDataException` 扩到任意异常，仍回退裸房间包，
   保证 `16/3/28/op29` 突发不会被半路截断。
3. `Batch-Service-FG` op16 的异常分支：塔运行态仍有效（`TowerActiveFG348`）时**不再回毒 ack**
   `{0,0x15}`（FG350 证明它会锁死选图界面），改为 `TowerEntryRecoveryFG374()` 补完整突发
   （`TOWER_ENTRY_ERROR_SUPPRESSED` / `TOWER_ENTRY_RECOVERED`）。真正的进场前拒绝（等级/物品/
   选图范围）仍回原生错误包。

## 5. 离线验收

```
python scripts/check_repo.py            # passed, 428 files / 195 stable sources
python scripts/build.py --smoke         # compiled 195 / smoke true
TowerFloors-FG348（全源重编，152 项）    # TOWER_FG348 checks=152 failures=0
TowerEntryCheck-FG374（真实存档 role14）：
  entry  : op14 → op16(type1) → op3 → op28 → op29        （无 142/143）
  op40   : op40 ack → op30 → op142 → op143               （塔身份只一次）
  +2s    : op12（uid=14，GBK 死神台词）                    （TOWER_DIALOG_ARMED）
```

新增断言：`Begin()` 清空武装状态；`DialogPending` 初值 false。

## 6. 部署

2026-10-28 21:29 全量重编并重启（`one-click.ps1 -Build -NoOneHitKill`）：
channel pid 29940 / entrance pid 41680，7002/17001 LISTENING，`oneHitKill=0`。
产物已确认含新日志串（`TOWER_DIALOG_ARMED` / `TOWER_ENTRY_RECOVERED` / `identityPreLoad=False`）。

## 7. 实机验收

1. 女格斗从亡者峡谷进迷惘之塔：日志顺序应为
   `SENT tower-entry16/3/28` → `SENT tower-room29` → `TOWER_ENTRY_COMMITTED ... identityPreLoad=False`
   → 客户端 `opcode=40` → `SENT tower-load-ack40/30` → `tower-info142` + `tower-stage143`
   → `TOWER_IDENTITY_SENT` → `TOWER_LOADED`。
2. **不应再出现** `SENT tower-info142/stage143` 在 `tower-entry16` 之前；不应出现
   `dungeon-entry-rejected` 或 `TOWER_ENTRY_ERROR_SUPPRESSED`。
3. 进图后约 2 秒聊天窗系统页出现“死神：我是死神，在绝望中寻求新生的人啊，欢迎你来到迷妄之塔！”
   （`TOWER_DIALOG_ARMED` → `TOWER_DIALOG ... phase=start`）。
4. 清完 1F：`TOWER_FLOOR_CLEARED` → `TOWER_DIALOG ... phase=complete` → 15 秒兜底或 op48 推 2F。
5. 若仍卡死，先看 `channel-evidence.log` 是否停在“已发 tower-room29 但无 opcode=40”，
   以区分“选图不关”和“载入无 ack”两类，再按 §3.1/§3.2 的日志锚点定位。

## 8. 未改动 / 已知残余

- 客户端塔模式仍未启动（不发 op162、无原生死神对话框）；台词仍走 op12 聊天行（FG371 结论）。
- 塔内 AP 随机怪、塔牌/排行榜/金币结算沿用 FG363/FG364 现状。
- 本批未改客户端 exe（md5 仍为 21bb2486…）。
- 同批并行会话的 FG373 容器抽奖等改动随本次全量重编一起进入稳定包（不属于本修复）。

## 9. 2026-10-28 21:37 实机第二轮：清完 2F 推 3F 卡死（op143 count=0）

进塔与 1F→2F 已通（新顺序生效：`identityPreLoad=False`、台词 2 秒后播）。玩家清完 2F 后
15 秒兜底推 3F（`21:37:44`），客户端**没有再发 op40**，卡在 3F 载入前。

**抓包对账（`channel-evidence.log.00126/00127`，DecryptCapture 口径解密）**

```
21:37:23  tower-room29 127B → op29 plain = 00 01 … 1A 79 07 + 7 行怪(uid100..106, 4/50004)
          tower-stage143 127B → 03 00? 实为 02 00 … 1A 79 07 + 同样 7 行   ✅ 一致
21:37:44  tower-room29  47B → 00 02 … 1B 79 01 + 1 行 APC(uid100, 20002, lv15, roleType5)
          tower-stage143 31B → 03 00 <seed> 1B 79 00 + 00                ❌ count=0
```

即房间包里有 1 个 AP 补层怪，紧随其后的 op143 却是空花名册。FG363d 已证明 count=0 的塔层包
会让客户端停在载入前（不再发 op40），与现象完全一致。

**根因**：`StageBody` 只输出“尚未被房间包登记（`ApcPendingFG186`）”的 side-channel APC。
推层顺序是 op29 → op143，`RoomBody()` 已经把这个 APC 登记成已生成，于是 op143 把它过滤掉。
凡是“只有 APC/AP 补层”的楼层（迷惘 3F/5F/10F/11F/15F 等）都会触发。

**rednf 原版对照**（`reference/rednf/remake/source/DNFServer/GameServer/Game/CDeathTower/`）

- `consistMap.inc`：把该层**所有** actor 塞进同一个 `m_mapInfo.m_monsterMap`——
  第一段 `[ai character]`（`m_roleType=5`，`m_instId=m_assignItemIdx++`）、
  第二段 `[apc random point]` 随机怪（`mobIdx=0x40` 起步，同样 `Add_Mob`）、
  第三段 `[monster]` 行、第四段特殊被动物品；实例号共用一个递增计数器。
- `makeStagePacket.inc:11`：`packet.put_byte(m_mapInfo.m_monsterMap.size())`，随后遍历写每一条
  （`put_int(mobId) / put_short(instId) / put_int(mobIndex) / level / roleType / field13 / field14`）。
  **没有任何 pending/已生成过滤**。
- `killMonster.inc`：`monster = it->second; m_mapInfo.m_monsterMap.erase(idx);`
  ⇒ 死掉的入口被删掉，op143 永远是“本层存活花名册”。
- `handleMoveMap.inc`：`moveNextStage` → `m_stage.reset()` → `consistMap(新层)` →
  只发 `put_header(0, 0x8f)`（=op143）再 `_checkMemberReady()`。原生推层**只发 op143**，
  不重发房间包；本客户端离线进场不启动塔场景，所以本 build 仍需先用 op29 触发载入，
  但两张包必须描述同一批 actor。

**修复**（`Tower-Run-FG348.cs` `StageBody`）

- 计数与行输出从“仅 pending”改为 **`pending || ApcAliveFG186(uid)`**：
  已登记且存活 → 照常列出；已登记且死亡 → 跳过（等价原生 erase）；
- 只有 `pending` 时才 `ApcRegisterSpawnFG186`（避免同一 floor 重复登记）；
- 注释写明与 `makeStagePacket`/`killMonster` 的对应关系。

**离线验收**

- `TowerFloors-FG348` → **155/155**，新增 3 条：3F 在 `RoomBody()` 之后调 `StageBody`，
  count 必须仍为 1、uid=100、怪来自 100 点档（600/5004/20002）。
- `TowerEntryCheck-FG374` 端到端 1F→2F→3F：floor3 的 room29 与 op143 都是
  `1 个 actor（uid 100 / 怪 5004 / lv15 / roleType5）`。

**部署**：2026-10-28 21:43 全量重编重启（channel pid 28396 / entrance pid 32752），
`check_repo` 195 源、`build.py --smoke` rc0。

**验收（实机）**：清完 2F 后 15 秒进 3F，日志应看到 `TOWER_FLOOR_ADVANCED floor=3 map=31003`
→ `SENT tower-room29` + `SENT tower-stage143`（两者都含 1 个 roleType5 actor）→ 客户端 `opcode=40`
→ `TOWER_LOADED floor=3`。若仍卡，抓这两条 `*.bin` 解密比对 count。

## 10. 2026-10-28 22:11 第三轮：2F→3F 必崩（客户端 2 格房间表越界，dump 定案）

第一轮修完后进塔/1F/2F 都正常，清完 2F 推 3F 仍崩。这一轮有**决定性 dump**：

- `start/TenProtect/Thread2026-10-28-21-37-44.dmp.z` 与 `...-22-11-23.dmp.z` 栈完全一致，
  且都落在“服务端推 3F 房间包”的同一秒（22:11:23.742 推送 → 22:11:23.951 落 dump）：
  `Eip=0x00463790`（CRT 陷阱）、栈 `0x48B0DA → 0x491804 → 0xF2B04B → 0xBC88B2 → 0x41EA1C(WndProc)`。
- 反汇编定位：客户端房间解析把包头的 **X/Y 当二维下标** 查自己的房间表
  （`0x47A530`：`index = rowBase[page]*Y + X`，再和全局房间表长度比较），越界就
  `call 0x128E513`（secure-CRT 参数漏斗 → 进程被杀）。
- 我们的 3F 包是 `X=0, Y=2`；1F/2F 是 Y=0/1。塔的 `.dgn` **没有 `[maze info]` 块**
  （实测 `dungeon/towers/towerofillusion.dgn` / `towerofdeath.dgn` 都没有），客户端走普通副本
  通道时只能建出**2 格（起点/首领）回退房间表** ⇒ `Y=0/1` 可进，`Y=2` 必然越界。
- 与 op143 内容无关：21:37 那轮 op143 是空花名册、22:11 那轮 op143 带 1 个 AP 怪，dump 一模一样。

**修复（服务端，绕开客户端那张 2 格表）**

- 楼层与地图从格子解耦：`Offline70DungeonFlowFG` 新增 `TowerFloorFG374` /
  `TowerMapOverrideFG374`，`CurrentMap` 在塔运行时优先返回覆盖地图；
- `CellYForClientFG374(floor) = (floor-1)%2`：每层只在客户端认识的两个格子间交替（0↔1），
  每次推进仍是合法的一步移动；
- `FloorMapFG374(id,floor)`：按 PVF `[death tower map indexes]` 取真实楼层图（31003/31025/30045…），
  写进 op29/op143；
- 格子复用前 `TowerResetRoomFG374()` 清掉该格上一层的所有按 RoomIndex 缓存的状态
  （死者集合、visited、side-channel APC 登记、房间怪池 `eliteRoomsFG118`、随机事件怪
  `eventRoomFG174`、被动对象击杀计数 `passiveKillsFG360`，服务端再 `roomLoot.Remove`）；
- `StageBody` 的 stage 号改用 `TowerFloorFG374`（不再用 Y+1）。

**代价**：客户端小地图只认识两格，会看到队伍在两格之间来回跳；每层的地图、怪物、掉落、
stage 号、结算全部按真实楼层走。

**离线验收**

- `TowerFloors-FG348` → **169/169**（新增：格子交替序列、三层图映射、覆盖优先于格子、
  复用格清态后 roster/TotalCount 为新层数据）；
- `TowerEntryCheck-FG374` 端到端 1F→2F→3F：3F 的 op29 = `(0,0) + map 31003 + 1 个 roleType5 actor`，
  op143 = `stage 3 + map 31003 + 同 1 个 actor`（不再出现 Y=2）；
- 直接加载**部署产物**复核：`client cells 1/2/3/15 = 0/1/0/0`、`floor maps 3/15=31003/31025`、
  `floor3 stage=3 map=31003 count=1` → `DEPLOY_CHECK PASS`。

**部署**：2026-10-28 22:30 全量重编重启（channel pid 41360 / entrance pid 400）。

**验收**：从 2F 清完推进 3F 起，日志应是
`TOWER_FLOOR_ADVANCED floor=3 map=31003` → `SENT tower-room29`（plain 以 `00 00` 开头）+
`SENT tower-stage143`（`03 00 … 1B 79 01 …`）→ 客户端 `opcode=40` → `TOWER_LOADED floor=3`。
若出现 `opcode=40` 缺失但 no dump，说明客户端把复用格当成“同一房间”跳过了载入，
下一步应改为在 op28 里给塔补一个可用的 `[maze info]`（客户端数据补丁）并回退本临时方案。

## 11. 2026-10-28 23:03 第四轮：3F 能进了但没怪（AP 补层 actor 没有锚点）

两格方案生效：3F 正常载入（`opcode=40` → `TOWER_LOADED floor=3 map=31003`，无 dump）。但玩家反馈
3F 没有怪、没有敌对 APC。抓包核对（`.00135/.00136`）：op29/op143 都带了 1 个 APC 行
（uid 100 / 600 / lv15 / roleType 5），客户端就是没生成。

**机制**：普通房间通道里，客户端是拿**地图文件自己的 `[monster]` / `[ai character]` 行**给包里的
actor 找落点的（行序/ID 对齐），`[apc random point]` 不是锚点。003f/011f（迷妄 3F/9F）两张图
只有 `[apc random point]` 和 `[monster spawn pos]`，没有任何可对齐的行 ⇒ 无论服务端发什么 actor
都会被丢弃。死亡塔的 30007/30013/30019/30024/30035 同理（它们的 `[ai character]` 楼层不受影响）。
native 没这个问题：它的客户端跑塔模块，AP 随机怪由 CStage 自己布点。

**修复（FG374c，服务端）**

- `TowerApRowsFG363()` 不再产线：AP 抽取表保留（确定性逻辑留档），但**永远不上 wire**——
  发一个生不出来的 actor 反而让服务端认为 APC 未死、楼层永远清不了；
- 新增 `FloorHostableFG374(id,floor)`：地图有 `[monster]` 行或 `[ai character]` 行才算可布防；
- `TowerAdvanceFG348` 推层时**直接跳过不可布防的楼层**（`TOWER_FLOOR_SKIPPED`），
  不再为它们发房间包/楼层包，也不播台词；若剩余层全不可布防则直接进入结算
  （`TOWER_ANCHORLESS_TOP`）。跳层后目标格可能与当前格相同（同奇偶），此时不再调用
  `dungeon.Move`，改为显式 `dungeon.Loaded=false` + `TowerResetRoomFG374()`；
- 迷妄塔实际层序变为 1,2,4,5,…,15（跳过 3、9），死亡塔跳过 30007/30013/30019/30024/30035。

**离线验收**：`TowerFloors-FG348` → **160/160**（AP-fill 断言改为“不上 wire”、新增
`FloorHostableFG374` 正反例）；`TowerEntryCheck-FG374` 端到端：清完 2F 后一次推进直接到
**4F**（op29 = cell(0,1) + map 31004 + 4 怪，op143 = stage 4 + map 31004，3F 完全不上 wire）。
部署产物反射复核：`hostable 11001:3=False 4=True / 11000:45=True`、`floor4 stage=4 map=31004 count=4`
→ `DEPLOY_CHECK PASS`。2026-10-28 23:21 重启（channel pid 42904 / entrance pid 11560）。

**遗留观察点**：带 `[ai character]` 的楼层（迷妄 5/10/12/13/14/15F）的 APC 是 roleType 5——
本客户端在普通通道里是否把它当敌对目标还没实测过；如果 5F 出现“APC 不还手/打不死导致清不了层”，
下一步把塔内 APC 的 role 字节改成 8（`ApcRoleByteFG186` 一行）或做客户端 PVF 补丁给
003f/011f 补锚点。给 003f/011f 补 `[monster]`/`[ai character]` 行可以恢复 3F/9F 的内容，
但需要动共享 Script.pvf 并让客户端重启，暂缓。

## 12. 2026-10-28 23:5x 第五轮：3F/9F 不跳层了——借用死亡塔的新手房间（用户指认内容）

用户指认：3F 原生就是从 100 档随机出**一个阿拉德初阶 AI 角色**（格斗 5004 / 鬼剑士 600 / 圣职 20002）。
跳层丢内容不可接受，但 pvfUtility 的 HTTP API 实测只读（五种写载荷探测，PVF 均无变化），
共享 Script.pvf 没有安全写入通道（GUI 正被并行会话占用；已留备份
`local-run/backup-fg374c/Script.pvf.pre-fg374c`）。

**方案：借房间皮。** 死亡塔 002F/003F 的 `[ai character]` 行正好锚定 600 / 5004（lv15，`[monster]` 敌对），
而且死亡塔房间与迷妄塔房间是同一套模板（同 tile、同死神 NPC）。于是：

- `FloorMapFG374`：迷妄 3F → **30002**（600 鬼剑士），9F → **30003**（5004 格斗家）；
  stage 号、死神台词、结算仍按迷妄塔楼层走；
- `Apc-Monsters-FG` 11001 表补 `{30002,600,15,5}` / `{30003,5004,15,5}`（键是 (dungeon,map)，
  不影响死亡塔自己的 2F/3F）；
- 死亡塔自己的 5 个无锚楼层（30007/30013/30019/30024/30035）维持跳过。

**验证**：`TowerFloors-FG348` → **160/160**（借皮断言：3F/9F 可布防、地图 30002/30003、
房间 APC=600/lv15、AP 抽取仍不上 wire）；`TowerEntryCheck-FG374` 端到端：3F 的 op29 =
`cell(0,0) + map 30002 + 1 个 roleType5 APC(600/lv15)`，op143 = `stage 3 + map 30002`；
部署产物反射复核 `floor3 stage=3 map=30002 count=1 actor=600` → PASS。
23:5x 全量重编重启（channel pid 39656 / entrance pid 16232）。

**风险与回退**：002F.map 的 `[dungeon]` 写的是 11000，跨副本用图未经实机验证——若 3F 载入失败
（无 op40/闪退），把 `FloorMapFG374` 的两行借皮删掉即回到"跳层"行为。若客户端不接受跨副本图，
长期的正解是给 003f/011f 补 `[ai character]` 锚点（等 Script.pvf 有安全写入通道后做）。

## 13. 2026-10-29 02:1x 第六轮：借皮撤回 + 复用格回执按层隔离（4F 打不死怪的根因）

实机（channel-evidence.log 02:12–02:14）：3F 借皮**能玩**——op42 击杀 600 成功、`TOWER_FLOOR_CLEARED floor=3`、
死神台词按迷妄塔 3F 播放（塔内 roleType5 APC 敌对可用，5/10/12/13/14/15F 的担心解除）。但：

1. **场景污染**：跨副本切图（31002→30002→31004）时客户端不卸载上一个副本的地图对象——
   玩家看到 3F/4F 各有两个死神 NPC、4F 像两张图叠在一起（002F 的 `[dungeon]=11000` 是根因）；
2. **4F 全部击杀被拒**：`REJECTED opcode=42 reason=Reused economy receipt with different payload`——
   4F 和 2F 复用格子 (0,1)，击杀回执 `RunId-room1-kill-<uid>` 撞车（回执机制本就是重放保护），
   楼层永远清不了。

**修复**

- **撤回借皮**：`FloorMapFG374` 回到 PVF 表，迷妄 3F/9F 恢复跳层（内容问题等 Script.pvf 锚点补丁）；
  `Apc-Monsters-FG` 11001 表里的 30002/30003 借用行撤除；
- **回执按层隔离**：`RoomReceiptScope` 对塔运行追加 `-floor<N>`——击杀/掉落/物件/疲劳/宠物经验/
  `[clear map]` 记账全部按"层"而不是按"格子"，复用格子不再撞车（GM 清图的范围守卫也因此更精确）。

**验证**：`TowerFloors-FG348` → **160/160**（借皮断言翻转回"无锚点/跳层"，新增"复用格回执按层"断言）；
端到端确认清完 2F 一次推进到 4F（map 31004/4 怪/stage 4，3F 不上 wire）；部署产物反射复核
`floor3 hostable=False`、`floor4 stage=4 map=31004 count=4 scope=…-room1-floor4` → PASS。
02:20 重编重启（channel pid 15952 / entrance pid 29188）。

**当前塔形态**：迷妄之塔 1,2,4,5,…,15（3F/9F 自动跳过、约 8 秒空档）；死亡塔跳过 30007/13/19/24/35。
3F/9F 的内容恢复依赖给 003f/011f 补 `[ai character]` 锚点行（Script.pvf 写入通道就绪后一行配置即可接回，
借皮代码留档在本文档第 12 节）。

## 14. 2026-10-29 03:1x 第七轮：3F 用同副本备用图 009f_no 恢复"阿拉德初阶"层（用户指认内容）

用户指认 3F 原生=随机一个阿拉德初阶 AI 角色。PVF 里翻出了现成答案：**迷妄塔自己的未启用层图
`IllusionTower/009f_no.map`（map.lst 31009）**——同属 11001（无跨副本场景污染），且带完整
`[monster]` 锚点行：`5007 / 5004 / 5002 / 5003 / 50000` 五只（就是阿拉德初阶系列的怪物版，
客户端按图内行定位）。原生 3F 的随机出一只 vs 这张图的固定五只略有出入，但内容、层级
（basis 30）、敌对性完全符合"塔的阿拉德初阶层"。

**改动（全部同副本，无借皮）**

- `Forest-Content-FG75.Map`：迷妄塔 cell (0,2)（3F）由 31003 → **31009**（表内注释说明缘由）；
- `Forest-Content-FG75` 怪物表补 `case 31009`：五只 {5007,5004,5002,5003,50000}，lv30/kind0
  （与客户端 basis30+field08 的算法一致）；
- `FloorMapFG374`/`FloorHostableFG374`/`RoomMonstersFG118` 无需再改——都经 `Forest.Map` 自动跟随。

**验证**：`TowerFloors-FG348` → **162/162**（3F 地图=31009、可布防、复用格 roster=5 只阿拉德初阶）；
端到端：3F 的 op29 = `cell(0,0) + map 31009 + 5 怪(5007/5004/5002/5003/50000, lv30)`、
op143 = `stage 3 + map 31009`；部署产物反射复核 `floor3 map=31009 hostable=True count=5 firstMonster=5007`
→ PASS。03:1x 重编重启（channel pid 42016 / entrance pid 39684）。

**注意**：31009 不在 `Floor-Catalog-FG119.MapRolls` 里——塔内掉落只走 FG364 按怪表
（这五只不在表内=无掉落，与原生"很多塔层空掉落"一致），`TryGetValue` 缺表也不会抛。
9F（31011）仍无锚点、维持跳层；若也要内容，候选是 012f_no（31012，1060/1061 两只）。

## 15. 2026-10-29 03:4x 死神台词的"剧情暂停"节拍（notify 170，服务端可直驱）

用户要的原生节拍：进层 → 与死神对话（人物/怪物全部静止）→ 对话完开打；清层 → 再一次对话 →
几秒后推层（不点也推，只是更久）。客户端的塔模式起不来，原生的塔内对话放不出来，但
**剧情暂停**本身有服务端开关：

- rednf `Dispatcher_DungeonEventStoryPause`（dispatch_sig_142.inc）：客户端对剧情暂停的请求经
  服务端校验后广播 **notify 0xaa(170)**，包体 `{uid u16, flag1(u8 暂停态), flag2(u8)}`；
  **flag2==1 时无条件放行**（绕过活动任务 `[event dialog]` 的地图/副本校验）⇒ 服务端可以随时
  主动广播暂停/恢复，不依赖任务数据；
- 客户端静态分发表（0x390C04，步长 4，index=opcode）第 170 项 = 0x78EE4A（分配 0x5a0 的
  通知对象，ctor 0x77FC00）——ctor 会**自建剧情 UI**（字符串 0xEAF0/0xEAF1 + 控件树），
  即"剧情播放中"的载体。

**接入（FG377，Tower-Run-FG348.cs）**

- 进层载入确认后：`TOWER_STORY_PAUSE pause {uid,1,1}` → 维护循环 **4 秒**后 resume `{uid,0,1}`；
  死神"on start"台词（op12）在暂停期间（+2s）落到聊天窗；
- 清层时：台词播完立即 pause → **6 秒**后 resume → 既有的 15 秒清层计时器照旧推层
  （resume 后约 9 秒进下层；不点也推，符合"更久"的描述）；
- 层推进/塔返回（op146/EPLP）时丢弃未发的 resume，**TowerReturn 里补发 resume**，
  保证退出塔/回城时客户端绝不停留在冻结态；
- `Begin()` 清空挂起状态；维护循环按层守卫（换层即丢弃旧 resume）。

**验证**：`TowerFloors-FG348` **163/163**（Begin 清态断言）；端到端 op40 后突发 =
`ack40/op30/op142/op143/op170{uid,1,1}`，+2s 台词、+4s resume 均按维护循环发出；
check_repo 199 源 + build smoke 通过。03:4x 重编重启（channel pid 23812 / entrance pid 45764）。

**待实机确认**：客户端收到 0xaa 后的具体表现（全场冻结/剧情 UI 形态/是否需要点击）以实机为准；
若 UI 是带"继续"按钮的对话框，节奏即与原生一致。台词本身仍在聊天窗（任务对话框样式
仍是 FG371 的客户端补丁线）。

## 16. 2026-10-29 04:0x 实机反馈修正：flag2=1 是"全体恢复"，暂停要用 flag2=0

实机（用户）：notify 170 的对话框**出来了**（就是与 NPC 交接任务同款的对话 UI、带"继续"按钮），
但人物没有冻结。对照 rednf 广播语义重新核对：`Dispatcher_DungeonEventStoryPause` 的注释写明
flag2 是"resume 全体标志"——`flag2==1` 走无条件放行分支是**恢复/全员**路径，不是"强制暂停"。
上一轮把暂停发成 `{flag1=1, flag2=1}` 实际广播的是"全体恢复"，所以只有对话框、没有冻结。

**修复**：暂停/恢复都走正常路径 `{uid, flag1, flag2=0}`——
`{uid,1,0}`=该成员进入剧情暂停（全队冻结，直到 flag1 回 0）；`{uid,0,0}`=解除。
端到端确认包体 `0E-00-01-00`（uid=14, pause=1, resume-flag=0）。重编重启（channel pid 652 /
entrance pid 30560）。待实机确认冻结是否生效；若仍未冻结，则冻结开关在剧情对象之外
（dungeon event story 的脚本状态），下一层取证方向是 0xaa 对象 slot8（0xD58180，"剧情是否
进行中"查询）的调用方和 `[esi+0x174]` 句柄的来源。

## 17. 2026-10-29 04:1x flag2=0 实测无效，恢复对话框模式（冻结归剧情状态）

`{flag1=1, flag2=0}` 实测：**什么都没有**——客户端在本地校验活动任务的 `[event dialog]` 行
（rednf `CheckDungeonEventStoryPause` 的客户端对应逻辑），没有匹配任务就整包丢弃。
`{flag1=1, flag2=1}` 实测：**对话框 UI 出现**（与 NPC 交接任务同款、带"继续"按钮）但**不冻结**；
客户端没有发任何与对话框相关的 C→S 包（288=活动奖励、137=信件，均无关）。

**结论**

- notify 170 的 flag2=1 在客户端的表现是"强制显示剧情对话框"，对话框的显示/关闭完全由服务端
  驱动（`{uid,1,1}` 显示、`{uid,0,1}` 关闭）——本批恢复该模式作为塔的对话节拍；
- **游戏冻结不在这条通知里**：它属于 dungeon-event-story 的"剧情进行中"状态，客户端只在
  自己播放剧情（任务/副本脚本数据）时置位。要连冻结一起还原，需要让客户端本地有
  塔层的剧情数据（给任务或副本脚本补 `[event dialog]`/`[tower dialog]` 触发行，
  即 Script.pvf 写入通道），或继续对 0xaa 对象的 slot8（0xD58180，"剧情进行中"查询）
  与 `[esi+0x174]` 句柄来源做运行时取证。

**当前塔对话节拍**：进层 → 对话框（可点继续）→ 4 秒自动关 → 开打；清层 → 对话框 → 6 秒自动关
→ 15 秒计时器推层。重编重启（channel pid 612）。

## 18. 2026-10-29 03:2x "还是没有对话框"的排查与修正：对话框被读图条销毁（时序竞态）

"还是没有剧情对话框"那次实测（03:18，pid 612）发出的 op170 与 03:10 成功那次**逐字节一致**
（部署产物 IL 已核对：body[3]=1），客户端侧响应也一致（op288/op137 同现）。两次唯一的行为差异：
**进图到载入确认的时长**——03:10 冷加载 13 秒（场景早已就绪、读图条早已消失），03:18 热加载
仅 2.3 秒（op40 到达时读图条还在消失）。notify 170 的对话框由通知对象在收到瞬间创建，落在
读图条拆除窗口内就会被一起销毁。

**修复**：进层路径的剧情对话框不再内联发送，改为**载入确认后 1.5 秒**由维护循环发出
（`StoryPausePending/StoryPauseFloor/StoryPauseUtc`），错开读图条拆除窗口；确认后 4 秒恢复不变。
清层路径场景已稳定，维持立即发送。端到端：op40 后突发不再含 op170，+2.2s 维护循环同时发出
op170（对话框）与 op12（死神台词）。`TowerFloors-FG348` **165/165**（Begin 清态断言扩展到
armed pause）。03:2x 重编重启（channel pid 12568）。
