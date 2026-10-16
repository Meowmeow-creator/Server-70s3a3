# 交接（2026-10-16）：万年雪山接入 + 掉落缺口 + 下一窗口任务

## 0. 一句话状态

万年雪山（城镇 5 / StormPass）已接入、7 个副本都能进；怪物专属掉落池已补齐；
**等级通用掉落（41..70 级）尚未实现**（配方已查清，见 §2.1）；
一批改动仍在工作区未提交（见 §2.5）。

## 1. 本窗口已完成（已构建并上线：频道 7002 / 入口 17001 + 客户端）

### 1.1 万年雪山区域接入
- 区域块 `[stormpass]` 对应副本：**40..45 + 141**
  - 40 CharnakRidge 山脊 / 41 TheBoyWithColdheart 冰心少年 / 42 RicuCeiling 利库天井 /
    43 WhiteLand 白色废墟 / 44 IcePalace 冰雪宫殿 / 45 ScasaLair 斯卡萨之巢 / 141 Bwanga 布万加的修炼场
  - **41 的房间目录是 `map/coldboy/`（与 .dgn 名不同）** → 生成器新增 `ROOMDIR = {41: "coldboy"}`；
    这是 41 一开始"进不去"（服务端 `Unsupported original room`）的根因。
- `local-run/pvf/gen_dungeon_content.py`：`DIRNAME` 增补 40..45/141，四个 patch 模式跑全表
  （Forest 地图/掩码/怪物；Clear-Experience basis；Monster-Experience 权重；Elite-Monsters champion；
  Event-Monsters；Floor-Catalog MapRolls + 专属池）；表探针确认每个副本每格都可解析
  （41 = 5801 起点 + 5802 首领房）。
- 城镇接线（`Town-Content-FG75.cs`，由 `local-run/pvf/gen_town_content.py` 生成）：
  城镇 5 的 Exists/NPC/门/副本门矩形 `14,354,19,125`；`DungeonGate` 增补 `town==5&&(area==2||area==3)`；
  `SeriaArea(5)=1`（登录落点 = 斯顿雪域赛利亚房间，之前落到广场图板外导致走不了）；
  `Economy-State-FG.cs` 的 `SaveTown`/`Validate` 城镇上限扩到 5。
- 选择协议 `Dungeon-Selection-Protocol.cs`：`StormPassBody{40..45,141}`、`SupportedInArea(5, area2/3)` 同集。

### 1.2 掉落池 A（怪物专属掉落，已修）
- `local-run/pvf/gen_monster_pools.py` 新增 `patch_missing` 模式：给"Forest 引用了但 Floor-Catalog
  完全没有行"的怪补生成行（本轮 11 个：1012/1013/1014/1016/1017/1021、50078/50079/50085/50088/50089）。
- 再跑原有 `patch` 模式填掉 36 个空占位池（含首领 1030 冷心少年）。
- 结果：该区域怪带区域材料（1013/1047/3211/3212/3227 等带权重），精英仍 3171。
- 无池 id（`.mob` 无掉落标签，原版亦然）：604/605/607/1034/61327/61806。

### 1.3 体验与工具
- 桌面快捷方式：**DNF70 启动游戏**（看门狗 + 客户端 + 崩溃捕获；已在运行则不重复启动）、
  **DNF70 重启服务端**（`restart-detached.ps1` + 拉客户端）→ 对应 `local-run/start-game.cmd`、`restart-server.cmd`。
- 文档：`docs/LESSONS-cn70-dungeon-porting.md`（通用工作流/坑）、本文件。

## 2. 未完成（下一个窗口执行）

### 2.1 【最高优先】B：等级通用掉落 41..70
- 现状：`data/floor-stack-FG126.dat` = 480 张表（pool1/3 × rarity0..5 × level1..40），且
  **只有 pool1/rarity0 有数据、31..40 全空、41..70 不存在**；`General-Stack-FG126.cs` 有
  `level>40 return;` 守卫与 pinned `CatalogSha256`；运行期 level 取**怪物等级**（≈ basis+偏移），
  与专属池 kind0 独立叠加。→ 这正是"高等级副本只掉金币"的根因。
- 配方（来源 `local-run/recon/floor-drop-source.md`，已验证）：
  - rednf `DropLayoutDeps/generateTable.inc` 的 `CItemGenRateTable::generateTable`：
    物品按 PVF `[grade]` 分桶、`[rarity]` 与 `[creation rate]`(gen_rate) 参与权重；
    等级范围来自 `etc/itemdropinfo_monseter.etc` 的 `[item drop ref table]`；
    `_itemTypeVerify` 分类（1 可堆叠 / 2 装备 / 3 任务物品 / 4 宠物）。
  - 未解谓词（需原版数据）：`isSpecialMonsterDropItem`、`IsRandomOption` → 报告中标注影响。
- 执行步骤：`gen_floor_stack.py`（PVF → 表）→ **闸门：先复现 1..40 已有数据，逐项一致才算配方正确**
  → 生成 1..70 → 扩 `General-Stack-FG126.cs` 等级域 + 更新哈希 → 探针驱动验证（1..40 关键级 +
  41..70 至少 45/46/55/64 非空）→ 重建部署。
- 验收：保真比对 100% + 探针输出 + `check_repo`/`build --smoke`。

### 2.2 翻牌奖励卡 41..70
- 同一套 `generateTable`（rednf `CClearReward/generateRandomItem.inc`）；`data/card-items-FG125.dat`
  只覆盖 1..39 → 41 级以上结算翻牌是空卡位。

