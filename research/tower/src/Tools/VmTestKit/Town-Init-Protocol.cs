using System;using System.IO;
// Fixed one-role offline diagnostic profile. Encrypted fixtures come from original native routines.
// This is deliberately not a general cipher implementation or a full town server.
class Offline70TownProtocol {
 internal static byte[] Hex(string text){byte[] b=new byte[text.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(text.Substring(i*2,2),16);return b;}
 internal static void Validate(Offline70Role role,byte[] keys){
  if(role==null||role.Id!=1||role.Name!="yoyo"||role.Job!=0||!Offline70MonsterExperience.SupportedLevel(role.Level)||keys==null||keys.Length!=334)throw new InvalidDataException("Unverified town fixture profile");
  for(int i=0;i<keys.Length;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Town fixtures require fixed diagnostic channel keys");
 }
 internal static byte[] Body(ushort opcode){
  if(opcode==3)return Hex("01010000");
  if(opcode==23)return Hex("01000101da01ea000503");
  if(opcode==24)return Hex("010101000100da01ea000503");
  throw new InvalidDataException("Unknown town fixture opcode");
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys){
  Validate(role,keys);byte[] encrypted;
  // F2A900 skips decompression for these three opcodes: encrypt padded raw fields.
  if(opcode==3)encrypted=Hex("6e562ed16d19cd2fb0e721ad029b218a");
  else if(opcode==23)encrypted=Hex("2470785f835107fa7a63cc584514af57");
  else if(opcode==24)encrypted=Hex("10030112d751b8505bfbe2a0577617ec");
  else throw new InvalidDataException("Unknown town fixture opcode");
  return Offline70RoleProtocol.Packet(0,opcode,encrypted);
 }
}
