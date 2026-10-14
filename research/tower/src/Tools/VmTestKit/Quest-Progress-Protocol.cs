using System;using System.IO;
class Offline70QuestProgress {
 internal static byte[] Crypt(byte[] data,byte[] keys,bool encrypt){
  if(keys.Length!=334||data.Length<8||data.Length>4096||data.Length%8!=0)throw new InvalidDataException("Progress cipher bounds");
  uint[] key=new uint[4];for(int j=0;j<4;j++)key[j]=BitConverter.ToUInt32(keys,204+j*4);byte[] result=new byte[data.Length];
  unchecked{for(int at=0;at<data.Length;at+=8){uint left=BitConverter.ToUInt32(data,at),right=BitConverter.ToUInt32(data,at+4),sum=encrypt?0:0xc6ef3720u;
   for(int r=0;r<32;r++){if(encrypt){left+=(((right<<4)^(right>>5))+right)^(sum+key[sum&3]);sum+=0x9e3779b9u;right+=(((left<<4)^(left>>5))+left)^(sum+key[(sum>>11)&3]);}
    else{right-=(((left<<4)^(left>>5))+left)^(sum+key[(sum>>11)&3]);sum-=0x9e3779b9u;left-=(((right<<4)^(right>>5))+right)^(sum+key[sum&3]);}}
   BitConverter.GetBytes(left).CopyTo(result,at);BitConverter.GetBytes(right).CopyTo(result,at+4);
  }}return result;
 }
 internal static ushort Parse(byte[] encrypted,byte[] keys){byte[] b=Crypt(encrypted,keys,false);if(b.Length!=8||BitConverter.ToUInt16(b,0)!=35||b[4]!=0)throw new InvalidDataException("Meet NPC request profile");for(int i=5;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Progress padding");return BitConverter.ToUInt16(b,2);}
 internal static byte[] Body(bool success){return success?new byte[]{1,101,0,0,0,0,0}:new byte[]{0,0x16};}
 internal static byte[] Reply(byte[] keys,bool success){byte[] body=Body(success),padded=new byte[8];body.CopyTo(padded,0);return Offline70RoleProtocol.Packet(1,35,Crypt(padded,keys,true));}
}
