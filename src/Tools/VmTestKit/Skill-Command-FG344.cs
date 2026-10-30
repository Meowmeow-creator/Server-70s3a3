using System;using System.IO;using System.Collections.Generic;
// FG344: per-role skill command styles — custom manual-input commands plus the skill-lock marker.
// Reference chain: CMD 344 SKILL_COMMAND_CUSTOMIZING (Dispatcher_Skill_Command_Customizing read
// 0x081d29ac / process 0x081d2b42) is {u8 commandCount; N×(u16 command, u8 keyCount, keyCount×u8
// key)} and is acknowledged by type1/op332 {01} or {00, err u16} (send 0x081d2da4). CMD 345
// SKILL_COMMAND_ALL_DEFAULT is empty and is acknowledged by type1/op333 the same way. The replay
// rides opcode 19's per-skill tail: send_skill_info 0x0866c46a emits {slot,id,level,cmdCount,
// keys...} per mastered skill, count 0 meaning "no stored command". The lock marker rides the
// same tail: key value 8 is the only wire-legal key that maps to no real input (the reference
// server rewrites 8→7 only because its 3-bit packed store cannot hold it, and its last-key rule
// rejects {0,1,2,3,7} but accepts 8), so {1,[8]} passes every reference validation while telling
// the client the skill has no manual command — the lock encoding this fix replays.
class Offline70SkillCommandFG344 {
 internal struct Entry { public ushort Id; public byte Count; public byte[] Keys; }
 internal const byte LockKeyFG344=8;
 internal const int MaxCommands=49;
 const string Magic="S70CMD34";
 internal static string PathFor(string folder,int roleId){return Path.Combine(folder,"skill-command-role-"+roleId+".bin");}
 static void Validate(List<Entry> list){
  if(list==null||list.Count>MaxCommands)throw new InvalidDataException("Skill command count");
  for(int i=0;i<list.Count;i++){Entry e=list[i];if(e.Id==0||e.Count<1||e.Count>8||e.Keys==null||e.Keys.Length!=8)throw new InvalidDataException("Skill command entry");
   for(int k=0;k<8;k++)if(e.Keys[k]>8)throw new InvalidDataException("Skill command key");
   for(int j=i+1;j<list.Count;j++)if(list[j].Id==e.Id)throw new InvalidDataException("Skill command duplicate id");}
 }
 internal static List<Entry> Load(string path){
  if(path==null||!File.Exists(path))return null;
  byte[] data=File.ReadAllBytes(path);
  if(data.Length<12||data.Length!=12+11*BitConverter.ToUInt32(data,8)||System.Text.Encoding.ASCII.GetString(data,0,8)!=Magic)throw new InvalidDataException("Skill command store format");
  uint n=BitConverter.ToUInt32(data,8);List<Entry> list=new List<Entry>();
  for(int i=0;i<n;i++){Entry e;e.Id=BitConverter.ToUInt16(data,12+11*i);e.Count=data[12+11*i+2];e.Keys=new byte[8];Buffer.BlockCopy(data,12+11*i+3,e.Keys,0,8);list.Add(e);}
  Validate(list);return list;
 }
 internal static void Save(string path,List<Entry> list){
  Validate(list);string temp=path+".pending-"+Guid.NewGuid().ToString("N"),backup=path+Offline70StoreBackupFG354.Suffix;
  try{
   using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
    w.Write(System.Text.Encoding.ASCII.GetBytes(Magic));w.Write((uint)list.Count);
    foreach(Entry e in list){w.Write(e.Id);w.Write(e.Count);w.Write(e.Keys,0,8);}
    byte[] body=m.ToArray();
    using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(body,0,body.Length);f.Flush();}
    List<Entry> check=Load(temp);if(check.Count!=list.Count)throw new InvalidDataException("Skill command readback");
    if(File.Exists(path))File.Replace(temp,path,backup);else File.Move(temp,path);
   }
   List<Entry> committed=Load(path);if(committed.Count!=list.Count)throw new InvalidDataException("Skill command committed readback");
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 internal static Entry? Find(List<Entry> list,ushort id){
  if(list==null)return null;
  for(int i=0;i<list.Count;i++)if(list[i].Id==id)return list[i];
  return null;
 }
 internal static void Merge(List<Entry> list,Entry e){for(int i=0;i<list.Count;i++)if(list[i].Id==e.Id){list[i]=e;return;}list.Add(e);}
 internal static bool Remove(List<Entry> list,ushort id){for(int i=0;i<list.Count;i++)if(list[i].Id==id){list.RemoveAt(i);return true;}return false;}
 internal static Entry Lock(ushort id){return new Entry{Id=id,Count=1,Keys=new byte[8]{LockKeyFG344,0,0,0,0,0,0,0}};}
 // Skill-lock restore rides S2C opcode 369: the client's receive dispatch table ends at op368
 // (dispatcher bound cmp eax,0x170), so an unpatched client drops the packet silently. The FG344
 // client patch adds a handler that writes {u32 tab, 64 u16 page-0 ids, 64 u16 page-1 ids} to the
 // lock table at 0x1840268/0x1840368/0x18403E8. It never touches 0x184026c..0x1840367, so the
 // per-slot command bytes that op19 populates survive regardless of packet order.
 internal const uint LockPacketMagicFG344=0x4C303753u;
 internal const int LockPacketPageSize=64;
 internal static byte[] LockPacketBodyFG344(List<Entry> all,out int count){
  List<ushort> ids=new List<ushort>();
  if(all!=null)foreach(Entry e in all)if(e.Count==1&&e.Keys[0]==LockKeyFG344&&ids.Count<LockPacketPageSize)ids.Add(e.Id);
  ids.Sort();count=ids.Count;
  byte[] body=new byte[8+4*LockPacketPageSize];
  BitConverter.GetBytes(LockPacketMagicFG344).CopyTo(body,0);
  for(int i=0;i<LockPacketPageSize;i++){
   BitConverter.GetBytes(i<ids.Count?ids[i]:(ushort)0xFFFF).CopyTo(body,8+2*i);
   BitConverter.GetBytes((ushort)0xFFFF).CopyTo(body,8+2*LockPacketPageSize+2*i);
  }
  return body;
 }
 // v5 carrier: the v4 op369 handler never proved itself on a live client, so the per-role table is
 // also published as the 512-byte skilllock.dat that the FG344 client patch reads next to its exe.
 // Layout == the client's table buffer: u32 header (0 = initialized), 252B command area (untouched
 // by the loader), 64 u16 page-0 ids, 64 u16 page-1 ids.
 internal static string ResolveClientLockFileFG344(string storeFolder){
  try{
   string cfg=Path.Combine(Path.Combine(storeFolder,"TestTools"),"client-lock-path.txt");
   if(File.Exists(cfg)){string dir=File.ReadAllText(cfg).Trim();if(dir.Length>0&&Directory.Exists(dir))return Path.Combine(dir,"skilllock.dat");}
  }catch(IOException){}
  string store=Path.GetFullPath(storeFolder),local=Path.GetDirectoryName(store),root=local==null?null:Path.GetDirectoryName(local);
  string guess=root==null?null:Path.Combine(root,"\u5730\u4E0B\u57CE\u4E0E\u52C7\u58EB");
  return guess!=null&&Directory.Exists(guess)?Path.Combine(guess,"skilllock.dat"):null;
 }
 internal static bool PublishClientLockFileFG344(string path,List<Entry> all){
  List<ushort> ids=new List<ushort>();
  if(all!=null)foreach(Entry e in all)if(e.Count==1&&e.Keys[0]==LockKeyFG344&&ids.Count<LockPacketPageSize)ids.Add(e.Id);
  ids.Sort();
  byte[] file=new byte[0x200];
  for(int i=4;i<0x100;i++)file[i]=0xFF;
  for(int i=0;i<LockPacketPageSize;i++){
   BitConverter.GetBytes(i<ids.Count?ids[i]:(ushort)0xFFFF).CopyTo(file,0x100+2*i);
   BitConverter.GetBytes((ushort)0xFFFF).CopyTo(file,0x100+2*LockPacketPageSize+2*i);
  }
  string temp=path+".pending-"+Guid.NewGuid().ToString("N");
  try{
   using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(file,0,file.Length);f.Flush();}
   if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   return true;
  }catch(IOException){
   try{File.Copy(temp,path,true);return true;}catch(IOException){return false;}
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 // Reference process rules: commandCount>49 → 0xfc; unknown skill → 0xfc; keyCount outside 1..5
 // → 0xfb; key outside 0..8 → 0xfa; last key in {0,1,2,3,7} → 0xfa. The error code prefixes the
 // message so the caller can echo it verbatim into the type1/332 failure body.
 internal static List<Entry> Parse344(byte[] cipher,byte[] keys,ICollection<byte> learned){
  if(cipher==null||cipher.Length==0||cipher.Length%16!=0||cipher.Length>16*26)throw new InvalidDataException("252:Skill command request length");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(cipher,keys,8,false);
  int commandCount=plain[0];if(commandCount>MaxCommands)throw new InvalidDataException("252:Skill command count");
  int at=1;List<Entry> list=new List<Entry>();
  for(int i=0;i<commandCount;i++){
   if(at+3>plain.Length)throw new InvalidDataException("252:Skill command entry truncated");
   ushort id=BitConverter.ToUInt16(plain,at);byte count=plain[at+2];at+=3;
   if(count<1||count>5)throw new InvalidDataException("251:Skill command key count");
   if(at+count>plain.Length)throw new InvalidDataException("252:Skill command keys truncated");
   Entry e;e.Id=id;e.Count=count;e.Keys=new byte[8];
   for(int k=0;k<count;k++){byte key=plain[at+k];if(key>8)throw new InvalidDataException("250:Skill command key value");e.Keys[k]=key;}
   byte last=e.Keys[count-1];
   if(last==0||last==1||last==2||last==3||last==7)throw new InvalidDataException("250:Skill command last key");
   at+=count;
   if(id==0||id>255||learned==null||!learned.Contains((byte)id))throw new InvalidDataException("252:Skill command unknown skill "+id);
   list.Add(e);
  }
  for(int i=at;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("252:Skill command padding");
  return list;
 }
 // The reference failure path also wipes every stored command (defaultSkillCommand); a single
 // player's one bad entry must not burn the whole bar, so this port keeps the store and only
 // rejects the request.
 internal static ushort ErrorCode(string message){int colon=message==null?-1:message.IndexOf(':');ushort code;return colon>0&&UInt16.TryParse(message.Substring(0,colon),out code)?code:(ushort)252;}
}
