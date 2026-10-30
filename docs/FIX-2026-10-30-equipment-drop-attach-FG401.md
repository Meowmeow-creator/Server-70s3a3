# FIX 2026-10-30：装备可丢 + `[attach type]` 决定落地/消失（FG401）

状态：**已编译部署，双服 15:02 重启**（启动日志 `attachTypesFG401=63465`）。待实机确认。

## 1. 玩家给出的机制（本次的判据）

> 装备可以在副本里丢，而且是丢了可以再捡起来那种。`[attach type]` `[trade]` 表明这个装备无法交易，
> account 为账号绑定，free 为自由交易，sealing 为封装。封装的装备都是可以在图里丢出来给其他人捡的，
> 但是帐绑 / 无法交易的道具在图里丢出去就会直接消失。在城镇里不管丢什么东西都是直接消失。

这条规则与原版解析器的取值一一对应（rednf `STEquipmentScript/ImportEquipmentScript.inc`，
`[attach type]` → `m_field8`；`STStackableScript` 同名表）：

| token | 值 | 副本内丢弃 | 依据 |
|---|---|---|---|
| `[free]` | 0 | 落地，可捡回 | `WarRoom::HandleItemDrop` switch 直达 |
| `[trade]` | 1 | **直接消失** | case 1 → `0x17`（原版拒绝；玩家语义=消失） |
| `[trade delete]` | 2 | **直接消失** | case 2 → `0x17` |
| `[sealing]` | 3 | 落地，可捡回 | case 3（原版再要求实例仍是封装态） |
| `[sealing trade]` | 4 | 落地，可捡回 | case 4 |
| `[account]` | 5 | **直接消失** | case 5 → `0x17` |

城镇删除（op18）本来就是"直接消失"（删除事务，无地面行），本次不动。

## 2. 数据：全量 attach 表

新生成器 `local-run/pvf/gen_attach_types_FG401.py`（走 pvfUtility 27000，缓存
`local-run/pvf/.attach_types_cache.json`）扫 **equipment.lst + stackable.lst 全部 63,465 个 id**，
写 `data/attach-types-FG401.dat`（magic `FG401AT1` + pvf sha256 + count + `{u32 id, u8 attach}`，
317,401 字节）。普查分布：

| 域 | free | trade | trade delete | sealing | sealing trade | account |
|---|---|---|---|---|---|---|
| 装备 50,136 | 2,933 | 41,687 | 284 | 5,160 | 72 | 0 |
| 杂项 13,329 | 2,528 | 10,105 | 683 | 0 | 0 | 13 |

即可落地装备 8,165 件（16%），其余 84% 是"丢出去就消失"的不可交易装备——这正是原版行为。
**表里没有的 id 一律按 `[free]` 处理**（= 之前的行为），目录缺一行绝不会让丢弃比现在更严。

## 3. 代码改动

| 文件 | 内容 |
|---|---|
| `Attach-Types-FG401.cs`（新） | 表加载 + `Attach/Grounds/Name`；`partial class Offline70Economy` 里的 `RestoreGroundFG401`（拾取时按实例还原）与 `InstanceFG401`（判定是否走实例路径） |
| `Batch-Player-Protocol-FG.cs` | `Offline70GroundFG` 增加 `FromBag` 标记 + 实例字段：耐久、强化 seed、改造等级、封蜡次数、封装态、Ability、到期时间 |
| `Batch-Service-FG.cs` | `Discard`：kind 白名单加 **4（装备，限背包槽 3..40、数量 1）**；按 attach 分支——可落地走原地面路径（并把实例字段写进地面行），不可落地走 `DROP_VANISHED`（op50 成功回包 + op14 增量，**不发 op40**）；`PickupGroundFG201` 改走 `RestoreGroundFG401` 并补一次全背包刷新；`Pickup` 对 `Unknown ground slot` 改为记 `PICKUP_UNKNOWN_SLOT` 并照常回 ack |
| `Economy-State-FG.cs` | `class Offline70Economy` → `partial`（只为把 FG401 的代码分文件放） |

**为什么要带实例状态**：以前地面行只有 `{slot,itemId,qty}`，捡回来是 `Grant` 新造一件——`+10`
强化、耐久、封装态全丢。FG401 让地面行携带原实例字段，拾取时原样还原（驱动的 §3 逐字段断言）。

**`FromBag` 是必须的**：只有玩家丢出来的行才带实例；怪物掉落表生成的行没有这个标记，仍走原
`Grant`（`AddItem` 会把耐久补成满值）。否则"怪物掉的装备"会被当成"玩家丢的"还原，耐久变 0。

**为什么未知地面行要回 ack**：不可交易道具丢出去是"消失"，而客户端在拖动时会先在脚下放一个
自己的预测物；以前 `Unknown ground slot` 直接抛出 → 不回 ack46 → 本局手捡卡死（FG395 回滚文档
记录的正是这个坑）。现在这种幽灵槽只记日志并回 ack。

## 4. 验证

| 驱动 | 结果 |
|---|---|
| `local-run/driver/EquipDrop-FG401-Check.cs`（新） | **40/40**：表加载 63,465 行、代表性 id 的 attach 判定、未知 id 回退 `[free]`、实例字段写入/还原/持久化/收据重放、怪物掉落仍走 grant 路径、叠类合并与到期时间保留、装备页满拒绝、`DROP_VANISHED`/`PICKUP_UNKNOWN_SLOT` 接线 |
| `QuestTabDiscard-FG400.cs` | 31/31 |
| `TownDelete-FG367-Check.cs` | 25/25 |
| `Discard50Check-FG395.cs`（契约已扩到 FG401） | 20/20 |

`python scripts/build.py` 202 源通过；`python scripts/check_repo.py` 549 文件 / 202 源通过；
启动日志 `RARITY_CATALOG_FG392 rows=40447 … attachTypesFG401=63465`。

## 5. 玩家验收

1. 副本里丢一件**封装/自由交易**的装备（白/蓝装多见）→ 掉在脚下，按 X 能捡回来，
   强化等级/耐久/封装态不变；日志 `DROP_COMMITTED … attach=3([sealing])`。
2. 副本里丢一件**不可交易/帐绑**装备 → 直接消失（背包少一件），日志
   `DROP_VANISHED … attach=1([trade])`。
3. 任务页物品（4183/3209 都是 `[trade]`）在副本里丢出去也是直接消失；城镇里删除也照旧消失。
4. 材料类同样按 attach 走：白/蓝材料（`[free]`）落地可捡，标记 `[trade]` 的材料落地即消失。

## 6. 已知限制（不是本次引入的）

- **品级 ≥ 2 的手动拾取闸门仍未解开**（FG394 §8 记录：补丁后实测仍零 op46，校验器不在栈上）。
  可落地装备里有 64% 是品级 ≥ 2（紫/粉），它们同样会掉在脚下但**捡不起来**——和怪物掉的紫装
  一样的既有问题，不是丢弃逻辑造成的。白/蓝（品级 0/1）装备完整闭环。
- 装备落地行不带强化/耐久字段，客户端地面图标可能显示为未强化外观；捡回来后数据是完整的。
- 分类里没有 `[account]` 装备（只有 13 个杂项是 `[account]`），所以"帐绑装备消失"目前用
  `[trade]`（不可交易）覆盖。
