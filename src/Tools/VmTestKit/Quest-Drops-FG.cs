using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;
class Offline70QuestDropFG {internal ushort QuestId;internal int ItemId;internal uint Quantity;internal int Chance,MaximumCount;}
class Offline70QuestDropsFG {
 // A kill/clear receipt determines its chance once, including retry after a
 // committed inventory transaction. Never reroll to force a quest item drop.
 internal static int Roll(string receipt){if(String.IsNullOrEmpty(receipt)||receipt.Length>128)throw new InvalidDataException("Quest drop receipt");using(SHA256 sha=SHA256.Create())return (int)(BitConverter.ToUInt32(sha.ComputeHash(Encoding.ASCII.GetBytes(receipt)),0)%100);}
 internal static List<Offline70QuestDropFG> Monster(Offline70QuestCatalogFG catalog,Offline70ProgressionStateFG state,int monster,int dungeon,int difficulty,int roll){if(roll<0||roll>=100)throw new InvalidDataException("Quest drop roll0..99");return Monster(catalog,state,monster,dungeon,difficulty,delegate(int maximum){return roll%maximum;});}
 internal static List<Offline70QuestDropFG> Monster(Offline70QuestCatalogFG catalog,Offline70ProgressionStateFG state,int monster,int dungeon,int difficulty,string receipt){return Monster(catalog,state,monster,dungeon,difficulty,StableDraws(receipt));}
 internal static Func<int,int> StableDraws(string receipt){if(String.IsNullOrEmpty(receipt)||receipt.Length>128)throw new InvalidDataException("Quest drop receipt");int draw=0;return delegate(int maximum){if(maximum<=0||maximum>10000||draw>=1000000)throw new InvalidDataException("Quest random draw bounds");using(SHA256 sha=SHA256.Create())return (int)(BitConverter.ToUInt32(sha.ComputeHash(Encoding.ASCII.GetBytes(receipt+"|draw"+(draw++))),0)%(uint)maximum);};}
 // TW CheckQuestMonster chooses one matching candidate for each active quest.
 // Amount means independent chance attempts. Only successful capped candidates
 // retry; a failed roll ends that quest. US60 uses the same control flow. Difficulty 4 (hero)
 // rows are shipped in the current catalog (5822/5854/5879/5883/5885), so both row validators
 // accept 0..4; a value above 4 is still rejected.
 internal static List<Offline70QuestDropFG> Monster(Offline70QuestCatalogFG catalog,Offline70ProgressionStateFG state,int monster,int dungeon,int difficulty,Func<int,int> random){
  if(catalog==null||state==null||random==null||monster<=0||dungeon<=0||difficulty<0||difficulty>4)throw new InvalidDataException("Quest monster event profile");List<Offline70QuestDropFG> result=new List<Offline70QuestDropFG>();Dictionary<int,uint> counts=new Dictionary<int,uint>();foreach(Offline70ItemCountFG owned in state.Items)counts.Add(owned.Id,owned.Quantity);
  foreach(Offline70QuestActiveFG active in state.Active){Offline70QuestDefinitionFG q=catalog.Get(active.Id);int[] rows=q.MonsterDrops;if(rows.Length%7!=0)throw new InvalidDataException("Quest monster drop row width");List<int> candidates=CandidatesFG365(rows,monster,dungeon,difficulty);
   while(candidates.Count>0){int selected=random(candidates.Count);if(selected<0||selected>=candidates.Count)throw new InvalidDataException("Quest candidate draw bounds");int offset=candidates[selected],item=rows[offset+3],chance=rows[offset+5],maximum=rows[offset+6];uint quantity=0;for(int attempt=0;attempt<rows[offset+4];attempt++){int roll=random(100);if(roll<0||roll>=100)throw new InvalidDataException("Quest chance draw bounds");if(roll<chance)quantity++;}if(quantity==0)break;uint existing;counts.TryGetValue(item,out existing);if(maximum>=0&&existing>=(uint)maximum){candidates.RemoveAt(selected);continue;}if(maximum>=0)quantity=Math.Min(quantity,(uint)maximum-existing);if(quantity==0)continue;counts[item]=checked(existing+quantity);result.Add(new Offline70QuestDropFG{QuestId=q.Id,ItemId=item,Quantity=quantity,Chance=chance,MaximumCount=maximum});break;}
  }return result;
 }
 // FG365 relaxed difficulty makes RowMatches accept every difficulty, so the per-difficulty
 // copies of the SAME item all landed in one candidate list, and the native single pick
 // (CheckKillMonster draws one row, then rolls only that row's chance) averaged a graded drop
 // down: 4041 追查皇女的行踪 ships 10/20/30/40 per difficulty but rolled ~25% at 王者
 // (live 2026-10-29 report "刷了好几把都刷不出[皇女的踪迹]"). Native matching keeps only
 // "-1 = any" plus the exact difficulty, so reproduce that set per item, and fall back to the
 // closest shipped difficulty only when the player's difficulty has no row at all - that
 // fallback is what keeps FG365's high-only quests droppable on 普通.
 internal static List<int> CandidatesFG365(int[] rows,int monster,int dungeon,int difficulty){
  Dictionary<int,List<int>> matches=new Dictionary<int,List<int>>();List<int> order=new List<int>();
  for(int i=0;i<rows.Length;i+=7){
   if(rows[i]<=0||rows[i+1]< -1||rows[i+2]< -1||rows[i+2]>4||rows[i+3]<=2||rows[i+4]<0||rows[i+4]>1000||rows[i+5]<0||rows[i+5]>100||rows[i+6]< -1)throw new InvalidDataException("Quest monster drop fields");
   if(rows[i]!=monster||!(rows[i+1]<0||rows[i+1]==dungeon)||!Offline70QuestRulesFG365.RowMatches(rows[i+2],difficulty))continue;
   int item=rows[i+3];List<int> group;if(!matches.TryGetValue(item,out group)){group=new List<int>();matches.Add(item,group);order.Add(item);}group.Add(i);
  }
  List<int> candidates=new List<int>();
  foreach(int item in order){
   List<int> group=matches[item],exact=new List<int>(),higher=new List<int>(),lower=new List<int>();int higherDiff=Int32.MaxValue,lowerDiff=Int32.MinValue;
   foreach(int row in group){
    int diff=rows[row+2];
    if(diff<0||diff==difficulty){exact.Add(row);continue;}
    if(diff>difficulty){if(diff<higherDiff){higherDiff=diff;higher.Clear();}if(diff==higherDiff)higher.Add(row);continue;}
    if(diff>lowerDiff){lowerDiff=diff;lower.Clear();}if(diff==lowerDiff)lower.Add(row);
   }
   // Strict mode already filtered every other difficulty out, so `exact` is the whole group.
   if(exact.Count>0)candidates.AddRange(exact);else if(higher.Count>0)candidates.AddRange(higher);else candidates.AddRange(lower);
  }
  return candidates;
 }
 internal static List<Offline70QuestDropFG> Clear(Offline70QuestCatalogFG catalog,Offline70ProgressionStateFG state,int dungeon,int difficulty,int roll){if(roll<0||roll>=100)throw new InvalidDataException("Quest clear drop roll0..99");return Clear(catalog,state,dungeon,difficulty,delegate(int maximum){return roll%maximum;});}
 internal static List<Offline70QuestDropFG> Clear(Offline70QuestCatalogFG catalog,Offline70ProgressionStateFG state,int dungeon,int difficulty,string receipt){return Clear(catalog,state,dungeon,difficulty,StableDraws(receipt));}
 // TW Quest::get_clear_itemlist 08353404 rolls each stackable attempt using
 // chance >= roll. Equipment has one roll and one item. Clear rows all run;
 // they do not use the monster path's single-candidate selection.
 internal static List<Offline70QuestDropFG> Clear(Offline70QuestCatalogFG catalog,Offline70ProgressionStateFG state,int dungeon,int difficulty,Func<int,int> random){
  if(catalog==null||state==null||random==null||dungeon<=0||difficulty<0||difficulty>4)throw new InvalidDataException("Quest clear event profile");List<Offline70QuestDropFG> result=new List<Offline70QuestDropFG>();Dictionary<int,uint> counts=new Dictionary<int,uint>();foreach(Offline70ItemCountFG owned in state.Items)counts.Add(owned.Id,owned.Quantity);
  foreach(Offline70QuestActiveFG active in state.Active){Offline70QuestDefinitionFG q=catalog.Get(active.Id);int[] rows=q.ClearDrops;if(rows.Length%6!=0)throw new InvalidDataException("Quest clear drop row width");for(int i=0;i<rows.Length;i+=6){if(rows[i]< -1||rows[i+1]< -1||rows[i+1]>4||rows[i+2]<=2||rows[i+3]<0||rows[i+3]>1000||rows[i+4]<0||rows[i+4]>100||rows[i+5]< -1)throw new InvalidDataException("Quest clear drop fields");if(rows[i]>=0&&rows[i]!=dungeon||!Offline70QuestRulesFG365.RowMatches(rows[i+1],difficulty))continue;int item=rows[i+2],maximum=rows[i+5];uint existing;counts.TryGetValue(item,out existing);if(maximum>=0&&existing>=(uint)maximum)continue;Offline70EconomyItemDefinition definition=Offline70Economy.Definition((uint)item);int attempts=definition.Kind==4?1:rows[i+3];uint quantity=0;for(int attempt=0;attempt<attempts;attempt++){int roll=random(100);if(roll<0||roll>=100)throw new InvalidDataException("Quest clear chance draw bounds");if(roll<=rows[i+4])quantity++;}if(maximum>=0)quantity=Math.Min(quantity,(uint)maximum-existing);if(quantity==0)continue;counts[item]=checked(existing+quantity);result.Add(new Offline70QuestDropFG{QuestId=q.Id,ItemId=item,Quantity=quantity,Chance=rows[i+4],MaximumCount=maximum});}
  }return result;
 }
}
