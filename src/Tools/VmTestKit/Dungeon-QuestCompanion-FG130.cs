using System;using System.IO;using System.Collections.Generic;
// Bound to current CN70 PVF quest4307, maze1 and map1618/1619/APC201.
partial class Offline70DungeonFlowFG {
 internal bool TowerRescueFG156;internal bool GsdRunFG130,GsdSpawnedFG130,GsdDeadFG130;internal ushort GsdQuestFG135,GsdApcFG135,GsdStartMapFG135;internal string GsdQuestInstanceFG130;
 internal bool QuestRoomRunFG205;internal ushort QuestRoomMapFG205,QuestRoomQuestFG205;internal byte QuestRoomXFG205,QuestRoomYFG205;
 // FG214 quest-maze companions: dgn files swap whole maze blocks via [quest connection] while a
 // quest is active. magmacave.dgn ([quest connection] 0 4417 2) moves cell (2,6) onto map 5014
 // (熔岩间, map/magmacave/maze(2,6).map) while quest 4417 [clear map] 5014 ([dungeon info] 34 2)
 // is active; bwanga.dgn ([quest connection] 0 4002 -1) replaces the ten-room 141 corridor with a
 // two-cell quest maze whose boss cell (1,0) is map 16020 ((1,0)maze0_bwanga.map: 56035 tagged
 // [boss] among four 61450 normals) while quest 4002 [hunt enemy] 141 0 56035 1 1 is active.
 internal bool MagmaQuestRunFG214;
 // FG215 quest 4408 (新手冒险家, dungeon 32 蜘蛛洞穴): the [clear map] row is [4421, 411] -- target
 // map 4421 (the quest-maze boss room, map.lst marpissalair/b4402(5,3).map) plus companion APC 411
 // (无谋的剑士, map.lst apc(1,2).map [ai character]) -- the same [targetMap, companionApcId] shape
 // as the GSD rows 4307/4308/4312 ([1619,201]/[1820,202]/[2408,202]). The dgn quest maze
 // ([quest connection] 0 4408 1, [boss map specification] 5 3 4421, [dungeon info] 32 1) is
 // honoured while the quest is accepted at difficulty>=1 by rerouting the boss cell to 4421 below;
 // the companion rides the ApcMonstersFG side channel ({32:{4421,411,25,5}}). Without the reroute
 // CurrentMap can never be 4421 and the [clear map] row can never complete.
 internal bool SwordmanRunFG215;
 // FG362 quest-gated rooms whose map no `.dgn` block references (2026-10-27 sweep):
 //   d60 quest 3912 (要员009, [common unique]) names ten 10601 `[monster] [boss]` actors that the
 //       PVF keeps in map 11124 (Odesa2/odesa_agent.map, [dungeon] 60); no d60 maze block lists
 //       11124, so the row could never advance. While 3912 is accepted at warrior+ the room
 //       replaces a normal cell, and the ten APCs ride the side channel (FG362.Apcs).
 //   d86 quest 5844 / d87 quest 5874 (小灯笼) name 61194, which only exists in the "congcong"
 //       alternate boss rooms ((0,0)boss_congcong / (10,0)Boss_congcong). d87's [boss map
 //       specification] lists 15032 as one of seven candidates; d86 references the (0,0) variant
 //       through the quest dialog. At king+ the boss cell uses that room so the boss exists.
 internal bool OdesaAgentRunFG362,CongcongBossRunFG362;internal ushort CongcongBossMapFG362;
 // d32 quest 4421 (帕丽丝找你 2/2, [dungeon info] 32 3): the 4421 maze block's start cell is the
 // "start" greed symbol; its quest-specific room is map 4426 (MarpissaLair/perisstart(4,0).map)
 // whose `[ai character]` 5202 帕丽丝 is the companion the condition text requires.
 internal bool ParisRunFG362;
 internal const ushort GsdUidFG130=900;
 internal void ConfigureGsdFG130(Offline70ProgressionStateFG state){ConfigureGblQuestFG171(state);SelectQuestMazeFG361(state);TowerRescueFG156=DungeonId==11&&state.Find(4601)!=null;
  // FG205 [appear map] companions, same shape as TowerRescue: a quest-specific room reroute so a
  // [clear map] row whose target map is not in the base maze can still be walked. 5729 (敌人的噩梦
  // 之夜, dungeon 83 night-raid camp 16337, [delete npc index] only) and 1475 (失踪的夏洛克, dungeon
  // 50 fallent-thieves rescue room 6220, client [appear map] 50 -1 6220 100). The rooms sit off the
  // start/boss path; dungeon 83 ships no monster pool, so its companion room is empty by nature.
  // FG384: the reroute is a fallback, never a second placement. d83's own 5729 [quest connection]
  // block (Offline70QuestMazeFG361, maze index 2) already draws 16337 at (1,0); while that block
  // was picked the (2,1) reroute also turned the ordinary 16326 room into 16337, and both cells
  // spawn map 16337's [ai character] 10628 沙影·贝利特 from CurrentMap - two Belits in one run
  // (player report 2026-10-29). MazeDrawsRoomFG384 keeps the reroute only for mazes that lack it.
  QuestRoomRunFG205=false;QuestRoomMapFG205=QuestRoomQuestFG205=0;QuestRoomXFG205=QuestRoomYFG205=0;
 MagmaQuestRunFG214=DungeonId==34&&Offline70QuestRulesFG365.Meets(2,Difficulty)&&state.Find(4417)!=null;
 SwordmanRunFG215=DungeonId==32&&Offline70QuestRulesFG365.Meets(1,Difficulty)&&state.Find(4408)!=null;
 OdesaAgentRunFG362=DungeonId==60&&Offline70QuestRulesFG365.Meets(3,Difficulty)&&state.Find(3912)!=null;
 CongcongBossRunFG362=false;CongcongBossMapFG362=0;
 if(Offline70QuestRulesFG365.Meets(3,Difficulty)&&DungeonId==86&&state.Find(5844)!=null){CongcongBossRunFG362=true;CongcongBossMapFG362=15042;}
 else if(Offline70QuestRulesFG365.Meets(3,Difficulty)&&DungeonId==87&&state.Find(5874)!=null){CongcongBossRunFG362=true;CongcongBossMapFG362=15032;}
 ParisRunFG362=DungeonId==32&&Offline70QuestRulesFG365.Meets(3,Difficulty)&&state.Find(4421)!=null&&MazeFG361!=null&&MazeFG361.Quest==4421;
 // FG211 hell-party variant bosses: the [hunt enemy] rows of 2603/2613/2619/2622 name deep-freak
 // bosses (6xxxx, monster.lst-verified .mob ids) that natively appear only inside a hell party.
 // The offline build has no hell mode, so while such a quest is active the variant boss also
 // stands in that dungeon's boss room at king/hero difficulty (the row demands difficulty>=3;
 // below that the companion stays away so ordinary runs are unchanged). Kind 3: the room cannot
 // settle while the variant lives.
 HellBossRunFG211=false;HellBossMonsterFG211=HellBossLevelFG211=0;
 if(Offline70QuestRulesFG365.Meets(3,Difficulty))foreach(ushort[] row in HellBossQuestsFG211)if(row[1]==DungeonId&&state.Find(row[0])!=null){HellBossRunFG211=true;HellBossMonsterFG211=row[2];HellBossLevelFG211=(ushort)Offline70ClearExperienceFG116.Basis(DungeonId);break;}
 // FG211 hell APC companion: 5310 (JangSamRang, aicharacter/fighter/jangsamrang, level 52)
 // is the hell-party variant boss of dungeon 140 named by 2610/2619. She has no .mob, so she
 // rides the APC side channel (no drops, APC kill credit feeds the [hunt enemy] row) and only
 // stands in the boss room while a naming quest is active at king/hero difficulty.
 HellApcRunFG211=false;HellApcIdFG211=HellApcLevelFG211=0;
 if(Offline70QuestRulesFG365.Meets(3,Difficulty)&&DungeonId==140&&(state.Find(2610)!=null||state.Find(2619)!=null)){HellApcRunFG211=true;HellApcIdFG211=5310;HellApcLevelFG211=52;}
 // FG211 monster companions: 26 wants the silver-clown monsters 61438x5/61437x8/61442x10 of d83 -
 // they spawn as plain roster rows while the quest is active instead of waiting for an
 // object-destroy feed. The other half of this row, d43+4522 (Sasquatch 61707 in the start room),
 // was dropped on 2026-10-26: the native objective of 4522 is "通关白色废墟" (epic_44_whiteland_1_3
 // [condition message]) and the real boss 61707 already feeds the hunt through KillMonster's
 // KillWithAssetsFG171, so the start-room companion only showed a lord-grade ape in room one
 // (user report #11) and let the hunt finish without the run.
 CompanionRowsRunFG211=DungeonId==83&&state.Find(26)!=null;
  if(DungeonId==83&&state.Find(5729)!=null&&!MazeDrawsRoomFG384(16337)){QuestRoomRunFG205=true;QuestRoomQuestFG205=5729;QuestRoomMapFG205=16337;QuestRoomXFG205=2;QuestRoomYFG205=1;}
  if(DungeonId==50&&state.Find(1475)!=null&&!MazeDrawsRoomFG384(6220)){QuestRoomRunFG205=true;QuestRoomQuestFG205=1475;QuestRoomMapFG205=6220;QuestRoomXFG205=2;QuestRoomYFG205=1;}GsdRunFG130=GsdSpawnedFG130=GsdDeadFG130=false;GsdQuestInstanceFG130=null;GsdQuestFG135=GsdApcFG135=GsdStartMapFG135=0;if(Difficulty<(DungeonId==15?2:3))return;ushort quest=(ushort)(DungeonId==11?4307:DungeonId==12?4308:DungeonId==15?4312:0);Offline70QuestActiveFG q=state.Find(quest);if(quest==0||q==null)return;GsdQuestFG135=quest;GsdApcFG135=(ushort)(quest==4307?201:202);GsdStartMapFG135=(ushort)(quest==4307?1618:quest==4308?1819:2421);GsdRunFG130=true;GsdQuestInstanceFG130=q.Instance;X=(byte)(quest==4312?1:0);Y=(byte)(quest==4307?1:quest==4308?0:5);}
 internal static readonly ushort[][] HellBossQuestsFG211=new ushort[][]{new ushort[]{2603,44,61704},new ushort[]{2613,80,61709},new ushort[]{2613,81,61112},new ushort[]{2613,82,61710},new ushort[]{2619,111,61108},new ushort[]{2619,112,61109},new ushort[]{2622,86,61192},new ushort[]{2622,87,61223}};
internal bool HellBossRunFG211;internal ushort HellBossMonsterFG211,HellBossLevelFG211;
internal bool HellApcRunFG211;internal ushort HellApcIdFG211,HellApcLevelFG211;
internal bool CompanionRowsRunFG211;
// FG362 native per-run room randomization: a .dgn cell whose [map specification] lists several
// candidate maps is rolled once per run (CBattle_Field::SetGridPath). The roll is cached per run
// and cell because MapFG130 is asked repeatedly (condition-monster candidates, CurrentMap per
// packet, card room count); RunId changes on every Begin(), so a cache hit from a previous run is
// never reused.
internal readonly Dictionary<int,KeyValuePair<string,ushort>> RandomMazePickFG362=new Dictionary<int,KeyValuePair<string,ushort>>();
ushort RandomMazeMapFG362(byte x,byte y){
 // Native CBattle_Field::get_candidator_map (rednf BattleMonsterDepsC2/get_candidator_map.inc) has a
 // separate branch for the run's end cell: it consults [boss map specification] / the boss-typed
 // room list, never the cell's [map specification]. BaseCandidates is the [map specification] list,
 // so a boss cell that also ships one (d4 (0,0) = 415/413, d6/d8 ...) used to be randomized into an
 // ordinary room and the dungeon's boss never spawned (live report 2026-10-28 "进入boss房间却没有boss").
 if(x==BossX&&y==BossY&&Offline70ForestFG75.Map(DungeonId,x,y)!=0)return Offline70ForestFG75.Map(DungeonId,x,y);
 ushort[] cands=Offline70RandomMazeFG362.BaseCandidates(DungeonId,(ushort)(x*256+y));
 if(cands==null||cands.Length==0)return Offline70ForestFG75.Map(DungeonId,x,y);
 if(cands.Length==1)return cands[0];
 int key=x*256+y;KeyValuePair<string,ushort> hit;
 if(RandomMazePickFG362.TryGetValue(key,out hit)&&hit.Key==RunId)return hit.Value;
 int draw=Offline70QuestDropsFG.StableDraws(RunId+"-random-maze-"+x+"-"+y+"-"+cands[0])(cands.Length);
 ushort map=cands[draw];RandomMazePickFG362[key]=new KeyValuePair<string,ushort>(RunId,map);return map;}
 // FG384: true when the picked maze block already draws `map` on one of its cells. See the FG205
 // reroute above: a map that is already part of the maze must not be rerouted onto a second cell,
 // or its [ai character] actors spawn once per cell.
 bool MazeDrawsRoomFG384(ushort map){if(MazeFG361==null||map==0)return false;foreach(ushort m in MazeFG361.Maps)if(m==map)return true;return false;}
ushort MapFG130(byte x,byte y){ushort gbl=GblMapFG171(x,y);if(gbl!=0)return gbl;if(TowerRescueFG156&&DungeonId==11&&x==2&&y==1)return 1614;if(QuestRoomRunFG205&&x==QuestRoomXFG205&&y==QuestRoomYFG205)return QuestRoomMapFG205;if(MagmaQuestRunFG214&&DungeonId==34&&x==2&&y==6)return 5014;if(SwordmanRunFG215&&DungeonId==32&&x==BossX&&y==BossY)return 4421;if(OdesaAgentRunFG362&&DungeonId==60&&x==MazeFG361.StartX&&y==MazeFG361.StartY)return 11124;if(CongcongBossRunFG362&&x==BossX&&y==BossY)return CongcongBossMapFG362;if(ParisRunFG362&&x==MazeFG361.StartX&&y==MazeFG361.StartY)return 4426;if(GsdRunFG130&&DungeonId==15&&x==1&&y==5)return 2421;if(GsdRunFG130&&DungeonId==12){if(x==0&&y==0)return 1819;if(x==1&&y==0)return 1821;if(x==0&&y==1)return 1820;if(x==1&&y==2)return 1807;}if(GsdRunFG130&&DungeonId==11){if(x==0&&y==0)return 1602;if(x==0&&y==1)return 1618;if(x==1&&y==0)return 1620;if(x==0&&y==2)return 1619;}if(MazeFG361!=null)return MazeMapFG361(x,y);return RandomMazeMapFG362(x,y);}
 int MaskFG130(byte x,byte y){if(GsdRunFG130&&DungeonId==12){if(x==0&&y==0)return 1;if(x==1&&y==0)return 13;if(x==0&&y==1)return 8;}if(GsdRunFG130&&DungeonId==11){if(x==0&&y==0)return 9;if(x==0&&y==1)return 2;if(x==1&&y==0)return 13;}if(MazeFG361!=null)return MazeMaskFG361(x,y);return Offline70ForestFG75.Mask(DungeonId,x,y);}
 bool CanMoveFG130(byte x,byte y){int dx=(int)x-X,dy=(int)y-Y;if(Math.Abs(dx)+Math.Abs(dy)!=1||MapFG130(x,y)==0)return false;int from=dx==1?1:dx==-1?4:dy==1?8:2,to=dx==1?4:dx==-1?1:dy==1?2:8;return (MaskFG130(X,Y)&from)!=0&&(MaskFG130(x,y)&to)!=0;}
 ushort[][] GsdMonstersFG130(){
  // FG362 quest-gated rooms replace one cell of an FG361 base block, so their roster has to be
  // answered before MazeRowsFG361() (which is keyed on the block's pinned map).
  if(OdesaAgentRunFG362&&DungeonId==60&&CurrentMap==11124)return new ushort[][]{};   // content is ten APC 10601
  if(ParisRunFG362&&CurrentMap==4426){ushort[][] paris=Offline70RandomMazeFG362.Roster(4426);if(paris!=null)return paris;}
  if(CongcongBossRunFG362&&CurrentMap==CongcongBossMapFG362){ushort[][] cong=Offline70RandomMazeFG362.Roster(CurrentMap);if(cong!=null)return cong;}
  // FG365 added a hardcoded 56716 row here because the elevator room was empty; FG375 later
  // generated the same `[special passive object] 1112 -> [monster] 56716 67 5 5 -1` row into
  // special-spawns-FG375.dat (native CMap::set_map inserts it with the object ordinal). Keeping
  // both spawned TWO cyclopses (uid 100 + 101): killing the visible one left the room unclear and
  // every op48 move died on "Dungeon room admission" (live 2026-10-29 16:22-16:24). The FG375
  // table is the native path, so the hardcoded row must not exist.
 ushort[][] mazeRows=MazeRowsFG361();if(mazeRows!=null)return mazeRows;ushort[][] gbl=GblMonstersFG171();if(gbl!=null)return gbl;
  // FG362: a randomized room's roster belongs to the map the run rolled, not to the Forest cell
  // (Forest-Content-FG75.Monsters is keyed on the cell's pinned map). The generated table carries
  // exactly the PVF [monster] rows of every candidate map, so lookups stay evidence-bound.
  ushort[][] randomRows=Offline70RandomMazeFG362.Roster(CurrentMap);if(randomRows!=null)return randomRows;
  // Quest-maze rosters: the client builds these rooms from the maze0 map files, which the
  // Forest rosters (keyed on the base map ids) never see.
  if(MagmaQuestRunFG214&&DungeonId==34&&CurrentMap==5014)return new ushort[][]{new ushort[]{601,43,0},new ushort[]{50034,43,0},new ushort[]{50033,43,0},new ushort[]{601,43,0},new ushort[]{50033,43,0},new ushort[]{50033,43,0},new ushort[]{601,43,0},new ushort[]{50033,43,0},new ushort[]{601,43,0},new ushort[]{50033,43,0},new ushort[]{50033,43,0},new ushort[]{50034,44,0}};
  // FG215 quest 4408 boss room 4421: marpissalair/b4402(5,3).map [monster] rows verbatim
  // (id, level=[basis level] 41 + row delta, kind from the trailing tag; 422 is the `[boss]`).
  if(SwordmanRunFG215&&DungeonId==32&&CurrentMap==4421)return new ushort[][]{new ushort[]{421,41,0},new ushort[]{411,42,0},new ushort[]{411,42,0},new ushort[]{420,41,0},new ushort[]{600,41,0},new ushort[]{422,44,3},new ushort[]{600,41,0},new ushort[]{420,41,0},new ushort[]{411,42,0},new ushort[]{411,42,0},new ushort[]{421,41,0}};
  if(TowerRescueFG156&&CurrentMap==1614)return new ushort[][]{new ushort[]{51,19,0},new ushort[]{50,19,0},new ushort[]{50,19,0},new ushort[]{50,19,0},new ushort[]{70,19,0},new ushort[]{51,19,0},new ushort[]{72,19,0},new ushort[]{72,19,0}};
  if(HellBossRunFG211&&CurrentMap==MapFG130(BossX,BossY)){ushort[][] room=Offline70ForestFG75.Monsters(DungeonId,X,Y);ushort[][] all=new ushort[room.Length+1][];Array.Copy(room,all,room.Length);all[room.Length]=new ushort[]{HellBossMonsterFG211,HellBossLevelFG211,3};return all;}
if(CompanionRowsRunFG211){ushort[][] extra=CompanionRowsFG211();if(extra!=null){ushort[][] room=Offline70ForestFG75.Monsters(DungeonId,X,Y);ushort[][] all=new ushort[room.Length+extra.Length][];Array.Copy(room,all,room.Length);Array.Copy(extra,0,all,room.Length,extra.Length);return all;}}
if(QuestRoomRunFG205&&CurrentMap==QuestRoomMapFG205){if(QuestRoomQuestFG205==1475)return new ushort[][]{new ushort[]{1073,51,0},new ushort[]{1070,51,0},new ushort[]{1073,51,0},new ushort[]{1071,51,0},new ushort[]{1073,51,0}};return new ushort[][]{};}if(!GsdRunFG130)return null;switch(CurrentMap){
  case 2421:return new ushort[][]{new ushort[]{37,28,0},new ushort[]{38,28,0},new ushort[]{37,28,0},new ushort[]{37,28,0},new ushort[]{38,28,0}};
 case 1807:return new ushort[][]{new ushort[]{37,21,0},new ushort[]{37,21,0},new ushort[]{37,21,0},new ushort[]{37,21,0},new ushort[]{37,21,0},new ushort[]{37,21,0}};
  case 1819:return new ushort[][]{new ushort[]{81,22,0},new ushort[]{81,22,0},new ushort[]{81,22,0},new ushort[]{81,22,0}};
 case 1820:return new ushort[][]{new ushort[]{39,22,0},new ushort[]{80,22,0},new ushort[]{40,23,3},new ushort[]{84,22,0},new ushort[]{39,22,0},new ushort[]{82,22,0},new ushort[]{84,22,0},new ushort[]{81,22,0},new ushort[]{39,22,0},new ushort[]{80,22,0}};
 case 1821:return new ushort[][]{new ushort[]{84,22,0},new ushort[]{81,22,0},new ushort[]{39,22,0},new ushort[]{81,22,0},new ushort[]{39,22,0},new ushort[]{38,22,0},new ushort[]{38,22,0},new ushort[]{39,22,0},new ushort[]{84,22,0}};
 case 1618:return new ushort[][]{new ushort[]{70,20,0},new ushort[]{70,20,0},new ushort[]{72,20,0},new ushort[]{72,20,0},new ushort[]{70,20,0},new ushort[]{72,20,0},new ushort[]{72,20,0},new ushort[]{70,20,0}};
 case 1619:return new ushort[][]{new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{50,20,0},new ushort[]{70,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{72,20,0},new ushort[]{51,20,0},new ushort[]{50,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{50,20,0},new ushort[]{51,20,0},new ushort[]{50,20,0},new ushort[]{71,21,3},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{72,20,0},new ushort[]{70,20,0}};
 case 1620:return new ushort[][]{new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{51,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0},new ushort[]{50,20,0}};
 case 1602:return Offline70ForestFG75.Monsters(11,1,0);default:return null;}}
 // Quest 26 (杂技爱好者) companion distribution across d83's eleven rooms after the start cell
// (reading order): ranks 0..4 carry all three clown ids, 5..7 drop 61438, 8..9 drop 61437 too,
// the boss cell carries none - totals are exactly the quest goals 5/8/10.
ushort[][] CompanionRowsFG211(){
if(DungeonId!=83)return null;switch(CurrentMap){
case 16320:case 16322:case 16323:case 16324:case 16325:return new ushort[][]{new ushort[]{61438,63,0},new ushort[]{61437,63,0},new ushort[]{61442,63,0}};
case 16326:case 16327:case 16328:return new ushort[][]{new ushort[]{61437,63,0},new ushort[]{61442,63,0}};
case 16329:case 16330:return new ushort[][]{new ushort[]{61442,63,0}};
default:return null;}}
internal bool GsdAliveFG130 {get{return GsdRunFG130&&GsdSpawnedFG130&&!GsdDeadFG130;}}
 internal bool DieGsdFG130(ushort uid){if(uid!=GsdUidFG130||!Active||!Loaded||!GsdRunFG130||!GsdSpawnedFG130||ClearPublished)throw new InvalidDataException("GSD death outside spawned quest run");if(GsdDeadFG130)return false;GsdDeadFG130=true;return true;}
}
partial class Offline70BatchServiceFG {
 void MapClearFG130(){progress=quests.MapVisit(questPath,dungeon.CurrentMap,dungeon.RoomReceiptScope+"-map-clear",dungeon.DungeonId,dungeon.Difficulty,dungeon.GsdAliveFG130,dungeon.GsdQuestInstanceFG130);}
}
