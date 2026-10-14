using System;using System.IO;using System.Net;using System.Net.Sockets;using System.Diagnostics;using System.Threading;using System.Security.Cryptography;
// Bounded loopback schema experiment. Optional preverified empty roster; no account storage or role creation.
class ProbeLoginCandidate {
 internal static uint BodyHash(byte[] b){
  unchecked {
   uint h=(uint)b.Length;int i=0,n=b.Length/4;
   for(int k=0;k<n;k++,i+=4){h+=(uint)(b[i]|b[i+1]<<8);uint t=((uint)(b[i+2]|b[i+3]<<8)<<11)^(h<<16);h^=t;h+=h>>11;}
   switch(b.Length&3){
    case 3:h+=(uint)(b[i]|b[i+1]<<8);h^=(h^((uint)b[i+2]<<2))<<16;h+=h>>11;break;
    case 2:h+=(uint)(b[i]|b[i+1]<<8);h^=h<<11;h+=h>>17;break;
    case 1:h+=b[i];h^=h<<10;h+=h>>1;break;
   }
   h^=h<<3;h+=h>>5;h^=h<<4;h+=h>>17;h^=h<<25;h+=h>>6;return h;
  }
 }
 internal static void ReceiverGate(byte[] packet,byte previous){
  byte[] body=new byte[packet.Length-15];Buffer.BlockCopy(packet,15,body,0,body.Length);
  uint h=BodyHash(body);byte folded=(byte)(h^(h>>8)^(h>>16)^(h>>24)^previous);
  // Native FB20F0 packs two interleaved byte values into the top 16 header bits.
  // Select the next receiver state as zero; the other byte carries folded body hash.
  uint packed=(uint)(((folded&3)<<6)|((folded&12)<<12)|((folded&48)>>2)|((folded&192)<<4));
  BitConverter.GetBytes(packed<<16).CopyTo(packet,11);
 }
 internal static byte[] LoginBody(){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)1); // success
   w.Write((byte)18);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);
   w.Write((uint)1); // opaque initialization value: semantics remain under investigation
   ProbeChannelInit.Dstr(w,"127.0.0.1");w.Write((uint)7200);w.Write((uint)7201);
   w.Write(new byte[11]);w.Write((uint)0);w.Write((uint)0);
   byte[] body=m.ToArray();if(body.Length!=50)throw new InvalidDataException("Login schema size");return body;
  }
 }
 internal static byte[] Crypt(byte[] input,byte[] keys,bool encrypt){
  if(keys.Length!=334||input.Length==0||input.Length%16!=0)throw new InvalidDataException("AES bounds");
  byte[] key=new byte[16];Buffer.BlockCopy(keys,8,key,0,16);
  using(AesManaged aes=new AesManaged()){aes.Key=key;aes.Mode=CipherMode.ECB;aes.Padding=PaddingMode.None;
   using(ICryptoTransform t=encrypt?aes.CreateEncryptor():aes.CreateDecryptor())return t.TransformFinalBlock(input,0,input.Length);
  }
 }
 internal static byte[] LoginPacket(byte[] keys){
  byte[] plain=LoginBody(),padded=new byte[64];plain.CopyTo(padded,0);byte[] encrypted=Crypt(padded,keys,true);
  byte[] packet=new byte[79];packet[0]=1;packet[1]=1;BitConverter.GetBytes(packet.Length).CopyTo(packet,3);encrypted.CopyTo(packet,15);ReceiverGate(packet,0);return packet;
 }
 static void Save(string path,byte[] data){using(FileStream f=new FileStream(path,FileMode.CreateNew,FileAccess.Write))f.Write(data,0,data.Length);}
 static bool LoginStrings(byte[] plain){
  using(MemoryStream m=new MemoryStream(plain))using(BinaryReader r=new BinaryReader(m)){
   for(int k=0;k<2;k++){uint size=r.ReadUInt32();if(size!=1||r.ReadByte()!=49)return false;}
   return true;
  }
 }
 static int Main(string[] args){
  if(args.Length==2&&args[0]=="--fixture"){
   byte[] keys=new byte[334];for(int i=0;i<keys.Length;i++)keys[i]=(byte)i;
   byte[] init=ProbeChannelInit.Packet(keys,0x12345678);ReceiverGate(init,0x27);
   Save(args[1]+".keys.bin",keys);Save(args[1]+".init.bin",init);Save(args[1]+".plain.bin",LoginBody());Save(args[1]+".reply.bin",LoginPacket(keys));return 0;
  }
  bool roleFixture=args.Length==3&&args[1]=="--role-fixture";
  if(args.Length!=1&&!roleFixture)return 2;string path=Path.GetFullPath(args[0]);
  byte[] fixedKeys=null,rolePacket=null;
  if(roleFixture){
   fixedKeys=File.ReadAllBytes(args[2]+".keys.bin");rolePacket=File.ReadAllBytes(args[2]+".reply.bin");
   if(fixedKeys.Length!=334||rolePacket.Length!=31||rolePacket[0]!=0||BitConverter.ToUInt16(rolePacket,1)!=2||BitConverter.ToInt32(rolePacket,3)!=31)throw new InvalidDataException("Role fixture bounds");
   for(int i=0;i<334;i++)if(fixedKeys[i]!=(byte)i)throw new InvalidDataException("Unexpected deterministic fixture key");
   using(SHA256 h=SHA256.Create())if(BitConverter.ToString(h.ComputeHash(rolePacket)).Replace("-","")!="164BBA08B4057E4F5585C4847E2488BAC54C789C6C852D09919EF77148154607")throw new InvalidDataException("Role fixture digest");
  }
  using(FileStream file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(StreamWriter log=new StreamWriter(file)){
   log.AutoFlush=true;TcpListener listener=new TcpListener(IPAddress.Loopback,7002);TcpClient client=null;
   try {
    listener.Start();log.WriteLine("Listening 127.0.0.1:7002; one 70 LOGIN schema candidate; no persistent accounts or roles; emptyRosterFixture="+roleFixture);
    Stopwatch clock=Stopwatch.StartNew();byte[] buffer=new byte[4096],pending=new byte[0],keys=null;int total=0;bool replied=false,rosterSent=false;
    while(clock.ElapsedMilliseconds<300000){
     if(client==null&&listener.Pending()){
      client=listener.AcceptTcpClient();log.WriteLine(DateTime.Now.ToString("o")+" Accepted "+client.Client.RemoteEndPoint);
      keys=new byte[334];if(roleFixture)fixedKeys.CopyTo(keys,0);else new RNGCryptoServiceProvider().GetBytes(keys);Save(path+".keys.bin",keys);
      byte[] init=ProbeChannelInit.Packet(keys,(uint)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds);
      ReceiverGate(init,0x27);Save(path+".sent.bin",init);client.GetStream().Write(init,0,init.Length);log.WriteLine(DateTime.Now.ToString("o")+" Sent CHANNELINFO length="+init.Length);
     }
     if(client!=null){
      if(client.Available>0){
       int count=client.GetStream().Read(buffer,0,Math.Min(buffer.Length,65536-total));total+=count;
       log.WriteLine(DateTime.Now.ToString("o")+" Received "+count+" "+BitConverter.ToString(buffer,0,count));
       byte[] merged=new byte[pending.Length+count];pending.CopyTo(merged,0);Buffer.BlockCopy(buffer,0,merged,pending.Length,count);pending=merged;
       while(pending.Length>=13){
        int size=BitConverter.ToInt32(pending,3);if(size<13||size>65536)throw new InvalidDataException("Outgoing frame bounds");if(pending.Length<size)break;
        ushort command=BitConverter.ToUInt16(pending,1),sequence=BitConverter.ToUInt16(pending,11);
        log.WriteLine(DateTime.Now.ToString("o")+" Frame type="+pending[0]+" opcode="+command+" size="+size+" seq="+sequence);
        if(pending[0]==1&&command==1&&!replied){
         byte[] encrypted=new byte[size-13];Buffer.BlockCopy(pending,13,encrypted,0,encrypted.Length);byte[] plain=Crypt(encrypted,keys,false);
         if(!LoginStrings(plain))throw new InvalidDataException("Unexpected bounded test login");Save(path+".request-plain.bin",plain);
         byte[] reply=LoginPacket(keys);Save(path+".login-plain.bin",LoginBody());Save(path+".login-reply.bin",reply);
         client.GetStream().Write(reply,0,reply.Length);replied=true;log.WriteLine(DateTime.Now.ToString("o")+" Sent LOGIN candidate length=79 plainLength=50; receiver gate encoded; low checksum byte unverified");
        }
        if(replied&&command==8)log.WriteLine(DateTime.Now.ToString("o")+" OBSERVED opcode8 after LOGIN candidate; handler progress requires corroboration; emptyRosterFixture="+roleFixture);
        if(replied&&command==8&&roleFixture&&!rosterSent){
         Save(path+".roster-reply.bin",rolePacket);client.GetStream().Write(rolePacket,0,rolePacket.Length);rosterSent=true;
         log.WriteLine(DateTime.Now.ToString("o")+" Sent USERINFO subtype2 empty roster candidate length=31; slotMax=12 sealCount=0 roleCount=0; no role creation implemented");
        }
        if(command==5)log.WriteLine(DateTime.Now.ToString("o")+" OBSERVED CREATE_CHARACTER request; captured only, no acknowledgement or account writes");
        byte[] rest=new byte[pending.Length-size];Buffer.BlockCopy(pending,size,rest,0,rest.Length);pending=rest;
       }
       if(total>=65536)break;
      }
      if(client.Client.Poll(0,SelectMode.SelectRead)&&client.Available==0){log.WriteLine(DateTime.Now.ToString("o")+" Peer closed");break;}
     }
     Thread.Sleep(20);
    }
    log.WriteLine("Completed bytes="+total+" loginCandidateSent="+replied+" emptyRosterSent="+rosterSent);return 0;
   }catch(Exception e){log.WriteLine(e.ToString());return 1;}finally{if(client!=null)client.Close();listener.Stop();}
  }
 }
}
