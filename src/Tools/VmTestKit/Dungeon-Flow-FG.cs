using System;using System.Collections.Generic;using System.IO;using System.Xml;using System.Xml.Serialization;
// Current70 offline diagnostic Lorien route: (0,0) map3, (0,1) map5, (0,2) map13.
// Exact current-PVF populations are retained. It is not an all-dungeon catalog.
partial class Offline70DungeonFlowFG {
 // Explicit diagnostic awards for the bounded Lorien route. These are not the
 // production PVF/server clear-reward formula and must not be presented as one.
 internal const uint DiagnosticClearExperience=100,DiagnosticClearGold=50;
 internal ushort DungeonId=1;internal string RunId;internal byte X,Y,Difficulty;internal bool Active,Loaded,ClearPublished;
 bool movePending;
 readonly HashSet<byte> visitedRooms=new HashSet<byte>();
 // Boss room entrance condition (PVF [boss room entrance condition], native check_boss_map):
 // one status per hunted monster id. Native SetGridPath picks spawnCnt random candidate rooms
 // at dungeon start (candidate = every candidate map of that cell declares the monster) and
 // merges that room's [monster condition] rows into the mob list; a kill of the monster id
 // anywhere increments the counter, and the boss cell refuses entry until every counter
 // reached maxKillCnt (check_boss_map returns 7 otherwise).
 internal sealed class ConditionStatusFG181{internal ushort MonsterId;internal List<int> Cells=new List<int>();internal int MaxKill,CurKill;}
 readonly List<ConditionStatusFG181> conditionStatusFG181=new List<ConditionStatusFG181>();
 internal bool BossRoomUnlockedFG181{get{foreach(ConditionStatusFG181 s in conditionStatusFG181)if(s.CurKill<s.MaxKill)return false;return true;}}
 // Event monsters: PVF [event monster]/[event monster3] rows are rolled once per room and the
 // monster ids are kept per room, so uids stay in the same 100..163 numbering as the room's
 // other actors (native: one shared instance counter). The run total is capped by the table's
 // attempt counts, which is what "1-2 event monsters per dungeon" means in the PVF.
 readonly Dictionary<int,List<ushort>> eventRoomFG174=new Dictionary<int,List<ushort>>();
 int eventSpawnedRunFG174;
 // Native SetGridPath rolls [event monster] only while the repeat event 0x24 is active
 // (CEventManager::GetRepeatEvent(0x24)->IsEventing), and the [event monster3] roster is a
 // China event monster list (chn_golden_goblin_junior). This server has no event manager, so
 // both tables stay off unless DNF70_EVENT_MONSTERS=1 is set explicitly - otherwise every
 // dungeon run happily spawns golden goblins in the first rooms (live report 2026-10-15).
 internal static readonly bool EventMonstersEnabledFG182=Environment.GetEnvironmentVariable("DNF70_EVENT_MONSTERS")=="1";
 internal int DiagnosticFatigueUsed {get{return visitedRooms.Count;}}
 internal int CardClearedRoomsFG125 {get{if(!ReadyToClear)throw new InvalidDataException("Card exploration requires boss clear");return visitedRooms.Count;}}
 internal int CardTotalRoomsFG125 {get{int total=0;if(DungeonId==1)return Maps.Length;if(DungeonId==2){for(byte y=0;y<3;y++)for(byte x=0;x<3;x++)if(Offline70DeepLorienFG72.Exists(x,y))total++;}else for(byte y=0;y<MazeHeightFG361;y++)for(byte x=0;x<MazeWidthFG361;x++)if(MapFG130(x,y)!=0)total++;if(total<1)throw new InvalidDataException("Card maze count");return total;}}
 readonly System.Diagnostics.Stopwatch clearTimer=new System.Diagnostics.Stopwatch();
 readonly System.Diagnostics.Stopwatch settlementTimer=new System.Diagnostics.Stopwatch();
 internal const int AutoReturnSeconds=30;
 internal bool AutoReturnDue {get{return Active&&ClearPublished&&settlementTimer.IsRunning&&settlementTimer.ElapsedMilliseconds>=AutoReturnSeconds*1000;}}
 internal uint ClearMilliseconds {get{return (uint)Math.Min(86400000L,clearTimer.ElapsedMilliseconds);}}
 // Original C03D40 treats any zero member word as end eligibility reason1;
 // 105EF50 then displays resource1DB (a member has zero fatigue). This is
 // independent of card selection. The bounded single-player three-room Lorien
 // uses the persisted ordinary fatigue balance; unused members remain FFFF.
 // Other dungeon costs and premium fatigue remain outside this route.
 internal byte[] EndEligibilityBody(int remaining){if(remaining<0||remaining>188)throw new InvalidDataException("Fatigue eligibility bounds");if(!Active||!ClearPublished||!IsBossRoom)throw new InvalidDataException("End eligibility requires completed Lorien");return new byte[]{1,(byte)remaining,0,255,255,255,255,255,255};}
 internal int VerifiedKillCount,VerifiedChampionCount,VerifiedSuperChampionCount,VerifiedBossCount;
 readonly HashSet<ushort>[] dead=new HashSet<ushort>[256];
 internal Offline70DungeonFlowFG(){for(int i=0;i<dead.Length;i++)dead[i]=new HashSet<ushort>();}
 internal int RoomIndex {get{if(HellRoomActiveFG356)return HellRoomIndexFG356;return DungeonId==1?Y:DungeonId==2?Y*3+X:Y*MazeWidthFG361+X;}}
 internal byte BossX {get{if(HamelinRun)return HamelinChaseFG374[hamelinChaseIdxFG374][0];if(GsdRunFG130&&DungeonId==12)return 0;if(MazeFG361!=null)return MazeFG361.BossX;return DungeonId>=3?Offline70ForestFG75.BossX(DungeonId):(byte)(DungeonId==1?0:2);}}
 internal byte BossY {get{if(HamelinRun)return HamelinChaseFG374[hamelinChaseIdxFG374][1];if(GsdRunFG130&&DungeonId==12)return 1;if(MazeFG361!=null)return MazeFG361.BossY;return DungeonId>=3?Offline70ForestFG75.BossY(DungeonId):(byte)(DungeonId==1?2:1);}}
 internal bool IsBossRoom {get{return X==BossX&&Y==BossY;}}
 // Native28 is native packet 0x1c (the op28 case constructs the 0x1c command object; base ctor
 // 0x410A40 receives opcode 0x1c): the caller prepends put_short(dungeonIdx) + put_byte(type) and
 // MakePacket(0x1c) then starts with put_byte(m_mazeType), followed by m_endX/m_endY. So byte 3
 // is the maze block index the client must build its grid from (see MazeIndexFG361); bytes 4/5
 // are the minimap boss position (same coordinates as combat/clear admission, including re-entry).
 // The rest of native 0x1c is m_hellPosX, m_hellPosY, m_bloodState, m_ridable.GetObjectCnt() and
 // one (m_x,m_y) cell pair per ridable, then put_short(todayLayer) - the 12-byte CN70 body captured
 // for Lorien (Dungeon-Entry-Protocol.cs) is exactly that prefix with every tail field zero, so the
 // ridable cell list shifts todayLayer and nothing else. The client keeps one dedicated minimap
 // sprite for it (Sprite/Interface/Common/Ridable/RidableMinimap.img), which is the only way the
 // player can find the walker in one of d88's four random rooms.
 internal byte[] EntryBody(){
  if(!Active)throw new InvalidDataException("No active dungeon entry");
  List<ushort[]> ridables=SelectedRidablesFG361();
  byte[] body=new byte[12+2*ridables.Count];
  BitConverter.GetBytes(DungeonId).CopyTo(body,0);body[2]=Difficulty;body[3]=(byte)(MazeIndexOverrideFG130>=0?MazeIndexOverrideFG130:MazeIndexFG361);body[4]=BossX;body[5]=BossY;
  body[9]=(byte)ridables.Count;
  for(int i=0;i<ridables.Count;i++){body[10+2*i]=(byte)ridables[i][0];body[11+2*i]=(byte)ridables[i][1];}
  return body;
 }
 // FG409: the native server swaps the room to its `[heroes mode map index]` variant itself.
 // CBattle_Field::getMap (rednf BattleMonsterDepsC2/getMap.inc, ORIG 0x08300ed4) rewrites
 // GetCurrentMapInfo()->m_mapIndex to CMap::m_b4 when get_dungeon_diff()==4, before ConsistMap()
 // builds the roster and before MakePacket(0x1d) tells the client which .map to load. Only the
 // seven [herosmode enable] dungeons can reach difficulty 4 (Hero-Unlock-FG358), and the table
 // only knows their room maps, so every other tier/dungeon stays on BaseMapFG409 unchanged.
 ushort HeroMapFG409(ushort map){return Difficulty==Offline70HeroUnlockFG358.HeroDifficulty?Offline70HeroMapsFG409.Resolve(map):map;}
 internal ushort BaseMapFG409 {get{if(Offline70DungeonFlowFG.TowerRunFG211(DungeonId)&&TowerMapOverrideFG374!=0)return TowerMapOverrideFG374;if(SealDoorOpenFG181&&IsSealDoor(X,Y))return SealDoorMapIdx;return DungeonId==1?Maps[Y]:DungeonId==2?Offline70DeepLorienFG72.Map(X,Y):MapFG130(X,Y);}}
 internal ushort CurrentMap {get{return HeroMapFG409(BaseMapFG409);}}
 // FG374: the CN70 client builds its room-record grid from the dungeon's own [maze info] blocks.
 // A tower .dgn has none, so the ordinary-room path only knows the 2-cell start/boss fallback and
 // its strict cell lookup dies in the secure-CRT funnel on (0,2) (live 2026-10-28 21:37:44 /
 // 22:11:23 dumps: 0x48B0DA -> 0x491804 -> 0xF2B04B -> 0xBC88B2 -> WndProc). The tower therefore
 // keeps its own floor + map and only alternates the client-visible cell between those two cells.
 internal ushort TowerMapOverrideFG374;internal int TowerFloorFG374;
 internal int CurrentCount {get{return RoomMonstersFG118().Length;}}
 // Total monster count including condition and event monsters.
 internal int TotalCount {get{int total=CurrentCount;total+=CurrentConditionMonsters().Count;total+=EventIdsForCurrentRoomFG174().Count;return total;}}
 internal ushort[] Monster(ushort uid){int i=uid-100;if(i>=0&&i<CurrentCount)return RoomMonstersFG118()[i];if(i>=CurrentCount){int ci=i-CurrentCount;var conds=CurrentConditionMonsters();if(ci>=0&&ci<conds.Count){var c=conds[ci];return new ushort[]{c[1],c[2],c[3]};}var evs=EventIdsForCurrentRoomFG174();if(i>=0&&i-CurrentCount-CurrentConditionMonsters().Count<evs.Count){return new ushort[]{evs[i-CurrentCount-CurrentConditionMonsters().Count],(byte)Offline70ClearExperienceFG116.Basis(DungeonId),(byte)0};}}throw new InvalidDataException("Room monster bounds");}
 // Condition monsters spawning in the current room: the room's own [monster condition] rows,
 // filtered to the run's rolled spawn rooms (native add_monster_list_bossroom_condition).
 internal List<ushort[]> CurrentConditionMonsters(){
  List<ushort[]> result=new List<ushort[]>();
  if(!Active||DungeonId<3||conditionStatusFG181.Count==0)return result;
  List<ushort[]> declared=ConditionMonstersFG.GetConditionMonsters(DungeonId,CurrentMap);
  if(declared.Count==0)return result;
  int cell=Y*MazeWidthFG361+X;
  foreach(ConditionStatusFG181 status in conditionStatusFG181){
   int instances=0;foreach(int c in status.Cells)if(c==cell)instances++;
   for(int k=0;k<instances;k++)foreach(ushort[] row in declared)if(row[1]==status.MonsterId)result.Add(row);
  }
  return result;
 }
 // Native SetGridPath: candidates are non-start/non-boss cells whose maps declare the hunted
 // monster; spawnCnt of them are drawn at random (duplicates allowed, as in the original).
 void BuildConditionStatusFG181(){
  conditionStatusFG181.Clear();
  if(DungeonId<3)return;
  ushort[][] hunt=ConditionMonstersFG.GetHuntConditions(DungeonId);
  if(hunt.Length==0)return;
  int width=MazeWidthFG361,height=MazeHeightFG361;
  byte startX=MazeStartXFG361,startY=MazeStartYFG361;
  Random roll=new Random(unchecked(Environment.TickCount*17+DungeonId*101+Difficulty));
  foreach(ushort[] triple in hunt){
   ConditionStatusFG181 status=new ConditionStatusFG181{MonsterId=triple[0],MaxKill=triple[2]};
   List<int> candidates=new List<int>();
   for(byte y=0;y<height;y++)for(byte x=0;x<width;x++){
    if(x==BossX&&y==BossY||x==startX&&y==startY)continue;
    ushort map=MapFG130(x,y);
    if(map==0)continue;
    if(ConditionMonstersFG.DeclaresMonster(DungeonId,map,status.MonsterId))candidates.Add(y*width+x);
   }
   int spawnCount=Math.Max(1,(int)triple[1]);
   for(int k=0;k<spawnCount&&candidates.Count>0;k++)status.Cells.Add(candidates[roll.Next(candidates.Count)]);
   conditionStatusFG181.Add(status);
  }
 }
 internal bool BossConditionGateFG181(byte x,byte y){return x==BossX&&y==BossY&&!BossRoomUnlockedFG181;}
 internal bool ConditionMonstersCleared(){return BossRoomUnlockedFG181;}
 // Event monster generation: roll probability and spawn if successful.
 // Returns list of spawned event monster UIDs (empty if none spawned).

