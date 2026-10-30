# 2026-10-26 商城两件礼包购买报“使用期限设置错误”（FG272）

## 现场

用户购买商城里的这两个商品，op67 被服务端拒绝，客户端只显示 `错误：使用期限设置错误`：

| 商品 | commodity | 礼包道具 | 价格 |
|---|---|---|---|
| 王者成长礼包 | 101117 | 2682620 | 1980 点券 |
| 魔王之契约礼包 (60天) | 100625 | 2682006 | 3880 点券 |

归档日志 `local-run/logs/archive/20261025-233755/channel-evidence.log`：

- `23:27:54 CASH_PURCHASE_COMMITTED commodity=101116 ...`（勇士成长礼包，已有实现，购买成功）
- `23:27:59 CASH_PURCHASE_REJECTED Unimplemented cash commodity, payment or option`
- `23:28:02 CASH_PURCHASE_REJECTED Unimplemented cash commodity, payment or option`

拒绝来自 `Cash-Purchase-FG45.cs` 的兜底分支：两个 commodity 都不在 FG80/FG108/FG129 的任何目录里，抛异常 → 回 0x13 → 客户端把 0x13 显示成“使用期限设置错误”。这不是有效期字段坏了，而是商品没有登记。

## PVF 证据（未改动的当前 CN PVF）

`etc/cerashop.etc` 原始节（pvfUtility HTTP 读取）：

```text
[package]
    101117  2682620  0  0  1980
    `王者成长礼包`  1  70043  -1  -1
    255  101116  2682619  0  0  1260
    `勇士成长礼包`  1  0  -1  -1

[charac premium package]
    100625  2682006  3880
    `魔王之契约礼包 (60天)`  0  0  1  60  100614
```

道具脚本：

- `stackable/cash/chn_20130108_New_grow_up_PackageAB/chn_2013_Grow_up_Start_PackageB.stk`
  → `[package data include usable period] 2681943 1 0  1 10 0  8 50 0  45 1 0  33 1 0`
  （王者成长礼包：帐号角色关联券 7天 ×1、复活币 ×10、HP 药 ×50、达人之契约 7天 ×1、霸王之契约 7天 ×1）
- `stackable/cash/chn_contract_devil/chn_60days_pack.stk`
  → `[package data]` 八个服务标记：
  `2681934 2681931 2681932 2681933 2681929 2681930 2682205 2682206`，各 ×1
- `[selectable character premium]` 的 7 天行把上面这八个道具一一映射到 premium type 0..7
  （2681934=0、2681931=1、2681932=2、2681933=3、2681929=4、2681930=5、2682205=6、2682206=7）；
  2682377（type 8，魔王之契约专属任务）不在这个礼包的内容里。

## 代码修改

1. 新增 `src/Tools/VmTestKit/Cash-Package-FG272.cs`
   - 101117：Special=10，Category=4，Once=true，Rewards 取自 Start_PackageB 的五行；
     直接发内容，不发 `[cera package]` 盒子（op47 没有盒子开启路径，发盒子会卡在背包里）。
   - 100625：Special=11，Category=4，Days=60，Rewards 为八个服务标记（仅供 op67 成功面板显示）；
     Grant 把 premium type 0..7 写入 `PremiumsFG129`，60 天，重复购买按 `max(now,old)+60d` 叠加。
   - 登记 33 / 45 / 2681943 的 item metadata（Kind=1；2681943 的脚本声明最低 60 级）。
   - 包内没有可选变体，所以只要求 payment=0（点券），option/period 字节忽略；两个 commodity 之外的请求返回 null，继续走原链路。
2. `Cash-Purchase-FG45.cs`：`Register()` 末尾调用 `Offline70CashPackageFG272.Register()`。
3. `Feedback-Cash-FG108.cs`：目录解析、`Valid`、`Purchase` 各加一条 Special 10/11 分支。
4. `Contract-Cargo-Skills-FG129.cs`
   - `Validate` 允许 premium type 0..8，并把 `PremiumsFG129` 上限从 3 提到 16（8 个服务 + 原有 22/27/29）。
   - `CashRefreshFG129` 对 Special=11 也触发 `PremiumNoticesFG129()`，买完立即发 op66，不必重登。
