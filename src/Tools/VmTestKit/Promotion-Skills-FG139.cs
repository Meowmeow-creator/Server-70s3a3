using System;using System.IO;
partial class Offline70SkillSlots {
 // Promotion give-skills come from the per-job creation table (grow row p is the script's
 // [growtype p+1]); this flattens it into the (id,level,...) shape the baseline uses.
 internal static byte[] AutoSkillsFG139(byte job,byte grow){
  if(job>=Offline70RoleCreateJobsFG202.CreatableJobsFG202||grow>4)throw new InvalidDataException("Promotion skill range");
  byte[][] rows=Offline70RoleCreateJobsFG202.GrowSkillsFG202[job][grow];byte[] flat=new byte[rows.Length*2];int at=0;
  for(int i=0;i<rows.Length;i++){flat[at++]=rows[i][0];flat[at++]=rows[i][1];}return flat;}
 internal static int BaseLevelFG139(Offline70SkillSlotState s,byte id){byte[] initialIds=StartingIdsFG202(Offline70RoleScopeFG202.CurrentJob),initialLevels=StartingLevelsFG202(Offline70RoleScopeFG202.CurrentJob);int i=Array.IndexOf(initialIds,id);int level=i<0?0:initialLevels[i];if(id==173&&s.ProfessionFG139!=0&&s.ProfessionFG139!=3)level=0;byte[] a=AutoSkillsFG139(Offline70RoleScopeFG202.CurrentJob,s.ProfessionFG139);for(int n=0;n<a.Length;n+=2)if(a[n]==id)level=Math.Max(level,a[n+1]);byte awake=Offline70RewardTypesFG183.AwakeningSkill(Offline70RoleScopeFG202.CurrentJob,s.ProfessionFG139);if(awake!=0&&id==awake)level=Math.Max(level,Offline70RewardTypesFG183.AwakeningSkillLevel);return level;}
 internal static bool NormalizeProfessionStateFG139(Offline70SkillSlotState s,byte grow){
  Validate(s);if(grow>4||s.ProfessionFG139!=0&&s.ProfessionFG139!=grow)throw new InvalidDataException("Profession baseline identity");if(s.ProfessionFG139==grow)return false;
  // First promotion resets the learned block and refunds every booked SP before the branch
  // give-skills land: the native promotion clears the old skill list so the player re-spends the
  // points on the new tree. Reset and ProfessionFG139 are committed together, so a crash before
  // the commit leaves the pre-promotion archive for the next Sync to retry - never a half-reset bar.
  if(s.ProfessionFG139==0)ResetPromotionFG139(s);
  s.ProfessionFG139=grow;if(grow==0)return false;GrantBranchSkillsFG139(s,grow);Validate(s);return true;}
 // The initial block length is per job (gunner rows carry 10 starting skills, at mage 12), so the
 // free-grant accounting must use the creation row count instead of the swordman SkillIds length.
 internal static void GrantBranchSkillsFG139(Offline70SkillSlotState s,byte grow){int initialCount=StartingIdsFG202(Offline70RoleScopeFG202.CurrentJob).Length;int heavy=Array.IndexOf(s.Ids,(byte)173);if(heavy>=0&&grow!=3)s.Levels[heavy]=0;byte[] a=AutoSkillsFG139(Offline70RoleScopeFG202.CurrentJob,grow);for(int n=0;n<a.Length;n+=2){byte id=a[n],baseLevel=a[n+1];int i=Array.IndexOf(s.Ids,id);if(i<0){byte slot=FindListFG123(s,id);i=s.Ids.Length;Array.Resize(ref s.Ids,i+1);Array.Resize(ref s.Slots,i+1);Array.Resize(ref s.Levels,i+1);s.Ids[i]=id;s.Slots[i]=slot;s.Levels[i]=baseLevel;}else{Offline70SkillRuleFG r=Rule(id);int free=Math.Min(s.Levels[i],baseLevel);if(i>=initialCount&&r!=null)s.SpentSp=checked((ushort)(s.SpentSp-free*r.Cost));s.Levels[i]=Math.Max(s.Levels[i],baseLevel);}}}
 // Reset shape for the first promotion: the current job's creation rows, all SP refunded, and the
 // player's placement kept only where the promoted rules still accept it. Active skills may sit in
 // any active slot; initial passives are pinned by the creation table, so they fall back to the
 // canonical row (a pre-promotion base-tab slot 6..53 is not valid once ProfessionFG139 is set).
 internal static void ResetPromotionFG139(Offline70SkillSlotState s){
  Validate(s);Offline70SkillSlotState baseline=Default();
  for(int i=0;i<baseline.Slots.Length&&i<s.Slots.Length;i++)if(Active(baseline.Ids[i])&&ActiveSlot(s.Slots[i]))baseline.Slots[i]=s.Slots[i];
  baseline.CommonPlacementFG81=true;Validate(baseline);
  s.Ids=baseline.Ids;s.Slots=baseline.Slots;s.Levels=baseline.Levels;s.SpentSp=0;s.CommonPlacementFG81=true;}
 internal static Offline70SkillSlotState NormalizeProfessionFG139(string path,byte grow){string full=Path.GetFullPath(path);using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=Load(full);if(NormalizeProfessionStateFG139(s,grow))CommitResetFG83(full,s);return Load(full);}}
 internal static int VisibleCountFG139(Offline70SkillSlotState s){int n=0;foreach(byte v in s.Levels)if(v>0)n++;return n;}
 internal static Offline70SkillSlotState LearnManyFG139(string path,byte level,byte id,byte increment,ushort bonus,byte grow,bool awakened,int extra){if(increment<1||increment>50)throw new InvalidDataException("Skill increment bounds");string full=Path.GetFullPath(path);using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=Load(full);for(int n=0;n<increment;n++)LearnStateFG129(s,level,id,checked((byte)(SkillLevel(s,id)+1)),bonus,grow,awakened,extra);CommitResetFG83(full,s);return Load(full);}}
 // Expert job grant (character/expertjob/*.exj `[skill]` = one level-1 skill per expert type, PVF
 // measured: 1 enchanter 191 enchant.skl / 2 alchemist 193 alchemy.skl / 3 disjointer 194
 // opendisjointshop.skl / 4 doll_controller 192 dollmaking.skl, each [purchase cost] 0
 // [required level] 20 [type] [passive] [growtype maximum level] 10x6). The original server applies
 // the .exj m_growSkillVec through growtype_skill() inside the quest_reward 0x14 branch
 // (quest_reward.inc:394-421), so the skill appears in the character's bank with the award; Cost=0
 // means it is never SP-learned (Learn rejects Cost==0), matching the PVF.
 internal static bool NormalizeExpertStateFG183(Offline70SkillSlotState s,byte expertType){Validate(s);if(expertType<1||expertType>4)throw new InvalidDataException("Expert job type bounds");byte id=Offline70RewardTypesFG183.ExpertJobSkill(expertType);int i=Array.IndexOf(s.Ids,id);if(i<0){byte slot=FindListFG123(s,id);i=s.Ids.Length;Array.Resize(ref s.Ids,i+1);Array.Resize(ref s.Slots,i+1);Array.Resize(ref s.Levels,i+1);s.Ids[i]=id;s.Slots[i]=slot;s.Levels[i]=1;}else if(s.Levels[i]==0)s.Levels[i]=1;else return false;Validate(s);return true;}
 internal static Offline70SkillSlotState GrantExpertSkillFG183(string path,byte expertType){string full=Path.GetFullPath(path);using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=Load(full);if(NormalizeExpertStateFG183(s,expertType))CommitResetFG83(full,s);return Load(full);}}
 // First-awakening skill grant. character/swordman/swordman.chr [growtype grow+1][awakening 1]
 // [awakening skill] holds exactly one (id,1) row per branch - PVF measured over the running HTTP
 // API: grow1 91 defensedownoncritical (gt2 剑魂→剑圣), grow2 89 darkaura (gt3 鬼泣→弑魂), grow3
 // 90 bleedingmastery (gt4 狂战士→狱血魔神), grow4 92 stuckuponcounter (gt5 阿修罗→大暗黑天),
 // i.e. chr [growtype N] == grow N-1, which the same blocks' [skill] rows already prove against
 // AutoFG139. The original grants it SERVER-SIDE inside the [awakening type] reward:
 // quest_reward.inc b8==2 -> set_grow_type(getCurCharFirstGrowType(),1,&ack,reason 0) ->
 // get_give_skill(firstGrow,1,giveVec,2) (CCharacter/get_give_skill_secondGrowtype.inc reads
 // m_secondGrowTypes[grow+1].m_skills[0], the same chr rows) -> growtype_skill(job,id,level) on
 // both trees, which only writes the level (SkillSlot/growtype_skill.inc) and never touches SP.
 // The level-1 grant is therefore free by construction, exactly like the expert Cost=0 convention
 // but for a different reason: calcUsedSP.inc builds its give list with get_give_skill(...,flag 0)
 // when a second grow is set and skips every skill at or below the given level
 // (`giveLevel!=0 ... if (buf[i].m_level<=curLevel) continue`), charging only the levels above it
 // - this file's (level - BaseLevelFG139) * Cost ledger. BaseLevelFG139 above is what makes that
 // hold, so the skill enters the state without a fictitious 30 SP consumption and without any
 // repeatable grant path.
 // Job is the swordman index 0 for the whole kit (skill/swordmanskill.lst ids, Skill-Rules-FG38
 // and the pinned 11 starting skill ids in Starting-Skills-Protocol.cs are the swordman catalog,
 // and recon/job-advancement-report.md section 2 proves s.Job never leaves 0); AwakeningTable
 // rows 1..5 are the standby tables of the other five character scripts.
 internal static bool NormalizeAwakeningStateFG183(Offline70SkillSlotState s,byte skillId){Validate(s);if(skillId==0)return false;if(Offline70RewardTypesFG183.AwakeningSkill(Offline70RoleScopeFG202.CurrentJob,s.ProfessionFG139)!=skillId)throw new InvalidDataException("觉醒技能 "+skillId+" 不属于 "+Offline70RewardTypesFG183.AdvancementNameFG204(Offline70RoleScopeFG202.CurrentJob,s.ProfessionFG139)+"（grow "+s.ProfessionFG139+"）这条分支");byte level=Offline70RewardTypesFG183.AwakeningSkillLevel;int i=Array.IndexOf(s.Ids,skillId);if(i<0){byte slot=FindListFG123(s,skillId);i=s.Ids.Length;Array.Resize(ref s.Ids,i+1);Array.Resize(ref s.Slots,i+1);Array.Resize(ref s.Levels,i+1);s.Ids[i]=skillId;s.Slots[i]=slot;s.Levels[i]=level;}else if(s.Levels[i]<level)s.Levels[i]=level;else return false;Validate(s);return true;}
 internal static Offline70SkillSlotState GrantAwakeningSkillFG183(string path,byte skillId){string full=Path.GetFullPath(path);using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=Load(full);if(NormalizeAwakeningStateFG183(s,skillId))CommitResetFG83(full,s);return Load(full);}}
}
