# 批次二交接：任务目标生成、掉落与计数（FG360，2026-10-27）

来源：`local-run/coord/TRIAGE-2026-10-27-player-feedback-15.md` 批次二（条目 3、4、5、6、10、14）。
并行约束：批次一/三由其他 agent 同时开工，本文件只记录批次二落地的部分，以及它们与批次一共用机制的接口点。

---

## 0. 一句话结论

| 条目 | 玩家现象 | 本批结论 | 状态 |
|---|---|---|---|
| 3 | 斯卡萨之巢领主房没有奥尔卡 | `scasalair.dgn` 的 `[quest connection] 0 4571 3` 本来就把领主格换成 9311，而 9311 里写着 `[ai character] 1513`（奥尔卡）；服务端从未加载过这套 quest 迷宫 | 已接（生成表 + APC 侧通道） |
| 4 | 免疫胶囊（3033×30 + 3187×10）完不成 | **3033 只在分解表里**（`Disjoint-Catalog-FG92.Extras[1]`），3187 只在 independent drop 里（d100 的 56411/56410/56107，3%–51%/杀）；floor-stack/object/world 三处都没有，这是数据边界不是 bug | 结论已取证，未造新源 |
| 5 | 卡勒特干部徽章（3249）精英/领主都不掉 | 3249 的 independent 行**一直都在**（12 个怪），但只有 4 个怪在旧名册里；批次一的 quest-maze 层上线后 9/11 个军官可刷（d81 一下多出 56501/61112/56106）。掉率 20%/60%/90%（普通/勇士/王者） | 机制已验，可达性随批次一提升 |
| 6 | 根特外围救俘虏不计数（8824） | 客户端上报被动物件破坏走**同一个 op42**，`passiveFlag=1` + `m_mobSeq`=物件 id；旧解析器把这类包当"actor 越界"整包拒收。已实现原生通道 | 已修 |
| 10 | 根特南门弹药筒（4313×40）不掉 | 4313 的唯一来源是任务 4363 自己的 `[enemy reward item]` 块（`61801 1 82 {1,2,3} 4313 1 100 40`），而 quest-catalog 从未把这 8 列块收进 MonsterDrops | 已重导（140 条） |
| 14 | 夜袭 4382/4381/4380/4379 不掉、了望台不计数、沙影贝利特不出现 | 438x 同上（5722/5724/5727/5733 的 `[enemy reward item]` 为空 MonsterDrops）；8877 走新的物件通道；16337 房格位原来错在 (2,1)（PVF 是 (1,0)）且名册为空、APC 10628 从未生成 | 已修 |

---

## 1. 协议取证（条目 6/14 的根）

`reference/rednf/remake/source/DNFServer/GameServer/Game/DisPatcherCore/dispatch_sig_16.inc`
（`DisPatcher_DieMob::dispatch_sig`，也就是我们 handler 里的 op42）读包顺序是：

```
mobSeq(2) fieldF(2) int×4(16) mobCount(1) contributors[10×mobCount]
pairA24(2) pairA26(2) A22(1) A23(1) **passiveFlag(1)** moveCheck(1) unused(2)
A28(1) A2d(4) A31(4) A29/A2b/A35/A37/A39/A3b/A3d(2×7)
```

`logical = 54 + 10*mobCount`，所以 **`passiveFlag` 在 `tail+6`**（`tail = 21+10*mobCount`）。原生分支：

```cpp
if (passiveFlag == 1) { party->DestroyPassiveObject((int)die.m_mobSeq); return 0; }
```

- `CParty::DestroyPassiveObject`（`CPartyOnlyOrig/DestroyPassiveObject.inc`）→
  `CBattle_Field::DestroyPassiveObject(idx)`（`BattleFieldLCB/DestroyPassiveObject.inc`）：
  只有当 `idx` 命中 `CMap::m_passiveObjectList` 才返回 true。
