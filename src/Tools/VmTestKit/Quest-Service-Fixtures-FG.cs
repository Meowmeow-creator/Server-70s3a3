using System;using System.IO;using System.Reflection;
// Independent wire fixture generator reflects the exact service under audit.
class QuestServiceFixturesFG {
 static int Main(string[] args){if(args.Length!=2)return 2;Directory.CreateDirectory(args[1]);Assembly source=Assembly.LoadFrom(Path.GetFullPath(args[0]));BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;byte[] keys=new byte[334];for(int i=0;i<334;i++)keys[i]=(byte)i;
  byte[] plain=new byte[16];BitConverter.GetBytes((ushort)33).CopyTo(plain,0);BitConverter.GetBytes((ushort)1002).CopyTo(plain,2);byte[] encrypted=(byte[])source.GetType("Offline70FlowCipherFG").GetMethod("Crypt",flags).Invoke(null,new object[]{plain,keys,5,true});byte[] frame=new byte[13+encrypted.Length];frame[0]=1;BitConverter.GetBytes((ushort)33).CopyTo(frame,1);BitConverter.GetBytes(frame.Length).CopyTo(frame,3);encrypted.CopyTo(frame,13);File.WriteAllBytes(Path.Combine(args[1],"accept-1002.bin"),frame);
  MethodInfo reply=source.GetType("Offline70QuestWireFG").GetMethod("ProgressReply",flags);foreach(uint remaining in new uint[]{0,1})File.WriteAllBytes(Path.Combine(args[1],"progress-1002-"+remaining+".bin"),(byte[])reply.Invoke(null,new object[]{(ushort)1002,remaining,true,keys}));
  foreach(uint experience in new uint[]{864,1458}){byte[] body=(byte[])source.GetType("Offline70ProgressionFG").GetMethod("ExperienceBody",flags).Invoke(null,new object[]{experience});byte[] packet=(byte[])source.GetType("Offline70RoleProtocol").GetMethod("BatchPacketFG",flags).Invoke(null,new object[]{(byte)0,(ushort)37,body,keys,true});File.WriteAllBytes(Path.Combine(args[1],"experience-"+experience+".bin"),packet);}Console.WriteLine("PASS exact service quest1002 and cumulative experience fixtures");return 0;
 }
}
