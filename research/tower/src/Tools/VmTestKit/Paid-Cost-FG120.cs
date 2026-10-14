using System;using System.IO;
// Current CN70 itemdropinfo_clearreward: commission by dungeon standard level.
// TW70 getGoldCardCommission and ordinary CParty clear_reward multiplier1.
// General random card items and premium/event discounts remain unimplemented.
class Offline70PaidCostFG120 {
 internal static readonly uint[] Commission=new uint[]{0,100,300,400,600,700,800,1000,1100,1200,1400,1500,1700,1800,1900,2100,2400,2600,2700,2900,3000,3200,3300,3500,3600,3800,3900,4100,4200,4400,4500,5100,5200,5400,5600,5700,5900,6100,6200,6400,6600,7200,7400,7600,7800,7900,8100,8300,8500,8600,8800,9000,9200,9300,9500,9700,9900,10100,10200,10400,10600,10800,10900,11100,11300,11500,11600,11800,12000,12200,12300,12500,12700,12900,13100,13200,13400,13600,13800,13900,14100,14300,14500,14600,14800,15000,15200,15300,15500,15700,15900,16100,16200,16400,16600,16800,16900,17100,17300,17500};
 internal static uint ForDungeon(ushort dungeon){ushort basis=Offline70ClearExperienceFG116.Basis(dungeon);if(basis<1||basis>=Commission.Length)throw new InvalidDataException("Unproven paid-card basis");return Commission[basis];}
 internal static byte[] Rewards(ushort dungeon,byte[] body){if(body==null||body.Length!=91)throw new InvalidDataException("Paid-card rewards shape");byte[] result=(byte[])body.Clone();BitConverter.GetBytes(ForDungeon(dungeon)).CopyTo(result,74);return result;}
}
