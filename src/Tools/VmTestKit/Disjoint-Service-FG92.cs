using System;using System.IO;using System.Collections.Generic;
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> disjointRequestsFG92=new Dictionary<ushort,string>();
 void DisjointFG92(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;
  if(disjointRequestsFG92.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Disjoint sequence reused");Note("DISJOINT_DUPLICATE_IGNORED seq="+sequence);return;}
  byte[] body;Dictionary<ushort,uint> collection=null;
  try{
   if(dungeon.Active||!Offline70TownContentFG75.Npc(town,area,13))throw new InvalidDataException("NPC disjoint requires Norton in current town area");
   Offline70DisjointFG92.Request r=Offline70DisjointFG92.Parse(encrypted,keys);Sync();collection=CollectionSnapshot();Offline70EconomyState before=assets;bool duplicate;
   assets=Offline70DisjointFG92.Open(economyPath,session+"-disjoint-"+sequence,r,Offline70DisjointFG92.Draw(10000),Offline70DisjointFG92.Draw(10000),out duplicate);
   if(duplicate){Note("DISJOINT_RECEIPT_DUPLICATE_IGNORED seq="+sequence);return;}
   body=Offline70DisjointFG92.Body(r,true,before,assets);Note("DISJOINT_COMMITTED slot="+r.Slot+" rewards="+body[4]);
  }catch(InvalidDataException e){body=Offline70DisjointFG92.Body(null,false,null,null);collection=null;Note("DISJOINT_REJECTED "+e.Message);}
  disjointRequestsFG92.Add(sequence,fingerprint);Send(body[0]==1?"disjoint28":"disjoint-failed28",Offline70DisjointFG92.Reply(body,keys));
  // Native28 owns reward insertion and target deletion; keep popup objects alive.
  if(body[0]==1){Sync();CollectionChanged(collection);}
 }
}
