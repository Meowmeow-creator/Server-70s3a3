using System;using System.IO;
// Current client's initial value and growtype1, multiplied by native fixed-point10.
// Unpromoted swordman only. Equipment, growtype2+ and awakening bonuses are not included.
class Offline70SwordmanGrowthFG {
 internal static byte[] Stats(byte level){if(level<1||level>70)throw new InvalidDataException("Swordman level bounds");int gained=level-1;using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
  w.Write((uint)(1940+590*gained));w.Write((uint)(1550+400*gained));w.Write((short)(96+69*gained));w.Write((short)(95+68*gained));w.Write((short)(66+63*gained));w.Write((short)(65+62*gained));
  w.Write((short)120);w.Write((short)120);w.Write((short)320);w.Write((short)-80);for(int i=0;i<17;i++)w.Write((short)0);w.Write((uint)(480000+3000*gained));w.Write((short)0);w.Write((short)(500+25*gained));w.Write(Offline70TestRatesFG114.Movement(8700));w.Write((ushort)8500);w.Write((ushort)7000);w.Write((short)(6000+15*gained));w.Write((short)4300);w.Write((uint)680000);w.Write((byte)100);w.Write((float)0);
  byte[] b=m.ToArray();if(b.Length!=87)throw new InvalidDataException("Native stat record bounds");return b;
 }}
}
