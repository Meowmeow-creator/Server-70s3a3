# 交接：商城目录全量接通 · 契约票 · 魔王之契约页签（FG272，2026-10-26）

> 面向下一位接手者的完整交接。先读第 0、1、6 节；第 2–5 节是改动与验收，第 7 节是可直接复制的命令。

## 0. 一句话现状

- **商城目录（cerashop.etc）已全量接通**：除 4 条数据自带停用标记的行外，所有 commodity 都能通过 op67 购买。
- **101117 王者成长礼包**：购买即生效（霸王/达人各 +7 天），不再产生不能用的契约票；2681943 关联券照旧入包（其系统未实现）。
- **100625 魔王之契约礼包**：可正常购买、扣费、不弹“使用期限设置错误”、**不再入包**；服务端只记 `ServiceDaysFG272` 天数台账（当前 r8 = 120 天，由两张购买回执反推）。
- **未解决**：客户端“期限道具信息栏 → 魔王之契约”页签仍然灰。已排除 premium 类型/背包限时道具/select 包体三条路线；需要逆客户端独立的“角色增值服务”状态包。第 6 节是全部线索与下一步。
- 当前线上：**12:02 构建**（含 `fg272-service-sync2`），双服运行中（7002 频道 / 17001 入口）。三套离线驱动 + `check_repo.py` 全绿（167 稳定源）。

## 1. 用户可见状态（角色 r8 实测）

| 项目 | 状态 |
|---|---|
| 霸王/达人契约 | 正常，期限道具信息栏“优惠”页显示（22/27 两条） |
| 王者成长礼包（101117, 1980 券） | 一次性；购买后 22/27 各 +7 天；发 2681943×1、复活币×10、雷米×50；**33/45 票不再入包** |
| 魔王之契约礼包（100625, 3880 券） | 可重复购买；扣费成功；**不入包**；`ServiceDaysFG272` 累加 60/单 |
| r8 存档现状 | premium = {22, 27}；无 8 个服务道具；`ServiceDaysFG272=120`；`CashOncePurchases={101116,101117}` |
| 魔王页签 | **灰（未解决）** |

## 2. 代码改动清单

| 文件 | 改动 |
|---|---|
| `src/Tools/VmTestKit/Cash-Package-FG272.cs`（新） | 王者礼包（Special=10）+ 魔王礼包（Special=11）+ 11 个纯物品商品；`Grant`；`MigrateTickets`（33/45→22/27 +7 天）；`MigrateServices`（回执反推天数、删 0..7、收回误发道具）；`ServiceItems` 8 个官方包内容 id；`MoonReceiptHash` |
| `src/Tools/VmTestKit/Cash-Purchase-FG45.cs` | `Register()` 末尾调用 `Offline70CashPackageFG272.Register()` |
| `src/Tools/VmTestKit/Feedback-Cash-FG108.cs` | `Resolve`/`Valid`/`Purchase` 接 Special=10/11 |
| `src/Tools/VmTestKit/Contract-Cargo-Skills-FG129.cs` | 18 个 `[selectable character premium]` 单项服务（Special=3, type 0..8）；`CashRefreshFG129` 对 Special=10/11 也重发 op66；`Validate` premium kind 放宽到 0..105、条目上限 32 |
| `src/Tools/VmTestKit/Economy-State-FG.cs` | `CashOncePurchases` 白名单加 101117；允许 **Kind=1** 带 `ExpiresUnix`（实验遗留，可保留）；`Use` 增加过期拒绝；新增 `ServiceDaysFG272` 字段 + 校验上限 36500 |
| `src/Tools/VmTestKit/Batch-Service-FG.cs` | `World()` 恢复链：`MigrateTickets` → `MigrateServices` → `Sync()` |
| `src/Tools/VmTestKit/GM-Console-FG201.cs` | 新增 `premium` 操作（`mode=add/del`、`kind`、`days`），当场推 op66；用于探针 |
| `data/avatar-cash-catalog.xml` | 追加 15 行（28584→28599），`[avatar]` 28599 行全覆盖 |
| `data/item-metadata.xml` | 追加 5 条 Kind=3（39056/40311/53849/53850/53851），总 19459 条 |

