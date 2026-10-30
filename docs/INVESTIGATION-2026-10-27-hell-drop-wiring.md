# 深渊（hell party）掉落概率与相关逻辑：接线调查（2026-10-27）

> **2026-10-27 更新：本调查的接线已经落地（FG356 + FG359）。** 生成器 `local-run/pvf/gen_hell_drops.py`、
> 目录 `data/hell-FG356.dat`、运行时 `src/Tools/VmTestKit/Hell-FG356.cs`，入口/封印门/地狱房/组怪/
> 掉落全部接上；随后 `gen_equip_drops.py` + `data/equip-drops-FG359.dat` + `Equip-Drops-FG359.cs`
> 又补上了普通副本缺失的 type2 通用装备、type4 creature 装备和 special（领主神器/88 本配方）。
> 离线验收：HellCheck 69 项 + EquipCheck 25 项 + Act4 回归全绿。实施细节见
> [FIX-2026-10-27-hell-drop-FG356.md](FIX-2026-10-27-hell-drop-FG356.md) 与
> [FIX-2026-10-27-equipment-drops-FG359.md](FIX-2026-10-27-equipment-drops-FG359.md)。下文 §3/§4 的
> "未接" 描述保留为取证时的历史状态。

> 取证方式：pvfUtility 27000 API 直读本机 CN70 `Script.pvf`；服务端源码 grep；
> `reference/rednf/remake` 的原生逐指令转写（每条都带 ORIG 地址）。
> 本文件只做调查结论，不改代码。

## 0. 结论（TL;DR）

1. **PVF 的深渊掉落概率表存在、可读，但完全没有接线。** 服务端没有任何一处读
   `etc/itemdropinfo_monster_hell.etc` 或 `etc/hellparty.etc`（grep 0 命中），`data/`
   里也没有对应产物。
2. **不只是"概率表没接"，整条深渊链都缺失**：入场类型字节、票券/免票、hell value、
   封印门开门、地狱房地图与刷怪、hell 掉落。本地唯一落地的是**封印门数据表**
   （`Seal-Doors-FG.cs`，30 个副本）和一个被 `HellPartyModeFG181=false` 常量卡死的
   掷骰骨架。
3. **顺带纠正两条旧结论**（见 §6）：之前文档称"原版 `special/hell` 两份 `.etc`
   在 PVF 里找不到"是错的——它们就是 `etc/itemdropinfo_monseter_extra.etc`（special）
   和 `etc/itemdropinfo_monster_hell.etc`（hell），都在库里。

## 1. PVF 侧数据（已逐字段取证）

### 1.1 `etc/itemdropinfo_monster_hell.etc` —— 深渊专属掉落

```
[drop prob count] 2
[dungeon difficulty drop prob]
   1   44   0     0     0     0     0        # lo hi w[diff0..4]
   45  99   335   675   875   1000  1000
[basis of rarity dicision] 2
   0  599000  989000  993000  1000000  1000001   # 行1（hell 难度 idx=1）
   0  800000  999100  1000000  1000001  1000002   # 行2（hell 难度 idx=2）
[item drop ref table]  level -> (minOffset, maxOffset=2)
   1 0 2 / 2 1 2 / 3 2 2 / 4 3 2 / 5 4 2 / 6 5 2 / 7 6 2 / 8..99 6 2
```

语义（原生 `CMonsterDrop_Hell`，sizeof 0x237C0）：

- **等级门槛**：怪物标准等级 1–44 五个难度权重全是 0（**低等级深渊不掉东西**），
  45–99 才启用。
- **掉落概率**：`threshold = w[difficulty] * dropRate / 100`，
  `rand(0,1000) <= threshold` 才掉。单人去深渊（dropRate=100）时约
  33.5% / 67.5% / 87.5% / 100% / 100%，对应难度 0..4。
- **稀有度**：`rand(1,1_000_000)` 走 `[basis of rarity dicision]` 的**第 1 或第 2 行**
  （行号 = hell 难度 idx，由 `[difficulty]` 权重表只可能取 1/2）：
  - 行1：59.9% r0 / 39% r1 / 0.4% r2 / 0.7% r3 / 1e-6 r4；
  - 行2：80% r0 / 19.91% r1 / 0.09% r2 / 1e-4 r3 / 1e-4 r4。
  - 幸运点（dropType=2）会挪动 r1/r2 边界（`CLuckPoint::GetItemRarity`）。
