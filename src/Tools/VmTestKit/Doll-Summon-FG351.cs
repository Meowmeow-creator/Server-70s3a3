using System;
// 控偶师人偶 (professional/puppet, stackable 2600029..2600265): [stackable type] `[waste]`
// with [expert type] `[doll_controller]`. The effect lives entirely in the client - the .stk
// carries [summon apc] 5405 25 25 900000 1 (or [summon npc]) and the client spawns the ally
// itself once the op47 use is acknowledged. There is deliberately no server-side actor: a
// friendly APC must not join Forest-Content-FG75 room rosters (it would break RoomCleared),
// and its kills are ordinary op42 monster deaths. Evidence: local-run/pvf/doll_catalog_probe.py,
// metadata via local-run/pvf/gen_doll_metadata.py (cooldown group 100, 900000 ms / 德利拉 300000).
internal static class Offline70DollSummonFG351 {
 internal static readonly uint[] Dolls=new uint[]{
  2600029,2600030,2600031,2600032,2600033,2600034,2600035,2600036,2600037,2600038,
  2600039,2600040,2600041,2600042,2600043,2600044,2600045,2600046,2600047,2600048,
  2600049,2600050,2600051,2600052,2600053,2600154,2600258,2600259,2600260,2600261,
  2600262,2600263,2600264,2600265};
 internal static bool Registered(uint item){foreach(ushort doll in Dolls)if(doll==item)return true;return false;}
}
