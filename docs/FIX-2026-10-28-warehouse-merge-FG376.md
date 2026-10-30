# FIX 2026-10-28：仓库取材料不并入已有堆叠、每次多占一格 —— FG376 仓库传送改由服务端选行

## 症状

从角色仓库取材料到背包时，不会并进背包里已有的同一材料堆，而是**每次取都新占一格**：
同一材料在背包里越拆越多行，材料页很快被拆满。把材料放回仓库也一样，不并入仓库里已有的那一堆。

## 根因（对照原版 rednf）

- 本服务端 `Offline70Economy.Move` 把 op19 请求里的 `toSpace/toPos` 当权威落点：
  目标格是空的就新建一行（`s.Inventory.Add(new ... TargetSlot ...)`），只有目标格恰好是同一物品时才合并。
- 原版 `CUser::exchange_interspace`（`reference/rednf/remake/source/DNFServer/GameServer/Game/CUserStatC/exchange_interspace.inc`）
  在**仓库路径上根本不读 `toPos`**：
  - **case 2→0（仓库→背包）**：`getCurCharacCargoR()->get_cargo_slot(fromSlot)` 取出源行，
    `insertItemIntoInventory`（`CInventory/insertItemIntoInventory.inc`）自己做落点：
    先 `stackInvenItemIntoQuickSlot`（快捷行 3..8），再 `stackInvenItemIntoCorrespondingSpace`
    （该物品类型自己的页，`CSlotBoundChecker::init` 的区间），**整份数量必须能被一行吸收**，
    否则 `insertInvenItemIntoCoresspondingSpace` 取该页第一个空格；一行吸收不下就返回 -1 整笔失败，
    不会拆成两行（一次回复也只能描述一个落点）。真正落点由 `*outSlot` 回包，
    `DisPatcherCore/process_19.inc` 把它写进 `stMoveItemParam.m_14`。
  - **case 0→2（背包→仓库）**：`getCurCharacCargoR()->check_item_exist(item.m_addInfo)` 先找仓库里
    已有的同物品行并把 `toSlot` 改成它，随后 `tryInsertItemSpecialSlot`/`insert_item_special_slot` 合并；
    客户端的 `toPos` 只在目标格为空时保留，**被别的物品占着的格子永不交换**。
- 也就是说客户端 op19 里带的 `toPos` 是它自己的视图，不是"我要放这格"。按它落点，等于每次取出都新开一行。

## 修复设计

`src/Tools/VmTestKit/Economy-State-FG.cs`：

- `Move` 新增 `out ushort destination` 重载（旧四参签名转发，既有夹具不用改）。
- 新增 `BagPageFG376`（list 0 各 kind 的页区间，与 `Inventory-Sort-FG76` 的分页一致）、
  `CargoKind`（`PositionCore` 的 list-2 子句）、`AbsorbsFG376`（同物品且整份能放进该行）、
  `BagDestinationFG376`、`CargoDestinationFG376`。选行顺序照原版：
  同物品堆（背包先 3..8 快捷行，再该 kind 的页；仓库按槽位升序）→ 请求的合法空格 → 该页第一个空格。
  数量不拆分、目标格占着就跳过（不交换）。
- 仓库方向（`2→0` / `0→2`）先算出真实落点，再用一份覆盖了 `TargetSlot/TargetItem` 的请求副本
  走原有的合并 / 整行移动逻辑：整行移动保留原条目对象（耐久、强化、`EquipmentSeed`、封装、到期全不动），
  部分取出新建行时补带 `Ability/ExpiresUnix`（时限消耗品）。
- 顺手修：部分移动分支改用已归一化的 `quantity`，不再直接用原始 `r.Quantity`（`0=全部` 时等价，避免踩 0）。

`src/Tools/VmTestKit/Batch-Service-FG.cs`（op19）：

- 回包带真实落点（`stMoveItemParam.m_14` 语义）。`MoveForStorage` 的"空槽在前"捕获顺序会把落点
  搬进客户端原始请求的 `SourceSlot` 字段，所以先按客户端字段序改写，再交给 `ReplyOrder`。
