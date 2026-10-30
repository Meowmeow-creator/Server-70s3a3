using System;using System.IO;using System.Text;using System.Collections.Generic;using System.Xml;using System.Xml.Serialization;
// 点券在 CN70 是账号钱包，不是角色钱包：参考服务端按登录号结算余额
// （reference/rednf .../NXCash/getBalance.inc 的入参是 userId，不是角色 id），客户端 op53 的余额面板
// 也从来只有一个数。原来的实现把 OfflineCash 存在每个角色自己的 economy 档里，于是每个角色各有一份点券。
// 这里照 FG158 的 premium-account1-FG158.xml 加一份账号侧车档 cash-account1-FG271.xml：它一存在，
// Offline70Economy.Load 就用它覆盖每个角色档里的两个钱包字段，角色档里的那份退化成缓存；
// 每次 Apply 先落这里、再提交角色档，所以中途出事只会让付掉的钱没到货，不会凭空多出一份货。
public class Offline70AccountCashStateFG271 {public int Format=1;public int AccountId=1;public uint Revision;public uint OfflineCash;public uint BoundCash;}
class Offline70AccountCashFG271 {
 internal const string FileName="cash-account1-FG271.xml";
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70AccountCashStateFG271));
 internal static string Wallet(string economy){return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(economy)),FileName);}
 internal static bool Exists(string economy){return File.Exists(Wallet(economy));}
 static void Validate(Offline70AccountCashStateFG271 s){if(s==null||s.Format!=1||s.AccountId!=1||s.OfflineCash>Offline70Economy.WalletLimit||s.BoundCash>Offline70Economy.WalletLimit)throw new InvalidDataException("Account cash profile");}
 // 没有侧车档就返回 null，让调用方保持逐角色钱包：老的独立存档与全部既有夹具目录都走这条分支。
 internal static Offline70AccountCashStateFG271 Load(string economy){string file=Wallet(economy);if(!File.Exists(file))return null;if(new FileInfo(file).Length>2000000)throw new InvalidDataException("Account cash size");using(FileStream f=File.OpenRead(file))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;Offline70AccountCashStateFG271 s=(Offline70AccountCashStateFG271)Serializer.Deserialize(r);Validate(s);return s;}}
 internal static void Inject(string economy,Offline70EconomyState s){if(s==null)return;Offline70AccountCashStateFG271 w=Load(economy);if(w==null)return;s.OfflineCash=w.OfflineCash;s.BoundCash=w.BoundCash;}
 // 只在侧车档已经存在、且数额真的变了时才写，所以纯背包/任务事务不会碰账号钱包。
 internal static bool Adopt(string economy,uint cash,uint bound){string file=Wallet(economy);if(!File.Exists(file))return false;using(FileStream held=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70AccountCashStateFG271 w=Load(economy);if(w==null||w.OfflineCash==cash&&w.BoundCash==bound)return false;if(w.Revision==UInt32.MaxValue)throw new InvalidDataException("Account cash revision capacity");Offline70AccountCashStateFG271 after=new Offline70AccountCashStateFG271{Format=1,AccountId=w.AccountId,Revision=checked(w.Revision+1),OfflineCash=cash,BoundCash=bound};Validate(after);string pending=file+".pending-"+Guid.NewGuid().ToString("N"),backup=file+Offline70StoreBackupFG354.Suffix;try{using(FileStream f=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,after);f.Flush();}if(File.Exists(file))File.Replace(pending,file,backup);else File.Move(pending,file);Offline70AccountCashStateFG271 verify=Load(economy);if(verify==null||verify.OfflineCash!=cash||verify.BoundCash!=bound||verify.Revision!=after.Revision)throw new InvalidDataException("Account cash readback");}finally{if(File.Exists(pending))File.Delete(pending);}return true;}}
 // 一次性并档：侧车档出现之前，点券一直躺在各自的角色档里，所以账号从名册全员的角色档求和起步。
 // 逐档读原始值（不注入，避免自问自答），读不动的角色记进回执而不是让登录失败——它的背包本来也已经降级了。
 internal static string Merge(string folder,List<Offline70Role> roles){
  string file=Path.Combine(Path.GetFullPath(folder),FileName);if(File.Exists(file))return null;
  uint cash=0,bound=0;int merged=0,missing=0;bool capped=false;StringBuilder skipped=new StringBuilder();
  foreach(Offline70Role stored in roles){
   if(stored==null||stored.Id==0)continue;
   string archive=Path.Combine(folder,Offline70RoleScopeFG202.EconomyFile(stored.Id));
   if(!File.Exists(archive)){missing++;continue;}
   try{
    Offline70EconomyState s=Offline70Economy.LoadRaw(archive);
    if(s.OfflineCash>Offline70Economy.WalletLimit-cash){capped=true;cash=Offline70Economy.WalletLimit;}else cash+=s.OfflineCash;
    if(s.BoundCash>Offline70Economy.WalletLimit-bound){capped=true;bound=Offline70Economy.WalletLimit;}else bound+=s.BoundCash;
    merged++;
   }
   catch(Exception e){if(skipped.Length!=0)skipped.Append(',');skipped.Append(stored.Id+"="+e.GetType().Name);}
  }
  using(FileStream held=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   if(File.Exists(file))return null;
   Offline70AccountCashStateFG271 after=new Offline70AccountCashStateFG271{Revision=1,OfflineCash=cash,BoundCash=bound};Validate(after);
   string pending=file+".pending-"+Guid.NewGuid().ToString("N");
   try{using(FileStream f=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,after);f.Flush();}File.Move(pending,file);}finally{if(File.Exists(pending))File.Delete(pending);}
   Offline70AccountCashStateFG271 verify=Load(file);
   if(verify==null||verify.OfflineCash!=cash||verify.BoundCash!=bound)throw new InvalidDataException("Account cash merge readback");
   return "CASH_ACCOUNT_MERGED roles="+merged+" absent="+missing+" cash="+cash+" bound="+bound+(capped?" capped=True":"")+(skipped.Length==0?"":" unreadable="+skipped);
  }
 }
}
