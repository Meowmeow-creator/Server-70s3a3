using System;using System.IO;using System.Collections.Generic;
class Offline70PotExpiryFG93 {
 internal static Offline70EconomyState Expire(string path,uint now){Offline70EconomyState before=Offline70Economy.Load(path);bool pending=false;foreach(Offline70EconomyEntry e in before.Inventory)if(e.ExpiresUnix!=0&&e.ExpiresUnix<=now)pending=true;if(!pending)return before;bool duplicate;return Offline70Economy.Apply(path,"avatar-expiry-"+before.Revision,"avatar-expiry",now.ToString(),delegate(Offline70EconomyState s){s.Inventory.RemoveAll(delegate(Offline70EconomyEntry e){return e.ExpiresUnix!=0&&e.ExpiresUnix<=now;});},out duplicate);}
}