5. `Economy-State-FG.cs`：`CashOncePurchases` 白名单加入 101117（与 101116 同一条一次性购买约束）。

## 离线验收（未开 socket、未碰在线存档）

`local-run/scratch/cash272/`：

- `PackageCashCheckFG272.exe`：25/25 PASS。覆盖真实 Resolve → Cart Purchase → Economy Validate 链：
  两个商品解析、内容行、CERA 支付边界、扣款、背包发放、复活币、一次性记录、60 天 premium 0..7、
  二次购买延长 60 天、重复购买被拒且无副作用、混合购物车原子性、与 101116 同车购买、每次提交后重读校验。
- `LiveStoreCheckFG272.exe`：把 `local-run/DNF70-batch-test-FG` 的 economy + 账号点券侧车复制到临时目录后：
  8 个角色档全部通过新校验；70 级角色 214384 点券一次买下两件（扣 5860）；
  王者礼包内容入包、契约礼包 8 个服务 60 天全部写入并重读通过。在线存档本身未被写入。

## 部署状态

按用户要求**没有重启服务端**。当前 7002 上运行的 `DNF70-Channel-FG.exe`（2026-10-26 00:03 构建）
仍是旧二进制，用户继续购买这两件仍会弹窗。源码已就绪，下一次
`python local-run/one-click.bat -Build`（或停服 → `python scripts/build.py --smoke` → 起服）即生效；
构建会自动收录 `VmTestKit` 下全部 .cs，无需改数据文件。

已知后续（不阻塞本次修复）：王者礼包发的 33/45/2681943 是 `[etc]` 契约票，购买本身已成功；
但这三个道具的 op47 使用路径（霸王/达人契约生效、帐号角色关联）仍未实现，使用时会被通用 Use 拒绝。

## 第二轮（同日）：全目录扫尾，未接商品一次接完

用户要求把除魔王之契约礼包外的全部未接商品一起接上。以 rednf 的 `importCashShopItemList`
记录布局（coin/item/premium/creature/avatar = 6 int + name + 2 int；package = 5+1+4；
nametag = 6+1；selectable character premium = 基行 6+name、后续 8 int+name）解析整份
cerashop.etc，与 FG80/FG108/FG129/FG152/FG210/FG211/FG272/宠物模块的 commodity 全集求差。

### 差集与处置

| 类别 | commodity | 处置 |
|---|---|---|
| [selectable character premium] 18 个单项服务 | 100537-100542/100545-100550/100817/100818/100882/100883/100983/100984 | FG129 Special=3 直接激活 premium type 0..8（7天/30天、价格逐行核对；item id 与 .stk 脚本名逐一对齐验证过映射） |
| [premium] 关联券/更名卡 | 100543/100544/100551/100552/100902 | FG272 Special=10 发对应票券物品（2681935/2681936/2681943/2681944/2682290）；关联/改名系统本身未实现，同礼包票券的取舍 |
| [item] 零散 | 100782 纯净的灵魂气息、100534 帐号金库升级工具、101100/101101 远古精灵的精华秘药 | FG272 Special=10 发物品（秘药按脚本登记 StackLimit 3、冷却 3600000ms 组 99；exp 加成 buff 机制未实现） |
| [creature] 奥兰遗失的袋子 | 101076/101077 | FG272 Special=10 发 2682571 ×1/×5；开袋（booster）FG110 已实现，买完即可开 |
| [avatar] 5 个无内容跳过的时装 | 39056/40311/53849/53850/53851 共 15 行 | 当时 pvfUtility 返回空内容被生成器跳过；本轮手工补 15 行 XML + 5 条 Kind=3 metadata（时段价格 300/660/1980、Options 按脚本 ability 行数） |
| [premium] 尾标记 99 的停用行 | 117465/100043/100061/100122 | 不接：数据自带停用标记（价格 1 的测试行与重复 SKU），保持干净拒绝 |

### 代码与数据改动（第二轮）

- `Contract-Cargo-Skills-FG129.cs`：BuildOffers 追加 18 条 mode=3 服务项。
- `Cash-Package-FG272.cs`：Offers 追加 11 条 Special=10 物品发放；Register 追加
  2681935/2681936/2681944（60级）、2682290（30级）、2682170（StackLimit 10）、2681921、
  2682616/2682617（StackLimit 3、冷却组 99）的 metadata。
