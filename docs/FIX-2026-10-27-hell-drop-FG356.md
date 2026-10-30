# FG356 深渊（hell party）掉落接线落地 — 2026-10-27

> 依据：[INVESTIGATION-2026-10-27-hell-drop-wiring.md](INVESTIGATION-2026-10-27-hell-drop-wiring.md)
> 的原生语义（rednf 逐指令转写）+ 本机 CN70 PVF 数据。目标：把 `etc/itemdropinfo_monster_hell.etc`
> 的概率真正接到单机服务端，并把深渊从"点不进去"变成"可以打、可以掉"。

## 1. 交付内容

### 新增

| 文件 | 作用 |
|---|---|
| `local-run/pvf/gen_hell_drops.py` | 从 PVF 生成 `data/hell-FG356.dat`（可重跑，含 JSON 报告 `local-run/pvf/export/hell-catalog-report.json`） |
| `Server-70s3a3/data/hell-FG356.dat` | 二进制目录：参数 / 难度表 / 89 组刷怪 / 31 张地狱房 / 51 个副本的深渊入场配置 / 2 行概率档 / 2 行稀有度 / 99 行等级窗口 / `[hell monster]` 集合 / 594 张装备池（35188 行） |
| `Server-70s3a3/src/Tools/VmTestKit/Hell-FG356.cs` | 目录加载与校验、`generateTable` 装备池、`CMonsterDrop_Hell` 概率/稀有度/抽取、入场闸门、地狱房状态与组怪击杀计数 |
| `local-run/driver/HellCheck-FG356.cs` + `.rsp` + `.exe` | 离线验收（69 项） |

### 修改

| 文件 | 改动 |
|---|---|
| `Batch-Service-FG.cs` | 构造器加载 `hell-FG356.dat`；op16 解析 native 的 `dungeon type`/`party type` 字节；`HellEntryPrepare`（免票/`[item condition]`×needCount/难度 roll/扣道具）；`KillMonster` 击杀前计数；`PlanMonsterDrops` 追加 hell 掉落；APC 死亡包携带 hell 掉落；`PlanHellGroundFG356` |
| `Dungeon-Flow-FG.cs` | `Begin` 增加 hell 参数（`ConfigureHellFG356` 先于封印门掷骰）；封印门改为 `hellPartyModeFG356` 才可能开；`RoomIndex` 给地狱房独立索引 250；`RoomCleared` 在地狱房额外要求 APC 全灭；`DNF70_HELL_DOOR=1` 测试开关 |
| `Elite-Monsters-FG118.cs` | 地狱房直接返回 PVF 房表 + 组怪（跳过冠军转换） |
| `Act4-Boss-Apc-FG176.cs` | `CurrentApcMonsters` 在地狱房返回组怪里的 `[ai character]` 行 |
| `data/hell-FG356.dat` | 新目录（build.py 会自动拷贝到 `build/stable`） |

## 2. PVF → 运行时映射

| PVF | 运行时 |
|---|---|
| `etc/itemdropinfo_monster_hell.etc [dungeon difficulty drop prob]` | `Offline70HellFG356.Plan`：按副本标准等级取档，`rand(0,1000) <= w[dungeon difficulty]`（dropRate=100） |
| `[basis of rarity dicision]` 两行 | `HellDifficulty`（A/B 权重只可能 roll 出 1/2）→ `GetItemRarity` 原样 ladder（首档是 rarity 1） |
| `[item drop ref table]` 等级窗口 | 生成装备池：`[grade]` 窗口 `L-minOffset..L+maxOffset-1`，rarity、`[creation rate]`>0、排除 `[special monster drop]`，**保留** random-option（hell 的 `keepRandomOption=true`） |
| `etc/hellparty.etc [difficulty]` | 每次击杀 8/6/4 次 roll、奖励缩放、A/B 难度权重、event |
| `[hellparty monster group]` | 89 组；flag=1 → APC 侧通道（role 5），flag=0 → 房表怪 |
| `worldmap/*.wdm [hell dungeon]/[hell quest]/[hell freepass item]/[item condition]/[dungeon]` | 51 个副本的深渊配置；`Offline70HellEntryFG356.Check` 做免票/条件道具/等级闸门 |
| `.dgn [seal door ...]`（已有 `Seal-Doors-FG.cs`） | 30/31 个封印门 → 地狱房 map；`CurrentMap` 走原逻辑 |
| `map/*.map [monster]` + `[special passive object] [hellparty]` | 房表基础怪 + 每个 order 按难度权重抽 1 组 |
| `.mob [hell monster]` | 1050–1053：组计数照常推进，但**不**产生 hell 掉落（原生同序） |

