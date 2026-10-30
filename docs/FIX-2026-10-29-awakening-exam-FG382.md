# FIX 2026-10-29 觉醒考试场「月光修炼场」进不去 (FG382)

## 症状

玩家接了觉醒任务（Epicjob achievement_50_* 系列，如 976「觉醒 - 剑圣 (3/4)」），要求"独自通关
赫顿玛尔市区里的『月光修炼场』"，但月光酒馆门里进不去该副本。

## 取证结论

- 副本号 **511 = dungeon/quest/training2.dgn**，显示名正是「月光修炼场」（同显示名的还有
  500/505/507/508 转职考试场）。`worldmap/moonlightpub.wdm` 的 `[dungeon]` 块里 511 有
  **33 行 `[in progress]`**，第 3 列就是全部 Epicjob 觉醒任务 id（905..997 / 5309..5352 /
  5680/5686 / 7614..7636 / 4084/4088 / 158）。
- 511 布局：2x1，起始房 (1,0)=**15129**（4 只陪练 61300/61301，等级 = basis 53），boss 房
  (0,0)=**15270**（怪 **61262 阿尔伯特**，`[NPC] 1009`，kind3 boss）；minLv 50、
  `[designate dungeon difficulty] 0`、`[limit party count] 1`。
- 服务端缺口（全部都没有 511，507 阿修罗转职考试场同样缺）：
  1. `Dungeon-Selection-Protocol.cs` 的 `PubBody()`（op5 档案）与 `SupportedInArea()` 只有
     {140,500,505,508}；
  2. `Forest-Content-FG75.cs` 无内容行 → op16 的 `Begin()` 抛 "Unsupported original dungeon
     profile"；
  3. `Clear-Experience-FG116`/`Monster-Experience-FG115`/`Elite-Monsters-FG118` 无结算行；
  4. `Floor-Catalog-FG119` 无 MapRolls/Pools → 击杀会抛 Floor map roster / Floor monster
     profile；
  5. 507 的 boss 房 15122 是 `[ai character]` APC 房（G.S.D 1514），`Apc-Monsters-FG.cs` 无行。
- 任务链本身已通：976 `Supported=true`；op35 [meet npc] 访问 FG362 已放开（1009 阿尔伯特在
  允许清单注释里点名）；op36 完成不卡 NPC 位置。唯一缺口就是副本本体。

## 修复

1. `local-run/pvf/gen_dungeon_content.py`：DIRNAME 增 `507: "training_asura", 511: "training2"`；
   顺带修过期匹配 `internal ushort MapFG130` → `ushort MapFG130`（FG130 源文件签名早变了）。
2. `patch 507` / `patch 511` → Forest-Content-FG75.cs 全套行（compare 0 mismatch）；
   `patch_levels` → Clear basis 23/53、XP weight 1.0f、champion {6,12,18,24,26}；
   `patch_floor_catalog` → MapRolls 15120(8)/15121(11)/15122(0)/15129(4)/15270(1)，
   Pools 占位 61262。**未跑 patch_new_mechanics**（两副本 .dgn 无 condition/event/seal 数据，
   该函数是整文件重写，不动它最安全）。
3. `Apc-Monsters-FG.cs` 手工加行 `{507,{15122,1514,84,8}}`（与 508 同一只 GSD 考官 APC；等级
   取 aicharacter.lst 1514 → cc_gsd.aic [minimum info] 第 5 字段 84）。511 的 boss 是
   `[monster]` 名册怪，不需要 APC 行。
4. `Dungeon-Selection-Protocol.cs`：`PubBody` → {140,500,505,507,508,511}，`SupportedInArea`
   town2 area4 子句加 507/511，注释更正（那批 wdm 行是 quest dungeon 不是 event dungeon）。

## 验收

- `local-run/driver/AwakeningExamCheck-FG382.cs`（全源 rsp + `-main:AwakeningExamCheckFG382`，
  产物 local-run/build-verify-fg382/）：**48/48 PASS**——几何/格子/掩码/怪表/门档案/结算表/
  地板目录/APC 行 + 引擎真实 `Begin(511,0)`/`Begin(507,0)`。
- 全量回归：`gen_dungeon_content.py compare <全部受支持副本>` 仅剩 2 个**文档写明的既有偏差**
  （7027 APC 替身、d51 哈穆林手工布局），其余 0 mismatch；HeroGateCheck-FG358 47/47；
  BwangaChainCheck-FG377 308/308；check_repo 428 文件；build.py --smoke 199 源。
- 部署：2026-10-29 03:10 restart-detached，7002/17001 双服 LISTENING。

## 玩家口径

重启后生效：接了觉醒任务后，去月光酒馆（赫顿玛尔）的门里就能看到「月光修炼场」（限定单人、
固定难度）；进门先过 4 只陪练，boss 房单挑阿尔伯特；通关后回酒馆与阿尔伯特对话交任务。同批
顺带修好阿修罗的转职考试场（507，考官 G.S.D）。

## 已知残留

- 考试场怪（61262/61300/61301/507 各房）掉落池为占位空池（与此前 61300/61301 已上线状态一致）；
  如需真实掉落，跑 `gen_monster_pools.py`（它会填全部空占位池，注意会带来无关 diff）。
- 140 月光酒馆本体的 5310 地狱 APC 仍是任务门控（FG211 的既有决定），未动。
