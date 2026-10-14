# 合并边界

稳定基线是 FG175；研究塔是独立 152 源候选且 `releaseAllowed=false`。二者共有文件内容不同，不能整目录覆盖，也不能直接把研究的 Batch-Service 或 Economy-State 换进 main。

| 区域 | 主要文件/数据 | 修改边界与必要联检 |
|---|---|---|
| 任务/称号 | Quest-*、Progression-*、TitleBook-*；quest-catalog.xml | 按任务 ID 分工；前置、材料、选奖、称号库存、outbox、旧档和重复交付一起核对 |
| 普通副本 | Dungeon-*、Forest-*、GBL-*、Room-*、Town-* | 按副本/房间分工；入口、等级/难度、加载、清房、移动、领主、结算联检 |
| 掉落/物品 | Item-*、Floor-*、Object-*、Clear-Items-*；item-metadata.xml、掉落表 | 不私自扩大掉落范围；真实掉落/拾取/满包恢复、扣量和重登联检 |
| 商城/NPC/宠物 | Cash-*、Npc-*、Commerce-*、Pet-* 等 | 公共经济写入只能走同一事务入口；新功能不得绕过收据或背包校验 |
| 门票/疲劳/黑钻 | Tower-DailyTickets-*、Fatigue-*、Black* | 20级、每天本地06点共享票补至5等现有规则保留；时钟、满包、同日重复联检 |
| 核心共享区 | Batch-Service-FG.cs、Economy-State/Protocol-FG.cs、Progression-FG.cs、Role-*、各 Cipher/Protocol | 提前声明，多人同改由一人集成；协议与经济/任务审查不能省略 |
| 研究塔 | research/tower/src、research/tower/data | 独立分支与输出；同步稳定修复按小补丁双向合并，完整塔未验证不能晋升 |
| 构建/发布 | scripts/、.github/、baseline/ | 构建源清单、数据版本、哈希和检查同步修改；基线 manifest 只读保留，新增基线另建记录 |
| 客户端 | 另行记录源码/补丁与本地资源依赖 | 当前仓库不含客户端二进制；不能顺带切 v51/52/53 原型，任何变更需单独证据和人验收 |

## 冲突处理

先记录 PR 的 base commit 和关联文件；rebase/merge 最新 main 后逐块检查语义，禁止“全部取我的/全部取对方”。XML 冲突按 ID 合并，检查重复 ID、前置和引用；经济字段冲突检查序列化兼容与原子保存；协议冲突核对实际字节长度和字段顺序。解决后重新编译并补跑受影响回归，不能只看冲突标记消失。

## 晋升塔功能

研究者提供相对当前 main 的最小补丁、数据白名单、原版本依据、真实请求/容量/重复/重登及旧功能回归证据，明确 AI、spawn、药品完整效果、退出清理、失败/成功奖励和真人结果。集成人逐项审核后才更新稳定源码与发布清单；不能把此前“内部回放通过”直接当作六塔已完成。
