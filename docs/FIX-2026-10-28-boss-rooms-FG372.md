# FIX 2026-10-28 批次：boss 房/多 boss 格/任务迷宫/[clear condition]/物件容量 (FG372)

玩家反馈五条（1 迷乱之村哈穆林、2 GBL教阿拉德分部书架、3 布万加的修炼场渐进开图、补充 幽暗密林深处无 boss + 掉落捡不起来、猛毒雷鸣废墟/烈焰格拉卡无 boss）。
全部定位到 **同一类根因的两个面**：房间表把「boss 格」解析成了普通房，以及任务迷宫层缺块。已修复并部署（7002 / 17001 已重启）。

离线验收：`local-run/driver/RoomsBossCheck-FG372.exe <data>` **104/104 ALL PASS**；
回归：`GhentBatchCheck-FG362` 120/120、`Triage-FG352-Check` 34/34、`check_repo.py` 428 文件、`build.py --smoke` 通过。

---

## 1. 猛毒雷鸣废墟/烈焰格拉卡/幽暗密林深处「进了 boss 房却没有 boss」+ 幽暗密林

**根因（生成器两处合成）**：`local-run/pvf/gen_dungeon_content.py`

1. `parse_dgn_mazes()` 把 `[boss map]` 的**整行整数**只取前两个当 (x,y)：
   `[boss map] 0 0 2 0` 被读成只有 (0,0)。d3/d4/d6/d8/d25/d31/d32… 的**第二、三个 boss 格**
   因此走上"普通格"分支；`[start map]` 同病（d3 两个起点只留一个）。
2. `derive()` 只把**第一个** boss 格当 boss 格（`boss = dgn["boss"][0]`），且 boss 格仍会吃
   `[map specification]`。原生 `CBattle_Field::get_candidator_map`（rednf
   `BattleMonsterDepsC2/get_candidator_map.inc`）对终点格只用 `gs->m_secondCandidates`
   （= `[boss map specification]`），为空才 `ChooseRandomMap(1, gridValue, dir)`（**boss 类型**表）；
   `[map specification]` 是普通格分支。

结果：boss 格指向了同坐标的**普通版本房间**（PVF 里成对存在：`416.map` 普通 / `b416.map` boss），
这种房间没有 `[boss]` 行 → 客户端在 boss 格里看不到 boss。
实测 d4 (2,0) 拿的是 416（`map/mirkwooddeep/416.map` `[type] [normal]`，7 只全 normal），
正确应是 428 `b416.map`（含 `[boss]` 行）。

**修复**：两处解析改对（`io_pairs` 取全部 (x,y) 对；boss 格集合 = 全部 `[boss map]` 行；
boss 格不再读 `[map specification]`）。用 `local-run/pvf/apply_boss_cells_fg372.py --write`
把修正落到 `Forest-Content-FG75.cs`（只改 Map 格与新增的 Monsters 行，不动任何手改）：

| 副本 | 格 | 旧(普通房) | 新(boss 房) |  | 副本 | 格 | 旧 | 新 |
|---|---|---|---|---|---|---|---|---|
| d3 幽暗密林 | (2,3) | 217 | **221** b201 |  | d27 | (4,2) | 6016 | **6018** |
| d4 幽暗密林深处 | (2,0) | 416 | **428** b416 |  | d31 | (3,0) | 4234 | **4236** |
| d5 | (3,2) | 617 | **618** b601 |  | d32 | (4,4)/(5,3) | 4416 | **4422/4421** |
| d6 猛毒雷鸣废墟 | (4,1) | 877 | **880** b831 |  | d42 | (3,0) | 9006 | **9007** |
| d7 | (3,2) | 1031 | **1028** b1002 |  | d43 | (3,0) | 9208 | **9211** |
| d8 烈焰格拉卡 | (4,2) | 1243 | **1247** b1203 |  | d50 | (3,2) | 6212 | **6210** |
| d9 | (4,2) | 1417 | **1419** b1402 |  | d80 | (4,2) | 10064 | **10066** |
| d11 | (3,2) | 1616 | **1615** (3,2)b1823 |  | d90 | (3,3) | 3910 | **3903** |
| d12 | (2,1) | 1811 | **1810** (2,1)b1830 |  | d91 | (2,0) | 3710 | **3703** |
| d13 | (1,0) | 2004 | **2005** (1,0)b2005 |  | d92 | (4,0) | 15184 | **15183** |
| d14 | (4,2) | 2217 | **2216** (5,2)b2402 |  | d1000 | (3,2) | 2825 | **2827** |
| d21 | (5,0) | 3020 | **3024** b3003 |  | d22 | (3,0) | 3246 | **3251** |
| d23 | (4,3) | 3422 | **3423** b3401 |  | d24 | (2,0) | 3629 | **3639** |
| d25 | (2,2) | 3815 | **3818** b3801(2,2) |  |  |  |  |  |

