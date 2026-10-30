using System;using System.IO;using System.Collections.Generic;
// Current CN70 PVF and complete TW8535ED2 / 8537070 ordinary solo routes.
// General random equipment pool and3227 secondary material await separate evidence.
class Offline70FloorDropsFG119 {
 internal const ushort FirstSlot=2000,LastSlot=4095;internal const int MaximumRows=32;
 internal class Reward {internal uint Item,Quantity;internal string Source;}
 internal static int Condition(byte level,uint gold){if(level<1||level>70)throw new InvalidDataException("Floor wallet level");return (int)Math.Min(100UL,(ulong)gold*100UL/Offline70FloorCatalogFG119.MoneyCap[level]);}
 internal static int GoldThreshold(byte basis,byte difficulty,byte kind,int condition,int standard){if(basis<1||basis>99||difficulty>4||kind>3||standard<0||standard>100)throw new InvalidDataException("Floor gold parameters");int p=0;foreach(int[] row in Offline70FloorCatalogFG119.Probability)if(basis>=row[0]&&basis<=row[1]){p=row[2];break;}int threshold=(int)((double)p*Offline70FloorCatalogFG119.GoldDifficulty[difficulty]*Offline70FloorCatalogFG119.GoldKind[kind]*Offline70FloorCatalogFG119.GoldParty[0]);condition=Math.Max(0,Math.Min(100,condition));if(condition>50){float penalty=(float)((double)(condition-50)*0.002f+0.01f);threshold=(int)((double)threshold*(1.0-penalty));}return threshold*standard/100;}
 internal static uint GoldAmount(byte monsterLevel,int variance){if(monsterLevel<1||monsterLevel>99||variance<-15||variance>15)throw new InvalidDataException("Floor gold row or variance");int basis=(int)Offline70FloorCatalogFG119.Gold[monsterLevel];return (uint)Math.Max(1,basis+basis*variance/100);}
 internal static uint[] GoldPiles(byte monsterLevel,byte basis,byte difficulty,byte kind,int condition,int standard,uint draw,int variance,uint bossCondition,uint bossQuantity){int threshold=GoldThreshold(basis,difficulty,kind,condition,standard);if(draw>=10000||bossCondition>=100||bossQuantity>=2)throw new InvalidDataException("Floor gold draw");if(draw>=threshold)return new uint[0];uint amount=GoldAmount(monsterLevel,variance);if(kind!=3)return new uint[]{amount};bool large=bossCondition<10;int count=large?4+(int)bossQuantity:1;amount=(uint)Math.Max(1,(int)amount*(large?110:100)/100);uint[] piles=new uint[count];for(int i=0;i<count;i++)piles[i]=amount;return piles;}
 internal static uint Specific(uint monster,byte kind,uint draw){if(kind>3||draw>=10000)throw new InvalidDataException("Floor specific draw");uint[][][] profiles;if(!Offline70FloorCatalogFG119.MonsterPools.TryGetValue(monster,out profiles)&&!Offline70QuestMazeFG361.Pools.TryGetValue(monster,out profiles)&&!Offline70RandomMazeFG362.Pools.TryGetValue(monster,out profiles))return 0;uint cumulative=0;foreach(uint[] row in profiles[kind]){cumulative=checked(cumulative+row[1]);if(draw<cumulative)return row[0];}return 0;}
 // Quest-maze rooms (FG361) are rooms like any other: Offline70QuestMazeFG361.Rolls carries the
 // same per-instance tok7/tok8 pair in the same uid-100 order, and Pools carries the .mob pools of
 // roster ids the generated catalog does not know. Both are consulted only when the catalog has no
 // row for that map / monster, so no shipped room changes behaviour.
 internal static List<Reward> Plan(Offline70DungeonFlowFG dungeon,ushort uid,byte playerLevel,uint gold,string receipt){
  if(dungeon==null||!dungeon.Active||!dungeon.Loaded)throw new InvalidDataException("Floor plan outside room");
  ushort[] mon=dungeon.Monster(uid);
  // Towers drop from their own table only (FG364). A tower monster's `.mob` [item] rows are the
  // ordinary outside-tower drops; inside the tower the native uses `[death tower item]` and the
  // floor-clear reward (etc/deathtower.etc) for gold/exp, so nothing below this branch applies:
  // no gold pile, no equipment, no stack/world/independent roll (live report 2026-10-27:
  // "塔内怪物掉的东西错误了，塔内只会掉一些专属的【塔】无色小晶块之类的道具").
  if(dungeon.TowerFlowFG211)return Offline70DeathTowerDropsFG364.Plan(mon[0],receipt);
  // FG409: a hero-tier room is the `[heroes mode map index]` .map (native getMap), and its
  // per-instance tok7/tok8 counts live in the generated hero table. Falling back to {1,0} there
  // would silently strip every item roll from hero rooms.
  byte[][] map;byte[] rolls;if(Offline70FloorCatalogFG119.MapRolls.TryGetValue(dungeon.CurrentMap,out map)||Offline70QuestMazeFG361.Rolls.TryGetValue(dungeon.CurrentMap,out map)||(map=Offline70RandomMazeFG362.Rolls(dungeon.CurrentMap))!=null||(map=Offline70HeroMapsFG409.RollTable(dungeon.CurrentMap))!=null){int rowFG375=uid-100;if(rowFG375<0)throw new InvalidDataException("Floor map roster");if(rowFG375>=map.Length){if(rowFG375>=map.Length+Offline70SpecialSpawnsFG375.MaxSpawns(dungeon.CurrentMap))throw new InvalidDataException("Floor map roster");rolls=new byte[]{0,0};}else rolls=map[rowFG375];}else rolls=new byte[]{1,0};byte basis=(byte)Offline70ClearExperienceFG116.Basis(dungeon.DungeonId);byte kind=(byte)mon[2];List<Reward> rewards=new List<Reward>();Func<int,int> goldDraw=Offline70QuestDropsFG.StableDraws(receipt+"-floor-gold"),specificDraw=Offline70QuestDropsFG.StableDraws(receipt+"-floor-specific");int condition=Condition(playerLevel,gold);
  for(int i=0;i<rolls[0];i++){uint chance=(uint)goldDraw(10000);int variance=goldDraw(31)-15;uint volumeDraw=(uint)goldDraw(100),pileDraw=(uint)goldDraw(2);foreach(uint amount in GoldPiles((byte)mon[1],basis,dungeon.Difficulty,kind,condition,100,chance,variance,volumeDraw,pileDraw))rewards.Add(new Reward{Item=0,Quantity=amount,Source="gold"});}
  for(int i=0;i<rolls[1];i++){uint item=Specific(mon[0],0,(uint)specificDraw(10000));if(item!=0)rewards.Add(new Reward{Item=item,Quantity=1,Source="specific-normal"});}
  if(kind>0){uint item=Specific(mon[0],kind,(uint)specificDraw(10000));if(item!=0)rewards.Add(new Reward{Item=item,Quantity=1,Source="specific-kind"});}
  // FG359 ordinary equipment: the native random phase (special table first, then type 2/4).
  // The special table is skipped only on the kill that finished a hell group (ref.m_field40).
  Offline70EquipDropsFG359.Append(rewards,mon[1],basis,dungeon.Difficulty,kind,receipt,dungeon.PendingHellRollsFG356==0);
  Offline70GeneralStackFG126.Append(rewards,mon[1],basis,dungeon.Difficulty,kind,rolls[0],receipt);
  Offline70WorldDropsFG182.Append(rewards,mon[1],kind,receipt);
  // Native independent drops (FG355): rates[difficulty], monster-level range filter, expect[0]
  // attempts for a solo party, then cumulative [list] picks. It runs after the older sources and
  // fills only the remaining ground-row capacity (MaximumRows), so this new source can never turn
  // a previously accepted kill into a capacity rejection. Its own receipt suffix keeps the
  // deterministic draws stable per kill without disturbing the floor draws above.
  Offline70IndependentDropsFG355.Append(rewards,mon[0],dungeon.Difficulty,(byte)mon[1],receipt+"-independent");
  // FG365: the 0.3% global challenge-book roll the original service ran on ordinary and
  // ancient monsters (the PVF has the items and hell consumes them, but no table names them).
  Offline70ChallengeBookFG365.Append(rewards,dungeon,(byte)mon[1],(byte)mon[2],receipt+"-challenge-book");
  if(rewards.Count>MaximumRows)throw new InvalidDataException("Floor row capacity");return rewards;
 }
}
