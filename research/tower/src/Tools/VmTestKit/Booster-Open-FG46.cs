using System;using System.IO;
// Current fortune_cookie_cerashop.stk has two weighted rows, both2682628 x1.
// This first opening is deterministic. The resulting box's two reward pools
// and percentage/status potions are separate from this verified first stage.
class Offline70BoosterOpenFG46 {
 internal static void Register(){Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682628,Kind=1,StackLimit=1,CooldownMilliseconds=1000});}
 internal static ushort Parse(byte[] cipher,byte[] keys){if(cipher==null||cipher.Length!=8)throw new InvalidDataException("Booster175 length");byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,7,false);for(int i=2;i<8;i++)if(b[i]!=0)throw new InvalidDataException("Booster175 padding");ushort slot=BitConverter.ToUInt16(b,0);if(!(slot>=3&&slot<9||slot>=57&&slot<89))throw new InvalidDataException("Booster175 slot");return slot;}
 internal static Offline70EconomyState Open(string path,string receipt,ushort slot,out bool duplicate){return Offline70Economy.Apply(path,receipt,"booster-open",slot+":2682608:2682628:1",delegate(Offline70EconomyState s){Offline70EconomyEntry e=Offline70Economy.Find(s,0,slot);if(e==null||e.ItemId!=2682608||e.Quantity!=1)throw new InvalidDataException("Unverified booster or missing source");s.Inventory.Remove(e);Offline70Economy.AddAssetItem(s,2682628,1);},out duplicate);}
 internal static byte[] Body(bool success,ushort slot){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)(success?1:0));if(!success)w.Write((byte)0x13);else{w.Write(slot);w.Write((uint)2682608);w.Write((uint)1);w.Write((ushort)1);w.Write((uint)2682628);w.Write((uint)1);}return m.ToArray();}}
 internal static byte[] Reply(bool success,ushort slot,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,175,Body(success,slot),keys,true);}
}
