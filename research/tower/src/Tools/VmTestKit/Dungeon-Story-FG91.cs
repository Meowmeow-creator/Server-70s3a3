using System;using System.IO;
// CN70 sends one event-index byte in command206. Native notify182 at4677B0
// reads serverId(u16), eventIndex(u8), then resumes/suspends the scene.
// TW170 carries a different record and must not be copied into CN70.
class Offline70DungeonStoryFG91 {
 internal static byte Parse(byte[] body,byte[] keys){
  if(body==null||body.Length!=8)throw new InvalidDataException("Story206 request block");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,206%14,false);
  if(plain[0]>1)throw new InvalidDataException("Story206 pause index");
  for(int i=1;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Story206 padding");
  return plain[0];
 }
 internal static byte[] Reply(ushort serverId,byte index,byte[] keys){
  if(serverId!=1||index>1)throw new InvalidDataException("Story182 offline actor");
  return Offline70RoleProtocol.BatchPacketFG(0,182,new byte[]{(byte)serverId,(byte)(serverId>>8),index},keys,false);
 }
}