- `data/avatar-cash-catalog.xml`：追加 15 行（28584 → 28599，[avatar] 节全覆盖）。
- `data/item-metadata.xml`：追加 5 条 Kind=3 记录（39056 帽/40311 上衣 JobMask=1，53849-51 鞋 JobMask=8）。

### 验收（第二轮）

- `CatalogSweepCheckFG272.exe`：**ALL PASS** —— 28599 个时装 commodity 全量 Resolve 通过；
  111 个普通 commodity（FG80+FG129+FG272）全量 Resolve；8 个稀有装扮兑换、30 个名称装饰卡全量；
  停用行 100043 保持拒绝；8 件新类商品一车购买（扣 11990）后 premium 0 延至 37 天、type 8 生效 7 天、
  四种物品入包、7 天时装入时装包且带期效，重读校验通过；100039/101117/100625 回归不变。
- `PackageCashCheckFG272.exe` 25/25、`LiveStoreCheckFG272.exe` 全部 PASS（在线档 8 角色载入 + 副本购买）。

部署口径不变：源码就绪，**未重启**；下次 one-click -Build / build.py 后一起生效。

## 第三轮（同日凌晨）：契约票"使用不了"与魔王页签灰的根因与修复

### 现场（01:45 重启后、01:51 实购）

- 101117/100625 两单 `CASH_PURCHASE_COMMITTED` 均成功；100625 后服务端发了 10 条 op66（0..7 + 既有 22/27）。
- 背包里有 33/45/2681943 各 1 张；**整场日志 0 条 op47**——客户端对 `[etc]` 票据根本不发使用包。
- 期限道具信息栏"优惠"页显示 霸王(22)/达人(27)，说明 op66 通道本身是通的；"魔王之契约"页签灰。

### 根因

1. **契约票不是"使用型"道具**：官方 `CCeraShop::IsSpecialItem` 把票据 item 映射到 premium type
   （31→18、33→20、35→22；44→24、45→25、47→27；137→29、193→30），购买时就地
   `handleSetUserPremium` + op66，背包里的票只是凭证。我们的礼包把票塞进背包却没记账 ⇒ 右键无反应、契约也没生效。
2. **魔王之契约页签走的是另一套"角色增值服务"**：`chn_characpremium.etc`（9 个服务的行为表）+
   `premiumserviceeffect.etc`（type→图标）。`[selectable]` 行第 5 列 0..8 是服务序号不是 premium type
   ——`premiumlist.etc` 里 0/6/8 没有条目，1-5/7 是网吧类增值，所以客户端对 0..7 不渲染该页签。
   参考端的 `PREMIUM_SERVICE` 通知是 op537，而这个 2013 客户端接收分发表只到 op368（表尾 0xCC 填充，
   内存读表确认），页签数据必然走其它通道，正确 type 待定位。

### 本轮代码改动

1. `Cash-Package-FG272.cs`
   - 王者礼包（101117）发放时不再落 33/45 两张死票，改为直接延长 premium 22/27 各 7 天（Days=7）；
     2681943 关联券照旧落包（其角色关联系统 op313-315 未实现，见下）。
   - 新增 `MigrateTickets`：进世界时把背包里已存在的 33/45 各转换成 7 天契约并移除（回执
     `fg272-ticket-migrate` 幂等，一人一次）。2681943 不动。
2. `Contract-Cargo-Skills-FG129.cs`：CashRefresh 对 Special=10 也重发 op66（买完立刻生效）；
   premium 合法 type 放宽到 0..105（官方 g_SPremiumInfo 空间），条目上限 16→32。
3. `Batch-Service-FG.cs`：World() 在 PotExpiry 之后调用 `MigrateTickets`。
4. `GM-Console-FG201.cs`：新增 `premium` 操作（`mode=add/del kind days`），当场重发 op66——
   用于逐段试探魔王页签认可的 type（建议候选：49-58、62-75、31-34），锁定后把 100625 的
   PremiumKinds 换成正确值。

### 验收

三件套全部重跑通过（167 源）：`PackageCashCheckFG272`（新增：购票后 22/27 各 +7 天、33/45 不再入包）、
`CatalogSweepCheckFG272`、`LiveStoreCheckFG272`（在线档副本：旧 22/27 各 +7 天、8 服务 60 天、重读通过）。

