using System;
// Nine-job character-creation table, generated from character/<job>/<job>.chr by
// local-run/pvf/gen_role_create_jobs.py (evidence: local-run/recon/role-create-jobs-report.md).
// Native create path (reference DisPatcherCore/process_5.inc): the request carries only
// slot+name, slot IS the job enum, and the server backfills stats from m_characList[job*0x7dc],
// skills from CCharacter::m_giveSkillList[0] (addSkillOnCreateCharacter) and the gift/equip pair
// from makeGift. Jobs 9 (demonic swordman) and 10 (creator mage) have no .chr in this client
// build, so they stay an explicit rejection rather than a silent no-op.
class Offline70RoleCreateJobsFG202 {
 // Valid creation slots are 0..CreatableJobsFG202-1; the slot byte equals the job enum.
 internal const byte CreatableJobsFG202=9;
 // CREATECHARACTEREQUIP[22] is uniform: every job starts with the same weapon and clothes.
 internal const uint CreateWeaponFG202=10400,CreateClothesFG202=12400;
 internal static bool CreatableFG202(byte job){return job<CreatableJobsFG202;}
 // [job] makeGift gift item; the native switch shares one gift across a family and its at-variant.
 internal static readonly uint[] GiftItemFG202=new uint[]{27600,29900,31300,33600,35001,31300,37002,29900,33600};
 // [job] [initial value] columns, same field order as Offline70GrowthTableFG200.
 internal static readonly int[][] InitialStatsFG202=new int[][]{
  new int[]{1940,1550,96,95,66,65,480000,500,6000}, // swordman
  new int[]{2140,1350,96,95,66,65,430000,500,6000}, // fighter
  new int[]{1740,1750,86,85,76,75,400000,500,6000}, // gunner
  new int[]{1340,2150,66,65,96,95,340000,800,5000}, // mage
  new int[]{2340,1150,86,85,66,85,480000,500,5000}, // priest
  new int[]{1640,1850,86,85,76,75,400000,500,5500}, // at gunner
  new int[]{1840,1650,96,75,86,65,430000,500,6000}, // thief
  new int[]{2240,1250,96,95,66,65,430000,500,6000}, // at fighter
  new int[]{1290,2200,61,65,101,95,340000,800,5000}, // at mage
 };
 // [job] remaining 87-byte stat-record columns that also come from [initial value]: fire/water/
 // dark/light resistance, move/attack/cast speed, jump power, weight (fixed-point10). hpRegen and
 // the seventeen active-resist slots stay zero in every script, and the trailing byte stays 100.
 internal static readonly int[][] InitialWireFG202=new int[][]{
  new int[]{120,120,320,-80,8700,8500,7000,4300,680000}, // swordman
  new int[]{120,120,-80,320,9300,9500,10000,4700,500000}, // fighter
  new int[]{120,120,120,120,8400,9500,8000,4900,600000}, // gunner
  new int[]{120,120,120,120,8200,10000,10000,3500,400000}, // mage
  new int[]{120,120,120,120,8000,9500,10000,5000,780000}, // priest
  new int[]{120,120,120,120,8700,9500,8000,4400,480000}, // at gunner
  new int[]{120,120,120,120,9800,10000,10000,4650,450000}, // thief
  new int[]{120,120,-80,320,9000,9500,10000,4700,500000}, // at fighter
  new int[]{120,120,120,120,8400,10000,10000,3500,500000}, // at mage
 };
 // [job] creation give-skills as (id,level,slot) triples: m_giveSkillList[0] placed by the native
 // rule (active -> first free of 0..5, else the group range from reform_ui_group_no). The rule
 // reproduces the captured swordman packet, which is what guards this generator.
 internal static readonly byte[][][] CreateSkillsFG202=new byte[][][]{
  new byte[][]{new byte[]{181,2,102},new byte[]{182,2,103},new byte[]{184,1,104},new byte[]{173,1,105},new byte[]{179,7,106},new byte[]{5,1,0},new byte[]{46,1,1},new byte[]{48,1,6},new byte[]{169,1,2},new byte[]{170,1,107},new byte[]{174,1,108}}, // swordman
  new byte[][]{new byte[]{181,3,102},new byte[]{182,3,103},new byte[]{184,1,104},new byte[]{172,1,105},new byte[]{179,7,106},new byte[]{5,1,0},new byte[]{23,1,6},new byte[]{46,1,1},new byte[]{169,1,2},new byte[]{170,1,107},new byte[]{174,1,108}}, // fighter
  new byte[][]{new byte[]{181,2,102},new byte[]{184,1,103},new byte[]{185,1,104},new byte[]{179,7,105},new byte[]{4,1,0},new byte[]{84,1,6},new byte[]{47,1,1},new byte[]{169,1,2},new byte[]{170,1,106},new byte[]{174,1,107}}, // gunner
  new byte[][]{new byte[]{181,3,102},new byte[]{182,3,103},new byte[]{184,2,104},new byte[]{187,1,105},new byte[]{179,7,106},new byte[]{11,1,0},new byte[]{12,1,1},new byte[]{34,1,6},new byte[]{169,1,2},new byte[]{170,1,107},new byte[]{174,1,108}}, // mage
  new byte[][]{new byte[]{181,1,102},new byte[]{182,1,103},new byte[]{184,1,104},new byte[]{179,7,105},new byte[]{1,1,0},new byte[]{8,1,1},new byte[]{173,1,106},new byte[]{6,1,6},new byte[]{169,1,2},new byte[]{170,1,107},new byte[]{174,1,108}}, // priest
  new byte[][]{new byte[]{181,2,102},new byte[]{184,1,103},new byte[]{185,1,104},new byte[]{179,7,105},new byte[]{4,1,0},new byte[]{84,1,6},new byte[]{47,1,1},new byte[]{169,1,2},new byte[]{170,1,106},new byte[]{174,1,107}}, // at gunner
  new byte[][]{new byte[]{181,2,102},new byte[]{182,2,103},new byte[]{184,1,104},new byte[]{179,7,105},new byte[]{169,1,0},new byte[]{170,1,106},new byte[]{60,1,107},new byte[]{1,1,1},new byte[]{10,1,6},new byte[]{24,1,2},new byte[]{174,1,108}}, // thief
  new byte[][]{new byte[]{181,3,102},new byte[]{182,3,103},new byte[]{184,1,104},new byte[]{172,1,105},new byte[]{179,7,106},new byte[]{5,1,0},new byte[]{23,1,6},new byte[]{46,1,1},new byte[]{169,1,2},new byte[]{170,1,107},new byte[]{174,1,108}}, // at fighter
  new byte[][]{new byte[]{33,1,6},new byte[]{181,3,102},new byte[]{182,3,103},new byte[]{184,2,104},new byte[]{187,1,105},new byte[]{179,7,106},new byte[]{1,1,0},new byte[]{38,1,7},new byte[]{26,1,1},new byte[]{169,1,2},new byte[]{170,1,107},new byte[]{174,1,108}}, // at mage
 };
 // [job][firstGrow] give-skill lists, row p being the script's [growtype p+1] block, i.e.
 // m_giveSkillList[p+1] handed out when the character promotes into that grow type. Empty rows
 // are real gaps in the script ([growtype 1] carries no give skills anywhere; fighter and at
 // fighter stop at [growtype 5]), not missing data.
 internal static readonly byte[][][][] GrowSkillsFG202=new byte[][][][]{
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{27,1},new byte[]{33,1},new byte[]{37,1},new byte[]{172,1},new byte[]{8,1},new byte[]{25,1},new byte[]{65,1},new byte[]{20,1},new byte[]{94,1},new byte[]{197,1}},new byte[][]{new byte[]{35,1},new byte[]{29,1},new byte[]{187,1},new byte[]{8,1},new byte[]{25,1},new byte[]{65,1},new byte[]{20,1},new byte[]{93,1},new byte[]{197,1}},new byte[][]{new byte[]{56,1},new byte[]{34,1},new byte[]{181,2},new byte[]{182,2},new byte[]{8,1},new byte[]{25,1},new byte[]{65,1},new byte[]{20,1},new byte[]{76,1},new byte[]{197,1}},new byte[][]{new byte[]{47,1},new byte[]{55,1},new byte[]{61,1},new byte[]{8,1},new byte[]{25,1},new byte[]{65,1},new byte[]{20,1},new byte[]{197,1}},new byte[][]{new byte[]{8,1},new byte[]{25,1},new byte[]{65,1},new byte[]{20,1},new byte[]{94,1},new byte[]{27,1},new byte[]{33,1},new byte[]{37,1},new byte[]{56,1},new byte[]{187,1}}}, // swordman
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{35,1},new byte[]{29,1},new byte[]{187,1},new byte[]{97,1},new byte[]{184,2},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{new byte[]{34,1},new byte[]{56,1},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{new byte[]{73,1},new byte[]{32,1},new byte[]{33,1},new byte[]{182,5},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{new byte[]{22,1},new byte[]{55,1},new byte[]{57,1},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{}}, // fighter
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{181,3},new byte[]{22,1},new byte[]{23,1},new byte[]{55,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{197,1}},new byte[][]{new byte[]{184,3},new byte[]{173,1},new byte[]{44,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{92,1},new byte[]{197,1}},new byte[][]{new byte[]{187,1},new byte[]{183,5},new byte[]{46,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{91,1},new byte[]{197,1}},new byte[][]{new byte[]{61,1},new byte[]{31,1},new byte[]{82,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{197,1}},new byte[][]{new byte[]{22,1},new byte[]{23,1},new byte[]{55,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{44,1},new byte[]{92,1},new byte[]{38,1},new byte[]{82,1}}}, // gunner
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{19,1},new byte[]{18,1},new byte[]{25,1},new byte[]{37,1},new byte[]{43,1},new byte[]{107,1},new byte[]{197,1}},new byte[][]{new byte[]{48,1},new byte[]{18,1},new byte[]{25,1},new byte[]{37,1},new byte[]{43,1},new byte[]{108,1},new byte[]{197,1}},new byte[][]{new byte[]{67,1},new byte[]{60,1},new byte[]{1,1},new byte[]{39,1},new byte[]{18,1},new byte[]{25,1},new byte[]{37,1},new byte[]{43,1},new byte[]{197,1}},new byte[][]{new byte[]{184,5},new byte[]{91,1},new byte[]{102,1},new byte[]{18,1},new byte[]{25,1},new byte[]{37,1},new byte[]{43,1},new byte[]{197,1}},new byte[][]{}}, // mage
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{54,1},new byte[]{34,1},new byte[]{43,1},new byte[]{7,1},new byte[]{61,1},new byte[]{11,1},new byte[]{64,1},new byte[]{197,1}},new byte[][]{new byte[]{29,1},new byte[]{13,1},new byte[]{91,1},new byte[]{7,1},new byte[]{61,1},new byte[]{11,1},new byte[]{64,1},new byte[]{197,1}},new byte[][]{new byte[]{68,1},new byte[]{92,1},new byte[]{96,1},new byte[]{67,1},new byte[]{7,1},new byte[]{61,1},new byte[]{11,1},new byte[]{64,1},new byte[]{197,1}},new byte[][]{new byte[]{126,1},new byte[]{7,1},new byte[]{61,1},new byte[]{11,1},new byte[]{64,1},new byte[]{123,1},new byte[]{114,1},new byte[]{197,1}},new byte[][]{new byte[]{54,1},new byte[]{34,1},new byte[]{43,1},new byte[]{7,1},new byte[]{61,1},new byte[]{11,1},new byte[]{64,1},new byte[]{68,1},new byte[]{92,1},new byte[]{96,1},new byte[]{67,1}}}, // priest
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{181,3},new byte[]{22,1},new byte[]{23,1},new byte[]{55,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{197,1}},new byte[][]{new byte[]{184,3},new byte[]{173,1},new byte[]{44,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{92,1},new byte[]{197,1}},new byte[][]{new byte[]{187,1},new byte[]{183,5},new byte[]{46,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{91,1},new byte[]{197,1}},new byte[][]{new byte[]{61,1},new byte[]{31,1},new byte[]{82,1},new byte[]{7,1},new byte[]{12,1},new byte[]{27,1},new byte[]{56,1},new byte[]{197,1}},new byte[][]{}}, // at gunner
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{61,1},new byte[]{9,1},new byte[]{3,1},new byte[]{23,1},new byte[]{5,1},new byte[]{26,1},new byte[]{197,1}},new byte[][]{new byte[]{62,1},new byte[]{51,1},new byte[]{3,1},new byte[]{23,1},new byte[]{5,1},new byte[]{26,1},new byte[]{197,1}},new byte[][]{new byte[]{63,1},new byte[]{3,1},new byte[]{23,1},new byte[]{5,1},new byte[]{26,1},new byte[]{197,1}},new byte[][]{new byte[]{64,1},new byte[]{3,1},new byte[]{23,1},new byte[]{5,1},new byte[]{26,1},new byte[]{197,1}},new byte[][]{}}, // thief
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{35,1},new byte[]{29,1},new byte[]{187,1},new byte[]{97,1},new byte[]{184,2},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{new byte[]{34,1},new byte[]{56,1},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{new byte[]{73,1},new byte[]{32,1},new byte[]{33,1},new byte[]{182,5},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{new byte[]{22,1},new byte[]{55,1},new byte[]{57,1},new byte[]{12,1},new byte[]{13,1},new byte[]{17,1},new byte[]{9,1},new byte[]{197,1}},new byte[][]{}}, // at fighter
  new byte[][][]{new byte[][]{},new byte[][]{new byte[]{5,1},new byte[]{34,1},new byte[]{17,1},new byte[]{26,1},new byte[]{36,1},new byte[]{197,1}},new byte[][]{new byte[]{35,1},new byte[]{17,1},new byte[]{21,1},new byte[]{26,1},new byte[]{37,1},new byte[]{197,1}},new byte[][]{new byte[]{67,1},new byte[]{60,1},new byte[]{1,1},new byte[]{39,1},new byte[]{18,1},new byte[]{25,1},new byte[]{37,1},new byte[]{43,1},new byte[]{197,1}},new byte[][]{new byte[]{184,5},new byte[]{91,1},new byte[]{102,1},new byte[]{18,1},new byte[]{25,1},new byte[]{37,1},new byte[]{43,1},new byte[]{197,1}},new byte[][]{}}, // at mage
 };
 // [job] first-awakening skill per grow 1..4 (index 0 unused), read from each script's
 // [growtype grow+1][awakening 1][awakening skill] row. Rows sit at the true job enum, so an
 // at-variant no longer receives its base family's awakening skill.
 internal static readonly byte[][] AwakeningFG202=new byte[][]{
  new byte[]{0,91,89,90,92}, // swordman
  new byte[]{0,72,70,65,66}, // fighter
  new byte[]{0,66,65,68,69}, // gunner
  new byte[]{0,73,75,84,110}, // mage
  new byte[]{0,86,87,89,122}, // priest
  new byte[]{0,94,93,95,99}, // at gunner
  new byte[]{0,56,58,0,0}, // thief
  new byte[]{0,114,65,116,115}, // at fighter
  new byte[]{0,51,50,84,110}, // at mage
 };
}
