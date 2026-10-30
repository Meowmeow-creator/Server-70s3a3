# FIX 2026-10-30：背包"任务"页物品无法丢弃（FG400）

状态：**已编译部署，双服 14:35 重启**（Channel pid 41144 / Entrance 26176，7002 + 17001 监听）。待实机确认。

## 1. 现象（用户 2026-10-30 报告）

背包"任务"页里的东西丢不掉（城镇里和副本里都不行），也没有任何提示。

## 2. 取证：客户端确实发了请求，是服务端拒的

`channel-evidence.log` 里当场抓到两条被拒的删除请求，用
`local-run/driver/DecryptCapture-FG349.exe`（identity 密钥表 + `op%14` 密码）解密：

| 时间 | opcode | 明文 | 服务端结果 |
|---|---|---|---|
| 14:17:41 | 18 | list 0, count 1, `{reason 1, slot 124, item 3012, qty 4}` | `TOWN_DELETE_COMMITTED`（基线） |
| **14:17:48** | 18 | list 0, count 1, `{reason 1, slot 154, item 3209, qty 2}` | **`TOWN_DELETE_REJECTED Delete unverified item kind`** |
| **14:18:08** | 18 | list 0, count 1, `{reason 1, slot 153, item 4183, qty 305}` | **同一条拒绝** |
| 14:18:37/41 | 18 | `{reason 1, slot 84, item 1205, qty 2}` | `Town deletion inside dungeon`（副本里用城镇删除，正常拒绝） |
| **14:18:48** | 50 | `X 437, Y 264, list 0, slot 106, qty 1, byte11=1` | **`REJECTED opcode=50 reason=Drop50 reserved or padding`** |
| 14:19:19 | 50 | `X 988, Y 301, list 0, slot 123, qty 7, byte11=0` | `DROP_COMMITTED ground=2003`（成功，玩家随后 X 键捡回） |
| 14:21:11 | 50 | `X 300, Y 321, list 0, slot 130, qty 1, byte11=0` | `DROP_COMMITTED ground=2005 item=3303` |

槽 153/154 正是"任务"页：`Offline70Economy.PositionCore` 把 `[quest]`（Kind 5）固定在
**list 0 槽 153..184**，存档里这两格就是 4183×305 和 3209×2（`item-metadata.xml` 里两者 `Kind=5`）。

所以两条独立的原因：

1. **城镇删除（op18）**：`Offline70TownDeleteFG367.Apply` 的 kind 白名单是 `{1,2,4,9,10}`，
   Kind 5 直接吃 `Delete unverified item kind`。客户端给的 slot/item/qty 全都对得上，纯粹被白名单拦死。
2. **副本内丢弃（op50）**：解析器要求第 11..15 字节全 0。第 11 字节其实是**客户端确认标志**：
   整叠拖动发 0，凡是会弹确认框的拖动发 1（装备 04:36 slot 25、紫色材料 04:37 slot 136、
   "9 个里只丢 1 个" 14:18:48 slot 106 全是 1）。FG395 修过这一位，13:10 按玩家要求整体回滚时
   连它一起退回了严格版，于是这类请求又整包被丢——客户端保留本地预测的脚下物品，看起来就是"丢不掉也没提示"。

## 3. 修复（FG400）

| 文件 | 内容 |
|---|---|
| `src/Tools/VmTestKit/Town-Delete-FG367.cs` | kind 白名单加入 **5**；Kind 5 追加页约束 `slot 153..184`（`Delete quest page position`），其余 kind 的规则不变 |
| `src/Tools/VmTestKit/Item-Drop-Protocol-FG.cs` | `Parse`：第 11 字节按确认位接受 **0/1**（`>1` 报 `Drop50 confirm flag=N`），只校验 12..15 为 0；另把"这一路上面永远拒绝所以是死代码"的旧注释改成实测事实（36 字节落地行客户端确实吃得下） |
| `src/Tools/VmTestKit/Batch-Service-FG.cs` | `Discard` 白名单加入 **5**，并加 `Quest page drop position` 页约束；op40 落地行 / op14 增量 / 回包全部沿用现形，不新增销毁路径 |

FG395 回滚的其余语义**全部不变**：装备/时装仍然不能副本内丢弃（`DROP_DELETED` 销毁路径不进游玩路径）、
地面行仍是 36 字节、`rare-auto-pick` 仍关闭、客户端品级闸门字节保持 02。

## 4. 验证

- `local-run/driver/QuestTabDiscard-FG400.cs`（新）**31/31 PASS**：回放上表 5 条实机明文（op50 确认位 0/1、
  确认位 2 拒、第 12 字节非零拒、list≠0 拒；op18 两组任务页请求解析出 slot/item/qty）→ 在种子存档里
  把 4183(305)/3209(2) 真正删掉、材质页不受影响、收据重放不二次扣减、越页/宠物窗/超量全拒。
- `local-run/driver/TownDelete-FG367-Check.cs` **25/25 PASS**（原 23 项 + 任务页删除 / 越页拒绝两项）。
- `local-run/driver/Discard50Check-FG395.cs` **19/19 PASS**（契约已按 FG400 更新：确认位 0/1 都过，
  保留位照样拦，kind 5 进白名单且被页约束，销毁路径仍不存在）。
- `python scripts/build.py`：201 源通过，无 error；新 exe 内含 `Drop50 confirm flag` / `Quest page drop
  position` / `Delete quest page position` 三个字符串。

## 5. 玩家验收

1. 重登后打开背包"任务"页，把不想要的东西拖出去：城镇里应直接消失（`TOWN_DELETE_COMMITTED`），
   副本里应落在脚下、可以 X 键捡回来（`DROP_COMMITTED` + op40）。
2. 副本里**只丢一叠里的一部分**（会弹确认框）现在也会成功——这是同一条修复，日志里不再出现
   `Drop50 reserved or padding`。
3. 服务端日志预期：城镇 `TOWN_DELETE_COMMITTED ... rows=1`；副本 `DROP_COMMITTED slot=153 ...`。

## 6. 边界与风险

- 任务物品删掉是**永久**的（城镇删除没有回收站），如果它正被某个进行中的任务计数，那个任务会缺材料，
  只能重新去打/重新接。这是玩家自己要的语义，和原版"任务页可删"一致。
- 任务页以外的 Kind 5（比如把 kind 5 塞进材质槽的旧档）仍然拒删；`NormalizeEmblemExpertSlots` 目前
  只归整 Kind 9/10，没有动 Kind 5。
- 副本内丢弃 Kind 5 走的是地面行，不是销毁：没捡回来就离开房间，物品就没了（和普通材料一致）。
  **FG401 更正**：落地还是消失由 `[attach type]` 决定，4183/3209 都是 `[trade]`，所以它们现在是
  "丢出去直接消失"，见 `FIX-2026-10-30-equipment-drop-attach-FG401.md`。
- 装备/时装在副本内仍然丢不了（回包 `discard-failed`），这是 FG395 回滚后保留的行为，本次没有改。
  **FG401 更正**：装备已可丢（Kind 4，按 `[attach type]` 落地或消失），时装仍未开放。
