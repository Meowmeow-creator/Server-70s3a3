# 交接：经验加成秘药（远古精灵的秘药系列）完全未实现 — 2026-10-26

> 状态：**未修**。用户实测“远古精灵的秘药”使用后没有出现经验 buff，经取证确认服务端在扣道具之前就拒绝了请求，道具未消耗、经验未加成、也没有任何 buff 状态。本文记录现象、完整证据链、根因、影响面、推荐实现方案与验收标准，供后续一次做完。

## 0. 一句话现状

- 用户实际使用的是 **item 2682617「远古精灵的精华秘药 (小)」**（60 分钟、1.5 倍经验、仅地下城可用），不是老版 7440。
- 服务端收到 op47 使用请求后立即回 `item-use-failed`，日志原因固定为 `Item use effect has not been implemented`；道具仍在背包（数量 1）。
- 根因是三层同时缺失：元数据没有经验倍率字段、`Use()` 只放行 HP/MP 回复类消耗品、通关结算没有任何经验倍率参与（也没有 buff 持久化与显示包）。
- 当前角色 r8 已是 70 级满经验（348386504 = 70 级封顶），即使现在补上实现，在这只角色上也看不到经验收益；验收必须换未满级角色。

## 1. 现象与复现

操作：在副本内（日志 dungeon=5）打开背包，对 slot 3 的秘药点“使用”。

用户可见结果：

- 没有出现任何 buff 图标/剩余时间；
- 通关后经验没有变化；
- 道具没有消失（说明客户端收到了失败回包，不是“用了但没显示”）。

服务端日志两次记录同一现象：

```
2026-10-26T01:53:46.670  FRAME opcode=47 bytes=29 seq=82
2026-10-26T01:53:46.679  SENT item-use-failed type=1 opcode=47 bytes=31
2026-10-26T01:53:46.679  REJECTED opcode=47 reason=Item use effect has not been implemented

2026-10-26T12:07:32.862  FRAME opcode=47 bytes=29 seq=63
2026-10-26T12:07:32.872  SENT item-use-failed type=1 opcode=47 bytes=31
2026-10-26T12:07:32.872  REJECTED opcode=47 reason=Item use effect has not been implemented
```

日志位置：`local-run/logs/channel-evidence.log`（12:07 这次）与 `local-run/logs/archive/20261026-020628/channel-evidence.log`（01:53 这次）。

## 2. 证据链（精确到字节）

### 2.1 请求解密结果

op47 请求体 16 字节，由角色协议 XTEA 加密（key = 0..333 字节，取偏移 172 起的 16 字节；解密时 **sum 必须每个 8 字节块重置为 0xc6ef3720**）。两次请求解密后一致：

| 字段 | 12:07:32 | 01:53:46 |
|---|---|---|
| 背包列表 | 0（普通背包） | 0 |
| 槽位 | 3 | 3 |
| InstanceChecksum | 7065 | 7065 |
| **item id** | **2682617** | **2682617** |
| tick | 951911 | 896940 |

原始密文文件：

- `local-run/logs/channel-evidence.log.request-334-47-317850fca3404b15a20e901a54c3f3cd.bin`
- `local-run/logs/archive/20261026-020628/channel-evidence.log.request-99-47-cedd0beb64c74507a0d9d423dbca2ff3.bin`

解码脚本见第 9 节。

### 2.2 存档侧

`local-run/DNF70-batch-test-FG/economy-account1-r8-FG.xml` 中该物品仍在：

```
<List>0</List><Slot>3</Slot><ItemId>2682617</ItemId><Quantity>1</Quantity>...
```

角色状态（同会话日志）：`WORLD_RESTORED level=70 experience=348386504`，而 `Offline70Progression-FG.cs` 的满级阈值 `Thresholds[68] = 348386504`，即经验已封顶。

## 3. 根因

### 3.1 PVF 里它是什么

`stackable/cash/chn_20130108_Red_Nostrum_of_Growth/chn_Red_Small_Nostrum_of_Growth.stk`（id 2682617）：

