using System;using System.IO;
// Fixed quest101 offline profile. Native algorithm8 vectors and reader format verified separately.
class Offline70QuestFinish {
 static void CheckKeys(byte[] keys){if(keys.Length!=334)throw new InvalidDataException("Finish key bounds");for(int i=0;i<keys.Length;i++)if(keys[i]!=(byte)i)throw new InvalidDataException("Finish profile requires fixed offline keys");}
 static byte[] Hex(string hex){byte[] b=new byte[hex.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(hex.Substring(i*2,2),16);return b;}
 internal static byte[] RequestPlain(){return Hex("24006500ffff01000000000000000000");}
 internal static byte[] RequestEncrypted(){return Hex("cfe01c82792064dc9c6699936ef6d95d");}
 internal static ushort Parse(byte[] body,byte[] keys){CheckKeys(keys);byte[] expected=RequestEncrypted();if(body.Length!=expected.Length)throw new InvalidDataException("Finish request bounds");for(int i=0;i<body.Length;i++)if(body[i]!=expected[i])throw new InvalidDataException("Only captured quest101 / NPC1 / no chosen reward finish profile supported");return 101;}
 internal static byte[] Body(bool success){return success?new byte[]{1,101,0,0,0,0,0,0,0,0,0}:new byte[]{0,0x16};}
 internal static byte[] Reply(byte[] keys,bool success){CheckKeys(keys);return Offline70RoleProtocol.Packet(1,36,Hex(success?"19df7327f138d1812b67dc01f816c5ec":"d0f1b462ad67f0695905719e8e107ad0"));}
 internal static byte[] GoldBody(uint gold){
  if(gold>999999999)throw new InvalidDataException("Offline gold balance bounds");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)0);w.Write((ushort)1); // Inventory list0, one row.
   w.Write((ushort)0);w.Write((uint)0);w.Write(gold);
   w.Write((byte)0);w.Write((ushort)0);w.Write((byte)0);
   w.Write((uint)0);w.Write((byte)0);w.Write((ushort)0);
   w.Write((byte)0); // Native 48A8A0 reads an empty socket list at offset24.
   w.Write((uint)0);w.Write((byte)0); // Period and additional u32 list count.
   w.Write((ushort)0); // Native 40B1F0 reads this field at offset30.
   w.Write((byte)0); // Native C7E980 reads an empty list at offset32.
   w.Write((byte)0); // Final row state at offset33.
   return m.ToArray();
  }
 }
 internal static byte[] GoldReply(byte[] keys,uint gold){CheckKeys(keys);byte[] body=GoldBody(gold),b=new byte[(body.Length+3)&~3];body.CopyTo(b,0);for(int i=0;i<b.Length;i++)b[i]^=keys[4+(i%4)];return Offline70RoleProtocol.Packet(0,14,b);}
 // Full list13 restores inventory silently. The u16 field is bag expansion stage
 // (0 = no expansion), followed by the same gold row as update14. This bounded
 // profile contains only virtual gold; extend it with all saved rows when items exist.
 internal static byte[] GoldRestoreBody(uint gold){byte[] row=GoldBody(gold),body=new byte[row.Length+2];body[0]=row[0];Array.Copy(row,1,body,3,row.Length-1);return body;}
 internal static byte[] GoldRestoreReply(byte[] keys,uint gold){CheckKeys(keys);byte[] body=GoldRestoreBody(gold),pad=new byte[(body.Length+15)&~15];body.CopyTo(pad,0);return Offline70RoleProtocol.Packet(0,13,Offline70GoldCipher.Crypt(pad,keys,13,true));}
 internal static byte[] HistoryBody(bool completed){byte[] b=new byte[106];BitConverter.GetBytes((uint)102).CopyTo(b,0);if(completed)b[4+101]=1;return b;}
 internal static byte[] HistoryReply(byte[] keys,bool completed){CheckKeys(keys);byte[] b=HistoryBody(completed),pad=new byte[(b.Length+15)&~15];b.CopyTo(pad,0);return Offline70RoleProtocol.Packet(0,365,ProbeLoginCandidate.Crypt(pad,keys,true));}
}
