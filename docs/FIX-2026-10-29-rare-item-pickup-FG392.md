# FIX 2026-10-29：稀有以上（品级 2/3/4/5）掉落无法拾取（FG392）

状态：**已编译、已部署、双服已在新构建上运行**（`RARITY_CATALOG_FG392 rows=40447 threshold=2 rareAutoPickup=1`）。
玩家侧验收待完成，见 §6。

## 1. 现象

副本里刷出来的紫色（稀有）及以上品质物品**走到上面按 X 捡不起来**：角色做拾取动作又收回，
物品不进包，之后本局手动拾取全部失效直到重登。白色/蓝色物品、金币、白装一直正常。

用户侧的直观描述是"服务端只接受品级 0/1 的材料"。

## 2. 根因：客户端的本地闸门，不是服务端的接受规则

`docs/INVESTIGATION-2026-10-29-rare-pickup.md` 已把这个症状钉在客户端；本次用会话日志复核并明确了分界线：

| 证据 | 结论 |
|---|---|
| 卡住的局里服务端**零条 `FRAME opcode=46`**，物品名牌正常显示、角色站在物品上 | 客户端在发包前本地撤销，服务端根本没有收到请求 |
| 全天所有**手动**（有 op46 前置帧）`PICKUP_COMMITTED`：3171/3030（rarity 1）、白装 18408/37305/27001/36315（rarity 0） | 手动拾取成功样本全部是 `[rarity]` 0/1 |
| 捡不起来的：3221 灾难的征兆、3187 灵魂晶石、3249 卡勒特干部的徽章、2749124 镶嵌栏开启装置（`[etc]`，消耗页）、3303 英雄的宿命、3324 卡勒特勋章、3300 虚空魔石碎片、7454 钻石硬币 | 全部 `[rarity]` 2；与 `[stackable type]`、页面、等级、价格、重量、图标均无关 |
| 同批 `[rarity]` 2 的 3187/3221/7454 由 **GM 自动拾取**（op39 由服务端发出）入库成功 | 客户端接受服务端驱动的拾取回包，闸门只在"玩家按键→发请求"这一段 |
| PVF 逐字段：3019 紫玛瑙 `[rarity]`1（能捡）vs 3221 `[rarity]`2（捡不起），其余字段不能解释 | 分界 = 物品 `[rarity]` |

原生服务端（rednf `CParty::get_item` / `DisPatcher_GetItem::process`）**从不读 rarity**，
所以不存在"服务端只放行 0/1"的规则，也无法靠放宽服务端校验解决；缺的是客户端那一帧请求。

## 3. 修复：品级 ≥2 的地面行由服务端代为拾取

不改客户端、不改落率、不放宽任何校验：地面行照常生成、死亡包照常下发（玩家仍能看到物品落地），
**服务端在死亡包之后自己走同一条拾取事务**（`PickupGroundFG201` → `Offline70Economy.Grant` →
op39 + 背包行），把客户端永远发不出请求的那些行收进背包。品级 0/1 保持手动拾取不变。

实现：

| 文件 | 内容 |
|---|---|
| `src/Tools/VmTestKit/Item-Rarity-FG392.cs` | 品级目录读取 + `Rarity()` / `Rare()`；`FG392RR1` 魔数、PVF 标签、行数、升序、rarity≤5 全部校验；文件缺失时保持"全部手动"（旧数据目录行为不变） |
| `src/Tools/VmTestKit/Rare-Auto-Pick-FG392.cs` | `QueueRarePickupFG392` / `PumpRarePickupFG392`：按房间 scope 入队，每 650ms 收一条；背包满时**留在地上重试**（最多 60 次，约 39 秒，腾空后继续），换房间/行已消失则记日志丢弃 |
| `src/Tools/VmTestKit/Batch-Service-FG.cs` | 启动加载目录并记 `RARITY_CATALOG_FG392`；怪物死亡、APC 死亡两条掉落路径在死亡包之后入队；主循环 `PumpRarePickupFG392()`；`ResetRoomLoot` 清队列 |
| `data/item-rarity-FG392.dat` | 40447 行（rarity0=1703, 1=3760, 2=13180, 3=1776, 4=832, 5=19196），202311 字节，sha256 `dff594e7ba0b31c633f8d99dce07466c65b4173838494eff62a0ee7779585be1` |
| `local-run/pvf/gen_item_rarity_FG392.py` | 生成器：`stackable/stackable.lst` + `equipment/equipment.lst` → 各 `.stk`/`.equ` 的 `[rarity]`，PVF 只经 pvfapi(27000) 读；`check` / `write --apply` |

