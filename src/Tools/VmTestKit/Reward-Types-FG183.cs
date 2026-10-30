using System;using System.IO;using System.Collections.Generic;
// FG183: the four quest reward kinds the engine still refuses, isolated as pure builders + tables.
// Wire evidence is the original server's CUser::quest_reward (reference/rednf/remake/source/DNFServer/
// GameServer/Game/CUserOnlyOrig/quest_reward.inc, lines 239-462). It writes the reward-type byte at
// the exact slot our Offline70QuestWireFG.FinishRewardBody leaves as 0 (ack.put_byte(q->m_fieldB8)),
// then per kind: 1 [grow type] -> grow byte + two skill banks (set_grow_type(&ack)); 2 [awakening
// type] -> second-grow byte + two skill banks; 3 [hell challenge] -> nothing; 0x14 [expert job] ->
// expert byte + two banks; 0x15 [hero] -> u16 dungeon id, plus a second op5 packet [1][dungeon][4].
// Our kind byte values are the same numbers STQuestScript.cpp assigns in its `[reward type]` parser
// (1 grow / 2 awakening / 3 hell challenge / 0x14 expert job / 0x15 hero).
class Offline70RewardTypesFG183 {
 // First-awakening skill per (job,grow): character/<job>/<job>.chr `[growtype N]` blocks, N = grow+1
 // (chr growtype 1 is the base class: chr [growtype name] = base, 剑魂/鬼泣/狂战士/阿修罗 as gt2..gt5,
 // matching skill/swordmanskill.lst: gt2 weaponmasteryup/equiplightsword/armormasterylightwm = 剑魂).
 // `[awakening 1] [awakening skill]` value returned here is the skill id; level is always 1
 // (AwakeningSkillLevel). Re-measured 2026-10-17 over the running PVF HTTP API: swordman
 // [growtype 2..5][awakening 1][awakening skill] = 91 1 / 89 1 / 90 1 / 92 1, and the same four
 // blocks' [skill] rows equal AutoFG139[1..4] one for one (27,33,37,172,8,25,65,20,94,197 /
 // 35,29,187,8,25,65,20,93,197 / 56,34,181x2,182x2,8,25,65,20,76,197 / 47,55,61,8,25,65,20,197),
 // which is the proof that chr [growtype N] carries grow N-1. Row 0 (swordman) is the only row
 // this engine could serve: Offline70SkillSlots pinned the swordman catalog and s.Job/role.Job never
 // left 0, so AwakeningSkill(0,grow) was what a grant used. The nine-row AwakeningFG202 now answers
 // and these six rows sit at job-enum rows 0,1,2,3,4,6 (recon/job-advancement-report.md §2), so AwakeningSkill(0,grow) is what a grant uses
 // and rows 1..5 are the standby tables of the other five character scripts (also re-measured:
 // fighter 72/70/65/66, gunner 66/65/68/69, mage 73/75/84/110, priest 86/87/89/122, thief 56/58
 // - all at level 1, all with an empty [awakening 2] skill row).
 // 0 = the client script grants nothing for that pair (thief gt4/gt5 have empty awakening lists).
 internal static readonly byte[][] AwakeningTable=new byte[][]{
  new byte[]{0,91,89,90,92},   // swordman : skill/swordman/{defensedownoncritical,darkaura,bleedingmastery,stuckuponcounter}.skl
  new byte[]{0,72,70,65,66},   // fighter
  new byte[]{0,66,65,68,69},   // gunner
  new byte[]{0,73,75,84,110},  // mage
  new byte[]{0,86,87,89,122},  // priest
  new byte[]{0,56,58,0,0}};    // thief (female): gt4/gt5 `[awakening skill]` empty in the CN70 script
 // Expert job table: character/expertjob.lst (1 enchanter / 2 alchemist / 3 disjointer / 4 doll) and
 // each character/expertjob/*.exj `[clear quests]` + `[skill]` (single skill, level 1).
 internal static readonly byte[] ExpertJobSkills=new byte[]{191,193,194,192};     // index expertType-1
 internal static readonly ushort[] ExpertJobQuests=new ushort[]{2702,2708,2710,2712};
 // [grow type]/[awakening type] grow byte packs first grow in bits0..3 and the awakening level in
 // bits4..6 (rednf Game/CUserCharacInfo.h `union GrowTypeBits` + CUser/set_grow_type.inc).
 internal static byte EncodeGrow(byte first,byte second){if(first>5||second>2)throw new InvalidDataException("Grow type field bounds");return (byte)(first|second<<4);}
 internal static byte FirstGrow(byte combined){return (byte)(combined&0x0F);}
 internal static byte SecondGrow(byte combined){return (byte)(combined>>4&0x07);}
 // A grow byte the engine accepts: first grow 1..5 in bits0..3 (the CN70 trees use 1..5) and
 // the first awakening level 0..1 in bits4..6. Zero itself means 'not promoted' and is only
 // legal where a caller explicitly treats zero as unset.
 internal static bool IsGrowByte(byte value){byte first=FirstGrow(value);return first>=1&&first<=5&&SecondGrow(value)<=1;}
 // FG204: which (job, first grow) branches the CN70 content actually carries, and what they are called
 // in Chinese. The economy/progression validators need the first half (a grow byte is only legal for the
 // job that owns the branch) and the GM console needs both, so the table lives here rather than in the
 // console file - a pinned-down validator and a permissive GM prompt must never disagree.
 // Source is data/quest-catalog.xml: the "转职 - X (n/m)" rows with JobChange=true carry the branch in
 // [grow type] (剑魂806/鬼泣803/狂战士809/阿修罗834、气功师7817/散打7820/街霸7824/柔道家7827、
 // 漫游枪手7831/枪炮师7834/机械师7837/弹药专家7840、元素师7842/召唤师7845/战斗法师7848/魔道学者7851、
 // 圣骑士7855/蓝拳圣使7859/驱魔师7862/复仇者7866、女枪手 7870/7873/7876/7879、刺客7882/死灵术士7886、
 // 男格斗 7889/7892/7895/7898、元素爆破师4065/冰结师4068); the settlement rows of the "觉醒 - X (n/4)"
 // chains (RewardSecondGrow=1) carry the awakening name at their own [grow type] (剑圣977/弑魂984/
 // 狱血魔神991/大暗黑天998、百花缭乱917/武神922/毒王912/暴风眼906、枪神933/狂暴者928/机械战神939/大将军945、
 // 大魔导师951/月之女皇960/贝亚娜斗神970/魔术师5353、天启者5310/神之手5317/龙斗士5324/末日审判者159、
 // 沾血蔷薇5332/重炮掌控者5336/机械之心5343/战争女神5347、银月5681/灵魂收割者5687、狂虎帝7615/武极7621/
 // 千手罗汉7631/风林火山7637、魔皇4085/冰冻之心4089).
 // Row order is the native job enum (local-run/recon/role-create-jobs-report.md §2, same order as
 // Role-Create-Jobs-FG202.cs InitialStatsFG202). Empty cell = this client build has no such branch: 暗夜使者
 // and 男魔法师 stop at grow 2 even though their .chr carries growtype 4/5 rows, and no JobChange quest
 // in the whole catalogue ever grants grow 5, so index 5 is absent by design.
 internal static readonly string[][] AdvancementNamesFG204=new string[][]{
  new string[]{"","剑魂","鬼泣","狂战士","阿修罗"},         // 0 swordman 鬼剑士
  new string[]{"","气功师","散打","街霸","柔道家"},          // 1 fighter 格斗家
  new string[]{"","漫游枪手","枪炮师","机械师","弹药专家"},    // 2 gunner 神枪手
  new string[]{"","元素师","召唤师","战斗法师","魔道学者"},    // 3 mage 魔法师
  new string[]{"","圣骑士","蓝拳圣使","驱魔师","复仇者"},      // 4 priest 圣职者
  new string[]{"","漫游枪手","枪炮师","机械师","弹药专家"},    // 5 at gunner 女枪手
  new string[]{"","刺客","死灵术士","",""},               // 6 thief 暗夜使者
  new string[]{"","气功师","散打","街霸","柔道家"},          // 7 at fighter 男格斗家
  new string[]{"","元素爆破师","冰结师","",""}};            // 8 at mage 男魔法师
 internal static readonly string[][] AwakeningNamesFG204=new string[][]{
  new string[]{"","剑圣","弑魂","狱血魔神","大暗黑天"},       // 0 swordman
  new string[]{"","百花缭乱","武神","毒王","暴风眼"},         // 1 fighter
  new string[]{"","枪神","狂暴者","机械战神","大将军"},       // 2 gunner
  new string[]{"","大魔导师","月之女皇","贝亚娜斗神","魔术师"},  // 3 mage
  new string[]{"","天启者","神之手","龙斗士","末日审判者"},     // 4 priest
  new string[]{"","沾血蔷薇","重炮掌控者","机械之心","战争女神"}, // 5 at gunner
  new string[]{"","银月","灵魂收割者","",""},               // 6 thief
  new string[]{"","狂虎帝","武极","千手罗汉","风林火山"},      // 7 at fighter
  new string[]{"","魔皇","冰冻之心","",""}};               // 8 at mage
 internal static string AdvancementNameFG204(byte job,byte first){return job<AdvancementNamesFG204.Length&&first<AdvancementNamesFG204[job].Length?AdvancementNamesFG204[job][first]:"";}
 internal static string AwakeningNameFG204(byte job,byte first){return job<AwakeningNamesFG204.Length&&first<AwakeningNamesFG204[job].Length?AwakeningNamesFG204[job][first]:"";}
 // Grow 0 is the un-promoted base class, which every creatable job has.
 internal static bool HasAdvancementFG204(byte job,byte first){return first==0||AdvancementNameFG204(job,first).Length!=0;}
 // Awakening skills are indexed by the native job enum, so the nine-row generated table answers;
 // AwakeningTable above stays as the hand-measured six-family baseline that
 // local-run/pvf/gen_role_create_jobs.py guards row for row before it will write.
 internal static byte AwakeningSkill(byte job,byte grow){if(job>=Offline70RoleCreateJobsFG202.CreatableJobsFG202||grow<1||grow>4)return 0;return Offline70RoleCreateJobsFG202.AwakeningFG202[job][grow];}
 // Level the character scripts attach to every awakening skill row. PVF measured 2026-10-17: all
 // 24 non-empty [awakening 1][awakening skill] rows of the six CN70 character scripts are (id,1);
 // no script grants an awakening skill at another level, and no awakening skill comes from the
 // client side (the server writes it in set_grow_type's second-grow branch, see Promotion-Skills-FG139).
 internal const byte AwakeningSkillLevel=1;
 // The skill a grow byte actually grants: only a byte that carries the awakening nibble does,
 // because the original writes the awakening skill inside the [awakening type] reward branch
 // (quest_reward.inc b8==2 -> set_grow_type(firstGrow, 1)). A bare first grow (1..4) and 0 both
 // map to 0 = nothing to grant, which is what Offline70BatchServiceFG.Sync and the probe check.
 internal static byte AwakeningSkillFor(byte job,byte growByte){return SecondGrow(growByte)==0?(byte)0:AwakeningSkill(job,FirstGrow(growByte));}
 internal static byte ExpertJobSkill(byte expertType){if(expertType<1||expertType>4)throw new InvalidDataException("Expert job type bounds");return ExpertJobSkills[expertType-1];}
 internal static ushort ExpertJobQuest(byte expertType){if(expertType<1||expertType>4)throw new InvalidDataException("Expert job type bounds");return ExpertJobQuests[expertType-1];}
 // The original writes the two skill banks of set_grow_type(&ack): bank0 = tree0 count + (slot,id,level)
 // triples, bank1 = tree1. Our promotion path (Offline70JobAdvancementFG91.Body) already ships that
 // exact triple shape with an empty second bank, so the awakening/expert replies reuse it. CN70 has a
 // single skill tree, so bank1 is the empty second bank the promotion fixture still expects.
 internal static byte[] Bank(Offline70SkillSlotState skills){
  Offline70SkillSlots.Validate(skills);byte count=checked((byte)Offline70SkillSlots.VisibleCountFG139(skills));
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write(count);for(int i=0;i<skills.Ids.Length;i++){if(skills.Levels[i]==0)continue;w.Write(skills.Slots[i]);w.Write(skills.Ids[i]);w.Write(skills.Levels[i]);}
   w.Write((byte)0);return m.ToArray();}
 }
 // Shared head: the success header, gold and consumed rows. The reward-type byte must sit where
 // FinishRewardBody put its 0 and no received row may exist (mirrors the promotion guard verbatim).
 static byte[] Head(ushort id,uint gold,Offline70EconomyState before,Offline70EconomyState after){
  byte[] item=Offline70QuestWireFG.FinishRewardBody(id,gold,before,after);int prefix=9+item[8]*7;
  if(item[prefix]!=0||item[prefix+1]!=0)throw new InvalidDataException("Mixed item reward kind");
  byte[] head=new byte[prefix];Array.Copy(item,head,prefix);return head;
 }
 public const byte KindAwakening=2,KindHellChallenge=3,KindExpertJob=0x14,KindHero=0x15;
 internal static byte[] AwakeningBody(ushort id,uint gold,byte secondGrow,Offline70EconomyState before,Offline70EconomyState after,byte[] bank){
  if(secondGrow!=1)throw new InvalidDataException("Only the first awakening is supported");if(bank==null||bank.Length<1)throw new InvalidDataException("Awakening skill bank");
  byte[] head=Head(id,gold,before,after);using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(head);w.Write(KindAwakening);w.Write(secondGrow);w.Write(bank);return m.ToArray();}
 }
 internal static byte[] HellChallengeBody(ushort id,uint gold,Offline70EconomyState before,Offline70EconomyState after){
  byte[] head=Head(id,gold,before,after);using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(head);w.Write(KindHellChallenge);return m.ToArray();}
 }
 internal static byte[] ExpertJobBody(ushort id,uint gold,byte expertType,Offline70EconomyState before,Offline70EconomyState after,byte[] bank){
  ExpertJobSkill(expertType);if(bank==null||bank.Length<1)throw new InvalidDataException("Expert job skill bank");
  byte[] head=Head(id,gold,before,after);using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(head);w.Write(KindExpertJob);w.Write(expertType);w.Write(bank);return m.ToArray();}
 }
 internal static byte[] HeroBody(ushort id,uint gold,ushort dungeon,Offline70EconomyState before,Offline70EconomyState after){
  if(dungeon==0)throw new InvalidDataException("Hero dungeon bounds");
  byte[] head=Head(id,gold,before,after);using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(head);w.Write(KindHero);w.Write(dungeon);return m.ToArray();}
 }
 internal static byte[] AwakeningReply(ushort id,uint gold,byte secondGrow,Offline70EconomyState before,Offline70EconomyState after,byte[] bank,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,36,AwakeningBody(id,gold,secondGrow,before,after,bank),keys,false);}
 internal static byte[] HellChallengeReply(ushort id,uint gold,Offline70EconomyState before,Offline70EconomyState after,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,36,HellChallengeBody(id,gold,before,after),keys,false);}
 internal static byte[] ExpertJobReply(ushort id,uint gold,byte expertType,Offline70EconomyState before,Offline70EconomyState after,byte[] bank,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,36,ExpertJobBody(id,gold,expertType,before,after,bank),keys,false);}
 internal static byte[] HeroReply(ushort id,uint gold,ushort dungeon,Offline70EconomyState before,Offline70EconomyState after,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(1,36,HeroBody(id,gold,dungeon,before,after),keys,false);}
 // op5 update after the hero quest. The original appends one entry [u16 dungeon][u16 4] to the
 // cleared-dungeon list (quest_reward.inc:456-460), but this client's per-entry value is a single
 // byte: the verified Dungeon-Selection-Protocol.Body(5)/ProfileBody fixtures ([u16 count] then
 // [u16 dungeon][u8 difficulty] per row) are live on towns 1..5, so value 4 (hero tier, above the
 // 3 = king our list advertises) is written as one byte here.
 internal static byte[] HeroUnlockBody(ushort dungeon){if(dungeon==0)throw new InvalidDataException("Hero dungeon bounds");return new byte[]{(byte)1,(byte)0,(byte)dungeon,(byte)(dungeon>>8),(byte)4};}
 internal static byte[] HeroUnlockPacket(ushort dungeon,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(0,5,HeroUnlockBody(dungeon),keys,false);}
 // notify EXPERT_JOB_INFO. The original sends it from the character-expert-job load
 // (APSystemOnlyOrig/RegistDispatcher.inc:199 DB_LoadExpertJob -> Inter_LoadExpertJob::dispatch_sig
 // calls loadData then SendExpertJobInfo), so this is the only packet that tells the client which
 // 副职业 the character has: the op4 select ACK has no expert byte (Inter_LoadEtc_impl/SendResult.inc)
 // and op19 only carries the skill list.
 // OPCODE: the reference server writes put_header(0,0xcd)=205, but DNF70_v50.exe registers opcode
 // 205 as an empty handler (a bare `ret` at 0x65b9c0, one of 45 opcodes in its ignore set) and parses
 // this packet at 217 instead (handler 0x47e830). The same +12 drift is documented for other notify
 // pairs (194->206, 170->182), so low opcodes agree at offset 0 while these high ones do not - never
 // port a notify number from the reference without an anchor on this client.
 // Body, matching what 0x47e830 actually reads: byte giveupCount (->[char+0x2cc]), byte expertType
 // (switch on type-1, table 0x47e9e8: types 1/2/4 -> byte count (->[char+0x2d8] vector) + count*dword
 // recipe ids; type 3 分解师 -> dword (->[char+0x2d0]) + dword (->[char+2d4])), then it refreshes the
 // 副职业 UI. Grade and endurance are the 分解机 the original stores per character (CExpertJob.h:247,
 // 249) and spends through [endurance reduce]; this engine has no disjoint machine (op28 decomposes
 // directly), so both are reported at the disjointer.exj initial state: grade 0 shipped as +1,
 // endurance 300. Recipe lists are not modelled, so 附魔师/炼金术师/人偶师 report an empty list.
 public const ushort ExpertJobInfoOpcode=217;
 const int DisjointerEnduranceInitial=300;// character/expertjob/disjointer.exj [endurance initial value]
 internal static byte[] ExpertJobInfoBody(byte expertType){return ExpertJobInfoBody(expertType,DisjointerEnduranceInitial,0);}
 // Type 3 carries giveup, type, machine grade, endurance, then the expertness exp. The client's
 // 217 handler reads grade/endurance; the extended-expert cave reads the next dword and stores it
 // at [char+0x2a4], which is the exp the panel shows as "Lv.n (exp / next)".
 internal static byte[] ExpertJobInfoBody(byte expertType,int endurance,int expertExp){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)0);w.Write(expertType);
   /* FG380: the grade dword is the disjointer machine tier and the client gates which item levels
      the machine accepts. Grade 1 is the worn starter machine - that is why equipment above ~level
      30 was refused for disassembly (2026-10-28 report). disjointer.exj's [disjoint result] table
      covers grades 0..8, so report a top-tier machine: the single-player server never wears out. */
   if(expertType==3){w.Write((int)5);w.Write(endurance);w.Write(expertExp);}
   else if(expertType>=1&&expertType<=4&&expertType!=3)w.Write((byte)0);
   return m.ToArray();}
 }
 internal static byte[] ExpertJobInfoReply(byte expertType,byte[] keys){return ExpertJobInfoReply(expertType,DisjointerEnduranceInitial,0,keys);}
 internal static byte[] ExpertJobInfoReply(byte expertType,int endurance,int expertExp,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(0,ExpertJobInfoOpcode,ExpertJobInfoBody(expertType,endurance,expertExp),keys,false);}
 // notify CREATE_DISJOINT_STORE: reference server NOTI 206, this client parses it at 218 (handler
 // 0x468570: word uniqueId, length-prefixed narrow name, byte vill, byte area, word x, word y,
 // dword level). The client resolves the owner by uniqueId and creates the machine object; the
 // display name is the first wide character of the decoded name, so the caller echoes the same
 // narrow name bytes the client sent.
 public const ushort DisjointMachineInfoOpcode=218;
 internal static byte[] DisjointMachineInfoBody(ushort uniqueId,byte[] name,byte vill,byte area,ushort x,ushort y,int level){
  if(name==null)name=new byte[0];
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write(uniqueId);w.Write(name.Length);w.Write(name);w.Write(vill);w.Write(area);w.Write(x);w.Write(y);w.Write(level);
   return m.ToArray();}
 }
 internal static byte[] DisjointMachineInfoReply(ushort uniqueId,byte[] name,byte vill,byte area,ushort x,ushort y,int level,byte[] keys){
  /* FG380: placement passes the legacy literal 1; advertise the same top tier the 217 record does,
     otherwise the client refuses high-level items at this machine. */
  return Offline70RoleProtocol.BatchPacketFG(0,DisjointMachineInfoOpcode,DisjointMachineInfoBody(uniqueId,name,vill,area,x,y,level<=1?5:level),keys,false);}
}
