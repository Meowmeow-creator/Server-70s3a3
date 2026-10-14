using System;using System.IO;using System.Collections.Generic;
// CN PVF RANDOMBOX contains two distinct int-data groups. Every row and
// fallback in each group resolves to the same item/count, so this box is fixed.
class Offline70WishBoxFG84 {
 internal const int CooldownKey=9804; // Reserved offline item-specific cooldown key.
 internal static void Register(){
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2600014,Kind=1,StackLimit=1000,MinimumLevel=60,CooldownMilliseconds=10000});
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682272,Kind=1,StackLimit=1000,MinimumLevel=1});
 }
 internal static ushort Parse(byte[] cipher,byte[] keys){
  if(cipher==null||cipher.Length!=16)throw new InvalidDataException("Random box223 length");
  byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,13,false);
  if(b[0]!=4||BitConverter.ToUInt16(b,3)!=65535)throw new InvalidDataException("Unimplemented random box mode/key");
  for(int i=5;i<16;i++)if(b[i]!=0)throw new InvalidDataException("Random box padding");
  ushort slot=BitConverter.ToUInt16(b,1);if(!(slot>=3&&slot<9||slot>=57&&slot<89))throw new InvalidDataException("Random box slot");return slot;
 }
 internal static Offline70EconomyState Open(string path,string receipt,ushort slot,long utcTicks,out bool duplicate){
  if(utcTicks<0||utcTicks>DateTime.MaxValue.Ticks)throw new InvalidDataException("Box time");
  return Offline70Economy.Apply(path,receipt,"wish-box",slot+":2681920",delegate(Offline70EconomyState s){
   Offline70EconomyEntry box=Offline70Economy.Find(s,0,slot);if(box==null||box.ItemId!=2681920||s.Level<1)throw new InvalidDataException("Missing supported wish box");
   // Cooldown belongs to the source item, not unrelated potion groups.
   Offline70EconomyCooldown cooldown=null;foreach(Offline70EconomyCooldown c in s.Cooldowns)if(c.Group==CooldownKey)cooldown=c;
   if(cooldown!=null&&(utcTicks<cooldown.UtcTicks||utcTicks-cooldown.UtcTicks<TimeSpan.TicksPerSecond))throw new InvalidDataException("Wish box cooldown");
   if(box.Quantity==1)s.Inventory.Remove(box);else box.Quantity--;
   Offline70Economy.AddAssetItem(s,2600014,2);Offline70Economy.AddAssetItem(s,2682272,1);
   if(cooldown==null){cooldown=new Offline70EconomyCooldown{Group=CooldownKey};s.Cooldowns.Add(cooldown);}cooldown.UtcTicks=utcTicks;
  },out duplicate);
 }
 internal static byte[] Body(bool success,ushort slot,Offline70EconomyState before,Offline70EconomyState after){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)(success?1:0));if(!success){w.Write((byte)0x13);return m.ToArray();}
   w.Write((byte)4);w.Write((byte)0);w.Write(slot);w.Write((ushort)65535);
   List<Offline70EconomyEntry> gains=new List<Offline70EconomyEntry>();
   foreach(Offline70EconomyEntry e in after.Inventory)if(e.List==0&&(e.ItemId==2600014||e.ItemId==2682272)){
    Offline70EconomyEntry old=Offline70Economy.Find(before,e.List,e.Slot);uint prior=old!=null&&old.ItemId==e.ItemId?old.Quantity:0;
    if(e.Quantity>prior)gains.Add(new Offline70EconomyEntry{Slot=e.Slot,ItemId=e.ItemId,Quantity=e.Quantity-prior});
   }
   uint teleport=0,blessing=0;foreach(Offline70EconomyEntry g in gains){if(g.ItemId==2600014)teleport+=g.Quantity;else blessing+=g.Quantity;}
   if(teleport!=2||blessing!=1)throw new InvalidDataException("Wish box award projection");
   w.Write(checked((ushort)gains.Count));foreach(Offline70EconomyEntry g in gains){
    // C111D0: slot2,item4,quantity4,upgrade2,event1,seal4,
    // option-count1 (463C20), state2 (40B1F0), list-count1 (C7E980), tail1.
    w.Write(g.Slot);w.Write(g.ItemId);w.Write(g.Quantity);w.Write((ushort)0);w.Write((byte)0);w.Write((uint)0);w.Write((byte)0);w.Write((ushort)0);w.Write((byte)0);w.Write((byte)0);
   }
   return m.ToArray();
  }
 }
 // Original CN 105DAF0 marks223 as an unfiltered response: no folded trailer.
 internal static byte[] Reply(byte[] body,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,223,body,keys,false);}
}
