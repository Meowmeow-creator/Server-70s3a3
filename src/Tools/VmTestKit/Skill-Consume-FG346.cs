using System;
using System.IO;
using System.Collections.Generic;
// FG346: 无色小晶块消耗（技能 [consume item]）。
//
// PVF 的 .skl 里凡是带 [consume item] 段的技能，都在使用该技能时扣无色小晶块：
//   [consume item]
//   	3037	2	2
// 三列 = 物品 id、1 级数量、满级数量（3037 的行两列恒等，所以不需要按技能等级插值）。
// 同一段可以有多行（召唤系三条 3/1/1），客户端逐行扣除，因此这里取行和。
//
// 客户端权威的那一侧是施放本身：CMD 41 USE_SKILL（rednf DisPatcher_UseSkill/read 0x081cad6d：
// {u8 gap, u8 skill, u16 target, ...}；服务端 process 只做槽位合法性统计和队伍广播，不碰物品），
// 所以扣无色只能由离线服务端自己记账。这里在 op41 落账，并把变化行用 op14 通知推回客户端，
// 客户端本地扣没扣都会收敛到存档里的数量。
//
// 表由 local-run/pvf 的 9 份 skill/<job>skill.lst × 各自 .skl 的 [consume item] 段生成
// （2026-10-26，222 条技能）：展平成 (技能 id, 无色数量) 对。
class Offline70SkillCubeFG346 {
 internal const uint ClearCube=3037;
 internal static readonly byte[][] Skills=new byte[][]{
  new byte[]{9,1,22,1,24,1,36,1,44,1,57,1,60,1,62,1,72,1,73,2,74,2,75,1,76,1,79,1,81,2,82,2,85,5,86,5,87,5,88,7,95,2,96,1,97,2,98,1,99,2,100,1,101,2,102,1,103,1}, // 0 鬼剑士
  new byte[]{15,1,16,1,18,1,19,1,52,1,63,5,64,5,67,5,68,1,71,5,74,1,76,1,77,2,79,2,82,2,83,1,88,2,89,1,90,2,99,1,100,2,101,1,102,1,103,2,104,2,105,2,106,1}, // 1 格斗家
  new byte[]{9,1,15,1,43,1,45,1,51,2,53,1,54,5,59,2,63,1,64,5,67,5,70,5,71,2,72,1,75,1,77,1,78,2,83,2,101,2,102,3,103,2,104,3,105,2,106,3,107,2,108,3}, // 2 神枪手
  new byte[]{10,1,27,1,46,5,47,5,49,1,55,1,58,2,59,2,66,1,68,2,69,2,74,5,81,2,82,7,83,7,103,1,104,2,105,2,106,1,109,5,111,2,112,3,113,3,114,2,115,2,117,2,119,2,120,2,121,2,122,2}, // 3 魔法师
  new byte[]{4,2,5,1,21,2,24,1,26,2,28,1,35,1,37,1,40,1,45,3,49,1,69,1,74,1,75,1,81,2,84,5,88,2,90,1,93,5,101,2,103,2,104,2,105,2,108,2,109,1,110,1,117,1,119,5,125,2,133,1,134,2}, // 4 圣职者
  new byte[]{9,1,15,1,43,1,45,1,51,2,53,1,59,2,63,1,71,2,72,1,75,1,77,1,78,2,83,2,96,5,97,5,98,5,100,7,101,2,102,3,103,2,104,3,105,2,106,3,107,2,108,3}, // 5 女神枪手
  new byte[]{33,1,34,1,37,2,39,1,40,2,45,1,47,2,55,1,57,7,59,5,74,2,75,3,76,2,77,1}, // 6 盗贼
  new byte[]{15,1,16,1,18,1,19,1,41,5,63,5,67,5,68,1,74,1,76,1,77,2,79,2,82,2,83,1,88,2,89,1,90,2,105,5,106,1,117,1,118,1,119,1,120,2,121,2,122,2,123,2,124,2}, // 7 女格斗家
  new byte[]{12,1,13,2,24,1,29,1,30,2,32,1,48,5,49,5,60,1,61,1,62,2,63,2}, // 8 女魔法师
 };
 internal static int Cost(byte job,int skill){
  if(job>=Skills.Length||skill<=0||skill>255)return 0;
  byte[] table=Skills[job];
  for(int i=0;i<table.Length;i+=2)if(table[i]==skill)return table[i+1];
  return 0;
 }
 // op41 的密文块是 16 字节（41%14=13 → Anubis 族），明文 {u8 gap, u8 skill, u16 target, ...}。
 internal static int Parse(byte[] body,byte[] keys,out ushort target){
  target=0;byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,41%14,false);
  if(plain.Length<4)throw new InvalidDataException("UseSkill41 body bounds");
  target=BitConverter.ToUInt16(plain,2);
  return plain[1];
 }
 internal static int Owned(Offline70EconomyState s){
  int total=0;foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&e.ItemId==ClearCube)total+=e.Quantity>Int32.MaxValue?Int32.MaxValue:(int)e.Quantity;
  return total;
 }
 // 从背包（list 0）的若干堆里按槽位顺序扣，返回实际扣到的数量。
 // 客户端在施放前已经检查过数量；万一存档比客户端少，只扣现有的，不把施放判死。
 internal static int Take(Offline70EconomyState s,int cost){
  if(cost<=0)return 0;
  List<Offline70EconomyEntry> stacks=new List<Offline70EconomyEntry>();
  foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&e.ItemId==ClearCube)stacks.Add(e);
  stacks.Sort(delegate(Offline70EconomyEntry a,Offline70EconomyEntry b){return a.Slot.CompareTo(b.Slot);});
  int left=cost;
  foreach(Offline70EconomyEntry e in stacks){
   if(left<=0)break;
   int take=(int)Math.Min((uint)left,e.Quantity);e.Quantity-=(uint)take;left-=take;
   if(e.Quantity==0)s.Inventory.Remove(e);
  }
  return cost-left;
 }
 // op14（notify 14）增量行：slot + itemId + 数量 + 期限……与 InventoryChangeBody 同形，
 // 这里只发无色这一行，用于客户端主动发过 op47 使用包、服务端却没有真实变化的情况。
 internal static byte[] UpdateReply(Offline70EconomyState s,ushort fallbackSlot,byte[] keys){
  Offline70EconomyEntry row=null;
  foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&e.ItemId==ClearCube&&(row==null||e.Slot<row.Slot))row=e;
  byte[] encoded;
  if(row==null){encoded=new byte[31];BitConverter.GetBytes(fallbackSlot).CopyTo(encoded,0);BitConverter.GetBytes(UInt32.MaxValue).CopyTo(encoded,2);}
  else encoded=Offline70EconomyProtocol.InventoryRow(row);
  byte[] body=new byte[3+encoded.Length];BitConverter.GetBytes((ushort)1).CopyTo(body,1);encoded.CopyTo(body,3);
  return Offline70EconomyProtocol.UpdatePacket(body,keys);
 }
}
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> skillUseFG346=new Dictionary<ushort,string>();
 // CMD 41 USE_SKILL：扣本技能 [consume item] 的无色并把结果推回客户端。
 void SkillUseFG346(byte[] body,ushort sequence){
  int skill;ushort target;
  try{skill=Offline70SkillCubeFG346.Parse(body,keys,out target);}
  catch(InvalidDataException e){Note("SKILL_USE_REJECTED reason="+e.Message);return;}
  int cost=Offline70SkillCubeFG346.Cost(role.Job,skill);
  if(cost<=0){Note("SKILL_USED job="+role.Job+" skill="+skill+" target="+target+" cube=0");return;}
  string fingerprint=Convert.ToBase64String(body),prior;
  if(skillUseFG346.TryGetValue(sequence,out prior)&&prior==fingerprint){Note("SKILL_CUBE_REPEAT seq="+sequence+" skill="+skill);return;}
  skillUseFG346[sequence]=fingerprint;
  if(playerDeadFG117){Note("SKILL_CUBE_SKIPPED dead=True skill="+skill);return;}
  int owned=Offline70SkillCubeFG346.Owned(assets);
  if(owned==0){Note("SKILL_CUBE_SHORT job="+role.Job+" skill="+skill+" cost="+cost+" owned=0");return;}
  Offline70EconomyState before=assets;
  try{
   bool duplicate;
   int[] taken={0};
   assets=Offline70Economy.Apply(economyPath,session+"-skill-cube-"+sequence,"skill-cube",role.Job+":"+skill+":"+cost,delegate(Offline70EconomyState s){taken[0]=Offline70SkillCubeFG346.Take(s,cost);},out duplicate);
   Sync();
   if(duplicate){Note("SKILL_CUBE_DUPLICATE seq="+sequence+" skill="+skill);return;}
   Send("skill-cube-inventory",Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys));
   Note("SKILL_CUBE_USED job="+role.Job+" skill="+skill+" target="+target+" cost="+cost+" taken="+taken[0]+" remaining="+Offline70SkillCubeFG346.Owned(assets));
  }catch(InvalidDataException e){Note("SKILL_CUBE_REJECTED skill="+skill+" reason="+e.Message);}
 }
 // CMD 47 USE_STACKABLE 指向无色：说明客户端把这枚无色当成"技能消耗"发过来了。
 // 扣账已经在 op41 落过，这里只回成功并把权威数量推回去，避免同一枚扣两次。
 void SkillCubeConfirmFG346(Offline70EconomyUseRequest r){
  Send("item-used",Offline70EconomyProtocol.UseReply(r,true,keys));
  Send("skill-cube-inventory",Offline70SkillCubeFG346.UpdateReply(assets,r.Slot,keys));
  Note("SKILL_CUBE_CONFIRM slot="+r.Slot+" remaining="+Offline70SkillCubeFG346.Owned(assets));
 }
}
