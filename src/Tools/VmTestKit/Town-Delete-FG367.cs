using System;using System.IO;
// op18 town delete semantics, kept out of the batch-service loop so the offline driver can
// exercise the same rules the live server applies (FG352 only tested the sale path, which let
// the pet-window case rot here).
static class Offline70TownDeleteFG367 {
 internal static void Apply(Offline70EconomyState state,Offline70DeleteRequestFG r){
  foreach(Offline70DeleteRowFG row in r.Rows){
   Offline70EconomyEntry e=Offline70Economy.Find(state,r.List,row.Slot);
   if(e==null||e.ItemId!=row.ItemId||e.Quantity<row.Quantity)throw new InvalidDataException("Delete ownership/count");
   Offline70EconomyItemDefinition def=Offline70Economy.Definition(e.ItemId);
   // Kind 8 is pet equipment: the pet window holds it at list 7 slots 140..188 and the
   // original discards it like any other item (live report 2026-10-27, capture 2026-10-28).
   if(r.List==7){
    if(def.Kind!=8)throw new InvalidDataException("Delete pet window item kind");
    if(row.Slot<140||row.Slot>=189||row.Quantity!=1)throw new InvalidDataException("Delete pet equipment position/count");
   }else{
    if(def.Kind!=1&&def.Kind!=2&&def.Kind!=4&&def.Kind!=9&&def.Kind!=10)throw new InvalidDataException("Delete unverified item kind");
    if(def.Kind==4&&(row.Slot<3||row.Slot>=41||row.Quantity!=1))throw new InvalidDataException("Delete equipment bag position/count");
   }
  }
  foreach(Offline70DeleteRowFG row in r.Rows){Offline70EconomyEntry e=Offline70Economy.Find(state,r.List,row.Slot);e.Quantity-=row.Quantity;if(e.Quantity==0)state.Inventory.Remove(e);}
 }
}
