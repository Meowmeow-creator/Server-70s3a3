using System;using System.IO;using System.Security.Cryptography;
// First native70 Lorien room only. Separate empty and PVF goblin profiles;
// damage settlement, room progression and loot remain outside this stage.
partial class Offline70RoleProtocol {
 internal static byte[] DungeonEntryBody(ushort opcode){return DungeonEntryBody(opcode,1);}
 // Native CParty::game_start writes the dungeon enter list as [liveCount][u16 unique_id][state]
 // per member (reference/rednf .../CParty/game_start.inc: enterPacket.put_short(
 // get_unique_id() & 0xffff)), and GameWorld::goto_dungeon's 0x17 notify repeats the same id.
 // The client resolves "which actor is me" from that id, so the old literal 1 only ever matched
 // roleId 1: every character created through the client entered a dungeon with no body and could
 // not walk through the cleared room's door (yoyo, id 1, was the only one that worked). Keep the
 // captured CN70 state byte (2) and only make the identity field dynamic.
 internal static byte[] DungeonEntryBody(ushort opcode,ushort uniqueId){
  if(opcode==16||opcode==40)return new byte[]{1};
  if(opcode==3)return new byte[]{1,(byte)uniqueId,(byte)(uniqueId>>8),2};
  if(opcode==28)return WithDungeonIntegrity(Offline70TownProtocol.Hex("010000000002000000000000")); // native u16, then post-notify checksum
  if(opcode==29)return Offline70TownProtocol.Hex("0000000000000000010300000000"); // room(0,0), map3; empty object lists
  if(opcode==30)return new byte[4];
  throw new InvalidDataException("Unverified dungeon-entry opcode");
 }
 // Native 105DCC0 folds MD5 into four bytes; 756EC0 reads this after handler 475CE0.
 // Keep it inside the cipher0 plaintext, separate from the encrypted-body header check.
 internal static byte[] WithDungeonIntegrity(byte[] body){byte[] digest;using(MD5 md5=MD5.Create())digest=md5.ComputeHash(body);byte[] complete=new byte[body.Length+4];body.CopyTo(complete,0);int n=body.Length;
  complete[n]=(byte)(digest[0]^digest[13]^digest[8]^digest[10]^0x81);
  complete[n+1]=(byte)(digest[1]^digest[9]^digest[5]^digest[12]^0x78);
  complete[n+2]=(byte)(digest[2]^digest[6]^digest[14]^digest[4]^0x1a);
  complete[n+3]=(byte)(digest[3]^digest[15]^digest[11]^digest[7]^0xbf);return complete;
 }
 internal static byte[] LorienMonsterRoomBody(){using(MemoryStream stream=new MemoryStream())using(BinaryWriter w=new BinaryWriter(stream)){
  w.Write((byte)0);w.Write((byte)0);w.Write((uint)0);w.Write((byte)0);w.Write((byte)0);w.Write((byte)1);w.Write((ushort)3);w.Write((byte)4);
  // Native70 row: u16 template order, u32 PVF list index, u16 runtime id,
  // u16 monster code, u8 level/type/box flag/box index. s2.map has four Lv1 goblins.
  for(ushort i=0;i<4;i++){w.Write(i);w.Write((uint)i);w.Write((ushort)(100+i));w.Write((ushort)1);w.Write((byte)1);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);}
  w.Write((byte)0);w.Write((byte)0);return stream.ToArray();
 }}
 internal static byte[] MonsterDeathBody(ushort uid){if(uid<100||uid>103)throw new InvalidDataException("Lorien runtime monster bounds");return new byte[]{(byte)uid,0,0,0,255,0};}
 internal static byte[] MonsterDeathReply(ushort uid,Offline70Role role,byte[] keys){Offline70TownProtocol.Validate(role,keys);return Packet(0,38,Offline70AreaCipher.Crypt(Pad(MonsterDeathBody(uid)),keys,10,true));}
 // Verified command42: 64-byte single-attacker body plus native 752930's
 // four-byte MD5 fold. Runtime authority is limited to this local first room.
 internal static ushort ParseMonsterDeathRequest(byte[] encrypted,Offline70Role role,byte[] keys){
  Offline70TownProtocol.Validate(role,keys);if(encrypted.Length!=68)throw new InvalidDataException("Monster death body bounds");
  byte[] plain=(byte[])encrypted.Clone();for(int i=0;i<plain.Length;i++)plain[i]^=keys[4+i%4];
  byte[] body=new byte[64];Buffer.BlockCopy(plain,0,body,0,64);byte[] checkedBody=WithDungeonIntegrity(body);
  for(int i=64;i<68;i++)if(plain[i]!=checkedBody[i])throw new InvalidDataException("Monster death checksum");
  ushort uid=BitConverter.ToUInt16(plain,0);
  if(uid<100||uid>103||BitConverter.ToUInt16(plain,2)!=role.Id)throw new InvalidDataException("Monster death actor bounds");
  if(plain[20]!=1||BitConverter.ToUInt16(plain,21)!=0x111||BitConverter.ToUInt16(plain,23)!=role.Id||BitConverter.ToUInt16(plain,29)==0)throw new InvalidDataException("Monster death single-player attacker profile");
  for(int i=35;i<39;i++)if(plain[i]!=0)throw new InvalidDataException("Monster death room flags");
  if(plain[41]!=0||BitConverter.ToUInt32(plain,42)!=1||BitConverter.ToUInt32(plain,46)!=32)throw new InvalidDataException("Monster death support profile");
  return uid;
 }
 internal static byte[] DungeonEntryReply(ushort opcode,Offline70Role role,byte[] keys){return DungeonEntryReply(opcode,role,keys,false);}
 internal static byte[] DungeonEntryReply(ushort opcode,Offline70Role role,byte[] keys,bool monsters){Offline70TownProtocol.Validate(role,keys);byte[] body=opcode==29&&monsters?LorienMonsterRoomBody():DungeonEntryBody(opcode,role.Id),encrypted;
  if(opcode==40)encrypted=DungeonLoadCrypt(Pad(body),keys,true);
  else if(opcode==16||opcode==30)encrypted=new Blowfish(keys).Encode(Pad(body));
  else if(opcode==3){byte[] padded=new byte[16];body.CopyTo(padded,0);encrypted=Offline70KeySettings.Crypt(padded,keys,true);}
  else if(opcode==28){encrypted=(byte[])body.Clone();for(int i=0;i<encrypted.Length;i++)encrypted[i]^=keys[4+i%4];}
  else if(opcode==29){byte[] padded=new byte[(body.Length+15)/16*16];body.CopyTo(padded,0);encrypted=ProbeLoginCandidate.Crypt(padded,keys,true);}
  else throw new InvalidDataException("Dungeon-entry cipher profile");return Packet(opcode==16||opcode==40?(byte)1:(byte)0,opcode,encrypted);
 }
 internal static bool LorienEntryMatches(byte[] encrypted,Offline70Role role,byte[] keys){Offline70TownProtocol.Validate(role,keys);byte[] expected=new Blowfish(keys).Encode(new byte[]{1,0,0,0,0,0,0,0});if(encrypted.Length!=expected.Length)return false;for(int i=0;i<expected.Length;i++)if(encrypted[i]!=expected[i])return false;return true;}
 internal static byte[] DungeonLoadCrypt(byte[] input,byte[] keys,bool encrypt){
  if(input==null||input.Length!=8||keys==null||keys.Length!=334)throw new InvalidDataException("Dungeon load cipher bounds");for(int i=0;i<keys.Length;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Dungeon load fixed keys required");
  // Native algorithm12 MULTI2 with128 rounds; native setup from fixed key
  // bytes278..317 reproduces the captured32-byte expanded key exactly.
  uint[] k=new uint[]{0x36CB2909u,0x8AB6B43Cu,0x568A10E1u,0xDC3CA4DDu,0x231706E0u,0xC738BCCAu,0xFC0CB21Fu,0x3B340ED5u};
  uint l=Big(input,0),r=Big(input,4);unchecked{
   if(encrypt){r^=l;for(int cycle=0;cycle<32;cycle++){int j=(cycle&1)*4;uint x=r+k[j];uint y=x+LoadRol(x,1)-1;l^=LoadRol(y,4)^y;
    x=l+k[j+1];y=x+LoadRol(x,2)+1;y=(LoadRol(y,8)^y)+k[j+2];x=LoadRol(y,1)-y;r^=LoadRol(x,16)^(l|x);
    x=r+k[j+3];l^=x+LoadRol(x,2)+1;if(cycle<31)r^=l;
   }}else{for(int cycle=0;cycle<32;cycle++){int j=((cycle&1)^1)*4;uint x=r+k[j+3];l^=x+LoadRol(x,2)+1;
    x=l+k[j+1];uint y=x+LoadRol(x,2)+1;y=(LoadRol(y,8)^y)+k[j+2];x=LoadRol(y,1)-y;r^=LoadRol(x,16)^(l|x);
    x=r+k[j];y=x+LoadRol(x,1)-1;l^=LoadRol(y,4)^y;r^=l;
   }}
  }byte[] output=new byte[8];BigWrite(output,0,l);BigWrite(output,4,r);return output;
 }
 static uint LoadRol(uint x,int n){return (x<<n)|(x>>(32-n));}
 internal static byte[] LorienLoadPlain(byte[] encrypted,Offline70Role role,byte[] keys){Offline70TownProtocol.Validate(role,keys);return DungeonLoadCrypt(encrypted,keys,false);}
 internal static bool LorienLoadMatches(byte[] encrypted,Offline70Role role,byte[] keys){Offline70TownProtocol.Validate(role,keys);if(encrypted.Length!=8)return false;byte[] plain=LorienLoadPlain(encrypted,role,keys);return BitConverter.ToUInt32(plain,0)==101;} // second native u32 field varies at runtime
}