共 28 格 / 27 副。任务迷宫层（`Quest-Maze-FG361`）的 base 块由同一生成器输出，因此 d25/d31/d32/d92
的 base 迷宫也同步换成了 boss 房间（生成器重跑后自动一致）。

**"掉落捡不起来"的处置**：无 boss 的 boss 格里，客户端房间永远进不了 clear 状态，
拾取/使用一直挂起（越多捡不了越积越死）。本次除修 boss 格外另加两道防御：
- 服务端**不再把未登记物品发到地面**（`FLOOR_DROP_SKIPPED` / `OBJECT_ITEM_SKIPPED`）——
  这种地面物品永远捡不走；世界掉落表里确实有未登记 id。
- op46 拾取失败时**必回失败 ack**（原先只有 48/49 回），避免客户端拾取动作永远 pending。

## 2. GBL教阿拉德分部：破坏书架不计数、进不了下个图

**根因三条**：
1. `data/passive-objects-FG360.dat` 生成器把"与上一行相同 id"的行当重复去掉
   （`ids[-1] != value`）。原生 `CMap::set_map` 是**每行一个实例**压进 `m_passiveObjectList`，
   `DestroyPassiveObject` 每次只接受一个实例。任务 3802 的两间房：13000 连续三行 `8500`
   → 旧表容量只剩 1；13059 两行 → 2；**合计 3 < 任务要求 4**，任务永远差一格。
2. 该任务迷宫块带 `[clear condition] [destroy object] 8500 4`：原生
   `CBattle_Field::IsClearDungeon()` 在有清关条件时**短路**成 `CClearCondition::IsClearCondition()`，
   服务端完全没实现 → 3802 的 boss 房 13059 本来就没有 `[boss]` 行，`ReadyToClear` 永远 false。
3. 任务迷宫块选择本身是对的（FG361 已按 `[quest connection] 0 3802 -1` 选 2×1）。

**修复**：
- 生成器保留全部实例并按行计数 → 13000 容量 3、13059 容量 2（13058 5 个）。
- 新实现 `[clear condition]`（**仅 3 个 PVF 块带此标签**）：
  `Dungeon-Flow-FG.cs` 增加 `SetClearConditionsFG372/FeedClearConditionFG372/ClearConditionMetFG372`，
  `ReadyToClear` 在有条件时改用条件（等价原生短路）；`Kill()` 按 kind→type（3→4 hunt boss、
  其它→2 hunt monster）喂条件，怪物死亡后再喂 type 1（clear map，原生 `prepare_dungeon_clear`）；
  物件破坏路径喂 type 0，满足时走 `ClearConditionTailFG372`（MapClear + op31 clear-request）。
  数据由 `gen_quest_mazes.py` 新增的 `parse_clear_conditions()` 输出到 `Variant.Clears`。
- 效果：破坏 4 个书架 → 立刻满足清关条件；任务计数 4→3→2→1→0 正常推进。

## 3. 布万加的修炼场：只开全图、只有霍克伊当过 boss

**根因**：d141 的 `.dgn` 有 7 个 `[maze info]` 块——base 10×1（boss (9,0)=16019 布万加）+
`[quest connection] 0 4002..4007`（2/4/6/8/9/10 间，boss 分别 16020..16024/16019）。
服务端只有 `BwangaTestRunFG214` 一条硬编码覆盖了 **quest 4002**（2 间 + 16020 + 名册）；
4003 以后没有块，客户端按自己的任务选块、服务端仍发 10 间 base 迷宫 → 中间 boss 房
（16021..16024 全是 `[boss]` 房）从来不出现，能打的只有 base 里的布万加/或 4002 的霍克伊。

