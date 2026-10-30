using System;using System.IO;using System.Collections.Generic;using System.Globalization;
// Algorithm-4 round translation with a native-exported schedule for the fixed diagnostic keys only.
partial class Offline70SelectCipher {
 static uint Ror(uint x){return (x>>1)|(x<<31);}static uint Rol(uint x){return (x<<1)|(x>>31);}
 static uint G0(uint x){return FixedSchedule[x&255]^FixedSchedule[256+((x>>8)&255)]^FixedSchedule[512+((x>>16)&255)]^FixedSchedule[768+(x>>24)];}
 static uint G1(uint x){return FixedSchedule[(x>>24)]^FixedSchedule[256+(x&255)]^FixedSchedule[512+((x>>8)&255)]^FixedSchedule[768+((x>>16)&255)];}
 static void Check(byte[] data,byte[] keys){if(keys.Length!=334||data.Length==0||data.Length%16!=0||data.Length>4096)throw new InvalidDataException("Select cipher bounds");for(int i=0;i<334;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Select candidate supports only the diagnostic key fixture");}
 internal static byte[] Crypt(byte[] data,byte[] keys,bool encrypt){Check(data,keys);byte[] result=new byte[data.Length];unchecked{for(int at=0;at<data.Length;at+=16){uint a,b,c,d;
  if(encrypt){a=BitConverter.ToUInt32(data,at)^FixedSchedule[1024];b=BitConverter.ToUInt32(data,at+4)^FixedSchedule[1025];c=BitConverter.ToUInt32(data,at+8)^FixedSchedule[1026];d=BitConverter.ToUInt32(data,at+12)^FixedSchedule[1027];
   for(int r=0;r<8;r++){int k=1032+4*r;uint t0=G0(a),t1=G1(b);c=Ror(c^(t0+t1+FixedSchedule[k]));d=Rol(d)^(t0+2*t1+FixedSchedule[k+1]);t0=G0(c);t1=G1(d);a=Ror(a^(t0+t1+FixedSchedule[k+2]));b=Rol(b)^(t0+2*t1+FixedSchedule[k+3]);}
   BitConverter.GetBytes(c^FixedSchedule[1028]).CopyTo(result,at);BitConverter.GetBytes(d^FixedSchedule[1029]).CopyTo(result,at+4);BitConverter.GetBytes(a^FixedSchedule[1030]).CopyTo(result,at+8);BitConverter.GetBytes(b^FixedSchedule[1031]).CopyTo(result,at+12);
  }else{c=BitConverter.ToUInt32(data,at)^FixedSchedule[1028];d=BitConverter.ToUInt32(data,at+4)^FixedSchedule[1029];a=BitConverter.ToUInt32(data,at+8)^FixedSchedule[1030];b=BitConverter.ToUInt32(data,at+12)^FixedSchedule[1031];
   for(int r=7;r>=0;r--){int k=1032+4*r;uint t0=G0(c),t1=G1(d);b=Ror(b^(t0+2*t1+FixedSchedule[k+3]));a=Rol(a)^(t0+t1+FixedSchedule[k+2]);t0=G0(a);t1=G1(b);d=Ror(d^(t0+2*t1+FixedSchedule[k+1]));c=Rol(c)^(t0+t1+FixedSchedule[k]);}
   BitConverter.GetBytes(a^FixedSchedule[1024]).CopyTo(result,at);BitConverter.GetBytes(b^FixedSchedule[1025]).CopyTo(result,at+4);BitConverter.GetBytes(c^FixedSchedule[1026]).CopyTo(result,at+8);BitConverter.GetBytes(d^FixedSchedule[1027]).CopyTo(result,at+12);
  }
 }}return result;}
 internal static int ParseSlot(byte[] encrypted,byte[] keys,int roleCount){byte[] plain=Crypt(encrypted,keys,false);if(plain.Length!=16||plain[0]>=roleCount)throw new InvalidDataException("Select slot bounds");for(int i=1;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Select padding");return plain[0];}
 internal static byte[] Body(Offline70Role role){return Body(role,false);}
 internal static byte[] Body(Offline70Role role,bool completedTutorial){return Body(role,completedTutorial,false);}
 internal static byte[] Body(Offline70Role role,bool completedTutorial,bool acceptedQuest){
  return Body(role,completedTutorial,acceptedQuest,1);
 }
 // The probe fixtures were captured against the town-1 diagnostic profile.
 internal static byte[] Body(Offline70Role role,bool completedTutorial,bool acceptedQuest,uint progress){return Body(role,completedTutorial,acceptedQuest,progress,1);}
 internal static byte[] Body(Offline70Role role,bool completedTutorial,bool acceptedQuest,uint progress,int town){
  if(progress>1)throw new InvalidDataException("Quest progress bounds");
  if(!Offline70TownContentFG75.TownSupported(town))throw new InvalidDataException("Select ACK town "+town);
  // This body carries id, creation time, fatigue and the twenty quest slots - no job byte and no
  // name - so the profile below is the whole dependency on the role: any creatable job can be
  // selected. The old job-0 pin was a verification stamp, not a data requirement.
  if(role.Id==0||!Offline70RoleCreateJobsFG202.CreatableFG202(role.Job)||!Offline70MonsterExperience.SupportedLevel(role.Level))throw new InvalidDataException("Unverified selected role id="+role.Id+" job="+role.Job+" level="+role.Level);
  DateTime created=DateTime.Parse(role.CreatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToUniversalTime();double seconds=(created-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;if(seconds<0||seconds>UInt32.MaxValue)throw new InvalidDataException("Created time bounds");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)1);w.Write((uint)0);w.Write((uint)seconds);w.Write(role.Id);w.Write((ushort)0);w.Write((ushort)156);w.Write((ushort)0);w.Write((byte)0);w.Write((uint)0);
   for(int i=0;i<20;i++){w.Write((ushort)(acceptedQuest&&i==0?101:0xffff));w.Write((uint)(acceptedQuest&&i==0?progress:0));}
   // The byte after the twenty quest slots is the village the client binds before it paints the
   // town (rednf Inter_LoadEtc::SendResult writes getCurCharacVill() here). A pinned 1 makes the
   // client bind Elvengard and then switch to the saved town, which plays the town intro
   // illustration the moment the character enters the Seria room.
   w.Write((byte)town);
   // EE43A0 reads tutorial state and completed-module count before the final two u16 values.
   w.Write((byte)(completedTutorial?1:0));w.Write((byte)(completedTutorial?1:0));if(completedTutorial)w.Write((byte)31);w.Write((ushort)0);w.Write((ushort)0);return m.ToArray();}
 }
 internal static byte[] Reply(Offline70Role role,byte[] keys){return Reply(role,keys,false);}
 internal static byte[] Reply(Offline70Role role,byte[] keys,bool completedTutorial){return Reply(role,keys,completedTutorial,false);}
 internal static byte[] Reply(Offline70Role role,byte[] keys,bool completedTutorial,bool acceptedQuest){return Reply(role,keys,completedTutorial,acceptedQuest,1);}
 internal static byte[] Reply(Offline70Role role,byte[] keys,bool completedTutorial,bool acceptedQuest,uint progress){byte[] body=Body(role,completedTutorial,acceptedQuest,progress),padded=new byte[(body.Length+15)&~15];body.CopyTo(padded,0);return Offline70RoleProtocol.Packet(1,4,Crypt(padded,keys,true));}
}
