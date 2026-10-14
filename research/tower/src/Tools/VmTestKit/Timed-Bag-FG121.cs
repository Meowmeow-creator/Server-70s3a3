using System;using System.IO;
// Current validated List1 avatars only. Worn omission has a known native cache
// gap and remains outside this timer; existing world-entry expiry is retained.
class Offline70TimedBagFG121 {
 internal static bool Expired(Offline70EconomyEntry e,uint now){return e.List==1&&e.ExpiresUnix!=0&&e.ExpiresUnix<=now;}
 internal static Offline70EconomyState Expire(string path,uint now){
  Offline70EconomyState before=Offline70Economy.Load(path);bool pending=false;
  foreach(Offline70EconomyEntry e in before.Inventory)if(Expired(e,now)){pending=true;break;}
  if(!pending)return before;
  bool duplicate;
  return Offline70Economy.Apply(path,"avatar-bag-expiry-"+before.Revision,"avatar-bag-expiry",now.ToString(System.Globalization.CultureInfo.InvariantCulture),delegate(Offline70EconomyState s){s.Inventory.RemoveAll(delegate(Offline70EconomyEntry e){return Expired(e,now);});},out duplicate);
 }
}
partial class Offline70BatchServiceFG {
 uint lastTimedBagScanFG121;
 void MaintainTimedBagFG121(){
  if(!selected||assets==null)return;
  uint now=Offline70PotFG93.UnixNow();if(now==lastTimedBagScanFG121)return;lastTimedBagScanFG121=now;
  bool pending=false;foreach(Offline70EconomyEntry e in assets.Inventory)if(Offline70TimedBagFG121.Expired(e,now)){pending=true;break;}
  if(!pending)return;
  int count=assets.Inventory.Count;assets=Offline70TimedBagFG121.Expire(economyPath,now);
  if(assets.Inventory.Count<count){Sync();Send("timed-avatar-bag13",Offline70EconomyProtocol.AvatarBagReply(assets,keys));Note("TIMED_BAG_AVATARS_EXPIRED count="+(count-assets.Inventory.Count)+" wornUnchanged=True");}
 }
}
