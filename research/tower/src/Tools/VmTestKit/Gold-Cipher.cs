using System;using System.IO;
// Native Twofish(4), Skipjack(11), Anubis(13), bounded fixed offline keys.
partial class Offline70GoldCipher {
 static uint Rol(uint x,int n){return (x<<n)|(x>>(32-n));}static uint Ror(uint x,int n){return (x>>n)|(x<<(32-n));}
 static uint G(uint x,bool rotate){return rotate?State4[x>>24]^State4[256+(x&255)]^State4[512+((x>>8)&255)]^State4[768+((x>>16)&255)]:State4[x&255]^State4[256+((x>>8)&255)]^State4[512+((x>>16)&255)]^State4[768+(x>>24)];}
 static uint[] Twofish(uint[] x,bool encrypt){unchecked{
  uint a=x[encrypt?0:2]^State4[1024+(encrypt?0:6)],b=x[encrypt?1:3]^State4[1024+(encrypt?1:7)],c=x[encrypt?2:0]^State4[1024+(encrypt?2:4)],d=x[encrypt?3:1]^State4[1024+(encrypt?3:5)];
  for(int i=0;i<16;i++){int r=encrypt?i:15-i,k=1032+2*r;uint t=G((r&1)==0?a:c,false),u=G((r&1)==0?b:d,true),f=t+u+State4[k],h=t+2*u+State4[k+1];
   if((r&1)==0){if(encrypt){c=Ror(c^f,1);d=Rol(d,1)^h;}else{c=Rol(c,1)^f;d=Ror(d^h,1);}}
   else {if(encrypt){a=Ror(a^f,1);b=Rol(b,1)^h;}else{a=Rol(a,1)^f;b=Ror(b^h,1);}}
  }
  return encrypt?new uint[]{c^State4[1028],d^State4[1029],a^State4[1030],b^State4[1031]}:new uint[]{a^State4[1024],b^State4[1025],c^State4[1026],d^State4[1027]};
 }}
 static ushort SkipG(ushort w,int r){int k=((r-1)*4)%10;byte a=(byte)(w>>8),b=(byte)w,c=(byte)(SkipTable[b^State11[k]]^a),d=(byte)(SkipTable[c^State11[(k+1)%10]]^b),e=(byte)(SkipTable[d^State11[(k+2)%10]]^c),f=(byte)(SkipTable[e^State11[(k+3)%10]]^d);return (ushort)((e<<8)|f);}
 static ushort SkipUndo(ushort w,int r){int k=((r-1)*4)%10;byte e=(byte)(w>>8),f=(byte)w,d=(byte)(f^SkipTable[e^State11[(k+3)%10]]),c=(byte)(e^SkipTable[d^State11[(k+2)%10]]),b=(byte)(d^SkipTable[c^State11[(k+1)%10]]),a=(byte)(c^SkipTable[b^State11[k]]);return (ushort)((a<<8)|b);}
 static uint[] Anubis(uint[] x,bool encrypt){int rounds=(int)State13[1],s=encrypt?2:78;uint[] v=new uint[4];for(int j=0;j<4;j++)v[j]=x[j]^State13[s+j];for(int r=1;r<=rounds;r++){uint[] n=new uint[4];for(int j=0;j<4;j++){int shift=24-8*j;uint a=AnubisTables[(v[0]>>shift)&255],b=AnubisTables[256+((v[1]>>shift)&255)],c=AnubisTables[512+((v[2]>>shift)&255)],d=AnubisTables[768+((v[3]>>shift)&255)];n[j]=(r==rounds?(a&0xff000000u)^(b&0xff0000u)^(c&0xff00u)^(d&255):a^b^c^d)^State13[s+4*r+j];}v=n;}return v;}
 internal static byte[] Crypt(byte[] input,byte[] keys,int index,bool encrypt){
  // A full104-slot avatar bag carries65-byte rows including native jewel data.
  int block=index==11?8:16,maximum=index==13?8192:4096;if(input==null||input.Length==0||input.Length>maximum||input.Length%block!=0||keys==null||keys.Length!=334||index!=4&&index!=11&&index!=13)throw new InvalidDataException("Gold cipher bounds");for(int i=0;i<334;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Gold cipher fixed key profile");byte[] result=new byte[input.Length];
  for(int at=0;at<input.Length;at+=block){if(index==11){ushort a=(ushort)(input[at]*256+input[at+1]),b=(ushort)(input[at+2]*256+input[at+3]),c=(ushort)(input[at+4]*256+input[at+5]),d=(ushort)(input[at+6]*256+input[at+7]);for(int i=0;i<32;i++){int r=encrypt?i+1:32-i;bool ruleA=r<=8||r>=17&&r<=24;ushort g;
    if(encrypt){g=SkipG(a,r);if(ruleA){ushort oldd=d;d=c;c=b;b=g;a=(ushort)(g^oldd^r);}else{ushort olda=a; a=d;d=c;c=(ushort)(olda^b^r);b=g;}}
    else {g=SkipUndo(b,r);if(ruleA){ushort olda=a;a=g;b=c;c=d;d=(ushort)(olda^SkipG(g,r)^r);}else {ushort olda=a,oldc=c;a=g;c=d;d=olda;b=(ushort)(oldc^g^r);}}
   }ushort[] w=new ushort[]{a,b,c,d};for(int i=0;i<4;i++){result[at+2*i]=(byte)(w[i]>>8);result[at+2*i+1]=(byte)w[i];}}
   else{uint[] x=new uint[4];for(int j=0;j<4;j++)x[j]=index==4?BitConverter.ToUInt32(input,at+4*j):Offline70RoleProtocol.Big(input,at+4*j);x=index==4?Twofish(x,encrypt):Anubis(x,encrypt);for(int j=0;j<4;j++){if(index==4)BitConverter.GetBytes(x[j]).CopyTo(result,at+4*j);else Offline70RoleProtocol.BigWrite(result,at+4*j,x[j]);}}
  }return result;
 }
}
