using System;using System.IO;using System.Xml;using System.Xml.Serialization;
partial class Offline70RoleProtocol {
 internal static byte StartingSkillSlot(byte job,byte id){
  // Placed by the native rule (active skills take the first free quick slot, everything else the
  // first free slot of its reformed group range) and recorded per job in the creation table.
  if(job>=Offline70RoleCreateJobsFG202.CreatableJobsFG202)throw new InvalidDataException("Uncreatable job");
  foreach(byte[] row in Offline70RoleCreateJobsFG202.CreateSkillsFG202[job])if(row[0]==id)return row[2];
  throw new InvalidDataException("Unverified starting skill slot");
 }
 // Native current70 487D60 local-player skill notification, independently of user-info2.
 internal static byte[] StartingSkillsBody(Offline70Role role){
  return StartingSkillsBody(role,Offline70SkillSlots.Default());
 }
 internal static byte[] StartingSkillsBody(Offline70Role role,Offline70SkillSlotState state){
  return StartingSkillsBody(role,state,0);
 }
 internal static byte[] StartingSkillsBody(Offline70Role role,Offline70SkillSlotState state,ushort sp){
  Offline70SkillSlots.Validate(state);
  AppearanceRecord(role);
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write(sp);w.Write((byte)state.Ids.Length); // bank0 SP and skill slots
   for(int i=0;i<state.Ids.Length;i++){w.Write(state.Slots[i]);w.Write(state.Ids[i]);w.Write(state.Levels[i]);w.Write((byte)0);}
   w.Write((ushort)0);w.Write((byte)0); // bank1 SP and empty slots
   w.Write((ushort)0);w.Write((ushort)0); // TP for bank0 and bank1; QP uses separate notify358
   return m.ToArray();
  }
 }
 internal static byte[] StartingSkillsReply(Offline70Role role,byte[] keys){Offline70TownProtocol.Validate(role,keys);return Packet(0,19,Xtea(Pad(StartingSkillsBody(role)),keys,true));}
 internal static byte[] StartingSkillsReply(Offline70Role role,byte[] keys,Offline70SkillSlotState state){Offline70TownProtocol.Validate(role,keys);return Packet(0,19,Xtea(Pad(StartingSkillsBody(role,state)),keys,true));}
 internal static byte[] StartingSkillsReply(Offline70Role role,byte[] keys,Offline70SkillSlotState state,ushort sp){Offline70TownProtocol.Validate(role,keys);return Packet(0,19,Xtea(Pad(StartingSkillsBody(role,state,sp)),keys,true));}
 // Native1016D00 writes three u8 fields without a command marker. NativeBF9C00
 // consumes bank/source/destination after the common success byte.
 internal static byte[] SkillSwapParse(byte[] encrypted,byte[] keys){
  Offline70TownProtocol.Validate(new Offline70Role{Id=1,Job=0,Level=1,Name="yoyo"},keys);
  if(encrypted.Length!=8)throw new InvalidDataException("Skill swap block bounds");byte[] plain=new Blowfish(keys).Decode(encrypted);
  if(plain[0]>1||plain[1]==255||plain[2]==255||plain[1]==plain[2])throw new InvalidDataException("Only bank0 distinct verified skill slots supported");
  // Native captured request contains only three fields. Its unused block tail
  // holds previous buffer bytes, so it cannot be treated as zero-filled data.
  return plain;
 }
 internal static byte[] SkillSwapRequest(byte bank,byte source,byte target,byte[] keys){return new Blowfish(keys).Encode(Pad(new byte[]{bank,source,target}));}
 internal static byte[] SkillSwapReply(byte[] keys,bool success,byte source,byte target){return Packet(1,30,new Blowfish(keys).Encode(Pad(success?new byte[]{1,0,source,target}:new byte[]{0,0x13})));}
}
public class Offline70SkillSlotState {public int Format=1;public ushort RoleId=Offline70RoleScopeFG202.Current;public byte Bank;public byte ProfessionFG139;public byte[] Slots;public byte[] Levels;public ushort SpentSp;public byte[] Ids;public bool CommonPlacementFG81;public string LastResetReceiptFG83;}
partial class Offline70SkillSlots {
 internal static readonly ushort[] TotalSp=new ushort[]{0,30,60,90,120,150,180,210,240,270,330,390,450,510,570,630,690,750,810,870,960,1050,1140,1230,1320,1410,1500,1590,1680,1770,1890,2010,2130,2250,2370,2490,2610,2730,2850,2970,3090,3210,3330,3450,3570,3690,3810,3930,4050,4170,4320,4470,4620,4770,4920,5070,5220,5370,5520,5670,5850,6030,6210,6390,6570,6750,6930,7110,7290,7470};
 internal static ushort Remaining(Offline70SkillSlotState s,byte level){return Remaining(s,level,0);}
 internal static ushort Remaining(Offline70SkillSlotState s,byte level,ushort bonus){Validate(s);if(level<1||level>70||bonus>10000||s.SpentSp>TotalSp[level-1]+bonus)throw new InvalidDataException("Skill SP ledger mismatch");return checked((ushort)(TotalSp[level-1]+bonus-s.SpentSp));}
 internal static byte SkillLevel(Offline70SkillSlotState s,byte id){Validate(s);int index=Array.IndexOf(s.Ids,id);return index<0?(byte)0:s.Levels[index];}
 internal static Offline70SkillSlotState LearnOne(string path,byte level,byte id){return LearnOne(path,level,id,0);}
 internal static Offline70SkillSlotState LearnOne(string path,byte level,byte id,ushort bonus){Offline70SkillSlotState s=Load(path);return Learn(path,level,id,checked((byte)(SkillLevel(s,id)+1)),bonus);}
 internal static Offline70SkillSlotState Learn(string path,byte level,byte id,byte next){return Learn(path,level,id,next,0);}
 internal static Offline70SkillSlotState LearnOne(string path,byte level,byte id,ushort bonus,byte grow,bool awakened){Offline70SkillSlotState s=Load(path);return Learn(path,level,id,checked((byte)(SkillLevel(s,id)+1)),bonus,grow,awakened);}
 internal static Offline70SkillSlotState Learn(string path,byte level,byte id,byte next,ushort bonus){return Learn(path,level,id,next,bonus,0,false);}
 // grow is the character's first grow and awakened its 一次觉醒 flag; JobMaximum needs both, the
 // 60/70 级 and 一觉 skills only ever carry a ceiling once the awakening nibble is set.
 internal static Offline70SkillSlotState Learn(string path,byte level,byte id,byte next,ushort bonus,byte grow,bool awakened){
  Offline70SkillRuleFG rule=Rule(id);if(rule==null||rule.Cost==0)throw new InvalidDataException("Skill outside untransferred SP catalog");
  string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));
  using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   Offline70SkillSlotState s=Load(full);Validate(s);if(next!=SkillLevel(s,id)+1||JobMaximum(rule,grow,awakened)==0||next>JobMaximum(rule,grow,awakened)||level<rule.Required+(next-1)*rule.Step||Remaining(s,level,bonus)<rule.Cost)throw new InvalidDataException("Skill level, role level or SP requirement rejected");
   for(int p=0;p<rule.Prerequisites.Length;p+=2)if(SkillLevel(s,rule.Prerequisites[p])<rule.Prerequisites[p+1])throw new InvalidDataException("Skill prerequisite rejected");
   int index=Array.IndexOf(s.Ids,id);if(index<0){byte slot=FindListFG123(s,id);index=s.Ids.Length;Array.Resize(ref s.Ids,index+1);Array.Resize(ref s.Slots,index+1);Array.Resize(ref s.Levels,index+1);s.Ids[index]=id;s.Slots[index]=slot;}
   s.Levels[index]=next;s.SpentSp=checked((ushort)(s.SpentSp+rule.Cost));Validate(s);
   string temp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+Offline70StoreBackupFG354.Suffix;
   try{using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}Validate(Load(temp));if(File.Exists(full))File.Replace(temp,full,backup);else File.Move(temp,full);Offline70SkillSlotState result=Load(full);if(result.Levels[index]!=next||result.SpentSp!=s.SpentSp)throw new InvalidDataException("Skill commit mismatch");return result;}finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70SkillSlotState));
 internal static readonly byte[] SkillIds=new byte[]{181,182,184,173,179,5,46,48,169,170,174};
 // The creation table is the per-job source of the initial learned list; SkillIds stays as the
 // swordman row that the pre-FG202 archives were written against.
 internal static byte[] StartingIdsFG202(byte job){byte[][] rows=Offline70RoleCreateJobsFG202.CreateSkillsFG202[job];byte[] ids=new byte[rows.Length];for(int i=0;i<rows.Length;i++)ids[i]=rows[i][0];return ids;}
 internal static byte[] StartingLevelsFG202(byte job){byte[][] rows=Offline70RoleCreateJobsFG202.CreateSkillsFG202[job];byte[] levels=new byte[rows.Length];for(int i=0;i<rows.Length;i++)levels[i]=rows[i][1];return levels;}
 internal static bool Active(byte id){Offline70SkillRuleFG r=Rule(id);return r!=null&&r.Active;}
 static byte FindSlot(Offline70SkillSlotState s,bool active){for(int slot=active?150:100;slot<(active?198:150);slot++)if(Array.IndexOf(s.Slots,(byte)slot)<0)return (byte)slot;throw new InvalidDataException("Learned skill list is full");}
 // Current70 captured common positions102..149 (backstep123);150..197 is trait.
 static byte CommonSlot(Offline70SkillSlotState s){for(int slot=102;slot<150;slot++)if(Array.IndexOf(s.Slots,(byte)slot)<0)return (byte)slot;throw new InvalidDataException("Common skill list full");}
 internal static Offline70SkillSlotState NormalizeCommon(string path){
  string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));
  using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   Offline70SkillSlotState s=Load(full);Validate(s);if(s.CommonPlacementFG81)return s;
   int index=Array.IndexOf(s.Ids,(byte)190);if(index>=0&&s.Slots[index]>=150&&s.Slots[index]<198)s.Slots[index]=CommonSlot(s);
   s.CommonPlacementFG81=true;Validate(s);string temp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+Offline70StoreBackupFG354.Suffix;
   try{using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}Validate(Load(temp));if(File.Exists(full))File.Replace(temp,full,backup);else File.Move(temp,full);return Load(full);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static bool Quick(int slot){return slot<6||slot>=198&&slot<204;}
 // CN70 uses low positions for job skills and100..197 for common/list skills.
 // Captured backstep200->115/109/110 proves these are not passive-only slots.
 // Reject passive occupants by skill definition, not by destination number.
 internal static bool ActiveSlot(int slot){return slot>=0&&slot<204;}
 internal static Offline70SkillSlotState Default(){return Default(Offline70RoleScopeFG202.CurrentJob);}
 // The creation table carries id, level and slot, so a character that has never been saved still
 // reports its own job's skill bar instead of the swordman one.
 internal static Offline70SkillSlotState Default(byte job){byte[][] rows=Offline70RoleCreateJobsFG202.CreateSkillsFG202[job];byte[] ids=new byte[rows.Length],levels=new byte[rows.Length],slots=new byte[rows.Length];
  for(int i=0;i<rows.Length;i++){ids[i]=rows[i][0];levels[i]=rows[i][1];slots[i]=rows[i][2];}
  return new Offline70SkillSlotState{Ids=ids,Levels=levels,Slots=slots};}
 internal static void Validate(Offline70SkillSlotState s){
  if(s==null||s.Format!=1||s.RoleId==0||s.Bank!=0||s.Slots==null)throw new InvalidDataException("Skill slot store profile");
  if(s.LastResetReceiptFG83!=null&&!Offline70SkillResetFG83.Receipt(s.LastResetReceiptFG83))throw new InvalidDataException("Skill reset marker");
  byte job=Offline70RoleScopeFG202.CurrentJob;byte[] initialIds=StartingIdsFG202(job),initial=StartingLevelsFG202(job);
  if(s.Ids==null)s.Ids=initialIds;if(s.Levels==null)s.Levels=initial;
  if(s.Ids.Length<initialIds.Length||s.Ids.Length>80||s.Levels.Length!=s.Ids.Length||s.Slots.Length!=s.Ids.Length||s.SpentSp>10000)throw new InvalidDataException("Skill level store bounds");
  bool[] used=new bool[255],idsUsed=new bool[255];int ledger=0;
  for(int i=0;i<s.Slots.Length;i++){byte id=s.Ids[i];int slot=s.Slots[i];if(id==255||idsUsed[id]||slot==255||used[slot])throw new InvalidDataException("Duplicate or invalid skill id/slot");idsUsed[id]=true;used[slot]=true;
   if(s.ProfessionFG139>4)throw new InvalidDataException("Profession baseline range");if(i<initialIds.Length&&id!=initialIds[i])throw new InvalidDataException("Initial skill ids changed");int baseline=BaseLevelFG139(s,id);Offline70SkillRuleFG r=Rule(id);
   if(s.Levels[i]<baseline||s.Levels[i]==0&&id!=173||r==null&&s.Levels[i]!=baseline||r!=null&&s.Levels[i]>Math.Max(baseline,r.Maximum))throw new InvalidDataException("Learned skill level bounds");
   if(i>=initialIds.Length&&r==null)throw new InvalidDataException("Unverified added skill");if(r!=null)ledger+=(s.Levels[i]-baseline)*r.Cost;
   if(Active(id)){if(!ActiveSlot(slot))throw new InvalidDataException("Active skill outside verified list/quick slots");}
   // The initial block is pinned by the per-job creation table; only skills learned after creation
   // go through the captured list/passive placement rules.
   else if(i<initialIds.Length?slot!=Offline70RoleProtocol.StartingSkillSlot(job,id):!PassiveSlotFG123(s,id,slot))throw new InvalidDataException("Passive skill placement changed");}
  if(s.SpentSp!=ledger)throw new InvalidDataException("Skill cost ledger mismatch");
 }
 internal static Offline70SkillSlotState Load(string path){
  if(!File.Exists(path))return Default();if(new FileInfo(path).Length>4096)throw new InvalidDataException("Skill slot store bounds");
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;Offline70SkillSlotState s=(Offline70SkillSlotState)Serializer.Deserialize(r);Validate(s);return s;}
 }
 // FG147: level-zero173 is absent from the client notification but retained in the CHR baseline.
 // Passive skills may exchange positions within their original tab, never enter quick slots.
 internal static bool MoveSlotFG147(Offline70SkillSlotState s,int i,byte target){return Active(s.Ids[i])||ListSlotFG123(s.Ids[i],s.Slots[i])&&ListSlotFG123(s.Ids[i],target);}
 internal static Offline70SkillSlotState Swap(string path,byte source,byte target){
  if(source==target||!ActiveSlot(source)||!ActiveSlot(target))throw new InvalidDataException("Only active skill list/quick slot swaps supported");
  string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));
  using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   Offline70SkillSlotState s=Load(full);bool found=false;for(int i=0;i<s.Slots.Length;i++){if(s.Slots[i]==source||s.Slots[i]==target){byte next=s.Slots[i]==source?target:source;if(!MoveSlotFG147(s,i,next))throw new InvalidDataException("Passive shortcut rejected");s.Slots[i]=next;found=true;}}
   if(!found)throw new InvalidDataException("No learned active skill in swap");Validate(s);
   string temp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+Offline70StoreBackupFG354.Suffix;
   try{using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}Validate(Load(temp));if(File.Exists(full))File.Replace(temp,full,backup);else File.Move(temp,full);Offline70SkillSlotState saved=Load(full);for(int i=0;i<s.Slots.Length;i++)if(s.Slots[i]!=saved.Slots[i])throw new InvalidDataException("Skill swap commit mismatch");return saved;}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
}