### 行为要点 / 坑
- 王者礼包发放时**跳过 33/45**（不落包），改为 `ApplyContract` 延长 premium 22/27；`MigrateTickets` 会把旧档里已有的 33/45 换成 +7 天并删除（回执 `fg272-ticket-migrate` 幂等）。
- 魔王礼包**不发道具**：`Grant` 只 `ServiceDaysFG272 += o.Days`。`MigrateServices` 回执 `fg272-service-sync2`：按回执计数×60 天写台账、删 premium 0..7、把任何误发的 8 个道具从背包收回。
- `MoonReceiptHash = sha256("cash-selection:|100625:11:0:60") = a22bc398e3317c945d1bc4f0a2636ebd76dfbe930bc4866c064ad801413d1e1b`。改造 100625 的 Days/Ability 会让这个指纹失效；以后新购买直接累加 `ServiceDaysFG272`，与该反推保持一致。
- `Economy.Validate` 的 Kind=1 期效放宽只是实验需要；如果以后要收紧，必须先确保没有任何存档残留带期效的 Kind=1 条目。
- 并行会话：`Batch-Service-FG.cs`、`Economy-State-FG.cs` 常被其他会话同时修改；动这两个文件前后都要重跑 `check_repo.py` + 三驱动，构建前确认没有别的会话在编译。

## 3. 目录覆盖差集（cerashop.etc 全量）

**已接**：
- `[avatar]` 28599 行（FG210）、`[nametag]` 30 条（FG108）、稀有装扮 8 条（FG108）+ 兑换 563000-563007（FG211，payment=1）
- `[coin]` 4 条、`[item]` 61 条（FG80 + FG272 补 101100/101101/100782/100534；100063-100069/101062 由 FG129 货舱升级接）、`[creature]` 12 条（宠物模块 + FG272 补 101076/101077）
- `[premium]` 全部 **enabled** 行：霸王/达人（FG129）、拍卖行券、技能类型扩展、18 个单项契约服务（FG129）、关联券×4+更名卡（FG272 发物品）
- `[package]` 101117（FG272）、101116（FG80）、`[charac premium package]` 100625（FG272）

**故意不接**（数据自带 `field20=99` 停用标记，客户端不卖）：
`117465`（Lethe3day，价格 1）、`100043`（霸王，价格 1）、`100061`（达人）、`100122`（拍卖行券）——保持干净拒绝。

**已知使用缺口**（购买都正常，但“使用”未实现）：
- 33/45 契约票：不需要使用（已改为购买即生效）。
- 2681943 帐号角色关联券：角色关联系统（op313 CONNECT / 314 DISCONNECT / 315 CHANGE_LINK_TYPE + 关联券通知包）未实现。
- 秘药（101100/101101 → 2682616/2682617）的经验 buff、角色更名卡（100902）、帐号金库升级工具（100534）的使用路径未实现。秘药的取证、根因与实现方案单独成文：[经验加成秘药交接](HANDOVER-2026-10-26-exp-bonus-potions.md)。

## 4. 离线验收

目录：`local-run/scratch/cash272/`

| 驱动 | 覆盖 | 结果 |
|---|---|---|
| `PackageCashCheckFG272.exe` | 目录解析/支付边界/王者礼包（契约即时、票不入包）/魔王礼包（台账、0..7 不再写）/二次购买 +60 天/一次性/混合车原子性/回执反推迁移与幂等/重读校验 | ALL PASS（约 26 项） |
| `CatalogSweepCheckFG272.exe` | 28599 时装 + 111 普通商品 + 8 兑换 + 30 名称卡全量 Resolve；停用行拒绝；8 件抽样一车购买 | ALL PASS |
| `LiveStoreCheckFG272.exe` | 在线档**副本**：8 角色档全载入、买两件、契约 +7 天、魔王台账、迁移收道具、重读 | ALL PASS |

复编译（Git Bash，注意 `-` 前缀与 rsp 用反斜杠路径）：