### 待办

- 魔王页签 type 定位（GM 探针，需一次重启后由用户配合看页签）。
- 角色关联系统（op313 CONNECT/DISCONNECT/CHANGE_CHARAC_LINK_TYPE + 关联券通知包）未实现。
- 契约票官方 type 细分（18/20/22、24/25/27）目前统一用 22/27 承载时长叠加，效果等价、显示合并。

## 第四轮：魔王页签 = 限时道具页签（2026-10-26 下午）

### 本轮取证

1. `.str` 字符串表在客户端内存里的布局：`{hash(u32), key_ptr, text_ptr}` 12B 步长；
   `time_limited_items_info_window_00/01/02` 的 hash = `0xAF87E474/75/76`，对应
   `期限道具信息栏/优惠/魔王之契约`（表在 heap，exe 里没有 hash 常量，说明拼写按 key 查）。
2. 魔王 8 项服务的名字（高效开罐/自动修理/…）**不在任何 .str 表**，只存在于各 `.stk` 的内联
   `[name]` —— 说明页签里的条目按**道具**渲染，而不是 premium 类型（上一轮 23 个 type 探针
   只改图标栏/优惠页，已排除 premium 路线）。
3. `Economy-Protocol.InventoryRow` 对**所有**道具都写 `ExpiresUnix`（+22），客户端库存行本就带
   到期时间；只是服务端 `Economy.Validate` 之前禁止非时装道具带期效（`d.Kind!=3 && ExpiresUnix!=0` 抛）。

### 改动

- `Economy-State-FG.cs`：允许 **Kind=1** 道具带 `ExpiresUnix`（Kind 3 之外唯一例外）；`Use` 增加
  过期拒绝。
- `Cash-Package-FG272.cs`：100625 的 60 天礼包不再写 premium 0..7，改为发放 8 个服务标记道具
  （2681934/2681931/2681932/2681933/2681929/2681930/2682205/2682206）并各自带 60 天期效；
  重复购买按 `max(now,old)+60d` 叠加。新增 8 条 Kind=1 物品元数据。
  新增 `MigrateServices`：把已有角色档里的 premium 0..7 换成同到期时间的 8 个限时道具并删除 0..7。
- `Batch-Service-FG.cs`：World() 在 MigrateTickets 之后调用 MigrateServices。

### 验收

- `PackageCashCheckFG272`：ALL PASS（8 道具 60 天、0..7 不再写入、重复购买 +60 天、迁移/幂等、重读校验）。
- `CatalogSweepCheckFG272`：ALL PASS；`LiveStoreCheckFG272`：ALL PASS（在线档副本购两件、8 道具、
  22/27 契约各 +7 天、重读通过）。
- `check_repo.py` 通过（167 稳定源）。**未重启**（用户在线）；下一轮一键启动后
  World() 会把现有 premium 0..7 迁移成 8 个限时服务道具，届时打开期限道具信息栏验证魔王页签。

## 第五轮：回执反推补发（首轮迁移空跑的原因）

首轮 `MigrateServices` 上线后进世界没发出 8 个道具：因为 02:39 的探针清理已经把该角色的
premium 0..7 删掉了，迁移找不到"买过 60 天礼包"的痕迹（premium 是唯一线索）。

修复：`ServiceDaysFG272` 新字段 + 回执反推。
- `MigrateServices` 统计 `Kind=cash-selection` 且 `PayloadSha256 = SHA256("cash-selection:|100625:11:0:60")`
  的回执数 ×60 天（实测 r8 有两张该回执 = 120 天），把 8 个服务道具发到 `now+120天`，
  写 `ServiceDaysFG272=120`；之后每次进世界命中 `purchased<=ServiceDays && 道具齐 && 无 0..7` 即跳过。
- 以后购买 100625 时 `Grant` 同步累加 `ServiceDaysFG272` 并延长道具，和回执计数保持一致，不会重复补。
- 驱动新增两条断言（回执迁移 120 天、二次迁移 no-op），三套驱动与 check_repo 全绿；
  11:44 构建已部署（`fg272-service-sync` 在 exe 中）。
