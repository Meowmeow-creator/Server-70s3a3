using System;using System.Collections.Generic;
// Event monster tables derived from PVF [event monster] and [event monster3] tags.
// Event monsters are probability-based additional monsters that spawn during a dungeon run.
internal static class EventMonstersFG {
 internal static readonly Dictionary<ushort,ushort[][]> EventTable =
  new Dictionary<ushort,ushort[][]>{
  {3,new ushort[][]{new ushort[]{1,10000,61726,1,300,80,1}}},
  {4,new ushort[][]{new ushort[]{1,10000,61726,1,300,80,1}}},
  {5,new ushort[][]{new ushort[]{1,10000,61726,1,300,80,1}}},
  {6,new ushort[][]{new ushort[]{1,8500,61726,1,300,80,1}}},
  {7,new ushort[][]{new ushort[]{2,7000,61726,1,300,80,1}}},
  {8,new ushort[][]{new ushort[]{1,10000,61726,1,300,80,1}}},
  {9,new ushort[][]{new ushort[]{1,10000,61726,1,300,80,1}}},
  {11,new ushort[][]{new ushort[]{1,10000,61726,1,300,80,1}}},
  {12,new ushort[][]{new ushort[]{2,7000,61726,1,300,80,1}}},
  {13,new ushort[][]{new ushort[]{2,7000,61727,1,300,80,1}}},
  {14,new ushort[][]{new ushort[]{1,10000,61727,1,300,80,1}}},
  {15,new ushort[][]{new ushort[]{2,8000,61727,1,300,80,1}}},
  {17,new ushort[][]{new ushort[]{1,8000,61727,1,300,80,1}}},
  {1000,new ushort[][]{new ushort[]{2,8000,61726,1,300,80,1}}},
  {21,new ushort[][]{new ushort[]{2,7500,61727,1,300,80,1}}},
  {22,new ushort[][]{new ushort[]{2,7500,61727,1,300,80,1}}},
  {23,new ushort[][]{new ushort[]{2,7000,61727,1,300,80,1}}},
  {24,new ushort[][]{new ushort[]{1,10000,61727,1,300,80,1}}},
  {25,new ushort[][]{new ushort[]{1,9000,61727,1,300,80,1}}},
  {26,new ushort[][]{new ushort[]{1,10000,61727,1,300,80,1}}},
  {27,new ushort[][]{new ushort[]{1,9000,61727,1,300,80,1}}},
  {31,new ushort[][]{new ushort[]{2,9000,61728,1,300,80,1}}},
  {32,new ushort[][]{new ushort[]{2,8000,61728,1,300,80,1}}},
  {34,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {35,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {36,new ushort[][]{new ushort[]{2,7000,61728,1,300,80,1}}},
  {37,new ushort[][]{new ushort[]{1,9000,61728,1,300,80,1}}},
  {40,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {41,new ushort[][]{new ushort[]{1,1500,61728,1,300,80,1}}},
  {42,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {43,new ushort[][]{new ushort[]{1,9000,61728,1,300,80,1}}},
  {44,new ushort[][]{new ushort[]{1,9000,61728,1,300,80,1}}},
  {45,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {141,new ushort[][]{new ushort[]{1,1500,61728,1,300,80,1}}},
 };
 internal static readonly Dictionary<ushort,ushort[][]> EventTable3 =
  new Dictionary<ushort,ushort[][]>{
  {11,new ushort[][]{new ushort[]{2,6000,64701,1,300,1,1}}},
  {12,new ushort[][]{new ushort[]{2,7000,64701,1,300,1,1}}},
  {13,new ushort[][]{new ushort[]{2,7000,64701,1,300,1,1}}},
  {14,new ushort[][]{new ushort[]{2,6500,64701,1,300,1,1}}},
  {15,new ushort[][]{new ushort[]{2,9000,64701,1,300,1,1}}},
  {17,new ushort[][]{new ushort[]{1,9000,64701,1,300,1,1}}},
  {21,new ushort[][]{new ushort[]{2,8000,64701,1,300,1,1}}},
  {22,new ushort[][]{new ushort[]{2,8000,64701,1,300,1,1}}},
  {23,new ushort[][]{new ushort[]{2,7000,64701,1,300,1,1}}},
  {24,new ushort[][]{new ushort[]{2,6000,64701,1,300,1,1}}},
  {25,new ushort[][]{new ushort[]{1,10000,64701,1,300,1,1}}},
  {26,new ushort[][]{new ushort[]{2,6500,64701,1,300,1,1}}},
  {27,new ushort[][]{new ushort[]{1,10000,64701,1,300,1,1}}},
  {31,new ushort[][]{new ushort[]{2,9000,64701,1,300,1,1}}},
  {32,new ushort[][]{new ushort[]{2,8500,64701,1,300,1,1}}},
  {33,new ushort[][]{new ushort[]{1,6000,64701,1,300,1,1}}},
  {34,new ushort[][]{new ushort[]{2,7000,64701,1,300,1,1}}},
  {35,new ushort[][]{new ushort[]{2,6500,64701,1,300,1,1}}},
  {36,new ushort[][]{new ushort[]{1,7500,64701,1,300,1,1}}},
  {37,new ushort[][]{new ushort[]{1,9000,64701,1,300,1,1}}},
  {1502,new ushort[][]{new ushort[]{1,7000,64701,1,300,1,1}}},
  {40,new ushort[][]{new ushort[]{1,10000,64701,1,300,1,1}}},
  {42,new ushort[][]{new ushort[]{1,10000,64701,1,300,1,1}}},
  {43,new ushort[][]{new ushort[]{1,10000,64701,1,300,1,1}}},
  {44,new ushort[][]{new ushort[]{1,9000,64701,1,300,1,1}}},
  {45,new ushort[][]{new ushort[]{1,10000,64701,1,300,1,1}}},
 };
 internal static readonly Dictionary<ushort,ushort> EventRateTable =
  new Dictionary<ushort,ushort>{
 };
 internal static readonly Dictionary<ushort,ushort> EventRateTable3 =
  new Dictionary<ushort,ushort>{
 };
}