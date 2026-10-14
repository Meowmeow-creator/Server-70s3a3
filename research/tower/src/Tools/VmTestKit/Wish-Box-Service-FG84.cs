using System;using System.IO;using System.Collections.Generic;
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> wishRequestsFG84=new Dictionary<ushort,string>();
 readonly Dictionary<ushort,byte[]> wishRepliesFG84=new Dictionary<ushort,byte[]>();
 void OpenWishBoxFG84(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;
  if(wishRequestsFG84.TryGetValue(sequence,out prior)){
   if(prior!=fingerprint)throw new InvalidDataException("Random box sequence reused");
   // Native223 consumes/adds quantities. Do not execute it again for a retry.
   Note("WISH_BOX_DUPLICATE_IGNORED seq="+sequence);return;
  }
  byte[] body,revival=null;Dictionary<ushort,uint> collection=null;
  try{
   if(dungeon.Active)throw new InvalidDataException("Wish box inside dungeon");ushort slot=Offline70WishBoxFG84.Parse(encrypted,keys);Sync();collection=CollectionSnapshot();Offline70EconomyState before=assets;bool duplicate;
   // Project only the exact committed inventory gains into the native reply.
   Offline70EconomyEntry source=Offline70Economy.Find(before,0,slot);if(source!=null&&source.ItemId==Offline70BlessingCatalogFG95.Box){assets=Offline70BlessingFG95.Open(economyPath,session+"-blessing-"+sequence,slot,new int[]{Offline70BlessingFG95.Draw(),Offline70BlessingFG95.Draw(),Offline70BlessingFG95.Draw()},out duplicate);body=Offline70BlessingFG95.Body(slot,before,assets);revival=Offline70BlessingFG95.RevivalBody(before,assets);Note("BLESSING_BOX_COMMITTED slot="+slot+" groups=3 revival="+assets.RevivalCoins+" gains="+Offline70BlessingFG95.Gains(before,assets).Count+" duplicate="+duplicate);}else{assets=Offline70WishBoxFG84.Open(economyPath,session+"-wish-"+sequence,slot,DateTime.UtcNow.Ticks,out duplicate);body=Offline70WishBoxFG84.Body(true,slot,before,assets);Note("WISH_BOX_COMMITTED slot="+slot+" teleport=2 blessing=1 duplicate="+duplicate);}
  }catch(InvalidDataException e){body=Offline70WishBoxFG84.Body(false,0,null,null);collection=null;Note("WISH_BOX_REJECTED "+e.Message);}
  byte[] reply=Offline70WishBoxFG84.Reply(body,keys);wishRequestsFG84.Add(sequence,fingerprint);wishRepliesFG84.Add(sequence,reply);Send(body[0]==1?"wish-opened223":"wish-failed223",reply);
  // Native223 owns source debit and reward insertion. A full13 reload here
  // destroys the objects retained by the reward window.
  if(body[0]==1){Sync();if(revival!=null)Send("blessing-revival-balance14",Offline70EconomyProtocol.UpdatePacket(revival,keys));CollectionChanged(collection);}
 }
}