- **物品池不是显式列表**：和普通掉落一样由 `CItemGenRateTable::generateTable`
  从全量 item grade 列表合成——按 `[item drop ref table]` 的等级窗口
  `L-minOffset .. L+maxOffset-1`（深渊为 **L-6..L+1**）、rarity、`_itemTypeVerify(type)`、
  `[creation rate]`（`CItem::get_gen_rate()`，缺省 0 即不入池）加权。
  `chooseItem(eItemDropType_2, rarity, 标准等级)` = **装备池**。

### 1.2 `etc/hellparty.etc` —— 深渊全局参数与刷怪组

- `[hellparty basic values]`：max base 100 / min base 1 / base 1 / base rising 5 /
  sub base rates 1 / level difference rates 10000 5000 2000 /
  difficulty rates 2000 4000 4500 6800 6800 / party rates 0 1800 4000 8000 /
  ancient dungeon add rate 8500 / guide line value 63。
- `[difficulty]`（5 行 A..E，原生映射到 `stHellParty_t.m_field6c/72/7e/84/8a[1..5]`）：

  | 行 | 每次击杀 hell 掉落 roll 次数 | 怪物数/奖励缩放 | 随机深渊难度权重 | 强制深渊难度权重 | event |
  |----|------|------|------|------|------|
  | A | 8 | 5 | 3 | 60 | 0 |
  | B | 6 | 4 | 97 | 40 | 0 |
  | C | 4 | 3 | 0 | 0 | 0 |
  | D | 4 | 3 | 0 | 0 | 1 |
  | E | 4 | 3 | 0 | 0 | 1 |

  两个难度权重列都是累加百分比：`m_field7e` 3/97 → 3% 难度1、97% 难度2；
  `m_field84` 60/40 → 60% 难度1、40% 难度2。**hell 难度实际只取 1/2**，
  正好对上两条 rarity 曲线。
- `[hellparty monster group]`：60 组（index 1..107、121..128、180..184、500、501），
  每组一个字母（A/B = 难度槽）、若干 `monsterId flag`（flag=1 → roleType 5 = AI
  character/APC；flag=0 → 普通怪，如 413、50068）。10618/15603 等不在
  `monster.lst` 里，是 APC。

### 1.3 世界图 `.wdm` 的深渊段（客户端选深渊/票券的数据源）

10 个 worldmap 带 `[hell dungeon] 1`：antberr、aradcartel、charnak、gblgrim、
lorien、lufthafen、moonlightpub、noirpera、northmyre、underfoot。每份还有：

- `[hell quest]` 2610/2613/2622/2603（通关计数任务，决定 hell quest 列表）；
- `[hell freepass item]` 2749501/2749535/2682582/2749403/8272/8376/8247（免票道具）；
- `[item condition]` 4158/4152/4393/4394/4356（封印门道具，每样 1 个）；
- `[kinggoldenhorn kingsilverhorn condition]` 2680998（金角/银角，另一条线）。

### 1.4 副本 `.dgn` 的封印门与地狱房

`[seal door map index]/[seal door pos]/[seal door appear rate]`，本地已生成
`Seal-Doors-FG.cs` 全表（30 条）。例：33→60026 (1,0) rate10；36→60020 (3,2) rate1；
1502→60036；44→60029；1506→60053。

地狱房 `.map`（`map/*/hell_*.map`，90 份）里 `[special passive object]` 带 30519
对象和 `[hellparty]` 段，行格式 **`groupIdx rate orderCnt`**，例
`map/underfoot/hell_underfoot.map`：`125 25 1 / 126 25 1 / … / 3 20 2 / 500 50 1`。
orderCnt 是组序键；每组的难度槽来自 `hellparty.etc` 里该组的字母 A..E。

## 2. 原生完整链路（rednf 逐指令证据）

入口/激活：

- 客户端 **op16 SelectDungeon** 请求 = `u16 dungeonIdx + s8 难度 + s8 ENUM_DUNGEON_TYPE +
  u8 partyType`（`Game/PacketTypes.h:1581 MSG_SELECT_DUNGEON`、
  `Game/DisPatcherCore/read_17.inc`）。`ENUM_DUNGEON_TYPE == 1` 即深渊模式。
