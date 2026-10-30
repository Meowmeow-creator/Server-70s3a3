# FG358 — 默认只开王者，英雄级改为任务完成后解锁

日期：2026-10-27
范围：op5 副本难度列表、op16 入场准入、`[hero]` 任务完成回包
状态：代码、离线验收、`build/stable` 重编译完成；玩家实机验收待做

## 需求

- 服务端默认只解锁到王者级（难度 0..3，四个档）。
- 英雄级（难度 4）必须完成该副本对应的 `[herosmode required quest]` 后才出现、才允许进入。

## 落地

### 1. 默认档位

`Offline70HeroUnlockFG358`（`src/Tools/VmTestKit/Hero-Unlock-FG358.cs`）定义：

- `NormalMaximumDifficulty = 3`（普通/冒险/勇士/王者）。
- `HeroDifficulty = 4`。
- 副本 → 任务映射（PVF 全量扫描 263 个 `.dgn` 得到）：
  - 52 → 5823，53 → 5835
  - 83 → 5884，84 → 5886，85 → 5888
  - 86 → 5855，87 → 5880
  - 1504 → 无（`[herosmode enable]` 有、`[herosmode required quest]` 无，保持 3）

`Dungeon-Selection-Protocol.cs`：

- op5 的固定 body 和 `ProfileBody` 现在都写 `NormalMaximumDifficulty=3`。
- 新增 `ProfileForArea(town, area, state)`：先按 3 生成原 profile，再逐行检查角色已完成任务，只把对应英雄副本的行改成 4。
- `Reply(..., town, area, state)` 重载把角色进度带进 op5。
- `TestMaximumDifficulty=4` 保留为绝对包上限（probe/flow 仍可用），但不再是默认广告值。

`Batch-Service-FG.cs`：

- `DungeonSelection()` 和 op15 的 op5 回包都传 `progress`。
- `EntryDungeon` 在原有难度/区域校验后调用 `RequireEntry(id, difficulty, progress)`：
  - 0..3 始终放行；
  - 4 只放行“是英雄副本且对应任务已完成”；
  - 非英雄副本或 1504 传 4 直接拒绝。
- `BeginDungeon()`（返回城镇后的重进路径）同样补了 `RequireEntry`。

### 2. 任务完成后的即时解锁

`[hero]` 任务（5823/5835/5855/5880/5884/5886/5888）交任务时：

- `Batch-Service` 用 `DungeonForQuest(r.QuestId)` 得到副本 id；
- op36 回包改用 `Offline70RewardTypesFG183.HeroReply`（kind `0x15` + u16 副本 id）；
- 紧接着发 `HeroUnlockPacket`，即 op5 增量 `[u16 count=1][u16 dungeon][u8 4]`；
- 日志记 `HERO_UNLOCK_SENT quest=... dungeon=...`。

这样客户端不需要重开副本列表就能看到英雄级；重新登录/重新开 op15 时，`ProfileForArea(...,progress)` 也会按已完成任务返回 4。

### 3. 1504 幽灵列车

它有 `[herosmode enable] 1` 但没有 `[herosmode required quest]`。按“任务完成才解锁”的规则，它始终是 3；op5 不会给它 4，op16 传 4 也会被拒。这与原版 `CDungeonClear::enumDungeonClear` 的行为一致（`getHeroesRequiredQuest()==0` 不会把行升到 4）。

## 验收

```
python scripts/check_repo.py
  -> {"repositoryChecksPassed": true, "checkedFiles": 428, "stableSources": 180}
python scripts/build.py --smoke
  -> {"compiled": 180, "research": false, "smoke": true}

local-run/driver/HeroGateCheck-FG358.cs  (47 checks)
  -> PASS 47 checks
     - 默认 3；op5 fallback 两行都是 3
     - 7 个副本的 quest 映射；1504 无任务、始终 3
     - 未完成任务：4 被拒；完成任务：4 放行
     - Antberr/Northmyre/Lufthafen profile 只抬高对应副本一行
     - 5883→5884 走完真实任务引擎后，83 的 profile 变 4，84 仍 3
     - op36 kind 0x15 + u16 副本；op5 增量 [1][dungeon][4]

local-run/driver/HeroEnemyDrops-FG357.cs  -> PASS 17 checks
local-run/driver/QuestSystems-FG211.cs    -> PASS 109 checks
local-run/driver/QuestCategoryProbe.cs    -> PASS 64 checks
```

注：`PortNorthmyreCheck` 的 “existing gates unchanged” 和 `PortGentCheck` 的编译错误是这两个旧 driver 本身的既有问题（前者检查 `DungeonGate(2,7)` 仍为 false，后者有 CS0623 初始化器错误），与本次难度改动无关；核心回归以上面四个为准。

## 部署

- 构建时 `DNF70-Channel-FG.exe` / `DNF70-Entrance-FG.exe` 均未运行。
- `build/stable` 已重编译（180 源，smoke true），目录仍然使用 `[enemy reward item]` 修复后的 `quest-catalog.xml`。
- 启动方式不变；本次没有启动游戏、没有碰玩家存档。

## 仍存在的边界

- 服务端没有“角色已通关副本表”，门槛是“任务完成”，不是“该副本至少通关过一次再解锁英雄”。如果后续要严格复刻原版 `dungeon_start` 的 `clearedDiff!=0`，需要补通关表。
- 英雄专属 special 掉落（88 件配方书）和英雄怪物数值/冠军数量的简化档仍未做。
