# 2026-10-28 凯莉普通强化「错误代码：19」：1852 条白名单换成原版逐件推导

## 现场

- 玩家 role 8（job 8，Lv.70）在凯莉（npc 17，town 2 area 0）点普通强化，客户端弹
  「强化装备出现错误 错误代码：19」。服务端 `local-run/logs/channel-evidence.log`：

  ```
  21:10:01 FRAME opcode=83 bytes=61 seq=492
  21:10:01 COUPON_REJECTED Ordinary reinforcement target unsupported
  21:10:01 SENT coupon-failed83 type=1 opcode=83 bytes=31
  ```

- 错误码 19 = `0x13`。该文案只来自 `Ordinary-Reinforcement-FG176.cs` 的目标校验
  （`!Targets.TryGetValue(...)||Ability!=0||ExpiresUnix!=0||Sealed`）。
- 同一时刻存档 `economy-account1-r8-FG.xml` 里 list 0 slot 3..40 的 Kind=4 装备只有两件：
  slot 10 = **35107**（`光之城主的誓魂十字`，30 级十字架，+0，耐久 35/35）、
  slot 11 = **33662**（+0 那一件，同款在身上 +31）。33662 在目录里且 Ability/Expires/Sealed
  全干净，会通过；所以被拒目标只能是 **35107**——它不在那 1852 条目录里。

## 根因

`data/ordinary-reinforcement-FG176.xml` 只有 1852 个目标（TW 参考清单里恰好存在的那些），
而 `data/item-metadata.xml` 登记了 **26122** 件 Kind=4 装备。参考实现里没有物品白名单：

```
CItemUpgrade::_CheckCondition
    item 脚本类型 10..21 且 != 11        else 0x13
    [durability] == 当前耐久              else 7
    table.GetPenaltyType / isUpgradableLevel / 空格预检 / 金币/材料
CItemUpgrade/_GetUpgradeInfo.inc -> upgrade_table_t::get_upgrade_info
    报价 = [cost][growthGrade]                      # growthGrade == .equ [grade]
    报价 = (int)(报价 * [type][equipType]        + 0.5f)
    报价 = (int)(报价 * [cost weights by rarity][rarity] + 0.5f)
    # 本版 etc/upgrade.etc 没有 [cost weight by upgrade level]，m_floatMap 为空 ⇒ 与强化等级无关
```

也就是说，原版是按每件装备的 `[grade]/[rarity]/[equipment type]` 现算，而不是查表。
我们缺的只是「把 PVF 这些字段预计算成目录」这一步。

## 修复

1. 新增 `local-run/pvf/gen_ordinary_reinforcement_FG176.py`：从三份已固定到同一份 Script.pvf 的数据
   重建目录——`data/disjoint-params-FG92.dat`（grade/rarity）、`data/item-metadata.xml`
   （Kind/EquipSlot/MinimumLevel/MaxDurability）、`etc/upgrade.etc`（`[cost]`/`[type]`/
   `[cost weights by rarity]`，走 pvfUtility 27000）。
   - **门 1**：旧 1852 行必须逐条重算一致（Grade/Rarity/MinimumLevel/EquipType/Durability/GoldCost
     六项全等），不一致就拒绝写盘。实测 **1852/1852 全等**，报价公式不是自证。
   - **门 2**：新集合必须严格包含旧集合（`dropped 0`）。
   - **门 3**：每行满足加载器自己的档位约束（Kind 4、槽 10..21 且 ≠11、grade<100、rarity≤5）。
   - **门 4**：`Steps` 31 行原样保留（材料 3171、阈值不变）。
2. `data/ordinary-reinforcement-FG176.xml` 由 1852 行重建为 **24965 行**（7,169,136 字节）：
   武器 6767 / 上衣 2477 / 下装 2384 / 头肩 2458 / 鞋 2394 / 腰带 2355 / 肩 1793 / 项链 1779 /
   戒指 1817 / 辅助装备 523 / 魔法石 218。标题（槽 11）、槽 0 仍不在目录，与原版 `==0xb -> 0x13` 一致。
3. `Ordinary-Reinforcement-FG176.cs`：目录体积上限 1 MiB → 16 MiB，并补 `Targets.Count>50000` 上限；
   拒绝日志拆成三条并带证据：`target not in catalog item=…` / `target state item=… ability=… expires=… sealed=…`
   / `beyond serialized grade range item=… upgrade=…`（此前只有一句 unsupported，查不到是哪件）。

不改：报价仍按目录里的固定值扣（本版没有每级成本权重表）；`3171` 数量/失败阈值/降级幅度仍用既有 31 行；
破坏返料仍只对已证明的分解档放行（见下面的边界）。

## 验收

```bash
python local-run/pvf/gen_ordinary_reinforcement_FG176.py            # 干跑，打印 1852 -> 24965
python local-run/pvf/gen_ordinary_reinforcement_FG176.py --apply    # 写盘
cd Server-70s3a3 && python scripts/build.py --smoke                 # 195 源编译 + 无参 exit 2
python scripts/check_repo.py                                        # repositoryChecksPassed
# 产品级离线驱动（用 build/stable 里真实数据，事务落可丢弃夹具目录）
local-run/driver/ReinforcementCoverage-FG375.exe \
    D:/70S3A3/local-run/reinf-coverage-fg375/fixtures D:/70S3A3/Server-70s3a3/data
→ {"allPassed":true,"checks":21,"targets":24965}
```

驱动覆盖：35107 目录档位（grade 34 / rarity 3 / 槽 10 / 耐久 35 / 报价 86400）；35107 +0 真实事务
（金 200000→113600、3171 5→4、强化 0→1、重登保持）；耐久不满回 7 且不落盘；标题 26597 仍回 0x13 且不落盘；
35107 在 +10 仍按「未证明破坏档」回 0x13 且不落盘；33662 在 +9 降级（result 2、9→6）、在 +10 破坏
（result 3、返料尾非空、目标移除、材料入包）。旧行抽检 33662 = grade 54 / rarity 2 / 110160 未漂移。

部署：`restart-detached.ps1` 重启后 channel 正常 listen（`oneHitKill=0`），7.1 MB 目录在启动期解析无异常。
玩家需要重新登录一次（旧连接随服务重启断开）。

## 边界（不得当成已完成）

1. **+10 以上破坏档仍只对 1308 条已证明分解档放行**。35107 带 `[impossible contents] disjoint`
   （无法分解），原版破坏时会走 `m_disJoint->GetResult`，该函数对不可分解物品的行为没有证据，
   因此继续在扣量前拒绝（回 0x13，日志会写 `Unproved break-return equipment profile`）。
   把返料扩到全量 `disjoint-params-FG92.dat` 是独立改动，需要单独取证。
2. `[limit upgradable level]`（`STEquipmentScript::isUpgradableLevel`）未实现：带这个标签的装备
   原版还会按角色等级区间拒绝，我们目前比原版宽松。
3. 成长装备（`.equ` 有 growth 标记）原版报价按角色等级线性取 grade，目录里存的是静态 `[grade]`。
4. 本版 `etc/upgrade.etc` 没有 `[cost weight by upgrade level]`，所以报价与强化等级无关这一条
   是本版数据的事实，不是跨版本结论。
5. 未做真人 UI 验收；新的目录行只证明服务端会受理，客户端是否给该装备开放强化按钮由客户端数据决定。
