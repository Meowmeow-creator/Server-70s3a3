# FIX 2026-10-26：技能锁定（禁用手搓）重登后丢失 —— FG344 技能命令样式持久化

## 症状

技能栏右键"锁定技能"（锁死后该技能不能用搓招/手搓指令放出）当场生效，但重新登录或换角色再进游戏后，锁定状态全部消失，恢复为未锁定。

## 根因（全链取证）

1. **锁定操作零网络流量**：全部近期会话日志（`logs/archive/20261026-010537` 等 4 份）逐 op 盘点，锁定前后没有出现 op344（SKILL_COMMAND_CUSTOMIZING）/op345（ALL_DEFAULT）或任何其他未实现 op——客户端锁定时**不发任何包**。
2. **锁定不落本地盘**：游戏目录 `DNF.cfg` mtime 仍是 2011-01-03（客户端从不写它），客户端其他配置也不写文件——锁定是纯客户端内存态。
3. **恢复源 = op19 的每技能命令尾巴**：rednf 权威实现 `CUser::send_skill_info`（0x0866c46a）里，op19（SKILLINFO）每条已学技能记录是 `{slot, id, level, cmdCount, keys...}`，`cmdCount=0` 表示"用默认指令"。本服务端旧实现（`Offline70FeedbackFG129.SkillsBody`）把第 4 字节**恒发 0**——客户端每次登录都被重置为"默认指令"，锁定/自定义搓招自然全丢。

## 修复设计

- 新增 per-role 存档 `skill-command-role-<id>.bin`（`S70CMD34` 魔数 + u32 条目数 + 每条 `{u16 skillId, u8 keyCount, u8 keys[8]}`，原子写 + 读回校验），存两样东西：玩家自定义搓招（op344 上报）和锁定标记。
- **锁定编码 = `{keyCount=1, keys=[8]}`**：参考服务器 wire 校验允许键值 0..8、末键不得为 {0,1,2,3,7}，而 8 是唯一不对应任何真实按键的合法值（rednf 服务端收到 8 会因 3-bit 打包存不下而改写成 7，恰证明 8 只在 wire 上存在）——一条永远无法按出的"指令"即等效锁定。**此编码是本次唯一待实测校准点**；若实测表现不符，只改 `Offline70SkillCommandFG344.Lock` 一处。
- op19 下发（`SkillsBody` 扩展）：有存储条目的技能改发 `{count, keys...}`，无条目的技能与旧版逐字节一致（driver 已证 102 字节全等，零回归）。
- 新接 CMD **op344**（自定义搓招上报，按参考规则校验：条数>49/未知技能→0xfc，键数∉1..5→0xfb，键值>8→0xfa，末键∈{0,1,2,3,7}→0xfa）+ **op345**（全部恢复默认=清空存档），ACK 走 type1 op332/op333（成功 `{01}`，失败 `{00, err u16}`）。与参考不同的是：单条校验失败**不清空整个存档**（参考服会 defaultSkillCommand 全清，单机没理由烧掉整栏）。
- GM 指令 `lock-skill` / `unlock-skill`（FG201）：绕过"客户端不发锁定包"的问题，把锁定直接登记进存档；已学技能校验 + 城镇门 + 客户端在线时推 `skills19` 刷新。

## 改动文件

- `src/Tools/VmTestKit/Skill-Command-FG344.cs`（新）：存档/解析/合并/锁定编码/op344 wire 解析。
- `src/Tools/VmTestKit/Contract-Cargo-Skills-FG129.cs`：`SkillsBody` 加命令尾巴重载；`SkillsReplyFG129` 携带命令档；`CustomizeSkillCommandFG344` / `DefaultSkillCommandFG345` 处理器。
- `src/Tools/VmTestKit/Batch-Service-FG.cs`：`commandPath` 字段 + `BindRole` 绑定 + `Handle` 分派 op344/345。
- `src/Tools/VmTestKit/GM-Console-FG201.cs`：`lock-skill` / `unlock-skill`。
- `local-run/gm_probe.py`：CLI 白名单加两 op。
- `local-run/driver/SkillCommandCheck-FG344.cs`（新）+ `Op212Dump-FG.cs`（新，op212 取证工具）。

