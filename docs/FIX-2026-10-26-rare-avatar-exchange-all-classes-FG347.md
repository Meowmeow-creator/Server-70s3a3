# FIX 2026-10-26 — 全职业商城天空套/稀有装扮兑换（FG347）

## 症状

男神枪手在商城稀有装扮页购买天空套（混沌末世天劫）任意部位，客户端弹购买失败提示，
服务端日志先前为 `CASH_PURCHASE_REJECTED Unimplemented cash commodity, payment or option`
→ 回失败码 `0x13`。女枪手（job 5）与男枪手（job 2）是两套不同数据，本修复针对 job 2。

## 根因

`Avatar-Cash-FG210.Resolve` 只把 `563000-563007`（男法“混沌不死邪魔”）当成 payment=1 的
兑换族；`Feedback-Cash-FG108` 只认 `166024-166031`（鬼剑士）。其余职业的兑换族虽然完整存在于
`etc/cerashop.etc [avatar]` 和 `avatar-cash-catalog.xml`，但 payment=1 请求会一路落空：

```
Offline70CashPurchaseFG45.Resolve
  -> Offline70FeedbackCashFG108.Resolve
       -> Offline70AvatarCashFG210.Resolve  (payment=1 且 commodity 不在 ExchangeCommodities -> null)
       -> FG272 / FG152 / FG129             (null)
       -> FG108 Avatars                     (只有 166024-166031)
  -> r.Payment!=0 -> throw "Unimplemented cash commodity, payment or option"
```

商城目录本身没有问题：男枪八件都在目录里，metadata 也都是 Kind=3、`JobMask=4`（job 2 位）、
部位槽位正确。

## 改动

`src/Tools/VmTestKit/Avatar-Cash-FG210.cs`：把 `ExchangeCommodities` 从男法 8 条扩为全职业
兑换族 48 条：

| 职业 | commodity | 部位道具（按目录顺序） |
| --- | --- | --- |
| 男格斗 | 216024-216031 | 43244/43664/43998/44411/44894/45287/45591/46069 |
| 男枪手 | 266024-266031 | 47249/47659/47996/48409/48883/49277/49588/50056 |
| 女法 | 316024-316031 | 51241/51622/51969/52406/52847/53239/53589/54035 |
| 圣职者 | 366024-366031 | 55185/55556/55942/56354/56776/57167/57541/57968 |
| 盗贼 | 466016-466023 | 1720085/1730062/1740057/1750071/1760064/1770061/1780055/1790064 |
| 男法 | 563000-563007 | 原 FG211 八件保持不变 |

鬼剑士 `166024-166031` 故意不加：它保持 FG108 的 `Special=1`、只收兑换券 2681594 的老路径，
避免改变既有字节语义。

兑换支付顺序不变：持有稀有装扮兑换券 2681594 → 消耗券；否则持有星光独角兽 63092 → 消耗 1 只；
两者都没有 → 仍然发放（这两个币种在本服务端没有获取线路，属既有 FG211 语义）。
`.equ` 的点券价（1980/2640/6000）仍可通过 payment=0 购买。

## 验收

离线 driver `local-run/scratch/cash67/GunnerExchangeCheck-FG347.cs`（17/17 PASS）：

- 266024-266031 八件全部解析为 Cost=0、Special=9、Voucher=63092 的兑换 offer，并通过 Valid；
- 八件 metadata 为 Kind=3、JobMask=4、槽位 0/1/2/6/3/4/7/5；
- 持有 1 张兑换券购买 266028：券扣 1、永久上衣 48883 入装扮栏、点券不变；
- 仅持独角兽：扣 1 只独角兽；
- 法师角色兑换男枪件被拒且券不丢；
- 能力选项越界、非永久期效、未知 payment 均被拒；
- payment=0 的 1980 永久帽子路径仍可用；
- 男法 563007 与鬼剑士 166024 老链路回归通过；
- 格斗家/女法/圣职者/盗贼兑换族解析与职业掩码正确；
- 五个非男法家族的兑换券全链路购买（券扣减、永久装扮入栏）逐项 PASS。