- `CParty::dungeon_start`（`Game/CParty/dungeon_start.inc:6`，ORIG 0x085a0954）：
  `CheckHaveHellPartyPassItem` → `check_random_appear_hell_dungeon` → `SelectDungeon`；
  进门后 `dtype==1 && m_sealDoorFlag` 时 `UseSealDoorItems`，失败整队拒绝。
- `CheckHaveHellPartyPassItem`（0x085a7b8a）：`[hell dungeon] 1 + type==1` 时
  走 `CheckDestroyConditionSealDoor`（0x085a7d86，逐成员查 `[item condition]`，
  minLevel>44、`getHellPartyEntranceItemNeedCount` 个），成功置 `m_sealDoorFlag`，
  失败回 0x10/0x1b 错误包（缺哪些道具）。
- `UseSealDoorItems`（0x085a8038）：有 `[hell freepass item]` 的成员免扣，否则逐成员
  扣 `[item condition]` 道具；`getHellPartyEntranceItemNeedCount` 决定数量。
- `CParty::cal_hellparty_value`（0x085b58ac）：每次开局累积
  `total = base_rising(5) × levelDiffRate × (difficultyRate + 1 + ancientAdd(0.85) + partyRate) + old`，
  上限 `[max base value]`=100；单人开局初值 = `[base value]`=1。
  `SelectDungeon`（0x082ff486）：`type==1` 强开深渊；否则要
  `isHellParty && memberCount != 1 && CheckHellPartyCondition()`
  （`total >= rand(100) && total >= [guide line value](63)`）——**单人不能随机遇深渊，
  只能显式选深渊模式（type=1）**。
- 客户端显示：`CParty::game_start` 的 op **0x1b(27)** 带 `int hellValue + 深渊任务列表`；
  值变化用 op **0xcf(207)** 广播（0x085b697c）。

刷怪与门：

- `add_monster_hell_party`（0x08301b8c）：`m_hellPartyActive != 0` 且当前地图有
  `get_hellparty_groupOrder_map(mapIndex)` 时 `add_hell_party_monster`。
- `add_hell_party_monster`（0x083071ba）：按 `m_hellPartyDifficulty` 取该 order 组总权重
  掷点，选中 groupIdx → 从 `hellparty.etc` 组表生成怪；写
  `m_hellPartyGroupIdx/+0x334`、`m_hellPartyGroupOrder/+0x336`、
  `m_field11b0 = m_field6c[diff]`、`m_field11b1 = 1`、`m_hellPartyScriptIdx = diff`、
  roleType 0/5。
- `procSpecialPassiveObject`（`STMapScript/procSpecialPassiveObject_1.inc`）解析
  `[hellparty]` → 每 map 的 group-order 池。

掉落（核心）：

1. `_MakeDropItems`（0x0830adf6，`CBattle_Field_KillChain/_MakeDropItems.inc`）写 ref：
   随机阶段 `m_field41 = getItemDropDungeonType()`；第二遍覆盖为
   `m_field41 = (mob.hellPartyGroupIdx != 0)`、`m_field42 = hellPartyScriptIdx`、
   `m_field35 = 副本标准等级`、`m_rateSlot = 难度`、`m_field3d = m_field11b0`、
   `m_field40 = killedAllGroups`。
2. `CMonsterDrop::generateSpecificItem`（0x08537070）：**`m_field41 != 0` → 
   `m_hell.hellPartySpecificItem`**。
3. `hellPartySpecificItem`（0x08535b08）：先做 AI/怪物的 independent drop / avatar /
   catch；`m_field40 != 0`（本房地狱组全灭）时循环 `extraCount` 次
   `CMonsterDrop_Hell::generateSpecificItem`。`extraCount = ref.m_field3d`，
   但 `.mob` 带 `[hell monster] 1`（`script.m_flag3a8`，`IsHellMonster()`）的怪为 0
   ——即**地狱组召出的 APC/怪才是 hell 掉落主体**；实测 413/50068/61704 等都没有
   该标签。
4. `CMonsterDrop_Hell::generateSpecificItem`（0x08535726）：`_getGenItemProbIdx(标准等级)`
   → `[dungeon difficulty drop prob]` 行 → 阈值 roll → rarity →
   `m_table.chooseItem(eItemDropType_2, rarity, 标准等级)` → 掉装备。
5. 另外 `CMonsterDrop::generateRandomItem`（0x08536da8）在 `m_field40==0` 时还会跑
   `CSpecialMonsterDrop::_generateRandomItem`（= `itemdropinfo_monseter_extra.etc`），
   地狱怪也吃这一路（随机装备列），所以这两张表是**同一批工作**。

