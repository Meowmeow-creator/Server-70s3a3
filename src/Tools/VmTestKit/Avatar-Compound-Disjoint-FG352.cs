using System;using System.Collections.Generic;using System.IO;using System.Xml;using System.Xml.Serialization;
// FG352 avatar synthesis (op102) and Daphne avatar disassembly (op205).
//
// Wire evidence (rednf remake, authoritative ORIG disassembly):
//  * op102 = Dispatcher_Compound_Avatar (PacketDispatcher.inc m_dispatchers[102]); request
//    MSG_COMPOUND_AVATAR.h (sizeof 0x23, data 22 bytes after the header): u16 result target slot
//    (GetInvenSlot(1) = the avatar window), u16 material A slot + u32 item id + u16 material B
//    slot + u32 item id (GetInvenSlot(2) = the avatar window), i32 option index, u16 result slot
//    hint, u8 option-check flag, u8 compound type (0/1/3/4). The reply is op 0x66
//    (WongWorkMiscB_A/_SendResult): byte1=1, byte2=result triples (byte,short,int -- the two
//    consumed material slots), short result slot, int result item id, int remain date, short
//    stack field, int 0x1e + 30-byte jewel socket blob, int 4 + 4-byte expansion blob.
//  * The synthesizer is NOT in the request: CCompoundAvatar::_CheckCondition consumes it through
//    CheckNeedItemList against the compound table's [material]/[material_upper] rows and reports
//    0x16 (材料不足) when the player owns neither. m_type==1 rides the golden-synthesizer rate
//    bonus (the [upper bind cube rate bonus] server parameter), so type 1 prefers item 899.
//  * _CheckCondition error codes this reply forwards: 0x11 (not avatar/ownership), 0x17 (part or
//    job-pool mismatch), 0x16 (no synthesizer), 0x13 (period category differs).
//  * Materials are not required to be compound-table rows: the original only demands two avatar
//    items of the same part usable by one common job (the job loop picks the table), which for
//    this engine is metadata JobMask + EquipSlot. FG381 relaxed the old "material must be a pool
//    item" gate that rejected the CN 274xxxx avatars (e.g. 2743735 白色阴阳术士道服 is not a row
//    of compoundavatar_atmage.etc but is a legal material).
//  * op205 = Dispatcher_DisJointAvatar (Dispatchers/dispatch_sig_51.inc): state 3 (town), request
//    u16 avatar slot + u32 item id, consume one bag avatar, reply op 0xcd with
//    byte1=1, short avatar slot, short inserted count, then per granted row
//    short slot + u32 item id + int quantity.
//  * Result pools are the PVF compound tables etc/CompoundAvatar_<job>.etc ([<part> avatar]
//    weighted blocks, [material] 21 / [material_upper] 899) and the grade-1 disassembly pools
//    etc/disjoint.etc [avatar disjoint info] row 0 -> the four colour booster boxes
//    (emblem_booster{red,yellow,green,blue}1.stk, first [stackable] block).
//  * Live report 2026-10-27: the synthesizer froze the client (op102 was silently dropped as
//    UNIMPLEMENTED) and Daphne did nothing (op205 likewise); both ops now answer.
public class Offline70CompoundPoolRowFG352 {[System.Xml.Serialization.XmlAttribute]public int Job;[System.Xml.Serialization.XmlAttribute]public int Part;[System.Xml.Serialization.XmlAttribute]public uint Item;[System.Xml.Serialization.XmlAttribute]public uint Weight;}
public class Offline70EmblemRowFG352 {[System.Xml.Serialization.XmlAttribute]public uint Item;[System.Xml.Serialization.XmlAttribute]public uint Weight;}
public class Offline70CompoundPoolDocumentFG352 {public int Format;public List<int> Materials;public List<Offline70EmblemRowFG352> Emblems;public List<Offline70CompoundPoolRowFG352> Pools;}

