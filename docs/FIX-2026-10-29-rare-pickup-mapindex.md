# FIX 2026-10-29：稀有以上物品拾取卡死（掉落行 mapIndex）

状态：已编译部署（check_repo 429/429、build --smoke、03:55 双服重启）；**待实机验收**。

## 现场

- 02:56：手捡泰拉石 3205（蓝），客户端发出 op46（ground 2002）；服务端正常入库（材料页 118 槽 ×1）并按原生布局回 op39（39B）+ ack46；**客户端不认应答**，拾取动作挂起，本局后续再无任何 op46。玩家看到的是"没显示拾取成功、之后什么都捡不起来"。
- 03:37 复现（灾难的征兆 3221，紫）：客户端地上**正确显示**物品名牌、角色站在物品上，但整局 **0 条 FRAME opcode=46** ——客户端本地拒绝发送拾取请求。同局同怪掉 2 件（金币 1067@2000 + 3221@2001）均正确显示。

## 排除

- 服务端拾取应答：`Offline70LootWireFG.Pickup` 在 FG375(10-28 22:08)/FG381(01:34)/02:35/当前四个构建反射调用**逐字节一致**；`PickupGroundFG201`/`Pickup`/`CollectionChanged` 方法 IL 长度零变化；wire 层（Batch-Player-Protocol-FG.cs 10-28 22:06 后）未改。
- 死亡包行宽：27B/行是 CN70 实证形状（2 件掉落的地面名牌正确显示即可反证客户端按 27B 解析）。
- 背包页：材料页 105..136（rednf `CSlotBoundChecker::init`），118 合法。

## 修复

`Offline70LootWireFG.Death` 新增 6 参重载，掉落行尾部 mapIndex 由写死 `65535` 改为真实地图索引（原生 `_MakeNotiPacketDieMonster` 每行末尾 `put_short(mapIndex)`）：

- `Batch-Player-Protocol-FG.cs`：`Death(...,ushort mapIndex)`；5 参重载保留（空 drops 调用不受影响，行宽 27B 不变）。
- `Batch-Service-FG.cs`：怪物死亡（`monster-death`）与 APC 死亡（`act4-boss-apc-died`）两处带掉落的调用传 `dungeon.CurrentMap`。

离线验证：反射构造 3221 单件掉落 @ map 10059，行尾两字节 `4B 27`、body 33 字节与旧版同长。

## 验收

重登客户端后，在副本内依次手捡：①紫色材料（灾难的征兆/灵魂晶石）②蓝色材料 ③金币。三者都应正常拾取且本局连续可捡。
若仍卡：下一嫌疑为拾取路径多发的 `ack46`（0x2e 在原生是 CommonError 的 opcode），再不然需反汇编 CN 客户端 X 键拾取的本地判定（0x390C04/0x78F176 表为 UI 层，非网络分发）。
