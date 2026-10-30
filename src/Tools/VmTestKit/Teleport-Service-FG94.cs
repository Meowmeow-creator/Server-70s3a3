using System;using System.IO;using System.Collections.Generic;
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> teleportRequestsFG94=new Dictionary<ushort,string>();
 void TeleportFG94(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;if(teleportRequestsFG94.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Teleport253 sequence reused");Note("TELEPORT_DUPLICATE_IGNORED seq="+sequence);return;}
  Offline70TeleportRequestFG94 r=null;try{
   r=Offline70TeleportPotionFG94.Parse(encrypted,keys);if(dungeon.Active||selectionOpen)throw new InvalidDataException("Teleport253 requires town outside selection");Sync();Offline70EconomyState before=assets;Dictionary<ushort,uint> collection=CollectionSnapshot();bool duplicate;
   assets=Offline70TeleportPotionFG94.Use(economyPath,session+"-teleport-"+sequence,r,DateTime.UtcNow.Ticks,out duplicate);teleportRequestsFG94.Add(sequence,fingerprint);if(duplicate){Note("TELEPORT_RECEIPT_DUPLICATE_IGNORED");return;}
   int nextArea=Offline70TownContentFG75.SeriaArea(r.Town);bool changed=town!=r.Town||area!=nextArea;town=r.Town;area=nextArea;Sync();
   if(changed){Offline70Gate gate=new Offline70Gate{Town=(byte)town,Area=(byte)area,X=474,Y=234,Direction=5,UniqueId=role.Id};foreach(ushort op in new ushort[]{23,24})Send("teleport-area"+op,gate.Reply(op,keys));}
   Send("teleport-effect150",Offline70TeleportPotionFG94.Effect(keys));Send("teleport-inventory14",Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys));Send("teleport-used253",Offline70TeleportPotionFG94.Reply(r,true,keys));CollectionChanged(collection);
   Note("TELEPORT_COMMITTED item="+r.ItemId+" sourceSlot="+r.Slot+" town="+town+" area="+area+" sourceConsumed=1 regionSaved=True");
  }catch(InvalidDataException e){if(!teleportRequestsFG94.ContainsKey(sequence))teleportRequestsFG94.Add(sequence,fingerprint);Send("teleport-failed253",Offline70TeleportPotionFG94.Reply(r,false,keys));Note("TELEPORT_REJECTED "+e.Message);}
 }
}
