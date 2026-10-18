# rednf 差距全表（2026-10-16 更新，取代 10-15 的玩法/基建两份分册）

**基线**：我们 = Server-70s3a3（国服 70 级、C# 单进程本地服）；rednf = 台服 1031/80 级、C++ 14 服务、逐字节重建。
**状态**：✅已完成 / ⚠️部分 / ❌未做 / n-a 架构不同（单进程本地服不需要）。
**可修性**：【离线】代码/PVF/静态即可完成并可自动验收；【真人】需玩家上手验收手感/平衡；【原始数据】需要原版表或外部数据源；【动态】需 x32dbg 动态调试。
标 ★ 的是 2026-10-16 本轮更新过的行。

## A. 玩法系统

| # | 系统 | rednf 出处 | 我们的现状 | 状态 | 可修性 |
|---|---|---|---|---|---|
| 1 | NPC 动态心情（bad/normal/good + 定时 + 送礼） | `NPCDynamicInfo*`、`NPCScript.h` | 无 | ❌ | 【离线】 |
| 2 | 网格迷宫（随机宽度/高度 + 候选图） | `CDungeon.h:54-155`、`getRandMazebyPartyCount.inc` | 按 .dgn 确定性选图（体验等价，非原版随机） | ⚠️ | 【离线】 |
| 3 | 事件怪物（概率刷新/去重/落点） | `CDungeon.h:180-200`、`getEventMonsters.inc` | ★ 表+掷骰+wire 字段+每房上限已做；默认关（原版需活动激活） | ✅ | 【离线】 |
| 4 | 任务触发类型（triggerType/subType/AppearMap） | `Quest.h:318-320,152-167` | 只做了 39/40 一条链路与访问/完成 | ⚠️ | 【离线】 |
| 5 | 队伍传送状态机 | `CPartyTelePort.h` | 只有传送药水 | ❌ | 【离线】 |
| 6 | 怪物脚本系统（AI/独立掉落/死亡塔物品） | `STMonsterScript.h`、`CMonster.h` | 只有怪物列表与专属掉落池 | ⚠️ | 【离线+真人】 |
| 7 | 副本指定掉落（被动对象/陷阱） | `CDungeon.h:161-170`、`getObjectDropItems.inc` | 未接（40 的 trap 2211、四类特殊被动都无掉落） | ❌ | 【离线】 |
| 8 | 封印门/路径门（+ 地狱房） | `CDungeon.h:129-134,241-248` | ★ 数据/路由/出现率已做；无深渊模式时门保持关闭，地狱房与道具消耗未做 | ⚠️ | 【离线】 |
| 9 | 领主房进入条件（hunt/seeking） | `CDungeon.h:421-430`、`check_boss_map` | ★ 35/37 已实装（随机落点+计数+首领格闸门） | ✅ | 【离线】 |
| 10 | 城镇区域脚本（areaIndex/rect/areaType） | `TownAreaScript.h` | 4 座城镇 + 城镇 5（万年雪山）由 PVF 生成；未支持城镇无 | ⚠️ | 【离线】 |
| 11 | PVP（房/匹配/平衡/死斗/经验惩罚） | `PvP_Room.h`、`CFairMatch.h` | 无 | ❌ | 【离线+真人】 |
| 12 | 死亡之塔 | `CDeathTower*` | 研究目录 `research/tower/` 有，未晋升 | ⚠️ | 【离线+真人】 |
| 13 | 绝望之塔 | `TowerOfDespairMgr.h` | 无 | ❌ | 【离线】 |
| 14 | 领主之塔 | `CBossTower*` | 无 | ❌ | 【离线】 |
| 15 | 无尽祭坛（阶段/时间线/生存） | `advancealtar_*` | 无 | ❌ | 【离线】 |
| 16 | 副本翻牌奖励（免费/付费、随机物品） | `CClearReward/generateRandomItem.inc` | ★ 1..39 有；41..70 空卡位（= 交接文档 2.2） | ⚠️ | 【原始数据/离线】 |
| 17 | 物品生成器（概率/字典） | `CItemGenerator*` | 元数据有，`generateTable` 未接（与 16 同源） | ⚠️ | 【原始数据/离线】 |
| 18 | 技能学习/上限/连招 | `CSkillCanLearn.h`、`ComboSkill.h` | 部分（学习/上限） | ⚠️ | 【离线】 |
| 19 | 强化/增幅（概率/惩罚/材料/随机选项） | `CItemUpgrade.h`、`CItemAmplifier.h` | 部分；PVF 表齐（`etc/upgrade*.etc`），"凯莉强化错误19"待修 | ⚠️ | 【离线】 |
| 20 | 疲劳系统（出席/电池/赠送/上限） | `CFatigue*` | 部分 | ⚠️ | 【离线】 |
| 21 | 经验/金币事件（双倍/组队/会员） | `CExpDoubleEvent.h` 等 | 无 | ❌ | 【离线】 |
| 22 | 拍卖行 | `CAuction*` | 无（rednf 另有独立拍卖服 + PVF 解析 bot） | ❌ | 【离线】 |
| 23 | 个人商店/交易 | `CPrivateStoreMgr.h`、`CTradeSpace.h` | 无 | ❌ | 【离线】 |
| 24 | 售货机 | `VendingMachine.h` | 无 | ❌ | 【离线】 |
| 25 | 蓝色大理石棋盘 | `BlueMarble*.h` | 无 | ❌ | 【离线】 |
| 26 | 突袭/力量战争 | `CAssaultMgr.h`、`CPowerWarEvent.h` | 无 | ❌ | 【离线】 |
| 27 | 大赛系统 | `TournamentFinalC3/` | 无 | ❌ | 【离线】 |
| 28 | 血统地下城 | `CharacBloodDungeon/` | 无 | ❌ | 【离线】 |
| 29 | 哈迪斯 | `CHades*.h` | 无 | ❌ | 【离线】 |
| 30 | 村庄怪物/对象 | `CVillageMonsterMgr.h` | 无 | ❌ | 【离线】 |
| 31 | 神器 | `CArtifact.h` | 无 | ❌ | 【离线】 |
| 32 | 秘密商店 | `CSecretShop*.h` | 无 | ❌ | 【离线】 |
| 33 | 任务商店 | `CQuestShop.h` | 无 | ❌ | 【离线】 |
| 34 | 物品限制版本 | `CItemLimitEdition.h` | 无 | ❌ | 【离线】 |
| 35 | 物品锁 | `CItemLock.h` | 无 | ❌ | 【离线】 |
| 36 | 物品制作技能 | `CItemMakingSkill.h` | 无 | ❌ | 【离线】 |
| 37 | 扩展装备槽 | `CExpandEquipslot.h` | 无 | ❌ | 【离线】 |
| 38 | 合成时装 | `CCompoundAvatar.h` | 无 | ❌ | 【离线】 |
| 39 | 随机选项（脚本/赋予） | `CRandomOptionScript.h` | 无 | ❌ | 【离线】 |
| 40 | 掉落比例事件 | `CItemDropRatioEvent.h` | 无 | ❌ | 【离线】 |
| 41 | 副本统计（经验/金币/入场/首领日志） | `CDungeonExpStatistic.h` | 无 | ❌ | 【离线】 |
| 42 | 祭坛日志 | `CAdvanceAltarLog.h` | 无 | ❌ | 【离线】 |
| 43 | 可骑乘 | `CRidable.h` | 无 | ❌ | 【离线】 |
| 44 | 环境系统 | `CEnvironment.h` | 无 | ❌ | 【离线】 |
| 45 | 时间门 | `CTimeGate.h` | 无 | ❌ | 【离线】 |
| 46 | 任务电影 | `QuestMovieInfo.h` | 无 | ❌ | 【离线】 |
| 47 | NPC 对话/电影数据 | `NpcDialogue.h`、`NPCScript.h` | 无 | ❌ | 【离线】 |
| 48 | 任务 NPC 等级索引（7 个 multimap） | `getQuestNPCList.inc` | 无 | ❌ | 【离线】 |
| 49 | 邮件奖励任务 | `Quest.h:170-185` | 无 | ❌ | 【离线】 |
| 50 | 副本事件对话（11 职业文案） | `Quest.h:191-227` | 无（刹影守门人那句就是这类） | ❌ | 【离线】 |
| 51 | 每日日程 | `CDailyScheduleManager.h` | 无 | ❌ | 【离线】 |
| 52 | 任务职业检查（checkPossibleJob） | `Quest/checkPossibleJob.inc` | 部分（target character 全职业宽松） | ⚠️ | 【离线】 |
| 53 | 任务可重复检查 | `Quest/isRepeatableQuest.inc` | 无 | ❌ | 【离线】 |
| 54 | 任务成长类型检查 | `Quest/_isNeededCheckGrowType.inc` | 部分 | ⚠️ | 【离线】 |
| 55 | 自动显示任务（3 int 槽） | `Quest.h:256-262` | 无 | ❌ | 【离线】 |
| 56 | 职业进阶/分解机 | `CExpertJob.h` | 部分 | ⚠️ | 【离线】 |
| 57 | 副本清除追踪 | `CDungeonClearTracer.h` | 无 | ❌ | 【离线】 |
| 58 | 比武场/战争房 | `WarField.h`、`WarRoom.h` | 无 | ❌ | 【离线】 |
| 59 | 迷惘之塔（20 级） | `WongWorkDeathTower_A/` | 无 | ❌ | 【离线】 |
| 60 | 极限祭坛变体 | `advancealtar_ProcStage/` | 无 | ❌ | 【离线】 |

