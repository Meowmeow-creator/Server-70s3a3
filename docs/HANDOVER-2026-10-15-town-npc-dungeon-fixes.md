# 交接文档：城镇门表/NPC 表 PVF 生成 + 副本门落点修复

日期：2026-10-15。分支：`main`。

## 1. 本轮改动清单

### 1.1 城镇门表改为从客户端 PVF 生成

**源文件**：`src/Tools/VmTestKit/Town-Content-FG75.cs`

`Town-Content-FG75.cs` 现在由 `tools/pvf/gen_town_content.py` 从导出的 `town/*.twn` + 区域 `.map` 的 `[town movable area]` 段生成。四个支持城镇（1 艾尔文防线 / 2 赫顿玛尔 / 3 西海岸 / 4 阿法利亚）共 58 条门行全部放行，旧表 36 条链路 0 丢失；矩形是客户端原始矩形（修正了 `OffsetReturn` 落点）。

生成器同时产出：
- `Portals()` — 门矩形表（客户端原始矩形）
- `Npc()` — NPC 白名单（来自各区域 map 的 `[NPC]` 段）
- `DungeonPortal()` — 副本入口矩形（来自 `[town movable area]` 里目标为 `-1/-1` 的行）

### 1.2 NPC 白名单改为从 PVF 的 `[NPC]` 段生成

**源文件**：`src/Tools/VmTestKit/Town-Content-FG75.cs`（同上）

NPC 白名单从 PVF 的 `[NPC]` 段生成，从 46 条增加到 95 条，0 丢失。修掉"对话题任务完不成"——阿法利亚的赛丽亚（town4/area1）与杜罗西（town4/area4）原先不在白名单里导致 op35 被拒。

### 1.3 退出副本选择 UI / 副本回城的落点

**源文件**：
- `src/Tools/VmTestKit/Batch-Service-FG.cs`
- `src/Tools/VmTestKit/Batch-Player-Protocol-FG.cs`

`Batch-Service-FG.cs` 的 `Town()` 不再写死 (857,215)/(8,247)，改为用新生成的 `Offline70TownContentFG75.DungeonPortal(town,area)`（客户端副本入口矩形，来自目标为 `-1/-1` 的行）算"门口外 16 像素"。`Batch-Player-Protocol-FG.cs` 的 `BatchTownReplyFG` 增加 `(x,y)` 参数。

### 1.4 经济状态城镇范围扩展

**源文件**：`src/Tools/VmTestKit/Economy-State-FG.cs`

`Validate()` 和 `SaveTown()` 的城镇范围从 1..3 扩展到 1..4，支持阿法利亚（town4）作为存档城镇。

### 1.5 副本门协议增强

**源文件**：`src/Tools/VmTestKit/Dungeon-Gate-Protocol.cs`

`OffsetReturn` 逻辑增强：新城镇链路可能直接出生在反向门矩形内，`OffsetReturn` 会把角色挪到目的地内部避免立即反弹。

### 1.6 测试辅助：一击必杀

**源文件**：`src/Tools/VmTestKit/Batch-Service-FG.cs`

新增 `DNF70_TEST_ONE_HIT_KILL` 环境变量（默认关）。开启后，房间加载后自动击杀所有怪物，用于 bug 狩猎。

## 2. 关键事实与坑

### 2.1 暗黑城无法进入 = 内容缺口（不是配置错误）

暗黑城 = Act4，客户端副本 id 31..37（`dungeon/dungeon.lst`），服务端副本内容表 `Offline70ForestFG75.Supported` 只覆盖 1..9/11..15/17/1000/21..27，且 `Offline70DungeonSelection.SupportedInArea` 里 town4 没有任何条目。需要按之前 7 个副本的做法把 Act4 内容（房间地图/怪物/清图条件）从 PVF 移植进来。

### 2.2 动态 NPC 任务覆盖

