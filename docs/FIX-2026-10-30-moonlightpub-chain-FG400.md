# FIX 2026-10-30：月光酒馆主线断链与「索西雅的灾星」称号任务（FG400）

状态：**已编译部署，双服重启**。待实机确认。

## 1. 现象（用户 2026-10-30 报告）

做完「月光酒馆的目击者」（4105）后：

- 索西雅处没有后续任务「期待再次相见」（4106），主线在此断掉；
- 更没有称号任务「索西雅的灾星」（15）。

存档核对：`local-run/DNF70-batch-test-FG/progression-r8-FG.xml`（r8，14:30）已完成表里
**有 4105**、没有 4106/15，与报告一致。

## 2. 根因（两个独立缺口）

### 2.1 4106 被「完成时寄信」的三个段落拒收

`n_quest/handonmyre/moonlightpub/epic_49_moonlightpub_jangsamrang_6.qst` 结尾带：

```
[mail time] 1
[mail title] `阿甘左留言`
[mail contents] `...`
```

这是原版服务在交任务后给角色寄信用的文本（rednf `Quest` 的
`m_mailQuestFlag/m_mailTitle/m_mailContents`，`CUser::quest_action` 的 finish 分支）。
离线服务没有邮箱系统，引擎的推进/发奖路径也完全不读这三个段落，但生成器把它们当成未知段落
（`extra:[mail time];extra:[mail title];extra:[mail contents]`）⇒ `Supported=false` ⇒ op21
永不下发 4106 ⇒ 4105 的唯一后继消失，整条月光酒馆史诗在目击者处断链，称号任务 15 的前置也就
永远不成立。

### 2.2 15 的判定是旧的，并叠加单机组队门槛

目录里 15 的记录是 `Supported=false / Reason=reward:[title]`，但：

- 称号簿 `Offline70TitleBookFG157.AwardsFG205` 早已有 `{15, 26615, 0, 25, 6520}` 一行；
- 当前生成器 `TITLE_AWARD_QUESTS` 包含 15，重导结果为 `Supported=true, TitleRewardFG157=true`；
- `CheckProfile` 对 15 全部通过（achievement / 奖励对 26615×1 / 无选择奖励 / 非重复 / 非转职）。

所以 `reward:[title]` 是过期文案（同 2026-10-25 称号批次里 12/25 的形态），需要重导。

另外 15 是 `[condition under clear]` **子类型 5**（`[int data] 140 3 2` = 副本 140 / 最低难度
3 / **队伍 ≥2 人**），文案也写着“组成 2 人队伍前往…月光酒馆”。原版在接取时按
`m_authenData.m_field10`（队伍人数）判定（rednf
`UserQuest/check_quest_subtype_condition.inc` case 5）；本服务是单机，`op49` 通关证据里
`partyMembers` 恒为 1（`Batch-Service-FG.cs` Clear 调用处写死 1），`Progression-FG.cs` 按
`data[2]>1 && partyMembers>=data[2]` 判定 ⇒ **即使翻成 supported 也永远完不成**。
`local-run/driver/QuestFlipAudit.cs:367` 早已把这条记成
`party-members-required2-server-passes-1`；受影响的不止 15：目录里还有 **30 条已 supported
的 sub5 任务**（3700–3726 远古系、4274/4295/4047、20587–20611 魔王系）同样是 `data[2]=2`，
同属“看着能接、永远交不了”的隐性缺口。

## 3. 修复（FG400）