### A′. 本轮（10-16）新增的差距（不在原 60 项里）

| # | 缺口 | 证据 | 状态 | 可修性 |
|---|---|---|---|---|
| 61 | **等级通用掉落 41..70 级**（"只掉金币"的根因） | `data/floor-stack-FG126.dat` 480 表只有 pool1/rarity0 有数据、31..40 空、41..70 不存在；`General-Stack-FG126.cs` `level>40 return;` | ❌ | 【原始数据/离线】配方已查清（`local-run/recon/floor-drop-source.md`），验收闸门=复现 1..40 |
| 62 | 区域任务内容 | 万年雪山 ~70 个任务文件（epic 2+1、urgent 2×24、repeat/normal）；阿法利亚除 39/40 外全部 | ❌ | 【离线】 |
| 63 | NPC 商店**覆盖度**（机制本身是通的，见下） | 商店机制 + PVF 生成的商品目录都已就位（21 个 shop、shop→NPC 绑定、`ShopAvailable(town,area,shop)` 按当前区域 NPC 白名单放行），雪山 NPC 39..42 已在其中；缺的只是目录/白名单**未覆盖**的那些 NPC 与商店 | ⚠️ | 【离线】 |
| 64 | 服务端"代杀"与客户端权威的冲突开关 | `DNF70_TEST_ONE_HIT_KILL` 线上开着 → 客户端击杀账簿不推进（守门人台词/门禁条件不消） | ⚠️ | 【离线】需产品决策 |
| 65 | 验收驱动读帧时序（`SnowLiveCheck`） | 服务端日志证明门已通，驱动轮询未收敛 | ⚠️ | 【离线】 |

