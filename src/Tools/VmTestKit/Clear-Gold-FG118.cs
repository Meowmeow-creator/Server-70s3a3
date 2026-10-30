using System;using System.IO;
// Ordinary solo CClearReward::generateSpecificItem, original TW70 arithmetic
// and current CN70 itemdropinfo_common/clearreward/rank parameters.
class Offline70ClearGoldFG118 {
 internal static readonly uint[] GoldByLevel=new uint[]{0,18,25,32,39,47,55,63,72,81,90,100,110,120,131,142,153,165,177,189,192,194,206,219,208,219,229,240,284,298,313,327,341,355,371,387,402,417,433,449,447,466,487,508,529,551,572,595,618,640,663,760,785,810,837,864,891,918,945,972,996,1020,1044,1068,1092,1116,1140,1164,1188,1212,1236,1260,1284,1308,1332,1356,1380,1404,1428,1452,1476,1500,1524,1548,1572,1596,1620,1644,1668,1692,1716,1740,1764,1788,1812,1836,1860,1884,1908,1932};
 internal static readonly float[] DifficultyRates=new float[]{1f,1.2f,1.4f,1.6f,1.8f};
 internal class Reward {internal ushort Dungeon,Basis;internal byte Difficulty,Rank;internal int Normal,Champion,SuperChampion,Boss,Weighted,Variance;internal uint Reference,GoldBasis,Intermediate,Member,Gold;internal float Combined;}
 static float Store(double v){return BitConverter.ToSingle(BitConverter.GetBytes((float)v),0);}
 internal static Reward Calculate(ushort dungeon,byte difficulty,int normal,int champion,int superChampion,int boss,byte rank,int variance){
  if(difficulty>4||normal<0||champion<0||superChampion<0||boss<0||normal+champion+superChampion+boss>4096||variance< -15||variance>15)throw new InvalidDataException("Verified clear gold inputs");
  Reward r=new Reward{Dungeon=dungeon,Basis=Offline70ClearExperienceFG116.Basis(dungeon),Difficulty=difficulty,Normal=normal,Champion=champion,SuperChampion=superChampion,Boss=boss,Rank=rank,Variance=variance};
  r.Reference=GoldByLevel[r.Basis];r.GoldBasis=r.Reference*175/1000;
  int index=Offline70ClearExperienceFG116.RankIndex(rank);float rankRate=index<0?0f:Offline70ClearExperienceFG116.RankRates[index];
  r.Combined=Store((1.0+(double)rankRate)*Store(DifficultyRates[difficulty]*1.0));
  // The native function omits its super-champion word; preserve that omission.
  r.Weighted=normal+2*champion+4*boss;
  uint half=r.GoldBasis*(uint)r.Weighted/2;r.Intermediate=checked((uint)((double)half*r.Combined));
  r.Member=checked((uint)(r.Intermediate/2.0*r.Combined));r.Gold=r.Member*(uint)(100+variance)/100;return r;
 }
 internal static Reward Run(Offline70DungeonFlowFG run,byte rank){
  int normal=run.VerifiedKillCount-run.VerifiedChampionCount-run.VerifiedSuperChampionCount-run.VerifiedBossCount;
  int variance=Offline70QuestDropsFG.StableDraws(run.RunId+"-free-gold")(31)-15;
  return Calculate(run.DungeonId,run.Difficulty,normal,run.VerifiedChampionCount,run.VerifiedSuperChampionCount,run.VerifiedBossCount,rank,variance);
 }
 internal static byte[] Body(uint baseXp,uint rankXp,uint gold){byte[] b=Offline70ClearExperienceFG116.Body(baseXp,rankXp);BitConverter.GetBytes(gold).CopyTo(b,67);return b;}
}
