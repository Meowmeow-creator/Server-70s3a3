# FG391 重复任务只能完成一次（op365 已完成表误含可重复任务）

日期：2026-10-29
玩家原话：「所有重复任务不能多次完成只能一个一个完成。」

## 结论

服务端把 **`[normaly repeat]` 任务也写进了下发给客户端的 op365「已完成任务表」**。
客户端收到这张表后把这些任务永久标成“已完成”，NPC 处不再把它当可接任务，于是每个
重复任务只能完成一次。原生服务端从不把可重复任务写进这张表。

## 证据（原生 = rednf/ORIG）

- `reference/rednf/.../Game/Quest/isRepeatableQuest.inc`：任务类型 4（`[normaly repeat]`）与 8（`[urgent]`）返回可重复。
- `reference/rednf/.../Game/UserQuest/finish_quest.inc`：完成任务时只有 `!isRepeatableQuest()` 才
  `m_cleared.setClearedQuest(questIdx)` —— **可重复任务永远不进 cleared 表**。
- `reference/rednf/.../Game/UserQuest/checkAcceptableQuest.inc` / `accept_quest.inc`：接任务时
  “已通关”过滤只对非重复任务生效。
- `reference/rednf/.../Game/CUser/send_clear_quest_list.inc`：登录后把 cleared 表原样下发
  （`put_int(0x7530)` + 0x7530 字节）。本服的 op365 就是这张表（早期 `Quest-Finish-Protocol.cs:32`
  的固定 102 字节样本同样是 `u32 长度 + 逐 id 字节`）。

## 改动

1. `src/Tools/VmTestKit/Quest-Catalog-FG.cs`
   - 新增 `Offline70QuestCatalogFG.ClearedForWire(IList<ushort>)`：按目录里的 `Repeatable`
     过滤，可重复任务（以及目录中不存在的 id 之外的一切）不进入 cleared 表。
2. `src/Tools/VmTestKit/Quest-Protocol-FG.cs`
   - `HistoryBody/HistoryReply` 增加 `(recorded, cleared)` 重载：**跨度仍覆盖全部已完成 id**
     （这样客户端旧状态里残留的可重复任务会被写成 0），只有非重复 id 置 1。
   - 旧的单参重载保留（等价于 cleared = completed），探针/历史样本不受影响。
3. `src/Tools/VmTestKit/Batch-Service-FG.cs`
   - 三处 `quest-history` 下发（登录、接任务跨天、结算）改用 `HistoryReply(progress.Completed, catalog.ClearedForWire(...), keys)`。
   - 同时补上**跨天重置后的 cleared 表刷新**：登录时先跑 `RollDaily`；接任务时若服务日变了就补发一次
     `quest-history`，让 `[daily]` 任务在新的一天真正重新变为可接（此前清掉了 `Completed` 但没告诉客户端）。

服务端内部的 `Completed` 列表语义不变（前置任务、条件奖励、outbox 校验都继续把它当“完成过”），
只是不再把它整个抄给客户端。

## 验证

新驱动 `local-run/driver/RepeatClearedCheckFG391.cs`（`local-run/build-verify-repeat/` 内编译）：

```
ok   5325 is a [normaly repeat] [meet npc] quest
ok   repeat quest offered after its prerequisite
ok   non-repeat completed quest stays unavailable
ok   non-repeat completion stays in the cleared map
ok   non-repeat awakening prerequisite 5308 stays in the cleared map
ok   repeat quest accepted with a persisted instance
ok   first repeat cycle pays out once
ok   completed repeat quest is absent from the cleared map
ok   history body still spans the full recorded id range
ok   op365 byte for the repeat quest stays 0
ok   op365 byte for the ordinary quest stays 1
ok   op365 byte for the non-repeat prerequisite stays 1
ok   completed repeat quest is offered again
ok   second acceptance creates a fresh instance
ok   second repeat cycle pays out again
ok   still absent after the second cycle
PASS FG391 repeat quests stay repeatable and never enter the op365 cleared map
```

`python scripts/check_repo.py` → `repositoryChecksPassed: true`（430 文件 / 200 源）。

回归说明：`QuestProgressionFixturesFG`（quest 102 经验奖励断言）在当前工作区失败，值是
`xp=1404` 而断言写死 `468`；该差异来自本批未提交的 `Test-Rates-FG114.cs` / 奖励口径改动，
与本修复无关（本修复不改奖励计算）。`QuestWireFixturesFG` 通过。

## 部署

停双服 → `python scripts/build.py --smoke` → 一键端重启。生效点：
登录后的 `quest-history`、每次任务结算后的刷新。**已完成的重复任务在下一次登录后即可再次接取**
（客户端每次启动的 cleared 表从零开始，本次登录收到的新表不再含重复任务）。

### 实机（部署后）端到端取证

`local-run/driver/LoginSelectSlotFG391.cs` 用槽位 2（roleId=8，`Completed` 里有 1 个重复任务 430）
走真实登录，服务端落盘 `logs/channel-evidence.log.00022-quest-history.bin`；
`local-run/driver/HistoryMapProbeFG391.cs` 按 15 字节包头解密并与目录/存档逐条比对：

```
header=15
plain=5888 span=5872
repeat recorded=1 repeat flagged=0        <- 430 不再被标记为已完成
ordinary recorded=167 ordinary missing=0 <- 普通任务一条不少
unexpected ones=0
PASS live op365 excludes repeatable completions
```

`LocalSelectSlot` 驱动是单会话的：客户端一断开频道服就 `PEER_CLOSED` 退出（既有行为），
所以取证后要再跑一次 `one-click.ps1` 把服务拉起来给玩家用。
注意 one-click 的“存活 0/2”自检是误报——进程和端口其实都在（本机实测两次），以 `tasklist`/`netstat` 为准。

