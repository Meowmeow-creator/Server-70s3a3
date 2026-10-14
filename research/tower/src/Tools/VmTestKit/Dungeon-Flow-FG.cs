using System;using System.Collections.Generic;using System.IO;using System.Xml;using System.Xml.Serialization;
// Current70 offline diagnostic Lorien route: (0,0) map3, (0,1) map5, (0,2) map13.
// Exact current-PVF populations are retained. It is not an all-dungeon catalog.
partial class Offline70DungeonFlowFG {
 // Explicit diagnostic awards for the bounded Lorien route. These are not the
 // production PVF/server clear-reward formula and must not be presented as one.
 internal const uint DiagnosticClearExperience=100,DiagnosticClearGold=50;
 internal ushort DungeonId=1;internal string RunId;internal byte X,Y,Difficulty;internal bool Active,Loaded,ClearPublished;
 bool movePending;
 readonly HashSet<byte> visitedRooms=new HashSet<byte>();
 internal int DiagnosticFatigueUsed {get{return visitedRooms.Count;}}
 internal int CardClearedRoomsFG125 {get{if(!ReadyToClear)throw new InvalidDataException("Card exploration requires boss clear");return visitedRooms.Count;}}
 internal int CardTotalRoomsFG125 {get{int total=0;if(DungeonId==1)return Maps.Length;if(DungeonId==2){for(byte y=0;y<3;y++)for(byte x=0;x<3;x++)if(Offline70DeepLorienFG72.Exists(x,y))total++;}else for(byte y=0;y<Offline70ForestFG75.Height(DungeonId);y++)for(byte x=0;x<Offline70ForestFG75.Width(DungeonId);x++)if(MapFG130(x,y)!=0)total++;if(total<1)throw new InvalidDataException("Card maze count");return total;}}
 readonly System.Diagnostics.Stopwatch clearTimer=new System.Diagnostics.Stopwatch();
 readonly System.Diagnostics.Stopwatch settlementTimer=new System.Diagnostics.Stopwatch();
 internal const int AutoReturnSeconds=30;
 internal bool AutoReturnDue {get{return Active&&ClearPublished&&settlementTimer.IsRunning&&settlementTimer.ElapsedMilliseconds>=AutoReturnSeconds*1000;}}
 internal uint ClearMilliseconds {get{return (uint)Math.Min(86400000L,clearTimer.ElapsedMilliseconds);}}
 // Original C03D40 treats any zero member word as end eligibility reason1;
 // 105EF50 then displays resource1DB (a member has zero fatigue). This is
 // independent of card selection. The bounded single-player three-room Lorien
 // uses the persisted ordinary fatigue balance; unused members remain FFFF.
 // Other dungeon costs and premium fatigue remain outside this route.
 internal byte[] EndEligibilityBody(int remaining){if(remaining<0||remaining>188)throw new InvalidDataException("Fatigue eligibility bounds");if(!Active||!ClearPublished||!IsBossRoom)throw new InvalidDataException("End eligibility requires completed Lorien");return new byte[]{1,(byte)remaining,0,255,255,255,255,255,255};}
 internal int VerifiedKillCount,VerifiedChampionCount,VerifiedSuperChampionCount,VerifiedBossCount;
 readonly HashSet<ushort>[] dead=new HashSet<ushort>[256];
 internal Offline70DungeonFlowFG(){for(int i=0;i<dead.Length;i++)dead[i]=new HashSet<ushort>();}
 internal int RoomIndex {get{return DungeonId==1?Y:DungeonId==2?Y*3+X:Y*Offline70ForestFG75.Width(DungeonId)+X;}}
 internal byte BossX {get{if(GsdRunFG130&&DungeonId==12)return 0;return DungeonId>=3?Offline70ForestFG75.BossX(DungeonId):(byte)(DungeonId==1?0:2);}}
 internal byte BossY {get{if(GsdRunFG130&&DungeonId==12)return 1;return DungeonId>=3?Offline70ForestFG75.BossY(DungeonId):(byte)(DungeonId==1?2:1);}}
 internal bool IsBossRoom {get{return X==BossX&&Y==BossY;}}
 // Native28 stores bytes4/5 as the minimap boss position. Use the same
 // coordinates as combat/clear admission, including re-entry after retry.
 internal byte[] EntryBody(){if(!Active)throw new InvalidDataException("No active dungeon entry");byte[] body=new byte[12];BitConverter.GetBytes(DungeonId).CopyTo(body,0);body[2]=Difficulty;body[3]=(byte)(GsdRunFG130?1:0);body[4]=BossX;body[5]=BossY;return body;}
 internal ushort CurrentMap {get{return DungeonId==1?Maps[Y]:DungeonId==2?Offline70DeepLorienFG72.Map(X,Y):MapFG130(X,Y);}}
 internal int CurrentCount {get{return RoomMonstersFG118().Length;}}
 internal ushort[] Monster(ushort uid){int i=uid-100;if(i<0||i>=CurrentCount)throw new InvalidDataException("Room monster bounds");return RoomMonstersFG118()[i];}
 internal static readonly ushort[] Maps=new ushort[]{3,5,13};internal static readonly int[] Counts=new int[]{4,5,7};
 internal bool RoomCleared {get{return Active&&dead[RoomIndex].Count==CurrentCount;}}
 // The ordinary Lorien route ends on its only boss, not on unreported minions.
 internal bool ReadyToClear {get{return Active&&Loaded&&BossKilled();}}
 internal string RoomReceiptScope {get{return RunId+"-room"+RoomIndex;}}
 internal void Begin(){Begin(DungeonId,Difficulty);}
 internal void Begin(ushort id){Begin(id,0);}
 internal void Begin(ushort id,byte difficulty){if(id<1||id>2&&!Offline70ForestFG75.Supported(id)||difficulty>Offline70DungeonSelection.TestMaximumDifficulty)throw new InvalidDataException("Unsupported dungeon or difficulty");if(Active)throw new InvalidDataException("A dungeon run is already active");DungeonId=id;Difficulty=difficulty;RunId=Guid.NewGuid().ToString("N");X= id>=3?Offline70ForestFG75.StartX(id):(byte)0;Y=id>=3?Offline70ForestFG75.StartY(id):(byte)(id==1?0:2);Active=true;Loaded=false;movePending=false;ClearPublished=false;clearTimer.Reset();settlementTimer.Reset();visitedRooms.Clear();ResetElitesFG118();VerifiedKillCount=VerifiedChampionCount=VerifiedSuperChampionCount=VerifiedBossCount=0;foreach(HashSet<ushort> set in dead)set.Clear();}
 internal void MarkLoaded(){if(!Active||Loaded)throw new InvalidDataException("Dungeon load state");Loaded=true;movePending=false;visitedRooms.Add((byte)RoomIndex);if(!clearTimer.IsRunning)clearTimer.Start();}
 internal void ValidateKill(ushort uid){if(!Active||!Loaded||ClearPublished||ReadyToClear||uid<100||uid>=100+CurrentCount)throw new InvalidDataException("Dungeon actor state or UID");}
 internal bool IsDead(ushort uid){if(!Active||uid<100||uid>=100+CurrentCount)throw new InvalidDataException("Dungeon actor query state or UID");return dead[RoomIndex].Contains(uid);}
 internal bool Kill(ushort uid){if(IsDead(uid))return false;ValidateKill(uid);if(!dead[RoomIndex].Add(uid))return false;VerifiedKillCount++;byte kind=(byte)Monster(uid)[2];if(kind==1)VerifiedChampionCount++;else if(kind==2)VerifiedSuperChampionCount++;else if(kind==3)VerifiedBossCount++;return true;}
 internal bool IsBoss(ushort uid){return Active&&IsBossRoom&&Monster(uid)[2]==3;}
 bool BossKilled(){if(!IsBossRoom)return false;bool found=false;for(ushort uid=100;uid<100+CurrentCount;uid++)if(IsBoss(uid)){found=true;if(!dead[RoomIndex].Contains(uid))return false;}return found;}
 internal ushort[] CleanupAfterBoss(){if(!ReadyToClear)throw new InvalidDataException("Boss cleanup state");List<ushort> removed=new List<ushort>();for(ushort uid=100;uid<100+CurrentCount;uid++)if(dead[RoomIndex].Add(uid))removed.Add(uid);return removed.ToArray();}
 internal bool IsPendingMove(byte x,byte y){return Active&&!Loaded&&movePending&&X==x&&Y==y;}
 internal void Move(byte x,byte y){if(!Active||!Loaded||ClearPublished||ReadyToClear||!RoomCleared||!(DungeonId==1?x==0&&y<=2: DungeonId==2?x<=2&&y<=2&&Offline70DeepLorienFG72.Exists(x,y):CanMoveFG130(x,y))||Math.Abs((int)y-Y)+Math.Abs((int)x-X)!=1)throw new InvalidDataException("Dungeon room admission");X=x;Y=y;Loaded=false;movePending=true;}
 internal void PublishClear(){if(!ReadyToClear||ClearPublished)throw new InvalidDataException("Dungeon clear state");ClearPublished=true;clearTimer.Stop();}
 // Start only after the client reaches end eligibility (after XP presentation),
 // so the 30-second wait does not cut short the preceding results animation.
 // Repeated eligibility requests must not extend the same run's deadline.
 internal void BeginEndWait(){if(!Active||!ClearPublished)throw new InvalidDataException("End wait outside settled dungeon");if(!settlementTimer.IsRunning)settlementTimer.Start();}
 internal void End(byte option){if(!Active||!ClearPublished||option>3)throw new InvalidDataException("Dungeon end state");if(option==3)return;Active=false;Loaded=false;movePending=false;settlementTimer.Stop();}
 // Voluntary return uses command45, rather than a fabricated pre-clear75 award.
 // This diagnostic abort retains already committed kills/items and grants no
 // clear award; the production fatigue/stamina failure penalty is not modeled.
 internal void Abort(){if(!Active)throw new InvalidDataException("No dungeon to abort");Active=false;Loaded=false;movePending=false;settlementTimer.Stop();}
 internal byte[] RoomBody(){if(!Active)throw new InvalidDataException("No active dungeon");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
  // Original native29 flag0 uses the retained client room, preserving broken objects.
  bool revisit=visitedRooms.Contains((byte)RoomIndex);w.Write(X);w.Write(Y);w.Write((uint)0);w.Write((byte)0);w.Write((byte)0);w.Write((byte)(revisit?0:1));if(revisit)return m.ToArray();
  bool spawnGsd=GsdRunFG130&&!GsdSpawnedFG130&&CurrentMap==GsdStartMapFG135;w.Write(CurrentMap);w.Write((byte)(CurrentCount-dead[RoomIndex].Count+(spawnGsd?1:0)));
  for(ushort i=0;i<CurrentCount;i++){ushort uid=(ushort)(100+i);if(dead[RoomIndex].Contains(uid))continue;ushort[] monster=Monster(uid);w.Write(i);w.Write((uint)i);w.Write(uid);w.Write(monster[0]);w.Write((byte)monster[1]);w.Write((byte)monster[2]);w.Write((byte)0);w.Write((byte)0);}if(spawnGsd){w.Write((ushort)0);w.Write((uint)0);w.Write(GsdUidFG130);w.Write(GsdApcFG135);w.Write((byte)84);w.Write((byte)5);w.Write((byte)0);w.Write((byte)0);GsdSpawnedFG130=true;}w.Write((byte)0);w.Write((byte)0);return m.ToArray();
 }}
 internal static byte[] ResultBody(byte rank,uint clearMilliseconds,byte technic,byte hit){if(rank>105||clearMilliseconds>86400000)throw new InvalidDataException("Clear score bounds");byte[] b=new byte[7];b[0]=rank;BitConverter.GetBytes(clearMilliseconds).CopyTo(b,1);b[5]=technic;b[6]=hit;return b;}
 // Native35 four-member projection with one local free gold card. The displayed
 // gold is the diagnostic clear award already committed by Clear(), not a new
 // grant. No paid card is offered. Native readers consume all91 logical bytes.
 internal static byte[] ClearRewardBody(uint totalExperience,uint baseExperience,uint previousExperience){if(totalExperience<previousExperience||totalExperience-previousExperience!=baseExperience)throw new InvalidDataException("Clear experience presentation mismatch");byte[] b=new byte[91];BitConverter.GetBytes(totalExperience).CopyTo(b,0);BitConverter.GetBytes(baseExperience).CopyTo(b,4);BitConverter.GetBytes(previousExperience).CopyTo(b,8);b[62]=1;BitConverter.GetBytes(DiagnosticClearGold).CopyTo(b,67);return b;}
 internal static byte[] BossBody(ushort uid){if(uid<100||uid>163)throw new InvalidDataException("Supported boss UID");return new byte[]{1,1,(byte)uid,(byte)(uid>>8)};}
 internal static byte[] Reply(byte kind,ushort opcode,byte[] body,byte[] keys){if(kind>1||body==null||keys==null||keys.Length!=334)throw new InvalidDataException("Dungeon frame bounds");byte[] padded,encrypted;int index=opcode%14;
  if(kind==0&&(opcode==34||opcode==35||opcode==127))body=Offline70RoleProtocol.WithDungeonIntegrity(body);
  if(opcode==29&&kind==0){padded=new byte[(body.Length+15)/16*16];body.CopyTo(padded,0);encrypted=ProbeLoginCandidate.Crypt(padded,keys,true);}
  else if(opcode==31&&kind==0||(opcode==3||opcode==45)&&kind==1){padded=new byte[16];body.CopyTo(padded,0);encrypted=Offline70KeySettings.Crypt(padded,keys,true);}
  else if((opcode==34||opcode==35)&&kind==0||opcode==48&&kind==1||opcode==49&&kind==1||opcode==75&&kind==1){padded=Offline70RoleProtocol.Pad(body);encrypted=Offline70FlowCipherFG.Crypt(padded,keys,index,true);}
  else if(opcode==127&&kind==0){padded=new byte[16];body.CopyTo(padded,0);encrypted=ProbeLoginCandidate.Crypt(padded,keys,true);}
  else throw new InvalidDataException("Dungeon wire opcode has no verified reader");return Offline70RoleProtocol.Packet(kind,opcode,encrypted);
 }
 internal static byte[] EndPlain(byte[] encrypted,byte[] keys){if(encrypted==null||(encrypted.Length!=8&&encrypted.Length!=16))throw new InvalidDataException("End request ciphertext size");byte[] plain=Offline70FlowCipherFG.Crypt(encrypted,keys,5,false);if(plain[0]>2||plain[1]>3)throw new InvalidDataException("End option bounds");for(int i=2;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("End request padding");return plain;}
 // Current70 type1/op48 has a 70-byte logical body padded to 72 bytes.
 // Native 105D9A0 excludes this command from the folded-MD5 trailer. Coordinates
 // occupy bytes 0/1; bytes 68/69 are also client data, not zero padding.
 // The subsequent client counters are not authoritative input.
 // Run/load/death/adjacency admission is enforced separately by Move().
 internal static byte[] MovePlain(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=72)throw new InvalidDataException("Move request ciphertext size");byte[] plain=Offline70FlowCipherFG.Crypt(encrypted,keys,6,false);if(plain[0]>15||plain[1]>15)throw new InvalidDataException("Move coordinates outside bounded maze grid");for(int i=70;i<72;i++)if(plain[i]!=0)throw new InvalidDataException("Move request padding");return plain;}
 internal static void ValidateClearRequest(byte[] encrypted,byte[] keys){ResultPlain(encrypted,keys);}
 // Original current70 requests cover both values: 0 exits, 1 returns to channel
 // selection. After successful reply3 the native handler closes the old socket;
 // the server must stop processing this connection and permit a new ch10 login.
 internal static byte ExitPlain(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=16)throw new InvalidDataException("Channel exit ciphertext size");byte[] plain=Offline70KeySettings.Crypt(encrypted,keys,false);if(plain[0]>1)throw new InvalidDataException("Channel exit login state");for(int i=1;i<16;i++)if(plain[i]!=0)throw new InvalidDataException("Channel exit padding");return plain[0];}
 internal static void ValidateAbortRequest(byte[] body){if(body==null||body.Length!=0)throw new InvalidDataException("Dungeon abort request must have no body");}
 // The native sender truncates a float to u8 at byte0; it has no <=105 clamp.
 // Packed helper 1EE3875 adds four u16 fields after the first 12 bytes, so the
 // logical body is 40 bytes, followed by fold4 and four zero padding bytes.
 // These are reported statistics, not authority for the server-side award.
 internal static byte[] ResultPlain(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=48)throw new InvalidDataException("Clear result ciphertext size");byte[] plain=Offline70FlowCipherFG.Crypt(encrypted,keys,7,false),body=new byte[40];Buffer.BlockCopy(plain,0,body,0,40);byte[] integrity=Offline70RoleProtocol.WithDungeonIntegrity(body);for(int i=40;i<44;i++)if(plain[i]!=integrity[i])throw new InvalidDataException("Clear result checksum");for(int i=44;i<48;i++)if(plain[i]!=0)throw new InvalidDataException("Clear result padding");if(body[1]!=0)throw new InvalidDataException("Clear result reserved field");return body;}
}
// This journal guards clear idempotence independently of the role stores.
// The caller's consolidated transaction must commit asset deltas before publishing
// clear; this file alone is not an atomic multi-file XP/gold transaction.
public class Offline70ClearReceiptFG {public string RunId;public ushort DungeonId=1;public uint Experience,Gold;}
public class Offline70ClearLedgerFG {public int Format=1;public ushort RoleId=1;public List<Offline70ClearReceiptFG> Receipts=new List<Offline70ClearReceiptFG>();}
class Offline70ClearStoreFG {
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70ClearLedgerFG));
 static void Validate(Offline70ClearLedgerFG s){if(s==null||s.Format!=1||s.RoleId!=1||s.Receipts==null||s.Receipts.Count>10000)throw new InvalidDataException("Clear journal bounds");HashSet<string> ids=new HashSet<string>();foreach(Offline70ClearReceiptFG r in s.Receipts){Guid id;if(r==null||r.DungeonId!=1||r.RunId==null||r.RunId.Length!=32||!TryGuid(r.RunId,out id)||!ids.Add(r.RunId)||r.Experience>1000000||r.Gold>1000000)throw new InvalidDataException("Clear journal receipt");}}
 static bool TryGuid(string value,out Guid id){try{id=new Guid(value);return true;}catch(FormatException){id=Guid.Empty;return false;}}
 internal static Offline70ClearLedgerFG Load(string path){if(!File.Exists(path))return new Offline70ClearLedgerFG();if(new FileInfo(path).Length>2000000)throw new InvalidDataException("Clear journal file bounds");using(FileStream f=File.OpenRead(path))using(XmlTextReader x=new XmlTextReader(f)){x.ProhibitDtd=true;x.XmlResolver=null;Offline70ClearLedgerFG s=(Offline70ClearLedgerFG)Serializer.Deserialize(x);Validate(s);return s;}}
 internal static bool Commit(string path,string runId,uint experience,uint gold){string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70ClearLedgerFG s=Load(full);foreach(Offline70ClearReceiptFG r in s.Receipts)if(r.RunId==runId){if(r.Experience!=experience||r.Gold!=gold)throw new InvalidDataException("Conflicting repeated clear award");return false;}s.Receipts.Add(new Offline70ClearReceiptFG{RunId=runId,Experience=experience,Gold=gold});Validate(s);string tmp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+".backup-"+Guid.NewGuid().ToString("N");try{using(FileStream f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}if(Load(tmp).Receipts.Count!=s.Receipts.Count)throw new InvalidDataException("Clear journal read-back mismatch");if(File.Exists(full))File.Replace(tmp,full,backup);else File.Move(tmp,full);if(Load(full).Receipts.Count!=s.Receipts.Count)throw new InvalidDataException("Clear journal commit mismatch");return true;}finally{if(File.Exists(tmp))File.Delete(tmp);}}}
}
