using System;using System.IO;
class Offline70DiscardRequestFG {internal ushort X,Y,Slot;internal byte List;internal uint Quantity;}
class Offline70DiscardProtocolFG {
 internal static Offline70DiscardRequestFG Parse(byte[] cipher,byte[] keys){if(cipher==null||cipher.Length!=16)throw new InvalidDataException("Drop50 cipher bounds");byte[] b=Offline70FlowCipherFG.Crypt(cipher,keys,8,false);for(int i=11;i<16;i++)if(b[i]!=0)throw new InvalidDataException("Drop50 reserved or padding");Offline70DiscardRequestFG r=new Offline70DiscardRequestFG{X=BitConverter.ToUInt16(b,0),Y=BitConverter.ToUInt16(b,2),List=b[4],Slot=BitConverter.ToUInt16(b,5),Quantity=BitConverter.ToUInt32(b,7)};if(r.List!=0||r.Quantity==0||r.Quantity>1000000||r.X>32767||r.Y>32767)throw new InvalidDataException("Drop50 request bounds");return r;}
 internal static byte[] Reply(Offline70DiscardRequestFG r,bool success,byte[] keys){byte[] b=success?new byte[8]:new byte[]{0,0x13};if(success){b[0]=1;b[1]=r.List;BitConverter.GetBytes(r.Slot).CopyTo(b,2);BitConverter.GetBytes(r.Quantity).CopyTo(b,4);}return Offline70RoleProtocol.BatchPacketFG(1,50,b,keys,false);}
 // Native49C920: scalar30, socket count1, manager state2, option count1,
 // trade flag1 and owner flag1. The two manager readers consume real fields.
 internal static byte[] Ground(Offline70DiscardRequestFG r,Offline70GroundFG d){byte[] b=new byte[36];BitConverter.GetBytes((ushort)1).CopyTo(b,0);BitConverter.GetBytes(r.X).CopyTo(b,2);BitConverter.GetBytes(r.Y).CopyTo(b,4);BitConverter.GetBytes(d.Slot).CopyTo(b,6);BitConverter.GetBytes(d.ItemId).CopyTo(b,8);BitConverter.GetBytes(d.Quantity).CopyTo(b,13);b[35]=1;return b;}
}

class Offline70DeleteRowFG {internal ushort Reason,Slot;internal uint ItemId,Quantity;}
class Offline70DeleteRequestFG {internal byte List;internal Offline70DeleteRowFG[] Rows;}
class Offline70DeleteProtocolFG {
 internal static Offline70DeleteRequestFG Parse(byte[] cipher,byte[] keys){
  if(cipher==null||cipher.Length<16||cipher.Length>4096||cipher.Length%16!=0)throw new InvalidDataException("Delete18 cipher bounds");
  byte[] b=Offline70GoldCipher.Crypt(cipher,keys,4,false);int count=b[1],length=7+12*count;
  if(count==0||count>32||b[0]!=0||length>b.Length||b.Length!=(length+15)/16*16)throw new InvalidDataException("Delete18 list/count bounds");
  if(b[length-1]!=0)throw new InvalidDataException("Delete18 safe flag");for(int i=length;i<b.Length;i++)if(b[i]!=0)throw new InvalidDataException("Delete18 padding");
  Offline70DeleteRequestFG r=new Offline70DeleteRequestFG{List=b[0],Rows=new Offline70DeleteRowFG[count]};
  for(int i=0;i<count;i++){int at=2+12*i;Offline70DeleteRowFG row=new Offline70DeleteRowFG{Reason=BitConverter.ToUInt16(b,at),Slot=BitConverter.ToUInt16(b,at+2),ItemId=BitConverter.ToUInt32(b,at+4),Quantity=BitConverter.ToUInt32(b,at+8)};if(row.Reason!=1||row.Quantity==0||row.Quantity>1000000)throw new InvalidDataException("Delete18 reason/count");for(int j=0;j<i;j++)if(r.Rows[j].Slot==row.Slot)throw new InvalidDataException("Delete18 duplicate slot");r.Rows[i]=row;}return r;
 }
 internal static byte[] Reply(Offline70DeleteRequestFG r,bool success,byte[] keys){byte[] b=success?new byte[3+8*r.Rows.Length]:new byte[]{0,0x13};if(success){b[0]=1;b[1]=r.List;b[2]=(byte)r.Rows.Length;for(int i=0;i<r.Rows.Length;i++){int at=3+8*i;BitConverter.GetBytes(r.Rows[i].Slot).CopyTo(b,at);BitConverter.GetBytes(r.Rows[i].Quantity).CopyTo(b,at+2);BitConverter.GetBytes(r.Rows[i].Reason).CopyTo(b,at+6);}}return Offline70RoleProtocol.BatchPacketFG(1,18,b,keys,false);}
}
