using System;using System.Collections.Generic;using System.IO;
// FG361: the `[quest connection]` maze layer for the dungeons whose .dgn ships more than one
// `[maze info]` block (60 热血八番街, 61 绿都, 81 根特东门, 84 根特补给战, 88 根特北门, 89 根特防御战).
//
// Native rule (rednf BattleDepsB2/GetAppropriateMaze.inc ORIG 0x082ff128 ->
// CParty::CheckQuestConnection):
//   * questType  0 = IsDoingQuest(questId)      - the maze applies while the quest is accepted
//                1 = isClearQuest(questId)      - only after it was cleared
//               -1 = base maze                  - CDungeon::set_dungeon puts it in m_mazeList
//   * a questType-0 maze also needs m_questMinDiff <= difficulty
//   * zero candidates -> CDungeon::GetRandMazebyPartyCount (the base maze)
// The table itself is generated from the PVF (local-run/pvf/gen_quest_mazes.py).
//
// Deviation, deliberate: where native draws at random among several matching quest mazes, this
// server takes the LAST matching block in .dgn order. In single play several epic stages can be
// accepted at once and a random earlier stage would hide the very room the current objective
// names (e.g. 3904 needs 10602, which only exists in the 3904 maze). Every dungeon here lists its
// blocks in chain order, so "last match" is "furthest stage".
partial class Offline70DungeonFlowFG {
 internal Offline70QuestMazeFG361.Variant MazeFG361;
 internal bool MazeLayerFG361{get{return MazeFG361!=null;}}
 // Called from ConfigureGsdFG130 right after Begin(), i.e. before the entry room packet is
 // composed, so the start cell and every later CurrentMap() answer come from the picked block.
 // Begin() already rolled [boss room entrance condition] rows against the pre-selection geometry;
 // no dungeon in this table ships that tag (it is 35/37 only), so those rolls stay valid. A future
 // dungeon carrying both would have to re-run BuildConditionStatusFG181() here.
 internal void SelectQuestMazeFG361(Offline70ProgressionStateFG state){
  MazeFG361=null;
  List<Offline70QuestMazeFG361.Variant> blocks;
  if(state==null||!Offline70QuestMazeFG361.Variants.TryGetValue(DungeonId,out blocks))return;
  Offline70QuestMazeFG361.Variant pick=null,baseMaze=null;
  foreach(Offline70QuestMazeFG361.Variant block in blocks){
   if(block.Quest==0){if(baseMaze==null)baseMaze=block;continue;}
   if(block.Quest<0)continue;   // questType 1 (isClearQuest) blocks: none in this data set
   if(state.Find(block.Quest)==null)continue;
   if(!Offline70QuestRulesFG365.Meets(block.MinDiff,Difficulty))continue;
   pick=block;
  }
  MazeFG361=pick??baseMaze;
  if(MazeFG361==null)throw new InvalidDataException("Quest maze table without a base block");
  SetClearConditionsFG372(MazeFG361.Clears);
  X=MazeFG361.StartX;Y=MazeFG361.StartY;
 }
 // Native CBattle_Field::SelectDungeon stores `m_mazeType = maze->m_mazeIndex` (+0x1ac) and the
 // dungeon entry packet 0x1c starts with `put_byte(m_mazeType)` (BattleDungeonMapA2/MakePacket.inc
 // case 0x1c); every later room packet (0x1d) only says where the party stands, so the client
 // builds its whole maze from this one byte. m_mazeIndex is the `[maze info]` block's 0-based
 // position in the .dgn (STDungeonScript/ImportDungeonScript.inc case 0x228: `m_mazeIndex =
 // m_mazeList170.size()` before push_back). Variants[] is emitted in that same .dgn order, so the
 // picked variant's list position is exactly the index the client must receive.
 internal int MazeIndexFG361{
  get{
   if(MazeFG361==null)return 0;
   List<Offline70QuestMazeFG361.Variant> blocks;
   if(!Offline70QuestMazeFG361.Variants.TryGetValue(DungeonId,out blocks))return 0;
   for(int i=0;i<blocks.Count;i++)if(object.ReferenceEquals(blocks[i],MazeFG361))return i;
   return 0;
  }
 }
 internal int MazeWidthFG361{get{return MazeFG361!=null?MazeFG361.Width:Offline70ForestFG75.Width(DungeonId);}}
 internal int MazeHeightFG361{get{return MazeFG361!=null?MazeFG361.Height:Offline70ForestFG75.Height(DungeonId);}}
 internal byte MazeStartXFG361{get{return MazeFG361!=null?MazeFG361.StartX:Offline70ForestFG75.StartX(DungeonId);}}
 internal byte MazeStartYFG361{get{return MazeFG361!=null?MazeFG361.StartY:Offline70ForestFG75.StartY(DungeonId);}}
 internal byte MazeBossXFG361{get{return MazeFG361!=null?MazeFG361.BossX:Offline70ForestFG75.BossX(DungeonId);}}
 internal byte MazeBossYFG361{get{return MazeFG361!=null?MazeFG361.BossY:Offline70ForestFG75.BossY(DungeonId);}}
 internal ushort MazeMapFG361(byte x,byte y){
  if(MazeFG361==null||x>=MazeFG361.Width||y>=MazeFG361.Height)return 0;
  return MazeFG361.Maps[y*MazeFG361.Width+x];
 }
 internal int MazeMaskFG361(byte x,byte y){
  if(MazeFG361==null||x>=MazeFG361.Width||y>=MazeFG361.Height)return 0;
  return MazeFG361.Masks[y*MazeFG361.Width+x];
 }
 // The current room's `[monster]` rows. Same shape as Forest-Content-FG75.Monsters(), but read
 // from the picked block's own map (a quest block may reuse a base map or bring a new one).
 ushort[][] MazeRowsFG361(){
  if(MazeFG361==null)return null;
  ushort map=MazeMapFG361(X,Y);
  ushort[][] rows;
  // FG409: at hero difficulty the room the client builds is the cell map's
  // `[heroes mode map index]` variant (native CBattle_Field::getMap), not the block's pinned
  // map, so the roster comes from the generated hero table. Quest-only room maps (d52 6514/6515)
  // carry no hero index and keep their block roster.
  ushort heroMapFG409;if(map!=0&&Difficulty==Offline70HeroUnlockFG358.HeroDifficulty&&Offline70HeroMapsFG409.Pairs.TryGetValue(map,out heroMapFG409)){ushort[][] heroRowsFG409=Offline70HeroMapsFG409.Roster(heroMapFG409);if(heroRowsFG409!=null)return heroRowsFG409;}
  // A cell without a map specification / grid value is not a room (CanMove already refuses to
  // walk into it). Composing a body there yields an empty roster instead of a "roster for map 0"
  // throw; a non-zero map missing from the table is still a generator bug and stays fatal.
  if(map==0)return new ushort[0][];
  if(!Offline70QuestMazeFG361.Rows.TryGetValue(map,out rows))throw new InvalidDataException("Quest maze roster for map "+map);
  return rows;
 }
 // Enemy `[ai character]` actors declared by the current room (native add_monster_APC_AI).
 // They ride the same side channel as ApcMonstersFG: outside TotalCount, no .mob drops, and
 // their op42 death still feeds the [hunt enemy] counter through ApcIdAtUid.
 internal List<ushort[]> QuestMazeApcsFG361(){
  List<ushort[]> result=new List<ushort[]>();
  if(MazeFG361==null)return result;
  List<ushort[]> rows;
  if(Offline70QuestMazeFG361.Apcs.TryGetValue(CurrentMap,out rows))foreach(ushort[] row in rows)result.Add(row);
  return result;
 }
 // True when this room already declares that actor: d140's base boss (map 16009, APC 5310 role 8)
 // is the same JangSamRang the FG211 quest-gated line adds at king/hero, and an actor may only be
 // written once (the client keys actors by uid; a second row would be a duplicate spawn).
 internal bool ApcDeclaredByQuestMazeFG361(ushort id){
  foreach(ushort[] row in QuestMazeApcsFG361())if(row[1]==id)return true;
  return false;
 }
 // ---- [ridable]: d88's AT-5T walker (61235) --------------------------------------------------
 // Native path (rednf CDungeon.h RidableScript + CRidable.h + CRidable/CheckAppearRidableObject.inc
 // + BattleDungeonMapA2/MakePacket.inc):
 //   * SetRidableObject() copies the `[ridable]` objects into CRidable with state 4 ("appear"),
 //     keyed by m_field0c, and only `[select]` of them: the block header is
 //     `[ridable] <select> <regenerate>`, and while select < row count the pool is shuffled and
 //     back() popped select times. d88 ships `1 1` over four candidate rows, so a run owns ONE
 //     walker in one random cell, not four;
 //   * the room packet (op29) ends with `<itemRows> <hellPartyActive> <rideableCount> <rows>
 //     <seatNo>`, and each row is exactly four ints
 //     (m_field10, m_field14, m_field0c, m_field18) - CRidable::SendSpawnRidableObject, ORIG
 //     0x82fece2; CheckAppearRidableObject patches the count byte it wrote first and only emits
 //     objects whose m_x/m_y are the room it is composing;
 //   * a `[ridable]` row of this PVF is `<cellX> <cellY> <monsterId> <pixelX> <pixelY>`, so
 //     m_x/m_y = the cell, m_field10/m_field14 = the pixel position, m_field0c = the walker mob,
 //     and m_field18 keeps the RidableObject ctor default 0xc8 = 200 ([neutral]).
 //   * the ride request is op 130 (rednf Dispatchers/dispatch_sig_130.inc): the reply is the
 //     generic command ack for command 0x13d = 317 (ok: one byte 1, error: 0,0x13).
 // This CN70 client ignores that section: the 2026-10-29 live d88 run entered cells (0,0)=15064
 // and (1,0)=15065 with the 17-byte section in the op29 frame (SENT size 175 = 15 header +
 // pad16(140 body + 17)) and no walker appeared; the client never sent op130 either. The same
 // client's actor rows are 14 bytes, not TW1031's 16, so its room protocol is a different
 // generation than rednf's ORIG. Default is therefore the actor-row spawn: 61235 is a real
 // monster.lst entry (NewMonsters/Riding/at_5t/at_5t.mob) with its own `[ridableskill
 // definition]`, so the client resolves it from the monster table and can mount it.
 // DNF70_AT5T_RIDABLE=1 re-enables the TW-style room-packet section for clients that parse it.
 // Both paths go through SelectedRidablesFG361(), so neither spawns the walker in a second cell.
 internal static readonly bool RideableNativeFG361=Environment.GetEnvironmentVariable("DNF70_AT5T_RIDABLE")=="1";
 // The [ridable] objects of the current cell as {cellX, cellY, monsterId, pixelX, pixelY} rows.
 static readonly List<ushort[]> noRidablesFG361=new List<ushort[]>();
 // Every candidate row the dungeon declares, regardless of where the party stands.
 internal List<ushort[]> RidablesFG361(){
  if(MazeFG361==null)return noRidablesFG361;
  ushort[] cells;
  if(!Offline70QuestMazeFG361.Ridable.TryGetValue(DungeonId,out cells))return noRidablesFG361;
  List<ushort[]> all=new List<ushort[]>();
  for(int i=0;i+4<cells.Length;i+=5)all.Add(new ushort[]{cells[i],cells[i+1],cells[i+2],cells[i+3],cells[i+4]});
  return all;
 }
 // [select]: how many of the candidates the native run actually owns. `[ridable] 1 1` (d88) means
 // one of the four cells, not all four — spawning one per cell let the player meet a second AT-5T
 // in the next random room, and while mounted it put the mount and that room's walker on screen
 // together. CRidable keys m_objects by the walker mob id, so one walker is also the native
 // ceiling for a single mob id. The pick follows RunId, like the native random_shuffle.
 internal List<ushort[]> SelectedRidablesFG361(){
  List<ushort[]> all=RidablesFG361();
  ushort select;
  if(all.Count==0||!Offline70QuestMazeFG361.RidableSelect.TryGetValue(DungeonId,out select)||select==0)return noRidablesFG361;
  if(select>=all.Count)return all;
  string seed=String.IsNullOrEmpty(RunId)?"dungeon-"+DungeonId:RunId;
  Func<int,int> draw=Offline70QuestDropsFG.StableDraws(seed+"-ridable-"+DungeonId);
  List<ushort[]> pool=new List<ushort[]>(all);
  List<ushort[]> picked=new List<ushort[]>();
  for(int i=0;i<select;i++){int at=draw(pool.Count);picked.Add(pool[at]);pool.RemoveAt(at);}
  return picked;
 }
 internal List<ushort[]> CurrentRidablesFG361(){
  List<ushort[]> result=new List<ushort[]>();
  foreach(ushort[] row in SelectedRidablesFG361())if(row[0]==X&&row[1]==Y)result.Add(row);
  return result;
 }
 internal int CurrentRideableCountFG361{get{return CurrentRidablesFG361().Count;}}
 // The native room-packet section: one count byte, then four ints per object. Empty (zero bytes)
 // for every room that declares no `[ridable]`, so those rooms keep their exact previous bytes.
 internal byte[] RideableSectionFG361(){
  List<ushort[]> rows=CurrentRidablesFG361();
  if(!RideableNativeFG361||rows.Count==0)return new byte[0];
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)rows.Count);
   foreach(ushort[] row in rows){w.Write((int)row[3]);w.Write((int)row[4]);w.Write((int)row[2]);w.Write((int)200);}
   return m.ToArray();
  }
 }
 // Actor-row spawn (default path): the walker is written into the room packet as an ordinary
 // monster actor of this room (uid in the APC side-channel range, so it never counts towards
 // TotalCount and no drop plan is attempted for it). role 0 is exactly the byte every normal
 // roster row carries (`Quest-Maze-FG361.Rows` third value), which is what makes the client
 // resolve mob 61235 from monster.lst; role 5 = [ai character] would send it to aicharacter.lst,
 // where 61235 does not exist. at_5t.mob itself declares team 200 (`[int data] ... 200 ...`),
 // so the actor is neutral rather than a hostile monster.
 internal List<ushort[]> QuestMazeRidablesFG361(){
  List<ushort[]> result=new List<ushort[]>();
  if(RideableNativeFG361)return result;
  foreach(ushort[] row in CurrentRidablesFG361())
   result.Add(new ushort[]{CurrentMap,row[2],(ushort)Offline70ClearExperienceFG116.Basis(DungeonId),(ushort)0});
  return result;
 }
 // ---- boss APC settlement ---------------------------------------------------
 // d89's final room (map 17045) has no `[monster]` row at all: the boss is the `[ai character]`
 // 10409 (gunner/Babylon, roleType 8) and the native run settles when that actor dies
 // (add_monster_APC_AI pushes roleType 8 into m_apcList). Rooms that also carry a kind-3 monster
 // keep the ordinary boss gate, so no other dungeon changes behaviour.
 internal bool BossApcGateFG361{
  get{
   if(!IsBossRoom)return false;
   List<ushort[]> rows;
   if(!Offline70QuestMazeFG361.Apcs.TryGetValue(CurrentMap,out rows))return false;
   bool boss=false;
   foreach(ushort[] row in rows)if(row[3]==8)boss=true;
   if(!boss)return false;
   for(ushort uid=100;uid<100+CurrentCount;uid++)if(Monster(uid)[2]==3)return false;
   return true;
  }
 }
 internal bool BossApcSlainFG361{
  get{
   bool declared=false;
   foreach(ushort[] row in QuestMazeApcsFG361()){
    if(row[3]!=8)continue;
    bool dead=false;
    foreach(ApcSlotFG186 slot in apcSlotsFG186)if(slot.RunId==RunId&&slot.Room==RoomIndex&&slot.Id==row[1]&&slot.Dead){dead=true;break;}
    if(!dead)return false;
    declared=true;
   }
   return declared;
  }
 }
 // The settlement handshake (op127 boss state + op31 clear request) is sent from the op42 APC
 // branch for this actor: it is a real boss, not a scenery APC.
 internal bool BossApcTailFG361(ushort uid){return IsBossRoom&&ApcRoleAtUid(uid)==8;}
 // Read-only views of the private maze helpers, so offline drivers can walk a variant with the
 // server's own admission rules instead of re-implementing them.
 internal ushort MapAtFG361(byte x,byte y){return MapFG130(x,y);}
 internal bool CanMoveFG361(byte x,byte y){return CanMoveFG130(x,y);}
 internal int MaskAtFG361(byte x,byte y){return MaskFG130(x,y);}
 internal static bool EdgeFG361(int fromMask,int toMask,int dx,int dy){
  int from=dx==1?1:dx==-1?4:dy==1?8:2,to=dx==1?4:dx==-1?1:dy==1?2:8;
  return (fromMask&from)!=0&&(toMask&to)!=0;
 }
}
partial class Offline70BatchServiceFG {
 // op 130 = Dispatcher_OperateRidableObject (rednf Dispatchers/dispatch_sig_130.inc): the client
 // reports "start ride" (type 2) / "end ride" (types 1,3) for one rideable index. The native
 // handler answers the generic command packet for command 0x13d = 317 and has no other payload,
 // so the request body itself needs no decryption here. Accept whenever the *dungeon* declares
 // `[ridable]` objects, not just the current cell: the player rides the walker across room
 // borders and then dismounts in a room that has no walker of its own, which used to be answered
 // with 0x13 ("cannot use") in the actor-row path because the native section was switched off.
 void RidableOpFG361(byte[] body){
  bool declares=Offline70QuestMazeFG361.Ridable.ContainsKey(dungeon.DungeonId);
  bool ok=dungeon.Active&&dungeon.Loaded&&declares;
  Note("RIDABLE_OP bytes="+body.Length+" dungeon="+dungeon.DungeonId+" room="+dungeon.Y+" rideables="+dungeon.CurrentRideableCountFG361+" declares="+declares+" ok="+ok);
  Ack(317,ok);
 }
}
