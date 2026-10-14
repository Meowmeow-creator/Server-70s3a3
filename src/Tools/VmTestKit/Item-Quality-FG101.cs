using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;
// TW70 ModItemAttr and current CN84: target ushort/item uint/source ushort;
// ACK is source ushort/remaining uint/operation ushort, then target notify14.
class Offline70ItemQualityFG101 {
 internal class Request {internal ushort Target,Source;internal uint Item;}
 static readonly RNGCryptoServiceProvider RandomSource=new RNGCryptoServiceProvider();
 internal static Request Parse(byte[] encrypted,byte[] keys){if(encrypted==null||(encrypted.Length!=8&&encrypted.Length!=16))throw new InvalidDataException("Quality84 length");byte[] b=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,0,false);for(int i=8;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Quality84 padding");Request r=new Request{Target=BitConverter.ToUInt16(b,0),Item=BitConverter.ToUInt32(b,2),Source=BitConverter.ToUInt16(b,6)};if(r.Target<3||r.Target>=41||r.Source==r.Target||!(r.Source>=3&&r.Source<9||r.Source>=57&&r.Source<89)||!Offline70ItemQualityCatalogFG101.Items.Contains(r.Item))throw new InvalidDataException("Quality84 position or original equipment type");return r;}
 // Original TW get_rand_int(0) calls libc rand(): all nonnegative signed31 bits,
 // including zero and the existing seed. It does not force a better/different grade.
 internal static int Draw(){byte[] b=new byte[4];lock(RandomSource)RandomSource.GetBytes(b);return (int)(BitConverter.ToUInt32(b,0)&0x7fffffffu);}
 internal static Offline70EconomyState Reset(string path,string receipt,Request r,int seed,out bool duplicate){if(r==null||seed<0)throw new InvalidDataException("Quality84 transaction");return Offline70Economy.Apply(path,receipt,"item-quality",r.Target+":"+r.Item+":"+r.Source,delegate(Offline70EconomyState s){Offline70EconomyEntry target=Offline70Economy.Find(s,0,r.Target),source=Offline70Economy.Find(s,0,r.Source);if(r.Target==r.Source||!Offline70ItemQualityCatalogFG101.Items.Contains(r.Item)||target==null||target.ItemId!=r.Item||target.Quantity!=1||Offline70Economy.Definition(target.ItemId).Kind!=4||source==null||source.ItemId!=15||source.Quantity==0)throw new InvalidDataException("Quality84 owned equipment and box required");target.EquipmentSeed=seed;if(source.Quantity==1)s.Inventory.Remove(source);else source.Quantity--;},out duplicate);}
 internal static byte[] Reply(Request r,Offline70EconomyState after,bool ok,byte[] keys){return Reply(r,after,ok,keys,2);}
 internal static byte[] Reply(Request r,Offline70EconomyState after,bool ok,byte[] keys,ushort operation){byte[] b=ok?new byte[9]:new byte[]{0,0x13};if(ok){b[0]=1;BitConverter.GetBytes(r.Source).CopyTo(b,1);Offline70EconomyEntry source=Offline70Economy.Find(after,0,r.Source);BitConverter.GetBytes(source==null?0u:source.Quantity).CopyTo(b,3);BitConverter.GetBytes(operation).CopyTo(b,7);}return Offline70RoleProtocol.BatchPacketFG(1,84,b,keys,false);}
 internal static byte[] Target(Offline70EconomyEntry e,byte[] keys){byte[] b=new byte[34];b[0]=0;b[1]=1;Offline70EconomyProtocol.InventoryRow(e).CopyTo(b,3);return Offline70RoleProtocol.BatchPacketFG(0,14,b,keys,false);}
}
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> qualityRequestsFG101=new Dictionary<ushort,string>();
 void QualityFG101(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;
  if(qualityRequestsFG101.TryGetValue(sequence,out prior)){if(prior!=fingerprint)Send("quality-sequence-failed84",Offline70ItemQualityFG101.Reply(null,null,false,keys));else Note("QUALITY_DUPLICATE_IGNORED");return;}
  Offline70ItemQualityFG101.Request r=null;Dictionary<ushort,uint> collection=null;bool success=false;ushort operation=2;
  try{
   if(dungeon.Active)throw new InvalidDataException("Quality84 requires town");
   r=Offline70ItemQualityFG101.Parse(encrypted,keys);Sync();collection=CollectionSnapshot();bool duplicate;
   Offline70EconomyEntry material=Offline70Economy.Find(assets,0,r.Source);
   if(material!=null&&material.ItemId==14){operation=1;assets=Offline70ItemResealFG102.Reseal(economyPath,session+"-reseal-"+sequence,r,out duplicate);}
   else assets=Offline70ItemQualityFG101.Reset(economyPath,session+"-quality-"+sequence,r,Offline70ItemQualityFG101.Draw(),out duplicate);
   Sync();success=true;Note("QUALITY_COMMITTED item="+r.Item+" slot="+r.Target+" seed="+Offline70Economy.Find(assets,0,r.Target).EquipmentSeed+" duplicate="+duplicate);
  }catch(InvalidDataException e){Note("QUALITY_REJECTED "+e.Message);}
  qualityRequestsFG101.Add(sequence,fingerprint);Send(success?"quality-reset84":"quality-failed84",Offline70ItemQualityFG101.Reply(r,assets,success,keys,operation));
  if(success){Send("quality-target14",Offline70ItemQualityFG101.Target(Offline70Economy.Find(assets,0,r.Target),keys));CollectionChanged(collection);}
 }
}
