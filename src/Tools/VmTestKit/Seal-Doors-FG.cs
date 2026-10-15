using System;using System.Collections.Generic;
// Seal door tables derived from PVF [seal door ...] tags.
// A seal door is a special room transition when hell party is active at a specific grid position.
internal static class SealDoorsFG {
 internal static readonly Dictionary<ushort,ushort[]> Table =
  new Dictionary<ushort,ushort[]>{
  {33,new ushort[]{60026,1,0,10}},
  {36,new ushort[]{60020,3,2,1}},
 };
 internal static bool HasSealDoor(ushort dungeonId){
  return Table.ContainsKey(dungeonId);}
 internal static ushort GetSealDoorMapIdx(ushort dungeonId){
  return Table.ContainsKey(dungeonId)?Table[dungeonId][0]: (ushort)0;}
 internal static byte GetSealDoorPosX(ushort dungeonId){
  return Table.ContainsKey(dungeonId)? (byte)Table[dungeonId][1]: (byte)0;}
 internal static byte GetSealDoorPosY(ushort dungeonId){
  return Table.ContainsKey(dungeonId)? (byte)Table[dungeonId][2]: (byte)0;}
 internal static ushort GetSealDoorRate(ushort dungeonId){
  return Table.ContainsKey(dungeonId)?Table[dungeonId][3]: (ushort)0;}
}