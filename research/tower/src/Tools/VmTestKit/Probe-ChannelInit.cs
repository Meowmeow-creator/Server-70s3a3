using System;using System.IO;using System.Net;using System.Net.Sockets;using System.Diagnostics;using System.Threading;using System.Text;using System.Security.Cryptography;
// One loopback connection, one CHANNELINFO candidate derived from this 70 client's reader.
// No login acknowledgement, account storage or role creation is implemented.
class ProbeChannelInit {
 internal static void Dstr(BinaryWriter writer,string value){byte[] bytes=Encoding.ASCII.GetBytes(value);writer.Write((uint)bytes.Length);writer.Write(bytes);}
 internal static byte[] Packet(byte[] keys,uint unixTime){
  if(keys.Length!=334)throw new InvalidDataException("Unexpected 70 key material length");
  byte[] body;
  using(MemoryStream stream=new MemoryStream())using(BinaryWriter writer=new BinaryWriter(stream)){
   writer.Write((byte)1);writer.Write(keys);Dstr(writer,"ch10");
   writer.Write((uint)1);writer.Write((uint)1);writer.Write((byte)1);writer.Write((byte)10);
   writer.Write(unixTime);writer.Write((uint)1);Dstr(writer,"127.0.0.1");
   writer.Write((uint)7200);writer.Write((uint)7201);writer.Write((uint)544);writer.Write((uint)496);
   body=stream.ToArray();
  }
  for(int i=0;i<body.Length;i++){byte value=(byte)(body[i]^0xB5);body[i]=(byte)((value>>6)|(value<<2));}
  byte[] packet=new byte[15+body.Length];packet[1]=1;BitConverter.GetBytes(packet.Length).CopyTo(packet,3);body.CopyTo(packet,15);
  return packet;
 }
 static int Main(string[] args){
  if(args.Length==2&&args[0]=="--fixture"){
   byte[] fixture=new byte[334];for(int i=0;i<fixture.Length;i++)fixture[i]=(byte)i;
   File.WriteAllBytes(args[1],Packet(fixture,0x12345678));return 0;
  }
  if(args.Length!=1)return 2;
  string logPath=Path.GetFullPath(args[0]);
  using(FileStream file=new FileStream(logPath,FileMode.CreateNew,FileAccess.Write))using(StreamWriter log=new StreamWriter(file)){
   log.AutoFlush=true;TcpListener listener=new TcpListener(IPAddress.Loopback,7002);TcpClient client=null;
   try {
    listener.Start();log.WriteLine("Listening 127.0.0.1:7002; one 70 CHANNELINFO candidate, no login response");
    Stopwatch clock=Stopwatch.StartNew();byte[] buffer=new byte[4096];int total=0;
    while(clock.ElapsedMilliseconds<300000){
     if(client==null&&listener.Pending()){
      client=listener.AcceptTcpClient();log.WriteLine(DateTime.Now.ToString("o")+" Accepted "+client.Client.RemoteEndPoint);
      byte[] keys=new byte[334];RNGCryptoServiceProvider rng=new RNGCryptoServiceProvider();rng.GetBytes(keys);
      using(FileStream savedKeys=new FileStream(logPath+".keys.bin",FileMode.CreateNew,FileAccess.Write))savedKeys.Write(keys,0,keys.Length);
      uint unixTime=(uint)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
      byte[] packet=Packet(keys,unixTime);
      using(FileStream savedPacket=new FileStream(logPath+".sent.bin",FileMode.CreateNew,FileAccess.Write))savedPacket.Write(packet,0,packet.Length);
      client.GetStream().Write(packet,0,packet.Length);
      log.WriteLine(DateTime.Now.ToString("o")+" Sent CHANNELINFO length="+packet.Length+" header=15 keyBytes=334");
     }
     if(client!=null){
      if(client.Available>0){int count=client.GetStream().Read(buffer,0,Math.Min(buffer.Length,65536-total));total+=count;log.WriteLine(DateTime.Now.ToString("o")+" Received "+count+" "+BitConverter.ToString(buffer,0,count));if(total>=65536)break;}
      if(client.Client.Poll(0,SelectMode.SelectRead)&&client.Available==0){log.WriteLine(DateTime.Now.ToString("o")+" Peer closed");break;}
     }
     Thread.Sleep(20);
    }
    log.WriteLine("Completed bytes="+total);return 0;
   }catch(Exception e){log.WriteLine(e.ToString());return 1;}
   finally{if(client!=null)client.Close();listener.Stop();}
  }
 }
}