**修复**：`gen_quest_mazes.py` 把 141 加进 `DUNGEONS`（六个阶段块全部落表），删掉
`BwangaTestRunFG214` 的全部特判（MapFG130/BossX/名册）。现在：
4002→2 间/16020(56035) · 4003→4 间/16021(56037) · 4004→6 间/16022(56038) ·
4005→8 间/16023(56036) · 4006→9 间/16024（`[ai character] 20607 [boss]`）·
4007→10 间/16019(61806 布万加)；没有阶段任务时仍是 10 间全图。

> **FG377 更正（2026-10-28 晚）**：`.dgn` 里 `0 4007 2` 的 4007 是 **quest.lst 行 4007 =
> 天帷巨兽"唯一的通道"**，不是布万加收尾任务；真正的"英雄的考验"(hunt 141 61806) 是
> **4014**（`epic_45_bwanga_7.qst` 的 lst 行号），`.dgn` 没有它的连接行，native 语义下
> 回退 base 块（=10 间、终点布万加），本服行为一致。整条链的端到端验收见
> `FIX-2026-10-28-bwanga-chain-FG377.md`（`BwangaChainCheck-FG377` 308/308）。
> 另外"客户端按自己的任务选块"这句不准确：客户端按 op28 的 m_mazeType 字节索引自己
> `.dgn` 的块（FG374 dump 实证 `0x47A530: rowBase[page]*Y+X`）。

## 4. 迷乱之村哈穆林：没到领主房就出 boss、打死卡住、清图无效

**PVF/native 的真机制（最终版，共三轮）**

1. **房间与格子必须对齐**：客户端用**房间地图自己的 `[greed]`** 画门，服务端迷宫门的约束是
   **格子的 `[greed]`**。旧表把 (1,0) 给了只有北门的 b6416 → 进房全灭后没有西门，客户端
   **一个 op48 都不发**（现场日志 19:44:11 起），这是"打死就卡住、清图也无效"。
2. **每个 boss 格有两套房**（全库只有 3 张 `[type] [dummy]` 地图，全在哈穆林）：

   | 格 | greed | `[dummy]` 房（假皮特 1078） | `[boss]` 房（真皮特 1077） |
   |---|---|---|---|
   | (0,0) | JJ | **d6420**（8 行，1078 在 index 3） | b6422 |
   | (1,0) | FF | **d6424**（9 行，1078 在 index 3） | b6423 |
   | (0,1) | CC | d6418 | **b6416**（1077 在 index 2） |

   `1096 Fake_PeterThePiper_Second` 在 CN70 任何 `.map` 里都没有引用（旧表臆造）。
3. **服务端把他降级、客户端仍当 boss**：`CMap::set_map`（rednf `CMap/set_map.inc:55`）对
   `[type] [dummy]` 地图做 `if (mapType==2 && roleType==3) { roleType = 1; m_54 = 1; }`
   —— 服务端侧假皮特是 roleType 1（**不计入 boss 列表/通关**），但房间包
   `MakePacket`（rednf `BattleDungeonMapA2/MakePacket.inc:66`）写的是
   `put_byte(mob.m_flag34 != 0 ? 3 : m_roleType)`，**送给客户端的角色字节恒为 3**：
   客户端在追人房里看到的依然是一只 **boss**（这就是"假身在该图里是 boss 怪"）。
4. **`m_flag34` 就是"boss 标记推进"的下发通道**：`_MakeNotiPacketDieMonster`
   （rednf `CBattle_Field_KillChain/`）尾部是
   `put_byte(0)`、`apcKillCount`(或 -1)、`put_byte(mob_flag34)`，**flag34 != 0 时再追加
   `m_endX`、`m_endY`**。所以假皮特死的那个死亡包会把**新的终点(领主房)坐标**带给客户端：
   客户端把 boss 标记挪到下一间、门照常可以走、不结算。
5. **终点在 boss 格之间推进**：`MoveMap:86` → `choose_random_bossmap_from_dummymaps`
   在进入当前终点格时把 `m_endX/m_endY` 换成下一个 boss 格（并把当前格标成 dummy）；
   `[dummy appear count] 2` = 两次假身。dummy 序列用完后最后那格保留 `[boss]` 房。
   另外 `add_monsters_current_map` 里 `roleType==3 && !m_dummyBossMap` 才把 boss 计入
   `m_apcList`（只有真 boss 计数）。

**实现（`Forest-Content-FG75.cs` + `Dungeon-Flow-FG.cs` + `Batch-Player-Protocol-FG.cs`）**

