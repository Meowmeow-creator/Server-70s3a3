# 交接：暗黑城 Act4（31..37）内容移植 + rednf 差距报告索引

日期：2026-10-15。分支：`main`。范围：只写 `D:/70S3A3`（本文件用正斜杠书写，原因见 §9）。

## 1. 本轮做了什么

把客户端 PVF 里的 **暗黑城 Act4 七个副本（31..37）** 变成服务端可玩内容：

| id | 目录 | 名称(PVF) | 尺寸 | 最低等级 | 基准等级 | 通关基准 | 经验权重 | 首领 |
|----|------|-----------|------|---------|---------|---------|---------|------|
| 31 | map/shallowkeep | 浅栖之地 | 4x5 | 37 | 40 | 7 14 21 28 30 | 1.0 | 普通怪 k3 |
| 32 | map/marpissalair | 蜘蛛洞穴 | 6x5 | 38 | 41 | 7 14 21 28 30 | 1.0 | 普通怪 k3 |
| 33 | map/kingsruins | 王的遗迹 | 6x1 | 52 | 55 | 0 0 0 0 0 | 1.0 | 普通怪 k3（单体） |
| 34 | map/magmacave | 熔岩穴 | 3x8 | 40 | 43 | 6 12 18 24 26 | 1.2 | 3 个 k3 |
| 35 | map/darkelfcemetary | 暗精灵墓地 | 5x3 | 39 | 42 | 6 12 18 24 26 | 1.5 | 普通怪 k3 |
| 36 | map/underfoot | 暗黑城入口 | 6x3 | 51 | 54 | 6 12 18 24 26 | 1.0 | 普通怪 k3 |
| 37 | map/shadowmaze | 暗影迷宫 | 3x3 | 41 | 44 | 6 12 18 24 26 | 1.2 | **APC 1301（[ai character]）** |

入口：阿法利亚（town4）。`town/alfhlyra.twn` 的 area2/3/4 是 `[dungeon gate] 5/11/13`；
area2（gate 5）是 Act4 组，本版本 area3/4 没有别的可实现内容，故三处都下发同一份 31..37 列表。

## 2. 推导规则（可复现，不看游戏也能做）

规则来自 rednf 对原版二进制的逐字节重建（`remake/source/DNFServer/GameServer/Game/`）：

- `STDungeonScript/ImportDungeonScript.inc`：`[size]`→宽高；`[greed]`→按 2 字符符号逐个写入
  格子值（`g_gridSymbols` = AA/BB/…/PP → 0..15，A=0 是墙）；`[map specification] x y 候选…`
  → 该格候选；`[start map]/[boss map]`；`[minimum required level]/[basis level]/[champion]/
  [experience increasing point]`。
- `STMapScript/ImportMapScript.inc`：房间 `.map` 的 `[dungeon]`=归属副本、`[type]`=[normal]/[boss]、
  `[greed]`=符号序列按 2 个一组读成 (gridValue, directionMask)、`[monster]` 行 =
  `id m4 m8 x y m14 m18lo m18hi [fixed|half|ratio] [normal|champion|super champion|boss]`。
- `CDataManagerMethods/load_map_files.inc`：`dungeon->InsertMap(gridValue, directionMask, map)`
  ——房间按 (格子值, 方向掩码) 注册。
- `BattleMonsterDepsC2/get_candidator_map.inc` + `BattleMonsterTailC3B/ChooseRandomMap.inc` +
  `CMap/CheckEntrance.inc`：格子取房间时用 `m_mapCandidates`（有 `[map specification]` 时）
  否则 `ChooseRandomMap(mapType, gridValue, dir)`（首领格用 boss 列表），并过滤 CheckEntrance。

由此得到本服务端表的等价规则（写入生成器）：

1. `Map(id,x,y)` = 该格 gridValue 下注册的房间；有 `[map specification]` 的格子取首个候选（首领格
   取首个 `[boss]` 型房间）；否则取该 gridValue 下 map.lst 索引最小的房间。
2. `Mask(id,x,y)` = **该格的 gridValue 本身**（bits 1=E、2=N、4=W、8=S，`CanMove` 用的就是它）。
3. `Monsters(map)` = 房间 `[monster]` 行，`level = m4 ? basisLevel + m8 : m8`，`kind` 取末尾标签
   （normal/champion/super champion/boss → 0/1/2/3）。
4. 房间 id 是**全局 `map/map.lst` 索引**（不是文件名里的数字）。PVF 里 map 路径必须全小写。

**验证**：把该规则套到已经上线的 21 个副本（3..9/1000/11..17/21..27）上，
结构字段（尺寸/最低等级/起点/首领）、全部 `Mask`、全部 `Monsters` 完全一致；
`Map` 有 45 处与既有表不同，但**每一处都是同一 gridValue 下注册的另一个合法房间**
（原表是当年实录原版随机挑的那一个），判定 `ELIGIBLE-ALTERNATIVE`，无一处非法。
命令：`python gen_dungeon_content.py compare 3 4 5 6 7 8 9 1000 11 12 13 14 15 17 21 22 23 24 25 26 27`
→ `MISMATCHES: 0`。

## 3. 改了哪些文件

