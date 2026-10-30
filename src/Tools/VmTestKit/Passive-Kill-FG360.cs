using System;using System.Collections.Generic;
// Passive-object destruction credit (FG360, batch 2 of the 2026-10-27 triage).
//
// Protocol evidence (reference/rednf DisPatcherCore/dispatch_sig_16.inc, the die packet):
//   short mobSeq, short fieldF, int x4, byte contributorCount, contributors[10],
//   short pairA24, short pairA26, byte A22, byte A23, BYTE passiveFlag, byte moveCheck,
//   short unused, byte A28, int A2d, int A31, short A29/A2b/A35/A37/A39/A3b/A3d
// so passiveFlag sits at tail+6 (tail = 21+10*count). The native branch is
//   if (passiveFlag == 1) { party->DestroyPassiveObject((int)die.m_mobSeq); return 0; }
// with no kill_monster call: no drop plan, no experience, no room-clear accounting.
// CParty::DestroyPassiveObject -> CBattle_Field::DestroyPassiveObject accepts the id only
// when it is a member of CMap::m_passiveObjectList, the `[passive object]` ids of the
// room's own .map (CMap/set_map.inc:122-131), which is exactly what
// data/passive-objects-FG360.dat carries.
//
// Quest objectives: [hunt enemy]/[hunt monster] rows can name a destructible object
// instead of a .mob (the 2026-10-26 recon note "hunt-enemy-missing-monsters.md":
// 8824 囚牢 / 8877 了望台 / 10269 卡勒特障碍物 are passive objects with enemyType 3, not
// monster ids). The native kill feed runs on monster deaths only, so the object
// destruction report is the one signal the client emits for those objectives; it is
// credited here and nowhere else.
partial class Offline70DungeonFlowFG {
 // room index -> object id -> destructions already credited. One entry per destroyed
 // instance, capped by how many times the map declares that id (a room may hold two
 // identical watchtowers), so a replayed frame cannot add a second count.
 readonly Dictionary<int,Dictionary<ushort,int>> passiveKillsFG360=new Dictionary<int,Dictionary<ushort,int>>();
 // Number of times the current room's map declares this object id (0 = not in this room).
 int PassiveCapacityFG360(ushort map,ushort id){
  ushort[] ids;if(!Offline70PassiveObjectsFG360.Table.TryGetValue(map,out ids))return 0;
  int count=0;foreach(ushort row in ids)if(row==id)count++;return count;
 }
 // Returns the 1-based destruction ordinal for a still-uncredited instance, or 0 when the
 // report is outside this room / already fully credited. Never throws for a wrong id: the
 // client reports whatever it destroyed and the room membership is the authority.
 internal int DestroyPassiveObjectFG360(ushort id){
  if(!Active||ClearPublished||id==0)return 0;
  ushort map=CurrentMap;
  int capacity=PassiveCapacityFG360(map,id);
  if(capacity<=0)capacity=Offline70ObjectDropsFG167.Capacity(map,id);
  if(capacity<=0)return 0;
  Dictionary<ushort,int> room;
  if(!passiveKillsFG360.TryGetValue(RoomIndex,out room)){room=new Dictionary<ushort,int>();passiveKillsFG360.Add(RoomIndex,room);}
  int prior;room.TryGetValue(id,out prior);
  if(prior>=capacity)return 0;
  room[id]=prior+1;
  return prior+1;
 }
 internal int PassiveKillsFG360(ushort id){Dictionary<ushort,int> room;int n;return passiveKillsFG360.TryGetValue(RoomIndex,out room)&&room.TryGetValue(id,out n)?n:0;}
}
