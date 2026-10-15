using System;using System.Collections.Generic;
// Event monster tables derived from PVF [event monster] and [event monster3] tags.
// Event monsters are probability-based additional monsters that spawn during a dungeon run.
internal static class EventMonstersFG {
 internal static readonly Dictionary<ushort,ushort[][]> EventTable =
  new Dictionary<ushort,ushort[][]>{
  {31,new ushort[][]{new ushort[]{2,9000,61728,1,300,80,1}}},
  {32,new ushort[][]{new ushort[]{2,8000,61728,1,300,80,1}}},
  {34,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {35,new ushort[][]{new ushort[]{1,10000,61728,1,300,80,1}}},
  {36,new ushort[][]{new ushort[]{2,7000,61728,1,300,80,1}}},
  {37,new ushort[][]{new ushort[]{1,9000,61728,1,300,80,1}}},
 };
 internal static readonly Dictionary<ushort,ushort[][]> EventTable3 =
  new Dictionary<ushort,ushort[][]>{
  {31,new ushort[][]{new ushort[]{2,9000,64701,1,300,1,1}}},
  {32,new ushort[][]{new ushort[]{2,8500,64701,1,300,1,1}}},
  {33,new ushort[][]{new ushort[]{1,6000,64701,1,300,1,1}}},
  {34,new ushort[][]{new ushort[]{2,7000,64701,1,300,1,1}}},
  {35,new ushort[][]{new ushort[]{2,6500,64701,1,300,1,1}}},
  {36,new ushort[][]{new ushort[]{1,7500,64701,1,300,1,1}}},
  {37,new ushort[][]{new ushort[]{1,9000,64701,1,300,1,1}}},
 };
 internal static readonly Dictionary<ushort,ushort> EventRateTable =
  new Dictionary<ushort,ushort>{
 };
 internal static readonly Dictionary<ushort,ushort> EventRateTable3 =
  new Dictionary<ushort,ushort>{
 };
}