# FIX 2026-10-26：副职业不显示 —— 参照包的 notify 205 在本客户端是空处理器，真正形态在 217

## 症状
`副职业 - 分解师`（任务 2710）完成后游戏里没有任何反应：存档已经是分解师
（`economy-account1-r8-FG.xml` 的 `ExpertJobTypeFG183=3`，`skills-r8-EZ.xml` 里有技能 194），
但副职业界面不出现，重进角色也不变。

## 原因（两层）
1. 服务端从来没告诉过客户端副职业。副职业既不在 op4 选角 ACK 里（参照服务端
   `Game/Inter_LoadEtc_impl/SendResult.inc` 里没有该字段），也不在 op19 技能表里，
   它只有一个专门的 notify：参照服务端 `Game/ExpertJobOnlyMgr/SendExpertJobInfo.inc`
   （`put_header(0,0xcd)`），在角色副职业数据加载完成时由 `Inter_LoadExpertJob::dispatch_sig`
   紧接 `loadData` 主动下发。**参照服务端里的 384 是它自己的 DB/Inter 分派槽
   （`APSystemOnlyOrig/RegistDispatcher.inc:199`、`InterDispatcher.inc:195`），不是客户端 CMD**，
   所以永远等不到"客户端请求 384"。
2. **notify 编号存在版本位移，不能照抄参照服务端。** 参照包写 205，但
   `DNF70_v50.exe` 把 205 注册成了一条空处理器：`0x65b9c0` 处只有单条 `ret`
   （前后都是 int3 填充），它属于该客户端自带的一张"忽略此包"集合
   （45 个 opcode 共用这个空函数：25,62,63,67,108-119,123,128,129,133,166,179,190,196,197,
   205,210,216,238,239,241,257,269,287,295-297,300,309,322,323,328,355,370,476）。
   本客户端真正解析副职业的是 **notify 217**，处理器 `0x47e830`。
   同一位移在别处也有记录（参照 194→本客户端 206、170→182）；
   低位编号是本位对齐的（op19 SKILLINFO、op21 ACCEPTABLE_QUEST_LIST、op28 DISJOINT_ITEM、
   op33/36/37 逐一对上），**所以每个 notify 号都要在本客户端找锚点校验，不能整表平移**。

## 客户端实际的读法（`0x47e830` 反汇编）
```
get_byte  -> [char+0x2cc]        // giveupCount
get_byte  -> expertType；sub 1 / cmp 3 / ja skip，跳表 0x47e9e8：
   type 1/2/4 -> 0x47e895：get_byte count（->[char+0x2d8] 向量 clear+resize），循环 count 次 get_dword 追加
   type 3     -> 0x47e941：get_dword -> [char+0x2d0]，get_dword -> [char+2d4]
收尾按 [char+0x2a0] 与 0/5 比较后调 0xd8d660(…,4,…) 刷新副职业界面
```
即 `byte giveup + byte type + (type==3 ? dword,dword : byte count + count*dword)`，
字段形态与参照服务端的 `SendExpertJobInfo` 一致，只有 opcode 不同。

## 改动
- `src/Tools/VmTestKit/Reward-Types-FG183.cs`：新增 `ExpertJobInfoOpcode=217`、
  `ExpertJobInfoBody`、`ExpertJobInfoReply`。type3 的两个 dword 填分解机等级+1 与耐久；
  耐久取 PVF `character/expertjob/disjointer.exj` 的 `[endurance initial value]=300`
  （不是编造的数）。本引擎的分解走 op28 直接拆、不经过分解机，所以等级/耐久按初值上报，
  不做扣减；附魔/炼金/人偶三种的配方表没有数据源，发空列表（count=0）。
- `src/Tools/VmTestKit/Batch-Service-FG.cs`：两处下发——登录序列里
  `Send("skills",...)` 之后（对齐参照服务端的"加载完就推"），以及授职分支
  `Send("expert-skills",...)` 之后（这样完成副职业任务当场就有反馈，不需要重登）。

## 验证（隔离实例，不碰在跑的 7002）
`local-run/build-staging-fg283/DNF70-Channel-FG.exe --batch-store
local-run/isolated-test/expertjob/DNF70-batch-test-FG --test-port 17041 --interactive`
配 `local-run/driver/ExpertJobLoginPushCheckFG283.cs`（按已验证的 `opcode%14` 块索引反解通知体）：
- slot 3 = roleId 8「12345」（分解师）：登录后收到 `217x1`，帧 31 字节，明文
  `00 03 01000000 2c010000` → giveup=0 / type=3 / 等级+1=1 / 耐久=300；
  服务端日志 `SENT expert-job-info type=0 opcode=217 bytes=31`。