- 房间：`(1,0)=d6424`、`(0,0)=d6420`（假身 1078，**名册 kind 3 = 客户端当 boss 画**）、
  `(0,1)=b6416`（真身 1077，kind 3）；服务端用 `DummyBossRowFG374`（map∈{6418,6420,6424} 且 id=1078）
  把他**排除在 boss/通关记账之外**（`IsBoss()` 返回 false、不算 VerifiedBossCount），
  但房间包照旧发 role 3，所以客户端是 boss、结算只看真身；
  `Floor-Catalog.MapRolls` 补 d6420(8 行)/d6424(9 行)，boss 行双倍金币 roll 在 index 3。
- 终点链：`HamelinChaseFG374 = (1,0)→(0,0)→(0,1)`，`BossX/BossY` 对 d51 返回**当前**终点
  （进图时 op28 的 boss 坐标 = (1,0)，即第一间追人房就先标成领主房）；
  门禁 `HamelinGateFG` 改成按链序号放行。
- 假皮特（`DummyBossRowFG374`：map∈{6418,6420,6424} 且 id=1078）死亡时：链序号 +1，
  `LastKillMovedEndFG374=true`；`KillMonster` 用 `Offline70LootWireFG.Death(...,endX,endY)`
  在死亡包尾追加新终点坐标（其它副本 endX<0，字节与以前完全一致）。
- 只有 (0,1) 的真身是 kind 3 → `IsBossRoom`+`BossKilled` 才成立，`ReadyToClear` 只在那里触发，
  结算/翻牌链路不变。

## 5. 同类普查（用户要求"查查其他副本"）

`local-run/pvf/audit_rooms_fg372.py`（新工具，可反复跑）逐副本逐格比对：
服务端 Map/Mask 与该房 `.map` 自己的 `[greed]`、boss 格是否 boss 房、boss 房是否有 kind-3 行、
名册与 PVF `[monster]` 行是否逐行一致。修前 28 格 `boss-cell-nonboss-map`；修后 **0**。
剩余 3 个"boss 房无 kind-3 boss"全部合法：
* d37 (2,0)/7027 —— 刹影 APC boss（`BossApcRoomFG176` 专用门）；
* d89 (0,1)/17045 —— `[ai character] 10409 [boss]` APC boss（`BossApcGateFG361`）；
* d140 (0,0)/16030 —— `[clear condition] [clear map] 16030`（本次接入通用清关条件）。

## 6. rednf 复核（第二轮，2026-10-28）：还补了什么 / 故意保留的偏差

把"boss 格"这批修复逐项对回 native 后，**又补了一处**并确认了若干偏差。

**补上的：通关判定改成 native 的 `check_grid_clear() && check_end_point()`**
- `CBattle_Field/IsClearDungeon.inc` = 有 `[clear condition]` 时短路成条件，否则
  `check_end_point()`；`CParty/prepare_dungeon_clear.inc` 才是真正的触发点：
  `if (check_grid_clear()) { …; if (ClearCondition(1, curMap)) ClearDungeon(); else if (check_end_point()) ClearDungeon(); }`
  —— **没有任何"必须打死 kind-3 boss"的要求**。
- 服务端原来 `BossKilled()` 要求终点房里有一条 kind-3 行且已死，因此 d140/16030 这种
  无 boss 的终点房只能靠 `DungeonId==140 && CurrentMap==16030` 硬编码。本轮改为：
  `IsBossRoom && RoomCleared`（房内无 kind-3 行时再加一条"本房 role-8 APC 也要死"），
  硬编码已删除，d89（APC boss）、d141 阶段 4006（16024）等同一形态自动覆盖。
- 顺带确认一个 native 细节：`[clear map]` 行是**进图后第一次击杀就满足**（`ClearCondition(1,curMap)`
  一为真立刻 `ClearDungeon()`），房间里的其它怪还活着也照样结算——所以 d110/3802 的
  "4 个书架即通关"和 d140/4102 的"进 16030 打一只就结算"都是原版口径。

**确认已对齐的部分**（逐条对过源码）：`get_candidator_map` 终点格只吃 `[boss map specification]`、
否则取 boss 类型表；普通格才用 `[map specification]`；`InsertMap` 的 map_type 0/2/其它 三桶；
`ChooseRandomMap` 的 `CheckEntrance(dir)` 过滤（等价于"房间 `.map` 的 `[greed]` 必须等于格的 mask"，
`audit_rooms_fg372.py` 现在 0 处不符）；dummy 机制三件套（set_map 降级 / MakePacket 恒发 3 /
flag34 死亡包带 m_endX,m_endY，`MoveMap:86` 推进终点）；`[clear condition]` 五个子标签与
`[clear map]` 右值恒 1（`ImportDungeonScript.inc:0x2a1-0x2ae`）。

