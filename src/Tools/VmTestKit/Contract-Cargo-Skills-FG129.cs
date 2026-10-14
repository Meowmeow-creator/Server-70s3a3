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
  if(s.CargoSlotsFG129<8||s.CargoSlotsFG129>136||(s.CargoSlotsFG129-8)%16!=0||s.PremiumsFG129==null||s.PremiumsFG129.Count>3||s.ActiveStyleFG129>1||!s.StyleOwnedFG129&&(s.ActiveStyleFG129!=0||s.SecondSkillsFG129!=null)||s.StyleOwnedFG129&&s.SecondSkillsFG129==null)throw new InvalidDataException("Contract/cargo/style store profile");
  HashSet<byte> kinds=new HashSet<byte>();foreach(Offline70PremiumFG129 p in s.PremiumsFG129)if(p==null||p.Kind!=22&&p.Kind!=27&&p.Kind!=29||p.Expires==0||!kinds.Add(p.Kind))throw new InvalidDataException("Premium expiration state");
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
 internal static byte[] SkillsBody(Offline70SkillSlotState first,Offline70EconomyState s){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   foreach(Offline70SkillSlotState bank in new Offline70SkillSlotState[]{first,s.SecondSkillsFG129}){
    if(bank==null){w.Write((ushort)0);w.Write((byte)0);continue;}
    Offline70SkillSlots.Validate(bank);w.Write(Offline70SkillSlots.Remaining(bank,s.Level,s.BonusSp));w.Write((byte)Offline70SkillSlots.VisibleCountFG139(bank));
    for(int i=0;i<bank.Ids.Length;i++){if(bank.Levels[i]==0)continue;w.Write(bank.Slots[i]);w.Write(bank.Ids[i]);w.Write(bank.Levels[i]);w.Write((byte)0);}
   }w.Write((ushort)0);w.Write((ushort)0);return m.ToArray();
  }
 }
 internal static byte[] StyleReplyBody(Offline70EconomyState s,Offline70SkillSlotState bank){using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
  w.Write((byte)1);w.Write(s.ActiveStyleFG129);w.Write((byte)0); // no transient skill-strength modifiers
  w.Write(s.Gold);w.Write(Offline70SkillSlots.Remaining(bank,s.Level,s.BonusSp));w.Write((byte)0);return m.ToArray();}}
}

partial class Offline70SkillSlots {
 internal static Offline70SkillSlotState NewStyleFG129(){Offline70SkillSlotState s=Default();Validate(s);for(int i=0;i<s.Ids.Length;i++)if(!Quick(s.Slots[i])&&!ListSlotFG123(s.Ids[i],s.Slots[i]))s.Slots[i]=255;for(int i=0;i<s.Ids.Length;i++)if(s.Slots[i]==255)s.Slots[i]=FindListFG123(s,s.Ids[i]);s.CommonPlacementFG81=true;Validate(s);return s;}
 internal static void LearnStateFG129(Offline70SkillSlotState s,byte level,byte id,byte next,ushort bonus,byte grow,int extraLevels){
  Offline70SkillRuleFG r=Rule(id);Validate(s);
  if(r==null||r.Cost==0||next!=SkillLevel(s,id)+1||JobMaximum(r,grow)==0||next>JobMaximum(r,grow)||level+extraLevels<r.Required+(next-1)*r.Step||Remaining(s,level,bonus)<r.Cost)throw new InvalidDataException("Skill level, role level or SP requirement rejected");
  for(int p=0;p<r.Prerequisites.Length;p+=2)if(SkillLevel(s,r.Prerequisites[p])<r.Prerequisites[p+1])throw new InvalidDataException("Skill prerequisite rejected");
  int i=Array.IndexOf(s.Ids,id);if(i<0){byte slot=FindListFG123(s,id);i=s.Ids.Length;Array.Resize(ref s.Ids,i+1);Array.Resize(ref s.Slots,i+1);Array.Resize(ref s.Levels,i+1);s.Ids[i]=id;s.Slots[i]=slot;}
  s.Levels[i]=next;s.SpentSp=checked((ushort)(s.SpentSp+r.Cost));Validate(s);
 }
 internal static Offline70SkillSlotState LearnOneFG129(string path,byte level,byte id,ushort bonus,byte grow,int extraLevels){string full=Path.GetFullPath(path);using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){Offline70SkillSlotState s=Load(full);LearnStateFG129(s,level,id,checked((byte)(SkillLevel(s,id)+1)),bonus,grow,extraLevels);CommitResetFG83(full,s);return Load(full);}}
 internal static void SwapStateFG129(Offline70SkillSlotState s,byte source,byte target){if(source==target||!ActiveSlot(source)||!ActiveSlot(target))throw new InvalidDataException("Skill swap range");bool found=false;for(int i=0;i<s.Slots.Length;i++)if(s.Slots[i]==source||s.Slots[i]==target){byte next=s.Slots[i]==source?target:source;if(!MoveSlotFG147(s,i,next))throw new InvalidDataException("Passive shortcut rejected");s.Slots[i]=next;found=true;}if(!found)throw new InvalidDataException("No learned active skill in swap");Validate(s);}
}

partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> styleRequestsFG129=new Dictionary<ushort,string>();readonly Dictionary<ushort,byte[]> styleRepliesFG129=new Dictionary<ushort,byte[]>();
 readonly HashSet<ushort> styleSucceededFG129=new HashSet<ushort>();readonly HashSet<byte> premiumClientActiveFG129=new HashSet<byte>();uint lastPremiumScanFG129;
 Offline70SkillSlotState ActiveSkillsFG129(){return assets.ActiveStyleFG129==0?Offline70SkillSlots.Load(skillPath):assets.SecondSkillsFG129;}
 byte[] SkillsReplyFG129(){return Offline70RoleProtocol.BatchPacketFG(0,19,Offline70FeedbackFG129.SkillsBody(Offline70SkillSlots.Load(skillPath),assets),keys,false);}
 void PremiumNoticesFG129(){uint now=Offline70PotFG93.UnixNow();premiumClientActiveFG129.Clear();foreach(Offline70PremiumFG129 p in assets.PremiumsFG129){Send("contract-state66",Offline70RoleProtocol.BatchPacketFG(0,66,Offline70FeedbackFG129.PremiumBody(p,now),keys,false));if(p.Expires>now)premiumClientActiveFG129.Add(p.Kind);}}
 void MaintainPremiumFG129(){if(!selected||assets==null)return;uint now=Offline70PotFG93.UnixNow();if(lastPremiumScanFG129==now)return;lastPremiumScanFG129=now;foreach(Offline70PremiumFG129 p in assets.PremiumsFG129)if(p.Expires<=now&&premiumClientActiveFG129.Remove(p.Kind)){Send("contract-expired66",Offline70RoleProtocol.BatchPacketFG(0,66,Offline70FeedbackFG129.PremiumBody(p,now),keys,false));Note("CONTRACT_EXPIRED kind="+p.Kind+" entitlement=False walletUnchanged=True");}}
 void CashRefreshFG129(Offline70CashPurchaseFG45.Offer[] offers){bool style=false,cargo=false,premium=false;foreach(Offline70CashPurchaseFG45.Offer o in offers){if(o.Special==3)premium=true;if(o.Special==4)style=true;if(o.Special==5)cargo=true;}
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
