using System;using System.IO;
// Two native-verified gate requests with the fixed offline keys. General gates are not supported.
class Offline70AreaProtocol {
 internal static byte[] RequestBody(){return RequestBody(false);}
 internal static byte[] RequestBody(bool returning){return Offline70TownProtocol.Hex(returning?"0101da01500105010000000000000000":"010011039e0005010001000000000000");}
 internal static bool MatchesExit(byte[] encrypted,Offline70Role role,byte[] keys){
  return MatchesGate(encrypted,role,keys,false);
 }
 internal static bool MatchesGate(byte[] encrypted,Offline70Role role,byte[] keys,bool returning){
  Offline70TownProtocol.Validate(role,keys);byte[] expected=Offline70TownProtocol.Hex(returning?"ba97dcd47ff5d81c6ea642ac280cf65c":"9cc67bfffe7bf7f4ef7a07c6fe159cbd");
  if(encrypted==null||encrypted.Length!=expected.Length)return false;
  for(int i=0;i<expected.Length;i++)if(encrypted[i]!=expected[i])return false;return true;
 }
 internal static byte[] Body(ushort opcode){return Body(opcode,false);}
 internal static byte[] Body(ushort opcode,bool returning){
  if(opcode==38)return new byte[]{1};
  if(opcode==23)return Offline70TownProtocol.Hex(returning?"01000101da0150010503":"0100010011039e000503");
  if(opcode==24)return Offline70TownProtocol.Hex(returning?"010101000100da0150010503":"01000100010011039e000503");
  throw new InvalidDataException("Unknown bounded area fixture opcode");
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys){
  return Reply(opcode,role,keys,false);
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys,bool returning){
  Offline70TownProtocol.Validate(role,keys);byte[] encrypted;
  if(opcode==38)encrypted=Offline70TownProtocol.Hex("c3a7307519cedc1f");
  else if(opcode==23)encrypted=Offline70TownProtocol.Hex(returning?"65e187f7a4dd828b7a63cc584514af57":"c3f73293f047929a7a63cc584514af57");
  else if(opcode==24)encrypted=Offline70TownProtocol.Hex(returning?"10030112d751b8503b3c89c01b69e84c":"10a4ab5122991a37d12aa78dce5f237b");
  else throw new InvalidDataException("Unknown bounded area fixture opcode");
  return Offline70RoleProtocol.Packet((byte)(opcode==38?1:0),opcode,encrypted);
 }
}