实机在线验收（2026-10-26 16:20，服务已部署）：

- GM 先把角色 13 `你好啊22`（job 2，Lv.70）绑为目标并发放兑换券 2681594 ×1；
- `LiveBuyGunnerFG347.exe` 用真实协议登录 → op4 选角（slot 7）→ op67
  `payment=1 period=3 option=0 commodity=266028`，回包 `success=1`；
- 服务端日志：
  `CASH_PURCHASE_COMMITTED commodity=266028 item=48883 rewardRows=1 cost=0 voucher=63092 cartCount=1 offlineCash=165904 seq=100`；
- GM 快照：`List=1 Slot=0 ItemId=48883 Quantity=1`，兑换券 2681594 已消失，点券仍为 165904；
- `LiveScanGunnerFG347.exe` 在新登录的 op13 装扮栏包内解出 `48883`（offset 5），且无兑换券。

全职业实机矩阵（2026-10-26 16:20-16:52，全部走真实 op4 选角 + op67 购买）：

| 职业（job） | commodity | 购买件 | 结果 | 路径 | 存档 |
| --- | --- | --- | --- | --- | --- |
| 鬼剑士 (0) | 166028 | 40890 混沌鬼影噬魂战甲 | success=1，券 2681594 扣除 | FG108 老路径 | 主 store roleId 12 |
| 格斗家 (1) | 216028 | 44894 混沌极武碎空胸甲 | success=1，券 2681594 扣除 | FG210 新兑换族 | 隔离 store roleId 2 |
| 神枪手 (2) | 266028 | 48883 混沌末世天劫战甲 | success=1，券 2681594 扣除 | FG210 新兑换族 | 主 store roleId 13 |
| 魔法师 (3) | 316028 | 52847 混沌幻域魔灵胸甲 | success=1，券 2681594 扣除 | FG210 新兑换族 | 主 store roleId 9 |
| 圣职者 (4) | 366028 | 56776 混沌圣灵审判胸甲 | success=1，券 2681594 扣除 | FG210 新兑换族 | 隔离 store roleId 3 |
| 暗夜使者 (6) | 466020 | 1760064 混沌血影寂舞胸甲 | success=1，券 2681594 扣除 | FG210 新兑换族 | 隔离 store roleId 4 |

男法 (8, 563xxx) 按用户要求不重复测试（FG211 已验收，本轮离线回归仍 PASS）。
女枪手 (5) 与男格斗家 (7) 在本 PVF 的 cerashop `[avatar]` 里没有稀有装扮行，商城无对应天空套可买，不适用。

隔离测试 store：`local-run/isolated-class-test/DNF70-batch-test-FG`（独立 17003 端口频道服，
`LiveCreateRolesFG347.exe` 用 op5 现场建 cf1/pr1/th1 三个角色，测完 GM `shutdown` 关闭；
主 store 的角色未被创建/删除）。

## 部署

- `python scripts/check_repo.py` → 428 files / 171 stable sources PASS；
- 停服 → `python scripts/build.py --smoke`（171 sources）→ `local-run/one-click.ps1` 拉起双服；
- 频道服 7002 / 入口服 17001 就绪。

## 相关文件

- 修复：`src/Tools/VmTestKit/Avatar-Cash-FG210.cs`
- 数据（未改）：`data/avatar-cash-catalog.xml`、`data/item-metadata.xml`
- 验收：`local-run/scratch/cash67/GunnerExchangeCheck-FG347.cs`、`LiveBuyGunnerFG347.cs`、
  `LiveScanGunnerFG347.cs`、`LiveCreateRolesFG347.cs`、`gm_call.py`、`sources-fg347.rsp`
- 隔离实测：`local-run/isolated-class-test/`（store、`channel.log`、`start-iso.ps1`）
