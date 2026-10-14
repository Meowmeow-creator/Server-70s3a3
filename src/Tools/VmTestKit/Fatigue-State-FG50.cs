using System;using System.IO;
// Offline ordinary-fatigue ledger. Service day is CST 06:00, matching the supplied reference.
// CN70 wire layout is verified independently; no JP60 fatigue packet is copied.
class Offline70FatigueFG50 {
 internal const int Maximum=156;
 internal static int ServiceDay(DateTime utc){if(utc.Kind!=DateTimeKind.Utc||utc.Ticks<new DateTime(1970,1,1).Ticks||utc.Ticks>DateTime.MaxValue.Ticks-TimeSpan.TicksPerHour*2)throw new InvalidDataException("Fatigue UTC clock bounds");return checked((int)((utc.Ticks-new DateTime(1970,1,1).Ticks+TimeSpan.TicksPerHour*2)/TimeSpan.TicksPerDay));}
 internal static Offline70EconomyState Refresh(string path,DateTime utc){int day=ServiceDay(utc);Offline70EconomyState before=Offline70Economy.Load(path);if(before.FatigueServiceDay>=day)return before;bool duplicate;return Offline70Economy.Apply(path,"fatigue-day-"+day,"fatigue-day",day.ToString(System.Globalization.CultureInfo.InvariantCulture),delegate(Offline70EconomyState s){if(s.FatigueServiceDay<day){if(s.FatigueServiceDay!=0)s.FatigueRemaining=Maximum;s.FatigueServiceDay=day;}},out duplicate);}
 internal static Offline70EconomyState Spend(string path,string receipt,bool entry,out bool duplicate){return Offline70Economy.Apply(path,receipt,"fatigue-room","ordinary-room:1",delegate(Offline70EconomyState s){if(entry&&s.FatigueRemaining==0)throw new InvalidDataException("Fatigue exhausted before entry");if(s.FatigueRemaining>0)s.FatigueRemaining--;},out duplicate);}
 internal static byte[] Display(Offline70EconomyState s,byte[] keys){return Offline70RoleProtocol.BatchFatigueFG(Maximum-s.FatigueRemaining,keys);}
 internal static bool Potion(uint item){return item==2682613||item==2682615;}
 // These event potions require remaining fatigue below their recovery amount.
 // No daily use limit is invented from a description without such a rule.
 internal static Offline70EconomyState UsePotion(string path,string receipt,Offline70EconomyUseRequest r,out bool duplicate){if(r==null||!Potion(r.ItemId))throw new InvalidDataException("Unknown fatigue potion");Offline70EconomyItemDefinition d=Offline70Economy.Definition(r.ItemId);int recovery=r.ItemId==2682613?1:30;return Offline70Economy.Apply(path,receipt,"fatigue-potion",r.List+":"+r.Slot+":"+r.ItemId+":"+r.InstanceChecksum+":"+r.Tick,delegate(Offline70EconomyState s){Offline70EconomyEntry e=Offline70Economy.Find(s,r.List,r.Slot);if(e==null||e.ItemId!=r.ItemId||d.Kind!=1||s.Level<d.MinimumLevel||d.Job!=255&&s.Job!=d.Job)throw new InvalidDataException("Fatigue potion ownership or level mismatch");if(s.FatigueRemaining>=recovery)throw new InvalidDataException("Fatigue potion threshold not met");if(e.Quantity==1)s.Inventory.Remove(e);else e.Quantity--;s.FatigueRemaining=Math.Min(Maximum,s.FatigueRemaining+recovery);},out duplicate);}
 // C02080 reads a u32 error after the generic status byte. A JP60 u8 error would underflow.
 internal static byte[] EntryFailure(byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,16,new byte[]{0,19,0,0,0},keys,false);}
}