- slot 0 = roleId 1「yoyo」（无副职业）：`217x1`，帧 23 字节，明文 `00 00`（参照的 default 分支就是只发两字节）。

**未验出**：真人客户端界面渲染。上面只证明包号和字段形态按本客户端的解析器对齐了。

## 追加（同日，玩家实机反馈"还是没有"之后的客户端侧取证）
面板仍然不亮，原因不在服务端，而在本 build 客户端的闸门字段：

- 面板/列表要求 `[角色+0x2a0] != 0` 才建条目（`0x4b7315`、`0x4b7884`、`0xff6271/0xff66e2/0xff6b42`），
  分解师快捷按钮要求 `[角色+0x2a0] == 3`（`0xd8aa44`、`0xd8ac1c`，且 `≠0 且 ≠5`），
  `0x814fd0`（取副职业类型）在该字段为 0 时返回 5。
- **217 处理器不写 `+0x2a0`**：它只写 `+0x2cc`(giveup)/`+0x2d0`(等级)/`+0x2d4`(耐久)，
  而且它函数尾部自己就要先读 `[char+0x2a0]` 才决定刷不刷新界面 —— 也就是说 217 无法自举。
- 全镜像扫 `mov [reg+0x2a0],…`（disp32 形态）47 个候选，逐个看所在函数：构造函数置 0、
  别的类钳到 [1,2] 的 setter、0x2a8 字节记录数组的配置表写入、清理代码、以及一处逐字段块拷贝
  （`0x5994F0`）——**没有一个落在调用包读取原语（`0x10c6160/0x10c6190/0x10c61c0`）的函数里**。
- 顺着块拷贝往上：调用方之一是名册/角色信息解析 `0x7FDF10`（`0x7FE23C` 处
  `mov ecx,[0x18c8278]; add ecx,0x14; push 0x18c8330; call 0x5994F0`，即把单例模板记录
  `0x18c8330` 拷进列表元素）。但该模板的 `+0x29c/+0x2cc/+0x2d4`（绝对地址
  `0x18c85cc/0x18c85d0/0x18c85d4`）**在整个镜像里 0 次引用**，只有 `+0x2a8`（`0x18c85d8`）被写。
  所以这条链同样填不出副职业类型。

**结论**：按现有静态证据，本 build 的副职业面板/分解师按钮**无法只靠服务端下发数据点亮**。
保留一条未排除的可能：`.vmp0`/加壳区里存在运行时才物化的写入者，静态扫不到。

可选后续（都要单独决定，未做）：
1. 运行时验证：在 `0x47e830` 入口与 `[角色+0x2a0]` 上下内存写入断点，看真实登录流程里有没有
   加壳代码写它——这能一次性判死或找出真包。
2. 客户端 hook：照本 build 已有的 `.s70net/.job134/.ttl157` 等 patch 段先例，在 217 处理器里
   把包内 type 字节补写进 `[char+0x2a0]`（约 6 字节补丁位，需重定位）。
3. 记为已知限制：任务链、存档、技能（194）、分解（op28 实测可拆）都已正常，仅面板不显示。


---

## 已落地（2026-10-26）：选方案 2，客户端 217 处理器补写 `[char+0x2a0]`

### 补丁

- 脚本：`local-run/patch-client-expert-job-type.py`（`--dry-run` 预演、`--verify` 校验、可传任意 exe 副本）。
- 钩子：`0x47E87C` 的 5 字节 `movzx eax,[ebp-2]; dec eax` 改成 `call 0x1D51000`，
  代码洞执行完同样的两条被搬走指令后 `ret` 回 `0x47E881`。
- 代码洞 `0x1D51000`（`.text` 第一个可执行区里 `0x1D50CB3..0x1D5FFA0` 的零填充；
  最近的 rel32 桩只到 `0x1D50CAA`，洞内无引用）：
  ```
  mov eax,[0x18aff18]          ; 217 处理器自己在 0x47E859 用的单例
  mov eax,[eax+0x3c]           ; 0x76e1c0 的返回值 = 当前角色
  test eax,eax / je skip
  movzx edx,[ebp-2]            ; 包里的 expertType
  test edx,edx / je skip
  mov [eax+0x2a0],edx          ; 关键写入（dword）
  skip: movzx eax,[ebp-2] / dec eax / ret
  ```
