using System;using System.IO;using System.Text;using System.Collections.Generic;
// Current CN worn pet slot is22, proven by493510/98CE40; older A14/86 slots differ.
class Offline70PetEquipFG97 {
 internal static Offline70EconomyEntry Equipped(Offline70EconomyState s){Offline70EconomyEntry e=Offline70Economy.Find(s,3,22);return e!=null&&Offline70PetFG96.IsPet(e.ItemId)?e:null;}
 internal static bool Same(Offline70EconomyEntry a,Offline70EconomyEntry b){return a==null&&b==null||a!=null&&b!=null&&a.ItemId==b.ItemId&&a.CreatureKey==b.CreatureKey;}
 // BF8CE0 branches7->3 atBF8F67/BF9193 and invokes the native owned-item
 // swap.3->7 returns atBF8FF7. Echo the real bag-first pair also for removal.
 // Avatar3->1 is likewise ignored by original BF8CE0; echo the real bag-first pair.
 // Do not remove+insert worn14:493510 cannot insert into an empty worn slot.
 internal static Offline70EconomyMoveRequest ReplyOrder(Offline70EconomyMoveRequest r){if(r.SourceList!=3||!((r.SourceSlot>=22&&r.SourceSlot<=25&&r.TargetList==7)||(r.SourceSlot<=8&&r.TargetList==1)))return r;return new Offline70EconomyMoveRequest{SourceList=r.TargetList,SourceSlot=r.TargetSlot,SourceItem=r.TargetItem,Quantity=r.Quantity,TargetList=r.SourceList,TargetSlot=r.SourceSlot,TargetItem=r.SourceItem,Tick=r.Tick,Alternate=r.Alternate,Reserved=r.Reserved};}
 // Original full appearance7FDF10 calls7FB840 at base offset32 after worn rows.
 // The wire living bit is inverted by7FB840.0 maps to +804=1 (inactive).
 //71BC70 applies it,74B790 returns +804==0. Healthy pets need wire1.
 // 32/41 were measured on the 4-byte-name record; the record is name-relative, so derive the pet
 // base from the u32 name length at +2 (petBase = 28+nameBytes, i.e. 32+(nameBytes-4)).
 internal static byte[] Appearance(byte[] record,Offline70EconomyState s){if(record==null||record.Length<26)throw new InvalidDataException("Base pet appearance bounds");int nameBytes=BitConverter.ToInt32(record,2);if(nameBytes<1||nameBytes>29||record.Length!=85+nameBytes)throw new InvalidDataException("Base pet appearance bounds");int petBase=28+nameBytes;Offline70EconomyEntry e=Equipped(s);if(e==null)return record;byte[] name=Encoding.GetEncoding(936).GetBytes(e.CreatureName);if(name.Length>=30)throw new InvalidDataException("Pet appearance name");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(record,0,petBase);w.Write(e.ItemId);w.Write((uint)name.Length);w.Write(name);w.Write((byte)(e.CreatureSatiety>0?1:0));w.Write(record,petBase+9,record.Length-petBase-9);return m.ToArray();}}
}