## 验证

- 离线 driver **9/9 ALL PASS**：无条目时 op19 与旧版逐字节等长等值；锁定条目存储回路；`{1,8}` 正确拼进锁定技能记录；op344 合法/四类非法请求（0xfb/0xfa/0xfa/0xfc）。
- `scripts/check_repo.py` 428 文件通过；`build.py --smoke` 167 源编译通过；双服已重启生效。
- GM 实测（对运行中服务）：lock id=5 → 存档 23 字节 `{S70CMD34,1,{5,1,[8]}}`；unlock → 存档删除；重复 unlock 优雅无操作；未学技能 id=200 拒绝。
- 顺带修复：并行塔批次的 `Apc-Monsters-FG.cs` 两行嵌套花括号笔误（42/43 行各多 1 个 `}`，编译被挡），按其本意补全。

## 用户实测步骤

1. 登录 yoyo（服务端已预置 id=5 的锁定条目）→ 打开技能栏看 id=5 技能是否呈锁定态；再右键锁定/解锁几个技能、自定义搓招，重上游戏/换角色验证保持。
2. 若锁定态显示不对（乱码/崩溃/无效果）：跑 `python local-run/gm_probe.py unlock-skill id=5` 清场，回报现象；校准只动 `Offline70SkillCommandFG344.Lock` 编码一处。
3. GM 入口：`python local-run/gm_probe.py lock-skill id=<已学技能id>` / `unlock-skill id=<id>`。

## 遗留

- 客户端对 `{1,[8]}` 的具体表现待实测（编码推理见上，有 GM 校准入口兜底）。
- 客户端真实右键锁定仍不会上报服务端（客户端行为，无法改协议）；锁定一旦经 GM/自定义登记，跨登录恢复由本修复保证。若要让"客户端右键锁定"也持久化，需要客户端补丁截获锁定动作——另行立项。

## 追加取证（2026-10-26 03:00,用户实测"仍重置"后）

用户实测证明锁定仍不跨登录。深挖客户端后的**架构定论**:

1. **op464 = 客户端的锁定表全量上报通道**(此前会话从未出现,用户首次测试锁定才触发):520B Blowfish(索引2),布局 = `{u32 512} + {u32 initFlag(0xFFFFFFFF=窗口初始化转储/0=有效), 252B 0xFF, 128×u16 锁定技能id升序列表(0xFFFF填充,明文偏移260起)} + {u32 0}`。开技能栏/每次锁定切换都全量重发(15 帧 dump 解密与用户操作逐帧吻合:[25]→[25,169]→[5,25,169]→[5,25]→[5]→[])。**服务端现已实现 op464 handler 存储该列表**(带初始化转储保护),此后服务端实时掌握玩家每次锁定。
2. **客户端无任何网络恢复路径**:锁定表 0x1840268(前段=按槽命令数据,0x1840368/0x18403E8=两页各 64×u16 锁定 id 列表)在世界进入时被 memset 清空(0xC0FAA0),列表由技能管理器(0x193ABEC)重建(0x890680/0x8907D0),管理器锁标志只有本地开关会置位。**锁定=会话本地状态,官方协议里服务端从不参与恢复**。
3. **op19 尾部 {count=1,keys=[8]} 的锁定编码已证伪并撤下**:客户端把键打包进 3-bit 字段(0..7),8 会被截断成 0,不但锁不了还可能改写技能显示的搓招。SkillsBody 现在只回放真实键值的 op344 自定义条目,锁标记不再进 op19。
4. 已核实客户端把锁定表序列化进角色存档流(0x46AF20,写游标 0x1964FB8),但读侧只有 memset 清空没有回填——存档流去向未闭环,不作为恢复通道。

## 结论与后续(客户端补丁)

