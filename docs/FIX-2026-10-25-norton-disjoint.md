# 2026-10-25 诺顿分解机（command 28）不可用

## 现场

- 角色：role 8，`12345`，job 8（男魔法师），Lv.70；所在城镇 town=2 area=0（`AREA_CHANGED town=2 area=0`，23:16:42）。
- 客户端在诺顿（npc 13）处点分解后，`local-run/logs/channel-evidence.log` 只有：
  `23:20:31 FRAME opcode=28 bytes=21 seq=96` → `23:20:31 UNIMPLEMENTED opcode=28 bodyBytes=8`，
  没有任何回包，客户端界面无变化。
- 同一份日志里还有 `23:18:30 UNIMPLEMENTED opcode=29`、`23:28:32 UNIMPLEMENTED opcode=105`，说明不是单独一条命令的问题。
- 抓包体（仍在加密态）：`logs/channel-evidence.log.request-59-28-1a38ca9b15424d859e5117ae7d710efd.bin`
  = `0E 05 06 F8 FB 05 06 07`，用服务端同一套 `BatchCryptFG`（索引 28%14=0、0..333 斜坡）解出
  `slot=10 list=0 tool=-1`，即装备栏第 10 格的一件普通装备。

## 根因

`src/Tools/VmTestKit/Batch-Service-FG.cs` 的 `Handle` 里，2026-10-25 的多角色改造把死亡拦截名单里的
`||op==206` 去掉时，把解释写成行内尾注释并且**没有换行**：

```
if(playerDeadFG117&&(op==42||op==46||op==48||op==49))throw new InvalidDataException("...");   // 206 stays allowed: ... resume notifyif(op==84){...}if(op==105){...}if(op==253){...}if(op==29){...}if(op==28){DisjointFG92(body,sequence);return;}
```

`//` 注释到行尾，于是同一行后面的五个分支全部被注释掉：

- 28 分解（`DisjointFG92`）、29 药水/丹（`PotFG93`）、84 品质（`QualityFG101`）、
  105 宠物孵化（`HatchPetFG96`）、253 传送（`TeleportFG94`）。

所以客户端发来的分解请求落到 `Note("UNIMPLEMENTED opcode="+op)`，没有回包。23:14 部署的稳定二进制
（进程 29944，起于 23:14:54）就是这份源码编译的，所以线上表现为“分解机点了没反应”。

原基线（HEAD）该行是 `(op==42||op==46||op==48||op==49||op==206)`，五个分支都在；本次只坏了注释写法，
没有别的逻辑变化。

## 代码修改

`src/Tools/VmTestKit/Batch-Service-FG.cs`：把注释独立成一行，恢复被吃掉的五个分支。

```csharp
if(playerDeadFG117&&(op==42||op==46||op==48||op==49))throw new InvalidDataException("Dead actor cannot fight, loot, move or settle");
// 206 stays allowed: a frozen scene can only be lifted by its own resume notify
if(op==84){...}if(op==105){...}if(op==253){...}if(op==29){...}if(op==28){DisjointFG92(body,sequence);return;}
```

语义与本次多角色改造的意图一致：死亡时仍拦 42/46/48/49，206 放行（原注释要表达的正是这一点）。

## 离线证据（`local-run/disjoint-fix/DisjointDispatchCheck-FG92.cs`）

驱动与稳定源码同批编译，不开 socket、不碰在线存档，在 `local-run/disjoint-fix/store/DNF70-batch-test-FG`
（在线存档的非备份快照，564 KB、34 份）上直接调用真实的 `Handle`：

