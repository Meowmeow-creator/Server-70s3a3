using System;using System.IO;using System.Xml;using System.Collections.Generic;
partial class Offline70SkillSlots {
 // The client puts a learned skill on whichever tab its slot number falls in, so picking the slot
 // is picking the tab. Skill ids are numbered per job (reference find_skill(job, index)), which is
 // why the swordman capture below cannot answer for the other eight creatable jobs: 男格斗's
 // 铁山靠/霸体护甲 (job 7 id 1/20) reused swordman's 普通 rows and landed on 普通 instead of 转职.
 // Swordman keeps the capture because it is the only tab evidence measured off a live client;
 // TabBaseFG273 is the original rule and answers for every job, including the ids the capture
 // never covered (they used to throw, which is why a non-swordman awakening could not be granted).
 static byte CapturedSwordmanStartFG123(byte id){switch(id){
  case 56:case 61:case 93:case 94:case 172:case 187:return 102;
  case 29:case 33:case 35:case 37:case 55:case 197:return 54;
  // First-awakening passives 89..92 sit in the same tab as the branch passives above. Evidence:
  // the original allocates a granted skill's slot from CSkill::get_group() (rednf
  // CListTables/init.inc:105-107 reform_ui_group_no([skill class], special,
  // [skill fitness growtype] row count) + SkillSlot/addSkillOnCreateCharacter.inc:57-79, whose
  // group ranges are g0 [6,54) / g1 [54,102) / g2 [102,150) / g3 [150,198) - exactly these four
  // tab bases). PVF measured (local-run/pvf/awakening_pvf_truth.py -> awakening-pvf-truth.tsv):
  // 89 class 3, 90 class 2, 91 class 1, 92 class 0, every one with an EMPTY [skill fitness
  // growtype] row list (0 rows <= 2 => group 1) and [type] [passive], so all four land in 54..101.
  // The same rule reproduces 119 of the 121 ids this switch groups; 43 jumpattackhighfast and
  // 197 conversion read 6 where the client was captured on 转职, so the capture wins for them.
  case 89:case 90:case 91:case 92:return 54;
  case 1:case 5:case 8:case 11:case 12:case 13:case 14:case 15:case 16:case 17:case 20:case 25:case 26:case 28:case 45:case 46:case 48:case 58:case 59:case 64:case 65:case 77:return 6;
  case 2:case 3:case 4:case 6:case 7:case 9:case 10:case 18:case 19:case 21:case 22:case 23:case 24:case 27:case 30:case 31:case 32:case 34:case 36:case 38:case 39:case 40:case 41:case 44:case 47:case 49:case 50:case 51:case 52:case 53:case 54:case 57:case 60:case 62:case 63:case 66:case 67:case 68:case 69:case 70:case 71:case 72:case 73:case 74:case 75:case 76:case 78:case 79:case 80:case 81:case 82:case 83:case 84:case 103:case 104:case 105:case 106:case 107:case 108:case 109:case 110:case 111:case 112:case 113:case 114:return 54;
  case 169:case 170:case 171:case 173:case 174:case 175:case 176:case 177:case 178:case 179:case 180:case 181:case 182:case 184:case 186:case 188:case 189:case 190:case 191:case 192:case 193:case 194:return 102;
  case 86:case 43:case 99:return 54;
  default:return 0;}}
 // CSkill/reform_ui_group_no.inc, read through the per-job PVF rows Skill-Rules-Jobs-FG203.cs now
 // carries: [feature skill type] -> 特性; [skill class] 4 -> 通用; class 0..3 -> 普通 when
 // [skill fitness growtype] names more than two grow types (the whole 大系 shares it) else 转职.
 // Verified 2026-10-26 by local-run/pvf/skill_tab_plan2.py: it reproduces all 71 tab-bearing rows
 // of the nine captured creation packets and 119 of the 121 swordman rows above.
 internal static byte TabBaseFG273(byte id){
  Offline70SkillRuleFG r=Rule(id);
  if(r==null)throw new InvalidDataException("Skill placement outside the job's PVF skill list");
  if(r.Trait)return 150;if(r.SkillClass==4)return 102;return r.Fitness>2?(byte)6:(byte)54;}
 internal static byte ListStartFG123(byte id){
  if(Offline70RoleScopeFG202.CurrentJob==0){byte captured=CapturedSwordmanStartFG123(id);if(captured!=0)return captured;}
  return TabBaseFG273(id);}
 internal static bool ListSlotFG123(byte id,int slot){int first=ListStartFG123(id);return slot>=first&&slot<first+48;}
 internal static bool PassiveSlotFG123(Offline70SkillSlotState s,byte id,int slot){return ListSlotFG123(id,slot)||slot>=100&&slot<150;}
 internal static byte FindListFG123(Offline70SkillSlotState s,byte id){int first=ListStartFG123(id);for(int slot=first;slot<first+48;slot++)if(Array.IndexOf(s.Slots,(byte)slot)<0)return (byte)slot;throw new InvalidDataException("Original skill tab is full");}
 // Load() asserts the tab, so a skill still parked where the old swordman-only table put it would
 // be rejected before this method could re-home it. Read the same archive without that assertion;
 // every other Validate rule still applies here and is re-checked before the file is replaced.
 static Offline70SkillSlotState ReadUnplacedFG273(string full){
  if(!File.Exists(full))return Default();
  if(new FileInfo(full).Length>4096)throw new InvalidDataException("Skill slot store bounds");
  using(FileStream f=File.OpenRead(full))using(XmlTextReader r=new XmlTextReader(f)){
   r.ProhibitDtd=true;r.XmlResolver=null;Offline70SkillSlotState s=(Offline70SkillSlotState)Serializer.Deserialize(r);
   byte job=Offline70RoleScopeFG202.CurrentJob;byte[] initial=StartingIdsFG202(job);
   if(s.Ids==null)s.Ids=initial;if(s.Levels==null)s.Levels=StartingLevelsFG202(job);
   if(s.Slots==null||s.Ids.Length<initial.Length||s.Ids.Length>80||s.Levels.Length!=s.Ids.Length||s.Slots.Length!=s.Ids.Length)throw new InvalidDataException("Skill level store bounds");
   return s;}}
 internal static Offline70SkillSlotState NormalizePlacementFG123(string path){string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=ReadUnplacedFG273(full);List<int> move=new List<int>();for(int i=0;i<s.Ids.Length;i++)if(!Quick(s.Slots[i])&&!ListSlotFG123(s.Ids[i],s.Slots[i])){if(i<StartingIdsFG202(Offline70RoleScopeFG202.CurrentJob).Length&&!Active(s.Ids[i]))throw new InvalidDataException("Initial passive slot changed");move.Add(i);s.Slots[i]=255;}if(move.Count==0){Validate(s);return s;}foreach(int i in move)s.Slots[i]=FindListFG123(s,s.Ids[i]);Validate(s);string temp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+Offline70StoreBackupFG354.Suffix;try{using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}Validate(Load(temp));if(File.Exists(full))File.Replace(temp,full,backup);else File.Move(temp,full);return Load(full);}finally{if(File.Exists(temp))File.Delete(temp);}}}
}
