# 修复：辅助装备/魔法石槽位解锁（[slot expansion]，FG345）

日期：2026-10-26。用户报告完成「穿戴辅助装备的资格 (2/2)」后槽位没有开启。

## 根因

1. 生成器把 `[reward type] [slot expansion]` 归进 `NOOP_REWARD_TYPES`，只把 Rewards 置空，
   服务端从未写入角色的扩展装备槽状态。
2. 客户端角色基础信息（op2 subtype1，`make_basic_info` type 1）在 87 字节属性块之后
   有一个 `m_exEquipSlotStat` 字节，离线服务一直发 0。
3. op36 任务完成 ACK 的 rewardType 0x16 分支应追加 `(newStat, slotMode)` 两字节，
   离线服务从未发过，因此完成瞬间客户端也没有任何更新。
4. 后续任务 676/677 的奖励 440197 / 450040（450114）在 item-metadata 里被登记成
   `EquipSlot=0`，即使槽位开启也无法穿上。

## 证据

- PVF：`n_quest/gent/nightassault/epic_60_nightassault_second_2.qst` →
  `[reward type] [slot expansion]`、`[reward int data] 20`；
  `n_quest/lufthafen/pirateonthetrain/epic_65_pirateonthetrain_magic_stone_2.qst` → `21`。
- rednf `CUserOnlyOrig/quest_reward.inc` 0x16 分支：mode 20 置 bit0、mode 21 置 bit1，
  ACK 追加 `GetCurExEquipSlotStat()` 与 slotMode；`CUser/make_basic_info.inc` 在属性块后、
  装备列表前写同一字节。
- 装备槽号：`support=20`、`magic stone=21`（`gen_item_metadata.py` 的设备类型表）。

## 改动

- 新增 `src/Tools/VmTestKit/Expand-Equip-Slot-FG345.cs`：649→20、650→21，
  位掩码 0..3，Award 走 `Offline70Economy.Apply`（receipt `...-expand-slot`）。
- `Economy-State-FG.cs`：持久化 `ExEquipSlotStatFG`（DefaultValue 0，旧档兼容）、
  Validate ≤3、Commit 回读校验、Move 时未解锁的 20/21 槽拒绝穿戴。
- `Economy-Protocol-FG.cs`：`AdditionBody` 写 `body[100]=ExEquipSlotStatFG`
  （属性块之后、已穿装备行之前）。
- `Quest-Protocol-FG.cs`：`FinishRewardBody/Reply` 新增 mode 参数；mode≠0 时
  写 rewardType 0x16 + stat + mode，且不再写普通任务的 received 行。
- `Batch-Service-FG.cs`：op36 完成 649/650 时先 Award 再回 ACK（带 mode）；
  进入世界时用 `CompletedQuests` 做 Reconcile，旧档补解锁。
- `data/item-metadata.xml`：440197→20、450040/450114→21，MaxDurability 1→0（PVF 无耐久）。
- `local-run/pvf/gen_snow_quests.py`：注释同步为服务端已实现，保留 649/650 映射表。

## 验证（2026-10-26）

- `python scripts/check_repo.py` 通过（169 源）。
- 暂存编译双 exe 成功，无参冒烟均 exit 2（`local-run/build-staging-fg345/`）。
- `local-run/driver/ExpandEquipSlot-FG345.cs`：30 项断言全过（位掩码、旧档迁移、
  重复 receipt、ACK 字节、addition[100]、锁定/解锁 Move、XML 旧档兼容）。
- `QuestLanding-FG207-fg345.exe`：80 项断言全过；`QuestWireFixtures-fg345`：通过。
- 实机包：解密 13:06 的 addition 包，`statByte@100=1 wornCount@101=11 id=8`；
  角色 8 存档出现 receipt `expand-slot-reconcile-649`（kind `expand-equip-slot`）、
  `ExEquipSlotStatFG=1`。
- 部署：`build/stable` exe（13:04:03）含本特性字符串，metadata 已同步；
  双服 13:04:06 重启，客户端 13:05/13:06 登录后已收到 stat=1 的 addition。

## 边界

- 已完成 649 的旧档通过进世界 Reconcile 解锁，不需要 GM 操作；650 同理。
- 尚未截图验证客户端槽位 UI 的最终绘制；若字节已到而格位仍不显示，剩余工作只在客户端
  渲染/解锁判断，不涉及服务端状态。
