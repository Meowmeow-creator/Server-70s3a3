# 2026-10-25 称号成就任务修复（怪兽猎杀者、烈焰征服者等）

## 1. 症状与根因

玩家侧表现：任务栏里没有称号类成就任务（`n_quest/title/achievement_*.qst` 一族），
例如 4303 怪兽猎杀者、3 烈焰征服者。

根因是一次**数据回归**，不是机制缺失：

- 称号簿事务、发奖、367/368/369 三条通知在 FG157/FG173 就已写完（`TitleBook-Service-FG157.cs`），
  发奖走 `Offline70QuestDefinitionFG.TitleRewardFG157` 这个开关；
- FG175 基线（70cfcc6）的任务目录里 **12 条**称号任务带 `<TitleRewardFG157>true</TitleRewardFG157>`
  且 `Supported=true`，当时是真能玩到的；
- c617256 重导任务目录时把这一族整排回成 `Supported=false` + `Reason=reward:[title]`，
  `<TitleRewardFG157>` 元素一条不剩（`git log -S "TitleRewardFG157>true"`：70cfcc6 12 条 → c617256 起 0 条）。
  引擎于是连任务都不列出来，玩家看到的是"称号成就任务消失"。

## 2. 这次改了什么

**代码（1 文件）**：`src/Tools/VmTestKit/TitleBook-Service-FG157.cs`

- 原来的格位来源是 13 项白名单 + 序号算式（`Quests` / `AwardSlots` / `Reward()` 的
  `26596+i` 和 4 个特例），只能覆盖那 13 条；
- 现在改成生成表 `AwardsFG205`：**30 行**，每行 `(任务号, 称号物品, 称号簿类别, 格位, 成就号)`，
  `Reward/AwardCategoryFG173/AwardSlotFG173/AchievementFG173/Award/AwardBody/CheckProfile` 全部改成查表。
- 表由 `local-run/titlefix/gen_title_awards.py` 从 **`etc/titlebook.etc`**（列就是
  `格位 类别 成就号 模式 物品`，**没有任务号**，见 §4）× 每条 `n_quest/title/*.qst` 的
  `[reward type]`+`[reward int data]` 生成，**不读 `data/quest-catalog.xml`**；
  生成器硬要求 FG157/FG173 已发过的 13 行逐字段不变、格位与成就号各自唯一，否则拒绝落盘。
  这样 `CheckProfile` 拿目录里的奖励物品回校这张表仍是两次独立取数的交叉校验，而不是自我循环。

**数据（1 文件）**：`data/quest-catalog.xml`

- 22 条称号任务：`Supported` false→true、`Reason` 换成
  `FG205 original CN title quest; titlebook slot award`、并补 `<TitleRewardFG157>true</TitleRewardFG157>`；
- 落盘脚本 `local-run/titlefix/flip_title_catalog.py`：记录数、Id 顺序、非目标记录逐字节、
  目标记录只能有这三处差异、CRLF 行数增量=22，全部断言通过；改前副本
  `local-run/titlefix/quest-catalog.before-title-flip.xml`。

## 3. 放行判定（为什么是这 22 条）

36 条 `Reason=reward:[title]` 逐条过六道闸（`local-run/titlefix/title_gate.py` + `plan_flip.py`，
后者把候选集在自己的不动点里再收敛一次，避免"A 靠 B 可达、而 B 没开"的自欺）：

| 闸 | 判据 |
|---|---|
| W 格位 | 奖励物品在 `etc/titlebook.etc` 里唯一对应一个格位，mode≥0、类别 0/1 |
| P 形态 | `Grade=[achievement]`、奖励是 `(称号物品,1)`、无选奖、不可重复、非转职 |
| O 目标 | 类型有对应计账路径：`[seeking]` 偶数宽、`[hunt monster]` 宽 4、`[meet npc]` 宽 1、`[condition under clear]` 按 SubType 宽度 |
| D 域 | 条件列/自带掉落列指到的副本在本构建开放副本集内；`[meet npc]` 的 NPC 站在已支持城镇区域 |
| M 材料 | `[seeking]` 的收集物在"含任务掉落/接取赠物/商店/地面堆"的定点可达集里 |
| R 前置 | 前置任务组能在同一不动点里做到 completed |

PVF 取数与目录的交叉核对（`local-run/titlefix/qst_crosscheck.py`）：**36 条全过**，
`[reward int data]`、`[int data]`、`[pre required quest]`、`[level]`、`[monster reward item]` 行
与目录逐字段一致 —— 也就是说目录本身没写错，缺的只是接线。

放行 22 条：`1, 2, 3, 4, 9, 10, 11, 13, 14, 21, 24, 60, 61, 446, 1028, 4303, 4423, 4572, 5850, 5851, 5852, 5853`。

