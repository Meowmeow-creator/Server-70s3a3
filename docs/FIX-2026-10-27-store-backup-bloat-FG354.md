# 2026-10-27 存档目录被旧档副本撑到 1GB（FG354）

## 现场

分享出去的单机包有玩家存档目录 1GB 以上。本机 `local-run/DNF70-batch-test-FG` 只读盘点：

| 项目 | 数量 | 占用 |
|---|---|---|
| `.backup-<32位guid>` 残留 | 5074 个 | 315MB |
| 真实存档（economy/progression/skills/quests/roles/cash…） | 74 个 | 2.1MB |
| 目录合计 | | 319MB |

真实存档里最大的是 `economy-account1-r8-FG.xml` 304,525 字节（882 条收据）、
`economy-account1-yoyo-FG.xml` 250,118 字节（1010 条收据）。经济档收据上限 50000 条
（`Economy-State-FG.cs` 的 `Receipts.Count>=50000`），按实测每条约 247 字节，满上限也才约 12MB。
也就是说：**1GB 里没有任何一部分是"存档数据本身"，全是历史版本副本。**

## 根因

所有存档提交都写成同一个形状（`Economy-State-FG.cs:33`、`Progression-FG.cs:37`、
`Quest-Accept-Protocol.cs:20`、`Starting-Skills-Protocol.cs:65/84/129`、
`Account-Cash-FG271.cs:19`、`BlackQualification-FG158.cs:24`、`Gold-Loot-Protocol.cs:16`、
`Item-Loot-Protocol.cs:11`、`Monster-Experience-Protocol.cs:24`、`Skill-Command-FG344.cs:34`、
`Skill-Reset-FG83.cs:15`、`Skill-Placement-FG123.cs:56`、`Dungeon-Flow-FG.cs:280`、
`Key-Settings-Protocol.cs:23`、`Probe-RoleCreateCandidate.cs:21/40`）：

```
temp = live + ".pending-" + Guid
backup = live + ".backup-" + Guid
File.Replace(temp, live, backup)     // 旧档被整份拷成 backup
finally { if (File.Exists(temp)) File.Delete(temp); }   // 只删 temp
```

`backup` 用完没有任何人读、没有任何人删。于是一次拾取、一次出售、一次任务推进、
一次换装，就在存档目录里永久留下一份**完整的上一版存档**。GUID 命名保证它们永远不会被覆盖，
只会越积越多。玩家玩得越久、点击越多，目录越大——这就是 1GB 的来源，与存档格式无关。

## 代码修改

1. 新增 `src/Tools/VmTestKit/Store-Backup-FG354.cs`
   - `Suffix=".bak"`：所有提交点改用固定备份名。`File.Replace` 对已存在的备份文件是原地覆盖
     （本机实测：`a=new / bak=old`），所以每个存档最多留一份上一版，占用上限 = 活动数据的 2 倍。
   - `OrphanBackup(name)`：只认 `<任意名>.backup-` + 32 位十六进制，人类命名的备份
     （例如本机的 `.backup-avengerquest-20261019-060059`）不碰。
   - `StalePendingFile`：`.pending-<32hex>` 超过 24 小时视为崩溃残留一并回收；正常提交在 finally 就删，
     24 小时阈值避开正在提交的另一进程。
   - `Sweep(folder, out files)`：启动时跑一次，返回回收字节数。
2. 18 处 `backup=<live>+".backup-"+Guid.NewGuid().ToString("N")` 统一改成
   `backup=<live>+Offline70StoreBackupFG354.Suffix`（15 个文件）。
3. `Batch-Service-FG.cs`
   - 构造期（校验 `DNF70-batch-test-FG` 目录名之后）执行 `Sweep`，结果先记字段；
     `Run` 的 LISTENING 处落日志 `STORE_SWEEP_FG354 orphanFiles=<n> bytes=<n>`（构造期 `Note` 还没建写入器，
     与 FG271 并档日志同样的处理）。

没有改存档格式、字段、事务顺序或回执语义：只是把"每次备份一个新名字"改成"覆盖同一个名字"，
外加一次只针对明确残留文件的清扫。

## 验证

1. 真机启动（存档副本，`--test-port 17499`，`DNF70-Channel-FG.exe`）：
   `STORE_SWEEP_FG354 orphanFiles=5071 bytes=318832999`，目录 319MB → 4.9MB；
   剩下的 3 个 `.backup-<人工名>` 按设计保留。
2. 离线提交取证（`build/scratch-fg354/Proof-FG354.cs`，走真实
   `Offline70Economy.Initialize/Apply → Commit → File.Replace`）：

| 步骤 | `*.backup-*` | `.bak` | Gold | duplicate |
|---|---|---|---|---|
| init | 0 | 0 | 0 | — |
| 事务 1 | 0 | 1 | 1 | False |
| 事务 2 | 0 | 1 | 3 | False |
| 重放事务 2 收据 | 0 | 1 | 3 | True |

   进程退出码 0：只留一个 `.bak`、无 guid 残留、稳定收据重放仍然幂等（金额不变）。
3. `python scripts/check_repo.py`：428 文件、177 稳定源，通过。
4. `python scripts/build.py --smoke`：177 源编译通过，无参拒绝 smoke 通过。

## 为什么不改成 sqlite / db

- 换格式解决不了这个问题：残留是 `File.Replace` 的备份参数造成的，旧存档里的 315MB 是历史文件，
  不是"格式低效"。改成 sqlite 后如果不改提交写法，旧目录里那 315MB 也还在。
- 真实数据只有 2.1MB / 74 个文件，格式本身没有体积问题。经济档就算收据记满上限也只有约 12MB。
- 改动面很大：8 类存档（economy/progression/skills/quests/experience/gold/items/keys/roles/cash）
  都带"先写 pending → 校验 → `File.Replace` → 回读比对 → 稳定收据防重复"的不变量，
  且各自有旧存档兼容逻辑（`NormalizeLegacyQuestSlots`、`NormalizeAvatarSlotsFG151`、FG123 技能位置迁移等）；
  启动器（`Launcher-70s3a3/src-tauri/src/store.rs`、`xml.rs`）和 GM 控制台都直接解析这些 XML，
  仓库检查脚本（`scripts/check_repo.py`）还把 `.db`/`.sqlite` 列为禁止提交的运行时文件。
  换存储引擎等于把这些不变量、兼容路径和两个解析方全部重写一遍，风险远大于收益。

## 边界与后续

- **未部署**：`dist/` 里的单机包和 zip 没有重新打；需要单独授权后再出新包。
- 老玩家的 1GB 存档不需要手工处理：下次启动频道服时 `Sweep` 自动回收，日志里有 `STORE_SWEEP_FG354` 一行。
- 每个存档多留一份 `.bak`（上一版），是为保留 `File.Replace` 失败时的原档；如后续要再省一半空间，
  可在回读校验成功后删掉 `.bak`，本次没有这么做。
- 另一个独立增长点：启动器每次启动把上次日志归档到 `logs/launcher-archive-*`，没有保留期上限
  （本机开发目录里另有一个 254MB 的 `client-crash-capture.log`，属于开发取证，不在玩家包内）。
- 回滚：把 18 处改回 GUID 命名即可；已生成的 `.bak` 是无害的多余文件，回滚后不会被读取。
