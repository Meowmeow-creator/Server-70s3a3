using System;using System.IO;
// 每次事务提交都走 File.Replace(temp, live, backup)，而 finally 只删 pending：
// 旧档就被拷成 "<file>.backup-<guid>" 永久留在存档目录里。一次拾取、出售、任务推进或换装
// 都会留一份完整旧档，分享出去的存档实测 319MB 里 315MB 是这类残留（5074 个），真正的存档
// 只有 2MB。改成固定 "<file>.bak"（File.Replace 会原地覆盖它）后，每个档最多留一份上一版，
// 占用上限变成活动数据的两倍；旧版本已经散落的 guid 残留由启动时的 Sweep 回收。
internal static class Offline70StoreBackupFG354 {
 internal const string Suffix=".bak";
 internal const string OrphanMarker=".backup-";
 static readonly TimeSpan StalePending=TimeSpan.FromHours(24);
 // 只认 "<任意名>.backup-" + 32 位十六进制（Guid.ToString("N")），
 // 玩家自己放的 *.bak、*.backup-手工 之类不碰。
 internal static bool OrphanBackup(string name){
  int dot=name.IndexOf(OrphanMarker,StringComparison.Ordinal);if(dot<0)return false;
  if(name.Length!=dot+OrphanMarker.Length+32)return false;
  for(int i=dot+OrphanMarker.Length;i<name.Length;i++){char c=name[i];if(!(c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F'))return false;}
  return true;
 }
 // pending 正常情况下 finally 就删了；只有进程崩溃才会留下，按 24 小时判陈旧，
 // 避开正在提交的另一进程。
 internal static bool StalePendingFile(string name,DateTime utc){
  int dot=name.IndexOf(".pending-",StringComparison.Ordinal);if(dot<0)return false;
  if(name.Length!=dot+9+32)return false;
  for(int i=dot+9;i<name.Length;i++){char c=name[i];if(!(c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F'))return false;}
  return utc<DateTime.UtcNow-StalePending;
 }
 internal static long Sweep(string folder,out int files){
  files=0;long bytes=0;
  if(String.IsNullOrEmpty(folder)||!Directory.Exists(folder))return 0;
  DateTime now=DateTime.UtcNow;
  foreach(string path in Directory.GetFiles(folder)){
   string name=Path.GetFileName(path);bool backup=OrphanBackup(name),pending=false;
   try{pending=!backup&&StalePendingFile(name,File.GetLastWriteTimeUtc(path));}
   catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}
   if(!backup&&!pending)continue;
   try{long length=new FileInfo(path).Length;File.Delete(path);bytes+=length;files++;}
   catch(IOException){}catch(UnauthorizedAccessException){}
  }
  return bytes;
 }
}
