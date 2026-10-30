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
}
