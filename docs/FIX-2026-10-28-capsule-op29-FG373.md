# FIX 2026-10-28 容器类道具统一接入（压缩胶囊 / 命运之试验 / 袖珍罐 …）— FG373

## 现象

- 2026-10-28 20:51:27 使用 7036 压缩的胶囊 → `CONTAINER_OPENED sourceSlot=3 reward=3116`（前一轮的
  单项修复已生效，开出黑硬物质）。
- 20:51:35 立即使用 **7040 命运之试验** → 又一次 `op29`，仍被罐子逻辑拒绝：
  `POT_REJECTED Pot ownership/level/gold`，客户端"无法开启"。
- 解包两次请求体（`DecryptCapture-FG349`）：7036 是 `prefix=0, slot=3`，7040 是 `prefix=0, slot=0x51=81`。
  存档核对：slot 81 = 7040×100。也就是**同类道具全都走 op29，此前只接了一个 7036**。

## 为什么要做成表（rednf 权威）

`reference/rednf/.../STStackableScript/ImportStackableScript.inc` 把 `[stackable type]` 映射成
`m_nItemType`，`CStackableItem/set_item.inc` 再按类型选解析器：

| PVF `[stackable type]` | m_nItemType | 解析器 |
|---|---|---|
| `[legacy]` | 6 | `createLotteryInfo` (0x89babc5)：`[int data]` 首项=未中签默认物，默认数量 1，其后 (物品,权重) 对，数量恒 1 |
| `[stackable legacy]` | 7 | `createStackableLotteryInfo` (0x89bacc1)：首项=默认物、第二项=默认数量，其后 (物品,权重,数量) 三元组 |
| `[upgradable legacy]` | 0xd | 同上，并且 `CUser::lottery_item` (0x086733a2) 对**装备奖励**额外按 ServerParameter 档位表 roll 强化值 |

开奖：`CStackableItem::get_lotto_item` (0x850ebfc) `randInt(100000)` 累积权重，未中签给默认项；
中签物品 id 为 0 或默认项为 0 时 `CUser::lottery_item` 走金币分支（本批表中无金币容器）。
入口：`DisPatcher_Lottery::dispatch_sig` (0x081f8380) → `CUser::lottery_item(slot)`，即 **op29**。

## 本次改动

1. **全量普查** `local-run/pvf/scan_legacy_containers_FG373.py`
   - 用 `stackable/stackable.lst` → `.stk`，只扫服务端能持有的 id（`data/item-metadata.xml` ∩ lst，
     4,388 个文件），取出 `[stackable type]` 与 `[int data]`。
   - 结果 **133 个容器**：23 个 `[legacy]`、107 个 `[upgradable legacy]`（其中 3 个是补登记奖励时
     新暴露出来的容器），产出 `legacy-containers-FG373.json`。
2. **补齐奖励物品** `local-run/pvf/register_container_rewards_FG373.py`
   - 176 个奖励 id 未登记：109 个是普通 stackable（`[throw]`/`[material]`/`[quest]`/`[etc]`/`[legacy]`），
     按 `gen_item_metadata.py` 的既有证据规则（`pvf_evidence` + `derive`，Kind 取
     `STACKABLE_KIND_EVIDENCE` 语料表，`[legacy]` 走已文档化的 token 决策）**追加 109 行**到
     `data/item-metadata.xml`（现 40614 条）。
   - 1 个拒绝（6044 `[set]`，语料无先例，不猜）；66 个不在 stackable 域（24 个 avatar `.equ`、
     12 个普通装备、30 个任何 lst 都查不到的旧 id）——这些奖励在表里被裁掉。
