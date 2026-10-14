using System;using System.IO;
// Bounded translation with native-exported schedules. This supports only the offline channel's fixed keys.
partial class Offline70AreaCipher {
 static ulong Big64(byte[] b,int at){return ((ulong)Offline70RoleProtocol.Big(b,at)<<32)|Offline70RoleProtocol.Big(b,at+4);}
 static void Write64(byte[] b,int at,ulong v){Offline70RoleProtocol.BigWrite(b,at,(uint)(v>>32));Offline70RoleProtocol.BigWrite(b,at+4,(uint)v);}
 internal static byte[] Crypt(byte[] data,byte[] keys,int index,bool encrypt){
  if(keys.Length!=334||data.Length==0||data.Length%8!=0||data.Length>(index==10?1024:256)||(index!=9&&index!=10))throw new InvalidDataException("Area cipher bounds");
  for(int i=0;i<334;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Fixed offline area keys required");
  byte[] result=new byte[data.Length];unchecked{for(int at=0;at<data.Length;at+=8){
   if(index==9){ulong v=Big64(data,at)^K9[encrypt?0:9];for(int round=1;round<=8;round++){ulong w=K9[(encrypt?0:9)+round];for(int j=0;j<8;j++){int shift=56-8*j;ulong x=T9[256*j+(int)((v>>shift)&255)];w^=round<8?x:x&(255UL<<shift);}v=w;}Write64(result,at,v);}
   else {uint l=Offline70RoleProtocol.Big(data,at),r=Offline70RoleProtocol.Big(data,at+4);for(int round=0;round<16;round++){int i=encrypt?round:15-round;uint d=i%3==0?K10[i]+r:i%3==1?K10[i]^r:K10[i]-r;int n=(int)K10[16+i]&31;d=(d<<n)|(d>>((32-n)&31));uint a=T10[d>>24],b=T10[256+((d>>16)&255)],c=T10[512+((d>>8)&255)],e=T10[768+(d&255)];uint f=i%3==0?((a^b)-c)+e:i%3==1?((a-b)+c)^e:((a+b)^c)-e;uint old=r;r=l^f;l=old;}Offline70RoleProtocol.BigWrite(result,at,r);Offline70RoleProtocol.BigWrite(result,at+4,l);}
  }}return result;
 }
}
