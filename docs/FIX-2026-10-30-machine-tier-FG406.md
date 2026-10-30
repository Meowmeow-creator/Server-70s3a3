# FIX 2026-10-30：摆摊分解机等级改为跟副职业等级挂钩（FG406）

## 现场

玩家 role 8（`12345`，男法 Lv.70，分解师 `ExpertJobTypeFG183=3`）截图：副职业面板是
**Lv.1**，但摆在街上的分解机悬浮框写 **`Lv.5 中高级分解机`**（手续费 1 金币）。玩家要求
"摆摊的等级应该跟副职业等级挂钩"。

存档实测：`economy-account1-r8-FG.xml` → `Level=70 ExpertJobTypeFG183=3
ExpertJobExpFG183=4 ExpertJobEnduranceFG183=295`，即 exp 4 远低于第一档 224 ⇒ 副职业 **1 级**。

## 根因

- 客户端把 217（副职业信息）里的 **type3 第一个 dword 直接存进 `[char+0x2d0]`**
  （`0x47e941` 段 `get_dword -> [esi+0x2d0]`），这个值就是悬浮框/店名对话框读的
  "分解机等级"：`0x41cf00` 用 `[char+0x2d0]` 拼 `Lv.%d …`（字符串 id `0xb4e8`），
  `0xD58330` 店名对话框也拿它当名字长度上限。
- 原版这个 dword 是 `m_enchantCount + 1`（`SendExpertJobInfo.inc` case 3），即
  **机器档位 = 存档里的 grade(0 基) + 1**。
- FG380/FG381 为了修"30 级以上装备不能分解"，把 217 与 218 的数值都**写死成 5**
  （`Reward-Types-FG183.cs` 里 `w.Write((int)5)` 与 `level<=1?5:level`），
  于是不论副职业几级，悬浮框都显示 Lv.5。

## 原版语义（以 rednf 为准）

| 位置 | 语义 |
| --- | --- |
| `ExpertJobOnlyMgr/SendExpertJobInfo.inc` case 3 | `put_int(m_enchantCount + 1); put_int(m_endurance)` |
| `ExpertJobOnlyMgr/OnUpgradeDisjointMachine.inc` | `if (grade + 1 >= expertLevel) return 0x13;` —— 机器档位**永远不超过副职业等级** |
| `CUser/GetCurExpertJobLevel.inc` | 等级 = 首个 `>` exp 的 `[expertness exp]` 行下标 +1（否则 9）；exp 恰在阈值上时查 `[expertjob level limit]`（1..9 → 角色 20/20/30/30/40/40/50/50/60），角色等级不够再 −1 |
| `STExpertJobOnlyOrig/GetDisjointMachineRepairCost.inc` | `row.m_field0 - row.m_field0 * endurance / row.m_field4`（`[endurance repair cost]` 按 `grade+1` 取行） |

本引擎没有 CMD 244（金币升机器），所以机器直接停在原版允许的上限：**档位 = 副职业等级**，
grade = 等级 − 1。

## 改动

1. **新增** `src/Tools/VmTestKit/Expert-Level-FG406.cs`
   - `[expertness exp]` 9 档、`[expertjob level limit]` 9 档、`[endurance repair cost]` 9 行
     （全部取自 `local-run/pvf/raw-r4/character_expertjob_disjointer.exj.txt` 与
     `character_expertjob.etc.txt`，不是编造值）；
   - `Level(type,exp,charLevel)` = `GetCurExpertJobLevel`；`Grade(level)=level-1`（0..8）；
     `MachineLevel(level)=grade+1`；`RepairPrice/RepairFull` = 上表公式。
2. `Reward-Types-FG183.cs`
   - 217：type3 的 dword 由 `ExpertJobInfoBody(...,int machineLevel)` 传入（1..9 越界即抛），
     不再写死 5；
   - 218：尾部 dword 语义更正为**手续费**，删掉 `level<=1?5:level` 的改写。
     依据：参照 `put_disjoint_machine_info` 写的是 `+0x1c`（`get_cost()`），
     `OnCreateDisjointStore` 拿这个入参跟 `[limit disjoint gold]=10000` 做范围校验；
     客户端把它存到 `machine+0x20c`（setter `0xdb4cd0`），`0x6c81af` 用它拼"手续费"文案，
     `0xdb4e46` 又把它原样写回 op250 放置包。
3. `Expert-Machine-FG183.cs`
   - 登录/刷新 217：档位 = `MachineTierFG406(assets)`；登录时耐久上限取当前档位的满值；
   - op250：解析 `{nameLen,name,fee,x,y}` 里的 fee（越界回落 1）并回填 218；
   - op233：该对话框路径不带手续费，按客户端默认 1；
   - op252 修理：按 `grade+1` 行算价（满耐久=0 金币），修满到该档满值。
4. `Economy-State-FG.cs`：`ExpertJobEnduranceFG183` 上限 300 → 500（`[endurance repair cost]`
   第 8/9 行满值是 400/500，档位 7+ 才用得到）。

## 验收

`local-run/build-verify-fg406/ExpertLevelCheck-FG406.exe`（41 断言全过，`RESULT EXPERT_LEVEL_FG406 OK`）：