```
[name] 远古精灵的精华秘药 (小)
[explain] 使用后，在60分钟内通关任意地下城，可获得1.5倍的经验值；该效果不会因角色在地下城中死亡、返回城镇或重新登陆游戏而消失；
          该秘药只能在地下城中使用，且开启双倍经验活动时效果会重叠；但通关亡者峡谷地下城时，不会额外增加经验值。
          (每天可以使用2次；该道具将于2013年4月30日凌晨6点全部删除)
[minimum level] 1
[stack limit] 3
[stackable type] `[waste]` 0
[cool time] 3600000
[cooltime group] 99
[stat change duration] 3600000 `myself`
[exp bonus rate] 0.5
```

关键语义：**`[exp bonus rate]` 是加成幅度，不是倍率**。`0.5` = +50%（1.5 倍），`1.0` = +100%（2 倍），`2.0` = +300%（3 倍）。持续时间取 `[stat change duration]`，冷却取 `[cool time]`，分组 `[cooltime group]` 与其它秘药共享（99）。`[stackable type]` 是 `[waste]`，即普通 Kind=1 消耗品。

### 3.2 服务端元数据结构没有这个字段

`src/Tools/VmTestKit/Economy-State-FG.cs:7`：

```csharp
public class Offline70EconomyItemDefinition { ItemId, Kind, StackLimit, MinimumLevel, Job, JobMask,
  EquipSlot, HpRecovery, MpRecovery, HpRecoveryPercent, MpRecoveryPercent, NativeStatusRecovery,
  CooldownMilliseconds, CooldownGroup, MaxDurability }
```

没有经验倍率，也没有“buff 持续时间”。

2682617 是在代码里手工注册的（`src/Tools/VmTestKit/Cash-Package-FG272.cs:67`），只给了 Kind/堆叠/等级/冷却：

```csharp
Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{
  ItemId=2682617, Kind=1, StackLimit=3, MinimumLevel=1,
  CooldownMilliseconds=3600000, CooldownGroup=99});
```

### 3.3 `Use()` 的门槛直接拒绝

`src/Tools/VmTestKit/Economy-State-FG.cs:59`：

```csharp
if(d.Kind!=1 || d.HpRecovery==0 && d.MpRecovery==0 && d.HpRecoveryPercent==0 &&
   d.MpRecoveryPercent==0 && !d.NativeStatusRecovery && !Offline70ThrowItemsFG90.Allowed(...))
    throw new InvalidDataException("Item use effect has not been implemented");
```

2682617 没有任何回复字段、不是投掷道具，于是必然命中这条异常。op47 的入口在 `src/Tools/VmTestKit/Batch-Service-FG.cs:433`，异常被捕获后回 `item-use-failed`，**不会进入扣道具的 `Apply`**，所以道具没消失。

### 3.4 通关结算没有倍率，也没有 buff 状态

`src/Tools/VmTestKit/Batch-Service-FG.cs:397` 的 `Clear()` 直接使用：

```csharp
Offline70ClearExperienceFG116.Reward clearXp = Calculate(...);
uint blackClearAward = Offline70BlackClearFG159.Award(clearXp, blackActiveFG158);
...
state.Experience = AddExperience(state.Experience, clearXp.Award + blackClearAward);
```

`src/Tools/VmTestKit/Clear-Experience-FG116.cs` 里只有基础值、难度、等级惩罚、rank 加成和测试倍率 `Offline70TestRatesFG114`，没有任何“玩家增益倍率”。经济状态里也没有 buff 到期时间字段，因此重登/跨副本无法保留效果。

结论：即使只把 `Use()` 的异常放开，也只会“扣道具但没有任何效果”，比现在更糟；必须三块一起做。

## 4. 影响面（全部经验加成秘药）

### 4.1 用户实际拥有的两件商城货

| item id | 名称 | rate | 时长 | 冷却 | 等级 | 注册情况 |
|---|---|---|---|---|---|---|
| 2682617 | 远古精灵的精华秘药 (小) | 0.5 | 60 min | 60 min (组 99) | 1 | 代码注册（FG272:67） |
| 2682616 | 远古精灵的精华秘药 (大) | 1.0 | 60 min | 60 min (组 99) | 60 | 代码注册（FG272:66） |