格式：`FG392RR1`(8) + PVF sha(64 ASCII) + `u32 count` + count×{`u32 id`,`u8 rarity`}，id 升序。

**开关**：存档目录里放 `rare-auto-pick.off` 即整体关闭（启动日志记 `rareAutoPickup=0`），
删除该文件并重启即恢复。品级阈值是 `Offline70ItemRarityFG392.Threshold = 2`。

## 4. 不变式

- **不新增发放途径**：走的还是 `PickupGroundFG201`（op46 手动拾取的同一条事务），
  收据仍由房间 scope + 地面槽位生成，重试/重放是 duplicate，不会双发。
- **满包无损**：拾取失败抛 `Economy bag capacity`，条目留在地上并重试，日志 `RARE_PICKUP_BAG_FULL`；
  超过 60 次才 `RARE_PICKUP_DEFERRED` 放弃（物品仍在地上，和现在一样）。
- **旧档/旧数据兼容**：没有 `item-rarity-FG392.dat` 时目录不加载，一切回到手动拾取；
  目录里没有的物品（缺 `[rarity]`、gold=0、未登记）一律当作 0，不会被自动拾取。
- 掉落来源、掉率、经验、任务计数、地面容量均未改动。

## 5. 离线验证

`python local-run/driver/RarePickupFG392Check.cs`（编译产物 `local-run/build-verify-fg392/RarePickupFG392Check.exe`，42/42 PASS）：

1. 目录身份：长度=76+5×count、PVF 标签与 `Offline70QuestCatalogFG.CurrentPvf` 一致、40447 行。
2. 日志里的真实物品：3019/3171/3030=1，3221/3187/3249/2749124/7454=2，36315/18408=0；
   `Rare()` 只对 ≥2 为真；gold(0) 与未登记 id 恒为 false。
3. 变异目录（魔数、PVF 标签、count 溢出、行序、rarity>5、截断）逐个被拒；随后正常目录可重新加载。
4. 拾取事务：3221 落在材料页 105..136、数量 1；同收据重放 duplicate 且不翻倍；换收据叠到同一格；
   材料页塞满 32 格后返回 `capacity` 错误（即 pump 的重试条件）。
5. 接线：服务类型上存在 `QueueRarePickupFG392`/`PumpRarePickupFG392`，死亡路径、APC 路径、
   `ResetRoomLoot`、主循环、启动加载五处源码断言全部命中。

仓库检查 `python scripts/check_repo.py`：430 文件、201 稳定源通过；`python scripts/build.py --smoke` 通过。

## 6. 玩家验收（待做）

1. 重登客户端（服务已在 2026-10-29 20:43 用新构建重启，客户端需要重新连接）。
2. 进任意副本刷到紫色材料（灾难的征兆/灵魂晶石/卡勒特的徽章等）：物品应落地后约 0.65 秒自动进包，
   服务端日志出现 `RARE_PICKUP_QUEUED ground=… item=… rarity=2` 与 `PICKUP_COMMITTED ground=… item=…`。
3. 同局继续手动捡白色/蓝色材料与金币，应仍是弯腰拾取、不再卡。
4. 背包材料页满时再掉紫材料应看到 `RARE_PICKUP_BAG_FULL … tries=N`，腾空格子后自动进包。

已知残余（未验证，需实机确认）：客户端若因为从未"允许"过该行而在收到 op39 后仍保留地面名牌，
该名牌会在换房时消失（背包里已经有物品，不会丢）；如果实机看到"物品在地上、包里也有"的重复显示，
请把 `rare-auto-pick.off` 放回存档目录并反馈，改走"掉地时直接入包、不下发该行"的变体。

## 7. 回滚

`build/stable/DNF70-Channel-FG.exe.prev-fg392` / `DNF70-Entrance-FG.exe.prev-fg392` 是本次替换前的
运行文件（2026-10-29 20:27 构建）。改回这两个文件名并重启服务即可；数据文件保留不影响旧构建（旧源不加载它）。

## 8. 边界声明

- 本文只证明"服务端把这些行收进了背包，且事务/满包/重复/旧档行为不变"；
  **客户端为什么对 `[rarity]`≥2 不发 op46 仍未定位**（`INVESTIGATION-2026-10-29-rare-pickup.md` §3 的全部静态路径仍然有效）。
- 没有改客户端、没有改 PVF、没有改掉率；`[rarity]` 数值本身来自 CN70 PVF 原文。
- 若后续在客户端侧找到该闸门（预期是本地品级策略），应改为让手动拾取恢复，并保留本模块作为兜底或移除。
