# 2026-10-28 分解机「无法分解的类型」：白名单换成原版的逐件参数

## 现场

- 玩家 role 8（`12345`，job 8，Lv.70）在诺顿（npc 13）处逐件分解，日志
  `local-run/logs/channel-evidence.log`：

  ```
  19:49:59 DISJOINT_COMMITTED slot=9  rewards=1
  19:50:03 DISJOINT_REJECTED Disjoint target is not ordinary owned equipment
  19:50:08 DISJOINT_COMMITTED slot=11 rewards=1
  19:50:11 DISJOINT_COMMITTED slot=12 rewards=2
  19:50:15 DISJOINT_COMMITTED slot=14 rewards=2
  19:50:17 DISJOINT_COMMITTED slot=15 rewards=2
  19:50:20 DISJOINT_COMMITTED slot=13 rewards=2
  19:50:24 DISJOINT_REJECTED Disjoint target is not ordinary owned equipment
  ```

- 8 个 op28 请求（`logs/channel-evidence.log.request-*-28-*.bin`，8 字节加密体；index 0 的
  `BatchCryptFG` 只做 XOR `keys[4+i%4]`，key 斜坡 `04 05 06 07` ⇒ `raw[0]^4` = 槽位）解出的槽位序列是
  9/11/12/13/14/15/16/17：前 6 个提交，**16 和 17 被拒**。
- 存档（`economy-account1-r8-FG.xml`）里这两格仍然在原位，说明事务没有扣物品：
  槽位 16 = **27659**（`n_kat_rkaede`，35 级太刀，rarity 0 白装）、槽位 17 = **16059**
  （`n_str_sbskyarn`，35 级布甲腰带，rarity 0 白装）。槽位 10 的 35107（30 级十字架，rarity 3）当时没点。
- 客户端弹的「无法分解的类型」就是服务端回包 `{0, 0x13}` 的文案。

## 根因（原版逻辑核对）

`reference/rednf/remake/source/DNFServer/GameServer/Game`：

| 原版位置 | 行为 |
| --- | --- |
| `ExpertJobLink4/_checkDisjointableItem.inc` | 物品脚本缺失 → `0x11`；`IsImpossibleDisjoint()` → `0x13`；`GetAttachType()==2`（`[attach type]` 为 `` `[trade delete]` ``）→ `0x11` |
| `CEquipItem/set_item.inc:94` | `m_impossibleDisjoint = script.m_6f6` |
| `STEquipmentScript/ImportEquipmentScript.inc:2971`（case `0xd04`） | `[impossible contents]` 块里出现 `` `disjoint` `` ⇒ `m_6f6 = 1` |
| `STEquipmentScript/ImportEquipmentScript.inc:981`（case `0x805`） | `[value]` → `script.m_field24`；`CEquipItem::set_item` 再拷到 `+0x30`，即 `GetSellPrice()` |
| `ItemUpgradeVtableB2/GetResult.inc` | `base = getItemSellPrice((int)(GetSellPrice()*1.1f),0,0,IsRandomOption())*1000/150`；`money = (int)(base * m_creationRate[rarity] / 1000f)`，最小 1；主产物固定 `m_cubeIndexVecs[4][0]` |
| `DisJoint/_GetAdditionalItem.inc` | 附加物按 `m_additional[rarity]` 池抽，数量 `grade / m_additionalWeight[rarity]`（命中）或 `grade / m_additionalRate[rarity]`，截断、最小 1 |

**原版没有任何 itemId 白名单**，结果全部由每件装备的 `[value]`/`[rarity]`/`[grade]` 现算，唯一的
"不可分解"就是 `[impossible contents] → disjoint`。

我们的 `Disjoint-Item-FG92.cs` 把 `Profiles.TryGetValue(itemId)` 当成前置条件：表里只有 1310 条
（占 `item-metadata.xml` 26,122 件装备的 5.0%，31–40 级 4.7%，61–70 级 0.3%；对 FG359 装备掉落池的
4,685 个 id 只覆盖 21.7%），表外的普通白装一律 `0x13`。抓到的两件正是表外装备。

另外，附带的 1310 条表本身没有误收：PVF 全量里带 `[impossible contents]` 的装备有 705 件，
与这张表交集为 0；掉落池里也是 0。所以问题只在"覆盖太少"，不在"该拒的没拒"。

## 代码与数据

