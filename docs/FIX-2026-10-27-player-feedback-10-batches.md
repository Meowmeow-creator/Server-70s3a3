# 2026-10-27 玩家反馈十组问题修复记录（FG365）

来源：玩家对 2026-10-26 中午分享包（`DNF70-一键端-20261026`）的反馈。当前源码树在 10-26 之后已经合入了 FG355–FG364 的多批修复，**分享包本身是旧构建**，所以先逐条区分“源码已修 / 分享包未带”和“确实还没修”。

验收：

- `python scripts/check_repo.py` 通过（192 个稳定源）。
- `python scripts/build.py --smoke` 通过（192 源，双服无参拒绝返回 2）。
- `local-run/driver/Fg365AcceptanceCheck.cs` **47/47 全绿**。
- `local-run/driver/Batch2QuestDropsCheck-FG360.cs`（按 FG365 难度策略更新两处断言）**78/78 全绿**。
- 双服已用 `local-run/restart-detached.ps1` 重启，7002/17001 监听正常。
- 新分享包：`dist/DNF70-一键端-20261027.zip`。

---

## 1. 逐条结论

| 玩家反馈 | 结论 | 证据 / 修复 |
|---|---|---|
| 血蝴蝶之舞：变异的昆虫、某位冒险家的梦、破坏虫茧等掉落/目标任务卡图 | **源码已修 + FG365 难度放宽** | FG361 任务迷宫加载 6514/6515、FG360 友军 NPC 1508/1507 与物件破坏记账；FG365 让任务目标/掉落行在普通难度也匹配 |
| 血蝴蝶之舞：某位冒险家的梦没有 NPC | 同上 | 6514 的 `[ai character] 1508` 已进 `Quest-Maze-FG361.Apcs`；任务迷宫现在不因难度低于 2 而回退 base |
| 疑惑之村：独眼怪刷不出来、电梯一直往下 | **FG365 新修** | `16408 (2,5)start.map` 无 `[monster]`，只有 `[special passive object] 1112` 的 `[monster] 56716 67 5 5 -1`。`GsdMonstersFG130` 现在在该房间生成 56716（独眼巨人），电梯起始房不再空房 |
| 列车上的海贼：第一图鳄鱼/鳄鱼巢，应该是美人鱼 | **FG365 新修** | d86 `[start map] (5,0)` 没有 map specification，旧生成器按 greed 取了 15016（鳄鱼图）。现改为 15020 `(5,0)Start.map`；同时修 d86 东侧 boss 15025、d87 起始 15009 |
| 列车上的海贼：所有掉落任务卡死、鳄鱼巢不计数 | **源码已修 + 难度放宽** | FG360 被动/物件破坏通道；FG365 让 5842 的 `[86,2,8793,3,10]` 在普通难度也计数 |
| 小灯笼的好朋友：没遇到小美人空空伊 | **源码已修（FG362）** | 5844 激活时 d86 boss 格改用 15042 `Boss_congcong`（61194）；5874 对应 d87 15032；FG365 去掉王者级门槛 |
| 提交和 NPC 谈谈的任务：窗口关闭、任务不结束、无奖励 | **FG365 新修** | op36 完成请求现在会在 `[meet npc]` 未收到 op35 时补记访问；4032/4033 等链条可正常收尾 |
| 多次点击 NPC 语音重复 | **未改（客户端侧）** | 服务端不发 NPC 语音；重复播放来自客户端点击反馈，未找到可安全修改的服务端包 |
| 物品 3326 强烈的气息发送不了 | **分享包数据缺失，已同步** | 当前 `item-metadata.xml` 有 3326（Kind 2），10-26 分享包没有；新包已带 |
| 黄龙大会入场不了 | **FG365 新接** | d120/d121 加入 `Forest-Content`（1×1，map 17100/17101，boss 56048/56156）、`SupportedInArea(12,2)`、`DungeonGate(12,2)`、op5 `ShonanBody`；入场消耗 4726/4727；4080 改为 supported |
| 夺回西部线：GT-9600 单人完成不了 | **源码已修 + 起始房修正** | 5866 是 `[condition under clear] d87`，通关即置位；d87 起始房改为 15009，避免旧图流程异常 |
| 搜索列车内部：破坏装备不统计 | **FG365 新修** | 10197/10198/10199 是 `[special passive object]`；`Object-Drops-FG167.Capacity` 现在把它们纳入物件破坏容量，op35 也接受 opt=1 增量方向 |
| 最高级的铁鳞：接任务卡死、需要极品铁鳞 | **难度放宽** | 5870 的 4401 行原本只匹配勇士/王者/英雄，普通难度不掉；FG365 后普通也匹配。4401 是 Kind 5 任务物品，直接入包 |
| 目标蓝影马萨乔：扫描后不掉胶囊 | **FG365 新修** | 147 增加 3315 掉落（56010/56011，全难度，100%，上限 1）；6056 仍由 146 的 StartItems 发放 |
| 铁鳞海贼团 / 舰长室进不去 | **FG365 新接** | 501/502 加入 `Forest-Content`、`LufthafenBody`、`SupportedInArea(9,1)`；501 boss 房放 61248，502 全 11 房按 PVF 名册生成 |
| 皇女行踪完成后没后续主线 | **FG365 新修** | 4033–4037 的 `deadhead:npc-absent:1005` 是误判（1005 是任务 NPC，不是城镇 NPC；op35 也不要求 NPC 在城镇）；已全部翻为 supported |
| 0.3 挑战书白图/远古图不掉 | **FG365 新加** | 新增 0.3% 非 boss 击杀全局掷骰：等级 ≤44 掉 4328 燃烧恶魔的挑战书，≥45 掉 4329 蓝冰恶魔的挑战书；塔和深渊房跳过 |
| 悲鸣洞穴虫子和领主不爆戮蛊的眼睛 | **源码已修（FG355）** | `independent-drops-FG355.dat` 中 61143 虫王戮蛊（20–50%）和 61147 戮蛊幼虫（5%）都有 3254；10-26 分享包没有 FG355 |
| 悲鸣任务异界的怨恨不掉 | **源码已修（FG360）** | 4352–4356 的 `MonsterDrops` 已从 `[enemy reward item]` 重导，4327 由 d1501 的 61143/61146/61147/56112–56117 掉落 |
| 与阿甘左同行：阿甘左不出现、房间不变 | **源码已修（FG361+FG360）** | 4358 的 quest maze 6808/6809 + 友军 APC 412；FG365 去掉 MinDiff 3 门槛 |
| 诺伊佩拉进不去 | **数据路径正确，门票在 4443** | 1502 由 town4 area3 的 AncientBody 提供；`[required item] 4343` 由 4443 奖励，商店不可买。若玩家未完成 4443，服务端按 0x11 拒绝是预期行为 |

