using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;
// Prerequisite state/serialization only: no Batch-Service entry hook/reward ACK.
class Offline70TowerActorFG171 {internal uint Index;internal ushort Uid,Code;internal byte Level,Type;}
class Offline70DeathTowerFloorsFG171 {
 internal const ushort Dungeon=11000;internal const int MinimumLevel=50,LastFloor=45;
 internal int Floor,Phase;internal uint Seed;internal string RunId;internal bool Active;
 readonly List<Offline70TowerActorFG171> actors=new List<Offline70TowerActorFG171>();readonly HashSet<ushort> dead=new HashSet<ushort>(),used=new HashSet<ushort>();
 internal Offline70TowerActorFG171[] Actors{get{return actors.ToArray();}}
 internal ushort Map{get{return (ushort)(30000+Floor);}}
 internal bool Cleared{get{return Active&&actors.Count>0&&dead.Count==actors.Count;}}
 internal bool ReadyToFinish{get{return Cleared&&Floor==LastFloor&&Phase==2;}}
 static readonly int[] Points=new int[]{100,150,170,200,250,350,500};
 static ushort[][] Fixed(int floor){switch(floor){case 1:return new ushort[][]{new ushort[]{0,55001,50,0},new ushort[]{1,55001,50,0},new ushort[]{2,55001,50,0}};
case 2:return new ushort[][]{new ushort[]{0,600,15,5}};
case 3:return new ushort[][]{new ushort[]{0,5004,15,5}};
case 4:return new ushort[][]{new ushort[]{0,20002,15,5}};
case 5:return new ushort[][]{new ushort[]{0,10402,25,5},new ushort[]{0,55002,50,0},new ushort[]{1,55002,50,0},new ushort[]{2,55002,50,0}};
case 6:return new ushort[][]{new ushort[]{0,55003,50,0},new ushort[]{1,55003,50,0},new ushort[]{2,55003,50,0},new ushort[]{3,55003,50,0},new ushort[]{4,55003,50,0},new ushort[]{5,55003,50,0},new ushort[]{6,55003,50,0},new ushort[]{7,55003,50,0}};
case 7:return new ushort[][]{};
case 8:return new ushort[][]{new ushort[]{0,55003,50,0},new ushort[]{1,55003,50,0},new ushort[]{2,50501,50,0},new ushort[]{3,55003,50,0},new ushort[]{4,50501,50,0},new ushort[]{5,50501,50,0},new ushort[]{6,55003,50,0}};
case 9:return new ushort[][]{new ushort[]{0,20402,40,5},new ushort[]{0,55003,50,0},new ushort[]{1,55003,50,0},new ushort[]{2,55003,50,0},new ushort[]{3,55003,50,0},new ushort[]{4,55003,50,0},new ushort[]{5,55003,50,0}};
case 10:return new ushort[][]{new ushort[]{0,605,25,5},new ushort[]{0,55004,50,0},new ushort[]{1,55004,50,0},new ushort[]{2,55004,50,0},new ushort[]{3,55004,50,0}};
case 11:return new ushort[][]{new ushort[]{0,55005,50,0},new ushort[]{1,55005,50,0},new ushort[]{2,55005,50,0},new ushort[]{3,55005,50,0},new ushort[]{4,55005,50,0}};
case 12:return new ushort[][]{new ushort[]{0,606,17,5},new ushort[]{0,55014,50,0},new ushort[]{1,55014,50,0},new ushort[]{2,55014,50,0},new ushort[]{3,55014,50,0},new ushort[]{4,55014,50,0}};
case 13:return new ushort[][]{};
case 14:return new ushort[][]{new ushort[]{0,15204,40,5},new ushort[]{0,55017,50,0},new ushort[]{1,55018,50,0},new ushort[]{2,55019,50,0},new ushort[]{3,55020,50,0},new ushort[]{4,50507,50,0}};
case 15:return new ushort[][]{new ushort[]{0,10201,35,5},new ushort[]{0,55007,50,0},new ushort[]{1,55008,50,0},new ushort[]{2,55008,50,0},new ushort[]{3,55008,50,0},new ushort[]{4,55008,50,0},new ushort[]{5,55008,50,0}};
case 16:return new ushort[][]{new ushort[]{0,55009,50,0},new ushort[]{1,55010,50,0},new ushort[]{2,55010,50,0},new ushort[]{3,55009,50,0},new ushort[]{4,55009,50,0},new ushort[]{5,55009,50,0},new ushort[]{6,55009,50,0},new ushort[]{7,55010,50,0},new ushort[]{8,55010,50,0},new ushort[]{9,55010,50,0}};
case 17:return new ushort[][]{new ushort[]{0,55018,50,0},new ushort[]{1,55018,50,0},new ushort[]{2,55018,50,0},new ushort[]{3,50502,50,0},new ushort[]{4,55018,50,0},new ushort[]{5,55018,50,0},new ushort[]{6,55018,50,0}};
case 18:return new ushort[][]{new ushort[]{0,601,40,5},new ushort[]{0,55011,50,0},new ushort[]{1,55011,50,0},new ushort[]{2,55011,50,0}};
case 19:return new ushort[][]{};
case 20:return new ushort[][]{new ushort[]{0,20600,35,5},new ushort[]{0,50503,50,0},new ushort[]{1,50503,50,0},new ushort[]{2,50503,50,0},new ushort[]{3,50503,50,0}};
case 21:return new ushort[][]{new ushort[]{0,50504,50,0},new ushort[]{1,50504,50,0},new ushort[]{2,50504,50,0},new ushort[]{3,50504,50,0},new ushort[]{4,50504,50,0},new ushort[]{5,50504,50,0},new ushort[]{6,50504,50,0}};
case 22:return new ushort[][]{new ushort[]{0,602,40,5},new ushort[]{0,55021,50,0},new ushort[]{1,55021,50,0}};
case 23:return new ushort[][]{new ushort[]{0,55013,50,0},new ushort[]{1,55013,50,0},new ushort[]{2,55013,50,0},new ushort[]{3,55013,50,0},new ushort[]{4,55013,50,0},new ushort[]{5,55013,50,0},new ushort[]{6,55013,50,0},new ushort[]{7,55013,50,0},new ushort[]{8,55013,50,0},new ushort[]{9,55013,50,0},new ushort[]{10,55013,50,0},new ushort[]{11,55013,50,0},new ushort[]{12,55013,50,0},new ushort[]{13,55013,50,0},new ushort[]{14,55013,50,0},new ushort[]{15,55013,50,0},new ushort[]{16,55013,50,0},new ushort[]{17,55013,50,0}};
case 24:return new ushort[][]{};
case 25:return new ushort[][]{new ushort[]{0,5600,25,5},new ushort[]{0,55006,50,0},new ushort[]{1,55006,50,0},new ushort[]{2,55006,50,0},new ushort[]{3,55006,50,0},new ushort[]{4,55006,50,0},new ushort[]{5,55006,50,0},new ushort[]{6,55006,50,0},new ushort[]{7,55006,50,0},new ushort[]{8,55006,50,0},new ushort[]{9,55006,50,0},new ushort[]{10,55006,50,0},new ushort[]{11,55006,50,0},new ushort[]{12,55006,50,0},new ushort[]{13,55006,50,0},new ushort[]{14,55006,50,0},new ushort[]{15,55006,50,0},new ushort[]{16,55006,50,0},new ushort[]{17,55006,50,0},new ushort[]{18,55006,50,0},new ushort[]{19,55006,50,0}};
case 26:return new ushort[][]{new ushort[]{0,55015,50,0},new ushort[]{1,55015,50,0},new ushort[]{2,55015,50,0},new ushort[]{3,55015,50,0},new ushort[]{4,55015,50,0},new ushort[]{5,55016,50,0},new ushort[]{6,55015,50,0},new ushort[]{7,55015,50,0},new ushort[]{8,55015,50,0}};
case 27:return new ushort[][]{new ushort[]{0,55004,50,0},new ushort[]{1,55004,50,0},new ushort[]{2,55004,50,0},new ushort[]{3,55004,50,0},new ushort[]{4,55004,50,0},new ushort[]{5,55004,50,0},new ushort[]{6,55004,50,0},new ushort[]{7,55004,50,0},new ushort[]{8,55004,50,0},new ushort[]{9,55004,50,0},new ushort[]{10,55004,50,0},new ushort[]{11,55004,50,0},new ushort[]{12,55004,50,0},new ushort[]{13,55004,50,0},new ushort[]{14,55004,50,0},new ushort[]{15,55004,50,0},new ushort[]{16,55004,50,0},new ushort[]{17,55004,50,0}};
case 28:return new ushort[][]{new ushort[]{0,15201,20,5},new ushort[]{1,15202,30,5},new ushort[]{2,15203,48,5}};
case 29:return new ushort[][]{new ushort[]{0,50505,50,0},new ushort[]{1,50506,50,0},new ushort[]{2,55012,50,0}};
case 30:return new ushort[][]{new ushort[]{0,10504,50,5},new ushort[]{1,20500,50,5}};
case 31:return new ushort[][]{new ushort[]{0,50508,50,0},new ushort[]{1,50508,50,0},new ushort[]{2,50508,50,0},new ushort[]{3,50508,50,0},new ushort[]{4,50508,50,0},new ushort[]{5,50508,50,0},new ushort[]{6,50508,50,0},new ushort[]{7,50508,50,0},new ushort[]{8,50508,50,0},new ushort[]{9,50508,50,0}};
case 32:return new ushort[][]{new ushort[]{0,55017,50,0},new ushort[]{1,55022,50,0},new ushort[]{2,55017,50,0},new ushort[]{3,55017,50,0},new ushort[]{4,55017,50,0},new ushort[]{5,55023,50,0}};
case 33:return new ushort[][]{new ushort[]{0,55019,50,0},new ushort[]{1,50509,50,0},new ushort[]{2,55019,50,0},new ushort[]{3,55019,50,0},new ushort[]{4,55024,50,0},new ushort[]{5,55019,50,0}};
case 34:return new ushort[][]{new ushort[]{0,15206,30,5},new ushort[]{1,15207,30,5},new ushort[]{2,15801,30,5},new ushort[]{3,15901,30,5}};
case 35:return new ushort[][]{};
case 36:return new ushort[][]{new ushort[]{0,5303,40,5},new ushort[]{1,5304,40,5}};
case 37:return new ushort[][]{new ushort[]{0,15205,40,5},new ushort[]{0,55025,50,0}};
case 38:return new ushort[][]{new ushort[]{0,5003,45,5}};
case 39:return new ushort[][]{new ushort[]{0,15502,50,5}};
case 40:return new ushort[][]{new ushort[]{0,10202,50,5}};
case 41:return new ushort[][]{new ushort[]{0,20501,50,5}};
case 42:return new ushort[][]{new ushort[]{0,20603,25,5}};
case 43:return new ushort[][]{new ushort[]{0,5309,50,5}};
case 44:return new ushort[][]{new ushort[]{0,20401,50,5}};
case 45:return new ushort[][]{new ushort[]{0,10203,50,5}};
 default:throw new InvalidDataException("Original tower floor");}}
 static int Budget(int floor){switch(floor){case 7:return 150;
case 13:return 250;
case 19:return 300;
case 24:return 400;
case 35:return 600;
case 38:return 400;
case 39:return 400;
case 40:return 400;
case 41:return 500;
case 42:return 600;
case 43:return 700;
case 44:return 800;
case 45:return 900;
 default:return 0;}}
 static ushort[][] Group(int point){switch(point){case 100:return new ushort[][]{new ushort[]{600,15},new ushort[]{5004,15},new ushort[]{20002,15}};
case 150:return new ushort[][]{new ushort[]{5301,50},new ushort[]{5302,35},new ushort[]{5306,50},new ushort[]{5312,40},new ushort[]{10401,20}};
case 170:return new ushort[][]{new ushort[]{604,25}};
case 200:return new ushort[][]{new ushort[]{501,30},new ushort[]{15206,30},new ushort[]{15801,30},new ushort[]{15901,30},new ushort[]{20403,20}};
case 250:return new ushort[][]{new ushort[]{500,40},new ushort[]{603,35},new ushort[]{5308,45},new ushort[]{5313,50},new ushort[]{15207,30},new ushort[]{15501,30},new ushort[]{20201,20}};
case 350:return new ushort[][]{new ushort[]{5305,50},new ushort[]{5307,45},new ushort[]{10301,45},new ushort[]{10304,45}};
case 500:return new ushort[][]{new ushort[]{20601,45}};
 default:throw new InvalidDataException("Original APC point group");}}
 int draw;
 int Random(int maximum){if(maximum<1||maximum>60000)throw new InvalidDataException("Tower random bound");using(SHA256 hash=SHA256.Create())return (int)(BitConverter.ToUInt32(hash.ComputeHash(Encoding.ASCII.GetBytes(RunId+"-"+Floor+"-"+(draw++))),0)%(uint)maximum);}
 void Add(uint index,ushort code,byte level,byte type,ref ushort uid){if(actors.Count>=64)throw new InvalidDataException("Tower actors bounds");while(used.Contains(uid)||uid==ushort.MaxValue||uid==1||uid==900){uid=(ushort)((uid+1)%60000);}used.Add(uid);actors.Add(new Offline70TowerActorFG171{Index=index,Uid=uid,Code=code,Level=level,Type=type});uid++;}
 void Load(){Phase=0;draw=0;actors.Clear();dead.Clear();ushort uid=(ushort)Random(60000);ushort[][] fixedActors=Fixed(Floor);foreach(ushort[] row in fixedActors)if(row[3]==5)Add(row[0],row[1],(byte)row[2],5,ref uid);
  int budget=Budget(Floor),attempts=0;uint index=64;HashSet<ushort> selected=new HashSet<ushort>();foreach(Offline70TowerActorFG171 actor in actors)selected.Add(actor.Code);
  while(budget>=Points[0]){if(++attempts>10000)throw new InvalidDataException("Original APC duplicate draw exhausted");int requested=Random(budget)+1,point=Points[0];foreach(int value in Points)if(value<=requested)point=value;ushort[][] group=Group(point);ushort[] choice=group[Random(group.Length)];if(selected.Contains(choice[0])){bool available=false;foreach(int eligiblePoint in Points)if(eligiblePoint<=budget)foreach(ushort[] candidate in Group(eligiblePoint))if(!selected.Contains(candidate[0]))available=true;if(!available)break;continue;}selected.Add(choice[0]);budget-=point;Add(index++,choice[0],(byte)choice[1],5,ref uid);}
  foreach(ushort[] row in fixedActors)if(row[3]!=5)Add(row[0],row[1],(byte)row[2],(byte)row[3],ref uid);if(actors.Count==0)throw new InvalidDataException("Empty original tower floor");Seed=(uint)Random(60000);
 }
 internal void Begin(byte level){if(Active||level<MinimumLevel||level>70)throw new InvalidDataException("Tower entry state/level");RunId=Guid.NewGuid().ToString("N");Floor=1;Active=true;used.Clear();Load();}
 internal bool Start(){if(!Active||Phase>1)throw new InvalidDataException("Tower stage start");if(Phase==1)return false;Phase=1;return true;}
 internal Offline70TowerActorFG171 Actor(ushort uid){if(!Active)throw new InvalidDataException("Inactive tower");foreach(Offline70TowerActorFG171 actor in actors)if(actor.Uid==uid)return actor;throw new InvalidDataException("Actor not in current tower floor");}
 internal bool IsDeadFG172(ushort uid){Actor(uid);return dead.Contains(uid);}
 internal bool Kill(ushort uid){Actor(uid);if(dead.Contains(uid))return false;if(!Active||Phase!=1)throw new InvalidDataException("Tower death before started stage");return dead.Add(uid);}
 internal bool FinishDialog(){if(!Cleared||Phase<1)throw new InvalidDataException("Tower stage finish requires all current actors dead");if(Phase==2)return false;Phase=2;return true;}
 internal void Advance(){if(!Cleared||Phase!=2||Floor>=LastFloor)throw new InvalidDataException("Tower move requires cleared/finished non-final floor");Floor++;Load();}
 internal byte[] Info(){if(!Active)throw new InvalidDataException("Inactive tower info");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((uint)Dungeon);w.Write((ushort)LastFloor);w.Write((byte)0);return m.ToArray();}}
 internal byte[] Room(){if(!Active)throw new InvalidDataException("Inactive tower room");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((ushort)Floor);w.Write(Seed);w.Write(Map);w.Write((byte)actors.Count);foreach(Offline70TowerActorFG171 a in actors){w.Write(a.Index);w.Write(a.Uid);w.Write(a.Code);w.Write(a.Level);w.Write(a.Type);w.Write((byte)0);w.Write((byte)0);}w.Write((byte)0);return m.ToArray();}}
}
