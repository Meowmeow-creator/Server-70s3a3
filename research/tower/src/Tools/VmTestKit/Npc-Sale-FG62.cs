using System;using System.IO;
// Verified CN70 prices: handbone3015=24; belt16400=value160*200/1000=32
// at durability20. This is not an all-item sale catalog.
class Offline70NpcSaleFG62 {
 internal class Request {internal byte List;internal ushort Slot,Count;}
 static bool Slot(ushort n){return n>=3&&n<=40||n>=57&&n<=88||n>=105&&n<=136;}
 internal static Request Parse(byte[] cipher,byte[] keys){if(cipher==null||cipher.Length!=16)throw new InvalidDataException("NPC sale request size");byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,10,false);Request r=new Request{List=b[0],Slot=BitConverter.ToUInt16(b,1),Count=BitConverter.ToUInt16(b,3)};for(int i=9;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("NPC sale padding");if(r.List!=0||!Slot(r.Slot)||r.Count==0||BitConverter.ToUInt32(b,5)!=(uint)r.List+r.Slot+r.Count)throw new InvalidDataException("NPC sale category or checksum");return r;}
 internal static uint UnitPrice(Offline70EconomyEntry e){return Offline70CommerceFG71.SalePrice(e);}
 internal static Offline70EconomyState Sell(string path,string receipt,Request r,out bool duplicate){if(r==null||r.List!=0||!Slot(r.Slot)||r.Count==0)throw new InvalidDataException("NPC sale request");return Offline70Economy.Apply(path,receipt,"npc-sale","verified-sale-FG66:"+r.List+":"+r.Slot+":"+r.Count,delegate(Offline70EconomyState s){Offline70EconomyEntry e=Offline70Economy.Find(s,r.List,r.Slot);uint unit=UnitPrice(e);if(e.Quantity<r.Count||Offline70Economy.Definition(e.ItemId).Kind==4&&r.Count!=1)throw new InvalidDataException("NPC sale quantity");ulong gain=(ulong)r.Count*unit;if(gain>Offline70Economy.WalletLimit-s.Gold)throw new InvalidDataException("NPC sale wallet overflow");e.Quantity-=r.Count;if(e.Quantity==0)s.Inventory.Remove(e);s.Gold+=(uint)gain;},out duplicate);}
 internal static byte[] Reply(Offline70EconomyState s,Request r,bool success,byte[] keys){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)(success?1:0));if(success){w.Write(s.Gold);w.Write(r.List);w.Write(r.Slot);w.Write(r.Count);}else w.Write((byte)0x17);return Offline70RoleProtocol.BatchPacketFG(1,24,m.ToArray(),keys,true);}}
}
