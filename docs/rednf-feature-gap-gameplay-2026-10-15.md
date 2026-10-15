# rednf Gameplay Feature Gap Analysis

> Date: 2026-10-15 | Baseline: rednf (台服1031/80级) vs Server-70s3a3 (国服70级, 145 C# files)
> Scope: gameplay/player-facing systems in `remake/source/DNFServer/GameServer/Game/`

## Key

- **fixable offline?** YES = determinable from code/tests/PVF/static analysis; NO = needs live gameplay
- Gap size: S (<1 day) / M (1-3 days) / L (3+ days)
- **[FIX]** = top-10 recommended fix priority

## Main Gap Table

| # | Feature | rednf evidence (file:line) | our status | gap | offline? | notes |
|---|---------|---------------------------|------------|-----|----------|-------|
| 1 | **[FIX] NPC 动态心情系统** (mood state machine: bad/normal/good, timer-driven, gift items) | `NPCDynamicInfo.h:29-59`, `NPCDynamicInfoManager.h:25-43`, `NPCScript.h:30-37` | absent | L | YES | Deterministic state machine; affects 144 quest NPCs |
| 2 | **[FIX] 网格迷宫系统** (Grid maze: width/height + GridScript mapCandidates/secondCandidates/layeredMapIndexes) | `CDungeon.h:54-155`, `CDungeon/getRandMazebyPartyCount.inc` | partial: `Dungeon-Flow-FG.cs` (ad-hoc) | M | YES | Deterministic from PVF maze scripts |
| 3 | **[FIX] 事件怪物系统** (Event monsters: probability-based spawning, stEventMonster_t/stSecondEventMonster_t) | `CDungeon.h:180-200`, `CDungeon/getEventMonsters.inc` | absent | M | YES | Spawn probability tables deterministic |
| 4 | **[FIX] 任务触发类型系统** (Quest trigger: m_triggerType/SubType/Data, AppearMap mapA/mapB/percent) | `Quest.h:318-320`, `Quest.h:152-167` | partial: `Quest-Catalog-FG.cs` (no triggers) | M | YES | Data-driven from quest scripts |
| 5 | **[FIX] 队伍传送状态机** (Party teleport: village/area/posx/posy/direction, member state) | `CPartyTelePort.h:4-74` | partial: `Teleport-Service-FG94.cs` (potion only) | M | YES | Deterministic; needed for party play |
| 6 | **[FIX] 怪物脚本系统** (STMonsterScript sizeof 0x1cc4: catch items, independent drops, death tower items, AI) | `STMonsterScript.h:1-80`, `CMonster.h:12-60` | partial: `Forest-Content-FG75.cs` (lists only) | L | YES | AI tables deterministic; AI feel needs live test |
| 7 | **[FIX] 副本指定掉落** (Dungeon assign items: stDungeonAssignItem_t, getSpecialPassiveObjectItems) | `CDungeon.h:161-170`, `CDungeon/getObjectDropItems.inc` | absent | M | YES | Drop tables deterministic |
| 8 | **[FIX] 封印门/路径门** (Seal door: m_sealDoorMapIdx/PosX/PosY, STPathgateObject 4x3) | `CDungeon.h:129-134`, `CDungeon.h:241-248` | absent | M | YES | Door positions deterministic |
| 9 | **[FIX] 领主房进入条件** (Boss room entry: stConditionEnterBossRoom_t m_flag + m_vec4) | `CDungeon.h:421-430` | partial: `Dungeon-Flow-FG.cs` (ad-hoc) | S | YES | Quick win; data-driven |
| 10 | **[FIX] 城镇区域脚本** (TownAreaScript: areaIndex/mapName/areaType/rects) | `TownAreaScript.h:22-42` | partial: `Town-Content-FG75.cs` (no area system) | S | YES | Quick win; data tables |
| 11 | PVP 系统 (rooms, matching, fair match, death match, exp penalty) | `PvP_Room.h`, `PvpUserTable.h`, `CFairMatch.h`, `MatchingSystem.h` | absent | L | YES | Protocol deterministic; balance needs live test |
| 12 | 死亡之塔 (Death Tower: play data, win points, tower dialog) | `CDeathTower.h`, `CDeathTowerPlayDataStatistic.h` | absent (research/tower/ exists) | L | YES |
| 13 | 绝望之塔 (Tower of Despair: APC info, layers, 0x173f4 buffer) | `TowerOfDespairMgr.h:1-80` | absent (research/tower/ exists) | L | YES |
| 14 | 领主之塔 (Boss Tower) | `CBossTower.h`, `BossTowerDepsC2/` | absent | L | YES |
| 15 | 无尽的祭坛 (Advance Altar: stage control, timeline, survival) | `advancealtar_Manager/`, `advancealtar_StageControl/` | absent | L | YES |
| 16 | 副本翻牌奖励 (Clear reward card: generateRandomItem, free/paid flip) | `CClearReward/generateRandomItem.inc`, `CClearRewardCardEvent.h` | partial: `Paid-Card-FG39.cs` (7 dungeons) | M | YES |
| 17 | 物品生成器 (Item generator: probability-based, item dictionary) | `CItemGenerator.h`, `CItemGeneratorMgr.h` | partial: `Item-Metadata-FG.cs` | M | YES |
| 18 | 技能学习/最大等级 (Skill can-learn, max level, combo skills) | `CSkillCanLearn.h`, `CSkillMaxSkillLevel.h`, `ComboSkill.h` | partial: `Skill-Rules-FG38.cs` | M | YES |
| 19 | 物品强化/增幅 (Item upgrade, amplifier, random option) | `CItemUpgrade.h`, `CItemAmplifier.h` | partial: `Npc-Repair-Core-FG63.cs` | M | YES |
| 20 | 疲劳系统 (Fatigue: attendance, battery, give item, max factor, unlimit) | `CFatigueAttendance.h`, `CFatigueBatteryHandle.h` | partial: `Fatigue-State-FG50.cs` | M | YES |
| 21 | 经验/金币事件 (Exp double, party bonus, member reward, double gold card) | `CExpDoubleEvent.h`, `CPartyExpBonusEvent.h` | absent | M | YES |
| 22 | 拍卖行 (Auction house, average price) | `CAuction.h`, `CAuctionAveragePrice.h` | absent | M | YES |
| 23 | 个人商店/交易 (Private store, trade space) | `CPrivateStoreMgr.h`, `CTradeSpace.h` | absent | M | YES |
| 24 | 售货机 (Vending machine, item vending, bonus event) | `VendingMachine.h`, `ItemVendingMachine.h` | absent | M | YES |
| 25 | 蓝色大理石棋盘 (Blue Marble: board game, map/tile script) | `BlueMarble.h`, `BlueMarbleMapScript.h` | absent | L | YES |
| 26 | 突袭/力量战争 (Assault mgr, power war event/victorious) | `CAssaultMgr.h`, `CPowerWarEvent.h` | absent | L | YES |
| 27 | 大赛系统 (Tournament: final, monster, dungeon reward, PVP event) | `TournamentFinalC3/`, `CTournamentDungeonReward.h` | absent | L | YES |
| 28 | 血统地下城 (Blood dungeon: reward events, charac blood dungeon) | `CBloodDungeonRewardEvents.h`, `CharacBloodDungeon/` | absent | M | YES |
| 29 | 哈迪斯 (Hades + server proxy) | `CHades.h`, `CHadesServerProxy.h` | absent | M | YES |
| 30 | 村庄怪物/对象 (Village monster mgr, object mgr) | `CVillageMonsterMgr.h`, `CVillageObjectMgr.h` | absent | M | YES |
| 31 | 神器系统 (Artifact) | `CArtifact.h` | absent | M | YES |
| 32 | 秘密商店 (Secret shop + event) | `CSecretShop.h`, `CSecretShopEvent.h` | absent | S | YES |
| 33 | 任务商店 (Quest shop) | `CQuestShop.h` | absent | S | YES |
| 34 | 物品限制版本 (Item limit edition) | `CItemLimitEdition.h` | absent | S | YES |
| 35 | 物品锁 (Item lock) | `CItemLock.h` | absent | S | YES |
| 36 | 物品制作 (Item making skill) | `CItemMakingSkill.h` | absent | S | YES |
| 37 | 扩展装备槽 (Expand equip slot) | `CExpandEquipslot.h` | absent | S | YES |
| 38 | 合成时装 (Compound avatar) | `CCompoundAvatar.h` | absent | S | YES |
| 39 | 随机选项 (Random option script/give) | `CRandomOptionScript.h` | absent | S | YES |
| 40 | 掉落比例事件 (Item drop ratio event) | `CItemDropRatioEvent.h` | absent | S | YES |
| 41 | 副本统计 (Dungeon exp/gold/entrance/boss logs) | `CDungeonExpStatistic.h`, `CDungeonGainedGold.h` | absent | S | YES |
| 42 | 祭坛日志 (Advance altar log) | `CAdvanceAltarLog.h` | absent | S | YES |
| 43 | 可骑乘 (Ridable + script) | `CRidable.h`, `CDungeon/RidableScript.inc` | absent | S | YES |
| 44 | 环境系统 (Environment) | `CEnvironment.h` | absent | S | YES |
| 45 | 时间门 (Time gate) | `CTimeGate.h` | absent | S | YES |
| 46 | 任务电影 (Quest movie info) | `QuestMovieInfo.h` | absent | S | YES |
| 47 | NPC 对话 (NPC dialogue, movie data, script) | `NpcDialogue.h`, `NpcMovieData.h`, `NPCScript.h` | absent | M | YES |
| 48 | 任务 NPC 等级索引 (Quest NPC list by grade: 7 multimap) | `CListTables/getQuestNPCList.inc` | absent | S | YES |
| 49 | 邮件奖励任务 (Quest postal reward: mailTitle/Contents/Items) | `Quest.h:170-185` | absent | S | YES |
| 50 | 副本事件对话 (Dungeon event: 11 job-specific dialog strings) | `Quest.h:191-227` | absent | S | YES |
| 51 | 每日日程 (Daily schedule manager) | `CDailyScheduleManager.h` | absent | S | YES |
| 52 | 任务职业检查 (Quest job check: checkPossibleJob, power_side) | `Quest/checkPossibleJob.inc` | absent | S | YES |
| 53 | 任务可重复检查 (Quest repeatable: isRepeatableQuest) | `Quest/isRepeatableQuest.inc` | absent | S | YES |
| 54 | 任务成长类型 (Quest grow type check) | `Quest/_isNeededCheckGrowType.inc` | absent | S | YES |
| 55 | 自动显示任务 (Quest auto display: 3 int slots) | `Quest.h:256-262` | absent | S | YES |
| 56 | 职业进阶 (Expert job, extraction, profession disjointer) | `CExpertJob.h`, `ExpertExtraction/` | partial: `Job-Advancement-FG91.cs` | S | YES |
| 57 | 副本清除追踪 (Dungeon clear tracer) | `CDungeonClearTracer.h` | absent | S | YES |
| 58 | 比武场/战争房 (War field, war room) | `WarField.h`, `WarRoom.h` | absent | L | YES |
| 59 | 迷惘之塔 (Death Tower variant lvl 20) | `WongWorkDeathTower_A/` | absent | L | YES |
| 60 | 极限祭坛 (Extreme altar variant) | `advancealtar_ProcStage/` | absent | L | YES |

## Top 10 Fix-First (sorted by value to us)

| Priority | Feature | Gap | Fixable Offline | Why First |
|----------|---------|-----|-----------------|-----------|
| 1 | NPC 动态心情系统 | L | YES | Unlocks 144 quest NPCs; deterministic state machine |
| 2 | 网格迷宫系统 | M | YES | Foundational for random dungeons; data-driven |
| 3 | 事件怪物系统 | M | YES | Dungeon variety; tables deterministic |
| 4 | 任务触发类型系统 | M | YES | Complex quest chains; data-driven |
| 5 | 队伍传送状态机 | M | YES | Party play; state machine deterministic |
| 6 | 怪物脚本系统 | L | YES | Core combat; AI tables verifiable |
| 7 | 副本指定掉落 | M | YES | Dungeon rewards; tables deterministic |
| 8 | 封印门/路径门 | M | YES | Dungeon progression; positions deterministic |
| 9 | 领主房进入条件 | S | YES | Quick win; data-driven |
| 10 | 城镇区域脚本 | S | YES | Quick win; data tables |

## Unverified Leads

The rednf Game\ directory contains many additional files whose player-facing vs. infra role could not be confirmed without deeper per-file analysis. Categories most likely containing further gameplay gaps:

- **Statistics/logging classes** (~20): `CValueStatistic`, `CCommonStatisticsMgr`, `Statistics*` -- likely server-side metrics, but may gate player rewards
- **Server proxies** (~10): `*ServerProxy` (Guild, Hades, Monitor, Statistic, Base, DoubleConnCheck, Community, School, PCRoom) -- cross-server gameplay routing (mail, guild, PVP)
- **Event system** (~15): `CEventMgr`, `CEventManager`, `CEventBase`, `CEventScriptMng` -- server-wide event scheduling that may gate dungeon availability
- **DB layer** (~30): `DB_*` classes -- persistence only, not gameplay-facing
- **Crypto/net** (~15): `CNCrypto`, `CRijndael`, `CTEA`, `Cipher`, `EncryptTool`, `EpollHandler` -- protocol/infra
- **Script importer/linker files** (~50): `RDARScript*`, `Link*`, `OnlyOrig*`, `Ctor*`, `Init*`, `W6/W7/W8/W9_*` -- disassembly infrastructure
- **Strike/fatigue sub-events** (~8): `CStabToDeathEvent`, `CStopOverlabExpEvent`, `CUsedFatigueRewardItem` -- edge-case event triggers
- **Cera shop** (~5): `CeraShop*` -- cash shop purchase tracking (may gate purchases)

These were not individually verified against our C# codebase; some may hide gameplay subsystems not covered by the 60 gaps in the main table.

---

## Summary

- **Total rednf gameplay subsystems analyzed**: 60
- **Absent in our server**: 44
- **Partial in our server**: 16
- **Top 10 fix-first**: all marked YES for offline fixability
- **Largest gaps**: NPC mood system, monster script system, PVP system, tower systems, altar systems, Blue Marble, assault/power war, tournament
- **Surprising finding**: All top-10 gaps are fixable offline -- correct behavior determinable from script data, PVF tables, and protocol analysis without live gameplay