**故意保留的偏差（都有理由，已记录）**
1. 终点格固定取 `[boss map]` 的第一个候选（native 是随机选一个）。native 的下场是：
   非终点的那几个候选格走普通格分支 → 拿到**普通房**——但客户端会把这些格都画成 boss 房，
   于是"boss 房没有 boss"。本服改成**每个候选格都给 boss 房**，牺牲随机性换掉空 boss 房。
2. native 在同 gridValue 的多个 boss 房里随机（d4 (2,0)：b416=428 或 b417(4,1)=462），
   本服取最小 id 固定（428）。要复刻需要给所有候选补名册 + 每局掷点。
3. native `check_grid_clear()` 只拦 `dropCnt==100 && m_field13==0` 的怪（精英不拦），
   本服要求整房清空（更严，不会提前开门）。
4. `[special passive object]` 的 `[monster]`/`[item]`/`[trap]` 子效果仍未实现（28 个新 boss 房
   里 d3/d4/d6/d8 等都有这种行；目前只走 FG167 的物件掉落池）。
5. `[start map]` 有多个候选时 native 随机，本服取第一个。

## 7. rednf 复核（第三轮）：boss 格被"每局随机房"覆盖掉的真因

上一轮留下的疑问（d4/d6/d8 明明终点格在表里就是 boss 房，为什么玩家还是看到"boss 房没 boss"）本轮查清：

- **`Offline70RandomMazeFG362` 会把 boss 格也随机掉**。它按 `[map specification]` 给每个格掷候选，
  而 d4 (0,0) 的 `[map specification]` 是 `0 0 415 413`（两张**普通**房），于是终点格每局被换成
  415/413——房间是普通房、没有 boss，但客户端照样按自己的 `.dgn [boss map]` 把它标成 boss 房
  → "进入boss房间却没有boss"。d6 (4,1)/d8 (4,2) 的同类报告也来自"另一个 boss 格是普通房"。
- **native 的终点格分支本来就该压过 `[map specification]`**：`get_candidator_map` 里终点格只查
  `m_secondCandidates`（`[boss map specification]`）/ boss 类型表，`[map specification]` 属普通格。
  修复：`RandomMazeMapFG362` 开头加一条——**终点格（BossX,BossY）直接返回 Forest 的 boss 房**，
  不再进入候选掷点。driver 新增 `[B2]` 段：27 副副本的终点格在**运行时**（`MapFG130` 一级）都仍
  解析到 boss 房，并单独钉死 d4 (0,0)=427 / d6 (2,0)=879 / d8 (3,1)=1245。

**关于"非终点 boss 格"的取舍（与 native 有偏差，已定）**

native 对非终点 boss 格走普通格分支（随机普通房）。但实测证据表明**这客户端是按自己 `.dgn` 的
`[boss map]` 标 boss 房的**：旧 d4 的终点格被 FG362 换成普通房 415/413 后，玩家仍然把它当 boss 房
并报"没有boss"。也就是说，若严格照 native 把非终点格恢复成普通房，玩家会再次看到"标着 boss 却是
普通房"（本轮的 d6/d8 报告正是那些格）。因此保留"每个 `[boss map]` 候选格都给 boss 房"：
- 终点格（BossX/BossY）的 boss 杀死才结算/出牌；
- 其余 boss 格的 boss 只是可选战，杀死不结算（与原版"多 boss 格"观感一致，且没有空 boss 房）。
若要严格 native（非终点格 = 普通房），把 `apply_boss_cells_fg372.py` 的 28 格改回复原值即可，
driver `[B]` 段会同时退化——这一条需要产品口确认。

## 8. 收紧通关判定：清空终点房不再等于通关（2026-10-28 晚间，玩家提问触发）

玩家指出一个真实风险：上一轮把通关放宽成 native 的"终点房清空即可"后，如果某副本的终点格
解析成了**普通房**（比如随机房覆盖、选错房），玩家把普通怪清掉就会**直接通关**——没有 boss 战。

