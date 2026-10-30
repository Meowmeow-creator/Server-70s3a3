using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;
// Illusion/death tower "[apc random point]" fill (FG363, 2026-10-27).
//
// Several tower maps declare no [monster] rows at all and carry [apc random point] instead
// (map/illusiontower/003f.map and 011f_9f.map = 100; deadtower 30007/13/19/24/35/38..45 with
// 150/250/300/400/600/400/400/400/500/600/700/800/900). The original server's consistMap second
// section (reference/rednf .../CDeathTower/consistMap.inc) draws AI characters from aicharacter.lst
// by appearance point until the map's budget is spent and sends them as roleType 5 actors; the
// native client never receives a tower floor packet with zero actors.
//
// Without this fill those floors are empty: the room packet has count 0, and the CN70 client
// crashes on it - live 2026-10-27 18:00:52, advancing 迷惘之塔 floor 2 (31002, seven monsters) to
// floor 3 (31003, no roster): BugTrace 18:00:52, connection dropped at the same second.
//
// Data: the {id, level} buckets below are the aicharacter appearance-point groups (the FG171 tower
// snapshot carried the same table), and the per-map budgets were read back from the PVF maps
// through pvfUtility (see docs/FIX-2026-10-27-illusion-tower-entry-FG363.md).
internal static class Offline70TowerApFillFG363 {
 internal static readonly int[] Points=new int[]{100,150,170,200,250,350,500};
 static ushort[][] Group(int point){
  switch(point){
   case 100:return new ushort[][]{new ushort[]{600,15},new ushort[]{5004,15},new ushort[]{20002,15}};
   case 150:return new ushort[][]{new ushort[]{5301,50},new ushort[]{5302,35},new ushort[]{5306,50},new ushort[]{5312,40},new ushort[]{10401,20}};
   case 170:return new ushort[][]{new ushort[]{604,25}};
   case 200:return new ushort[][]{new ushort[]{501,30},new ushort[]{15206,30},new ushort[]{15801,30},new ushort[]{15901,30},new ushort[]{20403,20}};
   case 250:return new ushort[][]{new ushort[]{500,40},new ushort[]{603,35},new ushort[]{5308,45},new ushort[]{5313,50},new ushort[]{15207,30},new ushort[]{15501,30},new ushort[]{20201,20}};
   case 350:return new ushort[][]{new ushort[]{5305,50},new ushort[]{5307,45},new ushort[]{10301,45},new ushort[]{10304,45}};
   case 500:return new ushort[][]{new ushort[]{20601,45}};
   default:throw new InvalidDataException("Tower AP point bucket");
  }
 }
 // PVF [apc random point] per floor map; 0 = the map has its own roster and needs no fill.
 internal static int ApcRandomPoint(ushort map){
  switch(map){
   case 30007:return 150;
   case 30013:return 250;
   case 30019:return 300;
   case 30024:return 400;
   case 30035:return 600;
   case 30038:case 30039:case 30040:return 400;
   case 30041:return 500;
   case 30042:return 600;
   case 30043:return 700;
   case 30044:return 800;
   case 30045:return 900;
   case 31003:case 31011:return 100;
   default:return 0;
  }
 }
 // Native loop: draw a point value in [1,budget], take the largest bucket <= it, pick a character
 // from that bucket, skip ids already placed on the floor, subtract the bucket and repeat while the
 // budget still covers the smallest bucket. Deterministic per (run, map) so the room body and the
 // stage packet always describe the same actors.
 internal static List<ushort[]> Draw(ushort map,string runKey,List<ushort> already){
  List<ushort[]> rows=new List<ushort[]>();
  int budget=ApcRandomPoint(map);
  if(budget<Points[0])return rows;
  HashSet<ushort> selected=new HashSet<ushort>();
  if(already!=null)foreach(ushort id in already)selected.Add(id);
  int draw=0,attempts=0;
  while(budget>=Points[0]){
   if(++attempts>10000)throw new InvalidDataException("Tower AP duplicate draw exhausted");
   int requested=Random(runKey,map,draw++,budget)+1;
   int point=Points[0];
   foreach(int value in Points)if(value<=requested)point=value;
   ushort[][] group=Group(point);
   ushort[] choice=group[Random(runKey,map,draw++,group.Length)];
   if(selected.Contains(choice[0])){
    bool available=false;
    foreach(int eligible in Points)if(eligible<=budget)foreach(ushort[] candidate in Group(eligible))if(!selected.Contains(candidate[0]))available=true;
    if(!available)break;
    continue;
   }
   selected.Add(choice[0]);
   budget-=point;
   rows.Add(new ushort[]{map,choice[0],choice[1],(ushort)5});
  }
  return rows;
 }
 static int Random(string runKey,ushort map,int draw,int maximum){
  if(maximum<1||maximum>60000)throw new InvalidDataException("Tower AP random bound");
  using(SHA256 hash=SHA256.Create())return (int)(BitConverter.ToUInt32(hash.ComputeHash(Encoding.ASCII.GetBytes((runKey??"")+"-"+map+"-"+draw)),0)%(uint)maximum);
 }
}
partial class Offline70DungeonFlowFG {
 readonly Dictionary<ushort,List<ushort[]>> towerApFillFG363=new Dictionary<ushort,List<ushort[]>>();
 // FG374c: AP-drawn actors are NEVER sent on the wire any more. The ordinary room path anchors
 // every room-packet row to the map's own [monster]/[ai character] rows (by position in that
 // list), and the [apc random point] maps have neither - live 2026-10-28 23:03: illusion floor 3
 // loaded with one APC row on the wire (uid 100 / 600 / lv15 / roleType 5) and nothing on screen.
 // An unspawnable row is worse than no row: the server would count the APC alive forever and the
 // floor could never clear. Anchor-less floors are skipped at advance time instead (see
 // FloorHostableFG374); native fills them because its client runs the tower module, which this
 // build never enters.
 internal List<ushort[]> TowerApRowsFG363(){
  if(!TowerRunFG211(DungeonId))return new List<ushort[]>();
  List<ushort[]> rows;
  if(towerApFillFG363.TryGetValue(CurrentMap,out rows))return rows;
  rows=new List<ushort[]>();
  towerApFillFG363[CurrentMap]=rows;
  return rows;
 }
}