| 文件 | 内容 |
|---|---|
| `local-run/pvf/gen_snow_quests.py` | `ABSORBED` 吸收 `[mail time]`/`[mail title]`/`[mail contents]`，注明是完成寄信文本、引擎不读、离线无邮箱；**`[mail enclose]` 仍拒收**（附带物品是真实奖励，必须另行实现） |
| `src/Tools/VmTestKit/Quest-Difficulty-FG365.cs` | 新增 `StrictParty` 策略（默认放宽，`DNF70_QUEST_PARTY_STRICT=1` 还原原版比较），与既有 `StrictDifficulty` 同址说明 |
| `src/Tools/VmTestKit/Progression-FG.cs` | `Clear` 的子类型 5：`data[2]==1` 仍要求恰好 1 人；`data[2]>1` 在单机策略下由单人通关满足（副本号/最低难度列仍照常校验） |
| `data/quest-catalog.xml`、`build/stable/quest-catalog.xml` | 4106：`Supported true` + 空 `Reason`；15：`Supported true` + 空 `Reason` + `<TitleRewardFG157>true</TitleRewardFG157>`。其余字段逐字节不动（记录数 3255、Id 顺序、非目标记录全等）。备份 `*.pre-fg400.bak` |
| `local-run/pvf/apply_moonlightpub_chain_FG400.py` | 落地器：用 **在线 PVF（pvfUtility 27000）** 重导 4106/15，逐字段核对后才翻；幂等、原子替换、保留 CRLF+BOM |
| `local-run/driver/QuestLanding-FG207.cs` | 旧断言“15 stays refused”按新策略更正 |
| `local-run/driver/MoonlightPubChain-FG400.cs` | 新验收驱动（61 项断言） |

生成器同步后重跑 `derive()` 对 4106 的结果是 `Supported=true / Reason 空`，对 15 是
`Supported=true / TitleRewardFG157=true / Rewards=[26615,1]`，与落地的记录一致。

## 4. 验收

离线（已跑）：

```
python scripts/check_repo.py                 -> 549 files / 201 stable sources
python scripts/build.py --smoke              -> compiled 201, smoke passed
local-run/build-verify-fg400/MoonlightPubChain-FG400.exe
                                             -> FG400 checks=61 (全绿)
```

驱动覆盖：

- 4106/15 的 supported、类型/条件/前置/奖励与称号簿行（15 → 26615、格位 0:25、成就 6520、
  op369 包体）；
- 可见性：4105 完成后 4106 出现在 `Offers()`，4106 完成前 15 不出现，完成后 15 出现；
- 引擎链：接受 4106 → `Kill(140,2,5310)` → ready → 交付；接受 15 → **单人** `Clear(140,3,1,...)`
  → ready → 交付，outbox 走 `TitleRewardFG157=26615` 且不带普通物品；
- 称号发放：`Award(15)` 写入 0:25 / 26615 / 成就 6520，重复发放幂等，经济状态 `Validate` 通过；
- sub5 策略两支：`2525`（1 人本）2 人通关不成立、1 人成立；`3700`（2 人本）单人成立；
- 副本 140 的 4106 变体：终点房 16009、APC 5310（role 8）随房间包登记、未死不算清房、
  死亡后走 `BossApcTailFG361` 结算并满足清关。

部署：停服 → `python scripts/build.py --smoke`（catalog 同步进 `build/stable`，哈希一致）→
`local-run/restart-detached.ps1`；7002/17001 均在听，启动日志无异常。

## 5. 玩家侧流程（r8 角色）

1. 重新登录（重启会断开会话）；到索西雅（NPC 5）接「期待再次相见」。
   r8 当前 19 个进行中任务，接 4106 正好到 20 上限，属正常边界。
2. 进月光酒馆（副本 140，建议勇士/以上）走到终点房，击杀**长三郎**（APC 5310）；
   该变体终点房就是 16009，5310 是唯一 boss，杀死即结算。
3. 回城交 4106（奖励金币/经验；原版的「阿甘左留言」信件不会出现，见残留）。
4. 索西雅处出现「索西雅的灾星」（15）：再通关一次副本 140 即可（单人有效，见取舍），
   交付后 title 26615 直接进称号簿 0:25，成就 6520 点亮。

## 6. 取舍与残留

- **没有邮件**：「阿甘左留言」是原版完成寄信，离线服务无邮箱子系统，本次只吸收文本、
  不伪造邮件（伪造会牵出 mailbox 存储/删除/附件到期整条链）。
- **组队门槛按单机放宽**：15 的客户端条件文案仍是“组成 2 人队伍”（客户端本地 .qst，
  改名要动客户端数据），服务端按单人通关结算；`DNF70_QUEST_PARTY_STRICT=1` 可还原原版比较
  用于取证。
- **连带收益**：30 条已 supported 的 sub5（远古/魔王系列，要求 2 人）从“永远交不了”变为可完成；
  这些记录本身没有再改。
- 15 的判定属于“过期 Reason”，本次只重导 4106/15 两条；其余 `reward:[title]` 已无记录被拒
  （`grep` 目录只剩 0 条），不需要再做批量。