---

## 2. FG365 代码改动

### 2.1 单机任务难度策略
新文件 `src/Tools/VmTestKit/Quest-Difficulty-FG365.cs`：

- 默认：任务目标/掉落行的最低难度列只作展示，任意难度都匹配；dungeon id 仍必须匹配。
- `DNF70_QUEST_DIFFICULTY_STRICT=1` 恢复原生 `m_questMinDiff` 门槛。
- 接入点：`Quest-Drops-FG`（monster/clear 行）、`Progression-FG`（KillState/Clear/MapVisit）、`Batch-Service-FG`（op35 rowMatches）、`Dungeon-QuestMaze-FG361`（MinDiff）、`Dungeon-QuestCompanion-FG130`（Magma/Swordman/Odesa/Congcong/Paris/Hell 变体）。

### 2.2 特殊被动物件
- `Object-Drops-FG167` 增加 `Capacity(map,id)`：`[special passive object]` 的地图行为表就是容量来源。
- `Passive-Kill-FG360.DestroyPassiveObjectFG360` 在 `[passive object]` 表未命中时回退到该表，因此 10197/10198/10199（搜查列车内部）、10229/10230/10231（油桶/宝箱）可计数。
- `Batch-Service` 的 op35 现在接受 `opt=0/1`；`[hunt enemy]` 增量触发不再被 “increment direction” 拒绝。

