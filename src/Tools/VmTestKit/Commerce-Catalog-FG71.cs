using System;using System.IO;using System.Collections.Generic;using System.Xml;using System.Xml.Serialization;
public class Offline70CommerceItemFG71 {public uint ItemId,Shop,BuyPrice,SaleValue,RepairValue;public ushort SaleRate;public byte Grade;public bool CanSell;}
// FG396: the [need material] exchange shelves (黑色瘟疫套装/幽灵列车石碑/深渊票链/黄龙曲玉) carry no
// gold [price] - the PVF prices them as `<currency item id> <count per one>` inside the item's own
// script, so an offer may charge an item currency instead of gold. CurrencyItem==0 keeps gold.
public class Offline70CommerceOfferFG79 {public uint Shop,ItemId,Price;public uint CurrencyItem;public uint CurrencyCount;}
public class Offline70CommerceShopFG79 {public uint Shop;public int Npc;}
public class Offline70CommerceCatalogDocumentFG71 {public int Format=1;public string PvfSha256;public List<Offline70CommerceItemFG71> Items=new List<Offline70CommerceItemFG71>();public List<Offline70CommerceOfferFG79> Offers=new List<Offline70CommerceOfferFG79>();public List<Offline70CommerceShopFG79> Shops=new List<Offline70CommerceShopFG79>();}
// Separate prices from behavior to preserve already verified item effects.
class Offline70CommerceFG71 {
 static Dictionary<uint,Offline70CommerceItemFG71> profiles=BasicProfiles();
 static Dictionary<string,uint> offerPrices=new Dictionary<string,uint>();static Dictionary<uint,int> shopNpcs=new Dictionary<uint,int>();
 static Dictionary<string,uint[]> offerExchanges=new Dictionary<string,uint[]>();
 static Dictionary<uint,Offline70CommerceItemFG71> BasicProfiles(){
  Dictionary<uint,Offline70CommerceItemFG71> d=new Dictionary<uint,Offline70CommerceItemFG71>();
  foreach(uint id in new uint[]{1106,1112})d.Add(id,new Offline70CommerceItemFG71{ItemId=id,Shop=2,BuyPrice=100,SaleValue=100,SaleRate=200,CanSell=true});
  d.Add(3015,new Offline70CommerceItemFG71{ItemId=3015,SaleValue=120,SaleRate=200,CanSell=true});
  d.Add(16400,new Offline70CommerceItemFG71{ItemId=16400,SaleValue=160,SaleRate=200,CanSell=true,RepairValue=160,Grade=1});return d;
 }
 internal static void Load(string path){
  if(new FileInfo(path).Length>8388608)throw new InvalidDataException("Commerce catalog size");Offline70CommerceCatalogDocumentFG71 document;
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;document=(Offline70CommerceCatalogDocumentFG71)new XmlSerializer(typeof(Offline70CommerceCatalogDocumentFG71)).Deserialize(r);}
  if(document.Format!=1||document.PvfSha256!=Offline70QuestCatalogFG.CurrentPvf||document.Items==null||document.Items.Count>10000)throw new InvalidDataException("Commerce catalog profile");
  Dictionary<uint,Offline70CommerceItemFG71> loaded=new Dictionary<uint,Offline70CommerceItemFG71>();
  foreach(Offline70CommerceItemFG71 p in document.Items){
   if(p==null||p.ItemId==0||p.ItemId>Int32.MaxValue||p.Shop!=0&&p.Shop!=2||p.BuyPrice>Offline70Economy.WalletLimit||p.SaleValue>Offline70Economy.WalletLimit||p.RepairValue>Offline70Economy.WalletLimit||p.SaleRate>1000||p.Grade>100||p.Shop==2&&p.BuyPrice==0||loaded.ContainsKey(p.ItemId))throw new InvalidDataException("Commerce item bounds");
   Offline70EconomyItemDefinition d=Offline70Economy.Definition(p.ItemId);
   if(p.Shop!=0&&d.Kind!=1&&d.Kind!=2&&d.Kind!=4&&d.Kind!=8&&d.Kind!=9&&d.Kind!=10||p.CanSell&&d.Kind!=1&&d.Kind!=2&&d.Kind!=4&&d.Kind!=8&&d.Kind!=9&&d.Kind!=10||p.RepairValue!=0&&(d.Kind!=4||d.MaxDurability==0))throw new InvalidDataException("Commerce item category");/* FG381: Kind 8 pet equipment joins the sellable/offerable set - Oran and the Danjin shops stock pet gear (2747155..) and FG352/FG367 already sell and delete it; buying it could hardly stay forbidden. */loaded.Add(p.ItemId,p);
  }
  foreach(uint id in new uint[]{1106,1112,3015,16400})if(!loaded.ContainsKey(id))throw new InvalidDataException("Missing verified commerce baseline");
  if(document.Offers==null||document.Shops==null||document.Offers.Count>10000||document.Shops.Count>1000)throw new InvalidDataException("NPC offer catalog bounds");Dictionary<string,uint> offers=new Dictionary<string,uint>();Dictionary<uint,int> merchants=new Dictionary<uint,int>();Dictionary<string,uint[]> exchanges=new Dictionary<string,uint[]>();
  foreach(Offline70CommerceShopFG79 shop in document.Shops){if(shop==null||shop.Shop==0||shop.Shop>2000||shop.Npc<=0||shop.Npc>2000||merchants.ContainsKey(shop.Shop))throw new InvalidDataException("NPC shop location bounds");merchants.Add(shop.Shop,shop.Npc);}
  foreach(Offline70CommerceOfferFG79 offer in document.Offers){
   if(offer==null||!merchants.ContainsKey(offer.Shop)||!loaded.ContainsKey(offer.ItemId))throw new InvalidDataException("NPC offer fields");
   if(offer.CurrencyItem!=0){if(offer.CurrencyCount==0||offer.CurrencyCount>1000000||offer.CurrencyItem>Int32.MaxValue||offer.Price!=0)throw new InvalidDataException("NPC offer currency fields");Offline70Economy.Definition(offer.CurrencyItem);}
   else if(offer.Price==0||offer.Price>Offline70Economy.WalletLimit)throw new InvalidDataException("NPC offer fields");
   Offline70EconomyItemDefinition d=Offline70Economy.Definition(offer.ItemId);string key=offer.Shop+":"+offer.ItemId;
   // FG396: Kind 5 joins the offerable set - 歌兰蒂斯' 破魔石 and 素喃's 入选资格 are [quest]-type
   // stackables the original shelves sell; selling them stays refused (CanSell=false, quest page).
   if(d.Kind!=1&&d.Kind!=2&&d.Kind!=4&&d.Kind!=5&&d.Kind!=8&&d.Kind!=9&&d.Kind!=10||offers.ContainsKey(key))throw new InvalidDataException("NPC offer category or duplicate");offers.Add(key,offer.Price);
   if(offer.CurrencyItem!=0)exchanges.Add(key,new uint[]{offer.CurrencyItem,offer.CurrencyCount});
  }
  // Older catalogs retain the original verified Linus offers.
  foreach(Offline70CommerceItemFG71 p in loaded.Values)if(p.Shop==2){string key="2:"+p.ItemId;uint value;if(offers.TryGetValue(key,out value)){if(value!=p.BuyPrice)throw new InvalidDataException("Linus offer price changed");}else offers.Add(key,p.BuyPrice);}
  if(!merchants.ContainsKey(2))merchants.Add(2,1);profiles=loaded;offerPrices=offers;shopNpcs=merchants;offerExchanges=exchanges;
 }
 internal static Offline70CommerceItemFG71 Get(uint id){Offline70CommerceItemFG71 p;if(!profiles.TryGetValue(id,out p))throw new InvalidDataException("Commerce item not registered");return p;}
 internal static uint BuyCost(uint id,uint count){return BuyCost(2,id,count);}
 internal static uint OfferPrice(uint shop,uint id){uint price;if(offerPrices.TryGetValue(shop+":"+id,out price))return price;Offline70CommerceItemFG71 p=Get(id);if(shop==2&&p.Shop==2)return p.BuyPrice;throw new InvalidDataException("Unregistered NPC offer");}
 internal static uint BuyCost(uint shop,uint id,uint count){Offline70EconomyItemDefinition d=Offline70Economy.Definition(id);uint price=OfferPrice(shop,id);if(count==0||count>(d.Kind==4?32u:1000u))throw new InvalidDataException("NPC quantity bounds");ulong total=(ulong)price*count;if(total>Offline70Economy.WalletLimit)throw new InvalidDataException("NPC purchase cost overflow");return (uint)total;}
 // FG396: what one purchase of `count` of `id` at `shop` actually costs. Exchange rows return the
 // item currency and its total amount with goldCost=0; gold rows return the wallet charge.
 internal static void OfferCost(uint shop,uint id,uint count,out uint currencyItem,out uint currencyAmount,out uint goldCost){
  Offline70EconomyItemDefinition d=Offline70Economy.Definition(id);
  if(count==0||count>(d.Kind==4?32u:1000u))throw new InvalidDataException("NPC quantity bounds");
  uint[] exchange;if(offerExchanges.TryGetValue(shop+":"+id,out exchange)){currencyItem=exchange[0];currencyAmount=checked(exchange[1]*count);goldCost=0;return;}
  currencyItem=0;currencyAmount=0;goldCost=BuyCost(shop,id,count);
 }
 internal static bool ShopAvailable(int town,int area,uint shop){int npc;return shopNpcs.TryGetValue(shop,out npc)&&Offline70TownContentFG75.Npc(town,area,npc);}
 internal static bool TradeAvailable(int town,int area){foreach(int npc in shopNpcs.Values)if(Offline70TownContentFG75.Npc(town,area,npc))return true;return false;}
 internal static uint SalePrice(Offline70EconomyEntry e){
  // CN70 semantics: every consumable/material/equipment is merchant-sellable; items with
  // no recorded profile (never stocked, no PVF [price]/[value]) sell for 0 gold.  [medal]
  // marks alchemy medals and does not block sale.
  if(e==null)throw new InvalidDataException("NPC sale ownership");Offline70EconomyItemDefinition d=Offline70Economy.Definition(e.ItemId);
  // Kind 8 (pet equipment) sells from the pet window list 7, unregistered profiles at 0 gold like any other item.
  // Kind 9 is the 装扮->徽章 page and Kind 10 the 副职业 page; both are ordinary merchant stackables.
  if(e.List!=0&&e.List!=7||d.Kind!=1&&d.Kind!=2&&d.Kind!=4&&d.Kind!=8&&d.Kind!=9&&d.Kind!=10)throw new InvalidDataException("NPC sale profile unavailable");Offline70CommerceItemFG71 p;profiles.TryGetValue(e.ItemId,out p);ulong unit=p==null?0:(ulong)p.SaleValue*p.SaleRate/1000;
  if(d.Kind==4){if(e.Quantity!=1||e.Durability>d.MaxDurability)throw new InvalidDataException("NPC equipment sale record");if(d.MaxDurability>0)unit=unit*e.Durability/d.MaxDurability;}
  if(d.Kind==8&&e.List!=7)throw new InvalidDataException("NPC pet equipment window");
  if(unit>Offline70Economy.WalletLimit)throw new InvalidDataException("NPC sale price overflow");return (uint)unit;
 }
 internal static uint RepairCost(Offline70EconomyEntry e){
  if(e==null)throw new InvalidDataException("Repair ownership");Offline70CommerceItemFG71 p=Get(e.ItemId);Offline70EconomyItemDefinition d=Offline70Economy.Definition(e.ItemId);
  if(d.Kind!=4||p.RepairValue==0||d.MaxDurability==0||e.Quantity!=1||e.Durability>d.MaxDurability)throw new InvalidDataException("Ordinary repair profile unavailable");
  // TW898C8FC and CN70 quote truncate, retaining the early integer /10.
  ulong basis=(ulong)p.RepairValue*(uint)(p.Grade+5)/10;ulong cost=basis*(uint)(d.MaxDurability-e.Durability)/((uint)d.MaxDurability*10);
  if(cost>Offline70Economy.WalletLimit)throw new InvalidDataException("Repair cost overflow");return (uint)cost;
 }
}
