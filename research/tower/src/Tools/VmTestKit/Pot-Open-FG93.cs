using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;
class Offline70PotFG93 {
 static readonly RNGCryptoServiceProvider RandomSource=new RNGCryptoServiceProvider();
 internal static int Draw(int bound){if(bound<=0||bound>100001)throw new InvalidDataException("Pot random bound");uint limit=UInt32.MaxValue-UInt32.MaxValue%(uint)bound;byte[] bytes=new byte[4];lock(RandomSource){uint value;do{RandomSource.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=limit);return (int)(value%(uint)bound);}}
 internal static int Seed(){byte[] b=new byte[4];lock(RandomSource){RandomSource.GetBytes(b);return BitConverter.ToInt32(b,0)&Int32.MaxValue;}}
 internal static uint UnixNow(){return checked((uint)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds);}
 // Three independent current CN clicks have a zero two-byte prefix followed
 // by the actual slot. TW29 reads its slot at zero and is not wire-compatible.
 internal static ushort Parse(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=16)throw new InvalidDataException("Pot29 request block");byte[] b=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,1,false);if(BitConverter.ToUInt16(b,0)!=0)throw new InvalidDataException("Pot29 unsupported prefix");for(int i=4;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Pot29 trailing bytes");ushort slot=BitConverter.ToUInt16(b,2);if(!(slot>=3&&slot<9||slot>=57&&slot<89))throw new InvalidDataException("Pot29 inventory slot");return slot;}
 internal static uint Select(Offline70PotCatalogFG93.Pool p,int roll){if(p==null||roll<0||roll>100000)throw new InvalidDataException("Pot roll");uint sum=0;foreach(uint[] r in p.Rewards){sum+=r[1];if(roll<sum)return r[0];}return p.Fallback;}
 internal static uint[] UpgradeBand(int roll){if(roll<0||roll>=10000)throw new InvalidDataException("Pot upgrade roll");foreach(uint[] b in Offline70PotCatalogFG93.UpgradeBands){if(roll<b[2])return b;roll-=(int)b[2];}throw new InvalidDataException("Original pot upgrade bands missing");}
 internal static byte Upgrade(int roll,int value){uint[] b=UpgradeBand(roll);if(value<0||value>b[1]-b[0])throw new InvalidDataException("Pot upgrade value");return (byte)(b[0]+(uint)value);}
 internal static Offline70EconomyState Open(string path,string receipt,ushort slot,int roll,int upgradeRoll,int upgradeValue,int seed,uint now,out bool duplicate){
  if(seed<0||now<1500000000)throw new InvalidDataException("Pot seed/time bounds");
  return Offline70Economy.Apply(path,receipt,"pot-open",slot.ToString(),delegate(Offline70EconomyState s){
   Offline70EconomyEntry source=Offline70Economy.Find(s,0,slot);Offline70PotCatalogFG93.Pool p;if(source==null||!Offline70PotCatalogFG93.Pools.TryGetValue(source.ItemId,out p)||s.Level<p.Minimum||source.Quantity==0||s.Gold<p.Cost)throw new InvalidDataException("Pot ownership/level/gold");
   uint item=Select(p,roll);Offline70PotCatalogFG93.Reward reward=Offline70PotCatalogFG93.Rewards[item];HashSet<string> old=new HashSet<string>();foreach(Offline70EconomyEntry e in s.Inventory)old.Add(e.List+":"+e.Slot);
   if(source.Quantity==1){s.Inventory.Remove(source);old.Remove("0:"+slot);}else source.Quantity--;
   Offline70Economy.AddAssetItem(s,item,1);Offline70EconomyEntry added=null;foreach(Offline70EconomyEntry e in s.Inventory)if(e.ItemId==item&&!old.Contains(e.List+":"+e.Slot)){if(added!=null)throw new InvalidDataException("Pot new instance ambiguity");added=e;}
   if(added==null)throw new InvalidDataException("Pot instance insertion missing");
   if(reward.Kind==4){added.EquipmentSeed=seed;added.Upgrade=Upgrade(upgradeRoll,upgradeValue);added.Sealed=reward.Sealed;added.Durability=Offline70Economy.Definition(item).MaxDurability;}
   else if(reward.Kind==3){added.Ability=0;added.ExpiresUnix=reward.Period==0?0:checked(now+(uint)reward.Period*86400u);}
   else throw new InvalidDataException("Unregistered original pot category");s.Gold-=p.Cost;
  },out duplicate);
 }
 internal static Offline70EconomyEntry NewReward(Offline70EconomyState before,Offline70EconomyState after){Offline70EconomyEntry found=null;foreach(Offline70EconomyEntry e in after.Inventory)if((e.List==0||e.List==1)&&Offline70Economy.Find(before,e.List,e.Slot)==null){if(found!=null)throw new InvalidDataException("Pot reward projection ambiguity");found=e;}if(found==null)throw new InvalidDataException("Pot reward projection absent");return found;}
 internal static byte[] Body(ushort source,Offline70EconomyEntry reward,bool success){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)(success?1:0));if(!success){w.Write((byte)0x13);return m.ToArray();}Offline70EconomyItemDefinition d=Offline70Economy.Definition(reward.ItemId);w.Write(source);w.Write(reward.Slot);w.Write(reward.ItemId);w.Write(d.Kind==4?(uint)reward.EquipmentSeed:(uint)0);w.Write(d.Kind==3?reward.Ability:reward.Durability);w.Write(reward.Upgrade);w.Write((byte)0);w.Write((ushort)0);if(d.Kind==3){w.Write((uint)30);w.Write(new byte[30]);}w.Write(reward.ExpiresUnix);w.Write((byte)0);w.Write((ushort)0);w.Write((byte)0);w.Write((byte)0);return m.ToArray();}}
 internal static byte[] Reply(byte[] body,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,29,body,keys,true);}
 internal static byte[] AvatarPeriodReply(Offline70EconomyEntry reward,byte[] keys){if(reward==null||reward.List!=1||reward.ExpiresUnix==0)throw new InvalidDataException("Pot avatar period context");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)1);w.Write((ushort)1);w.Write(Offline70EconomyProtocol.InventoryRow(reward));return Offline70EconomyProtocol.UpdatePacket(m.ToArray(),keys);}}
}
