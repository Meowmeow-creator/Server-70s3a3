using System;using System.IO;
// Lorien basis level1 ->100 gold in current itemdropinfo_clearreward.etc.
// Fixed known-item offer exercises paid cards; it is not the random drop formula.
class Offline70PaidCardFG {
 internal const uint Cost=100,ItemId=1000,Quantity=1;
 internal static byte[] ClearBody(uint total,uint gained,uint before){
  // Native35 groups1/2 are additional free-card categories, not the selected
  // paid face. Native74 inserts the actual paid reward into category3.
  byte[] body=Offline70DungeonFlowFG.ClearRewardBody(total,gained,before);
  BitConverter.GetBytes(Cost).CopyTo(body,74);return body;
 }
 internal static byte[] SelectionBody(byte free,byte paid){
  using(MemoryStream s=new MemoryStream())using(BinaryWriter w=new BinaryWriter(s)){
   // Each row is a CARD position; the field is its owning PARTY member.
   // 105EA10(card,owner) and105EAD0(card,owner) expect owner0 for local yoyo.
   w.Write((byte)1);for(int card=0;card<4;card++){
    w.Write(card==free?(byte)0:(byte)255);w.Write(card==paid?(byte)0:(byte)255);
    // Native1061E20 draws entry1 only when the paid vector has >1 entry;
    // entry0 is always its gold face, including a zero-gold placeholder.
    bool reward=card==paid;w.Write(reward?(byte)2:(byte)0);
    if(reward){w.Write((uint)0);w.Write((uint)0);w.Write(ItemId);w.Write(Quantity);}w.Write((byte)0);
   }return s.ToArray();
  }
 }
 // The legacy fixture wrapper represents Lorien Lv1 only. Runtime passes its proven dungeon fee.
 internal static Offline70EconomyState Purchase(string path,string runId,out bool duplicate){return Purchase(path,runId,Cost,out duplicate);}
 internal static Offline70EconomyState Purchase(string path,string runId,uint fee,out bool duplicate){
  if(fee==0||fee>Offline70Economy.WalletLimit)throw new InvalidDataException("Paid card fee bounds");
  Guid run;try{run=new Guid(runId);}catch(Exception e){throw new InvalidDataException("Paid card run id",e);}if(run.ToString("N")!=runId)throw new InvalidDataException("Paid card run id format");
  return Offline70Economy.Apply(path,runId+"-paid-card","paid-card",fee+":"+ItemId+":"+Quantity,delegate(Offline70EconomyState state){if(state.Gold<fee)throw new InvalidDataException("Insufficient paid-card gold");Offline70Economy.AddAssetItem(state,ItemId,Quantity);state.Gold-=fee;},out duplicate);
 }
}