## 3. 本地服务端现状（逐条证据）

| 环节 | 现状 | 证据 |
|---|---|---|
| hell 掉落概率表 | **未接**：无 reader、无 `data/*.dat` | `grep -r itemdropinfo` 仅命中 monseter/clearreward 注释；`data/` 无 hell 产物 |
| hellparty.etc | **未接**：无 hell value、无难度权重、无怪物组 | 源码无 `hellparty`/`m_field6c` 类结构 |
| 入场类型字节 | **拒绝**：op16 只读 id+难度，`plain[3..7]` 必须全 0 | `Batch-Service-FG.cs:435 EntryDungeon`（native 这 5 字节是 type/partyType，塔是唯一豁免） |
| op27（hell 值/任务） | **写死 13 字节**（与全零 fixture 同型），无 hellValue/hell quest | `Dungeon-Selection-Protocol.cs:19` + `Reply(27)` 固定密文 `67dda576…` |
| hell value 累积 | 无 | 无 `cal_hellparty_value` 对应实现 |
| 封印门 | 表有、掷骰有、**恒关** | `Dungeon-Flow-FG.cs:150-163`：`HellPartyModeFG181=false`，`sealDoorOpen = roll<rate && false` |
| 封印门道具/免票 | 无 | 未解析 `[hell freepass item]`/`[item condition]` |
| 地狱房地图 | **不服务** | 60026/60020 等不在 `FloorCatalogFG119.MapRolls`；`Act4-Mechanics-Check-FG.cs:138-148` 明确断言 "hell map 60026 not served" |
| 地狱刷怪 | 无 | 无 `[hellparty]` 组序解析、无 `add_hell_party_monster` 等价物；`Dungeon-QuestCompanion-FG130.cs:33-59` 只是把深渊变体 boss/5310 做成任务触发的替代 |
| 普通装备池（pool2） | **也没接** | `Floor-Catalog-FG119.Probability` 行 = monseter 表（`1 15 986 60 309 15 30`），但只消费 row[2]（金币）与 row[3]/[5]（pool1/3）；row[4]=309 装备列、row[6]=30 creature 列从未使用；`gen_floor_stack.py` 也只产 pool1/3 |
| special 装备池 | **未接** | 无 `itemdropinfo_monseter_extra` 产物 |

现有已接的掉落源（可复用其基础设施）：`itemdropinfo_monseter.etc`（FG126/FG119）、
`clearreward`（FG125 翻牌）、`independentdrop`（FG355）、`worlddrop`（FG182）、
`object`（FG167）。唯一缺的是"装备类池"（pool2/special/hell），三者共用
`CItemGenRateTable::generateTable` 语义。

## 4. 若要接线：工作分解

前置（数据层，全部可离线从 PVF 生成，`gen_card_items.py` 已证明装备 rarity/creation
rate 可解析）：

1. `gen_hell_drops.py`：解析
   - `etc/itemdropinfo_monster_hell.etc`（2 行等级档、2 行 rarity、等级窗口）；
   - `etc/hellparty.etc`（basic values、difficulty、monster group）；
   - 30 个 `.dgn` 的 seal door（已有）+ 10 个 `.wdm` 的 hell 段；
   - 90 份 `map/*/hell_*.map` 的 `[hellparty]` 组序；
   产出 `data/hell-*.dat`（magic+PVF sha，沿用 FG126 模式），并按
   `generateTable` 语义生成 (rarity, level) → 装备池表（可直接复用 FG126/FG125
   的构建代码，只换 `[item drop ref table]` 的窗口与 rarity 行）。
2. 运行期（按依赖顺序）：
   - op16 接受 `m_field10 == 1`；新增 hell 状态机（value 累积/扣除、op27 首包、
     op207 广播）；
   - 票券/免票/封印门道具校验与扣除（`[hell freepass item]`/`[item condition]`）；
   - 封印门路由打开（`HellPartyModeFG181` → 运行时开），地狱房 map 加入 roster
     （含 `[hellparty]` 刷怪，roleType 5 走现有 APC 房间花名册）；
   - 击杀链：`hellPartyKilled`（本房地狱组全灭）→ 每组怪 8/6/4 次 hell 表 roll；
   - 同时补 `[hell monster]`（`.mob` flag）排除和 special 表（随机阶段），否则
     地狱房里的普通怪/装备掉率依旧不对。
