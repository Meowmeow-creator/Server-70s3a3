# 功能：进入游戏时播报服务端版本（v0.4，FG353）

日期：2026-10-27。需求：进入游戏时在聊天窗播报当前服务端版本 `v0.4`，以后出现差异时
凭这一行就能确认对方跑的是哪一版服务端。

## 协议依据

服务端自有系统消息（非玩家聊天）在 rednf 里是 `GameWorld::notifySystemMessage`
（0x08197d00），它调用 `GameWorld::make_packet_chat_msg(packet, user, msg, len, type=0,
notify=ENUM_NOTIPACKET_MESSAGE)`：

- 包头：`put_header(0, 0xc)` —— [0]=0（notify 通道），[1..2]=0xc = 12
  （`g_szNotiPacketName[12] = ENUM_NOTIPACKET_MESSAGE`）。[3..6] 由 `finalize` 写总长，
  与批次服务 `Packet(type, opcode, ...)` 的布局一致。
- 体：`{type u8, uid u16, serverGroup u8, len i32, text bytes}`；`user==0` 时 uid/group 写 0。
  `type=0` 是"无发送者的系统行"，客户端按系统提示渲染，不需要角色对象。
- 加密：本仓库批次协议按 `index = opcode % 14` 选密码，op12 → index 12（MULTI2 8 字节块），
  与客户端 op12 handler 一致。
- 客户端侧：接收分发表文件偏移 `0x390C04 + 12*4 = 0x390C34` → 运行地址 `0x78BB08`
  （RVA `0x38BB08`）trampoline：`push 0x188; call 0xcbccd0` 分配包对象、子命令 9，
  `call 0x3782b0` 入队（`call 0x10a40` 参数含 0xc），即 op12 有独立 handler。

## 改动

- 新增 `src/Tools/VmTestKit/Server-Version-Notice-FG353.cs`：
  - `Offline70ServerVersionNoticeFG353.Version = "v0.4"`（唯一版本常量，升级只改这一行）；
  - `Text = "服务端版本 " + Version`；`Body` 按上面的布局写 GBK（CP936，严格 fallback）；
  - `System(text, keys)` = `BatchPacketFG(0, 12, Body, keys, false)`。
- `src/Tools/VmTestKit/Batch-Service-FG.cs`：`World()` 末尾（settings/options 推送之后、
  `WORLD_RESTORED` 之前）调用 `ServerVersionNoticeFG353()`，即每次选角进世界都发一次，
  证据日志记 `SENT server-version type=0 opcode=12 ...` 与 `SERVER_VERSION_NOTICE version=v0.4`。

## 验证（2026-10-27）

- `python scripts/check_repo.py`：通过（176 稳定源）。
- `python scripts/build.py --smoke`：176 源编译通过，双 exe 无参冒烟 exit 2。
- 离线字节检查 `local-run/driver/ServerVersionNotice-FG353.cs`（纯构造+解密，无 socket）：
  PASS —— 39 字节整包、header[0]=0、opcode=12、总长=39、体 24 字节（8 对齐）、
  type=0 / uid=0 / group=0 / len=15、GBK 文本 = "服务端版本 v0.4"、填充为 0。
- 端到端检查 `local-run/driver/ServerVersionLive-FG353.cs`：连隔离频道服
  （`--test-port 17002`，store 为 `local-run/scratch-fg353/` 下的可丢弃副本），
  登录→名册→op4 选槽后持续读包 28 帧，回落 op12 并原位解密：
  `type=0 uid=0 group=0 len=15 text=服务端版本 v0.4 padsZero=True` → PASS。
- 部署：停旧服 → `build.py --smoke` → `local-run/restart-detached.ps1` 重启，
  7002/17001 各 1 实例监听（会话内实测）。

## 边界

- 只发一行系统消息，不写存档、不改任何状态；重复进世界（含换角色、回选角再进）各发一次。
- 未做实机截图：客户端聊天窗的最终绘制（颜色/页签）未在本轮验收；若字节已到而界面不显示，
  问题在客户端聊天窗的过滤/页签，不在服务端包体。
- `v0.4` 是手工常量；以后发版只改 `Server-Version-Notice-FG353.cs` 的 `Version`。

## 2026-10-29 更新：版本 v0.5 + 高亮聊天类型

- `Version` 改为 `v0.5`，聊天窗显示 `服务端版本 v0.5`。
- 聊天类型从 0（无发送者系统行）改为 `ChatType = 11 (0xb)`。依据：客户端
  `etc/chatsystem.etc`（PVF，pvfUtility 可直读）把聊天类型按月分页，第二页整组是
  `11 12 13 14 15 38`，与红字排开；rednf `GameWorld::send_chat_msg` 的
  `case 0xb/0xc/0xe` 正是 MegaPhone（喇叭）类 —— 客户端按喇叭样式渲染，即需求要的"高亮"。
  `case 2`=队伍、`case 3/0x22`=附近、`case 6`=公会、`case 8`=会员，可作类型语义参照。
- 非 0 类型会按 uid 解引用发送者演员（2026-10-27 实测 `type=1 + uid=0` 直接把客户端打死），
  因此 `Body(uid,text)` / `System(text,uid,keys)` 现在写入接收角色的 unique_id（`role.Id`），
  调用点 `World()` 里本就持有 `role`。
- 验证：`check_repo.py` 430 文件 / 201 稳定源通过；`build.py --smoke` 201 源通过；
  离线 `local-run/driver/ServerVersionNotice-FG353.cs` PASS（39 字节 / type=11 / uid=12 /
  GBK 文本 15 字节 / 零填充）；隔离频道（`--test-port 17002` + 可丢弃 store 副本）跑
  `ServerVersionLive-FG353.exe`：`frames=31 op12Found=True type=11 uid=1 group=0 len=15
  text=服务端版本 v0.5 padsZero=True` → PASS，隔离实例日志同见
  `SERVER_VERSION_NOTICE version=v0.5 type=11 uid=1`。
- 部署：停双服 → `build.py --smoke` → `local-run/restart-detached.ps1`，7002/17001 各 1 实例。
- 边界：本轮仍未截图验收颜色 —— 能证明的是包已按喇叭类型发给正确的发送者；客户端若把它
  归到"喇叭"分页而非综合页，把 `ChatType` 改回 0（一行回滚）即可。
