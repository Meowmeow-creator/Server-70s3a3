using System;using System.Globalization;using System.IO;
// Quest [slot expansion] (PVF [reward int data] 20 = support / 21 = magic stone).
// Reference quest_reward.inc:0x16 branch keeps m_exEquipSlotStat as a two-bit mask
// (bit0 support, bit1 magic stone), sets it on completion, and appends
// (newStat, slotMode) after the op36 rewardType byte 0x16. make_basic_info type 1
// emits the same byte between the 87-byte stat block and the worn-equipment list.
static class Offline70ExpandEquipSlotFG345 {
 internal const byte SupportMode=20,MagicStoneMode=21;
 internal static readonly ushort[] ExpansionQuests=new ushort[]{649,650};
 internal static byte Mode(ushort questId){return questId==649?SupportMode:questId==650?MagicStoneMode:(byte)0;}
 internal static bool Open(byte stat,ushort slot){if(stat>3)throw new InvalidDataException("Expand equip slot state");return slot==SupportMode?(stat&1)!=0:slot==MagicStoneMode?(stat&2)!=0:true;}
 internal static byte Apply(byte stat,byte mode){if(stat>3||mode!=SupportMode&&mode!=MagicStoneMode)throw new InvalidDataException("Expand equip slot mode");return mode==SupportMode?(byte)((stat&1)!=0?stat:stat|1):(byte)((stat&2)!=0?stat:stat|2);}
 internal static void ValidateReply(byte stat,byte mode){if(stat>3||mode!=SupportMode&&mode!=MagicStoneMode)throw new InvalidDataException("Expand equip slot reply state");}
 internal static Offline70EconomyState Award(string path,string receipt,ushort questId,out bool duplicate){byte mode=Mode(questId);if(mode==0)throw new InvalidDataException("Quest has no slot expansion mode");return Offline70Economy.Apply(path,receipt,"expand-equip-slot",questId.ToString(CultureInfo.InvariantCulture),delegate(Offline70EconomyState s){s.ExEquipSlotStatFG=Apply(s.ExEquipSlotStatFG,mode);},out duplicate);}
 // Characters that completed the quest on a build without this feature must unlock on the
 // next world entry, so the archive's own completion list is the migration source.
 internal static Offline70EconomyState Reconcile(string path,Offline70EconomyState assets){if(assets==null)return null;foreach(ushort quest in ExpansionQuests){if(!assets.CompletedQuests.Contains((uint)quest)||Open(assets.ExEquipSlotStatFG,Mode(quest)))continue;bool duplicate;assets=Award(path,"expand-slot-reconcile-"+quest.ToString(CultureInfo.InvariantCulture),quest,out duplicate);}return assets;}
}
