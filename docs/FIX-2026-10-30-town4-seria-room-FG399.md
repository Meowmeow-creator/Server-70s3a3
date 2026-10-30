# FIX 2026-10-30：阿法利亚登录落在营地而不是赛丽亚房间（FG399，SeriaArea 漏区）

状态：**已编译部署，双服重启**。待实机确认。

## 1. 现象（用户 2026-10-30 报告）

上线后角色不在赛丽亚房间，而是站在城镇里；**只有阿法利亚这样**，其他城镇正常。

## 2. 根因：`SeriaArea` 手写链缺 town 4，落到默认 0

登录（以及传送药剂换城、GM 回存档城镇）都走同一条路径：

```
Batch-Service-FG.cs  World():   town=assets.SavedTown;
                                area=Offline70TownContentFG75.SeriaArea(town);
                                new Offline70Gate{Town,Area,X=474,Y=234,Direction=5,UniqueId=role.Id}
```

`Offline70TownContentFG75.SeriaArea` 是一串手写三元式，原本是

```
town==1?1 : town==2?5 : town==3?2 : town==5?1 : town==6?4 : town==9?2 : town==12?3 : 0
```

即 town 4 没有分支，直接吃最后的 `0`。而阿法利亚的第 0 区是营地，第 1 区才是赛丽亚房间：

| town | 可做登录点的区 | twn 证据 | 该区 NPC | 该区出口门 |
|---|---|---|---|---|
| 1 elvengard | 1 | `Elvengard/Gate.map` `[gate] 474 234` | 2/22/32 | `330,324,289,24 → 1/0` |
| 2 hendonmyre | 5 | `HendonMyre/Gate_Hendon.map` | 2/22/32 | `330,324,289,24 → 2/0` |
| 3 westcoast | 2 | `HendonMyre/Gate_West.map` | 2/22/32 | `330,324,289,24 → 3/0` |
| **4 alfhlyra** | **1** | **`Alfhlyra/Gate_Alfh.map` `[gate] 474 234`** | **2/22/32** | **`330,324,289,24 → 4/0`** |
| 5 stormpass | 1 | `StormPass/Gate_Storm.map` | 2/22/32 | `330,324,289,24 → 5/0` |
| 6 gent | 4 | `Gent/Gate_Gent.map` | 2/22/32 | `330,324,289,24 → 6/0` |
| 9 lufthafen | 2 | `LuftHafen/Gate_LuftHafent.map` | 2/22/32 | `330,324,289,24 → 9/0` |
| 12 shonan | 3 | `Shonan/Gate_Shonan.map` | 2/22/32 | `330,324,289,24 → 12/0` |

阿法利亚第 0 区（`Alfhlyra/Alfhlyra.map`）的 NPC 是 23/24/25/26/44，门通往 town1/3（回艾尔文防线路）
和 town4 的 1/2/3/4 区——是营地本地图，不是房间。所以 area=0 + `474,234` 就让角色站在营地里。

同样的手写链在生成器 `local-run/pvf/gen_town_content.py` 里也有一份（第 145 行），因此重跑生成器会
把这个坑写回去；历史司机 `Town4Check` 还把它钉成断言 `SeriaArea(4)==0`，等于给 bug 上了锁。

## 3. 修复（FG399）

| 文件 | 内容 |
|---|---|
| `src/Tools/VmTestKit/Town-Content-FG75.cs` | `SeriaArea` 增加 `town==4?1`；注释改成规则说明（登录区 = 该城镇 `[gate] 474 234` 所在区） |
| `local-run/pvf/gen_town_content.py` | `SeriaArea` 不再手写：从导出的 `.twn` 里取 `kind == "gate"` 的区号生成选择式，一个城镇没有唯一 `[gate]` 区时直接报错退出 |
| `local-run/driver/Town4Check.cs` | 断言改为 `SeriaArea(4)==1 && Exists(4,1)`（原 `==0` 是在钉 bug） |
| `tools/pvf/README.md`、`docs/HANDOVER-2026-10-15-town-npc-dungeon-fixes.md` | 验收口径更正与更正注 |

范围：`SeriaArea` 同时被登录、`Teleport-Service-FG94`（传送药剂换城）、`GM-Console` 的
回存档城镇使用，三处现在都落在赛丽亚房间，与其余七个城镇一致。

## 4. 验收

离线（不碰存档）：

```
python scripts/check_repo.py
python scripts/build.py --smoke
```

全源 + 小驱动，断言 8 个城镇的 `SeriaArea` 与 twn 的 `[gate]` 区一致，并对 town 4 构造
op23/op24 回包确认 `Area` 字节 = 1、`X/Y` = 474/234。

实机：角色存档城镇 = 4 的号重新登录 → 应出现在 `Alfhlyra/Gate_Alfh.map` 房间里（赛丽亚旁边），
从房间门走出去才进营地。

## 5. 残留

- 世界里没有"赛丽亚房间 → 营地"之外的区域记忆：存档只存 `SavedTown`（城镇号），不存区号，
  所以每次登录都回到赛丽亚房间是设计内的行为，与原生一致。
- 传送药剂换城同样只记城镇号，理由同上。
