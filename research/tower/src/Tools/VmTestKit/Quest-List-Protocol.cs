using System;using System.IO;
// Native-verified notify21 for the current fresh Lv1 role. It publishes offers, not acceptance or rewards.
class Offline70QuestProtocol {
 internal static byte[] Body(Offline70Role role){
  if(role.Id!=1||role.Job!=0||!Offline70MonsterExperience.SupportedLevel(role.Level)||role.Name!="yoyo")throw new InvalidDataException("Unverified quest role profile");
  // Current 70 PVF: quest101 铁匠林纳斯, all jobs, level1, no prerequisite.
  // Native 491820 reads u8 level, u16 count, u16 quest IDs.
  return new byte[]{role.Level,1,0,101,0};
 }
 internal static byte[] Reply(Offline70Role role,byte[] keys){
  Body(role);if(keys.Length!=334)throw new InvalidDataException("Quest keys bounds");
  for(int i=0;i<keys.Length;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Only fixed offline quest keys verified");
  // Native algorithm7: little-endian XTEA, key bytes204..219, raw body with zero block padding.
  byte[] padded=new byte[8];Body(role).CopyTo(padded,0);byte[] encrypted=Offline70QuestProgress.Crypt(padded,keys,true);
  return Offline70RoleProtocol.Packet(0,21,encrypted);
 }
 internal static byte[] Body(Offline70Role role,bool completed){Body(role);return completed?new byte[]{role.Level,0,0}:Body(role);}
 internal static byte[] Reply(Offline70Role role,byte[] keys,bool completed){if(!completed)return Reply(role,keys);byte[] b=Body(role,true),pad=new byte[8];b.CopyTo(pad,0);return Offline70RoleProtocol.Packet(0,21,Offline70QuestProgress.Crypt(pad,keys,true));}
}