- `CMap::m_passiveObjectList` 的来源（`CMap/set_map.inc:122-131`）：房间 `.map` 里
  **`[passive object]` 每行的第一个 int**，按文件顺序 push；`[special passive object]` 不进这个表。
- 这条分支**不进 `kill_monster`**：不掷掉落、不给经验、不动房间清空计数；而且是在
  `m_killMobCounter[mobSeq]++` 之后——也就是原生把物件也算进"击杀表"，只是不进入怪物掉落链。

旧服务端 `BatchDeathRequestFG` 的 actor 边界检查是 `(uid<100||uid>163)&&uid!=900` 直接抛
`Batch death actor bounds`，所以**每一包物件破坏都被整包拒收**（这也是 2026-10-26 归档里
"零 passiveFlag 帧"的另一半原因：帧根本进不到能记录 tail 的那一步）。历史 648 包 op42 抓包
（`local-run/**/*.request-*-42-*.bin`，XOR keys 4..7 解密）uid 全部落在 100..119，说明当时的
副本里也没有可破坏物件被真的打掉，两件事并不矛盾。

### 落地

- `src/Tools/VmTestKit/Passive-Objects-FG360.cs`：`data/passive-objects-FG360.dat` 的加载器，
  `Declares(map,id)` = 原生 `m_passiveObjectList` 成员判定。
- `src/Tools/VmTestKit/Passive-Kill-FG360.cs`：`DestroyPassiveObjectFG360(id)` 返回该房间该 id
  **尚未记账的第几个实例**（0 = 不在本房间/已记满）。同一房间同一 id 允许出现几次就记几次
  （两个了望台是两个实例），重放同一包不会重复计数。
- `Batch-Player-Protocol-FG.cs`：`BatchDeathRequestFG(..., out bool passive)`，`passiveFlag` 取自
  `tail+6`；`passive=true` 时只保留帧形状/折叠/填充校验，跳过 owner/actor 检查（原生也不查），
  并在 `LastDeathTailFG` 末尾追加 ` passive=True`。
- `Batch-Service-FG.cs`：`Kill()` 见到 `passive` 立刻转 `PassiveObjectKillFG360(id)`——只做
  `KillWithAssetsFG171(dungeon,difficulty, id, receipt)` + 任务进度增量包，**不掷任何掉落、
  不给经验、不发死亡通知**。
- `data/passive-objects-FG360.dat` 由 `local-run/pvf/gen_passive_objects_fg360.py` 生成：
  2035 张 map 有 `[passive object]`，46810 字节；生成器内置三条复现闸门
  （16337/8877、10053/8824、15069/10269），跑不过就拒绝写盘。

### 为什么物件可以算任务击杀

`[hunt enemy]` 的目标列可以不是 `.mob`。2026-10-26 的
`local-run/recon/hunt-enemy-missing-monsters.md` 已判定 enemyType 3 的行指向可破坏物件
（d31 `[special passive object]`），而 4203 的 8824（囚牢）、5723 的 8877（了望台）、
4273 的 10269（卡勒特障碍物）在所有 `monster/monster.lst` 里都不存在。客户端对这类目标的
**唯一**信号就是上面那条物件破坏包，因此只能用它记账；这条通道只在"id 是本房间声明的物件"时
成立，且只推进 monster 列等于该 id 的 `[hunt *]` 行（difficulty/dungeon 门槛仍由
`KillStateFG171` 原来的列判定执行）。

---

## 2. `[enemy reward item]` 8 列 → MonsterDrops 7 列（条目 10/14）

原生列序（`reference/rednf/.../STQuestScript.cpp:696-716`，`MonsterKillItem` 定义在 `Quest.h:107`）：

