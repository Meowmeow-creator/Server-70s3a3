using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

// Current CN70 PVF commodity IDs, prices, durations and CERA_SPECIALITEM66.
// Permanent style ownership and the second skill page commit with the wallet.
public class Offline70PremiumFG129 { public byte Kind; public uint Expires; }

class Offline70FeedbackFG129 {
 internal static readonly Dictionary<uint,Offline70CashPurchaseFG45.Offer> Offers=BuildOffers();
 static Dictionary<uint,Offline70CashPurchaseFG45.Offer> BuildOffers(){
  Dictionary<uint,Offline70CashPurchaseFG45.Offer> d=new Dictionary<uint,Offline70CashPurchaseFG45.Offer>();
  Add(d,100039,31,780,3,22,3);Add(d,100041,33,1180,7,22,3);
  Add(d,100058,44,1080,3,27,3);Add(d,100059,45,1680,7,27,3);
  Add(d,100205,193,780,30,29,3);Add(d,100494,821,5800,0,0,4);
  uint[] ids={100063,100064,100065,100066,100067,100068,100069,101062};
  uint[] items={50,57,58,59,60,61,62,2660235};uint[] costs={60,120,240,480,960,1920,2100,2100};
  for(int i=0;i<ids.Length;i++)Add(d,ids[i],items[i],costs[i],(uint)(24+16*i),0,5);
  // cerashop.etc [selectable character premium]: name-last rows of 8 ints
  // (0, -1, commodity, item, premiumType, days, 0, price) + name. The service item ids were
  // cross-checked against their .stk [name] fields, so the type mapping below is exact; these
  // are the individually sold 魔王之契约 services the 60-day package (FG272) bundles.
  Add(d,100542,2681934,200,7,0,3);Add(d,100539,2681931,250,7,1,3);Add(d,100540,2681932,200,7,2,3);
  Add(d,100541,2681933,250,7,3,3);Add(d,100537,2681929,200,7,4,3);Add(d,100538,2681930,200,7,5,3);
  Add(d,100817,2682205,250,7,6,3);Add(d,100882,2682206,200,7,7,3);Add(d,100983,2682377,250,7,8,3);
  Add(d,100550,2681934,500,30,0,3);Add(d,100547,2681931,500,30,1,3);Add(d,100548,2681932,300,30,2,3);
  Add(d,100549,2681933,500,30,3,3);Add(d,100545,2681929,300,30,4,3);Add(d,100546,2681930,300,30,5,3);
  Add(d,100818,2682205,500,30,6,3);Add(d,100883,2682206,300,30,7,3);Add(d,100984,2682377,500,30,8,3);
  return d;
 }
 static void Add(Dictionary<uint,Offline70CashPurchaseFG45.Offer> d,uint id,uint item,uint price,uint duration,ushort kind,byte mode){
  d.Add(id,new Offline70CashPurchaseFG45.Offer{Commodity=id,Category=mode==5?2u:3u,Item=item,Cost=price,Days=duration,Ability=kind,Special=mode,Rewards=new uint[][]{new uint[]{item,1}}});
 }
 internal static Offline70CashPurchaseFG45.Offer Resolve(Offline70CashPurchaseFG45.Request r){
  Offline70CashPurchaseFG45.Offer o;if(r==null||!Offers.TryGetValue(r.Commodity,out o))return null;
  if(r.Payment!=0||r.Option!=255||r.Period!=0&&r.Period!=255)throw new InvalidDataException("Contract/style/cargo cash selection");return o;
 }
 internal static bool Valid(Offline70CashPurchaseFG45.Offer o){Offline70CashPurchaseFG45.Offer known;return o!=null&&Offers.TryGetValue(o.Commodity,out known)&&Object.ReferenceEquals(known,o);}
 internal static void Validate(Offline70EconomyState s){
  if(s.CargoSlotsFG129<8||s.CargoSlotsFG129>136||(s.CargoSlotsFG129-8)%16!=0||s.PremiumsFG129==null||s.PremiumsFG129.Count>32||s.ActiveStyleFG129>1||!s.StyleOwnedFG129&&(s.ActiveStyleFG129!=0||s.SecondSkillsFG129!=null)||s.StyleOwnedFG129&&s.SecondSkillsFG129==null)throw new InvalidDataException("Contract/cargo/style store profile");
  HashSet<byte> kinds=new HashSet<byte>();foreach(Offline70PremiumFG129 p in s.PremiumsFG129)if(p==null||p.Kind>105||p.Expires==0||!kinds.Add(p.Kind))throw new InvalidDataException("Premium expiration state");
  if(s.SecondSkillsFG129!=null){Offline70SkillSlots.Validate(s.SecondSkillsFG129);Offline70SkillSlots.Remaining(s.SecondSkillsFG129,s.Level,s.BonusSp);}
  foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==2&&e.Slot>=s.CargoSlotsFG129)throw new InvalidDataException("Cargo slot exceeds owned capacity");
 }
 internal static bool Active(Offline70EconomyState s,byte kind,uint now){foreach(Offline70PremiumFG129 p in s.PremiumsFG129)if(p.Kind==kind&&p.Expires>now)return true;return false;}
 internal static void Grant(Offline70EconomyState s,Offline70CashPurchaseFG45.Offer o,uint now){
  if(!Valid(o))throw new InvalidDataException("Unregistered contract/cargo/style offer");
  if(o.Special==3){Offline70PremiumFG129 found=null;foreach(Offline70PremiumFG129 p in s.PremiumsFG129)if(p.Kind==o.Ability)found=p;
   if(found==null){found=new Offline70PremiumFG129{Kind=(byte)o.Ability};s.PremiumsFG129.Add(found);}found.Expires=checked(Math.Max(now,found.Expires)+o.Days*86400u);
  }else if(o.Special==4){if(s.StyleOwnedFG129)throw new InvalidDataException("Skill style already purchased");s.SecondSkillsFG129=Offline70SkillSlots.NewStyleFG129();s.StyleOwnedFG129=true;s.ActiveStyleFG129=0;
  }else if(o.Special==5){if(o.Days!=s.CargoSlotsFG129+16)throw new InvalidDataException("Cargo upgrade requires the next owned tier");s.CargoSlotsFG129=checked((ushort)o.Days);}
 }
 internal static byte[] PremiumBody(Offline70PremiumFG129 p,uint now){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((ushort)2);w.Write(p.Kind);w.Write(p.Expires>now?p.Expires-now:0u);return m.ToArray();}}
 internal static byte[] CargoUpgradeBody(ushort slots){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((ushort)1);w.Write(slots);return m.ToArray();}}
 internal static byte[] SkillsBody(Offline70SkillSlotState first,Offline70EconomyState s){return SkillsBody(first,s,null);}
 // Reference send_skill_info 0x0866c46a: after each mastered skill's {slot,id,level} the record
 // carries the stored command style — cmdCount then cmdCount key bytes, count 0 meaning "use the
 // default command". The old fixed trailing zero was exactly the count-0 case, so this stays
 // byte-identical for a role with no stored commands.
 internal static byte[] SkillsBody(Offline70SkillSlotState first,Offline70EconomyState s,List<Offline70SkillCommandFG344.Entry> commands){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   foreach(Offline70SkillSlotState bank in new Offline70SkillSlotState[]{first,s.SecondSkillsFG129}){
    if(bank==null){w.Write((ushort)0);w.Write((byte)0);continue;}
    Offline70SkillSlots.Validate(bank);w.Write(Offline70SkillSlots.Remaining(bank,s.Level,s.BonusSp));w.Write((byte)Offline70SkillSlots.VisibleCountFG139(bank));
    for(int i=0;i<bank.Ids.Length;i++){if(bank.Levels[i]==0)continue;w.Write(bank.Slots[i]);w.Write(bank.Ids[i]);w.Write(bank.Levels[i]);
     Offline70SkillCommandFG344.Entry? cmd=Offline70SkillCommandFG344.Find(commands,bank.Ids[i]);
     /* Lock markers ({1,[8]}) must NOT ride the tail: the client packs keys into 3-bit fields,
        so 8 truncates to 0 and would REWRITE the skill's displayed manual command. Replay only
        entries with real keys (true op344 customizations); locks ride op464 (client patch pending). */
     if(cmd!=null&&cmd.Value.Count>0&&cmd.Value.Keys[0]!=Offline70SkillCommandFG344.LockKeyFG344){w.Write(cmd.Value.Count);for(int k=0;k<cmd.Value.Count&&k<cmd.Value.Keys.Length;k++)w.Write(cmd.Value.Keys[k]);}
     else w.Write((byte)0);}
   }w.Write(s.BonusTp);w.Write((ushort)0);return m.ToArray();
  }
 }
 // ORIG send_skill_info (rednf CUser/send_skill_info.inc) ends op19 with
 // get_remain_sfp_at_index(2) then (3): the page-0 and page-1 特性技能点 (TP) wallets.
 // Page-1 TP rides the style page's own wallet, which this service never grants.
 internal static byte[] StyleReplyBody(Offline70EconomyState s,Offline70SkillSlotState bank){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
  w.Write((byte)1);w.Write(s.ActiveStyleFG129);w.Write((byte)0); // no transient skill-strength modifiers
  w.Write(s.Gold);w.Write(Offline70SkillSlots.Remaining(bank,s.Level,s.BonusSp));w.Write((byte)0);return m.ToArray();}}
}

