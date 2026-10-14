using System;using System.IO;
// Original CN70 task rewards. Native USE_STACKABLE dispatches the original throw action.
class Offline70ThrowItemsFG90 {
 internal static bool Known(uint item){return item==6018||item==6019;}
 internal static bool Allowed(uint item,Offline70EconomyItemDefinition d){
  if(!Known(item))return false;
  if(d.ItemId!=item||d.Kind!=1||d.MinimumLevel!=20||d.StackLimit!=1000||d.CooldownMilliseconds!=500||d.CooldownGroup!=(int)item||d.HpRecovery!=0||d.MpRecovery!=0||d.HpRecoveryPercent!=0||d.MpRecoveryPercent!=0||d.NativeStatusRecovery)throw new InvalidDataException("Original throw item definition mismatch");
  return true;
 }
}
