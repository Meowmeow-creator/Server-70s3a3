# 验收驱动（drivers）

本目录包含用于隔离服务验收的只读驱动。它们不参与服务端编译，仅用于验证。

## 文件

| 文件 | 用途 |
|---|---|
| `TownDoorsCheck.cs` | 静态门表验收：从 PVF 导出的 `town/*.twn` + `map/*.map` 读取门行，逐一验证服务端 op38 校验链（Exists + Linked + town1 白名单 + 边界）。同时检查 NPC 覆盖与副本入口注册数。 |
| `GateLiveCheck2.cs` | 线上验收：登录后走真实 op38 门（阿法利亚营地 + 营地链路）、发 op35 对话题任务（赛丽亚/杜罗西）、开关副本选择 UI（op15/op146）并验证落点。 |

## 编译

用 C# 编译器（`csc`）编译，需要把 145 个稳定源码 + 驱动文件一起传入。
在 Git Bash 下必须用 `-nologo` 这种短横线选项，斜杠会被当成路径。

### 步骤

1. 在仓库根目录生成一个 `.rsp` 响应文件，列出所有源文件：

```bash
# 在仓库根目录执行
ls src/Tools/VmTestKit/*.cs > /tmp/sources.rsp
echo "tools/drivers/TownDoorsCheck.cs" >> /tmp/sources.rsp
```

2. 编译（以 TownDoorsCheck 为例）：

```bash
csc -nologo -target:exe -main:TownDoorsCheck -out:TownDoorsCheck.exe @/tmp/sources.rsp
```

3. 编译 GateLiveCheck2：

```bash
csc -nologo -target:exe -main:GateLiveCheck -out:GateLiveSuite.exe @/tmp/sources.rsp
```

注意：`GateLiveCheck2.cs` 的 Main 类名是 `GateLiveCheck`（不是文件名）。

### 运行

```bash
# 静态门表验收（需要 PVF 导出目录）
TownDoorsCheck.exe "D:\70S3A3\local-run\pvf\export"
# 期望：accepted=58  unexpected rejects=0  PASS=True

# 线上验收（需要隔离服务已启动）
GateLiveSuite.exe 127.0.0.1 17003
# 期望：12/12 PASS
```
