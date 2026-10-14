using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

// Bounded entrance responses based on ACT4 and optional A21 list layout.
// This is a protocol compatibility experiment, not an authentication server.
class Offline70BatchEntranceFG {
 static byte[] key=new byte[32];
 static byte[] channelScript; static int channelId=1;
 static byte[] Response() {
  byte[] packet = new byte[47]; packet[1]=12; packet[2]=47; packet[10]=1;
  RNGCryptoServiceProvider rng=new RNGCryptoServiceProvider();rng.GetBytes(key);
  Buffer.BlockCopy(key,0,packet,15,32); return packet;
 }
 static byte[] TokenResponse() {
  byte[] plaintext=new byte[32];plaintext[0]=(byte)(channelScript==null?1:0);
  System.Text.Encoding.ASCII.GetBytes(channelScript==null?"dnf2008-local":"70-local-1").CopyTo(plaintext,4);
  byte[] aesKey=new byte[16];Buffer.BlockCopy(key,0,aesKey,0,16);
  byte[] payload;
  using(RijndaelManaged aes=new RijndaelManaged()) {
   aes.KeySize=128;aes.BlockSize=128;aes.Mode=CipherMode.ECB;aes.Padding=PaddingMode.None;aes.Key=aesKey;
   using(ICryptoTransform transform=aes.CreateEncryptor())payload=transform.TransformFinalBlock(plaintext,0,plaintext.Length);
  }
  byte[] packet=new byte[43];packet[1]=6;packet[2]=43;packet[10]=1;Buffer.BlockCopy(payload,0,packet,11,32);return packet;
 }
 static byte[] ChannelResponse() {
  // Client verifies: i16 integrated PvP group, u32 count, then a 48-byte row.
  byte[] plain=new byte[64];plain[0]=1;plain[2]=1;
  System.Text.Encoding.ASCII.GetBytes("#ch."+channelId).CopyTo(plain,6);plain[26]=100;
  System.Text.Encoding.ASCII.GetBytes("127.0.0.1").CopyTo(plain,34);
  BitConverter.GetBytes(7002).CopyTo(plain,50);
  byte[] aesKey=new byte[16];Buffer.BlockCopy(key,0,aesKey,0,16);byte[] encrypted;
  using(RijndaelManaged aes=new RijndaelManaged()) {
   aes.KeySize=128;aes.BlockSize=128;aes.Mode=CipherMode.ECB;aes.Padding=PaddingMode.None;aes.Key=aesKey;
   using(ICryptoTransform transform=aes.CreateEncryptor())encrypted=transform.TransformFinalBlock(plain,0,plain.Length);
  }
  // RFC1950 zlib wrapper around one final stored DEFLATE block (64 bytes).
  byte[] zlib=new byte[75];zlib[0]=0x78;zlib[1]=1;zlib[2]=1;zlib[3]=64;zlib[5]=191;zlib[6]=255;
  Buffer.BlockCopy(encrypted,0,zlib,7,64);uint a=1,b=0;
  foreach(byte value in encrypted){a=(a+value)%65521;b=(b+a)%65521;}uint checksum=(b<<16)|a;
  for(int i=0;i<4;i++)zlib[71+i]=(byte)(checksum>>(24-8*i));
  byte[] packet=new byte[86];packet[1]=18;packet[2]=86;packet[10]=1;Buffer.BlockCopy(zlib,0,packet,11,75);return packet;
 }
 static byte[] ScriptResponse() {
  byte[] plain=new byte[((channelScript.Length+15)/16)*16];Buffer.BlockCopy(channelScript,0,plain,0,channelScript.Length);
  byte[] aesKey=new byte[16];Buffer.BlockCopy(key,0,aesKey,0,16);byte[] encrypted;
  using(RijndaelManaged aes=new RijndaelManaged()) {
   aes.KeySize=128;aes.BlockSize=128;aes.Mode=CipherMode.ECB;aes.Padding=PaddingMode.None;aes.Key=aesKey;
   using(ICryptoTransform transform=aes.CreateEncryptor())encrypted=transform.TransformFinalBlock(plain,0,plain.Length);
  }
  int n=encrypted.Length;byte[] z=new byte[n+11];z[0]=0x78;z[1]=1;z[2]=1;
  z[3]=(byte)n;z[4]=(byte)(n>>8);z[5]=(byte)~n;z[6]=(byte)(~n>>8);Buffer.BlockCopy(encrypted,0,z,7,n);
  uint a=1,b=0;foreach(byte v in encrypted){a=(a+v)%65521;b=(b+a)%65521;}uint sum=(b<<16)|a;
  for(int i=0;i<4;i++)z[n+7+i]=(byte)(sum>>(24-8*i));
  byte[] packet=new byte[11+z.Length];packet[1]=10;BitConverter.GetBytes(packet.Length).CopyTo(packet,2);packet[10]=1;Buffer.BlockCopy(z,0,packet,11,z.Length);return packet;
 }
 static bool Hello(List<byte> bytes) {
  if(bytes.Count<43)return false;
  byte[] prefix={0,11,43,0,0,0,0,0,0,0,1};
  for(int i=0;i<prefix.Length;i++)if(bytes[i]!=prefix[i])return false;
  return true;
 }
 static int Main(string[] args) {
  bool interactive=args.Length>0&&args[args.Length-1]=="--interactive";
  if(interactive)Array.Resize(ref args,args.Length-1);
  int port=7001;
  if(args.Length!=4&&args.Length!=6)return 2;
  if(args.Length==6&&(args[4]!="--test-port"||!Int32.TryParse(args[5],out port)||port<17000||port>17999))return 2;
  if(args[1]!="--script-response"||args[3]!="10")return 2;
  channelId=10;channelScript=File.ReadAllBytes(args[2]);
  if(channelScript.Length<1||channelScript.Length>60000)return 2;
  using(StreamWriter log=new StreamWriter(args[0],false)){
   log.AutoFlush=true;TcpListener listener=new TcpListener(IPAddress.Loopback,port);int session=0;
   try{
    listener.Start();log.WriteLine(DateTime.Now.ToString("o")+" LISTENING loopback="+port+" interactive="+interactive+" noTimeLimit="+interactive);
    do{
     using(TcpClient client=listener.AcceptTcpClient()){
      client.NoDelay=true;session++;List<byte> received=new List<byte>();byte[] buffer=new byte[4096];
      bool sent=false,tokenSent=false,channelSent=false,scriptSent=false;long total=0;
      log.WriteLine(DateTime.Now.ToString("o")+" ACCEPTED session="+session);
      try{
       NetworkStream stream=client.GetStream();int count;
       while((count=stream.Read(buffer,0,buffer.Length))>0){
        total+=count;for(int i=0;i<count;i++)received.Add(buffer[i]);
        while(received.Count>=11){
         uint length=BitConverter.ToUInt32(received.ToArray(),2);
         if(length<11||length>65536)throw new InvalidDataException("Entrance frame length");
         if(received.Count<length)break;
         byte[] response=null;byte op=received[1];
         if(!sent){if(!Hello(received))throw new InvalidDataException("Unexpected entrance hello");response=Response();sent=true;}
         else if(received[0]==0&&received[10]==1){
          if(!tokenSent&&op==5&&length==27){response=TokenResponse();tokenSent=true;}
          else if(tokenSent&&!scriptSent&&op==9){response=ScriptResponse();scriptSent=true;}
          else if(tokenSent&&!channelSent&&op==17&&length==31){response=ChannelResponse();channelSent=true;}
         }
         log.WriteLine(DateTime.Now.ToString("o")+" FRAME session="+session+" opcode="+op+" bytes="+length);
         received.RemoveRange(0,(int)length);
         if(response!=null){stream.Write(response,0,response.Length);log.WriteLine(DateTime.Now.ToString("o")+" SENT session="+session+" opcode="+response[1]+" bytes="+response.Length);}
        }
        if(!interactive&&total>=1048576)break;
       }
       log.WriteLine(DateTime.Now.ToString("o")+" PEER_CLOSED session="+session+" bytes="+total+" handshakeComplete="+(sent&&tokenSent&&scriptSent&&channelSent)+" pendingBytes="+received.Count+" listenerRetained="+interactive);
      }catch(IOException e){log.WriteLine(DateTime.Now.ToString("o")+" PEER_IO session="+session+" "+e.Message);}
       catch(SocketException e){log.WriteLine(DateTime.Now.ToString("o")+" PEER_SOCKET session="+session+" "+e.SocketErrorCode);}
       catch(InvalidDataException e){log.WriteLine(DateTime.Now.ToString("o")+" PEER_REJECTED session="+session+" "+e.Message);}
     }
    }while(interactive);
    return 0;
   }catch(Exception e){log.WriteLine(DateTime.Now.ToString("o")+" FATAL "+e);return 1;}
   finally{listener.Stop();}
  }
 }
}

