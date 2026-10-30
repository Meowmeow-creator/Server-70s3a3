# 2026-10-26 觉醒后技能学不了（60/70 级、一觉主动/被动全职业被拒）

## 现场

- 角色：role 8，男魔法师（job 8）冰结师分支，Lv.70，grow 字节 18（firstGrow 2 + 一次觉醒 1）。
- 2026-10-26 12:14 完成一次觉醒后点 60/70 级技能，服务端日志（`local-run/logs/channel-evidence.log`）：

```
12:14:30.377 SKILL_LEARN_REQUEST bank=0 id=60 increment=1
12:14:30.383 SKILL_LEARN_REJECTED Skill level, role level or SP requirement rejected
12:14:36.552 SKILL_LEARN_REQUEST bank=0 id=50 increment=1
12:14:36.559 SKILL_LEARN_REJECTED Skill level, role level or SP requirement rejected
```

- 同一批里 `id=8`、`id=10` 都 `SKILL_LEARN_COMMITTED`，SP 还剩 6330，等级 70 —— 不是 SP、不是等级、
  也不是觉醒没生效；客户端按 PVF 允许点击并发包，是服务端单方面拒绝。

## 根因

CN70 的 .skl 有**两张**等级上限表：

| 标签 | 列 | 含义 |
|---|---|---|
| `[growtype maximum level]` | 6 列，列号 = firstGrow 0..4，配合 `[skill fitness growtype]` | 未觉醒（转职分支）上限 |
| `[second growtype maximum level]` | 12 列，**只有偶数列 2/4/6/8 有值，列号 = 2×firstGrow** | 一次觉醒之后的上限 |

`local-run/pvf/gen_skill_rules.py` 过去只读前一张表（`local-run/pvf/second-growtype-census.py` 全量普查
1637 个 .skl：**132 行的上限只存在于第二张表**，且没有任何一行同时带两张表的值）。于是这 132 行被生成成
`GrowCaps={0,0,0,0,0}`，`Offline70SkillSlots.JobMaximum(r,grow)` 返回 0，而 `LearnStateFG129`
（`Contract-Cargo-Skills-FG129.cs:85`）的第一条就是 `JobMaximum==0 → 拒绝`。

受影响的正好是每支转职线的四个技能：一觉被动（需要等级 48）、一觉主动（50）、60 级、70 级。
按职业分布（PVF 实测）：鬼剑士 16、女格斗 16、神枪手 16、魔法师 19、圣职者 17、女枪手 16、
暗夜使者 8、男格斗 16、男法师 8 —— 共 132 行（其中圣职者 id 120「Execution」`[purchase cost] 0`，
本来就不走 SP 学习路径）。

`Skill-Rules-FG38.cs` 里原先有一段注释把这归因成「QP 技能路径本构建未实现」，只对了一半：一觉被动确实
不靠 SP 从 0 买到 1（由 `.chr [awakening skill]` 免费发放），但 **60/70 级技能和一觉技能 1 级以上的上限
就在这第二张表里**，这段注释把两类技能一起误判成了"不可学"。

## 代码修改（稳定源，168→169 源，编译与 check_repo 通过）

1. `local-run/pvf/gen_skill_rules.py`
   - 新增 `positioned()` 按制表符**保列**解析（丢空 cell 会把后面的上限平移到别的分支）。
   - 读 `[second growtype maximum level]`，取列 `2*grow` 输出新字段 `AwakenCaps[5]`。
   - `Maximum` 改为 `max(growtype 前五列, AwakenCaps)`（只上调不下调：`Validate` 用它做存档上界，
     圣职者四行 req-99 的 10/10/10/20 保持不变）。
   - 守卫扩到 `awaken` 字段：`--check` 仍要求逐字段复现 `Skill-Rules-FG38.cs`。
2. `Skill-Rules-FG38.cs`
   - `Offline70SkillRuleFG` 加 `internal byte[] AwakenCaps=new byte[5];`（手写行默认全 0，行为不变）。
   - `JobMaximum(r, grow)` → `JobMaximum(r, grow, awakened)`：未觉醒只认 `GrowCaps[grow]`，
     觉醒后取 `max(GrowCaps, AwakenCaps)`。这正好还原原版闸门**60/70 级技能必须一次觉醒**。
   - 89/90/91/92 四行按实测补 `Maximum` 与 `AwakenCaps`，并就地更正上面那段错误结论。
3. `Skill-Rules-Jobs-FG203.cs`：重新生成 1637 行。字段级 diff 实测：`GrowCaps` 改动 0 行，
   `Id/Cost/Required/Step/Active/SkillClass/Fitness/Trait/Prerequisites` 一字未动，
   只有 132 行 `Maximum` 上调并全部带上 `AwakenCaps`。
4. 调用点传觉醒位：`Batch-Service-FG.cs:271` 两个 bank 都传
   `Offline70RewardTypesFG183.SecondGrow(...)!=0`；`LearnStateFG129 / LearnOneFG129 / LearnManyFG139 /
   Learn(...)` 签名加 `bool awakened`（`Starting-Skills-Protocol.cs`、`Contract-Cargo-Skills-FG129.cs`、
   `Promotion-Skills-FG139.cs`）。旧的 2 参 `JobMaximum` 已删除，不留兼容重载。

