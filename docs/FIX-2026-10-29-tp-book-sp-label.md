# FIX 2026-10-29：TP 技能书用完显示成 SP+5（op32 回包 type 字节）

状态：已编译部署（check_repo 430/430、build --smoke、20:07 双服重启）；需重登客户端复验。

## 现场

- 19:54 玩家连用 7 本 TP+5 技能书（1205）。服务端存档正确：`TP_BOOK_COMMITTED item=1205 grantTp=5 bonusTp=5…35`，
  经济存档 `BonusTp=35 / BonusSp=0`，随后还发了 `skills19` 刷新（`SENT tp-book-skills19`）。
- 但客户端画面把这 5 点记到了 **SP** 上（每本 +5，本局累计 +35），特性技能页的 TP 不涨。

## 根因

op32（客户端「使用增加状态物品」请求，服务端 `StatusItemFG83`）的成功回包体是 12 字节：

```
{ result=1, slot(u16), type(u8), value(u32), 0(u16), 0(u16) }
```

ORIG `CUser::increase_status`（rednf `CUserOnlyOrig/increase_status.inc`，原始地址 0x0866xxxx）对所有点数书都用这同一形，
**只有 `type` 字节区分钱包**：

| 物品 | (type, value) |
|---|---|
| 0x407 = 1031 SP+5 书 | type **0**（SP） |
| 0x40e = 1038 SP+20 书 | type **0** |
| 0x4b4 = 1204 TP+1 书 | type **0x11**（SFP/TP） |
| 0x4b5 = 1205 TP+5 书 | type **0x11** |

客户端就是按这个字节决定提示文案与本地加点对象；服务端 `SkillBookReply` 之前把第 3 字节写死 0，
于是 TP 书被客户端当作 SP 书回执 → 显示/本地缓存 SP+5。

（同一 switch 里 0x11 分支体也写得很清楚：`gain_sfp(value); history_log_sfp(...)`，回包仍是那 12 字节；
0x12 = SFP 技能树重建、0x17 = 一日遗忘河，可见 type 是物品效果的枚举，不是错误码。）

## 修复

- `Economy-Protocol-FG.cs`：`SkillBookReply(slot, amount, type, success, keys)`，把 `type` 写入 b[3]。
- `Batch-Service-FG.cs::UseSkillBook`：SP 书传 type 0（行为不变）。
- `Skill-Reset-Service-FG83.cs::UseTpBookFG214`：1204/1205 传 type **0x11**；顺手更正注释里
  「ORIG 只在任务结算折算、不认 op32 使用」的旧判断——`increase_status` 明确有 0x4b4/0x4b5 两个 case。

服务端钱包逻辑未动（仍只加 `BonusTp`），op19 尾部 `[BonusTp, 0]` 也未改。

## 验收

- 离线：新 driver 断言加入 `TpShopQuest-FG215`——TP 书回包解密后 `b[3]==0x11`、SP 书 `b[3]==0`；整套 ALL PASS。
- 实机：重登后（本会话被写坏的本地 SP 显示随重登复位）再用 1205，应显示 TP +5、SP 不变；
  用 1031/1038 仍显示 SP 加点。

## 第二轮（同日 20:2x）：没有提示 + 技能窗被关

实机复验反馈两点：TP 数值正确上涨了，但①没有像 SP 那样弹系统提示；②每用一本 TP 书，已打开的技能窗会被关掉。

- **提示**：CN70 客户端只有 `type=0`（SP）有原生弹窗，`type=0x11` 没有对应文案（全 PVF `.str` 里也没有 TP 加点的消息模板）。
  改为服务端补一条 op12 系统行（`make_packet_chat_msg` 的 type0 体，uid=收包角色 unique_id，依据 FG363 实机结论），
  文本 `TP +5（当前剩余 TP 35）`。SP 书路径不动（原生弹窗本来就正常）。
- **技能窗被关**：`CUser::increase_status` 的 0x4b4/0x4b5 分支**只发那 12 字节回包就 return**——全函数
  只有 `case 0x12`（SFP 技能树重建）会 `send_skill_info()`。我们此前在点数书后多发了一条 op19 全量技能信息，
  客户端把这当成"技能数据整体重载"，开着的技能窗随之拆除。已删除 `UseTpBookFG214` 的成功/重放两条 op19，
  钱包刷新改由客户端自己按回包里的 `value` 记账（这正是原版不发 op19 的原因）。
- 若实机仍见技能窗被关：那就不是 op19 而是客户端 0x11 分支自身的刷新行为，只能走客户端补丁（未做）。
