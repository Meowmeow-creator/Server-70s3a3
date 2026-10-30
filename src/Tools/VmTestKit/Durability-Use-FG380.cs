using System;using System.IO;
// FG380: equipment durability consumption (CN op51 = 0x33 DECREASE_DURABILITY).
//
// Native chain (rednf, the only equipment-durability decrement in the original server):
//   DisPatcher_UseEquip::process (read_48.inc / process_48.inc) -> CInventory::use_equip(slot):
//   slot 0..0x19, worn row must be equipment, current durability != 0, then m_fieldb-- (one point
//   per report). The dispatcher always answers through send_22.inc: header(1,0x33) with {1, slot}
//   on success, {0, errNo} otherwise (0xb when the row is already worn out, 0x13 non-equipment,
//   0x11 no script). Death and skill use never touch durability server-side, and repair is op25
//   (already implemented as Npc-Repair-Core-FG63).
//
// This engine had no op51 at all - the client's durability drain was client-only display and the
// store kept full durability forever (2026-10-28 report 装备不消耗耐久). The client sends one
// frame per durability point its own combat model consumes; the frame is 8 encrypted bytes whose
// first byte is the worn-equipment slot.
partial class Offline70BatchServiceFG {
 void DurabilityUseFG380(byte[] body,ushort sequence){
  byte slot;byte[] reply;
  try{
   byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,51%14,false);
   if(plain.Length<1)throw new InvalidDataException("Durability51 empty body");
   slot=plain[0];
   if(slot>0x19)throw new InvalidDataException("Durability51 slot bounds");
   Sync();
   bool duplicate;bool missing=false;
   assets=Offline70Economy.Apply(economyPath,"session"+session+"-dura-"+sequence,"durability-use",slot.ToString(),delegate(Offline70EconomyState s){
    Offline70EconomyEntry e=Offline70Economy.Find(s,3,slot);
    if(e==null||Offline70Economy.Definition(e.ItemId).Kind!=4){missing=true;return;}
    if(e.Durability==0){missing=true;return;}
    e.Durability--;
   },out duplicate);
   Sync();
   Offline70EconomyEntry worn=Offline70Economy.Find(assets,3,slot);
   byte errNo=(byte)(worn==null||Offline70Economy.Definition(worn.ItemId).Kind!=4?0x13:worn.Durability==0?(byte)0xb:(byte)1);
   reply=missing?new byte[]{0,errNo}:new byte[]{1,slot};
   Send(missing?"durability-failed51":"durability-used51",Offline70RoleProtocol.BatchPacketFG(1,51,reply,keys,false));
   Note("DURABILITY_USED slot="+slot+" left="+(worn==null?"-":worn.Durability.ToString())+" item="+(worn==null?0:worn.ItemId)+" duplicate="+duplicate+" seq="+sequence);
  }catch(InvalidDataException e){
   Send("durability-failed51",Offline70RoleProtocol.BatchPacketFG(1,51,new byte[]{0,0x11},keys,false));
   Note("REJECTED opcode=51 reason="+e.Message);
  }
 }
}