## 离线证据 `local-run/driver/AwakenCapCheck-FG286.cs` → `AWAKENCAP_CHECK_OK`（27 条断言全 PASS，13:05 重建后复跑）

- 冰结师（job 8 grow 2，Lv.70）：
  - 前置已补齐时，`awakened=false` 请求 id 60 仍被 `requirement rejected` 拒；同一状态
    `awakened=true` 立即通过 —— 差的就是觉醒位。
  - id 60 冰凌破 1→3 提交，第 4 级拒（PVF 上限 3，SP 3×80=240 记账一致）。
  - id 50 冰封奥义 1→8 提交（48+7×3=69≤70），第 9 级被等级门槛拒。
  - id 63 千里冰封：先报 `Skill prerequisite rejected`；补 前置 7 到 5 级后 1 级提交，第 2 级拒（上限 1）。
  - id 48 千旋冰轮破 1→11 提交（50+10×2=70），第 12 级拒；带契约 +5 时 1→13 提交、第 14 级拒
    （这就是 role 8 实机看到的数字）。
  - 回归：普通分支技能 id 23 在 `awakened=false/true` 下都照常学习。
- 鬼剑士 grow 1（剑魂）：id 98 未觉醒拒 → 觉醒后 1→3、第 4 拒；一觉被动 91 提到 4。
- 表不变式：1637 行全部满足 `JobMaximum(pre)==GrowCaps`、`JobMaximum(post)==max(GrowCaps,AwakenCaps)`；
  觉醒后才出现上限的行 = 132；两张表同列同时非零的行 = 0。
- 存档兼容 A/B：`ArchiveValidateProbe-FG286.cs` 分别编译进「改动前源」
  （`local-run/tmp/pre-fg286-src`，只回退本次 6 处改动）与「改动后源」跑同一份真实存档目录，
  两边输出逐行相同：8 份存档 7 份 OK、`skills-r9-EZ.xml` 同样报
  `Passive skill placement changed` —— 属改动前就存在的存档缺陷（另案），不是本次引入。

## 隔离服实跑 `local-run/driver/AwakenLiveCheck-FG286.cs`（17021 + 可丢弃存档副本，9/9 PASS）

真实 op31 打进 `local-run/awakencaps-check/build/DNF70-Channel-FG.exe`（13:05 重建的稳定源产物），日志
`local-run/awakencaps-check/logs/channel.log`：10 条 `SKILL_LEARN_COMMITTED`、4 条 `SKILL_LEARN_REJECTED`。

```
13:05:51 COMMITTED id=60 level=1 / level=2 / level=3   13:05:51 REJECTED requirement (第 4 级)
13:05:51 COMMITTED id=50 level=4                        13:05:51 REJECTED prerequisite (id 63)
13:05:51 COMMITTED id=7  level=5 -> id=63 level=1       13:05:51 REJECTED requirement (id 63 第 2 级)
13:06:03 COMMITTED id=22/31/16 前置 -> id=48 level=13   13:06:03 REJECTED requirement (第 14 级)
```

## 边界

- **只改服务端判定，没动客户端**：客户端本来就按第二张表放行，因此界面无需补丁。
- 未觉醒角色依旧学不了 60/70 级技能（与原版一致），本次没有放宽这条闸门。
- 一觉主动/被动的可用等级仍受 `Required+(n-1)*Step ≤ 角色等级(+契约 5)` 约束，上限值取自 PVF。
- 70 级技能普遍带前置（千里冰封要 id 7 到 5 级、千旋冰轮破要 16/31/10 各 5 级），玩家要先补前置，
  服务端会明确报 `Skill prerequisite rejected`。
- TP/特性（`Upgrade`）技能与本问题无关：它们走 `[growtype maximum level]`，`Cost=0` 不进 SP 路径。
- **部署不是本次做的**：按 `Server-70s3a3/AGENTS.md`「未明确授权不得启动/结束实际游戏」，我只把改动
  落进稳定源并编译到 `local-run/build-staging-awakencaps/`（12:34 与 13:05 两次，未覆盖运行中的
  `build/stable`）。13:04:03/13:04:05 由登录器侧重新构建并重启了 `build/stable` 两个 exe：
  `build-proof.json` sourceCount=169、smokePassed=true，进程 pid 6800 于 13:04:06 起，
  实测 `DNF70-Channel-FG.exe` 字节里含 `AwakenCaps` 元数据名 —— 修复已在跑的服上生效。

## 复跑

```
python D:/70S3A3/local-run/pvf/gen_skill_rules.py --check          # 守卫：drift 2 = 已允许的 step 差异
python D:/70S3A3/local-run/pvf/second_growtype_census.py           # 132 行只带第二张表上限
# 两个驱动都用 v4.0.30319 csc：稳定源 168 个 .cs + 驱动 .cs，驱动文件必须带 UTF-8 BOM
```
