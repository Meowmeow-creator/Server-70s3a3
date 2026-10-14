using System;using System.IO;using System.IO.Compression;using System.Text;using System.Collections.Generic;
// Only the verified 70 offline roster and ghost-knight creation profile; no town/game initialization.
partial class Offline70RoleProtocol {
 internal static readonly Encoding NameEncoding=Encoding.GetEncoding(936,new EncoderExceptionFallback(),new DecoderExceptionFallback());
 internal static uint Big(byte[] data,int at){return ((uint)data[at]<<24)|((uint)data[at+1]<<16)|((uint)data[at+2]<<8)|data[at+3];}
 internal static void BigWrite(byte[] data,int at,uint value){data[at]=(byte)(value>>24);data[at+1]=(byte)(value>>16);data[at+2]=(byte)(value>>8);data[at+3]=(byte)value;}
 internal static byte[] Xtea(byte[] data,byte[] allKeys,bool encrypt){
  if(allKeys.Length!=334||data.Length==0||data.Length%8!=0||data.Length>4096)throw new InvalidDataException("XTEA bounds");
  uint[] key=new uint[4];for(int j=0;j<4;j++)key[j]=Big(allKeys,172+j*4);byte[] result=new byte[data.Length];
  unchecked{for(int at=0;at<data.Length;at+=8){uint left=Big(data,at),right=Big(data,at+4),sum=encrypt?0:0xc6ef3720u;
   for(int round=0;round<32;round++){if(encrypt){left+=(((right<<4)^(right>>5))+right)^(sum+key[sum&3]);sum+=0x9e3779b9u;right+=(((left<<4)^(left>>5))+left)^(sum+key[(sum>>11)&3]);}
    else{right-=(((left<<4)^(left>>5))+left)^(sum+key[(sum>>11)&3]);sum-=0x9e3779b9u;left-=(((right<<4)^(right>>5))+right)^(sum+key[sum&3]);}}
   BigWrite(result,at,left);BigWrite(result,at+4,right);
  }}return result;
 }
 sealed class Blowfish {
  uint[] table=(uint[])BlowfishInitial.Clone();
  uint F(uint word){unchecked{return ((table[18+(word>>24)]+table[274+((word>>16)&255)])^table[530+((word>>8)&255)])+table[786+(word&255)];}}
  void Encrypt(ref uint left,ref uint right){unchecked{for(int round=0;round<16;round++){left^=table[round];right^=F(left);uint swap=left;left=right;right=swap;}uint last=left;left=right;right=last;right^=table[16];left^=table[17];}}
  void Decrypt(ref uint left,ref uint right){unchecked{for(int round=17;round>1;round--){left^=table[round];right^=F(left);uint swap=left;left=right;right=swap;}uint last=left;left=right;right=last;right^=table[1];left^=table[0];}}
  internal Blowfish(byte[] keys){
   if(keys.Length!=334)throw new InvalidDataException("Blowfish key bounds");int index=24;
   for(int word=0;word<18;word++){uint value=0;for(int b=0;b<4;b++){value=(value<<8)|keys[index++];if(index==80)index=24;}table[word]^=value;}
   uint left=0,right=0;for(int word=0;word<table.Length;word+=2){Encrypt(ref left,ref right);table[word]=left;table[word+1]=right;}
  }
  internal byte[] Encode(byte[] plain){if(plain.Length==0||plain.Length%8!=0)throw new InvalidDataException("Blowfish block bounds");byte[] result=new byte[plain.Length];for(int at=0;at<plain.Length;at+=8){uint left=Big(plain,at),right=Big(plain,at+4);Encrypt(ref left,ref right);BigWrite(result,at,left);BigWrite(result,at+4,right);}return result;}
  internal byte[] Decode(byte[] encrypted){if(encrypted.Length==0||encrypted.Length%8!=0)throw new InvalidDataException("Blowfish block bounds");byte[] result=new byte[encrypted.Length];for(int at=0;at<encrypted.Length;at+=8){uint left=Big(encrypted,at),right=Big(encrypted,at+4);Decrypt(ref left,ref right);BigWrite(result,at,left);BigWrite(result,at+4,right);}return result;}
 }
 internal static byte[] Pad(byte[] data){byte[] result=new byte[(data.Length+7)&~7];data.CopyTo(result,0);return result;}
 internal static byte[] Zlib(byte[] plain){
  byte[] deflated;using(MemoryStream m=new MemoryStream()){using(DeflateStream writer=new DeflateStream(m,CompressionMode.Compress,true))writer.Write(plain,0,plain.Length);deflated=m.ToArray();}
  byte[] result=new byte[deflated.Length+6];result[0]=0x78;result[1]=0x9c;deflated.CopyTo(result,2);
  uint first=1,second=0;foreach(byte value in plain){first=(first+value)%65521;second=(second+first)%65521;}BigWrite(result,result.Length-4,(second<<16)|first);return result;
 }
 internal static byte[] Packet(byte type,ushort opcode,byte[] encrypted){byte[] packet=new byte[15+encrypted.Length];packet[0]=type;BitConverter.GetBytes(opcode).CopyTo(packet,1);BitConverter.GetBytes(packet.Length).CopyTo(packet,3);encrypted.CopyTo(packet,15);ProbeLoginCandidate.ReceiverGate(packet,0);return packet;}
 internal static byte[] CreateReply(byte[] keys,bool success,byte error){return Packet(1,5,Xtea(Pad(success?new byte[]{1}:new byte[]{0,error}),keys,true));}
 internal static byte[] RosterBody(List<Offline70Role> roles){
  if(roles.Count>12)throw new InvalidDataException("Roster limit");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   // Subtype 2 then current/previous available slots. A zero previous value
   // replays all unlock animations on every ordinary roster refresh.
   w.Write((byte)2);w.Write((byte)12);w.Write((byte)12);w.Write((ushort)roles.Count);
   foreach(Offline70Role role in roles){byte[] name=NameEncoding.GetBytes(role.Name);if(role.Id==0||role.Job!=0||!Offline70MonsterExperience.SupportedLevel(role.Level)||name.Length==0||name.Length>29)throw new InvalidDataException("Unverified role profile");
    // Native 7FD4B0 stores these in the two name-status vectors; they are not channel IDs.
    w.Write(role.Id);w.Write((uint)name.Length);w.Write(name);w.Write((byte)0);w.Write((byte)0);
    w.Write(role.Job);w.Write((byte)0);w.Write(role.Level);w.Write((byte)0);
    w.Write(new byte[3]);w.Write(new byte[8]);w.Write((byte)0);w.Write((uint)0);w.Write(new byte[3]);w.Write((byte)3);w.Write(new byte[2]);
   }return m.ToArray();
  }
 }
 internal static byte[] RosterReply(byte[] keys,List<Offline70Role> roles){byte[] plain=RosterBody(roles);if(plain.Length>1024)throw new InvalidDataException("Roster body bounds");return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(plain))));}
 internal static Offline70Role ParseCreate(byte[] encrypted,byte[] keys){
  byte[] plain=Xtea(encrypted,keys,false);if(plain.Length<5)throw new InvalidDataException("Create request length");uint length=BitConverter.ToUInt32(plain,1);
  if(plain[0]!=0||length<1||length>29||5+length>plain.Length||plain.Length-(5+length)>7)throw new InvalidDataException("Only ghost-knight job 0 is verified in this test");
  for(int index=5+(int)length;index<plain.Length;index++)if(plain[index]!=0)throw new InvalidDataException("Unexpected create tail");
  string name=NameEncoding.GetString(plain,5,(int)length);if(name.Length==0)throw new InvalidDataException("Empty name");foreach(char character in name)if(!Char.IsLetterOrDigit(character)&&character!='_')throw new InvalidDataException("Unsupported name characters");
  return new Offline70Role{Job=0,Level=1,Name=name,CreatedUtc=DateTime.UtcNow.ToString("o")};
 }
}
public class Offline70Role {public ushort Id;public string Name;public byte Job;public byte Level;public string CreatedUtc;}
public class Offline70RoleStore {public int Format=1;public int AccountId=1;public List<Offline70Role> Roles=new List<Offline70Role>();}
