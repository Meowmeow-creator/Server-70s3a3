# FIX 2026-10-27 · 分诊批次三（物品/背包状态/客户端协议）——FG350

任务书：`local-run/coord/TRIAGE-2026-10-26-three-batches.md` 批次三（条目 2、3、9、10）。
本文覆盖全部四条的取证结论、修复内容、离线验证与游戏内验收清单。

## 取证工具（本次新增，可复用）

`local-run/driver/DecryptCapture-FG349(.cs/.exe/.rsp)` —— 用服务端同款 per-opcode 密码
（index=opcode%14，恒等密钥表 0..333）离线解密 `channel-evidence.log.request-*.bin` 抓包体。
sent 包是整包存储：先按包头 [3..6] 的 size 剥 15 字节头再解密体。
以后任何"日志里有 REJECTED/UNIMPLEMENTED 但不知道客户端发了什么"的问题，先用它重放抓包，不要猜。

## #9 迷妄之塔进入后全交互卡死 —— 已修（三处）

**分诊假设被证据推翻**：
- op142/op143 字节与原生逐字节一致（`handleStartGame` 的 0x8e/0x8f 布局；伪造包与抓包
  `00114-tower-info142.bin` 完全相同；143 体 = stage1/seed/map 31001/3 怪 {0,100,idx4,lv30}…，符合
  `CStage::makeStagePacket`）。
- **op541/op543 不是塔专属**：自 10-15 起所有会话都有（今天 200 次，城内正常游玩时段也在发）。
  解密后是客户端遥测/TP 类报告（541 恒 56B 结构化上报；543 带哈希负载 104/136/152B 增长；
  另有 157/537 小包同族）。fire-and-forget，无需应答，保持 UNIMPLEMENTED 不影响任何功能。
  （17 字节内发现：op157 体=40,1,0,0；op537 体=时间戳+计数。）

**真正根因**（抓包时间线 23:09:13.55→13.868）：
1. 客户端选图对话框只认**普通进场突发 16(type1 ack)/3/28** 才关闭；FG348 只回 142/143，
   对话框不关，客户端 14ms 后自动重发 op16（seq 36/37 同毫秒双发=点按重试，共 5 次）。
2. 服务端把重发按 `Dungeon entry outside inactive corridor` 拒绝并回 type1 {0,0x15} 毒 ack，
   覆盖了已接受的进场状态 ⇒ 客户端永远停在选图界面，全部交互冻结。
3. 用户点取消发 op146，又被 selection 守卫拒绝 ⇒ 连退路都没有。

**修复**：
- `Tower-Run-FG348.cs` `TowerBeginFG348`：先发普通进场突发 {16,3,28}，再发 142/143。
- `Batch-Service-FG.cs` op16：塔进行中的重发改**静默忽略**（`TOWER_ENTRY_DUPLICATE_IGNORED`）。
- `Batch-Service-FG.cs` op146：塔进行中改走 `TowerReturnFG348()`（弃局回城、不结算）。

**验收**：进迷惘之塔→应加载 1F 并出现怪物（日志 `TOWER_ENTRY_COMMITTED` 后跟
`tower-entry16/3/28`、`tower-info142`、`tower-stage143`，客户端跟发 op40）；强制退出路径：
进塔后点取消/返回 → 应回城且日志有 `TOWER_SELECTION_RETURN`。op40→`TOWER_LOADED`。

## #3 装备无法丢弃/分解、整理无效 —— 已修（op18 解析两处）

**分诊假设被证据推翻**：四个副本状态守卫都没问题——归档日志里 op18/20/28/50 的
REJECTED 只有 **2 条 `TOWN_DELETE_REJECTED Delete18 safe flag`**（22:49:44、22:50:23）。
解密抓包：op18 体 = list0 + count1 + 行{Reason=2, Slot=119, Item=3037, Qty=1} +
4 字节校验和 + **确认旗标=1**。旧解析要求行尾旗标=0（极性反了）且 Reason 只收 1。
原生依据：`MSG_DELETE_ITEM`（+0x4bf 尾）与 `DisPatcher_DeleteItem::send`（send_9.inc）——
成功 ACK={1, itemSpace, count, 每行 slot u16/count u32/reason u16}，reason 2/3/4 是
客户端的方块统计删除口径（process_18 delKind），1..4 全部是删除。

**修复**：`Item-Drop-Protocol-FG.cs` Parse —— 行尾确认旗标收 0/1；Reason 放宽到 1..4；
填充零校验、重复槽校验、ACK 布局不变。

**验收**：城镇背包拖物品到地面/删除按钮（含确认弹窗）→ 物品消失、日志
`TOWN_DELETE_COMMITTED`；应仍拒绝：装备栏直接删除（invenKind=2 不经 op18）、
数量 0、填充非零。若"整理/分解"仍复现，抓 op20/op28 的 REJECTED 日志再查
（本批日志中两者零拒绝记录）。

