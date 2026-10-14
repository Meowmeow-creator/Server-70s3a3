using System;using System.IO;
class Offline70NpcRepairFG63 {
 internal class Request {internal byte List;internal ushort Slot;}
 internal static Request Parse(byte[] cipher,byte[] keys){if(cipher==null||cipher.Length!=8)throw new InvalidDataException("Repair request length");byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,11,false);Request r=new Request{List=b[0],Slot=BitConverter.ToUInt16(b,1)};if(BitConverter.ToUInt16(b,3)!=UInt16.MaxValue||b[5]!=0||b[6]!=0||b[7]!=0)throw new InvalidDataException("Repair request marker or padding");if(!(r.List==0&&(r.Slot==UInt16.MaxValue||r.Slot>=9&&r.Slot<=40)||r.List==3&&r.Slot>=11&&r.Slot<=22))throw new InvalidDataException("Repair target bounds");return r;}
 internal static void ParseAll(byte[] cipher,byte[] keys){Request r=Parse(cipher,keys);if(r.List!=0||r.Slot!=UInt16.MaxValue)throw new InvalidDataException("Not all-repair");}
 internal static byte[] Reply(Offline70EconomyState s,bool success,byte[] keys){return Reply(s,success,keys,0,UInt16.MaxValue);}
 internal static byte[] Reply(Offline70EconomyState s,bool success,byte[] keys,byte list,ushort slot){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)(success?1:0));if(success){w.Write(s.Gold);w.Write(list);w.Write(slot);}else w.Write((byte)0x11);return Offline70RoleProtocol.BatchPacketFG(1,25,m.ToArray(),keys,true);}}
}
