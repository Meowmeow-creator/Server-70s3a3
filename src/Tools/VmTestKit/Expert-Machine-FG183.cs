using System;using System.IO;using System.Collections.Generic;
// Disjointer machine placement (FG183 follow-up).
//
// Wire facts taken from DNF70_v50.exe, not guessed:
//  * Client -> server create request is opcode 233 (not the reference's 238): the store-name
//    dialog's confirm path 0xD58330 validates the name, then 0x718E70 builds the packet with
//    push 0xe9 and writes {byte flag, int nameBytes, nameBytes} (0x638E0 writes a 4-byte byte
//    length + narrow/GBK bytes).
//  * Server -> client machine info is notify 218, the reference NOTI 206 shifted by the same +12
//    as expert info 205->217. Handler 0x468570 reads exactly: word uniqueId, length-prefixed
//    narrow name, byte vill, byte area, word x, word y, dword level, then resolves the owner by
//    uniqueId (0x36dfa0) and creates the village object at that point.
//  * The command ACK uses the request opcode with header type 1 and a single success byte,
//    matching the reference CDisjointer::OnCreateDisjointStore (put_header(1,0xee); put_byte(1)).
//
// op37 SET_USER_POSITION is the only position stream the client sends (reference
// DisPatcher_SetPos: short x, short y, byte z, short dir). Keeping the last point lets the
// machine appear under the player instead of at the map default.
partial class Offline70BatchServiceFG {
 int expertLastX=474,expertLastY=234; // world-entry point until the client reports a move
 int expertEndurance=300;             // session value; reset when a machine is placed