```
captured client request: slot=10 list=0 tool=-1 bodyBytes=8
PASS Norton (npc13) is registered in town 2 area 0
role 8 slot 10 before: item=18113 qty=1 material3037=0
-- dispatch of the captured command 28 --
  evidence: DISJOINT_COMMITTED slot=10 rewards=1
PASS command 28 is dispatched, not UNIMPLEMENTED
PASS command 28 commits a disassembly for slot 10
role 8 after: material3037=1 gained=1 slot10=(removed)
PASS source equipment left the bag
PASS material 3037 was credited
-- repeated sequence --
  evidence: DISJOINT_DUPLICATE_IGNORED seq=4242
PASS repeated sequence is ignored instead of paid twice
PASS duplicate left the ledger unchanged
-- fresh sequence on the emptied slot --
  evidence: DISJOINT_REJECTED Disjoint target is not ordinary owned equipment
PASS a fresh sequence with the emptied slot is rejected
PASS rejected request left the ledger unchanged
FG92_OK
```

同一份驱动在修复前只会走到 `UNIMPLEMENTED opcode=28`（线上 23:20:31 的实际日志），因此
“抓到 UNIMPLEMENTED → 修完 COMMITTED”本身就是派发分支的对照证据。

编译与冒烟（暂存目录 `local-run/disjoint-fix/build/`，164 源）：两个宿主 exe 均编译通过，
无参数运行返回 2（与 `scripts/build.py --smoke` 的断言一致）；`python scripts/check_repo.py` 通过。

## 部署（2026-10-25 23:38，用户主动要求重启服务端与客户端）

修复刚做完时用户要求不重启，所以运行中的 29944/44928 一直是 23:14 那份带缺陷的产物；23:37 用户改为
要求重启，按下述顺序部署：

1. 预编译校验：当前工作区 164 源编译到 `local-run/disjoint-fix/prebuild/`，通过（避免一键脚本先停服后编译、
   编译失败就把服务停在半路）。
2. `local-run\one-click.ps1 -Build -NoOneHitKill -Client`：编译写出 `build/stable`（164 源 + 数据），
   旧日志归档到 `logs/archive/20261025-233755`（259 份，含本次抓包证据）。带 `-NoOneHitKill` 是因为原服
   启动在 `LISTENING ... oneHitKill=0`，不能因为重启就打开测试秒杀。
3. 一键脚本的自检有个时序缺陷：它在拉起后**只等 900ms** 就要求两个进程都已存在，这次报
   `服务进程启动即退出(存活 0/2)` 并退出（服务实际还在加载 4.5MB 的任务/物品表）。按它打印的参数用
   WMI 直接拉起两个服后，7002/17001 均在监听。
4. 新进程：频道 12224、入口 45204（起 23:38:29），`LISTENING loopback=7002 ... oneHitKill=0`；客户端 18392
   起 23:39:13，23:39:24 登录成功、23:39:25 `ROLE_BOUND slot=3 roleId=8 name=12345 job=8`。

部署后再用同一份 `Deployed28Probe`（反射加载 `build/stable/DNF70-Channel-FG.exe` 本身、在存档副本上驱动
真实派发）复检：

```
同一抓包请求 → note: DISJOINT_COMMITTED slot=10 rewards=1
material3037 0 -> 1  committed=True unimplemented=False  → DEPLOYED28_OK
```

对 23:14 的旧产物跑同一探针是 `UNIMPLEMENTED opcode=28`（`DEPLOYED28_UNIMPLEMENTED`），前后对照成立。
重启后的新会话日志里已经没有 28/29/84/105/253 的 UNIMPLEMENTED。

## 边界

- 本次一行修复同时恢复了 84/105/253/29/28 五个分支；这四个此前同样返回 UNIMPLEMENTED 的功能
  （品质重铸、宠物孵化、传送、药水/丹）会一起恢复，属于预期，不是附带改动。
- 分解本身仍按 FG92 既有规则：只接受装备栏 1..40 格、数量 1 的普通装备，产物走 `Offline70Economy`
  原子提交 + 收据防重复；没有放宽任何校验或改掉率。
- 只处理本次上报的分解机；多角色、称号/时装等其它在改项不在本次范围。注意这次部署编译的是当前工作区，
  因此也带上了并行在改的称号/时装/物品表改动（例如商城 67 目前会 `CASH_PURCHASE_REJECTED`），
  那些属于另一条线，出问题按那条线处理，不影响本次分解修复。
