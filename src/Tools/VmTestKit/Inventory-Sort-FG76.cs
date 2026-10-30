using System;using System.IO;using System.Collections.Generic;
// CN70 E277C0 sends list + selected tab; BF1570 reads only the list back.
// Neither direction has a folded-MD5 trailer. TW SortItem sends success then
// send_itemspace. Preserve complete records and compact each CN70 page separately.
class Offline70InventorySortFG76 {
 internal class Request {internal byte List,Tab;}
 internal static Request Parse(byte[] encrypted,byte[] keys){
  if(encrypted==null||encrypted.Length!=8)throw new InvalidDataException("Sort20 request block length");
  byte[] b=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,6,false);
  if(b[0]!=0&&b[0]!=1&&b[0]!=2&&b[0]!=7||b[1]>11)throw new InvalidDataException("Sort20 container or tab");
  for(int i=2;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Sort20 request padding");
  return new Request{List=b[0],Tab=b[1]};
 }
 internal static byte[] Reply(byte list,bool success,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,20,success?new byte[]{1,list}:new byte[]{0,0x17,list},keys,false);}
 internal static Offline70EconomyState Sort(string path,string receipt,Request r,out bool duplicate){
  if(r==null||r.List!=0&&r.List!=1&&r.List!=2&&r.List!=7)throw new InvalidDataException("Sort20 unsupported container");
  return Offline70Economy.Apply(path,receipt,"inventory-sort",r.List+":"+r.Tab,delegate(Offline70EconomyState s){
   // Wallet, item shortcuts, worn equipment and other containers stay fixed.
   // 201 is the 副职业 page and 249 the 装扮->徽章 page (CSlotBoundChecker 0xc9/0xf9 bases).
   int[] first=r.List==0?new int[]{9,57,105,153,201,249}:r.List==7?new int[]{0,140,189}:new int[]{0};
   foreach(int start in first){int end=start+(r.List==0?32:r.List==7?(start==0?140:49):r.List==2?s.CargoSlotsFG129:104);List<Offline70EconomyEntry> page=new List<Offline70EconomyEntry>();
    foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==r.List&&e.Slot>=start&&e.Slot<end)page.Add(e);
    page.Sort(delegate(Offline70EconomyEntry a,Offline70EconomyEntry b){return a.Slot.CompareTo(b.Slot);});
    for(int i=0;i<page.Count;i++)page[i].Slot=(ushort)(start+i);
   }
  },out duplicate);
 }
}
