using System;using System.IO;
class Offline70TeleportRequestFG94 {internal ushort Slot;internal uint ItemId;internal byte List,Town;}
// Original CN command253 writes slot2/item4/space1/town1. TW241 is the
// corresponding dispatcher; its opcode is deliberately not copied to CN.
class Offline70TeleportPotionFG94 {
 internal static Offline70TeleportRequestFG94 Parse(byte[] encrypted,byte[] keys){
  if(encrypted==null||encrypted.Length!=16)throw new InvalidDataException("Teleport253 block size");byte[] b=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,1,false);
  for(int i=8;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Teleport253 padding");
  Offline70TeleportRequestFG94 r=new Offline70TeleportRequestFG94{Slot=BitConverter.ToUInt16(b,0),ItemId=BitConverter.ToUInt32(b,2),List=b[6],Town=b[7]};
  if(r.List!=0||r.ItemId!=2600014||!(r.Slot>=3&&r.Slot<9||r.Slot>=57&&r.Slot<89)||!Offline70TownContentFG75.TownSupported(r.Town))throw new InvalidDataException("Teleport253 item/space/slot/region");return r;
 }
 internal static Offline70EconomyState Use(string path,string receipt,Offline70TeleportRequestFG94 r,long ticks,out bool duplicate){
  if(r==null||r.List!=0||r.ItemId!=2600014||!Offline70TownContentFG75.TownSupported(r.Town)||ticks<0||ticks>DateTime.MaxValue.Ticks)throw new InvalidDataException("Teleport253 transaction profile");
  return Offline70Economy.Apply(path,receipt,"teleport-potion",r.Slot+":"+r.ItemId+":"+r.List+":"+r.Town,delegate(Offline70EconomyState s){
   Offline70EconomyEntry source=Offline70Economy.Find(s,r.List,r.Slot);Offline70EconomyItemDefinition d=Offline70Economy.Definition(r.ItemId);
   if(source==null||source.ItemId!=r.ItemId||source.Quantity==0||s.Level<d.MinimumLevel||d.Kind!=1)throw new InvalidDataException("Teleport253 ownership/level");
   Offline70EconomyCooldown group=null;foreach(Offline70EconomyCooldown c in s.Cooldowns)if(c.Group==d.CooldownGroup)group=c;
   long cooldown=(long)d.CooldownMilliseconds*TimeSpan.TicksPerMillisecond;if(cooldown>0&&group!=null&&(ticks<group.UtcTicks||ticks-group.UtcTicks<cooldown))throw new InvalidDataException("Teleport253 cooldown");
   if(source.Quantity==1)s.Inventory.Remove(source);else source.Quantity--;s.SavedTown=r.Town;s.LastUseUtcTicks=ticks;
   if(cooldown>0){if(group==null){group=new Offline70EconomyCooldown{Group=d.CooldownGroup};s.Cooldowns.Add(group);}group.UtcTicks=ticks;}
  },out duplicate);
 }
 internal static byte[] Body(Offline70TeleportRequestFG94 r,bool success){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)(success?1:0));if(success){w.Write(r.Slot);w.Write(r.ItemId);}else w.Write((byte)0x13);return m.ToArray();}}
 internal static byte[] Reply(Offline70TeleportRequestFG94 r,bool success,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,253,Body(r,success),keys,false);}
 internal static byte[] Effect(byte[] keys){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((ushort)1);w.Write((ushort)474);w.Write((ushort)234);w.Write((byte)5);return Offline70RoleProtocol.BatchPacketFG(0,150,m.ToArray(),keys,false);}}
}
