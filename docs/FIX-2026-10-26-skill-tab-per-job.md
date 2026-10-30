# 2026-10-26 技能分页按职业取（铁山靠/霸体护甲错页）

## 现场

- 角色：男格斗（job 7，`atfighter`），Lv.70，已转职。
- 技能栏（K）里 **铁山靠** 与 **霸体护甲** 学完之后出现在「普通」页，而这两个技能 PVF 里
  `[skill fitness growtype]` 只写了一个 grow（`[growtype maximum level] 0 0 30 0 0 0` /
  `0 0 20 0 0 0`），属于「转职后某分支才可学」，应该在「转职」页。
- 同一份表还让另外八个职业各自错开：审计结果见下。

## 根因

客户端把已学技能画在哪一页，只看服务端发的 **slot 号落在哪个区间**（`SkillSlot/addSkillOnCreateCharacter.inc`
的 g0 `[6,54)` / g1 `[54,102)` / g2 `[102,150)` / g3 `[150,198)`，即 普通/转职/通用/特性）。
选 slot 就是选页。

`Skill-Placement-FG123.cs` 的 `ListStartFG123(byte id)` 是一张**只按 skill id 索引**的表，而它是从
鬼剑士实采来的。技能 id 在 PVF 里是**按职业各自编号**的（`find_skill(job, index)`，见
`skill/<job>skill.lst`），所以这张表只能回答鬼剑士：男格斗的 id 1/20 撞上鬼剑士的 id 1/20，被分到
了「普通」。同一假设还造成两类后果：

- 表里没有的 id 直接 `throw "Skill placement outside original SP catalog"` —— 非鬼剑士的觉醒技能
  （如 atfighter grow 3/4 的 115/116）和全部特性技能都落不了位。
- 表里有但语义不同的 id 静默走错页。

原版算法在 `CSkill/reform_ui_group_no.inc`（`CListTables/init.inc:105-107` 调用）：

```
[feature skill type] != 0            -> g3 特性
[skill class] 0..3:  fitCount > 2    -> g0 普通   （整条大系都能学）
                 else fitCount <= 2  -> g1 转职   （只有个别分支能学）
[skill class] 4                      -> g2 通用
```

`fitCount` = `[skill fitness growtype]` 里整数的个数。

## 代码修改（稳定源，166 源编译通过）

1. `Skill-Rules-Jobs-FG203.cs`（`local-run/pvf/gen_skill_rules.py` 重新生成）
   每行新增三个分页输入：`SkillClass`=`[skill class]`、`Fitness`=`[skill fitness growtype]` 条目数、
   `Trait`=`[feature skill type]`。原有 1637 行的其余字段逐字段与旧文件一致（脚本比对 0 处差异）。
2. `Skill-Rules-FG38.cs`：`Offline70SkillRuleFG` 加这三个字段。手写鬼剑士表只作为生成器的
   字段基线、运行时不再被读，所以这三项留空并注明。
3. `Skill-Placement-FG123.cs`
   - 新增 `TabBaseFG273(id)`：读 `Rule(id)`（已经是按职业查的）套上面的规则。
   - `ListStartFG123(id)`：鬼剑士仍优先用实采表（改名 `CapturedSwordmanStartFG123`），
     表里没有的 id 以及其它八个职业一律走 `TabBaseFG273`。
   - 删掉 `PreJobFG123`/`PreJobSkillFG123`/`ListSlotStateFG123`：那张表是“未转职角色看不到转职页”
     的补救，而按规则重算后九个职业**一条都不需要**（没有任何技能既 `[skill fitness growtype]`
     含 grow 0、又条目数 ≤2）。`FindListFG123` 随之退回单趟查找。
   - 新增 `ReadUnplacedFG273`：`NormalizePlacementFG123` 改用不带分页断言的读档，否则老档里
     停在旧页的被动技能会先被 `Validate` 判成 `Passive skill placement changed` 而读不进来。
4. `Batch-Service-FG.cs`：`Sync(bool)` 在 `NormalizeProfessionFG139` 之前先
   `NormalizePlacementFG123(skillPath)`。构造期那次只覆盖 role 1，切角色的路径由 Sync 兜住。
5. `Contract-Cargo-Skills-FG129.cs`、`Starting-Skills-Protocol.cs`：跟随 `ListSlotFG123` 改名。

## 证据

规则侧（`local-run/pvf/skill_tab_plan2.py`、`audit_skill_tab.py`，走 27000 的 PVF HTTP 只读接口，
1637 个 `.skl` 全部取到）：

- 九个职业创建包里 **71 条带页槽位**（slot≥6）全部被规则复现，0 处不符。
- 鬼剑士实采表 121 条里规则复现 119 条；`43 jumpattackhighfast`、`197 conversion` 实采是转职、
  规则算普通，**保留实采**（鬼剑士因此零行为变化）。
- 改前基线：1637 个 (职业, id) 里 **594 个会抛**、另有 **249 个落在错页**（job0 2 / job1 31 /
  job2 34 / job3 31 / job4 37 / job5 34 / job6 32 / job7 30 / job8 20，其中 job0 的 2 个不改）。

运行侧（`local-run/driver/SkillTabCheckFG273.cs`，与服务端同源编译，只写 `%TEMP%` 临时档，
不开 socket、不碰在线存档）14 项全 PASS：

```
九个职业 1637 行 unresolvable=0；job 7 分页计数 19/95/34/54 与 PVF 审计一致
鬼剑士 197/43 仍在转职、190 仍在通用、85 从抛异常变为可落位、250 仍拒绝
男格斗 grow2 实学：霸体护甲 slot 55、铁山靠 slot 56（转职页）
        conversion slot 11、suplex slot 10（普通页）、armormasterylightsk slot 109（通用页）
老档迁移：把铁山靠塞回 6..53、conversion 塞回 54..101 后，Load 仍报
        "Passive skill placement changed"，NormalizePlacementFG123 把它们分别搬回 56 / 11，
        重写后的档能正常 Load，再跑一次不再移动任何东西
```

## 部署

**未部署。** `build/stable/DNF70-Channel-FG.exe`（PID 9384）与 `DNF70-Entrance-FG.exe`（PID 41156）
正在运行，`build.py` 覆盖产物报 `CS0016`，因此本批改动的验证产物在
`local-run/build-staging-fg273/`（两个 exe + 驱动，166 源 0 error）。等腾出环境后按
`local-run/one-click.ps1 -Build` 走停服 → `build.py --smoke` → 拉起 → 端口自检，再进游戏开一次
技能栏确认。

## 边界

- 玩家视角：改完部署后，男格斗的铁山靠/霸体护甲会自己挪到「转职」页，其它八个职业同理会各归各位；
  以前一点就报“落不了位”的 594 项（非鬼剑士觉醒技能、各职业特性技能）现在能发得出去。
- 「特性」页（150..197）这次才第一次真正被用上，**实机显示未验**：范围来自
  `addSkillOnCreateCharacter.inc` 与 `Starting-Skills-Protocol.cs` 里 `FindSlot` 的既有注释，
  但没在客户端截图核对过。
- 只改分页，没动 SP 价格、可学判定与赠技清单；`Learn` 的 `Cost==0` 闸门仍在，所以特性技能不会
  因此变成可用 SP 学习。
- 老档由 Sync 自动重排槽位，不回改 SP 账本；重排只动 slot，不动 id 与等级。
