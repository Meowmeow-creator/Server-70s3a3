using System;using System.IO;using System.Xml;using System.Text;
// FG366 test-only channel: the death-god tower lines are proven to be unreachable through the
// client's own tower UIC (four live send orders + live-memory forensics, HANDOVER-2026-10-27 §3), so
// the line has to ride a notify that draws a text window by itself. Which of the ≤368 handlers does
// that, and with which body layout, is only answerable on screen, and every round must not need a
// rebuild - this file therefore exposes one GM op that emits an arbitrary notify with a chosen body
// template (or literal hex) for the running client. It writes no save state and no quest/amount.
internal static class Offline70DialogProbeFG366 {
 static readonly Encoding Text=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
 // Every template carries the same GBK text; the shapes below are the layouts the client's own
 // chat/story/server-message handlers plausibly read (op12 body, uid-first, length-prefixed,
 // rednf CSharedServerMessageManager::SendServerMessageInfo double text).
 internal static byte[] Body(string template,string text){
  byte[] encoded=GbkFG366(text);
  if(template=="chat")return Combine(new byte[]{0},Word(0),new byte[]{0},Dword(encoded.Length),encoded);
  if(template=="uid-len")return Combine(Word(0),Dword(encoded.Length),encoded);
  if(template=="len-first")return Combine(Dword(encoded.Length),encoded);
  if(template=="uid-word-len")return Combine(Word(0),Word(encoded.Length),encoded);
  if(template=="uid-type-word-len")return Combine(Word(0),new byte[]{0},Word(encoded.Length),encoded);
  if(template=="uid-type-dword-len")return Combine(Word(0),new byte[]{0},Dword(encoded.Length),encoded);
  if(template=="server-message")return Combine(new byte[]{1,1,0},Dword(encoded.Length),encoded,Dword(0));
  if(template=="server-message-title")return Combine(new byte[]{1,1,0},Dword(6),GbkFG366("死神："),Dword(encoded.Length),encoded);
  if(template=="all-zero")return new byte[64];
  throw new InvalidDataException("未知的对话框探针模板 "+template);
 }
 internal static byte[] Hex(string raw){
  string s=raw.Replace(" ","").Replace("-","");
  if(s.Length%2!=0||s.Length>2048)throw new InvalidDataException("探针 hex 必须是 1..1024 字节的十六进制");
  byte[] b=new byte[s.Length/2];
  for(int i=0;i<b.Length;i++){int hi=NibbleFG366(s[2*i]),lo=NibbleFG366(s[2*i+1]);b[i]=(byte)((hi<<4)|lo);}
  return b;
 }
 static int NibbleFG366(char c){
  if(c>='0'&&c<='9')return c-'0';
  if(c>='a'&&c<='f')return c-'a'+10;
  if(c>='A'&&c<='F')return c-'A'+10;
  throw new InvalidDataException("探针 hex 含非十六进制字符 "+c);
 }
 internal static bool KnownTemplate(string template){
  string[] names=new string[]{"chat","uid-len","len-first","uid-word-len","uid-type-word-len","uid-type-dword-len","server-message","server-message-title","all-zero"};
  for(int i=0;i<names.Length;i++)if(names[i]==template)return true;
  return false;
 }
 static byte[] GbkFG366(string s){
  try{return Text.GetBytes(s);}catch(EncoderFallbackException){throw new InvalidDataException("探针文本不能编码为 GBK");}
 }
 static byte[] Word(int v){return new byte[]{(byte)v,(byte)(v>>8)};}
 static byte[] Dword(int v){return new byte[]{(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)};}
 static byte[] Combine(params byte[][] parts){
  int total=0;for(int i=0;i<parts.Length;i++)total+=parts[i].Length;
  byte[] all=new byte[total];int at=0;
  for(int i=0;i<parts.Length;i++){Buffer.BlockCopy(parts[i],0,all,at,parts[i].Length);at+=parts[i].Length;}
  return all;
 }
}
partial class Offline70BatchServiceFG {
 // opcode <= 368 is the client's dispatch ceiling (cmp eax,0x171 / ja default), so a request above
 // it would be silently dropped; the probe refuses instead of reporting a send that cannot arrive.
 string GmDialogProbeFG366(string receipt,XmlElement e){
  if(!GmLiveFG201||!selected)throw new InvalidDataException("对话框探针需要客户端已经连接并进入游戏，否则看不到画面。");
  uint op=GmNumberFG201(e,"opcode",1,368);
  string hex=e.GetAttribute("hex"),template=e.GetAttribute("template");
  string text=e.GetAttribute("text");if(text=="")text="死神：对话框探针 op"+op;
  byte[] body;
  if(hex.Length!=0)body=Offline70DialogProbeFG366.Hex(hex);
  else{
   if(template=="")template="chat";
   if(!Offline70DialogProbeFG366.KnownTemplate(template))throw new InvalidDataException("未知的对话框探针模板 "+template+"（可用：chat、uid-len、len-first、uid-word-len、uid-type-word-len、uid-type-dword-len、server-message、server-message-title、all-zero，或直接给 hex）。");
   body=Offline70DialogProbeFG366.Body(template,text);
  }
  Send("gm-dialog"+op,Offline70RoleProtocol.BatchPacketFG(0,(ushort)op,body,keys,false));
  Note("GM_DIALOG_PROBE receipt="+receipt+" op="+op+" template="+(hex.Length!=0?"hex":template)+" bodyBytes="+body.Length+" text="+text);
  return "已发送 op"+op+"（"+(hex.Length!=0?"hex":template)+"，体 "+body.Length+" 字节，密文索引 "+(op%14)+"）："+text;
 }
}
