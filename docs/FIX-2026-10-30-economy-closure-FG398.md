# FIX 2026-10-30 — 修理/强化破坏/分解 经济闭环 FG398

三件套（NPC 修理、+10 破坏返料、NPC 分解）的取证与修复记录。
构建：`python scripts/build.py --smoke` 201 源通过；部署：`restart-detached.ps1`（13:52）。

## 起点

FG375 之后三条链的状态：强化已是 PVF 全量目录（24965/26122），分解已换成全量参数表，
**修理仍是白名单**——`CommerceFG71.RepairCost` 要求 commerce 目录存在 `RepairValue!=0` 的行，
而这只覆盖 2416 件装备；同时 FG380 已把 op51 耐久消耗接上，玩家穿着未登记装备掉耐久后
点修理拿到 `{0,0x11}`（确认后无反应）。另外 +10 破坏档只对 1308 条"已证实分解档"放行，
29 件 `EquipSlot=0` 的武器既不进强化目录也修不了。

## 原生取证（reference/rednf，全部直读源码而非推断）

| 议题 | 原生位置 | 结论 |
|---|---|---|
| 修理请求边界 | `Store/repair_equip.inc` | 单件**没有槽位下限/上限**：msg space 0→背包、2→仓库、3→装备栏，其余 0x11；只校验行本身（`m_addInfo!=0`、`ITEM_TYPE_EQUIP`、`maxEndurance!=0`） |
| 修理报价 | `Store/getEquipmentRepairPrice.inc` | `(int)(((grade+5)*baseCost)/10) * itemRepairCost_ / maxEnd * (maxEnd-curEnd) * rate`，`baseCost`=脚本 `[value]`；`rate`=`[repair cost rate by upgrade level]` |
| 修理等级相关性 | pvf `etc/upgrade.etc` | 该表 100 项**全为 1.0** → 报价与强化等级无关（放大器同样不改变） |
| 破坏返料资格 | `CItemUpgrade/_ProcPenalty.inc` | 删物品后直接 `m_disJoint->GetResult(itemIdx)`，**没有任何 `_checkDisjointableItem` 前置**；脚本缺失才返回 0x11 |
| 破坏返料基数 | `ItemUpgradeVtableB2/GetResult.inc` | `GetSellPrice*1.1 → getItemSellPrice*1000/creationConst → *creationRate[rarity]/1000`，最小 1 |
| 破坏附加物 | `ItemUpgradeVtableB2/_getAdditionalDisjointItem.inc` | `max(0, grade-baseGrade[rarity]) < gap` 或 `minUpgrade >= up` 则不产；否则 `(long long)(pow(up-minUpgrade,2)*weight[rarity])` |
| 附加物常量 | pvf `etc/upgrade.etc [disjoint]` | `[disjoint bonus item]3243`、`[equip level const]40`、`[upgrade const for bonus item count]10`、权重 `1.0/1.2/1.5/1.0`、修正 `1/1/2/4/5/3` —— 与既存硬编码**逐项相同** |
| 低稀有度破坏线 | pvf `etc/upgrade.etc [destroy level by rarity]` | 只有 `common/uncommon = 8`，其余稀有度缺省 → 现实现 `rarity<2 && upgrade>=8` 正确 |
| 装备等级上限 | pvf `etc/upgrade.etc [max upgrade level by rarity]` | `common/uncommon = 50`，而档表只到 +30 → `[limit upgradable level]` 在本版**不可达**，无需实现 |
| 分解空间 | `DisPatcherCore/_disjoint.inc` + `CTitleBook.cpp:88` | space 经 `GetInvenTypeFromItemSpace` 映射后只校验行；穿在身上的装备是合法来源 |

`itemRepairCost_` 是全局 float（`InitScriptProducers_4.cpp:91`，由装备脚本 `[repair cost]` 写入），
本 PVF 里**没有**任何文件带该标签；现报价用的 `/(maxEndurance*10)` 形式与唯一 CN70 实测值
（16400：value160/grade1/dur20，缺 10 点报价 4）一致，等价的常数即 0.1。此项标为**模型边界**。

## 改动

1. **修理覆盖（`Commerce-Catalog-FG71.RepairCost`）**：commerce 行缺席或 `RepairValue==0` 时回退
   `data/disjoint-params-FG92.dat` 的 `[value]/[grade]`（该表 26122 行，正是强化目录的数据源）。
   已核：2416 条旧报价与过账逐条 bit 一致（`RepairValue` 恒等于参数表 `value`，零差异）。
   覆盖：18711 件"有耐久的 Kind-4"全部可报价（原 2416）；7411 件 `MaxDurability=0`
   （首饰 5389、称号 1113、辅助/魔法石 741、其余）本来就不会掉耐久，仍按 0x11 拒。
   整数截断保留：33300（value8/grade1/dur35）报价就是 0，原生同样截断成 0。
