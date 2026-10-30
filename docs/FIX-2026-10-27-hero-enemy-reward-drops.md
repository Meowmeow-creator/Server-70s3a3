# FG357 — 英雄级 83/84 解锁链材料接通（[enemy reward item]）

日期：2026-10-27
范围：任务 5883/5885 的材料掉落；顺带修掉同一批英雄难度上界漏改
状态：代码、目录、离线验收完成；`build/stable` 已重编译（服务当时未运行）；玩家实机验收待做

## 症状

- 83 夜间袭击战的英雄级解锁链：5883（1/2，收集 4382×20）→ 5884（2/2）。
- 84 补给线阻断战的英雄级解锁链：5885（1/2，收集 4396×10）→ 5886（2/2）。
- 5883/5885 在目录里是 Supported=true，但 `MonsterDrops` 为空；4382/4396 在怪池、通用掉落、世界掉落、独立掉落、商店、其他任务奖励里都没有来源，所以两条链永远交不了，83/84 的英雄级永远开不了。

## 根因

1. 5883/5885 用的是 `[enemy reward item]`，不是常见的 `[monster reward item]`：
   ```
   [enemy reward item]
       56001  1  83  3  4382  1  100  20
   ```
   它是 8 列（monster, enemyType, dungeon, difficulty, item, attempts, chance, max），比引擎的 7 列 `MonsterDrops` 多一列 enemyType。
2. `local-run/pvf/gen_snow_quests.py` 把 `[enemy reward item]` 列进 `ABSORBED`，整段丢弃，所以目录里这两条 `MonsterDrops` 是空的。
3. 引擎 `Quest-Drops-FG.cs` 的怪物/通关掉落行校验还把难度上界卡在 3；而 5822/5854/5879（52/86/87 的解锁链）的 `[monster reward item]` 表本来就带英雄行（难度 4），所以这些任务在王者级击杀时也会被 `Quest monster drop fields` 整包拒绝。
4. 通用堆叠掉落 `General-Stack-FG126.cs` 的 `diff>3` 是 FG211 七处上界放宽时漏掉的第八处；英雄级首杀会在 `FloorDrops.Plan → Append → Threshold` 抛 `Ordinary floor stack profile`，击杀不计数。

## 改动

### 生成器（`local-run/pvf/gen_snow_quests.py`）

- `derive()` 解析 `[enemy reward item]`，按 8 列切行并把 enemyType 列去掉，折叠成引擎的 7 列 `MonsterDrops`；行宽不是 8 的倍数时给 `enemy reward item width` 拒绝理由。
- 该标签仍留在 `ABSORBED`（避免被报成 unhandled extra），但注释明确它是被 derive 消费的。
- 全量核查过缓存里的 128 个带该标签的 `.qst`：行宽全部是 8 的倍数，难度分布 -1/0/1/2/3/4，无越界行。

### 引擎（`Server-70s3a3/src/Tools/VmTestKit/`）

- `Quest-Drops-FG.cs`：怪物掉落行难度上界 `>3 → >4`；通关掉落行 `>3 → >4`。这样 5822/5854/5879 带英雄行的表在王者级击杀时不再整包抛错，5883/5885 的难度 4 行也能装载。
- `General-Stack-FG126.cs`：`diff>3 → diff>4`。同文件 `Difficulty` 数组本来就有第 5 档（hero = 2f），英雄击杀的通用堆叠掉落现在可以正常计算。

### 目录（`data/quest-catalog.xml` + `build/stable/quest-catalog.xml`）

- 5883 的 `MonsterDrops` 从空变为 8 行（56001/56422/61458/61804 × 难度 3/4，4382，1 次，100%，上限 20）。
- 5885 的 `MonsterDrops` 从空变为 4 行（56005/61013 × 难度 3/4，4396，1 次，100%，上限 10）。
- 只改这两个记录的 `<MonsterDrops>` 元素，其余 3253 条记录逐字节不变；CRLF/BOM 保持。
- 备份：`data/quest-catalog.xml.pre-hero-enemy.bak`、`build/stable/quest-catalog.xml.pre-hero-enemy.bak`。
- 可复现脚本：`local-run/pvf/apply_enemy_reward_hero.py`（默认 dry-run，`--write` 落盘）。

## 验收

```
python scripts/check_repo.py
  -> {"repositoryChecksPassed": true, "checkedFiles": 428, "stableSources": 179}
python scripts/build.py --smoke
  -> {"compiled": 179, "research": false, "smoke": true}

local-run/driver/HeroEnemyDrops-FG357.cs  (17 checks)
  -> PASS 17 checks
     - 5883 8 行 / 4382；5885 4 行 / 4396
     - 83 的普通怪 56001、boss 61458 在王者级掉落 4382
     - 83/84 的难度 4 行也可 roll（不再抛）
     - 5822 带英雄行时，王者级 62012@52 仍掉 4387（回归）
     - General-Stack Threshold(4) 与 Append(4) 都不抛
     - 链条闭合：4382×20 交 5883 → 接/交 5884；4396×10 交 5885 → 接/交 5886

local-run/driver/QuestSystems-FG211.cs  (109 checks)
  -> PASS 109 checks
```

## 部署

- 构建时 `DNF70-Channel-FG.exe` / `DNF70-Entrance-FG.exe` 均未运行，`build/stable` 已由 `scripts/build.py --smoke` 重编译并复制目录。
- 启动方式不变（`local-run/one-click.bat` 会重新编译，不必手动搬 exe）。
- 本次没有启动游戏、没有碰玩家存档。

## 仍然没做的英雄级缺口（不在本批范围）

1. `[hero]` 奖励仍是 no-op：7 条解锁任务的 op36 kind `0x15` 和 op5 增量行不会发；客户端能否只凭任务历史点亮英雄级未实测。
2. 没有角色级通关表；op5 仍把所有已接入副本写成难度 4。
3. 英雄专属 special 掉落（88 件配方书）未接。
4. 英雄怪物数值/冠军数量仍是简化档（王者怪池 + 2.4 经验 / 1.8 金币；原版第 5 列 HP ×3.5 等未在服务端计算）。
