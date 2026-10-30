# FIX 2026-10-30：APC 做领主时打死领主不清小怪（FG404）

状态：**已编译、已部署**。`build/stable/DNF70-Channel-FG.exe`（2026-10-30 15:02:48，203 源）
的 `BossClearTailFG181` 已含本次清扫（IL 级核对：`BossClearTailFG181` 里确实有对
`CleanupAfterBoss` 的调用，292 字节），15:03:11 起的服务加载的就是它。
玩家侧验收 = 下一局进 APC 领主房看小怪是否随领主一起倒下。

> 编号说明：先写成 FG402，发现同日已有别的改动占用 FG402（`Card-Drop-Probe-FG402.cs`，临时玩家探针）
> 与 FG403（op18 / APC 任务进度两份 FIX 文档），故本修复改用 **FG404**。

## 1. 现象

玩家反馈（2026-10-30）：

> 0.5酒馆 / 海岸八番街 / 绿都 / 霍利奥 史莫克将军做领主期间会出现打死领主结算时候小怪不清空现象
> （对任务没影响只是发现了反馈一下）。像莫呐哼 火箭侠 长三郎这些大领主不会出现通关小怪还在的情况。
> 猜测是APC做领主的时候会导致这种情况。

也就是说：结算面板正常弹出、任务计数正常，**只是领主房剩下的普通怪不倒**。

## 2. 根因：两条结算规则的差别，缺的是"清扫帧"

服务端有两种领主房结算规则：

| 领主类型 | `ReadyToClear` 判据 | 结果 |
|---|---|---|
| kind-3 普通首领行（莫纳亨 61328、火箭侠 61708 …） | `BossKilled()`：终点房**清空**（`RoomCleared`）且房里有 kind-3 行已死 | 玩家必须把小怪清光才能开终点，领主死时房间本来就空了，**永远不会留下小怪** |
| 角色 8 `[ai character]` 首领 APC（史莫克 10602、5603、610、408、5310、10409 …） | `BossApcGateFG361 → BossApcSlainFG361`：只看首领 APC 是否死 | 小怪可以还活着就结算 |

客户端只有收到某个小怪的 **op38 死亡包**才会把它清掉。kind-3 路径由 `KillMonster` 在
`ReadyToClear` 时调 `dungeon.CleanupAfterBoss()` 逐个补发；而 APC 首领的死亡走 op42 的 APC 分支，
结算交给 `BossClearTailFG181`（op127 首领状态 + op31 结算请求），**这条尾巴里没有清扫**：

```
void BossClearTailFG181(ushort uid){                       // 修复前
  if(dungeon.ReadyToClear){...MapClearFG130();Send("boss-state",...);}
  SettlePetRoomFG109();
  if(dungeon.ReadyToClear){...Send("clear-request",...);}
}
```

原生只有一条规则（rednf `Dispatcher_BossDieCheck` → `CParty::OnTimeoutBossDie` 把房间剩下的怪全部
kill_monsters），与首领是怪物行还是 APC 无关；所以这里属于移植缺口，不是客户端行为差异。

这也正好解释了玩家的对照：莫纳亨（11217 房间只有 kind-3 一只）、火箭侠（11123 房间）这类"大领主"
的房间在结算前必然已被清空，不会出现残留。

## 3. 修复

`src/Tools/VmTestKit/Batch-Service-FG.cs` `BossClearTailFG181` 在结算握手之前补上与 `KillMonster`
完全相同的一轮清扫：

```
void BossClearTailFG181(ushort uid){
  if(dungeon.ReadyToClear&&!dungeon.TowerFlowFG211){foreach(ushort leftover in dungeon.CleanupAfterBoss())
    Send("boss-minion-cleanup",BatchPacketFG(0,38,Death(leftover,...),keys,false));}
  if(dungeon.ReadyToClear){if(dungeon.RoomCleared)MapClearFG130();Send("boss-state",...);}
  ...
}
```

顺序与 kind-3 路径一致：领主死亡包（APC 分支已发）→ 每个小怪一条 op38 → `MapClearFG130`（房间已清空
才记任务清图）→ op127 → 宠物结算 → op31。

`CleanupAfterBoss` 自带的三条性质直接复用，不需要新逻辑：

* 跳过 FG376 友方 actor（`noncombatants-FG376.dat`，当前只有 61114）与 FG379 保护目标（冰之心 1034）；
* 按 `dead[RoomIndex]` 记账，天然幂等：重复的 APC 死亡帧不会二次清扫；
* 只覆盖名册内的 100..100+CurrentCount，友方 APC / GSD / 载具不在其中。

塔（`TowerFlowFG211`）保持原样：塔层结算走 `TowerKillTailFG348`，不进这条尾巴。

## 4. 影响面（离线普查：本构建所有角色 8 首领房）