| 列 | 原生字段 | 语义 |
|---|---|---|
| 1 | `m_killCount` | 怪物 id（`CheckQuestMonster` 调用点传的是 `mobIndex`） |
| 2 | `m_enemyType` | 1=普通怪、2=APC（`roleType 5..8` 时传 2） |
| 3 | `m_mapIdx` | 地下城 id（调用点第一个实参是 `getDungeon()->get_index()`） |
| 4 | `m_dungeonIdx` | 难度（行里枚举 1..4） |
| 5 | `m_field10` | 物品 id |
| 6 | `m_field14` | 尝试次数 |
| 7 | `m_field18` | 掉率（%） |
| 8 | `m_field1c` | 需求数量（原生"已有≥needCount 就删掉这个候选重掷"） |

引擎的 `MonsterDrops` 是 7 列（无 enemyType），语义一一对应；第 8 列当 `MaximumCount` 用，
效果与原生"需求满足后不再出"等价。`gen_snow_quests.derive()` 2026-10-26 已支持这个折叠，
但目录一直没重导——这就是 4363/5722/5724/5727/5733 等 140 条"客户端有块、服务端空表"的来源。

**落地**：`local-run/pvf/apply_enemy_reward_fg360.py`

- 只改 `MonsterDrops` 元素；只改当前为空表的记录；`Supported`/`Reason`/其它字段逐字节不动；
- 双解析器交叉验证：脚本自己的 `[enemy reward item]`→7 列折叠必须与
  `gen_snow_quests.derive()` 的结果完全相等，否则跳过；
- 物品必须已在 4 个运行时 metadata 里登记（否则 `AssetsKnown()` 会把整条任务藏掉）；
- 记录数 3051..3300、无裸 LF、无 CRCRLF、非目标记录逐字节不变、备份 `.pre-fg360-enemy.bak`、
  `.pending` 原子替换、可重跑（第二次 0 条）。
- 结果：**140 条 / 双树（`data/` + `build/stable/`）**，4,552,654 → 4,912,777 字节。
- 唯一跳过：**5791**（`n_quest/gent/supplycut/training_61_supplycut_sample.qst`）。该文件把
  `[type]`/`[sub type]` 写成"标签+值同一行"，生成器的行扫描器会越过 `[/enemy reward item]`
  多读一个 `-1`；本工具的闭合标签解析得到 14 个值（2 行），与 `derive()` 的 0 不一致，
  按"双解析器必须一致"的闸门跳过。该任务是 `[training]`（C 类，未支持），不影响可玩内容。

---

## 3. 任务迷宫层（条目 3/14 的房间与同伴）

批次一新建了通用的 `[quest connection]` maze 层（`Dungeon-QuestMaze-FG361.cs` +
生成表 `Quest-Maze-FG361.cs`，选择规则 `SelectQuestMazeFG361`：questType 0 需
`IsDoingQuest` 且 `m_questMinDiff <= difficulty`，取**.dgn 顺序最后一个命中块**，没有命中用 base 块）。

批次二只做两处**数据**扩展（`local-run/pvf/gen_quest_mazes.py`）：

1. `DUNGEONS` 增加 **45、83**：
   - d45 `scasalair.dgn`：base 块 boss(0,0)=9310；`[quest connection] 0 4571 3` 块
     size 3×4、start(0,3)、boss(1,0)=**9311**（`map/scasalair/quest_boss(0,0).map`），
     另有 9312/9314/9315 三个普通格。
   - d83 `nightassault.dgn`：base 5×4 boss(4,3)=16331；`[quest connection] 0 5729 2`
     把 (1,0) 换成 **16337**（`map/nightassault/q(1,0)verict.map`）。5720/5734/5737 也一并入库。
2. `COMPANION_CHARACTER` 白名单：默认 `[character]` 阵营的 `[ai character]` 不进敌方 APC 表，
   只对这两个"任务文本点名要在房间里出现"的同伴放行——
   `(45, 9311, 1513)` 奥尔卡（`aicharacter/_jojochan/swordman/orca/orca.aic`，
   `[minimum info] 奥尔卡 … 70`）与 `(83, 16337, 10628)` 沙影·贝利特
   （`aicharacter/gunner/Verrickt/Verrickt.aic`，`[minimum info] 沙影·贝利特 … 80`）。
   等级由 `aicharacter.lst` → `.aic [minimum info]` 第 5 字段读出，不硬编码。

