using System;using System.IO;using System.Collections.Generic;
// Current CN creature/exptable.tbl: thresholds for levels2..56. TW regular
// creatures gain actual room fatigue only at clear; the four integrated artifacts have no XP bonus. Supported boss-room completion follows the existing boss-ends-run service rule.
class Offline70PetExperienceFG109 {
 internal static readonly uint[] Thresholds={2,5,9,15,23,33,44,57,71,88,106,126,148,171,196,223,252,283,315,350,388,429,472,517,565,615,668,725,785,849,918,993,1073,1158,1245,1335,1430,1532,1642,1759,1879,2011,2151,2301,2464,2639,2829,3049,3294,3564,3879,4229,4629,5079,5579};
 internal static byte Maximum(uint item){if(item==63023)return 20;if(item==2681359||item==2747024||item==63146)return 50;throw new InvalidDataException("Unreviewed pet experience profile");}
 internal static byte Level(uint item,uint experience){if(experience>Int32.MaxValue)throw new InvalidDataException("Creature experience bounds");byte maximum=Maximum(item),level=1;while(level<maximum&&experience>=Thresholds[level-1])level++;return level;}
 internal static bool Valid(Offline70EconomyEntry e){return e!=null&&e.CreatureExperience<=Int32.MaxValue&&e.CreatureLevel==Level(e.ItemId,e.CreatureExperience);}
 internal static bool Eligible(Offline70EconomyEntry e,uint fatigue){return fatigue>0&&fatigue<=156&&e!=null&&e.CreatureSatiety>0&&e.CreatureLevel<Maximum(e.ItemId);}
 internal static uint ActualCharge(int before,int beforeDay,Offline70EconomyState afterState){int after=afterState.FatigueRemaining;if(afterState.FatigueServiceDay>beforeDay)before=Offline70FatigueFG50.Maximum;if(after>before||before-after>1)throw new InvalidDataException("Actual pet fatigue charge");return (uint)(before-after);}
 internal static Offline70EconomyState Settle(string path,string receipt,uint fatigue,out bool duplicate){
  if(fatigue>156)throw new InvalidDataException("Creature room fatigue bounds");Offline70EconomyState before=Offline70Economy.Load(path);bool recorded=false;foreach(Offline70EconomyReceipt r in before.Receipts)if(r.Id==receipt){recorded=true;break;}
  duplicate=false;if(!recorded&&!Eligible(Offline70PetEquipFG97.Equipped(before),fatigue))return before;
  return Offline70Economy.Apply(path,receipt,"pet-room-xp",fatigue.ToString(System.Globalization.CultureInfo.InvariantCulture),delegate(Offline70EconomyState s){Offline70EconomyEntry e=Offline70PetEquipFG97.Equipped(s);if(!Eligible(e,fatigue))throw new InvalidDataException("Creature room award state changed");if(e.CreatureExperience>Int32.MaxValue-fatigue)throw new InvalidDataException("Creature experience overflow");e.CreatureExperience+=fatigue;e.CreatureLevel=Level(e.ItemId,e.CreatureExperience);},out duplicate);
 }
 // CN4783E0 reads exactly BYTElevel+U32totalXP; TW's extra mode byte is absent.
 internal static byte[] ExperienceBody(Offline70EconomyEntry e){if(e==null||!Offline70PetFG96.IsPet(e.ItemId)||!Valid(e))throw new InvalidDataException("Creature XP wire profile");byte[] b=new byte[5];b[0]=e.CreatureLevel;BitConverter.GetBytes(e.CreatureExperience).CopyTo(b,1);return b;}
 internal static byte[] ExperienceReply(Offline70EconomyEntry e,byte[] keys){return Offline70RoleProtocol.BatchPacketFG(0,102,ExperienceBody(e),keys,false);}
}
partial class Offline70BatchServiceFG {
 readonly Dictionary<string,uint> petRoomFatigueFG109=new Dictionary<string,uint>();readonly HashSet<string> petRoomsSettledFG109=new HashSet<string>();
 void ResetPetRoomsFG109(){petRoomFatigueFG109.Clear();petRoomsSettledFG109.Clear();}
 void RecordPetFatigueFG109(string room,int before,int beforeDay,Offline70EconomyState afterState,bool duplicate){uint charged=Offline70BlackFG158.ActualCharge(before,beforeDay,afterState);if(duplicate)return;if(petRoomFatigueFG109.Count>=256&&!petRoomFatigueFG109.ContainsKey(room))throw new InvalidDataException("Pet room charge capacity");petRoomFatigueFG109[room]=charged;}
 void SettlePetRoomFG109(){
  if(!dungeon.Active||!dungeon.Loaded||!(dungeon.RoomCleared||dungeon.ReadyToClear)||petRoomsSettledFG109.Contains(dungeon.RoomReceiptScope))return;
  uint fatigue;petRoomFatigueFG109.TryGetValue(dungeon.RoomReceiptScope,out fatigue);Offline70EconomyEntry before=Offline70PetEquipFG97.Equipped(assets);uint uid=before==null?0:before.CreatureKey,xp=before==null?0:before.CreatureExperience;bool duplicate;
  assets=Offline70PetExperienceFG109.Settle(economyPath,dungeon.RoomReceiptScope+"-pet-xp",fatigue,out duplicate);petRoomsSettledFG109.Add(dungeon.RoomReceiptScope);Offline70EconomyEntry after=Offline70PetEquipFG97.Equipped(assets);
  if(!duplicate&&after!=null&&after.CreatureKey==uid&&after.CreatureExperience>xp){Send("pet-room-experience102",Offline70PetExperienceFG109.ExperienceReply(after,keys));Note("PET_ROOM_XP_COMMITTED room="+dungeon.RoomIndex+" uid="+uid+" actualFatigue="+fatigue+" xp="+after.CreatureExperience+" level="+after.CreatureLevel);}
 }
}
