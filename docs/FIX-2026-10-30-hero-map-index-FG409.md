# FIX 2026-10-30：英雄级房间没有换成 `[heroes mode map index]` 变体（FG409，血蝴蝶英雄领主房无茧、领主站位不同）

状态：**源码已改、离线验收 52/52 + 回归通过**；未部署（`build/stable` 被运行中的双服占用，停服重编重启后生效）。

## 1. 现象（用户 2026-10-30 报告）

对比 B 站早期原版「英雄级 血蝴蝶之舞」录像后指出：我们的英雄难度领主房间

- 领主 62012 出现的位置和原版不一样；
- 房间里**没有虫茧**（原版英雄领主房满地都是茧）。

普通难度看起来正常，所以问题只在英雄级。

## 2. PVF 里本来就有英雄专用房间

每个房间 `.map` 都可以带一个 `[heroes mode map index]`，指向该房间的英雄变体。血蝴蝶（dungeon 52）四个房间都有：

| 普通房 | 英雄房 | 内容差异 |
|---|---|---|
| `6512(1,1)boss1.map` | **6518** | `[monster]` 8 行 → **11 行**（多 4 只 62013），领主 62012 从 order 3 挪到 **order 4**（1113,314 → **1230,302**），`[passive object]` 里多了 **13 个 10006 虫茧** |
| `6513(1,1)boss2.map` | **6519** | 同上，领主在 (341,305)，同样有茧 |
| `6503(1,4)nomal.map` | **6516** | 6 行 → 7 行 62013，另有 `[special passive object] 8938/8939` 的 `[monster]` 动作（茧房孵化 62014/62015，共 14 只） |
| `6504(1,3)named.map` | **6517** | 5 行不变，追加 4 个 `8938` + 1 个 `8939` 的孵化怪 |

CN70 PVF 全库共 58 个带 `[heroes mode map index]` 的 map（血蝴蝶、疑惑之村、夜袭、补给线阻断、追击歼灭、列车上的海贼、夺回列车 + 铁鳞海贼团 6 个，后者不在英雄副本列表里）。

## 3. 谁负责换图：服务端

rednf 原生实现（`GameServer/Game/BattleMonsterDepsC2/getMap.inc`，ORIG `0x08300ed4`）：

```cpp
const CMap* CBattle_Field::getMap(int mapIndex) {
    CMap* map = G_CDataManager()->find_map(mapIndex);
    ...
    if (get_dungeon_diff() == 4 && map->m_b4 != 0) {          // 4 = 英雄级
        GetCurrentMapInfo()->m_mapIndex = map->m_b4;          // 先改写当前图号
        map = G_CDataManager()->find_map(map->m_b4);          // 再取英雄图
    }
    return map;
}
```

`m_b4` 就是 `STMapScript::m_maxMonsterDropException`，由 `ImportMapScript.inc:1095` 从 `[heroes mode map index]` 读入、`CMap/set_map.inc:45` 拷贝过来。`ConsistMap()`（`BattleDepsB2/ConsistMap.inc`）随后用它装出房间名单，`MakePacket(0x1d)` 再把 **改写后的** `m_mapIndex` 发给客户端。也就是说：

- 客户端永远只加载服务端告诉它的图号（op29 房间包的 u16 地图号）；
- 怪物在客户端按**该 map 文件的 `[monster]` 行顺序**摆位（原生 `mob.m_mobId = ref.m_orderIdx`，房间包每行的第一个 u32 就是 order index），所以服务端名单必须来自同一张英雄图；
- 虫茧是 `[passive object]`，客户端从 map 文件直接生成，服务端只要"当前图号=6518"它们就会出现。

我们的服务端从来没有读这个字段（`grep 6518` 在改动前全库为空），英雄难度一直沿用普通房间 6512：名单 8 行、领主 order 3、没有茧——和用户看到的现象完全一致。

## 4. 修复（FG409）

新增生成物 `src/Tools/VmTestKit/Hero-Maps-FG409.cs`（由 `local-run/pvf/gen_hero_maps_FG409.py` 从 CN70 PVF 生成，禁止手改）：

| 表 | 内容 |
|---|---|
| `Pairs` | 52 条 `base -> hero`（只收 `[dungeon]` 标签落在七个 `[herosmode enable]` 副本里的 map；铁鳞海贼团 6 条不进表） |
| `Rosters` | 51 张英雄图的 `[monster]` 行，`{id, level, kind}`，level 规则与 `Forest-Content-FG75` 相同（`rel=1` 时 `[basis level]+lvl`） |
| `Rolls` | 同序 tok7/tok8（金币/特定掉落次数，同 `Floor-Catalog-FG119` 约定） |
| `Objects` | 23 张英雄图的 `[special passive object] [item]` 动作（原生 ObjectIndex 编号；池子仍用副本自己的 `[special passive object item]` 行） |