服务端已能持久化每次锁定(op464 存储 + GM lock/unlock),**要让锁定跨登录恢复,必须给客户端打补丁**:世界进入时锁定表被清空(0xC0FAF4 的 memset(0x1840268,0xFF,0x200)),补丁在该点之后把服务端随某个既有包带来的锁定列表写回 0x1840368/0x18403E8 并触发重建。候选载体:①op19 末尾追加 128B 锁定表(客户端按记录数解析,尾部字节疑似被忽略,需一次实测);②文件法(补丁在 op464 发送点写文件、在 memset 后读文件)。补丁规模 ~100-200B 代码洞 + 1-2 处 5 字节跳转,风险与既有 0x7FC08C/0x1092DC0 补丁同级,动手前备份 exe。**等用户确认后再动客户端。**

## 客户端补丁已落地（2026-10-26 11:24，用户确认后执行）

**补丁内容**（`local-run/patch-client-skilllock.py`，可重放；已备份 `DNF70_v50.exe.backup-skilllock-20261026-113943`）:

- 代码洞 `0x1D60000`（4KB 对齐零页，.text 可执行）内放两个桩 + 路径串 + 临时变量。
- **钩子 A @0x890530**（`push 0x1d0`，op464 发送前一刻，锁定表 0x1840268 已终态）→ SAVE 桩：`WriteFile` 整 512B 表到 `D:8S3A3\地下城与勇士\skilllock.dat`，随后重放原指令并跳回。
- **钩子 B @0xC0FB0A**（`call 0xa7da00`，紧跟世界进入时清空锁定表的 `memset(0x1840268,0xFF,0x200)`）→ LOAD 桩：`ReadFile` 回填整表，随后**带守卫地调 0x890680**（技能管理器就绪时把锁定表同步进技能栏槽对象），再重放原 call 并跳回。
- 两个桩均 pushad/pushfd 全保护、栈平衡严格、弹出钩子返回地址后重放被替换指令；反汇编逐条核验过（曾修掉 rel32 偏一字节）。

**服务端配套**（`Contract-Cargo-Skills-FG129.cs`）:`PublishClientSkillLockFG344` 在**每次进世界（op4/World）时按当前角色**重建 skilllock.dat(flag=0 + page0 前 64 个锁 id 升序)，多角色各恢复各的。GM lock/unlock 写服务端档，下次进世界同步进文件。

**已验证**:客户端字节正确(钩子 call 目标、代码洞内容、GBK 路径串)；服务端 LoginDriver 登录 yoyo 后文件生成 512B，page0=[5]，日志 `CLIENT_LOCK_FILE_PUBLISHED role=1 ids=1`。

**待用户实测**:客户端已因打补丁被关闭——重新用启动器开游戏 → 登录 yoyo → 技能栏应看到 id=5 锁定 → 任意锁定/解锁 → 重登/换角色验证保持。若客户端起不来，把 `DNF70_v50.exe.backup-skilllock-20261026-113943` 改回原名即可回滚。

## 补丁第一版崩溃与修复（2026-10-26 11:31，/GS 0xC0000409）

首版打上后客户端登录进世界 0.6 秒闪退（TP BugTrace: ExceptionCode 0xC0000409，ExceptionAddress 0x0132550B，故障点在 LOAD 钩子所在函数 0xC10006 的栈哨兵检查）。根因:**Windows API 是 stdcall、被调方自己清参**,而桩里又在每个 API 后 `add esp,N` 二次清栈,栈被逐次抬高踩坏外层函数栈帧与 canary。修复:删掉全部三个 `add esp`;同时去掉 LOAD 桩里对 0x890680 的立即同步调用(角色加载期技能栏槽对象可能未就绪,改由客户端开技能栏时的既有流程 0x890680 完成表→槽同步)。第二版已重新打补丁并启动成功(pid 43892)。

## 通用版（v2,2026-10-26 11:40，任意安装路径）