- 包里 type=0（无副职业）时**不写**，保留 `0x814FD0` 的“5=无”哨兵语义。
- 客户端备份：`地下城与勇士/DNF70_v50.exe.backup-expert-20261026-114121`。
  补丁只改这一处 5 字节 + 32 字节代码洞，其余字节不变。

### 运行时证据（非侵入 ReadProcessMemory，未用 DebugActiveProcess）

客户端角色 8（分解师，存档 `ExpertJobTypeFG183=3`）登录后实读：

```
singleton=[0x18aff18]=2D0ED050  character=[singleton+0x3c]=31997468
char+0x2a0 expertType = 00000003   -> disjointer
char+0x2cc giveup     = 00000000
char+0x2d0 grade      = 00000001
char+0x2d4 endurance  = 0000012C   (300, disjointer.exj 初值)
```

复现命令：`python local-run/driver/expert_field_peek.py`
（客户端在登录/选角界面时读不到角色，返回码 2；`expertType` 为 0 时返回码 3）。

### 为什么这能同时解释两个症状

- `0x4B7315` / `0x4B7884`（角色信息里的副职业面板/列表）在 `[char+0x2a0]==0` 时直接跳过；
- `0xD8AA44`（分解师快捷按钮）要求 `[char+0x2a0]==3`，`0xD8AC1C` 要求 `!=0` 且 `!=5`；
- 217 处理器尾部的刷新条件本身就是 `[char+0x2a0]!=0 && !=5`，所以旧包永远走“不刷新”分支；
- 技能 194 早已在 op19 的已学习列表里，但副职业身份没有落地，专属入口全部保持锁定。

### 边界（仍未做，别误报为已修）

- 分解机商店六件套（CMD 238/239/240/244/245/246）与副职业制作（CMD 242）仍是服务端空白；
  本次只解决“角色识别 + 角色信息面板 + 专属入口解锁”。若玩家点开个人分解机后服务端日志出现
  `UNIMPLEMENTED opcode=238/239...`，那是下一条独立特性。
- NPC 分解（CMD 28）与技能 191..194 的授予/显示不受影响，继续按原路径工作。

### 回滚

用上面的备份覆盖回 `DNF70_v50.exe` 即可（补丁脚本幂等，重复运行会打印
`already patched; nothing to do`）。客户端升级/重装后需重新运行补丁脚本。

---

## 追加（同日）：摆分解机无反应 —— 建店请求 233 / 机器信息 218

### 客户端实测链路

- 角色信息修好后，玩家点「摆分解机」仍无反馈。静态取证找到客户端自己的建店发送函数
  `0x718E70`：它 `push 0xE9`（**opcode 233**，不是参照端的 238），随后写
  `{byte flag, int nameBytes, nameBytes}`（`0x638E0` 写 4 字节字节长度 + 窄字符集名字）。
  真正发送前，店名对话框 `0xD58330` 会用 `get_disjoint_machine_grade()`（`[char+0x2d0]`）
  校验名字长度；新机器 grade=1，超过 1 个 UTF-16 字符就弹 `0xA4A5` 并**不发包**。
  这就是“没有反应”的来源之一。
- 服务端→客户端的机器信息是 **notify 218**（参照端 NOTI 206，同样 +12 位移），处理器
  `0x468570` 逐字段读取：`word uniqueId, 长度前缀窄名字, byte vill, byte area,
  word x, word y, dword level`，然后用 `0x36dfa0(uniqueId)` 找角色并在
  `0x9b4bd0/0x9b4cf0/0x9b4cd0` 处创建村对象。通知注册表在文件 `0x9E6D7` 处
  （`push 0x47e830 / push 0xd9` 即 217 副职业信息，紧邻的 `0x468570 / 0xda` 就是 218）。
- 建店 ACK 用请求同号 233、type 1、body `{1}`（参照 `OnCreateDisjointStore`
  的 `put_header(1,0xee); put_byte(1)`）。

### 服务端改动