已堵住：`BossKilled()` 在 `IsBossRoom && RoomCleared` 之外，**要求终点房真的拥有一条 kind-3 行**
（房间清空则它已死）。三条仍然合法的通关路径都在更前面短路：
`BossApcRoomFG176`（d37 刹影）、`BossApcGateFG361`（role-8 APC boss，d89/d141-4006）、
以及 `ReadyToClear` 的 `[clear condition]` 分支（d110/3802、d87/5866、d140/4102）。
因此：
* 普通怪清空 ≠ 通关（普通房当终点房也不会误判）；
* 非终点 boss 格里的 boss 打死 ≠ 通关（只是可选战）；
* 终点 boss 格里的 boss 打死 = 通关；
* 有 `[clear condition]` 的迷宫按条件通关（如 GBL 拆 4 个书架、16030 进房第一杀）。

driver 新增负向断言（`[F]` 段）：d140/4102 的终点房 16030 八只普通怪全清后
`ReadyToClear` 仍为 false，只有 `[clear map] 16030` 这一行满足时才通关。
`Randomization` 段同步豁免终点格（native 终点格走 boss 分支，不参与 `[map specification]` 掷点）。

验收：`RoomsBossCheck-FG372` **87/87**、`GhentBatchCheck-FG362` **120/120**。

## 9. FG375：`[special passive object]` 的物件效果（书架/木桶这类）

上几轮一直挂着的最后一块内容缺口，本轮按 rednf 实现完。

**native 事实**
- 文件格式（`STMapScript/ExportMapScript.inc:334-374`）：`[special passive object]` 里每个物件是一行
  `<objId> <m_4> <m_8> <m_c>`，紧跟 `<动作数>`，再跟每条动作
  `` `[monster]`/`[trap]`/`[item]`/`[quest]` `` + `<m_4> <m_8> <m_c> <m_10> <m_14>`。
- `CMap::set_map`（`CMap/set_map.inc:71-114`）在**房间地图载入时**逐条动作处理：
  * `[monster]`（type 0）：`m_10==0` 跳过；`rand(100) > (m_c*100)/m_10` 跳过；等级（`m_8`）须在 1..200；
    通过则 `insert_monster({id=m_4, level=m_8}, area)`，`area` = 该物件在表里的序号 → 该怪还被打上
    **champion 标记**（`insert_monster(monster, area!=-1)` 置 `m_champion=1`）。
    等级按**绝对值**取（`ref.m_flag04==0` → `m_level = ref.m_field08`）。
  * `[item]`（type 2）：`insert_item(...)` → 只登记到 `m_itemList`，由 `add_item_curr_map` 生成地面物品，
    物品池来自 `.dgn` 的 `[special passive object item]` —— 这一半本来就由 FG167 的物件掉落池实现。
  * `[trap]`（type 1）/`[quest]`（type 3）：set_map 里**没有服务端动作**（陷阱是客户端表现）。
  * `[hellparty]` 这类新标签在 set_map 里同样落到"无动作"。
- 生成出来的怪没有掉落 roll（`insert_monster` 让 `m_pair18` 两列保持 0）。

**实现**
- 生成器 `local-run/pvf/gen_special_spawns_fg375.py` → `data/special-spawns-FG375.dat`
  （331 张房、916 条动作；已内置 native 的三道守卫：`m_10==0`、等级 1..200、负概率永不触发）。
- `Special-Spawns-FG375.cs`：`Load`（边界校验）+ `Append(runId, room, map, roster)`
  —— 按 `rand(100) > pct` 逐条掷点（同一局同一房稳定），命中则以 **kind 1（champion）** 追加到房名册。
- `RoomMonstersFG118`：名册末尾追加（因此会随房间包下发、计入房间清空；不参与精英配额改写）。
- `Floor-Drops.Plan`：uid 超出该房 roll 表但仍在 `MaxSpawns` 允许范围内时按 `{0,0}`（无掉落）处理，
  与 native 一致；超出则照旧抛 `Floor map roster`。
- 引擎启动时加载。

**效果**
- d4 的 b416 这类 boss 房现在按概率带出物件守护怪（b416 有 9 条动作，5 条 100% 必出）；
- d89/17045 那种"整张图没有 `[monster]`、只有 30 条物件动作"的 boss 房终于有杂兵了；
- 所有 331 张有物件动作的房间都会按数据概率补怪。

验收：`RoomsBossCheck-FG372` **95/95**（新增 [H] 段：表规模、b416 九条动作、名册增长、
必出动作、champion 标记、同局稳定），`GhentBatchCheck-FG362` **120/120**。

