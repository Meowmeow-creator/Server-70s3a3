# FIX 2026-10-29：「破坏物件」类任务即时计数 —— FG384 客户端上行 + 服务端接收

## 背景

FG383 已让 `[hunt enemy]` enemyType=3（破坏囚牢/了望台/书架/油桶…）在**房间结算**时合成记账。玩家要求改成"打破物件那一刻 +1"。取证结论：该客户端对物件破坏**零上行**（031554 会话 478 帧全解密：无 op42 passiveFlag、无 op35；只有 op541/542/543 遥测），所以必须在客户端侧补一个上行。本批次落地：

1. **客户端补丁**（`local-run/patch-client-cage-break-trigger.py`）：hook 通用 actor 死亡虚槽 `0x009502D0`，对非怪物实现（`[vtable+0x54] ∈ {0x9502D0, 0x7B2700, 0x40EA50}`，排除 61 个怪物的 `0x895690` 族）读 `[ecx+0xC9C]`（actor id，唯一写点 `0x9538D0`），用客户端自己的发包链 `0xBC78D0/0xBC79C0/0xBC7A00`（cipher `key%14`→索引 7，已实证 `0xFCC440` 魔数 `0x92492493`）合成 op35 体 `{0x0023, 0xFFFF, objectId, 1}` 发出。代码洞 `0x1D51100..0x1D5119F`（154 字节，capstone 逐条核对），body 暂存 `0x1D511A0`，诊断计数器 `0x1D511A8/AC/B0/B4`。真实客户端已打补丁（备份 `DNF70_v50.exe.bak-fg384-20261029-045628`），打补丁后客户端可正常启动（WMI 拉起、TP 未拦）。
2. **服务端接收**（`Batch-Service-FG.cs` op35 分支）：`questId==0xFFFF` 视为物件破坏上行，直接走既有 `PassiveObjectKillFG360(group)`——房图 `[passive object]` 声明校验、每房声明数封顶、receipt 幂等、任务计数推进、op35 进度回包全部复用。伪造/重放上行不能超记。已部署（199 源编译，双服 37816/28132）。

## 验证

- 补丁脚本在副本上 dry-run/写入/verify 全过；服务端 `check_repo` 429 文件通过、`build.py --smoke` 199 源通过，已部署。
- **v1 实机失败教训（2026-10-29 09:xx）**：v1 在钩子里用代码洞内的绝对地址写诊断计数器（每次 actor 死亡都写、先于类型过滤），实机"一打怪就崩"（AppHang + 多次 10 秒内静默退出）。根因：运行时代码页保护 = `PAGE_EXECUTE_WRITECOPY`（VirtualQueryEx 实测 0x80），运行时写代码页会让页面变私有脏页，反篡改扫描当场杀进程；静态改文件（补丁本身）不受影响（副职业补丁先例）。**已回退客户端，玩家恢复可玩。**
- **v2 设计**：运行时零镜像写入——8 字节 op35 包体改为栈上现场构造（`sub esp,8` / 写 `[esp..]` / `mov ebx,esp`），三个发包调用后 `add esp,8` 归还；诊断计数器全部删除（验证信号改用服务端日志 `PASSIVE_OBJECT_REPORT`）。cave 126 字节，capstone 逐条核对，栈平衡。副本上 PATCHED + MATCH 通过。
- 待实机确认（v2）：打破笼子瞬间任务计数 +1；服务端日志 `PASSIVE_OBJECT_REPORT id=8824`（无 `CREDIT_SYNTHETIC` 前缀）后跟 `PASSIVE_OBJECT_CREDITED`。

## 诊断与回退

- 诊断：`python local-run/driver/cage_break_probe.py --watch` 读客户端计数器（PROCESS_VM_READ，非调试附加——DebugActiveProcess 会杀客户端）。`total/whitelisted` 应随怪物/物件死亡增长，`last_id` 应出现物件 id。
- **若打破物件时 `total` 不动**：说明物件破坏不走通用死亡虚槽 `0x9502D0`，转次优钩子 `0x0094C720`（共享伤害入口，入口同样可读 `[ecx+0xC9C]`，需动态定 HP 归零判定；已有逆向结论见 recon）。
- 回退客户端：`python local-run/patch-client-cage-break-trigger.py --restore`（或把 `.bak-fg384-*` 拷回）。
- FG383 的房间结算合成记账保留为兜底：客户端不发（未打补丁/补丁失效）时任务仍可在房清时计数；两条链共用 `DestroyPassiveObjectFG360` 的每房声明数上限，不会双记。

## 证据与出处

- 客户端逆向：op35 构造点 `0xA44D20`（会校验 questId，故不能用它发 0xFFFF，改走通用发包链）；op35 原生体 5 字节 `{0x23, questId, group u8}` 补零到 8；actor id 写点 `0x9538D0`；死亡虚槽 `0x9502D0`/`0x9502D5` 续接点。
- 实机抓包：`request-*-35-*.bin` 解密全部为 `{23 00, questId, 00 00 00 00}`；物件房窗口零事件帧。
