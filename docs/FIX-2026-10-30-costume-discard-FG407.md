# FIX 2026-10-30：时装（装扮）可以丢弃了（FG407，装扮栏 = 物品空间 1）

状态：**已编译部署，双服 15:45 重启**（构建内含 FG401 的 attach 分流）。待实机确认。

## 1. 现象

玩家报告："时装不能丢弃也是一个问题"。装扮栏里的时装既不能在城镇删除，也不能在副本里丢。

## 2. 根因：两层都按"主背包"写死了

1. **解析层**：`Offline70DiscardProtocolFG.Parse`（op50）要求第 4 字节（物品空间）必须是 0；
   `Offline70DeleteProtocolFG.Parse`（op18）只接受 0 和 7（主背包 / 宠物窗）。时装在
   **物品空间 1**（`Offline70Economy.Position`：kind 3 → list 1 槽 0..103），所以请求在解析层
   就被判成 `bounds`，连回包都没有——客户端只看到"没反应"。
2. **语义层**：两处 kind 白名单都没有 3（`Delete unverified item kind` / `Only verified …`）。

## 3. 时装的可交易性：全库都是"不可交易"

用 FG401 的 attach 表统计装扮文件（`equipment/character/**/avatar/**`，23,731 件）：
**全部是 `[attach type] [trade]`**，没有一件 `[sealing]`/`[free]`。按玩家给的规则，
时装在副本里丢出去就是**直接消失**，城镇删除同样消失——不存在"掉在地上等人捡"的时装。

代码仍按 attach 表统一判定（`Grounds()`），所以哪天出现封装时装也会自动走落地路径。

## 4. 改动（FG407）

| 文件 | 内容 |
|---|---|
| `Item-Drop-Protocol-FG.cs` | op50 `Parse`：物品空间从"必须 0"改为 **0..7 边界**（拒绝信息带上实际值，猜错能在日志里看见）；op18 `Parse`：接受空间 **1**（时装），仍拒绝 3（已穿戴）等 |
| `Batch-Service-FG.cs` | `Discard`：kind 白名单加 **3**；`Kind 3 → list 1、槽 0..103、数量 1`；非 kind 3 的请求不许走 list≠0（`Drop space does not hold that kind`） |
| `Town-Delete-FG367.cs` | `Apply`：新增 `List==1` 分支——只收 kind 3、槽 0..103、数量 1 |
| `Attach-Types-FG401.cs` | `BagPageFG401` 改成带 list 的页表（kind 3 → list 1 0..104），`RestoreGroundFG401` 按物品的"本页"回填；`InstanceFG401` 把 kind 3 和装备一样当作必须保留实例（Ability/期限） |

## 5. 验证

`local-run/driver/EquipDrop-FG401-Check.cs` **52/52**，其中 §7/§8 覆盖本次：

- 2744037 是 kind 3 且 attach=`[trade]` → 走消失路径；
- 时装从装扮栏丢出后按实例路径还原回 **list 1**，Ability 保留；
- 城镇删除：list 1 槽 0 删除成功、槽 104 拒绝、把装备塞进 list 1 拒绝；
- 两个解析器都接受物品空间 1；op18 仍拒绝空间 3、op50 仍拒绝空间 8。

其余驱动同步更新并全绿：`QuestTabDiscard-FG400` 32/32、`TownDelete-FG367-Check` 25/25、
`Discard50Check-FG395` 21/21。`build.py` 203 源通过，`check_repo.py` 549 文件通过，
启动日志 `attachTypesFG401=63465`。

## 6. 玩家验收

1. 城镇里把装扮栏里不要的时装拖出去 → 直接消失，日志 `TOWN_DELETE_COMMITTED … list=1`。
2. 副本里丢时装 → 直接消失，日志 `DROP_VANISHED … attach=1([trade])`。
3. 如果日志出现 `Delete18 list/count bounds list=N` 或 `Drop50 request bounds list=N`，
   说明客户端给时装用的物品空间不是 1，把 N 告诉我就行（一行就能放开）。

## 7. 边界

- **已穿戴的时装（物品空间 3）不支持直接丢**：要先把时装脱下来（客户端自己会发 op18 换回装扮栏），
  再删。原版也不允许拖已穿戴的装备。
- 时装删除是**永久**的，没有回收站；商城买的时装删了不会退点券。
- 若某个"风格/套装"（`StyleOwnedFG129`/`ActiveStyleFG129`）正引用被删的时装，服务端不会自动
  取消该风格——目前没有玩家报告相关问题，先不动。
