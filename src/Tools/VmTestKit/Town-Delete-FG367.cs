using System;using System.IO;using System.Collections.Generic;
// op18 town delete semantics, kept out of the batch-service loop so the offline driver can
// exercise the same rules the live server applies (FG352 only tested the sale path, which let
// the pet-window case rot here).
static class Offline70TownDeleteFG367 {
 // FG405: the frame is native delete-item (rednf DisPatcher_DeleteItem::process 0x081c1dda) and
 // carries one row per item with an opType: 1 = the bag's own delete button, 2/3/4 = automated
 // consumption (native feeds them to the cube statistics under user state 5, i.e. in dungeon).
 // 灵魂猎者 (20143) spends one 灵魂晶石 (3187) with opType 3 right before its client-side 60 s
 // buff, and skill casts announce their 无色小晶块 (3037) with opType 2. FG346 already debits
 // that cube on the op41 cast, so replaying the opType-2 cube row would charge the same cast
 // twice; every other row is exactly the debit the client performed locally.
 internal static Offline70DeleteRequestFG FilterFG405(Offline70DeleteRequestFG r,out int skipped){
  List<Offline70DeleteRowFG> rows=new List<Offline70DeleteRowFG>();skipped=0;
  foreach(Offline70DeleteRowFG row in r.Rows){if(row.Reason==2&&row.ItemId==Offline70SkillCubeFG346.ClearCube){skipped++;continue;}rows.Add(row);}
  return new Offline70DeleteRequestFG{List=r.List,Rows=rows.ToArray()};
 }
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
   }else if(r.List==1){
    // FG407: the avatar bag (kind 3, list 1 slots 0..103 - Offline70Economy.Position). The
    // player asked to be able to throw costumes away; every avatar in the current PVF carries
    // `[attach type] [trade]`, so this is always a plain removal, exactly like the client's
    // "this item will disappear" confirmation promises before it sends the frame.
    if(def.Kind!=3)throw new InvalidDataException("Delete avatar bag item kind");
    if(row.Slot>=104||row.Quantity!=1)throw new InvalidDataException("Delete avatar position/count");
   }else{
    if(def.Kind!=1&&def.Kind!=2&&def.Kind!=4&&def.Kind!=5&&def.Kind!=9&&def.Kind!=10)throw new InvalidDataException("Delete unverified item kind");
    if(def.Kind==4&&(row.Slot<3||row.Slot>=41||row.Quantity!=1))throw new InvalidDataException("Delete equipment bag position/count");
    // FG400: [quest] items (Kind 5) are the bag's quest page, list 0 slots 153..184
    // (PositionCore). Live op18 captures 2026-10-30 14:17:48 (slot 154 item 3209 x2) and
    // 14:18:08 (slot 153 item 4183 x305) prove the client itself asks to delete them - the
    // request died here on the kind whitelist, which is the reported "quest tab cannot be
    // discarded". The page bound keeps the acceptance surface to that page only.
    if(def.Kind==5&&(row.Slot<153||row.Slot>=185))throw new InvalidDataException("Delete quest page position");
   }
  }
  foreach(Offline70DeleteRowFG row in r.Rows){Offline70EconomyEntry e=Offline70Economy.Find(state,r.List,row.Slot);e.Quantity-=row.Quantity;if(e.Quantity==0)state.Inventory.Remove(e);}
 }
}