partial class Offline70SkillSlots {
 internal static Offline70SkillSlotState NewStyleFG129(){Offline70SkillSlotState s=Default();Validate(s);for(int i=0;i<s.Ids.Length;i++)if(!Quick(s.Slots[i])&&!ListSlotFG123(s.Ids[i],s.Slots[i]))s.Slots[i]=255;for(int i=0;i<s.Ids.Length;i++)if(s.Slots[i]==255)s.Slots[i]=FindListFG123(s,s.Ids[i]);s.CommonPlacementFG81=true;Validate(s);return s;}
 internal static void LearnStateFG129(Offline70SkillSlotState s,byte level,byte id,byte next,ushort bonus,byte grow,bool awakened,int extraLevels){
  Offline70SkillRuleFG r=Rule(id);Validate(s);
  // 特性技能 carry Cost=0 (no SP) and a per-level [special purchase cost] TP price; the caller
  // (Batch-Service-FG LearnSkill) checks funds and deducts BonusTp before reaching here, matching
  // rednf CSkill::get_need_sp_sfp_for_level branching on IsSpecialSkill.
  // Cost==0 *active* skills are the 魔化-form sub-skills (复仇者 `审判` priest/execution.skl id 120 is
  // the only one in CN70; mage 39 the other): they cost no SP and are learned through the tree, so
  // the ORIG master_new_skill charges 0 and admits them. Cost==0 *passives* stay refused - those are
  // expert-job grants (191..194) and auto-granted masteries.
  if(r==null||r.Cost==0&&!(r.Trait&&r.TraitCost>0)&&!r.Active||next!=SkillLevel(s,id)+1||JobMaximum(r,grow,awakened)==0||next>JobMaximum(r,grow,awakened)||level+extraLevels<r.Required+(next-1)*r.Step||Remaining(s,level,bonus)<r.Cost)throw new InvalidDataException("Skill level, role level or SP requirement rejected");
  for(int p=0;p<r.Prerequisites.Length;p+=2)if(SkillLevel(s,r.Prerequisites[p])<r.Prerequisites[p+1])throw new InvalidDataException("Skill prerequisite rejected");
  int i=Array.IndexOf(s.Ids,id);if(i<0){byte slot=FindListFG123(s,id);i=s.Ids.Length;Array.Resize(ref s.Ids,i+1);Array.Resize(ref s.Slots,i+1);Array.Resize(ref s.Levels,i+1);s.Ids[i]=id;s.Slots[i]=slot;}
  s.Levels[i]=next;s.SpentSp=checked((ushort)(s.SpentSp+r.Cost));Validate(s);
 }
 internal static Offline70SkillSlotState LearnOneFG129(string path,byte level,byte id,ushort bonus,byte grow,bool awakened,int extraLevels){string full=Path.GetFullPath(path);using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=Load(full);LearnStateFG129(s,level,id,checked((byte)(SkillLevel(s,id)+1)),bonus,grow,awakened,extraLevels);CommitResetFG83(full,s);return Load(full);}}
 internal static void SwapStateFG129(Offline70SkillSlotState s,byte source,byte target){if(source==target||!ActiveSlot(source)||!ActiveSlot(target))throw new InvalidDataException("Skill swap range");bool found=false;for(int i=0;i<s.Slots.Length;i++)if(s.Slots[i]==source||s.Slots[i]==target){byte next=s.Slots[i]==source?target:source;if(!MoveSlotFG147(s,i,next))throw new InvalidDataException("Passive shortcut rejected");s.Slots[i]=next;found=true;}if(!found)throw new InvalidDataException("No learned active skill in swap");Validate(s);}
}

partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> styleRequestsFG129=new Dictionary<ushort,string>();readonly Dictionary<ushort,byte[]> styleRepliesFG129=new Dictionary<ushort,byte[]>();
 readonly HashSet<ushort> styleSucceededFG129=new HashSet<ushort>();readonly HashSet<byte> premiumClientActiveFG129=new HashSet<byte>();uint lastPremiumScanFG129;
 Offline70SkillSlotState ActiveSkillsFG129(){return assets.ActiveStyleFG129==0?Offline70SkillSlots.Load(skillPath):assets.SecondSkillsFG129;}
 // Skill locks ride two carriers at every world entry: S2C op369 (harmless to clients that stop at
 // op368) and the 512-byte skilllock.dat next to the client exe, which the FG344 v5 patch reads
 // back into the lock table after the world-entry memset. op464 remains the upload side, so the
 // loop is "client dumps its table -> per-role store -> per-role file + op369 at every world entry".
 void PublishClientSkillLockFG344(){
  try{
   List<Offline70SkillCommandFG344.Entry> all=Offline70SkillCommandFG344.Load(commandPath);
   int ids;byte[] body=Offline70SkillCommandFG344.LockPacketBodyFG344(all,out ids);
   Send("skill-lock369",Offline70RoleProtocol.BatchPacketFG(0,369,body,keys,false));
   string lockFile=Offline70SkillCommandFG344.ResolveClientLockFileFG344(folder);
   bool published=lockFile!=null&&Offline70SkillCommandFG344.PublishClientLockFileFG344(lockFile,all);
   Note("CLIENT_LOCK_PACKET_SENT role="+role.Id+" ids="+ids+" bytes="+body.Length+(published?" file="+lockFile:" file=SKIP"));
  }catch(Exception e){Note("CLIENT_LOCK_PACKET_SKIP "+e.GetType().Name+" "+e.Message);}
 }
 byte[] SkillsReplyFG129(){List<Offline70SkillCommandFG344.Entry> cmdsFG344=Offline70SkillCommandFG344.Load(commandPath);byte[] bodyFG344=Offline70FeedbackFG129.SkillsBody(Offline70SkillSlots.Load(skillPath),assets,cmdsFG344);Note("SKILLS19 body="+bodyFG344.Length+" cmds="+(cmdsFG344==null?"null":cmdsFG344.Count.ToString()));return Offline70RoleProtocol.BatchPacketFG(0,19,bodyFG344,keys,false);}
 // CMD 464: the client dumps its current skill-lock table on every skill-window toggle — 520
 // bytes (Blowfish, index 2): u16 2, u16 0, u32@4 (0xFFFFFFFF on the initializing window-open
 // dump), 256B region A (0xff fill), 128×u16 locked skill ids at offset 260 (0xffff filler),
 // u32 tail. Store the list so it survives relog; the replay rides op19 (SkillsBody).
 void SkillLockTableFG344(byte[] body){
  try{
   if(body.Length!=520)throw new InvalidDataException("Skill lock table length "+body.Length);
   byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,2,false);
   uint init=BitConverter.ToUInt32(plain,4);
   if(init==0xFFFFFFFFu){Note("SKILL_LOCK_TABLE skipped initial-dump");return;}
   List<byte> ids=new List<byte>();
   for(int i=260;i<516;i+=2){ushort id=BitConverter.ToUInt16(plain,i);if(id!=0xFFFF&&id>=1&&id<=255&&!ids.Contains((byte)id))ids.Add((byte)id);}
   List<Offline70SkillCommandFG344.Entry> all=Offline70SkillCommandFG344.Load(commandPath)??new List<Offline70SkillCommandFG344.Entry>();
   all.RemoveAll(delegate(Offline70SkillCommandFG344.Entry e){return e.Count==1&&e.Keys[0]==Offline70SkillCommandFG344.LockKeyFG344;});
   foreach(byte id in ids)Offline70SkillCommandFG344.Merge(all,Offline70SkillCommandFG344.Lock(id));
   Offline70SkillCommandFG344.Save(commandPath,all);
   Note("SKILL_LOCK_TABLE ids=["+string.Join(",",ids)+"] entries="+all.Count);
  }catch(InvalidDataException e){Note("SKILL_LOCK_REJECTED "+e.Message);}
 }
 // CMD 344 SKILL_COMMAND_CUSTOMIZING. The reference wipes every stored command when one entry
 // fails validation (process 0x081d2b42 → defaultSkillCommand); a single-player bad entry must
 // not burn the whole bar, so this port keeps the store and only rejects the request.
 void CustomizeSkillCommandFG344(byte[] body,ushort sequence){
  ushort err=0;int applied=0;
  try{
   Offline70SkillSlotState bank=Offline70SkillSlots.Load(skillPath);
   HashSet<byte> learned=new HashSet<byte>(bank.Ids);
   if(assets.SecondSkillsFG129!=null)foreach(byte id in assets.SecondSkillsFG129.Ids)learned.Add(id);
   List<Offline70SkillCommandFG344.Entry> parsed=Offline70SkillCommandFG344.Parse344(body,keys,learned);
   List<Offline70SkillCommandFG344.Entry> all=Offline70SkillCommandFG344.Load(commandPath)??new List<Offline70SkillCommandFG344.Entry>();
   foreach(Offline70SkillCommandFG344.Entry e in parsed){Offline70SkillCommandFG344.Merge(all,e);applied++;}
   Offline70SkillCommandFG344.Save(commandPath,all);
  }catch(InvalidDataException e){err=Offline70SkillCommandFG344.ErrorCode(e.Message);Note("SKILL_COMMAND_REJECTED code="+err+" reason="+e.Message);}
  if(err==0){Send("skill-command-ack332",Offline70RoleProtocol.BatchPacketFG(1,332,new byte[]{1},keys,false));Note("SKILL_COMMAND_SAVED entries="+applied+" total="+(Offline70SkillCommandFG344.Load(commandPath)??new List<Offline70SkillCommandFG344.Entry>()).Count);}
  else Send("skill-command-ack332",Offline70RoleProtocol.BatchPacketFG(1,332,new byte[]{0,(byte)err,0},keys,false));
 }
 // CMD 345 SKILL_COMMAND_ALL_DEFAULT: reference defaultSkillCommand returns false (failure code
 // 0xfc) when the map is already empty, and the ACK body mirrors the 344 shape.
 void DefaultSkillCommandFG345(){
  bool any=File.Exists(commandPath);
  if(any)File.Delete(commandPath);
  Send("skill-command-default-ack333",Offline70RoleProtocol.BatchPacketFG(1,333,any?new byte[]{1}:new byte[]{0,0xfc,0},keys,false));
  Note("SKILL_COMMAND_DEFAULT cleared="+(any?"True":"already-empty"));
 }
 void PremiumNoticesFG129(){uint now=Offline70PotFG93.UnixNow();premiumClientActiveFG129.Clear();foreach(Offline70PremiumFG129 p in assets.PremiumsFG129){Send("contract-state66",Offline70RoleProtocol.BatchPacketFG(0,66,Offline70FeedbackFG129.PremiumBody(p,now),keys,false));if(p.Expires>now)premiumClientActiveFG129.Add(p.Kind);}}
 void MaintainPremiumFG129(){if(!selected||assets==null)return;uint now=Offline70PotFG93.UnixNow();if(lastPremiumScanFG129==now)return;lastPremiumScanFG129=now;foreach(Offline70PremiumFG129 p in assets.PremiumsFG129)if(p.Expires<=now&&premiumClientActiveFG129.Remove(p.Kind)){Send("contract-expired66",Offline70RoleProtocol.BatchPacketFG(0,66,Offline70FeedbackFG129.PremiumBody(p,now),keys,false));Note("CONTRACT_EXPIRED kind="+p.Kind+" entitlement=False walletUnchanged=True");}}
 void CashRefreshFG129(Offline70CashPurchaseFG45.Offer[] offers){bool style=false,cargo=false,premium=false;foreach(Offline70CashPurchaseFG45.Offer o in offers){if(o.Special==3||o.Special==10||o.Special==11)premium=true;if(o.Special==4)style=true;if(o.Special==5)cargo=true;}
  if(cargo){Send("cargo-upgrade66",Offline70RoleProtocol.BatchPacketFG(0,66,Offline70FeedbackFG129.CargoUpgradeBody(assets.CargoSlotsFG129),keys,false));Send("cargo-restored13",Offline70EconomyProtocol.CargoReplyFG129(assets,keys));}
  if(premium)PremiumNoticesFG129();if(style){Appearance();Send("style-owned-addition2",AdditionReplyFG129());Send("style-owned-skills19",SkillsReplyFG129());}
 }
 byte[] AdditionReplyFG129(){byte[] b=Offline70EconomyProtocol.AdditionBody(role,assets);int prefix=101+Offline70EconomyProtocol.WornRecord(assets).Length+8;
  // 7FCC80 reads the active page and both skill-level tables; local ownership is
  // separately read by7FC6A0 from the appearance record's sixth byte from the end.
  // Preserve all subsequent stat/QP fields and all worn instance data.
  int start=prefix;int oldCount=b[start+1];int second=start+2+2*oldCount;int oldSecond=b[second];int end=second+1+2*oldSecond;
  Offline70SkillSlotState first=Offline70SkillSlots.Load(skillPath),other=assets.SecondSkillsFG129;
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(b,0,start);w.Write(assets.StyleOwnedFG129?assets.ActiveStyleFG129:(byte)255);
   foreach(Offline70SkillSlotState page in new Offline70SkillSlotState[]{first,other}){w.Write((byte)(page==null?0:Offline70SkillSlots.VisibleCountFG139(page)));if(page!=null)for(int i=0;i<page.Ids.Length;i++){if(page.Levels[i]==0)continue;w.Write(page.Ids[i]);w.Write(page.Levels[i]);}}
   w.Write(b,end,b.Length-end);return Offline70RoleProtocol.BatchPacketFG(0,2,Offline70RoleProtocol.Zlib(m.ToArray()),keys,false);
  }
 }
 void ChangeStyleFG129(byte[] encrypted,ushort sequence){string fingerprint=Convert.ToBase64String(encrypted),prior;if(styleRequestsFG129.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Style sequence reused");Sync();if(styleSucceededFG129.Contains(sequence)){Send("style-repeat-current19",SkillsReplyFG129());Send("style-repeat276",Offline70RoleProtocol.BatchPacketFG(1,276,Offline70FeedbackFG129.StyleReplyBody(assets,skills),keys,false));}else Send("style-repeat276",styleRepliesFG129[sequence]);return;}
  byte[] reply;try{if(dungeon.Active||encrypted.Length!=8)throw new InvalidDataException("Skill style change requires town");byte[] r=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,10,false);Sync();if(r[0]!=assets.ActiveStyleFG129||!assets.StyleOwnedFG129)throw new InvalidDataException("Style ownership or current-page mismatch");bool duplicate;assets=Offline70Economy.Apply(economyPath,session+"-style-"+sequence,"skill-style",r[0].ToString(),delegate(Offline70EconomyState s){if(s.ActiveStyleFG129!=r[0]||!s.StyleOwnedFG129)throw new InvalidDataException("Style changed before commit");s.ActiveStyleFG129=(byte)(1-r[0]);},out duplicate);skills=ActiveSkillsFG129();skillResetEpochFG83++;
   // Notify19 populates both native page objects before276 changes the active pointer.
   Send("style-pages19",SkillsReplyFG129());reply=Offline70RoleProtocol.BatchPacketFG(1,276,Offline70FeedbackFG129.StyleReplyBody(assets,skills),keys,false);styleSucceededFG129.Add(sequence);Note("SKILL_STYLE_COMMITTED bank="+assets.ActiveStyleFG129+" remainingSp="+RemainingSp());
  }catch(InvalidDataException e){reply=Offline70RoleProtocol.BatchPacketFG(1,276,new byte[]{0,0x13},keys,false);Note("SKILL_STYLE_REJECTED "+e.Message);}
  styleRequestsFG129.Add(sequence,fingerprint);styleRepliesFG129.Add(sequence,reply);Send("style-change276",reply);
 }
}
