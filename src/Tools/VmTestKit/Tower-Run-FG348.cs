using System;using System.IO;using System.Collections.Generic;
// Native death-tower run protocol for 11000 (死亡之塔, 45 floors) and 11001 (迷惘之塔, 15 floors).
//
// The CN70 client classifies a dungeon by its PVF [dungeon type]:
//   towerofdeath.dgn    -> `tower of death`   (kind 1, CDeathTower)
//   towerofillusion.dgn -> `tower of death`   (kind 1, CDeathTower)
//   ztowerofdespair001  -> `tower of despair` (kind 3, CBossTower, not served by this build)
// Kind-1 dungeons are driven by the death-tower protocol. The original server
// (reference/rednf/.../CDeathTower/handleStartGame.inc) answers the entry op16 with
//   op142 = int dungeonIdx, short endStage, byte bloodState, byte randomBuffType
//   op143 = short stage, int seed, short mapIndex, byte mobCount, mobs[], byte itemCount, items[]
// and drives every later floor through op143 from CStage::makeStagePacket
// (mob = int mobId(roster idx), short instId(uid), int mobIndex(monster id), byte level,
// byte roleType, byte field13, byte field14). The client reports progression with
//   op40  map load      -> op40 type1 ack {1} + op30 int 0
//   op162 stage command -> byte 1 start / byte 2 finish (+ four member-time shorts)
//   op48  next floor    -> op143 of stage+1
// and finishes a run with op144 ranking, op145 reward display, op146 EPLP menu,
// op26 {cmd,subCmd} -> op75 {1,cmd,subCmd} broadcast and return to the village.
//
// Live captures (2026-10-26 22:59 / 23:09, 2026-10-27 16:52) show one correction to the above:
// the CN70 client loads a tower floor only after the ordinary op29 room packet. op142/op143 are
// ignored while the dungeon-selection dialog is up, so this build enters through 16/3/28 + op29
// (the FG211 sequence that demonstrably loaded the floor) and hands over 142/143 once the client
// has reported the load. Every later floor is also shipped as op29 (load) followed by op143
// (native stage number/HUD), and the PVF floor map comes from the ordinary maze table, which
// already carries the exact [death tower map indexes] order.
internal sealed class Offline70TowerRunFG348 {
 internal const ushort DeathTower=11000,IllusionTower=11001;
 // PVF [death tower map indexes]: 11000 lists 45 floor maps 30001..30045; 11001 lists 15 maps in
 // non-sequential order (31001,31002,31003,31004,31005,31007,31008,31010,31011,31006,31019,
 // 31015,31020,31024,31025). The stage number is 1-based, exactly like getCurrentStage().
 internal static bool IsTower(ushort id){return id==DeathTower||id==IllusionTower;}
 internal static int EndFloorFor(ushort id){
  if(id==DeathTower)return 45;
  if(id==IllusionTower)return 15;
  throw new InvalidDataException("Tower identity");
 }
 // Tower floors are the cells of a one-column vertical maze: floor N is cell (0, N-1). The floor
 // number is 1-based (native getCurrentStage), the maze coordinate is 0-based, so advancing to
 // floor N means moving to Y = N-1. Passing the floor number directly moves two cells from the
 // start cell and dungeon.Move refuses it (live 2026-10-27 17:43: "Dungeon room admission").
 internal static byte CellYForFloor(int floor){
  if(floor<1||floor>45)throw new InvalidDataException("Tower floor bounds");
  return (byte)(floor-1);
 }
 // FG374: the client enters a tower through the ordinary room path (only op29 makes this build
 // load a floor) and that path's room grid comes from the dungeon's [maze info] blocks. The tower
 // .dgn ships none, so the grid is the 2-cell start/boss fallback: (0,0) and (0,1) exist, and the
 // client's strict cell lookup on (0,2) takes the secure-CRT bounds path (dumps 2026-10-28
 // 21:37:44 / 22:11:23, stack 0x48B0DA->0x491804->0xF2B04B->0xBC88B2->WndProc). The floor and its
 // map are carried separately (TowerMapOverrideFG374), only the cell alternates between the two
 // cells the client knows. Native CDeathTower needs none of this: its tower object owns the whole
 // stage table and makeStagePacket alone moves the client.
 internal static byte CellYForClientFG374(int floor){
  if(floor<1||floor>45)throw new InvalidDataException("Tower floor bounds");
  return (byte)((floor-1)%2);
 }
 // Per-floor map of the dungeon's PVF [death tower map indexes] row, independent of the cell.
 // FG374c: illusion 3F/9F (003f/011f) carry only [apc random point], so the ordinary room path has
 // no anchor rows there and every actor we send is dropped (live 2026-10-28 23:03) - those floors
 // are skipped at advance time. A 2026-10-29 02:13 attempt borrowed the death tower's 002F/003F
 // rooms for them (the anchors match the native draws) and it worked once, but the client keeps the
 // previous dungeon's scene alive around a cross-dungeon map switch: the user saw two death-god
 // NPCs and two overlapping maps on 3F AND 4F. The borrow is therefore reverted until Script.pvf
 // can grow real [ai character] anchors for 003f/011f.
 internal static ushort FloorMapFG374(ushort dungeonId,int floor){
  if(!IsTower(dungeonId)||floor<1||floor>EndFloorFor(dungeonId))throw new InvalidDataException("Tower floor map bounds");
  return Offline70ForestFG75.Map(dungeonId,0,(byte)(floor-1));
 }
 // FG374c: can this client host the floor at all? The ordinary room path anchors every room-packet
 // row to the map's own [monster] / [ai character] rows, so a map with neither (only
 // [apc random point]) renders empty no matter what the server sends - live 2026-10-28 23:03:
 // illusion floor 3 loaded with one APC row on the wire and an empty room. Such floors are
 // skipped at advance time (native hosts them because its client runs the tower module).
 internal static bool FloorHostableFG374(ushort dungeonId,int floor){
  if(!IsTower(dungeonId)||floor<1||floor>EndFloorFor(dungeonId))throw new InvalidDataException("Tower floor bounds");
  if(Offline70ForestFG75.Monsters(dungeonId,0,(byte)(floor-1)).Length>0)return true;
  return ApcMonstersFG.GetApcMonsters(dungeonId,FloorMapFG374(dungeonId,floor)).Count>0;
 }
 // FG377: the story-pause notification (notify 170 / 0xaa, {uid u16, flag1 u8, flag2 u8}) is what
 // freezes dungeon gameplay around native story dialogs (rednf Dispatcher_DungeonEventStoryPause
 // broadcasts it; flag2=1 bypasses the quest [event dialog] validation, so the server can drive the
 // pause directly). The tower plays it at floor load and floor clear, and releases it from the
 // maintenance loop a few seconds later - the native "dialog -> fight -> dialog -> next floor" beat.
 internal bool StoryResumePending;internal int StoryResumeFloor;internal DateTime StoryResumeUtc;
 internal bool StoryPausePending;internal int StoryPauseFloor;internal DateTime StoryPauseUtc;
 // Early op142/143 copy, armed by the entry burst (see TowerBeginFG348).
 internal bool IdentityEarlyPending;internal DateTime IdentityEarlyUtc;
 // Entry room packet, sent one tick after the identity so 142/143 land before the floor exists.
 internal bool RoomEarlyPending;internal DateTime RoomEarlyUtc;internal byte[] EarlyRoomBody;
 // Native-order safety net: send the proven 16/3/28 + room burst if the client never reports a load.
 internal bool EntryFallbackPending;internal DateTime EntryFallbackUtc;
 internal ushort DungeonId;internal int Floor,EndFloor;internal string RunId;
 internal bool Active,Loaded,StageStarted,StageFinished;
 // op142/op143 tower identity is sent once, after the client has reported the floor load: while
 // the dungeon-selection dialog is up the client ignores both (live 2026-10-26/27 captures).
 internal bool IdentitySent;
 // op48 replay guard: the native MoveMap has no request ids of its own, so the frame sequence of
 // the advance that already happened is kept and an exact replay is ignored instead of erroring.
 internal ushort LastMoveSequence;internal bool HasMoveSequence;
 // Finish sequence: armed by the last kill of the top floor (native _onPrepareFinishTower's 5s
 // timer) or by the client's own stage-finish command; phase 0 rank -> 1 display -> 2 grant.
 internal bool FinishPending;internal int Phase;internal DateTime DueUtc;
 // Floors whose PVF roster is empty have nothing to kill, so nothing in the tower protocol would
 // move the client off them. The maintenance loop advances such a floor after a grace period; a
 // client in tower mode may also send op48 first, and one late op48 from the skipped floor is then
 // ignored instead of answered with an error.
 internal bool AutoAdvancePending;internal int AutoAdvanceFloor;internal DateTime AutoAdvanceDueUtc;
 internal int AutoAdvancedFromFloor=-1;internal DateTime AutoAdvancedUtc=DateTime.MinValue;
 // A cleared floor is pushed by the client's own op48 in tower mode; when it does not (ordinary
 // scene, no next-floor portal) the same grace timer pushes it so a run can never stall on a
 // cleared floor. 15s leaves room for the death-god "on complete" dialog.
 internal bool ClearAdvancePending;internal int ClearAdvanceFloor;internal DateTime ClearAdvanceDueUtc;
 // Death-god "on start" line (Tower-Dialog-FG363.cs). The op12 body must carry the role uid (this
 // client drops uid=0 lines), but the packet is queued right after the load confirmation, while the
 // dungeon scene and the player's creature are still being created. Sending it at that instant is
 // the only tower change between the working 2026-10-27 session and the 2026-10-28 freeze, so the
 // line is armed here and played by the maintenance loop a couple of seconds later, when the scene
 // can resolve the sender. Content and floor timing are unchanged.
 internal bool DialogPending;internal int DialogFloor;internal DateTime DialogDueUtc;
 internal uint ClearExperience;internal bool ClearSettled;
 internal void Begin(ushort id,string runId){
  if(!IsTower(id)||String.IsNullOrEmpty(runId))throw new InvalidDataException("Tower begin");
  DungeonId=id;RunId=runId;Floor=1;EndFloor=EndFloorFor(id);
  Active=true;Loaded=false;StageStarted=StageFinished=false;IdentitySent=false;FinishPending=false;Phase=0;DueUtc=DateTime.MinValue;ClearExperience=0;ClearSettled=false;HasMoveSequence=false;
  AutoAdvancePending=false;AutoAdvanceFloor=0;AutoAdvanceDueUtc=DateTime.MinValue;AutoAdvancedFromFloor=-1;AutoAdvancedUtc=DateTime.MinValue;
  ClearAdvancePending=false;ClearAdvanceFloor=0;ClearAdvanceDueUtc=DateTime.MinValue;
  DialogPending=false;DialogFloor=0;DialogDueUtc=DateTime.MinValue;
  StoryResumePending=false;StoryResumeFloor=0;StoryResumeUtc=DateTime.MinValue;
  StoryPausePending=false;StoryPauseFloor=0;StoryPauseUtc=DateTime.MinValue;
  IdentityEarlyPending=false;IdentityEarlyUtc=DateTime.MinValue;
   RoomEarlyPending=false;RoomEarlyUtc=DateTime.MinValue;
   EntryFallbackPending=false;EntryFallbackUtc=DateTime.MinValue;
 }
 // op142. bloodState 0 = normal tower run; randomBuffType 0x0b is the original
 // "party == NULL" sentinel (no random buff).
 internal static byte[] InfoBody(ushort id,int endFloor){
  if(!IsTower(id)||endFloor!=EndFloorFor(id))throw new InvalidDataException("Tower info bounds");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((uint)id);w.Write((ushort)endFloor);w.Write((byte)0);w.Write((byte)0x0b);return m.ToArray();
  }
 }
 // op143 for the dungeon's current floor. Roster rows come from the same tables the ordinary
 // room packet uses (Forest-Content-FG75.Monsters per floor map + ApcMonstersFG [ai character]
 // rows), so the tower kill/report path stays identical to the rest of the server.
 //
 // Native shape (rednf CDeathTower): consistMap pushes EVERY actor of the floor - [ai character]
 // rows, the [apc random point] draws, the [monster] rows - into one m_mapInfo.m_monsterMap under
 // one instance-id counter, and makeStagePacket simply writes m_monsterMap.size() and every entry.
 // killMonster erases the killed entry. So op143 is the floor's ALIVE ROSTER, independent of
 // whether the room packet has already spawned (registered) those actors.
 //
 // This build used to emit only side-channel APCs that were still "pending" (not yet spawned by
 // the ordinary RoomBody). Floor advancement sends op29 first (it is what makes this client load),
 // so the room packet had already registered the actor and op143 came out with count 0 - live
 // 2026-10-28 21:37:44, 2F -> 3F: room 31003 carried one AP-fill actor (uid 100 / 20002 / lv15 /
 // roleType 5) and the following op143 was `03 00 ... 1B 79 00 00`, after which the client never
 // sent op40 for floor 3 (the 0-actor stage packet freeze from FG363d). The stage packet must
 // describe exactly what the room packet spawned, in either order.
 internal static byte[] StageBody(Offline70DungeonFlowFG d){
  if(d==null||!d.Active||!d.TowerFlowFG211)throw new InvalidDataException("Tower stage outside tower run");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((ushort)(d.TowerFloorFG374>0?d.TowerFloorFG374:d.Y+1)); // stage number, 1-based (Y is only the client cell now)
   w.Write(unchecked((uint)Environment.TickCount)); // native get_rand_int(0)
   w.Write(d.CurrentMap);
   List<ushort[]> apcs=d.CurrentApcMonsters();int alive=0;
   for(int i=0;i<d.CurrentCount;i++)if(!d.IsDead((ushort)(100+i)))alive++;
   for(int ai=0;ai<apcs.Count;ai++){ushort apcUid=d.ApcUidFG186(ai);if(d.ApcPendingFG186(apcUid)||d.ApcAliveFG186(apcUid))alive++;}
   w.Write((byte)alive);
   for(int i=0;i<d.CurrentCount;i++){
    ushort uid=(ushort)(100+i);if(d.IsDead(uid))continue;
    ushort[] row=d.Monster(uid);
    w.Write(i);w.Write(uid);w.Write((int)row[0]);w.Write((byte)row[1]);w.Write((byte)row[2]);w.Write((byte)0);w.Write((byte)0);
   }
   for(int ai=0;ai<apcs.Count;ai++){
    ushort apcUid=d.ApcUidFG186(ai);bool pending=d.ApcPendingFG186(apcUid);
    // Registered and dead: native killMonster already erased it from the roster.
    if(!pending&&!d.ApcAliveFG186(apcUid))continue;
    ushort[] row=apcs[ai];
    // roleType 5..8 in the original consistMap first section; the declared role byte is kept.
    w.Write(ai);w.Write(apcUid);w.Write((int)row[1]);w.Write((byte)row[2]);w.Write(Offline70DungeonFlowFG.ApcRoleByteFG186(row));w.Write((byte)0);w.Write((byte)0);
    // The room packet may have registered it already (it ran first on this floor); only an actor
    // the room has not written yet needs the spawn registration here.
    if(pending)d.ApcRegisterSpawnFG186(apcUid,row);
   }
   w.Write((byte)0);                                // no map item rows on these floors in this build
   return m.ToArray();
  }
 }
 // op144 CDeathTowerRanking::makeDisplayRangking body. The record blocks are
 // CDeathTowerRanking::_makeRecordPacket: per member int nameLen + name + byte + byte, then
 // short stage, int playTime, int rank. A fresh offline tower keeps no ranking, so the best
 // block is byte0 and the five top-ranker slots are empty records.
 internal static byte[] RankingBody(uint clearMilliseconds,int stage){
  if(clearMilliseconds>86400000||stage<1||stage>45)throw new InvalidDataException("Tower ranking bounds");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((int)clearMilliseconds);w.Write(stage);w.Write((byte)0);w.Write(0);
   w.Write((byte)0);                      // makeMyBestRecordPacket: no record
   for(int slot=0;slot<5;slot++){
    for(int member=0;member<4;member++){w.Write(0);w.Write((byte)0);w.Write((byte)0);}
    w.Write((ushort)0);w.Write(0);w.Write(0);
   }
   return m.ToArray();
  }
 }
 // op145 makeDisplayReward: int exp, then four [byte cnt + cnt*(int itemId,int addInfo)] groups.
 // Solo offline run: the local member is group 0 and this build generates no tower cards, so all
 // four groups are empty and the displayed exp is exactly what TowerSettleFG348 commits.
 internal static byte[] RewardBody(uint experience){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((int)experience);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);return m.ToArray();
  }
 }
}
// Service hooks for the native tower protocol. Kept in the same FG so the whole flow can be
// reviewed against reference/rednf/.../CDeathTower/*.inc in one place.
partial class Offline70BatchServiceFG {
 Offline70TowerRunFG348 towerFG348;
 // Identity-bound: a tower run left behind by an earlier connection (the player closed the client
 // mid-run) must never take over the next dungeon. Before this guard a stale run made a normal
 // dungeon 90 entry route op40/op48 into the tower hooks (live log 2026-10-27 16:57).
 bool TowerActiveFG348 {get{return towerFG348!=null&&towerFG348.Active&&dungeon!=null&&dungeon.Active&&towerFG348.DungeonId==dungeon.DungeonId&&towerFG348.RunId==dungeon.RunId;}}

