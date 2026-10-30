# FG348：死亡之塔/迷惘之塔改走原生塔协议（2026-10-26）

## 玩家现场

- 女格斗转职链（7815/7819/7827 等 `[condition under clear]` 目标 11001）进迷惘之塔后**卡在第 1 层**，无法向上。
- 进死亡之塔（11000）/绝望之塔（11008）时画面显示的是**迷惘之塔**。

## 根因

FG211 把 11000/11001 当成普通副本接进门区，op16 回的是普通副本四连包（16/3/28/29），楼层推进靠“1×N 竖迷宫 + op48 相邻格移动”。CN70 客户端按 PVF `[dungeon type]` 判定：

| dgn | `[dungeon type]` | 客户端分支 |
|---|---|---|
| dungeon/towers/towerofdeath.dgn（11000） | `tower of death` | kind 1，CDeathTower |
| dungeon/towers/towerofillusion.dgn（11001） | `tower of death` | kind 1，CDeathTower |
| dungeon/towers/ztowerofdespair001.dgn（11008） | `tower of despair` | kind 3，CBossTower（本 build 不开放） |

kind 1 的塔**不使用普通房间流**。原生服务端（`reference/rednf/.../CDeathTower/handleStartGame.inc`）对 op16 只回 `op142`（塔身份/总层数）和 `op143`（第 1 层楼层包），之后每层都由 `op143` 驱动，客户端用 `op162` 报阶段开始/结束、用 `op48` 请求下一层。因为 FG211 从没发过 op142，客户端拿不到 `dungeonIdx`，所有塔都落回默认塔（迷惘之塔）；因为没有 op143 续层，清完第 1 层后 op48 也不会被正确应答。

另外 `Elite-Monsters-FG118.Quota` 把迷宫宽高限制在 1..16；死亡之塔是 1×45，进塔取房间怪池时直接抛 `Champion quota input`（连接被拒）。塔内 PVF 没有 `[champion]` 行，本就不该走这个配额。

## 本批改动

### 新增 `src/Tools/VmTestKit/Tower-Run-FG348.cs`

- `Offline70TowerRunFG348`：塔运行态（11000=45 层 / 11001=15 层，阶段/加载/结算阶段）。
- `InfoBody`（op142）：`int dungeonIdx, short endStage, byte bloodState=0, byte randomBuffType=0x0b`。
- `StageBody`（op143）：`short stage(1-based), int seed, short mapIndex, byte count`，每条形如
  `int mobId(本层序号), short instId(uid=100+i), int mobIndex(怪物id), byte level, byte roleType, byte f13, byte f14`；
  末尾 `byte itemCount=0`。镜像 `CDeathTower::CStage::makeStagePacket`。
  **顶层 [ai character] APC（迷惘 15F 的 608、死亡 45F 的 10203）也在这里登记**，否则 `RoomCleared` 会忽略 APC，15F 不杀 608 也能通关。
- `RankingBody`（op144）：空排行榜记录（best 段 byte0 + 5 条空记录），长度 184。
- `RewardBody`（op145）：`int exp` + 4 组空物品栏。
- 服务钩子：`TowerBegin/TowerLoad/TowerStageCommand/TowerMove/TowerKillTail/MaintainTower/TowerSettle/TowerEplp/TowerReturn`。

### 修改

- `Batch-Service-FG.cs`
  - op16：11000/11001 走 `TowerBeginFG348`（仍扣 4183 邀请函、不耗疲劳），不再发 16/3/28/29。
  - op40：塔内回原生 ack（首层 type1 op40 `{1}` + type0 op30 `int 0`；后续层只发 op30）。
  - op48：塔内交给 `TowerMoveFG348`（校验已清层 + 客户端已报 finish，层号 +1 后发下一层 op143）。
  - op162：新增 `Dispatcher_DeathTowerStageCommand`（byte 1=开始，2=结束）。
  - op26：塔结算后的 EPLP 选择，回 `type1 op75 {1,cmd,sub}` 并回城。
  - 击杀：塔清层不再触发普通副本的 op127 boss-state / op31 clear-request；最后一只怪或 APC 死亡时武装结算（原生 5 秒定时链的简化：2s 排行→1s 奖励展示→5s 入账→op146）。
  - APC 击杀后补发塔内任务进度。
  - `Town()`/op7 清空塔运行态。
