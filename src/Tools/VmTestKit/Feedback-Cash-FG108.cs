using System;using System.IO;using System.Text;using System.Collections.Generic;
// Selection and price data are generated from the unmodified current CN PVF.
class Offline70FeedbackCashFG108 {
 internal static bool AbilityValid(Offline70EconomyEntry e){if(Offline70AvatarCashFG210.Known(e.ItemId))return Offline70AvatarCashFG210.AbilityValid(e);if(Offline70SkinCashFG152.Known(e.ItemId))return Offline70SkinCashFG152.AbilityValid(e);foreach(Offline70FeedbackCatalogFG108.Avatar a in Offline70FeedbackCatalogFG108.Avatars.Values)if(a.Item==e.ItemId)return a.Options==0?e.Ability==0:e.Ability<a.Options;return e.Ability<=4;}
 internal static Offline70CashPurchaseFG45.Offer Resolve(Offline70CashPurchaseFG45.Request r){
  Offline70CashPurchaseFG45.Offer avatar=Offline70AvatarCashFG210.Resolve(r);if(avatar!=null)return avatar;
  Offline70CashPurchaseFG45.Offer package=Offline70CashPackageFG272.Resolve(r);if(package!=null)return package;
  Offline70CashPurchaseFG45.Offer skin=Offline70SkinCashFG152.Resolve(r);if(skin!=null)return skin;Offline70CashPurchaseFG45.Offer extra=Offline70FeedbackFG129.Resolve(r);if(extra!=null)return extra;if(r==null)return null;
  Offline70FeedbackCatalogFG108.Avatar a;
  if(Offline70FeedbackCatalogFG108.Avatars.TryGetValue(r.Commodity,out a)){
   if(r.Payment!=1||r.Period!=3||(a.Options==0?r.Option!=255:r.Option>=a.Options))throw new InvalidDataException("Rare avatar voucher/period/ability selection");
   return new Offline70CashPurchaseFG45.Offer{Commodity=r.Commodity,Category=0,Item=a.Item,Cost=0,Special=1,Voucher=2681594,Ability=a.Options==0?(ushort)0:r.Option,Rewards=new uint[][]{new uint[]{a.Item,1}}};
  }
  Offline70FeedbackCatalogFG108.Tag t;
  if(Offline70FeedbackCatalogFG108.Tags.TryGetValue(r.Commodity,out t)){
   if(r.Payment!=0||r.Option!=255||r.Period!=0&&r.Period!=255)throw new InvalidDataException("Name card selection");
   return new Offline70CashPurchaseFG45.Offer{Commodity=r.Commodity,Category=5,Item=t.Item,Cost=t.Price,Special=2,Days=t.Days,Rewards=new uint[][]{new uint[]{t.Item,1}}};
  }
  return null;
 }
 internal static bool Valid(Offline70CashPurchaseFG45.Offer o){
  if(o==null)return false;if(o.Special==8)return Offline70SkinCashFG152.Valid(o);if(o.Special==9)return Offline70AvatarCashFG210.Valid(o);if(o.Special==10||o.Special==11)return Offline70CashPackageFG272.Valid(o);if(Offline70FeedbackFG129.Valid(o))return true;
  if(o.Special==0){Offline70CashPurchaseFG45.Offer known;return Offline70CashCatalogFG80.Offers.TryGetValue(o.Commodity,out known)&&Object.ReferenceEquals(o,known);}
  if(o.Once||o.Rewards==null||o.Rewards.Length!=1||o.Rewards[0].Length!=2||o.Rewards[0][0]!=o.Item||o.Rewards[0][1]!=1)return false;
  if(o.Special==1){Offline70FeedbackCatalogFG108.Avatar a;return Offline70FeedbackCatalogFG108.Avatars.TryGetValue(o.Commodity,out a)&&o.Category==0&&o.Item==a.Item&&o.Cost==0&&o.Voucher==2681594&&o.Days==0&&(a.Options==0?o.Ability==0:o.Ability<a.Options);}
  if(o.Special==2){Offline70FeedbackCatalogFG108.Tag t;return Offline70FeedbackCatalogFG108.Tags.TryGetValue(o.Commodity,out t)&&o.Category==5&&o.Item==t.Item&&o.Cost==t.Price&&o.Days==t.Days&&o.Voucher==0&&o.Ability==0;}
  return false;
 }
 internal static Offline70EconomyState Purchase(string path,string receipt,Offline70CashPurchaseFG45.Offer[] offers,out bool duplicate){
  if(offers==null||offers.Length<1||offers.Length>36)throw new InvalidDataException("Special cash selection count");
  StringBuilder payload=new StringBuilder();ulong cost=0;uint now=Offline70PotFG93.UnixNow();
  foreach(Offline70CashPurchaseFG45.Offer o in offers){if(!Valid(o))throw new InvalidDataException("Unregistered special cash offer");cost+=o.Cost;payload.Append('|').Append(o.Commodity).Append(':').Append(o.Special).Append(':').Append(o.Ability).Append(':').Append(o.Days);}
  if(cost>Offline70Economy.WalletLimit)throw new InvalidDataException("Special cash total range");
  return Offline70Economy.Apply(path,receipt,"cash-selection",payload.ToString(),delegate(Offline70EconomyState s){
   if(s.OfflineCash<cost)throw new InvalidDataException("Insufficient cash for entire selection");
   foreach(Offline70CashPurchaseFG45.Offer o in offers){
    if(o.Special==8){Offline70SkinCashFG152.Grant(s,o,now);}else if(o.Special==9){Offline70AvatarCashFG210.Grant(s,o,now);}else if(o.Special==10||o.Special==11){Offline70CashPackageFG272.Grant(s,o,now);}else if(o.Special>=3){Offline70FeedbackFG129.Grant(s,o,now);}
    else if(o.Special==1){
     Offline70EconomyItemDefinition d=Offline70Economy.Definition(o.Item);if(d.Kind!=3||d.Job!=s.Job)throw new InvalidDataException("Rare avatar character job");
     Offline70Economy.RemoveAssetItem(s,o.Voucher,1);HashSet<ushort> occupied=new HashSet<ushort>();foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==1)occupied.Add(e.Slot);
     Offline70Economy.AddAssetItem(s,o.Item,1);Offline70EconomyEntry added=null;foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==1&&e.ItemId==o.Item&&!occupied.Contains(e.Slot))added=e;
     if(added==null)throw new InvalidDataException("Rare avatar instance not inserted");added.Ability=o.Ability;added.ExpiresUnix=0;
    }else if(o.Special==2){
     uint begin=s.NameTagItemFG108==o.Item&&s.NameTagExpiryFG108>now?s.NameTagExpiryFG108:now;
     s.NameTagExpiryFG108=checked(begin+o.Days*86400u);s.NameTagItemFG108=o.Item;
    }else{
     if(o.Once&&s.CashOncePurchases.Contains(o.Commodity))throw new InvalidDataException("Cash selection lifetime limit");
     foreach(uint[] r in o.Rewards)if(r[0]==1){if(r[1]>Offline70Economy.WalletLimit-s.RevivalCoins)throw new InvalidDataException("Cash revival capacity");s.RevivalCoins+=r[1];}else Offline70Economy.AddPurchasedItem(s,r[0],r[1]);
     if(o.Once)s.CashOncePurchases.Add(o.Commodity);
    }
   }
   s.OfflineCash-=(uint)cost;
  },out duplicate);
 }
 internal static void Expire(string path){Offline70EconomyState s=Offline70Economy.Load(path);uint now=Offline70PotFG93.UnixNow();if(s.NameTagItemFG108==0||s.NameTagExpiryFG108>now)return;bool duplicate;Offline70Economy.Apply(path,"name-card-expire-"+s.NameTagItemFG108+"-"+s.NameTagExpiryFG108,"name-card-expire",s.NameTagItemFG108+":"+s.NameTagExpiryFG108,delegate(Offline70EconomyState next){if(next.NameTagItemFG108!=s.NameTagItemFG108||next.NameTagExpiryFG108!=s.NameTagExpiryFG108)throw new InvalidDataException("Name card expiration changed");next.NameTagItemFG108=0;next.NameTagExpiryFG108=0;},out duplicate);}
 // 18/22 were measured on the 4-byte-name record; the record is name-relative, so derive the
 // name-card fields from the u32 name length at +2 (18-(nameBytes-4), 22-(nameBytes-4)).
 internal static byte[] Appearance(byte[] record,Offline70EconomyState s){if(record==null||record.Length<26)throw new InvalidDataException("Name card appearance record");int nameBytes=BitConverter.ToInt32(record,2);if(nameBytes<1||nameBytes>29||record.Length<18+nameBytes)throw new InvalidDataException("Name card appearance record");BitConverter.GetBytes(s.NameTagItemFG108).CopyTo(record,14+nameBytes);BitConverter.GetBytes(s.NameTagExpiryFG108).CopyTo(record,18+nameBytes);return record;}
}
