using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;
// CN command28 is DISJOINT_ITEM. Command26 is a different operation.
class Offline70DisjointFG92 {
 static readonly RNGCryptoServiceProvider RandomSource=new RNGCryptoServiceProvider();
 internal class Request {internal ushort Slot;internal byte List;internal short Tool;}
 internal static Request Parse(byte[] encrypted,byte[] keys){
  if(encrypted==null||encrypted.Length!=8)throw new InvalidDataException("Disjoint28 request length");
  byte[] b=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,0,false);
  for(int i=5;i<8;i++)if(b[i]!=0)throw new InvalidDataException("Disjoint28 request padding");
  Request r=new Request{Slot=BitConverter.ToUInt16(b,0),List=b[2],Tool=BitConverter.ToInt16(b,3)};
  if(r.List!=0||r.Slot<3||r.Slot>=41||r.Tool!=-1)throw new InvalidDataException("Only original NPC ordinary-equipment disjoint supported");return r;
 }
 internal static int Draw(int bound){if(bound<=0||bound>10000)throw new InvalidDataException("Disjoint random bound");uint limit=UInt32.MaxValue-UInt32.MaxValue%(uint)bound;byte[] bytes=new byte[4];lock(RandomSource){uint value;do{RandomSource.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=limit);return (int)(value%(uint)bound);}}
 internal static uint BaseCount(Offline70DisjointCatalogFG92.Profile p){
  // TW84733F6: GetSellPrice*1.1, ordinary sale rate, integer /150,
  // then original single-precision rarity multiplier and /1000.
  ulong adjusted=(ulong)p.Value*11/10;ulong price=adjusted*200/1000;
  float count=(float)(price*1000/150)*Offline70DisjointCatalogFG92.BaseFactors[p.Rarity]/1000f;
  if(Single.IsNaN(count)||Single.IsInfinity(count)||count>1000000)throw new InvalidDataException("Disjoint count bounds");return Math.Max(1u,(uint)count);
 }
 internal static Offline70EconomyState Open(string path,string receipt,Request r,int choice,int chance,out bool duplicate){
  if(r==null||choice<0||choice>=10000||chance<0||chance>=10000)throw new InvalidDataException("Disjoint transaction arguments");
  return Offline70Economy.Apply(path,receipt,"disjoint",r.List+":"+r.Slot+":"+r.Tool,delegate(Offline70EconomyState s){
   Offline70EconomyEntry source=Offline70Economy.Find(s,r.List,r.Slot);Offline70DisjointCatalogFG92.Profile p;
   if(r.List!=0||r.Tool!=-1||source==null||source.Quantity!=1||Offline70Economy.Definition(source.ItemId).Kind!=4||!Offline70DisjointCatalogFG92.Profiles.TryGetValue(source.ItemId,out p))throw new InvalidDataException("Disjoint target is not ordinary owned equipment");
   uint basic=BaseCount(p);uint[] pool=Offline70DisjointCatalogFG92.Extras[p.Rarity];
   s.Inventory.Remove(source);Offline70Economy.AddAssetItem(s,3037,basic);
   if(pool.Length>0){
    float divisor=chance<Offline70DisjointCatalogFG92.JackpotPercents[p.Rarity]*100?Offline70DisjointCatalogFG92.JackpotDivisors[p.Rarity]:Offline70DisjointCatalogFG92.NormalDivisors[p.Rarity];
    float count=(float)p.Grade/divisor;if(divisor<=0||Single.IsNaN(count)||Single.IsInfinity(count)||count>1000000)throw new InvalidDataException("Disjoint extra count bounds");
    Offline70Economy.AddAssetItem(s,pool[choice%pool.Length],Math.Max(1u,(uint)count));
   }
  },out duplicate);
 }
 internal static byte[] Body(Request r,bool success,Offline70EconomyState before,Offline70EconomyState after){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)(success?1:0));if(!success){w.Write((byte)0x13);return m.ToArray();}
   List<Offline70EconomyEntry> gains=new List<Offline70EconomyEntry>();
   foreach(Offline70EconomyEntry e in after.Inventory)if(e.List==0){Offline70EconomyEntry old=Offline70Economy.Find(before,0,e.Slot);uint previous=old!=null&&old.ItemId==e.ItemId?old.Quantity:0;if(e.Quantity>previous)gains.Add(new Offline70EconomyEntry{Slot=e.Slot,ItemId=e.ItemId,Quantity=e.Quantity-previous});}
   if(gains.Count==0||gains.Count>32)throw new InvalidDataException("Disjoint reward projection");
   w.Write(r.Slot);w.Write(r.List);w.Write((byte)gains.Count);
   foreach(Offline70EconomyEntry e in gains){if(Offline70Economy.Definition(e.ItemId).Kind!=2)throw new InvalidDataException("Disjoint material projection");w.Write(e.Slot);w.Write(e.ItemId);w.Write(e.Quantity);}return m.ToArray();
  }
 }
 internal static byte[] Reply(byte[] body,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,28,body,keys,false);}
}
