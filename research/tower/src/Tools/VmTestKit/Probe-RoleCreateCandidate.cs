using System;using System.IO;using System.Text;using System.Xml;using System.Xml.Serialization;using System.Net;using System.Net.Sockets;using System.Diagnostics;using System.Threading;using System.Collections.Generic;
// Bounded single-player loopback service. Separate durable test store, not the ACT4 main database.
class ProbeRoleCreateCandidate {
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70RoleStore));
 static void Validate(Offline70RoleStore store){
  if(store==null||store.Format!=1||store.AccountId!=1||store.Roles==null||store.Roles.Count>12)throw new InvalidDataException("Role store version or bounds");
  HashSet<ushort> ids=new HashSet<ushort>();HashSet<string> names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(Offline70Role role in store.Roles){if(role==null||role.Id==0||!ids.Add(role.Id)||String.IsNullOrEmpty(role.Name)||!names.Add(role.Name)||role.Job!=0||role.Level!=1)throw new InvalidDataException("Role store profile");byte[] name=Offline70RoleProtocol.NameEncoding.GetBytes(role.Name);if(name.Length<1||name.Length>29)throw new InvalidDataException("Role store name bounds");}
 }
 internal static Offline70RoleStore Load(string path){
  if(!File.Exists(path))return new Offline70RoleStore();if(new FileInfo(path).Length>65536)throw new InvalidDataException("Role store file limit");
  using(FileStream f=File.OpenRead(path))using(XmlTextReader reader=new XmlTextReader(f)){reader.ProhibitDtd=true;reader.XmlResolver=null;Offline70RoleStore store=(Offline70RoleStore)Serializer.Deserialize(reader);Validate(store);return store;}
 }
 internal static Offline70RoleStore Create(string path,Offline70Role role){
  string directory=Path.GetDirectoryName(Path.GetFullPath(path));Directory.CreateDirectory(directory);
  // Serialize creators across diagnostic runs; the main server never uses this file.
  using(FileStream held=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   Offline70RoleStore store=Load(path);if(store.Roles.Count>=12)throw new InvalidDataException("No free role slots");ushort largest=0;
   foreach(Offline70Role prior in store.Roles){if(String.Equals(prior.Name,role.Name,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Role name already exists");if(prior.Id>largest)largest=prior.Id;}
   if(largest==UInt16.MaxValue)throw new InvalidDataException("Role id limit");role.Id=(ushort)(largest+1);store.Roles.Add(role);Validate(store);
   string temporary=path+".pending-"+Guid.NewGuid().ToString("N"),backup=path+".backup-"+Guid.NewGuid().ToString("N");
   try {
    using(FileStream f=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,store);f.Flush();}
    Offline70RoleStore candidate=Load(temporary);if(candidate.Roles.Count!=store.Roles.Count||candidate.Roles[candidate.Roles.Count-1].Name!=role.Name)throw new InvalidDataException("Role store read-back mismatch");
    if(File.Exists(path))File.Replace(temporary,path,backup);else File.Move(temporary,path);
    Offline70RoleStore committed=Load(path);Offline70Role saved=committed.Roles[committed.Roles.Count-1];if(saved.Id!=role.Id||saved.Name!=role.Name||saved.Job!=role.Job)throw new InvalidDataException("Committed role read-back mismatch");return committed;
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
 static void Save(string path,byte[] data){using(FileStream f=new FileStream(path,FileMode.CreateNew,FileAccess.Write))f.Write(data,0,data.Length);}
 static void Note(StreamWriter log,string message){log.WriteLine(DateTime.Now.ToString("o")+" "+message);}
 static byte[] Keys(){byte[] keys=new byte[334];for(int i=0;i<keys.Length;i++)keys[i]=(byte)i;return keys;}
 static void Send(TcpClient client,StreamWriter log,string logPath,string label,byte[] packet){Save(logPath+"."+label+".bin",packet);client.GetStream().Write(packet,0,packet.Length);Note(log,"Sent "+label+" length="+packet.Length);}
 static int Fixtures(string prefix,string requestPath){
  byte[] keys=Keys(),request=File.ReadAllBytes(requestPath);if(request.Length<21||request.Length>128||request[0]!=1||BitConverter.ToUInt16(request,1)!=5||BitConverter.ToInt32(request,3)!=request.Length)throw new InvalidDataException("Captured create fixture bounds");
  byte[] encrypted=new byte[request.Length-13];Buffer.BlockCopy(request,13,encrypted,0,encrypted.Length);Offline70Role role=Offline70RoleProtocol.ParseCreate(encrypted,keys);role.Id=1;
  List<Offline70Role> roles=new List<Offline70Role>();roles.Add(role);Save(prefix+".keys.bin",keys);Save(prefix+".role-plain.bin",Offline70RoleProtocol.RosterBody(roles));Save(prefix+".role-reply.bin",Offline70RoleProtocol.RosterReply(keys,roles));Save(prefix+".empty-reply.bin",Offline70RoleProtocol.RosterReply(keys,new List<Offline70Role>()));Save(prefix+".create-reply.bin",Offline70RoleProtocol.CreateReply(keys,true,0));Save(prefix+".error-reply.bin",Offline70RoleProtocol.CreateReply(keys,false,4));
  string testStore=prefix+".store.xml";if(File.Exists(testStore))throw new InvalidDataException("Do not overwrite store test");Create(testStore,new Offline70Role{Job=role.Job,Level=role.Level,Name=role.Name,CreatedUtc=role.CreatedUtc});
  Offline70RoleStore reloaded=Load(testStore);if(reloaded.Roles.Count!=1||reloaded.Roles[0].Name!=role.Name||reloaded.Roles[0].Id!=1)throw new InvalidDataException("Persistence fixture");bool duplicateRejected=false;
  try{Create(testStore,new Offline70Role{Job=0,Level=1,Name=role.Name,CreatedUtc=role.CreatedUtc});}catch(InvalidDataException){duplicateRejected=true;}
  if(!duplicateRejected||Load(testStore).Roles.Count!=1)throw new InvalidDataException("Duplicate persistence fixture");Console.WriteLine("Fixture native comparison ready; stored and reloaded one role; duplicate rejected; name="+role.Name);return 0;
 }
 static int Main(string[] args){
  if(args.Length==2&&args[0]=="--gold-loot-fixtures")return GoldLootFixtures.Run(args[1]);
  if(args.Length==2&&args[0]=="--item-loot-fixtures")return ItemLootFixtures.Run(args[1]);
  if(args.Length==2&&args[0]=="--monster-death-request-check"){try{ushort uid=Offline70RoleProtocol.ParseMonsterDeathRequest(File.ReadAllBytes(args[1]),new Offline70Role{Id=1,Name="yoyo",Job=0,Level=1},Keys());Console.WriteLine("ACCEPTED uid="+uid);return 0;}catch(InvalidDataException e){Console.WriteLine("REJECTED "+e.Message);return 3;}}
  if(args.Length==2&&args[0]=="--combat-stat-fixture"){Offline70Role role=new Offline70Role{Id=1,Name="yoyo",Job=0,Level=1};Save(args[1]+".record.bin",Offline70RoleProtocol.AdditionRecord(role));Save(args[1]+".plain.bin",Offline70RoleProtocol.AdditionBody(role));Save(args[1]+".packet.bin",Offline70RoleProtocol.AdditionReply(role,Keys()));Console.WriteLine("Combat stat native comparison fixture emitted");return 0;}
  if(args.Length==2&&args[0]=="--monster-death-fixture"){Offline70Role role=new Offline70Role{Id=1,Name="yoyo",Job=0,Level=1};foreach(ushort uid in new ushort[]{100,101,102,103}){Save(args[1]+"."+uid+".plain.bin",Offline70RoleProtocol.MonsterDeathBody(uid));Save(args[1]+"."+uid+".packet.bin",Offline70RoleProtocol.MonsterDeathReply(uid,role,Keys()));}Console.WriteLine("Death notification native comparison fixtures emitted");return 0;}
  if(args.Length==2&&(args[0]=="--dungeon-entry-fixture"||args[0]=="--dungeon-monsters-fixture")){bool monsters=args[0]=="--dungeon-monsters-fixture";Offline70Role role=new Offline70Role{Id=1,Name="yoyo",Job=0,Level=1};byte[] keys=Keys();foreach(ushort op in new ushort[]{16,3,28,29,40,30}){Save(args[1]+"."+op+".plain.bin",op==29&&monsters?Offline70RoleProtocol.LorienMonsterRoomBody():Offline70RoleProtocol.DungeonEntryBody(op));Save(args[1]+"."+op+".packet.bin",Offline70RoleProtocol.DungeonEntryReply(op,role,keys,monsters));}Console.WriteLine("First-room candidate fixtures emitted; gameplay unverified");return 0;}
  if(args.Length==2&&args[0]=="--key-settings-fixture"){
   byte[] keys=Keys();Save(args[1]+".schedule.bin",Offline70KeySettings.Schedule(keys));foreach(int n in new int[]{16,32,64,192}){byte[] plain=new byte[n];for(int i=0;i<n;i++)plain[i]=(byte)i;Save(args[1]+".cipher-"+n+".bin",Offline70KeySettings.Crypt(plain,keys,true));byte[] back=Offline70KeySettings.Crypt(Offline70KeySettings.Crypt(plain,keys,true),keys,false);for(int i=0;i<n;i++)if(back[i]!=plain[i])throw new InvalidDataException("Keyboard cipher roundtrip");}
   byte[] mapping=new byte[182];for(int i=0;i<91;i++)BitConverter.GetBytes((ushort)(i+1)).CopyTo(mapping,2*i);byte[] request=new byte[192];BitConverter.GetBytes((uint)182).CopyTo(request,0);mapping.CopyTo(request,4);Save(args[1]+".request-plain.bin",request);Save(args[1]+".request-encrypted.bin",Offline70KeySettings.Crypt(request,keys,true));Save(args[1]+".restore-plain.bin",Offline70KeySettings.Body(mapping));Save(args[1]+".restore-packet.bin",Offline70KeySettings.Reply(mapping,keys));Offline70KeySettings.Save(args[1]+".store.bin",Offline70KeySettings.Parse(Offline70KeySettings.Crypt(request,keys,true),keys));
   mapping[0]=100;Offline70KeySettings.Save(args[1]+".store.bin",mapping);if(Offline70KeySettings.Load(args[1]+".store.bin")[0]!=100)throw new InvalidDataException("Keyboard replacement persistence");bool rejected=false;request[0]=181;try{Offline70KeySettings.Parse(Offline70KeySettings.Crypt(request,keys,true),keys);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new InvalidDataException("Keyboard malformed request accepted");Console.WriteLine("PASS keyboard cipher, atomic persistence and malformed length rejection");return 0;
  }
  if(args.Length==2&&args[0]=="--dungeon-selection-fixture"){
   Offline70Role role=new Offline70Role{Id=1,Name="yoyo",Job=0,Level=1};byte[] keys=Keys();
   foreach(ushort opcode in Offline70DungeonSelection.Opcodes){Save(args[1]+"."+opcode+".plain.bin",Offline70DungeonSelection.Body(opcode));Save(args[1]+"."+opcode+".packet.bin",Offline70DungeonSelection.Reply(opcode,role,keys));}
   bool rejectedKeys=false,rejectedRole=false;keys[333]^=1;try{Offline70DungeonSelection.Reply(27,role,keys);}catch(InvalidDataException){rejectedKeys=true;}keys=Keys();role.Level=3;try{Offline70DungeonSelection.Reply(27,role,keys);}catch(InvalidDataException){rejectedRole=true;}
   if(!rejectedKeys||!rejectedRole)throw new InvalidDataException("Dungeon selection profile controls");Console.WriteLine("PASS bounded native70 selection fixtures; wrong role and key profile rejected");return 0;
  }
  if(args.Length==2&&args[0]=="--quest-fixture"){
   Offline70Role role=new Offline70Role{Id=1,Name="yoyo",Job=0,Level=1};byte[] keys=Keys();
   Save(args[1]+".plain.bin",Offline70QuestProtocol.Body(role));Save(args[1]+".packet.bin",Offline70QuestProtocol.Reply(role,keys));
   bool rejectedRole=false,rejectedKeys=false;role.Level=3;
   try{Offline70QuestProtocol.Reply(role,keys);}catch(InvalidDataException){rejectedRole=true;}role.Level=1;keys[204]^=1;
   try{Offline70QuestProtocol.Reply(role,keys);}catch(InvalidDataException){rejectedKeys=true;}
   if(!rejectedRole||!rejectedKeys)throw new InvalidDataException("Quest fixture profile controls failed");
   Console.WriteLine("PASS quest packet emitted; wrong level and keys rejected");return 0;
  }
  if(args.Length==3&&args[0]=="--fixture")return Fixtures(args[1],args[2]);
  int listenPort=7002;
  if(args.Length>=6&&args[args.Length-2]=="--test-port"){
   if(!Int32.TryParse(args[args.Length-1],out listenPort)||listenPort<17000||listenPort>17999)throw new InvalidDataException("Isolated replay port range");
   string[] portArgs=new string[args.Length-2];Array.Copy(args,portArgs,portArgs.Length);args=portArgs;
  }
  bool finishGoldOnly=args.Length>=4&&args[3]=="--quest-finish-gold-only";
  bool finishSafe=args.Length>=4&&args[3]=="--quest-finish-safe";
  bool dungeonInfoOnly=args.Length>=4&&args[3]=="--keys-dungeon-info";
  bool itemDropTest=args.Length>=4&&args[3]=="--keys-dungeon-item-test";
  bool goldDropTest=args.Length>=4&&args[3]=="--keys-dungeon-gold-test";
  bool goldDrops=args.Length>=4&&(args[3]=="--keys-dungeon-gold"||goldDropTest||itemDropTest);
  bool levelUpTest=args.Length>=4&&(args[3]=="--keys-dungeon-levelup-test"||args[3]=="--keys-dungeon-skills-test");
  bool startingSkills=args.Length>=4&&(args[3]=="--keys-dungeon-skills"||args[3]=="--keys-dungeon-skills-test"||goldDrops);
  bool levelUp=args.Length>=4&&(args[3]=="--keys-dungeon-levelup"||levelUpTest||startingSkills);
  bool monsterExperience=args.Length>=4&&(args[3]=="--keys-dungeon-experience"||levelUp);
  bool monsterDeathConfirm=args.Length>=4&&(args[3]=="--keys-dungeon-death"||monsterExperience);
  bool monsterDeathCapture=args.Length>=4&&(args[3]=="--keys-dungeon-death-capture"||monsterDeathConfirm);
  bool dungeonMonsters=args.Length>=4&&(args[3]=="--keys-dungeon-monsters"||monsterDeathCapture);
  bool dungeonEntry=args.Length>=4&&(args[3]=="--keys-dungeon-entry"||dungeonInfoOnly||dungeonMonsters);
  bool keySettings=args.Length>=4&&(args[3]=="--keys-dungeon-select"||dungeonEntry);
  bool dungeonSelection=args.Length>=4&&(args[3]=="--dungeon-select"||keySettings);
  bool questFinish=args.Length>=4&&(args[3]=="--quest-finish"||finishSafe||finishGoldOnly||dungeonSelection);
  bool questProgress=args.Length>=4&&(args[3]=="--quest-progress"||questFinish);
  bool questAccept=args.Length>=4&&(args[3]=="--quest-accept"||questProgress);
  bool questList=args.Length>=4&&(args[3]=="--quest-list"||questAccept);
  bool interactive=args.Length==5&&(args[3]=="--area-roundtrip"||questList)&&args[4]=="--interactive";
  if(interactive){string[] coreArgs=new string[4];Array.Copy(args,coreArgs,4);args=coreArgs;}
  if((args.Length!=3&&args.Length!=4)||args[1]!="--store"||(args.Length==4&&args[3]!="--appearance"&&args[3]!="--addition"&&args[3]!="--town"&&args[3]!="--loading"&&args[3]!="--world"&&args[3]!="--world-town"&&args[3]!="--area-exit"&&args[3]!="--area-roundtrip"&&!questList))return 2;
  bool appearance=args.Length==4,roundTrip=appearance&&(args[3]=="--area-roundtrip"||questList),areaExit=appearance&&(args[3]=="--area-exit"||roundTrip),loading=appearance&&args[3]=="--loading",completedTutorial=appearance&&(args[3]=="--world-town"||areaExit),world=appearance&&(args[3]=="--world"||completedTutorial),town=appearance&&(args[3]=="--town"||loading||world),addition=appearance&&(args[3]=="--addition"||town);
  string path=Path.GetFullPath(args[0]),storePath=Path.GetFullPath(args[2]);
  string permitted=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,itemDropTest?"DNF70-item-drop-test-FF":(goldDropTest?"DNF70-gold-drop-test-FE":(levelUpTest?"DNF70-levelup-test-EU":"DNF70-offline-role-test")));
  if(Path.GetDirectoryName(storePath)!=permitted||Path.GetFileName(storePath)!="roles-CX.xml")throw new InvalidDataException("Unexpected diagnostic store path");
  string keyPath=Path.Combine(permitted,"keys-account1-EH.bin");byte[] savedKeys=keySettings?Offline70KeySettings.Load(keyPath):null;int keyRequests=0;
  string questPath=Path.Combine(permitted,"quests-yoyo-DO.xml");Offline70QuestState questState=questAccept?Offline70QuestAccept.Load(questPath):new Offline70QuestState();int questRequests=0,progressRequests=0,finishRequests=0;
  string experiencePath=Path.Combine(permitted,"experience-yoyo-ET.xml");Offline70ExperienceState experienceState=monsterExperience?Offline70MonsterExperience.Load(experiencePath):new Offline70ExperienceState();
  string skillPath=Path.Combine(permitted,"skills-yoyo-EZ.xml");Offline70SkillSlotState skillSlots=startingSkills?Offline70SkillSlots.Load(skillPath):null;int skillSwapRequests=0;
  string goldPath=Path.Combine(permitted,"gold-loot-yoyo-FE.xml");Offline70GoldState goldState=goldDrops?Offline70GoldLoot.Load(goldPath):new Offline70GoldState();string goldRun=Guid.NewGuid().ToString("N");Random goldRandom=new Random();Dictionary<ushort,Offline70GoldDrop> groundGold=new Dictionary<ushort,Offline70GoldDrop>();HashSet<ushort> pickedGold=new HashSet<ushort>();int goldRequests=0;
  string itemPath=Path.Combine(permitted,"items-yoyo-FF.xml");Offline70ItemState itemState=itemDropTest?Offline70ItemLoot.Load(itemPath):new Offline70ItemState();Dictionary<ushort,Offline70ItemDrop> groundItems=new Dictionary<ushort,Offline70ItemDrop>();HashSet<ushort> pickedItems=new HashSet<ushort>();int itemRequests=0;
  using(FileStream file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(StreamWriter log=new StreamWriter(file)){
   log.AutoFlush=true;TcpListener listener=new TcpListener(IPAddress.Loopback,listenPort);TcpClient client=null;
   try {
    Offline70RoleStore store=Load(storePath);if(monsterExperience)Offline70MonsterExperience.Apply(store,experienceState);Note(log,"Store loaded accountId=1 roles="+store.Roles.Count+" path="+storePath);if(levelUpTest)Note(log,"LEVELUP_TEST isolated progression copy; starting experience is a diagnostic seed, original player store is not used");
    listener.Start();Note(log,"Listening 127.0.0.1:"+listenPort+"; single-player diagnostic service; noTimeLimit="+interactive);
    Stopwatch timer=Stopwatch.StartNew();byte[] buffer=new byte[4096],pending=new byte[0],keys=Keys();long received=0;int replies=0,selectRequests=0,selectedSlot=-1,areaRequests=0,areaTransitions=0,currentArea=1,dungeonSelectionRequests=0,deathRequests=0;bool loginSent=false,rosterSent=false,selectSent=false,areaSent=false,inDungeon=false,dungeonLoaded=false;HashSet<ushort> deadMonsters=new HashSet<ushort>();
    if(areaExit)Note(log,roundTrip?"AREA_ROUNDTRIP profile: captured town1/area1 <-> town1/area0 gates; session-only state, create disabled; noTimeLimit="+interactive:"AREA_EXIT profile: one captured town1/area1 -> town1/area0 exit only; no return gate or position persistence; create disabled");
    while(interactive||timer.ElapsedMilliseconds<300000){
     if(client==null&&listener.Pending()){
      client=listener.AcceptTcpClient();Note(log,"Accepted "+client.Client.RemoteEndPoint);Save(path+".keys.bin",keys);
      byte[] init=ProbeChannelInit.Packet(keys,(uint)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds);ProbeLoginCandidate.ReceiverGate(init,0x27);Send(client,log,path,"channel-init",init);
     }
     if(client!=null){
      if(client.Available>0){int count=client.GetStream().Read(buffer,0,interactive?buffer.Length:Math.Min(buffer.Length,(int)(65536-received)));received+=count;Note(log,"Received "+count+" "+BitConverter.ToString(buffer,0,count));byte[] joined=new byte[pending.Length+count];pending.CopyTo(joined,0);Buffer.BlockCopy(buffer,0,joined,pending.Length,count);pending=joined;
       while(pending.Length>=13){int size=BitConverter.ToInt32(pending,3);if(size<13||size>65536)throw new InvalidDataException("Outgoing frame bounds");if(pending.Length<size)break;ushort command=BitConverter.ToUInt16(pending,1),sequence=BitConverter.ToUInt16(pending,11);Note(log,"Frame type="+pending[0]+" opcode="+command+" size="+size+" seq="+sequence);
        byte[] body=new byte[size-13];Buffer.BlockCopy(pending,13,body,0,body.Length);
        if(pending[0]==1&&command==1&&!loginSent){byte[] plain=ProbeLoginCandidate.Crypt(body,keys,false);if(plain.Length<10||BitConverter.ToUInt32(plain,0)!=1||plain[4]!=49||BitConverter.ToUInt32(plain,5)!=1||plain[9]!=49)throw new InvalidDataException("Expected bounded account 1 login");Send(client,log,path,"login-reply",ProbeLoginCandidate.LoginPacket(keys));loginSent=true;}
        if(pending[0]==1&&command==8&&loginSent&&!rosterSent){Send(client,log,path,"initial-roster",Offline70RoleProtocol.RosterReply(keys,store.Roles));Note(log,"Initial roster roles="+store.Roles.Count);rosterSent=true;if(keySettings&&savedKeys!=null){Send(client,log,path,"keyboard-restore",Offline70KeySettings.Reply(savedKeys,keys));Note(log,"KEYBOARD_RESTORED account=1 bytes=182 nativeNotify=185 phase=before-select; saved mapping read from separate store");}}
        if(pending[0]==1&&command==4&&loginSent&&rosterSent){
         int slot=Offline70SelectCipher.ParseSlot(body,keys,store.Roles.Count);Offline70Role selected=store.Roles[slot];
         selectRequests++;Note(log,"SELECT_REQUEST count="+selectRequests+" seq="+sequence+" slot="+slot+" priorReply="+selectSent);
         if(selectSent&&!town){Note(log,"Repeated SELECT captured; earlier candidate does not replay initialization");}
         else {
         if(!interactive&&selectRequests>2||selectedSlot>=0&&selectedSlot!=slot)throw new InvalidDataException("SELECT replay diagnostic bounds");selectedSlot=slot;
         string suffix=selectSent?"-repeat-"+selectRequests:"";
         Save(path+".select-request-plain"+suffix+".bin",Offline70SelectCipher.Crypt(body,keys,false));Save(path+".select-ack-body"+suffix+".bin",Offline70SelectCipher.Body(selected,completedTutorial,questAccept&&questState.Accepted&&!questState.Completed,questState.Progress));
         Note(log,"SELECT slot="+slot+" roleId="+selected.Id+" name="+selected.Name+" townCandidate=1; no store write; full initialization unfinished");
         Send(client,log,path,"select-ack"+suffix,Offline70SelectCipher.Reply(selected,keys,completedTutorial,questAccept&&questState.Accepted&&!questState.Completed,questState.Progress));selectSent=true;
         if(questAccept)Note(log,"QUEST_RESTORE role=1 quest101Accepted="+questState.Accepted+" progress="+questState.Progress+" completed="+questState.Completed+" gold="+questState.Gold+"; separate quest state; original role store unchanged");
         if(areaExit){currentArea=1;areaSent=false;}
         if(completedTutorial)Note(log,"TUTORIAL diagnostic reply state=1 completedModules=[31]; no store write; original EE03F0 auto-entry gate expected to skip");
         if(appearance){Thread.Sleep(250);Save(path+".appearance-plain"+suffix+".bin",Offline70RoleProtocol.AppearanceBody(selected));Send(client,log,path,"appearance-notify"+suffix,Offline70RoleProtocol.AppearanceReply(selected,keys));Note(log,"APPEARANCE subtype=0 actorId=1 name=yoyo level=1; minimal zero optional fields; full attributes/town initialization unfinished");}
         if(addition){Thread.Sleep(250);Save(path+".addition-plain"+suffix+".bin",Offline70RoleProtocol.AdditionBody(selected,experienceState.TotalExperience,startingSkills));Send(client,log,path,"addition-notify"+suffix,Offline70RoleProtocol.AdditionReply(selected,keys,experienceState.TotalExperience,startingSkills));Note(log,"ADDITION subtype=1 actorId=1 statBytes=87; current70 PVF startingSkillCount="+(startingSkills?11:0)+" activeBank=0; equipment and optional lists empty");if(monsterExperience)Note(log,"EXPERIENCE_RESTORED role=1 total="+experienceState.TotalExperience+" nativeNotify=2 silent=True; no reward notification");if(startingSkills){Save(path+".skills19-plain"+suffix+".bin",Offline70RoleProtocol.StartingSkillsBody(selected,skillSlots));Send(client,log,path,"skills19-notify"+suffix,Offline70RoleProtocol.StartingSkillsReply(selected,keys,skillSlots));Note(log,"STARTING_SKILLS nativeNotify=19 localPlayer=True count=11 banks=2 savedSlots="+File.Exists(skillPath)+"; learning and skill points pending");}}
         if(town){foreach(ushort opcode in new ushort[]{3,23,24}){Thread.Sleep(250);Save(path+".town-"+opcode+"-plain"+suffix+".bin",Offline70TownProtocol.Body(opcode));Send(client,log,path,"town-"+opcode+"-notify"+suffix,Offline70TownProtocol.Reply(opcode,selected,keys));}Note(log,"TOWN_INIT actor=1 town=1 area=1 x=474 y=234 dir=5 flags=3; fixed diagnostic fixtures; complete town/gameplay not verified");}
         if(loading){Thread.Sleep(500);Save(path+".loading-complete-plain"+suffix+".bin",Offline70RoleProtocol.LoadingCompleteBody());Send(client,log,path,"loading-complete-notify"+suffix,Offline70RoleProtocol.LoadingCompleteReply(selected,keys));Note(log,"LOADING_COMPLETE notify30 field=u32 zero candidate; no uplink40 required in this diagnostic; actual town rendering not verified");}
         if(world){Thread.Sleep(500);Save(path+".enter-world-complete-plain"+suffix+".bin",Offline70RoleProtocol.EnterWorldCompleteBody());Send(client,log,path,"enter-world-complete-notify"+suffix,Offline70RoleProtocol.EnterWorldCompleteReply(selected,keys));Note(log,"ENTER_GAMEWORLD_COMPLETE notify136 native callback49CEA0; opaque 7-byte body ignored by handler; notify30 omitted for town; actual rendering/gameplay not verified");}
         if(questFinish&&questState.Completed&&!finishSafe){if(!finishGoldOnly)Send(client,log,path,"quest-history-restore"+suffix,Offline70QuestFinish.HistoryReply(keys,true));uint restoredGold=Offline70GoldLoot.Total(goldState,questState.Gold);Send(client,log,path,"quest-gold-restore"+suffix,itemDropTest?Offline70ItemLoot.RestoreReply(keys,restoredGold,itemState):Offline70QuestFinish.GoldRestoreReply(keys,restoredGold));Note(log,"QUEST_FINISH_RESTORE completed101=True gold="+restoredGold+" historyHeld="+finishGoldOnly+" silent=True; no additional reward granted");}
         else if(goldDrops&&(goldState.Earned>0||itemDropTest&&itemState.Quantity>0)){uint restoredGold=Offline70GoldLoot.Total(goldState,questState.Gold);Send(client,log,path,"loot-gold-restore"+suffix,itemDropTest?Offline70ItemLoot.RestoreReply(keys,restoredGold,itemState):Offline70QuestFinish.GoldRestoreReply(keys,restoredGold));}
         if(itemDropTest)Note(log,"ITEM_RESTORED id=3015 slot=105 quantity="+itemState.Quantity+" gold="+Offline70GoldLoot.Total(goldState,questState.Gold)+" silent=True; one material profile, copied test store");
         if(finishSafe&&questState.Completed)Note(log,"QUEST_FINISH_SAFE completed101=True gold="+questState.Gold+" retained in store; extra history/gold notifications held for crash isolation");
         if(questList){Thread.Sleep(250);Save(path+".quest-list-plain"+suffix+".bin",Offline70QuestProtocol.Body(selected,questState.Completed));Send(client,log,path,"quest-list-notify"+suffix,Offline70QuestProtocol.Reply(selected,keys,questState.Completed));Note(log,"QUEST_LIST notify21 native491820 level=1 count="+(questState.Completed?0:1)+" ids="+(questState.Completed?"[]":"[101]")+"; current70 PVF bounded quest101 profile; no unverified successor quests offered");}
         }
        }
        if(pending[0]==1&&command==33&&questAccept&&selectSent){
         questRequests++;string label="quest-accept-"+questRequests;bool allowed=false;
         Save(path+"."+label+".encrypted.bin",body);
         try {ushort quest=Offline70QuestAccept.Parse(body,keys);Save(path+"."+label+".plain.bin",Offline70RoleProtocol.Xtea(body,keys,false));
          Offline70Role selected=store.Roles[selectedSlot];Offline70QuestProtocol.Body(selected);
          if(quest==101){questState=Offline70QuestAccept.Accept(questPath);allowed=true;Note(log,"QUEST_COMMITTED quest=101 role=1 progress="+questState.Progress+" reloadedAccepted="+questState.Accepted+" request="+questRequests);}
          else Note(log,"QUEST_REJECTED unsupported quest="+quest);
         }catch(InvalidDataException e){Note(log,"QUEST_REJECTED "+e.Message);}
         byte[] acceptPlain=Offline70QuestAccept.Body(allowed);if(allowed)BitConverter.GetBytes(questState.Progress).CopyTo(acceptPlain,3);Save(path+"."+label+".reply-plain.bin",acceptPlain);Send(client,log,path,label+"-reply",Offline70QuestAccept.Reply(keys,allowed,questState.Progress));
        }
        if(pending[0]==1&&command==35&&questProgress&&selectSent){
         string label="quest-progress-"+(++progressRequests);bool allowed=false;Save(path+"."+label+".encrypted.bin",body);
         try{ushort quest=Offline70QuestProgress.Parse(body,keys);Save(path+"."+label+".plain.bin",Offline70QuestProgress.Crypt(body,keys,false));
          if(quest!=101||!questState.Accepted||currentArea!=0)throw new InvalidDataException("Quest101 meet NPC requires accepted quest in Elvengard");
          questState=Offline70QuestAccept.Ready(questPath);allowed=true;Note(log,"QUEST_READY quest=101 role=1 progress="+questState.Progress+" reloaded=True sourceArea="+currentArea+"; finish and rewards unfinished");
         }catch(InvalidDataException e){Note(log,"QUEST_PROGRESS_REJECTED "+e.Message);}
         Save(path+"."+label+".reply-plain.bin",Offline70QuestProgress.Body(allowed));Send(client,log,path,label+"-reply",Offline70QuestProgress.Reply(keys,allowed));
        }
        if(pending[0]==1&&command==36&&questFinish&&selectSent){
         string label="quest-finish-"+(++finishRequests);bool allowed=false,duplicate=false;Save(path+"."+label+".encrypted.bin",body);
         try{ushort quest=Offline70QuestFinish.Parse(body,keys);Save(path+"."+label+".plain.bin",Offline70QuestFinish.RequestPlain());
          if(quest!=101||currentArea!=0)throw new InvalidDataException("Quest101 finish requires captured NPC1 request in Elvengard");
          duplicate=questState.Completed;questState=Offline70QuestAccept.Complete(questPath);allowed=true;Note(log,"QUEST_FINISHED quest=101 role=1 completed="+questState.Completed+" gold="+questState.Gold+" duplicate="+duplicate+"; completion and reward committed together and reloaded");
         }catch(InvalidDataException e){Note(log,"QUEST_FINISH_REJECTED "+e.Message);}
         Save(path+"."+label+".reply-plain.bin",Offline70QuestFinish.Body(allowed));Send(client,log,path,label+"-reply",Offline70QuestFinish.Reply(keys,allowed));
         if(allowed){if(!finishSafe){if(!duplicate)Send(client,log,path,label+"-gold",Offline70QuestFinish.GoldReply(keys,questState.Gold));if(!finishGoldOnly)Send(client,log,path,label+"-history",Offline70QuestFinish.HistoryReply(keys,true));}Send(client,log,path,label+"-offers",Offline70QuestProtocol.Reply(store.Roles[selectedSlot],keys,true));}
        }
        if(pending[0]==1&&command==38&&areaExit&&selectSent){
         areaRequests++;if(!interactive&&areaRequests>12)throw new InvalidDataException("Area diagnostic request limit");
         Save(path+".area-request-"+areaRequests+".encrypted.bin",body);
         Offline70Role selected=store.Roles[selectedSlot];
         if(questAccept){
          Offline70Gate gate=null;try{gate=Offline70Gate.Parse(body,selected,keys,currentArea);}catch(InvalidDataException e){Note(log,"GATE_REJECTED request="+areaRequests+" currentArea="+currentArea+" reason="+e.Message);}
          if(gate!=null){areaTransitions++;string label="gate-step-"+areaTransitions+"-";Save(path+"."+label+"request-plain.bin",gate.Plain);
           foreach(ushort op in new ushort[]{38,23,24}){Save(path+"."+label+op+"-plain.bin",gate.Body(op));Send(client,log,path,label+op+"-reply",gate.Reply(op,keys));Thread.Sleep(250);}
           currentArea=gate.Area;areaSent=true;Note(log,"GATE_SENT step="+areaTransitions+" sourceArea="+BitConverter.ToUInt16(gate.Plain,9)+" area="+gate.Area+" x="+gate.X+" y="+gate.Y+"; source checked; full dungeon gameplay unfinished");
          }else Send(client,log,path,"gate-rejected-"+areaRequests,Offline70Gate.Reject(keys));
         }else {
         bool returning=roundTrip&&currentArea==0;
         if((roundTrip||!areaSent)&&Offline70AreaProtocol.MatchesGate(body,selected,keys,returning)){
          areaTransitions++;if(!interactive&&areaTransitions>8)throw new InvalidDataException("Gate transition diagnostic limit");
          string label=roundTrip?"area-step-"+areaTransitions+"-":"area-";
          Save(path+"."+(roundTrip?label+"request-plain":"area-request-plain")+".bin",Offline70AreaProtocol.RequestBody(returning));
          string target=returning?"town=1 area=1 x=474 y=336 dir=5 sourceTown=1 sourceArea=0":"town=1 area=0 x=785 y=158 dir=5 sourceTown=1 sourceArea=1";
          Note(log,"AREA_REQUEST step="+areaTransitions+" matched native captured fields "+target+" seq="+sequence+"; complete encrypted fixture match; uplink CRC not fully validated");
          foreach(ushort opcode in new ushort[]{38,23,24}){Save(path+"."+label+opcode+"-plain.bin",Offline70AreaProtocol.Body(opcode,returning));Send(client,log,path,label+opcode+"-reply",Offline70AreaProtocol.Reply(opcode,selected,keys,returning));Thread.Sleep(250);}
          areaSent=true;currentArea=returning?1:0;Note(log,"AREA_SENT step="+areaTransitions+" "+target+"; ack38 success=1 then notifications23/24; wire-only diagnostic, no store write");
         }else Note(log,"AREA_UNSUPPORTED count="+areaRequests+" currentArea="+currentArea+" priorAreaReply="+areaSent+"; captured without transition; only preverified gate fixtures are supported");
         }
        }
        if(pending[0]==1&&command==15&&dungeonSelection){
         if(loginSent&&selectSent&&selectedSlot==0&&currentArea==2&&body.Length==0){
          dungeonSelectionRequests++;string label="dungeon-selection-"+dungeonSelectionRequests+"-";Offline70Role selected=store.Roles[selectedSlot];
          foreach(ushort opcode in Offline70DungeonSelection.Opcodes){Save(path+"."+label+opcode+"-plain.bin",Offline70DungeonSelection.Body(opcode));Send(client,log,path,label+opcode+"-notify",Offline70DungeonSelection.Reply(opcode,selected,keys));}
          Note(log,"DUNGEON_SELECTION_SENT request="+dungeonSelectionRequests+" seq="+sequence+" town=1 area=2 role=1 dungeon=1 normal=0 native27Body=13; no store write; dungeon entry and combat unfinished");
         }else Note(log,"DUNGEON_SELECTION_REJECTED seq="+sequence+" area="+currentArea+" slot="+selectedSlot+" body="+body.Length+"; requires selected role1, corridor area2 and captured empty request");
        }
        if(pending[0]==1&&command==16&&dungeonSelection){Note(log,"DUNGEON_SELECT_REQUEST seq="+sequence+" body="+BitConverter.ToString(body));if(dungeonEntry&&selectSent&&currentArea==2&&dungeonSelectionRequests>0&&!inDungeon&&Offline70RoleProtocol.LorienEntryMatches(body,store.Roles[selectedSlot],keys)){foreach(ushort op in (dungeonInfoOnly?new ushort[]{16,3,28}:new ushort[]{16,3,28,29})){Send(client,log,path,"lorien-entry-"+op,Offline70RoleProtocol.DungeonEntryReply(op,store.Roles[selectedSlot],keys,dungeonMonsters));Thread.Sleep(100);}inDungeon=true;Note(log,"LORIEN_ENTRY_SENT dungeon=1 normal=0 maze=0 boss=0,2 room=0,0 map=3 monsters="+(dungeonMonsters?4:0)+" infoOnly="+dungeonInfoOnly+"; native70 first-room candidate, damage settlement and drops unfinished");}}
        if(pending[0]==1&&command==40&&dungeonEntry){Note(log,"DUNGEON_LOAD_REQUEST seq="+sequence+" body="+BitConverter.ToString(body));if(!dungeonInfoOnly&&inDungeon&&!dungeonLoaded&&Offline70RoleProtocol.LorienLoadMatches(body,store.Roles[selectedSlot],keys)){Send(client,log,path,"lorien-load-ack",Offline70RoleProtocol.DungeonEntryReply(40,store.Roles[selectedSlot],keys));Send(client,log,path,"lorien-load-complete",Offline70RoleProtocol.DungeonEntryReply(30,store.Roles[selectedSlot],keys));dungeonLoaded=true;byte[] loadPlain=Offline70RoleProtocol.LorienLoadPlain(body,store.Roles[selectedSlot],keys);Save(path+".lorien-load.request-plain.bin",loadPlain);Note(log,"LORIEN_LOAD_COMPLETE_SENT ack40 notify30 marker="+BitConverter.ToUInt32(loadPlain,0)+" clientState="+BitConverter.ToUInt32(loadPlain,4)+"; validated native8-byte fields");}}
        if(pending[0]==1&&command==30&&startingSkills&&selectSent){string label="skill-swap-"+(++skillSwapRequests);bool allowed=false;byte source=0,target=0;Save(path+"."+label+".encrypted.bin",body);
         try{byte[] plain=Offline70RoleProtocol.SkillSwapParse(body,keys);Save(path+"."+label+".plain.bin",plain);source=plain[1];target=plain[2];skillSlots=Offline70SkillSlots.Swap(skillPath,source,target);allowed=true;Note(log,"SKILL_SLOTS_COMMITTED role=1 bank=0 source="+source+" target="+target+" reloaded=True seq="+sequence);}
         catch(InvalidDataException e){Note(log,"SKILL_SWAP_REJECTED "+e.Message);}
         Send(client,log,path,label+"-reply",Offline70RoleProtocol.SkillSwapReply(keys,allowed,source,target));}
        if(pending[0]==1&&command==213&&keySettings&&loginSent&&selectSent){string label="keyboard-save-"+(++keyRequests);try{byte[] mapping=Offline70KeySettings.Parse(body,keys);Offline70KeySettings.Save(keyPath,mapping);savedKeys=Offline70KeySettings.Load(keyPath);Save(path+"."+label+".mapping.bin",savedKeys);Note(log,"KEYBOARD_COMMITTED account=1 bytes=182 seq="+sequence+" reloaded=True; role and quest stores unchanged");}catch(InvalidDataException e){Note(log,"KEYBOARD_REJECTED "+e.Message);}}
        if(pending[0]==1&&command==42&&monsterDeathCapture&&selectSent&&inDungeon&&dungeonLoaded){
         string label="monster-death-"+(++deathRequests);Save(path+"."+label+".encrypted.bin",body);
         if(body.Length>=32&&body.Length<=512&&body.Length%4==0){byte[] plain=(byte[])body.Clone();for(int i=0;i<plain.Length;i++)plain[i]^=keys[4+i%4];Save(path+"."+label+".plain.bin",plain);Note(log,"MONSTER_DEATH_CAPTURE seq="+sequence+" bytes="+plain.Length+" uid="+BitConverter.ToUInt16(plain,0)+" killer="+BitConverter.ToUInt16(plain,2)+" hex="+BitConverter.ToString(plain));}
         else Note(log,"MONSTER_DEATH_CAPTURE_REJECTED length="+body.Length);
         if(monsterDeathConfirm){try{Offline70Role role=store.Roles[selectedSlot];ushort uid=Offline70RoleProtocol.ParseMonsterDeathRequest(body,role,keys);if(!deadMonsters.Contains(uid)){
          bool experienceAwarded=false;byte previousLevel=role.Level;
          if(monsterExperience){try{experienceState=Offline70MonsterExperience.Award(experiencePath,uid);Offline70MonsterExperience.Apply(store,experienceState);experienceAwarded=true;Note(log,"EXPERIENCE_COMMITTED role=1 uid="+uid+" gained="+Offline70MonsterExperience.GoblinExperience+" total="+experienceState.TotalExperience+" reloaded=True");}catch(InvalidDataException e){Note(log,"EXPERIENCE_HELD uid="+uid+" "+e.Message);}}
          Offline70GoldDrop goldDrop=goldDrops&&!itemDropTest?Offline70GoldLoot.Spawn(uid,goldRun,goldRandom,goldDropTest):null;if(goldDrop!=null){groundGold.Add(goldDrop.Slot,goldDrop);Save(path+"."+label+".gold-death-plain.bin",Offline70GoldLoot.DeathBody(uid,goldDrop));Note(log,"GOLD_DROP_SPAWNED uid="+uid+" slot="+goldDrop.Slot+" amount="+goldDrop.Amount+" forcedTest="+goldDropTest+"; ground only, balance unchanged");}
          Offline70ItemDrop itemDrop=itemDropTest?Offline70ItemLoot.Spawn(uid,goldRun):null;if(itemDrop!=null){groundItems.Add(itemDrop.Slot,itemDrop);Save(path+"."+label+".item-death-plain.bin",Offline70ItemLoot.DeathBody(uid,itemDrop));Note(log,"ITEM_DROP_SPAWNED uid="+uid+" slot="+itemDrop.Slot+" id=3015 quantity=1 forcedTest=True; ground only");}
          deadMonsters.Add(uid);Send(client,log,path,label+"-notify38",itemDropTest?Offline70ItemLoot.DeathReply(uid,itemDrop,role,keys):(goldDrops?Offline70GoldLoot.DeathReply(uid,goldDrop,role,keys):Offline70RoleProtocol.MonsterDeathReply(uid,role,keys)));
          if(experienceAwarded){Save(path+"."+label+".exp-plain.bin",Offline70MonsterExperience.Body(experienceState.TotalExperience));Send(client,log,path,label+"-notify37",Offline70MonsterExperience.Reply(role,keys,experienceState.TotalExperience));}
          if(experienceAwarded&&role.Level!=previousLevel){Send(client,log,path,label+"-levelup-stats",Offline70RoleProtocol.AdditionReply(role,keys,experienceState.TotalExperience,startingSkills));if(startingSkills)Send(client,log,path,label+"-levelup-skills",Offline70RoleProtocol.StartingSkillsReply(role,keys,skillSlots));Send(client,log,path,label+"-levelup-offers",Offline70QuestProtocol.Reply(role,keys,questState.Completed));Note(log,"LEVELUP_COMMITTED role=1 oldLevel="+previousLevel+" level="+role.Level+" total="+experienceState.TotalExperience+" nextThreshold=2653 nativeNotify=37; PVF first growth attributes sent, startingSkillCount="+(startingSkills?11:0)+"; SP and skill learning pending");}
          Note(log,"MONSTER_DEATH_CONFIRMED uid="+uid+" seq="+sequence+" deadCount="+deadMonsters.Count+" checksum=True experienceAwarded="+experienceAwarded+"; gold and item stores unchanged");
         }else Note(log,"MONSTER_DEATH_DUPLICATE uid="+uid+"; no repeated notification or reward");}catch(InvalidDataException e){Note(log,"MONSTER_DEATH_REJECTED seq="+sequence+" "+e.Message);}}
        }
        if(pending[0]==1&&command==46&&goldDrops&&!itemDropTest){string label="gold-pickup-"+(++goldRequests);Save(path+"."+label+".encrypted.bin",body);
         try{if(!selectSent||selectedSlot!=0||!inDungeon||!dungeonLoaded)throw new InvalidDataException("Gold room state");byte[] plain=Offline70GoldLoot.ParsePlain(body,keys);Save(path+"."+label+".plain.bin",plain);ushort slot=BitConverter.ToUInt16(plain,0);Offline70GoldDrop drop;
          if(pickedGold.Contains(slot)){Send(client,log,path,label+"-duplicate-ack",Offline70GoldLoot.Ack(keys));Note(log,"GOLD_PICKUP_DUPLICATE slot="+slot+"; no acquisition replay");}
          else{if(!groundGold.TryGetValue(slot,out drop))throw new InvalidDataException("Gold ground slot unavailable");bool duplicate;goldState=Offline70GoldLoot.Pickup(goldPath,drop,questState.Gold,out duplicate);groundGold.Remove(slot);pickedGold.Add(slot);if(!duplicate)Send(client,log,path,label+"-notify39",Offline70GoldLoot.PickupReply(slot,drop.Amount,keys));Send(client,log,path,label+"-ack46",Offline70GoldLoot.Ack(keys));Note(log,"GOLD_PICKUP_COMMITTED slot="+slot+" gained="+drop.Amount+" balance="+Offline70GoldLoot.Total(goldState,questState.Gold)+" receipt="+drop.Receipt+" duplicate="+duplicate+" reloaded=True");}
         }catch(InvalidDataException e){Note(log,"GOLD_PICKUP_REJECTED "+e.Message);}
        }
        if(pending[0]==1&&command==46&&itemDropTest){string label="item-pickup-"+(++itemRequests);Save(path+"."+label+".encrypted.bin",body);try{if(!selectSent||selectedSlot!=0||!inDungeon||!dungeonLoaded)throw new InvalidDataException("Material room state");byte[] plain=Offline70GoldLoot.ParsePlain(body,keys);Save(path+"."+label+".plain.bin",plain);ushort slot=BitConverter.ToUInt16(plain,0);Offline70ItemDrop drop;if(pickedItems.Contains(slot)){Send(client,log,path,label+"-duplicate-ack",Offline70GoldLoot.Ack(keys));Note(log,"ITEM_PICKUP_DUPLICATE slot="+slot+"; no repeated inventory or acquisition");}else{if(!groundItems.TryGetValue(slot,out drop))throw new InvalidDataException("Material ground slot unavailable");bool duplicate;itemState=Offline70ItemLoot.Pickup(itemPath,drop,out duplicate);groundItems.Remove(slot);pickedItems.Add(slot);if(!duplicate)Send(client,log,path,label+"-notify39",Offline70ItemLoot.PickupReply(slot,keys));Send(client,log,path,label+"-ack46",Offline70GoldLoot.Ack(keys));Note(log,"ITEM_PICKUP_COMMITTED slot="+slot+" id=3015 inventorySlot=105 quantity="+itemState.Quantity+" receipt="+drop.Receipt+" duplicate="+duplicate+" reloaded=True; gold unchanged");}}catch(InvalidDataException e){Note(log,"ITEM_PICKUP_REJECTED "+e.Message);}}
        if(pending[0]==1&&command==5&&loginSent&&rosterSent&&!areaExit){
         if(++replies>12)throw new InvalidDataException("Create reply limit");string label="create-"+replies;
         try {
          Offline70Role role=Offline70RoleProtocol.ParseCreate(body,keys);Save(path+"."+label+".request-plain.bin",Offline70RoleProtocol.Xtea(body,keys,false));
          store=Create(storePath,role);Note(log,"COMMITTED roleId="+role.Id+" job="+role.Job+" level="+role.Level+" name="+role.Name+" reloadedRoleCount="+store.Roles.Count);
          Send(client,log,path,label+"-reply",Offline70RoleProtocol.CreateReply(keys,true,0));Send(client,log,path,label+"-roster",Offline70RoleProtocol.RosterReply(keys,store.Roles));
         }catch(InvalidDataException e){Note(log,"REJECTED create: "+e.Message);Send(client,log,path,label+"-rejected",Offline70RoleProtocol.CreateReply(keys,false,4));}
        }
        byte[] rest=new byte[pending.Length-size];Buffer.BlockCopy(pending,size,rest,0,rest.Length);pending=rest;
       }if(!interactive&&received>=65536)break;
      }
      if(client.Client.Poll(0,SelectMode.SelectRead)&&client.Available==0){Note(log,"Peer closed");break;}
     }Thread.Sleep(20);
    }Note(log,"Completed bytes="+received+" roles="+store.Roles.Count+" createReplies="+replies+" selectRequests="+selectRequests);return 0;
   }catch(Exception e){Note(log,"ERROR "+e);return 1;}finally{if(client!=null)client.Close();listener.Stop();}
  }
 }
}