 bool ExpertMachineOp(ushort op,byte[] body,ushort sequence){
  if(op==37){
   try{byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,op%14,false);if(plain.Length>=4){expertLastX=BitConverter.ToUInt16(plain,0);expertLastY=BitConverter.ToUInt16(plain,2);}}catch(InvalidDataException){}
   return true; // movement is client-side; there is no reply for this command
  }
  // The quick-button placement path sends opcode 250 (builder 0xDB4D30) with
  // {string len, string, int fee, short x, short y}; the client then sits in its loading
  // state until the matching ACK arrives. Answer it before the op233 name-dialog path.
  // The int is the stall fee the player typed: builder 0xdb4e46 writes machine+0x20c, which is the
  // dword the 218 record carried back. Echo it instead of inventing one.
  if(op==250){
   byte[] req;
   try{req=Offline70RoleProtocol.BatchCryptFG(body,keys,op%14,false);}
   catch(InvalidDataException e){Note("EXPERT_MACHINE_REJECTED op250 decrypt "+e.Message);return true;}
   if(req.Length<13){Note("EXPERT_MACHINE_REJECTED op250 short body="+req.Length);return true;}
   int nameLen=BitConverter.ToInt32(req,0);
   if(nameLen<0||12+nameLen>req.Length){Note("EXPERT_MACHINE_REJECTED op250 nameLen="+nameLen+" body="+req.Length);return true;}
   int fee=BitConverter.ToInt32(req,4+nameLen);
   int x=BitConverter.ToUInt16(req,8+nameLen),y=BitConverter.ToUInt16(req,10+nameLen);
   if(assets==null||assets.ExpertJobTypeFG183!=Offline70ExpertLevelFG406.DisjointerType){
    Note("EXPERT_MACHINE_REJECTED role="+(role==null?0:role.Id)+" expert="+(assets==null?0:assets.ExpertJobTypeFG183)+" reason=not disjointer");
    Send("expert-machine-ack250",Offline70RoleProtocol.BatchPacketFG(1,250,new byte[]{0},keys,false));
    return true;
   }
   // [limit disjoint gold] 10000 is the reference's range check on the same field.
   if(fee<0||fee>10000){Note("EXPERT_MACHINE_REJECTED op250 fee="+fee);fee=1;}
   SendMachineInfo(new byte[]{0x41},x,y,fee);
   Send("expert-machine-ack250",Offline70RoleProtocol.BatchPacketFG(1,250,new byte[]{1},keys,false));
   Note("EXPERT_MACHINE_PLACED role="+role.Id+" kind="+(nameLen>0?req[4]:0)+" at="+town+","+area+" "+x+","+y+" fee="+fee+" tier="+MachineTierFG406(assets)+" endurance="+expertEndurance);
   return true;
  }
  // Machine decomposition: reference CMD 239 REQUEST_DISJOINT_ITEM, this client 251
  // (read_198.inc: short machineUid, short slot, signed char backpack type). The reward
  // transaction is the same one op28 uses; the reply body is the shared disjoint result
  // {1, slot, list, count, [newSlot(short), itemId(uint), quantity(uint)]...}.
  if(op==251){
   byte[] req;
   try{req=Offline70RoleProtocol.BatchCryptFG(body,keys,op%14,false);}
   catch(InvalidDataException e){Note("EXPERT_MACHINE_DISJOINT_REJECTED decrypt "+e.Message);return true;}
   if(req.Length<5){Note("EXPERT_MACHINE_DISJOINT_REJECTED short body="+req.Length);return true;}
   ushort machineUid=BitConverter.ToUInt16(req,0);
   short slot=BitConverter.ToInt16(req,2);
   byte list=req[4];
   if(assets==null||assets.ExpertJobTypeFG183!=3||role==null||machineUid!=(ushort)(role.Id&0xffff)||list!=0||slot<3||slot>=41){
    Note("EXPERT_MACHINE_DISJOINT_REJECTED role="+(role==null?0:role.Id)+" uid="+machineUid+" slot="+slot+" list="+list+" expert="+(assets==null?0:assets.ExpertJobTypeFG183));
    Send("expert-machine-disjoint-failed",Offline70RoleProtocol.BatchPacketFG(1,251,Offline70DisjointFG92.Body(null,false,null,null,0x13),keys,false));
    return true;
   }
   string fingerprint=Convert.ToBase64String(body),prior;
   if(disjointRequestsFG92.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Machine disjoint sequence reused");Note("EXPERT_MACHINE_DISJOINT_DUPLICATE seq="+sequence);return true;}
   byte[] result;Dictionary<ushort,uint> collection=null;
   try{
    Sync();collection=CollectionSnapshot();Offline70EconomyState before=assets;bool duplicate;
    Offline70DisjointFG92.Request r=new Offline70DisjointFG92.Request{Slot=(ushort)slot,List=list,Tool=-1};
    assets=Offline70DisjointFG92.Open(economyPath,session+"-machine-disjoint-"+sequence,r,Offline70DisjointFG92.Draw(10000),Offline70DisjointFG92.Draw(10000),out duplicate);
    if(duplicate){Note("EXPERT_MACHINE_DISJOINT_RECEIPT_DUPLICATE seq="+sequence);return true;}
    result=Offline70DisjointFG92.Body(r,true,before,assets,0);
    // CDisjointer::complete_disjoint appends {money, remainingEndurance} after the gain rows.
    // Omitting this tail made the client read past the body and store garbage (0x30303030)
    // into [char+0x2d4], which is the corrupted "8084..." endurance in the machine UI.
    int use=Offline70DisjointFG92.Draw(3)+1; // disjointer.exj [endurance reduce] 1 3
    if(use>expertEndurance)use=expertEndurance;
    expertEndurance-=use;
    // Expertness exp comes from [gain exp] 0 1 in disjointer.exj (0..1 per decomposition).
    // Persist it in the economy state and push it to the client. The stock 217 handler only reads
    // the tier/endurance dwords (0x47e941), so the exp reaches the UI through the character
    // basic-info record (op2 subtype 1) - exactly what the reference does on every exp change:
    // CExpertJob::IncreaseExpertJobExp sends put_header(0,2)+make_basic_info, and make_basic_info
    // carries put_byte(expertType)+put_int(expertExp) (make_basic_info.inc:249-250). Sending only
    // the 217 refresh left the panel's exp at its login value.
    int gained=1+Offline70DisjointFG92.Draw(2); // [gain exp] 0 1 base; reference also adds the item's additional exp, not modelled yet
    bool expDuplicate;
    assets=Offline70Economy.Apply(economyPath,session+"-expert-exp-"+sequence,"expert-exp",gained+":"+expertEndurance,delegate(Offline70EconomyState s){if(gained>0)s.ExpertJobExpFG183=Math.Min(2054,s.ExpertJobExpFG183+gained);s.ExpertJobEnduranceFG183=expertEndurance;},out expDuplicate);
    byte[] tail=new byte[result.Length+8];
    Buffer.BlockCopy(result,0,tail,0,result.Length);
    BitConverter.GetBytes(assets.Gold).CopyTo(tail,result.Length);
    BitConverter.GetBytes(expertEndurance).CopyTo(tail,result.Length+4);
    result=tail;
    Note("EXPERT_MACHINE_DISJOINT_COMMITTED slot="+slot+" gains="+tail[4]+" money="+assets.Gold+" endurance="+expertEndurance+" used="+use+" expGained="+gained+" expertExp="+assets.ExpertJobExpFG183);
   }catch(Offline70DisjointFG92.Rejected e){result=Offline70DisjointFG92.Body(null,false,null,null,e.Code);collection=null;Note("EXPERT_MACHINE_DISJOINT_REJECTED code="+e.Code+" "+e.Message);}
   catch(InvalidDataException e){result=Offline70DisjointFG92.Body(null,false,null,null,0x13);collection=null;Note("EXPERT_MACHINE_DISJOINT_REJECTED code=19 "+e.Message);}
   disjointRequestsFG92.Add(sequence,fingerprint);
   Send(result[0]==1?"expert-machine-disjoint":"expert-machine-disjoint-failed",Offline70RoleProtocol.BatchPacketFG(1,251,result,keys,false));
   if(result[0]==1){Sync();CollectionChanged(collection);Appearance();SendExpertJobInfoRefreshFG183();}
   return true;
  }
  // Norton repair: reference CMD 240 REPAIR_DISJOINT_MACHINE, this client 252 (empty body).
  // Reference OnRepairDisjointMachine replies on the request opcode with
  // {byte 1, int money, int endurance}. The price is
  // GetDisjointMachineRepairCost(grade + 1, endurance) = rowCost - rowCost*endurance/rowFull, so a
  // full machine is free and the row (and the repaired value) follows the machine grade.
  if(op==252){
   if(assets==null||assets.ExpertJobTypeFG183!=Offline70ExpertLevelFG406.DisjointerType){
    Note("EXPERT_MACHINE_REPAIR_REJECTED reason=not disjointer");
    Send("expert-machine-repair-failed",Offline70RoleProtocol.BatchPacketFG(1,252,new byte[]{0,0x13},keys,false));
    return true;
   }
   int repairGrade=MachineGradeFG406(assets);
   int repairFull=Offline70ExpertLevelFG406.RepairFull(repairGrade);
   int cost=Offline70ExpertLevelFG406.RepairPrice(repairGrade,expertEndurance);
   if(assets.Gold<(uint)cost){
    Note("EXPERT_MACHINE_REPAIR_REJECTED gold="+assets.Gold+" cost="+cost);
    Send("expert-machine-repair-failed",Offline70RoleProtocol.BatchPacketFG(1,252,new byte[]{0,0x13},keys,false));
    return true;
   }
   bool repairDuplicate;
   assets=Offline70Economy.Apply(economyPath,session+"-expert-repair-"+sequence,"expert-repair",cost+":"+expertEndurance+":"+repairFull,delegate(Offline70EconomyState s){if(s.Gold<(uint)cost)throw new InvalidDataException("Repair wallet");s.Gold-=(uint)cost;s.ExpertJobEnduranceFG183=repairFull;},out repairDuplicate);
   expertEndurance=repairFull;
   byte[] repairBody=new byte[9];
   repairBody[0]=1;BitConverter.GetBytes((int)assets.Gold).CopyTo(repairBody,1);BitConverter.GetBytes(expertEndurance).CopyTo(repairBody,5);
   Send("expert-machine-repaired",Offline70RoleProtocol.BatchPacketFG(1,252,repairBody,keys,false));
   SendExpertJobInfoRefreshFG183();
   Note("EXPERT_MACHINE_REPAIRED role="+role.Id+" grade="+repairGrade+" cost="+cost+" money="+assets.Gold+" endurance="+expertEndurance);
   return true;
  }
  if(op!=233)return false;
  byte[] req233;
  try{req233=Offline70RoleProtocol.BatchCryptFG(body,keys,op%14,false);}
  catch(InvalidDataException e){Note("EXPERT_MACHINE_REJECTED decrypt "+e.Message);return true;}
  if(assets==null||assets.ExpertJobTypeFG183!=Offline70ExpertLevelFG406.DisjointerType){
   Note("EXPERT_MACHINE_REJECTED role="+(role==null?0:role.Id)+" expert="+(assets==null?0:assets.ExpertJobTypeFG183)+" reason=not disjointer");
   Send("expert-machine-ack",Offline70RoleProtocol.BatchPacketFG(1,233,new byte[]{0},keys,false));
   return true;
  }
  if(req233.Length<5){Note("EXPERT_MACHINE_REJECTED short body="+req233.Length);return true;}
  int len=BitConverter.ToInt32(req233,1);
  if(len<0||5+len>req233.Length){Note("EXPERT_MACHINE_REJECTED nameLen="+len+" body="+req233.Length);return true;}
  byte flag=req233[0];
  byte[] name=new byte[len];
  Buffer.BlockCopy(req233,5,name,0,len);
  // This dialog path carries no fee; the client's placement dialog defaults to 1 gold.
  SendMachineInfo(name,expertLastX,expertLastY,1);
  Send("expert-machine-ack",Offline70RoleProtocol.BatchPacketFG(1,233,new byte[]{1},keys,false));
  Note("EXPERT_MACHINE_CREATED role="+role.Id+" flag="+flag+" nameBytes="+len+" at="+town+","+area+" "+expertLastX+","+expertLastY+" tier="+MachineTierFG406(assets));
  return true;
 }
 void SendMachineInfo(byte[] name,int x,int y,int fee){
  Send("expert-machine-info",Offline70RewardTypesFG183.DisjointMachineInfoReply((ushort)(role.Id&0xffff),name,(byte)town,(byte)area,(ushort)x,(ushort)y,fee,keys));
 }
 // The machine tier the client shows (217 dword = machine grade + 1) is the character's expert job
 // level: CDisjointer::OnUpgradeDisjointMachine refuses to push the grade past `expertLevel - 1`, and
 // this server has no gold upgrade command, so the machine always sits at that cap.
 static int MachineTierFG406(Offline70EconomyState s){return Offline70ExpertLevelFG406.MachineLevel(Offline70ExpertLevelFG406.Level(s.ExpertJobTypeFG183,s.ExpertJobExpFG183,s.Level));}
 static int MachineGradeFG406(Offline70EconomyState s){return Offline70ExpertLevelFG406.Grade(Offline70ExpertLevelFG406.Level(s.ExpertJobTypeFG183,s.ExpertJobExpFG183,s.Level));}
 // World entry: the 217 record carries giveup/type/tier/endurance and the expertness exp. A fresh
 // session starts the machine at the disjointer.exj initial endurance; the exp is the saved value.
 // FG406: the tier is the expert job level (see MachineTierFG406), so a level-1 分解师 reports Lv.1
 // instead of the fixed Lv.5 FG381 advertised.
 internal byte[] ExpertJobLoginInfoFG183(Offline70EconomyState s,byte[] keys){
  int tier=MachineTierFG406(s),full=Offline70ExpertLevelFG406.RepairFull(Offline70ExpertLevelFG406.Grade(tier));
  expertEndurance=Math.Max(0,Math.Min(full,s.ExpertJobEnduranceFG183));
  return Offline70RewardTypesFG183.ExpertJobInfoReply(s.ExpertJobTypeFG183,expertEndurance,s.ExpertJobExpFG183,tier,keys);
 }
 void SendExpertJobInfoRefreshFG183(){
  Send("expert-job-info",Offline70RewardTypesFG183.ExpertJobInfoReply(assets.ExpertJobTypeFG183,expertEndurance,assets.ExpertJobExpFG183,MachineTierFG406(assets),keys));
 }
}
