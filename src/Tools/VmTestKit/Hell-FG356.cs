using System;using System.IO;using System.Text;using System.Collections.Generic;
// FG356: abyss (hell party) content for the CN70 offline server.
//
// Every number in data/hell-FG356.dat is derived from the client PVF by
// local-run/pvf/gen_hell_drops.py; the runtime rules below are transcribed from the native
// reconstruction in reference/rednf/remake/source/DNFServer/GameServer/Game:
//   CParty::dungeon_start / CheckHaveHellPartyPassItem / CheckDestroyConditionSealDoor /
//   UseSealDoorItems / CheckHellDungeonFreepassItemHaveAndDel   (entry + ticket consumption)
//   GetHellPartyDifficulty / GetHellPartyEventType               (forced hell difficulty roll)
//   CBattle_Field::add_monster_hell_party + add_hell_party_monster (room group spawn)
//   KillHellPartyGroupMonsterCnt                                 (per-group remaining count)
//   CMonsterDrop::generateSpecificItem -> hellPartySpecificItem -> 
//   CMonsterDrop_Hell::generateSpecificItem                      (8/6/4 item rolls per kill)
//   CItemGenRateTable::generateTable / CLuckPoint::GetItemRarity (gear pool + rarity row)
//
// Single-player semantics that matter here (ORIG ClearDungeon): with memberCount==1 the party's
// hell value is reset to [base value] (1) instead of accumulated, and SelectDungeon only enters
// the random-hell branch when memberCount != 1. A solo run therefore *always* uses the explicit
// hell mode (op16 dungeon type 1); the wagon value only drives the client's gauge, not the drop.
class Offline70HellCatalogFG356{
 internal const string Magic="FG356HL1";internal const int MaximumBytes=8388608;
 internal sealed class Difficulty{internal byte ItemRolls;internal ushort RewardScale;internal byte Event;internal byte[] RandomWeights,ForcedWeights;}
 internal sealed class Group{internal ushort Index;internal byte Slot;internal ushort[] Monsters;internal byte[] Roles;}
 internal sealed class Order{internal byte Index;internal ushort[] Groups;internal ushort[] Rates;}
 internal sealed class BaseMonster{internal ushort Id;internal byte Rel,Add,Kind;}
 internal sealed class RoomMap{internal ushort MapId;internal BaseMonster[] Base;internal Order[] Orders;}
 internal sealed class DungeonHell{internal ushort Dungeon;internal bool Enabled;internal ushort[] Quests;internal uint[] FreepassItems;internal ushort[] FreepassCounts;internal uint[] Conditions;}
 internal sealed class Bracket{internal ushort Lo,Hi;internal ushort[] Weights;}
 internal sealed class Pool{internal uint[] Cumulative;internal uint[] Items;}
 internal static int MaxBaseValue,MinBaseValue,BaseValue,BaseRisingValue,GuideLine;
 internal static float SubBaseRates,PassSubBaseRates,AncientDungeonAddRate;
 internal static float[] LevelDiffRates=new float[4],DifficultyRates=new float[5],PartyRates=new float[4];
 internal static Difficulty[] Difficulties;
 internal static Dictionary<ushort,Group> Groups;
 internal static Dictionary<ushort,RoomMap> Maps;
 internal static Dictionary<ushort,DungeonHell> Dungeons;
 internal static Bracket[] Brackets;
 internal static int[][] RarityBasis;                  // [0] unused, [1..rows] script rows
 internal static byte[] WindowMin,WindowMax;           // index = level-1
 internal static HashSet<ushort> HellMonsters=new HashSet<ushort>();
 internal static Dictionary<int,Pool> Pools=new Dictionary<int,Pool>();   // key=(rarity<<8)|level
 internal static int PoolTables,PoolRows;
 internal static bool Loaded;
 static string sourcePath;

 internal static int PoolKey(int rarity,int level){return (rarity<<8)|level;}
 internal static Pool PoolFor(int rarity,int level){Pool p;return Pools.TryGetValue(PoolKey(rarity,level),out p)?p:null;}
 internal static RoomMap Map(ushort id){RoomMap m;return Maps.TryGetValue(id,out m)?m:null;}
 internal static Group GroupAt(ushort id){Group g;return Groups.TryGetValue(id,out g)?g:null;}
 internal static DungeonHell Profile(ushort dungeon){DungeonHell d;return Dungeons.TryGetValue(dungeon,out d)?d:null;}

