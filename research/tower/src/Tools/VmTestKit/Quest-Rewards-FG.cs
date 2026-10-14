using System;using System.IO;
class Offline70QuestBasicRewardFG {internal uint Experience,Gold;}
partial class Offline70QuestRewardsFG {
 // These are basic single-player rewards only. TW70's getters use table[level],
 // not DSX60's table[level-1]. Current PVF metadata and native getter execution
 // are recorded beside the fixtures. Premium/event/growth/anti-abuse are absent.
 internal static Offline70QuestBasicRewardFG Calculate(Offline70QuestDefinitionFG q,byte level){return Calculate(q,level,q==null?null:q.Rewards);}
 internal static Offline70QuestBasicRewardFG Calculate(Offline70QuestDefinitionFG q,byte level,int[] rewards){
  if(q==null||!q.Supported||q.MinLevel<1||q.MinLevel>70||level<1||level>70||q.GoldMultiple<0||q.GoldMultiple>1000000||rewards==null||rewards.Length%2!=0)throw new InvalidDataException("Basic quest reward profile");
  int gap=level-q.MinLevel,penalty=q.IgnoreRewardLevel||gap<=6?100:gap<=11?GreenPenalty:GreyPenalty;
  int lookup=q.IgnoreRewardLevel?level:q.MinLevel;bool hasGold=false;for(int i=0;i<rewards.Length;i+=2){if(rewards[i]<0||rewards[i+1]<=0)throw new InvalidDataException("Quest reward pair");if(rewards[i]==0)hasGold=true;}
  ulong experience=q.Repeatable?0:((ulong)ExpTable[lookup]*(uint)Weight(q.Difficulty)/100)*(uint)penalty/100;
  ulong gold=hasGold?((ulong)GoldTable[lookup]*(uint)penalty/100)*(uint)q.GoldMultiple/100:0;
  // Native arithmetic truncates both divisions separately. Do not add a minimum
  // one-gold rule or treat the nominal item0 count as an additional grant.
  if(experience>int.MaxValue||gold>int.MaxValue)throw new InvalidDataException("Native quest basic reward arithmetic bounds");
  return new Offline70QuestBasicRewardFG{Experience=(uint)experience,Gold=(uint)gold};
 }
}
