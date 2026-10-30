using System;
// FG365: single-player quest difficulty policy.
//
// The CN70 PVF gates a large part of the epic chain on the dungeon difficulty the client
// selected ("要求地下城难度为冒险级/勇士级/王者级以上"). A solo server has no party to carry
// the player through a higher tier, and the 2026-10-27 player feedback showed the practical
// result: every drop/objective quest in 血蝴蝶之舞, 疑惑之村, 列车上的海贼 and 夺回西部线
// looked stuck because the player entered on 普通 and the server silently dropped every
// objective below the row's minimum difficulty.
//
// Policy: by default the required difficulty column is treated as informational, i.e. a
// quest objective/drop row matches at any difficulty. The rows themselves and the dungeon
// id still have to match, so this cannot credit a quest in the wrong dungeon. Set
// DNF70_QUEST_DIFFICULTY_STRICT=1 to restore the native m_questMinDiff gate.
internal static class Offline70QuestRulesFG365 {
 internal static readonly bool StrictDifficulty=Environment.GetEnvironmentVariable("DNF70_QUEST_DIFFICULTY_STRICT")=="1";
 // minimum <= actual, with -1 meaning "any difficulty"; anything below -1 is malformed.
 internal static bool Meets(int minimum,int actual){if(minimum< -1)return false;return minimum==-1||!StrictDifficulty||actual>=minimum;}
 // [monster reward item]/[enemy reward item] rows use an exact difficulty column where -1
 // means "all difficulties". In relaxed mode every difficulty matches that column.
 internal static bool RowMatches(int rowDifficulty,int actual){if(rowDifficulty< -1)return false;return rowDifficulty==-1||!StrictDifficulty||rowDifficulty==actual;}
 // FG400: [condition under clear] sub type 5 is the "form an N-person party" row ([dungeon,
 // minDifficulty, partySize]; native UserQuest/check_quest_subtype_condition.inc case 5 requires
 // m_authenData.m_field10 >= at2). This service is single-player and its op49 clear evidence always
 // carries one member, so a 2+ row could never be met: title quest 15 索西雅的灾星 (the moonlight-pub
 // chain's last ring) and 30 already-supported 远古/[魔王] rows asking for two were permanently
 // blocked. By default the solo clear satisfies the party column; DNF70_QUEST_PARTY_STRICT=1
 // restores the literal comparison for evidence runs.
 internal static readonly bool StrictParty=Environment.GetEnvironmentVariable("DNF70_QUEST_PARTY_STRICT")=="1";
}