class Offline70CompoundFG352 {
 internal const ushort NormalSynthesizer=21;   // 装扮合成器  stackable/cash/cube_bind.stk
 internal const ushort GoldenSynthesizer=899;  // 黄金装扮合成器 stackable/cash/cube_goldbind.stk
 internal static readonly Dictionary<uint,int> SlotOf=new Dictionary<uint,int>();          // avatar item -> job*8+part
 internal static readonly Dictionary<int,List<Offline70CompoundPoolRowFG352>> Pools=new Dictionary<int,List<Offline70CompoundPoolRowFG352>>();
 internal static readonly List<Offline70EmblemRowFG352> Emblems=new List<Offline70EmblemRowFG352>();
 internal static bool Loaded;
 internal static void Load(string path){
  if(new FileInfo(path).Length>4194304)throw new InvalidDataException("Compound pool size");
  Offline70CompoundPoolDocumentFG352 c;
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;c=(Offline70CompoundPoolDocumentFG352)new XmlSerializer(typeof(Offline70CompoundPoolDocumentFG352)).Deserialize(r);}
  if(c.Format!=1||c.Materials==null||c.Materials.Count!=2||c.Materials[0]!=NormalSynthesizer||c.Materials[1]!=GoldenSynthesizer||c.Emblems==null||c.Emblems.Count==0||c.Pools==null||c.Pools.Count==0)throw new InvalidDataException("Compound pool profile");
  SlotOf.Clear();Pools.Clear();Emblems.Clear();
  foreach(Offline70EmblemRowFG352 row in c.Emblems){
   if(row==null||row.Item==0||row.Weight==0)throw new InvalidDataException("Compound emblem row");
   Offline70Economy.Definition(row.Item);Emblems.Add(row);
  }
  foreach(Offline70CompoundPoolRowFG352 row in c.Pools){
   if(row==null||row.Job<0||row.Job>8||row.Part<0||row.Part>7||row.Item==0||row.Weight==0)throw new InvalidDataException("Compound pool row");
   Offline70Economy.Definition(row.Item);
   int key=row.Job*8+row.Part;List<Offline70CompoundPoolRowFG352> rows;
   if(!Pools.TryGetValue(key,out rows)){rows=new List<Offline70CompoundPoolRowFG352>();Pools.Add(key,rows);}
   foreach(Offline70CompoundPoolRowFG352 prior in rows)if(prior.Item==row.Item)throw new InvalidDataException("Duplicate compound pool item");
   rows.Add(row);
   int priorKey;if(SlotOf.TryGetValue(row.Item,out priorKey)&&priorKey!=key)throw new InvalidDataException("Compound item in two pools");
   SlotOf[row.Item]=key;
  }
  Loaded=true;
 }
 // Weighted draw inside one (job, part) pool. The golden synthesizer raises the chance of the
 // low-weight rare entries (their weight is the PVF rare slot of the block) by 3x, mirroring
 // the [upper rare rate] tier of the same table.
 internal static uint Roll(int key,bool golden,int draw){
  List<Offline70CompoundPoolRowFG352> rows;if(!Pools.TryGetValue(key,out rows))throw new InvalidDataException("Compound pool key");
  ulong total=0;foreach(Offline70CompoundPoolRowFG352 row in rows)total+=(ulong)Weight(row,golden);
  if(total==0)throw new InvalidDataException("Compound pool weight");
  ulong pick=(ulong)draw*total/10000UL;ulong run=0;
  foreach(Offline70CompoundPoolRowFG352 row in rows){run+=(ulong)Weight(row,golden);if(pick<run)return row.Item;}
  return rows[rows.Count-1].Item;
 }
 internal static uint RollEmblem(int draw){
  ulong total=0;foreach(Offline70EmblemRowFG352 row in Emblems)total+=row.Weight;
  if(total==0)throw new InvalidDataException("Emblem pool weight");
  ulong pick=(ulong)draw*total/10000UL;ulong run=0;
  foreach(Offline70EmblemRowFG352 row in Emblems){run+=row.Weight;if(pick<run)return row.Item;}
  return Emblems[Emblems.Count-1].Item;
 }
 static uint Weight(Offline70CompoundPoolRowFG352 row,bool golden){return golden&&row.Weight<1000?row.Weight*3:row.Weight;}
 internal static bool IsSynthesizer(uint item){return item==NormalSynthesizer||item==GoldenSynthesizer;}
 // FG381: the compound table a material pair feeds is the first job both avatars are usable on
 // (native _MakeCompoundInfo's job 0..10 loop), keyed to the pool of that job and the shared part.
 internal static int PoolKeyFor(byte partA,byte partB,uint maskA,uint maskB){
  if(partA!=partB)return -1;
  for(int job=0;job<=8;job++){
   if((maskA>>job&1)==0||(maskB>>job&1)==0)continue;
   if(Pools.ContainsKey(job*8+partA))return job*8+partA;
  }
  return -1;
 }
}

partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> compoundRequestsFG352=new Dictionary<ushort,string>();
 readonly Dictionary<ushort,string> avatarDisjointRequestsFG351=new Dictionary<ushort,string>();

 static HashSet<ushort> SlotsFG352(Offline70EconomyState s,byte list){HashSet<ushort> set=new HashSet<ushort>();foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==list)set.Add(e.Slot);return set;}
 static ushort GrantedSlotFG352(Offline70EconomyState s,byte list,uint item,HashSet<ushort> before){foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==list&&e.ItemId==item&&!before.Contains(e.Slot))return e.Slot;foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==list&&e.ItemId==item)return e.Slot;throw new InvalidDataException("Granted item slot not found");}
 static void ConsumeFG352(Offline70EconomyState s,byte list,ushort slot,uint item,int count){Offline70EconomyEntry e=Offline70Economy.Find(s,list,slot);if(e==null||e.ItemId!=item||e.Quantity<count)throw new InvalidDataException("Compound stale slot");e.Quantity-=(uint)count;if(e.Quantity==0)s.Inventory.Remove(e);}
 static int ConsumeSynthesizerFG381(Offline70EconomyState s,bool golden){
  uint preferred=golden?(uint)Offline70CompoundFG352.GoldenSynthesizer:(uint)Offline70CompoundFG352.NormalSynthesizer,fallback=golden?(uint)Offline70CompoundFG352.NormalSynthesizer:(uint)Offline70CompoundFG352.GoldenSynthesizer;
  foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&e.ItemId==preferred&&e.Quantity>0){e.Quantity--;if(e.Quantity==0)s.Inventory.Remove(e);return (int)preferred;}
  foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&e.ItemId==fallback&&e.Quantity>0){e.Quantity--;if(e.Quantity==0)s.Inventory.Remove(e);return (int)fallback;}
  return -1;
 }
 bool OwnsSynthesizerFG381(bool golden){
  uint preferred=golden?(uint)Offline70CompoundFG352.GoldenSynthesizer:(uint)Offline70CompoundFG352.NormalSynthesizer,fallback=golden?(uint)Offline70CompoundFG352.NormalSynthesizer:(uint)Offline70CompoundFG352.GoldenSynthesizer;
  foreach(Offline70EconomyEntry e in assets.Inventory)if(e.List==0&&(e.ItemId==preferred||e.ItemId==fallback)&&e.Quantity>0)return true;
  return false;
 }

 // op102: two same-part avatars + one synthesizer (table row, not a request field) -> one avatar
 // drawn from that (job, part) pool. Errors follow the native _CheckCondition codes so the client
 // renders the same text the original server would have produced.
 void AvatarCompoundFG352(byte[] body,ushort sequence){
  string fingerprint=Convert.ToBase64String(body),prior;
  if(compoundRequestsFG352.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Compound sequence reused");Note("COMPOUND_DUPLICATE_IGNORED seq="+sequence);return;}
  byte[] reply=null;Dictionary<ushort,uint> collection=null;string resultDetail=null;byte errorCode=0x16;
  try{
   if(dungeon.Active)throw new InvalidDataException("Avatar compound requires town");
   if(!Offline70CompoundFG352.Loaded){errorCode=0x11;throw new InvalidDataException("Compound pool not loaded");}
   byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,102%14,false);
   if(plain.Length<22||plain.Length>64)throw new InvalidDataException("Compound request size");for(int i=22;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Compound request padding");
   ushort targetSlot=BitConverter.ToUInt16(plain,0);   // MSG m_avatarSlot: GetInvenSlot(1), avatar window
   ushort matSlotA=BitConverter.ToUInt16(plain,2);uint matItemA=BitConverter.ToUInt32(plain,4);
   ushort matSlotB=BitConverter.ToUInt16(plain,8);uint matItemB=BitConverter.ToUInt32(plain,10);
   int optionIdx=BitConverter.ToInt32(plain,14);ushort slotHint=BitConverter.ToUInt16(plain,18);
   byte optionFlag=plain[20],type=plain[21];
   if(type!=0&&type!=1&&type!=3&&type!=4){errorCode=0x17;throw new InvalidDataException("Compound type");}
   if(matSlotA==matSlotB){errorCode=0x11;throw new InvalidDataException("Compound duplicate material slot");}
   Sync();
   Offline70EconomyEntry matA=Offline70Economy.Find(assets,1,matSlotA),matB=Offline70Economy.Find(assets,1,matSlotB);
   if(matA==null||matA.ItemId!=matItemA||matB==null||matB.ItemId!=matItemB){errorCode=0x11;throw new InvalidDataException("Compound material ownership");}
   Offline70EconomyItemDefinition defA=Offline70Economy.Definition(matItemA),defB=Offline70Economy.Definition(matItemB);
   if(defA.Kind!=3||defB.Kind!=3){errorCode=0x11;throw new InvalidDataException("Compound material kind");}
   int key=Offline70CompoundFG352.PoolKeyFor((byte)defA.EquipSlot,(byte)defB.EquipSlot,defA.JobMask,defB.JobMask);
   if(key<0){errorCode=0x17;throw new InvalidDataException("Compound materials differ in part or have no common job pool");}
   bool golden=type==1;
   if(!OwnsSynthesizerFG381(golden)){errorCode=0x16;throw new InvalidDataException("Compound synthesizer missing");}
   int draw=Offline70QuestDropsFG.StableDraws("compound-"+session+"-"+sequence+":"+matItemA+":"+matItemB+":"+type+":"+optionIdx)(10000);
   uint resultItem=Offline70CompoundFG352.Roll(key,golden,draw);
   HashSet<ushort> before=SlotsFG352(assets,1);
   collection=CollectionSnapshot();
   bool duplicate;int usedBox=-1;
   assets=Offline70Economy.Apply(economyPath,session+"-compound-"+sequence,"avatar-compound",matSlotA+":"+matItemA+":"+matSlotB+":"+matItemB+":"+type+":"+resultItem,delegate(Offline70EconomyState state){
    ConsumeFG352(state,1,matSlotA,matItemA,1);
    ConsumeFG352(state,1,matSlotB,matItemB,1);
    usedBox=ConsumeSynthesizerFG381(state,golden);
    Offline70Economy.AddAssetItem(state,resultItem,1);
   },out duplicate);
   ushort resultSlot=GrantedSlotFG352(assets,1,resultItem,before);
   using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
    w.Write((byte)1);w.Write((byte)2);
    w.Write((byte)1);w.Write(matSlotA);w.Write(1);
    w.Write((byte)1);w.Write(matSlotB);w.Write(1);
    w.Write(resultSlot);w.Write(resultItem);
    w.Write(0);                                    // remainDate: permanent gift avatar
    w.Write((ushort)1);                            // stack field of the granted row
    w.Write(0x1e);w.Write(new byte[0x1e]);         // jewel socket blob of a fresh avatar
    w.Write(4);w.Write(new byte[4]);               // expansion info of a fresh avatar
    reply=m.ToArray();
   }
   resultDetail="item="+resultItem+" slot="+resultSlot+" pool="+key+" golden="+golden+" box="+usedBox+" type="+type+" option="+optionIdx+" flag="+optionFlag+" target="+targetSlot+" slotHint="+slotHint;
  }catch(InvalidDataException e){
   resultDetail="REJECTED "+e.Message;
   reply=new byte[]{0,errorCode};
  }
  compoundRequestsFG352.Add(sequence,fingerprint);
  Send(resultDetail.StartsWith("REJECTED")?"avatar-compound-failed":"avatar-compound",Offline70RoleProtocol.BatchPacketFG(1,102,reply,keys,false));
  Note("COMPOUND "+(resultDetail.StartsWith("REJECTED")?"FAILED ":"COMMITTED ")+resultDetail+" seq="+sequence);
  if(collection!=null){Sync();CollectionChanged(collection);}
 }

 // op205: Daphne disassembles one bag avatar into one grade-1 emblem (disjoint.etc avatar row 0
 // -> the four colour booster boxes' first weighted block).
 void AvatarDisjointFG351(byte[] body,ushort sequence){
  string fingerprint=Convert.ToBase64String(body),prior;
  if(avatarDisjointRequestsFG351.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Avatar disjoint sequence reused");Note("AVATAR_DISJOINT_DUPLICATE_IGNORED seq="+sequence);return;}
  byte[] reply=null;Dictionary<ushort,uint> collection=null;string resultDetail=null;
  try{
   if(dungeon.Active)throw new InvalidDataException("Avatar disjoint requires a town");
   if(!Offline70CompoundFG352.Loaded)throw new InvalidDataException("Compound pool not loaded");
   byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,205%14,false);
   if(plain.Length<6||plain.Length>32)throw new InvalidDataException("Avatar disjoint request size");for(int i=6;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Avatar disjoint request padding");
   ushort slot=BitConverter.ToUInt16(plain,0);uint itemIdx=BitConverter.ToUInt32(plain,2);
   Sync();
   Offline70EconomyEntry avatar=Offline70Economy.Find(assets,1,slot);
   if(avatar==null||avatar.ItemId!=itemIdx)throw new InvalidDataException("Avatar disjoint ownership");
   if(Offline70Economy.Definition(avatar.ItemId).Kind!=3)throw new InvalidDataException("Avatar disjoint item kind");
   int draw=Offline70QuestDropsFG.StableDraws("avatar-disjoint-"+session+"-"+sequence+":"+slot+":"+itemIdx)(10000);
   uint emblem=Offline70CompoundFG352.RollEmblem(draw);
   HashSet<ushort> before=SlotsFG352(assets,0);
   collection=CollectionSnapshot();
   bool duplicate;
   assets=Offline70Economy.Apply(economyPath,session+"-avatar-disjoint-"+sequence,"avatar-disjoint",slot+":"+itemIdx+":"+emblem,delegate(Offline70EconomyState state){
    ConsumeFG352(state,1,slot,itemIdx,1);
    Offline70Economy.AddAssetItem(state,emblem,1);
   },out duplicate);
   ushort emblemSlot=GrantedSlotFG352(assets,0,emblem,before);
   using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
    w.Write((byte)1);
    w.Write(slot);w.Write((ushort)1);
    w.Write(emblemSlot);w.Write(emblem);w.Write(1);
    reply=m.ToArray();
   }
   resultDetail="avatar="+itemIdx+" slot="+slot+" emblem="+emblem+" emblemSlot="+emblemSlot;
  }catch(InvalidDataException e){
   resultDetail="REJECTED "+e.Message;
   reply=new byte[]{0,0x16};
  }
  avatarDisjointRequestsFG351.Add(sequence,fingerprint);
  Send(resultDetail.StartsWith("REJECTED")?"avatar-disjoint-failed":"avatar-disjoint",Offline70RoleProtocol.BatchPacketFG(1,205,reply,keys,false));
  Note("AVATAR_DISJOINT "+(resultDetail.StartsWith("REJECTED")?"FAILED ":"COMMITTED ")+resultDetail+" seq="+sequence);
  if(collection!=null){Sync();CollectionChanged(collection);}
 }
}
