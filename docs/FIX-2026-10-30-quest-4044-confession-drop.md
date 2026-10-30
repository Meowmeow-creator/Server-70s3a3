# FIX 2026-10-30：「范·弗拉丁的自白书」（4044）刷不出任务物品【范·弗拉丁的自白书】（4701）

## 症状

玩家报告主线任务「范·弗拉丁的自白书」（4044，泽丁·施奈德发布/交付）需要
【梅尔文的测谎硬币】(4724) + 【范·弗拉丁的自白书】(4701)，但包里没有 4701，任务交不了。

## 原生数据（客户端 PVF 与服务端目录逐字节一致）

`n_quest/lufthafen/haze/epic_67_haze_16.qst`（pvfUtility 实时读取）：

```
[monster reward item]
	61266	92	-1	4701	1	100	1
[int data]
	4724	1
	4701	1
```

即：雾都赫伊斯（d92）首领范·弗拉丁（61266）在**任意难度**下 100% 掉落 4701×1，上限 1。

列语义经 rednf 原生实现核对（`Quest/CheckKillMonster.inc`：`m_killCount`=怪物、
`m_mapIdx`=副本、`m_dungeonIdx` 与 `UserQuest.m_authenData.m_characClass` 比较 = 难度；
后者由 `UserQuest/set_authen_data.inc` 在接任务时写入，实为难度字段），与
`Server-70s3a3/src/Tools/VmTestKit/Quest-Drops-FG.cs` 的
`(monster, dungeon, difficulty, item, attempts, chance, maximum)` 一致。

## 服务端链路核验（离线 driver，全部走生产代码）

新探针 `local-run/driver/Quest4044DropCheck.cs`（27/27 PASS，`data/` 与 `build/stable/` 各跑一遍）：

- 4044 目录形状：名字 / Supported / `[seeking]` / 59→58 / 需求 4724×1+4701×1 / 唯一掉落行
  `61266/92/-1/4701/chance100`；
- 掉落：难度 0/1/2/3 各 200 次 **100% 命中 4701×1**；已持有 4724 不影响掉落；
- 4701 元数据 Kind=5（任务页直发），落 `list0 slot 153..184`；
- op14 增量包确实带出这条任务页行；
- 任务计数：拿齐两件后 `WireRemaining=0`；`Complete(4044, npc 58)` 成功；缺 4701 时按预期拒绝；
- 清关保底（本次新增，见下）：d92 各难度通关均补发 4701×1、其他副本不串、上限 1 不与首领掉落叠加。

新探针 `local-run/driver/D92BossProbe.cs`（20/20 PASS）：

- 基础迷宫四个难度的终点格都是 15183 首领房，roster 里都有 61266；
- **只**接 4044 时 `MazeIndexFG361=0`（基础迷宫），首领存在。

## 现场证据（为什么玩家会觉得"从来不掉"）

`local-run/logs/channel-evidence.log`（2026-10-30 14:27–14:35 场次）：

- 14:30:27 玩家用 GM 一次性完成 4043（`GM_QUEST_COMPLETED id=4043 ... topped=3042×10,3299×30`），
  14:30:31 才接 4044；此后全场没有一次 d92 副本（`DUNGEON_BLOCK` 只有 d87 ×3，14:18–14:21）。
- 角色 8 存档里 4044 `Active`、`Progress=0`，全服所有存档从未出现过 4701。
- 10-29 的两把 d92（FG390 记录）是**在 4041 生效期间**跑的；4041 的【皇女的踪迹】在侧房精英
  56031 狙击手艾丽格身上，首领本来就不掉它 —— 所以"打死首领什么都不给"当时是正常的。
  换句话说：**还没有任何一场"4044 生效时打死 61266"的实机记录**。

因此服务端逻辑本身没有坏；但确实存在两条会把任务逼进死路的边角，值得兜底：

1. **早期雾都任务会换掉迷宫层。** d92 的 `.dgn` 带 3 个 `[quest connection]` 迷宫块
   （4039 / 4040 / 4058），`SelectQuestMazeFG361` 按"取最后一个匹配块"实现；这三个五房布局
   **都没有 61266**。正常链路下 4039/4040 必须先交付（`Available` 要求前一任务在
   `Completed`）才能接 4044，两者不可能并存；但**老存档**里若仍挂着 4058（它现在是
   `Supported=false`，接不进也交不掉），迷宫层会永久被 4058 块劫持，雾都赫伊斯再也没有首领，
   4044 的 4701 就永远刷不出（`D92BossProbe` C 组复现了这个劫持）。
2. 任务页满时，Kind=5 的直发会被 `QUEST_DIRECT_DEFERRED bagCapacity` 跳过（下次击杀仍可再掉）。

## 修复

`data/quest-catalog.xml` + `build/stable/quest-catalog.xml` 的 4044 增加一条**清关保底**
（普通 6 列 ClearDrops 布局，与 1000/3081、2/3087 的既有写法相同）：

```xml
<ClearDrops>
  <int>92</int>
  <int>-1</int>
  <int>4701</int>
  <int>1</int>
  <int>100</int>
  <int>1</int>
</ClearDrops>
```

- 只在 **4044 处于进行中** 时生效（`Offline70QuestDropsFG.Clear` 遍历 Active）；
- 只在 d92 通关时结算，难度 -1 全难度，**上限 1** —— 首领 100% 直发仍是首选来源，
  已经拿到时清关行看到 `existing==maximum` 直接跳过，不会重复给；
- 老存档迷宫被劫持、或直发因背包/任务页满被跳过的场合，通关即补，任务不会再死锁。

脚本：`local-run/pvf/apply_quest4044_clear_drop.py`（幂等，保留 BOM 与 CRLF；`--write` 落盘）。
这是**单机可玩性兜底**，不是原生行为：原版该物品只从首领掉落。若要回到纯原生，
删掉这两处 `<ClearDrops>` 块即可，首领直发路径不受影响。

## 验证

- `Quest4044DropCheck` 27/27 PASS（`data/` 与 `build/stable/` 各一遍）；
- `D92BossProbe` 20/20 PASS；
- 回归：`Fg365AcceptanceCheck` 50/50 PASS；`Batch2QuestDropsCheck-FG360` 78/78 PASS
  （按当前源重建后运行）；
- 部署：目录两份 `quest-catalog.xml` 已写入（4914526 字节，二者逐字节相同），
  双服已于 2026-10-30 14:58 在新目录上重启（服务进程启动时间晚于目录写入时间）。

## 给玩家的结论

- 该物品的原生来源：**雾都赫伊斯（d92）首领「范·弗拉丁」100% 掉落**，任意难度；
  10-29 那两把是「追查皇女的行踪」任务生效时跑的，那个任务的东西在**右侧第 3 格侧房的
  狙击手艾丽格**身上，首领不掉，属正常。
- 本次新增保底：**只要「范·弗拉丁的自白书」任务在进行中，通关雾都赫伊斯就会把
  【范·弗拉丁的自白书】补进任务页**（上限 1 个，已有时不重复给）。
- 拿齐【梅尔文的测谎硬币】+【范·弗拉丁的自白书】后回城找泽丁·施奈德交付即可。