## 4. 仍留闸的 14 条（逐条机制链，不是"没做"）

- **8 条被上游任务卡住**（称号机制本身已可用，接上上游就通）：
  - 5 人偶的诱惑 ← 120 人偶之王道格里 ← 119 水落石出 ← **4601 行踪不明的罗莉安 `Supported=false: extra:[delete npc index]`**
  - 6 石巨人终结者 / 59 光明守护者：同上一条链（经 121/122/123/124/125/126）
  - 26 杂技爱好者 ← **5729 敌人的噩梦之夜 `extra:[delete npc index]`**
  - 93 卡勒特追击者 ← 5781 英雄 - 大决战 ← **5780 `objective:[condition under clear]:11`**
  - 15 索西雅的灾星 ← **4106 期待再次相见 `extra:[mail title/contents/time]`**（本构建无邮件投递）
  - 3606 死神的气息 - 神秘的影像 ← 3592 德莱弗斯的目的 ← **3591 征服死亡之塔 `objective:[hunt enemy]:0`**
- **1 条缺物品来源**：58 后勤补给队长 —— 收集物 3272 在掉落/商店/赠物里都没有出处。
- **6 条无称号格位**：399-403 远古系列（26023-26027）与 1332 不灭之王波罗丁（26065）——
  奖励物品不在 `etc/titlebook.etc` 里，走不了称号簿事务；它们不是"格位歧义"，是**这张表没这一列**。
  原版把这类当作可穿戴装备称号发进背包，是另一条路，未做，不算接通。

## 5. 实测证据（隔离实例，端口 17032，一次性存档副本）

用当前稳定源码 + 当前 `data/` 编到 `local-run/titlefix/build3/`，
存档取 `local-run/DNF70-batch-test-FG` 的**副本**（`store4`，锁与 backup 已清），
驱动 `local-run/titlefix/TitleAwardCheck3.exe`：

- 结算链走的是引擎自己的 op36 处理：日志里 `QUEST_LOG_COMPLETED id=3 / 4303 / 21` 各 1 条；
  3 烈焰征服者是"真实接取(op33) + 真实交付(op36)"，交付前 GM 只发了 100 个 3073 火焰结晶，
  交付后 3073 在背包里归零 —— 材料真被扣掉。
- 称号簿落位：`(0,0)=26596`（怪兽猎杀者）、`(0,1)=26597`（烈焰征服者）、`(0,30)=26616`（向暗黑城进军，
  这条**不在**原 13 项白名单里，是新表的第一条端到端证明）；三行 `Sealed=true`，
  背包里没有称号物品残留（`Inventory` 47 条里 0 条 2659x/26616）。
- 台账 `[21, 4303, 3]`；`title-award369` 取证包 **3** 个、`title-achievements368` 24 个、`title-book367` 84 个。
- 重登后称号簿与台账逐字节不变；对已完成的 3 再发一次被拒（不铸造第二份，copies=1）。
- 留闸的反例也验了：GM 接 5 人偶的诱惑 → `Unsupported quest 5`，说明不是 blanket 打开。
- 驱动自身有两处内联读档判定（`active=False`/`completed=False`）是**读取时机**造成的假阴性，
  以服务端日志与最终存档为准；这两处不计入上面的结论。

任务可见性按玩家本尊存档量（`TitleOfferProbe3.exe`，当前源码+当前目录+存档副本）：
角色 Lv70/剑魂，`supported title quests=22`，**当场能接的 1 条**（21 向暗黑城进军，前置 70 已做），
按"把还能做的任务都做完"的定点口径 **22/22 可通达**（该口径忽略材料，是上限不是保证）；
`supported-but-unreachable` 为 0。

## 6. 部署状态（重要）

本次修复没有重启、也没有停过任何进程。修复落盘后，本仓库被另一路构建/部署流程在
23:47 重新编译（`build/stable/build-proof.json` sourceCount=165）、23:50:53 起了新的频道实例
（PID 38776，端口 7002，客户端有 ESTABLISHED），该实例加载的正是带 22 条称号任务的
`build/stable/quest-catalog.xml`（23:26 版，含本次接线）。旁证：旧白名单二进制在加载这 22 条
`<TitleRewardFG157>` 时必然被 `CheckProfile` 判 `Original title quest profile` 而启动失败，
既然它起来了并在服务，说明运行的二进制已含新表。

## 7. 未做

- 6 条无格位的远古/波罗丁称号（要按"装备称号直接发背包"另开一条路）；
- 8 条链首的 `extra:[delete npc index]` / `[mail ...]` / `[condition under clear]:11` / `[hunt enemy]:0` 机制；
- 58 的 3272 物品来源；
- 真人客户端里的称号页/完成弹窗表现（本轮只在协议+存档层取证，未操作游戏窗口）。