生成结果（`Quest-Maze-FG361.cs`）：

```
{9311,   new ushort[][]{new ushort[]{61456,60,3}}}                       // 领主房名册
{16337,  new ushort[][]{61441,63,0},{61437,63,0},{61442,63,0},
                        {61437,63,0},{61442,63,0},{61443,63,0}}          // 地图 [monster] 原样
Apcs: {9311,{9311,1513,70,5}}  {16337,{16337,10628,80,5}}
```

它们走已有的 APC 侧通道（`ApcUidFG186` = `100+TotalCount+index`，不进 `TotalCount`、不掷掉落），
所以 4571 的击杀目标仍然是 61456（`[hunt enemy] 45 3 61456 1 1`）——奥尔卡只是"房间里看得见"。

**注意**：旧代码 `Dungeon-QuestCompanion-FG130` 里 5729 的 `QuestRoomXFG205/YFG205` 是 (2,1)。
`.dgn` 写的是 `[map specification] 1 0 16337`，正确格位是 **(1,0)**；批次一的 maze 层接管后
这条旧覆盖不再触发（`QuestRoomRunFG205` 只由 1475 使用），格位问题随生成表消失。

---

## 4. 掉落可达性核查（条目 4/5）

### 3249 卡勒特干部的徽章（条目 5）

`data/independent-drops-FG355.dat` 里 3249 有 12 条记录（`rates=百万分率`）。按
`Offline70IndependentDropsFG355.Plan` 的确定性抽取（`SHA256(receipt|fg355|purpose|attempt)`）
在 Python 里独立复算 1200 次/怪，单杀命中率：

| 怪 | 普通 | 勇士(2) | 王者(3) | 现在是否有房间 |
|---|---|---|---|---|
| 56501 | 20.7% | 62.4% | 90.6% | d81 10045/10008 |
| 61112 | 19.3% | 58.6% | 89.5% | d81 10002/10003 |
| 56106 | 20.1% | 58.8% | 89.9% | d81 10009/10014 |
| 61709 | 22.8% | 62.1% | 88.8% | d80 10065 |
| 56713 | 21.0% | 61.3% | 89.8% | d80 10055 |
| 61712 | 21.3% | 59.8% | 90.7% | d82 10068 |
| 61708 | 17.3% | 59.7% | 90.2% | d60 11123 |
| 61328 | 20.0% | 59.8% | 90.3% | d61 11217/11232 |
| 61710 / 56502 / 56503 | 同上量级 | | | **不在**（d82 的 maze1/boss2 房与 (4,0)normal，当前迷宫没有） |

即：条目 5 不是"没接掉落"，而是"军官所在的房间没被刷出来"。批次一的 maze 层上线后
**9/11 个军官可刷**（d81 一下从 1 个变 3 个），d82 剩下的三个要等 d82 迷宫补齐（批次一 d82 未列）。

### 3033/3187（条目 4）

- `data/floor-stack-FG126.dat`（840 表）、`object-drops-FG167.xml`、`world-drops-FG182.xml`、
  `card-items-FG125.dat`、`hell-FG356.dat`、`equip-drops-FG359.dat`：**都不含** 3033/3187。
- 3187 灵魂晶石：independent 5 条（`56107/56410/56411/56418/56419`），其中
  `56411`(d100 12005)、`56410`(d100 12004)、`56107`(d100 12002) 在我们名册里，
  单杀 2.8%–51%（随难度）。
- 3033 黑色小晶块：只在**分解**表 `Disjoint-Catalog-FG92.Extras[1] = {3033,3034,3035,3036}`
  （稀有度 1 装备分解产物）。原版的获取途径本来就是分解，不是怪物掉落。
