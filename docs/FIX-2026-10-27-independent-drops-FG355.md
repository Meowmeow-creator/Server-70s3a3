# FG355 独立掉落（independentdrop）接通

日期：2026-10-27
范围：普通怪物死亡掉落（`CBossStage::killMonsterDropItem` 对应的单机路径）
状态：代码与数据已完成，离线验收全绿；**未部署、未重启实际服务**，玩家实机验收待做。

## 症状与根因

用户反馈：龙人之塔领主鲁卡斯应掉专属领主神器「落雷惊鸿战矛」，实际从不掉落。

根因不是单个物品漏配，而是整套 `independentdrop` 没有接入：

1. `etc/independent_drop.etc` 的 `[independent drop]` 段按怪物记录专属掉落；
2. `etc/independentdrop.lst` + 142 个 `etc/independentdrop/*.etc` 是命名池，供主表 flag=2 合并；
3. 服务端此前只读 `.mob` 的 `[item]/[boss drop item]` 标签生成 `MonsterPools`，
   源码对 `independent_drop` 零引用；
4. 鲁卡斯（怪物 71，`monster/draconian/lakius.mob`）的 `.mob` 没有 33107，
   主表记录却明确写了它，所以必须由独立掉落系统产出。

## 原版语义（rednf 逐指令转写）

- `CBossStage/killMonsterDropItem.inc`：
  - `ref.m_rateSlot = m_difficulty` → `rates[difficulty]`
  - `ref.m_field34 = eraseCnt = mob.m_level` → `rangeMin/rangeMax` 过滤
  - `ref.m_field3e_byte = party member count` → 单人 `expect[0]`
  - 其余 ref 字段保持构造函数默认（`m_field24=0` 随机、`m_field25=0`、
    `m_premiumDropRate=0`、`m_field30=0`、`m_field3c_byte=0`、`m_field3f=0`）
- `MonsterIndependentDrop/generateIndependentItem.inc`：
  - 每条命中：`rates[difficulty] > rand(1_000_000)`，次数 `expect[0]`，上限 `limit`
  - 有 `[list]`：`rand(mainRate)` 取第一个 `cumulative > roll` 的 item
  - 无 `[list]`：每个 count 生成一次 `itemIdx`

主表文本实际是 **16 个整数一条记录**（最后一个是 flag，没有 levelFilter）：

```
field0 monster itemIdx rate0..rate4 expect0..expect3 limit rangeMin rangeMax flag
```

行宽是按 17 token 折行的，按行解析会错位；正确做法是忽略换行按 token 流解析。
`field0=0` 是普通怪物（monster.lst 索引），`field0=1` 是 AI character 文件索引。

鲁卡斯记录：

```
0 71 0 8552 10200 11952 13600 13600 1 1 1 1 1 0 0 1 [list]
    33107 100
    14551 50
    16550 50
```

- `33107` 落雷惊鸿战矛（稀有度 3，20 级，魔法师战矛）
- `14551` 落雷惊鸿肩甲
- `16550` 落雷惊鸿腰带
- 难度 0 的单次命中率 8552/1,000,000；命中后按 50% / 25% / 25% 三选一。

## 落地

### 数据

- 生成器：`local-run/pvf/gen_independent_drops.py`
- 容器：`Server-70s3a3/data/independent-drops-FG355.dat`（`FG355ID1`）
  - 1813 条普通怪物记录 / 402 个怪物
  - flag 分布：1439 单物品、178 内联列表、196 命名池引用
  - 9974 个不同 item id
- 物品补登记：`local-run/pvf/gen_independent_metadata.py`
  - 复用 `gen_item_metadata.py` 的 `.stk` 证据规则（`[stackable type]` 语料先例 +
    六个逐 token 决策）
  - 追加 2497 条定义到 `data/item-metadata.xml`（40,478 条，13,003,964 字节）
  - 仍缺 4 个：`261`（不在 equipment/stackable.lst）、`2681819/2682169/2682170`
    （`[random upgradable legacy]` 随机罐，无语料先例，不猜 Kind）
- 生成报告：`local-run/pvf/export/independent-drops-FG355.report.json`

### 运行期

- 新模块：`src/Tools/VmTestKit/Independent-Drops-FG355.cs`
  - `Load`：校验 magic/PVF 指纹/记录边界；对物品 id 做 `Offline70Economy.Definition`
    探测，未登记项保留概率槽但标记 `Registered=false`
  - `Plan`：按难度/怪等级/单人 expect 执行原版 roll；固定 receipt 的 SHA256 抽样保证重放稳定
  - `Append`：只填充剩余 ground 行容量（`MaximumRows=32`），不会让新来源把原本能通过的
    击杀变成容量拒绝
- 接入：`Floor-Drops-FG119.cs` 在既有 specific/general/world 掉落之后调用 `Append`
- 启动：`Batch-Service-FG.cs` 在三个 item-metadata 注册之后 `Load` 新 `.dat`

### 未纳入

- `field0=1` 的 322 条 AI character 记录：原版走 `CAICharacterList::init` 独立路径，
  本服 APC 死亡目前是明确的 no-drop 分支；本次不改变 APC 行为。
- 玩家实测：需要部署 `build/stable` 并重启隔离服务后，用真人/驱动杀鲁卡斯验证。

## 验收

```
python scripts/check_repo.py                      -> repositoryChecksPassed
python scripts/build.py --smoke                   -> compiled 178, smoke true
python local-run/pvf/gen_item_metadata.py verify   -> VERIFY PASS
python local-run/pvf/gen_independent_drops.py      -> 1813 records / 9974 items / 4 unregistered
python local-run/pvf/gen_independent_metadata.py   -> 9970 registered / 4 missing
```

独立驱动：`local-run/driver/IndependentDropCheck-FG355.cs`（29 项 PASS）

覆盖：
- 表结构：1813 条 / 402 怪 / 4 个未登记 item
- 鲁卡斯记录：33107/14551/16550 权重、五档掉率、单人 expect/limit
- 固定 draw 边界：8552 不中 / 8551 中；pick 0/100/150/199
- flag=2 命名池展开：61349 累积从 1 开始、末项等于 mainRate
- 级别过滤：61349 在 50 级不中、67 级命中
- 容量守卫：32 行已满时追加 0，空列表追加 5
- **端到端**：`Floor-Drops.Plan()` 5000 次怪 1 命中 item 3015；
  龙人之塔地图 1601、uid 115（怪 71）20000 次杀怪命中落雷惊鸿三件之一

## 回滚

- 代码：删除 `Independent-Drops-FG355.cs`，还原 `Floor-Drops-FG119.cs` 的 `Append` 调用
  和 `Batch-Service-FG.cs` 的 `Load` 行。
- 数据：删除 `data/independent-drops-FG355.dat`；
  `item-metadata.xml` 用 `local-run/pvf/backup-2026-10-27-independent/item-metadata.xml.before-FG355`
  覆盖（注意：该备份是追加前文件，只在需要整体回滚时使用）。
