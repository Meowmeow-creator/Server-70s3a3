using System;
// FG365: the 0.3% challenge-book source for ordinary/ancient dungeons.
//
// Player report (2026-10-27): "0.3挑战书白图远古图都没有掉落". The PVF ships the two
// challenge books as stackable quest items (4328 燃烧恶魔的挑战书, 4329 蓝冰恶魔的挑战书)
// and the hell entry consumes them, but no `.mob`, object pool, independent table or world
// table references them: in the original service they were a low-rate global monster drop.
// Restore that single-player source at 0.3% per non-boss kill (30/10000), split at the
// hell difficulty break the PVF uses (level <=44 -> burning, >=45 -> blue ice). Towers keep
// their own `[death tower item]` table and hell rooms keep their party rolls, so neither is
// touched here.
internal static class Offline70ChallengeBookFG365 {
 internal const uint Burning=4328,BlueIce=4329;
 internal static void Append(System.Collections.Generic.List<Offline70FloorDropsFG119.Reward> rewards,Offline70DungeonFlowFG dungeon,int monsterLevel,byte kind,string receipt){
  if(rewards==null||dungeon==null||String.IsNullOrEmpty(receipt)||monsterLevel<1||monsterLevel>99)return;
  if(kind==3||dungeon.TowerFlowFG211||dungeon.HellRoomActiveFG356)return;
  if(rewards.Count>=Offline70FloorDropsFG119.MaximumRows)return;
  if(Offline70ClearItemsFG125.Draw(receipt,"challenge-book-fg365",10000)>=30)return;
  uint item=monsterLevel<=44?Burning:BlueIce;
  rewards.Add(new Offline70FloorDropsFG119.Reward{Item=item,Quantity=1,Source="challenge-book-fg365"});
 }
}
