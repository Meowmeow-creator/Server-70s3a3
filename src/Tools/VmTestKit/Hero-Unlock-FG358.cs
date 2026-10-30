using System;using System.IO;using System.Collections.Generic;
// FG358: the default difficulty ladder stops at king (3). Hero (4) is not a per-dungeon static
// value: seven .dgn files carry [herosmode enable] 1 plus [herosmode required quest], and the
// native client lights the tier from the cleared-dungeon list that CUser::enumDungeonClear builds
// (required quest completed -> that dungeon's row becomes 4). The server therefore advertises
// 0..3 everywhere and only writes 4 for the dungeon whose quest is in the character's completed
// set. 1504 GhostTrain carries [herosmode enable] but no required quest, so it stays at 3 just
// like the native clear-list path (getHeroesRequiredQuest() returns 0).
//
// Dungeon -> required quest (PVF tags, verified by scanning all 263 .dgn):
//   52 DanceingButterfly -> 5823      53 SuspiciousVillage -> 5835
//   83 NightAssault      -> 5884      84 Supplycut          -> 5886
//   85 CounterAttack     -> 5888      86 pirateonthetrain  -> 5855
//   87 seatrainretaking  -> 5880      1504 GhostTrain      -> (none)
class Offline70HeroUnlockFG358 {
 internal const byte NormalMaximumDifficulty=3;
 internal const byte HeroDifficulty=4;
 static readonly Dictionary<ushort,ushort> RequiredQuest=new Dictionary<ushort,ushort>{
  {52,5823},{53,5835},{83,5884},{84,5886},{85,5888},{86,5855},{87,5880}};
 static readonly Dictionary<ushort,ushort> QuestDungeon=BuildReverse();
 static Dictionary<ushort,ushort> BuildReverse(){
  Dictionary<ushort,ushort> result=new Dictionary<ushort,ushort>();
  foreach(KeyValuePair<ushort,ushort> pair in RequiredQuest)result.Add(pair.Value,pair.Key);
  return result;
 }
 internal static bool IsHeroDungeon(ushort dungeon){return RequiredQuest.ContainsKey(dungeon);}
 internal static ushort RequiredQuestFor(ushort dungeon){
  ushort quest;return RequiredQuest.TryGetValue(dungeon,out quest)?quest:(ushort)0;
 }
 internal static ushort DungeonForQuest(ushort quest){
  ushort dungeon;return QuestDungeon.TryGetValue(quest,out dungeon)?dungeon:(ushort)0;
 }
 internal static bool Unlocked(ushort dungeon,Offline70ProgressionStateFG state){
  ushort quest=RequiredQuestFor(dungeon);
  return quest!=0&&state!=null&&state.Completed.Contains(quest);
 }
 internal static byte MaximumDifficulty(ushort dungeon,Offline70ProgressionStateFG state){
  return Unlocked(dungeon,state)?HeroDifficulty:NormalMaximumDifficulty;
 }
 // Defense in depth for the entry path: 0..3 is always admitted (the caller still checks the
 // absolute TestMaximumDifficulty cap), 4 only for a hero dungeon whose required quest is done.
 internal static void RequireEntry(ushort dungeon,byte difficulty,Offline70ProgressionStateFG state){
  if(difficulty<=NormalMaximumDifficulty)return;
  if(difficulty!=HeroDifficulty)throw new InvalidDataException("Difficulty beyond hero tier");
  if(!Unlocked(dungeon,state))throw new InvalidDataException("Hero difficulty requires the completed hero quest");
 }
}