- 结论：两条都有原生源（一件靠分解、一件靠 d100 独立掉落 / 分解），**本批没有新造掉率**。
  `[seeking]` 判定本身已由验收脚本证明可用（放满 30/3187 10 后 `WireRemaining == 0`）。

---

## 5. 验收

新增离线 driver：`local-run/driver/Batch2QuestDropsCheck-FG360.{cs,rsp}`

```
cd local-run/driver
python -c "import glob;src=sorted(glob.glob(r'D:\70S3A3\Server-70s3a3\src\Tools\VmTestKit\*.cs'));src=[p for p in src if not p.endswith('Batch2QuestDropsCheck-FG360.cs')];src.append(r'D:\70S3A3\local-run\driver\Batch2QuestDropsCheck-FG360.cs');open('Batch2QuestDropsCheck-FG360.rsp','w',encoding='utf-8-sig').write(''.join('\"%s\"\r\n'%p for p in src))"
/c/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe -nologo -target:exe \
  -main:Batch2QuestDropsCheckFG360 -out:Batch2QuestDropsCheck-FG360.exe @Batch2QuestDropsCheck-FG360.rsp
./Batch2QuestDropsCheck-FG360.exe "D:/70S3A3/Server-70s3a3/data"
```

覆盖：

- A：4363/5722/5724/5727/5733 的 MonsterDrops 行形状；真抽取能出目标物品；需求满足后 cap 生效；
  5722 行覆盖难度 1..4、5733 只覆盖 2..4（PVF 原样）。
- B：passive-object 表加载；16337/8877、10053/8824、15069/10269 成员判定；同房间同 id 按声明次数
  记（10053 声明了**两个** 8824，所以 1/2/0）；非本房间物件被拒；4203 七次物件上报后 7/7；
  5723 难度 2 满足、难度 1 不满足。
- C：d45 有 4571 → 4571 迷宫（boss(1,0)=9311、名册 61456 kind3、APC 1513）；无 4571 → base(0,0)=9310
  且无奥尔卡；难度 2 → 回 base（MinDiff 3）。d83 有 5729 → (1,0)=16337、6 只怪、APC 10628。
- D：3249 从 56501/61709/56713/61712 出货；3187 从 d100 三只怪出货。
- E：3033 在分解表。

**结果：50 项全绿**（`RESULT passed=50 failed=0`）。
另有 `scripts/check_repo.py` 428 文件通过、`gen_item_metadata.py check` 显示
`quest-catalog.xml ids 1705 missing 0`、`BOOT-BLOCKING missing: 0`、
`quests that cannot be accepted or completed: 0`。

编译验证：运行中的 `build/stable/DNF70-Channel-FG.exe` 与 `-Entrance-` 正在被使用，
所以本批编译到 **`local-run/build-staging-fg360/`**（两个 exe 与 27 个数据文件），
没有替换 `build/stable`（与 FG205 起的惯例一致）。要上线：停服务 → `python scripts/build.py`
→ 重启。

### 并行 agent 的两处编译修复（不改语义）

批次一新建的 `Ghent-Quest-Mazes-FG217.cs` 里，死代码 `BaseCandidates` 声明成
`ushort[][]` 但每个 case 返回 `ushort[]`，导致整树编译失败（CS0029，30+ 处）。该函数无任何调用者，
本批把**生成器** `gen_ghent_quest_mazes.py` 的发射行与生成文件里那一处声明同步改成 `ushort[]`。

批次三新建的 `Random-Maze-FG362.cs`（`gen_random_mazes.py`）有同一类问题，另外 `Pools` 的键类型
是 `int`，而 `Floor-Drops-FG119.Specific()` / `Floor-Catalog.MonsterPools` / `QuestMazeFG361.Pools`
都是 `uint` 键，导致 `Floor-Drops-FG119.cs` 的 `TryGetValue(uint)` 匹配失败（CS1502）。
同样改生成器与生成文件：`BaseCandidates` → `ushort[]`，`Pools` → `Dictionary<uint,uint[][][]>`。
两处都留了注释；重新生成不会回退。

