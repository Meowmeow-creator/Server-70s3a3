using System;using System.IO;
// User's explicit10% replaces currentPVF5%. Retain the original premium clear
// base/rank separation and f32-rate/truncation, then the existing test scale.
class Offline70BlackClearFG159 {
 internal static uint Award(Offline70ClearExperienceFG116.Reward ordinary,bool active){if(ordinary==null)throw new InvalidDataException("Black clear reward profile");if(!active)return 0;float rate=(float)(10.0/100.0);uint original=checked((uint)((double)ordinary.Base*rate));return Offline70TestRatesFG114.Experience(original);}
 internal static byte[] Body(byte[] ordinary,uint awarded){if(ordinary==null||ordinary.Length<91)throw new InvalidDataException("Black clear body profile");byte[] result=(byte[])ordinary.Clone();BitConverter.GetBytes(awarded).CopyTo(result,25);return result;}
}