```bash
cd /d/70S3A3
python - <<'PY'
from pathlib import Path
root=Path(r"D:\70S3A3\Server-70s3a3\src\Tools\VmTestKit")
out=Path(r"D:\70S3A3\local-run\scratch\cash272")
files=sorted(root.glob("*.cs"))
for rsp,extra in [("pkg.rsp","PackageCashCheckFG272.cs"),("sweep.rsp","CatalogSweepCheckFG272.cs"),("live.rsp","LiveStoreCheckFG272.cs")]:
    (out/rsp).write_text("\n".join('"%s"'%p for p in files+[out/extra]),encoding="utf-8")
PY
CSC=/c/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe
"$CSC" -nologo -target:exe -main:PackageCashCheckFG272 -out:"D:/70S3A3/local-run/scratch/cash272/PackageCashCheckFG272.exe" "@D:/70S3A3/local-run/scratch/cash272/pkg.rsp"
./local-run/scratch/cash272/PackageCashCheckFG272.exe "D:\\70S3A3\\Server-70s3a3\\data"
```

## 5. 构建/部署

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File 'D:\70S3A3\local-run\one-click.ps1' -Build
```

- 作用：停旧服 → `build.py --smoke` → 归档日志 → WMI 分离拉双服 → 端口自检。
- 确认新代码进包（UTF-16 字符串搜索）：

```bash
python - <<'PY'
data=open(r"D:\70S3A3\Server-70s3a3\build\stable\DNF70-Channel-FG.exe","rb").read()
for s in ["fg272-ticket-migrate","fg272-service-sync2","a22bc398e3317c945d1bc4f0a2636ebd"]:
    print(s, data.find(s.encode("utf-16-le"))>=0)
