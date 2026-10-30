using System;using System.Collections.Generic;using System.IO;using System.Xml;using System.Xml.Serialization;
// Commodity rows generated from the unmodified current CN cerashop.etc [avatar] section by
// local-run/pvf/gen_avatar_cash_catalog.py. Per-item price is the [avatar type select] tier of
// the item's own .equ script (coat 450/990/2970, belt 300/660/1980, ...), Options is the
// [avatar select ability] row count, and Period is the shop usage-period variant the client
// echoes in the op67 request: 1=7 days, 2=30 days, 3=permanent -- the same convention the
// FG152 skin table and the FG108 rare-avatar path already capture. CERA payment via payment=0;
// the rare-avatar exchange page pays with one Starlight Unicorn per piece via payment=1.
public class Offline70AvatarCashRowFG210 {public uint Commodity;public uint Item;public byte Period;public uint Price;public byte Options;}
public class Offline70AvatarCashCatalogFG210 {public int Format;public List<Offline70AvatarCashRowFG210> Rows;}
class Offline70AvatarCashFG210 {
 internal static readonly Dictionary<uint,Offline70AvatarCashRowFG210> Rows=new Dictionary<uint,Offline70AvatarCashRowFG210>();
 internal static readonly Dictionary<uint,Offline70AvatarCashRowFG210> Items=new Dictionary<uint,Offline70AvatarCashRowFG210>();
 // The mall rare-avatar exchange page buys each class's Chaos set with payment=1: the live
 // request for 563007 (2026-10-25 op67) carries payment=1 period=3 option=0 and the client popup
 // names the price as one Starlight Unicorn -- creature/unicorn.cre, pet item 63092. A holder of
 // the rare-avatar voucher 2681594 exchanges with that instead (the client binary wires both ids
 // into the same feature, voucher referenced first; live evidence 2026-10-26: a voucher holder's
 // exchange request is sent and accepted). Every class family carries the same sextet row shape
 // in the same cerashop [avatar] section, so the whole set resolves through this exchange class.
 // The swordman family 166024-166031 is deliberately absent: it keeps the older FG108
 // voucher-only path (Special=1), which must stay byte-compatible. The .equ cera tiers stay
 // reachable through the payment=0 path below.
 internal static readonly uint[] ExchangeCommodities={
  216024,216025,216026,216027,216028,216029,216030,216031, // fighter (female)
  266024,266025,266026,266027,266028,266029,266030,266031, // gunner
  316024,316025,316026,316027,316028,316029,316030,316031, // mage
  366024,366025,366026,366027,366028,366029,366030,366031, // priest
  466016,466017,466018,466019,466020,466021,466022,466023, // thief
  563000,563001,563002,563003,563004,563005,563006,563007, // at mage
  516008,516009,516010,516011,516012,516013,516014,516015}; // at fighter: the male fighter's sky-set exchange family (2026-10-30 report - the purchase resolved to nothing while every other class worked; PVF puts his pieces under character/fighter/at_avatar)
 internal const uint ExchangeVoucher=63092;
 // Offer.Voucher carries ExchangeVoucher as the exchange-class marker; the piece's actual price
 // is the first of these currencies the player owns, checked in this order.
 internal static readonly uint[] ExchangePrices={2681594,63092};
 internal static bool IsExchange(uint commodity){foreach(uint c in ExchangeCommodities)if(c==commodity)return true;return false;}
 internal static uint Days(byte period){return period==1?7u:period==2?30u:0u;}
 internal static void Load(string path){
  if(new FileInfo(path).Length>16777216)throw new InvalidDataException("Avatar cash catalog size");
  Offline70AvatarCashCatalogFG210 c;
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;c=(Offline70AvatarCashCatalogFG210)new XmlSerializer(typeof(Offline70AvatarCashCatalogFG210)).Deserialize(r);}
  if(c.Format!=1||c.Rows==null||c.Rows.Count<1)throw new InvalidDataException("Avatar cash catalog profile");
  Rows.Clear();Items.Clear();
  foreach(Offline70AvatarCashRowFG210 row in c.Rows){
   if(row==null||row.Commodity==0||row.Item==0||row.Period<1||row.Period>3||row.Price==0||row.Price>Offline70Economy.WalletLimit)throw new InvalidDataException("Avatar cash row profile");
   if(Rows.ContainsKey(row.Commodity))throw new InvalidDataException("Duplicate avatar cash commodity");
   Rows.Add(row.Commodity,row);
   if(row.Options!=0){Offline70AvatarCashRowFG210 prior;if(Items.TryGetValue(row.Item,out prior)&&prior.Options!=row.Options)throw new InvalidDataException("Avatar cash item options conflict");Items[row.Item]=row;}
   Offline70Economy.Definition(row.Item);
  }
 }
 internal static bool Known(uint item){return Items.ContainsKey(item);}
 internal static bool AbilityValid(Offline70EconomyEntry e){Offline70AvatarCashRowFG210 row;return Items.TryGetValue(e.ItemId,out row)?(row.Options==0?e.Ability==0:e.Ability<row.Options):e.Ability<=4;}
 internal static Offline70CashPurchaseFG45.Offer Resolve(Offline70CashPurchaseFG45.Request r){
  Offline70AvatarCashRowFG210 row;if(r==null||!Rows.TryGetValue(r.Commodity,out row))return null;
  if(r.Period!=row.Period||(row.Options==0?r.Option!=255:r.Option>=row.Options))throw new InvalidDataException("Avatar cash payment, period or ability selection");
  if(r.Payment==1){
   // Not one of the unicorn pieces: fall back to the FG108 voucher exchange (commodities
   // 166024-166031 are in this catalog too) or a clean unimplemented reject downstream.
   if(!IsExchange(r.Commodity))return null;
   return new Offline70CashPurchaseFG45.Offer{Commodity=r.Commodity,Category=0,Item=row.Item,Cost=0,Days=0,Special=9,Voucher=ExchangeVoucher,Ability=row.Options==0?(ushort)0:r.Option,Rewards=new uint[][]{new uint[]{row.Item,1}}};
  }
  if(r.Payment!=0)throw new InvalidDataException("Avatar cash payment, period or ability selection");
  return new Offline70CashPurchaseFG45.Offer{Commodity=r.Commodity,Category=0,Item=row.Item,Cost=row.Price,Days=Days(row.Period),Special=9,Ability=row.Options==0?(ushort)0:r.Option,Rewards=new uint[][]{new uint[]{row.Item,1}}};
 }
 internal static bool Valid(Offline70CashPurchaseFG45.Offer o){
  Offline70AvatarCashRowFG210 row;if(o==null||o.Special!=9||!Rows.TryGetValue(o.Commodity,out row))return false;
  if(IsExchange(o.Commodity)&&o.Voucher==ExchangeVoucher){if(o.Cost!=0)return false;}
  else{if(o.Voucher!=0||o.Cost!=row.Price)return false;}
  return o.Category==0&&o.Item==row.Item&&o.Days==Days(row.Period)&&!o.Once&&o.Rewards!=null&&o.Rewards.Length==1&&o.Rewards[0].Length==2&&o.Rewards[0][0]==row.Item&&o.Rewards[0][1]==1&&(row.Options==0?o.Ability==0:o.Ability<row.Options);
 }
 internal static void Grant(Offline70EconomyState s,Offline70CashPurchaseFG45.Offer o,uint now){
  if(!Valid(o))throw new InvalidDataException("Unregistered avatar cash offer");
  Offline70EconomyItemDefinition d=Offline70Economy.Definition(o.Item);
  if(d.Kind!=3||!((d.Job==255||d.Job==s.Job)&&(d.JobMask==0||(d.JobMask&(1u<<s.Job))!=0)))throw new InvalidDataException("Avatar cash character job");
  HashSet<ushort> occupied=new HashSet<ushort>();foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==1)occupied.Add(e.Slot);
  Offline70Economy.AddAssetItem(s,o.Item,1);Offline70EconomyEntry added=null;
  foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==1&&e.ItemId==o.Item&&!occupied.Contains(e.Slot))added=e;
  if(added==null)throw new InvalidDataException("Avatar cash instance not inserted");
  added.Ability=o.Ability;added.ExpiresUnix=o.Days==0?0:checked(now+o.Days*86400u);
  if(o.Voucher!=0){
   // The exchange page takes either currency: the rare-avatar voucher (2681594, the dedicated
   // item -- the client binary wires it and the unicorn into the same feature, voucher first)
   // or the Starlight Unicorn pet (63092, the popup's display item). Nothing owned -> the piece
   // is still granted (the currencies are unobtainable here); the service note's voucher= field
   // marks the exchange class either way.
   foreach(uint price in ExchangePrices){
    ulong owned=0;foreach(Offline70EconomyEntry e in s.Inventory)if(e.List!=3&&e.ItemId==price)owned+=e.Quantity;
    if(owned>0){Offline70Economy.RemoveAssetItem(s,price,1);break;}
   }
  }
 }
}
