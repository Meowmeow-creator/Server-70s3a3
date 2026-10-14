using System;using System.IO;using System.Text;using System.Collections.Generic;
// Original CN70 commodity ids and rewards are generated from cerashop.etc.
class Offline70CashPurchaseFG45 {
 internal class Offer {internal uint Commodity,Category,Item,Cost,Voucher,Days;internal ushort Ability;internal byte Special;internal bool Once;internal uint[][] Rewards;}
 internal class Request {internal uint Commodity;internal byte Option,Period,Payment;}
 internal static void Register(){Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682608,Kind=1,StackLimit=1,CooldownMilliseconds=1000});Offline70CashCatalogFG80.Register();}
 internal static Request Parse(byte[] cipher,byte[] keys){
  if(cipher==null||cipher.Length!=16)throw new InvalidDataException("Cash67 single selection length");
  byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,11,false);
  if(b[0]!=0||b[1]!=1)throw new InvalidDataException("Unverified cash gifting or cart layout");
  for(int i=9;i<16;i++)if(b[i]!=0)throw new InvalidDataException("Unverified cash selection extras");
  return new Request{Payment=b[2],Option=b[3],Period=b[4],Commodity=BitConverter.ToUInt32(b,5)};
 }
 internal static Offer Resolve(Request r){
  Offer selected=Offline70FeedbackCashFG108.Resolve(r);if(selected!=null)return selected;
  Offer offer;if(r==null||r.Payment!=0||r.Option!=255||r.Period!=0&&r.Period!=255||!Offline70CashCatalogFG80.Offers.TryGetValue(r.Commodity,out offer))throw new InvalidDataException("Unimplemented cash commodity, payment or option");
  return offer;
 }
 internal static Offline70EconomyState Purchase(string path,string receipt,Offer offer,out bool duplicate){
  if(offer!=null&&offer.Special!=0)return Offline70FeedbackCashFG108.Purchase(path,receipt,new Offer[]{offer},out duplicate);
  if(offer==null||!Object.ReferenceEquals(Resolve(new Request{Commodity=offer.Commodity,Option=255}),offer))throw new InvalidDataException("Unregistered cash offer");
  StringBuilder payload=new StringBuilder();payload.Append(offer.Commodity).Append(':').Append(offer.Cost);
  foreach(uint[] reward in offer.Rewards)payload.Append(':').Append(reward[0]).Append(':').Append(reward[1]);
  return Offline70Economy.Apply(path,receipt,"cash-commodity",payload.ToString(),delegate(Offline70EconomyState s){
   if(s.OfflineCash<offer.Cost)throw new InvalidDataException("Insufficient offline cash");
   if(offer.Once&&s.CashOncePurchases.Contains(offer.Commodity))throw new InvalidDataException("Cash commodity lifetime limit reached");
   foreach(uint[] reward in offer.Rewards){
    if(reward[0]==1){if(reward[1]>Offline70Economy.WalletLimit-s.RevivalCoins)throw new InvalidDataException("Revival balance capacity");s.RevivalCoins+=reward[1];}
    else Offline70Economy.AddPurchasedItem(s,reward[0],reward[1]);
   }
   s.OfflineCash-=offer.Cost;if(offer.Once)s.CashOncePurchases.Add(offer.Commodity);
  },out duplicate);
 }
 internal static byte[] Body(bool success,Request request,Offer offer){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)(success?1:0));if(!success)w.Write((byte)0x13);w.Write((byte)0);
   w.Write(offer==null?(uint)2:offer.Category);w.Write(request==null?(uint)0:request.Commodity);
   w.Write((uint)0);w.Write((uint)0);w.Write(success?UInt32.MaxValue:(uint)0);
   if(success){w.Write((ushort)offer.Rewards.Length);foreach(uint[] r in offer.Rewards){w.Write(r[0]);w.Write(r[1]);}}
   return m.ToArray();
  }
 }
 internal static byte[] Reply(bool success,Request r,Offer offer,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,67,Body(success,r,offer),keys,true);}
 // Retain the accepted cookie fixture API.
 internal static Offline70EconomyState Purchase(string path,string receipt,out bool duplicate){return Purchase(path,receipt,Offline70CashCatalogFG80.Offers[101099],out duplicate);}
 internal static byte[] Body(bool success){return Body(success,new Request{Commodity=101099},Offline70CashCatalogFG80.Offers[101099]);}
 internal static byte[] Reply(bool success,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,67,Body(success),keys,true);}
}
