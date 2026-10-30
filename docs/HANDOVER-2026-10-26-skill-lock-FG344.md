# 交接：技能锁定持久化（FG344）— 2026-10-26

> 状态：**已完成并被用户实测验收**（"修复好了"）。服务端已部署、客户端 v2 通用版补丁已应用并验证。
> 本文覆盖：需求、根因、协议与客户端逆向结论、改动清单、验证证据、分发与回滚、已知限制与后续可选工作。

## 1. 需求与验收

技能栏右键锁定某个技能（锁死后该技能不能用搓招/手搓指令放出）。旧行为：锁定**当次会话**有效，但重上游戏或重选角色后全部恢复未锁定。

验收：锁定跨重登/换角色保持；多角色互不串档；任意安装路径可用（通用版）。

## 2. 根因（两半，缺一不可）

### 2.1 客户端侧（决定性事实）

- **锁定操作零网络上报、零本地落盘**：逐 op 盘点全部历史会话日志，锁定/解锁时段无任何未知/相关 op（早期搜 344/345 全 0 命中）；游戏目录 `DNF.cfg` mtime 仍是 2011 年。锁定是**纯会话内存态**。（后来才发现上报其实存在，见 2.2。）
- **客户端没有"从网络恢复锁定"的路径**：世界进入时锁定表被 `memset(0x1840268,0xFF,0x200)` 清空（函数 0xC0FAA0 内 0xC0FAF4）；锁定列表（0x1840368 树0 / 0x18403E8 树1，各 64×u16）由技能管理器（0x193ABEC）经 0x890680/0x8907D0 重建；管理器的锁标志只有本地开关会置位。**官方协议设计里服务端从不参与锁定恢复。**
- 客户端内部结构：`0x1840268` = 512B 表 `{u32 标志(0=有效,0xFFFFFFFF=初始化中), 0xFF 填充, 每页 64×u16 升序锁定 id(0xFFFF 填充, 偏移 0x100/0x180)}`；**不按技能 id 索引，是打包列表**。技能键值在客户端按 3-bit 打包（0..7），这是关键约束（见 2.3）。

### 2.2 上报通道 = op464（此前从未被抓到，因为用户没测过锁定）

- **op464 CMD**（客户端→服务端，520B，Blowfish=index 2）：`{u32 512} + {u32 initFlag, 252B 0xFF, 128×u16 锁定 id（明文偏移 260 起，0xFFFF 填充，升序，每页 64 个）} + {u32 0}`。
- 开技能栏、每次锁定/解锁都**全量重发**。初始化转储的特征是 `initFlag==0xFFFFFFFF`（服务端 handler 据此跳过，防止登录后首次开栏就把存档清掉）。
- 实证：15 帧 dump 解密与用户操作逐帧吻合（[25]→[25,169]→[5,25,169]→[5,25]→[5]→[]）。

### 2.3 为什么"往 op19 拼锁定编码"行不通（已证伪并撤下）

参考服务端 rednf 的 `send_skill_info`（0x0866c46a）每技能记录是 `{slot,id,level,cmdCount,keys...}`，曾尝试用 `{count=1, keys=[8]}` 表示锁定。**证伪**：客户端把键值打进 3-bit 字段（0..7），8 会被截断成 0，不但锁不了还可能改写技能显示的搓招。现状：SkillsBody 只回放**真实键值**的 op344 自定义条目，锁标记**不**进 op19。

## 3. 方案（服务端 + 客户端补丁）

### 3.1 服务端（C#，已入源码/构建）

| 文件 | 内容 |
|---|---|
| `src/Tools/VmTestKit/Skill-Command-FG344.cs`（新） | 按角色存档 `skill-command-role-<id>.bin`（魔数 `S70CMD34`，条目 `{u16 id,u8 count,u8 keys[8]}`，原子写+读回校验）；op344 wire 解析（rednf 规则：条数>49/未知技能→0xfc，键数∉1..5→0xfb，键值>8→0xfa，末键∈{0,1,2,3,7}→0xfa）；锁定条目编码 `{1,[8]}`（仅服务端存档语义，不回放） |
| `src/Tools/VmTestKit/Contract-Cargo-Skills-FG129.cs` | `SkillsBody` 三参重载（op19 命令尾巴回放真实自定义指令，锁标记跳过）；`SkillsReplyFG129`（带 `SKILLS19 body/cmds` 诊断日志）；**`SkillLockTableFG344`（op464 handler：解析锁定列表→合并进按角色存档，初始化转储跳过）**；`ResolveClientSkillLockPathFG344`（读 `<store>/TestTools/client-lock-path.txt`，缺省回退旧绝对路径）；`PublishClientSkillLockFG344`（**每次进世界按当前角色重建 512B 锁定文件**）；op344/op345 处理器（自定义搓招/全部默认，ACK type1 op332/op333） |
| `src/Tools/VmTestKit/Batch-Service-FG.cs` | `commandPath` 字段 + `BindRole` 绑定；`Handle` 分派：`op==344/345/464`；`World()` 在发送 select 前调用 `PublishClientSkillLockFG344()` |
| `src/Tools/VmTestKit/GM-Console-FG201.cs` | `lock-skill` / `unlock-skill`（id=1..255，已学校验、城镇门、在线推 skills19） |
| `local-run/gm_probe.py` | CLI 白名单加 `lock-skill` / `unlock-skill` |

