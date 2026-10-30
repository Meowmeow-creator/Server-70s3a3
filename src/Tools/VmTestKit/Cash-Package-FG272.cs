using System;using System.IO;using System.Collections.Generic;
// cerashop.etc package commodities measured on the unmodified CN PVF.
//
// [package] 101117 = 王者成长礼包, item 2682620, 1980 CERA. The item script
// stackable/cash/chn_20130108_New_grow_up_PackageAB/chn_2013_Grow_up_Start_PackageB.stk
// carries [package data include usable period] 2681943:1:0, 1:10:0, 8:50:0, 45:1:0, 33:1:0.
// 101116 (勇士成长礼包) already takes the same direct-grant path in FG80, and the live
// store accepted it at 23:27:54, so this package grants its contents directly instead of
// inserting the [cera package] box: op47 has no box-open handler and would strand it.
//
// [charac premium package] 100625 = 魔王之契约礼包 (60天), item 2682006, 3880 CERA, 60 days.
// Its chn_60days_pack.stk [package data] names the eight service markers
// 2681934/2681931/2681932/2681933/2681929/2681930/2682205/2682206, and the 7-day rows of
// [selectable character premium] map those same items to premium types 0..7 (the ninth row,
// 2682377, is the daily-quest service and is not part of this package). The package therefore
// activates types 0..7 for 60 days in the premium list that op66 already carries; the marker
// items themselves are not inserted (same reason as above).
class Offline70CashPackageFG272 {
 internal const byte ItemPackage=10,PremiumPackage=11;
 internal const uint KingCommodity=101117,MoonCommodity=100625;
 internal static readonly Offline70CashPurchaseFG45.Offer King;
 internal static readonly Offline70CashPurchaseFG45.Offer Moon;
 internal static readonly Dictionary<uint,Offline70CashPurchaseFG45.Offer> Offers=new Dictionary<uint,Offline70CashPurchaseFG45.Offer>();
 internal static readonly uint[] ServiceItems={2681934,2681931,2681932,2681933,2681929,2681930,2682205,2682206};
 static Offline70CashPackageFG272(){
  King=new Offline70CashPurchaseFG45.Offer{Commodity=KingCommodity,Category=4,Item=2682620,Cost=1980,Days=7,Special=ItemPackage,Once=true,
   Rewards=new uint[][]{new uint[]{2681943,1},new uint[]{1,10},new uint[]{8,50},new uint[]{45,1},new uint[]{33,1}}};
  Moon=new Offline70CashPurchaseFG45.Offer{Commodity=MoonCommodity,Category=4,Item=2682006,Cost=3880,Days=60,Special=PremiumPackage,
   Rewards=new uint[][]{new uint[]{2681934,1},new uint[]{2681931,1},new uint[]{2681932,1},new uint[]{2681933,1},new uint[]{2681929,1},new uint[]{2681930,1},new uint[]{2682205,1},new uint[]{2682206,1}}};
  Offers.Add(King.Commodity,King);Offers.Add(Moon.Commodity,Moon);
  // The remaining single-item shop rows that have no dedicated mechanism: [premium] 关联券/更名卡,
  // [item] 灵魂气息/金库工具/秘药 and [creature] 奥兰遗失的袋子 (its 2682571 booster opening is
  // already implemented by FG110). Each grants its own item; the link/rename/buff systems those
  // items unlock stay unimplemented, same trade-off as the growth package tickets.
  AddItem(100543,2681935,1,880,3);AddItem(100544,2681936,1,1380,3);AddItem(100551,2681943,1,1180,3);
  AddItem(100552,2681944,1,1880,3);AddItem(100902,2682290,1,8800,3);
  AddItem(100782,2682170,1,2800,2);AddItem(100534,2681921,1,2000,2);
  AddItem(101100,2682616,1,580,2);AddItem(101101,2682617,1,280,2);
  AddItem(101076,2682571,1,480,3);AddItem(101077,2682571,5,2380,3);
 }
 static void AddItem(uint commodity,uint item,uint count,uint price,byte category){
  Offline70CashPurchaseFG45.Offer offer=new Offline70CashPurchaseFG45.Offer{Commodity=commodity,Category=category,Item=item,Cost=price,Special=ItemPackage,Once=false,
   Rewards=new uint[][]{new uint[]{item,count}}};
  Offers.Add(commodity,offer);
 }
 // 33/45/2681943 are [etc] stackables granted by the growth package. 2681943's script
 // declares [minimum level] 60; the grant path does not gate on level, the metadata keeps
 // the inventory/use profile honest for the level-70 live character.
 internal static void Register(){
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=33,Kind=1,StackLimit=1000,MinimumLevel=1,CooldownMilliseconds=0,CooldownGroup=0});
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=45,Kind=1,StackLimit=1000,MinimumLevel=1,CooldownMilliseconds=0,CooldownGroup=0});
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2681943,Kind=1,StackLimit=1000,MinimumLevel=60,CooldownMilliseconds=0,CooldownGroup=0});
  // link_character7/15.stk and link_account15.stk: [minimum level] 60.
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2681935,Kind=1,StackLimit=1000,MinimumLevel=60,CooldownMilliseconds=0,CooldownGroup=0});
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2681936,Kind=1,StackLimit=1000,MinimumLevel=60,CooldownMilliseconds=0,CooldownGroup=0});
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2681944,Kind=1,StackLimit=1000,MinimumLevel=60,CooldownMilliseconds=0,CooldownGroup=0});
  // chn_character_rename.stk: [minimum level] 30.
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682290,Kind=1,StackLimit=1000,MinimumLevel=30,CooldownMilliseconds=0,CooldownGroup=0});
  // chn_rare_breath_spirit.stk: [stack limit] 10.
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682170,Kind=1,StackLimit=10,MinimumLevel=1,CooldownMilliseconds=0,CooldownGroup=0});
  // account_cargo1.stk.
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2681921,Kind=1,StackLimit=1000,MinimumLevel=1,CooldownMilliseconds=0,CooldownGroup=0});
  // 8 个魔王之契约服务标记道具：.stk 都是 [etc] / [minimum level] 1，礼包按 60 天期效发放。
  foreach(uint item in ServiceItems)Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=item,Kind=1,StackLimit=1000,MinimumLevel=1,CooldownMilliseconds=0,CooldownGroup=0});
  // Red Nostrum of Growth: [stack limit] 3, [cool time] 3600000 in [cooltime group] 99.
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682616,Kind=1,StackLimit=3,MinimumLevel=60,CooldownMilliseconds=3600000,CooldownGroup=99});
  Offline70Economy.RegisterMetadata(new Offline70EconomyItemDefinition{ItemId=2682617,Kind=1,StackLimit=3,MinimumLevel=1,CooldownMilliseconds=3600000,CooldownGroup=99});
 }
 // The package has no selectable variants: the commodity fixes both price and contents, so
 // the client's option/period bytes are ignored. CERA (payment 0) is still required.
 internal static Offline70CashPurchaseFG45.Offer Resolve(Offline70CashPurchaseFG45.Request r){
  Offline70CashPurchaseFG45.Offer offer;if(r==null||!Offers.TryGetValue(r.Commodity,out offer))return null;
  if(r.Payment!=0)throw new InvalidDataException("Cash package payment");
  return offer;
 }
 internal static bool Valid(Offline70CashPurchaseFG45.Offer o){
  Offline70CashPurchaseFG45.Offer known;return o!=null&&Offers.TryGetValue(o.Commodity,out known)&&Object.ReferenceEquals(o,known);
 }
 internal static void Grant(Offline70EconomyState s,Offline70CashPurchaseFG45.Offer o,uint now){
  if(!Valid(o))throw new InvalidDataException("Unregistered cash package offer");
  if(o.Special==ItemPackage){
   if(o.Once&&s.CashOncePurchases.Contains(o.Commodity))throw new InvalidDataException("Cash package lifetime limit");
   foreach(uint[] reward in o.Rewards){
    // 霸王/达人之契约票 (33/45) 是 [etc] 被动票据：客户端对它们不发 op47 使用包（官方流程在
    // 购买时就地生效），所以这里不再落包，改成直接延长 22/27 两条契约（见下）。
    if(o.Commodity==KingCommodity&&(reward[0]==33||reward[0]==45))continue;
    if(reward[0]==1){if(reward[1]>Offline70Economy.WalletLimit-s.RevivalCoins)throw new InvalidDataException("Cash package revival capacity");s.RevivalCoins+=reward[1];}
    else Offline70Economy.AddPurchasedItem(s,reward[0],reward[1]);
   }
   if(o.Commodity==KingCommodity)ApplyContract(s,now,o.Days);
   if(o.Once)s.CashOncePurchases.Add(o.Commodity);
  }else if(o.Special==PremiumPackage){
   // 魔王之契约的服务在客户端"期限道具信息栏"里是按限时道具展示的（服务名来自 .stk 内联
   // [name]，不在任何 .str 表），所以 60 天礼包直接发 8 个带到期时间的服务道具；0..7
   // premium 类型不是客户端认的魔王服务（实测只会在右上角图标栏出现旧契约名）。
   // 和霸王契约一样：购买即生效、不入背包。真正的魔王之契约状态包尚未实现，这里只记天数台账，
   // 客户端页签的清零/点亮等协议就绪后从这里补记 entitlement。
   s.ServiceDaysFG272=checked(s.ServiceDaysFG272+o.Days);
  }else throw new InvalidDataException("Cash package mode");
 }
 internal static readonly byte[] ContractKinds={22,27};
 static void ApplyContract(Offline70EconomyState s,uint now,uint days){
  foreach(byte kind in ContractKinds){
   Offline70PremiumFG129 found=null;foreach(Offline70PremiumFG129 p in s.PremiumsFG129)if(p.Kind==kind)found=p;
   if(found==null){found=new Offline70PremiumFG129{Kind=kind};s.PremiumsFG129.Add(found);}
   found.Expires=checked(Math.Max(now,found.Expires)+days*86400u);
  }
 }
 // 2026-10-26 01:51 的两单礼包把 33/45 当物品发了出去，客户端对 [etc] 票据不发 op47，等于死物。
 // 进世界时把它们换成 7 天契约并从背包移除（回执幂等，一人一次）；2681943 关联券对应的
 // 角色关联系统（op313-315 + 专门通知包）服务端未实现，先保留物品不动。
 internal static readonly uint[] TicketItems={33,45};
 internal static Offline70EconomyState MigrateTickets(string path,Offline70EconomyState current){
  bool pending=false;foreach(Offline70EconomyEntry e in current.Inventory)if(Array.IndexOf(TicketItems,e.ItemId)>=0)pending=true;
  if(!pending)return current;
  bool duplicate;
  return Offline70Economy.Apply(path,"fg272-ticket-migrate","fg272-ticket-migrate","33:45:7d",delegate(Offline70EconomyState s){
   uint now=Offline70PotFG93.UnixNow();
   foreach(uint item in TicketItems){
    Offline70EconomyEntry owned=null;foreach(Offline70EconomyEntry e in s.Inventory)if(e.ItemId==item){owned=e;break;}
    if(owned==null)continue;
    if(owned.Quantity==1)s.Inventory.Remove(owned);else owned.Quantity--;
    ApplyContract(s,now,7);
   }
  },out duplicate);
 }
 // 已经把 60 天礼包买成 0..7 premium 的角色：把那段 entitlement 换回 8 个限时服务道具
 // （到期时间取原 premium 的最晚值，至少 60 天），并清掉错误的 0..7。
 // SHA256("cash-selection:|100625:11:0:60") —— op67 购买 100625 的回执指纹（FG108 的 payload=
 // "|commodity:special:ability:days"）。历史购买在 ServiceDaysFG272 字段存在之前，只能靠回执次数
 // 反推已购天数；之后的购买直接累加 ServiceDaysFG272，两边保持一致，迁移不会重复补。
 internal const string MoonReceiptHash="a22bc398e3317c945d1bc4f0a2636ebd76dfbe930bc4866c064ad801413d1e1b";
 internal static Offline70EconomyState MigrateServices(string path,Offline70EconomyState current){
  uint purchased=0;
  foreach(Offline70EconomyReceipt r in current.Receipts)if(r!=null&&r.Kind=="cash-selection"&&r.PayloadSha256==MoonReceiptHash)purchased+=60;
  uint legacyExpiry=0;bool legacy=false;
  foreach(Offline70PremiumFG129 p in current.PremiumsFG129)if(p.Kind<=7){legacy=true;legacyExpiry=Math.Max(legacyExpiry,p.Expires);}
  bool stray=false;foreach(uint item in ServiceItems)foreach(Offline70EconomyEntry e in current.Inventory)if(e.ItemId==item){stray=true;break;}
  if(purchased<=current.ServiceDaysFG272&&!legacy&&!stray)return current;
  bool duplicate;
  return Offline70Economy.Apply(path,"fg272-service-sync2","fg272-service-sync2",purchased+":"+legacyExpiry+":"+stray,delegate(Offline70EconomyState s){
   // 上一版误把服务标记道具发进了背包：这里全部收回（它们不是可持有点券道具）。契约本身
   // 只记 ServiceDaysFG272 台账，等状态包实现后按台账补记，不在背包留东西。
   s.Inventory.RemoveAll(delegate(Offline70EconomyEntry e){return Array.IndexOf(ServiceItems,e.ItemId)>=0;});
   s.PremiumsFG129.RemoveAll(delegate(Offline70PremiumFG129 p){return p.Kind<=7;});
   if(purchased>s.ServiceDaysFG272)s.ServiceDaysFG272=purchased;
  },out duplicate);
 }
}