## #2 压缩的胶囊无法开启 —— 已修（新增 FG350）

**证据链**：任务 5/7（id 32，奖励 7036）→ 玩家右键用 7036 → 被
`Economy-State-FG.Use` 门禁拒（`Item use effect has not been implemented`）→
6/7（id 33，[seeking] 4036×1）卡死。
PVF（`stackable/ect/capsule_compressed.stk`）：`[stackable type] [legacy]`、
`[int data] 3116 4036 50000 3117 5000`。
原生语义（rednf 权威）：`[legacy]`=type6 → `createLotteryInfo`（0x89babc5）：
**第 1 个 int = 未中签默认物品**，其后 (物品,权重) 对、数量全 1；
`CStackableItem::get_lotto_item`（0x850ebfc）：randInt(100000) 累积权重，未中签给默认。
⇒ 开启结果：**50% 微型芯片 4036**（6/7 交付物）、**5% 下级元素结晶 3117**、
**45% 黑硬物质 3116**（顺带接上了 PROGRESS_HANDOVER 缺的黑硬物质来源）。

**修复**：
- 新文件 `src/Tools/VmTestKit/Capsule-Lottery-FG350.cs`：注册表 + 按 receipt 确定性 roll
  （SHA256(receipt:attempt)，同 Booster-Rewards-FG47.Plan 的重放契约）。
- `Economy-State-FG.cs`：新增 `UseCapsuleLotteryFG350`（消耗与产出同一原子事务）；
  普通 `Use` 明确拒绝注册胶囊（防止无产出消耗）。
- `Batch-Service-FG.cs` op47：注册胶囊路由到专用服务，成功后发
  `capsule-open-assets`（InventoryChangeReply）让新物品上屏。

**验收**：用压缩的胶囊 → 客户端扣 1 个、背包出现 4036/3117/3116 之一，日志
`ITEM_USED id=7036 capsule=True` + `capsule-open-assets`；连续开两个分布应近似 5:0.5:4.5。

## #10 称号佩戴后无效果 —— 已修（刷新缺失；显示层待实测）

**证据**：日志 45 次 op420/421 全部 `TITLE_MOVE_COMMITTED`（佩戴槽=3:11，26625/26626）——
服务端记账一直是对的，但成功后**从不重发外观/属性包**，客户端的已穿戴行与属性块保持旧值。
op19 的普通装备移动早有同款刷新（`if(!dungeon.Active&&(SourceList==3||TargetList==3))Appearance()`）。

**修复**：`TitleBook-Service-FG157.cs` `TitleMoveFG157` 成功后 `Sync()+Appearance()+
AdditionReplyFG129()`（本 handler 限城镇，无副本内延迟刷新问题）。

**验收**：称号簿佩戴任意称号 → 头顶称号文本出现、角色面板出现称号属性；
卸下同理。**若佩戴刷新后头顶/面板仍不显示**，则缺的是客户端侧渲染链（称号 PVF 选项
或登录时"当前佩戴"状态包），属客户端补丁线——届时按分诊原案抓佩戴前后 op37 属性包对比定论。

## 验证与部署记录

- `scripts/check_repo.py` PASS（428 文件 / 173 稳定源）。
- 新离线 driver `local-run/driver/TriageBatch3Check-FG350`：**28/28 PASS**
  （重放两条真实 op18 抓包解析成功、守卫负例、胶囊 roll 确定性/分布 50.3/4.8/44.9、
  scratch 档原子开启+重放不双发+普通 Use 拒绝、ACK 布局对齐原生 send_9）。
- 既有 `TowerFloors-FG348` 回归 **56/56 PASS**。
- `scripts/build.py --smoke` rc0（173 源）；服务已停→编→`restart-detached.ps1` 重启
  （channel 7580/7002，entrance 17260/17001）。

## 涉及文件（批次三归属边界内）

| 文件 | 改动 |
|---|---|
| `src/Tools/VmTestKit/Tower-Run-FG348.cs` | TowerBeginFG348 先发 16/3/28 |
| `src/Tools/VmTestKit/Batch-Service-FG.cs` | op16 塔内重发忽略；op146 塔内退城；op47 胶囊路由（均为批次三分发区） |
| `src/Tools/VmTestKit/Item-Drop-Protocol-FG.cs` | op18 确认旗标极性 + Reason 1..4 |
| `src/Tools/VmTestKit/Capsule-Lottery-FG350.cs` | 新增（胶囊开启表） |
| `src/Tools/VmTestKit/Economy-State-FG.cs` | UseCapsuleLotteryFG350 + 普通 Use 拒绝胶囊 |
| `src/Tools/VmTestKit/TitleBook-Service-FG157.cs` | 佩戴/卸下后刷新外观+属性 |
| `local-run/driver/DecryptCapture-FG349.*`、`TriageBatch3Check-FG350.*` | 取证/验证工具 |
