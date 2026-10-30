using System;using System.IO;
// Multi-role store scoping. Every per-character archive (economy, progression, quest log, skill
// slots, experience, gold/item loot, clear journal) tags itself with the id of the role that owns
// it, and the batch service binds exactly one role at a time: when one is created and again on
// every character select. Current stays 1 until something is bound, so archives and fixtures that
// predate multi-role stores validate byte-for-byte as before.
class Offline70RoleScopeFG202 {
 internal static ushort Current=1;
 internal static byte CurrentJob=0;
 internal static void Bind(Offline70Role role){if(role==null||role.Id==0)throw new InvalidDataException("Role scope identity");Current=role.Id;CurrentJob=role.Job;}
 // Store validators only require a non-zero owner, because the roster reads every character's
 // archive in one pass. Ownership is therefore asserted here, at the boundary where a loaded
 // archive is handed to the role that is about to use it.
 internal static void Owns(Offline70Role role,ushort owner){if(role==null||role.Id==0||owner!=role.Id)throw new InvalidDataException("Archive owned by "+owner+" does not belong to role "+(role==null?0:role.Id));}
 // Role 1 is the character delivered before multi-role stores existed and its file names embed the
 // display name "yoyo"; those paths must not move. Later roles get id-keyed names instead of
 // name-keyed ones, so a client-supplied character name can never influence a file system path.
 internal static string EconomyFile(ushort id){return id==1?"economy-account1-yoyo-FG.xml":"economy-account1-r"+id+"-FG.xml";}
 internal static string ProgressionFile(ushort id){return id==1?"progression-yoyo-FG.xml":"progression-r"+id+"-FG.xml";}
 internal static string SkillsFile(ushort id){return id==1?"skills-yoyo-EZ.xml":"skills-r"+id+"-EZ.xml";}
}
