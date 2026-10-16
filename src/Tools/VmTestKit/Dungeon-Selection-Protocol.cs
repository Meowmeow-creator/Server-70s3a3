using System;using System.IO;
// Verified native70 fields and fixed-key fixtures for role1, Lv1 Lorien normal selection.
// Native packet readers are retained. This does not initialize a dungeon or combat.
class Offline70DungeonSelection {
 // User-authorized difficulty override for every implemented dungeon profile.
 internal const byte TestMaximumDifficulty=3;
 internal static readonly ushort[] Opcodes=new ushort[]{3,26,5,27};
 internal static byte[] Body(ushort opcode){
  if(opcode==3)return new byte[]{1,1,0,1}; // u8 count, u16 actor, u8 state=selection
  if(opcode==26)return new byte[]{0}; // local party host seat
  if(opcode==5)return new byte[]{2,0,1,0,TestMaximumDifficulty,2,0,TestMaximumDifficulty};
  if(opcode==27)return new byte[13]; // u32 hell state; 3 flag/count bytes; empty item group count; u32 guide; u8 share
  throw new InvalidDataException("Unverified dungeon selection opcode");
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys){
  return Reply(opcode,role,keys,false);
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys,bool forest){
  return Reply(opcode,role,keys,forest?2:1);
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys,int town){
  return Reply(opcode,role,keys,town,3);
 }
 internal static byte[] Reply(ushort opcode,Offline70Role role,byte[] keys,int town,int area){
  Offline70TownProtocol.Validate(role,keys);string cipher;
  if(opcode==3)cipher="107e8e0553966c9b9ffbc7f26f9b2855";
  else if(opcode==26)cipher="e55bd513598bfde3";
  else if(opcode==5)return Offline70RoleProtocol.Packet(0,5,Offline70RoleProtocol.BatchCryptFG(Offline70RoleProtocol.Pad(town==2?ForestBody():town==3?((area==4||area==6)?GblBody():SkyBody()):town==4?(area==3?AncientBody():Act4Body()):town==5?StormPassBody():Body(5)),keys,5,true));
  else if(opcode==27)cipher="67dda5765662d5df55ca73c1454fae2f";
  else throw new InvalidDataException("Unverified dungeon selection opcode");
  return Offline70RoleProtocol.Packet(0,opcode,Offline70TownProtocol.Hex(cipher));
 }
 internal static byte[] ProfileBody(ushort[] ids){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((ushort)ids.Length);foreach(ushort id in ids){w.Write(id);w.Write(TestMaximumDifficulty);}return m.ToArray();}}
 internal static byte[] ForestBody(){return ProfileBody(new ushort[]{3,4,5,6,7,8,9,1000});}
 internal static byte[] SkyBody(){return ProfileBody(new ushort[]{11,12,13,14,15,17});}
 internal static byte[] GblBody(){return ProfileBody(new ushort[]{21,22,23,24,25,26,27});}
 // Act4 (暗黑城) is offered by 阿法利亚 (town4) gates: town/alfhlyra.twn marks area2 as
 // `[dungeon gate] 5` (the Act4 group), area3/area4 as gates 11/13 which hold nothing else
 // in this build, so all three areas currently serve the same client-derived list.
 internal static byte[] Act4Body(){return ProfileBody(new ushort[]{31,32,33,34,35,36,37});}
 // Ancient dungeon (诺伊佩拉, 1502) is offered by 阿法利亚 (town4) area3 gate.
 // area2 keeps the Act4 group (31..37); area4 is fail-closed (no dungeons).
 // Client evidence: at 阿法利亚 area3 (gate 11) the client shows the 暗黑城 dungeon list from its
// own data, so Act4 ids must stay enterable there (8 rejected op16 attempts on 2026-10-15) while
// 诺伊佩拉 is also reachable. The profile body therefore carries both groups at area3.
internal static byte[] AncientBody(){return ProfileBody(new ushort[]{31,32,33,34,35,36,37,1502});}
 // 万年雪山 (Snow Mountain / StormPass, town5) offers its two dungeons 40 (CharnakRidge) and
 // 41 (TheBoyWithColdheart) from the client's own dungeon-gate areas area2/area3
 // (town/stormpass.twn marks both as `[dungeon gate] 6`).
 internal static byte[] StormPassBody(){return ProfileBody(new ushort[]{40,41,42,43,44,45,141});}
 internal static bool SupportedInTown(int town,ushort id){return SupportedInArea(town,3,id);}
 internal static bool SupportedInArea(int town,int area,ushort id){return town==1&&(id==1||id==2)||town==2&&(id>=3&&id<=9||id==1000)||town==3&&((area==3||area==5)&&(id>=11&&id<=15||id==17)||(area==4||area==6)&&id>=21&&id<=27)||town==4&&(area==2&&id>=31&&id<=37||area==3&&(id>=31&&id<=37||id==1502))||town==5&&(area==2||area==3)&&(id>=40&&id<=45||id==141);}
}
