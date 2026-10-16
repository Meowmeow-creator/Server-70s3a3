# 交接（2026-10-16 第二波）：掉落·翻牌·名册·商店·任务线 + 并发会话警示

上一份：`docs/HANDOVER-2026-10-16-snow-region-and-drops.md`（第一波：万年雪山接入）。本文件只写第二波做完的事、验证到什么程度、以及**没做完的确切原因**。所有数字都是跑出来的，未跑的一律标「未验出」。

## 0. 先读这条：仓库有并发写入

第二波进行到 18:20 之后，**另一个会话开始在同一工作区写入 FG176-182 特性**，且 19:10 仍在写：

- 新文件（未跟踪）：`Ordinary-Reinforcement-FG176.cs`、`Break-Returns-FG177.cs`、`Retry-Projection-FG178.cs`、`Protection-FG180.cs`、`World-Drops-FG182.cs` + `data/ordinary-reinforcement-FG176.xml`、`data/reinforcement-item-metadata-FG176.xml`、`data/world-drops-FG182.xml`、`data/world-item-metadata-FG182.xml`
- 被它改动的既有文件：`Batch-Service-FG.cs`、`Floor-Drops-FG119.cs`、`Coupon-Service-FG141.cs`、`Fatigue-State-FG50.cs`、`Act4-Boss-Apc-FG176.cs`

**后果（重要）**：第二波的全部验证是对 **18:20 之前的树**做的，最后一次构建是 17:21。而"任务可兑现"的判定依赖 `Batch-Service-FG.cs`（op33 接单 / op36 交付 / `AssetsKnown`）与 `Floor-Drops-FG119.cs`（计杀喂入）——这两个文件都在 18:29 被并发改过。所以**提交前必须重跑第 4 节的复验闸门**，不能直接引用这里的数字。

## 1. 已完成并已验证（第二波）

| 项 | 产物 | 我独立复核到的证据 |
|---|---|---|
| 等级通用掉落扩到 1..70 | `data/floor-stack-FG126.dat` 480→840 表；`General-Stack-FG126.cs` 守卫 40→70 + pinned 哈希 | 原 480 表逐字节零改动；CDF 单调无违例；PVF tag 不变；报价探针实证 lv41→5216 / 45→5217 / 55→9563 / 70→2659542 |
| 翻牌奖励卡 1..70 | `data/card-items-FG125.dat` 648→1680 表、2174→4820 properties；`Clear-Items-FG125.cs` 域上限 | 原 648 表与原 2174 条 properties 零改动；40/40 报价行在 41..70 出装备（lv45→10075，含 sealed 变体 63509/10085/14093） |
| 物品登记 | `data/item-metadata.xml` 3475→6121→**7029** | 三段都只追加、无重复、`<PvfSha256>` 未动、复跑幂等；容器侧 unregistered=0 |
| 副本 40 有怪可打 | `Floor-Catalog-FG119.cs` Maps 400→409 | 根因是生成器"先删后补"自毁，已改为只增不减 |
| 名册行数规则 | 同上，`gen_dungeon_content.py` | 规则 `普通+condMax+eventCap` 对 Act4 31..37 的 81 间房 81/81 印证；GSD/GBL 13 图 +47 行；幂等 md5 复跑不变 |
| 首领掉落次数 | `MapRolls` 行值改为 PVF `[monster]` 第 7/8 列 | 28 图 55 处改动、381 图逐字节不动、基线 `{2,2}=3` 原位、`{2,1}` 35→83、md5 `38861ce1` 复跑不变 |
| 雪域 NPC 商店 | `data/commerce-catalog.xml` items 3200→3246、offers 339→517、shops 20→24 | 只追加；shop46「有映射零报价」依 PVF 证据撤回；`ShopAvailable(5,·)` 实测成立 |
| `[hunt enemy]` 计杀链 | `Progression-FG.cs`（`HuntStride`/`HuntGoal`，5 列、goal 末列） | 引擎内 30/30 断言 + 线路级 60/60（含杀前交付被拒、难度不足不计、杀错怪不计、重复凭据不重复计、4014 仍拒） |
| 任务翻绿 | `data/quest-catalog.xml`：hunt enemy 14 条 + 阿法利亚 66 条，总 true 1176→**1256** | 3051 硬钉未动、非目标记录逐字节不动、幂等；我的 66 条 0 未登记物品、0 前置未绿 |
| 验收工具 | `local-run/driver/SnowLiveCheck.cs`（轮询）+ `run-walk.ps1` | 7 副本走查 126 OK / 0 FAIL；`run-walk.ps1` 已补"每次重置存档"（否则疲劳度耗尽会假报 FAIL） |

## 2. 被证伪或被纠正的结论（别照抄旧文档）

- `local-run/recon/floor-stack-FG126-analysis.txt` 说"只有 pool1/rarity0 有数据" → **错**，原文件 `(1,2)`/`(3,2)` 各有 36 级非空。
- 第一波说"高等级只掉金币的根因是 level>40 守卫" → **只对一小半**。阈值尺度与槽位映射经 PVF 重算 160/160 全等，稀有度切点与 `[basis of rarity dicision]` 逐字相等，`rarity0` 在 31+ **本就该空**（14 件 rarity0 全是 `[throw]`、grade 上限 24；rarity1 全 PVF 0 件）。真实触发率：basis42 阈值 `10/10000`（pool1）/`5/10000`（pool3），41 级每杀约 **0.006%**，1..30 级约 0.056%。高等级真正掉落来源是**怪物专属池 kind0**（每次尝试约 70%）。
- 我自己犯过、已纠正的三处：①把"29 条违规"当实测写进回复（其实没跑）；②手搓 Python 解析器误判"61806 不在 141 房表"；③阈值用步长 4 扫成 8（真值 10）。教训：**复验一律调引擎真身，不手搓表解析**。
- `);return p;}` 同行写法**不是**回归：HEAD 与两份改前备份里都是 2 处，属该文件既有风格，不要"顺手修排版"。
- 仓库内 `.bak` 残留是我复跑生成器造成的（生成器过去把备份写在稳定源旁边）。现 `gen_dungeon_content.py`/`gen_monster_pools.py` 都改到 `local-run/pvf/gen-backups/`。

