# FIX 2026-10-30：副本内 op18 删除被整体拒绝（FG405，灵魂猎者不消耗灵魂晶石）

状态：**源码已改、离线验收 19/19 + 回归 25/25 + 31/31 通过**；未部署（双服仍在跑旧构建，
`build/stable` 被运行中的 7001/7002 占用；停服重编重启后生效）。

## 1. 现象（用户 2026-10-30 报告）

- 装备"灵魂猎者"（20143）的 20% 攻击力 buff 每 60 秒都在触发，但**背包里的灵魂晶石（3187）从
  不减少**，等于白嫖。

## 2. 这个物品的效果根本不在 PVF 里

`equipment/equipment.lst` → `20143 = character/common/amulet/neck_soulchaser.equ`，整个文件只有
名字/说明/属性/`[cool time] 10000`/`[need material] 2749212 129`，**没有 `[if]/[then]` 段，也没有
`[effect]` 或 `[consume item]`**（对照：`sswd_27155.equ` 这种"HP<10% 消耗 1 个灵魂晶石"的装备，
PVF 里是显式 `[if]…[then]` + `[consume item] 3187 1`）。

真正的实现硬编码在客户端里：

| 位置 | 作用 |
|---|---|
| 客户端物品类工厂 `0x00CC3851` | `sub eax,0x4EAF(=20143)` 命中后 `new 0x123C` + `call 0x00CBC240`（专属装备类，vtable `0x015001B4`，对象尾部 `+0x1218` 是计时器、`+0x1238` 是 buff 对象） |
| 专属类 update `0x00CBC340` | ①取全局背包 `0x018AFF58`：`call 0x00E24C80(3187)` 取数量、`call 0x00E24D60(3187)` 取槽位；②数量 `<=0` 或槽位 `==-1` 时**整段跳过**（不放 buff、不扣）；③`push 0xEA60(60000ms)` 给 `+0x1218` 计时器并挂 buff；④把 `{slot,1}` 交给 `0x00E28AD0` 请求扣除 |
| 扣除发送器 `0x00E28AD0` | 只被 3 处调用（参数 = 1/2/3/4 的 opType）。`Begin(0x12)` 后写 `{space u8, count u8, 每行 opType u16 + slot u16 + item u32 + count u32}`，灵魂猎者这一路传 **3**，城镇丢弃 UI 传 1，技能无色传 2 |

也就是说：**客户端先检查、再上 buff、最后才发 op18 删除**。服务端一拒，buff 照样生效、晶石永远不扣。

## 3. 服务端根因：op18 被当成"只许在城镇用"

`Batch-Service-FG.cs` 的 `DeleteTown` 里有一句：

```csharp
if(dungeon.Active)throw new InvalidDataException("Town deletion inside dungeon");
```

实机日志（`logs/archive/20261030-142750/channel-evidence.log`）：

```
14:18:37 FRAME opcode=18 bytes=45 seq=156 → TOWN_DELETE_REJECTED Town deletion inside dungeon
14:18:41 FRAME opcode=18 bytes=45 seq=157 → TOWN_DELETE_REJECTED Town deletion inside dungeon
14:21:31 FRAME opcode=18 bytes=45 seq=220 → TOWN_DELETE_REJECTED Town deletion inside dungeon
```

三条抓包解出来分别是 `{reason 1, slot 84, item 1205, qty 2}`（副本里删技能书）和
`{reason 2, slot 153, item 3037(无色小晶块), qty 1}`（施放前客户端自己报扣）。可见副本内
**所有** op18 都被拒，灵魂猎者只是其中最贵的一个受害者。

## 4. rednf 的原生语义

- `DisPatcher_DeleteItem::process`（ORIG `0x081C1DDA`，`DisPatcherCore/process_18.inc`）**没有任何
  城镇限制**：解包 → `check_error` → 逐行 `condition_check` → `delete_item(space, position, itemIdx,
  count, delKind)`。唯一的 `user->get_state()` 分支是 `state==5`（副本内）时把 `m_opType` 2/3/4
  记进 `CUBE_STATISTIC_FIELD 0x67/0x68/0x69`，`m_opType==1` → `delKind 5/6`，其余 → `delKind 3`。
