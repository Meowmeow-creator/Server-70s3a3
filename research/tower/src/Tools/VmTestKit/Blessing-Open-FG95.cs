using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;
class Offline70BlessingFG95 {
 static readonly RNGCryptoServiceProvider RandomSource=new RNGCryptoServiceProvider();
 internal static int Draw(){uint bound=Offline70BlessingCatalogFG95.Denominator,limit=UInt32.MaxValue-UInt32.MaxValue%bound;byte[] b=new byte[4];lock(RandomSource){uint n;do{RandomSource.GetBytes(b);n=BitConverter.ToUInt32(b,0);}while(n>=limit);return (int)(n%bound);}}
 internal static uint[] Select(int group,int roll){if(group<0||group>=Offline70BlessingCatalogFG95.Pools.Length||roll<0||roll>=Offline70BlessingCatalogFG95.Denominator)throw new InvalidDataException("Blessing pool/roll");Offline70BlessingCatalogFG95.Pool p=Offline70BlessingCatalogFG95.Pools[group];uint sum=0;foreach(uint[] row in p.Rewards){sum+=row[1];if((uint)roll<sum)return new uint[]{row[0],row[2],row[3]};}return (uint[])p.Fallback.Clone();}
 internal static Offline70EconomyState Open(string path,string receipt,ushort slot,int[] rolls,out bool duplicate){
  if(rolls==null||rolls.Length!=3)throw new InvalidDataException("Three original blessing groups required");uint[][] awards=new uint[3][];for(int i=0;i<3;i++)awards[i]=Select(i,rolls[i]);
  return Offline70Economy.Apply(path,receipt,"blessing-box",slot+":2682272",delegate(Offline70EconomyState s){
   Offline70EconomyEntry box=Offline70Economy.Find(s,0,slot);if(box==null||box.ItemId!=Offline70BlessingCatalogFG95.Box||s.Level<1)throw new InvalidDataException("Blessing source ownership");if(box.Quantity==1)s.Inventory.Remove(box);else box.Quantity--;
   foreach(uint[] award in awards){if(award[0]==42){if(award[1]>Offline70Economy.WalletLimit-s.RevivalCoins)throw new InvalidDataException("Blessing revival capacity");s.RevivalCoins+=award[1];}else Offline70Economy.AddAssetItem(s,award[0],award[1]);}
  },out duplicate);
 }
 internal static List<Offline70EconomyEntry> Gains(Offline70EconomyState before,Offline70EconomyState after){
  Offline70Economy.Validate(before);Offline70Economy.Validate(after);List<Offline70EconomyEntry> gains=new List<Offline70EconomyEntry>();
  foreach(Offline70EconomyEntry e in after.Inventory){if(e.List!=0&&e.List!=7)continue;Offline70EconomyEntry old=Offline70Economy.Find(before,e.List,e.Slot);uint prior=old!=null&&old.ItemId==e.ItemId?old.Quantity:0;if(e.Quantity>prior)gains.Add(new Offline70EconomyEntry{List=e.List,Slot=e.Slot,ItemId=e.ItemId,Quantity=e.Quantity-prior,Sealed=e.Sealed});}
  if(after.RevivalCoins<before.RevivalCoins||before.Gold!=after.Gold||before.OfflineCash!=after.OfflineCash)throw new InvalidDataException("Blessing wallet projection");if(after.RevivalCoins>before.RevivalCoins)gains.Add(new Offline70EconomyEntry{List=0,Slot=UInt16.MaxValue,ItemId=42,Quantity=after.RevivalCoins-before.RevivalCoins});
  if(gains.Count==0||gains.Count>10)throw new InvalidDataException("Blessing award projection");return gains;
 }
 internal static byte[] Body(ushort slot,Offline70EconomyState before,Offline70EconomyState after){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){List<Offline70EconomyEntry> gains=Gains(before,after);w.Write((byte)1);w.Write((byte)4);w.Write((byte)0);w.Write(slot);w.Write(UInt16.MaxValue);w.Write(checked((ushort)gains.Count));foreach(Offline70EconomyEntry e in gains){w.Write(e.Slot);w.Write(e.ItemId);w.Write(e.Quantity);w.Write((ushort)0);w.Write((byte)0);w.Write((uint)0);w.Write((byte)0);w.Write((ushort)0);w.Write((byte)0);w.Write((byte)0);}return m.ToArray();}}
 // 65535 is the original223 display-only row. It never inserts item42 into a bag.
 internal static byte[] RevivalBody(Offline70EconomyState before,Offline70EconomyState after){if(before.RevivalCoins==after.RevivalCoins)return null;byte[] b=new byte[34];b[1]=1;BitConverter.GetBytes((ushort)1).CopyTo(b,3);BitConverter.GetBytes((uint)1).CopyTo(b,5);BitConverter.GetBytes(after.RevivalCoins).CopyTo(b,9);return b;}
}