- 新文件 `src/Tools/VmTestKit/Expert-Machine-FG183.cs`（partial）：
  - `op37 SET_USER_POSITION`：解密后取 `short x, short y` 存为机器落点（此前被当
    UNIMPLEMENTED 丢弃）；
  - `op233`：解密 `{flag,nameLen,name}`，校验 `ExpertJobTypeFG183==3`，发 218 +
    type1/233 `{1}`，日志 `EXPERT_MACHINE_CREATED`；
  - 非分解师回 type1/233 `{0}` 并记 `EXPERT_MACHINE_REJECTED`。
- `Reward-Types-FG183.cs` 新增 `DisjointMachineInfoOpcode=218`、
  `DisjointMachineInfoBody/Reply`（uid/nameLen/name/vill/area/x/y/level）。
- `Batch-Service-FG.cs` 在 `UNIMPLEMENTED` 前加 `if(ExpertMachineOp(op,body,sequence))return;`。

### 客户端补丁

- `local-run/patch-client-expert-machine-name.py`：`0xD5829E` 的 `76 09`（jbe）改成
  `EB 09`（jmp），跳过“名字太长”拒绝分支；后面的 `0xD5830B..0xD58324` 本来就会把
  名字截到 grade 长度，所以线上字节仍合法（1 个字符）。备份
  `DNF70_v50.exe.backup-expert-machine-20261026-122622`。

### 隔离实例实测（不碰 7002）

`local-run/driver/ExpertMachineCheckFG183.cs`（登录→选角 slot 3→op37→op233）：

```
EXPERT type=3 hex=0003010000002c010000
SEND op233 body=0001000000410000
NOTIFY218 kind=0 bytes=47 plain=080001000000410205f4012c010100...
ACK233 kind=1 bytes=23 plain=0100000000000000
MACHINE uid=8 nameLen=1 name=A vill=2 area=5 x=500 y=300 level=1
RESULT EXPERT_MACHINE_INFO OK
```

服务端日志：`SENT expert-machine-info type=0 opcode=218`、
`SENT expert-machine-ack type=1 opcode=233`、`EXPERT_MACHINE_CREATED role=8 ...`。

### 剩余风险

- 218 的客户端对象创建依赖 `0x36dfa0(uniqueId)` 找到本地角色；我们填的是
  `role.Id & 0xffff`（客户端按 `unique_id=role.Id` 认自己，见既有 memory）。
- 摆摊/分解机的商店交易六件套（238/239/240/244/245/246）与制作 242 仍未实现；
  本次只打通“放置分解机”这一条。

---

## 追加（同日二）：摆摊成功后的分解请求 —— op250 放置 / op251 分解

### 实测出来的真实请求

- **op250 = 点击地面放置分解机**（客户端发送函数 `0xDB4D30`）：body 为
  `{长度前缀 1 字节 string, int, short x, short y}`，实例值
  `01000000 01 01000000 df02 2401 000000`。此前的“一直在加载”就是服务端没有回 op250。
  服务端现在回：notify 218（机器信息）+ type1/250 `{1}` ACK，加载立即结束。
- **op251 = 分解机分解物品**（参照 `CMD 239 REQUEST_DISJOINT_ITEM`）：body 为
  `short machineUid, short slot, byte backpackList`，实例值 `0800 0900 00`（uid=8,
  slot=9, list=0），与参照 `Dispatcher_RequestDisjointItem::process` 的
  `m_field0e/m_field10/m_field12` 完全一致。
  奖励事务复用 op28 的 `Offline70DisjointFG92.Open`；回包是参照 `_disjoint` 的共享结果体
  `{1, slot, list, count, [newSlot(short), itemId(uint), quantity(uint)]...}`，只是发在
  **op251**（参照把 cmd 参数原样作为回包头，NPC 是 28、分解机是 239→本客户端 251）。

### 服务端改动（Expert-Machine-FG183.cs）

- op250：解析 x/y，校验分解师，发 218 + type1/250。
- op251：解析 uid/slot/list，校验 `uid == role.Id`、list=0、slot 3..40；用
  `session+"-machine-disjoint-"+sequence` 做幂等事务；成功发 type1/251 结果体，
  失败发 `{0,0x13}`；成功时 `Sync()+CollectionChanged`。
- 两个 op 都进 `ExpertMachineOp`，在 `UNIMPLEMENTED` 之前拦截。

### 隔离实测