## B. 基建 / 架构

| # | 能力 | rednf 出处 | 我们 | 状态 | 可修性 |
|---|---|---|---|---|---|
| 1 | DB 存取层 + schema | `DBMW/DBManager.*`（100+ 查询） | JSON 持久化 + 收据/原子提交（`Economy-State-FG`） | ⚠️ | 【离线】 |
| 2 | 包分发框架（ID 表驱动） | `DNFPacketDecoder.cpp`、`Dispatchers.cpp` | 手写分发（`Batch-Service-FG`） | ⚠️ | 【离线】 |
| 3 | 信号/崩溃恢复 | `DNFSignalTranslator.cpp` | 无（证据日志降级 + 看门狗） | ⚠️ | 【离线】 |
| 4 | 定时器/事件调度 | `TimerDispatcher.*`（1411 行） | 只有少量 Stopwatch | ❌ | 【离线】 |
| 5 | 加密：RC6/Anubis/MD5/SHA/TEA | `CNCryptoAlgo.cpp`、`TEA.cpp` | 固定离线密钥的 area/gold/flow/select 密码（已够用） | ⚠️ | 【离线】 |
| 6 | 公会系统 | `Guild/`（2980+893+421+606+845 行） | 无 | ❌ | 【离线】 |
| 7 | 社区/好友 | `Community/` | 无 | ❌ | 【离线】 |
| 8 | 拍卖服 + bot + PVF 解析 | `ServerLab/Auction/`、`auction_bot/pvf.py` | 无（PVF 解析我们已有 pvfapi） | ❌ | 【离线】 |
| 9 | Relay/频道服务协议 | `Relay/`、`ChannelOld/` | 单 TCP 监听 | n-a | 参考用 |
| 10 | 日志系统 | `ServerCommon/DNFFileLog.*` | 证据日志（异步队列 + 降级） | ⚠️ | 【离线】 |
| 11 | 监控/统计/负载 | `Statics/`、`ServerLoadChecker.cpp` | 无 | ❌ | 【离线】 |
| 12 | XML 配置/热重载 | `DBMW/ServerXml.cpp` | 全硬编码 + env 开关 | ⚠️ | 【离线】 |
| 13 | 脚本引擎（flex/bison） | `SlangFlexLexerEngine.cpp`（16246 行） | 无（数据全由生成器转 C# 表） | ⚠️ | 【离线】 |
| 14 | 包名表/追踪 | `PacketNameTables.*` | 无（日志打 opcode 数字） | ❌ | 【离线】 |
| 15 | 版本校验 | `DNFVersion.cpp` | 无（接受任意连接） | ❌ | 【离线】 |
| 16 | 踢人/封禁 | `DNFKillUserConfig.cpp` | 无 | ❌ | 【离线】 |
| 17 | 用户会话管理 | `DNFUserManager.cpp` | 单会话 | ⚠️ | 【离线】 |
| 18 | Core dump | `Community/CoreDump.*` | 客户端有崩溃捕获（自研调试器） | ⚠️ | 【动态】 |
| 19 | UDP 通道 | `DNFUdpHandler.cpp` | 无（TCP-only） | n-a | — |
| 20 | AAS 账号认证 | `Game/AASHandler.cpp` | 简单登录探针 | n-a | — |
| 21 | STUN | `StunServer/` | 无 | n-a | — |
| 22 | Reactor 事件循环 | `Relay/Reactor.cpp` | 同步 TCP | n-a | — |
| 23 | Linux 服务管理 | `LinuxService.cpp` | 无 | n-a | — |
| 24 | secsvr 三件套（反作弊） | `secsvr/`（TenCrypt） | 无 | n-a | — |
| 25 | Manager 服 | `Manager/` | 无 | n-a | — |
| 26 | RDAR 脚本导入 | `DNFShared/GameScript/` | 无 | ❌ | 【原始数据】 |
| 27 | 异常处理框架 | `Exception.cpp` | `InvalidDataException` + 拒绝日志 | ⚠️ | 【离线】 |
| 28 | 进程管理 | `Community/ProcessManager.*` | 看门狗（`watch-services.ps1`） | ⚠️ | 【离线】 |
| 29 | 归档日志 | `Community/ArchiveLog.*` | 证据日志轮转 | ⚠️ | 【离线】 |
| 30 | 定时活动热重载 | `DBManager.h`（OnTimeEvent） | 无 | ❌ | 【离线】 |
| 31 | ★ 深渊（hell party）模式（封印门道具消耗/地狱房） | `dungeon_start`、`UseSealDoorItems`、`CheckDestroyConditionSealDoor` | 无 → 封印门保持关闭 | ❌ | 【离线】 |

