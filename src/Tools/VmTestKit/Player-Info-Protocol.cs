using System;using System.IO;
// Native 7FDF10 minimal appearance profile; empty collections and zero optional fields only.
partial class Offline70RoleProtocol {
 internal static byte[] AppearanceRecord(Offline70Role role){
  if(role.Id!=1||role.Job!=0||!Offline70MonsterExperience.SupportedLevel(role.Level)||role.Name!="yoyo")throw new InvalidDataException("Only saved Lv1/Lv2 yoyo appearance supported");
  byte[] record=new byte[89];BitConverter.GetBytes(role.Id).CopyTo(record,0);
  byte[] name=NameEncoding.GetBytes(role.Name);BitConverter.GetBytes((uint)name.Length).CopyTo(record,2);name.CopyTo(record,6);
  record[10]=role.Job;record[12]=role.Level;
  // Scalar widths, empty strings and zero counts are recorded in appearance-native-name-DC.json.
  return record;
 }
 internal static byte[] AppearanceBody(Offline70Role role){byte[] record=AppearanceRecord(role),plain=new byte[3+record.Length];plain[0]=0;plain[1]=1;record.CopyTo(plain,3);return plain;}
 internal static byte[] AppearanceReply(Offline70Role role,byte[] keys){return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(AppearanceBody(role)))));}
 // Diagnostic base values from this client's swordman.chr, scaled as native 71F330 expects.
 // Zero equipment, skills and optional collections; gameplay is not implemented by this packet.
 internal static byte[] AdditionRecord(Offline70Role role){
  AppearanceRecord(role);byte[] stats;
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   // Current70 swordman.chr initial values plus growtype1 for the first level gained.
   int grown=role.Level-1;w.Write((uint)(1940+590*grown));w.Write((uint)(1550+400*grown));w.Write((short)(96+69*grown));w.Write((short)(95+68*grown));w.Write((short)(66+63*grown));w.Write((short)(65+62*grown));
   w.Write((short)120);w.Write((short)120);w.Write((short)320);w.Write((short)-80);
   for(int i=0;i<17;i++)w.Write((short)0);
   w.Write((uint)(480000+3000*grown));w.Write((short)0);w.Write((short)(500+25*grown));w.Write(Offline70TestRatesFG114.Movement(8700));
   w.Write((ushort)8500);w.Write((ushort)7000);w.Write((short)(6000+15*grown));w.Write((short)4300);w.Write((uint)680000);w.Write((byte)100);
   // Native 71F73E maps float at +53 to actor+D14, scaled by 0.1.
   // ED087A adds the final copy (+26D0) to attack MISS probability.
   // This is a collision adjustment, not swordman.chr's jump speed (950).
   w.Write((float)0);
   stats=m.ToArray();
  }
  if(stats.Length!=87)throw new InvalidDataException("70 stat structure bounds");
  // The zero-list native discovery record is 44 bytes, including id, exp and stat length.
  byte[] record=new byte[131];BitConverter.GetBytes(role.Id).CopyTo(record,0);BitConverter.GetBytes((uint)87).CopyTo(record,6);stats.CopyTo(record,10);return record;
 }
 internal static byte[] AdditionBody(Offline70Role role){byte[] record=AdditionRecord(role),plain=new byte[3+record.Length];plain[0]=1;plain[1]=1;record.CopyTo(plain,3);return plain;}
 internal static byte[] AdditionReply(Offline70Role role,byte[] keys){return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(AdditionBody(role)))));}
 // Restore absolute saved EXP through initial user-info instead of gained-EXP37.
 internal static byte[] AdditionBody(Offline70Role role,uint experience){if(role.Level!=Offline70MonsterExperience.Level(experience))throw new InvalidDataException("Restored role and experience level mismatch");byte[] body=AdditionBody(role);BitConverter.GetBytes(experience).CopyTo(body,5);return body;}
 internal static byte[] AdditionReply(Offline70Role role,byte[] keys,uint experience){return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(AdditionBody(role,experience)))));}
 // Current client's swordman.chr [skill], not the Taiwanese server's attributes.
 // Native 7FCC80 reads active bank, then two byte-counted banks of (byte id, byte level).
 internal static byte[] AdditionRecord(Offline70Role role,bool startingSkills){
  byte[] empty=AdditionRecord(role);if(!startingSkills)return empty;
  byte[] skills=new byte[]{181,2,182,2,184,1,173,1,179,7,5,1,46,1,48,1,169,1,170,1,174,1};
  byte[] record=new byte[empty.Length+skills.Length];Array.Copy(empty,0,record,0,108);
  record[107]=0;record[108]=(byte)(skills.Length/2);skills.CopyTo(record,109);
  record[109+skills.Length]=0; // second bank empty
  Array.Copy(empty,110,record,110+skills.Length,empty.Length-110);return record;
 }
 internal static byte[] AdditionBody(Offline70Role role,uint experience,bool startingSkills){
  if(role.Level!=Offline70MonsterExperience.Level(experience))throw new InvalidDataException("Restored role and experience level mismatch");
  byte[] record=AdditionRecord(role,startingSkills),body=new byte[3+record.Length];body[0]=1;body[1]=1;record.CopyTo(body,3);BitConverter.GetBytes(experience).CopyTo(body,5);return body;
 }
 internal static byte[] AdditionReply(Offline70Role role,byte[] keys,uint experience,bool startingSkills){return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(AdditionBody(role,experience,startingSkills)))));}
}
