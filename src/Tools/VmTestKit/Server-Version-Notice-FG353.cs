using System;using System.IO;using System.Text;
// Server-originated system line shown in the chat window at world entry. rednf
// GameWorld::notifySystemMessage builds the same packet through make_packet_chat_msg:
// packet id 0 + notify ENUM_NOTIPACKET_MESSAGE (0xc), body {type u8, uid u16,
// serverGroup u8, len i32, GBK text}. Type 0 is the senderless system line; type 11
// (0xb) is the mega-phone class the client groups into its highlighted tab
// (etc/chatsystem.etc page 2: 11 12 13 14 15 38; rednf send_chat_msg case 0xb/0xc/0xe),
// so the line renders as the gold notice. Non-zero types resolve the sender actor by
// uid and crash on uid 0, so uid must be the receiving role's unique id.
// opcode%14=12 keeps the batch MULTI2 cipher, which is what the client's op12 handler uses.
class Offline70ServerVersionNoticeFG353 {
 internal const string Version="v0.5";
 internal const byte ChatType=0xb;
 internal static readonly Encoding TextEncoding=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
 internal static string Text{get{return "服务端版本 "+Version;}}
 internal static byte[] Body(ushort uid,string text){
  if(String.IsNullOrEmpty(text))throw new InvalidDataException("Server notice text");
  byte[] encoded;try{encoded=TextEncoding.GetBytes(text);}catch(EncoderFallbackException){throw new InvalidDataException("Server notice encoding");}
  if(encoded.Length>200)throw new InvalidDataException("Server notice length");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(ChatType);w.Write(uid);w.Write((byte)0);w.Write((uint)encoded.Length);w.Write(encoded);return m.ToArray();}
 }
 internal static byte[] System(string text,ushort uid,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(0,12,Body(uid,text),keys,false);}
}
partial class Offline70BatchServiceFG {
 // One line per world entry, so a player screenshot names the running build.
 void ServerVersionNoticeFG353(){string text=Offline70ServerVersionNoticeFG353.Text;Send("server-version",Offline70ServerVersionNoticeFG353.System(text,role.Id,keys));Note("SERVER_VERSION_NOTICE version="+Offline70ServerVersionNoticeFG353.Version+" type="+Offline70ServerVersionNoticeFG353.ChatType+" uid="+role.Id+" textBytes="+Offline70ServerVersionNoticeFG353.TextEncoding.GetByteCount(text));}
}