3. 回归/验收：离线 driver 钉概率（固定落地 receipt 的确定性抽样），再做一次真人
   深渊房走查（现有 `Act4-Mechanics-Check` 的封印门断言需要同步改）。

风险/注意：

- 深渊掉落是**装备池**，需要 `[creation rate]` + rarity 的 item universe；这同时
  把"高等级只掉金币不掉装备"（pool2）问题一起解决，建议同批做。
- `[difficulty]` 只有 A/B 两档可用（C/D/E 权重为 0），rare 曲线只有两行，别按 5 档
  实现。
- 单人只能走 `type==1` 的强制深渊；随机遇深渊要求 ≥2 人（原生代码限制）。
- 客户端侧 UI（深渊按钮/能量条）由客户端的 PVF + op27 驱动；服务端不接 op27 时
  客户端显示为 0，但不会崩（现状即如此）。

## 5. 关键代码/数据索引

PVF（`local-run/pvf/export-hell/` 已有本地导出）：
`etc/hellparty.etc`、`etc/itemdropinfo_monster_hell.etc`、
`etc/itemdropinfo_monseter_extra.etc`、`worldmap/*.wdm`、`dungeon/**.dgn`、
`map/*/hell_*.map`。

本地服务端：
`src/Tools/VmTestKit/Dungeon-Flow-FG.cs:150-163`、
`Seal-Doors-FG.cs`、`Dungeon-Selection-Protocol.cs:19`、
`Batch-Service-FG.cs:435/451/452`、`Act4-Mechanics-Check-FG.cs:138-148`、
`Floor-Drops-FG119.cs`、`General-Stack-FG126.cs`、`Dungeon-QuestCompanion-FG130.cs:33-59`。

原生参考（`reference/rednf/remake/source/DNFServer/GameServer/Game/`）：
`BattleDungeonMapA2/SelectDungeon.inc`、`BattleDungeonMapA2/check_random_appear_hell_dungeon.inc`、
`CParty/dungeon_start.inc`、`CPartyDepsA2/CheckHaveHellPartyPassItem.inc`、
`CPartyDepsA2/UseSealDoorItems.inc`、`CParty/CheckDestroyConditionSealDoor.inc`、
`CPartyDepsD2/cal_hellparty_value.inc`、`CPartyDepsD2/sub_hellparty_value.inc`、
`CPartyDepsD2/send_hellParty_value.inc`、`CParty/game_start.inc`、
`BattleMonsterDepsC2/add_monster_hell_party.inc`、`BattleMonsterTailC3A/add_hell_party_monster.inc`、
`STMapScript/procSpecialPassiveObject_1.inc`、`CBattle_Field_KillChain/_MakeDropItems.inc`、
`WongWorkDrop_A/generateSpecificItem_2.inc`、`WongWorkDrop_B/hellPartySpecificItem.inc`、
`WongWorkDrop_B/generateSpecificItem.inc`、`DropLayoutDeps/loadScript.inc`、
`DropLayoutDeps/generateTable.inc`、`CClearReward/GetItemRarity.inc`、
`CtorProducers_2C/initialize.inc`、`InitScriptProducers_7/importHellPartyScript.inc`、
`CItemGenerator.h`。

## 6. 顺带纠正的旧结论

1. `docs/HANDOVER-2026-10-16-wave2-drops-quests-shops.md:43` /
   `docs/PLAN-single-player-priority-2026-10-16.md:47` 称
   "`special/hell` 两份 `.etc` 在 PVF 里找不到（20 个命名全 miss）"——**错误**。
   原版加载名在 `CtorProducers_2C/initialize.inc`：
   `Etc/ItemDropInfo_Monseter.etc`（普通）、`Etc/ItemDropInfo_Monster_Hell.etc`（hell）、
   `Etc/ItemDropInfo_Monseter_Extra.etc`（special，第三参）。这两份文件在本地 PVF
   都存在，88 件高级配方书/领主神器的 special 掉落**可做，不再被"缺文件"阻塞**。
2. `docs/REDNF-GAP-2026-10-16-full.md:19/117` 的"封印门数据/路由/出现率已做，无深渊
   模式时门保持关闭"仍然准确；但可以补充：**门后面所需的 hell 表、组怪、装备池
   一个都没接**，不是"只差一个开关"。
