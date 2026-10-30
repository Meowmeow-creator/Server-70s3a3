using System;using System.IO;using System.Text;
// FG401: PVF `[attach type]` catalogue for the dungeon drop path.
//
// Player 2026-10-30: "封装的装备都是可以在图里丢出来给其他人捡的，但是帐绑/无法交易的道具
// 在图里丢出去就会直接消失". The numbers are the original parser's own field (rednf
// STEquipmentScript/ImportEquipmentScript.inc `[attach type]` -> m_field8):
//   [free] 0, [trade] 1, [trade delete] 2, [sealing] 3, [sealing trade] 4, [account] 5
// and they are exactly what WarRoom::HandleItemDrop switches on (0/4 drop, 1/2/5 refuse,
// 3 only while the instance is still sealed).
//
// File layout: magic "FG401AT1" (8) + pvf sha256 (64 ascii) + u32 count + count*{u32 id,u8 attach}.
static class Offline70AttachTypesFG401 {
 internal const string Magic="FG401AT1";internal const int MaximumBytes=1048576,MaximumRows=200000;
 static System.Collections.Generic.Dictionary<uint,byte> Table;
 internal static void Load(string path){
  if(File.Exists(path)==false)return;
  if(new FileInfo(path).Length>MaximumBytes)throw new InvalidDataException("Attach type catalog size");
  byte[] raw=File.ReadAllBytes(path);if(raw.Length<76||Encoding.ASCII.GetString(raw,0,8)!=Magic)throw new InvalidDataException("Attach type catalog magic");
  for(int i=0;i<64;i++)if(raw[8+i]<0x20||raw[8+i]>0x7e)throw new InvalidDataException("Attach type catalog PVF tag");
  if(Encoding.ASCII.GetString(raw,8,64)!=Offline70QuestCatalogFG.CurrentPvf)throw new InvalidDataException("Attach type catalog PVF");
  int count=BitConverter.ToInt32(raw,72);if(count<0||count>MaximumRows||raw.Length!=76+5*count)throw new InvalidDataException("Attach type catalog count");
  System.Collections.Generic.Dictionary<uint,byte> table=new System.Collections.Generic.Dictionary<uint,byte>(count);long previous=-1;
  for(int i=0;i<count;i++){uint item=BitConverter.ToUInt32(raw,76+5*i);byte attach=raw[80+5*i];if(item<=previous||attach>5)throw new InvalidDataException("Attach type catalog row");previous=item;table.Add(item,attach);}
  Table=table;
 }
 internal static int Count{get{return Table==null?0:Table.Count;}}
 // An unknown id stays `[free]`, i.e. the pre-FG401 behaviour, so a catalogue gap can never
 // make the drop path stricter than what the player already had.
 internal static byte Attach(uint itemId){byte a;if(Table!=null&&Table.TryGetValue(itemId,out a))return a;return 0;}
 // [free] / [sealing] / [sealing trade] stay on the floor; [trade] / [trade delete] / [account]
 // disappear the moment they leave the bag.
 internal static bool Grounds(uint itemId){byte a=Attach(itemId);return a==0||a==3||a==4;}
 internal static string Name(byte a){switch(a){case 0:return "[free]";case 1:return "[trade]";case 2:return "[trade delete]";case 3:return "[sealing]";case 4:return "[sealing trade]";case 5:return "[account]";default:return "?attach";}}
}
// Instance-preserving pickup: a dropped item must come back exactly as it left (durability,
// reinforcement seed, upgrade, reseal count, seal state, stack expiry), otherwise dropping a
// +10 weapon and picking it up again would launder it into a fresh one.
partial class Offline70Economy {
 // Kind decides the home list as well: costumes (kind 3) live in the avatar bag (list 1), every
 // other droppable kind lives in the main bag (list 0).
 static void BagPageFG401(Offline70EconomyItemDefinition d,out byte list,out int start,out int end){list=0;switch(d.Kind){case 1:start=57;end=89;break;case 2:start=105;end=137;break;case 4:start=9;end=41;break;case 5:start=153;end=185;break;case 9:start=249;end=281;break;case 10:start=201;end=233;break;case 3:list=1;start=0;end=104;break;default:throw new InvalidDataException("Ground instance has no bag page");}}
 // Only a row the player dropped carries an instance; death-table drops keep the plain grant
 // path (which is also what stamps fresh durability onto equipment).
 internal static bool InstanceFG401(Offline70GroundFG d){
  if(d==null||d.ItemId==0||!d.FromBag)return false;
  byte kind=Definition(d.ItemId).Kind;
  if(kind==4||kind==3)return true;
  return d.Durability!=0||d.EquipmentSeed!=-1||d.Upgrade!=0||d.ResealCount!=0||d.Sealed||d.Ability!=0||d.ExpiresUnix!=0;
 }
 internal static Offline70EconomyState RestoreGroundFG401(string path,string receipt,Offline70GroundFG drop,out bool duplicate){
  if(drop==null||drop.ItemId==0||drop.Quantity==0||drop.Quantity>1000000)throw new InvalidDataException("Ground instance profile");
  Offline70EconomyItemDefinition def=Definition(drop.ItemId);byte home;int start,end;BagPageFG401(def,out home,out start,out end);
  return Apply(path,receipt,"ground-restore",drop.ItemId+":"+drop.Quantity+":"+drop.Durability+":"+drop.EquipmentSeed+":"+drop.Upgrade+":"+drop.ResealCount+":"+(drop.Sealed?1:0)+":"+drop.Ability+":"+drop.ExpiresUnix,delegate(Offline70EconomyState s){
   uint left=drop.Quantity;
   if(Stackable(def))foreach(Offline70EconomyEntry e in s.Inventory)if(left!=0&&e.ItemId==drop.ItemId&&e.List==(def.Kind==7?7:0)&&e.Quantity<def.StackLimit&&e.Ability==drop.Ability&&e.ExpiresUnix==drop.ExpiresUnix){uint take=Math.Min(left,def.StackLimit-e.Quantity);e.Quantity+=take;left-=take;}
   if(left!=0){
    int slot=start;while(slot<end&&Find(s,home,(ushort)slot)!=null)slot++;
    if(slot>=end)throw new InvalidDataException("Ground instance bag capacity");
    s.Inventory.Add(new Offline70EconomyEntry{List=home,Slot=(ushort)slot,ItemId=drop.ItemId,Quantity=left,Durability=drop.Durability,EquipmentSeed=drop.EquipmentSeed,Upgrade=drop.Upgrade,ResealCount=drop.ResealCount,Sealed=drop.Sealed,Ability=drop.Ability,ExpiresUnix=drop.ExpiresUnix});
   }
  },out duplicate);
 }
}