- `GetInvenTypeFromItemSpace`：`0→1, 1→2, 2→4, 3→0, 7→3, 0x12→5`，其余 6；`m_itemSpace==1`
  （avatar）直接 `0x17` 拒绝。
- rednf **没有** 20143 / 3187 的物品级硬编码：`ImportEquipmentScript.inc` 里 `[need material]`（`0x81E`）
  只落 `m_pair28`，被 `CItem::GetNeedMaterial()` 用于 NPC 限量购买/商店材料校验；`[consume item]`
  这类 `[if]/[then]` 动作由 `EquipmentParameterInfo`（`readCondtion.inc`/`readAction.inc`，
  `[consume item]` → `0x1248`）解析，而 20143 的 `.equ` 里没有这些段。所以原版对这个物品就是
  **"客户端算、服务端只收删除包"**，服务端不该另做判定。

结论：应当按原生放行副本内 op18。

## 5. 修复（FG405）

| 文件 | 内容 |
|---|---|
| `src/Tools/VmTestKit/Batch-Service-FG.cs` | `DeleteTown` 删掉 `if(dungeon.Active) throw "Town deletion inside dungeon"`；新增 `FilterFG405` 过滤 + 日志字段（`skipped=`、`inDungeon=`）；全部行都被过滤时只回成功 ack |
| `src/Tools/VmTestKit/Town-Delete-FG367.cs` | 新增 `FilterFG405(r,out skipped)`：**跳过 `opType==2 且 item==3037`（无色小晶块）的行**——FG346 已经在 op41 施放时扣过同一枚，原样回放会一次施放扣两颗；其余行（含灵魂猎者的 `opType 3 / 3187`、`opType 1` 手动删除、`opType 4`）照常落地 |

其余校验一律不动：item 归属/数量、Kind 白名单、装备只许背包 3..40、任务页只许 153..184、
宠物窗 list 7 只许 Kind 8 且 140..188、list 只收 0/7、行 reason 只收 1..4。

## 6. 验收（离线，无 socket）

`python scripts/check_repo.py`：549 文件 / 203 源通过。

`local-run/driver/SoulChaser18Check-FG405.cs`（全源 + driver 编译，19/19 PASS）：

1. **真实抓包重放**：`F72B971D…909972`（2026-10-30 14:18:37/41 副本内那条 45 字节帧）解析为
   `{reason 1, slot 84, item 1205, qty 2}`，`FilterFG405` 不丢行，落在 `qty 3` 的存档上得到 `qty 1`。
2. **灵魂猎者形状**：`{space 0, count 1, opType 3, slot 120, 3187, 1}` 加密后解析 → 过滤保留 →
   扣 1（2→1；1→整堆消失）；背包没有该物仍照旧以 `Delete ownership/count` 拒绝。
3. **无色不双扣**：`{opType 2, slot 153, 3037, 1}` 被 `FilterFG405` 跳过（`skipped=1`、0 行落地）；
   混合帧 `{3037×1(2) + 3187×1(3)}` 只保留晶石行；同物品 `opType 1` 仍按普通删除处理。
4. **接线**：`Batch-Service-FG.cs` 里旧的 throw 已消失、走 `FilterFG405`、`DELETE18_CUBE_SKIP`
   与 `inDungeon=` 日志存在。

回归：`TownDelete-FG367-Check` 25/25、`QuestTabDiscard-FG400` 31/31 全过。

## 7. 生效前提与已知边界

- **需要停服 → `python scripts/build.py --smoke` → 重启双服** 才生效（当前 7001/7002 在跑旧构建）。
- 灵魂猎者的 20% 攻击力仍是**客户端本地结算**：服务端只负责把晶石扣掉，不再校验 buff 数值。
  如果客户端"没有晶石"时仍能触发（例如 buff 已存在期间反复刷新），服务端不会补罚——与原生一致。
- op18 只接受 `list ∈ {0,7}`。灵魂猎者发包时 byte0 取自客户端背包管理器（城镇丢弃抓包实测为 0），
  若将来出现其它 space 值，日志会以 `Delete18 list/count bounds list=N` 暴露，再按 `GetInvenTypeFromItemSpace`
  的映射补。
