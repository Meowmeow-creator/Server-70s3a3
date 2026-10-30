# FG386 根特防御战首领房不出怪（2026-10-29）

玩家反馈：**根特防御战**（dungeon 89）做 `调查敌军作战力`（任务 4280，`[condition under clear] 89 1`）
时，最后的首领房（基础迷宫 `(0,1)` = map **17045**）**不刷怪也不出领主**；按原版应该是三批卡勒特
士兵之后出领主。

## 1. 房间数据（客户端 `Script.pvf`，与服务端派生表逐字一致）

`map/gentdefence/17045_boss.map`：

* `[monster]`：**0 行**（整张图没有常规怪表）
* `[special passive object]`：9 个物件 14057..14065，共 **30 条 `[monster]` 动作**
  （三组 × 三物件，坐标 710,277 / 605,154 / 694,368 三处重叠 → 就是玩家记忆里的"三波"）
* `[ai character]`：`10409 681 292 0 [monster] [boss] 2 0`（领主 Babylon，side-channel APC）
* `[event monster position]` 4 行、`[NPC] 1000 [left]`

所以这一房的全部敌人 = **30 条物件召唤怪 + 1 只 APC 首领**，没有任何 `[monster]` 行可以"按行号"摆放。

## 2. 根因：召唤怪的 actor 行缺了 native 的两个尾字节

rednf 原生链路（逐行引用）：

* `CMap::insert_monster(monster, area)`（`CMap/insert_monster.inc`）：
  `area != -1 → item.m_champion = 1; item.m_posX = area`（area = 物件在 `[special passive object]`
  里的序号）。`m_roleType` **不动**，仍是 `[monster]` 动作清空后的 0（`[normal]`）。
* `CBattle_Field::add_monsters_current_map`（`BattleMonsterDepsC2/add_monsters_current_map.inc`）：
  ```cpp
  mob.m_roleType = static_cast<char>(ref.m_roleType);   // = 0
  mob.m_field13  = ref.m_champion;                     // = 1
  mob.m_field14  = static_cast<char>(ref.m_posX);      // = area（0..8）
  mob.m_mobId    = ref.m_orderIdx;                     // = 0（Clear() 后未赋值）
  ```
* `CBattle_Field::MakePacket(0x1d)`（`BattleDungeonMapA2/MakePacket.inc`）把 `m_field13`/`m_field14`
  作为每条 actor 行的**最后两个字节**下发。

即原生包行是：`roleType=0`、`field13=1`、`field14=物件序号`、`mobId=0`。

FG375 的实现只把动作记成 `{id, level, 1}`，`Dungeon-Flow.RoomBody` 于是按普通怪写成
`roleType=1`（第 6 字节）、`field13=0`、`field14=0`、`mobId=i`（0..29）。缺了 `field14`（所属物件），
客户端既无法把这些 actor 绑到 9 个 `[special passive object]` 上，也没有 `[monster]` 行可用来摆位，
整房 actor 列表因此不生效 → **既看不到杂兵，也看不到最后那只 10409 首领**。

> 顺带解释了为什么其它房间没事：普通房 actor 行本来就是 `roleType∈{0,1,2,3}` + `0,0`，
> 与 native 一致；`17045` 是首个"整房只有物件召唤怪"的房间，FG375 之前这里只有 1 条 APC 行
> （roleType 8），所以首领一直正常。
> 也解释了 `[named monster map pos]` 的用途：d89 的 9 条 `(4,0)×3 / (2,1)×3 / (4,2)×3`
> 正是这 9 个物件序号对应的三只命名怪（59013/59015/59014）的"已在别的房间清掉就不再召唤"表，
> 三组各含一只命名怪 —— 与数据完全吻合。

## 3. 修复

| 文件 | 改动 |
| --- | --- |
| `local-run/pvf/gen_special_spawns_fg375.py` | 行格式 `{mobId, level, pct}` → `{mobId, level, pct, area}`（area = 物件序号）；magic `FG375SS2`；`map/map.lst` 索引优先读本地缓存 `export/map_lst_index.json`（不必起 pvfUtility） |
| `data/special-spawns-FG375.dat` | 重新生成：331 张图 / 916 条动作（数目不变），每行 8 字节 |
| `src/Tools/VmTestKit/Special-Spawns-FG375.cs` | 读 8 字节行；`Append` 追加 4 列名册行 `{id, level, roleType=0, area}`（roleType 与下发的字节一致，见下） |
| `src/Tools/VmTestKit/Dungeon-Flow-FG.cs` | `RoomBody`：4 列行按 native 写 `mobId=0 / roleType=row[2] / field13=1 / field14=area`；3 列行（普通/条件/事件/APC）字节不变 |
| `src/Tools/VmTestKit/Monster-Experience-FG115.cs` | `Calculate` 的行校验 `Length!=3` → `Length<3`（4 列名册行只读前三列；否则每次死亡都抛 `Monster XP actor fields`） |

