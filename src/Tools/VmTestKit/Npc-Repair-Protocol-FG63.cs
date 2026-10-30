using System;using System.IO;
class Offline70NpcRepairFG63 {
 internal class Request {internal byte List;internal ushort Slot;}
 // Native check (Store::repair_equip, reference/rednf Store/repair_equip.inc) has NO slot
 // bound at all: msg space 0 -> backpack, 2 -> cargo, 3 -> worn panel, anything else 0x11,
 // and the row itself is validated (m_addInfo!=0, ITEM_TYPE_EQUIP, max endurance!=0). FG398
 // drops the invented 9..40 / 11..22 windows that rejected slot 10 (weapon) and slot 22 on
 // one side each; the core resolves the row and answers 0x11 when nothing is there.
 // Cargo (2) stays refused: that reply needs a cargo-space refresh we do not model yet.
 internal static Request Parse(byte[] cipher,byte[] keys){if(cipher==null||cipher.Length!=8)throw new InvalidDataException("Repair request length");byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,11,false);Request r=new Request{List=b[0],Slot=BitConverter.ToUInt16(b,1)};if(BitConverter.ToUInt16(b,3)!=UInt16.MaxValue||b[5]!=0||b[6]!=0||b[7]!=0)throw new InvalidDataException("Repair request marker or padding");if(r.List!=0&&r.List!=3)throw new InvalidDataException("Repair target space");return r;}
 internal static void ParseAll(byte[] cipher,byte[] keys){Request r=Parse(cipher,keys);if(r.List!=0||r.Slot!=UInt16.MaxValue)throw new InvalidDataException("Not all-repair");}
 internal static byte[] Reply(Offline70EconomyState s,bool success,byte[] keys){return Reply(s,success,keys,0,UInt16.MaxValue);}
 internal static byte[] Reply(Offline70EconomyState s,bool success,byte[] keys,byte list,ushort slot){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)(success?1:0));if(success){w.Write(s.Gold);w.Write(list);w.Write(slot);}else w.Write((byte)0x11);return Offline70RoleProtocol.BatchPacketFG(1,25,m.ToArray(),keys,true);}}
}
