using System;using System.IO;using System.Text;using System.Collections.Generic;using System.Security.Cryptography;
// QP is a separate native wallet (notify358), not either of skill19's TP banks.
partial class Offline70QuestPointsFG81 {
 internal static void Credit(Offline70EconomyState s,string receipt,ushort quest){
  ushort amount=Points(quest);if(amount==0||s.QuestPointReceipts.Contains(receipt))return;
  if(amount>Int32.MaxValue-s.QuestPoints)throw new InvalidDataException("Quest point wallet overflow");
  s.QuestPoints+=amount;s.QuestPointReceipts.Add(receipt);
 }
 static bool Paid(Offline70EconomyState s,Offline70QuestOutboxFG plan){
  if(!plan.Delivered||!s.CompletedQuests.Contains(plan.QuestId))return false;
  foreach(Offline70EconomyReceipt receipt in s.Receipts)if(receipt.Id==plan.Receipt&&receipt.Kind=="quest-complete")return true;
  return false;
 }
 internal static Offline70EconomyState Recover(string path,Offline70ProgressionStateFG progress){
  Offline70EconomyState before=Offline70Economy.Load(path);List<Offline70QuestOutboxFG> missing=new List<Offline70QuestOutboxFG>();
  foreach(Offline70QuestOutboxFG plan in progress.Outbox)if(Points(plan.QuestId)>0&&!before.QuestPointReceipts.Contains(plan.Receipt)&&Paid(before,plan))missing.Add(plan);
  if(missing.Count==0)return before;
  missing.Sort(delegate(Offline70QuestOutboxFG a,Offline70QuestOutboxFG b){return String.CompareOrdinal(a.Receipt,b.Receipt);});
  StringBuilder payload=new StringBuilder();foreach(Offline70QuestOutboxFG plan in missing)payload.Append(plan.Receipt).Append(':').Append(Points(plan.QuestId)).Append(';');
  string fingerprint;using(SHA256 hash=SHA256.Create())fingerprint=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()))).Replace("-","");
  bool duplicate;return Offline70Economy.Apply(path,"qp-recover-"+fingerprint,"quest-qp-recovery",fingerprint,delegate(Offline70EconomyState s){foreach(Offline70QuestOutboxFG plan in missing){if(!Paid(s,plan))throw new InvalidDataException("Missing committed quest reward proof");Credit(s,plan.Receipt,plan.QuestId);}},out duplicate);
 }
 internal static byte[] Body(Offline70EconomyState s){Offline70Economy.Validate(s);byte[] b=new byte[7];BitConverter.GetBytes(s.QuestPoints).CopyTo(b,0);BitConverter.GetBytes(s.QuestPointResetCount).CopyTo(b,4);return b;}
 internal static byte[] Reply(Offline70EconomyState s,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(0,358,Body(s),keys,false);}
}