2. **修理请求边界（`Npc-Repair-Protocol-FG63` / `Npc-Repair-Core-FG63`）**：删掉自造的
   `9..40` / `11..22` 两个窗口（它们分别拒掉槽 10 和槽 22），改为 space∈{0,3} + 按 (list,slot)
   查行，查不到即 0x11。全修扫描按原生两段：装备栏 10..21 + 背包 3..8；全修遇到单件无档案
   时跳过而不是整批失败（原生循环无 per-item 中止路径）。
3. **+10 破坏档（`Break-Returns-FG177`）**：资格门从 1310 条 `Disjoint-Catalog-FG92` 改为
   "参数表有该 id"——原生 `_ProcPenalty` 本来就不做分解前置校验。可破坏装备数从 1308
   升到 24994（全部强化目标）。返料仍为 3037 基数 + 稀有度池 + 3243 附加物。
4. **分解空间（已撤回）**：本轮曾按 `_disjoint` 的空间映射放开"穿在身上的装备"，用户当天确认
   **"拆之前先脱下来"才是正确行为**、仓库装备也确实不能修，故 `Disjoint-Item-FG92` 恢复为
   list 0 + 槽 3..40 的背包限定；`[impossible contents] disjoint`(0x13)、`[trade delete]`(0x11)
   两条原版闸门不变。文献层面原版确实只校验行不校验空间，但产品行为以用户确认为准。
5. **29 件 `EquipSlot=0` 武器**：`local-run/pvf/fix_slot0_equipment_FG398.py` 逐件读 `.equ`
   的 `[equipment type]`（29/29 全为 `[weapon]`）后把 `EquipSlot` 改 10；重跑
   `gen_ordinary_reinforcement_FG176.py --apply`，强化目录 24965 → **24994**
   （= 26122 − 1128 称号，槽 11 被原生规则排除）。存档核查：29 件均不在任何角色的
   背包/装备栏里，`Position` 校验不受影响。

## 验收

- `EconomyClosure-FG398`：**21218 checks 全过**（`repairPriced=18711`、`legacyQuotes=2416`
  逐条一致、`catalog=24994`、`breakReturnable=24994`、`slot0Fixed=29`）。含端到端事务：
  35107 修满、称号/空槽拒绝且不写盘、全修一次扣费两件、35107 +10 破坏返料、
  33662 +12 返 3243×4、10004 +8 低稀有度破坏、穿着的装备拒绝分解(0x11)且不写盘、
  同一件放背包可正常分解、两种闸门码不变。
- `ReinforcementCoverage-FG375`（已同步到 FG398 期望值）：21/21 过。
- 两个 driver 都对 `build/stable`（已部署数据）复跑通过。
- `python scripts/check_repo.py`：549 文件 / 201 稳定源通过。
- 服务：`DNF70-Channel-FG`/`DNF70-Entrance-FG` 13:52 启动，7002/17001 监听，
  日志 `LISTENING`、无异常；`build-proof.json` 记录 201 源 / smoke 通过。

## 残留（明确不做的部分）

1. **仓库(space 2)修理**不做——用户 2026-10-30 确认"仓库的装备也确实不能修"，这是正确行为不是缺口
   （原生 `repair_equip` 有 cargo 分支，但产品口径以用户为准）。
2. **全修范围**只覆盖原生两段（装备栏 10..21、背包 3..8），背包其他格子的装备仍需单件修。
3. **`itemRepairCost_`** 全局常数未从 PVF 取到（本 PVF 无 `[repair cost]`），沿用 0.1 等价式。
4. **破坏附加物**：原生 `_ProcPenalty` 对非放大器装备会调用 4 参与 5 参重载各一次
   （同表同参数），此处只发一次，避免材料翻倍；5 参重载的放大器语义未还原。
5. **分解机（op251）** 仍只收背包 list 0；同一原生函数理论上也接受穿装备，但机器请求
   只有 11 字节、无抓包，不改。
6. 放大器强化（`[item amplifier]`/amp 表）整链未实现。
7. 实机验证待玩家下次点修理/强化/分解；客户端不重连不会触发这些路径。
