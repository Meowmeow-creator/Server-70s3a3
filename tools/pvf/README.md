# PVF 城镇/门表导出与再生成（本地）

> 本轮（10-15）补充：生成器现在同时产出 **Npc()**（来自各区域 map 的 `[NPC]` 段）
> 与 **DungeonPortal()**（来自 `[town movable area]` 里目标为 `-1/-1` 的行，即副本入口矩形）。
> 服务端 `Town()`（op146 退出副本选择 UI / 副本回城）用它把角色落到副本入口门口附近，
> 取代原先写死的 (857,215)/(8,247)。
>
> 隔离验收（不动线上 7002，遵守“一次性存档 + 17000-17999”）：
> ```bash
> copy /y 的副本 -> local-run\isolated-test\DNF70-batch-test-FG   # 目录名必须是 DNF70-batch-test-FG
> DNF70-Channel-FG.exe ...\channel-isolated.log --batch-store ...\isolated-test\DNF70-batch-test-FG --test-port 17003 --interactive
> driver\GateLiveSuite.exe 127.0.0.1 17003
> ```
> 注意：服务端的证据日志与其 `*.log.*.bin` 分片都是用 CreateNew 打开的，重启前必须删干净，
> 否则服务会“没启动起来”（闪崩）。已提供 `run-services.bat`/`start-channel.bat`/`start-entrance.bat`
> 与 `watch-services.ps1`（守卫进程，缺谁补谁）。

本目录是一次性工具目录：把客户端 `D:\70S3A3\地下城与勇士\Script.pvf` 里的城镇脚本导出为
本地文本，再据此**重新生成**服务端城镇表 `..\..\Server-70s3a3\src\Tools\VmTestKit\Town-Content-FG75.cs`。

## 1. 为什么用 pvfUtility（而不是 serveRust 的读取器）

`D:\serveRust` 里确实有 PVF 读取代码，但都只支持新版 `NKPI` 容器：

- `tool/pvf-lib/`（Rust 只读解析库，`pvf_data/mod.rs`，profile: `utility-guarded` / `us-zimo`）
- `analysis_output/re_client/pvf_patch/pvf_patch.py`（Python，NKPI 写补丁）
- `reference/ServerS4A21/Tool/PvfLib/*.cs`（C# 完整读写：`PvfArchive`/`PvfDecryptor`/`PvfUnpacker`）
- `data/Script.pvf`（美服客户端的 PVF，同样是新版格式）

本机 2013 国服客户端的 `Script.pvf` 是**老 GUID 头格式**：`u32 0x24` + 36 字节 GUID + 文件数等，
索引区被 4 字节周期异或覆盖（实测相位1↔2 差 0x10、1↔3 差 0x66，但无法从 6 组常见密钥推回明文；
`D:\90轮回\90CN轮回源码\登录器\Pvf\` 里的 C# 读取器也只支持“旧版 nkpi / protected_nkpi”，读不了这一种）。

可用的读取器是本机的 **pvfUtility**（`D:\PVF\pvfUtility.exe`，.NET 6 单文件包）。做法：
把它的单文件包解出到 `local-run\tools\pvfUtility-bundle\`（`probe20_toc.py`），再用 .NET 6
桌面运行时在本目录启动：

```
"C:\Program Files\dotnet\dotnet.exe" pvfUtility.dll "D:\70S3A3\地下城与勇士\Script.pvf"
```

程序启动后会开一个本地 HTTP 服务（日志：`HTTP服务: 服务已启动在 27000 端口,支持第三方访问`），
本目录的脚本就是用这个接口取文件正文的。

## 2. HTTP API（实测有效）

- 基址：`http://127.0.0.1:27000`
- 版本：`GET /Api/PvfUtiltiy/getVersion` → `{"Data":"2025.9.28.1",...}`
- **批量读文件**：`POST /Api/PvfUtiltiy/GetFileContents`，体 `{"FileList":["town/town.lst", ...]}`
  → `{"Data":{"FileContentData":{"town/town.lst":"#PVF_File\r\n..."}}}`
  - 路径必须**小写**且大小写敏感（`town/Alfhlyra.twn` 读不到，`town/alfhlyra.twn` 才行）
  - 返回的是反编译后的脚本文本；中文字符串按 TW 编码显示为乱码，但数字、路径、标签都正确
- 其它端点（`GetFileList`/`GetItemInfos`/`SearchPvf`/`ImportFile`/`SaveAsPvfFile` 等）见内存字符串表；
  参数格式未逐个验证，读文件用上面的批量接口即可。

## 3. 门数据的语义

城镇脚本 `town/<name>.twn` 定义区域：

```
[area]
    0
    `Alfhlyra/Alfhlyra.map`
    `[normal]`            ← 或 `[gate]` / `[dungeon gate] N`
[/area]
```

每个区域的 `map/<路径>.map` 里，**门表**是：

```
[town movable area]
    9    183    22    258    1    3
    x    y      w     h     目标城镇  目标区域
[/town movable area]
```

- 行 = 客户端自己的触发矩形 + 目标 (town, area)；`-1/-2` 目标是特殊标记行，已跳过。
- 客户端发门请求（op38）时携带的 x/y 是**目标区域里反向门矩形的中心**（例：`town1/area0 →
  town1/area2` 的请求位置是 area2 反向门 `-2,176,21,143` 的中心 (8,247)），这也是服务端
  `OffsetReturn` 用矩形把角色挪出门外、避免反弹的依据。因此表里的矩形必须是客户端原始矩形。

## 4. 复现步骤

```bash
cd D:\70S3A3\local-run\pvf
python fetch_towns.py                       # 导出 12 个 town/*.twn -> export/town/*.twn.txt
python fetch_maps.py                        # 导出 64 个区域 map    -> export/map/map_*.map.txt
python gen_town_content.py                  # 重新生成 Town-Content-FG75.cs（含新旧链路对比打印）
```

`gen_town_content.py` 会打印每个城镇/区域的门行，并输出“旧表链路是否全部保留”的对比；
如出现 `pairs present in old but NOT in new`，需要人工确认是否是有意收窄。

验收（协议级、只读）：

```bash
cd D:\70S3A3\local-run\driver
csc -nologo -target:exe -main:TownDoorsCheck -out:TownDoorsCheck.exe @tdc.rsp
TownDoorsCheck.exe "D:\70S3A3\local-run\pvf\export"
# 期望：accepted=58  unexpected rejects=0  PASS=True
```

其它验收：`Town4Check`（一次性存档副本上 SaveTown(4) 往返 + `SeriaArea(4)==1`，即 alfhlyra
`[gate] 474 234` 的 Gate_Alfh 赛丽亚房；2026-10-30 前误钉成 `==0` 营地，见
`docs/FIX-2026-10-30-town4-seria-room-FG399.md`）、
`LoginDriver 127.0.0.1 7002`（登录→角色→选角进世界）。

## 5. 注意事项

- pvfUtility 的启动窗口会请求 WebView2（本机缺 WebView2Loader 加载路径），会弹“起始页加载失败”，
  不影响 PVF 读取与 HTTP 服务；用窗口 `WM_CLOSE` 关掉该提示即可。
- 本目录内所有工具只读客户端的 `Script.pvf`；服务端改动只落在 `Server-70s3a3` 源码与 `build/`。