- **客户端补丁 v2**：路径不再写死——桩内 `GetModuleFileNameW(NULL,buf,260)` 取自身 exe 全路径，反向扫描最后一个反斜杠，追加 `skilllock.dat`，用 `CreateFileW` 打开。宽字符全链路（主 kernel32 IAT 有 GetModuleFileNameW/CreateFileW）。桩 151B×2，位于代码洞 0x1D60000；宽路径缓冲 0x1D60200、追加串 0x1D60420、临时变量 0x1D60440。反汇编核验（含失败分支）。
- **服务端路径解析**：`ResolveClientSkillLockPathFG344()` 每次进世界时读 `<store>/TestTools/client-lock-path.txt` 第一行（UTF-8），无配置回退旧路径 `D:8S3A3\地下城与勇士\skilllock.dat`。
- **启动器自动写配置**：`one-click.ps1`（在 `$clientExe` 解析后）与 `try-v50.ps1`（在 `$p` 后）把 `<clientDir>\skilllock.dat` 写入该配置；**必须 `-Encoding UTF8`**——`-Encoding ASCII` 会把中文目录名写成 `??????`（已实测踩坑并修正）。
- 实测：配置覆盖（临时路径）与缺省回退均发布成功；删除游戏目录文件后登录 yoyo 自动重建 512B（ids=[5]）。

## 补丁 v3：拖技能进快捷栏闪退的修复（2026-10-26 20:27）

**症状**：把转职页技能（本次 source=109）拖进快捷技能栏，客户端约 0.2 秒内静默闪退。服务端日志显示 op30 `SKILL_SWAP_COMMITTED source=109 target=2` 正常提交并回包，随后 `PEER_CLOSED`。

**证据**：

- TP `start/TenProtect/2026-10-26-18-35-55.dmp.z` + `BugTrace.log`：`ExceptionCode 0xC0000008`（STATUS_INVALID_HANDLE），`ExceptionAddress 0x77CC172A`。32 位 ntdll 基址 `0x77C70000`，RVA `0x5172A` 落在 `RtlLeaveCriticalSection`（RVA 0x51660 起）的 raise 分支。
- 崩溃帧第一个参数 `0x00A9D755` = `0x890535 + 0x20D220`，正是以 `this=0x890535` 算出的临界区地址；`0x890535` 也是钩子 A 的返回地址。
- 调用链（BugTrace 栈）：技能窗刷新 `0xD9D410` → `0xD99D90` → `0x595D40`（拖拽/换位处理，`0x1016D00` 发 op30）→ `0xDA8D30` → `0x8903B0`（op464 发送）→ `BeginPacket 0xBC78D0`。

**根因**：v2 SAVE 桩尾部用 `pop ecx` 丢弃钩子返回地址，覆盖了 ECX。`0x890535` 的下一条指令 `call 0xBC78D0` 依赖 `ECX = [0x18E7C78]`（连接对象 this）；于是 BeginPacket 用 `0x890535` 当 this，`EnterCriticalSection` 写到代码段地址 `0xA9D755`，`LeaveCriticalSection` 判定临界区无效 → `0xC0000008`。开技能栏时的 op464 走另一处未打钩的发送点（`0x89090B`），因此此前只有“拖技能进快捷栏”会触发。

**修复**：`local-run/patch-client-skilllock.py` 的桩尾改为 `popfd; popal; add esp,4`（不触碰任何寄存器），两处失败分支统一跳回该尾部；被替换指令（SAVE 的 `push 0x1d0`、LOAD 的 `call 0xa7da00`）照常重放。脚本内置自检：尾部字节、失败分支目标、重放指令与跳回目标。

**部署**：新增 `--upgrade [exe]`，可在已装钩子的 exe 上只重写代码洞。已升级 `地下城与勇士/DNF70_v50.exe`（备份 `DNF70_v50.exe.backup-skilllock-ecxfix-20261026-202724`）以及 `dist/DNF70-一键端-20261026` 下两份客户端副本。字节级校验 PASS（仅代码洞 30 字节变化）。

**边界**：运行中的客户端仍映射旧代码，需重启客户端后修复才生效；`dist` 下两个 zip 仍是修复前的 exe，分享前需重新打包。

## v4（2026-10-27）：去掉 skilllock.dat，改为 S2C op369 协议下发

**动机**：v1–v3 靠客户端 exe 同目录的 `skilllock.dat` + `<store>/TestTools/client-lock-path.txt` 桥接，换安装路径、分发和权限都会出问题。锁定本身仍是客户端会话态（读侧没有原生恢复路径，见上文 xref 取证），所以 exe 补丁保留，但载体改成服务端下发的一个接收包。