```
SEND op250 body=010000000101000000bc024001000000
SEND op251 body=08000900000000000000000000000000
NOTIFY218 ... x=700 y=320
ACK250  plain=01000000...
ACK251  plain=01090000017700dd0b00000c00000000
EXPERT_MACHINE_DISJOINT_COMMITTED slot=9 gains=1
```

`ACK251` 解出：success=1 / slot=9 / list=0 / count=1 / newSlot=0x77(119) /
itemId=0x0bdd(3037 无色小晶块) / quantity=12。与 NPC 分解同一条事务和同一套掉落表。

### 线上边界

- 分解机对象不持久化：服务重启/重登后机器消失，需要重新放（op250 已通）。
- 商店交易六件套（238/239/240/244/245/246）与制作 242 仍未实现；op251 只覆盖
  “用分解机分解自己的装备”这条。

---

## 追加（同日三）：分解后耐久度错乱（8084…）

### 根因

参照 `CDisjointer::complete_disjoint`（`ExpertJobLink4/complete_disjoint.inc`）在掉落行之后还会
追加两个 int：

```
packet.put_int(requestUser->getCurCharacMoney());
packet.put_int(get_disjoint_machine_endurance());
```

我们的 op251 回包只发了 `Offline70DisjointFG92.Body` 的
`{1, slot, list, count, [newSlot(short), itemId(uint), quantity(uint)]…}`，没有这两个尾部字段。
客户端 op251 处理器按参照格式继续读，读到了包外/填充数据，把
`[char+0x2d4]` 写成了 `0x30303030`（四个字符 `'0'`），机器 UI 把它当十进制显示成
“8084…”。

### 修复

`Expert-Machine-FG183.cs`：

- 新增会话字段 `expertEndurance=300`；op250 放置新机器时重置为 300
  （`character/expertjob/disjointer.exj [endurance initial value]`）。
- op251 成功时按 `[endurance reduce] 1 3` 随机扣 1..3 点，并在结果体末尾追加
  `money(int)` + `remainingEndurance(int)`，与参照 `complete_disjoint` 逐字段同序。
- 失败包仍是 `{0,0x13}`（参照 catch 分支不带尾部）。

### 实测

隔离实例（角色 8 的 slot 11 装备）：

```
ACK251 plain=010b0000017700dd0b000014000000eca3010029010000...
             1  slot=11 list=0 count=1 newSlot=119 item=3037 qty=20 money=107500 endurance=297
```

线上同一会话连续分解两件：

```
12:49:38 EXPERT_MACHINE_PLACED ... endurance=300
12:49:42 EXPERT_MACHINE_DISJOINT_COMMITTED slot=11 ... endurance=298 used=2
12:49:46 EXPERT_MACHINE_DISJOINT_COMMITTED slot=12 ... endurance=296 used=2
```

客户端内存 `char+0x2d4` 同步为 `0x00000128`（296），不再是 `0x30303030`。

### 边界

耐久目前是**会话级**：放置新机器/重登时按 217 的初值回到 300，没有写进经济存档；
要做跨登录持久化需要给 `Offline70EconomyState` 加字段并迁移旧 XML，另开一项。

---

## 追加（同日四）：副职业经验与分解机耐久的持久化

### 经验

- 参照 `CUser/make_basic_info.inc:249-250` 在 op2 basic info 里写
  `put_byte(GetCurCharacExpertJobType())` + `put_int(GetCurCharacExpertJobExp())`；
  本 build 对应的字段就是 `[char+0x2a0]`（类型）和 `[char+0x2a4]`（经验）。
- 217 的 type-3 body 扩展为 `giveup, type, grade, endurance, expertExp`；登录和分解后刷新
  都带经验。
- 客户端补丁 cave 在类型 3 时从包游标 `0x1964fb8+8`（grade/endurance 之后）读经验写
  `[char+0x2a4]`；并加 `cmp edx,2054 / ja` 守卫，异常值不再写进 UI。
- 经济存档新增 `ExpertJobExpFG183`（默认 0，上限 2054），`Validate` 与 Commit 读回都比对。
- 分解获得 `disjointer.exj [gain exp] 0 1` 的 0..1 经验，事务 receipt
  `session-expert-exp-<seq>` 幂等。隔离实测：exp 1 → 分解 +1 → exp 2，217 刷新
  `0003010000002a01000002000000`（endurance=298, exp=2）。

### 耐久

