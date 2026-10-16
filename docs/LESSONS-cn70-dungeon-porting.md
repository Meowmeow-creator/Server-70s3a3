# 经验总结：CN70 副本/区域移植（2026-10-15，经三站验证：暗黑城、阿法利亚、刹影）

## 一、能重复的工作流（三站都用它，直接照搬）

1. **先取证，后动手**。三份证据源各有分工，不要混用：
   - **PVF（客户端数据）**＝"是什么"的唯一来源：`.dgn`/`.map`/`.lst`/`.etc`/`.mob`/`.aic`，用 pvfUtility HTTP API(27000) 读（老 GUID 格式只能这样读）。
   - **rednf（原版服务端逐字节重建）**＝"怎么算"的蓝本：字段布局、状态机、公式、包体结构，注释常带 ORIG 地址，可信度高；但**内容数据不可用**（台服 1031 ≠ 国服 70），且协议字段宽度可能与本客户端不同。
   - **客户端反汇编**＝只在上面两者都说不清时用（行为分歧、崩溃根因）。静态优先；动态调试必须有明确的断点清单和"改服务端字段即可验证"的对照组。
2. **数据一律走生成器，不手抄**：`local-run/pvf/gen_*_content.py`（`compare` 零差异 → `patch` 写回 C# 表）。手抄必错；生成器同时是最好的文档。
3. **每条结论要能指到行**：文件:行号 + 原文 + 【已验证】/【推断】。**子 agent 的"总结"要和它的 dump 文件抽查对照**——本次就出现过"仅 h5401 有 `[monster condition]`"的错结论（实际 22 张图），照抄会做错整套掉落/门禁。
4. **隔离实例是硬性前提**：17003 频道 + 存档副本（`local-run/tools/start-isolated.ps1`），线上 7002 只在最后部署；驱动按 `gate→op5→op16→op29→op40→op48→op49→op75` 走完整流程。
5. **验收两层，缺一不可**：
   - 静态：表/反射断言（`Act4ContentCheck`/`Act4MechanicsCheck` 风格）+ **运行时状态机单测**（例：未击杀封印石→首领格锁定；击杀→解锁）。
   - 隔离线上：真实报文（op42 死亡上报、op48 移动、op49 结算）；**必须断言"客户端能看到的东西"**——结算页＝op127+op31 必须真的下发，否则会漏掉"服务端自洽、客户端收不到"的 bug（本次刹影就是这类）。
6. **部署三步**：停服务（否则 build CS0016）→ `check_repo` + `build --smoke` → `restart-detached.ps1` + `launch-client-detached.ps1`，最后把诊断工具重新挂上。

## 二、踩过的坑（判断"机制坏了"之前先排除这些）

- **服务端代杀 ≠ 客户端权威**：客户端是战斗权威方，它自己的击杀账簿（任务推进、门禁条件计数）**只在客户端亲手击杀时推进**；`DNF70_TEST_ONE_HIT_KILL=1` 这类测试辅助会让机制"看起来坏了"（守门人台词不翻、条件不消、goblin 乱刷）。**先确认 kill 是谁杀的**（日志里 `ONE_HIT_KILL_SWEEP testAid=True`）。
- **uid 空间**：APC/条件怪/事件怪必须与同房怪物**共用 100..163 连续计数器**，且计入房间怪物总数；否则客户端认不到 actor（整局 0 条 op42/op49，静默）。
- **wire 字段按客户端版本，不照抄蓝本**：房间条目 `mobIndex` 在 CN70 客户端是 u16（rednf/TW1031 是 u32，16 字节 vs 14 字节）；事件怪 `mobId` 是 10000+ 区间标记；死亡包尾巴是 `[0][boss类回真计数 : 普通怪回 -1][flag34]`。
- **"数据在、没人用"比"算错"更常见**：`[boss room entrance condition]`、封印门、事件怪都出现过"生成器/表里有，运行期没接线"。新机制落地后用**数据 id grep 全仓**确认运行期真的读了它。
- **三处同步**：新增源文件后驱动 `.rsp` 源清单会过期（假 "name does not exist"）；新增表要同步加静态检查与驱动断言；新增 env 开关要写进文档。
- **进程与文件锁**：服务在跑时 build 报 CS0016；重启前清 `channel-evidence.log*`（CreateNew）；隔离实例与线上**不能共用存档目录**；存档副本在每个 driver 前重置（任务状态残留 → 第二次跑出现"假失败"）。
- **工具层会损坏 `\70`**（八进制转义）：写路径用正斜杠或 `chr(92)`；`.ps1`/`.bat` 要 UTF-8 BOM；`grep` 在 GBK 日志上不可靠，用 python 解码。
- **调试器铁律**：不能让调试器替进程吞异常（对访问违例回 `DBG_CONTINUE` = 同地址死循环，本次把客户端卡死过一次）；同地址重复 N 次必须自动 detach。
- **测试辅助开关要显式管理**：`DNF70_TEST_ONE_HIT_KILL` / `DNF70_EVENT_MONSTERS` / `DNF70_ACT4_APC_BOSS` 等默认值要写进文档，且**线上与隔离实例的取值分开说明**，否则"线上表现"会被辅助开关污染。

## 三、本次建立的可复用资产

- 生成器：`gen_dungeon_content.py`（.dgn+房间+条件怪/事件怪/封印门/首领房条件/等级/掉落）、`gen_monster_pools.py`（.mob→掉落池）、`gen_town_content.py`、`gen_quest_content.py`。
- 驱动：`Act4ContentCheck`、`Act4MechanicsCheck`（31 项，含运行时闸门单测）、`Act4LiveCheck`（全流程 + Target/PathBetween 走位 + 清关握手断言）、`NoirperaCheck(±Live)`、`AlfhlyraQuestCheck`、`GateLiveSuite`。
- 工具：`tools/start-isolated.ps1`（17003 隔离栈）、`tools/crash-capture.py`（客户端静默自尽捕获，断点 `0xE8E513` 等）、`tools/stop-watchdog.ps1`。
- 文档：`docs/HANDOVER-2026-10-15-act4-port.md`（三站实现/验收/未完成清单）、`docs/FEATURE_STATUS.md`。

> 后续轮次（万年雪山接入、掉落缺口）的经验与交接见 `docs/HANDOVER-2026-10-16-snow-region-and-drops.md`。
