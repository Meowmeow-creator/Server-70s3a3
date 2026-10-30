using System;using System.IO;using System.Collections.Generic;
// The FG service uses separate state; it keeps previous bounded profiles intact.
partial class Offline70RoleProtocol {
 internal static byte[] BatchCryptFG(byte[] data,byte[] keys,int index,bool encrypt){
  if(index==0){byte[] b=(byte[])data.Clone();for(int i=0;i<b.Length;i++)b[i]^=keys[4+i%4];return b;}
  if(index==1)return ProbeLoginCandidate.Crypt(data,keys,encrypt);
  if(index==2)return encrypt?new Blowfish(keys).Encode(data):new Blowfish(keys).Decode(data);
  if(index==3)return Offline70KeySettings.Crypt(data,keys,encrypt);
  if(index==4||index==11||index==13)return Offline70GoldCipher.Crypt(data,keys,index,encrypt);
  if(index>=5&&index<=8)return Offline70FlowCipherFG.Crypt(data,keys,index,encrypt);
  if(index==9||index==10)return Offline70AreaCipher.Crypt(data,keys,index,encrypt);
  if(index==12){if(data.Length%8!=0)throw new InvalidDataException("Batch MULTI2 bounds");byte[] b=new byte[data.Length];for(int at=0;at<data.Length;at+=8){byte[] chunk=new byte[8];Buffer.BlockCopy(data,at,chunk,0,8);DungeonLoadCrypt(chunk,keys,encrypt).CopyTo(b,at);}return b;}
  throw new InvalidDataException("Unregistered batch cipher");
 }
 internal static byte[] BatchPacketFG(byte kind,ushort opcode,byte[] plain,byte[] keys,bool integrity){if(integrity)plain=WithDungeonIntegrity(plain);int index=opcode%14,block=index==0?4:index==1||index==3||index==4||index==8||index==13?16:8;return Packet(kind,opcode,BatchCryptFG(Offline70GoldLoot.Pad(plain,block),keys,index,true));}
 internal static byte[] BatchTownReplyFG(ushort destination,ushort op,Offline70Role role,byte[] keys,int x,int y){BatchBaseRoleFG(role);if(destination!=2)throw new InvalidDataException("Unsupported return area");if(op==3)return Offline70TownProtocol.Reply(op,BatchBaseRoleFG(role),keys);return new Offline70Gate{Area=2,X=(ushort)x,Y=(ushort)y,Direction=5,UniqueId=role.Id}.Reply(op,keys);}
 internal static Offline70Role BatchBaseRoleFG(Offline70Role role){if(role.Id==0||!Offline70RoleCreateJobsFG202.CreatableFG202(role.Job)||String.IsNullOrEmpty(role.Name)||role.Level<1||role.Level>70)throw new InvalidDataException("FG saved role profile");return new Offline70Role{Id=role.Id,Job=role.Job,Level=1,Name=role.Name,CreatedUtc=role.CreatedUtc};}
 internal static byte[] BatchRosterFG(Offline70Role role,byte[] keys){Offline70Role basic=BatchBaseRoleFG(role);byte[] b=RosterBody(new List<Offline70Role>(new Offline70Role[]{basic}));b[15+NameEncoding.GetByteCount(role.Name)]=role.Level;return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(b))));}
 internal static byte[] BatchRosterFG(Offline70Role role,Offline70EconomyState assets,byte[] keys){
  Offline70Economy.Validate(assets);if(role.Id!=assets.RoleId||role.Job!=assets.Job||role.Level!=assets.Level)throw new InvalidDataException("Roster asset identity");
  byte[] basic=RosterBody(new List<Offline70Role>(new Offline70Role[]{BatchBaseRoleFG(role)}));basic[14+NameEncoding.GetByteCount(role.Name)]=assets.GrowType;basic[15+NameEncoding.GetByteCount(role.Name)]=role.Level;
  int at=18+NameEncoding.GetByteCount(role.Name);if(basic[at]!=0)throw new InvalidDataException("Roster worn row boundary");
  List<Offline70EconomyEntry> worn=new List<Offline70EconomyEntry>();foreach(Offline70EconomyEntry e in assets.Inventory)if(e.List==3)worn.Add(e);worn.Sort(delegate(Offline70EconomyEntry a,Offline70EconomyEntry b){return a.Slot.CompareTo(b.Slot);});
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(basic,0,at);w.Write((byte)worn.Count);foreach(Offline70EconomyEntry e in worn){w.Write((byte)e.Slot);w.Write(e.ItemId);w.Write((byte)0);w.Write((uint)0);w.Write((uint)0);}w.Write(basic,at+1,basic.Length-at-1);return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(m.ToArray()))));}
 }
 // Roster for every stored character. Each record keeps its own grow byte, level and worn-gear
 // rows, so a second character shows its own class and equipment instead of replaying role 1.
 // RosterBody emits a five-byte header per call, so only the first record's header is kept.
 internal static byte[] BatchRosterFG(List<Offline70Role> roles,List<Offline70EconomyState> saved,byte[] keys){
  if(roles.Count<1||roles.Count!=saved.Count||roles.Count>12)throw new InvalidDataException("Roster role list");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)2);w.Write((byte)SlotLimitOverrideFG208);w.Write((byte)SlotEffectCountFG208(roles.Count));w.Write((ushort)roles.Count);
   for(int index=0;index<roles.Count;index++){
    Offline70Role role=BatchBaseRoleFG(roles[index]);Offline70EconomyState assets=saved[index];
    byte[] one=RosterBody(new List<Offline70Role>(new Offline70Role[]{role}));int nameBytes=NameEncoding.GetByteCount(role.Name);
    // A null archive is the caller's per-character verdict. The identity row is still emitted, with the
    // stored level, grow 0 and no worn-gear rows, so the client keeps one row per role and its select
    // slot still addresses the same character instead of showing an empty list.
    List<Offline70EconomyEntry> worn=new List<Offline70EconomyEntry>();
    if(assets!=null){
     Offline70Economy.Validate(assets);if(role.Id!=assets.RoleId||role.Job!=assets.Job)throw new InvalidDataException("Roster asset identity");
     one[14+nameBytes]=assets.GrowType;one[15+nameBytes]=assets.Level;
     foreach(Offline70EconomyEntry e in assets.Inventory)if(e.List==3)worn.Add(e);worn.Sort(delegate(Offline70EconomyEntry a,Offline70EconomyEntry b){return a.Slot.CompareTo(b.Slot);});
    }else one[15+nameBytes]=roles[index].Level;
    int at=18+nameBytes;if(one[at]!=0)throw new InvalidDataException("Roster worn row boundary");
    w.Write(one,5,at-5);w.Write((byte)worn.Count);foreach(Offline70EconomyEntry e in worn){w.Write((byte)e.Slot);w.Write(e.ItemId);w.Write((byte)0);w.Write((uint)0);w.Write((uint)0);}w.Write(one,at+1,one.Length-at-1);
   }return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(m.ToArray()))));}
 }
 internal static byte[] BatchAppearanceFG(Offline70Role role,byte[] keys){byte[] b=AppearanceBody(BatchBaseRoleFG(role));b[15]=role.Level;return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(b))));}
 internal static byte[] BatchAdditionBodyFG(Offline70Role role,uint total,byte growByte){if(role.Level!=Offline70ProgressionFG.Level(total))throw new InvalidDataException("FG role/experience mismatch");byte[] b=AdditionBody(BatchBaseRoleFG(role),0,true);BitConverter.GetBytes(total).CopyTo(b,5);Offline70SwordmanGrowthFG.Stats(role.Job,growByte,role.Level).CopyTo(b,13);return b;}
 internal static byte[] BatchAdditionFG(Offline70Role role,uint total,byte growByte,byte[] keys){return Packet(0,2,new Blowfish(keys).Encode(Pad(Zlib(BatchAdditionBodyFG(role,total,growByte)))));}
 // Original notify48 (46D5C0): remaining, used, three auxiliary counters,
 // two flags. The HUD uses remaining/(remaining+used), not select-ACK values.
 // Auxiliary counters and flags remain zero in the existing offline profile.
 internal static byte[] BatchFatigueFG(int used,byte[] keys){if(used<0||used>156)throw new InvalidDataException("Diagnostic fatigue bounds");byte[] b=new byte[22];BitConverter.GetBytes((uint)(156-used)).CopyTo(b,0);BitConverter.GetBytes((uint)used).CopyTo(b,4);return BatchPacketFG(0,48,b,keys,false);}
 internal static byte[] BatchSelectFG(Offline70Role role,Offline70ProgressionStateFG state,Offline70QuestCatalogFG catalog,Offline70QuestEngineFG engine,byte[] keys,int town){if(state.Active.Count>20)throw new InvalidDataException("Native active quest limit");byte[] b=Offline70SelectCipher.Body(BatchBaseRoleFG(role),true,false,1,town);int slot=0;for(int i=0;i<state.Active.Count;i++){Offline70QuestActiveFG q=state.Active[i];if(!catalog.ActiveVisible(catalog.Get(q.Id),state))continue;BitConverter.GetBytes(q.Id).CopyTo(b,22+6*slot);BitConverter.GetBytes(engine.WireRemaining(catalog.Get(q.Id),q,state)).CopyTo(b,24+6*slot);slot++;}return BatchPacketFG(1,4,b,keys,false);}
 // Original senders serialize inventory/equipment and skill state summaries.
 // These vary with player assets; 101 was an old empty-inventory observation.
 // This offline service records both summaries without claiming an authoritative
 // recomputation. The caller admits only a selected player awaiting a room load.
 internal static byte[] BatchLoadRequestFG(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=8)throw new InvalidDataException("Native room-load request must contain two u32 summaries");return DungeonLoadCrypt(encrypted,keys,false);}
 // Single-player native42: 64 logical bytes plus fold4. The support/statistics
 // fields at 42/46 vary with the monster/map; 1/32 were only first-room
 // observations. Keep them as client reports, never as award authority.
 // Support the current native variable contributor list.
 // Integrity and the live victim roster remain checked; owner membership is evidence only.
 // Client statistics never determine XP, drops or the dungeon-clear award.
 internal static ushort BatchDeathRequestFG(byte[] encrypted,Offline70Role role,byte[] keys){return BatchDeathRequestFG(encrypted,role,keys,false);}
 internal static ushort BatchDeathRequestFG(byte[] encrypted,Offline70Role role,byte[] keys,bool liveGsd){return BatchDeathRequestFG(encrypted,role,keys,delegate(ushort id){return id==role.Id||liveGsd&&id==Offline70DungeonFlowFG.GsdUidFG130;});}
 // The current CN writer carries a byte count and ten bytes per contributor.
 // Owner/contributor keys do not set rewards. The service validates the exact
 // announced victim and its live room before committing death, XP and drops.
 // Actual CN70 no-attacker deaths: ownerFFFF, zero contributor rows, full original fold/padding.
 // Only ordinary announced victims qualify; Kill still requires the live loaded room roster.
 // An empty or foreign owner column never rejects the victim: unknown rows are skipped.
 internal static ushort BatchDeathRequestFG(byte[] encrypted,Offline70Role role,byte[] keys,Func<ushort,bool> ownerAllowed){bool passive;return BatchDeathRequestFG(encrypted,role,keys,ownerAllowed,out passive);}
 // Native die packet layout (rednf DisPatcherCore/dispatch_sig_16.inc read order):
 //   mobSeq(2) fieldF(2) int[4](16) mobCount(1) contributors[10] pairA24(2) pairA26(2)
 //   A22(1) A23(1) **passiveFlag(1)** moveCheck(1) unused(2) A28(1) A2d(4) A31(4)
 //   A29/A2b/A35/A37/A39/A3b/A3d(2 each)  => logical = 54 + 10*count, passiveFlag at tail+6.
 // passiveFlag==1 means the frame reports a destroyed passive object: m_mobSeq is the
 // object id (above the 100..163 monster uid range) and the native server answers it with
 // CParty::DestroyPassiveObject(mobSeq) only, never with kill_monster. The owner/actor
 // checks below therefore apply to monster deaths only; the frame's own layout, fold and
 // padding stay enforced for both shapes.
 internal static ushort BatchDeathRequestFG(byte[] encrypted,Offline70Role role,byte[] keys,Func<ushort,bool> ownerAllowed,out bool passive){
  passive=false;Offline70TownProtocol.Validate(BatchBaseRoleFG(role),keys);if(encrypted==null||encrypted.Length<60||encrypted.Length>2612||ownerAllowed==null)throw new InvalidDataException("Batch death native size");byte[] plain=BatchCryptFG(encrypted,keys,0,false);int count=plain[20],logical=54+10*count,padded=(logical+7)&~3;if(encrypted.Length!=padded)throw new InvalidDataException("Batch death layout");byte[] body=new byte[logical];Buffer.BlockCopy(plain,0,body,0,logical);byte[] folded=WithDungeonIntegrity(body);for(int i=0;i<4;i++)if(plain[logical+i]!=folded[logical+i])throw new InvalidDataException("Batch death integrity");for(int i=logical+4;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Batch death padding");ushort uid=BitConverter.ToUInt16(body,0),attacker=BitConverter.ToUInt16(body,2);int tail=21+10*count;passive=body[tail+6]==1;
  // Record the tail before any rejection so the evidence log keeps the passive flag.
  System.Text.StringBuilder tailFG=new System.Text.StringBuilder("tail=");
  for(int i=tail;i<tail+11&&i<body.Length;i++)tailFG.Append(body[i].ToString("x2"));
  LastDeathTailFG=tailFG.ToString()+(passive?" passive=True":"");
  if(passive){if(uid==0)throw new InvalidDataException("Batch death passive id");return uid;}
  // Native dispatch_sig_16 (ORIG 0x081eb0c4) reads each contributor row as
  //   (skillId u16, uid u16, damage u32, hitCount u16)
  // and its anti-cheat loop opens with `if (die.m_skillList[i] != 0x111) continue;`. A row carrying
  // another skill id is a different actor's damage - a monster hitting a monster (shadowmaze 7011 on
  // 2026-10-28 sent skill 0x211 rows 105->106/104 and 104->105), a summon or a pet - and is legal
  // evidence. Demanding 0x111 threw the whole death away, so the victim stayed alive in the room
  // roster and RoomCleared never fired (that room stalled at 8/11 dead and the door never opened).
  // Foreign, unknown and duplicate rows are therefore skipped exactly like the native loop skips
  // them; only the frame layout, the fold, the victim range and the caller's live roster gate a kill.
  bool unowned=count==0&&attacker==UInt16.MaxValue&&uid!=Offline70DungeonFlowFG.GsdUidFG130;
  // A monster is the legal announced attacker of a monster-vs-monster kill, so the actor gate admits
  // a live-roster uid (100..163) beside the friendly owner keys.
  if((uid<100||uid>163)&&uid!=Offline70DungeonFlowFG.GsdUidFG130||!unowned&&!(ownerAllowed(attacker)||attacker>=100&&attacker<=163))throw new InvalidDataException("Batch death actor bounds");
  HashSet<ushort> contributors=new HashSet<ushort>();int ownerRows=0;
  for(int i=0;i<count;i++){int at=21+10*i;ushort skill=BitConverter.ToUInt16(body,at),contributor=BitConverter.ToUInt16(body,at+2);if(skill!=0x111||!ownerAllowed(contributor)||!contributors.Add(contributor))continue;ownerRows++;}
  // An empty owner column is not fatal either: the victim still has to die on the server or the room
  // strands. Record the shape for the evidence log instead of refusing the kill.
  if(ownerRows==0&&count>0)LastDeathTailFG+=" foreign=True attacker="+attacker+" rows="+count;
  // The tail behind the contributor rows is client-reported room state, not a server
  // authority. The old "room flags zero + support flag <=1" rule came from kind-0 rows
  // only: a kind>=1 boss death fills the tail with its own APC-kill/flag bytes (the same
  // fields the notify tail of _MakeNotiPacketDieMonster carries), so the hard check
  // rejected the boss death report itself and the room never reached ReadyToClear - seen
  // 2026-10-26 on the dragon-tower boss (monster 71) with 21 kind-0 rows in the same run
  // passing. Record the bytes, enforce nothing; the live victim roster check below the
  // caller stays the gate.
  return uid;
 }
 internal static string LastDeathTailFG="";
 // Native DisPatcher_GetItem::process (rednf DisPatcherCore/process_43.inc) reads MSG_GET_ITEM as
 // slot(u16), flag(u8), pos(u8), item x/y(u16 each), checksum(u16, %7==0), a must-be-1 byte,
 // pad(u16) and pickup x/y(u16 each) - and rejects none of them: the values only feed the hack
 // counters, and pos>3 merely selects error 0x51 instead of 0x15 when get_field_item finds
 // nothing. The old "flag==0 && pos<=1 && every coordinate <=4096" test turned a legitimate
 // frame into a silent rejection that left the client's pickup action pending, so only the
 // cipher shape is enforced here; the ground table lookup below is the real gate (native 0x15).
 internal static byte[] BatchPickupRequestFG(byte[] encrypted,byte[] keys){if(encrypted==null||encrypted.Length!=16)throw new InvalidDataException("Batch pickup size");return BatchCryptFG(encrypted,keys,4,false);}
}
class Offline70GroundFG {internal ushort Slot;internal uint ItemId,Quantity;internal string Receipt;}
class Offline70LootWireFG {
 // Native _MakeNotiPacketDieMonster (ORIG 0x0830b8cc) tail is put_byte(0), then
 // convertAPCRoleType2MonsterRoleType(roleType) != 0 ? m_apcKillCount : -1, then flag34.
 // Monster kinds 1..3 and APC roles 6..8 convert non-zero (their deaths carry the count);
 // kind 0 and APC role 5 carry -1. The old code always wrote -1, which is what a boss death
 // must not do.
 internal static byte[] Death(ushort uid,IList<Offline70GroundFG> drops){return Death(uid,drops,-1);}
 internal static byte[] Death(ushort uid,IList<Offline70GroundFG> drops,int apcKillCount){return Death(uid,drops,apcKillCount,-1,-1);}
 // FG374: m_flag34 != 0 appends the new end position (rednf _MakeNotiPacketDieMonster: put_byte(0), apcKillCount, put_byte(flag34), then endX/endY when the flag is set). CMap::set_map gives a `[type] [dummy]` room's boss row that flag, so the fake boss death moves the client's boss marker instead of locking the doors.
 internal static byte[] Death(ushort uid,IList<Offline70GroundFG> drops,int apcKillCount,int endX,int endY){return Death(uid,drops,apcKillCount,endX,endY,65535);}
 // The drop row tail is the row's map index (native _MakeNotiPacketDieMonster ends each
 // row with put_short(mapIndex)); a hard-coded 65535 marks every ground item as belonging
 // to no map. Live reports of rare-item pickups going dead pointed at this tail.
 internal static byte[] Death(ushort uid,IList<Offline70GroundFG> drops,int apcKillCount,int endX,int endY,ushort mapIndex){if((uid<100||uid>163)&&uid!=Offline70DungeonFlowFG.GsdUidFG130||drops.Count>Offline70FloorDropsFG119.MaximumRows||uid==Offline70DungeonFlowFG.GsdUidFG130&&drops.Count!=0||apcKillCount>255)throw new InvalidDataException("Batch death rows");using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(uid);w.Write((byte)drops.Count);foreach(Offline70GroundFG d in drops){if(d.Slot<2000||d.Slot>Offline70FloorDropsFG119.LastSlot||d.Quantity==0)throw new InvalidDataException("Ground record");w.Write(d.Slot);w.Write(d.ItemId);w.Write(d.Quantity);w.Write((ushort)0);w.Write((byte)0);w.Write((ushort)0);w.Write((uint)0);w.Write(new byte[6]);w.Write(mapIndex);}w.Write(new byte[]{0,(byte)(apcKillCount<0?255:apcKillCount),(byte)(endX<0?0:1)});if(endX>=0){if(endX>31||endY<0||endY>31)throw new InvalidDataException("Death end position bounds");w.Write((byte)endX);w.Write((byte)endY);}return m.ToArray();}}
 // Native pickup notify 0x27: makePickupItemHeader(user->get_unique_id(), idx) writes
 // idx then the picker uid; appendPickupItem writes name[4], the recipient uid, the
 // inventory slot and the item type. Both uids are the picking role's unique_id - a
 // hard-coded 1 (the yoyo-era character) leaves the client's own pickup action pending
 // after the first item of a room, so every later pickup key press sends nothing.
 // Money keeps the same header, then four (present, amount, present, extra) rows.
 // FG396: the trailing itemType is a client space-route hint, not the full kind - native
 // sends 7 for a stackable whose client item type is 9 ([avatar emblem]) and for creature
 // items, 1 for avatar equipment, and 0 for everything else. Leaving the byte at 0 for
 // emblem pickups routed them like common stacks (rednf CDeathTower handlePickupItem).
 internal static byte[] Pickup(Offline70GroundFG d,ushort destination,ushort uniqueId){if(d.ItemId==0){if(d.Slot<2000||d.Slot>Offline70FloorDropsFG119.LastSlot||d.Quantity==0||d.Quantity>Offline70GoldLoot.Limit)throw new InvalidDataException("Floor gold pickup");byte[] gold=new byte[29];BitConverter.GetBytes(d.Slot).CopyTo(gold,0);BitConverter.GetBytes(uniqueId).CopyTo(gold,2);gold[4]=1;BitConverter.GetBytes(d.Quantity).CopyTo(gold,5);gold[9]=1;return gold;}byte type=0;try{Offline70EconomyItemDefinition def=Offline70Economy.Definition(d.ItemId);if(def.Kind==9||def.Kind==6||def.Kind==7||def.Kind==8)type=7;else if(def.Kind==3)type=1;}catch(InvalidDataException){}byte[] b=new byte[13];BitConverter.GetBytes(d.Slot).CopyTo(b,0);BitConverter.GetBytes(uniqueId).CopyTo(b,2);BitConverter.GetBytes(uniqueId).CopyTo(b,8);BitConverter.GetBytes(destination).CopyTo(b,10);b[12]=type;return b;}
}
