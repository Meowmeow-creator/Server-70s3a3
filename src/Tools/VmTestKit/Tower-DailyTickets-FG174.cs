using System;using System.IO;
// User-confirmed5 tickets, shared item4183, with the supplied daily06:00 reset.
// Reference AddDailyItem supplies top-up semantics; no held tickets removed.
class Offline70TowerDailyFG174 {
 internal static int Day(long utcTicks){if(utcTicks<0||utcTicks>DateTime.MaxValue.Ticks-2*TimeSpan.TicksPerHour)throw new InvalidDataException("Tower daily clock bounds");return (int)((utcTicks+2*TimeSpan.TicksPerHour)/TimeSpan.TicksPerDay);}
 internal static uint Owned(Offline70EconomyState s){uint count=0;foreach(Offline70EconomyEntry item in s.Inventory)if(item.List==0&&item.ItemId==4183)count=checked(count+item.Quantity);return count;}
 internal static void Validate(Offline70EconomyState s){if(s.TowerTicketDayFG174<0||s.TowerTicketDayFG174>3652058)throw new InvalidDataException("Tower ticket service day");}
 internal static Offline70EconomyState Grant(string path,long utcTicks,out bool duplicate){int day=Day(utcTicks);Offline70EconomyState state=Offline70Economy.Load(path);duplicate=false;if(state.Level<20||state.TowerTicketDayFG174>=day)return state;return Offline70Economy.Apply(path,"tower-daily-174-"+day,"tower-daily","4183:topup:5:day:"+day,delegate(Offline70EconomyState s){if(s.Level<20||s.TowerTicketDayFG174>=day)throw new InvalidDataException("Tower ticket day qualification changed");uint owned=Owned(s);if(owned<5)Offline70Economy.AddAssetItem(s,4183,5-owned);s.TowerTicketDayFG174=day;},out duplicate);}
}
partial class Offline70BatchServiceFG {
 long towerDailyRetryFG174;
 void MaintainTowerDailyFG174(){if(!selected||dungeon.Active)return;long now=DateTime.UtcNow.Ticks;int day=Offline70TowerDailyFG174.Day(now);if(assets.Level<20||assets.TowerTicketDayFG174>=day||now<towerDailyRetryFG174)return;try{Offline70EconomyState before=assets;bool duplicate;assets=Offline70TowerDailyFG174.Grant(economyPath,now,out duplicate);Sync();byte[] delta=Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys);if(delta!=null)Send("tower-daily-tickets",delta);Note("TOWER_DAILY_TICKETS day="+day+" ownedBefore="+Offline70TowerDailyFG174.Owned(before)+" ownedAfter="+Offline70TowerDailyFG174.Owned(assets)+" target=5 sharedDeathIllusion=True duplicate="+duplicate);}catch(InvalidDataException e){towerDailyRetryFG174=now+5*TimeSpan.TicksPerSecond;Note("TOWER_DAILY_TICKETS_PENDING reason="+e.Message);}}
}