- 经济存档新增 `ExpertJobEnduranceFG183`（`DefaultValue=300`，范围 0..300）。
- 登录 217 发存档耐久，不再每次重置 300；op250 放新机器也不重置
  （参照 `CDisjointer::complete_disjoint` 扣的是机主存档耐久）。
- 分解时 `[endurance reduce] 1 3` 扣 1..3，和 `ExpertJobExpFG183` 在同一个
  `Offline70Economy.Apply` 事务里落盘。隔离实测：300 → 298，重连登录 217
  仍发 298，XML 有 `<ExpertJobEnduranceFG183>298</ExpertJobEnduranceFG183>`。

### 仍未做

- `GetExpertJobAddtionalExp`（装备自带的额外副职业经验）未接，所以单次经验严格按
  `[gain exp] 0 1` 随机 0/1；要更快的成长需要另接 `.equ` 的 additional exp。
- 分解机的商店交易/制作仍未做。

### 线上实测（2026-10-26 13:06，角色 8）

```
13:06:21 EXPERT_MACHINE_PLACED ... endurance=300
13:06:25 FRAME opcode=251 -> EXPERT_MACHINE_DISJOINT_COMMITTED slot=9 ... endurance=299 used=1 expGained=1 expertExp=1
13:06:25 SENT expert-job-info type=0 opcode=217 bytes=31
13:06:33 EXPERT_MACHINE_PLACED ... endurance=299   <- 重放机器没有重置
13:06:47 EXPERT_MACHINE_PLACED ... endurance=299
```

客户端内存：`+2A4 expertExp=1`、`+2D4 endurance=0x12B(299)`；
`economy-account1-r8-FG.xml`：`<ExpertJobExpFG183>1</ExpertJobExpFG183>`、
`<ExpertJobEnduranceFG183>299</ExpertJobEnduranceFG183>`。
“Lv.9 (-1238059468 / 2054)” 的负值不再出现（cave 的 0..2054 守卫 + 217 始终带经验字段）。

---

## 追加（同日五）：副职业经验的真正字段 +0xE81（+0x2A4 不是经验）

### 之前为什么会乱码

- `+0x2A4` 是 op2 basic-info 解析出来的 **容器/字符串指针**（`0x4154A0` 还把它当
  `std::string` 读），不是经验整数。把经验直接写 `+0x2A4` 会把容器写坏，
  UI 就出现 `Lv.9 (-1238059468 / 2054)` 这类负数。
- 参照 `CUserCharacInfo.h:475`：`int m_expertJobExp; // +0xe81`（`m_expertJobType` 在
  `+0xe7d`）。本 build 的 `0x76e1c0` 返回的角色对象同样把经验放在 **+0xE81**；
  `217` 处理器的 `+0x2A0/+0x2CC/+0x2D0/+0x2D4/+0x2D8` 是副职业扩展数据，不含经验。

### 修复

- 客户端补丁 cave：type=3 时从 217 包游标 `+8`（grade/endurance 之后）读经验，
  `cmp 2054 / ja` 守卫后写 **`[char+0xE81]`**；不再写 `+0x2A4`。
- 217 type-3 body 仍然是 `giveup,type,grade,endurance,expertExp`；登录、分解、修理后
  都发。
- 经验获得从参照 `[gain exp] 0 1` 的 0/1 改成 **1..2**（单机向；参照还会加装备自带的
  additional exp，本引擎未接，避免 50% 空转）。
- 经济存档 `ExpertJobExpFG183` / `ExpertJobEnduranceFG183` 继续持久化。

### 线上实测（13:23:43，角色 8）

```
EXPERT_MACHINE_DISJOINT_COMMITTED slot=9 gains=2 ... endurance=297 used=3 expGained=2 expertExp=3
客户端 +E81 expertExp = 00000003
economy-account1-r8-FG.xml: <ExpertJobExpFG183>3</ExpertJobExpFG183>
```

`+0x2A4` 保留为 op2 容器字段，不再写入。

### 诺顿修理（同日）

客户端 **op252** = 参照 CMD 240 `REPAIR_DISJOINT_MACHINE`。服务端校验分解师、扣
`[endurance repair cost]` 1 级价 **10000**、耐久回 300、落盘，回 `type1/252
{1, money, endurance}` 并补 217。隔离实测 `EXPERT_MACHINE_REPAIRED money=97500 endurance=300`。
