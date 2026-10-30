# 2026-10-25 点券改为账号钱包（FG271）

## 现场

同一账号下的角色各有各的点券，商城余额跟着角色走。实档读数（`local-run/DNF70-batch-test-FG`，23:30 只读查看）：

| 角色档 | RoleId | OfflineCash | BoundCash |
|---|---|---|---|
| `economy-account1-yoyo-FG.xml` | 1 | 9400 | 0 |
| `economy-account1-r2-FG.xml` | 2 | 25604 | 0 |
| `economy-account1-r7-FG.xml` | 7 | 1400 | 0 |
| `economy-account1-r8-FG.xml` | 8 | 86230 | 0 |
| r9 / r10 / r12 / r13 | 9/10/12/13 | 0 | 0 |

## 根因

`Offline70EconomyState.OfflineCash` 是**角色档**字段，而角色档按 `Offline70RoleScopeFG202.EconomyFile(roleId)`
一个角色一个文件（`Batch-Service-FG.cs:20`、`Batch-Service-FG.cs:44`）。商城扣款
（`Cash-Purchase-FG45.cs:25/31`、`Cash-Cart-FG104.cs:24/30`、`Feedback-Cash-FG108.cs:34/51`、
`Economy-State-FG.cs:55`）、GM 发券（`GM-Console-FG201.cs:212`）和 op53 余额包（`Economy-Protocol-FG.cs:7`）
全都读写这一份角色档里的钱，文件名里的 `account1` 只是历史遗留，没有任何一处按账号取数。

原版语义是账号级：参考服务端按登录号取余额和结算
（`reference/rednf/remake/source/DNFServer/GameServer/Game/NXCash/getBalance.inc:3`，入参 `userId`，
配合 `nexon::cash::MAX_NEXON_ID_LENTH`，不是角色 id）。

## 代码修改

点券（连同没有任何发放线路的 `BoundCash`）搬进一份账号侧车档 `cash-account1-FG271.xml`，
形状照已有的账号侧车 `premium-account1-FG158.xml`（`BlackQualification-FG158.cs:10`）。

1. 新增 `Account-Cash-FG271.cs`：侧车档的校验、读、写（加锁 + pending + `File.Replace` + 回读），
   以及一次性并档 `Merge`。侧车档**不存在时一切照旧**——`Inject`/`Adopt` 直接返回，
   老存档与所有既有夹具目录行为不变。
2. `Economy-State-FG.cs`
   - `Load` 拆成 `Load`（读原始档后覆盖钱包字段）+ `LoadRaw`（不注入，供并档统计各角色档里的旧值）。
   - `Initialize` 在提交前先 `Inject`，避免 `Commit` 的回读校验（`verify.OfflineCash!=s.OfflineCash`）
     在新建角色上误判；诊断种子 10000 只在侧车档还没生成的首次迁移里计数，不会重复入账。
   - `Apply` / `ApplyWithReceiptFG176` 在 `Commit` **之前** `Adopt`：先把钱落到账号钱包，再提交角色档。
     中途出事只会"付了钱没到货"（角色档仍是旧的），不会出现"白得一份货"。数额没变就不写，
     所以纯背包/任务事务不碰账号钱包。
3. `Batch-Service-FG.cs`：构造期在旧 yoyo 迁移之后、`Sync` 之前调用一次 `Merge`，把名册里每个角色档自带的
   点券求和成账号余额；回执文本 `CASH_ACCOUNT_MERGED roles=.. cash=.. bound=..` 先存进
   `cashMergeNoteFG271`，`Run` 里 evidence writer 建好后与 `LISTENING` 一起写日志（构造期 `Note` 是空放的）。
   读不动的角色档记名不挡登录（`unreadable=`），没档的记 `absent=`。
4. `GM-Console-FG201.cs`：`cash` 操作的注释与玩家可见回执改为账号钱包口径。

扣款、发券、op53 余额包、GM 快照属性一处没动：它们读写的 `assets.OfflineCash` 现在就是账号值。

## 离线证据（`local-run/scratch/cashfg271/`，未开 socket、未碰在线存档）

1. 机制回归 `CashShareCheck.exe`（166 源独立构建，全新可丢弃目录）：**16/16 通过**——
   并档前各读各的（5000/3000）；并档后两个角色都读到 8000；角色 2 买 2000 → 账号剩 6000 且角色 3 立刻看到 6000；
   同回执重放不再扣款；角色 3 用 GM 加 1500 → 角色 2 看到 7500；不动点券的事务不 bump 账号钱包 revision；
   超额扣款被拒且余额不变；并档后新建的角色一进来就是 7500（不再额外发钱）；`Merge` 重跑返回 null 不重复累加。
2. 真实存档形状 `CashStartupCheck.exe`：拿 `local-run/DNF70-batch-test-FG` 的**副本**（35 个文件，排除
   `.backup-*/.pending-*/.lock`）跑完整服务构造（不调 `Run`，不开端口）：构造成功 →
   `cash-account1-FG271.xml` 生成，`account cash=114384 bound=0 revision=1`；
   名册 8 个角色（1/2/7/8/9/10/12/13）逐个 `Load` 全部读到同一个 114384；
   副本里 8 份角色档的 OfflineCash 求和 = 114384，与账号钱包逐字相等（**并档无损**）；
   第二次构造 revision 仍是 1、仍是 114384（不会二次累加）。
