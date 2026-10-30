# FIX 2026-10-28 宠物装备无法丢弃（op18 物品空间 7）— FG367

玩家反馈（2026-10-28 20:21）：宠物窗口里右键丢弃宠物装备无反应。上一批 FG352 已声称修好
（`DeleteTown` 放行 Kind 8、限 list 7），但那条白名单实际是**不可达代码**：请求在更早的
`Offline70DeleteProtocolFG.Parse` 就被拒了。

## 根因（抓包直证）

频道服证据日志 `local-run/logs/channel-evidence.log` 20:21:38：

```
FRAME opcode=18 bytes=45 seq=912
SENT town-delete-failed type=1 opcode=18 bytes=31
TOWN_DELETE_REJECTED Delete18 list/count bounds
```

证据日志同时把 32 字节密文体落盘（`...request-1901-18-2da87db9...bin`）。用会话固定密钥
（keys[i]=i、index4 金密码 Twofish）解密：

```
07 01 | 01 00 | 8C 00 | 27 EB 29 00 | 01 00 00 00 | 56 CC 1E 00 | 00 ...
list=7  count=1  reason=1  slot=140  item=2747175  qty=1  checksum  confirm=0
```

即客户端**确实是用 op18 丢弃宠物装备**，只是把「宠物窗口」这个物品空间标成 **7**（红色/背包是 0）。
`Parse` 里写死了 `b[0]!=0` 就抛 `Delete18 list/count bounds`，所以：

- FG352 的 `Delete pet equipment window` 分支永远走不到；
- 服务端连「物品是否存在/什么 Kind」都没看到就回了失败包，客户端表现为没反应。

存档侧核对：`economy-account1-r8-FG.xml` 里 2747175 正在 `List=7 / Slot=140 / Quantity=1`，
`item-metadata.xml` 登记 Kind 8（宠物装备），与抓包完全一致。

同一晚 19:44–20:08 的 8 次 op18 失败是另一回事（list 0、reason 2、slot 119、item 3037，且当时
`dungeon.Active`），与本条无关。

## 修复

1. `Item-Drop-Protocol-FG.cs` `Offline70DeleteProtocolFG.Parse`：物品空间白名单从「只收 0」
   改为 **{0, 7}**；错误信息带上 `list=/count=/bytes=` 便于下次一眼定位。
2. 新增 `Town-Delete-FG367.cs` `Offline70TownDeleteFG367.Apply`，把 op18 的经济语义从
   `Batch-Service` 的内联匿名委托里提出来（原样搬移 + 收紧规则），删除行动点改为调用它：
   - list 0（背包）：Kind 1/2/4；Kind 4 仍要求槽位 3..40、数量 1；
   - list 7（宠物窗口）：**只收 Kind 8**，槽位 140..188、数量 1（宠物本体 0..139 不走这条路）；
   - 所有权/数量校验与「整批校验通过后才扣减」的原子性不变。
3. `TOWN_DELETE_COMMITTED` 日志加 `list=` 字段。

回包不变：仍按原生 `DisPatcher_DeleteItem::send`（rednf `send_9.inc`）布局
`{1, list, count, (slot u16, qty u32, reason u16)*}`。list 7 的删除成功后
`InventoryChangeReply` 天然为 null（它只差分 list 0），因此只发原生确认包，与原生服一致。

## 离线验收

`local-run/driver/TownDelete-FG367-Check.cs`（rsp 由 `make_fg367_rsp.py` 生成；csc v4.0.30319）：

```
[1] 抓包体解析：list 7 / 1 行 / {reason 1, slot 140, item 2747175, qty 1}
[2] 其它物品空间（5）与超长块仍被拒
[3] 同一字节流在 scratch 经济档案上：首次提交成功、list 7 slot 140 消失、落盘后仍消失、
    同序号重放不二次扣减
[4] 规则矩阵：背包消耗品扣减、背包装备槽 20 通过、槽 2 拒、宠物窗口放非 Kind 8 拒、
    槽 139 拒、数量 2 拒、批内坏行整批拒且好行不被改动、itemId 不符拒
[5] 回包字节：type1/op18、body {1,7,1}、行 {slot 140 u16, qty 1 u32, reason 1 u16}
checks=23 failures=0
```

## 部署

`check_repo.py`（428 文件 / 194 稳定源）→ 停服 → `build.py --smoke`（194 源）→
`restart-detached.ps1`：7002 / 17001 LISTENING，日志 `STORE_SWEEP_FG354` + `LISTENING` 正常。

## 实机待验

重开客户端后，在宠物窗口右键丢弃任意宠物装备：
- 预期物品立刻从宠物窗口消失，日志出现 `TOWN_DELETE_COMMITTED ... list=7`；
- 若仍失败，日志会给出 `Delete18 list/count bounds list=?` 或
  `TOWN_DELETE_REJECTED <规则名>`，可直接对照上面的规则矩阵定位。

已知未覆盖的一条支线：**已经穿在宠物身上的**宠物装备（存档里在 list 3 槽 22..25）如果客户端也允许
直接丢弃，它会发 list=3，当前解析层仍会拒。需要时再补「先卸下（刷新外观）再扣减」的清单，
本篇没有改动这条路径。
