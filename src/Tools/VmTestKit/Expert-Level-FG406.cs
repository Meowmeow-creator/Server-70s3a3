using System;
// FG406: the placed disassembler machine's tier follows the character's expert job level.
//
// Before this file the 217 record and the 218 machine record advertised a fixed top tier (5), which
// FG381 introduced to stop the client refusing high-level equipment. The side effect was a Lv.70
// character whose 副职业 panel says "Lv.1" placing a "Lv.5 中高级分解机". The original ties the two:
//   CCharacExpertJob stores the machine grade per character (CExpertJob.h m_enchantCount, +0x0c);
//   SendExpertJobInfo.inc sends put_int(m_enchantCount + 1) - so the number the client shows is the
//   grade plus one;
//   CDisjointer::OnUpgradeDisjointMachine refuses to raise the grade past `expertLevel - 1`
//   (`if (grade + 1 >= expertLevel) return 0x13`), i.e. the displayed tier never exceeds the level.
// This server has no gold upgrade command (CMD 244 stays unimplemented), so the machine simply
// follows the maximum the original would allow: tier = expert level, grade = level - 1.
//
// Level tables (PVF, verified in local-run/pvf/raw-r4/character_expertjob_disjointer.exj.txt and
// character_expertjob.etc.txt):
//   [expertness exp]      224 449 674 899 1124 1349 1574 1799 2054   (9 rows => max level 9)
//   [expertjob level limit] 1..9 -> required character level 20 20 30 30 40 40 50 50 60
//   [endurance repair cost] 10000/300 .. 50000/500 (9 rows, grade+1 is the 1-based row)
// Level rule is STExpertJobScript::GetLevel + CUser::GetCurExpertJobLevel: the first threshold
// strictly greater than the exp gives the level (else the row count); when the exp sits exactly on a
// threshold the [expertjob level limit] row for that level is checked and a too-low character level
// drops one level.
class Offline70ExpertLevelFG406 {
 internal static readonly int[] ExpertnessExp={224,449,674,899,1124,1349,1574,1799,2054};
 internal static readonly int[] CharacterLevelLimit={20,20,30,30,40,40,50,50,60};
 internal static readonly int[] EnduranceRepairCost={10000,15000,20000,25000,30000,40000,50000,50000,50000};
 internal static readonly int[] EnduranceRepairFull={300,300,300,300,300,300,300,400,500};
 internal const byte DisjointerType=3;
 internal static int MaximumGrade{get{return EnduranceRepairCost.Length-1;}}
 // CUser::GetCurExpertJobLevel. expertType 0 = no expert job (the client's 0x814FD0 reports 5 for
 // "none"; the 217 body has no level field in that case).
 internal static int Level(int expertType,int expertExp,int charLevel){
  if(expertType==0)return 0;
  if(expertExp<0)expertExp=0;
  int level=ExpertnessExp.Length;
  bool boundary=false;
  for(int i=0;i<ExpertnessExp.Length;i++){
   if(ExpertnessExp[i]==expertExp)boundary=true;
   if(ExpertnessExp[i]>expertExp){level=i+1;break;}
  }
  if(boundary&&CharacterLevelLimit[level-1]>charLevel)level--;
  return level<1?1:level;
 }
 // CCharacExpertJob grade is 0-based; the 217 record and the client's "Lv.n" show grade + 1.
 internal static int Grade(int expertLevel){int g=expertLevel-1;return g<0?0:(g>MaximumGrade?MaximumGrade:g);}
 internal static int MachineLevel(int expertLevel){return Grade(expertLevel)+1;}
 // GetDisjointMachineRepairCost(grade + 1, currentEndurance): the full price minus the share that is
 // still charged by the remaining endurance (32-bit truncating division, like the original).
 internal static int RepairPrice(int grade,int endurance){
  int row=grade<0?0:(grade>MaximumGrade?MaximumGrade:grade);
  int full=EnduranceRepairFull[row];
  if(full<=0)return 0;
  if(endurance<0)endurance=0;
  if(endurance>full)endurance=full;
  return EnduranceRepairCost[row]-(EnduranceRepairCost[row]*endurance)/full;
 }
 internal static int RepairFull(int grade){int row=grade<0?0:(grade>MaximumGrade?MaximumGrade:grade);return EnduranceRepairFull[row];}
}