生成器（本地工具，非仓库）：
- `local-run/pvf/gen_dungeon_content.py` — 导出/解析/推导/比对/**打补丁**一条龙；
  `compare` / `emit` / `drynun` / `patch` / `patch_levels` / `patch_floor_catalog` 模式。
- `local-run/pvf/recon_act3_maze.py` — 早期侦察脚本（保留，用于对照房间注册表）。
- `local-run/pvf/export/act4_expected.tsv` — 验收用的期望清单（由生成器写出）。

服务端（`src/Tools/VmTestKit/`，均为增量修改，原文件备份在同目录 `*.bak`）：
| 文件 | 改动 |
|------|------|
| `Forest-Content-FG75.cs` | `Supported` 增加 `id>=31&&id<=37`；7 个 switch 增加 31..37：尺寸/最低等级/起点/首领 + 92 条 Map + 92 条 Mask + 81 条 Monsters |
| `Clear-Experience-FG116.cs` | `Basis` 增加 31..37 = 40/41/55/43/42/54/44（来自 `[basis level]`） |
| `Monster-Experience-FG115.cs` | `DungeonWeight` 增加 31..37 = 1.0/1.0/1.0/1.2/1.5/1.0/1.2（`[experience increasing point]`） |
| `Elite-Monsters-FG118.cs` | `DungeonBasis` 增加 31..37 五档（`[champion]`） |
| `Floor-Catalog-FG119.cs` | `MapRolls` 增加 81 张房图（每怪 {1,1} 行，沿用既有表格式）；`MonsterPools` 增加 41 个 Act4 怪（**空池**，见 §5） |
| `Dungeon-Selection-Protocol.cs` | `SupportedInArea` 增加 `town==4&&(area==2\|\|3\|\|4)&&id>=31&&id<=37`；新增 `Act4Body()`；op5 对 town4 下发 Act4 列表 |
| `Town-Content-FG75.cs` | `DungeonGate` 增加 town4 area2/3/4（生成器 `gen_town_content.py` 会原样保留该行） |
| `General-Stack-FG126.cs` | `Append`：`level>40` 直接返回（数据域见 §5），不再拒绝击杀 |
| `Clear-Items-FG125.cs` | 新增 `HasTable(level)`；`Offer` 在无卡表等级（如 Act4 的 40..55）返回**空卡报价**；`Claim` 对空付费卡不再扣费 |
| `Dungeon-Flow-FG.cs` | `BossKilled()` 遇到 Act4 首领房改用 APC 状态；`RoomBody()` 追加 APC 条目（优先进 `[ai character]` 槽位） |
| `Batch-Service-FG.cs` | uid 900 的死亡在 Act4 首领房走 `KillBossApcFG176`（否则仍走 GSD 路径）；`FriendlyDeathOwnerFG140` 放行 APC；`TestSweepRoom` 也会收掉 APC |
| **新增** `Act4-Boss-Apc-FG176.cs` | 暗影迷宫首领 APC 的状态机（RunId 归属，避免改 `Begin()`） |

`docs/` 下新增两份 rednf 差距报告（见 §7）。

## 4. 暗影迷宫（37）的首领不是怪物

`map/shadowmaze/b1..b4.map` 是空 `[monster]` + 一条
`[ai character] 1301 <x> <y> 0 `[monster]` `[boss]` <cx> <cy>``；
`aicharacter/aicharacter.lst` 里 1301 = `swordman/Sayoung/Sayoung.aic`，其 `[minimum info]`
第 6 个字段 = 47（等级；201 GSD 同处读到 84，正是既有 GSD 房体里硬编码的 84，互为印证）。
`[ai character]` 的 role 标签 → roleType 5/6/7/8（normal/champion/super champion/boss），
房体里 APC 槽位第 6 字节用的就是它（既有 GSD 写 5 = `[normal]`，本处写 8 = `[boss]`）。

实现：进入首领房时房体追加 `uid=900, apc=1301, level=47, role=8`；客户端用与 GSD 相同的 op42
上报该 uid 死亡；`ReadyToClear` 改为“APC 已死”。整条路径只对 `DungeonId==37 && 首领格` 生效。

## 5. 明确未接的部分（不要当成已完成）

| 缺口 | 证据/边界 | 影响 |
|------|-----------|------|
| Act4 怪的**特定物品掉落池** | `Floor-Catalog` 的 `MonsterPools` 是原版表提取物；41 个 Act4 怪目前写空池 | 只有金币掉落，无专属物品 |
| **通用堆叠掉落** level>40 | `data/floor-stack-FG126.dat` 是 480 张表 = pool{1,3}×rarity{0..5}×level{1..40}，Act4 怪 41..58 在域外 | 无堆叠物；`Append` 改为跳过而非拒绝击杀 |
| **翻牌卡** level>39 | `data/card-items-FG125.dat` 上限 39（Act4 基准 40..55 在域外） | 通关给空卡位（marker=1）、付费卡不扣费；不是“发错卡”而是“没卡” |
| **建筑/对象掉落** | `object-drops-FG167.xml` 无 Act4 地图条目 | 日志 `OBJECT_ITEMS_PLANNED rows=0`，无建筑掉物 |
| `[monster condition]`（25 个 Act4 房间有） | 我们无条件怪系统 | 首领房条件怪（如 35 的 e54xx/h54xx）不生成 |
| `[event monster position]`（152 个房间有） | 我们无事件怪系统 | 事件怪不生成 |
| 友善 APC 房（`peris`/`apc(1,2)`/`perisstart(4,0)`） | 这些是同一 gridValue 的“另一个合法房间”，确定性取最小索引时不会选中 | 少了可选剧情 APC，不影响通关 |
| town4 gate 11/13 的真实语义 | 客户端 `[dungeon gate] 11/13` 具体对应哪组副本未取到证据 | 当前与 gate5 共用 Act4 列表（fail-open，接入别的副本时需改） |
| 等级 70 测试角色的经验增量 | `actualDelta=0`（70 级上限） | 验收里 op35 的 base/rank 为 0；服务端日志 `award=` 仍显示公式结果 |

## 6. 验收

静态（不需要服务）：
```bash
cd D:/70S3A3/local-run/driver
csc -nologo -target:exe -main:Act4ContentCheck -out:Act4ContentCheck.exe @act4.rsp Act4ContentCheck.cs
Act4ContentCheck.exe "D:/70S3A3/local-run/pvf/export" "D:/70S3A3/Server-70s3a3/data"
# 期望：PASS Act4 content 31..37: tables, masks, rosters, reachability, selection, catalog coverage and degradation
```
它检查：31..37 是否受支持、尺寸/最低等级/起点/首领/基准/权重/五档 champion 与 PVF 一致、
每格 Map/Mask/Monsters 与 PVF 一致、map id 在 `map/map.lst` 里存在、相邻格移动规则对称、
**起点能靠 CanMove 走到首领格且每个房间都可达**、首领房可清（k3 或 APC）、
城镇/选区接线、掉落目录覆盖、以及 level>40 与 Act4 等级的“降级不报错”。

隔离线上（一次性存档 + 17000 段端口）：
```bash
# 1) 隔离构建（注意：线上 7002 在跑时会占住 build/stable，所以编到隔离目录）
cd D:/70S3A3/Server-70s3a3
python -c "import glob;[print('\"D:'+chr(92)+'70S3A3'+chr(92)+'Server-70s3a3'+chr(92)+'src'+chr(92)+'Tools'+chr(92)+'VmTestKit'+chr(92)+p+'\"') for p in sorted(glob.glob('src/Tools/VmTestKit/*.cs'))]" > D:/70S3A3/local-run/isolated-test/act4.rsp
"C:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe" -nologo -target:exe -main:Offline70BatchServiceFG \
  -out:"D:/70S3A3/local-run/isolated-test/build/DNF70-Channel-FG.exe" "@D:/70S3A3/local-run/isolated-test/act4.rsp"
cp D:/70S3A3/Server-70s3a3/data/* D:/70S3A3/local-run/isolated-test/build/

# 2) 一次性存档 + 隔离频道服（目录名必须是 DNF70-batch-test-FG）
cp -r D:/70S3A3/local-run/DNF70-batch-test-FG D:/70S3A3/local-run/isolated-test/DNF70-batch-test-FG
rm -f D:/70S3A3/local-run/isolated-test/channel-isolated.log*
DNF70_TEST_ONE_HIT_KILL=1 D:/70S3A3/local-run/isolated-test/build/DNF70-Channel-FG.exe \
  D:/70S3A3/local-run/isolated-test/channel-isolated.log \
  --batch-store D:/70S3A3/local-run/isolated-test/DNF70-batch-test-FG --test-port 17003 --interactive

# 3) 两条验收
cd D:/70S3A3/local-run/driver
Act4LiveCheck.exe 127.0.0.1 17003                      # 用一击必杀辅助
Act4LiveCheck.exe 127.0.0.1 17003 --manual-kill        # 不开辅助，全部用真实 op42 击杀上报
# 期望：PASS Act4 live (31..37 entry, rooms, route, clear, return)
```
线上验收每个副本都走完：op15 选区列表=31..37 → op16 入场 → op29 房体与表逐字段一致
→ 沿 CanMove 路线移动 → 首领房 → op49 结算（金币入账、卡位空、经验公式已算）
→ op73 结束资格 → op75 回城到 `DungeonPortal(4,2)` 门外 16 像素 = (1038,238)。
日志证据：7 条 `DUNGEON_CLEAR_COMMITTED`（basis=40/41/55/43/42/54/44，award>0）；
唯一的 `REJECTED` 是 37 第一次 op49（辅助扫场的时序），重试即通过。

回归：`python scripts/check_repo.py` → `repositoryChecksPassed: true`（146 源文件）；
`python scripts/build.py --smoke` 在本机因**线上频道服占用 `build/stable/DNF70-Channel-FG.exe`**
无法写出产物（不是编译错误）——需要在玩家下线窗口重跑并重启服务。

## 7. rednf 差距报告（第二项任务）

- `docs/rednf-feature-gap-gameplay-2026-10-15.md`（60 项玩法系统）
- `docs/rednf-feature-gap-infra-2026-10-15.md`（30 项基础设施/服务）
两份都是子 agent 读 rednf 源码得出的**线索表**，不是最终结论，使用注意：
1. 大致 15 行是“只凭类名判定 absent”，没有逐项核对我们的 146 个源文件（报告末尾的
   unverified leads 已承认）；引用具体类名/行号的（NPC 心情、网格迷宫、事件怪、任务触发、
   队伍传送、封印门、拍卖、个人商店、可骑乘、村庄怪、任务 NPC 分级）我抽查过，方向可靠。
2. `fixable offline = YES` 被过度使用：数据表/协议布局/确定性公式类判 YES 是对的；
   AI 手感、经济与 PVP 平衡、卡表迁移等即使静态可写也必须真人验收——报告里 PVP 一行
   自己就矛盾（表格 YES、正文说 balance 需实测）。
3. 基础设施报告的“价值”排序是按代码量，不是按我们的需要：单进程本地服真正值得抄的是
   包分发表（`DNFPacketDecoder.cpp` 的 handler 表）、定时器/事件调度、优雅退出与存档 flush、
   以及 `PacketNameTables` 这类调试用命名表；DB/14 服务/STUN 与本架构无关。
4. `auction_bot/auction_bot/pvf.py` 是**新版 NKPI** 容器解析器，读不了我们 2013 国服
   老 GUID 头 `Script.pvf`（见 `local-run/pvf/README.md` 与 `reference/rednf-comparison.md`），
   不要把它当成我们 PVF 工具链的替代。

## 8. 复现/回退

- 重新生成 Act4：`cd D:/70S3A3/local-run/pvf && python gen_dungeon_content.py patch 31 32 33 34 35 36 37`
  （幂等：会先删掉这 7 个 id 自己的旧条目再写入；`compare` 会打印 old/new 差异）。
- 回退：同目录下的 `*.bak` 是本轮修改前的原文；`gen_dungeon_content.patch_levels/patch_floor_catalog`
  也各自留了 `.bak`。回退后需重跑静态+线上验收。
- 生成器改表前会打印“old vs new”（playbook §7.4 的要求），Act4 的对照数据在
  `local-run/pvf/export/act4_expected.tsv`。

## 9. 环境坑（本轮再次踩到）

- 工具层会把文本里的 `\70` 当八进制转义（`\070` = `8`），写成 `D:8S3A3`；
  交接文档里 `D:8S3A3 eference` 就是这个事故的旧痕迹。写文件内容/命令时避免
  在路径里写反斜杠 + `70`，或改成正斜杠 / `chr(92)` 拼接。
- 服务端证据日志与分片用 CreateNew：重启前必须删 `channel-isolated.log*`（否则静默闪退）。
- 线上 `build/stable/*.exe` 被运行中的服务占用 → 不能在玩家在线时重建。

---

# 追加（同日第二轮）：暗影迷宫首领结算修复 + 诺伊佩拉（1502）接入

## 10. 暗影迷宫首领死后不弹结算（评分/翻牌）——根因与修复

现象（玩家实测 + 服务端日志）：首领房 `dungeon=37 map=7027` 载入后服务端确实杀掉了 APC 并发出死亡通知
（`ONE_HIT_KILL_SWEEP room=0 monsters=0 killed=1` + `SENT act4-boss-apc-died opcode=38`），
但整局客户端 **op42（死亡上报）0 条、op49（评分请求）0 条** → 不触发结算 UI。

根因（原版证据）：`remake/source/DNFServer/GameServer/Game/BattleMonsterDepsC2/add_monster_APC_AI.inc`
里 `mob.m_instId = seq++` —— APC 的实例号取**和同房间怪物同一个连续计数器**，所以落在客户端认得的
100..163 段；我们沿用了 GSD 的写死 900，客户端按实例号认人/校验，900 被丢弃，于是"首领没死、副本没清"。

修复（已上线，2026-10-15 14:32 构建）：
- `Act4-Boss-Apc-FG176.cs`：新增 `ApcUidFG176 = 100 + CurrentCount`、`ApcSpawnedUidFG176`（记录本次实际发的号）、
  `ApcRoutingFG176(uid)`（兼容旧 900）、`ApcArmFG176()`；`KillBossApcFG176` 改用路由判定。
- `Dungeon-Flow-FG.cs`：房间包两处 APC 槽位（Act4 首领 / GSD 友军）都发 `ApcUidFG176` 并 `ApcArmFG176()`。
- `Dungeon-QuestCompanion-FG130.cs`、`Batch-Service-FG.cs`：GSD 死亡校验、死亡所有权校验、`Kill()` 路由、
  辅助扫场统一走 `ApcRoutingFG176`/`ApcUidFG176`。
- 验收：隔离实例两轮（辅助扫场 + 全程真实 op42）均 PASS，手动轮一次 op49 即通过、零异常拒绝；
  `Act4LiveCheck` 新增断言：首领房条目必须是 `uid=100 且 apc=1301 level=47 role=8`。
- **待真人确认**：客户端是否因此弹出评分+翻牌结算页（驱动只能验到"服务端认可首领死亡、op49 可通过"）。

## 11. 诺伊佩拉（1502，Ancient/Noirpera）接入

内容（生成器推导，`python gen_dungeon_content.py compare 1502` = 0 差异）：
5x2 迷宫、7 个房间（map.lst 7101..7107）、47 只怪、起点 (0,0)、首领 (4,0)；
首领房 `map7(4,0)_boss.map` 的首领是 **普通 `[monster]` 61001（NewMonsters/Direzie/Direzie.mob，等级 65）**，不是 APC；
`[minimum required level] 61`、`[basis level] 64`、经验权重 1.0、`[champion]` 全 0。

接线：`SupportedInArea` town4 改为 `area2 → 31..37`、`area3 → 1502`、**area4 → 无（fail-closed）**；
`DungeonGate(4,3)` 保留、`DungeonGate(4,4)` 去掉；op5 对 `town4/area3` 下发 `AncientBody()` = `{1502}`。
（area3 = 客户端 `[dungeon gate] 11`，其"就是古代副本门"这一点仍是推断，未取到直接证据。）

验收（我（main agent）独立复跑，非仅子 agent 自报）：
- 静态 `driver/NoirperaCheck.exe "local-run/pvf/export" "Server-70s3a3/data"` → PASS
- 隔离线上 `driver/NoirperaLiveCheck.exe 127.0.0.1 17003` → PASS（area0→area3 门、op5 列表=1502、
  入场、沿 CanMove 路线走完、结算金币>0、回城落点 (1038,238)），日志零 REJECTED
- `python scripts/check_repo.py` → PASS（146 源文件）

1502 的已知缺口（与 Act4 同类）：基准等级 64 超出通用堆叠（≤40）/翻牌卡（≤39）数据域 → 空卡位、无堆叠物；
怪物专属掉落池由另一路（`.mob` 提取）统一补齐，见 §12。

## 12. 追加（同日第三轮）：阿法利亚 gate 归位、证据日志降级、客户端反汇编结论

### 12.1 area3 必须同时接受暗黑城（2026-10-15 玩家报"进不去暗黑城"）
玩家在城市 4/area3（gate 11）连点 8 次 op16 全被 `Dungeon selection outside supported test profile` 拒——
**客户端在该门显示的是它自己那份暗黑城列表**（此前 fail-open 恰好一致，所以能进）。归位做法：
`SupportedInArea(4,3)` 现在同时接受 `31..37` 与 `1502`，`AncientBody()` 下发 `{31..37,1502}`；
`area2` 仍只 31..37、`area4` 仍 fail-closed。验收驱动新增 `Area3Check`（门→op5 列表=两组→op16(31) 接受→op45 中止→回城），
用**部署版二进制**在隔离实例复跑 PASS（同时 area2 的 31/37 全流程 PASS）。

### 12.2 证据日志改为"写不了就降级"（用户批准）
- `Gameplay-Evidence-FG171.cs`：写入失败不再抛回业务路径——置 `degraded`、丢弃后续诊断、stderr 打一行；
  `Dispose` 也不再因"未写完/failure"抛异常（曾导致服务在退出时被记为崩溃）。
- `Batch-Entrance-FG.cs`：`log` 打开失败（重复实例/文件被占）退回 `TextWriter.Null` 继续服务。
- 意义：此前"日志撞名（CreateNew）→ 进程静默消失 → 看门狗重复拉起"的整类事故被消除；
  handover §2.3 与 local-run 文档里"必须删日志"的规矩仍然推荐（诊断完整性），但不再是致命条件。

### 12.3 客户端反汇编取证（子 agent，静态）
目标 `地下城与勇士/DNF70_v50.exe`（55MB，玩家在用的本地改版）。结论：
- **客户端没有 100..163 实例号范围校验**（全二进制穷举 `cmp` 组合，置信度高）——
  "900 被范围检查丢弃"的机制假设被**否证**；但原版 APC 实例号来自与怪物同一计数器这一事实不变，
  改号仍是当时唯一有据可依的动作（子 agent 的独立建议也是"换成怪物段实例号试"）。
- 本地改版的收发分发表在 `0x00790C04`（256 项，节 `.s70net`，原版 DNF.exe 无此表）：
  op29 房体 → `0x0078FC78`（解析 `0x0099FF90`→`0x00935F50`）；op38 死亡 → `0x0078F1EA`（`0x00780750`）；
  op49 评分 → `0x0078CCEC`。
- **op49 的触发条件未定位**（低置信度）；actor 查找用的字段（index/instId/mobIndex）未定。
- 下一步（动态）：x32dbg 在 `0x0099FF90`（房体解析）与 `0x00780750`（死亡）下断，观察客户端用哪个字段做 actor key；
  或在 `0x00620C60` 反向追 op49 的调用条件。

### 12.4 掉落池状态
`Floor-Catalog-FG119.cs` 的 `MonsterPools` 由子 agent 用 `monster/*.mob` 的 `[item]` /
`[common champion drop item]` 等段重生成（生成器 `local-run/pvf/gen_monster_pools.py`）。
在它收尾前，本轮部署用**快照构建**（`local-run/snapshot-src/`）把缺失的 40 个 id 补回空池占位，
保证"杀怪不报 Floor monster profile"；其真实池写入后需要一次重建+重启。

## 13. 追加（同日第四轮）：掉落池落地、37 首领闪退与替身方案

### 13.1 怪物专属掉落池已接（真池，非空池）
子 agent 用 `monster/*.mob` 的 `[item]`(kind0) / `[common champion drop item]`(kind1) /
`[super champion drop item]`(kind2) / `[boss drop item]`(kind3) 重生成 `Floor-Catalog-FG119.cs` 的
`MonsterPools`：**对既有 108 个真实池拟合 108/108 完全一致**；写入 40 个缺失 id；
`Forest-Content-FG75.cs` 引用的 155 个怪 id 现在 **0 缺失**（生成器 `local-run/pvf/gen_monster_pools.py`）。
实测（部署版二进制、隔离实例）：Act4 房间出现 `source=specific-normal` / `source=specific-kind` 掉落 ✓。

### 13.2 暗影迷宫首领：客户端"静默自我终止"→ 临时替身
玩家实测：APC 首领（实例号改为 100 后）**能出现、能打**，但**客户端在处理它的死亡瞬间静默退出**
（Windows 事件日志无任何崩溃记录 ⇒ 自我保护式终止）。服务端无异常（已发 op38，uid=100）。
处理：
- 首领房 `MonsterLists[7027]` 改为**替身普通首领** `{50098,47,3}`（该副本自己的精英怪，boss 档），
  `MapRolls[7027]` 同步为 1 行；生成器加 `APC_BOSS_STANDIN` 常量保证再生成不丢。
- APC 路径保留在 `Act4-Boss-Apc-FG176.cs` 的 `Act4ApcBossEnabled`（现由环境变量 `DNF70_ACT4_APC_BOSS=1`
  打开，默认关），供反汇编子 agent 复现与将来切回。
- 验收：37 全流程（入场→路线→首领→结算→回城）PASS、零拒绝；首领房房体含 1 条 kind=3 怪物 ✓。

### 13.3 阿法利亚 area3 同时接受两组（客户端在该门显示暗黑城列表）
见 §12.1；现在 `AncientBody()` = `{31..37,1502}`，`SupportedInArea(4,3)` 同时接受两者。

### 13.4 入口服新增 `--channel-port`（隔离调试）
本地改版入口把频道地址写死为 `127.0.0.1:7002`，导致隔离调试的客户端会被指向线上服。
`Batch-Entrance-FG.cs` 现在支持 `--channel-port 17000..17999`（默认 7002），隔离栈可用
入口 17011 + 频道 17003，客户端参数里的入口端口换 17011 即可，不碰玩家会话。

### 13.5 通用堆叠 / 翻牌卡可行性结论（子 agent 调研）
- **FG126（通用堆叠）**：`data/floor-stack-FG126.dat` 的物品 id 在 PVF 各表中**找不到来源**
  （不在 `etc/itemdictionary/itemdictionary.etc` 里）⇒ 无法从 PVF 重生成，需原版表提取。
- **FG125（翻牌卡）**：`etc/itemdropinfo_monseter.etc` 的 `[item drop ref table]`（99 行，覆盖 1..99 级）
  说明等级域不是障碍；但卡表条目 id 与 itemdictionary 的"字典 id"不一致，**字段语义未解**
  ⇒ 下一步需要解 `itemdictionary.etc` 的 13 个数字字段含义，才能重生成 39..70 的卡表。

### 13.6 另记：`data/DNF70-channel-info-CG.etc` 里的 `[dungeon]` 区块
入口服下发给客户端的区域分组数据（region → 副本 id 列表）。现有块 6/7 = Alfhlyra / north_myre
= `31,32,33,34,35,1500,50`。它是客户端 UI 的区域分组来源之一，后续应与我们实现的集合对齐
（补 36/37/1502、去掉未实现的 1500/50）；本轮未改（现有 op5 列表已能正常进本）。

## 14. rednf 作为"业务蓝本"的使用规范（2026-10-15 定）

用户问：rednf 能否作为查漏补缺的蓝本，从阿法利亚开始。结论：**能，但只能当"行为/逻辑蓝本"，不能当数据源**。

- 可用（高价值）：服务端**语义/状态机/公式/字段布局**，且它是逐字节重建（注释带 ORIG 地址）⇒ 可信度高。
  今天的关键修复（房间包计数含 APC、APC 实例号与怪物同计数器）都来自这里。
- 不可用：**内容数据**。其内容在子模块 `.origin_1031`（llnut/dnf），本机是**空目录**；且目标是台服 1031（80 级），
  与国服 70 级不同版本 ⇒ 地图/怪物/物品/任务等"是什么"必须继续从我们的 PVF 取（Act4 已是此流程）。
- 不可照抄：协议/加密（客户端版本不同）、14 服务/RDAR/MySQL 架构 ⇒ 只借语义。
- 差异风险：台服与国服的细节（等级/掉率/文本/开放集）可能不同 ⇒ 每处都要用我们的证据（PVF/抓包/隔离验收）复核，
  真人验收不可替代（手感/平衡）。
- 现成示例（已核实）：**强化/增幅**——rednf 有 `CItemUpgrade`（概率/惩罚/MT19937 用法逐字节）、
  `CItemAmplifier`（增幅材料/惩罚类型/选项生成）、`CCompoundAvatar`、`CEnchanterExtraction`；
  我们 PVF 有配套表 `etc/upgrade.etc`、`etc/chn_high_ranking_upgrade.etc`、`etc/amplifyupgrade.etc`、
  `etc/amplifyitem.etc`、`etc/upgrade_separate.etc` ⇒ "逻辑从 rednf、数据从 PVF"的完整闭环，
  可直接用于"普通凯莉强化错误19"这条待办。

### 14.1 区域归属（官方证据）
客户端原始 `etc/channel_info.etc`（与 `data/DNF70-channel-info-CG.etc` 同源）的 `[dungeon]` 区块：
1 `[elven_guard]`=1,2 / 2 `[granfloris]`=3..9,1000 / 3 `[sky_catle]`=11..17 / 4 `[behemoth]`=21..27 /
5 `[stormpass]`=40,41 / 6 `[Alfhlyra]`=31 32 33 34 35 1500 50 / 7 `[north_myre]`=同上。
⇒ **阿法利亚 = 暗黑城(31..35…) + 古代副本(Vilmark 1500 等)** 是官方区域数据；
门号与区块序号不是简单一一对应（gate5 在客户端数据里指向 Alfhlyra 组），实际以玩家可达行为为准：
area2 = 31..37、area3 = 31..37 + 1502（当前实现）。1500 系列（古代副本）是阿法利亚的正统内容，尚未接入。

### 14.2 阿法利亚第一站的两条工作流（已派子 agent）
1. 房间机制：条件怪 `[monster condition]`(25 房)、事件怪 `[event monster]`+`[event monster position]`(152 房)、
   封印门/路径门与领主房进入条件 —— rednf 语义（`getEventMonsters.inc`/`stConditionEnterBossRoom_t`/
   `SetGridPath.inc` 等）+ 我们 PVF 数据 + 隔离验收。
2. 任务链与动态 NPC：`n_quest/alphraira/**`(106 个) 与我们任务目录的差集、144 个动态 NPC 任务的最小规则集
   （对照 rednf `NPCDynamicInfo`）、并先落地一条完整任务链。

## 15. 阿法利亚第一站：房间机制 + 任务链（2026-10-15 晚，已上线）

### 15.1 房间机制（子 agent A 实现 + 我集成修复，全部验收通过）
新文件：`Condition-Monsters-FG.cs`（PVF `[monster condition]` → 35 的 22 张图各 4 只、37 的 4 张图各 1 只）、
`Event-Monsters-FG.cs`（`[event monster]`/`[event monster3]`，6+7 个副本）、`Seal-Doors-FG.cs`（33 地图 60026/(1,0)/rate10、
36 地图 60020/(3,2)/rate1）、`Act4-Mechanics-Check-FG.cs`（27 项反射静态检查，全过）。
`Dungeon-Flow-FG.cs` 增加：`TotalCount`（普通+条件+本房间事件）、`Monster(uid)` 三类 actor、
`CurrentConditionMonsters/ConditionMonstersCleared`、`RollEventMonsters`、`IsSealDoor/SealDoorMapIdx`。

集成时我修掉的关键问题（每条都有原版证据）：
1. **事件怪字段读反**：PVF 行是 `<尝试次数> <触发概率/10000> <monsterId 61xxx> <eventIdx> <权重> <field0c> <每次数量>`
   （rednf `STDungeonScript` 0x766：两个标量 + 5 元组循环；61xxx 是 `monster/monster.lst` 里的真怪：61728 RichGoblin2 /
   64701 chn_goldenGoblin_junior）。原实现把 9000 当怪、61728 当概率，会发出不存在的怪。
2. **uid 越界**：事件怪原用 200+，超出客户端/服务端认的 100..163 段 ⇒ 玩家打死不上报、辅助也扫不到 ⇒
   `RoomCleared` 永远不成立 ⇒ **所有 Act4 副本无法通关**（严重）。现在三类 actor 统一从 100 连续编号（原版同一计数器），
   `ValidateKill/IsDead/Kill/RoomBody` 同步。
3. **刷新时机**：原来在 `MarkLoaded()`（房间包已发出之后）⇒ 事件怪永远进不了包。现在在 `RoomBody()` 组装前刷新，
   每个房间只刷一次、整本总量按表的尝试数封顶（31→4、32→4、33→1、34→3、35→3、36→3、37→2）。
4. **掉落面**：为条件怪/事件怪的 uid 补齐 `MapRolls` 行（普通+条件+事件上限）并给 7 个新怪 id 补 `MonsterPools`
   （50073-50076/50097/61728/64701，按 `.mob` 的 `[item]`/`[common champion drop item]` 派生）。

验收：静态 `Act4ContentCheck` + `NoirperaCheck` + `Act4MechanicsCheck`(27) 全 PASS；
隔离线上 `Act4LiveCheck` 对 31/35/36/37（含事件怪、条件怪、封印门、替身首领）全流程 PASS、零拒绝、首次 op49 即通过。

### 15.2 任务链（子 agent 分析 + 我接手实现与验收）
- `[target character]` 的真相（PVF `quest/epic/4-1.qst`）：每行 `[job] -1 -1`，7 个职业全列、无等级限制 ⇒
  是"全职业可用"的宽松过滤，不是进阶职业检查（rednf 侧该 tag 只写一个 `m_job` 字段）。
- 任务 39/40/41（前往阿法利亚营地 → 战争危机 → 加尔的测试）已 `Supported=true`；NPC 23/25 本就在阿法利亚 `[NPC]` 白名单里。
- 无奖励选择的副本，客户端结束请求的 selection 必须是 **0xFFFF**（`Progression-FG.cs` 的校验：
  `Selection.Length==0 && selectionIndex!=ushort.MaxValue` ⇒ 拒绝）。
- 验收（`driver/AlfhlyraQuestCheck.cs`）：39 与 40 **接取→访问 NPC→结束 全通过**；41 被"Quest not ready"正确拦住
  （它的目标是 `[condition under clear]`，需要真通一次对应副本，属预期）。
- 剩余 33 条被卡任务的 6 类卡点与下一刀建议见子 agent 报告（`[condition under clear]` 8 条、`[depend give item]` 5 条…）。

### 15.3 未验证 / 简化（不要在别处当成已完成）
- 封印门只做了静态验证（地图/坐标/概率），没有"走到该格换图"的线上证据。
- `[boss room entrance condition]` 已进生成器解析，但**尚未接线**。
- 事件怪的落点是"按房间 + 整本封顶"的简化（原版按格子摆放），且第二事件表不去重（原版规则）。

## 16. 第二轮（同日）：暗黑城机制补全 + 刹影真身 + 封印石机制（2026-10-15 19:48 上线）

### 16.1 证据裁决（两处子 agent 结论冲突的裁决 + 一处推翻）
- **客户端静默退出根因候选（S5）**：旧笔记里的 `0x00935CF8 cmp [esi+0x1d8],8` 不是 roleType 分支
  （`+0x1D8` 是 MSVC `std::string` 的 SSO 容量阈值，**推翻**）；真正会"无异常记录杀进程"的是
  secure-CRT 无效参数处理 `0x128E513 -> TerminateProcess(0xC0000417)`（GS 失败走 `0x12A0D8E`）。
  即：任何一次 checked-STL 越界都会静默自尽；这与"改 uid=100 后能打但死亡瞬间退出"吻合。
- **死亡通知包（rednf `_MakeNotiPacketDieMonster`，ORIG 0x0830b8cc）**：尾巴是
  `put_byte(0)` + `convertAPCRoleType2MonsterRoleType(roleType)!=0 ? m_apcKillCount : -1` + `flag34`。
  即 **boss 类（怪 kind1..3 / APC role6..8）死亡必须回真计数，不能回 -1**；我们此前恒回 -1（旧 `{0,255,0}`），
  本次改为按类型回计数（`Offline70LootWireFG.Death(uid,drops,apcKillCount)`）。
- **房间包条目 14 vs 16 字节**：rednf（TW1031）用 `u32 mobIndex`，我们的 CN70 客户端实测只认 14 字节
  （多怪房间逐条对齐正确、击杀/清房全对上）⇒ 维持 14。

### 16.2 领主房进入条件 + 封印石（37 的 50097）与 35 的雕像（50073-76）
PVF `[boss room entrance condition]` = `[hunt monster] <id> <spawnCnt> <maxKillCnt>`（35: 4 条；37: 1 条）。
原版语义（rednf SetGridPath/check_boss_map/kill_monster）：dungeon start 时对每条 hunt 随机挑 `spawnCnt`
个**候选格**（该格所有候选图都含该怪的 `[monster condition]`），进入该房时才把条件怪并入 mobList；
击杀按 monsterIdx 计数；**未满足时 check_boss_map 返回 7 拒绝进入终点格**。
旧实现的缺陷（本轮修复）：条件怪只在首领房生成且 37 首领房不声明条件怪 ⇒ **37 从来没有封印石**、
35 的雕像只会出现在首领房。现在：
- `Dungeon-Flow-FG.cs`：`BuildConditionStatusFG181()`（Begin 时按候选格随机落点）、
  `CurrentConditionMonsters()`（按本房格与状态匹配）、`Kill()` 计数、
  `BossRoomUnlockedFG181` / `BossConditionGateFG181`（Move 拒绝未满足的首领格）、
  `BossKilled()` 改用 `BossRoomUnlockedFG181`。
- `Condition-Monsters-FG.cs`：新增 `HuntConditions` + `DeclaresMonster`。
- 隔离线上验证：走完全部候选房后首领房放行；`--manual-kill`（真实 op42）同样通过。

### 16.3 封印门（33/36）与地狱房（60026/60020）
`[seal door map index/pos/appear rate]` + `[hell dungeon]`：原版把封印门格路由到 hell 房
（kingruins 60026 / underfoot 60020），但 dungeon_start 还要求 **hell-party 模式 + 逐成员消耗封印门道具**
（`CheckDestroyConditionSealDoor`/`UseSealDoorItems`）。本服务端未实现该模式 ⇒ 封印门保持关闭、
该格沿用本图（否则玩家会进到没有怪物表的地狱空房——33 的实测失败就是这个）。`RollSealDoorFG181()`
保留掷骰与 `HellPartyModeFG181=false` 闸门，接入时只需打开。

### 16.4 事件怪 wire 保真
rednf `add_monster_event`（ORIG 0x083023ec）：`mobId = instBase`（**10000+**，这是"事件怪"的标记区间）、
`mobIndex = evs.m_eventIdx`（PVF 元组第一项，本数据里就是 61xxx 怪 id）、`level = get_standard_level()`、
`roleType = 0`、`group = 0`，且**每房实际生成数 ≤ 本房怪数**。PVF 行读取顺序确认为
`<attempts> <prob/10000>` + 5 元组 `<eventIdx> <spawnCount> <weight> <field0c> <field10>`
（`attempts`=m_3d4→getTryGenEventMonsterCnt，`prob`=m_3d8→getTryGenEventMonsterProb）。本轮已按此修正。

### 16.5 刹影真身（不再替身）
`Act4ApcBossEnabled` 默认 **开**（`DNF70_ACT4_APC_BOSS=0` 退回替身）；`RoomMonstersFG118()` 在
APC 激活时过滤掉替身 `50098 kind3`。APC 条目（uid=100, apc=1301, level=47, role=8）与红蓝本一致。

### 16.6 客户端崩溃捕获器（现挂在运行中的客户端上）
`local-run/tools/crash-capture.py`（+ `start-crash-capture.ps1`）：ctypes 用户态调试器，
断点 = 基址+RVA：`0xE8E513`（无效参数漏斗，第一优先）、`0xEA0D8E`（GS）、`0xE8F649/0xE8F658`（CRT 退出）、
`0x70E62DD`（mod 装载器，写保护失败可忽略）。命中时记录 EIP/ESP/EBP、`[esp]` 返回地址、EBP 链与栈 hex，
随后恢复原字节继续。attach/detach 已实测（客户端存活）。**若再闪退**：读
`local-run/logs/client-crash-capture.log` 里最后一段 `[BP_HIT]`（返回地址链即出错点）。

### 16.7 验收（最终构建，隔离实例 17003 + 线上 7002/17001）
- `check_repo` 348 文件 / 150 稳定源；`build --smoke` 150 编译通过。
- `Act4ContentCheck` PASS；`Act4MechanicsCheck` **31 项全过**（新增 hunt/闸门/封印门路由 8 项，
  含"未满足时首领格锁定 → 击杀封印石后解锁"的运行时单测）。
- `Act4LiveCheck`：31/32/33/34/35/36/37 全 PASS（37 另跑 `--manual-kill` 真实 op42 通过）；
  零 REJECTED；35 会走完 22 个候选房并在随机房击杀 4 座雕像后放行首领房。
- `NoirperaLiveCheck` PASS（其 op5 期望已按 area3 联合列表更新）；`AlfhlyraQuestCheck` PASS
  （39/40 全通过；41 按设计被 `[condition under clear]` 目标拦住，驱动已把该拦截记为期望态）。
- 部署：19:48 重启线上（channel 7002 / entrance 17001 + 看门狗）；19:49 启动客户端；
  崩溃捕获器已 attach。

### 16.8 本轮未做（诚实清单）
- **翻牌卡 40..70 级物品域**：需重实现 rednf `generateTable`（type×rarity×level + itemTypeVerify）才能重生成
  `card-items-FG125.dat` 的 40..70 段；<39 级照旧。结算面板显示逻辑不受影响（空卡位）。
- **封印门地狱房**：hell-party 模式与封印门道具消耗未实现（见 16.3）。
- 33 的 `[required item] 4083` / `[ancient dungeon]` 入场道具、`[coin limit]` 未校验。
- `data/DNF70-channel-info-CG.etc` 的区域分组仍列未实现的 1500/50（客户端列表实际由 op5 驱动，实测 36/37/1502 可进）。
- 事件怪落点仍是"按房随机 + 每房上限"（原版在 dungeon start 选房，我们按房进入时掷骰；统计效果一致）。
- quest 41 的 `[condition under clear]` 目标与其他被卡任务（详见 §15.2 清单）。

## 17. 第三轮（同日 20:23-20:30）：首领结算握手 + 事件怪活动闸门（玩家实测反馈）

### 17.1 "刹影打败后没有结算页面" —— APC 首领漏跑清关尾巴（已修）
现场证据（线上 `logs/channel-evidence.log`）：`SENT act4-boss-apc-died opcode=38` 有，
**`boss-state`(127) / `clear-request`(31) 一条都没有** —— APC 首领死亡走 `Kill()` 的 APC 分支并
`return`，绕过了 `KillMonster` 尾部那段清关握手（普通首领/替身首领因为走 KillMonster 才正常弹结算）。
修复：新增 `BossClearTailFG181(uid)`（op127 `BossBody` + `SettlePetRoomFG109` + op31 clear request），
在 `Kill()` 的 APC 分支与 `TestSweepRoom` 的 APC 击杀后各调一次。
验收新增：`Act4LiveCheck` 记录全部收到的 opcode，在走完首领房后断言 **op127+op31 已下发**
（需要等 5 秒收包，因为它们紧跟死亡帧之后）。37（辅助扫场 + 真实 op42 两模式）、35、31 全 PASS。

### 17.2 "一图和二图莫名其妙出现黄金哥布林" —— 事件怪缺活动闸门（已修）
原版 `SetGridPath` 的 `[event monster]` 段有闸门：`CEventManager::GetRepeatEvent(0x24)->IsEventing` 为真才掷骰；
`[event monster3]`（64701 = `China_Monster/Event/chn_golden_goblin_junior`，中国活动怪）在 TW1031 重建里
没有闸门，但它的内容就是"活动怪"，默认满副本乱刷显然不符合预期（玩家实测：一图二图各冒一只）。
修复：`Dungeon-Flow-FG.cs` 新增 `EventMonstersEnabledFG182`（环境变量 `DNF70_EVENT_MONSTERS=1` 打开），
默认**关闭**两张事件怪表；数据与掷骰逻辑保留，将来接活动系统时只开开关。
验证：隔离实例整轮跑完，日志里 `61728/64701` 出现 0 次，扫场统计回到 `monsters=N killed=N`。

### 17.3 另记：崩溃捕获器的缺陷与加固
首版捕获器把非断点异常当"已处理"回 `DBG_CONTINUE`，导致 2026-10-15 20:12 客户端在一个访问违例上
空转（日志 210 万行、窗口句柄丢失、CPU 940s）。现改为：非断点异常一律 `DBG_EXCEPTION_NOT_HANDLED` 交还进程；
同一 (tid, code, addr) 重复 200 次即自动 `DebugActiveProcessStop` 并退出；DLL 事件日志降噪。
（教训：调试器绝不能吞掉访问违例，否则等于把进程的退出路径焊死。）

### 17.4 首领房门口"守门人"的两句提示语 = 客户端对象自己的状态机（非缺陷）
玩家观察到：未清封印石时守门人提示"未解除封印无法进入"，清完再走近则变成"你非得见他一面吗？"。
取证结论（子 agent 全量检索 122952 个 PVF 文件 + 客户端 3.0GB/1549 个文件，含 NPK/exe，GBK 与 UTF-16 双向）：
两句**字面文本**都不在可检索数据里（它们是客户端字符串表里 id 3652/3653 的条目，表被压缩/加密）。
真正的驱动者是**被动对象**：
- `passiveobject/monster/gatekeeper/gatekeeper2.obj`：`[int data] 3652 3653 50097`、`[pass type] \`[do not pass]\``、
  `[basic motion] Animation/Stay1.ani`（`[etc motion] Stay2.ani` = 两种状态各一套动作）——两个文本 id + **37 的封印石 id**。
- `passiveobject/monster/gatekeeper/gatekeeper.obj`：`[int data] 3650 3651 50073 50074 50075 50076` —— **35 的四座雕像**。
即：守门人是"挡路对象"，按它列出的条件怪是否全部死亡在两组台词/两套动作间切换。
地图侧：`map/shadowmaze/h7011.map`（我们服务在格 (1,0)，正好是首领房 (0,0) 的邻格）与 `h7013.map` 放置 gatekeeper2。
**服务端不需要下发任何台词**；我们的职责（在 4 个候选封印房生成 50097、按击杀计数、放行/拒绝首领格）已按原版实现并验证。
"你非得见他一面吗？"就是解除封印后的那句剧情台词（刹影线的守门人劝告），不是错误状态。

### 17.5 守门人台词停在"未解除封印"的真正原因 = 线上开着一击必杀测试开关（用户决定暂不修）
链路：线上频道沿用 `DNF70_TEST_ONE_HIT_KILL=1`（`start-server.bat`/`start-channel.bat`/`restart-detached.ps1` 都设），
进入房间后由**服务端** `TestSweepRoom` 把全房怪（含封印石 50097）杀掉并只回死亡通知（op38）。
客户端是战斗权威方：它自己的"我打死了封印石"账簿（门禁条件/任务计数）**只在客户端亲手击杀时推进**，
收到服务端代杀的死亡通知只会移除 actor —— 于是守门人对象（gatekeeper2.obj，`[int data] 3652 3653 50097`）
一直停在"未解除封印"那组台词，而**服务端闸门其实是开的**（同一局玩家随后也确实进了首领房，
20:37:08 首领房扫场 killed=1 = APC）。线上日志实证：`uid=112 monster=50097` 的击杀来自扫场
（`ONE_HIT_KILL_SWEEP ... killed=13`）而非玩家 op42。
结论：这是测试辅助开关的副作用，不是机制缺陷。**用户 2026-10-15 明确"先不修复"**，脚本已还原为原本的
aid=on 状态；要恢复"正常打怪"只需把三个启动脚本里那一行去掉（或只给玩家侧关掉，隔离实例保留）。