**客户端（`local-run/patch-client-skilllock.py` v4，幂等，可从 clean/v3/v4 就地升级）**

- `0x38B8D8`：分发表边界 `cmp eax,0x170` → `0x171`（1 字节）。op369 在原生客户端的表项是 int3 填充，未打补丁的客户端会把它静默丢弃，因此新服务端 + 旧客户端不会崩。
- `0x7911C8`：op369 表项 → `0x1D60000` 收包桩。
- 收包桩（72B）：校验 body 首 4 字节魔数 `S70L`（`0x4C303753`），把 `{u32 tab, 64×u16 page0, 64×u16 page1}` 写到 `0x1840268 / 0x1840368 / 0x18403E8`，同时镜像到洞内 260B 缓冲并置 pending；`jmp 0x790BEF`（分发表默认出口，`xor eax,eax` + SEH 收尾）。
- `0xC0FB0A`：保留 v3 的世界进入钩子，但 LOAD 桩从 `ReadFile` 改成“pending 则 memcpy”；v3 的 `0x890530` 保存钩子已还原为 `push 0x1d0`，客户端不再写任何文件。
- 只写 tab 字和两页锁定表，不碰 `0x184026c..0x1840367` 的按槽命令区，op19 的自定义搓招不受包序影响；进世界 memset 先把表清成 0xFF，pending 桩在 memset 之后回填，因此包早到/晚到都正确。

**服务端（`Contract-Cargo-Skills-FG129.cs` + `Skill-Command-FG344.cs`）**

- `PublishClientSkillLockFG344()` 不再写文件/读路径配置，改为 `Send("skill-lock369", BatchPacketFG(0,369,body,keys,false))`；body = 264B `{u32 'S70L', u32 0, 排序后的 64×u16 锁定 id（0xFFFF 填充）, 64×u16 0xFFFF}`。
- op464 仍是锁定表上传权威（客户端开技能栏/切换时全量上报），闭环 = op464 上传 → 按角色存档 → 每次进世界 op369 下发。
- `one-click.ps1` / `try-v50.ps1` 删除 `client-lock-path.txt` 写入。

**验证（2026-10-27）**

- `check_repo.py` 428 文件 / `build.py --smoke` 176 源通过。
- `SkillCommandCheck-FG344` 重编译后 17 项 ALL PASS：新增 op369 魔数/长度/tab/排序/page1 填充检查；旧的“锁标记走 op19 尾部”断言按 03:00 已证伪的行为改成“锁定不影响 op19 字节”。
- LoginDriver 登录 yoyo：收到 `type=0 op=369 bytes=279`（15 头 + 264 体）；证据侧车 `channel-evidence.log.00005-skill-lock369.bin` 解密（index=369%14=5）得到 `53 37 30 4c 00000000 0500 ffff...` = 魔数 S70L、tab 0、page0=[5]、page1 全 FFFF。
- 已打补丁：`地下城与勇士/DNF70_v50.exe`（备份 `.backup-skilllock-v4-20261027-103219`）+ `dist/DNF70-一键端-20261026` 两份副本；两个 zip 内的 exe 已替换（原 zip 备份 `*.pre-v4.bak`，zip 文件名仍是 20261026）。

**待实机验收**：重开客户端 → 登录 yoyo → 打开技能栏应看到 id=5 锁定 → 锁定/解锁、拖技能进快捷栏（v3 崩溃场景）、重登/换角色验证保持。运行时客户端仍映射旧代码，必须重开客户端后补丁才生效。

**兼容/回滚**：旧客户端 + 新服务端 = op369 被丢弃（锁定不恢复但不崩）；新客户端 + 旧服务端 = 无包可收（锁定不恢复）；必须成对分发。回滚客户端 = 恢复 `.backup-skilllock-v4-*`；回滚服务端 = 恢复文件发布版（v3 段）。v3 的 `skilllock.dat` 与 `client-lock-path.txt` 已移到 `local-run/backup-2026-10-27-skilllock-dat/`，可随时取回。