- `Elite-Monsters-FG118.cs`：塔内跳过 `[champion]` 配额（1×45 超出原生 16×16 迷宫界）。
- `EntryDungeon`：塔请求不再要求 `plain[3..7]` 全零（原生塔请求这些字节带 dungeon type 标志），并把塔的 difficulty 固定为 0（塔无难度分级）。普通副本校验不变。

### 结算语义

- 通关层数 = 塔总层数（15/45）；`quests.Clear(dungeon 11001/11000, difficulty, …, visited=total=EndFloor)` 完成 7815/7819/7827/7840/7847/7857/7860/7879/7887 等 `[condition under clear]`。
- 任务 64/65（`[hunt enemy]` 11001 杀 608 ×2）由顶层 APC 击杀进 `KillWithAssetsFG171`，并回发 kill-quest-progress。
- 奖励 exp 用现有 `ClearExperience`（11001 basis 30 / 11000 basis 60），显示值与入账值一致；本批**不发塔牌**（op145 四组空），排行榜为空记录。
- 11008 绝望之塔仍不开放：op16 直接拒绝，不会再串到迷惘之塔。

## 离线验收

```
python scripts/check_repo.py                 # 428 文件 / 172 稳定源，通过
csc 编译 DNF70-Channel-FG / DNF70-Entrance-FG（暂存目录 local-run/build-staging-fg348）
local-run/build-staging-fg348/TowerFloors-FG348.exe
  -> TOWER_FG348 checks=56 failures=0
```

断言覆盖：11000/11001 身份与层数、op142 字段、op143 楼层/地图/怪物行与 APC 行、15F APC 608 的 uid=109/等级/roleType 与“APC 未死不通关”、死亡 45F 的 APC 10203、op144/op145 包长与字段。无 socket / store / 玩家存档。

## 上线

服务端进程正占用 `build/stable/*.exe` 时不能直接重编。关掉客户端后执行一次：

```
D:\70S3A3\local-run\one-click.bat
```

（= 停旧服 → 在 `Server-70s3a3` 重编 → WMI 分离拉起双服 → 端口自检。分发包 `dist/` 里的 exe 是 13:56 的旧包，本修复只在源码/重编后生效。）

## 实机验收步骤

1. 20 级女格斗接 7815/7819 转职线，进亡者峡谷 → 迷惘之塔。
2. 确认进的是**迷惘之塔**（不是死亡之塔），第 1 层清完后能出“下一层”并进入第 2 层；连续推进到 15F。
3. 15F 杀完 9 只怪 + APC 608，出现排行/奖励/EPLP 菜单；选返回后回亡者峡谷；任务 64/65 计数 +1、7819 的“通关迷惘之塔”变可交。
4. 用 ≥50 级角色点死亡之塔，确认 op142 带的是 11000、层数 45，且不再显示迷惘之塔。
5. 点绝望之塔应保持不可进（无“跳到迷惘之塔”）。

若仍卡层，证据在 `local-run/logs/channel-evidence.log`：`FRAME opcode=…` 与 `SENT tower-…`，以及 `channel-evidence.log.request-*-<op>-*.bin` 原始请求体。重点看是否出现 `opcode=162` / `opcode=48`，以及服务端是否打出 `TOWER_STAGE_FINISH` / `TOWER_FLOOR_ADVANCED`。

## 已知简化（不算完成）

- 塔内 AI/随机 AP 怪、特殊被动物品、每层位次（map_monster m_field13/14）未还原；空怪层按空层直接过。
- 塔牌/金币结算、排行榜、记录续存未做（op145 0 张、op144 空记录）。
- 11008 绝望之塔（CBossTower）未接；11006/11007 祭坛仍是普通副本近似。
- 组队、塔内复活限次、塔内掉落拾取细节沿用普通副本路径。
