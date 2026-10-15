using System;using System.IO;using System.Collections.Generic;using System.Threading;
// Only diagnostic text/packet copies use this queue. Character/economy/quest
// transactions retain their synchronous, atomic commits before replies.
class Offline70EvidenceWriterFG171:IDisposable {
 sealed class Record {internal string Path,Text;internal byte[] Bytes;}
 readonly object gate=new object();readonly Queue<Record> pending=new Queue<Record>();readonly string prefix;readonly Thread worker;int queuedBytes;bool closing,closed;Exception failure;
 // Degraded mode: a diagnostic sink that cannot be opened/written (leftover log with
 // CreateNew, a locked file, a full disk) must never take the service down. Callers
 // keep serving; the failure is reported once on stderr and diagnostics stop.
 bool degraded;
 internal bool Degraded {get{lock(gate)return degraded;}}
 internal Offline70EvidenceWriterFG171(string path){prefix=System.IO.Path.GetFullPath(path);worker=new Thread(Write);worker.IsBackground=true;worker.Name="DNF70 diagnostic evidence";worker.Start();}
 void Add(Record record){int count=record.Bytes==null?record.Text.Length*2:record.Bytes.Length;lock(gate){while(failure==null&&!closing&&(pending.Count>=4096||queuedBytes+count>8388608))Monitor.Wait(gate);if(failure!=null||degraded)return;if(closing)return;pending.Enqueue(record);queuedBytes+=count;Monitor.PulseAll(gate);}}
 internal void Line(string text){if(text==null||text.Length>65536)throw new InvalidDataException("Diagnostic text bound");Add(new Record{Text=text});}
 internal void Packet(string path,byte[] data){string full=System.IO.Path.GetFullPath(path);if(!full.StartsWith(prefix+".",StringComparison.OrdinalIgnoreCase)||data==null||data.Length>65536)throw new InvalidDataException("Diagnostic packet scope/bound");Add(new Record{Path=full,Bytes=(byte[])data.Clone()});}
 void Write(){try{using(FileStream file=new FileStream(prefix,FileMode.CreateNew,FileAccess.Write))using(StreamWriter text=new StreamWriter(file)){for(;;){Record record;bool idle;lock(gate){while(pending.Count==0&&!closing)Monitor.Wait(gate);if(pending.Count==0)break;record=pending.Dequeue();queuedBytes-=record.Bytes==null?record.Text.Length*2:record.Bytes.Length;idle=pending.Count==0;Monitor.PulseAll(gate);}if(record.Bytes==null)text.WriteLine(record.Text);else using(FileStream packet=new FileStream(record.Path,FileMode.CreateNew,FileAccess.Write))packet.Write(record.Bytes,0,record.Bytes.Length);if(idle)text.Flush();}text.Flush();}}catch(Exception e){lock(gate){if(!degraded){degraded=true;failure=e;pending.Clear();queuedBytes=0;}Monitor.PulseAll(gate);}try{Console.Error.WriteLine("DNF70 diagnostic evidence degraded (service continues): "+e.GetType().Name+" "+e.Message);}catch{}}finally{lock(gate){closed=true;Monitor.PulseAll(gate);}}}
 public void Dispose(){lock(gate){closing=true;Monitor.PulseAll(gate);}worker.Join();bool incomplete;string why;lock(gate){incomplete=!closed||pending.Count!=0||failure!=null;why=failure==null?(closed?"":"writer did not finish"):"writer failed";}if(incomplete)try{Console.Error.WriteLine("DNF70 diagnostic evidence incomplete at shutdown ("+why+")");}catch{}}
}
