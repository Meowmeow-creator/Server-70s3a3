# FIX 2026-10-27 批次四：宠物装备丢弃/出售、哈穆林三阶段、装扮合成器与达芙妮分解 (FG351/FG352)

反馈三条（用户编号 10/11/12），全部修复并部署到稳定服（7002 / 17001 已重启）。
离线验收：`local-run/driver/Triage-FG352-Check.exe <data-dir> <scratch-dir>` → **ALL PASS**（31 条断言）。

## 10. 宠物装备无法丢弃 / 无法出售

**根因（两处独立拦截）**
- 丢弃：`Batch-Service-FG.cs` `DeleteTown`（op18）只放行 `Kind 1/2/4`，宠物装备是 **Kind 8**
  （`item-metadata.xml` 的 85 条 kind 8；实测档案 `2747158` 在 list 7 slot 140），直接被
  `Delete unverified item kind` 拒绝。
- 出售：`Npc-Sale-FG62.cs` 的 `Parse`/`Sell` 要求 `List==0` 且 slot 落在普通背包区间；宠物装备
  在宠物窗口 **list 7 slot 140..188**，先被 `NPC sale category` 拒；即使放行，
  `Commerce-Catalog-FG71.cs` `SalePrice` 也要求 `List==0 && Kind∈{1,2,4}`。

**修复**
- `DeleteTown`：放行 Kind 8，并限定它只能来自 list 7（`Delete pet equipment window`）。
- `Npc-Sale-FG62`：新增 `SaleList(list,slot)` = `list 0` 原区间 **或** `list 7 && slot 140..188`；
  `Parse`/`Sell` 都改用它。
- `SalePrice`：`(list 0 或 list 7) && Kind∈{1,2,4,8}`；Kind 8 必须 list 7；未登记 profile →
  **0 金币**（沿用用户 2026-10-26 确认的"未登记=0 金币"语义，不引入[medal]/上架过滤）。

## 11. 迷乱之村哈穆林：三阶段、三次击败 boss 才能通关

**PVF 证据（原版机制）**
- `dungeon/act6/villageoftemptation.dgn`：`[boss map]` = **三个 boss 格** (0,0)(1,0)(0,1)；
  `[dummy appear count] 2` —— 全库 263 个 .dgn **只有哈穆林**有该字段（2 个假身）。
- `monster/monster.lst`：1078=`Fake_PeterThePiper`、1096=`Fake_PeterThePiper_Second`、
  1077=`PeterThePiper`（真身，`[int data]` 血量级 200000 vs 假身 15000；真身有 `[on die]` 台词）。
- 三张 boss 图 `map/villigeoftemptation/b6416|b6422|b6423.map` 的 `[monster]` 静态行都写着 1077；
  假身 id 由服务端按阶段替换（rednf `SetGridPath.inc` + `choose_random_bossmap_from_dummymaps.inc`：
  假身放在**其它** boss 格、`add_monsters_current_map` 里 `m_roleType==3 && !m_dummyBossMap` 才进
  `m_apcList`，即**只有链上最后一格的 boss 击杀计入通关**）。
- 服务端迷宫 `Forest-Content-FG75.Mask`：d51 的 (0,0) mask 9=E+S、(1,0) 5=E+W、(0,1) 2=N
  —— 三个 boss 格天然连成 迷宫→(1,0)→(0,0)→(0,1) 的链，(0,1) 是死胡同，无法跳过。

**修复（链式三房，(0,1) 才是计数 boss 房）**
- `Forest-Content-FG75.cs`
  - `Map`：cell (1,0)=13057 → **6416**（原 6411 普通房）；cell (0,1)=13061 → **6423**（原 6401）。
  - `BossY(51)` 0 → **1**：`IsBossRoom`、小地图 boss 位、`ReadyToClear` 全部落在 (0,1)。
  - 名册：`6422` boss 行 1077→**1096**(假身2)；(0,0) 用 b6422 原行序；
    新增 `6416` = b6416 行序、boss 索引 2 = **1078**(假身1)；
    新增 `6423` = b6423 行序、boss 索引 4 = **1077**(真身)。
- `Floor-Catalog-FG119.cs`：`MapRolls` 补 6416（第 2 行双掷）/6423（第 4 行双掷）；
  `MonsterPools` 补 1078/1096（镜像 1077）。
- `Dungeon-Flow-FG.cs`：`Kill()` 在 (1,0)/(0,0) 的 kind-3 死亡时置
  `HamelinChainEastFG/HamelinChainHubFG`；`Move()` 经 `HamelinGateFG` 锁 (0,0)/(0,1)（纵深防御，
  实际几何上本来就跳不过去）。`Begin()` 每次新 run 自动清零（新实例）。
