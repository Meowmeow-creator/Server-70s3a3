using System;using System.IO;using System.Text;using System.Collections.Generic;
// Current CN user all-buy captures: header gift/count, nine bytes per ordinary
// selection, then the original eight-byte cipher padding. A14 ReadCashPurchases
// confirms per-row payment/options, avatar-choice count and package-choice count.
// Gifting, alternate payments and variable option/selection groups remain refused.
class Offline70CashCartFG104 {
 internal static Offline70CashPurchaseFG45.Request[] Parse(byte[] cipher,byte[] keys){
  if(cipher==null||cipher.Length<16||cipher.Length>328||cipher.Length%8!=0)throw new InvalidDataException("Cash67 cart bounds");
  byte[] b=Offline70RoleProtocol.BatchCryptFG(cipher,keys,11,false);int count=b[1];
  if(b[0]!=0||count<1||count>36)throw new InvalidDataException("Cash67 cart count or gift");
  int used=2+9*count;if(cipher.Length!=((used+7)&~7))throw new InvalidDataException("Cash67 cart stride");
  Offline70CashPurchaseFG45.Request[] rows=new Offline70CashPurchaseFG45.Request[count];
  for(int i=0;i<count;i++){int at=2+9*i;if(b[at+7]!=0||b[at+8]!=0)throw new InvalidDataException("Cash67 variable choices not implemented");rows[i]=new Offline70CashPurchaseFG45.Request{Payment=b[at],Option=b[at+1],Period=b[at+2],Commodity=BitConverter.ToUInt32(b,at+3)};if(rows[i].Commodity==0)throw new InvalidDataException("Cash67 empty commodity");}
  for(int i=used;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Cash67 cart padding");return rows;
 }
 internal static Offline70EconomyState Purchase(string path,string receipt,Offline70CashPurchaseFG45.Offer[] offers,out bool duplicate){
  if(offers==null||offers.Length<1||offers.Length>36)throw new InvalidDataException("Cash cart selection count");
  foreach(Offline70CashPurchaseFG45.Offer selected in offers)if(selected!=null&&selected.Special!=0)return Offline70FeedbackCashFG108.Purchase(path,receipt,offers,out duplicate);
  if(offers.Length==1)return Offline70CashPurchaseFG45.Purchase(path,receipt,offers[0],out duplicate);
  StringBuilder payload=new StringBuilder();ulong total=0;
  foreach(Offline70CashPurchaseFG45.Offer offer in offers){if(offer==null||!Object.ReferenceEquals(Offline70CashPurchaseFG45.Resolve(new Offline70CashPurchaseFG45.Request{Commodity=offer.Commodity,Option=255}),offer))throw new InvalidDataException("Cash cart unregistered offer");total+=offer.Cost;payload.Append('|').Append(offer.Commodity).Append(':').Append(offer.Cost);foreach(uint[] reward in offer.Rewards)payload.Append(':').Append(reward[0]).Append(':').Append(reward[1]);}
  if(total>Offline70Economy.WalletLimit)throw new InvalidDataException("Cash cart total exceeds balance range");uint cost=(uint)total;
  return Offline70Economy.Apply(path,receipt,"cash-cart",payload.ToString(),delegate(Offline70EconomyState s){
   if(s.OfflineCash<cost)throw new InvalidDataException("Insufficient cash for entire cart");
   foreach(Offline70CashPurchaseFG45.Offer offer in offers){
    if(offer.Once&&s.CashOncePurchases.Contains(offer.Commodity))throw new InvalidDataException("Cash cart lifetime purchase limit");
    foreach(uint[] reward in offer.Rewards){if(reward[0]==1){if(reward[1]>Offline70Economy.WalletLimit-s.RevivalCoins)throw new InvalidDataException("Cash cart revival capacity");s.RevivalCoins+=reward[1];}else Offline70Economy.AddPurchasedItem(s,reward[0],reward[1]);}
    if(offer.Once)s.CashOncePurchases.Add(offer.Commodity);
   }
   s.OfflineCash-=cost;
  },out duplicate);
 }
 internal static byte[][] Replies(bool success,Offline70CashPurchaseFG45.Request[] requests,Offline70CashPurchaseFG45.Offer[] offers,byte[] keys){
  int count=requests==null?1:requests.Length;byte[][] replies=new byte[count][];
  for(int i=0;i<count;i++){
   Offline70CashPurchaseFG45.Request request=requests==null?null:requests[i];Offline70CashPurchaseFG45.Offer offer=offers==null?null:offers[i];byte[] b=Offline70CashPurchaseFG45.Body(success,request,offer);
   // BFB8A0 compares the ordinal and total before closing the all-buy workflow.
   if(count>1){BitConverter.GetBytes((uint)(i+1)).CopyTo(b,success?10:11);BitConverter.GetBytes((uint)count).CopyTo(b,success?14:15);}
   replies[i]=Offline70RoleProtocol.BatchPacketFG(1,67,b,keys,true);
  }
  return replies;
 }
}
