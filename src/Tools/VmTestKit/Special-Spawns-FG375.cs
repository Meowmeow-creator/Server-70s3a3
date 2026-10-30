using System;using System.Collections.Generic;using System.IO;
// FG375: `[special passive object]` `[monster]` actions.
//
// Native chain (rednf):
//   STMapScript ImportMapScript parses `[special passive object]` into STAssignSpecialPassiveObj
//   rows; STMapScript/ExportMapScript.inc:334-374 shows the file layout this table is generated
//   from (`<objId> <m_4> <m_8> <m_c>` / `<actionCount>` / `<tag> <m_4> <m_8> <m_c> <m_10> <m_14>`).
//   CMap::set_map (CMap/set_map.inc:71-114) walks the actions when the room map is loaded:
//     * type 0 = `[monster]`: m_10 == 0 -> skip; rand(100) > (m_c*100)/m_10 -> skip;
//       level (m_8) must be 1..200; then insert_monster({id=m_4, level=m_8}, area) with area =
//       the object ordinal, which also flags that monster as a champion.
//     * type 2 = `[item]`: insert_item(...) - the object drop pools already served by
//       data/object-drops-FG167.xml (Offline70ObjectDropsFG167).
//     * types 1 `[trap]` / 3 `[quest]`: no server-side effect here (the client plays the trap).
//   add_monsters_current_map then reads the level as ABSOLUTE (ref.m_flag04 == 0 -> m_field08).
//
// The spawned actors are appended to the room roster, so they arrive in the room packet and count
// for the room clear. Their drop-roll columns are 0 in native (insert_monster leaves m_pair18 at
// 0), which Floor-Drops honours through MaxSpawns(): a uid past the map's roll table but inside
// the special-spawn allowance rolls nothing instead of tripping the roster guard.
//
// FG386 (2026-10-29 live report: d89/17045 根特防御战 boss room empty): the actor row carries the
// owner link. Native add_monsters_current_map writes `mob.m_field13 = ref.m_champion` and
// `mob.m_field14 = ref.m_posX` (BattleMonsterDepsC2/add_monsters_current_map.inc:127-138), and
// MakePacket(0x1d) emits both as the row's last two bytes. insert_monster set them from `area`
// (CMap/insert_monster.inc:19-24: `area != -1 -> m_champion = 1, m_posX = area`), so an object
// action monster is roleType 0 (`[normal]`) + field13=1 + field14=<object ordinal>. The FG375 row
// only kept {id, level, 1}, which packetised as roleType 1 / 0 / 0: no owner ordinal, so the
// client could neither bind the monster to its [special passive object] nor place it (map 17045 has
// no [monster] rows at all) and the whole room stayed empty. The roster row now carries the
// original object ordinal in a 4th column; Dungeon-Flow's RoomBody writes the native triple.
internal static class Offline70SpecialSpawnsFG375{
 internal const int MaximumSpawns=64;
 internal static readonly Dictionary<ushort,ushort[][]> Table=new Dictionary<ushort,ushort[][]>();
 internal static void Load(string path){
  Table.Clear();
  if(!File.Exists(path))return;
  byte[] raw=File.ReadAllBytes(path);
  if(raw.Length<74||System.Text.Encoding.ASCII.GetString(raw,0,8)!="FG375SS2")throw new InvalidDataException("Special spawn table magic");
  int at=8+64;
  int maps=BitConverter.ToUInt16(raw,at);at+=2;
  if(maps>4000)throw new InvalidDataException("Special spawn map count");
  for(int m=0;m<maps;m++){
   if(at+4>raw.Length)throw new InvalidDataException("Special spawn table bounds");
   ushort map=BitConverter.ToUInt16(raw,at);int rows=BitConverter.ToUInt16(raw,at+2);at+=4;
   if(rows<1||rows>MaximumSpawns||at+rows*8>raw.Length)throw new InvalidDataException("Special spawn rows");
   ushort[][] list=new ushort[rows][];
   for(int r=0;r<rows;r++){
    list[r]=new ushort[]{BitConverter.ToUInt16(raw,at),BitConverter.ToUInt16(raw,at+2),BitConverter.ToUInt16(raw,at+4),BitConverter.ToUInt16(raw,at+6)};
    if(list[r][0]==0||list[r][1]==0||list[r][1]>200||list[r][2]>100||list[r][3]>MaximumSpawns)throw new InvalidDataException("Special spawn row");
    at+=8;
   }
   if(Table.ContainsKey(map))throw new InvalidDataException("Special spawn duplicate map");
   Table.Add(map,list);
  }
 }
 internal static int MaxSpawns(ushort map){ushort[][] rows;return Table.TryGetValue(map,out rows)?rows.Length:0;}
 // Native roll per action: skip when rand(100) > pct (pct = (m_c*100)/m_10, clamped at load).
 internal static void Append(string runId,int room,ushort map,List<ushort[]> roster){
  ushort[][] rows;if(roster==null||!Table.TryGetValue(map,out rows))return;
  for(int i=0;i<rows.Length;i++){
   int draw=Offline70QuestDropsFG.StableDraws(runId+"-spobj-"+room+"-"+map+"-"+i)(100);
   if(draw>(int)rows[i][2])continue;
   // Native m_roleType stays 0 ([normal]) even though m_champion is 1: the champion marker is
   // field13 on the wire, not the role byte. XP/gold read column 2, so it must be what we send.
   roster.Add(new ushort[]{rows[i][0],rows[i][1],0,rows[i][3]});
  }
 }
}