### 2.3 副本内容
- `Forest-Content-FG75`：
  - d86：start 15020、east boss 15025，并补 15020/15025 名册。
  - d87：start 15009，并补名册。
  - 501：15091 空起始、15092 boss 61248（啰嗦的雄人鱼）。
  - 502：15097–15107 全 11 房，boss 61248。
  - 120/121：17100/17101 竞技场，boss 56048 痞子张海 / 56156 吉秀。
- `gen_dungeon_content.py`：start/boss 格缺 `[map specification]` 时优先用文件名里的 `(x,y)` 房间，避免再次回退到错误地图。
- `Clear-Experience-FG116.Basis` / `Monster-Experience-FG115.DungeonWeight`：501/502=69，120/121=73，权重 1.0。

### 2.4 入场与主线
- `Entry-Cost-FG185`：120→4726×1、121→4727×1，coinLimit 1。
- `Dungeon-Selection-Protocol`：`ShonanBody={120,121}`、town12 area2 分发；`LufthafenBody` 增加 501/502；`SupportedInArea` 增加 town9 area1 的 501/502、town12 area2 的 120/121。
- `Town-Content-FG75.DungeonGate`：town12 area2。
- `Batch-Service.EntryDungeon`：120/121 难度固定 3（`[designate dungeon difficulty] 3`）。
- `data/quest-catalog.xml`：4033–4037、4080 翻为 supported；147 加 3315 掉落、4447 加 3253 掉落（`apply_quest_drop_supplements_fg365.py`，可重跑幂等）。

### 2.5 0.3 挑战书
`Challenge-Book-Drops-FG365.cs` + `Floor-Drops-FG119` 接线：非 boss、非塔、非深渊房，30/10000（0.3%）掉 4328/4329。

---

## 3. 已知未闭环 / 需要实机确认

1. **疑惑之村电梯**：已补起始房独眼巨人，电梯起始房不再空；如果客户端在电梯动画里仍要求后续楼层切换（16415–16418 四选一），需要一次真人 op29/op48 抓包确认。
2. **黄龙大会**：目前是“1 个 boss 的竞技场”最小闭环，未实现 `[tournament monster]` 15 人轮战、`[result card]` 结算牌和轮次疲劳；4080 可完成，但不是完整大会体验。
3. **舰长室 501**：空起始房 + 61248 boss 的可通关占位，剧情对话/`[quest npc dungeon]` 语义未实现。
4. **NPC 语音重复**：服务端无语音包发送点，判断为客户端点击反馈。
5. **0.3% 挑战书**：按玩家描述恢复的全局掉率；若原版只在特定白图/远古图，需要再按副本白名单收窄。
6. **诺伊佩拉**：门票 4343 只从 4443 来；如果希望更易进入，可另加 NPC 商店/掉落，当前未改。

---

## 4. 复现命令

```bash
cd D:/70S3A3/Server-70s3a3
python scripts/check_repo.py
python scripts/build.py --smoke
# 离线验收（全源 + driver；用 Python 传参数，避免 Git Bash/MSYS 把 @rsp 路径改写）
python - <<'PY'
import glob, subprocess
src = sorted(glob.glob('src/Tools/VmTestKit/*.cs')) + [r'D:\70S3A3\local-run\driver\Fg365AcceptanceCheck.cs']
csc = r'C:/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe'
out = r'D:\70S3A3\local-run\build-staging-fg365\Fg365AcceptanceCheck.exe'
r = subprocess.run([csc, '-nologo', '-target:exe', '-main:Fg365AcceptanceCheck', '-out:' + out] + src,
                   capture_output=True)
print(r.returncode, (r.stdout + r.stderr).decode('utf-8', 'replace')[:2000])
PY
D:/70S3A3/local-run/build-staging-fg365/Fg365AcceptanceCheck.exe D:/70S3A3/Server-70s3a3/data
```

> 注意：Git Bash 调 csc 一律用 `-` 前缀，不要 `/`；`.rsp` 里的绝对 `D:/...` 路径在部分 csc 版本会被截成当前目录，直接传源列表更稳。
