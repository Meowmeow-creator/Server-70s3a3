# FIX 2026-10-30 — 诺斯玛尔远古链断点：「传说中的赫尔德」不出现（FG408）

玩家反馈：做完 **远古 - 黑色瘟疫狄瑞吉** 后，后续的 **远古 - 传说中的赫尔德** 不出现，
净化污秽气息/暗黑疫苗整条支线一起消失。

## 结论

不是击杀没结算，是任务目录把交接任务判成了不支持：

| id | 名称 | 原状态 | 原因 |
|----|------|--------|------|
| 5990 | 远古 - 传说中的赫尔德 | `Supported=false` | `extra:[delete npc index];extra:[target character]` |
| 5994 | 远古 - 魔法师的使命（法师分支） | `Supported=false` | 同上 |

`Offline70QuestCatalogFG.Available()` 第一行就是 `!q.Supported → false`，`Get()` 对未支持
记录直接抛 `Unsupported quest 5990`，所以 5989 完成后服务器永远不会把这个 id 放进 offer
列表（op21 只发 id，任务名是客户端本地表）。

根因是**两次落地之间的时间差**：5990/5994 是 2026-10-19 由 `gen_current_quests.py`（底层
`gen_snow_quests.derive`）从 n_quest 树带进来的；当时 `[delete npc index]` 还没进
`ABSORBED`，于是整条记录按 unsupported 落地。2026-10-26 为了救 4601（行踪不明的罗莉安）
才把 `[delete npc index]` 提升为可吸收段落，但只重导了 4601，**诺斯玛尔这批没有再跑一遍**，
两条记录就停在 10-19 的判决上。`[target character]` 是同一个提前返回的连带原因：
`derive()` 在记录已被拒时不会再去算 JobMask。

## PVF 原文与语义

`n_quest/northmyre/leshphon/epic_67_leshphon_1_10.qst`（5990）：

```
[npc index]          28          # 接取：艾丽丝（西海岸 town3）
[complete npc index] 62          # 交付：杜罗西（阿法利亚 town4 area4）
[delete npc index]   28          # 任务期间艾丽丝离场，客户端按自己的任务态移除该 NPC
[target character]   `[swordman]` `[fighter]` `[at fighter]` `[gunner]` `[at gunner]` `[priest]` `[thief]`
[type] `[meet npc]`  [int data] 62
[pre required quest] 5989
```

`epic_67_leshphon_m_1_10.qst`（5994）是法师分支，`[target character]` 只有
`[mage]` / `[at mage]`，其余相同，交付后同样触发赫尔德附身杜罗西的剧情。

两条 extras 都可以安全吸收：

* `[delete npc index]` —— 只影响客户端城镇 NPC 显隐（艾丽丝去报信），引擎推进路径从不读它，
  目录 schema 也没有对应元素；4601 的既有判决与逐字段复导证明同一处理成立。
* `[target character]` —— 引擎**已经实现**：`Offline70QuestDefinitionFG.JobMask`，
  位 i 对应 `JobNamesFG204[i]`（0 swordman / 1 fighter / 2 gunner / 3 mage / 4 priest /
  5 at gunner / 6 thief / 7 at fighter / 8 at mage），由
  `Offline70QuestCatalogFG.JobAllowed` 在接取/完成两处同时校验。同一族的
  5981=247 / 5993=264 / 5991=247 / 5995=264 就是这个约定的现成样本。

按 PVF 列表推导：5990 = 位 0,1,2,4,5,6,7 = **247**；5994 = 位 3,8 = **264**。
不补掩码而只翻 Supported 会让法师和非法师同时看到两个版本，因此必须成对补。

## 改动

`local-run/pvf/apply_leshphon_hilder_FG408.py`（原位、幂等、带不变量校验）：

```
5990: <Supported>false</Supported> → true
      <Reason>extra:[delete npc index];extra:[target character]</Reason> → <Reason />
      <GrowType>-1</GrowType> 后插入 <JobMask>247</JobMask>
5994: 同上，<JobMask>264</JobMask>
```

* 同时写 `data/quest-catalog.xml` 与 `build/stable/quest-catalog.xml`（运行中的频道服读
  `AppDomain.BaseDirectory`，即 build/stable 的那份），两份字节一致。
* 记录数 3255 不变、id 多重集不变、`<PvfSha256>` 不变、BOM/CRLF 不变；逐块 diff 只有这两条
  记录的 5 行。
* 备份：`local-run/pvf/backup-2026-10-30-leshphon-hilder/quest-catalog.{data,stable}.bak`。

## 验收

`local-run/driver/LeshphonHilderCheck-FG408.cs`（真实 catalog loader + 真实推进引擎，
34 项断言）：

* 5990/5994 supported，掩码 247/264，接取 28 / 交付 62 / 前置 5989；
* offer 矩阵：职业 0/1/2/4/5/6/7 只见 5990，职业 3/8 只见 5994；未完成 5989 或 67 级时两者
  都不出现；
* 非法师：5989 → 5990（对错 NPC 不给进度、错交付人拒绝）→ 5991 供给，材料精确为
  `3305×10 3306×1 3038×2 3039×2 3042×4`，未凑齐不能交，交完开出 5998；
* 法师：5989 → 5994 → 5995 → 5998 同理。

复跑：

```
LeshphonHilderCheck-FG408.exe <quest-catalog.xml> <fresh-dir>
```

补丁前的备份目录跑同一 driver 会在 `Get(5990)` 抛 `Unsupported quest 5990`，即玩家看到的
断点；补丁后 PASS。既有 `QuestProgressionFixtures` / `QuestRewardsFixtures` 在本机对补丁
前后**同点失败**（FG114 测试倍率环境缺失，与本次改动无关）。

部署：`restart-detached.ps1` 于 16:32 重启双服（7002 / 17001 均 LISTENING，目录加载无异常）。
重启会断开在线会话，重新登录即可。

## 遗留（下一步才需要决定）

5991/5995「净化污秽气息」本身的材料来源已核对：

* 污秽之血 3305、狄瑞吉之血 3306 —— `independent-drops-FG355.dat` 里由痛苦之村列瑟芬的
  56142/56143/56144（变异昆虫）与 62116（黑色瘟疫狄瑞吉）掉落，可达；
* 黑色/白色/无色大晶体 3038/3039/3042 —— **当前构建没有来源**：分解（诺顿 op28）只产出小晶块
  3033-3037，大晶体在原版靠「制作无色大晶体」之类的配方道具（`[craftmanship]`）产出，而
  本构建没有副职业制作系统；商店目录里 5 个 id 都是 BuyPrice 0。

所以 5990/5994 之后玩家会卡在材料上（任务能接、能看见，但凑不齐）。要补齐只有两条路：
给大晶体加商人货架（如林纳斯/罗莉安，价格参照 PVF `[price] 400`），或实现配方制作。
这是内容/经济决定，本次未动。

## 未做

* 未重打分享包（`dist/70s3a3 v0.5.zip`、`dist/DNF70-一键端-20261029` 里的目录仍是旧的）；
* 未改 `dist/DNF70-一键端-20261029` 解包目录。