- 结果：假身1 → 假身2 击杀都只出普通怪死亡/经验（**不**发 op127/op31），(0,1) 真身 1077 死亡才
  `ReadyToClear` → 原通关/结算/翻牌链路完全不变；任务 69 `[hunt monster 1077]` 仍由真身击杀完成。

## 12. 装扮合成器合成后卡死 / 达芙妮处分解不了

**根因**：两个 op 服务端都没实现，`Handle()` 落到 `UNIMPLEMENTED` 静默丢弃 → 客户端等回包 → 卡死/无反应。
- **op102** = 装扮合成（rednf `PacketDispatcher.inc` m_dispatchers[102]，`MSG_COMPOUND_AVATAR` 22B 载荷）；
  回包 op102(kind1) 由 `WongWorkMiscB_A/_SendResult` 逐字节给出。
- **op205** = 时装分解（`Dispatchers/dispatch_sig_51.inc`，u16 槽 + u32 物品 id；回包 op205(kind1)：
  `byte1, short 时装槽, short 插入数, (short 槽, u32 物品, int 数量)...`）。

**修复（新文件 `Avatar-Compound-Disjoint-FG352.cs`）**
- op102 `AvatarCompoundFG352`：校验合成器（item **21 装扮合成器** / **899 黄金装扮合成器**，来自
  `etc/compoundavatar_*.etc` 的 `[material]/[material_upper]`）在 list 0 槽位；两件素材必须是
  list 1 时装且 itemId 与请求一致、同 (职业,部位) 池；按 PVF 池加权抽结果（黄金合成器对低权重
  稀有档 ×3）；一次经济事务里扣合成器+两件素材、发结果时装；回包按 _SendResult 布局
  （两条素材三元组 + 结果槽/物品/期限0/珠宝 30B/expansion 4B）。
- op205 `AvatarDisjointFG351`：校验 list 1 时装槽与 id（kind 3）；按 `etc/disjoint.etc`
  `[avatar disjoint info]` 第 0 行 = 四色一级徽章盒，取 `emblem_booster{red,yellow,green,blue}1.stk`
  首个 `[stackable]` 块加权抽 **56 种叶子徽章**之一；扣时装、发徽章；回包按 ORIG 布局。
- 分块填充：op102 走 index4 金密码（16B 块）、op205 走 index9 区域密码（8B 块），请求解析允许
  尾部零填充（22→32 / 6→8）。
- 数据：`local-run/pvf/gen_compound_avatar_pools.py`（`--write`）生成
  `data/compound-avatar-pools-FG352.xml`（9 职业×8 部位 2085 条结果池 + 56 条徽章池 + 合成器 id），
  并向 `data/item-metadata.xml` **追加 202 行**：21/899（Kind 1）、56 叶子徽章（Kind 1）、
  144 件未登记稀有装扮（Kind 3，字段取自各自 `.equ`：`[minimum level]`、`[usable job]`→Job255+JobMask、
  部位→EquipSlot 语料映射 hat0 hair1 face2 coat3 pants4 shoes5 neck6 belt7）。无重复 id、LF 结尾。

## 部署与验收

- `check_repo.py` 428 文件 / 175 稳定源通过；`build.py --smoke` 通过；`restart-detached.ps1` 起双服
  （7002/17001 LISTENING，日志无异常）。
- 离线 driver：`Triage-FG352-Check.exe`（rsp 由 `make_fg352_rsp.py` 生成，csc 用 v4.0.30319 全路径）
  覆盖：kind8 价格/位置/出售全链（含 list 7 真实请求加密→Parse→Sell→0 金币）、d51 三 boss 房
  图/名册/掷骰/池、完整走图（(2,4)→…→(1,0)→(0,0)→(0,1)）逐房清怪 + **非末房不结算/末房结算**、
  合成池 2085/72/56、稀有档低概率（11/1429）、徽章池、新物品登记与发放校验。

## 实机待验（客户端行为无法离线覆盖）

1. 宠物装备：右键丢弃、NPC 出售（预期 0 金币或登记价）都可走通。
2. 哈穆林：进 boss 区后应连打三个房间的皮特（(1,0)→(0,0)→(0,1)），前两次不结算、第三次结算；
   注意客户端对 boss 格门/小地图的渲染（数据上三格都是 boss 格，客户端自己的 .dgn 也这么写）。
3. 合成器：合成后应弹结果窗口而不是卡死；背包需有 item 21/899（商城/时装合成器），否则请 GM 发。
4. 达芙妮：分解任意 list 1 时装应得到 1 个徽章（普通档），弹窗显示徽章名。
