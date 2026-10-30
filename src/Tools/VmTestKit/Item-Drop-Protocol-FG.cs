using System;using System.IO;
class Offline70DiscardRequestFG {internal ushort X,Y,Slot;internal byte List;internal uint Quantity;}
class Offline70DiscardProtocolFG {
 internal static Offline70DiscardRequestFG Parse(byte[] cipher,byte[] keys){if(cipher==null||cipher.Length!=16)throw new InvalidDataException("Drop50 cipher bounds");byte[] b=Offline70FlowCipherFG.Crypt(cipher,keys,8,false);
  // FG400: byte 11 is the client's confirm flag, live-captured 2026-10-30. Whole-stack drags
  // send 0 (slot 123 x7 14:19:19, slot 130 item 3303 14:21:11), while every flow that raises
  // the discard dialog sends 1 (equipment slot 25 04:36, purple material slot 136 04:37,
  // "drop 1 of a 9 stack" slot 106 14:18:48 - that last one is in the 14:18 REJECTED line).
  // Demanding 0 here refused those requests before the ownership/kind checks ever ran and the
  // client kept its locally predicted floor item, i.e. a silent "cannot discard".
  if(b[11]>1)throw new InvalidDataException("Drop50 confirm flag="+b[11]);
  for(int i=12;i<16;i++)if(b[i]!=0)throw new InvalidDataException("Drop50 reserved or padding");
  Offline70DiscardRequestFG r=new Offline70DiscardRequestFG{X=BitConverter.ToUInt16(b,0),Y=BitConverter.ToUInt16(b,2),List=b[4],Slot=BitConverter.ToUInt16(b,5),Quantity=BitConverter.ToUInt32(b,7)};
  // FG407: the avatar bag is a different item space (list 1, see Offline70Economy.Position), so
  // byte 4 is no longer pinned to 0 here - the handler checks kind against space. Any space a
  // forged frame names is bounded to the same 0..7 the economy model uses, and the rejection
  // message keeps the value so a wrong guess is visible in the log instead of silent.
  if(r.List>7||r.Quantity==0||r.Quantity>1000000||r.X>32767||r.Y>32767)throw new InvalidDataException("Drop50 request bounds list="+r.List);
  return r;}
 internal static byte[] Reply(Offline70DiscardRequestFG r,bool success,byte[] keys){byte[] b=success?new byte[8]:new byte[]{0,0x13};if(success){b[0]=1;b[1]=r.List;BitConverter.GetBytes(r.Slot).CopyTo(b,2);BitConverter.GetBytes(r.Quantity).CopyTo(b,4);}return Offline70RoleProtocol.BatchPacketFG(1,50,b,keys,false);}
 // Pre-FG395 discard ground row (36 bytes). Once FG400 let the op50 parse through again this
 // path is live and the client does consume it: live 2026-10-30 14:19/14:21 the player pressed
 // X on discarded stacks and the op46 requests carried exactly the ground slots this builder
 // handed out (2003/2004/2005 -> PICKUP_COMMITTED), so the row renders. Do not "improve" the
 // shape without a fresh live capture.
 internal static byte[] Ground(Offline70DiscardRequestFG r,Offline70GroundFG d){byte[] b=new byte[36];BitConverter.GetBytes((ushort)1).CopyTo(b,0);BitConverter.GetBytes(r.X).CopyTo(b,2);BitConverter.GetBytes(r.Y).CopyTo(b,4);BitConverter.GetBytes(d.Slot).CopyTo(b,6);BitConverter.GetBytes(d.ItemId).CopyTo(b,8);BitConverter.GetBytes(d.Quantity).CopyTo(b,13);b[35]=1;return b;}
 // FG396 ground-row probe layouts for the CN70 op40 parser. The envelope is proven (the
 // client decrypts and parses it - layout 1 makes it spam OVERFLOW_INFO op232 body
 // {opcode 40,0..} at ~500/s), only the row shape is unknown. Layout 1 is the known
 // freeze trigger used to capture the client parser via a runtime stack dump.
 internal static byte[] ProbeRowFG396(int layout,ushort uid,ushort mapIndex){
  ushort slot=2000;uint item=3221;byte[] b;
  if(layout==1){b=new byte[34];BitConverter.GetBytes(uid).CopyTo(b,0);BitConverter.GetBytes((ushort)400).CopyTo(b,2);BitConverter.GetBytes((ushort)300).CopyTo(b,4);BitConverter.GetBytes(slot).CopyTo(b,6);BitConverter.GetBytes(item).CopyTo(b,8);BitConverter.GetBytes((uint)1).CopyTo(b,12);BitConverter.GetBytes(mapIndex).CopyTo(b,31);b[33]=1;return b;}
  if(layout==2){b=new byte[64];BitConverter.GetBytes(uid).CopyTo(b,0);BitConverter.GetBytes((ushort)400).CopyTo(b,2);BitConverter.GetBytes((ushort)300).CopyTo(b,4);BitConverter.GetBytes(slot).CopyTo(b,6);BitConverter.GetBytes(item).CopyTo(b,8);BitConverter.GetBytes((ushort)1).CopyTo(b,12);BitConverter.GetBytes(slot).CopyTo(b,14);BitConverter.GetBytes(item).CopyTo(b,16);BitConverter.GetBytes((uint)1).CopyTo(b,20);BitConverter.GetBytes(mapIndex).CopyTo(b,40);b[41]=1;return b;}
  if(layout==3){b=new byte[64];BitConverter.GetBytes(uid).CopyTo(b,0);BitConverter.GetBytes((ushort)400).CopyTo(b,2);BitConverter.GetBytes((ushort)300).CopyTo(b,4);BitConverter.GetBytes((ushort)1).CopyTo(b,6);BitConverter.GetBytes(slot).CopyTo(b,8);BitConverter.GetBytes(item).CopyTo(b,10);b[13]=0;BitConverter.GetBytes((uint)1).CopyTo(b,14);BitConverter.GetBytes(mapIndex).CopyTo(b,40);b[63]=1;return b;}
  throw new InvalidDataException("Ground probe layout");}

}

