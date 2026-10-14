using System;using System.IO;
// Native 47D2E0 reads one u32. This is a zero-value diagnostic candidate, not world support.
partial class Offline70RoleProtocol {
 internal static byte[] LoadingCompleteBody(){return new byte[4];}
 internal static byte[] LoadingCompleteReply(Offline70Role role,byte[] keys){
  if(role==null||role.Id!=1||role.Name!="yoyo"||role.Job!=0||!Offline70MonsterExperience.SupportedLevel(role.Level)||keys==null||keys.Length!=334)throw new InvalidDataException("Unverified loading-complete profile");
  for(int i=0;i<keys.Length;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Expected fixed offline channel keys");
  // Opcode 30 selects index 2; F2A900 skips decompression for this opcode.
  return Packet(0,30,new Blowfish(keys).Encode(Pad(LoadingCompleteBody())));
 }
}