3. **数据表** `local-run/pvf/gen_container_lottery_FG373.py` → `Server-70s3a3/data/container-lottery-FG373.dat`
   - **123 个容器**；10 个因默认奖励仍未登记而整罐禁用（7694–7704 一族，默认物是 7203–7247 那批
     查不到的旧 id）。裁掉 537 条无法发放的奖励行（多为 avatar `.equ`），其余权重按 PVF 原值。
   - 格式：`FG373CL2` + PVF sha + u16 数量 + 每容器 `{itemId, mode, flags, 默认物, 默认数量, 条目[]}`
     + 尾部密封装备 id 列表。
4. **服务端**
   - 新增 `src/Tools/VmTestKit/Container-Lottery-FG373.cs`：加载/校验表，按 receipt 确定性抽奖
     （与 FG47/FG350 同一重放契约）；`[upgradable legacy]` 的强化 roll 与装备种子也由 receipt 派生。
   - `Economy-State-FG.cs` 新增 `UseContainerFG373`：消耗与发放同一原子事务；数量取中签条目；
     装备奖励落在 9–40 号装备格并写回 seed / 强化值 / 满耐久。
   - `Pot-Service-FG93.cs`（op29）：11 个手工罐子（7140/7144/…/7628）继续走原罐池（它带金币成本与
     封印/期限装饰），**其余注册容器一律走 FG373 表**；容器不要求城镇（原生就是背包使用）。
   - `Batch-Service-FG.cs` op47 同样改走 FG373（客户端若在别的上下文用"使用"通道也能开）。
   - `Capsule-Lottery-FG350.cs` 变成薄壳委托 FG373，旧 driver 不用改。
   - **装备封装**：普查时顺带取每条装备/神器奖励的 `[attach type]`，`[sealing]` 的 2,590 个 id 进表
     尾部；`UseContainerFG373` 发放时写回 `Sealed`，op14 增量行把封装字节带给客户端（客户端用
     这一点显示"封装"并据此判断可交易）。封装只影响交易，穿上即解封——`Offline70Economy.Move`
     装备到 3 号列表时清 `Sealed`，与原生一致。

## 验收

`local-run/driver/ContainerOp29Check-FG373`（全源编译）：**51/51 PASS**

- 表加载 123 个容器；7036/7040 在表内，7694 已禁用，7140 仍归罐池；
- 实机两次 op29 明文（slot 3 / slot 81）解析正确；
- 7040 分布 65.2% / 27.6% / 7.2%（PVF 权重 65000 / 28000 / 7000）；同 receipt 结果一致；
- 7036 分布 50.0% / 4.8% / 45.2%（PVF 50000 / 5000 / 默认）；
- scratch 存档开 7040：容器减 1、3206 并入已有堆叠（×3→×4）投影正确、op29 回包字段正确、
  同 receipt 重放不双发；
- `[upgradable legacy]` 7748：奖励为装备、落装备页、seed 与强化值写回、强化值落在档位带内，
  回包 +9=seed / +13=耐久 / +15=强化；
- 封装：表里 2,590 个密封装备 id（20127 是 `[sealing]`、20087 是 `[free]`）；开出一个密封奖励后
  存档行 `Sealed=true`、op14 增量行字节=1、重读存档仍是封装；
- 裁剪容器 7620 仍可开（472/532 条），剩余奖励全部可发放；
- 普通 `Use` 仍拒绝容器（防止无产出消耗）。

## 已知边界（后续可做）

- 被禁用的 10 个旧罐（7694–7704 等）默认奖励是 7203–7247 这批**任何 PVF 索引都查不到**的 id，
  客户端 PVF 里已无对应物品文件，短期无法补。
- 60 个容器有被裁掉的奖励行（合计 537 条），主要是 avatar `.equ` 与 12 件未登记装备；要补需按
  卡片/时装域的证据规则逐条登记。
- 罐池路径（7140…7628）的 `Sealed` 仍来自 Pot-Catalog-FG93 自己的表，未改动。

## 部署

`check_repo.py` PASS（428 文件 / 194 稳定源）→ 停启动器与双服 → `build.py --smoke`（195 源，data/*.dat
随构建复制）→ `one-click.ps1` 拉起。
