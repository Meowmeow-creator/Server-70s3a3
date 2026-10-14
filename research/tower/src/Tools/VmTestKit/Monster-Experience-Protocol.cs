using System;using System.IO;using System.Xml;using System.Xml.Serialization;
// Bounded Lv1-to-Lv2 yoyo progression. Role level is derived from one atomic EXP store.
public class Offline70ExperienceState {public int Format=1;public ushort RoleId=1;public uint TotalExperience;}
class Offline70MonsterExperience {
 internal const uint NextLevelExperience=1000; // Current70 character/exptable.tbl first value.
 internal const uint LevelThreeExperience=2653; // Current70 cumulative second value; Lv3 held until adapted.
 internal static bool SupportedLevel(byte level){return level==1||level==2;}
 internal static byte Level(uint total){if(total>=LevelThreeExperience)throw new InvalidDataException("Only Lv1-to-Lv2 progression supported");return (byte)(total>=NextLevelExperience?2:1);}
 // Lv1 reward reference30, current PVF monster rate1, solo rate1,
 // normal difficulty0.93 and Lorien experience weight1.2; floor(33.48).
 internal const uint GoblinExperience=33;
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70ExperienceState));
 static void Validate(Offline70ExperienceState s){if(s==null||s.Format!=1||s.RoleId!=1)throw new InvalidDataException("Experience store profile");Level(s.TotalExperience);}
 internal static void Apply(Offline70RoleStore store,Offline70ExperienceState s){Validate(s);foreach(Offline70Role role in store.Roles)if(role.Id==s.RoleId)role.Level=Level(s.TotalExperience);}
 internal static Offline70ExperienceState Load(string path){
  if(!File.Exists(path))return new Offline70ExperienceState();if(new FileInfo(path).Length>4096)throw new InvalidDataException("Experience store bounds");
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;Offline70ExperienceState s=(Offline70ExperienceState)Serializer.Deserialize(r);Validate(s);return s;}
 }
 internal static Offline70ExperienceState Award(string path,ushort uid){
  if(uid<100||uid>103)throw new InvalidDataException("Only Lorien first-room goblin experience supported");
  string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));
  using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   Offline70ExperienceState s=Load(full);s.TotalExperience=checked(s.TotalExperience+GoblinExperience);Validate(s);
   string temp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+".backup-"+Guid.NewGuid().ToString("N");
   try{using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}
    if(Load(temp).TotalExperience!=s.TotalExperience)throw new InvalidDataException("Experience read-back mismatch");
    if(File.Exists(full))File.Replace(temp,full,backup);else File.Move(temp,full);
    Offline70ExperienceState committed=Load(full);if(committed.TotalExperience!=s.TotalExperience)throw new InvalidDataException("Experience commit mismatch");return committed;
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static byte[] Body(uint total){Validate(new Offline70ExperienceState{TotalExperience=total});byte[] b=new byte[38];b[0]=Level(total);BitConverter.GetBytes(total).CopyTo(b,1);return b;}
 internal static byte[] Reply(Offline70Role role,byte[] keys,uint total){Offline70TownProtocol.Validate(role,keys);if(role.Level!=Level(total))throw new InvalidDataException("Experience and role level mismatch");return Offline70RoleProtocol.Packet(0,37,Offline70AreaCipher.Crypt(Offline70RoleProtocol.Pad(Offline70RoleProtocol.WithDungeonIntegrity(Body(total))),keys,9,true));}
}