产物：`scripts/build.py --smoke`（199 源，两 exe，smoke 通过），`restart-detached.ps1` 拉起，
7002 / 17001 监听正常。

## 4. 实机取证（15:13–15:14，修复第一版上线后）

玩家当场进了 d89 基础块（`DUNGEON_BLOCK dungeon=89 difficulty=3 block=0 quest=0 rooms=6 boss=0,1`）：

```
15:14:30 SENT next-room opcode=29 bytes=463
15:14:30 ROOM_LOADED room=1 map=17045 monsters=30/30
15:14:37 FRAME opcode=42 ... REJECTED opcode=42 reason=Monster XP actor fields  (uid=101)
15:14:40 ... uid=102  15:14:43 ... uid=100/109  15:14:50 ... uid=104/105/106/107 ...
```

即：**客户端真的把 30 只召唤怪显示出来了、并且在上报死亡**（这半分钟里 op42 的 uid 全是
100..115）——证明 FG386 的尾字节修复起效；但服务端把它们全拒了，根因是名册行变成 4 列后
`Monster-Experience-FG115.Calculate` 的长度校验（`monster.Length!=3`）每次都抛
`Monster XP actor fields`，于是击杀不计、房间永不清空、首领也永远不出结算。第二版补上该行校验
（并把这些行的 roleType 定成 native 的 0，让 XP/死亡包尾巴按普通怪口径走）。

## 5. 验收（离线，`local-run/driver/GentDefenceBossRoom-FG386.cs`）

驱动按服务端真实顺序走完 d89 基础迷宫（op48 组包 → op40 MarkLoaded），对每房打印房包：

```
move  cell=(1,1) map=17043 count=9  total=9  body=140
move  cell=(0,1) map=17045 count=30 total=30 body=448 actorCountByte=31
row 0 bytes=00-00-00-00-00-00-64-00-69-F1-3E-00-01-00
row 9 bytes=09-00-00-00-00-00-6D-00-85-E6-3E-00-01-02   (59013, area 2)
all: roleType=0 field13=1 seq=0, field14=0..8
apc: map=17045 id=10409 level=62 role=8
PASS every wave row is role 0 / field13 1 / seq 0 (native insert_monster(area) shape)
PASS last wave row (59014) carries area 8
PASS 30 条物件召唤怪 / PASS 10409 role-8 首领 / PASS 通关走 APC 门
（同名驱动同时逐 uid 跑 MonsterExperienceFG115.Calculate，确认 4 列行不再抛 Monster XP actor fields）
```

`field14` 与物件序号一一对应（0..2 / 3..5 / 6..8 三组，末位分别是 59013 / 59015 / 59014），
与 `[named monster map pos]` 的三格完全吻合。

## 6. 回归与残留

* `GhentBatchCheck-FG362` **120/120**（连跑两次稳定；第一次那条偶发失败是并发会话残留，与本改动无关）。
* `RoomsBossCheck-FG372` **130/130**。其中 [H] 段原断言"物件召唤怪都是 champion(kind==1)"已按
  FG386 改成"带 area 列、roleType 保持 0"——原断言把 `m_champion` 与 `m_roleType` 混为一谈。
* 实机已确认（15:14 那半分钟）：客户端能看到并上报这 30 只召唤怪的死亡；第二版只补了服务端收单。
  剩余待玩家复测的只有完整流程（清空杂兵 → 击杀 10409 → 正常结算）。
* 本轮**没有**改波次逻辑：30 条召唤怪依旧是"进房即在名册"，分批现身由客户端物件时间轴表现——
  这与 native `set_map` 一致（原生也一次 insert 完）。若实机确认三批没有分批出现，再单独取证客户端物件时间轴。
* 同类房间（331 张有 `[special passive object] [monster]` 动作的图，如 d4 b416、d31 浅栖之地）
  同步换成 native 行形，属于同类修正。
