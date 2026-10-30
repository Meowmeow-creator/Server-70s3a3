using System;using System.IO;using System.IO.Compression;using System.Text;using System.Collections.Generic;
// Verified 70 offline roster and the nine-job creation profile (see Role-Create-Jobs-FG202); no town/game initialization.
partial class Offline70RoleProtocol {
 internal static readonly Encoding NameEncoding=Encoding.GetEncoding(936,new EncoderExceptionFallback(),new DecoderExceptionFallback());
 // Advertised account character-slot limit, and the store cap that must agree with it.
 internal const int SlotLimitFG202=0x11;
 // The roster's two header bytes are the account's two DB slot columns, in the order the reference
 // writes them (`DB_Login/_getGameDBCharacView.inc:49-53` loads charac_view.slot_effect_count and
 // charac_slot_limit, `InterDispatcher/dispatch_sig_2.inc:672` hands them to
 // `CUser::setCharacSlotCountInfo(effect, limit)`, and `CUser/make_basic_info.inc:329-330` emits
 // limit-then-effect). What the live client then does with them is measured, not inferred:
 //  1. effect == the number of records in this packet is REQUIRED. 17/17 with four characters, and
 //     12/12 and 17/16 with one, all answer "slot limit reached" without ever sending opcode 5;
 //     17/<roster size> creates normally. So the reference's zero fallback (which sets BOTH columns
 //     to the default) is not a state this client accepts for a populated account.
 //  2. the unlock reveal plays for limit - effect slots, so the animation tracks this first byte,
 //     not the second. Hence both bytes are GM-tunable (`rosterSlotLimit`/`rosterSlotEffect`) and the
 //     shipped pairing stays the measured-working one: limit 17, effect = roster size.
 //  2026-10-25 static pass over DNF70_v50.exe (local-run/patch-roster-reveal-off.py) refined that
 //  model. 0x7FC060 parses the two bytes as (limit, effect) but only keeps pending = limit - effect
 //  (slot-model globals 0x18CBD74/0x18CBD80, no-reveal flag 0x18CBC00 = pending<=0); the raw effect
 //  byte is never stored. The reveal replays on EVERY op2 because the page latch 0x18C827E is
 //  zeroed per packet (0x7FE700). The create-button gate 0x8B8360 toasts string 0x8C when its list
 //  size reaches limit, which explains the grey button at limit 8 with a full roster. Official
 //  closure is op236 (reference process_236.inc: client confirms the reveal, server sets effect =
 //  limit and persists), so the official steady state is pending == 0 — a state no server-only
 //  pairing can combine with creation (the A/B refusals above). Shipped fix: 2-byte client patch
 //  `sub ecx,edx -> xor ecx,ecx` at VA 0x7FC08C forces pending 0 (exact official steady state)
 //  while this pairing stays unchanged.
 internal const int SlotLimitDefaultFG208=SlotLimitFG202;
 internal static int SlotLimitOverrideFG208=SlotLimitDefaultFG208;
 // -1 keeps the effect byte equal to the record count (the only pairing the live client has ever
 // accepted for creating); 0..the limit sends a fixed value for an A/B against the live client.
 internal static int SlotEffectOverrideFG208=-1;
 internal static int SlotEffectCountFG208(int rosterCount){return SlotEffectOverrideFG208<0?rosterCount:SlotEffectOverrideFG208;}
 internal static uint Big(byte[] data,int at){return ((uint)data[at]<<24)|((uint)data[at+1]<<16)|((uint)data[at+2]<<8)|data[at+3];}
 internal static void BigWrite(byte[] data,int at,uint value){data[at]=(byte)(value>>24);data[at+1]=(byte)(value>>16);data[at+2]=(byte)(value>>8);data[at+3]=(byte)value;}
 internal static byte[] Xtea(byte[] data,byte[] allKeys,bool encrypt){
  if(allKeys.Length!=334||data.Length==0||data.Length%8!=0||data.Length>4096)throw new InvalidDataException("XTEA bounds");
  uint[] key=new uint[4];for(int j=0;j<4;j++)key[j]=Big(allKeys,172+j*4);byte[] result=new byte[data.Length];
  unchecked{for(int at=0;at<data.Length;at+=8){uint left=Big(data,at),right=Big(data,at+4),sum=encrypt?0:0xc6ef3720u;
   for(int round=0;round<32;round++){if(encrypt){left+=(((right<<4)^(right>>5))+right)^(sum+key[sum&3]);sum+=0x9e3779b9u;right+=(((left<<4)^(left>>5))+left)^(sum+key[(sum>>11)&3]);}
    else{right-=(((left<<4)^(left>>5))+left)^(sum+key[(sum>>11)&3]);sum-=0x9e3779b9u;left-=(((right<<4)^(right>>5))+right)^(sum+key[sum&3]);}}
   BigWrite(result,at,left);BigWrite(result,at+4,right);
  }}return result;
 }
 sealed class Blowfish {
  uint[] table=(uint[])BlowfishInitial.Clone();
  uint F(uint word){unchecked{return ((table[18+(word>>24)]+table[274+((word>>16)&255)])^table[530+((word>>8)&255)])+table[786+(word&255)];}}
  void Encrypt(ref uint left,ref uint right){unchecked{for(int round=0;round<16;round++){left^=table[round];right^=F(left);uint swap=left;left=right;right=swap;}uint last=left;left=right;right=last;right^=table[16];left^=table[17];}}
  void Decrypt(ref uint left,ref uint right){unchecked{for(int round=17;round>1;round--){left^=table[round];right^=F(left);uint swap=left;left=right;right=swap;}uint last=left;left=right;right=last;right^=table[1];left^=table[0];}}
  internal Blowfish(byte[] keys){
   if(keys.Length!=334)throw new InvalidDataException("Blowfish key bounds");int index=24;
   for(int word=0;word<18;word++){uint value=0;for(int b=0;b<4;b++){value=(value<<8)|keys[index++];if(index==80)index=24;}table[word]^=value;}
   uint left=0,right=0;for(int word=0;word<table.Length;word+=2){Encrypt(ref left,ref right);table[word]=left;table[word+1]=right;}
  }
  internal byte[] Encode(byte[] plain){if(plain.Length==0||plain.Length%8!=0)throw new InvalidDataException("Blowfish block bounds");byte[] result=new byte[plain.Length];for(int at=0;at<plain.Length;at+=8){uint left=Big(plain,at),right=Big(plain,at+4);Encrypt(ref left,ref right);BigWrite(result,at,left);BigWrite(result,at+4,right);}return result;}
  internal byte[] Decode(byte[] encrypted){if(encrypted.Length==0||encrypted.Length%8!=0)throw new InvalidDataException("Blowfish block bounds");byte[] result=new byte[encrypted.Length];for(int at=0;at<encrypted.Length;at+=8){uint left=Big(encrypted,at),right=Big(encrypted,at+4);Decrypt(ref left,ref right);BigWrite(result,at,left);BigWrite(result,at+4,right);}return result;}
 }
 internal static byte[] Pad(byte[] data){byte[] result=new byte[(data.Length+7)&~7];data.CopyTo(result,0);return result;}
 internal static byte[] Zlib(byte[] plain){
  byte[] deflated;using(MemoryStream m=new MemoryStream()){using(DeflateStream writer=new DeflateStream(m,CompressionMode.Compress,true))writer.Write(plain,0,plain.Length);deflated=m.ToArray();}
  byte[] result=new byte[deflated.Length+6];result[0]=0x78;result[1]=0x9c;deflated.CopyTo(result,2);
  uint first=1,second=0;foreach(byte value in plain){first=(first+value)%65521;second=(second+first)%65521;}BigWrite(result,result.Length-4,(second<<16)|first);return result;
 }
 internal static byte[] Packet(byte type,ushort opcode,byte[] encrypted){byte[] packet=new byte[15+encrypted.Length];packet[0]=type;BitConverter.GetBytes(opcode).CopyTo(packet,1);BitConverter.GetBytes(packet.Length).CopyTo(packet,3);encrypted.CopyTo(packet,15);ProbeLoginCandidate.ReceiverGate(packet,0);return packet;}
 internal static byte[] CreateReply(byte[] keys,bool success,byte error){return Packet(1,5,Xtea(Pad(success?new byte[]{1}:new byte[]{0,error}),keys,true));}
 internal static byte[] RosterBody(List<Offline70Role> roles){
  if(roles.Count>SlotLimitFG202)throw new InvalidDataException("Roster limit");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   // Subtype 2, then the account's two slot columns in the order CUser::make_basic_info writes them
   // (reference make_basic_info.inc:329-330): charac_slot_limit first, slot_effect_count second.
   // See SlotEffectOverrideFG208 for why the shipped pairing is limit == effect, not the roster size.
   w.Write((byte)2);w.Write((byte)SlotLimitOverrideFG208);w.Write((byte)SlotEffectCountFG208(roles.Count));w.Write((ushort)roles.Count);
   foreach(Offline70Role role in roles){byte[] name=NameEncoding.GetBytes(role.Name);if(role.Id==0||!Offline70RoleCreateJobsFG202.CreatableFG202(role.Job)||!Offline70MonsterExperience.SupportedLevel(role.Level)||name.Length==0||name.Length>29)throw new InvalidDataException("Unverified role profile");
    // Native 7FD4B0 stores these in the two name-status vectors; they are not channel IDs.
    w.Write(role.Id);w.Write((uint)name.Length);w.Write(name);w.Write((byte)0);w.Write((byte)0);
    w.Write(role.Job);w.Write((byte)0);w.Write(role.Level);w.Write((byte)0);
    w.Write(new byte[3]);w.Write(new byte[8]);w.Write((byte)0);w.Write((uint)0);w.Write(new byte[3]);w.Write((byte)3);w.Write(new byte[2]);
   }return m.ToArray();
  }
 }
 internal static byte[] RosterReply(byte[] keys,List<Offline70Role> roles){byte[] plain=RosterBody(roles);if(plain.Length>1024)throw new InvalidDataException("Roster body bounds");return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(plain))));}
 internal static Offline70Role ParseCreate(byte[] encrypted,byte[] keys){
  byte[] plain=Xtea(encrypted,keys,false);if(plain.Length<5)throw new InvalidDataException("Create request length");uint length=BitConverter.ToUInt32(plain,1);
  // Body is slot(u8)+nameLength(u32)+name, matching MSG_CREATE_CHARACTER (+0xd slot, +0xe length,
  // +0x12 name). The slot IS the native job enum: reference process_5.inc indexes m_characList by
  // it and never reads a job field from the wire. Slots 9 and 10 have no .chr in this client build.
  // Everything after the name is buffer residue: the native copies a fixed 0x1e name field with
  // strncpy and never inspects the tail, so it must not be required to be zero (a three-character
  // name is legitimate and would otherwise be rejected).
  if(!Offline70RoleCreateJobsFG202.CreatableFG202(plain[0])||length<1||length>29||5+length>plain.Length||plain.Length>5+30+7)throw new InvalidDataException("Create slot "+plain[0]+" is not a creatable job");
  string name=NameEncoding.GetString(plain,5,(int)length);if(name.Length==0)throw new InvalidDataException("Empty name");foreach(char character in name)if(!Char.IsLetterOrDigit(character)&&character!='_')throw new InvalidDataException("Unsupported name characters");
  return new Offline70Role{Job=plain[0],Level=1,Name=name,CreatedUtc=DateTime.UtcNow.ToString("o")};
 }
}
public class Offline70Role {public ushort Id;public string Name;public byte Job;public byte Level;public string CreatedUtc;}
public class Offline70RoleStore {public int Format=1;public int AccountId=1;public List<Offline70Role> Roles=new List<Offline70Role>();}