 internal static void Load(string path){
  if(Loaded)return;byte[] bytes=File.ReadAllBytes(path);
  if(bytes.Length<74||bytes.Length>MaximumBytes)throw new InvalidDataException("Hell catalog size");
  if(Encoding.ASCII.GetString(bytes,0,8)!=Magic)throw new InvalidDataException("Hell catalog magic");
  string sha=Encoding.ASCII.GetString(bytes,8,64);
  if(!String.Equals(sha,Offline70QuestCatalogFG.CurrentPvf,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Hell catalog PVF");
  using(BinaryReader r=new BinaryReader(new MemoryStream(bytes,false),Encoding.ASCII)){
   r.BaseStream.Position=72;
   MaxBaseValue=r.ReadInt32();MinBaseValue=r.ReadInt32();BaseValue=r.ReadInt32();BaseRisingValue=r.ReadInt32();
   SubBaseRates=r.ReadSingle();PassSubBaseRates=r.ReadSingle();AncientDungeonAddRate=r.ReadSingle();
   for(int i=0;i<4;i++)LevelDiffRates[i]=r.ReadSingle();
   for(int i=0;i<5;i++)DifficultyRates[i]=r.ReadSingle();
   for(int i=0;i<4;i++)PartyRates[i]=r.ReadSingle();
   GuideLine=r.ReadInt32();
   if(MaxBaseValue<=0||MinBaseValue<0||BaseValue<0||BaseRisingValue<0||GuideLine<0)throw new InvalidDataException("Hell parameters");
   int dc=r.ReadByte();if(dc!=5)throw new InvalidDataException("Hell difficulty count");
   Difficulties=new Difficulty[dc];
   for(int i=0;i<dc;i++){
    Difficulty d=new Difficulty();d.ItemRolls=r.ReadByte();d.RewardScale=r.ReadUInt16();d.Event=r.ReadByte();
    d.RandomWeights=r.ReadBytes(6);d.ForcedWeights=r.ReadBytes(6);if(d.RandomWeights.Length!=6||d.ForcedWeights.Length!=6)throw new InvalidDataException("Hell difficulty weights");
    Difficulties[i]=d;
   }
   int gc=r.ReadUInt16();if(gc<1||gc>512)throw new InvalidDataException("Hell group count");
   Groups=new Dictionary<ushort,Group>();
   for(int i=0;i<gc;i++){
    Group g=new Group();g.Index=r.ReadUInt16();g.Slot=r.ReadByte();int mc=r.ReadUInt16();
    if(g.Index==0||g.Slot==0||g.Slot>5||mc>64)throw new InvalidDataException("Hell group bounds");
    g.Monsters=new ushort[mc];g.Roles=new byte[mc];
    for(int k=0;k<mc;k++){g.Monsters[k]=r.ReadUInt16();g.Roles[k]=r.ReadByte();if(g.Monsters[k]<3||g.Roles[k]>1)throw new InvalidDataException("Hell group monster");}
    if(Groups.ContainsKey(g.Index))throw new InvalidDataException("Hell group duplicate");
    Groups.Add(g.Index,g);
   }
   int mc2=r.ReadUInt16();if(mc2<1||mc2>256)throw new InvalidDataException("Hell map count");
   Maps=new Dictionary<ushort,RoomMap>();
   for(int i=0;i<mc2;i++){
    RoomMap m=new RoomMap();m.MapId=r.ReadUInt16();int bc=r.ReadByte();
    if(m.MapId==0||bc>64)throw new InvalidDataException("Hell map bounds");
    m.Base=new BaseMonster[bc];
    for(int k=0;k<bc;k++){BaseMonster b=new BaseMonster();b.Id=r.ReadUInt16();b.Rel=r.ReadByte();b.Add=r.ReadByte();b.Kind=r.ReadByte();if(b.Id<3||b.Rel>1||b.Kind>3)throw new InvalidDataException("Hell map monster");m.Base[k]=b;}
    int oc=r.ReadByte();if(oc>16)throw new InvalidDataException("Hell order count");
    m.Orders=new Order[oc];
    for(int k=0;k<oc;k++){
     Order o=new Order();o.Index=r.ReadByte();int rc=r.ReadUInt16();if(o.Index==0||o.Index>9||rc>64)throw new InvalidDataException("Hell order bounds");
     o.Groups=new ushort[rc];o.Rates=new ushort[rc];
     for(int j=0;j<rc;j++){o.Groups[j]=r.ReadUInt16();o.Rates[j]=r.ReadUInt16();if(!Groups.ContainsKey(o.Groups[j])||o.Rates[j]==0)throw new InvalidDataException("Hell order rate");}
     m.Orders[k]=o;
    }
    if(Maps.ContainsKey(m.MapId))throw new InvalidDataException("Hell map duplicate");
    Maps.Add(m.MapId,m);
   }
   int hc=r.ReadUInt16();if(hc<1||hc>256)throw new InvalidDataException("Hell dungeon count");
   Dungeons=new Dictionary<ushort,DungeonHell>();
   for(int i=0;i<hc;i++){
    DungeonHell d=new DungeonHell();d.Dungeon=r.ReadUInt16();d.Enabled=r.ReadByte()!=0;if(d.Dungeon==0||d.Enabled==false)throw new InvalidDataException("Hell dungeon flag");
    int qc=r.ReadByte();if(qc>32)throw new InvalidDataException("Hell quest count");d.Quests=new ushort[qc];for(int k=0;k<qc;k++)d.Quests[k]=r.ReadUInt16();
    int fc=r.ReadByte();if(fc>16)throw new InvalidDataException("Hell freepass count");d.FreepassItems=new uint[fc];d.FreepassCounts=new ushort[fc];
    for(int k=0;k<fc;k++){d.FreepassItems[k]=r.ReadUInt32();d.FreepassCounts[k]=r.ReadUInt16();if(d.FreepassItems[k]<3||d.FreepassCounts[k]==0)throw new InvalidDataException("Hell freepass item");}
    int cc=r.ReadByte();if(cc>16)throw new InvalidDataException("Hell condition count");d.Conditions=new uint[cc];for(int k=0;k<cc;k++){d.Conditions[k]=r.ReadUInt32();if(d.Conditions[k]<3)throw new InvalidDataException("Hell condition item");}
    if(Dungeons.ContainsKey(d.Dungeon))throw new InvalidDataException("Hell dungeon duplicate");
    Dungeons.Add(d.Dungeon,d);
   }
   int bc2=r.ReadByte();if(bc2<1||bc2>8)throw new InvalidDataException("Hell bracket count");
   Brackets=new Bracket[bc2];
   for(int i=0;i<bc2;i++){
    Bracket b=new Bracket();b.Lo=r.ReadUInt16();b.Hi=r.ReadUInt16();b.Weights=new ushort[5];
    for(int k=0;k<5;k++)b.Weights[k]=r.ReadUInt16();
    if(b.Lo<1||b.Hi<b.Lo||b.Hi>255)throw new InvalidDataException("Hell bracket bounds");
    Brackets[i]=b;
   }
   int rc2=r.ReadByte();if(rc2<1||rc2>5)throw new InvalidDataException("Hell rarity row count");
   RarityBasis=new int[rc2+1][];
   RarityBasis[0]=new int[]{0,0,0,0,0,0};
   for(int i=1;i<=rc2;i++){int[] row=new int[6];for(int k=0;k<6;k++)row[k]=r.ReadInt32();if(row[0]!=0||row[5]<=row[1])throw new InvalidDataException("Hell rarity row");RarityBasis[i]=row;}
   int wc=r.ReadByte();if(wc<1||wc>200)throw new InvalidDataException("Hell level window count");
   WindowMin=new byte[wc];WindowMax=new byte[wc];
   for(int i=0;i<wc;i++){WindowMin[i]=r.ReadByte();WindowMax[i]=r.ReadByte();if(WindowMin[i]>200||WindowMax[i]>200)throw new InvalidDataException("Hell level window");}
   int hmc=r.ReadUInt16();if(hmc>2048)throw new InvalidDataException("Hell monster flag count");
   HellMonsters=new HashSet<ushort>();
   for(int i=0;i<hmc;i++){ushort id=r.ReadUInt16();if(id<3)throw new InvalidDataException("Hell monster id");HellMonsters.Add(id);}
   int pc=r.ReadInt32();if(pc<1||pc>4096)throw new InvalidDataException("Hell pool table count");
   Pools=new Dictionary<int,Pool>(pc);PoolRows=0;
   for(int i=0;i<pc;i++){
    int rarity=r.ReadByte(),level=r.ReadByte(),rc3=r.ReadUInt16();
    if(rarity>5||level<1||rc3>4096)throw new InvalidDataException("Hell pool table bounds");
    Pool p=new Pool();p.Cumulative=new uint[rc3];p.Items=new uint[rc3];uint previous=0;
    for(int k=0;k<rc3;k++){uint cumulative=r.ReadUInt32(),item=r.ReadUInt32();if(cumulative<=previous||item<3)throw new InvalidDataException("Hell pool row");p.Cumulative[k]=cumulative;p.Items[k]=item;previous=cumulative;}
    int key=PoolKey(rarity,level);if(Pools.ContainsKey(key))throw new InvalidDataException("Hell pool duplicate");Pools.Add(key,p);PoolRows+=rc3;
   }
   if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Hell catalog trailing bytes");
   PoolTables=pc;sourcePath=path;Loaded=true;
  }
  RegisterRoomTables();
 }

 // The floor planner needs (a) a MapRolls row for every uid of the hell room and (b) a monster
 // profile for every roster id: the summon-only actors of the hell maps have no .mob drop table,
 // and native _generateRandomItem simply yields nothing for them. Empty pools reproduce that
 // without weakening the ordinary "Floor monster profile" guard.
 internal static void RegisterRoomTables(){
  int addedMaps=0,addedPools=0;
  foreach(RoomMap m in Maps.Values){
   int rows=m.Base.Length;
   foreach(Order o in m.Orders){
    int best=0;
    foreach(ushort gid in o.Groups){Group g;if(!Groups.TryGetValue(gid,out g))continue;if(g.Slot!=1&&g.Slot!=2)continue;if(g.Monsters.Length>best)best=g.Monsters.Length;}
    rows+=best;
   }
   if(rows<1)rows=1;
   if(!Offline70FloorCatalogFG119.MapRolls.ContainsKey(m.MapId)){
    byte[][] table=new byte[rows][];for(int i=0;i<rows;i++)table[i]=new byte[]{1,1};
    Offline70FloorCatalogFG119.MapRolls.Add(m.MapId,table);addedMaps++;
   }
   foreach(BaseMonster b in m.Base)if(EnsurePool(b.Id))addedPools++;
   foreach(Order o in m.Orders)foreach(ushort gid in o.Groups){Group g;if(!Groups.TryGetValue(gid,out g))continue;foreach(ushort id in g.Monsters)if(EnsurePool(id))addedPools++;}
  }
  if(addedMaps!=Maps.Count||addedPools<0)throw new InvalidDataException("Hell room table drift");
 }
 static bool EnsurePool(ushort monsterId){
  if(Offline70FloorCatalogFG119.MonsterPools.ContainsKey(monsterId))return false;
  Offline70FloorCatalogFG119.MonsterPools.Add(monsterId,new uint[][][]{new uint[][]{},new uint[][]{},new uint[][]{},new uint[][]{}});
  return true;
 }
}

// Pure entry gate for the native CParty::CheckHaveHellPartyPassItem / CheckDestroyConditionSealDoor
// pair: a [hell freepass item] holder passes for free, otherwise every [item condition] stack must
// reach getHellPartyEntranceItemNeedCount(minLevel) and minLevel has to be >= 45.
class Offline70HellEntryFG356{
 internal sealed class Decision{internal int Code;internal int Need;internal int FreepassIndex=-1;internal List<uint> Missing=new List<uint>();internal bool Ok{get{return Code==0;}}}
 internal static Decision Check(ushort dungeonId,Offline70EconomyState assets,int minLevel){
  Decision d=new Decision();
  if(!Offline70HellCatalogFG356.Loaded){d.Code=1;return d;}
  Offline70HellCatalogFG356.DungeonHell hell=Offline70HellCatalogFG356.Profile(dungeonId);
  if(hell==null||!hell.Enabled){d.Code=2;return d;}
  if(SealDoorsFG.HasSealDoor(dungeonId)&&Offline70HellCatalogFG356.Map(SealDoorsFG.GetSealDoorMapIdx(dungeonId))==null){d.Code=3;return d;}
  d.Need=Offline70HellFG356.NeedCount(minLevel);
  for(int i=0;i<hell.FreepassItems.Length;i++)if(Offline70EntryCostFG185.Owned(assets,hell.FreepassItems[i])>=hell.FreepassCounts[i]){d.FreepassIndex=i;return d;}
  if(minLevel<=44){d.Code=4;d.Missing.AddRange(hell.Conditions);return d;}
  foreach(uint id in hell.Conditions)if(Offline70EntryCostFG185.Owned(assets,id)<d.Need)d.Missing.Add(id);
  if(d.Missing.Count>0)d.Code=5;
  return d;
 }
}

class Offline70HellFG356{
 // Global free function `getHellPartyEntranceItemNeedCount` (ORIG 0x08891be6): (40*level-1800)/100+10.
 internal static int NeedCount(int minLevel){return (40*minLevel-1800)/100+10;}
 internal static byte RollForcedDifficulty(Random rng){
  int roll=rng.Next(100),acc=0;
  for(int i=1;i<=5;i++){acc+=Offline70HellCatalogFG356.Difficulties[i-1].ForcedWeights[i];if(acc>=roll)return (byte)i;}
  return 0;
 }
 internal static byte EventType(byte hellDifficulty){return hellDifficulty<1||hellDifficulty>5?(byte)0:Offline70HellCatalogFG356.Difficulties[hellDifficulty-1].Event;}
 internal static int ItemRolls(byte hellDifficulty){return hellDifficulty<1||hellDifficulty>5?0:Offline70HellCatalogFG356.Difficulties[hellDifficulty-1].ItemRolls;}
 internal static int ProbIndex(int level){
  foreach(Offline70HellCatalogFG356.Bracket b in Offline70HellCatalogFG356.Brackets)if(level>=b.Lo&&level<=b.Hi)return Array.IndexOf(Offline70HellCatalogFG356.Brackets,b);
  return -1;
 }
 // CLuckPoint::GetItemRarity with no luck users (the offline party has none): first i with
 // roll <= basis[i]; table[0] is 0, so the ladder starts at 1 exactly like the native table.
 internal static int GetItemRarity(int[] table,int roll){for(int i=0;i<=5;i++)if(roll<=table[i])return i;return 0;}
 internal static uint Choose(Offline70HellCatalogFG356.Pool pool,uint draw){
  int low=0,high=pool.Cumulative.Length-1;
  while(low<high){int middle=(low+high)/2;if(pool.Cumulative[middle]>=draw)high=middle;else low=middle+1;}
  return pool.Cumulative[low]>=draw?pool.Items[low]:0;
 }
 static readonly HashSet<uint> KnownItems=new HashSet<uint>(),MissingItems=new HashSet<uint>();
 internal static bool Registered(uint item){
  if(KnownItems.Contains(item))return true;if(MissingItems.Contains(item))return false;
  try{Offline70Economy.Definition(item);KnownItems.Add(item);return true;}catch(InvalidDataException){MissingItems.Add(item);return false;}
 }
 // CMonsterDrop_Hell::generateSpecificItem x ItemRolls: the level bracket picks the per-dungeon-
 // difficulty threshold (w[diff]*dropRate/100, dropRate=minStdDropRate=100), the rarity row is
 // indexed by the hell difficulty, and the item comes from the (rarity, standard level) gear pool.
 internal static List<Offline70FloorDropsFG119.Reward> Plan(ushort dungeonId,byte dungeonDifficulty,byte hellDifficulty,int itemRolls,string receipt,Func<string,uint,uint> draw){
  List<Offline70FloorDropsFG119.Reward> rewards=new List<Offline70FloorDropsFG119.Reward>();
  if(itemRolls<=0||draw==null)return rewards;
  int level=Offline70ClearExperienceFG116.Basis(dungeonId);
  int bracket=ProbIndex(level);if(bracket<0)return rewards;
  int threshold=Offline70HellCatalogFG356.Brackets[bracket].Weights[dungeonDifficulty];
  int[] rarityRow=hellDifficulty>=1&&hellDifficulty<Offline70HellCatalogFG356.RarityBasis.Length?Offline70HellCatalogFG356.RarityBasis[hellDifficulty]:Offline70HellCatalogFG356.RarityBasis[0];
  for(int i=0;i<itemRolls;i++){
   if(draw("hell:chance:"+i,1001)>threshold)continue;
   int rarity=GetItemRarity(rarityRow,(int)(1+draw("hell:rarity:"+i,1000000)));
   Offline70HellCatalogFG356.Pool pool=Offline70HellCatalogFG356.PoolFor(rarity,level);
   if(pool==null||pool.Cumulative.Length==0)continue;
   uint total=pool.Cumulative[pool.Cumulative.Length-1];
   uint item=Choose(pool,draw("hell:item:"+i,total));
   if(item==0||!Registered(item))continue;
   rewards.Add(new Offline70FloorDropsFG119.Reward{Item=item,Quantity=1,Source="hell-original356"});
  }
  return rewards;
 }
 internal static List<Offline70FloorDropsFG119.Reward> Plan(Offline70DungeonFlowFG dungeon,int itemRolls,string receipt){
  if(dungeon==null||itemRolls<=0)return new List<Offline70FloorDropsFG119.Reward>();
  return Plan(dungeon.DungeonId,dungeon.Difficulty,dungeon.HellDifficultyFG356,itemRolls,receipt,
   delegate(string purpose,uint maximum){return Offline70ClearItemsFG125.Draw(receipt,purpose,maximum);});
 }
}

partial class Offline70DungeonFlowFG{
 internal const int HellRoomIndexFG356=250;
 internal const ushort HellNoGroupFG356=0xFFFF;
 bool hellPartyModeFG356;byte hellDifficultyFG356=1,hellEventTypeFG356;
 bool hellRolledFG356;
 readonly List<ushort[]> hellRosterFG356=new List<ushort[]>();
 readonly List<int> hellRosterGroupFG356=new List<int>();
 readonly List<ushort[]> hellApcsFG356=new List<ushort[]>();
 readonly List<int> hellApcGroupFG356=new List<int>();
 readonly Dictionary<int,int> hellRemainingFG356=new Dictionary<int,int>();
 bool hellAllGroupsFG356;int pendingHellRollsFG356;
 internal bool HellPartyModeFG356{get{return hellPartyModeFG356;}}
 internal byte HellDifficultyFG356{get{return hellDifficultyFG356;}}
 internal byte HellEventTypeFG356{get{return hellEventTypeFG356;}}
 internal bool HellRoomActiveFG356{get{return Active&&hellPartyModeFG356&&SealDoorOpenFG181&&IsSealDoor(X,Y);}}
 internal int PendingHellRollsFG356{get{return pendingHellRollsFG356;}}
 internal int TakePendingHellRollsFG356(){int value=pendingHellRollsFG356;pendingHellRollsFG356=0;return value;}
 internal void ConfigureHellFG356(bool hellParty,byte hellDifficulty,byte hellEventType){
  hellPartyModeFG356=hellParty;hellDifficultyFG356=hellDifficulty<1?(byte)1:hellDifficulty;hellEventTypeFG356=hellEventType;
  hellRolledFG356=false;hellRosterFG356.Clear();hellRosterGroupFG356.Clear();hellApcsFG356.Clear();hellApcGroupFG356.Clear();hellRemainingFG356.Clear();hellAllGroupsFG356=false;pendingHellRollsFG356=0;
 }
 // The room roster is the hell map's own [monster] rows plus one group per [hellparty] order,
 // picked with the same per-order cumulative rate the native add_hell_party_monster uses.
 internal ushort[][] HellRoomMonstersFG356(){
  if(!hellRolledFG356)RollHellRoomFG356();
  return hellRosterFG356.ToArray();
 }
 internal List<ushort[]> HellRoomApcsFG356(){
  if(!HellRoomActiveFG356)return new List<ushort[]>();
  if(!hellRolledFG356)RollHellRoomFG356();
  return hellApcsFG356;
 }
 internal int HellGroupRemainingFG356(int group){int remaining;return hellRemainingFG356.TryGetValue(group,out remaining)?remaining:-1;}
 void RollHellRoomFG356(){
  if(!Offline70HellCatalogFG356.Loaded)throw new InvalidDataException("Hell catalog not loaded");
  ushort mapId=SealDoorMapIdx;
  Offline70HellCatalogFG356.RoomMap map=Offline70HellCatalogFG356.Map(mapId);
  if(map==null)throw new InvalidDataException("Hell room map profile "+mapId);
  int level=Offline70ClearExperienceFG116.Basis(DungeonId);if(level<1)level=1;if(level>200)level=200;
  Func<int,int> draw=Offline70QuestDropsFG.StableDraws(RunId+"-hell-room-"+mapId);
  hellRosterFG356.Clear();hellRosterGroupFG356.Clear();hellApcsFG356.Clear();hellApcGroupFG356.Clear();hellRemainingFG356.Clear();
  foreach(Offline70HellCatalogFG356.BaseMonster b in map.Base){
   int actorLevel=b.Rel!=0?level+b.Add:b.Add;if(actorLevel<1)actorLevel=1;if(actorLevel>200)actorLevel=200;
   hellRosterFG356.Add(new ushort[]{b.Id,(ushort)actorLevel,b.Kind});hellRosterGroupFG356.Add(-1);
  }
  foreach(Offline70HellCatalogFG356.Order order in map.Orders){
   int total=0;
   for(int i=0;i<order.Groups.Length;i++){Offline70HellCatalogFG356.Group g=Offline70HellCatalogFG356.GroupAt(order.Groups[i]);if(g!=null&&g.Slot==hellDifficultyFG356)total+=order.Rates[i];}
   if(total<=0)continue;
   int roll=draw(total),acc=0;Offline70HellCatalogFG356.Group picked=null;
   for(int i=0;i<order.Groups.Length;i++){
    Offline70HellCatalogFG356.Group g=Offline70HellCatalogFG356.GroupAt(order.Groups[i]);
    if(g==null||g.Slot!=hellDifficultyFG356)continue;
    acc+=order.Rates[i];if(acc>=roll){picked=g;break;}
   }
   if(picked==null)continue;
   int count=0;
   for(int i=0;i<picked.Monsters.Length;i++){
    if(picked.Roles[i]==1){hellApcsFG356.Add(new ushort[]{mapId,picked.Monsters[i],(ushort)level,(ushort)5});hellApcGroupFG356.Add(picked.Index);}
    else{hellRosterFG356.Add(new ushort[]{picked.Monsters[i],(ushort)level,(ushort)0});hellRosterGroupFG356.Add(picked.Index);}
    count++;
   }
   // Native insert(make_pair(groupIdx, i-1)): the map's group counter starts one below the
   // group size and the kill that finds 0 erases the group.
   hellRemainingFG356[picked.Index]=count-1;
  }
  hellRolledFG356=true;
 }
 // KillHellPartyGroupMonsterCnt: returns the hell item roll count of this kill. The native
 // m_killedAllHellGroups latch also suppresses the roll for every later kill (early return).
 int HellGroupKillFG356(int group){
  if(hellAllGroupsFG356)return 0;
  int remaining;if(!hellRemainingFG356.TryGetValue(group,out remaining))return 0;
  if(remaining>0){hellRemainingFG356[group]=remaining-1;return 0;}
  hellRemainingFG356.Remove(group);
  if(hellRemainingFG356.Count==0){hellAllGroupsFG356=true;return Offline70HellFG356.ItemRolls(hellDifficultyFG356);}
  return 0;
 }
 internal int HellBeforeKillFG356(ushort uid){
  pendingHellRollsFG356=0;
  if(!HellRoomActiveFG356||!hellRolledFG356)return 0;
  int index=uid-100;if(index<0||index>=hellRosterGroupFG356.Count)return 0;
  int group=hellRosterGroupFG356[index];if(group<0)return 0;
  // Native order: KillHellPartyGroupMonsterCnt runs for every hell-group mob (it is what advances
  // the per-group counter), and only afterwards hellPartySpecificItem nulls extraCount for a
  // [hell monster] .mob. The suppression must not leave a zero-remaining group stuck in the map.
  int rolls=HellGroupKillFG356(group);
  if(rolls>0&&Offline70HellCatalogFG356.HellMonsters.Contains(hellRosterFG356[index][0]))rolls=0;
  pendingHellRollsFG356=rolls;return rolls;
 }
 internal int HellBeforeKillApcFG356(ushort uid){
  pendingHellRollsFG356=0;
  if(!HellRoomActiveFG356||!hellRolledFG356)return 0;
  for(int i=0;i<hellApcGroupFG356.Count;i++)if(ApcUidFG186(i)==uid){int rolls=HellGroupKillFG356(hellApcGroupFG356[i]);pendingHellRollsFG356=rolls;return rolls;}
  return 0;
 }
}
