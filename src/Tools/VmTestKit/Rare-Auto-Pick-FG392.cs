using System;using System.IO;using System.Collections.Generic;using System.Text;
// FG392: item [rarity] catalog + the server-side pickup for rows the client will not request.
//
// Live evidence (2026-10-29): the CN70 client refuses to send the dungeon pickup frame (op46)
// for a ground row whose PVF [rarity] is 2 or above - it starts the pickup animation and cancels
// it locally before any frame leaves the process (docs/INVESTIGATION-2026-10-29-rare-pickup.md).
// Every manual PICKUP_COMMITTED in the session logs is rarity 0/1 (all of them merges into an
// existing stack or rarity-0 equipment), while the rarity-2 rows (3221/3187/3249/2749124/3303/
// 3324/3300) sit on the floor with no op46 at all. The native pickup path (rednf CParty::get_item
// + DisPatcher_GetItem) never reads rarity, so no server acceptance rule can change that local
// gate; what the server can do is deliver those rows itself through the pickup transaction the
// client already accepts (op39 + the bag row), which is exactly what the GM auto-pickup toggle
// proved in the same logs.
//
// So: ground rows with rarity >= 2 are queued for the same pickup path (never a raw grant), a
// step after the death frame so the client has spawned the item first, and retried while the
// room stays current and the bag refuses them (full page). rarity 0/1 keeps the manual pickup.
// FG394 (2026-10-30) located the client gate itself: the pickup validator at client 0x93D2E0
// refuses rows whose profile rarity ([item+0xDC]) is >= 2 before the request is ever built
// (cmp dword [eax+0xdc],2 / jge -> toast 0x874). That one byte is patched to 6 in the client,
// so manual pickup now works for every droppable tier and this module is a fallback only:
// opt-in via `rare-auto-pick.on` in the store, default off.
// A missing/invalid catalog leaves every row manual, so an older data folder behaves exactly
// like before this file existed.
class Offline70ItemRarityFG392 {
 internal const string Magic="FG392RR1";internal const int MaximumBytes=4194304,MaximumRows=200000,Threshold=2;
 static Dictionary<uint,byte> Table;
 internal static void Load(string path){
  if(File.Exists(path)==false)return;
  if(new FileInfo(path).Length>MaximumBytes)throw new InvalidDataException("Item rarity catalog size");
  byte[] raw=File.ReadAllBytes(path);if(raw.Length<76||Encoding.ASCII.GetString(raw,0,8)!=Magic)throw new InvalidDataException("Item rarity catalog magic");
  for(int i=0;i<64;i++)if(raw[8+i]<0x20||raw[8+i]>0x7e)throw new InvalidDataException("Item rarity catalog PVF tag");
  if(Encoding.ASCII.GetString(raw,8,64)!=Offline70QuestCatalogFG.CurrentPvf)throw new InvalidDataException("Item rarity catalog PVF");
  int count=BitConverter.ToInt32(raw,72);if(count<0||count>MaximumRows||raw.Length!=76+5*count)throw new InvalidDataException("Item rarity catalog count");
  Dictionary<uint,byte> table=new Dictionary<uint,byte>(count);uint previous=0;
  for(int i=0;i<count;i++){uint item=BitConverter.ToUInt32(raw,76+5*i);byte rarity=raw[80+5*i];if(item<=previous||rarity>5)throw new InvalidDataException("Item rarity catalog row");previous=item;table.Add(item,rarity);}
  Table=table;
 }
 internal static bool Loaded{get{return Table!=null;}}
 internal static int Count{get{return Table==null?0:Table.Count;}}
 internal static bool Known(uint item){return Table!=null&&Table.ContainsKey(item);}
 // Unknown ids (missing [rarity] tag, unregistered rows, gold) are never rare, so a partial
 // catalog can only keep the previous manual behaviour.
 internal static byte Rarity(uint item){byte rarity;return Table!=null&&Table.TryGetValue(item,out rarity)?rarity:(byte)0;}
 internal static bool Rare(uint item){return Rarity(item)>=Threshold;}
 internal static long RarePickDelayMs=650;
}
partial class Offline70BatchServiceFG {
 // The queue is keyed by room scope exactly like the GM auto-pickup queue: a room change must not
 // collect a row that the next room's roster reuses.
 readonly List<KeyValuePair<string,ushort>> rarePickQueueFG392=new List<KeyValuePair<string,ushort>>();
 readonly Dictionary<string,int> rarePickRetryFG392=new Dictionary<string,int>();
 long rarePickDueFG392;int rarePickAttemptsFG392;int?rarePickEnabledFG392;
 bool RarePickEnabledFG392(){if(rarePickEnabledFG392==null){bool on=false;try{/* Opt-in since FG394: the client gate patch makes manual pickup work for rarity 2..5, so the server-side collection is a fallback only (a running auto-collect would race the player's own pickup and confuse the acceptance test). */on=File.Exists(Path.Combine(folder,"rare-auto-pick.on"));}catch(IOException){}rarePickEnabledFG392=on?1:0;}return rarePickEnabledFG392.Value==1;}
 void QueueRarePickupFG392(List<Offline70GroundFG> drops){
  if(drops==null||drops.Count==0||!Offline70ItemRarityFG392.Loaded||!RarePickEnabledFG392())return;
  string scope=dungeon.RoomReceiptScope;
  foreach(Offline70GroundFG drop in drops){
   if(drop.ItemId==0||!Offline70ItemRarityFG392.Rare(drop.ItemId))continue;
   KeyValuePair<string,ushort> entry=new KeyValuePair<string,ushort>(scope,drop.Slot);
   if(rarePickQueueFG392.Contains(entry))continue;
   rarePickQueueFG392.Add(entry);
   Note("RARE_PICKUP_QUEUED ground="+drop.Slot+" item="+drop.ItemId+" rarity="+Offline70ItemRarityFG392.Rarity(drop.ItemId)+" quantity="+drop.Quantity);
  }
 }
 // One row per step, same cadence as the GM sweep, so the client sees the drop land and then the
 // authorised pickup. A bag-capacity refusal is retried (the player can free a page mid-room);
 // anything else (row already gone, room changed) drops the entry with a log line.
 void PumpRarePickupFG392(){
  long now=DateTime.UtcNow.Ticks;
  if(rarePickQueueFG392.Count==0||now<rarePickDueFG392)return;
  rarePickDueFG392=now+(long)Offline70ItemRarityFG392.RarePickDelayMs*TimeSpan.TicksPerMillisecond;
  KeyValuePair<string,ushort> entry=rarePickQueueFG392[0];rarePickQueueFG392.RemoveAt(0);
  string key=entry.Key+":"+entry.Value;
  if(!dungeon.Active||!dungeon.Loaded||entry.Key!=dungeon.RoomReceiptScope){rarePickRetryFG392.Remove(key);Note("RARE_PICKUP_STALE ground="+entry.Value+" room="+dungeon.RoomReceiptScope);return;}
  try{if(PickupGroundFG201(entry.Value)){rarePickRetryFG392.Remove(key);rarePickAttemptsFG392++;}}
  catch(InvalidDataException ex){
   if(ex.Message.IndexOf("capacity",StringComparison.OrdinalIgnoreCase)>=0){
    int tries;rarePickRetryFG392.TryGetValue(key,out tries);tries++;
    if(tries>60){rarePickRetryFG392.Remove(key);Note("RARE_PICKUP_DEFERRED ground="+entry.Value+" tries="+tries+" reason="+ex.Message);return;}
    rarePickRetryFG392[key]=tries;rarePickQueueFG392.Add(entry);Note("RARE_PICKUP_BAG_FULL ground="+entry.Value+" tries="+tries);return;
   }
   rarePickRetryFG392.Remove(key);Note("RARE_PICKUP_DROPPED ground="+entry.Value+" reason="+ex.Message);
  }
 }
 void ResetRarePickupFG392(){rarePickQueueFG392.Clear();rarePickRetryFG392.Clear();rarePickAttemptsFG392=0;}
}
