# 铁鳞海贼团 / 舰长室 无法进入（501 / 502）

日期：2026-10-29
状态：已修复并部署到 `Server-70s3a3/build/stable`（服务已重启）

## 现象

玩家在鲁夫特悬空海港（town9 area1）点《铁鳞海贼团》进不去；服务端日志停在：

```
15:53:09.849 ENTRY_REQUEST id=502 difficulty=0 plain=F6-01-00-00-00-00-00-00
15:53:09.884 DUNGEON_BLOCK dungeon=502 difficulty=0 block=0 quest=0 rooms=0 boss=10,0
15:53:10.055 SENT dungeon-entry16 type=1 opcode=16 bytes=23
15:53:10.055 SENT dungeon-entry3  type=0 opcode=3  bytes=31
15:53:10.055 SENT dungeon-entry28 type=0 opcode=28 bytes=31
15:53:10.055 REJECTED opcode=16 reason=Unproven champion dungeon
15:53:10.055 SENT dungeon-entry-rejected type=1 opcode=16 bytes=23
```

客户端已经收到 op16/3/28 三张"入场确认"，但随后本应跟上的 op29 房间包没有发出来，
所以界面停在选图/读条前；再点一次命中 `Dungeon entry outside inactive corridor`
（此时 `dungeon.Active` 已为 true），整局卡住。舰长室 501 是同一路径、同一死因。

## 根因

`Offline70DungeonFlowFG.Begin()` 成功之后，入场流程最后一步要发 op29 房间包：

`ObjectRoomBodyFG167()` → `dungeon.RoomBody()` → `RoomMonstersFG118()`
→ `Offline70EliteMonstersFG118.DungeonBasis(dungeon, difficulty)`。

`DungeonBasis` 是按副本查 `[champion]` 五档表的硬编码 switch，FG365 把 501/502 的
房间表（`Forest-Content-FG75`）、门区（`LufthafenBody` / `SupportedInArea(9,1)`）、
`Clear-Experience-FG116.Basis`、`Monster-Experience-FG115.DungeonWeight`、
`Offline70DungeonSelection` 都接了，唯独漏了这张表，于是 default 抛
`Unproven champion dungeon`，异常把 op29 吞掉，外层只回一张 op16 拒绝包。

这也解释了为什么 `Fg365AcceptanceCheck` 全绿：原来只断言了 `SupportedInArea` 和
`ForestFG75.Monsters`，从未为 501/502 真正拼过一次房间包。

## PVF 依据

```
dungeon/quest/captainscabin.dgn  (501 舰长室, size 2x1, start 15091, boss map 15092/61248)
  [champion]  3 6 9 12 9
dungeon/quest/steelscales.dgn    (502 铁鳞海贼团, size 11x1, start 15097, boss map 15107)
  [champion]  3 6 9 12 9
```

两者与 52/53/1506/83/84/85/89/86/87/92/93 同档 `{3,6,9,12,9}`。
房源见 `local-run/pvf/export/dgn-all/501.dgn.txt`、`502.dgn.txt`。

## 改动

1. `src/Tools/VmTestKit/Elite-Monsters-FG118.cs`
   在 `DungeonBasis` 里补 `case 501:case 502:values=new int[]{3,6,9,12,9};break;`（附注释说明本次回归）。
2. `local-run/pvf/gen_dungeon_content.py`
   `DIRNAME` 增加 `501: "captainscabin", 502: "steelscales"`，以后重跑生成器不会再丢这两行。
3. `local-run/driver/Fg365AcceptanceCheck.cs`
   D 段新增：501 走一次起始房 + boss 房拼包（要求 61248 kind3 在场），502 拼起始房（要求怪物数 > 0）。
   这类"只在入场时炸"的缺表从此有离线回归。
4. `local-run/driver/SteelScalesEntryProbe.cs`（新增）
   真连服务端（登录 → 切到 town9 area1 → op16 入场 → 收 op29 → op40 载房 → op45 退出）的实机探针。

## 验收

离线（`Fg365AcceptanceCheck.exe Server-70s3a3/data`）：

```
PASS d501 舰长室 boss room composes its roster (61248 kind3)
PASS d502 铁鳞海贼团 composes its room body (monsters=10, bytes=154)
==ALL PASS passed=50 failed=0 ==
```

实机探针（重建后的 `build/stable` 服务，独立端口 + 存档副本，未触碰玩家存档）：

```
-- 501 舰长室
  recv op=16 type=1 bytes=23 / op=3 / op=28 / op=29 bytes=31
  entry501=op16 accept room=True monsters=0 map=15091
  room load -> ack
-- 502 铁鳞海贼团
  recv op=16 type=1 bytes=23 / op=3 / op=28 / op=29 bytes=175
  entry502=op16 accept room=True monsters=10 map=15097
  room load -> ack
RESULT: PASS
```

服务端日志同一时刻：

```
ENTRY_REQUEST id=501 ... DUNGEON_BLOCK dungeon=501 boss=1,0 ... FATIGUE_ENTRY_COMMITTED remaining=155 dungeon=501
ENTRY_REQUEST id=502 ... DUNGEON_BLOCK dungeon=502 boss=10,0 ... FATIGUE_ENTRY_COMMITTED remaining=154 dungeon=502
```

没有 `REJECTED opcode=16`。玩家侧要点：需要重启后的服务端（已重启）+ 客户端重新进一次
town9 area1 的选图界面。

## 残留（未在本次处理）

- 501 的 PVF 带 `[no fatigue]` / `[quest npc dungeon]`，服务端目前仍按普通副本扣 1 点疲劳；
  疲劳不阻挡进入，属于保真度缺口。
- 501 内容仍是 FG365 的占位（空起始房 + 61248 boss），剧本对话/任务 NPC 语义未实现。
- 502 的 `[event monster] 61728`、`[special passive object item] 3296` 走的是既有通用通道，
  本次未新增验收；如需可另开一轮实机确认物件与事件怪表现。