---

## 6. 部署注意

1. 本批改了**数据文件**（`data/quest-catalog.xml`、`build/stable/quest-catalog.xml`、
   新文件 `data/passive-objects-FG360.dat`）和**源码**（新增 2 个 .cs + 3 处接入）。
   `scripts/build.py` 会把 `data/*` 拷进 `build/stable/`；`dist/` 里的角色包需要重新打包
   （记忆里 dist 的 catalog 本来就落后源码树）。
2. 服务端只在启动时加载 catalogs；改完必须重启才生效。
3. `passive-objects-FG360.dat` 缺失会让服务构造期直接抛错（和其它 catalog 一致），不要漏拷。
4. GM 按中文名搜物品搜不到是登录器 `catalog.rs search_items` 只匹配 id/类别名（2026-10-27 已记），
   不是服务端发不出来；给材料做 GM 验证时请直接用 id（3033/3187/3249/4313/4382/4381/4380/4379）。

## 7. 遗留 / 不属于本批

- **5791** 跳过（见 §2），是 `[training]` 任务，不支持。
- `[enemy reward item]` 第 2 列 enemyType 在 7 列折叠里丢弃：原生 1=普通怪、2=APC，折叠后
  enemyType=2 的行会在普通怪击杀时也掷一次（轻微多给，不是缺给）。要精确需把
  `MonsterDrops` 加宽到 8 列并同步 `AssetsKnown`/`Quest-Drops`/生成器，属机制级改动，单独立项。
- 条目 11 的 10269 与条目 6/14 共用本批的物件通道，但 d88 的房间/载具在批次一，验收要两边都合。
- d82（根特南门）maze1/boss2 房间缺失导致 61710/56502/56503 不可刷；d82 不在这轮批次一的清单里。

---

## 8. 同类问题普查（第二轮，用户要求"不能光修一处"）

见 `docs/AUDIT-2026-10-27-batch2-same-class-sweep.md`。结论：

1. **任务迷宫**：全库 26 个 `.dgn` 带 `[quest connection]`，原来只加载 9 个。本轮把
   25/52/53/82/87/92/93/110/1501 加入 FG361（d82 的 base 块本是任务 4360 的 2×2 迷宫，
   补上后 3249 的 11 个军官全部可刷）；d140 属 Act4，d11/12/15/32/34/141 由专用分支处理。
2. **怪物专属掉落**：42 只怪（死亡之塔 55001-55025、幻影塔 50501-50509、极限塔 56438-56445、
   Act4 幼龙 60030、根特 BOSS 61112/61710）的 `.mob` 声明了掉落但目录是空池，已用
   `apply_monster_pools_fg360.py` 按 PVF 补齐（手调的 1078/1096/56713 保持原样）。
3. **物件掉落**：生成器只覆盖 36 个副本，Act5+/根特/Act7 完全没有物件掉落；且任务房不在可达集合
   （16337 的 3269 柴火永远不掉）。已把副本表统一到 `gen_dungeon_content` 并并入 FG361/FG362 房间：
   96→356 张带动作房间、34→57 个池物品，复现闸门 96/96。
4. **同伴 NPC**：26 张可达房间的 `[character]` 友军原来全不生成（含奥尔卡/沙影·贝利特），
   生成器改为"房间声明的阵营行全部生成"，26/26 进表。
5. **残留**：3016 是"半表"（只吃了 `[monster reward item]`，未动）；37 个 `[seeking]` 目标物品
   在所有已实现系统里仍无源，归属分解主产物 / 商店覆盖 / 事件系统，本轮不造掉率。

验收 driver 扩到 **69/69 全绿**（新增 F 段：物件掉落含 16337/3269、塔怪 55001 池、
d82 BOSS 61710 池、四个同伴房间）。