| 副本 / 任务 | 首领房 | 首领 APC | 房内小怪 | 修复前 | 修复后 |
|---|---|---|---|---|---|
| d60 3904 热血八番街 | 11116 | 10602 史莫克将军 | 6 | 残留 | 随领主倒下 |
| d61 3908 绿都 | 11210 | 5603 德鲁伊·梅亚 | 4 | 残留 | 同上 |
| d61 3909 绿都 | 11214 | 610 | 10 | 残留 | 同上 |
| d140 4103 月光酒馆 | 16031 | 20301 酒馆捣乱者 | 8 | 残留 | 同上 |
| d140 4104 月光酒馆 | 16032 | 408 霍利奥 | 13 | 残留 | 同上 |
| d140 4106 月光酒馆 | 16009 | 5310 长三郎 | 6 | 残留 | 同上 |
| d141 4006 修炼场 | 16024 | 20607 | 3 | 残留 | 同上 |
| d89 根特防御战 | 17045 | 10409 Babylon | 0（房间无名册行） | — | 无变化 |
| Act4 刹影房（d31–37 / 1502） | 各首领房 | 刹影 | 有 | 残留 | 同上（原生同样清扫） |

关于玩家提到的 **长三郎**：数据上它也是角色 8 首领 APC（d140 4106 的 16009 房，6 只小怪），
按修复前逻辑同样会残留；如果实机看到它是干净的，多半是那局已经把小怪清掉了。这次一并覆盖。

`CreditRoomDestroyObjectsFG375`（破坏物件类任务）没有加进 APC 尾巴：普查 d60/61/140/141/89 的
`[hunt enemy]` enemyType=3 行与这些房间的 `[passive object]` 表**零交集**，加了也不会产生任何计数，
保持最小改动。

## 5. 离线验收

`local-run/driver/BossMinionSweep-FG404.cs`（全源 + driver 编译，220 项全过）：

* 逐个走到上表 8 个 APC 首领房：断言 `BossApcGateFG361` 成立、首领死前不结算、首领 APC 死后
  **立即** `ReadyToClear=true` 而 `RoomCleared=false`（= 玩家报的状态），`CleanupAfterBoss()` 返回
  的正是房间里活着的普通怪、且全部是非友方非保护目标，清扫后 `RoomCleared=true`，再调一次返回 0；
* kind-3 回归（d60 3906 / 11123 / 火箭侠 61708）：首领单死**不**结算（终点房未清空），清空后才结算，
  清扫返回 0 —— 证明"大领主不会残留"本来就是这条规则，未被本次改动影响；
* 接线：`BossClearTailFG181` 含清扫、`KillMonster` 保留自己的清扫、op42 APC 分支仍把角色 8 首领
  死亡路由到该尾巴、编译产物里 `BossClearTailFG181` 确实调用了 `CleanupAfterBoss`（IL 级核对）。

复现命令（Git Bash；rsp 必须二进制写，`write_text` 会把 `\r\n` 变 `\r\r\n` 让 csc 只读到文件名）：

```bash
python - <<'PY'   # 生成全源 + driver 的 rsp
from pathlib import Path
src=sorted(Path('D:/70S3A3/Server-70s3a3/src/Tools/VmTestKit').glob('*.cs'))
out=Path('D:/70S3A3/local-run/tmp/fg404-scratch');out.mkdir(parents=True,exist_ok=True)
q=lambda s:'"'+s+'"'
lines=['-nologo','-target:exe','-main:BossMinionSweepFG404',
 f'-out:{q(str(out/"BossMinionSweep-FG404.exe"))}']+[q(str(p)) for p in src]+['BossMinionSweep-FG404.cs']
(out/'driver.rsp').write_bytes(('\r\n'.join(lines)+'\r\n').encode('utf-8'))
PY
cd D:/70S3A3/local-run/driver   # driver 用相对名，避免 rsp 里绝对路径被 csc 截断
/c/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe @D:/70S3A3/local-run/tmp/fg404-scratch/driver.rsp
D:/70S3A3/local-run/tmp/fg404-scratch/BossMinionSweep-FG404.exe D:/70S3A3/Server-70s3a3
```

`python scripts/check_repo.py` → `repositoryChecksPassed: true`（203 稳定源）；
`csc` 全源编译 0 error（仅既有 CS0618/CS0169/CS0162 警告）。
最后跑一遍时源码里还带着同日并行改动的 `Card-Drop-Probe-FG402.cs` 等，driver 仍 220/220。

## 6. 残留

* 真机验收未做（离线只能证明引擎与接线）：需要下一局进 11116 / 11210 / 11214 / 16031 / 16032 /
  16009 / 16024 任一 APC 领主房，先留几只小怪再杀领主，看小怪是否一起倒下、门是否正常开。
* 没有改客户端、掉落、经验、任务计数、房间名册；旧存档无需迁移。