### 3.2 客户端补丁（v2 通用版）

- 脚本：`local-run/patch-client-skilllock.py`（全 ASCII 源码；可重放；带原字节断言）。
- 已应用：`地下城与勇士/DNF70_v50.exe`，回滚备份 `DNF70_v50.exe.backup-skilllock-20261026-113943`。
- **钩子 A @0x890530**（`push 0x1d0`，op464 发送前一刻，表已终态）→ SAVE 桩：`GetModuleFileNameW(NULL,buf,260)` 取自身 exe 路径→反扫最后一个 `\`→追加 `skilllock.dat`→`CreateFileW`（CREATE_ALWAYS）+`WriteFile` 512B。结束重放 `push 0x1d0` 并 `jmp 0x890535`。
- **钩子 B @0xC0FB0A**（`call 0xa7da00`，紧跟世界进入清表 memset 之后）→ LOAD 桩：同法拼路径→`CreateFileW`(OPEN_EXISTING)+`ReadFile` 回填 512B→重放 `call 0xa7da00` 并 `jmp 0xC0FB0F`。
- 代码洞 `0x1D60000`（4KB 对齐零页，.text 可执行）：SAVE 桩 0x1D60000、LOAD 桩 0x1D60100（各 151B）、宽路径缓冲 0x1D60200、追加串 0x1D60420（UTF-16LE）、临时变量 0x1D60440。
- 桩规范（下次再改必须遵守）：pushad/pushfd 全保护；**Win32 API 是 stdcall，被调方清参，桩内不得再 `add esp`**（第一版就是因此 /GS 闪退）；调用进入的返回地址要 `pop ecx` 丢弃后再重放原指令；所有 rel32 用"指令起点+5"精确计算并反汇编核验。

### 3.3 启动器联动（通用路径）

- `local-run/one-click.ps1`（`$clientExe` 解析后）与 `local-run/try-v50.ps1`（`$p` 后）自动写 `<store>/TestTools/client-lock-path.txt` = `<clientDir>\skilllock.dat`，**必须 `-Encoding UTF8`**（ASCII 会把中文目录写成 `??????`，已踩坑）。
- 不使用这两个启动器时：手动建该配置文件（UTF-8，一行完整路径）。

## 4. 验证证据

- 离线 driver `local-run/driver/SkillCommandCheck-FG344.cs` + `build/stable/SkillCommandCheck-FG344.exe`：**9/9 ALL PASS**（无条目时 op19 与旧版逐字节一致 102B；存档回路；`{1,8}` 拼接位置；op344 合法/四类非法）；另有 `Op212Dump-FG.exe`（op212 取证工具）。
- `scripts/check_repo.py` 428 文件、`build.py --smoke` 167 源全绿。
- LoginDriver：yoyo 登录 → `SKILLS19 body=154 cmds=1`、`CLIENT_LOCK_FILE_PUBLISHED role=1 ids=1`；配置覆盖（临时路径）与缺省回退均验证；删除游戏目录文件后登录自动重建 512B。
- 客户端：v2 启动存活；用户实测锁定跨重登保持 ✓（v1 曾因 stdcall 二次清栈 /GS 闪退，v2 修复）。

## 5. 分发（给朋友）

两样都要：
1. **打过补丁的 `DNF70_v50.exe`**（当前这份；任意安装目录可用）；
2. **更新后的服务端**（build/stable 两个 exe，或整棵源码重新构建）。

前提：朋友的 `DNF70_v50.exe` 必须**同一构建**（补丁按固定地址打；脚本会校验原字节，不同构建会安全拒绝——此时把 exe 发来重新定位钩子）。若不用 one-click/try-v50 启动，需手动写 `client-lock-path.txt`。

回滚：`DNF70_v50.exe.backup-skilllock-20261026-113943` 改名回 `DNF70_v50.exe`。

## 6. 已知限制 / 后续可选

1. **锁定在会话内要生效，需打开一次技能栏**：恢复的只是表；表→技能槽同步（0x890680）由客户端开技能栏的既有流程完成。世界加载期直接调用它曾被认为有风险（槽对象未就绪），v2 未调用；若要"进世界立即生效"，可在世界构建完成后的某个稳定点挂钩再调 0x890680（另行验证）。
2. 锁定文件是**单机全局**一份，服务端按角色在每次进世界时重写；客户端 SAVE 会把当前会话状态写回。因此多角色切换的准确性依赖"服务端 op464 按角色存档 + 每次 op4 重写文件"这条链，**不要在角色切换后、进世界前手动改文件**。
3. 客户端右键锁定的动作本身仍不上报 op464 之外的任何东西（op464 就是全部），服务端不参与其语义；锁定是否"生效"（禁止手搓）完全由客户端本地开关决定。
4. 本补丁修改 exe 代码段（两处钩子+代码洞），TenProtect 实测未拦截；若未来客户端更新/换构建，补丁需按新地址重做（脚本断言会先拒绝）。
5. op344/op345（自定义搓招协议）虽已实现并回放，但当前客户端在日志中从未发送——自定义指令的完整客户端闭环未被实测过（锁定时不受影响）。

## 7. 常用命令

```bash
# 构建与部署（先停服）
python local-run/stop-services.py
cd Server-70s3a3 && python scripts/check_repo.py && python scripts/build.py --smoke
cd .. && powershell -ExecutionPolicy Bypass -File local-run/restart-detached.ps1