1. **新数据表** `data/disjoint-params-FG92.dat`（287,418 B）
   由 `local-run/pvf/gen_disjoint_params.py` 生成：
   magic `FG92DP01` + PVF sha256(64B) + int32 行数 + 行 `{uint32 itemId, uint32 value,
   byte rarity, byte grade, byte flags}`（flags bit0 = impossible-disjoint，bit1 = trade-delete）。
   覆盖 `item-metadata.xml` 的 Kind=4 全集 26,122 行；其中 705 行 impossible-disjoint、189 行
   trade-delete（本 build 的 trade-delete 全是称号）、1,615 行无 `[value]`/`[price]`（存 0，
   公式仍给出最小 1）。

   生成器内置四道门禁，任一不过就不写文件：PVF sha 与 `Quest-Catalog-FG.cs` 的 `CurrentPvf` 一致；
   每个注册 Kind=4 都有 `.equ` 与数值 `[rarity]`/`[grade]`；**旧的 1310 条目录必须逐条重新推导出
   完全相同的 Rarity/Grade/Value 且都不得带排他标记**；行数等于 Kind=4 闭包。

2. **新加载器** `src/Tools/VmTestKit/Disjoint-Params-FG92.cs`
   沿用 `FG359` 的 magic+sha 校验风格（sha 不符拒绝启动），行数/magic/重复/越界/尾随字节全校验；
   在 `Batch-Service-FG.cs` 启动序列里紧跟 `Offline70EquipCatalogFG359.Load` 载入。

3. **`Disjoint-Item-FG92.cs`**
   - 拒因改成原版口径并带上回包码：空槽/非装备/数量≠1/未注册 → `0x11`；
     `[impossible contents] disjoint` → `0x13`；`[trade delete]` → `0x11`。
   - 产出依旧用 Disjoint.etc 常量（`BaseFactors`/`Jackpot*`/`NormalDivisors`/`Extras`），
     只把逐件输入从"1310 条表"换成参数表；`BaseCount` 改成 `(value,rarity)` 两个基本量，
     `Break-Returns-FG177` 同步改调用，语义不变。
   - `Body` 增加 `code` 参数，失败回包不再写死 `0x13`；`Expert-Machine-FG183`（op251 个人分解机，
     复用同一 `Open`）同步支持 `Rejected` 的错误码。
   - `Disjoint-Catalog-FG92.cs` 保留 1310 行但加注为"生成器回归 fixture + FG177 门禁"，
     运行期不再读 `Profiles`。

## 离线验收（`local-run/disjoint-params-fg92/DisjointParamsCheck-FG92.cs`）

驱动与稳定源同批编译，在**存档副本**（`local-run/disjoint-params-fg92/store/DNF70-batch-test-FG`，
另注入 26070@30、16057@31 两格）上反射调用真实 `Handle`，24 项全绿：

```
params rows 26122
legacy rows 1310 missing 0 mismatched 0 flagged 0
DISJOINT_REJECTED code=19 Disjoint target has [impossible contents] disjoint   (35107)
DISJOINT_REJECTED code=17 Disjoint target is [trade delete]                    (26070)
DISJOINT_REJECTED code=17 Disjoint target is not ordinary owned equipment      (空槽)
DISJOINT_COMMITTED slot=16  27659 -> 27 x 3037 (value 19040 → 27.92 截断)
DISJOINT_COMMITTED slot=17  16059 ->  5 x 3037 (value 4080 → 5.98 截断)
DISJOINT_COMMITTED slot=31  16057 -> 12 x 3037 + rarity-1 附加池（旧表行为不变）
FG92_PARAMS_OK
```

编译冒烟：`csc` 全源（193 源）两宿主 `returncode 0`；无参数运行均返回 2（与 `scripts/build.py --smoke`
断言一致）；`python scripts/check_repo.py` 通过（428 文件 / 193 稳定源）。

## 部署

产物输出到 `local-run/build-verify-fg92/`（未覆盖 `build/stable`，因为运行中的服务锁着 exe）。
上线顺序：停双服 → `python scripts/build.py --smoke`（会自动把 `data/disjoint-params-FG92.dat`
拷进 `build/stable`）→ `one-click.ps1` 或 WMI 分离拉双服 → 重开客户端（停服会杀当前会话）。

## 边界

- 参数表只覆盖 `item-metadata.xml` 已登记的 Kind=4；未登记装备本来也无法入包。
- 随机属性物品的 `getItemSellPrice` 走 `itemPriceRates_[2]`（`IsRandomOption()`），本引擎的掉落不产
  随机属性装备，仍按 `itemPriceRates_[0]=200` 计算（既有口径，未改）。
- 未鉴定装备（原版 `0xae`）与分解券/便携分解机的等级门槛（`0xd`/`1`）本引擎没有对应状态，不在本次范围。
- 旧客户端 UI 侧若按 `[impossible contents]` 灰掉按钮，属客户端自身行为；本次只对齐服务端判定与回包码。
