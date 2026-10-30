# 2026-10-28 徽章 / 副职业材料入错背包页（FG374）

部署状态：已编入 `build/stable`（195 源，`check_repo` + `build.py --smoke` 通过），双服重启后生效。
离线回归：`EmblemExpertSlotCheck-FG374` 20/20 ALL PASS；`gen_item_metadata.py verify/check`、
`gen_commerce_content.py gate` 均 exit 0。

## 1. 用户症状

- 在达芙妮处买的徽章进入「快捷栏」和「消耗品」页，而不是「装扮 → 徽章」页。
- 副职业的卡片（附魔卡片等）进入「材料」页，而不是「副职业」页。

## 2. 根因

服务端把两类道具登记成了通用堆叠物：

- `[stackable type] [avatar emblem]` → Kind 1（消耗品页 list0 57..88，购买还先填快捷栏 3..8）
- `[stackable type] [material expert job]` → Kind 2（材料页 list0 105..136）

客户端背包页是按**槽位号**画的，不看道具类型：list0 每个页 32 格，页基址
`57/105/153/201/249...`。所以 Kind 决定了道具出现在哪一页。

## 3. 原始页表（证据）

rednf `CSlotBoundChecker::init`（df_game_r 原服）按 `Inven_Item::ITEM_TYPE` 登记槽位范围：

```
insert(9,   0x38, ITEM_TYPE_EQUIP)          9..40     装备
insert(0x39,0x68, ITEM_TYPE_2)              57..88    消耗品
insert(0x69,0x98, ITEM_TYPE_3)              105..136  材料
insert(0x99,0xc8, ITEM_TYPE_4)              153..184  任务
insert(0xc9,0xf8, ITEM_TYPE_10)             201..232  副职业
insert(0xf9,0x137,ITEM_TYPE_9)              249..280  装扮→徽章
```

`CInventory::GetItemType` 把 PVF `[stackable type]` 的 m_1e0 折成 ITEM_TYPE：

```
m_1e0 == 0x14 ([avatar emblem])     -> ITEM_TYPE 9
m_1e0 == 0x02 ([material expert job]) -> ITEM_TYPE 10
```

客户端 UI 侧交叉验证：`ui/inventory/inventory_item_cn.ui` 的 tab group 1000 顺序为
装备/消耗品/材料/**副职业**/任务，格子 `[grid data] 8 4`＝每页 32 格；
`ui/inventory/inventory_avatar.ui` 的 tab group 2000 为 装扮/**徽章**。
`Economy-State-FG.cs` 里原有的 `NormalizeLegacyQuestSlots` 注释也把 201..232 称作
"subprofession page"。

## 4. 修复

### 4.1 道具域

- `data/item-metadata.xml`：215 条已登记记录改类（只改 `<Kind>` 两位数字，其余字节不动）：
  - 徽章 76 条 Kind 1 → **9**（FG352 的 2530020+ 56 条 + 达芙妮 20 条 2500000..2530004）
  - 副职业材料 139 条 Kind 2 → **10**（附魔卡片 mcard_* 与 professional/material）
  - 迁移脚本 `local-run/pvf/reclass_emblem_expert_kinds.py`（plan/apply/check，带时间戳备份）
- `local-run/pvf/gen_item_metadata.py`：证据表补 `[avatar emblem] → (9,76)`、
  `[material expert job] → (10,139)`；`QUEST_KINDS` 加 9/10；`bounds_violation` Kind 上限 10；
  `verify` 的 floor126 类别门禁加 9/10。
- `local-run/pvf/gen_commerce_content.py`：`SELLABLE_KINDS` 加 9/10（商店/收购类别）。

### 4.2 服务端

`Economy-State-FG.cs`
- `RegisterMetadata` 允许 Kind ≤10。
- `Position` 拆成 `PositionCore(...,legacy)`：
  - Kind9：list0 249..280；`legacy` 时兼容旧档的 3..8 / 57..88。
  - Kind10：list0 201..232；`legacy` 时兼容旧档的 105..136。
  - list2（仓库）对 9/10 继续放行，不破坏存仓。
- 新增 `HomePosition`（严格页）供 op19 落点校验：**玩家不能把徽章拖回消耗品页**。
- `Stackable` 加 9/10；`AddItem` 起始页 249/201；`AddPurchasedItem` 的 Kind1 快捷栏优先逻辑
  对 9/10 不生效（购买直接进专属页）。
- 新增 `NormalizeEmblemExpertSlots`，`LoadRaw` 里在 `NormalizeLegacyQuestSlots` 之后执行：
  旧档 57/105 页上的徽章/副职业材料在内存里搬进 249/201 页，下一次原子提交落盘；
  目标页满时保留旧位置（Position 兼容）而不是让存档加载失败。
- `Validate` 的背包总行数上限 637 → 700（多出两页共 64 个合法槽位）。

`Commerce-Catalog-FG71.cs`：Shop/CanSell/Offer/SalePrice 类别集合加 9/10。
`Npc-Purchase-FG44.cs`：op21 回包类别加 9/10。
`Npc-Sale-FG62.cs`：出售槽位范围加 201..232、249..280。
`Town-Delete-FG367.cs`：op18 删除类别加 9/10。
`Batch-Service-FG.cs`：地牢内丢弃（op50）类别加 9/10。
`General-Stack-FG126.cs` / `Object-Drops-FG167.cs` / `World-Drops-FG182.cs`：
掉落表类别加 9/10（世界掉落表里确有 29 个徽章 id、7 个副职业材料 id）。
`Inventory-Sort-FG76.cs`：list0 排序页加入 249（徽章页也参与整理）。
`Test-Item-Service-FG106.cs`：GM 发放 `Supported` 允许 Kind ≤10。

## 5. 验收

- `EmblemExpertSlotCheck-FG374`（`local-run/driver/EmblemExpertSlotCheck-FG374.cs`）
  20 项全过：
  - 注册：2500000/2530020=9，3600/3700=10，2749124 仍为 1；
  - 启动期全数据目录加载（world/independent/passive/tower/hell/avatar-cash/compound/coupon/
    reinforcement/object/card125/equip359/disjoint92/floor126/commerce）全部通过；
  - 购买徽章落 249/250，不再占快捷栏；普通药水仍填 3..8；
  - 附魔卡片落 201；
  - 移动到消耗品页被拒，徽章页内移动成功；
  - 旧档 58/106 槽的徽章/卡片载入后被归一化到 249/201 页；
  - 整理保持两页内的堆叠；出售/删除/GM 发放均可用。
- `python scripts/check_repo.py` → passed（428 文件 / 195 源）
- `python scripts/build.py --smoke` → compiled 195 / smoke true
- `gen_item_metadata.py verify` → VERIFY PASS；`check` → BOOT-BLOCKING missing: 0
- `gen_commerce_content.py gate` → exit 0

## 6. 残留

- 背包页归属现在与原始服务器一致；徽章的**使用侧**（开孔/镶嵌/合成）与服务端技能/副职业
  制作 op 仍未实现，仍需真实抓包确认 opcode（见 FG370 文档 §4）。
- 未登录的旧角色档在下次登录（Load）时自动归一化，不需要手工改档。