商城 commodity 映射：101101 → 2682617（280 券），101100 → 2682616（580 券），见 `Cash-Package-FG272.cs:38`。`docs/HANDOVER-2026-10-26-cash-catalog-and-devil-contract-FG272.md` 第 3 节已把“秘药经验 buff 未实现”列为已知缺口。

### 4.2 全量清单

用 `local-run/pvf/stk-contents-cache.json` 扫描 `[exp bonus rate]` 得到 73 个非 cash 目录的 stackable（附录 A）。另外 `stackable/stackable.lst` 里 cash/ 目录还有 2682616/2682617 等变体。合计约 75 个 id，其中 **只有 23 个在 `data/item-metadata.xml` 里注册过**（全部 Kind=1、冷却 0），其余 52 个连元数据都没有——如果掉落/商城/任务发放到这些 id，`Definition()` 会抛 `Item metadata has not been registered`。

建议实现时**只接通实际可获得的来源**（当前是商城 101100/101101 → 2682616/2682617），其余 id 按需登记；不要一次性把 75 个全放出去。

## 5. 推荐实现方案

按下面顺序做，每步都能独立验收：

### 5.1 数据面：独立 catalog，不要改 item-metadata 的固定 schema

`data/item-metadata.xml` 是 19000+ 条的固定字段表，仓库政策是 append-only（`gen_item_metadata.py` 会逐字段重推校验）。给 Kind=1 加新字段意味着重写全部记录，风险大且会触发该校验。

建议新增 `data/exp-bonus-potions-FGxxx.xml`（仿 `avatar-cash-catalog.xml` 的加载方式）：

```xml
<Offline70ExpBonusPotionCatalog>
  <Offline70ExpBonusPotion ItemId="2682617" Rate="0.5" DurationMs="3600000"
                           CooldownMs="3600000" CooldownGroup="99" DailyLimit="2"
                           DungeonOnly="true" TowerExcluded="true"/>
  <Offline70ExpBonusPotion ItemId="2682616" Rate="1.0" DurationMs="3600000"
                           CooldownMs="3600000" CooldownGroup="99" DailyLimit="2"
                           DungeonOnly="true" TowerExcluded="true"/>
</Offline70ExpBonusPotionCatalog>
```

加载器放在新文件（如 `Exp-Bonus-Potion-FGxxx.cs`），在 `Offline70BatchServiceFG` 构造时调用一次。生成器可以照 `gen_item_metadata.py` 的模式从 PVF 取证：读 `stackable.lst` → 取对应 `.stk` 的 `[exp bonus rate]` / `[stat change duration]` / `[cool time]` / `[cooltime group]`，并断言值与 XML 一致。注意 `[cooltime group]` 在 Kind=1 的现有元数据里被约定丢弃（恒 0），所以冷却/分组只能放在这个新 catalog 里。

### 5.2 状态面：buff 持久化

在 `Offline70EconomyState` 增加（按角色存档，随重登保留，离线也计时）：

```csharp
public uint ExpBonusRateMilli;      // 0.5 => 500
public long ExpBonusUntilUnix;      // 绝对到期时间
public int  ExpBonusDailyDay;       // 服务日，配合 DailyLimit 计数
public int  ExpBonusDailyUses;
```

用 `Offline70Economy.Apply(receipt, ...)` 写入，保证回执幂等；`Validate` 增加边界（rate 0..5000、到期时间合理上限、每日计数 0..100）。到期时间用绝对 Unix 秒，重登后按当前时间判断，不要重算时长。

### 5.3 使用面：放行并真正生效

在 `Economy-State-FG.cs` 的 `Use()` 里，把经验秘药作为一条独立分支（不要放宽全局门槛）：