## 10. GBL 书架任务仍不计数（2026-10-28 深夜，玩家复测）：客户端根本不上报物件破坏

**现场证据**（`local-run/logs/archive/20261028-232100/channel-evidence.log`，正好是玩家复测那一局）
- 23:18:04 接任务 3802；23:19:19 进副本，选块正确（map=13000，quest-maze 2×1，起点 (1,0)）；
- 整局 d110 的入站帧只有：op16 进图、op40 房间加载、op206 剧情暂停 ×2、op41 技能 ×3、op48 移动 ×74、
  op288/op537/op541/542/543 遥测 —— **没有任何 op42（死亡/物件破坏）**；
- 全部归档日志里 `PASSIVE_OBJECT_REPORT` 也是 **0 条**（历史 648 包 op42 抓包里 uid 全在 100..119）。

结论：**这个客户端不会上报被动物件破坏**，所以 native 那条
"die 包 passiveFlag=1 → DestroyPassiveObject" 的通道在这台客户端上永远不会触发 →
`[clear condition] [destroy object]` 收不到喂点，任务计数自然不动。
（另外 native `DestroyPassiveObject` 只调 `ClearCondition(0,id)`，本身也不喂任务，任务那条本来就依赖客户端上报。）

**修复：由服务端按可观测事件补记（房间清空即视为本房物件已破坏）**
- `Dungeon-Flow`：新增 `ClearConditionWantsFG372(id)`（该 id 是否被某条 `[destroy object]` 行点名）。
- `Batch-Service`：`PassiveObjectKillFG360(id, publish)` 增加发布开关；新增
  `CreditRoomDestroyObjectsFG375()`——房间清空时，把该房 `.map` 声明的、且被清关条件点名的物件 id
  逐个交给同一条记账/任务通道（`DestroyPassiveObjectFG360` 的"每房每 id 按声明次数封顶"仍然生效，
  所以不会超记、重放不会重复；`publish=false` 时清关由 `KillMonster` 原有的 `ReadyToClear` 尾巴统一发布，
  避免重复发 op31）。
- 效果（任务 3802）：13000（3 个书架）清房 → 计数 3/4、门开；13059（2 个书架）清房 → 补第 4 个 →
  任务计数满足、`[destroy object] 8500 4` 清关 → 正常结算，下一个副本解锁。
- 只对有 `[clear condition]` 的迷宫生效（d110/3802、d87/5866、d140/4102；后两者是 `[clear map]`，
  `ClearConditionWantsFG372` 直接返回 false，不受影响）。

**第三轮（玩家补述后定案）：那三只根本不是敌人**
- 玩家补述：进房触发对话、"这三个怪物好像没有敌意"、**无法造成伤害**、书柜打掉后门亮了却进不去、
  一键清图清掉它们后就能进。
- 查证：map 13000 的三条 `[monster]` 都是 **61114**，而 `monster.lst` 指向
  `NewMonsters/Gbl/LadyZealot/LadyZealot_CharacterTeam.mob`（GBL女信徒，`[attack action]
  Action/speech.act`、`[attack info]` 为空）——**`_CharacterTeam` 变体、友好场景角色**，客户端不让打，
  所以它永远不会死。我们的 `RoomCleared` 要求全房死光 → 门被这只"打不死的友好怪"永久锁死。
  普查 745 只可出场的怪：`_CharacterTeam` 只有 1 只（61114）；"`[attack info]` 为空"有 91 只
  （含 2..8 号哥布林等正常可杀怪），"speech 攻击动作"2 只，只有命名约定足够窄。
- 修复（FG376）：
  * `data/noncombatants-FG376.dat`（`local-run/pvf/gen_noncombatants_fg376.py`）——友好角色 id 表；
  * `RoomCleared` 跳过这些角色（它们仍留在名册里让客户端摆场景/播对话）；
  * 服务端的"房间清空即可破坏物件"补记改为**由客户端的移动请求触发**：客户端只有在它自己的
    `[clear condition]` 满足时才会把门点亮并请求进入，所以 `op48` 到达即视为本房物件已被破坏。
    离开 13000 → 记 3 个（任务 3/4）→ 门开；离开 13059 → 补第 4 个 → `[destroy object] 8500 4`
    满足、副本结算（此时不再放行移动，直接走清关尾包）。