- 等级表：exp 0/223→1，224@角色20→2、@角色19→1，449@29→2、@30→3，899@40→5，
  1349@50→7，1799@59→8、@60→9，2054→9，超上限→9，无副职业→0；
- 档位：1 级→tier 1/grade 0，5 级→5/4，9 级→9/8；
- 217 body：type3 = `00 03 <tier> <endurance> <exp>`，tier 1/5 两例，tier 0/10 抛异常；
- 218 body：uid/name/vill/area/x/y/fee 逐字段，fee 1 原样回填（不再被改成 5），
  fee 10000 收、10001 与负数拒；
- 修理价：grade0 满耐久 0、295/300→167、150→5000、0→10000，grade4 半血 15000，
  grade7 300/400→12500；
- **真实存档副本**：role 8 → `charLevel=70 expertExp=4 endurance=295` ⇒ tier **1**，
  登录 217 的 tier/endurance/exp 三字段与存档一致，exp 到 224 会变成 tier 2。

回归：`Triage-FG381-Check` 27/28，唯一 FAIL 是 **"quick-bar synthesizer relocated to the
consumable page slot 3"** —— 该断言在 FG374（徽章 Kind9 / 副职业材料 Kind10 页表）之后就已经过期
（`NormalizeEmblemExpertSlots` 现在只搬 Kind9/10，`git show HEAD` 里也没有 Kind1 分支），与本改无关。

构建/部署：`check_repo.py`（549 文件 / 203 稳定源）→ 停双服 → `build.py --smoke` →
`restart-detached.ps1`（7002/17001 监听正常，`oneHitKill=0` 与玩家上一局一致）。

## 剩余风险

- **机器能不能拆高等级装备没在本轮复核。** FG381 声称"客户端按这个值设可分解装备等级上限"，
  但客户端全镜像里读 `[char+0x2d0]` 的 dword 只有 6 处（`0x41860d/0x418768/0x418aec/0x4191ac/
  0x41d107/0x41d518`），没有一处落在物品判定链上；而 10-28 FG92 事故里 grade=1 时客户端照样把
  35 级太刀/腰带的 op28 发出去了。若玩家实测 Lv.1 机器拒收高等级装备，那是客户端本地闸，
  需要另外找地址；服务端 op28/op251 都没有等级闸。
- 机器档位不再有金币升级（CMD 244 仍未实现），档位随副职业经验自动上浮；
  副职业经验只从 op251（自己的机器分解）获得（`[gain exp] 0..1` + 物品附加经验），
  与原版一致 —— 想升档就要用机器拆低等级装备攒 exp。
- 客户端分解机窗口的"预计产物"按机器 grade 查 `[disjoint result]` 表，而服务端 op251 仍复用
  op28 的稀有度系数（不含 grade），预计值与实得可能差一档（既有简化，未在本轮处理）。

## 追加（同日二）：分解获得经验在界面上不涨

玩家反馈"使用分解机分解没有获得经验值"。服务端日志与存档证明经验**一直在加**：

```
15:56:32 EXPERT_MACHINE_DISJOINT_COMMITTED slot=17 ... expGained=1 expertExp=5
15:56:36 ... expGained=2 expertExp=7
15:57:18 ... expGained=1 expertExp=17
economy-account1-r8-FG.xml: <ExpertJobExpFG183>17</ExpertJobExpFG183>
```

问题是**客户端界面拿不到更新**：

- 217（副职业信息）的 type3 分支在客户端只读两个 dword（`0x47e941`：`[char+0x2d0]` 档位、
  `[char+0x2d4]` 耐久），**不读经验**；FG183 时代的 cave 把经验写到 `[char+0xE81]`，
  但全镜像搜 `81 0E 00 00`（`[reg+0xE81]` 的 disp32）只有 cave 自己那一条写、没有任何读
  ⇒ 那个字段是死字段（+0xE81 还不是 4 对齐，基本是 padding）。
- 原版的经验是走**角色基础信息**（op2 subtype 1 = `make_basic_info`）下发的：
  `CExpertJob::IncreaseExpertJobExp` 每次经验变化都发 `put_header(0,2)` + `make_basic_info`，
  而 `make_basic_info.inc:249-250` 正是 `put_byte(GetCurCharacExpertJobType())` +
  `put_int(GetCurCharacExpertJobExp())`。我们的 `AppearanceRecord` 也写了这两项
  （`expertAt=55+nameBytes+14*rows`），但**只在登录/换装时发**，分解后没发 ⇒ 面板停在登录值。

修复：`Expert-Machine-FG183.cs` 的 op251 成功分支在 217 刷新之前补一次 `Appearance()`
（= op2 basic-info），授职分支（`Batch-Service-FG.cs` 的 `EXPERT_JOB_AWARDED`）同样补发。
这与原版 `IncreaseExpertJobExp` 的发送时机一致。

驱动新增断言（`ExpertLevelCheck-FG406`）：`AppearanceRecord` 的 `expertAt` 处
type=3、exp=17，且记录长度足够 —— 40 断言全过，`RESULT EXPERT_LEVEL_FG406 OK`。