483 个 `[meet npc]` 任务里 339 个可达，144 个的 NPC 属于剧情动态出现（不在城镇静态 `[NPC]` 数据里），仍会被拒；需要动态规则才能覆盖。

### 2.3 服务端证据日志 CreateNew 坑

服务端证据日志和 `*.log.*.bin` 分片都用 `CreateNew` 打开，重启前必须删干净，否则服务启动即闪崩。这是之前"服务端没启动起来"的真正原因。`run-services.bat` / `start-channel.bat` / `start-entrance.bat` 已内置清理逻辑。

### 2.4 PVF 读取路线

客户端 `Script.pvf` 是 2013 国服老 GUID 头格式，serveRust/90CN 的读取器都读不了。可用本机 `D:\PVF\pvfUtility.exe`（.NET 6 单文件包，解包到 `D:\70S3A3\local-run\tools\pvfUtility-bundle` 后用 `dotnet pvfUtility.dll "<Script.pvf>"` 启动），它开 `http://127.0.0.1:27000` 的 HTTP API。

取文件：`POST /Api/PvfUtiltiy/GetFileContents`，体 `{"FileList":["town/town.lst"]}`（路径必须小写）。

详见 `tools/pvf/README.md`。

### 2.5 隔离验收

复制存档到 `<任意路径>\DNF70-batch-test-FG` + `--test-port 17xxx`，目录名必须是 `DNF70-batch-test-FG`。

## 3. 验收命令与期望结果

### 3.1 静态门表验收

```bash
cd D:\70S3A3\local-run\driver
csc -nologo -target:exe -main:TownDoorsCheck -out:TownDoorsCheck.exe @tdc.rsp
TownDoorsCheck.exe "D:\70S3A3\local-run\pvf\export"
```

期望：`accepted=58  unexpected rejects=0  PASS=True`

### 3.2 线上验收

```bash
# 先启动隔离服务
cd D:\70S3A3\local-run
run-services.bat

# 再跑线上验收
cd D:\70S3A3\local-run\driver
GateLiveSuite.exe 127.0.0.1 17003
```

期望：12/12 PASS（门 + op35 任务 + op15/op146 落点）

### 3.3 登录链路验收

```bash
cd D:\70S3A3\local-run\driver
LoginDriver.exe 127.0.0.1 7002
```

期望：登录 -> 角色 -> 选角进世界 PASS

### 3.4 阿法利亚验收

```bash
cd D:\70S3A3\local-run\driver
Town4Check.exe
```

期望：一次性存档副本上 SaveTown(4) 往返 + `SeriaArea(4)==0` PASS

### 3.5 仓库检查与构建

```bash
cd D:\70S3A3\Server-70s3a3
python scripts/check_repo.py
python scripts/build.py --smoke
```

期望：`repositoryChecksPassed: true` + 构建通过

## 4. 下一任 Agent 建议任务（按优先级）

1. **暗黑城 Act4 内容移植**（最高优先级）：按之前 7 个副本的做法，把 Act4 内容（房间地图/怪物/清图条件）从 PVF 移植进来。需要：
   - 在 `Offline70ForestFG75.Supported` 里添加 31..37
   - 在 `Offline70DungeonSelection.SupportedInArea` 里为 town4 添加条目
   - 从 PVF 导出 Act4 地图数据并生成房间/怪物/清图条件

2. **动态 NPC 任务覆盖**：483 个 `[meet npc]` 任务里 144 个的 NPC 属于剧情动态出现（不在城镇静态 `[NPC]` 数据里），需要动态规则才能覆盖。

3. **其余未完成项**：可从 `docs/PROGRESS_HANDOVER.md` 取。

## 5. 硬性约束

- 只写 `D:\70S3A3`，不写其他路径
- 测试只用一次性存档且端口用 17000..17999
- 提交前跑 `python scripts/check_repo.py` 与 `python scripts/build.py --smoke`
- 真实存档不动
- 不要修改 `src/Tools/VmTestKit/` 下已有文件的代码内容（除非是明确的 bug 修复）
