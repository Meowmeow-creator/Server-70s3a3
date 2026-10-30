using System;using System.Collections.Generic;using System.IO;
// FG376: friendly scene actors inside `[monster]` rows.
//
// The CN70 PVF ships `*_CharacterTeam.mob` variants of some monsters - the same actor re-used as a
// cutscene companion. The only one in reachable room rosters is 61114
// (`NewMonsters/Gbl/LadyZealot/LadyZealot_CharacterTeam.mob`, GBL女信徒, `[attack action]
// Action/speech.act`, empty `[attack info]`): GBL教阿拉德分部 quest 3802's start room 13000 lists
// it three times, the client draws it as a non-hostile actor and never lets the player damage it,
// so it can never die.
//
// Native `check_grid_clear()` (rednf UserCoreDepsB2/check_grid_clear.inc) only blocks on a monster
// with `dropCnt == 100 && m_field13 == 0`; this server's `RoomCleared` demands the whole roster,
// which turned a friendly actor into an unkillable door lock (live report 2026-10-28: the three
// cultists could not be damaged, the door lit up after the bookcases broke but would not admit the
// player until a GM kill-all swept them). Non-combatants therefore stay in the roster (the client
// needs them for the scene) but never gate the room.
internal static class Offline70NonCombatantsFG376{
 static readonly HashSet<ushort> Ids=new HashSet<ushort>();
 internal static int Count{get{return Ids.Count;}}
 internal static void Load(string path){
  Ids.Clear();
  if(!File.Exists(path))return;
  byte[] raw=File.ReadAllBytes(path);
  if(raw.Length<74||System.Text.Encoding.ASCII.GetString(raw,0,8)!="FG376NC1")throw new InvalidDataException("Non-combatant list magic");
  int at=8+64;
  int count=BitConverter.ToUInt16(raw,at);at+=2;
  if(count>4096||at+count*2>raw.Length)throw new InvalidDataException("Non-combatant list bounds");
  for(int i=0;i<count;i++){ushort id=BitConverter.ToUInt16(raw,at);at+=2;if(id==0)throw new InvalidDataException("Non-combatant id");Ids.Add(id);}
 }
 internal static bool Is(ushort monster){return Ids.Contains(monster);}
}
