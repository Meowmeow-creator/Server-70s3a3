# 2026-10-26 技能无色小晶块消耗（FG346）

## 需求

PVF 的技能脚本里凡是带下面这种段的，使用时都扣无色小晶块（物品 3037）：

```
[consume item]
	3037	2	2
```

三列是 `物品 id / 1 级数量 / 满级数量`。全量普查：1713 个 `skill/*.skl` 里 262 个带 `[consume item]`，
其中 229 个是 3037（另有 4 个召唤类脚本用 `0 110 400` 这种占位行，不计）；9 个职业的技能表
（`skill/<job>skill.lst`）合计 **222 个技能**要扣无色。3037 的行两列恒等（1/1、2/2、3/3、5/5、7/7），
召唤系一个脚本有三行（3+1+1），客户端逐行扣，所以按行和算。

例（鬼剑士）：拔刀斩 1、幻影剑舞 2、崩山裂地斩 2、魔狱血刹 5、极·鬼剑术(暴风式) 5、暗天波动眼 7；
后跳/上挑/武器奥义这类没有该段，不扣。

## 根因

客户端是施放的权威方。CMD 41 `USE_SKILL` 的包体（rednf `DisPatcher_UseSkill/read` 0x081cad6d：
`{u8 gap, u8 skillId, u16 target, ...}`，服务端 process 只做 `IsValidUsingSkill` 统计和队伍广播）
不带物品信息，原服务端也没有任何“技能扣物品”的落点——`STSkillScript::m_useItems11c` 在 rednf
里只有 `ImportSkillScript` 写入和 `sync_script` 打印两处消费者。离线服务端此前把 op41 整个记为
`UNIMPLEMENTED`，所以存档里的无色永远不减（客户端本地扣了也会在重登时被存档覆盖）。

## 实现

- `src/Tools/VmTestKit/Skill-Consume-FG346.cs`
  - `Offline70SkillCubeFG346.Skills`：9 个职业的 (技能 id, 无色数量) 表，222 条，由
    `skill/<job>skill.lst` × 各自 `.skl` 的 `[consume item]` 段生成。
  - `Cost(job, skill)`、`Parse(op41 body)`（41%14=13 → Anubis 族，明文取 `[1]`=技能 id、
    `[2..3]`=目标）、`Owned/Take`（背包 list 0 的多堆按槽位先扣，扣空移除）、
    `UpdateReply`（op14 单行绝对数量，用于客户端主动发 op47 时回推权威值）。
- `Batch-Service-FG.cs`
  - 分发表新增 `if(op==41){SkillUseFG346(body,sequence);return;}`。
  - op47 分支：`r.ItemId==3037` 走 `SkillCubeConfirmFG346`——回 `item-used` 成功 + op14 权威数量，
    **不再扣**（扣账由 op41 负责，避免同一枚扣两次）。

行为约定：

- 扣账用 `Offline70Economy.Apply` 落每日志（`session-skill-cube-<seq>`），同一 sequence 重放不重复扣。
- 扣完发 op14 增量（`InventoryChangeReply`，与分解/复活/结算同一条通道），客户端本地扣没扣都会收敛到存档值。
- 存档比客户端少时只扣现有的（`SKILL_CUBE_SHORT` 记 0 条），不把已经播出去的施放判死。
- 尸体施放（`playerDeadFG117`）不扣；无 `[consume item]` 的技能只记 `SKILL_USED ... cube=0`。

## 取证链

- PVF：pvfUtility HTTP API 批量取 1713 个 `.skl`；表在 `local-run/scratch/consume-item/`（普查脚本产物）。
- op41 布局：用 0..333 固定密钥把 `local-run/logs/archive/*` 里 182 个真实 op41 包体（16 字节）
  按 Anubis(13) 解出来全部是 `00 <技能id> <目标 u16> <4 字节> <2 字节> 0000`；抽到的技能 id
  与当时角色的职业技能表逐条对得上（如 0xA9=后跳、0x43=里·鬼剑术、0x14=地裂·波动剑、
  0x12=鬼影步；job 8 的 0x14=瞬移、0x1B=火柱），且当时这些会话里没有一个 op41 命中无色技能表，
  与“日志里 0 条 op47”一致。

## 验收

离线（`Skill-Consume-Fixtures-FG346.exe`）：32 项全过——表值（含召唤 3+1+1、7 枚上限、未知职业/技能=0）、
op41 加解密往返、多堆扣减顺序、op14 行内容（含扣空后的 gone 行）、收据幂等与不足时只扣剩余。

隔离服务（17003 + 一次性 store 副本，未动线上 7002 存档）：

```
SKILL_CUBE_USED job=0 skill=85 target=1500 cost=5 taken=5 remaining=1095
SKILL_CUBE_USED job=0 skill=9 target=0 cost=1 taken=1 remaining=1094
SKILL_USED job=0 skill=169 target=0 cube=0
SENT item-used type=1 opcode=47 bytes=31
SKILL_CUBE_CONFIRM slot=106 remaining=1094
```

存档：槽 106 由 1000 → 994（5+1），槽 107 仍 100，日志里两条 `skill-cube` 收据，Revision 1042→1044。
op47 确认没有二次扣减。

## 未做/注意

- 客户端若在本地已经没有无色时仍发出施放（正常会被客户端自己拦住），服务端只扣现有数量，不会回滚施放。
- `[consume item]` 目前只有 3037 一种物品；表结构是 (id, count) 对，后续出现别的物品直接扩表即可。
- 本次没有真人实机验收：部署后第一次进副本用无色技能，确认左下角数量立即下降、重登后不复原。