class Offline70DeleteRowFG {internal ushort Reason,Slot;internal uint ItemId,Quantity;}
class Offline70DeleteRequestFG {internal byte List;internal Offline70DeleteRowFG[] Rows;}
class Offline70DeleteProtocolFG {
 internal static Offline70DeleteRequestFG Parse(byte[] cipher,byte[] keys){
  if(cipher==null||cipher.Length<16||cipher.Length>4096||cipher.Length%16!=0)throw new InvalidDataException("Delete18 cipher bounds");
  byte[] b=Offline70GoldCipher.Crypt(cipher,keys,4,false);int count=b[1],length=7+12*count;
  // Live capture 2026-10-28 20:21:38 (pet equipment discard, save economy-account1-r8): the
  // client also deletes out of the pet window, which it tags with item space 7 -- body
  // {07 01 | reason 1, slot 140, item 2747175, qty 1 | checksum 56 CC 1E 00, confirm 0}.
  // Demanding list 0 here rejected every pet-window delete before DeleteTown could look at it.
  // FG407 adds item space 1 (the avatar bag, Offline70Economy.Position kind 3 list 1 slots 0..103);
  // everything else stays refused so a forged frame cannot reach the worn/cargo spaces.
  if(count==0||count>32||(b[0]!=0&&b[0]!=1&&b[0]!=7)||length>b.Length||b.Length!=(length+15)/16*16)throw new InvalidDataException("Delete18 list/count bounds list="+b[0]+" count="+count+" bytes="+b.Length);
  // Live capture 2026-10-26 22:49 (triage #3): the wire tail after the rows is a 4-byte
  // checksum plus a confirm byte the client sets to 1 (native MSG_DELETE_ITEM +0x4bf tail);
  // demanding 0 here rejected every town delete. reason 2/3/4 are the client's cube-statistic
  // delete flavours (rednf process_18 delKind mapping), not only 1.
  if(b[length-1]!=1&&b[length-1]!=0)throw new InvalidDataException("Delete18 confirm flag");for(int i=length;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Delete18 padding");
  Offline70DeleteRequestFG r=new Offline70DeleteRequestFG{List=b[0],Rows=new Offline70DeleteRowFG[count]};
  for(int i=0;i<count;i++){int at=2+12*i;Offline70DeleteRowFG row=new Offline70DeleteRowFG{Reason=BitConverter.ToUInt16(b,at),Slot=BitConverter.ToUInt16(b,at+2),ItemId=BitConverter.ToUInt32(b,at+4),Quantity=BitConverter.ToUInt32(b,at+8)};if(row.Reason<1||row.Reason>4||row.Quantity==0||row.Quantity>1000000)throw new InvalidDataException("Delete18 reason/count");for(int j=0;j<i;j++)if(r.Rows[j].Slot==row.Slot)throw new InvalidDataException("Delete18 duplicate slot");r.Rows[i]=row;}return r;
 }
 internal static byte[] Reply(Offline70DeleteRequestFG r,bool success,byte[] keys){byte[] b=success?new byte[3+8*r.Rows.Length]:new byte[]{0,0x13};if(success){b[0]=1;b[1]=r.List;b[2]=(byte)r.Rows.Length;for(int i=0;i<r.Rows.Length;i++){int at=3+8*i;BitConverter.GetBytes(r.Rows[i].Slot).CopyTo(b,at);BitConverter.GetBytes(r.Rows[i].Quantity).CopyTo(b,at+2);BitConverter.GetBytes(r.Rows[i].Reason).CopyTo(b,at+6);}}return Offline70RoleProtocol.BatchPacketFG(1,18,b,keys,false);}
}
