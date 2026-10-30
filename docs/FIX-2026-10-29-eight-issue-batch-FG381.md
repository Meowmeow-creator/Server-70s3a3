# FIX 2026-10-29：八条玩家反馈批量修复（FG381）

2026-10-28 深夜玩家反馈的八条问题，全部以 rednf 原版逻辑为准绳修复；01:28 构建、01:36 部署，
登录探针 LOGIN_OK。离线验收 `local-run/build-verify-fg381/Triage-FG381-Check.exe`（28 断言全过，
rsp=`local-run/driver/fg381-all.rsp`）；FG215 回归 ALL PASS；FG352 driver 有 2 条
**FG370 徽章 Kind9 化导致的过期断言**（叶子徽章 2500018 现为 Kind9，driver 还在断 Kind1），
与本次改动无关。

## 1. GBL教孵化场（d112）打领主必卡死
- 现场结论：领主 61109 的召唤物是客户端本地生成、uid 排在房间 roster 之后（≥100+TotalCount）。
  op42 死亡上报打到 `Dungeon actor UID`（Dungeon-Flow-FG.cs ValidateKill）时，外层 catch
  **只对 `Batch death actor bounds` 这一种消息回 monster-death-failed**，其余拒绝静默 →
  客户端永远等不到 op38 → 卡死。
- 原版依据：rednf `DisPatcherCore/dispatch_sig_16.inc`（ORIG 0x081eb0c4）——kill_monster
  **任何**失败分支都回 `header(1,0x2a)+put_byte(0)+put_byte(5)+put_short(mobSeq)`。
- 修复：`Batch-Service-FG.cs` op42 catch 改为除"密文形状都解不开"（native size）外全部回
  失败包；DEATH_FAILURE_ACK 日志带 reason。boss 房 roster 本身（Forest-Content 13038：6×61111
  +61805+61109 kind3）与 PVF 一致，无需动数据。

## 2. 装备不消耗耐久
- 原版依据（rednf）：`PacketDispatcher.inc:74` op51(0x33)=DisPatcher_UseEquip →
  `CInventory/use_equip.inc`（槽 0..0x19、每次 `m_fieldb--`）→ `DisPatcherCore/send_22.inc`
  回 `{1,slot}` / `{0,errNo}`（0xb=已磨没，0x13=非装备）。死亡不扣耐久（process_40.inc 无耐久操作）。
- 我们此前完全没有 op51（客户端本地显示、服务端永不减）。新增
  `Durability-Use-FG380.cs`：解密（index 51%14）→ list3 槽位 Kind4 且 Durability>0 → -1，
  回包对齐原版；事务 receipt=`session<n>-dura-<seq>` 天然去重。

## 3. 30 级以上装备不能分解（分解机等级）
- op28 参数表链路（FG92）无任何等级闸（rednf `_checkDisjointableItem` 只有 4 条判定）；
  真正的闸在**分解机等级**：FG183 的 217 副职业信息与 218 分解机信息把 grade/level 写死 1
  （老旧的分解机），客户端按这个值设"可分解装备等级"上限 → 30 级以上本地就拒。
- 修复：`Reward-Types-FG183.cs` 两处 grade 提到 5（完美的分解机；disjointer.exj
  [disjoint result] 覆盖 grade 0..8）。
- **2026-10-30 已被 FG406 取代**（`docs/FIX-2026-10-30-machine-tier-FG406.md`）：写死 5 会让
  Lv.1 的分解师摆出 Lv.5 的机器。217 的 dword 改回原版语义（`m_enchantCount + 1`，即副职业
  等级），218 的 dword 更正为手续费。若"高等级装备不能分解"复现，那它来自客户端本地闸，
  不是这个字段（客户端全镜像里 `[char+0x2d0]` 的读取点没有一个落在物品判定链上）。

## 4. 不能调品级
- 调品级=op84（Dispatcher_ModItemAttr，品级调整箱分支）：rednf 直接
  `set_add_info(rand())` 写新种子；**op84 我们早已实现**（Item-Quality-FG101，材料箱 15）。
- 真正缺口：**op47 使用品级调整箱被通用 Use 拒**（"Item use effect has not been implemented"）
  → UI 根本打不开。修复：Batch-Service-FG op47 对 {15,897,2669390} 只 ACK 不消耗
  （原版箱子在 op84 确认时才消耗）；Item-Quality-FG101 材料集扩到三件箱子。

## 5. 装扮合成器"材料不足"
- **op102 我们从未收到过客户端请求**（全部归档日志 0 条 FRAME opcode=102）→ 客户端本地判定失败。
- 服务端 op102 本身也有两处与原版 wire 相悖（rednf `MSG_COMPOUND_AVATAR.h` 权威）：
  ①字段0是 **GetInvenSlot(1) 目标时装槽**（我们此前当合成器槽位查 list0）；②合成器不在请求里，
  原版按表 [material]/[material_upper] 经 CheckNeedItemList 消耗，m_type==1 走黄金合成器(899)。
  旧代码还要求材料必须在结果池表里——CN 274xxxx 时装不在 2013 表里，永远拒。
- 修复（Avatar-Compound-Disjoint-FG352.cs 重写）：字段0=target 槽（仅记录）；合成器按
  type 优先 899/21、双写兜底，缺货回 0x16（客户端文本即"材料不足"）；材料改为
  JobMask+EquipSlot（部位）推导池键（原版 job 循环语义），错误码对齐 0x11/0x13/0x16/0x17。
