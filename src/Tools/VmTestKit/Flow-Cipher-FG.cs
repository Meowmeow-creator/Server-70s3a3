using System;using System.IO;
// Additional dungeon flow wrappers. Native70 cipher6 is KASUMI; 5 and 7 were already verified.
partial class Offline70FlowCipherFG {
 static uint Rotate16(uint x){return ((x<<1)|(x>>15))&65535;}
 static uint FI(uint x,uint key){uint nine=S9[(x>>7)&511]^(x&127),seven=S7[x&127]^(nine&127)^(key>>9);nine=S9[nine^(key&511)]^seven;seven=S7[seven]^(nine&127);return (seven<<9)+nine;}
 static uint FO(uint x,int r){uint a=FI((x>>16)^State6[16+r],State6[40+r])^(x&65535);uint c=FI((x&65535)^State6[24+r],State6[48+r])^a;a=FI(a^State6[32+r],State6[56+r])^c;return (c<<16)+a;}
 static uint FL(uint x,int r){uint high=x>>16,low=Rotate16(State6[r]&high)^(x&65535);return ((Rotate16(State6[8+r]|low)^high)<<16)+low;}
 static uint Rol(uint x,int n){return (x<<n)|(x>>(32-n));}
 static void Theta(uint[] a,uint[] k){uint t=a[0]^a[2];t^=Rol(t,8)^Rol(t,24);a[1]^=t;a[3]^=t;for(int i=0;i<4;i++)a[i]^=k[i];t=a[1]^a[3];t^=Rol(t,8)^Rol(t,24);a[0]^=t;a[2]^=t;}
 static void Nonlinear(uint[] a){a[0]=Rol(a[0],1);a[2]=Rol(a[2],5);a[3]=Rol(a[3],2);a[1]^=~a[3]&~a[2];a[0]^=a[2]&a[1];uint swap=a[0];a[0]=a[3];a[3]=swap;a[2]^=a[0]^a[1]^a[3];a[1]^=~a[3]&~a[2];a[0]^=a[2]&a[1];a[0]=Rol(a[0],31);a[2]=Rol(a[2],27);a[3]=Rol(a[3],30);}
 static byte[] Noekeon(byte[] data,byte[] keys,bool encrypt){uint[] k=new uint[4];for(int i=0;i<4;i++)k[i]=Offline70RoleProtocol.Big(keys,220+4*i);if(!encrypt)Theta(k,new uint[4]);byte[] result=new byte[data.Length],rc=new byte[]{0x80,0x1b,0x36,0x6c,0xd8,0xab,0x4d,0x9a,0x2f,0x5e,0xbc,0x63,0xc6,0x97,0x35,0x6a,0xd4};
  for(int at=0;at<data.Length;at+=16){uint[] a=new uint[4];for(int i=0;i<4;i++)a[i]=Offline70RoleProtocol.Big(data,at+4*i);if(encrypt){for(int r=0;r<16;r++){a[0]^=rc[r];Theta(a,k);Nonlinear(a);}a[0]^=rc[16];Theta(a,k);}else{for(int r=16;r>0;r--){Theta(a,k);a[0]^=rc[r];Nonlinear(a);}Theta(a,k);a[0]^=rc[0];}for(int i=0;i<4;i++)Offline70RoleProtocol.BigWrite(result,at+4*i,a[i]);}return result;
 }
 internal static byte[] Crypt(byte[] data,byte[] keys,int index,bool encrypt){
  if(keys==null||keys.Length!=334||data==null||data.Length==0||data.Length>4096||data.Length%(index==8?16:8)!=0||index<5||index>8)throw new InvalidDataException("Flow cipher bounds");
  for(int i=0;i<334;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Fixed offline flow keys required");
  if(index==5)return Offline70RoleProtocol.Xtea(data,keys,encrypt);if(index==7)return Offline70QuestProgress.Crypt(data,keys,encrypt);if(index==8)return Noekeon(data,keys,encrypt);
  byte[] result=new byte[data.Length];unchecked{for(int at=0;at<data.Length;at+=8){uint left=Offline70RoleProtocol.Big(data,at),right=Offline70RoleProtocol.Big(data,at+4);
   for(int i=0;i<8;i++){int r=encrypt?i:7-i;if((r&1)==0)right^=FO(FL(left,r),r);else left^=FL(FO(right,r),r);}
   Offline70RoleProtocol.BigWrite(result,at,left);Offline70RoleProtocol.BigWrite(result,at+4,right);
  }}return result;
 }
}
class FlowCipherFixturesFG {
 static int Main(string[] args){if(args.Length!=1)return 2;Directory.CreateDirectory(args[0]);byte[] keys=new byte[334];for(int i=0;i<334;i++)keys[i]=(byte)i;
  foreach(int index in new int[]{6,7,8})foreach(int count in new int[]{1,2,4,8}){int size=count*(index==8?16:8);byte[] plain=new byte[size];for(int i=0;i<size;i++)plain[i]=(byte)i;byte[] encrypted=Offline70FlowCipherFG.Crypt(plain,keys,index,true),back=Offline70FlowCipherFG.Crypt(encrypted,keys,index,false);for(int i=0;i<size;i++)if(back[i]!=plain[i])throw new InvalidDataException("Flow cipher roundtrip");string file=Path.Combine(args[0],"cipher"+index+"-"+size+".bin");using(FileStream f=new FileStream(file,FileMode.CreateNew,FileAccess.Write))f.Write(encrypted,0,encrypted.Length);}
  Console.WriteLine("PASS compiled flow cipher vectors and roundtrips");return 0;
 }
}
