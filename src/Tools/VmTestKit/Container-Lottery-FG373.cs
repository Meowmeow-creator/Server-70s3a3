using System;using System.IO;using System.Text;using System.Security.Cryptography;using System.Collections.Generic;
// PVF-driven container-open table (FG373). Every [legacy] / [stackable legacy] / [upgradable
// legacy] item the server can hold is in data/container-lottery-FG373.dat, generated from
// local-run/pvf/scan_legacy_containers_FG373.py.
//
// rednf evidence:
//   STStackableScript/ImportStackableScript.inc: "[legacy]"->m_1e0 6, "[stackable legacy]"->7,
//     "[upgradable legacy]"->0xd.
//   CStackableItem/set_item.inc: 6 -> createLotteryInfo, 7 and 0xd -> createStackableLotteryInfo.
//   createLotteryInfo (0x89babc5): data[0]=default item, default count 1, then (item, weight)
//     pairs, every count 1.
//   createStackableLotteryInfo (0x89bacc1): data[0]=default item, data[1]=default count, then
//     (item, weight, count) triples.
//   CStackableItem/get_lotto_item (0x850ebfc): randInt(100000), cumulative weights, miss ->
//     default; item id 0 in a hit (or a default of 0) pays gold in CUser::lottery_item.
//   CUser::lottery_item (0x086733a2): a type 0xd ticket rolls the ServerParameter upgrade bands
//     for an equipment reward (the [upgradable legacy] flag below).
//   Equipment rewards keep their PVF `[attach type] `[sealing]`` state: a trailing id list in the
//     data file marks the sealed ones, the same evidence the card container stores
//     (gen_card_items.py:44). Sealing is cosmetic for tradeability here -- equipping clears it in
//     Offline70Economy.Move, which is what the original client does.
class Offline70ContainerEntryFG373 {internal uint Item,Weight,Count;}
class Offline70ContainerFG373 {
 internal byte Mode,Flags;internal uint DefaultItem,DefaultCount;internal Offline70ContainerEntryFG373[] Entries;
 internal const byte ModePairs=0,ModeTriples=1,FlagUpgradeRoll=1;
 internal bool UpgradeRoll{get{return(Flags&FlagUpgradeRoll)!=0;}}
}
static class Offline70ContainerLotteryFG373 {
 const string Magic="FG373CL2";
 static Dictionary<uint,Offline70ContainerFG373> table=new Dictionary<uint,Offline70ContainerFG373>();
 static HashSet<uint> sealedItems=new HashSet<uint>();
 internal static string Pvf{get;private set;}
 internal static int SealedCount{get{return sealedItems.Count;}}
 internal static bool IsSealed(uint item){return sealedItems.Contains(item);}
 internal static int Count{get{return table.Count;}}
 internal static bool Registered(uint item){return table.ContainsKey(item);}
 internal static Offline70ContainerFG373 Get(uint item){Offline70ContainerFG373 c;return table.TryGetValue(item,out c)?c:null;}
 // The table is trimmed to reward ids the economy can register, so a rolled reward always has a
 // definition; the bounds below are the file's own profile, not a content filter.
 internal static void Load(string path){
  if(!File.Exists(path))throw new InvalidDataException("Container lottery table missing");
  byte[] raw=File.ReadAllBytes(path);
  if(raw.Length<74||raw.Length>8388608)throw new InvalidDataException("Container lottery table bounds");
  if(Encoding.ASCII.GetString(raw,0,8)!=Magic)throw new InvalidDataException("Container lottery table magic");
  string pvf=Encoding.ASCII.GetString(raw,8,64);foreach(char c in pvf)if(!(c>='0'&&c<='9'||c>='A'&&c<='F'))throw new InvalidDataException("Container lottery PVF hash");
  int at=72;int count=BitConverter.ToUInt16(raw,at);at+=2;if(count==0||count>4096)throw new InvalidDataException("Container lottery count");
  Dictionary<uint,Offline70ContainerFG373> loaded=new Dictionary<uint,Offline70ContainerFG373>();
  for(int i=0;i<count;i++){
   if(at+16>raw.Length)throw new InvalidDataException("Container lottery record bounds");
   uint item=BitConverter.ToUInt32(raw,at);at+=4;byte mode=raw[at++],flags=raw[at++];
   uint def=BitConverter.ToUInt32(raw,at);at+=4;uint defCount=BitConverter.ToUInt32(raw,at);at+=4;
   int entries=BitConverter.ToUInt16(raw,at);at+=2;
   if(item==0||loaded.ContainsKey(item)||mode>Offline70ContainerFG373.ModeTriples||(flags&~Offline70ContainerFG373.FlagUpgradeRoll)!=0||defCount==0||defCount>1000000||entries<1||entries>65535)throw new InvalidDataException("Container lottery record profile");
   if(at+entries*12>raw.Length)throw new InvalidDataException("Container lottery entry bounds");
   Offline70ContainerEntryFG373[] rows=new Offline70ContainerEntryFG373[entries];
   for(int n=0;n<entries;n++){
    uint entryItem=BitConverter.ToUInt32(raw,at),weight=BitConverter.ToUInt32(raw,at+4),entryCount=BitConverter.ToUInt32(raw,at+8);at+=12;
    if(weight==0||entryCount==0||entryCount>1000000)throw new InvalidDataException("Container lottery entry profile");
    rows[n]=new Offline70ContainerEntryFG373{Item=entryItem,Weight=weight,Count=entryCount};
   }
   loaded.Add(item,new Offline70ContainerFG373{Mode=mode,Flags=flags,DefaultItem=def,DefaultCount=defCount,Entries=rows});
  }
  if(at+4>raw.Length)throw new InvalidDataException("Container lottery sealed bounds");
  uint sealedCount=BitConverter.ToUInt32(raw,at);at+=4;
  if(sealedCount>8192)throw new InvalidDataException("Container lottery sealed count");
  if(at+(long)sealedCount*4>raw.Length)throw new InvalidDataException("Container lottery sealed bounds");
  HashSet<uint> sealedSet=new HashSet<uint>();
  for(uint n=0;n<sealedCount;n++){uint id=BitConverter.ToUInt32(raw,at);at+=4;if(id==0||!sealedSet.Add(id))throw new InvalidDataException("Container lottery sealed profile");}
  if(at!=raw.Length)throw new InvalidDataException("Container lottery trailing bytes");
  table=loaded;sealedItems=sealedSet;Pvf=pvf;
 }
 // Receipt-derived draws keep the same replay contract as the rest of the economy: a duplicated
 // sequence must reproduce its committed grant byte for byte.
 static uint Digest(string receipt,string salt,int attempt){
  using(SHA256 sha=SHA256.Create()){byte[] h=sha.ComputeHash(Encoding.UTF8.GetBytes(receipt+salt+attempt));return BitConverter.ToUInt32(h,0);}
 }
 static uint Draw(string receipt,string salt,uint bound){
  if(bound==0||bound>1000000)throw new InvalidDataException("Container draw bound");
  for(int attempt=0;;attempt++){uint value=Digest(receipt,salt,attempt);if(value>=4294900000U)continue;return value%bound;}
 }
 // randInt(100000) walk (strictly "sum > rand"), miss -> the default entry. The count travels
 // with the winning entry (createStackableLotteryInfo keeps one count per triple).
 internal static uint Roll(string receipt,uint container,out uint count){
  if(String.IsNullOrEmpty(receipt))throw new InvalidDataException("Container roll receipt");
  Offline70ContainerFG373 c=Get(container);if(c==null)throw new InvalidDataException("Container lottery is not registered");
  uint rand=Draw(receipt,"#container:",100000),sum=0;
  foreach(Offline70ContainerEntryFG373 e in c.Entries){sum+=e.Weight;if(sum>rand){count=e.Count;return e.Item;}}
  count=c.DefaultCount;return c.DefaultItem;
 }
 internal static uint Roll(string receipt,uint container){uint count;return Roll(receipt,container,out count);}
 internal static int UpgradeRoll(string receipt){return(int)Draw(receipt,"#upgrade:",10000);}
 internal static int UpgradeValue(string receipt,int span){return span<=0?0:(int)Draw(receipt,"#upvalue:",(uint)span);}
 internal static int Seed(string receipt){return(int)(Digest(receipt,"#seed:",0)&Int32.MaxValue);}
}