服务端接线（英雄难度 = `Hero-Unlock-FG358.HeroDifficulty == 4`，也只有那七个副本能进 4）：

| 文件 | 改动 |
|---|---|
| `Dungeon-Flow-FG.cs` | 拆出 `BaseMapFG409`（原算式）+ `CurrentMap = HeroMapFG409(BaseMapFG409)`；英雄难度下按 `Pairs` 换图 |
| `Elite-Monsters-FG118.cs` | 新增 `RoomBaseRowsFG409()`：名单跟随**当前加载的图**（英雄图用它自己的行），否则回 Forest 表 |
| `Dungeon-QuestMaze-FG361.cs` | `MazeRowsFG361()` 在英雄难度下先查英雄表（否则 d52 这类有多 `[maze info]` 块的副本会退回基础块 pinned map 的名单） |
| `Floor-Drops-FG119.cs` | 地图掷点表补英雄图回退（否则英雄房整间掉光物品掷点） |
| `Object-Drops-FG167.cs` | `Plan`/`Capacity` 支持英雄图的 `[special passive object]` 行（池子复用副本行） |
| `Dungeon-QuestCompanion-FG130.cs` | d83 任务 26 同伴分配、地狱首领房判定改比 `BaseMapFG409`；两份 roster 拼接改走 `RoomBaseRowsFG409()` |
| `Act4-Boss-Apc-FG176.cs` | 地狱 APC 的 Boss 房判定同样改比 `BaseMapFG409` |

非英雄难度（0..3）完全走原路径：`HeroMapFG409` 直接返回入参，`RoomBaseRowsFG409` 查不到英雄名单就回 Forest——测试里难度 0 的十个副本图号逐一核对未变。

## 5. 验收

`local-run/driver/HeroMap409Check.cs`（新全源 + driver 编译，**52/52 PASS**）：

1. 表完整性：52 对/51 图、每对目标都有名单和掷点表、行序对齐、`Resolve(6512)=6518`、`Resolve(6501)=6501`、铁鳞海贼团不入表。
2. 血蝴蝶对照：普通领主房 6512/8 只/领主 uid 103；英雄领主房 **6518/11 只/领主 uid 104**、102 是 62013；两种房间包 offset 9 的地图号分别为 6512/6518。
3. 虫茧：`PassiveObjectsFG360.Declares(6518,10006)`、`(6519,10006)` 为真（破坏报告会被接收）。
4. 七个英雄副本逐一：有英雄变体的 Boss 格换图且保留 kind-3 行；16331/17009/13088/15039/15029 这几张没有 `[heroes mode map index]` 的 Boss 图保持原样；难度 3 的房间图号/数量不变。
5. 房间内容：d52 (1,4) 加载 6516，名单 7+14=21（FG375 茧房孵化进包）；英雄房每一行都能过 `FloorDrops.FG119.Plan`，越界 uid 仍被拒；d85 13091 的 10137 木桶可掉落；d83 任务 26 同伴在英雄房仍能分配。
6. 回归：难度 0 的 d3/11/17/32/34/45/51/60/89/140 图号不变。

其余回归（同源重编后运行）：`Fg365AcceptanceCheck` 50/50、`HellCheck-FG356` 69/69、`BossMinionSweep-FG404` 220、`EquipCheck-FG359` 25/25、`Discard50Check-FG395` 21/21、`RidableCheck-FG361` 49、`QuestSystems-FG211` 109、`SupplyCutObjectCreditCheck-FG387` 36/36、`GentDefenceBossRoom-FG386` ALL PASS、`BwangaChainCheck-FG377` 308/308、`TowerFloors-FG348` 165、`RescueCageCreditCheck-FG383` 35/35。

`scripts/check_repo.py`：549 文件 / 204 源通过。`RoomsBossCheck-FG372` 有 1 条断言失败，是 FG396 已记录的 d81 (1,2) 10000→10011 数据变更与旧驱动期望不一致，与本次无关（改动前的 fg386 构建在旧驱动源码下 130 项全过）。

## 6. 未做 / 遗留

- **未部署**：`build/stable` 双服仍在跑旧构建，停止服务后 `python scripts/build.py --smoke` + 重启即生效。客户端无需任何改动。
- `6512`/`6513` 两个 Boss 图候选仍未按 `[greed]` 入口方向选择（原生按 `CheckEntrance(dir)` 挑 6512 或 6513）。两个都换到英雄图后，我们固定走 6512→6518；从另一侧进门原版会是 6519。属于既有简化，本次不改。
- 英雄图专属的额外掉率数据（例如英雄图新增怪的 `.mob` 特定池）沿用现有 `MonsterPools`：`61229/56011/56124` 三只仍未登记池子，只会掉金币与通用掉落。