### 2.3 区域内容
- 万年雪山：约 70 个任务文件（epic 两条线 + 一条线、urgent 2×24、repeat/normal）、NPC 商店、剧情对话。
- 阿法利亚：除 39/40（41 等通关目标）外的任务链、NPC 商店。
- rednf 全局项（跨区域）：NPC 动态心情与对话（144 个任务 NPC 的根）、怪物脚本 AI、
  网格迷宫随机（现为按 .dgn 确定性选图）、副本指定掉落（被动对象/陷阱）、封印门地狱房（需深渊模式）。

### 2.4 验收工具
- `SnowLiveCheck`（驱动）读服务端应答的时序未收敛：服务端日志已证明城镇门是通的，
  需要把 `Gate()`/`Load()` 的读帧改成"轮询到状态匹配或超时"，再跑 40..45/141 的整局走查。

### 2.5 提交 / PR
- 工作区未提交：`Server-70s3a3/src/Tools/VmTestKit/` 下 10+ 个文件（Town-Content、Dungeon-Selection、
  Economy-State、Forest-Content、Clear/Monster-Experience、Elite-Monsters、Floor-Catalog、
  Condition/Event/Seal-Monsters）与 `docs/LESSONS-cn70-dungeon-porting.md`、本文件。
- 按仓库规矩：提交到 `mine`（用户 fork），功能线走 `feature/...` 分支 + PR 给 `origin`（zzdiandian）；
  参考上一批做法（commit 49502eb / 分支 feature/cn70-alfharia-act4-dungeons）。
- **不要提交**：构建产物、日志、PVF、存档（`.gitignore` 已覆盖）。

## 3. 环境操作手册（照抄即可）

| 目的 | 命令 / 文件 |
|---|---|
| 起线上（频道 7002 + 入口 17001 + 看门狗） | `powershell -File D:/70S3A3/local-run/restart-detached.ps1` |
| 起客户端（分离） | `powershell -File D:/70S3A3/local-run/launch-client-detached.ps1` |
| 一键（含崩溃捕获） | 桌面「DNF70 启动游戏」/「DNF70 重启服务端」 |
| 隔离实例（17003 + 存档副本） | `local-run/tools/start-isolated.ps1`；驱动 `SnowLiveCheck.exe 127.0.0.1 17003 <storeDir> <dataDir>` |
| 停服务（构建前必做，否则 CS0016） | `taskkill /F /IM DNF70-Channel-FG.exe /IM DNF70-Entrance-FG.exe` + `local-run/tools/stop-watchdog.ps1` |
| 构建 | 先停服务 → `python scripts/check_repo.py` → `python scripts/build.py --smoke` → 重启 |
| 服务端证据 | `local-run/logs/channel-evidence.log`（`REJECTED`/`FLOOR_DROP_PLANNED`/`ACT4_BOSS_APC_DEATH`…） |
| 客户端崩溃捕获 | `local-run/logs/client-crash-capture.log` → 读最后一段 `[BP_HIT]` 的返回地址链 |
| 数据探针 | `local-run/driver/SnowTableProbe.exe`（各副本每格的 map/monsters/mask） |

开关现状：`DNF70_TEST_ONE_HIT_KILL=1`（线上开：全房怪由服务端秒杀——会让客户端自己的击杀账簿不推进，
见 §4.8）、`DNF70_EVENT_MONSTERS` 默认关、`DNF70_ACT4_APC_BOSS` 默认开（真身刹影，`=0` 退替身）。

## 4. 经验总结（本轮新增；通用部分见 `docs/LESSONS-cn70-dungeon-porting.md`）

1. **同一份内容在客户端里有两套命名**：副本脚本名 ≠ 房间目录名（41：`TheBoyWithColdheart.dgn` ↔
   `map/coldboy/`）。判断方法：在 PVF 全文件清单里**按房间文件名反查目录**，不要靠名字猜。
2. **"空占位池"和"完全没有行"是两类缺口**，工具都要能处理（`patch` vs `patch_missing`）；
   只看"空池"会整块漏掉没生成的怪（本轮首领 1017/1014/1030 就是"没有行"）。
3. **重写整文件的生成器模式要一次性传全量 id**：`patch_new_mechanics` 只按传入 id 生成，
   分两次跑会把别的副本的表挤掉（本轮踩了两次）；改表后必须复跑静态检查。
4. **数据域边界是隐藏地雷**：`floor-stack-FG126` 只到 40 级、`card-items-FG125` 只到 39 级——
   新区域等级一超域就整块丢奖励。加新区前先查它引用到的所有数据域上限。
5. **登录落点与副本门都是服务端发/判的**：`SeriaArea(town)` + 固定 `(474,234)` 决定出生点
   （新城镇不加就落到广场图板外、卡死）；`DungeonGate(town,area)` 是历史硬编码表，新区域必须显式加，
   否则客户端点门时服务端回 `UNIMPLEMENTED opcode=15`、玩家"点什么都没反应"。
6. **客户端它自己的工作台数据优先**：万年雪山客户端画 7 个副本（它自己的世界地图），
   服务端允许集必须覆盖它，否则"全都进不去"。
7. **工具的 CLI 要和函数对齐**：`patch_levels`/`patch_floor_catalog` 之前有函数没入口，
   本轮补进 `main()` 分支才跑得起来。
8. **服务端代杀 vs 客户端权威**：`DNF70_TEST_ONE_HIT_KILL=1` 时全房怪由服务端秒杀，
   客户端自己的击杀账簿（任务/门禁条件计数）不会推进——判断"机制坏了"前先看 kill 是谁杀的。
9. **子 agent 产出必须抽查**（本轮命中：房间条件怪数量、房间目录名、掉落池三类错结论），
   且它们有配额上限（本轮 B 的实现被 `Usage limit reached` 挡住）→ 关键路径留一手自己做的余地。
10. **每落地一个机制，用它的 id 全仓 grep 确认运行期真的读了它**；没有"数据在、没人用"这种假完成。
