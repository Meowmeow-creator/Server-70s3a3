using System;using System.IO;
// Grow 0 reproduces the client's [initial value]+[growtype 1] curve byte for byte (verified at
// every level 1..70); a promoted character gets its own [growtype firstGrow+1] row from
// Offline70GrowthTableFG200, matching the native segmented CalcStat model. The name is the
// original swordman-only delivery; the job argument now selects any of the nine creatable
// rows. Equipment bonuses stay fixed; the resistance/speed/jump/weight columns come from the
// same [initial value] block per job (Offline70RoleCreateJobsFG202.InitialWireFG202), and the
// seventeen active-resist slots and hpRegen are zero in every script.
class Offline70SwordmanGrowthFG {
 internal static byte[] Stats(byte job,byte growByte,byte level){if(job>=Offline70RoleCreateJobsFG202.CreatableJobsFG202||level<1||level>70||!Offline70GrowthTableFG200.SupportedGrowFG200(growByte))throw new InvalidDataException("Job growth bounds");int[] g=Offline70GrowthTableFG200.GrowthAtFG200(job,growByte,level);int[] x=Offline70RoleCreateJobsFG202.InitialWireFG202[job];using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
  w.Write((uint)g[0]);w.Write((uint)g[1]);w.Write((short)g[2]);w.Write((short)g[3]);w.Write((short)g[4]);w.Write((short)g[5]);
  w.Write((short)x[0]);w.Write((short)x[1]);w.Write((short)x[2]);w.Write((short)x[3]);for(int i=0;i<17;i++)w.Write((short)0);w.Write((uint)g[6]);w.Write((short)0);w.Write((short)g[7]);w.Write(Offline70TestRatesFG114.Movement((uint)x[4]));w.Write((ushort)x[5]);w.Write((ushort)x[6]);w.Write((short)g[8]);w.Write((short)x[7]);w.Write((uint)x[8]);w.Write((byte)100);w.Write((float)0);
  byte[] b=m.ToArray();if(b.Length!=87)throw new InvalidDataException("Native stat record bounds");return b;
 }}
}
