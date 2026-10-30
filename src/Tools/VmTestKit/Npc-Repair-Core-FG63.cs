using System;using System.IO;using System.Collections.Generic;
// First repair profile: unenhanced quest belt16400. Price inputs read from CN70
// PVF: value160, grade1, durability20, normal upgrade0 repair rate1.
// CN70 quote for10 missing durability is4: truncate, not DSX60 rounding.
//
// FG398: the profile is no longer limited to the 2416 equipment rows that
// commerce-catalog.xml happens to carry. Store::repair_equip (reference/rednf
// Store/repair_equip.inc) only needs the item script: value/grade from the script,
// endurance from the item, and [repair cost rate by upgrade level] (all 1.0 in this
// PVF, so the quote is level independent). RepairCost now falls back to
// data/disjoint-params-FG92.dat, which already pins [value]/[grade]/[rarity] for all
// 26122 Kind-4 ids - the same table the reinforcement catalog is derived from.
//
// FG398 also drops the invented slot windows: native validates the row
// (m_addInfo!=0, ITEM_TYPE_EQUIP, max endurance!=0) and never pre-filters by slot,
// so the old 9..40 / 11..22 pair rejected slot 10 on one side and slot 22 on the
// other. A row that is not damaged equipment answers 0x11 as before.
class Offline70NpcRepairCoreFG63 {
 internal static uint Cost(Offline70EconomyEntry e){return Offline70CommerceFG71.RepairCost(e);}
 // Worn slots are 10..21 (EquipSlot) and native's second full-repair loop is exactly
 // those; the first loop reads backpack 3..8. Nothing wider: the client's own bulk
 // quote is what the player saw, so scanning more rows would overcharge.
 static bool AllCandidate(Offline70EconomyEntry e){return e.List==3&&e.Slot>=10&&e.Slot<=21||e.List==0&&e.Slot>=3&&e.Slot<=8;}
 internal static Offline70EconomyState Repair(string path,string receipt,byte list,ushort slot,out bool duplicate){bool all=list==0&&slot==UInt16.MaxValue;if(!all&&list!=0&&list!=3)throw new InvalidDataException("Repair target space");return Offline70Economy.Apply(path,receipt,"npc-repair","ordinary-FG71:"+list+":"+slot,delegate(Offline70EconomyState s){List<Offline70EconomyEntry> targets=new List<Offline70EconomyEntry>();ulong total=0;foreach(Offline70EconomyEntry e in s.Inventory){bool candidate=all?AllCandidate(e):e.List==list&&e.Slot==slot;if(!candidate)continue;Offline70EconomyItemDefinition d=Offline70Economy.Definition(e.ItemId);if(d.Kind!=4)continue;if(d.MaxDurability==0||e.Durability>=d.MaxDurability)continue;uint cost;try{cost=Cost(e);}catch(InvalidDataException){if(!all)throw;continue;}// native never aborts a bulk repair on one item
 if(total+cost>Offline70Economy.WalletLimit)throw new InvalidDataException("Repair total overflow");total+=cost;targets.Add(e);}if(targets.Count==0)throw new InvalidDataException("No damaged equipment");if(s.Gold<total)throw new InvalidDataException("Insufficient repair gold");foreach(Offline70EconomyEntry e in targets)e.Durability=Offline70Economy.Definition(e.ItemId).MaxDurability;s.Gold-=(uint)total;},out duplicate);}
}