1. 查 `ExpBonusPotionCatalog`；命中才走这条分支。
2. 校验：在已加载的副本内（`dungeon.Active && dungeon.Loaded`）；等级 ≥ 元数据；冷却组；每日次数。
3. 扣 1 个道具（保留现有 ownership / 过期校验）。
4. 写 `ExpBonusRateMilli / ExpBonusUntilUnix`；同组已存在 buff 时按“覆盖或拒绝”定契约（建议覆盖并重置到期时间，与原版一致）。
5. 记冷却（`CooldownMilliseconds` 用 catalog 里的值，组 99）。
6. 回 op47 成功；保留 `Note("EXP_POTION_USED ...")` 诊断日志。

“仅地下城可用”是 PVF explain 的硬要求，必须在服务端拒绝，不能只靠客户端。

**待验证**：op47 成功回包后，客户端是否会自己根据 `.stk` 的 `[stat change duration]` 显示 buff 图标。如果不会，需要再找状态/buff 包（可能是 op176 窗口或独立的 buff 列表包），先把服务端倍率做对，图标单独做。

### 5.4 结算面：通关经验乘倍率

`Clear()` 里只对“普通通关经验”应用倍率，并且要定义清楚与现有项的乘法顺序：

```csharp
double bonus = 1.0 + ExpBonusRate;                  // 0.5 => 1.5
uint baseAward  = Scale(clearXp.BaseAward,  bonus); // 与 rank 加成分别缩放
uint rankAward  = Scale(clearXp.RankAward,  bonus);
// 之后仍走 Offline70TestRatesFG114（测试倍率）与 BlackClearExperience
```

建议只缩放 `BaseAward`/`RankAward`，然后让 `actualDelta` 自然反映真实入账（`clearRewardsFG116` 的 `actualBase/actualRank` 就是按 `assets.Experience - actualBefore` 反推的，op35/op37 会自动显示正确值）。以下情况**不加成**：

- `blackActiveFG158`（亡者峡谷/黑色大地）；
- `dungeon.TowerFlowFG211`（塔）；
- 其余“活动双倍”暂不存在，注释里留接口即可。

等级封顶时 `AddExperience` 会截断，属于正常行为；日志里 `DUNGEON_CLEAR_COMMITTED` 的 `award`/`actualDelta` 可用于对账。

### 5.5 每日次数

PVF 里“每天可以使用 2 次”只写在 explain 文本，没有结构化字段。先按文本实现，用服务日 `Offline70FatigueFG50.ServiceDay` + `ExpBonusDailyUses` 记账；如果以后在 PVF 找到正式字段再替换。

## 6. 坑与约束

- **rate 不是倍率**：0.5 → ×1.5；直接乘会少一半收益。
- **冷却组 99 是共享的**：其它秘药（7440 等）也用 99。实现后用了 2 小时秘药会同时锁住小秘药，这是原版行为，不要“优化”掉。
- **当前元数据对 Kind=1 的冷却一律记 0**：`gen_item_metadata.py` 对 Kind=1 采用 convention 0，所以新 catalog 必须自己带冷却值；不要把冷却塞回 item-metadata（会与生成器校验冲突）。
- **不要在 70 级角色上验收经验**：`Thresholds[68]` 封顶，加多少都看不出来；用低等级角色或临时改档。
- **道具不能再“假成功”**：现在至少保证道具还在；改 `Use()` 时若只放行不写 buff，会退化成扣道具无效果。
- **并发改动**：`Economy-State-FG.cs`、`Batch-Service-FG.cs` 经常被并行会话修改；动手前后都跑 `python scripts/check_repo.py`，编译前先停服（运行中的 exe 会锁 `build/stable/*.exe`，报 CS0016）。
- **Git Bash 下 csc 用 `-` 前缀**（`-nologo`），`/nologo` 会被路径改写。
- **文档/脚本里写 Windows 路径**用正斜杠或字符串拼接，工具层会把 `\70` 当八进制转义。
- **PVF 取证走 pvfUtility 27000 API**：`local-run/pvf/pvfapi.py`；cash/ 目录路径要先拼 `stackable/` 前缀并转小写（本文件第 9 节有例子）。