## C. 本轮已完成、不再算差距（留档）

★ 事件怪物系统（表/wire/落点/上限/活动开关）、领主房进入条件、封印石+首领格闸门、封印门路由与出现率、
死包 `apcKillCount`、APC 死亡清关握手、刹影真身、掉落专属池（108/108 拟合 + 区域补齐 11 个缺失 id
+ 36 个空占位）、万年雪山 7 副本全表（40..45、141）、城镇 5（门/登录落点/城镇上限）、阿法利亚 area3
联合列表、任务 39/40、证据日志降级、入口 `--channel-port`、客户端崩溃捕获器、桌面快捷方式、
隔离栈（17003）与验收驱动体系。

这些的实现在 `docs/HANDOVER-2026-10-15-act4-port.md`（§1-§17）与
`docs/HANDOVER-2026-10-16-snow-region-and-drops.md` 有完整记录。

## D. 建议的下一步排序（按价值 / 可离线性）

1. **B：等级通用掉落 41..70**（A′61）——影响所有 >40 级副本的"材料掉落"体感；配方已就绪、验收闸门明确。
2. **翻牌卡 41..70**（A16/17）——与 B 同源（`generateTable`），做完结算奖励才完整。
3. **区域任务内容**（A′62 + A4/47/50/52/53/54）——按区域推进（阿法利亚剩余 → 万年雪山）；
   NPC 对话/事件文案是同一批工作。
4. **封印门地狱房 + 深渊模式**（A8、B31）——33/36 的秘密房与逐成员道具消耗。
5. **副本指定掉落（被动对象/陷阱）**（A7）——补齐"打桶掉材料"这类体感。
6. **NPC 动态心情**（A1）——解锁 144 个任务 NPC 的根。
7. **强化/增幅收尾**（A19，"凯莉强化错误19"）。
8. 网格迷宫随机化（A2）、怪物脚本 AI（A6）——手感类，需真人验收。

> 每项落地时沿用既定流程：PVF 取数据 → 生成器出表 → 静态断言 + 隔离线上验收 → 部署 → 记文档。
> 通用工作流与坑见 `docs/LESSONS-cn70-dungeon-porting.md`。