 internal List<ushort> RollEventMonsters(){
  var spawned=new List<ushort>();
  if(!Active)return spawned;   // the room packet is composed before the client confirms the load
  if(!EventMonstersEnabledFG182)return spawned;   // event monsters need an active event (native 0x24)
  if(eventRoomFG174.ContainsKey(RoomIndex))return eventRoomFG174[RoomIndex];   // once per room
  // PVF [event monster] row: <attempts> <triggerProb/10000> then tuples
  // <eventIdx> <spawnCount> <weight> <field0c> <field10> (rednf STDungeonScript 0x766 read order;
  // 61xxx is the monster id the client spawns for that entry, spawnCount is m_monsterIdx).
  Random roll=new Random(unchecked(Environment.TickCount*31+DungeonId*7+RoomIndex));
  int cap=EventCapFG174();
  // Native add_monster_event caps the room at the room's own monster count (spawned<m_monsterMap.size()).
  int roomCap=Math.Min(cap,CurrentCount);
  foreach(ushort[][] table in new ushort[][][]{EventMonstersFG.EventTable.ContainsKey(DungeonId)?EventMonstersFG.EventTable[DungeonId]:new ushort[0][],
                                               EventMonstersFG.EventTable3.ContainsKey(DungeonId)?EventMonstersFG.EventTable3[DungeonId]:new ushort[0][]}){
   foreach(ushort[] row in table){
    if(row.Length<7)continue;
    int attempts=row[0],trigger=row[1],spawnCount=Math.Max(1,(int)row[3]);
    for(int attempt=0;attempt<attempts;attempt++){
     if(eventSpawnedRunFG174>=cap||spawned.Count>=roomCap)break;
     if(roll.Next(10000)>=trigger)continue;
     for(int i=0;i<spawnCount&&eventSpawnedRunFG174<cap&&spawned.Count<roomCap;i++){spawned.Add(row[2]);eventSpawnedRunFG174++;}
    }
   }
  }
  eventRoomFG174[RoomIndex]=spawned;
  return spawned;
 }
 int EventCapFG174(){
  int cap=0;
  foreach(Dictionary<ushort,ushort[][]> table in new Dictionary<ushort,ushort[][]>[]{EventMonstersFG.EventTable,EventMonstersFG.EventTable3}){
   ushort[][] rows;
   if(!table.TryGetValue(DungeonId,out rows))continue;
   foreach(ushort[] row in rows)if(row.Length>=7)cap+=row[0];
  }
  return cap;
 }
 List<ushort> EventIdsForCurrentRoomFG174(){List<ushort> ids;return eventRoomFG174.TryGetValue(RoomIndex,out ids)?ids:emptyEventsFG174;}
 static readonly List<ushort> emptyEventsFG174=new List<ushort>();
 // Check if a position is a seal door for this dungeon.
 internal bool IsSealDoor(byte x,byte y){
  if(!SealDoorsFG.HasSealDoor(DungeonId))return false;
  return x==SealDoorsFG.GetSealDoorPosX(DungeonId)&&y==SealDoorsFG.GetSealDoorPosY(DungeonId);
 }
 // Get the seal door map index for this dungeon (0 if none).
 internal ushort SealDoorMapIdx{get{return SealDoorsFG.GetSealDoorMapIdx(DungeonId);}}
 // [seal door map index]/[seal door pos]/[seal door appear rate] route the seal door cell into
 // the hell room (kingsruins 60026 / underfoot 60020) when the run rolled the appear rate AND
 // the party entered via the native hell mode (op16 dungeon type 1, see Hell-FG356.cs).
 internal static readonly bool HellPartyModeFG181=false;   // historical constant; runtime state is hellPartyModeFG356
 bool sealDoorOpenFG181;
 internal bool SealDoorOpenFG181{get{return sealDoorOpenFG181;}}
 // Live/e2e test aid: DNF70_HELL_DOOR=1 forces the hell seal door open on hell-mode runs
 // (the native [seal door appear rate] is 1..10%, which makes manual verification tedious).
 internal static readonly bool ForceHellDoorFG356=Environment.GetEnvironmentVariable("DNF70_HELL_DOOR")=="1";
 void RollSealDoorFG181(){
  sealDoorOpenFG181=false;
  if(!SealDoorsFG.HasSealDoor(DungeonId))return;
  int rate=SealDoorsFG.GetSealDoorRate(DungeonId);
  Random roll=new Random(unchecked(Environment.TickCount*29+DungeonId*13+Difficulty));
  if(hellPartyModeFG356&&(ForceHellDoorFG356||roll.Next(100)<rate))sealDoorOpenFG181=true;
 }
 internal static readonly ushort[] Maps=new ushort[]{3,5,13};internal static readonly int[] Counts=new int[]{4,5,7};
 // FG376: friendly `*_CharacterTeam` actors sit in the roster (the client draws them and runs
 // their scene) but cannot be damaged, so they must never gate the door. Native check_grid_clear
 // only blocks on an ordinary monster; this is the same exemption for the PVF's friendly variants.
 internal bool RoomCleared {get{
  if(!Active)return false;
  int count=TotalCount;
  // FG396: `[special passive object]` box monsters stop gating the door when the room also has
  // its own [monster] rows - a player who never breaks the box must still be able to move on
  // (2026-10-30 report). A room whose entire roster is object-spawned keeps the old rule.
  int spawns=SpecialSpawnCountFG396(RoomIndex);
  int blockingCurrent=spawns>0&&CurrentCount-spawns>0?CurrentCount-spawns:CurrentCount;
  for(ushort uid=100;uid<100+blockingCurrent;uid++){
   if(uid>=100+blockingCurrent)continue;   // condition/event actors are counted below
   if(Offline70NonCombatantsFG376.Is(Monster(uid)[0])||ProtectObjectiveFG379(Monster(uid)[0]))continue;
   if(!dead[RoomIndex].Contains(uid))return false;
  }
  for(ushort uid=(ushort)(100+CurrentCount);uid<100+count;uid++)if(!dead[RoomIndex].Contains(uid))return false;
  return (!(TowerFlowFG211||HellRoomActiveFG356)||ApcAliveUidsFG186().Count==0);}}
 // Towers (2026-10-26): the death/illusion tower floors are a one-cell-wide vertical maze -
 // floor N is cell (0,N-1), the top floor is the boss cell. A tower floor's roster plus its
 // [ai character] APCs are the whole floor content, so a floor is cleared only when every
 // roster monster AND every spawned APC is dead; the top floor settles the run the same way
 // (its boss is the floor APC, e.g. 608 on illusion 15F, not a kind-3 monster row).
 internal static bool TowerRunFG211(ushort id){return id==11000||id==11001;}
 internal bool TowerFlowFG211 {get{return TowerRunFG211(DungeonId);}}
 // The ordinary Lorien route ends on its only boss, not on unreported minions. A maze block
 // that ships its own [clear condition] (FG372) replaces that rule entirely - the native
 // IsClearDungeon() returns CClearCondition::IsClearCondition() and never consults the end point.
 internal bool ReadyToClear {get{return Active&&Loaded&&(HasClearConditionFG372?ClearConditionMetFG372:BossKilled());}}
 internal string RoomReceiptScope {get{return RunId+"-room"+RoomIndex+(TowerRunFG211(DungeonId)?"-floor"+TowerFloorFG374:"");}}
 // FG374c: a tower reuses its two client-visible cells, so every room-scoped economy receipt
 // (kills, drops, objects, pet xp, map-clear credit) must also carry the floor - live 2026-10-29
 // 02:14: floor 4 in cell (0,1) hit floor 2's receipts and every kill reply was "Reused economy
 // receipt with different payload", leaving the floor unclearable.
 internal void Begin(){Begin(DungeonId,Difficulty);}
 internal void Begin(ushort id){Begin(id,0);}
 internal void Begin(ushort id,byte difficulty){Begin(id,difficulty,false,1,0);}
 internal void Begin(ushort id,byte difficulty,bool hellParty,byte hellDifficulty,byte hellEventType){ConfigureHellFG356(hellParty,hellDifficulty,hellEventType);if(id<1||id>2&&!Offline70ForestFG75.Supported(id)||difficulty>Offline70DungeonSelection.TestMaximumDifficulty)throw new InvalidDataException("Unsupported dungeon or difficulty");if(Active)throw new InvalidDataException("A dungeon run is already active");TowerMapOverrideFG374=0;TowerFloorFG374=0;DungeonId=id;Difficulty=difficulty;RunId=Guid.NewGuid().ToString("N");X= id>=3?Offline70ForestFG75.StartX(id):(byte)0;Y=id>=3?Offline70ForestFG75.StartY(id):(byte)(id==1?0:2);Active=true;Loaded=false;movePending=false;ClearPublished=false;clearTimer.Reset();settlementTimer.Reset();visitedRooms.Clear();ResetElitesFG118();VerifiedKillCount=VerifiedChampionCount=VerifiedSuperChampionCount=VerifiedBossCount=0;foreach(HashSet<ushort> set in dead)set.Clear();BuildConditionStatusFG181();RollSealDoorFG181();eventRoomFG174.Clear();eventSpawnedRunFG174=0;SetClearConditionsFG372(null);hamelinChaseIdxFG374=0;LastKillMovedEndFG374=false;ResetElevatorFG389(id);}
 internal void MarkLoaded(){if(!Active||Loaded)throw new InvalidDataException("Dungeon load state");Loaded=true;movePending=false;visitedRooms.Add((byte)RoomIndex);if(!clearTimer.IsRunning)clearTimer.Start();ArmElevatorFG389();}
 internal void ValidateKill(ushort uid){if(!Active||!Loaded||ClearPublished||ReadyToClear)throw new InvalidDataException("Dungeon actor state");if(uid<100||uid>=100+TotalCount)throw new InvalidDataException("Dungeon actor UID");}
 internal bool IsDead(ushort uid){if(!Active)throw new InvalidDataException("Dungeon actor query state");if(uid<100||uid>=100+TotalCount)throw new InvalidDataException("Dungeon actor query UID");return dead[RoomIndex].Contains(uid);}
 // Dungeon 51 迷乱之村哈穆林 (PVF [dummy appear count] 2 + rednf SetGridPath): the boss-cell
 // chain is (1,0) fake 1078 -> (0,0) fake_second 1096 -> (0,1) real 1077. The (0,0)/(0,1) doors
 // unlock only after the previous chain room's kind-3 Peter died, and only the (0,1) room is the
 // boss room (BossX/BossY), so ReadyToClear still fires on exactly one kill.
 // FG374: d51 迷乱之村哈穆林 is a chase. Every `[boss map]` cell is a boss cell and the minimap-end
 // (m_endX/m_endY) walks the chain: the client is told the end at entry (op28 bytes 4/5) and every
 // later move rides the fake boss death packet (rednf CBattle_Field_KillChain/
 // _MakeNotiPacketDieMonster: put_byte(0), apcKillCount, put_byte(m_flag34), and when flag34!=0
 // the new end coordinates). CMap::set_map downgrades a `[type] [dummy]` room's boss row to
 // roleType 1 and sets that flag, so the fake dies as a champion and the run continues.
 // Our maze exposes the boss cells in (1,0)->(0,0)->(0,1) order, so the end starts on the first
 // room the player reaches and only the last cell carries the real kind-3 Peter.
 internal static readonly byte[][] HamelinChaseFG374=new byte[][]{new byte[]{1,0},new byte[]{0,0},new byte[]{0,1}};
 int hamelinChaseIdxFG374;
 // set by Kill() when the killed actor moved the end, read by KillMonster for the death tail
 internal bool LastKillMovedEndFG374;
 internal static bool DummyBossRowFG374(ushort map,ushort monster){return monster==1078&&(map==6418||map==6420||map==6424);}
 internal bool HamelinRun {get{return DungeonId==51;}}
 // ---- FG372 [clear condition] ------------------------------------------------
 // A maze block whose [clear condition] list is non-empty replaces the ordinary "boss killed"
 // rule: rednf CBattle_Field::IsClearDungeon() short-circuits to CClearCondition::
 // IsClearCondition(), and kill_monster / prepare_dungeon_clear feed ClearCondition(type, id)
 // instead of the end-point check. Types (STDungeonScript ImportDungeonScript 0x2a1-0x2ae):
 //   0 [destroy object] object id  count     1 [clear map]      map index (count forced to 1)
 //   2 [hunt monster]   monster id count     3 [hunt apc]       apc id   count
 //   4 [hunt boss]      monster id count
 // CN70 ships three blocks with the tag: d110 quest 3802 ([destroy object] 8500 4 - the GBL
 // bookcases the quest 3802 objective counts), d87 quest 5866 and d140 quest 4102 ([clear map]).
 internal sealed class ClearConditionFG372{internal int Type,Left,Right,Cur;}
 readonly List<ClearConditionFG372> clearConditionsFG372=new List<ClearConditionFG372>();
 internal bool HasClearConditionFG372{get{return clearConditionsFG372.Count>0;}}
 internal void SetClearConditionsFG372(ushort[][] rows){
  clearConditionsFG372.Clear();
  if(rows==null)return;
  foreach(ushort[] row in rows){
   if(row==null||row.Length<3||row[0]>4||row[2]==0)throw new InvalidDataException("Clear condition row");
   clearConditionsFG372.Add(new ClearConditionFG372{Type=row[0],Left=row[1],Right=row[2]});
  }
 }
 // Native CClearCondition::ClearCondition(type,idx) -> true only once EVERY row is satisfied;
 // the caller (kill_monster) then calls ClearDungeon().
 internal bool ClearConditionWantsFG372(ushort id){foreach(ClearConditionFG372 c in clearConditionsFG372)if(c.Type==0&&c.Left==id)return true;return false;}
 internal bool FeedClearConditionFG372(int type,int idx){
  if(clearConditionsFG372.Count==0)return false;
  bool hit=false;
  foreach(ClearConditionFG372 c in clearConditionsFG372)
   if(c.Type==type&&c.Left==idx&&c.Cur<c.Right){c.Cur++;hit=true;}
  return hit&&ClearConditionMetFG372;
 }
 internal bool ClearConditionMetFG372{
  get{if(clearConditionsFG372.Count==0)return false;foreach(ClearConditionFG372 c in clearConditionsFG372)if(c.Cur<c.Right)return false;return true;}
 }
 internal string ClearConditionStateFG372{get{if(clearConditionsFG372.Count==0)return "none";string s="";foreach(ClearConditionFG372 c in clearConditionsFG372)s+=(s.Length>0?" ":"")+"t"+c.Type+":"+c.Left+"="+c.Cur+"/"+c.Right;return s;}}
 internal bool Kill(ushort uid){if(IsDead(uid))return false;ValidateKill(uid);if(!dead[RoomIndex].Add(uid))return false;if(uid>=100+CurrentCount){int ci=uid-100-CurrentCount;List<ushort[]> conditions=CurrentConditionMonsters();if(ci<conditions.Count)foreach(ConditionStatusFG181 status in conditionStatusFG181)if(status.MonsterId==conditions[ci][1]&&status.CurKill<status.MaxKill)status.CurKill++;}VerifiedKillCount++;byte kind=(byte)Monster(uid)[2];LastKillMovedEndFG374=false;bool dummyBossFG374=HamelinRun&&DummyBossRowFG374(CurrentMap,Monster(uid)[0]);if(dummyBossFG374){LastKillMovedEndFG374=true;if(hamelinChaseIdxFG374<HamelinChaseFG374.Length-1)hamelinChaseIdxFG374++;}FeedClearConditionFG372(kind==3?4:2,Monster(uid)[0]);if(kind==1||dummyBossFG374)VerifiedChampionCount++;else if(kind==2)VerifiedSuperChampionCount++;else if(kind==3)VerifiedBossCount++;return true;}
 internal bool IsBoss(ushort uid){return Active&&IsBossRoom&&Monster(uid)[2]==3&&!DummyBossRowFG374(CurrentMap,Monster(uid)[0]);}
 bool BossKilled(){if(BossApcRoomFG176)return BossApcSlainFG176;if(BossApcGateFG361)return BossApcSlainFG361;if(TowerFlowFG211)return IsBossRoom&&RoomCleared;
  // Native IsClearDungeon() (rednf CBattle_Field/IsClearDungeon.inc) is check_grid_clear() &&
  // check_end_point(): the end room only has to be emptied. We keep one extra requirement the
  // native data always satisfies anyway: the end room must actually OWN a boss (a kind-3 row,
  // already dead because the room is cleared). A boss-less end room would otherwise end the run
  // the moment its ordinary monsters died - a player who wanders into the end cell of a dungeon
  // whose boss room failed to resolve would get an instant clear with no boss fight. Rooms whose
  // boss is a role-8 [ai character] are gated above (BossApcGateFG361 / BossApcRoomFG176), and
  // the three mazes that ship [clear condition] never reach this method (ReadyToClear uses the
  // condition branch), so no dungeon loses its clear path.
  if(!IsBossRoom)return false;if(!BossRoomUnlockedFG181)return false;if(!RoomCleared)return false;
  for(ushort uid=100;uid<100+CurrentCount;uid++)if(Monster(uid)[2]==3)return true;
  return false;}
 // FG379: the cleanup pass must obey the same exemptions as RoomCleared - it runs right after
 // ReadyToClear in the service (live report 2026-10-29 01:59: a boss-minion-cleanup death packet
 // for 冰之心 uid 102 landed one tick after 查理's own death packet, because this loop had no
 // exemption and the FG378 sweep had been correctly skipped). Friendly actors and protect
 // objectives must never receive cleanup deaths.
 internal ushort[] CleanupAfterBoss(){if(!ReadyToClear)throw new InvalidDataException("Boss cleanup state");List<ushort> removed=new List<ushort>();for(ushort uid=100;uid<100+CurrentCount;uid++){if(Offline70NonCombatantsFG376.Is(Monster(uid)[0])||ProtectObjectiveFG379(Monster(uid)[0]))continue;if(dead[RoomIndex].Add(uid))removed.Add(uid);}return removed.ToArray();}
 // FG378 (2026-10-28 冰心少年 d41: four toys + 查理 1030 committed, the run never settled).
 // Native settles a boss run through Dispatcher_BossDieCheck (process_40.inc): the client's
 // op120 erases the dead boss from m_apcList and an empty list schedules a 1s timer whose
 // handler CParty::OnTimeoutBossDie (0x085aa5d4) kill_monsters every leftover with
 // m_dropCnt != 0. So native never deadlocks on a leftover the client cannot kill, and the
 // effective rule is "the end room's last kind-3 death settles the run". Our client sends no
 // op120 (zero occurrences in every archived session), so the op42 death of the end room's
 // last kind-3 row is the equivalent trigger. The sweep = CleanupAfterBoss's marking,
 // restricted to the ordinary boss-death flow: no tower, no [clear condition] maze, no
 // APC-boss room, and the end room only - non-end boss cells keep the kill-everything rule,
 // d51 fake rooms fail IsBossRoom because the chase already advanced BossX/BossY past the
 // killer's cell, and FG376 friendly actors stay alive exactly like RoomCleared treats them.
 // FG379 (same evening, player correction): the first sweep also killed 冰之心 1034 - wrong.
 // The client warns on boss-room entry "若冰之心被破坏，则任务失败" and epic_43_
 // theboywithcoldheart_1_5 spells it out ("虽然守住了查理的心脏 - 冰之心"): the heart is the
 // PROTECTED objective of the fight, not a hostile leftover. Native would sweep it (it is a
 // plain [fixed][normal] row there), but the client's own fail-on-destroyed rule wins per the
 // align-with-native protocol, so protect objectives never gate the door and are never swept;
 // the run settles with the heart alive. Death-by-Charlie stays client-local: the client plays
 // its fail sequence and aborts on its own, and no archived session ever reported the heart.
 internal static bool ProtectObjectiveFG379(ushort monster){return monster==1034;}
 internal bool Kind3SweepPendingFG378(ushort uid){
  if(!Active||!Loaded||ClearPublished)return false;
  if(TowerFlowFG211||HasClearConditionFG372||BossApcRoomFG176||BossApcGateFG361)return false;
  if(!IsBossRoom)return false;
  if(uid<100||uid>=100+CurrentCount||Monster(uid)[2]!=3)return false;
  for(ushort u=100;u<100+CurrentCount;u++)if(u!=uid&&Monster(u)[2]==3&&!dead[RoomIndex].Contains(u))return false;
  return !RoomCleared;
 }
 internal List<ushort> SweepLeftoversFG378(){
  List<ushort> removed=new List<ushort>();
  for(ushort u=100;u<100+TotalCount;u++){
   if(u<100+CurrentCount&&(Offline70NonCombatantsFG376.Is(Monster(u)[0])||ProtectObjectiveFG379(Monster(u)[0])))continue;
   if(!dead[RoomIndex].Add(u))continue;
   removed.Add(u);
  }
  return removed;
 }
 internal bool IsPendingMove(byte x,byte y){return Active&&!Loaded&&movePending&&X==x&&Y==y;}
 internal void Move(byte x,byte y){if(!Active||!Loaded||ClearPublished||ReadyToClear||!RoomCleared||!(DungeonId==1?x==0&&y<=2: DungeonId==2?x<=2&&y<=2&&Offline70DeepLorienFG72.Exists(x,y):CanMoveFG130(x,y))||BossConditionGateFG181(x,y)||HamelinGateFG(x,y)||Math.Abs((int)y-Y)+Math.Abs((int)x-X)!=1)throw new InvalidDataException("Dungeon room admission");X=x;Y=y;Loaded=false;movePending=true;}
 // Chain doors: the hub boss room (0,0) opens after the (1,0) fake died, the real boss room
 // (0,1) opens after the (0,0) fake died. Moving backward inside cleared rooms stays free.
 internal bool HamelinGateFG(byte x,byte y){return HamelinRun&&((x==0&&y==0&&hamelinChaseIdxFG374<1)||(x==0&&y==1&&hamelinChaseIdxFG374<2));}
 internal void PublishClear(){if(!ReadyToClear||ClearPublished)throw new InvalidDataException("Dungeon clear state");ClearPublished=true;clearTimer.Stop();}
 // Start only after the client reaches end eligibility (after XP presentation),
 // so the 30-second wait does not cut short the preceding results animation.
 // Repeated eligibility requests must not extend the same run's deadline.
 internal void BeginEndWait(){if(!Active||!ClearPublished)throw new InvalidDataException("End wait outside settled dungeon");if(!settlementTimer.IsRunning)settlementTimer.Start();}
 internal void End(byte option){if(!Active||!ClearPublished||option>3)throw new InvalidDataException("Dungeon end state");if(option==3)return;Active=false;Loaded=false;movePending=false;settlementTimer.Stop();}
 // Voluntary return uses command45, rather than a fabricated pre-clear75 award.
 // This diagnostic abort retains already committed kills/items and grants no
 // clear award; the production fatigue/stamina failure penalty is not modeled.
 internal void Abort(){if(!Active)throw new InvalidDataException("No dungeon to abort");Active=false;Loaded=false;movePending=false;settlementTimer.Stop();}
 internal byte[] RoomBody(){if(!Active)throw new InvalidDataException("No active dungeon");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
  // Original native29 flag0 uses the retained client room, preserving broken objects.
  bool revisit=visitedRooms.Contains((byte)RoomIndex);w.Write(X);w.Write(Y);w.Write((uint)0);w.Write((byte)0);w.Write((byte)0);w.Write((byte)(revisit?0:1));if(revisit)return m.ToArray();
  bool spawnGsd=GsdRunFG130&&!GsdSpawnedFG130&&CurrentMap==GsdStartMapFG135;w.Write(CurrentMap);// Native 0x1d counts m_monsterMap.size(), i.e. the [ai character] APCs that
// add_monster_APC_AI pushed into the same map (roleType 5..8): an APC written
// outside the count is read by the client as the object section and never spawns.
  RollEventMonsters();            // must run before the room packet is composed, otherwise the spawns never reach the client
  var conds=CurrentConditionMonsters();
  // Side-channel APCs of this room (generated ApcMonstersFG rows, see Act4-Boss-Apc-FG176.cs). They
  // count towards the native m_monsterMap.size() byte, which is what makes the client spawn them,
  // but never towards TotalCount: their uids must stay outside ValidateKill's monster range so no
  // drop plan (MapRolls/MonsterPools) is ever attempted for an actor that has no .mob profile.
  var apcs=CurrentApcMonsters();int pendingApcsFG186=0;for(int ai=0;ai<apcs.Count;ai++)if(ApcPendingFG186(ApcUidFG186(ai)))pendingApcsFG186++;
w.Write((byte)(TotalCount-dead[RoomIndex].Count+(spawnGsd?1:0)+pendingApcsFG186));  // both APC slots count (native m_monsterMap.size())
  // FG386: a `[special passive object]` `[monster]` action row carries the owner ordinal in a 4th
  // column (Special-Spawns-FG375.cs). Native sends that actor as roleType 0 with m_field13=1
  // (m_champion) and m_field14=area (m_posX) and m_mobId=m_orderIdx=0 (insert_monster cleared the
  // assign struct) - the two tail bytes are what bind the monster to the object that spawns it, and
  // map 17045 has no [monster] rows to place it by. Writing the FG375 shape (role 1 / 0 / 0) left
  // the whole 根特防御战 boss room empty on the live client.
  for(ushort i=0;i<CurrentCount;i++){ushort uid=(ushort)(100+i);if(dead[RoomIndex].Contains(uid))continue;ushort[] monster=Monster(uid);bool special=monster.Length>3;
   w.Write(i);w.Write((uint)(special?0:i));w.Write(uid);w.Write(monster[0]);w.Write((byte)monster[1]);w.Write((byte)monster[2]);w.Write((byte)(special?1:0));w.Write((byte)(special?monster[3]:0));}
  // Condition monsters: extra monsters that must be killed before the boss spawns.
  for(int ci=0;ci<conds.Count;ci++){ushort uid=(ushort)(100+CurrentCount+ci);if(dead[RoomIndex].Contains(uid))continue;var c=conds[ci];w.Write((ushort)(CurrentCount+ci));w.Write((uint)(CurrentCount+ci));w.Write(uid);w.Write(c[1]);w.Write((byte)c[2]);w.Write((byte)c[3]);w.Write((byte)0);w.Write((byte)0);}
  // Event monsters (native add_monster_event): the wire mobId is the 10000+ instance base
  // (that range is what marks an event monster), mobIndex is the [event monster] entry value
  // and the level is the dungeon's standard level. Group index 0 mirrors the native memset.
  var evs=EventIdsForCurrentRoomFG174();for(int xi=0;xi<evs.Count;xi++){ushort uid=(ushort)(100+CurrentCount+conds.Count+xi);if(dead[RoomIndex].Contains(uid))continue;w.Write((ushort)0);w.Write((uint)(10000+xi));w.Write(uid);w.Write(evs[xi]);w.Write((byte)Offline70ClearExperienceFG116.Basis(DungeonId));w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);}
  if(spawnGsd){w.Write((ushort)0);w.Write((uint)0);w.Write(GsdUidFG130);w.Write(GsdApcFG135);w.Write((byte)84);w.Write((byte)5);w.Write((byte)0);w.Write((byte)0);GsdSpawnedFG130=true;}for(int ai=0;ai<apcs.Count;ai++){ushort apcUidFG186=ApcUidFG186(ai);if(!ApcPendingFG186(apcUidFG186))continue;ushort[] apcRowFG186=apcs[ai];w.Write((ushort)0);w.Write((uint)0);w.Write(apcUidFG186);w.Write(apcRowFG186[1]);w.Write((byte)apcRowFG186[2]);w.Write(ApcRoleByteFG186(apcRowFG186));w.Write((byte)0);w.Write((byte)0);ApcRegisterSpawnFG186(apcUidFG186,apcRowFG186);}w.Write((byte)0);w.Write((byte)0);return m.ToArray();
 }}
 // FG396: the CN70 client reads the settlement panel's 操作 (operation) grade from the byte
 // after 被击数. The op49 request carries style (u32 1..7) at b4 and technique (u32 0..4) at b8;
 // echoing the style back here makes the panel show the operation grade the run actually earned
 // instead of the constant 0 (=F) the seven-byte body produced (2026-10-30 report: 操作评分恒F).
 // The 被击数 at b6 was already verified against the panel, so its offset is untouched.
 internal static byte[] ResultBody(byte rank,uint clearMilliseconds,byte technic,byte hit){return ResultBody(rank,clearMilliseconds,technic,hit,0);}
 internal static byte[] ResultBody(byte rank,uint clearMilliseconds,byte technic,byte hit,byte style){if(rank>105||clearMilliseconds>86400000)throw new InvalidDataException("Clear score bounds");byte[] b=new byte[8];b[0]=rank;BitConverter.GetBytes(clearMilliseconds).CopyTo(b,1);b[5]=technic;b[6]=hit;b[7]=style;return b;}
 // Native35 four-member projection with one local free gold card. The displayed
 // gold is the diagnostic clear award already committed by Clear(), not a new
 // grant. No paid card is offered. Native readers consume all91 logical bytes.
 internal static byte[] ClearRewardBody(uint totalExperience,uint baseExperience,uint previousExperience){if(totalExperience<previousExperience||totalExperience-previousExperience!=baseExperience)throw new InvalidDataException("Clear experience presentation mismatch");byte[] b=new byte[91];BitConverter.GetBytes(totalExperience).CopyTo(b,0);BitConverter.GetBytes(baseExperience).CopyTo(b,4);BitConverter.GetBytes(previousExperience).CopyTo(b,8);b[62]=1;BitConverter.GetBytes(DiagnosticClearGold).CopyTo(b,67);return b;}
 internal static byte[] BossBody(ushort uid){if(uid<100||uid>163)throw new InvalidDataException("Supported boss UID");return new byte[]{1,1,(byte)uid,(byte)(uid>>8)};}
 internal static byte[] Reply(byte kind,ushort opcode,byte[] body,byte[] keys){if(kind>1||body==null||keys==null||keys.Length!=334)throw new InvalidDataException("Dungeon frame bounds");byte[] padded,encrypted;int index=opcode%14;
  if(kind==0&&(opcode==34||opcode==35||opcode==127))body=Offline70RoleProtocol.WithDungeonIntegrity(body);
  if(opcode==29&&kind==0){padded=new byte[(body.Length+15)/16*16];body.CopyTo(padded,0);encrypted=ProbeLoginCandidate.Crypt(padded,keys,true);}
  else if(opcode==31&&kind==0||(opcode==3||opcode==45)&&kind==1){padded=new byte[16];body.CopyTo(padded,0);encrypted=Offline70KeySettings.Crypt(padded,keys,true);}
  else if((opcode==34||opcode==35)&&kind==0||opcode==48&&kind==1||opcode==49&&kind==1||opcode==75&&kind==1){padded=Offline70RoleProtocol.Pad(body);encrypted=Offline70FlowCipherFG.Crypt(padded,keys,index,true);}
  else if(opcode==127&&kind==0){padded=new byte[16];body.CopyTo(padded,0);encrypted=ProbeLoginCandidate.Crypt(padded,keys,true);}
  else throw new InvalidDataException("Dungeon wire opcode has no verified reader");return Offline70RoleProtocol.Packet(kind,opcode,encrypted);
 }
 internal static byte[] EndPlain(byte[] encrypted,byte[] keys){if(encrypted==null||(encrypted.Length!=8&&encrypted.Length!=16))throw new InvalidDataException("End request ciphertext size");byte[] plain=Offline70FlowCipherFG.Crypt(encrypted,keys,5,false);if(plain[0]>2||plain[1]>3)throw new InvalidDataException("End option bounds");for(int i=2;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("End request padding");return plain;}
 // Current70 type1/op48 has a 70-byte logical body padded to 72 bytes.
 // Native 105D9A0 excludes this command from the folded-MD5 trailer. Coordinates
 // occupy bytes 0/1; bytes 68/69 are also client data, not zero padding.
 // The subsequent client counters are not authoritative input.
 // Run/load/death/adjacency admission is enforced separately by Move().
 internal static byte[] MovePlain(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=72)throw new InvalidDataException("Move request ciphertext size");byte[] plain=Offline70FlowCipherFG.Crypt(encrypted,keys,6,false);if(plain[0]>63||plain[1]>63)throw new InvalidDataException("Move coordinates outside bounded maze grid");/*64-cell bound: the 45-floor death tower needs y up to 44*/for(int i=70;i<72;i++)if(plain[i]!=0)throw new InvalidDataException("Move request padding");return plain;}
 internal static void ValidateClearRequest(byte[] encrypted,byte[] keys){ResultPlain(encrypted,keys);}
 // Original current70 requests cover both values: 0 exits, 1 returns to channel
 // selection. After successful reply3 the native handler closes the old socket;
 // the server must stop processing this connection and permit a new ch10 login.
 internal static byte ExitPlain(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=16)throw new InvalidDataException("Channel exit ciphertext size");byte[] plain=Offline70KeySettings.Crypt(encrypted,keys,false);if(plain[0]>1)throw new InvalidDataException("Channel exit login state");for(int i=1;i<16;i++)if(plain[i]!=0)throw new InvalidDataException("Channel exit padding");return plain[0];}
 internal static void ValidateAbortRequest(byte[] body){if(body==null||body.Length!=0)throw new InvalidDataException("Dungeon abort request must have no body");}
 // The native sender truncates a float to u8 at byte0; it has no <=105 clamp.
 // Packed helper 1EE3875 adds four u16 fields after the first 12 bytes, so the
 // logical body is 40 bytes, followed by fold4 and four zero padding bytes.
 // These are reported statistics, not authority for the server-side award.
 internal static byte[] ResultPlain(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=48)throw new InvalidDataException("Clear result ciphertext size");byte[] plain=Offline70FlowCipherFG.Crypt(encrypted,keys,7,false),body=new byte[40];Buffer.BlockCopy(plain,0,body,0,40);byte[] integrity=Offline70RoleProtocol.WithDungeonIntegrity(body);for(int i=40;i<44;i++)if(plain[i]!=integrity[i])throw new InvalidDataException("Clear result checksum");for(int i=44;i<48;i++)if(plain[i]!=0)throw new InvalidDataException("Clear result padding");if(body[1]!=0)throw new InvalidDataException("Clear result reserved field");return body;}
}
// This journal guards clear idempotence independently of the role stores.
// The caller's consolidated transaction must commit asset deltas before publishing
// clear; this file alone is not an atomic multi-file XP/gold transaction.
public class Offline70ClearReceiptFG {public string RunId;public ushort DungeonId=1;public uint Experience,Gold;}
public class Offline70ClearLedgerFG {public int Format=1;public ushort RoleId=Offline70RoleScopeFG202.Current;public List<Offline70ClearReceiptFG> Receipts=new List<Offline70ClearReceiptFG>();}
class Offline70ClearStoreFG {
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70ClearLedgerFG));
 static void Validate(Offline70ClearLedgerFG s){if(s==null||s.Format!=1||s.RoleId==0||s.Receipts==null||s.Receipts.Count>10000)throw new InvalidDataException("Clear journal bounds");HashSet<string> ids=new HashSet<string>();foreach(Offline70ClearReceiptFG r in s.Receipts){Guid id;if(r==null||r.DungeonId!=1||r.RunId==null||r.RunId.Length!=32||!TryGuid(r.RunId,out id)||!ids.Add(r.RunId)||r.Experience>1000000||r.Gold>1000000)throw new InvalidDataException("Clear journal receipt");}}
 static bool TryGuid(string value,out Guid id){try{id=new Guid(value);return true;}catch(FormatException){id=Guid.Empty;return false;}}
 internal static Offline70ClearLedgerFG Load(string path){if(!File.Exists(path))return new Offline70ClearLedgerFG();if(new FileInfo(path).Length>2000000)throw new InvalidDataException("Clear journal file bounds");using(FileStream f=File.OpenRead(path))using(XmlTextReader x=new XmlTextReader(f)){x.ProhibitDtd=true;x.XmlResolver=null;Offline70ClearLedgerFG s=(Offline70ClearLedgerFG)Serializer.Deserialize(x);Validate(s);return s;}}
 internal static bool Commit(string path,string runId,uint experience,uint gold){string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70ClearLedgerFG s=Load(full);foreach(Offline70ClearReceiptFG r in s.Receipts)if(r.RunId==runId){if(r.Experience!=experience||r.Gold!=gold)throw new InvalidDataException("Conflicting repeated clear award");return false;}s.Receipts.Add(new Offline70ClearReceiptFG{RunId=runId,Experience=experience,Gold=gold});Validate(s);string tmp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+Offline70StoreBackupFG354.Suffix;try{using(FileStream f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}if(Load(tmp).Receipts.Count!=s.Receipts.Count)throw new InvalidDataException("Clear journal read-back mismatch");if(File.Exists(full))File.Replace(tmp,full,backup);else File.Move(tmp,full);if(Load(full).Receipts.Count!=s.Receipts.Count)throw new InvalidDataException("Clear journal commit mismatch");return true;}finally{if(File.Exists(tmp))File.Delete(tmp);}}}
}