# 离线自测
Server-70s3a3/build/stable/SkillCommandCheck-FG344.exe "D:/70S3A3/local-run/DNF70-batch-test-FG"
D:/70S3A3/local-run/driver/LoginDriver.exe 127.0.0.1 7002

# GM 锁定/解锁（角色级存档；下次进世界写入客户端文件）
python local-run/gm_probe.py lock-skill id=5
python local-run/gm_probe.py unlock-skill id=5

# 打/重打客户端补丁（先关游戏；脚本自带备份与断言）
python local-run/patch-client-skilllock.py --dry-run
python local-run/patch-client-skilllock.py

# 回滚客户端
cd 地下城与勇士 && copy /y DNF70_v50.exe.backup-skilllock-20261026-113943 DNF70_v50.exe
```

## 8. 关键地址速查（客户端 DNF70_v50.exe）

| 地址 | 含义 |
|---|---|
| 0x890530 | op464 发送块起点（`push 0x1d0`，钩子 A） |
| 0x890210 | 重建锁定表并发送 op464 的函数（开技能栏/切换时） |
| 0x890680 / 0x8907D0 | 表↔技能栏槽对象同步（开栏时表→槽；槽变化时槽→表） |
| 0xC0FAF4 | 世界进入时 `memset(0x1840268,0xFF,0x200)`（钩子 B 在 0xC0FB0A） |
| 0x1840268 | 512B 锁定表（u32 标志 + 命令区 + 两页锁定列表） |
| 0x1840368 / 0x18403E8 | 树0/树1 锁定 id 列表（各 64×u16） |
| 0x193ABEC | 技能管理器单例（+0x479C 数据区） |
| 0x487D60 | 技能窗口开启流程（内部调 0x890680 与 0x890210） |
| 0x1D60000 | 本补丁代码洞 |
| 0x790C04 | **网络包分发表**（索引 0..0x170；每 case 构造带 opcode 的包对象）。注意：本次会话曾误判为 UI 组件表，已在实证（cmp eax,0x170 与 op508 无 handler 闪退）下纠正。 |

## v4（2026-10-27）：协议载体替代 skilllock.dat

- 服务端不再写 `skilllock.dat`/读 `client-lock-path.txt`：`PublishClientSkillLockFG344()` 改为在每次进世界时发 `type0/op369`，body 264B = `{u32 'S70L', u32 0, 64×u16 排序锁定 id, 64×u16 0xFFFF}`；op464 仍是上传权威。
- 客户端补丁 v4（`local-run/patch-client-skilllock.py`）：分发表边界 `0x38B8D8` 的 `0x170→0x171`（1B）+ op369 表项 `0x7911C8`→`0x1D60000` + 72B 收包桩 + `0xC0FB0A` 钩子换成内存回填桩（pending→memcpy）；v3 的 `0x890530` 保存钩子已还原为 `push 0x1d0`。收包桩只写 `0x1840268` 的 tab 字和 `0x1840368/0x18403E8` 两页，不碰 `0x184026c..0x1840367` 命令区。
- 验证：driver 17 项 ALL PASS；LoginDriver 收到 `op=369 bytes=279`；证据侧车解密为 `S70L + tab0 + page0[5] + page1 FFFF`。主客户端与 `dist` 两份副本、两个 zip 内 exe 均已升级（备份 `.backup-skilllock-v4-*` / `*.pre-v4.bak`）。
- 兼容：旧客户端把 op369 当未知 op 丢弃；新旧必须成对分发。详见 `docs/FIX-2026-10-26-skill-command-lock-FG344.md` 的 v4 段。
