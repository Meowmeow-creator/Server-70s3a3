# FIX 2026-10-30 — 罗杰的生产功能（FG410）

玩家反馈：大晶体（3038/3039/3042）没有获取途径，而原版靠 **罗杰（生产商）的生产窗口** 用
小晶块换大晶体。本次把这条链接通：配方表、27 号协议、原子扣料/发奖、回包与增量刷新。

## rednf 对照（服务端语义来源）

| 环节 | rednf（reference/rednf/remake/...） | 我们的状态 |
|------|--------------------------------------|-----------|
| 命令号 | `PacketDispatcher/PacketDispatcher.inc: m_dispatchers[27] = DisPatcher_Compound` | op27 之前落 `UNIMPLEMENTED`，无 handler |
| 请求 | `DisPatcherCore/dispatch_sig_10.inc`：`int itemId, byte type, ushort count`；`compound_item(itemId, type==1, count, 0x1b)` | 同布局（16 字节密文，尾部 0 填充） |
| 配方定位 | `CUserOnlyOrig/_GetRecipeItem.inc`：type==1 走物品 id，要求 `[stackable type] [recipe]` | 配方表按 item id 索引 |
| 数据来源 | `npc/roger.npc [role] [product item] 20` + `itemshop/itemproduct1.shp` + 各 `*.stk [int data]` | 同源（shop 20 / npc 29，49 行） |
| 配方结构 | `createRecipeInfo_1.inc`：`n1 材料对(0=金币) n2 产物对 n3 技能对 [尾部]` | 逐字段同解析 |
| 成功判定 | `isSuccessCompound.inc`：`[success rate] 0 = 必成功` | Chance==0 必成功；否则按回执确定性掷 100 |
| 主链 | `compound_item.inc`：材料/产物按 count 倍乘、金币与材料同事务扣、`_ProcCompoundCore` | `ApplyRowsFG410` 同一事务内扣金+扣料+发产物 |
| 回包 | `_ProcCompoundCore.inc`（cubeMode=type==1）：`byte 删除数{byte 空间,short 槽,int 数}`；`byte 插入数{byte 0,short 槽,int 物品,int 数,byte 强化,short 耐久,byte 能力类型,short 能力值,byte 标记,put_packet}` | 同布局；`put_packet` 对普通材料是 3 字节（随机选项数 0、强化分离、交易限制） |
| 错误码 | 0x11 配方/通用、0x15 材料不足、0x13 空间/NPC | 同 |

rednf 的 `LimitNpcBuyItem`（第 1895 行附近）是**别的**系统：它按 `[need material]` 做“限量兑换购买”，
不是生产；生产只看 `DisPatcher_Compound`/`compound_item`。

## 改动

* `local-run/pvf/gen_production_recipes_FG410.py`：从 PVF 读 shop 20 的 49 条配方，按
  `createRecipeInfo` 解析 `[int data]`。9 条 2012 雪之灵活动配方产出的是装备（.equ），
  材料 2682589 也不在本构建可获得，**明确跳过**；其余 40 条（布片/皮革/碎骨/铁片/硬化剂/
  砥石/五种属性石/五种大晶体/光辉魔石/恶魔结晶）全部落地。
* 物品登记：生产链涉及 59 个物品 id，其中 20 个此前未登记（3135/3136/3138/3139/3140/3143/
  3144/3145/3147/3148/3149/3152/3153/3157/3158/3169/3172/3173/3174/3240），用
  `gen_item_metadata.derive` 的既有推导全部登记为 Kind 2 / 堆叠 1000 / 1 级。
  登记前备份 `local-run/pvf/backup-2026-10-30-production/item-metadata.before-FG410.xml`。
* `data/production-recipes-FG410.xml`（data 与 build/stable 双份，17085 字节）：
  40 条 `{Id, Name, Materials, Outputs, Skills, Chance, MaintainAbility, FailOutputs}`。