## 7. 验收标准

### 7.1 离线 driver（建议 `ExpBonusPotionCheck-FGxxx`）

1. catalog 解析与边界：rate 0..5000、DurationMs 1..86400000、CooldownGroup 0..10000、未知 id 拒绝。
2. 非地下城使用被拒；已加载副本内成功；死亡状态被拒（沿用 `playerDeadFG117`）。
3. 扣道具、buff 到期时间 = 使用时刻 + DurationMs；回执幂等（同 tick 重放不二次扣）。
4. 重载 economy XML 后 buff 仍在，过期后自然失效。
5. 冷却组共享：用 2682616 后 2682617 在使用 1 小时内被拒。
6. 每日 2 次上限；跨服务日重置。
7. 结算倍率：给定干净 fixture，`BaseAward/RankAward` 分别 ×1.5；黑色大地/塔不加成；与 `Offline70TestRatesFG114` 的乘法顺序有断言。
8. 未注册 id 不会被误放行（仍走原 `Item metadata` 错误）。

### 7.2 在线验收（用未满级角色）

1. 副本内用 2682617：op47 成功、道具 -1、buff 图标出现（若客户端不自显，记录为独立缺口）。
2. 通关：`DUNGEON_CLEAR_COMMITTED` 的 `award` 与“基础×1.5”一致；op35/op37 显示的经验同步。
3. 城镇内使用被拒；第一次使用后 1 小时内再次使用被冷却拒绝；重登后 buff 与到期时间保留。
4. 回归：普通 HP/MP 药水、疲劳药、宠物食物、投掷道具的 op47 行为不变。
5. `check_repo.py`、`build.py --smoke`、既有全部离线驱动全绿。

## 8. 代码与文件索引

| 位置 | 内容 |
|---|---|
| `src/Tools/VmTestKit/Economy-State-FG.cs:7` | `Offline70EconomyItemDefinition` 字段（无经验倍率） |
| `src/Tools/VmTestKit/Economy-State-FG.cs:59` | `Use()` 的“效果未实现”门槛（根因） |
| `src/Tools/VmTestKit/Economy-Protocol-FG.cs:8/10` | op47 `ParseUse` / `UseReply` |
| `src/Tools/VmTestKit/Batch-Service-FG.cs:433` | op47 分派与失败回包 |
| `src/Tools/VmTestKit/Batch-Service-FG.cs:397` | `Clear()` 结算（要加倍率的位置） |
| `src/Tools/VmTestKit/Clear-Experience-FG116.cs` | 通关经验公式（无玩家增益） |
| `src/Tools/VmTestKit/BlackClearExperience-FG159.cs` | 亡者峡谷 10% 附加经验（不应再乘秘药） |
| `src/Tools/VmTestKit/Progression-FG.cs:20/103` | 经验封顶 `Thresholds[68]`、`AwardExperience` |
| `src/Tools/VmTestKit/Cash-Package-FG272.cs:38/66/67` | 101100/101101 映射与 2682616/2682617 注册 |
| `local-run/pvf/gen_item_metadata.py` | PVF 取证/元数据生成器（Kind=1 冷却约定 0 在这里） |
| `data/item-metadata.xml` | 现有注册表（append-only，勿加字段） |
| `local-run/pvf/stk-contents-cache.json` | 73 个秘药 id 的离线缓存（附录 A 来源） |
| `local-run/pvf/pvfapi.py` | pvfUtility HTTP API 客户端 |

## 9. 复现与取证命令

### 9.1 找使用请求

```bash
grep -n "opcode=47" /d/70S3A3/local-run/logs/channel-evidence.log
```

### 9.2 解密 op47 请求体

