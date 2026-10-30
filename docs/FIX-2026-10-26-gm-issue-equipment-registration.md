# 接入：GM 发道具的武器/防具/首饰/左右槽/称号（2026-10-26）

日期：2026-10-26。玩家反馈登录器 GM 发道具时大量物品报「该物品未接入服务端物品表」。

## 根因

`Offline70TestItemsFG106.Supported()` 要求 id 已在运行时物品表注册；PVF 的
`equipment/equipment.lst` 有 50,136 个装备 id，其中 34,980 个从未登记。目标五类
（武器、防具、首饰、辅助装备/魔法石、称号）缺 20,817 个，占这些类别总数的 80%。

## 改动

- `src/Tools/VmTestKit/Item-Metadata-FG.cs`：单文件上限 8 MiB/20,000 条 →
  64 MiB/100,000 条（`MaximumBytes`/`MaximumItems`）。目标五类全部登记后主表
  37,748 条、12,029,814 字节，旧上限会直接拒绝启动。
- `data/item-metadata.xml`：在 `</Items>` 前追加 20,817 条
  `Offline70EconomyItemDefinition`，旧有 16,931 条逐字节未改（`</Items>` 之前的
  前缀与文件尾完全一致）。
- `local-run/pvf/gen_item_metadata.py`：新增 `equipment-batch` 模式。按
  `[equipment type]` token 选类，先对同域已登记记录跑 `equipment_validation_gate`
  再追加；字段全部来自 `.equ` 的 `derive_equipment`，拒绝项逐条打印且不猜。
  同时把 `MAX_FILE`/`MAX_ITEMS` 同步到新的加载器上限，并修正紧凑块追加时首行没有
  换行、导致旧文件最后一行被一起改写的问题。

## 登记结果

| 类别 | token | 新增 | 现在总计 |
|---|---|---:|---:|
| 武器 | `[weapon]` | 4,670 | 6,796 |
| 上衣 | `[coat]` | 1,992 | 2,477 |
| 下装 | `[pants]` | 1,979 | 2,458 |
| 肩 | `[shoulder]` | 2,059 | 2,384 |
| 腰 | `[waist]` | 2,045 | 2,355 |
| 鞋 | `[shoes]` | 1,841 | 2,394 |
| 戒指 | `[ring]` | 1,721 | 1,817 |
| 手镯 | `[wrist]` | 1,692 | 1,779 |
| 项链 | `[amulet]` | 1,514 | 1,793 |
| 辅助装备（左槽） | `[support]` | 171 | 523 |
| 魔法石（右槽） | `[magic stone]` | 121 | 218 |
| 称号 | `[title name]` | 1,012 | 1,128 |

字段推导：Kind 4（槽位 10..21）、StackLimit 1、MinimumLevel 取 PVF、Job 255 +
JobMask 取 `[usable job]`、EquipSlot 取 `[equipment type]`、MaxDurability 取
`[durability]`（无标签为 0）、Cooldown 0。同域已登记的 5,305 条里 4,998 条与重新
推导完全一致；剩余差异只有已知历史约定（EquipSlot 0 的 29 条、Job/JobMask 单职业
两种编码 263/262 条、无耐久标签存 1 的 15 条），本次不改写旧记录。

## 验证（2026-10-26）

- `python gen_item_metadata.py equipment-batch --apply`：追加 20,817 条；
  再跑一次输出 `nothing to append`（幂等）。
- `python gen_item_metadata.py verify`：`records 37748, distinct 37748, bounds
  violations 0`；floor126/card125/commerce/object-drop/coupon/quest 引用全部解析；
  `VERIFY PASS`。
- `python scripts/check_repo.py`：通过（171 稳定源）。
- `python scripts/build.py --smoke`：171 源编译成功，无参冒烟 exit 2。
- 独立存档 + `--test-port 17002`（`local-run/verify-gm-gear/`）实发：
  `33995`(武器)、`10839`(上衣)、`420003`(戒指)、`440299`(左槽)、`450031`(右槽)、
  `2747118`(称号) 全部 `success=1`，存档 `before 0 → after 1`，槽位/耐久正确；
  旧物品 `2681594` 回归成功。
- 负向对照：未接入的装扮 `42600` 仍返回 `success=0` /
  「操作失败：该物品未接入服务端物品表。」，说明闸门没有放宽成无条件放行。
- 线上双服已用 `build/stable` 重启（channel pid 38320 :7002、entrance pid 35444
  :17001），`build/stable/item-metadata.xml` 与 `data/item-metadata.xml` 同哈希
  `7533bb742be891eab55b6944db7672d8`。

## 边界

- 本次只接入目标五类。仍未接入：装扮 13,877、光环 97、宠物/蛋 161、名称装饰卡 15、
  神器 13，以及堆叠品 11,465（设计图/礼包/徽章等）。
- 宠物仍受 `Offline70PetFG96.IsEgg` 的 4 个硬编码蛋限制；光环（槽 9）和名称卡
  （未知 token）仍被登记器拒绝，不属于本批。
- `local-run/pvf/gen_item_metadata.py` 不在服务端 git 仓库内，属于本地登记工具。
- `dist/` 下的发布包未重新打包；本次部署目标是工作区 `build/stable` 与线上 store。