## 3. 入口语义（单机）

1. op16 请求第 4 字节 `ENUM_DUNGEON_TYPE==1` 表示深渊；第 5 字节是 native 的 party/blood 字节（0/1），
   第 6–8 字节必须为 0。
2. 世界图不是 `[hell dungeon]` 时：按原生 `CheckHaveHellPartyPassItem` 的"直接放行"分支进入普通副本
   （不扣票、无地狱房）。
3. 是深渊图时：
   - 持有任一 `[hell freepass item]`（如 2749501）→ 直接通过；入口按 `it->second` 扣 1 个；
   - 否则要求 `dungeon minLevel >= 45` 且每个 `[item condition]` 都持有
     `getHellPartyEntranceItemNeedCount(minLevel) = (40*minLevel-1800)/100+10` 个（45 级=10、70 级=20）；
     不足时回 native `{0,0x1b,count,u16 items}` op16 错误包；
   - 通过后按 `[difficulty]` 的强制权重 roll 深渊难度（60% → 1、40% → 2），并记录 event 类型；
   - 扣道具走 receipt 账本（`<runId>-hell-pass`），重放/崩溃不会二次扣。
4. 封印门在 hell run 内按 `[seal door appear rate]` 掷骰（1%–10%）。`DNF70_HELL_DOOR=1` 可强制开（测试）。
5. 走进封印门格 → 服务地狱房 map（如 60026），房间花名册 = 房表怪 + 组怪；组怪 APC 走侧通道；
   本房全部组怪死亡的那一击按难度 roll 8/6/4 次 hell 表；APC 死亡包同样携带掉落。
6. 房间清空（怪物 + APC）后才能继续走格。

## 4. 验收

| 项 | 结果 |
|---|---|
| `python scripts/check_repo.py` | 428 文件/179 源通过 |
| `python scripts/build.py --smoke` | 编译通过、无参拒绝通过 |
| `local-run/driver/HellCheck-FG356.exe`（在 `build/stable` 运行） | **PASS checks=69**：目录 594 表/35188 行/89 组/31 房；概率阈值 335/675/875/1000；稀有度 ladder；31 个封印门副本全部有 profile+房表+MapRolls 行；入场闸门（12 个 4152、免票、低等级、非深渊图回退）；地狱房 60026 组怪 + APC；最后一击 8 次 roll 并落 2 件装备 |
| `Act4MechanicsCheck.exe DNF70-Channel-FG.exe` | ALL CHECKS PASSED（封印门在普通 run 仍恒关） |
| 服务启动（17099 + 一次性 store） | `LISTENING`，无加载异常（目录随构造器加载） |

复跑：

```bash
python local-run/pvf/gen_hell_drops.py --write
cd Server-70s3a3 && python scripts/build.py --smoke
cp local-run/driver/HellCheck-FG356.exe Server-70s3a3/build/stable/
cd Server-70s3a3/build/stable && ./HellCheck-FG356.exe
```

## 5. 已知边界（本批未做）

1. **op27 / op207（深渊能量条）仍是原样**：本地 op27 回复是写死的 13 字节，未填 `hellValue`/hell quest 列表。
   单人原生的深渊值本来就恒定 = `[base value]`（1，ClearDungeon 对 memberCount==1 重置），不影响入场与掉落；
   界面能量条/任务列表要动 op27 布局，需客户端验证后再做。
2. **普通装备列（pool2）与 `itemdropinfo_monseter_extra`（special）仍未接**：本批只接 hell 池。
   三者共用 `generateTable` 装备池语义，hell 的产品可以直接复用。
3. **未实机跑**：本批验收全部离线（driver + 反射检查 + 服务启动）；真实客户端走一遍深渊房门/掉落
   需要一次人工回归（建议先 `DNF70_HELL_DOOR=1` 在 33/36 试门，再关掉试自然出现率）。
4. 地狱房没有额外的 `[event monster]`/`[monster condition]` 复刻：房表怪由 PVF 房表驱动，事件怪仍受
   `DNF70_EVENT_MONSTERS=1` 全局开关控制。
5. 深渊值累积（≥2 人随机遇深渊）未实现——单机不会走那条分支（原生 `memberCount != 1` 才进入）。