**顺带**：`ROOM_LOADED` 日志现在带 `map=` 与 `monsters=当前/总数`，下次能一眼看出客户端拿到了几只怪。

验收：`RoomsBossCheck-FG372` **102/102**（新增 [C2] 段完整重放"清房→补记→4/4 通关"）。

## 改动文件

- `local-run/pvf/gen_dungeon_content.py`：`[start map]/[boss map]` 全部 (x,y) 对、全部 boss 格、boss 格不吃 `[map specification]`
- `local-run/pvf/apply_boss_cells_fg372.py`（新）：把 28 格修正落到 Forest-Content
- `local-run/pvf/gen_quest_mazes.py`：d141 入表、`parse_clear_conditions()`、`Variant.Clears`、boss 格规则同 Forest
- `local-run/pvf/gen_passive_objects_fg360.py`：保留每一行实例
- `local-run/pvf/audit_rooms_fg372.py`（新）：房间/boss 格普查
- `Server-70s3a3/src/Tools/VmTestKit/Forest-Content-FG75.cs`：28 格 Map + 23 个 Monsters 行 + d51 互换
- `Server-70s3a3/src/Tools/VmTestKit/Quest-Maze-FG361.cs`：重新生成（d141 + Clears）
- `Server-70s3a3/src/Tools/VmTestKit/Dungeon-Flow-FG.cs`：FG372 清关条件状态机
- `Server-70s3a3/src/Tools/VmTestKit/Dungeon-QuestMaze-FG361.cs`：选块后装载清关条件
- `Server-70s3a3/src/Tools/VmTestKit/Dungeon-QuestCompanion-FG130.cs`：删 d141 4002 硬编码
- `Server-70s3a3/src/Tools/VmTestKit/Batch-Service-FG.cs`：物件破坏喂清关条件 + 清关尾包、未登记掉落拦截、op46 失败回 ack
- `Server-70s3a3/src/Tools/VmTestKit/Object-Drops-FG167.cs`：物件掉落过滤未登记物品
- `Server-70s3a3/data/passive-objects-FG360.dat`：重新生成（46.8KB → 70.8KB，实例数不再被合并）

## 实机待验

1. 哈穆林：进图小地图 boss 标记应在**第一间追人房**；打死假皮特后标记**挪到下一间**、门可以走；
   连打两间假身后到 (0,1) 真皮特，打死才结算（日志里会有 `CHASE_END_MOVED ... end=X,Y`）。
2. GBL 分部：接任务 3802 进图，两间房 5 个书架，打掉 4 个任务计数推进、随即通关；能进下个副本。
3. 布万加：4002 两个房间、4003 四个 … 4007 十个，每阶段末尾都是该阶段 boss（霍克伊→…→布万加）。
4. 幽暗密林深处/猛毒雷鸣废墟/烈焰格拉卡：boss 房应有 boss（b 系列房间）。
5. 掉落：若仍有"捡不起来的道具"，看服务端日志 `FLOOR_DROP_SKIPPED/OBJECT_ITEM_SKIPPED`
   或 `REJECTED opcode=46 reason=...`——这两种现在都会显式记录。

## 已知残留（未在本轮）

- d89/17045 的 boss 房**没有杂兵**：它的怪物是 `[special passive object]` 的 `[monster]` 效果行
  （破坏物件→召唤怪物），服务端尚未实现该机制；boss（APC 10409）本身正常。
- 客户端 `[dummy appear count]` 的小地图 boss 标记是否有自己的随机链，未做客户端取证；
  服务端固定末位 (0,1) 结算（与原 FG351 一致）。原生终点是 3 个 boss 格里随机的一个，
  所以原版每次进图"真皮特在哪"可能不同；要完全复刻需要让服务端也随机选终点并把
  `EntryBody` 的 boss 坐标随 dummy 链推进下发（本机客户端对这组包尚未取证）。
- **跨 agent 冲突（不是本轮改动）**：`data/item-metadata.xml` 在 21:22 被另一路工作（FG92 徽章）
  改成叶子徽章 `Kind 9`（徽章袋 249..280），因此 `Triage-FG352-Check` 里"叶子徽章 kind 1"的
  两条断言现在会红；经济层 Kind 9 是完整支持的，属于 FG352 断言过期，需由 FG92 侧决定口径。
- `[special passive object]` 的 `[item]`/`[trap]`/`[monster]` 子行整体仍未实现（物件掉落走 FG167 的物件池）。