3. `python scripts/check_repo.py` 通过：`repositoryChecksPassed=true checkedFiles=428 stableSources=165`。
4. 登录器：`cargo test` **50 passed / 0 failed**（新增 `mall_balance_follows_the_account_wallet_sidecar`）；
   项目自带 typescript 走 `ts.createProgram` 检查 `src/main.ts`，**0 diagnostics**。

## 编译证明的口径

上面两条证据都出自**源码隔离副本**（`local-run/scratch/cashfg271/`）：把 `src/Tools/VmTestKit/*.cs` 复制到
scratch，只加两个一次性检查程序，用同一个 `csc.exe v4` 编译，输出到 scratch 自己的目录，不碰 `build/stable`。
最后一轮把副本整体刷新到当时的仓库源后重编，`CashShareCheck` 与 `CashStartupCheck` 两个目标都是
**0 error**（`check_repo.py` 同时通过，165 源）。

期间 `Pot-Catalog-FG93.cs`（23:40 写入的 `uplegacy-pools-FG204`）有一阵把 11 条 `p.Add` 挂在
`return p;}` 之后，编译报 CS1519/CS0116/CS1022；那是另一条在跑的线的未提交改动，我没有动它，
23:46 那边自行补好了。仓库稳定编译此刻只剩 CS0016（产物被运行中的服务端占用）。

`Offline70EconomyFixtures` 的 `NullReferenceException` 已 A/B 对照：带本修改与不带本修改（164 源基线树）
**报错完全一样**，是夹具自身的前置条件问题，不是本修改引入的。


## 部署（本轮没有重启服务端）

按要求我没有停服、没有覆盖运行中的 exe。`python scripts/build.py` 现在唯一报错是
`CS0016: 无法创建 build\stable\DNF70-Channel-FG.exe —— 文件被另一进程占用`，即所有 C# 源已经编过、
只差落盘；等空闲时按 `local-run/one-click.ps1 -Build` 走标准流程即可。

实际发生的事：另一条在跑的线在 23:47 用 `sourceCount=165`（已含本修改）出过稳定产物，此后 23:50、23:58、
00:03 又各拉起一次进程（本轮我全程没有停服、没有覆盖产物）。23:58 那份 `DNF70-Channel-FG.exe` 里
逐串探针实测：`Offline70AccountCashFG271`、字面量 `cash-account1-FG271.xml`、`CASH_ACCOUNT_MERGED`、
新回执文案"点券已写入账号钱包"、旧文案"点券已写入存档"（无）、字段 `cashMergeNoteFG271` ——
前四项 YES、旧文案 NO、字段 YES，即**含本轮全部改动**的产物已经在跑。

因此**并档已经在这份在线存档上发生过一次**，实测：

```
local-run/DNF70-batch-test-FG/cash-account1-FG271.xml
  Revision=1  OfflineCash=114384  BoundCash=0
logs/channel-evidence.log  23:53:07
  WORLD_RESTORED level=70 ... offlineCash=114384      ← 在线角色 2，自身档里原本只有 20324
logs/channel-evidence.log  00:03:31
  WORLD_RESTORED level=70 ... gold=59138 offlineCash=114384   ← 换到 yoyo（自身档仍是 9400）
```

并档前后逐档读数（r2 20324 + r7 1400 + r8 83260 + yoyo 9400，其余 0）求和 = 114384，与账号钱包逐字相等，
在线存档没有损失也没有翻倍。`CASH_ACCOUNT_MERGED` 这一行没进当前日志：并档发生在 evidence writer 建好之前，
`Note` 空放；已在下一段补掉，之后每次真正并档都会留痕（现存存档再启动时 `Merge` 返回 null，不再重复累加）。


本轮另外补的两处，也已经在 23:58 那份产物里（上面逐串探针已证）：

- 构造期 `Note` 是空放的（`evidenceFG171` 要到 `Run` 里才 new），改成先记在 `cashMergeNoteFG271`，
  `Run` 里 writer 建好后与 `LISTENING` 一起落日志。在线存档已经并过档，重跑 `Merge` 返回 null，不会二次累加。
- GM `cash` 操作的玩家可见回执改成账号口径："点券已写入账号钱包 …（同账号所有角色共用这一笔…）"。



## 边界

- 并档把每个角色档自带的点券**求和**进账号，没有销毁也没有翻倍；角色档里旧值不再被读，只作为缓存被覆写。
- 只改点券/代币券两个字段的归属：金币、复活币、经验、任务、背包仍旧逐角色。
- 跨角色的"重复扣款保护"仍逐角色（回执记在角色档里），同一连接一次只绑一个角色，所以不会跨角色误判重复。
- 两次文件写入（账号钱包 → 角色档）不是一个事务，取的是"先扣钱后发货"的失败方向；
  本地单机存档，窗口只可能出现在进程被强杀于两次写入之间。
- 老存档、夹具目录、以及没起过新版服务端的存档：侧车档不存在，行为与改动前逐字节一致。
- 真人客户端内的商城表现未验（要等你重启后实机点一次商城）。