```python
# python
M = 0xffffffff; DELTA = 0x9e3779b9
keys = bytes(i & 0xff for i in range(334))
key = [int.from_bytes(keys[172 + j*4:176 + j*4], 'big') for j in range(4)]

def dec(data):
    out = bytearray(len(data))
    for at in range(0, len(data), 8):          # 每个 8 字节块 sum 必须重置
        l = int.from_bytes(data[at:at+4], 'big')
        r = int.from_bytes(data[at+4:at+8], 'big')
        s = 0xc6ef3720
        for _ in range(32):
            r = (r - (((((l << 4) & M) ^ (l >> 5)) + l) ^ ((s + key[(s >> 11) & 3]) & M))) & M
            s = (s - DELTA) & M
            l = (l - (((((r << 4) & M) ^ (r >> 5)) + r) ^ ((s + key[s & 3]) & M))) & M
        out[at:at+4] = l.to_bytes(4, 'big')
        out[at+4:at+8] = r.to_bytes(4, 'big')
    return bytes(out)

b = dec(open(r"local-run/logs/channel-evidence.log.request-334-47-317850fca3404b15a20e901a54c3f3cd.bin", "rb").read())
print("slot", int.from_bytes(b[0:2], "little"), "list", b[2],
      "item", int.from_bytes(b[7:11], "little"), "tick", int.from_bytes(b[11:15], "little"))
```

### 9.3 查背包是否还有道具

```bash
python - <<'PY'
import re
s = open(r"D:/70S3A3/local-run/DNF70-batch-test-FG/economy-account1-r8-FG.xml", encoding="utf-8").read()
for m in re.finditer(r"<Offline70EconomyEntry>(.*?)</Offline70EconomyEntry>", s):
    if "<ItemId>2682617</ItemId>" in m.group(1):
        print(m.group(1))
PY
```

### 9.4 取 PVF 里的秘药脚本（注意先拼 `stackable/` 并转小写）

```python
import sys; sys.path.insert(0, r"D:/70S3A3/local-run/pvf")
import pvfapi
p = "stackable/cash/chn_20130108_red_nostrum_of_growth/chn_red_small_nostrum_of_growth.stk"
print(pvfapi.get_files([p])[p])
```

## 10. 待决问题

1. **buff 图标来源**：op47 成功回包是否足以让客户端自显 buff？先用实现后的服务端实测；不自显再找状态包（优先排查 op176/op2 尾部或独立 buff 列表）。
2. **每日 2 次**：PVF 只在 explain 文本里写，是否有未提取的结构化字段；没有就按文本实现。
3. **叠加语义**：原版“开启双倍经验活动时效果会重叠”，当前服没有活动倍率，先留接口。
4. **同组重复使用**：覆盖到期时间还是拒绝，按原版手感定；建议覆盖并重置。
5. **范围**：只接通商城 2682616/2682617，还是把附录 A 中当前可通过掉落/任务获得的 id 一并登记。建议先做两件商城货，再按来源逐个补。

## 附录 A：全部带 `[exp bonus rate]` 的 stackable（非 cash 目录，73 条）

`rate` 为加成幅度（0.5 = ×1.5）；`XML已注册` 指 `data/item-metadata.xml` 是否已有该 id（代码注册的 2682616/2682617 不在此表，见 4.1）。

