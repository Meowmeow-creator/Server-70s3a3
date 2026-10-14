using System;using System.IO;using System.Collections.Generic;
// Registered kind3 avatar expiry only, using original14 FFFFFFFF removal.
// No use/move success acknowledgement or character/room rebuild is emitted.
class Offline70TimedWornFG122 {
 internal static bool Expired(Offline70EconomyEntry e,uint now){return e.List==3&&Offline70Economy.Definition(e.ItemId).Kind==3&&e.ExpiresUnix!=0&&e.ExpiresUnix<=now;}
 internal static Offline70EconomyState Expire(string path,uint now){
  Offline70EconomyState before=Offline70Economy.Load(path);bool pending=false;foreach(Offline70EconomyEntry e in before.Inventory)if(Expired(e,now)){pending=true;break;}
  if(!pending)return before;bool duplicate;return Offline70Economy.Apply(path,"worn-avatar-expiry-"+before.Revision,"worn-avatar-expiry",now.ToString(System.Globalization.CultureInfo.InvariantCulture),delegate(Offline70EconomyState s){s.Inventory.RemoveAll(delegate(Offline70EconomyEntry e){return Expired(e,now);});},out duplicate);
 }
 internal static byte[] RemovedReply(Offline70EconomyState before,Offline70EconomyState after,byte[] keys){
  List<ushort> slots=new List<ushort>();foreach(Offline70EconomyEntry e in before.Inventory)if(e.List==3&&Offline70Economy.Definition(e.ItemId).Kind==3&&Offline70Economy.Find(after,3,e.Slot)==null)slots.Add(e.Slot);slots.Sort();if(slots.Count==0)throw new InvalidDataException("Worn expiry removal context");
  byte[] body=new byte[3+31*slots.Count];body[0]=3;BitConverter.GetBytes((ushort)slots.Count).CopyTo(body,1);for(int i=0;i<slots.Count;i++){BitConverter.GetBytes(slots[i]).CopyTo(body,3+31*i);BitConverter.GetBytes(UInt32.MaxValue).CopyTo(body,5+31*i);}return Offline70EconomyProtocol.UpdatePacket(body,keys);
 }
}
partial class Offline70BatchServiceFG {
 uint lastTimedWornScanFG122;
 void MaintainTimedWornFG122(){
  if(!selected||assets==null)return;uint now=Offline70PotFG93.UnixNow();if(now==lastTimedWornScanFG122)return;lastTimedWornScanFG122=now;
  bool pending=false;foreach(Offline70EconomyEntry e in assets.Inventory)if(Offline70TimedWornFG122.Expired(e,now)){pending=true;break;}
  if(!pending)return;Offline70EconomyState before=assets;assets=Offline70TimedWornFG122.Expire(economyPath,now);if(assets.Inventory.Count<before.Inventory.Count){Sync();Send("timed-worn-avatar-removed14",Offline70TimedWornFG122.RemovedReply(before,assets,keys));Note("TIMED_WORN_AVATARS_EXPIRED count="+(before.Inventory.Count-assets.Inventory.Count)+" actorRebuild=False");}
 }
}
