using System;
using System.IO;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading;

// Independent, isolated entrance lifecycle check. No client or player save is opened.
class BatchEntranceFixturesFG {
 static Process owned; static int checks, sessions; static string directory;
 static HashSet<string> keys=new HashSet<string>(); static List<string> steps=new List<string>();
 static void Check(bool good,string text){if(!good)throw new InvalidDataException(text);checks++;}
 static string Quote(string value){if(value.IndexOf('"')>=0)throw new InvalidDataException("Quote in path");return "\""+value+"\"";}
 static byte[] Frame(byte opcode,int length){byte[] value=new byte[length];value[1]=opcode;BitConverter.GetBytes(length).CopyTo(value,2);value[10]=1;return value;}
 static byte[] ReadExact(NetworkStream stream,int length){byte[] result=new byte[length];int at=0;while(at<length){int count=stream.Read(result,at,length-at);if(count==0)throw new EndOfStreamException();at+=count;}return result;}
 static byte[] Receive(NetworkStream stream,byte opcode){byte[] header=ReadExact(stream,11);int size=BitConverter.ToInt32(header,2);Check(header[0]==0&&header[1]==opcode&&header[10]==1,"Response header opcode "+opcode);Check(size>=11&&size<=65536,"Response bounded length");byte[] result=new byte[size];header.CopyTo(result,0);ReadExact(stream,size-11).CopyTo(result,11);return result;}
 static byte[] Aes(byte[] encrypted,byte[] key){byte[] shortKey=new byte[16];Array.Copy(key,shortKey,16);using(RijndaelManaged cipher=new RijndaelManaged()){cipher.KeySize=128;cipher.BlockSize=128;cipher.Mode=CipherMode.ECB;cipher.Padding=PaddingMode.None;cipher.Key=shortKey;using(ICryptoTransform transform=cipher.CreateDecryptor())return transform.TransformFinalBlock(encrypted,0,encrypted.Length);}}
 static byte[] StoredZlib(byte[] frame){int offset=11;Check(frame.Length>=22&&frame[offset]==0x78&&frame[offset+1]==1&&frame[offset+2]==1,"Stored final zlib framing");int size=frame[offset+3]|frame[offset+4]<<8;int inverse=frame[offset+5]|frame[offset+6]<<8;Check((size^inverse)==65535&&frame.Length==size+22,"Stored zlib length complement");byte[] result=new byte[size];Array.Copy(frame,offset+7,result,0,size);uint a=1,b=0;foreach(byte value in result){a=(a+value)%65521;b=(b+a)%65521;}uint sum=(b<<16)|a;for(int i=0;i<4;i++)Check(frame[offset+7+size+i]==(byte)(sum>>(24-i*8)),"Zlib Adler32 byte "+i);return result;}
 static void Alive(string label){owned.Refresh();Check(!owned.HasExited,"Entrance exited during "+label);}
 static TcpClient Connect(){DateTime until=DateTime.UtcNow.AddSeconds(5);while(true){Alive("connect");TcpClient peer=new TcpClient();try{peer.Connect("127.0.0.1",17006);peer.ReceiveTimeout=4000;peer.SendTimeout=4000;return peer;}catch(SocketException){peer.Close();if(DateTime.UtcNow>=until)throw;Thread.Sleep(40);}}}
 static byte[] Hello(NetworkStream stream,bool fragment){byte[] hello=Frame(11,43);if(fragment){stream.Write(hello,0,5);Thread.Sleep(40);stream.Write(hello,5,38);}else stream.Write(hello,0,43);byte[] response=Receive(stream,12);Check(response.Length==47,"Hello response length");byte[] key=new byte[32];Array.Copy(response,15,key,0,32);Check(keys.Add(BitConverter.ToString(key)),"Fresh handshake key");return key;}
 static void Handshake(byte[] script,string label,bool fragment){using(TcpClient peer=Connect()){NetworkStream stream=peer.GetStream();byte[] key=Hello(stream,fragment);byte[] tokenRequest=Frame(5,27);stream.Write(tokenRequest,0,tokenRequest.Length);byte[] tokenFrame=Receive(stream,6);Check(tokenFrame.Length==43,"Token response length");byte[] tokenCipher=new byte[32];Array.Copy(tokenFrame,11,tokenCipher,0,32);byte[] token=Aes(tokenCipher,key);Check(token[0]==0&&Encoding.ASCII.GetString(token,4,10)=="70-local-1","Token plaintext");for(int i=14;i<32;i++)Check(token[i]==0,"Token padding");
  byte[] scriptRequest=Frame(9,11);stream.Write(scriptRequest,0,scriptRequest.Length);byte[] scriptPlain=Aes(StoredZlib(Receive(stream,10)),key);Check(scriptPlain.Length==(script.Length+15)/16*16,"Padded script length");for(int i=0;i<script.Length;i++)if(scriptPlain[i]!=script[i])throw new InvalidDataException("Script plaintext mismatch at "+i);checks++;for(int i=script.Length;i<scriptPlain.Length;i++)Check(scriptPlain[i]==0,"Script zero pad");
  byte[] channelRequest=Frame(17,31);Encoding.ASCII.GetBytes("cain").CopyTo(channelRequest,11);stream.Write(channelRequest,0,channelRequest.Length);byte[] channel=Aes(StoredZlib(Receive(stream,18)),key);Check(channel.Length==64,"Channel plaintext length");Check(BitConverter.ToInt16(channel,0)==1&&BitConverter.ToInt32(channel,2)==1,"Channel group and row count");Check(Encoding.ASCII.GetString(channel,6,6)=="#ch.10","Channel name");Check(channel[26]==100,"Channel load");Check(Encoding.ASCII.GetString(channel,34,9)=="127.0.0.1","Channel address");Check(BitConverter.ToInt32(channel,50)==7002,"Channel target port");sessions++;}
  Thread.Sleep(120);Alive(label+" normal close");steps.Add(label);
 }
 static void Broken(byte[] bytes,string label){using(TcpClient peer=Connect()){if(bytes.Length>0)peer.GetStream().Write(bytes,0,bytes.Length);}Thread.Sleep(150);Alive(label);steps.Add(label);}
 static int Main(string[] args){if(args.Length!=3)return 2;directory=Path.GetFullPath(args[2]);Directory.CreateDirectory(directory);try{byte[] script=File.ReadAllBytes(args[1]);ProcessStartInfo info=new ProcessStartInfo(Path.GetFullPath(args[0]),Quote(Path.Combine(directory,"entrance.txt"))+" --script-response "+Quote(Path.GetFullPath(args[1]))+" 10 --test-port 17006 --interactive");info.UseShellExecute=false;info.CreateNoWindow=true;info.WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(args[0]));owned=Process.Start(info);
  for(int i=1;i<=3;i++)Handshake(script,"full-handshake-"+i,i==2);
  Broken(new byte[0],"empty-peer");Handshake(script,"recover-after-empty",false);
  Broken(new byte[]{0,11,43,0,0},"partial-header-peer");Handshake(script,"recover-after-partial-header",false);
  byte[] partial=Frame(11,43);Array.Resize(ref partial,20);Broken(partial,"partial-body-peer");Handshake(script,"recover-after-partial-body",false);
  Broken(Frame(10,43),"bad-first-opcode-peer");Handshake(script,"recover-after-bad-opcode",false);
  byte[] bad=Frame(11,11);BitConverter.GetBytes(10).CopyTo(bad,2);Broken(bad,"invalid-small-length-peer");Handshake(script,"recover-after-invalid-length",false);
  using(TcpClient peer=Connect()){NetworkStream stream=peer.GetStream();Hello(stream,false);byte[] oversized=Frame(5,11);BitConverter.GetBytes(65537).CopyTo(oversized,2);stream.Write(oversized,0,11);}Thread.Sleep(150);Alive("bad length after hello");steps.Add("invalid-length-after-hello");Handshake(script,"recover-after-session-error",false);
  File.WriteAllText(Path.Combine(directory,"fixture-result.txt"),"PASS checks="+checks+" sessions="+sessions+" uniqueKeys="+keys.Count+"\r\n"+String.Join("\r\n",steps.ToArray()),Encoding.UTF8);return 0;
 }catch(Exception error){File.WriteAllText(Path.Combine(directory,"fixture-result.txt"),"FAIL "+error,Encoding.UTF8);Console.Error.WriteLine(error);return 1;}finally{if(owned!=null){owned.Refresh();if(!owned.HasExited){owned.Kill();owned.WaitForExit(3000);}owned.Dispose();}}}
}