## 3. 没做完的，以及确切原因（不含糊）

1. **雪山史诗链仍断两处**：
   - 4006/4012 的目标怪 `20607` 在 PVF 里只是 `.map` 的 `[ai character]` **APC 行**，而本构建既没有通用 APC 生成，`Batch-Service-FG.cs:156` 又把 APC/GSD 死亡分流进首领 APC 分支、**不进 `:160` 计杀链** → 击杀不计任务。同类共 8 条（607/609/1400/11400/1301/20607/21400 等）。→ 待办：通用 APC 表 + APC 击杀计入。
   - 4014 的奖励 `1041/1042` **在 PVF 里没有物品文件**（14 个此类 id 之一，挡 165 条任务）→ 登记救不了，属原版数据缺失。4008 依赖 4007+4014，所以 4008 仍永远交不了。
2. **815 不是怪**：`enemyType=3` 的行指向 `[special passive object]` 破坏物。这反过来证明 `enemyType` 是**类别判别**，而当前实现有意忽略该列（注释已写明证据边界）。
3. **高级配方书无处可得**：88 件 grade 62-67 的 `[recipe]` 被 `[special monster drop]` 剔出通用表，原版走 `CSpecialMonsterDrop`（`m_blocks[5][3][4][6][200]`）未实现，且这 88 件在 `MonsterPools` 0 命中。阻塞：`special/hell` 两份 `.etc` 在 PVF 里找不到（试过 20 个命名全 miss）→ 需原版服务端文件清单或动态调试。
4. **首领金币翻倍的运行期归因：未验出**。两次走查击杀数同为 305、金币条目 42→46（方向一致），但两次跑的 receipt 不同、掉落是概率掷骰，不能归因；且日志里 `FLOOR_DROP_PLANNED` 与 `KILL_EXP_CALCULATED` 无法可靠配对。要验得先给驱动加**固定 receipt 模式**。
5. **GSD/GBL 在 `DNF70_EVENT_MONSTERS=1` 下不抛 `Floor map roster`：未验出**（现有驱动只覆盖城镇 5，缺 Act1/2 驱动）。
6. 阿法利亚仍有 **45 条**任务被硬缺口挡住（`no-dungeon-profile` 84 处：副本 52/53/60-64/80-89/110-112/140 无迷宫表）；`alphlyra-flip-plan.json` 只包含能兑现的 66 条。
7. 存量 **70 条** `Supported=true` 的任务引用未登记物品：不会崩，`Offers()` 直接把它们从可接列表过滤掉（所以是"看不见"而非"点了报错"）。要不要回红是产品决定，我没动。
8. `Quest-Catalog-FG.cs:17` 把 `Quests.Count==3051` 钉死 → 任务域**永远只能原地改写**，任何"接入新任务"都必须先解决这条硬钉。

## 4. 提交前的复验步骤（按顺序，别跳）

```
# 1) 确认并发会话已停（看 mtime 是否还在推进）
ls -l --time-style=+%H:%M src/Tools/VmTestKit/*.cs | sort -k6 | tail
# 2) 一键闸门（3051 条 / 我的翻绿 / hunt 链 / 阿法利亚审计）
python D:/70S3A3/local-run/pvf/reverify_quest_flips.py
# 3) 静态 + 构建（构建前必须停线上，否则 CS0016）
python scripts/check_repo.py
python scripts/build.py --smoke
# 4) 隔离实例走查（run-walk.ps1 已含"每次重置存档"）
powershell -File D:/70S3A3/local-run/isolated-test/run-walk.ps1 -Tag final
# 5) 只暂存本波文件提交；REDNF-GAP 文档留本地不入库
```

本波文件清单（第 5 步只加这些）：`data/{floor-stack-FG126.dat,card-items-FG125.dat,item-metadata.xml,commerce-catalog.xml,quest-catalog.xml}`、`src/Tools/VmTestKit/{General-Stack-FG126.cs,Clear-Items-FG125.cs,Floor-Catalog-FG119.cs,Progression-FG.cs}`，以及第一波那 11 个 VmTestKit 文件与两份 docs。

## 5. 环境要点（照抄）

- 线上：频道 7002 + 入口 17001（当前跑的是 **17:21 的构建**，不含并发会话改动）。
- 隔离：`--batch-store` 是**单会话**，客户端断开即 `FINISHED`；每次走查前必须重置存档副本，否则疲劳度耗尽会假报 FAIL。
- 驱动/探针 exe **钉住数据哈希**：数据改了要重编探针（`csc` 在 `local-run/driver` 里用短横选项跑 `@rsp`）。
- 证据日志：`local-run/isolated-test/channel-<tag>-<hhmmss>.log`（名字不能复用，否则诊断降级）；关键字 `FLOOR_DROP_PLANNED`/`REJECTED`/`ONE_HIT_KILL_SWEEP`/`KILL_DUPLICATE`/`QUEST_REWARD_COMMITTED`/`FATIGUE_ENTRY_REJECTED`。
- 关键工件：`local-run/hunt-enemy-audit.txt`（可达性审计）、`local-run/alphlyra-flip-plan.json`（66 条 + 9 轮）、`local-run/pvf/set_quest_supported.py`（原地改写器，带三重不变量）、`local-run/pvf/reverify_quest_flips.py`（闸门）。
