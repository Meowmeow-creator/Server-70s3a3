using System;using System.IO;using System.Xml;using System.Text;using System.Collections.Generic;using System.Diagnostics;
// Manual, local test grants run on the same service thread as gameplay.
// The GUI never edits retained economy files; success requires durable ownership.
class Offline70TestItemsFG106 {
 internal static bool Supported(uint id){try{Offline70EconomyItemDefinition d=Offline70Economy.Definition(id);return d.Kind>=1&&d.Kind<=8&&(d.Kind!=6||Offline70PetFG96.IsEgg(id));}catch(InvalidDataException){return false;}}
 internal static ulong Count(Offline70EconomyState s,uint id){ulong n=0;foreach(Offline70EconomyEntry e in s.Inventory)if(e.ItemId==id&&e.List!=3)n+=e.Quantity;return n;}
 internal static Offline70EconomyState Grant(string path,string receipt,uint item,uint count,bool shortcut,out bool duplicate){
  if(!Supported(item))throw new InvalidDataException("Test grant unsupported item");Offline70EconomyItemDefinition d=Offline70Economy.Definition(item);
  if(count==0||count>1000000||(d.Kind==3||d.Kind==4||d.Kind==6||d.Kind==8)&&count>32)throw new InvalidDataException("Test grant count bounds");
  return Offline70Economy.Apply(path,"manual-test-"+receipt,"manual-test-item",item+":"+count+":"+shortcut,delegate(Offline70EconomyState s){
   HashSet<string> old=new HashSet<string>();foreach(Offline70EconomyEntry e in s.Inventory)old.Add(e.List+":"+e.Slot);
   if(shortcut)Offline70Economy.AddPurchasedItem(s,item,count);else Offline70Economy.AddAssetItem(s,item,count);
   foreach(Offline70EconomyEntry e in s.Inventory)if(!old.Contains(e.List+":"+e.Slot)&&(d.Kind==4||d.Kind==8))e.EquipmentSeed=Offline70ItemQualityFG101.Draw();
  },out duplicate);
 }
 internal static byte[] GrantedRows(Offline70EconomyState after,uint item,byte list,byte[] keys){
  List<Offline70EconomyEntry> rows=new List<Offline70EconomyEntry>();foreach(Offline70EconomyEntry e in after.Inventory)if(e.List==list&&e.ItemId==item)rows.Add(e);rows.Sort(delegate(Offline70EconomyEntry a,Offline70EconomyEntry b){return a.Slot.CompareTo(b.Slot);});if(rows.Count==0)return null;
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(list);w.Write((ushort)rows.Count);foreach(Offline70EconomyEntry e in rows)w.Write(Offline70EconomyProtocol.InventoryRow(e));return Offline70EconomyProtocol.UpdatePacket(m.ToArray(),keys);}
 }
 internal static XmlDocument ReadSmall(string path){if(new FileInfo(path).Length>4096)throw new InvalidDataException("Test request size");XmlDocument d=new XmlDocument();d.XmlResolver=null;using(XmlTextReader r=new XmlTextReader(path)){r.ProhibitDtd=true;r.XmlResolver=null;d.Load(r);}return d;}
 internal static void Write(string path,XmlDocument doc){string temp=path+".pending";using(FileStream f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){doc.Save(f);f.Flush();}if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
}
partial class Offline70BatchServiceFG {
 string testItemDirectoryFG106,testItemTokenFG106=Guid.NewGuid().ToString("N"),testItemPublishedFG106;long testItemPollFG106;
 void InitializeTestItemsFG106(){testItemDirectoryFG106=Path.Combine(folder,"TestTools");Directory.CreateDirectory(testItemDirectoryFG106);PublishTestItemsFG106();}
 void PublishTestItemsFG106(){string state=selected+":"+dungeon.Active+":"+town+":"+area+":"+role.Level;if(state==testItemPublishedFG106)return;XmlDocument d=new XmlDocument();XmlElement e=d.CreateElement("TestItemService");d.AppendChild(e);e.SetAttribute("version","FG106");e.SetAttribute("token",testItemTokenFG106);e.SetAttribute("pid",Process.GetCurrentProcess().Id.ToString());e.SetAttribute("service",Process.GetCurrentProcess().MainModule.FileName);e.SetAttribute("store",folder);e.SetAttribute("role",role.Name);e.SetAttribute("ready",selected&&!dungeon.Active?"1":"0");e.SetAttribute("level",role.Level.ToString());e.SetAttribute("town",town.ToString());Offline70TestItemsFG106.Write(Path.Combine(testItemDirectoryFG106,"service.xml"),d);testItemPublishedFG106=state;}
 void TestItemReplyFG106(string receipt,bool ok,string message,uint item,uint count,bool duplicate,ulong before,ulong after){XmlDocument d=new XmlDocument();XmlElement e=d.CreateElement("TestItemReply");d.AppendChild(e);e.SetAttribute("version","FG106");e.SetAttribute("token",testItemTokenFG106);e.SetAttribute("receipt",receipt);e.SetAttribute("success",ok?"1":"0");e.SetAttribute("message",message);e.SetAttribute("item",item.ToString());e.SetAttribute("count",count.ToString());e.SetAttribute("duplicate",duplicate?"1":"0");e.SetAttribute("before",before.ToString());e.SetAttribute("after",after.ToString());e.SetAttribute("revision",assets.Revision.ToString());Offline70TestItemsFG106.Write(Path.Combine(testItemDirectoryFG106,receipt+".reply.xml"),d);}
 void MaintainTestItemsFG106(){
  if(testItemDirectoryFG106==null)return;PublishTestItemsFG106();long now=DateTime.UtcNow.Ticks;if(now-testItemPollFG106<TimeSpan.TicksPerMillisecond*200)return;testItemPollFG106=now;
  string[] requests=Directory.GetFiles(testItemDirectoryFG106,"*.req.xml");Array.Sort(requests,StringComparer.Ordinal);int done=0;
  foreach(string path in requests){if(done++>=16)break;string receipt=Path.GetFileName(path).Replace(".req.xml","");Guid guid;try{guid=new Guid(receipt);}catch(FormatException){continue;}if(guid.ToString("N")!=receipt)continue;
   uint item=0,count=0;ulong before=0,after=0;bool duplicate=false;try{
    XmlElement e=Offline70TestItemsFG106.ReadSmall(path).DocumentElement;if(e==null||e.Name!="TestItemRequest"||e.Attributes.Count!=6||e.GetAttribute("version")!="FG106"||e.GetAttribute("token")!=testItemTokenFG106||e.GetAttribute("receipt")!=receipt||!UInt32.TryParse(e.GetAttribute("item"),out item)||!UInt32.TryParse(e.GetAttribute("count"),out count)||e.GetAttribute("shortcut")!="0"&&e.GetAttribute("shortcut")!="1")throw new InvalidDataException("Test request profile");
    if(!selected||dungeon.Active)throw new InvalidDataException("Test grant requires selected character in town");Sync();before=Offline70TestItemsFG106.Count(assets,item);Dictionary<ushort,uint> collection=CollectionSnapshot();
    assets=Offline70TestItemsFG106.Grant(economyPath,receipt,item,count,e.GetAttribute("shortcut")=="1",out duplicate);Sync();after=Offline70TestItemsFG106.Count(assets,item);
    foreach(byte list in new byte[]{0,1,7})Send("manual-test-item14",Offline70TestItemsFG106.GrantedRows(assets,item,list,keys));CollectionChanged(collection);
    Note("MANUAL_TEST_ITEM_COMMITTED receipt="+receipt+" item="+item+" count="+count+" before="+before+" after="+after+" duplicate="+duplicate);
    TestItemReplyFG106(receipt,true,duplicate?"已核对上次发送结果，本次没有重复发放。":"发送完成，已保存并同步到当前角色。",item,count,duplicate,before,after);
   }catch(InvalidDataException ex){Note("MANUAL_TEST_ITEM_REJECTED "+ex.Message);string message=ex.Message.IndexOf("capacity",StringComparison.OrdinalIgnoreCase)>=0?"发送失败：对应背包已满，整次发送没有增加物品。":ex.Message.IndexOf("town",StringComparison.OrdinalIgnoreCase)>=0?"发送失败：请先选择 yoyo 并回到城镇。":ex.Message.IndexOf("count",StringComparison.OrdinalIgnoreCase)>=0?"发送失败：数量超出范围；装备、装扮或宠物蛋每次最多32件。":"发送失败：物品未接入或请求无效，存档没有增加物品。";TestItemReplyFG106(receipt,false,message,item,count,false,before,after);
   }catch(XmlException ex){Note("MANUAL_TEST_ITEM_REJECTED "+ex.Message);TestItemReplyFG106(receipt,false,"发送失败：请求格式不正确。",item,count,false,before,after);}
   if(Path.GetFullPath(Path.GetDirectoryName(path))==Path.GetFullPath(testItemDirectoryFG106))File.Delete(path);
  }
 }
}