| id | 名称 | rate | 分钟 | 冷却分钟 | XML已注册 |
|---|---|---|---|---|---|
| 7377 | 远古精灵的秘药 | 1 | 120 | 120 | Y |
| 7429 | 远古精灵的秘药 (灰) | 0.2 | 120 | 120 | Y |
| 7440 | 远古精灵的秘药 | 1 | 120 | 120 | Y |
| 7544 | 远古精灵的秘药 (小) | 1 | 30 | 30 | Y |
| 7559 | 远古精灵的秘药 (测试用) | 1 | 10 | 10 | N |
| 7583 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7584 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7585 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7586 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7587 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7588 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7589 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7590 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7591 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7592 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7593 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7638 | name_7638 | 1 | 30 | 30 | N |
| 7639 | name_7639 | 1 | 30 | 30 | N |
| 7640 | name_7640 | 1 | 30 | 30 | N |
| 7678 | name_7678 | 1 | 30 | 30 | N |
| 7773 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7774 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7775 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7776 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7777 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7778 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7779 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 7803 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 7825 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 7829 | 远古精灵的超级秘药 | 2 | 120 | 120 | N |
| 7831 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 7850 | 远古精灵的超级秘药 | 2 | 120 | 120 | N |
| 7901 | 远古精灵的超级秘药 - 1次 | 2 | 30 | 30 | N |
| 7902 | 远古精灵的超级秘药 - 2次 | 2 | 30 | 30 | N |
| 7903 | 远古精灵的超级秘药 - 3次 | 2 | 30 | 30 | N |
| 7904 | 远古精灵的超级秘药 - 4次 | 2 | 30 | 30 | N |
| 7981 | 远古精灵的超级秘药 (帐号绑定) | 2 | 30 | 30 | N |
| 7982 | 远古精灵的超级秘药 (帐号绑定) | 2 | 30 | 30 | N |
| 7983 | 远古精灵的超级秘药 (帐号绑定) | 2 | 30 | 30 | N |
| 7984 | 远古精灵的超级秘药 (帐号绑定) | 2 | 30 | 30 | N |
| 8042 | name_8042 | 2 | 30 | 30 | N |
| 8043 | name_8043 | 2 | 30 | 30 | N |
| 8081 | name_8081 | 2 | 30 | 30 | N |
| 8082 | name_8082 | 1 | 30 | 30 | N |
| 8225 | (( | 1 | 10 | 10 | N |
| 8269 | 远古精灵的秘药 | 1 | 30 | 30 | Y |
| 8288 | (( | 2 | 30 | 30 | N |
| 8344 | name_8344 | 0.5 | 30 | 30 | N |
| 8345 | name_8345 | 1 | 30 | 30 | N |
| 8346 | name_8346 | 2 | 30 | 30 | N |
| 8374 | name_8374 | 1 | 30 | 30 | N |
| 2680406 | 远古精灵的秘药 (橙色) | 0.3 | 60 | 60 | N |
| 2680407 | 远古精灵的秘药 (黄色) | 0.5 | 30 | 30 | N |
| 2680408 | 远古精灵的秘药 (红色) | 1 | 30 | 30 | N |
| 2680747 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2680756 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2680787 | 远古精灵的秘药 (小) | 0.5 | 30 | 30 | N |
| 2680904 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2680912 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2680913 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2681513 | 远古精灵的秘药 (小) | 1 | 60 | 60 | N |
| 2681514 | 远古精灵的秘药 (黄) | 0.5 | 30 | 30 | N |
| 2681515 | 远古精灵的秘药 (红) | 1 | 60 | 60 | N |
| 2681584 | 远古精灵的秘药 (红) | 1 | 60 | 60 | N |
| 2681586 | 远古精灵的秘药 (小) | 1 | 30 | 30 | N |
| 2681641 | 远古精灵的秘药 (红) | 1 | 60 | 60 | N |
| 2681737 | 远古精灵的秘药 (红) | 1 | 60 | 60 | N |
| 2681814 | 远古精灵的秘药 (红) | 1 | 60 | 60 | N |
| 2749356 | 远古精灵的秘药 (3天) | 1 | 30 | 30 | N |
| 2749357 | 远古精灵的秘药 (3天) | 1 | 30 | 30 | N |
| 2749389 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2749390 | 远古精灵的秘药 | 1 | 30 | 30 | N |
| 2749402 | 远古精灵的秘药 | 0.5 | 30 | 30 | N |

注：表里的“分钟”同时是 buff 时长与冷却，因为本批数据两者恰好相等；实现时仍以 PVF 两个字段分别取证。

## 相关文档

- `docs/HANDOVER-2026-10-26-cash-catalog-and-devil-contract-FG272.md` 第 3 节“已知使用缺口”已登记本问题。
- `docs/FIX-2026-10-26-cash-packages-FG272.md`：101100/101101 商品与道具映射、2682616/2682617 注册来源。
- `local-run/coord/HANDOVER-QUEST-SYSTEMS-2026-10-26.md`：经验/等级相关系统级缺口（若要做“经验加成”统一结算可一并参考）。