PY
```

- 进世界时会自动跑两条迁移；迁移回执写入角色档，重复进世界不会重复执行。

## 6. 未解决：魔王之契约页签（核心遗留）

### 6.1 已排除（都有实测证据，别再试）
1. **premium type 路线**：激活过 0–12、16–28、49–58、85–105，页签始终灰；变化的只有右上角图标栏/优惠页/悬浮名称（16-21=霸王族、23-26=达人族、9-12=祝福/守护/玩家等旧契约名）。`premiumlist.etc` 全表没有魔王服务条目。
2. **背包限时道具路线**：发过 8 个 60 天期效的服务道具（2681934…2682206），页签仍灰；用户确认官方语义是“购买即生效、不入包”。
3. **select(op4) 包体**：`Offline70SelectCipher.Body` 全字段已映射（id/创建时间/疲劳/20 任务槽/城镇/教学位），没有 premium/service 区。
4. **op537 PREMIUM_SERVICE**：参考端的包号超出本 2013 客户端接收分发表（≤368），内存读表确认表尾 0xCC 填充。

### 6.2 客户端锚点（可直接复用）
- **分发表**：文件偏移 `0x390C04`，`table[op]` = 运行地址；`RVA = 运行地址 - 0x400000`；此 exe 的 RVA == 文件偏移。`dis_tool.py` 的参数用 **RVA**。
- **`.str` 表内存布局**：`{hash(u32), key_ptr, text_ptr}` 12B 步长；`time_limited_items_info_window_00/01/02` 的 hash = `0xAF87E474/75/76`（= 期限道具信息栏/优惠/魔王之契约，ui.chn.str 索引 366/367/368）。
- **chn_characpremium.etc 解析对象**：全局 `0x18ef6f4` → 对象 `0x18ef700`；+0x74/+0x88/+0x9c 是三组 8 字节记录 vector；getter `0x85a2b0`/`0x85a370`/`0x85a3e0`/`0x85a410`/`0x85a490`；“current type” 取 0x3b/0x3c/0x3d/0x55。消费者 xref：`0x713e0`、`0x7b240`、`0x1a71e0`、`0x589ac0` 附近。
- **op66 执行函数** RVA：`0x790310`、`0x790440`、`0x7906f0`、`0x790a80`、`0x791770`、`0x791d80`、`0x798b60`、`0x7a6bc0`、`0x7a87e0`；分发指针表在数据段 `0x1094200` / `0x10948a0` 附近。
- **字符串离线解密**：算法在 `local-run/tmp/ui-static/keynames.py`（blob 头 w0/b1/w2 → seed/ln/key → 滚动 XOR；0x2CA1 表示明文 UTF-16）。全 exe 绝对地址扫描解密后**没有** `time_limited`/`premium`/`魔王`/`契约` 命中 ⇒ UI 窗口按 `.str` 索引引用，不按 key 字符串。
- **进世界时客户端发、我们未实现的请求**（`UNIMPLEMENTED` 日志）：`82`（空）、`66`（空）、`134`（空）、`157`（16B：`24 00 00 00 01 00...`）、`406`（4B 0）、`342`（16B）、`250`（16B：`01 00 00 00 01 01 00 00 00 F4 05 1E 01...`）、`541/543`（周期包，非请求）。
- 请求解密工具：`local-run/driver/RequestDecode-FG214.exe <log> <op>`。

### 6.3 下一步（按性价比排序）
1. **反查启动请求的回包格式**：对 op82/134/157/250/342 逐个用 `table[op]-0x400000` 找 handler，trampoline→parser→execute，确认哪个读“9 个服务 + 剩余时间”的列表。找到后服务端新增对应回包（世界入口/购买后发送），页签即可点亮。注意 `0x790100` 的“list-parse”家族有 15 个调用点（RVA `0x79037b/0x7904b1/0x79075c/0x790aec/0x79186c/0x791dec/0x7a0a9d/0x7a186b/0x7a300f/0x7a4060/0x7a6d30/0x7a93c1/0x7ac3d6/0x7ac819/0x7ada9d`），可作交叉参照。
2. **窗口打开时内存 diff**：让用户打开期限道具信息栏 → 扫描窗口对象（标题/tab widget）→ 找页签 enable 字节 → 反查写入代码。需要用户配合保持窗口打开。
3. 找到状态结构后：按 `ServiceDaysFG272`（r8=120 天）换算 9 个服务的到期时间并下发；同时把“购买即生效”接回。

### 6.4 GM 探针工具（已可用）
- 命令：`premium`，`mode=add|del`、`kind=0..105`、`days=1..3650`；当场重发 op66。
- 请求文件格式（放 `<store>/TestTools/<guid N>.gm.xml`）：

```xml
<GMRequest version="FG201" token="<service.xml 里的 token>" receipt="<32位guid>" op="premium" mode="add" kind="49" days="1" />
```

- **坑**：客户端未连接时 GM 写入落在 **yoyo** 档；先发 `role roleId=8` 切绑定。`token` 每次服务重启都会变，必须从 `TestTools/service.xml` 动态读。
- 现成一键脚本：`local-run/TestTools-probe/probe-devil.ps1`（参数 `-From/-To`）、`cleanup-devil.ps1`、对应 `.bat`（PowerShell + UTF-8 BOM；别再用内联 `-Command` 拼，容易转义坏）。

## 7. 常用命令速查

```bash
cd /d/70S3A3/Server-70s3a3 && python scripts/check_repo.py
# 三驱动（见第 4 节）; 构建部署（见第 5 节）
# 看当前角色档状态
python - <<'PY'
import re
t=open(r"D:\70S3A3\local-run\DNF70-batch-test-FG\economy-account1-r8-FG.xml",encoding="utf-8").read()
print("premium:",sorted(int(m.group(1)) for m in re.finditer(r"<Kind>(\d+)</Kind><Expires>",t)))
print("serviceDays:",re.findall(r"<ServiceDaysFG272>(\d+)</ServiceDaysFG272>",t))
PY
```

## 8. 本次改动文件清单

- 新文件：`src/Tools/VmTestKit/Cash-Package-FG272.cs`
- 修改：`Cash-Purchase-FG45.cs`、`Feedback-Cash-FG108.cs`、`Contract-Cargo-Skills-FG129.cs`、`Economy-State-FG.cs`、`Batch-Service-FG.cs`、`GM-Console-FG201.cs`、`data/item-metadata.xml`、`data/avatar-cash-catalog.xml`（后者在仓库里是未跟踪文件）
- 文档：`docs/FIX-2026-10-26-cash-packages-FG272.md`（逐轮流水账，含全部证据与失败假设）
- 离线工具：`local-run/scratch/cash272/`（三个驱动 + rsp）、`local-run/TestTools-probe/`（GM 探针脚本）

## 9. 交接提醒

- 这个仓库的稳定源是 167 个 `.cs`（`check_repo.py` 会报数）；新增文件只要放进 `src/Tools/VmTestKit/` 就会被构建自动收录。
- 构建前先停服（one-click 会做）；运行中的 exe 被锁定时 `csc` 会报 CS0016。
- 用户在线时不要手动改在线角色档；所有状态改动走服务端事务或 GM 控制台。
- 所有 `D:/70S3A3` 路径在工具文本里建议用正斜杠（反斜杠+70 会被某些工具层当八进制转义）。
