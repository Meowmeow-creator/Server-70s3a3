using System;using System.IO;
// CN70 sends the story-pause flag in command206. Native notify182 at4677B0
// reads uniqueId(u16), pauseFlag(u8), then resumes/suspends the scene.
// TW170 carries a different record and must not be copied into CN70.
class Offline70DungeonStoryFG91 {
 // The native reader pauses the scene when the notify byte is 0 and resumes it on any other value;
 // the u16 is only logged. So a 206 that never gets its 182 back strands the room forever - actors
 // and monsters stay frozen after the boss dialogue (epic_5_sunderland_mutant_5 / 落雷凯诺,
 // 疯掉的魔法师克拉赫). rednf's Dispatcher_DungeonEventStoryPause validates nothing and reads TWO
 // flag bytes there (pause, then resume-all), which is what the old "index<=1 and every later byte
 // zero" check rejected - precisely the resume half of the pair. Only the first two bytes count, so
 // uninitialised block padding cannot unfreeze a room that is still in its dialogue.
 internal static byte[] Plain(byte[] body,byte[] keys){
  if(body==null||body.Length==0||body.Length%8!=0)throw new InvalidDataException("Story206 request block");
  return Offline70RoleProtocol.BatchCryptFG(body,keys,206%14,false);
 }
 internal static byte Flag(byte[] plain){return plain[0]!=0||(plain.Length>1&&plain[1]!=0)?(byte)1:(byte)0;}
 internal static byte[] Reply(ushort uniqueId,byte index,byte[] keys){
  return Offline70RoleProtocol.BatchPacketFG(0,182,new byte[]{(byte)uniqueId,(byte)(uniqueId>>8),index},keys,false);
 }
}