 // op16 tail for 11000/11001: the invitation (4183) was already consumed by the caller; towers
 // never spend fatigue.
 //
 // Entry packet order, from five live captures:
 //   2026-10-26 22:59 (FG211, ordinary entry): 16/3/28 + op29 room -> client answers op40 1.5s later.
 //     This is the only sequence that ever loaded a tower floor from a cold selection dialog.
 //   2026-10-26 23:09 (FG348) and 2026-10-27 16:52 (FG350): 142/143 alone, and 16/3/28 + 142/143,
 //     both left the client on the dungeon-selection dialog re-sending op16, never op40.
 //   2026-10-27 17:43 (FG363, identity after op40): the floor loads and plays, but the client never
 //     enters its tower mode (no death-god dialog, no op162 stage commands).
 //   2026-10-27 18:00 (FG363 second round, 142/143 before the burst): also loaded, but the tower
 //     module still never starts; a 2026-10-28 19:38 client freeze was the first regression after
 //     that reordering. The pre-load pair is ignored at best (the 18:39 heap scan found no op142/143
 //     handler object) and a modal race with the open selection dialog at worst, so it is not worth
 //     the risk: only 16/3/28 closes the dialog and only op29 starts the load.
 // The load trigger is op29. Identity (op142 information + op143 stage) is therefore handed over
 // once the client reports the first floor load, where the scene exists and dialog state cannot
 // swallow or mishandle it. Both packets are native-shaped either way; the client's tower module
 // (obfuscated segment) is the only consumer and it demonstrably does not start in an offline entry.
 void TowerBeginFG348(ushort dungeonId){
  towerFG348=new Offline70TowerRunFG348();
  towerFG348.Begin(dungeonId,dungeon.RunId);
  // FG374: floor 1 lives in client cell (0,0); the map is carried by the override so later floors
  // can reuse the same two cells without losing their [death tower map indexes] row.
  dungeon.TowerFloorFG374=1;
  dungeon.TowerMapOverrideFG374=Offline70TowerRunFG348.FloorMapFG374(dungeonId,1);
  towerDialogStartFloorFG363=towerDialogCompleteFloorFG363=-1;
  // Build the room body before the burst so a data-side refusal cannot cut the burst in half, and
  // never let one escape: the fallback is the bare native room body (see TowerRoomBodyFG348).
  byte[] room=TowerRoomBodyFG348();
  towerFG348.EarlyRoomBody=room;   // built exactly once: ObjectRoomBodyFG167 refuses to plan twice
  // Entry-order space is exhausted (live 2026-10-29 08:50/08:51/08:58):
  //   identity only (native)            -> client never leaves the selection dialog
  //   ack16+3 -> identity -> room       -> same wedge, the 1.5s fallback cannot recover it
  //   ack16+3+28 -> identity -> room    -> loads, but as an ORDINARY dungeon scene
  // So 142/143 must never precede the room packet: this client always processes op29 first and
  // only then tolerates the tower identity. Whatever arms its tower module happens before the
  // start-game request, not in the entry burst. Keep the proven order.
  foreach(ushort notify in new ushort[]{16,3,28})Send("tower-entry"+notify,EntryReply(notify));
  TowerIdentityFG348(true);
  towerFG348.RoomEarlyPending=true;towerFG348.RoomEarlyUtc=DateTime.UtcNow.AddMilliseconds(250);
  Note("TOWER_ENTRY_COMMITTED run="+dungeon.RunId+" dungeon="+dungeonId+" floors="+towerFG348.EndFloor+" floor=1 map="+dungeon.CurrentMap+" identityPreLoad=True order=entry-identity-room");
 }
 // Object-room rows are optional scenery drops; a planner refusal must not turn a committed tower
 // entry into a rejected op16 (that poison ack is what froze the client on the selection dialog in
 // the 2026-10-26/27 captures). Fall back to the bare room body. Any unexpected planner failure is
 // contained here too: a half-sent entry burst freezes the client just like the poison ack.
 byte[] TowerRoomBodyFG348(){
  try{return ObjectRoomBodyFG167();}
  catch(Exception e){Note("TOWER_ROOM_BODY_FALLBACK run="+dungeon.RunId+" floor="+towerFG348.Floor+" reason="+e.GetType().Name+":"+e.Message);return dungeon.RoomBody();}
 }
 // FG377: story dialog on/off (notify 170). Body {uid u16, flag1 u8, flag2 u8}.
 // Live 2026-10-29 06:42:56: the PAUSE works inside the tower (the dialog appears and the client
 // stops sending gameplay frames for the whole 4s window), but the RESUME (flag1=0) kills the
 // client: 0xC0000005 at 0x25492A00, stack 0x5D5B2A -> 0x268387A -> 0x7B6DA0 -> indirect call into
 // garbage, in the same second the packet went out. The client never sends the story-pause command
 // itself (no op194 in any capture), so nothing proves it expects the server to close the window -
 // the player's 继续 button may be the only safe close. Automatic resume is therefore off: the
 // pause is still sent (dialog + freeze), and whether the client can close it locally is what the
 // live run has to answer. Flip StoryResumeFG377 back on only with that answer in hand.
 internal const bool StoryResumeFG377=false;
 // Story PAUSE is off as well now. Disassembly of the live image at the crash site shows why:
 //   005D5B20 mov ecx,[ecx+edi*4]   ; object list [esi+0x14]..[esi+0x18]
 //   005D5B25 mov eax,[edx+0x24]    ; virtual slot 9
 //   005D5B28 call eax              ; <-- both crashes
 // and the two call targets were 0x25492A00 / 0x00000002, i.e. freed memory reused by the heap
 // (the object header is already a freelist link). Both crashes happen 4-6s after the pause - the
 // point where the client finishes its own story playback and frees the window, leaving the dead
 // pointer in the per-frame update list. A story window this client did not create from local data
 // is a dangling reference, so the notification is not a usable dialog channel here.
 internal const bool StoryPauseFG377=false;
 void TowerStoryPauseFG377(bool pause){
  if(!StoryPauseFG377){Note("TOWER_STORY_SUPPRESSED run="+dungeon.RunId+" floor="+towerFG348.Floor+" pause="+pause);return;}
  if(!pause&&!StoryResumeFG377){Note("TOWER_STORY_RESUME_SUPPRESSED run="+dungeon.RunId+" floor="+towerFG348.Floor);return;}
  byte[] body=new byte[]{(byte)role.Id,(byte)(role.Id>>8),(byte)(pause?1:0),1};
  Send("tower-story-"+(pause?"pause":"resume"),Offline70RoleProtocol.BatchPacketFG(0,170,body,keys,false));
  Note("TOWER_STORY_PAUSE run="+dungeon.RunId+" floor="+towerFG348.Floor+" pause="+pause);
 }
 void TowerIdentityFG348(bool early=false){
  if(towerFG348==null)return;
  if(!early&&towerFG348.IdentitySent)return;
  if(!early)towerFG348.IdentitySent=true;
  Send("tower-info142",Offline70RoleProtocol.BatchPacketFG(0,142,Offline70TowerRunFG348.InfoBody(towerFG348.DungeonId,towerFG348.EndFloor),keys,false));
  Send("tower-stage143",Offline70RoleProtocol.BatchPacketFG(0,143,Offline70TowerRunFG348.StageBody(dungeon),keys,false));
  Note((early?"TOWER_IDENTITY_EARLY":"TOWER_IDENTITY_SENT")+" run="+dungeon.RunId+" dungeon="+towerFG348.DungeonId+" floors="+towerFG348.EndFloor+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap);
 }
 // Last-resort entry completion. The client leaves the dungeon-selection dialog only on 16/3/28 and
 // starts the floor load only on op29, so an entry handler that dies between those replies leaves
 // the session frozen on the dialog. Only called when the tower run is already committed
 // (TowerActiveFG348); a genuine pre-commit rejection still gets the native error ack.
 void TowerEntryRecoveryFG374(){
  if(!TowerActiveFG348)return;
  byte[] room;
  try{room=TowerRoomBodyFG348();}catch(Exception e){room=dungeon.RoomBody();Note("TOWER_ENTRY_RECOVERY_BODY reason="+e.GetType().Name+":"+e.Message);}
  foreach(ushort notify in new ushort[]{16,3,28})Send("tower-entry-retry"+notify,EntryReply(notify));
  Send("tower-room29-retry",Offline70DungeonFlowFG.Reply(0,29,room,keys));
  Note("TOWER_ENTRY_RECOVERED run="+dungeon.RunId+" dungeon="+towerFG348.DungeonId+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap);
 }
 void TowerLoadFG348(byte[] body){
  if(!TowerActiveFG348)throw new InvalidDataException("Tower floor load outside pending floor");
  // The load confirmation is the one packet the client waits for during the loading screen: losing
  // its ack is an unrecoverable freeze. The two u32 summaries are recorded only, so a body size
  // surprise degrades to zero summaries instead of throwing past the ack.
  byte[] load;
  try{load=Offline70RoleProtocol.BatchLoadRequestFG(body,keys);}
  catch(InvalidDataException e){load=new byte[8];Note("TOWER_LOAD_BODY_IGNORED run="+dungeon.RunId+" floor="+towerFG348.Floor+" bytes="+(body==null?0:body.Length)+" reason="+e.Message);}
  try{Sync();}catch(InvalidDataException e){Note("TOWER_LOAD_SYNC_PENDING run="+dungeon.RunId+" floor="+towerFG348.Floor+" reason="+e.Message);}
  bool duplicate=dungeon.Loaded;
  if(!duplicate)dungeon.MarkLoaded();
  towerFG348.Loaded=true;
  // Native handleFinishLoading answers every load request with the 0x28 cmd ok packet and sends
  // makeFinishLoading once the members are ready. The ordinary flow also acknowledges op40 per
  // room, and with the room packet now present the client may report a load it already has, so the
  // ack is repeated instead of throwing on the duplicate.
  Send("tower-load-ack40",Offline70RoleProtocol.BatchPacketFG(1,40,new byte[]{1},keys,false));
  Send("tower-loaded30",Offline70RoleProtocol.BatchPacketFG(0,30,new byte[4],keys,false));
  if(towerFG348.Floor==1)TowerIdentityFG348();
  // The client's own tower scene never starts here (no op162 / no HUD), so the death-god "on start"
  // line is played through op12 system chat once the floor is loaded (Tower-Dialog-FG363.cs). It is
  // armed, not sent, because the scene/creature for the uid in the op12 body is still being built at
  // the instant the load is acknowledged (see DialogPending).
  towerFG348.DialogPending=true;towerFG348.DialogFloor=towerFG348.Floor;towerFG348.DialogDueUtc=DateTime.UtcNow.AddSeconds(2);
  // FG377: the death-god "on start" beat - freeze the floor while the line plays, release after.
  // The pause must go out inline, with the load ack. The 2026-10-29 1.5s-delayed variant is the
  // one that crashes: both delayed runs died 4-6s later with the SAME stack
  // (0x5D5B2A -> 0xD7198B -> 0xEDCA0A -> 0x4C3705 -> 0xFF25BA -> thread entry 0x129835A) ending
  // in a call through a garbage function pointer, once with the resume and once without it
  // (07:00:25, 6.5s after the pause, resume suppressed). The inline runs (03:10/03:18/04:0x) never
  // crashed and 04:0x actually showed the dialog, so inline is the only known-good timing.
  towerFG348.StoryPausePending=false;
  TowerStoryPauseFG377(true);
  Note("TOWER_STORY_PAUSE_ARMED run="+dungeon.RunId+" floor="+towerFG348.Floor+" delayMs=0");
  // PVF rosters for several tower floors are empty (illusion 31003/31005/31010/31011, most of the
  // death tower): the native server fills those with AP random monsters, which this build does not
  // generate. Nothing on such a floor can ever clear it from the client side, so the maintenance
  // loop advances it after a grace period.
  if(dungeon.TotalCount==0&&dungeon.ApcAliveUidsFG186().Count==0&&towerFG348.Floor<towerFG348.EndFloor){
   towerFG348.AutoAdvancePending=true;towerFG348.AutoAdvanceFloor=towerFG348.Floor;towerFG348.AutoAdvanceDueUtc=DateTime.UtcNow.AddSeconds(8);
   Note("TOWER_EMPTY_FLOOR run="+dungeon.RunId+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap+" nativeApRandomMonsters=True");
  }
  Note("TOWER_LOADED run="+dungeon.RunId+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap+" summaries="+BitConverter.ToUInt32(load,0)+"/"+BitConverter.ToUInt32(load,4)+" duplicate="+duplicate);
 }
 // op162 Dispatcher_DeathTowerStageCommand: byte 1 = stage start, byte 2 = stage finish
 // (the four member-time shorts that follow are reports only).
 void TowerStageCommandFG348(byte[] body){
  if(!TowerActiveFG348)throw new InvalidDataException("Tower stage command outside tower");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,162%14,false);
  if(plain==null||plain.Length<1)throw new InvalidDataException("Tower stage request size");
  byte cmd=plain[0];
  if(cmd==1){
   // The stage start arrives after the client's death-god "on start" dialog; a floor that is
   // already loaded may or may not have reported it, so the command is recorded, not gated.
   if(towerFG348.StageStarted){Note("TOWER_STAGE_START_DUPLICATE floor="+towerFG348.Floor);return;}
   towerFG348.StageStarted=true;Note("TOWER_STAGE_START run="+dungeon.RunId+" floor="+towerFG348.Floor+" loaded="+dungeon.Loaded);return;
  }
  if(cmd==2){
   if(towerFG348.StageFinished){Note("TOWER_STAGE_FINISH_DUPLICATE floor="+towerFG348.Floor);return;}
   towerFG348.StageFinished=true;
   Note("TOWER_STAGE_FINISH run="+dungeon.RunId+" floor="+towerFG348.Floor+" cleared="+dungeon.RoomCleared+" report="+BitConverter.ToString(plain));
   if(towerFG348.Floor==towerFG348.EndFloor&&dungeon.RoomCleared)TowerArmFinishFG348(0);
   return;
  }
  throw new InvalidDataException("Tower stage command byte");
 }
 // op48 DisPatcher_MoveMap routed to CDeathTower::handleMoveMap. The native handler takes no
 // arguments, so the body's maze coordinates are not authoritative; the request means "stage
 // clear, load the next floor". A body shape mismatch is logged and ignored, and an exact
 // sequence replay is dropped instead of erroring.
 void TowerMoveFG348(byte[] body,ushort sequence){
  if(towerFG348!=null&&towerFG348.HasMoveSequence&&towerFG348.LastMoveSequence==sequence){Note("TOWER_MOVE_DUPLICATE seq="+sequence);return;}
  try{Offline70DungeonFlowFG.MovePlain(body,keys);}catch(InvalidDataException e){Note("TOWER_MOVE_BODY_IGNORED reason="+e.Message);}
  if(!TowerActiveFG348||!dungeon.Loaded)throw new InvalidDataException("Tower move outside loaded floor");
  // Native handleMoveMap gates on the stage state that op162 commands set. A client that entered
  // through the ordinary room packet never sends op162 (live 2026-10-27 16:58: op48 arrived for a
  // cleared room with no stage command at all), so a cleared floor is the authoritative condition
  // here; the stage counters stay recorded for the native-order clients.
  if(!dungeon.RoomCleared){
   // One late op48 from a floor the maintenance loop already skipped: swallow it, do not poison
   // the new floor with an error acknowledgement.
   if(towerFG348.AutoAdvancedFromFloor>=0&&towerFG348.Floor==towerFG348.AutoAdvancedFromFloor+1&&DateTime.UtcNow<towerFG348.AutoAdvancedUtc.AddSeconds(60)){
    Note("TOWER_MOVE_AFTER_AUTO_IGNORED run="+dungeon.RunId+" floor="+towerFG348.Floor+" autoFrom="+towerFG348.AutoAdvancedFromFloor+" seq="+sequence);return;
   }
   TowerErrorFG348(48,0x16);Note("TOWER_MOVE_REJECTED run="+dungeon.RunId+" floor="+towerFG348.Floor+" started="+towerFG348.StageStarted+" finished="+towerFG348.StageFinished+" cleared=False");return;
  }
  if(towerFG348.Floor>=towerFG348.EndFloor){TowerErrorFG348(48,0x16);Note("TOWER_MOVE_TOP_FLOOR floor="+towerFG348.Floor);return;}
  // op29 is what loads a floor for a client that entered through the ordinary room packet; op143
  // is the native next-stage packet (stage number / tower HUD / death-god dialog). Both describe
  // the same floor map and roster, and the native meaning goes last so a client in tower mode ends
  // on the stage it should display.
  TowerAdvanceFG348(sequence,true);
 }
 // Shared floor advance for op48 and the empty-floor timer. dungeon.Move can refuse (adjacency or
 // clear state), so the op48 acknowledgement is only sent after the move committed; the duplicate
 // sequence guard is armed only when a real request carried the sequence.
 void TowerAdvanceFG348(ushort sequence,bool ack){
  int next=towerFG348.Floor+1;
  // FG374c: floors whose map has no [monster]/[ai character] anchor cannot host any actor in the
  // ordinary room path (live 2026-10-28 23:03, floor 3 empty) - jump straight over them so the
  // player never stands in an unclosable room. Skipping floors that share the current cell means
  // the target cell can equal the current one, so the one-step Move is conditional now.
  int skipped=0;
  while(next<=towerFG348.EndFloor&&!Offline70TowerRunFG348.FloorHostableFG374(towerFG348.DungeonId,next)){skipped++;next++;}
  if(skipped>0)Note("TOWER_FLOOR_SKIPPED run="+dungeon.RunId+" from="+(towerFG348.Floor+1)+" to="+next+" count="+skipped+" anchorless=true");
  if(next>towerFG348.EndFloor){Note("TOWER_ANCHORLESS_TOP run="+dungeon.RunId+" floor="+towerFG348.Floor);TowerArmFinishFG348(2000);return;}
  // FG374: the client only knows the two cells of a maze-less dungeon's fallback grid, so the cell
  // alternates (0<->1) while the real floor and its map travel separately.
  if(Offline70TowerRunFG348.CellYForClientFG374(next)!=dungeon.Y)dungeon.Move(0,Offline70TowerRunFG348.CellYForClientFG374(next));
  // The reused cell must start as a fresh room: no dead uids, not "visited" (or RoomBody would
  // return the bare retained-room header), no APC registrations from its previous floor.
  dungeon.TowerResetRoomFG374();
  dungeon.Loaded=false;   // same-cell advances skip Move, so the pending-load state is reset here
  dungeon.TowerFloorFG374=next;
  dungeon.TowerMapOverrideFG374=Offline70TowerRunFG348.FloorMapFG374(towerFG348.DungeonId,next);
  towerFG348.Floor=next;towerFG348.Loaded=false;towerFG348.StageStarted=false;towerFG348.StageFinished=false;
  towerFG348.DialogPending=false;towerFG348.StoryResumePending=false;towerFG348.StoryPausePending=false;
  if(ack){towerFG348.LastMoveSequence=sequence;towerFG348.HasMoveSequence=true;}
  roomLoot.Remove(dungeon.RoomIndex);   // fresh ground/object plan for the reused cell
  SelectRoomLoot();
  if(ack)Ack(48,true);
  Send("tower-room29",Offline70DungeonFlowFG.Reply(0,29,TowerRoomBodyFG348(),keys));
  Send("tower-stage143",Offline70RoleProtocol.BatchPacketFG(0,143,Offline70TowerRunFG348.StageBody(dungeon),keys,false));
  Note("TOWER_FLOOR_ADVANCED run="+dungeon.RunId+" floor="+next+" map="+dungeon.CurrentMap+" skipped="+skipped+" source="+(ack?"op48":"empty-floor-timer"));
 }
 void TowerErrorFG348(ushort op,byte error){Send("tower-error"+op,Offline70RoleProtocol.BatchPacketFG(1,op,new byte[]{0,error},keys,false));}
 // Last-kill hook (native handleKillMonster -> _onFinishStage and, on the top floor,
 // _onPrepareFinishTower). Called from both the monster and [ai character] APC death paths.
 void TowerKillTailFG348(ushort uid){
  if(!TowerActiveFG348)return;
  if(!dungeon.RoomCleared)return;
  MapClearFG130();// idempotent receipt: also covers floors whose last actor is the [ai character] APC
  towerFG348.DialogPending=false;// a cleared floor must not also play its late "on start" line
  Note("TOWER_FLOOR_CLEARED run="+dungeon.RunId+" floor="+towerFG348.Floor+" uid="+uid+" map="+dungeon.CurrentMap);
  // Death-god "on complete" line before the floor is pushed (Tower-Dialog-FG363.cs), with the same
  // story freeze; the resume fires a few seconds later and the clear timer pushes the next floor.
  TowerDialogFG363(true);
  TowerStoryPauseFG377(true);
  towerFG348.StoryResumePending=true;towerFG348.StoryResumeFloor=towerFG348.Floor;towerFG348.StoryResumeUtc=DateTime.UtcNow.AddSeconds(6);
  Note("TOWER_STORY_PAUSE_ARMED run="+dungeon.RunId+" floor="+towerFG348.Floor+" resumeSeconds=6");
  if(towerFG348.Floor>=towerFG348.EndFloor){TowerArmFinishFG348(2000);return;}
  // The client's op48 normally follows its "on complete" dialog; the timer is the fallback for a
  // client that stays in the ordinary scene and never asks for the next floor.
  if(!towerFG348.ClearAdvancePending||towerFG348.ClearAdvanceFloor!=towerFG348.Floor){
   towerFG348.ClearAdvancePending=true;towerFG348.ClearAdvanceFloor=towerFG348.Floor;towerFG348.ClearAdvanceDueUtc=DateTime.UtcNow.AddSeconds(15);
   Note("TOWER_CLEAR_ADVANCE_ARMED run="+dungeon.RunId+" floor="+towerFG348.Floor+" delaySeconds=15");
  }
 }
 void TowerArmFinishFG348(int delayMs){
  if(!TowerActiveFG348||towerFG348.ClearSettled||towerFG348.FinishPending)return;
  towerFG348.FinishPending=true;towerFG348.Phase=0;towerFG348.DueUtc=DateTime.UtcNow.AddMilliseconds(delayMs);
  Note("TOWER_FINISH_ARMED run="+dungeon.RunId+" floor="+towerFG348.Floor+" delayMs="+delayMs+" kills="+dungeon.VerifiedKillCount);
 }
 // Native timer chain: 5s -> _beginTowerClearProcess (op144 ranking), 1s -> op145 reward
 // display, 5s -> grant + op146 EPLP menu, 60s -> return if the client never chose.
 void MaintainTowerFG348(){
  // The maintenance loop runs outside Handle()'s InvalidDataException guard: a bad tower save or
  // quest row must never escape and take the channel process down. On failure the sequence is
  // parked at the return phase so it retries the town exit instead of re-settling every tick.
  try{
   // Death-god "on start" line, played a couple of seconds after the load ack so the dungeon scene
   // can resolve the sender uid of the op12 line (DialogPending in Offline70TowerRunFG348).
   // Native-parity early identity copy (armed by the entry burst, 150ms later).
   if(TowerActiveFG348&&towerFG348.IdentityEarlyPending){
    if(DateTime.UtcNow>=towerFG348.IdentityEarlyUtc){towerFG348.IdentityEarlyPending=false;TowerIdentityFG348(true);}
   }
   // Room packet for the entry, one maintenance tick after the identity (see TowerBeginFG348).
   if(TowerActiveFG348&&towerFG348.RoomEarlyPending){
    if(DateTime.UtcNow>=towerFG348.RoomEarlyUtc){
     towerFG348.RoomEarlyPending=false;
     byte[] pendingRoom=towerFG348.EarlyRoomBody;
     if(pendingRoom==null){try{pendingRoom=TowerRoomBodyFG348();}catch(Exception e){pendingRoom=dungeon.RoomBody();Note("TOWER_ROOM_EARLY_FALLBACK reason="+e.GetType().Name);}}
     Send("tower-room29-early",Offline70DungeonFlowFG.Reply(0,29,pendingRoom,keys));
     towerFG348.EarlyRoomBody=null;
     Note("TOWER_ROOM_EARLY_SENT run="+dungeon.RunId+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap);
    }
   }
   // Native-order safety net (see TowerBeginFG348): the floor never loaded, so the client is
   // still on the selection dialog. Replay the proven burst so the run can continue.
   if(TowerActiveFG348&&towerFG348.EntryFallbackPending){
    if(towerFG348.Loaded||!dungeon.Active)towerFG348.EntryFallbackPending=false;
    else if(DateTime.UtcNow>=towerFG348.EntryFallbackUtc){
     towerFG348.EntryFallbackPending=false;
     foreach(ushort notify in new ushort[]{16,3,28})Send("tower-entry-fallback"+notify,EntryReply(notify));
     byte[] fb=towerFG348.EarlyRoomBody;
     if(fb==null){try{fb=TowerRoomBodyFG348();}catch(Exception e){fb=dungeon.RoomBody();Note("TOWER_ENTRY_FALLBACK_BODY reason="+e.GetType().Name);}}
     Send("tower-room29-fallback",Offline70DungeonFlowFG.Reply(0,29,fb,keys));
     towerFG348.EarlyRoomBody=null;
     Note("TOWER_ENTRY_FALLBACK run="+dungeon.RunId+" floor="+towerFG348.Floor+" reason=no-load");
    }
   }
   if(TowerActiveFG348&&towerFG348.StoryPausePending){
    if(towerFG348.Floor!=towerFG348.StoryPauseFloor)towerFG348.StoryPausePending=false;
    else if(DateTime.UtcNow>=towerFG348.StoryPauseUtc){
     towerFG348.StoryPausePending=false;
     TowerStoryPauseFG377(true);
     towerFG348.StoryResumePending=true;towerFG348.StoryResumeFloor=towerFG348.Floor;towerFG348.StoryResumeUtc=DateTime.UtcNow.AddSeconds(4);
    }
   }
   if(TowerActiveFG348&&towerFG348.StoryResumePending){
    if(towerFG348.Floor!=towerFG348.StoryResumeFloor)towerFG348.StoryResumePending=false;
    else if(DateTime.UtcNow>=towerFG348.StoryResumeUtc){towerFG348.StoryResumePending=false;TowerStoryPauseFG377(false);}
   }
   if(TowerActiveFG348&&towerFG348.DialogPending){
    if(towerFG348.Floor!=towerFG348.DialogFloor||!dungeon.Loaded||dungeon.RoomCleared)towerFG348.DialogPending=false;
    else if(DateTime.UtcNow>=towerFG348.DialogDueUtc){towerFG348.DialogPending=false;TowerDialogFG363(false);}
   }
   if(TowerActiveFG348&&towerFG348.ClearAdvancePending){
    if(towerFG348.Floor!=towerFG348.ClearAdvanceFloor||towerFG348.Floor>=towerFG348.EndFloor)towerFG348.ClearAdvancePending=false;
    else if(DateTime.UtcNow>=towerFG348.ClearAdvanceDueUtc&&dungeon.Loaded&&dungeon.RoomCleared){
     int from=towerFG348.Floor;
     towerFG348.ClearAdvancePending=false;
     try{TowerAdvanceFG348(0,false);towerFG348.AutoAdvancedFromFloor=from;towerFG348.AutoAdvancedUtc=DateTime.UtcNow;Note("TOWER_CLEAR_ADVANCE run="+dungeon.RunId+" from="+from+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap+" source=clear-timer");}
     catch(InvalidDataException e){Note("TOWER_CLEAR_ADVANCE_REJECTED run="+dungeon.RunId+" from="+from+" reason="+e.Message);}
    }
   }
   if(TowerActiveFG348&&towerFG348.AutoAdvancePending){
    if(towerFG348.Floor!=towerFG348.AutoAdvanceFloor||towerFG348.Floor>=towerFG348.EndFloor)towerFG348.AutoAdvancePending=false;
    else if(DateTime.UtcNow>=towerFG348.AutoAdvanceDueUtc&&dungeon.Loaded&&dungeon.RoomCleared){
     int from=towerFG348.Floor;
     towerFG348.AutoAdvancePending=false;
     try{TowerAdvanceFG348(0,false);towerFG348.AutoAdvancedFromFloor=from;towerFG348.AutoAdvancedUtc=DateTime.UtcNow;Note("TOWER_EMPTY_FLOOR_ADVANCED run="+dungeon.RunId+" from="+from+" floor="+towerFG348.Floor+" map="+dungeon.CurrentMap);}
     catch(InvalidDataException e){Note("TOWER_EMPTY_FLOOR_ADVANCE_REJECTED run="+dungeon.RunId+" from="+from+" reason="+e.Message);}
    }
   }
   if(!TowerActiveFG348||!towerFG348.FinishPending)return;
   if(DateTime.UtcNow<towerFG348.DueUtc)return;
   if(towerFG348.Phase==0){
    Send("tower-ranking144",Offline70RoleProtocol.BatchPacketFG(0,144,Offline70TowerRunFG348.RankingBody(dungeon.ClearMilliseconds,towerFG348.Floor),keys,false));
    try{towerFG348.ClearExperience=Offline70ClearExperienceFG116.Calculate(dungeon.DungeonId,dungeon.Difficulty,role.Level,Math.Max(1,dungeon.VerifiedKillCount),0).Award;}
    catch(InvalidDataException e){towerFG348.ClearExperience=0;Note("TOWER_EXP_PENDING reason="+e.Message);}
    towerFG348.Phase=1;towerFG348.DueUtc=DateTime.UtcNow.AddSeconds(1);
    Note("TOWER_RANKING_SENT run="+dungeon.RunId+" floor="+towerFG348.Floor+" seconds="+(dungeon.ClearMilliseconds/1000)+" exp="+towerFG348.ClearExperience);return;
   }
   if(towerFG348.Phase==1){
    Send("tower-reward145",Offline70RoleProtocol.BatchPacketFG(0,145,Offline70TowerRunFG348.RewardBody(towerFG348.ClearExperience),keys,false));
    towerFG348.Phase=2;towerFG348.DueUtc=DateTime.UtcNow.AddSeconds(5);
    Note("TOWER_REWARD_DISPLAYED run="+dungeon.RunId+" exp="+towerFG348.ClearExperience);return;
   }
   if(towerFG348.Phase==2){TowerSettleFG348();towerFG348.Phase=3;towerFG348.DueUtc=DateTime.UtcNow.AddSeconds(60);return;}
   TowerReturnFG348();
  }catch(InvalidDataException e){
   Note("TOWER_MAINTAIN_REJECTED reason="+e.Message);
   if(towerFG348!=null){towerFG348.ClearSettled=true;towerFG348.FinishPending=true;towerFG348.Phase=3;towerFG348.DueUtc=DateTime.UtcNow.AddSeconds(10);}
  }
 }
 // Commit the displayed exp once (RunId receipt), complete [condition under clear] rows for
 // this tower, then open the native EPLP menu.
 void TowerSettleFG348(){
  if(!TowerActiveFG348||towerFG348.ClearSettled)return;
  Sync();byte previous=role.Level;string receipt=dungeon.RunId+"-tower-clear";bool duplicate=false;
  uint award=towerFG348.ClearExperience;
  try{
   assets=Offline70Economy.Apply(economyPath,receipt,"tower-clear",dungeon.DungeonId+":"+towerFG348.EndFloor+":"+award,delegate(Offline70EconomyState state){state.Experience=Offline70ProgressionFG.AddExperience(state.Experience,award);state.Level=Offline70ProgressionFG.Level(state.Experience);},out duplicate);
  }catch(InvalidDataException e){Note("TOWER_CLEAR_XP_PENDING reason="+e.Message);}
  Sync();
  uint seconds=(uint)(dungeon.ClearMilliseconds/1000);
  progress=quests.Clear(questPath,dungeon.DungeonId,dungeon.Difficulty,1,(byte)0,usedRevivalCoinsFG117>0,seconds,(byte)0,0,towerFG348.EndFloor,towerFG348.EndFloor,0,receipt+"-quest");
  foreach(Offline70QuestActiveFG a in progress.Active)if(catalog.ActiveVisible(catalog.Get(a.Id),progress))Send("tower-clear-quest-progress",Offline70QuestWireFG.ProgressReply(a.Id,quests.WireRemaining(catalog.Get(a.Id),a,progress),true,keys));
  towerFG348.ClearSettled=true;
  Send("tower-experience",Offline70RoleProtocol.BatchPacketFG(0,37,Experience(),keys,true));
  LevelChanged(previous);
  bool hasTicket=Offline70EntryCostFG185.Owned(assets,4183)>0;
  Send("tower-eplp146",Offline70RoleProtocol.BatchPacketFG(0,146,new byte[]{(byte)(hasTicket?1:0)},keys,false));
  Note("TOWER_CLEAR_COMMITTED run="+dungeon.RunId+" dungeon="+dungeon.DungeonId+" floors="+towerFG348.EndFloor+" kills="+dungeon.VerifiedKillCount+" experience="+award+" duplicate="+duplicate+" seconds="+seconds+" ticketLeft="+hasTicket);
 }
 // op26 Dispatcher_EPLPCommand: broadcast op75 {1,cmd,sub}, then the native 0x4e timer returns
 // the party to the village.
 void TowerEplpFG348(byte[] body){
  if(!TowerActiveFG348)throw new InvalidDataException("Tower EPLP outside tower");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,26%14,false);
  if(plain==null||plain.Length<2)throw new InvalidDataException("Tower EPLP request size");
  byte cmd=plain[0],sub=plain[1];
  Send("tower-eplp-ack75",Offline70RoleProtocol.BatchPacketFG(1,75,new byte[]{1,cmd,sub},keys,false));
  Note("TOWER_EPLP run="+dungeon.RunId+" cmd="+cmd+" sub="+sub+" settled="+towerFG348.ClearSettled);
  if(!towerFG348.ClearSettled)return;
  // Native subCmd 3 keeps the party in the tower; this offline build has no second-run loop, so
  // it waits for the 60s fallback instead of fabricating one.
  if(sub==3){Note("TOWER_EPLP_STAY run="+dungeon.RunId+" noAward=True");return;}
  towerFG348.FinishPending=true;towerFG348.Phase=3;towerFG348.DueUtc=DateTime.UtcNow.AddSeconds(sub==2?1:3);
 }
 void TowerReturnFG348(){
  if(towerFG348==null)return;
  string run=towerFG348.RunId;int floors=towerFG348.EndFloor;
  if(towerFG348.StoryResumePending)TowerStoryPauseFG377(false);   // never leave the client frozen on exit
  towerFG348=null;
  if(dungeon.Active)dungeon.Abort();
  ReturnToEntry();
  Note("TOWER_RETURN_TOWN run="+run+" floors="+floors+" noNewAward=True");
 }
}


// FG374: a tower floor reuses one of the two cells the client knows (CellYForClientFG374), so the
// cell has to behave like a brand new room when the next floor enters it: no killed uids from the
// previous floor, not in visitedRooms (RoomBody would otherwise answer with the bare retained-room
// header and the client would keep the old actors), and no side-channel APC registrations keyed to
// (RunId, cell) from the floor that used it last.
partial class Offline70DungeonFlowFG {
 internal void TowerResetRoomFG374(){
  if(!Active)throw new InvalidDataException("Tower room reset outside run");
  int room=RoomIndex;
  dead[room].Clear();
  visitedRooms.Remove((byte)room);
  apcSlotsFG186.RemoveAll(delegate(ApcSlotFG186 s){return s.RunId==RunId&&s.Room==room;});
  // Every other per-cell cache must forget the floor that used this cell last: the room roster,
  // the rolled event monsters and the passive-object kill counters are all keyed by RoomIndex.
  eliteRoomsFG118.Remove(room);
  eventRoomFG174.Remove(room);
  passiveKillsFG360.Remove(room);
  movePending=false;
 }
}