- `ITEM_MOVED` 日志补 `from=` / `to=`，便于以后从日志直接看出落到哪一行。

## 改动文件

- `src/Tools/VmTestKit/Economy-State-FG.cs`：`Move` 两个重载 + 5 个选行辅助函数。
- `src/Tools/VmTestKit/Batch-Service-FG.cs`：op19 回包落点回填 + 日志。
- `src/Tools/VmTestKit/Economy-Fixtures-FG.cs`：新增 9 条仓库夹具，并修正 2 条过期断言（见下）。

## 验证（全部离线；未开 socket、未碰在线存档）

1. **夹具** `EconomyFixtures`：**46/46 通过**。
   - 顺带修好一件旧事：这份夹具早先一直 `NullReferenceException` 在第 38 行
     （`Find(s,0,201)` 返回 null），因为任务品页早已从 201 移到 153/154（`PositionCore`/`AddItem` 的
     Kind5 区间），断言还写 201。改成 153/154 后整份夹具恢复可跑——也就是说此前那 27 条一直没跑。
   - 新增 9 条：取出并入已有材料堆、放回并入仓库已有堆、整堆取满时改落空格、下一次取出复用刚建的行、
     "空槽在前"顺序仍落到合并行、任务品不能入仓、装备整行取出（落点=首个空格且耐久/强化/seed 不变）、
     仓库满时拒绝存入（无改动）。
2. **A/B 对照**：把 `storage` 强制为 `false`（等价修复前行为）重编 → 夹具第一条仓库断言即失败
   `warehouse withdraw merges into the existing material stack instead of opening a new row`。
3. **真实 op19 回包**（`local-run/warehouse-merge-FG376/WarehouseMoveCheck-FG376.cs`：反射直驱真实
   `Offline70BatchServiceFG.Handle(19,...)`，无 socket，一次性存档）：**7/7 通过**，解出的回包明文：
   - 取出（from=2:0, to=0:106）：`01-02-00-00-02-00-00-00-00-69-00-...` → to=0x69=105，即被合并的那一行；
   - 空槽在前顺序：`01-00-69-00-01-00-00-00-02-00-00-...` → 客户端字段序，落点回填在 `SourceSlot`；
   - 修复前同一条请求的回包是 `...-00-6A-00`（106，即客户端请求的新格）。
   - 同目录 `evidence-fg375.log` 与服务端日志同形：`ITEM_MOVED item=3012 from=2:0 to=0:105 duplicate=False`
     （两条），`*.bin` 是这两次 op19 回包原文。
4. `python scripts/check_repo.py` 通过：`repositoryChecksPassed=true checkedFiles=428 stableSources=195`。
5. 稳定编译：两个目标 **0 error**（源码隔离输出到 `local-run/tmp/fg375/out/`），无参启动返回码 2。
   `python scripts/build.py --smoke` 目前唯一报错仍是
   `CS0016: 无法写入 build\stable\DNF70-Channel-FG.exe（被另一进程占用）`——服务端在跑、客户端在线。

## 部署

落码完成、待重启生效。停服重编：

```
powershell -File local-run\one-click.ps1 -Build
```

或 `python local-run/serve.py --restart`。本轮没有停服/重启：客户端（`DNF70_v50.exe`）在线。

## 边界

- 账号金库（list `0xc`）、公会仓库本服务端没建模，未涉及。
- 仓库↔装备栏（`2→3`）保持旧行为；原版该组合直接返回 1（不支持），这次不扩大范围。
- 仓库容量档（`CargoSlotsFG129` 8..136）沿用同一规则：选行只在 `0..CargoSlotsFG129-1` 内找空位。
- 未做：客户端侧对"落点与请求不同"的视觉复检；按原版回包语义客户端应以 `m_14` 为准，
  实机验收时重点看背包里那一行数量是否正确跳变、有没有多出空行。