- 另加存档归一化：list0 槽 3..8 的 Kind1 堆叠物（旧 GM 发放的合成器等）迁到 57..88 消耗品页
  （Economy-State-FG NormalizeEmblemExpertSlots）——合成器此前躺在装备页。

## 6. 索西雅/奥兰奶奶/小铁匠商店买卖不了
- 23:51 现场 5 连 `NPC_PURCHASE_REJECTED Unregistered NPC offer`。根因：NPC 商店货物按 PVF
  itemshop/*.shp 展示，服务端 commerce-catalog 只登记了城镇扫描器看得见的部分——索西雅(shop5)
  12 件、奥兰(shop22) 11 件全缺，卡妮娜/辛达/夏洛克/弹金等 30 个商店共 3300+ 缺口。
- 修复：`local-run/pvf/backfill_commerce_offers_fg381.py` 直读 itemshop.lst 全量对账，复用
  gen_item_metadata（492 个新物品登记）与 gen_commerce_content 的 Evidence/item_row/apply_append
  （405 条 Items 行 + 追加 offers/shops）；Commerce-Catalog-FG71 loader 类别放行 **Kind8**
  （宠物装备可售可买，与 FG352/FG367 出售/丢弃闭环对齐）。无 [price] 的货物（PVP 点券商店等）
  保留跳过——金币商店无法给它定价。最终：索西雅 12/12、奥兰 9 件（2747165/2747180 无价跳过）、
  卡妮娜 73、辛达 42、弹金 73/11/12/26/12。
- 坑：回填第一版只加 offers 忘了 Items 行 → loader 拒载；v2 补齐（已修）。

## 7. 达人/霸王契约角色不互通
- 原版语义：premium 挂在 **CUser（账号会话）**（rednf WongWork::CUserPremium），不挂 CCharacter。
- 修复：新增 `Premiums-Account-FG381.cs` 账号级台账
  `<store>/premiums-account1-FG381.xml`；Sync() 每轮"角色缓存→台账 max 合并、台账→角色缓存
  覆盖"（购买/GM 加成自动上浮，内容寻址 receipt 幂等）；GM `premium del` 同步删台账
  （删除无法走 max 合并）。旧角色档里的 premium 首次登录自动并入台账，全体角色共享。

## 8. 升级到 70 拿不到"时代先锋"
- 称号物品 26628（`equipment/character/common/title/i_am_lv70.equ` DNF时代先锋），授予任务
  `n_quest/title/titlebook_1_general30.qst`：`[grade] [title]` + `[custom quest]` +
  `[int data] 1 0 70`（角色达到 Lv70，无 NPC 交递）+ `[reward type] [item] 26628`。
- gen_title_awards.py 只收 `[reward type] [title]` 所以漏了它（AwardsFG205 无 6531 行）；
  quest-catalog 也没登记 6531。修复：TitleBook-Service-FG157 AwardsFG205 手补
  `{6531,26628,0,46,6531}`（FG211 先例）+ `Lv70TitleHealFG381(receipt,level)` 双钩子：
  LevelChanged（升级瞬间）与 World()（已 70 级的存量角色进世界补发），op369/368 推送。

## 部署与验收
- 停服 → `scripts/build.py --smoke`（199 源）→ 数据同步 build/stable → `one-click.ps1`
  （自检误报 0/2，实际双服已起：频道 7002 / 入口 17001）→ LoginDriver LOGIN_OK。
- 离线：`Triage-FG381-Check` 28/28；`TpShopQuest-FG215` ALL PASS。
- 玩家侧自验点：进角色后 r8 背包里 5 个装扮合成器会出现在消耗品页；契约在别的角色登录后生效；
  已 70 级角色进世界即得时代先锋。

## 追补（FG382，02:35 部署）：奥兰奶奶宠物装备购买"失败但已扣钱"
- 玩家实测 02:24：三连 `NPC_PURCHASE_REJECTED NPC committed row absent`。根因：BuyNpc 在事务
  **提交后**回扫新行时只看 list 0，而 Oran 全部商品（63502..63514、2747155..2747180）都是
  Kind 8 → AddItem 路由进 list 7（宠物装备栏 140..188）→ 永远找不到 → 回失败包。
  期间 5 笔购买实际都已入档（r8 金币 938301→302961，物品在宠物装备栏），只是客户端被告知失败。
- 原版依据：rednf `DisPatcherCore/process_21.inc`——生物物品同样走 insertItemIntoInventory
  + 成功回包，另有 InsertCreatureItem 生物栏同步；回包 wire 本身无列表字节。
- 修复：行扫描改为全列表（按 (list,slot) 对比 before 数量差）；Reply 类别放行 Kind8
  （wire 不带列表字节，列表语义由后续整包刷新表达）；AdditionalRows 主行匹配改 (list,slot)；
  提交后对 List!=0 的发放补发宠物装备栏整包刷新（PetBagReply，与商城/魔力袋同款通道）。
- 验收：`Triage-FG381-Check` 增至 31 断言（含 Kind8 购买全链路）ALL PASS；02:35 构建、
  one-click 重启、LoginDriver PASS。
