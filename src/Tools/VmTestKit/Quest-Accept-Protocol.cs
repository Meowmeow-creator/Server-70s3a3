using System;using System.IO;using System.Xml;using System.Xml.Serialization;
public class Offline70QuestState {public int Format=1;public ushort RoleId=Offline70RoleScopeFG202.Current;public bool Accepted;public uint Progress=1;public bool Completed;public uint Gold;}
class Offline70QuestAccept {
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70QuestState));
 static void Validate(Offline70QuestState s){if(s==null||s.Format!=1||s.RoleId==0||s.Progress>1||!s.Accepted&&s.Progress!=1||s.Completed&&(!s.Accepted||s.Progress!=0)||s.Gold!=(s.Completed?100u:0u))throw new InvalidDataException("Quest state profile");}
 internal static Offline70QuestState Load(string path){
  if(!File.Exists(path))return new Offline70QuestState();if(new FileInfo(path).Length>4096)throw new InvalidDataException("Quest state file bounds");
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;Offline70QuestState s=(Offline70QuestState)Serializer.Deserialize(r);Validate(s);return s;}
 }
 internal static Offline70QuestState Accept(string path){return Update(path,0);}
 internal static Offline70QuestState Ready(string path){return Update(path,1);}
 internal static Offline70QuestState Complete(string path){return Update(path,2);}
 static Offline70QuestState Update(string path,int operation){
  string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));
  using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
   Offline70QuestState s=Load(full);
   if(operation==2){if(s.Completed)return s;if(!s.Accepted||s.Progress!=0)throw new InvalidDataException("Quest not ready");s.Completed=true;s.Gold=100;}
   else{if(s.Completed)throw new InvalidDataException("Quest already completed");if(operation==1){if(!s.Accepted)throw new InvalidDataException("Quest not accepted");if(s.Progress==0)return s;s.Progress=0;}else{if(s.Accepted)return s;s.Accepted=true;}}
   Validate(s);
   string temp=full+".pending-"+Guid.NewGuid().ToString("N"),backup=full+Offline70StoreBackupFG354.Suffix;
   try{using(FileStream f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Serializer.Serialize(f,s);f.Flush();}
    Offline70QuestState candidate=Load(temp);if(!candidate.Accepted||candidate.Progress!=s.Progress||candidate.Completed!=s.Completed||candidate.Gold!=s.Gold)throw new InvalidDataException("Quest read-back mismatch");
    if(File.Exists(full))File.Replace(temp,full,backup);else File.Move(temp,full);
    Offline70QuestState committed=Load(full);if(!committed.Accepted||committed.Progress!=s.Progress||committed.Completed!=s.Completed||committed.Gold!=s.Gold)throw new InvalidDataException("Quest commit mismatch");return committed;
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static ushort Parse(byte[] encrypted,byte[] keys){
  byte[] b=Offline70RoleProtocol.Xtea(encrypted,keys,false);if(b.Length!=16)throw new InvalidDataException("Accept request bounds");
  if(BitConverter.ToUInt16(b,0)!=33)throw new InvalidDataException("Accept command marker");
  ushort id=BitConverter.ToUInt16(b,2);for(int i=4;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Accept padding");return id;
 }
 internal static byte[] Body(bool success){return success?new byte[]{1,101,0,1,0,0,0,0}:new byte[]{0,0x12};}
 internal static byte[] Reply(byte[] keys,bool success){byte[] body=Body(success),padded=new byte[8];body.CopyTo(padded,0);return Offline70RoleProtocol.Packet(1,33,Offline70RoleProtocol.Xtea(padded,keys,true));}
 internal static byte[] Reply(byte[] keys,bool success,uint progress){if(progress>1)throw new InvalidDataException("Accept progress bounds");byte[] body=Body(success);if(success)BitConverter.GetBytes(progress).CopyTo(body,3);byte[] padded=new byte[8];body.CopyTo(padded,0);return Offline70RoleProtocol.Packet(1,33,Offline70RoleProtocol.Xtea(padded,keys,true));}
}
