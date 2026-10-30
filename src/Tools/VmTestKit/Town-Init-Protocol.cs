using System;using System.IO;
// Fixed one-role offline diagnostic profile. Encrypted fixtures come from original native routines.
// This is deliberately not a general cipher implementation or a full town server.
class Offline70TownProtocol {
 internal static byte[] Hex(string text){byte[] b=new byte[text.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(text.Substring(i*2,2),16);return b;}
 internal static void Validate(Offline70Role role,byte[] keys){
  // op3's enter list now carries the playing role's unique_id; op23/op24 carry state, town, area
  // and coordinates. The profile check below is the real dependency. The fixed-key requirement
  // stays: the encrypted fixtures were captured against this channel key material.
  if(role==null||role.Id==0||!Offline70RoleCreateJobsFG202.CreatableFG202(role.Job)||!Offline70MonsterExperience.SupportedLevel(role.Level)||keys==null||keys.Length!=334)throw new InvalidDataException("Unverified town profile id="+(role==null?0:role.Id)+" job="+(role==null?0:role.Job)+" level="+(role==null?0:role.Level));
  for(int i=0;i<keys.Length;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Town fixtures require fixed diagnostic channel keys");
 }
 internal static byte[] Body(ushort opcode){return Body(opcode,1);}
 // op3 is the same enter list the dungeon entry uses: [count][u16 unique_id][state]. The captured
 // yoyo form is 01 01 00 00 (state 0 = town), and Offline70KeySettings.Crypt reproduces its old
 // ciphertext 6e562e... byte for byte. Sending a literal 1 there only ever named role 1; op23/op24
 // already carry the playing id, so make op3 agree instead of relying on them to overwrite it.
 internal static byte[] Body(ushort opcode,ushort uniqueId){
  if(opcode==3)return new byte[]{1,(byte)uniqueId,(byte)(uniqueId>>8),0};
  if(opcode==23)return Hex("01000101da01ea000503");
  if(opcode==24)return Hex("010101000100da01ea000503");
  throw new InvalidDataException("Unknown town fixture opcode");
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys){
  Validate(role,keys);byte[] encrypted;
  // F2A900 skips decompression for these three opcodes: encrypt padded raw fields.
  if(opcode==3){byte[] padded=new byte[16];Body(3,role.Id).CopyTo(padded,0);encrypted=Offline70KeySettings.Crypt(padded,keys,true);}
  else if(opcode==23)encrypted=Hex("2470785f835107fa7a63cc584514af57");
  else if(opcode==24)encrypted=Hex("10030112d751b8505bfbe2a0577617ec");
  else throw new InvalidDataException("Unknown town fixture opcode");
  return Offline70RoleProtocol.Packet(0,opcode,encrypted);
 }
}