* `src/Tools/VmTestKit/Production-Recipes-FG410.cs`：表加载与启动期校验（PvfSha256、shop 20/
  npc 29、配方宽度、每个材料/产物必须是已登记的堆叠类）。
* `src/Tools/VmTestKit/Production-Service-FG410.cs`：op27 handler。
  * 只在**西海岸（town 3，罗杰所在城镇）**受理，副本内拒绝；原生 27 号命令本身不校验 NPC
    （客户端只从罗杰窗口发包），这里收窄到他的城镇，避免在副本里凭空生产；
  * 请求尾部必须全 0、type 必须 1（罗杰窗口按配方 id）、数量 1..500；
  * 材料里的 item 0 是金币；金币与材料在主背包（list 0）内原子扣除，产物用
    `AddAssetItem` 落进材料页；同一回执重发不重复扣发；
  * 回包 op27（kind 1、op%14=13 的 GoldCipher），随后补一条 op14 增量（金币 + 背包行），
    与购买路径的刷新方式一致；
  * 成功日志 `PRODUCTION_COMMITTED`，失败 `PRODUCTION_REJECTED code=…`。

## 验收

`local-run/driver/ProductionCheck-FG410.cs`（真实表 + 真实经济事务 + 回包构建，36 项）：

* 40 条配方；`制作无色大晶体` = 3037×100 + 500 金币 → 3042×1；黑/白/红/蓝/金五种大晶体
  逐条核对；布片/皮革/碎骨/铁片/硬化剂/砥石/属性石/光辉魔石/恶魔结晶抽样；
* 倍乘：count=3 → 3037×300 + 1500 金币；
* 原子性：材料不足时抛错，金币、材料和产物全部不变；
* 回包逐字节：2 条删除行（材料 space=3 slot=105 count=100；金币 0/0/500）、1 条插入行
  （slot/3042/1 + 10 字节零尾），并验证插入行槽位确实指向发到的大晶体；
* 密文往返：请求/回包 27%14=13 的 GoldCipher 加解密一致；
* 重复回执不重复扣发。

复跑：

```
ProductionCheck-FG410.exe <data-dir 或 build/stable> <fresh-dir>
```

另有**真实服务端联调**（`local-run/driver/ProductionLive-FG410.cs`）：把存档复制到隔离店
（yoyo 的 SavedTown 改 3），用隔离频道 `--test-port 17003` 起服，走完整登录（账号 1/1）→
选角色槽 0 → 发 27 号包，结果：

* 回包 op27 `del=2 ins=1 body=37`（材料行 + 金币行 + 1 条产物行，16 字节块补齐到 48）；
* 隔离存档核对：金币 60506→60006、无色小晶块 1105→1005、无色大晶体 0→1；
* 未知配方（1234）回 `[0,0x11]`；
* 日志 `PRODUCTION_COMMITTED recipe=5090 name=制作无色大晶体 count=1 success=True gold=500 outputs=1`。

部署：`python scripts/build.py --smoke`（206 源通过）→ `restart-detached.ps1`（17:21 起双服，
7002/17001 LISTENING）。`check_repo.py` 549 文件通过。

## 尚未验证 / 不做

* **客户端回包渲染未实机验证**：服务端字节（含真实隔离联调）已经钉住，但 27 号回包在客户端
  生产窗口里的展示（删除/插入行、金币刷新）只能靠真人点一次罗杰的“生产”确认。若客户端对行格式
  有额外要求，服务端日志会先出现 `PRODUCTION_COMMITTED`，再按客户端表现定位。
* type=0（用背包里的配方物品生产）未实现：罗杰窗口走 type=1；type=0 留给“设计图”类内容。
* 制作技能（`CItemMakingSkill` / `[weapon making]` 等熟练度、成功率加成、制作经验）未建模；
  罗杰这 40 条配方本身 `[success rate]` 都是 0（必成功），不影响本次目标。
* 9 条 2012 雪之灵活动配方（产出装备）未接。
* 分享包 zip 未重打。
