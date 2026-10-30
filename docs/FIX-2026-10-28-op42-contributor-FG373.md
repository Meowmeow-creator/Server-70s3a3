# FIX 2026-10-28：暗影迷宫杀绿名怪后卡房间 / 无法进下一张图 —— FG373 op42 contributor 行不再拒杀

## 症状

玩家在暗影迷宫（`dungeon/act4/shadowmaze.dgn`，dungeon 37）地图 **7011**（11 只怪）杀掉绿名精英后，剩余怪清完也开不了门，只能放弃（日志 `20:12:22 op45 abort-dungeon`）。

## 取证（`local-run/logs/channel-evidence.log`，20:11 那局）

- 该房间固定 11 只（`Forest-Content-FG75.cs:2588`，`{601,603,601,600,601,1098,600,603,602,601,602}`），服务端只提交了 8 只 `KILL_COMMITTED`（uid 100/101/102/103/107/108/109/110），uid **104/105/106** 三次 `REJECTED opcode=42 reason=Batch death contributor profile`。
- `RoomCleared` = `dead[room].Count >= TotalCount`（`Dungeon-Flow-FG.cs:166`），停在 8/11 → `Move()` 抛 `Dungeon room admission`；客户端又在等服务端 op38 死亡确认，所以连 op48 移动请求都不发（20:11:31 之后无 op48/op29）。
- 三个被拒包离线解密（会话密钥即固定诊断密钥 `keys[i]=i`，`Offline70TownProtocol.Validate` 也强制它；MD5 折码全部校验通过）：

  | 受害者 | attacker | contributor 行 |
  |---|---|---|
  | uid 106 | 8（玩家） | `skill=0x211 uid=105 dmg=78 hit=1`、`skill=0x111 uid=8 dmg=48291 hit=1` |
  | uid 105 | 8 | `skill=0x211 uid=104 dmg=269 hit=2`、`skill=0x111 uid=8 dmg=50201 hit=1` |
  | uid 104 | 8 | `skill=0x211 uid=105 dmg=81 hit=1`、`skill=0x111 uid=8 dmg=51283 hit=1` |

  即 **怪打怪**（1098 与旁边 600/601 互伤），客户端把另一只怪也写进 contributor 列表。

## 根因

`Batch-Player-Protocol-FG.cs` 旧解析把每条 contributor 行首的 u16 当成固定标记，`!=0x111` 就整包 `throw`。而原生 `DisPatcherCore/dispatch_sig_16.inc:293`（ORIG 0x081eb0c4）是

```c
if (die.m_skillList[i] != 0x111) continue;
```

行布局是 `(skillId u16, uid u16, damage u32, hitCount u16)`：`0x211` 只是另一只怪的技能号，非 `0x111` 行在原生侧**跳过**而不是判包非法。把"跳过"升级成"拒收"→ 击杀永不上账 → 白尸锁房间。

同一晚 20:06:46 也有一包被拒（uid 105），但客户端 6 秒后重发了一版只带玩家行的包，房间才侥幸清掉。

## 改动

`src/Tools/VmTestKit/Batch-Player-Protocol-FG.cs`（`BatchDeathRequestFG`，行 93-110）：

1. contributor 行按原生规则处理：`skill!=0x111`、非本队 owner、重复 uid **一律 continue**，不再拒包；`hitCount==0` 也不再拒（原生是记 0x6e 并置 1）。
2. 怪物可以作为 announced attacker（怪打怪的合法形态）：actor 门禁在 owner 键之外接受存活房内 uid `100..163`。
3. **无人头行击杀也放行**（原 `Batch death owner absent from contributors` 删除）：单机下宁可给一只怪的掉落/经验，也不能把房间锁死。经验/掉落仍走原路径、victim roster 仍是真正的门。
4. 仍**必须**拒绝的：帧长/折码/填充、受害者 uid 不在 `100..163`（GSD 900 除外）、`attacker=0xFFFF` 哨兵却带 contributor 行。
5. 证据日志：无人头行时 `LastDeathTailFG` 追加 `foreign=True attacker=<id> rows=<n>`，出现在 `KILL_COMMITTED` 行里。

`research/tower/src/Tools/VmTestKit/Batch-Player-Protocol-FG.cs` 同步同一语义（研究树另有 room-flag 校验，未动）。

## 验证据（全部离线，未起服、未碰 `build/stable`）

- `python scripts/check_repo.py`：`repositoryChecksPassed`，428 文件 / 194 稳定源。
- 稳定源 194 源编译 `DNF70-Channel-FG.exe` / `DNF70-Entrance-FG.exe`：rc 0；无参 smoke 均返回 2（输出在 `local-run/build-verify-fg373/stable/`）。
- `python scripts/build.py --research --smoke`：152 源编译 + smoke 通过。
- 新 driver `local-run/driver/MixedContributorCheck-FG373.cs`：**14/14**，含三份真实被拒包的原始密文（还原在 `local-run/build-verify-fg373/captures/`）+ 11 个合成用例（怪打怪、无人头行、怪物 attacker、重复行、hit=0、APC owner、无 attacker 哨兵放行；越界 uid、哨兵带行、折码、填充仍拒）。
- 回归：`GhentBatchCheck-FG362` 120/120、`Batch2QuestDropsCheck-FG360` 78/78。

## 生效方式

需停服重编重启（`build/stable` 被运行中进程锁住）：停服 → `python scripts/build.py --smoke` → 起服 → 重开客户端。卡死的那一局已放弃回城，无法补回，重进暗影迷宫即可。

## 同类残留（未改，留档）

- 若某只怪只被**宠物/召唤物**打死、且客户端把该 act 写成 `>=164` 的 uid，行会被当作 owner（`FriendlyDeathOwnerFG140` 的 `owner>=164` 兜底），行为与本次修复一致，不会锁房。
- `ApcUidFG186` 侧通道 APC 的 uid 若超过 163（`100+TotalCount+index`），仍会被受害者 uid 门禁拒掉；当前房内怪物数 ≤ 20，实际不会触发，但要扩容房间时必须同步放宽。
