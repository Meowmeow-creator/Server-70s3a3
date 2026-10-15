using System;using System.IO;
// Act4 ShadowMaze (dungeon 37) has no [monster] boss: map/shadowmaze/b1..b4.map carry
//   [ai character]
//       1301 <x> <y> 0 `[monster]` `[boss]` <cx> <cy>
//   [/ai character]
// with an empty [monster] section, and the .dgn boss cell (2,0) resolves to b1.map
// ([greed] "EE EE" -> gridValue 4 = the cell's mask 4). The native server spawns that
// actor as an APC and the client reports its death like the GSD quest APC, so the room
// body uses the same APC slot (uid 900) that Dungeon-QuestCompanion-FG130 already writes.
// Level 47 is the [minimum info] level of aicharacter/swordman/Sayoung/Sayoung.aic
// (apc id 1301); the role byte mirrors the APC roleType table (5 normal / 6 champion /
// 7 super champion / 8 boss) used by the GSD write, which sends 5 for `[normal]`.
partial class Offline70DungeonFlowFG {
 // Native 0x8301d76 add_monster_APC_AI assigns mob.m_instId = seq++, the SAME counter that
 // numbers the room's monsters, so an APC instance id sits in the monster range (100..163).
 // The client keys actors by that id: an APC sent as 900 is never death-reported (0 op42 in a
 // whole shadowmaze run) and the clear/score flow never starts. Legacy boss 900 stays accepted only inside its boss room; quest GSD keeps its own900 UID.
 internal ushort ApcUidFG176 {get{return (ushort)(100+TotalCount);}}   // after every actor incl. condition/event monsters
 internal ushort ApcSpawnedUidFG176;
 internal bool ApcRoutingFG176(ushort uid){return BossApcRoomFG176&&(uid==ApcSpawnedUidFG176||uid==GsdUidFG130);}
 internal void ApcArmFG176(){ApcSpawnedUidFG176=ApcUidFG176;}
 // 2026-10-15 (second pass): the client-side roleType>=8 theory was refuted and the
 // silent exit funnel was located (secure-CRT invalid-parameter handler 0x128E513 ->
 // TerminateProcess 0xC0000417); the death notification is now byte-faithful to rednf
 // _MakeNotiPacketDieMonster, which writes the APC kill count (not -1) for boss roles.
 // Default is the real 刹影 APC; set DNF70_ACT4_APC_BOSS=0 to fall back to the stand-in
 // normal boss (50098 kind 3) without a rebuild.
 internal static readonly bool Act4ApcBossEnabled=Environment.GetEnvironmentVariable("DNF70_ACT4_APC_BOSS")!="0";
 internal const ushort BossApcIdFG176=1301;
 internal const byte BossApcLevelFG176=47;
 // A/B switch for the client-side roleType>=8 branch (0x00935CF8 cmp [esi+0x1d8],8):
 // DNF70_ACT4_APC_ROLE=5|6|7|8 overrides the emitted role byte (default 8 = [boss]).
 internal static readonly byte BossApcRoleFG176=ApcRoleFromEnvironmentFG176();
 static byte ApcRoleFromEnvironmentFG176(){byte role;string value=Environment.GetEnvironmentVariable("DNF70_ACT4_APC_ROLE");return value!=null&&byte.TryParse(value,out role)&&role>=5&&role<=8?role:(byte)8;}
 string bossApcRunFG176;bool bossApcSpawnedFG176,bossApcDeadFG176;
 // RunId identifies the run, so a fresh Begin() clears the state without a hook there.
 internal bool BossApcRoomFG176 {get{if(!Act4ApcBossEnabled)return false;return Active&&DungeonId==37&&IsBossRoom;}}
 internal bool BossApcPendingFG176 {get{return BossApcRoomFG176&&bossApcRunFG176!=RunId;}}
 internal bool BossApcAliveFG176 {get{return BossApcRoomFG176&&bossApcRunFG176==RunId&&bossApcSpawnedFG176&&!bossApcDeadFG176;}}
 internal bool BossApcSlainFG176 {get{return BossApcRoomFG176&&bossApcRunFG176==RunId&&bossApcDeadFG176;}}
 internal void BossApcSpawnFG176(){bossApcRunFG176=RunId;bossApcSpawnedFG176=true;bossApcDeadFG176=false;}
 internal bool KillBossApcFG176(ushort uid){if(!Active||!Loaded||ClearPublished||!ApcRoutingFG176(uid)||bossApcRunFG176!=RunId||!bossApcSpawnedFG176)throw new InvalidDataException("Act4 boss APC death outside its spawned boss room");if(bossApcDeadFG176)return false;bossApcDeadFG176=true;return true;}
}
