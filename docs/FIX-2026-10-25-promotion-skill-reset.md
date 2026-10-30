# 2026-10-25 首次转职重置技能与返还 SP

## 现场

- 角色：role 8，`12345`，job 8（男魔法师），Lv.70。
- 22:57:27 完成任务 4068（grow 奖励）后日志：
  `JOB_PROMOTED grow=2 ... spentSp=275 visibleSkills=22`。
- 客户端表现：技能没有被清空，SP 没有退回。存档实值 `SpentSp=275`、22 个可见技能。

## 根因

技能档的 `ProfessionFG139` 只在 `Offline70SkillSlots.NormalizeProfessionStateFG139` 里从 0
改成新分支，并补发该分支的 `[growtype]` 赠技，但**没有把已学技能退回本职业创建基线，也没有把
`SpentSp` 归零**。因此任务结算只改了 economy 的 grow 字节；技能与 SP 账本原样保留。

原版行为是首次转职清空已学技能、全额返还已花 SP，让玩家在新技能树上重新分配。

## 代码修改（稳定源，163 源编译通过）

1. `Promotion-Skills-FG139.cs`
   - `NormalizeProfessionStateFG139`：在 `ProfessionFG139==0 && grow!=0`（首次转职）时先调用
     `ResetPromotionFG139`，再写入新 ProfessionFG139 并补发分支赠技。重置与 ProfessionFG139
     在同一次技能档提交里落盘：崩溃只会留下转职前状态，下次 Sync 重试，不会出现半重置。
   - 拆出 `GrantBranchSkillsFG139`；免赠等级的 SP 记账从写死的 11 改为
     `StartingIdsFG202(CurrentJob).Length`（枪手 10、男法 12）。
   - 新增 `ResetPromotionFG139`：按本职业创建行重建技能（`Default()`），仅保留转职后规则仍
     接受的原槽位（主动技能可在任意 active 槽；初始被动回到创建表钉死的位置），`SpentSp=0`。
2. `Skill-Reset-FG83.cs`
   - `ResetPlanFG83`（洗点瓶）复制原槽位的循环从 `SkillIds.Length`（写死 11）改为目标基线长度；
     修复枪手 10 行越界、男法 12 行被 outbox 拒绝的问题。
   - 新增 `InitialIdsFG83`，outbox 校验不再写死 `Ids.Length==11`，改判“是否某个职业的创建行”，
     避免经济档在校验其它角色时错误依赖当前绑定的 job。
3. `Skill-Placement-FG123.cs`
   - `NormalizePlacementFG123` 的初始被动判定同样改为按本职业创建行长度。

## 离线证据（`local-run/driver/PromotionResetCheck-FG205.cs`）

驱动用服务端同一批类编译运行（不开 socket、不碰在线存档）：

- 男法 fixture（转职前 17 技能、8/18 等级 4、26 等级 2、`SpentSp=275`）→ grow 2：
  `SpentSp=0`、可见 17、剩余 SP 7470、ids/slots/levels 与预期逐字节一致、二次 Sync 无变化。
- 鬼剑 fixture（初始被动 179 升 2 级 + 学会 27）→ grow 1：SP 全退、179 回 7 级、27 作为分支
  免赠技能保留 1 级。
- 枪手（10 行）/男法（12 行）洗点计划与 outbox 校验均通过。
- 文件级路径（加锁 → 提交 → 重载）通过，并留下 `.backup-*`。

## 在线存档修复（未重启服务端）

用同一套类对 `local-run/DNF70-batch-test-FG/skills-r8-EZ.xml` 执行一次转职重置：

```
before: spentSp=275 visible=22 remaining=7195
after : spentSp=0   visible=17 remaining=7470
ids   : 33,181,182,184,187,179,1,38,26,169,170,174,35,17,21,37,197
```

修复前副本：`local-run/tmp/fg205-repair/skills-r8-EZ.pre-fg205.xml`（另有 `CommitResetFG83`
生成的 `.backup-*`）。修复后 7002 连接保持 ESTABLISHED，日志无断线。

## 部署（2026-10-25 23:14，用户要求重启）

按 `local-run/one-click.ps1 -Build` 走标准流程：停旧服 → `build.py --smoke`（163 源，编译+无参冒烟通过）
→ 归档旧日志 → WMI 分离拉起 → 端口自检。新进程：频道 29944、入口 44928，127.0.0.1:7002 / :17001
均监听。登录驱动复测 `LOGIN_OK success=1`、`ROSTER_SENT roles=8 degraded=0`；role 8 修复后的技能档
哈希在启动后未变化（启动 Sync 不会动已就绪的档案）。运行中的 exe 已替换为带本修复的产物，
后续所有首次转职都会重置技能并返还 SP。

## 边界

- 客户端需要重新进一次角色（本次重启已自然断开旧会话）；服务端存档已是正确值。
- 只修了本次上报的 role 8。其它历史已转职角色（role 1/9/12 等）未回改，避免误动已重新分配的 SP。
