using System;using System.IO;using System.Xml;using System.Text;using System.Threading;using System.Collections.Generic;using System.Diagnostics;
// FG201: file-driven GM endpoint for the single-player launcher. Requests are <guid>.gm.xml under
// <store>/TestTools and run on the same thread as gameplay, so every mutation stays inside the
// durable economy/quest transactions. Quest accept/finish synthesise the client opcode-33/36
// request bodies, so the GM path cannot drift from the proven dialog path. With no client
// connected the store is still authoritative and the next character select restores it.
partial class Offline70BatchServiceFG {
 // The client cannot absorb a whole room of death/drop/pickup frames at once (live crash
 // 2026-10-21 12:58:26: five synchronous sweeps inside 16s, then GM_FAULT IOException writing to the
 // socket). Server-initiated kills are therefore paced, one actor per step, like MaintainOneHitKill.
 const int KillSweepStepMs=350,AutoPickupStepMs=400;
 readonly List<ushort> gmKillSweepFG201=new List<ushort>();
 readonly List<KeyValuePair<string,ushort>> gmPickQueueFG201=new List<KeyValuePair<string,ushort>>();
 long gmKillDueFG201,gmPickDueFG201;string gmKillScopeFG201;int gmKillDoneFG201;
 string gmDirectoryFG201,gmTogglePathFG201;long gmPollFG201,gmToggleStampFG201;bool gmStopFG201,autoPickupFG201;
 void InitializeGmFG201(){gmDirectoryFG201=Path.Combine(folder,"TestTools");Directory.CreateDirectory(gmDirectoryFG201);gmTogglePathFG201=Path.Combine(gmDirectoryFG201,"gm-toggles.xml");ReadGmTogglesFG201(true);}
 // The toggle lives in a file next to the request queue so it survives a service restart and can
 // be inspected without a client session; the mtime gate keeps the 100ms tick off the disk.
 void ReadGmTogglesFG201(bool force){
  try{
   long stamp=File.Exists(gmTogglePathFG201)?File.GetLastWriteTimeUtc(gmTogglePathFG201).Ticks:0;
   if(!force&&stamp==gmToggleStampFG201)return;
   gmToggleStampFG201=stamp;
   XmlDocument toggles=stamp==0?null:Offline70TestItemsFG106.ReadSmall(gmTogglePathFG201);
   autoPickupFG201=toggles!=null&&toggles.DocumentElement.GetAttribute("autoPickup")=="1";
   int effect=RosterSlotByteFG201(toggles==null?null:toggles.DocumentElement.GetAttribute("rosterSlotEffect"),-1,Offline70RoleProtocol.SlotLimitFG202,-1);
   int limit=RosterSlotByteFG201(toggles==null?null:toggles.DocumentElement.GetAttribute("rosterSlotLimit"),1,Offline70RoleProtocol.SlotLimitFG202,Offline70RoleProtocol.SlotLimitDefaultFG208);
   if(effect!=Offline70RoleProtocol.SlotEffectOverrideFG208||limit!=Offline70RoleProtocol.SlotLimitOverrideFG208){
    Offline70RoleProtocol.SlotEffectOverrideFG208=effect;Offline70RoleProtocol.SlotLimitOverrideFG208=limit;
    Note("GM_ROSTER_SLOTS limit="+limit+" effect="+(effect<0?"roster-size":effect.ToString())+" applied=next-roster");
   }
  }
  catch(Exception e){autoPickupFG201=false;if(force)Note("GM_TOGGLE_UNREADABLE "+e.GetType().Name+" "+e.Message);}
 }
 // The two roster slot header bytes (charac_slot_limit, slot_effect_count) stay sweepable from this
 // file because the live client's own check is only observable by visiting the selection page: a value
 // outside the accepted range, or an unreadable attribute, falls back to the shipped default rather
 // than to a guess. rosterSlotEffect accepts "auto" (== record count, the only pairing measured to
 // let opcode 5 through), "limit", or a number; rosterSlotLimit accepts 1..the advertised cap.
 static int RosterSlotByteFG201(string raw,int low,int high,int fallback){
  if(raw==null||raw.Length==0)return fallback;
  if(raw=="auto")return low==-1?-1:fallback;
  if(raw=="limit")return high;
  int parsed;return Int32.TryParse(raw,out parsed)&&parsed>=low&&parsed<=high?parsed:fallback;
 }
 void WriteGmToggleFG201(string name,bool value){
  XmlDocument d=new XmlDocument();XmlElement e=d.CreateElement("GMToggles");d.AppendChild(e);
  int effect=Offline70RoleProtocol.SlotEffectOverrideFG208,limit=Offline70RoleProtocol.SlotLimitOverrideFG208;
   e.SetAttribute("version","FG201");e.SetAttribute("autoPickup",(name=="autoPickup"?value:autoPickupFG201)?"1":"0");
   e.SetAttribute("rosterSlotLimit",limit.ToString());e.SetAttribute("rosterSlotEffect",effect<0?"auto":effect.ToString());
   Offline70TestItemsFG106.Write(gmTogglePathFG201,d);
  autoPickupFG201=name=="autoPickup"?value:autoPickupFG201;gmToggleStampFG201=File.GetLastWriteTimeUtc(gmTogglePathFG201).Ticks;
 }
 bool GmLiveFG201 {get{return peer!=null;}}
 void MaintainGmFG201(){
  if(gmDirectoryFG201==null)return;
  long now=DateTime.UtcNow.Ticks;
  if(now-gmPollFG201<TimeSpan.TicksPerMillisecond*100)return;
  gmPollFG201=now;
  ReadGmTogglesFG201(false);
  PumpGmSweepsFG201();
  string[] requests=Directory.GetFiles(gmDirectoryFG201,"*.gm.xml");
  Array.Sort(requests,StringComparer.Ordinal);
  int done=0;
  foreach(string path in requests){
   if(done++>=8)break;
   if(Path.GetFullPath(Path.GetDirectoryName(path))!=Path.GetFullPath(gmDirectoryFG201))continue;
   string receipt=Path.GetFileName(path).Replace(".gm.xml","");
   Guid guid;try{guid=new Guid(receipt);}catch(FormatException){continue;}
   if(guid.ToString("N")!=receipt)continue;
   string op="",message="";bool ok=false;XmlDocument reply=new XmlDocument();
   try{
    XmlElement request=Offline70TestItemsFG106.ReadSmall(path).DocumentElement;
    if(request==null||request.Name!="GMRequest"||request.GetAttribute("version")!="FG201"||request.GetAttribute("token")!=testItemTokenFG106||request.GetAttribute("receipt")!=receipt||string.IsNullOrEmpty(op=request.GetAttribute("op"))||op.Length>32)throw new InvalidDataException("GM 请求的签名、回执号或操作名不正确。");
    message=GmExecuteFG201(receipt,op,request);ok=true;
   }
   catch(InvalidDataException e){message=GmFailureFG201(e.Message);Note("GM_REJECTED receipt="+receipt+" op="+op+" reason="+e.Message);}
   catch(XmlException e){message="GM 请求不是有效 XML："+e.Message;Note("GM_REJECTED receipt="+receipt+" op="+op+" reason="+e.Message);}
   catch(Exception e){message="GM 请求在服务端异常终止："+e.GetType().Name+" "+e.Message;Note("GM_FAULT receipt="+receipt+" op="+op+" error="+e.GetType().Name+" "+e.Message);}
   try{
    XmlElement head=reply.CreateElement("GMReply");reply.AppendChild(head);
    head.SetAttribute("version","FG201");head.SetAttribute("token",testItemTokenFG106);head.SetAttribute("receipt",receipt);head.SetAttribute("op",op);
    head.SetAttribute("success",ok?"1":"0");head.SetAttribute("message",message);head.SetAttribute("pid",Process.GetCurrentProcess().Id.ToString());
    try{head.AppendChild(GmSnapshotFG201(reply));}
    catch(Exception e){head.SetAttribute("snapshotError",e.GetType().Name+": "+e.Message);}
    Offline70TestItemsFG106.Write(Path.Combine(gmDirectoryFG201,receipt+".gm.reply.xml"),reply);
    File.Delete(path);
   }
   catch(IOException e){Note("GM_REPLY_PENDING receipt="+receipt+" error="+e.Message);}
   if(ok){try{PublishTestItemsFG106();}catch(IOException){}Note("GM_COMMIT receipt="+receipt+" op="+op+" message="+message);}
  }
 }
 string GmFailureFG201(string raw){
  if(raw.IndexOf("GM ",StringComparison.Ordinal)==0)return raw.Substring(3);
  if(raw.IndexOf("capacity",StringComparison.OrdinalIgnoreCase)>=0)return "操作失败：对应背包已满，本次没有写入物品。";
  if(raw.IndexOf("town",StringComparison.OrdinalIgnoreCase)>=0)return "操作失败：角色当前不在可行走的城镇区域。";
  if(raw.IndexOf("Test grant unsupported item",StringComparison.Ordinal)>=0)return "操作失败：该物品未接入服务端物品表。";
  if(raw.IndexOf("gear attributes need",StringComparison.Ordinal)>=0)return "操作失败：强化/品质种子/再封印/封印只对装备类物品生效（物品类型 4 或 8），材料消耗品不能带这些属性。";
  if(raw.IndexOf("reseal needs weapon gear",StringComparison.Ordinal)>=0)return "操作失败：再封印只对武器与装备（物品类型 4）有效，装扮类装备不行。";
  if(raw.IndexOf("gear attribute bounds",StringComparison.Ordinal)>=0)return "操作失败：强化只能写 0..31，再封印 0..7，品质种子 0..2147483647。";
  if(raw.IndexOf("Quest not ready",StringComparison.Ordinal)>=0)return "操作失败：任务目标仍未满足，服务端没有完成该任务。";
  return "操作被服务端拒绝："+raw;
 }
 string GmExecuteFG201(string receipt,string op,XmlElement e){
  switch(op){
   case "status":return "已读取当前角色快照。";
   case "role":return GmSelectRoleFG201(receipt,e);
   case "level":return GmLevelFG201(receipt,e);
   case "item":return GmItemFG201(receipt,e);
   case "cash":return GmCashFG201(receipt,e);
   case "gold":return GmGoldFG201(receipt,e);
   case "fatigue":return GmFatigueFG201(receipt,e);
   case "promote":return GmGrowFG201(receipt,e);
   case "change-branch":return GmChangeBranchFG201(receipt,e);
   case "awaken":return GmAwakenFG201(receipt,e);
   case "quest-accept":return GmQuestAcceptFG201(receipt,e);
   case "quest-complete":return GmQuestCompleteFG201(receipt,e);
   case "dungeon-killall":return GmKillAllFG201(receipt,e);
   case "loot-pickall":return GmPickAllFG201(receipt);
   case "loot-autopick":return GmAutoPickToggleFG201(e);
   case "lock-skill":return GmLockSkillFG201(e,true);
   case "unlock-skill":return GmLockSkillFG201(e,false);
   case "premium":return GmPremiumFG201(receipt,e);
   case "dialog-probe":return GmDialogProbeFG366(receipt,e);
   case "shutdown":gmStopFG201=true;return "服务端正在退出，端口即将释放。";
   default:throw new InvalidDataException("GM 未知操作 "+op+"。");
  }
 }
 uint GmNumberFG201(XmlElement e,string name,uint minimum,uint maximum){uint value;if(!UInt32.TryParse(e.GetAttribute(name),out value)||value<minimum||value>maximum)throw new InvalidDataException("GM 参数 "+name+" 必须是 "+minimum+".."+maximum+" 之间的整数。");return value;}
 uint GmOptionalNumberFG201(XmlElement e,string name,uint fallback,uint minimum,uint maximum){string text=e.GetAttribute(name);if(text=="")return fallback;uint value;if(!UInt32.TryParse(text,out value)||value<minimum||value>maximum)throw new InvalidDataException("GM 参数 "+name+" 必须是 "+minimum+".."+maximum+" 之间的整数。");return value;}
 bool GmFlagFG201(XmlElement e,string name,bool fallback){string text=e.GetAttribute(name);if(text=="")return fallback;if(text!="0"&&text!="1")throw new InvalidDataException("GM 参数 "+name+" 必须是 0 或 1。");return text=="1";}
 void GmRequireTownFG201(){if(GmLiveFG201&&!selected)throw new InvalidDataException("客户端已连接但还没有选择角色，请先进入城镇。");if(dungeon.Active)throw new InvalidDataException("角色当前在副本里，请先回到城镇再执行该操作。");}
 // An idle service keeps the last session's town/area; opcode 36 only admits completion inside a
 // walked area, so the saved town from the economy file is the faithful equivalent of a re-select.
 void GmUseSavedTownFG201(){if(GmLiveFG201&&selected)return;town=assets.SavedTown;area=Offline70TownContentFG75.SeriaArea(town);}
 // GM 目标角色切换：FG202 之后每个角色都有自己的 economy/progression/skills 档，而所有 GM 写入都落在
 // "当前绑定的角色"上，所以登录器必须能指名给哪个角色做操作。绑定只有两处发生——建角和客户端 opcode 4
 // 选角（Batch-Service-FG.cs:355），这里照抄 opcode 4 的顺序：清会话缓存 → 挪槽位 → BindRole → 再碰档，
 // 因为 Offline70RoleScopeFG202.Current 是所有档里 RoleId 的缺省值，顺序颠倒会把新档标成旧角色的主人。
 // 客户端在线时不开放：CreateRole 那类路径会把绑定偷偷切走，抢绑定只会让会话状态和客户端看到的角色不一致。
 string GmSelectRoleFG201(string receipt,XmlElement e){
  if(GmLiveFG201)throw new InvalidDataException("客户端已经连上频道服，服务端必须跟着它选的角色走：请先让玩家退出到选角界面或断开客户端，再切 GM 目标角色。");
  ushort id=(ushort)GmNumberFG201(e,"roleId",1,UInt16.MaxValue);
  Offline70Role target=null;foreach(Offline70Role saved in characters.Roles)if(saved.Id==id)target=saved;
  if(target==null){
   StringBuilder ids=new StringBuilder();foreach(Offline70Role saved in characters.Roles){if(ids.Length!=0)ids.Append('、');ids.Append(saved.Id);}
   throw new InvalidDataException("角色 "+id+" 不在这个账号的角色列表里（现有 id："+(ids.Length==0?"无":ids.ToString())+"）。");
  }
  // 目标已经是当前绑定：不做任何写入，回执里的快照就是现状，登录器重试同一张请求也不会推进 revision。
  if(target==role)return "GM 目标角色已经是 "+target.Name+"（roleId "+target.Id+"，"+GmJobNameFG201(target.Job)+" Lv."+target.Level+"）,本次没有切换。";
  Offline70Role previous=role;int previousSlot=selectedSlot;
  ResetSessionCaches();selectedSlot=characters.Roles.IndexOf(target);BindRole(target);ResetRoomLoot();
  // 此刻 assets/progress/skills 还是上一个角色的档，先探一次新档再 Sync：Sync 会改写这三个缓存，
  // 探失败就把绑定原样退回，服务端的会话状态不会出现"半认得"的角色。
  try{Offline70Economy.Load(economyPath);}
  catch(Exception ex){selectedSlot=previousSlot;BindRole(previous);throw new InvalidDataException("角色 "+target.Name+" 的存档读不出来："+ex.Message+"（可能还没建号或档已损坏，服务端没有切换目标角色。）");}
  Sync();
  GmUseSavedTownFG201();
  Note("GM_ROLE_BOUND receipt="+receipt+" from="+previous.Id+" to="+target.Id+" name="+target.Name+" job="+target.Job+" level="+role.Level);
  PublishTestItemsFG106();
  return "GM 目标角色已切到 "+target.Name+"（roleId "+target.Id+"，"+GmJobNameFG201(target.Job)+" Lv."+role.Level+"），后续 level/item/promote/awaken/quest-* 都写在它的存档上；客户端没连上时改动下次进城镇生效。";
 }
 string GmLevelFG201(string receipt,XmlElement e){
  uint target=GmNumberFG201(e,"to",1,70);
  try{Offline70SkillSlots.Remaining(skills,(byte)target,assets.BonusSp);}
  catch(InvalidDataException){throw new InvalidDataException("不能降到 Lv."+target+"：已分配的技能点超过该等级上限，请先洗点或减少技能等级。");}
  uint xp=target<=1?0:Offline70ProgressionFG.Threshold((byte)target);
  Offline70EconomyState beforeAssets=assets;byte previous=role.Level;bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-level",target+":"+xp,delegate(Offline70EconomyState s){s.Experience=xp;s.Level=Offline70ProgressionFG.Level(xp);},out duplicate);
  Sync();
  if(GmLiveFG201&&selected){
   if(beforeAssets.Experience!=assets.Experience)Send("gm-level-experience",Offline70RoleProtocol.BatchPacketFG(0,37,Experience(),keys,true));
   LevelChanged(previous);
  }
  Note("GM_LEVEL_SET receipt="+receipt+" from="+previous+" to="+role.Level+" experience="+assets.Experience+" duplicate="+duplicate);
  return (duplicate?"已核对上次回执，等级没有重复写入。":"等级已写入存档。")+" Lv."+previous+" → Lv."+role.Level+"（累计经验 "+assets.Experience+"）。";
 }
 string GmItemFG201(string receipt,XmlElement e){
  uint item=GmNumberFG201(e,"item",1,UInt32.MaxValue),count=GmNumberFG201(e,"count",1,1000000);bool shortcut=GmFlagFG201(e,"shortcut",false);
  int upgrade=GmOptionalIntFG201(e,"upgrade",0,31),seed=GmOptionalIntFG201(e,"seed",0,Int32.MaxValue),reseal=GmOptionalIntFG201(e,"reseal",0,7),seal=GmOptionalIntFG201(e,"sealed",0,1);
  GmRequireTownFG201();
  Sync();
  ulong before=Offline70TestItemsFG106.Count(assets,item);Dictionary<ushort,uint> collection=selected?CollectionSnapshot():null;bool duplicate;
  assets=Offline70TestItemsFG106.Grant(economyPath,receipt,item,count,shortcut,upgrade,seed,reseal,seal,out duplicate);
  Sync();
  ulong after=Offline70TestItemsFG106.Count(assets,item);
  // 同步给客户端一律走 op13 ITEM_LIST 整包（rednf 里 13=NOTIPACKET_ITEM_LIST、14=UPDATE_ITEM_LIST）。
  // 原来的 GrantedRows 只把"这一件物品"的几行塞进 op14，是全仓库唯一一次用局部行推送整页语义，
  // 客户端因此会把补上来的堆当成新图标，界面上同一种材料占两格，而存档里其实是一格（实测 3037：
  // slot106 100→200→300，再发 900 才按 StackLimit 1000 分成 1000+200 两格）。
  if(selected){Send("gm-item13-bag",Offline70EconomyProtocol.InventoryRestoreReply(assets,keys));Send("gm-item13-avatar",Offline70EconomyProtocol.AvatarBagReply(assets,keys));Send("gm-item13-pet",Offline70EconomyProtocol.PetBagReply(assets,keys));CollectionChanged(collection);}
  Note("GM_ITEM_COMMITTED receipt="+receipt+" item="+item+" count="+count+" shortcut="+shortcut+" upgrade="+upgrade+" seed="+seed+" reseal="+reseal+" sealed="+seal+" before="+before+" after="+after+" duplicate="+duplicate);
  return (duplicate?"已核对上次回执，本次没有重复发放。":selected?"发送完成，已保存并同步到当前角色。":"已写入存档，下次进入城镇即可看到。")+" 物品 "+item+" 持有量 "+before+" → "+after+"。"+GmGearTextFG201(upgrade,seed,reseal,seal);
 }
 // 装备属性只在 kind 4/8 上有意义，FG106 的发放路径已经拒掉了别的组合；这里只负责把"这次到底改了什么"
 // 回读给登录器，免得界面以为强化没生效。
 static string GmGearTextFG201(int upgrade,int seed,int reseal,int seal){
  List<string> parts=new List<string>();
  if(upgrade>=0)parts.Add("强化 +"+upgrade);
  if(seed>=0)parts.Add("品质种子 "+seed);
  if(reseal>=0)parts.Add("再封印 "+reseal+" 次");
  if(seal>=0)parts.Add(seal==1?"已封印":"未封印");
  return parts.Count==0?"":"（本批新条目："+string.Join("、",parts.ToArray())+"）";
 }
 int GmOptionalIntFG201(XmlElement e,string name,int minimum,int maximum){
  string text=e.GetAttribute(name);if(text=="")return -1;
  int value;if(!Int32.TryParse(text,out value)||value<minimum||value>maximum)throw new InvalidDataException("GM 参数 "+name+" 必须是 "+minimum+".."+maximum+" 之间的整数。");
  return value;
 }
 // 点券 = OfflineCash（商城与合约扣的就是这一笔，op53 余额包同步给客户端）；自 FG271 起它是账号钱包，
 // 角色档里的那份只是缓存，加给谁都等于加给全账号。BoundCash 是绑定代币券，
 // 本服务端没有任何发放线路，所以这里只读不写，回执里一并报出来。
 string GmCashFG201(string receipt,XmlElement e){
  uint amount=GmNumberFG201(e,"amount",1,99999999);
  // 点券是纯钱包写入，不碰背包/世界，所以和"设置等级"一样不加城镇门。
  Sync();
  uint before=assets.OfflineCash;bool duplicate;
  try{assets=Offline70Economy.Grant(economyPath,"gm-"+receipt,0,amount,0,0,out duplicate);}
  catch(InvalidDataException ex){if(ex.Message.IndexOf("wallet",StringComparison.OrdinalIgnoreCase)>=0)throw new InvalidDataException("点券加不上去：当前 "+assets.OfflineCash+"，本次 "+amount+"，超过钱包上限 999999999。服务端没有写入。");throw;}
  Sync();
  if(GmLiveFG201&&selected)Send("gm-offline-cash",Offline70EconomyProtocol.CashBalanceReply(assets,keys));
  Note("GM_CASH_COMMITTED receipt="+receipt+" amount="+amount+" before="+before+" after="+assets.OfflineCash+" boundCash="+assets.BoundCash+" duplicate="+duplicate);
  return (duplicate?"已核对上次回执，点券没有重复发放。":"点券已写入账号钱包。")+" 点券 "+before+" → "+assets.OfflineCash+"（同账号所有角色共用这一笔；代币券 "+assets.BoundCash+" 未变）"+(GmLiveFG201&&selected?"，已推送到客户端。":"，下次进入城镇生效。");
 }
 // 技能锁定登记（FG344）：客户端右键锁定是纯内存状态（实测零上报、零本地写入），所以把
 // "锁定了哪些技能"落在服务端的命令样式档里，进世界时随 op19 的每技能命令尾巴回放。
 // 编码 {count=1, key=8}：8 是协议里唯一不对应真实按键的合法键值，能通过参考服务端的全部
 // wire 校验。若实测后要换编码，只改 Offline70SkillCommandFG344.Lock 一处。
 // FG272 探针：直接对当前角色增删 premium type 并当场重发 op66。魔王之契约页签走
 // 客户端自己的"角色增值服务"表（chn_characpremium.etc/premiumserviceeffect.etc），服务端
 // 尚未确定它认可的 type 段，用这个命令逐段试探：premium add kind days / premium del kind。
 string GmPremiumFG201(string receipt,XmlElement e){
  string mode=e.GetAttribute("mode");if(mode=="")mode="add";
  if(mode!="add"&&mode!="del")throw new InvalidDataException("GM 参数 mode 只能是 add 或 del。");
  byte kind=(byte)GmNumberFG201(e,"kind",0,105);
  Sync();
  if(mode=="del"){
   bool duplicate;
   assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-premium-del",kind.ToString(),delegate(Offline70EconomyState s){
    Offline70PremiumFG129 found=null;foreach(Offline70PremiumFG129 pr in s.PremiumsFG129)if(pr.Kind==kind)found=pr;
    if(found==null)throw new InvalidDataException("premium type "+kind+" 本来就不存在。");
    s.PremiumsFG129.Remove(found);},out duplicate);Offline70AccountPremiumsFG381.Remove(folder,kind);/* FG381: a deletion can never propagate through the max-merge, so clear the account ledger too. */
   if(GmLiveFG201&&selected)PremiumNoticesFG129();
   Note("GM_PREMIUM_DEL receipt="+receipt+" kind="+kind+" duplicate="+duplicate);
   return "premium type "+kind+" 已删除"+(GmLiveFG201&&selected?"，op66 已重发。":"。");
  }
  uint days=GmNumberFG201(e,"days",1,3650);
  bool dup;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-premium-add",kind+":"+days,delegate(Offline70EconomyState s){
   Offline70PremiumFG129 found=null;foreach(Offline70PremiumFG129 pr in s.PremiumsFG129)if(pr.Kind==kind)found=pr;
   uint now=Offline70PotFG93.UnixNow();
   if(found==null){found=new Offline70PremiumFG129{Kind=kind};s.PremiumsFG129.Add(found);}
   found.Expires=checked(Math.Max(now,found.Expires)+days*86400u);},out dup);Offline70AccountPremiumsFG381.Merge(folder,assets.PremiumsFG129);/* FG381: push the extension into the account ledger so every other role picks it up at its next Sync. */
  if(GmLiveFG201&&selected)PremiumNoticesFG129();
  Note("GM_PREMIUM_ADD receipt="+receipt+" kind="+kind+" days="+days+" duplicate="+dup);
  return "premium type "+kind+" 已叠加 "+days+" 天"+(GmLiveFG201&&selected?"，op66 已重发，可直接打开期限道具信息栏查看。":"。");
 }
 string GmLockSkillFG201(XmlElement e,bool lockIt){
  GmRequireTownFG201();
  uint id=GmNumberFG201(e,"id",1,255);
  Offline70SkillSlotState bank=Offline70SkillSlots.Load(skillPath);
  if(Array.IndexOf(bank.Ids,(byte)id)<0&&(assets.SecondSkillsFG129==null||Array.IndexOf(assets.SecondSkillsFG129.Ids,(byte)id)<0))throw new InvalidDataException("技能 "+id+" 还没有学习，无法登记锁定。");
  List<Offline70SkillCommandFG344.Entry> all=Offline70SkillCommandFG344.Load(commandPath)??new List<Offline70SkillCommandFG344.Entry>();
  if(lockIt)Offline70SkillCommandFG344.Merge(all,Offline70SkillCommandFG344.Lock((ushort)id));
  else if(!Offline70SkillCommandFG344.Remove(all,(ushort)id))return "技能 "+id+" 本来就不在锁定名单里，没有改动。";
  Offline70SkillCommandFG344.Save(commandPath,all);
  if(GmLiveFG201&&selected)Send("gm-skills19",SkillsReplyFG129());
  Note("GM_SKILL_COMMAND_"+(lockIt?"LOCKED":"UNLOCKED")+" id="+id+" total="+all.Count);
  return (lockIt?"已锁定技能 "+id+"（登录/换角色后仍保持）。":"已解锁技能 "+id+"。")+"当前锁定/自定义条目 "+all.Count+" 条"+(GmLiveFG201&&selected?"，已推送到客户端，重开技能栏即可看到。":"，下次进入城镇生效。");
 }
 // 金币和点券一样是纯钱包写入，走 FG106 的同一笔事务（Grant 的第一个钱包参数），所以不加城镇门。
 // 刷新用 InventoryChangeReply（开罐子/QP 商店/复活/传送/任务结算刷金币都走它，body 里那条 31 字节的金币行是
 // 客户端认的形状）；WalletReply 那条 34 字节行在本构建里没有任何在线调用方，不拿来赌。
 string GmGoldFG201(string receipt,XmlElement e){
  uint amount=GmNumberFG201(e,"amount",1,99999999);
  Sync();
  Offline70EconomyState before=assets;uint beforeGold=assets.Gold;bool duplicate;
  try{assets=Offline70Economy.Grant(economyPath,"gm-"+receipt,amount,0,0,0,out duplicate);}
  catch(InvalidDataException ex){if(ex.Message.IndexOf("wallet",StringComparison.OrdinalIgnoreCase)>=0)throw new InvalidDataException("金币加不上去：当前 "+assets.Gold+"，本次 "+amount+"，超过钱包上限 999999999。服务端没有写入。");throw;}
  Sync();
  if(GmLiveFG201&&selected)Send("gm-gold14",Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys));
  Note("GM_GOLD_COMMITTED receipt="+receipt+" amount="+amount+" before="+beforeGold+" after="+assets.Gold+" duplicate="+duplicate);
  return (duplicate?"已核对上次回执，金币没有重复发放。":"金币已写入存档。")+" 金币 "+beforeGold+" → "+assets.Gold+(GmLiveFG201&&selected?"，已推送到客户端。":"，下次进入城镇生效。");
 }
 // 疲劳一键回满：普通疲劳回 156，黑钻额外疲劳的当日用量一并清零（Remaining 是两者之和，只回
 // 普通那半截会让黑钻玩家看着还是没满）。副本进行中拒绝：宠物疲劳计费把入场/房间两次扣点
 // 夹在中间对账，外部改档会算出负差直接抛异常。
 string GmFatigueFG201(string receipt,XmlElement e){
  GmRequireTownFG201();
  Sync();
  bool black=Offline70BlackFG158.Active(economyPath,DateTime.UtcNow);
  int before=Offline70BlackFG158.Remaining(assets,black);bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-fatigue","full",delegate(Offline70EconomyState s){s.FatigueRemaining=Offline70FatigueFG50.Maximum;s.BlackFatigueUsedFG158=0;},out duplicate);
  Sync();
  int after=Offline70BlackFG158.Remaining(assets,black);
  if(GmLiveFG201&&selected)Send("gm-fatigue-display",Offline70BlackFG158.Display(assets,black,keys));
  Note("GM_FATIGUE_COMMITTED receipt="+receipt+" before="+before+" after="+after+" black="+black+" duplicate="+duplicate);
  return (duplicate?"已核对上次回执，疲劳值没有重复写入。":"疲劳值已恢复满。")+" 剩余疲劳 "+before+" → "+after+"（黑钻加成 "+(black?"生效":"未生效")+"）"+(GmLiveFG201&&selected?"，已推送到客户端。":"，下次进入城镇生效。");
 }
 // 换转职分支：CN70 客户端没有"改分支"这条玩法线路，所以是纯 GM 语义——先把技能档退回本职业的
 // 未转职基线（等同免费洗点、觉醒清零），再写新的 grow 字节。顺序不能反：反了万一中间崩溃，
 // 技能档还带着旧分支的 ProfessionFG139，而 grow 已经是新分支，Sync 里的
 // NormalizeProfessionStateFG139 会直接抛 "Profession baseline identity"，角色就卡在读不出来的状态。
 string GmChangeBranchFG201(string receipt,XmlElement e){
  uint first=GmNumberFG201(e,"grow",1,4);
  GmRequireTownFG201();
  string branch=GmGrowNameFG201(role.Job,(byte)first);
  if(branch.Length==0)throw new InvalidDataException(GmJobNameFG201(role.Job)+" 在这套脚本里只有 grow "+GmLegalGrowTextFG201(role.Job)+"，没有 grow="+first+" 这个转职分支。");
  byte current=Offline70RewardTypesFG183.FirstGrow(assets.GrowType);
  if(current==0)throw new InvalidDataException("该角色还没转职，请直接用“转职”（grow="+first+"）。");
  if(current==(byte)first)throw new InvalidDataException("该角色已经是 "+branch+"（grow="+first+"），不需要换分支。");
  string from=GmGrowNameFG201(role.Job,current);
  GmResetSkillsFG201(receipt);
  bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-change-branch",current+">"+first,delegate(Offline70EconomyState s){
   s.GrowType=Offline70RewardTypesFG183.EncodeGrow((byte)first,0);
   // 第二技能栏（装扮风格解锁的那一栏）也退回基线，否则它的 ProfessionFG139 还标着旧分支；
   // 它必须与 StyleOwnedFG129 同时有/同时无，所以只能重置、不能置空。
   if(s.SecondSkillsFG129!=null)s.SecondSkillsFG129=Offline70SkillSlots.ResetPlanFG83(s.SecondSkillsFG129,"gm-"+receipt+"-second");
  },out duplicate);
  Sync();
  if(GmLiveFG201&&selected){Appearance();Send("gm-job-attributes",AdditionReplyFG129());Send("gm-job-skills",SkillsReplyFG129());Offers();}
  Note("GM_CHANGE_BRANCH receipt="+receipt+" job="+role.Job+" from="+current+" to="+first+" grow="+assets.GrowType+" spentSp="+skills.SpentSp+" visibleSkills="+Offline70SkillSlots.VisibleCountFG139(skills)+" remainingSp="+RemainingSp()+" duplicate="+duplicate);
  return "转职分支已换成 "+GmJobNameFG201(role.Job)+"-"+branch+"（grow 字节 "+assets.GrowType+"，原 "+from+"）：技能点已全部退回、觉醒清零，剩余 SP "+RemainingSp()+"，可见技能 "+Offline70SkillSlots.VisibleCountFG139(skills)+" 个"+(GmLiveFG201&&selected?"，已推送到客户端。":"，下次进入城镇生效。");
 }
 // 与洗点瓶同一套 durable 提交：拿住技能档的 .lock，把整档换成 Default()（本职业 11 个初始技能 +
 // 原快捷栏布局），ProfessionFG139 归零后由 Sync 按新分支重新发赠技。
 void GmResetSkillsFG201(string receipt){
  string full=Path.GetFullPath(skillPath);
  using(FileStream held=new FileStream(full+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
   Offline70SkillSlots.CommitResetFG83(full,Offline70SkillSlots.ResetPlanFG83(Offline70SkillSlots.Load(full),"gm-"+receipt));
 }
 string GmGrowFG201(string receipt,XmlElement e){
  uint first=GmNumberFG201(e,"grow",1,4);
  GmRequireTownFG201();
  // 转职表早就是九职业齐的（Role-Create-Jobs-FG202.cs:61-71 的 GrowSkillsFG202 按 job 行取赠技，
  // Promotion-Skills-FG139.cs:5-10 也按 Offline70RoleScopeFG202.CurrentJob 查行），所以这里不再假定
  // job 0：只看这个职业有没有这条分支，分支名留空即拒绝。
  string branch=GmGrowNameFG201(role.Job,(byte)first);
  if(branch.Length==0)throw new InvalidDataException(GmJobNameFG201(role.Job)+" 在这套脚本里只有 grow "+GmLegalGrowTextFG201(role.Job)+"，没有 grow="+first+" 这个转职分支。");
  if(Offline70RewardTypesFG183.FirstGrow(assets.GrowType)!=0)throw new InvalidDataException("该角色已转职为 "+GmGrowNameFG201(role.Job,Offline70RewardTypesFG183.FirstGrow(assets.GrowType))+"（grow="+assets.GrowType+"）。");
  byte grow=Offline70RewardTypesFG183.EncodeGrow((byte)first,0);bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-grow",grow.ToString(),delegate(Offline70EconomyState s){s.GrowType=grow;},out duplicate);
  Sync();
  if(GmLiveFG201&&selected){Appearance();Send("gm-job-attributes",AdditionReplyFG129());Send("gm-job-skills",SkillsReplyFG129());Offers();}
  Note("GM_PROMOTE receipt="+receipt+" job="+role.Job+" grow="+grow+" firstGrow="+first+" visibleSkills="+Offline70SkillSlots.VisibleCountFG139(skills)+" duplicate="+duplicate);
  return "转职已写入存档："+GmJobNameFG201(role.Job)+"-"+branch+"（grow 字节 "+grow+"），可见技能 "+Offline70SkillSlots.VisibleCountFG139(skills)+" 个"+(GmLiveFG201&&selected?"，已推送到客户端。":"，下次进入城镇生效。");
 }
 string GmAwakenFG201(string receipt,XmlElement e){
  GmRequireTownFG201();
  byte first=Offline70RewardTypesFG183.FirstGrow(assets.GrowType);
  if(first<1||first>4)throw new InvalidDataException("觉醒需要先完成一次转职（当前一次转职="+first+"）。");
  if(Offline70RewardTypesFG183.SecondGrow(assets.GrowType)!=0)throw new InvalidDataException("该角色已完成一次觉醒（grow="+assets.GrowType+"）。");
  string branch=GmGrowNameFG201(role.Job,first),awakening=GmAwakeningNameFG201(role.Job,first);
  byte grow=Offline70RewardTypesFG183.EncodeGrow(first,1);bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-awaken",grow.ToString(),delegate(Offline70EconomyState s){s.GrowType=grow;},out duplicate);
  Sync();
  byte skill=Offline70RewardTypesFG183.AwakeningSkillFor(role.Job,grow);
  if(GmLiveFG201&&selected){Appearance();Send("gm-awaken-attributes",AdditionReplyFG129());Send("gm-awaken-skills",SkillsReplyFG129());Offers();}
  Note("GM_AWAKEN receipt="+receipt+" job="+role.Job+" grow="+grow+" awakeningSkill="+skill+" visibleSkills="+Offline70SkillSlots.VisibleCountFG139(skills)+" duplicate="+duplicate);
  return "一次觉醒已写入存档："+GmJobNameFG201(role.Job)+"-"+branch+" → "+(awakening.Length==0?branch:awakening)+"（grow 字节 "+grow+"），觉醒技能 "+(skill==0?"（这套脚本里 "+GmJobNameFG201(role.Job)+"-"+branch+" 的一次觉醒没有赠技能行）":skill+" Lv."+Offline70RewardTypesFG183.AwakeningSkillLevel)+"，可见技能 "+Offline70SkillSlots.VisibleCountFG139(skills)+" 个。";
 }
 // 九职业的基础职业名（job 枚举顺序 = local-run/recon/role-create-jobs-report.md §2）。7/8 是 at fighter/at mage，
 // 即男格斗家与男魔法师：atfighter.chr 与 fighter.chr 的 [growtype name] 四名全同，靠一次觉醒名区分
 // （狂虎帝/武极/千手罗汉/风林火山 是 7，百花缭乱/武神/毒王/暴风眼 是 1）；atmage.chr 的分支是
 // 元素爆破师/冰结师，而魔法师（3）是元素师/召唤师/战斗法师/魔道学者。转职分支名与
 // 一次觉醒名不在这里，它们和档校验共用 Reward-Types-FG183.cs 的 FG204 表：GM 提示允许写什么，
 // 校验器就必须允许写什么，两处各抄一份迟早会打起来。
 static readonly string[] GmJobBaseNamesFG201=new string[]{"鬼剑士","格斗家","神枪手","魔法师","圣职者","女枪手","暗夜使者","男格斗家","男魔法师"};
 // 空串是"越界"和"该职业没有这个分支"共用的答案：调用方（promote/awaken/快照）据此拒绝写档或留白，
 // 而不是编一个不存在的职业名给登录器。表在 Reward-Types-FG183.cs，与经济档校验同源。
 static string GmGrowNameFG201(byte job,byte first){return Offline70RewardTypesFG183.AdvancementNameFG204(job,first);}
 static string GmAwakeningNameFG201(byte job,byte first){return Offline70RewardTypesFG183.AwakeningNameFG204(job,first);}
 static string GmJobNameFG201(byte job){return job<GmJobBaseNamesFG201.Length?GmJobBaseNamesFG201[job]:"职业 "+job;}
 // 拒绝文案要说清这个职业到底能选哪几个 grow，否则登录器只知道"没有 grow=4"还会再试一次。
 static string GmLegalGrowTextFG201(byte job){if(job>=Offline70RewardTypesFG183.AdvancementNamesFG204.Length)return "无";List<string> parts=new List<string>();for(int first=1;first<Offline70RewardTypesFG183.AdvancementNamesFG204[job].Length;first++)if(Offline70RewardTypesFG183.AdvancementNamesFG204[job][first].Length!=0)parts.Add(first.ToString());return parts.Count==0?"无可用分支":string.Join("、",parts.ToArray());}
 byte[] GmAcceptBodyFG201(ushort questId){byte[] plain=new byte[16];BitConverter.GetBytes((ushort)33).CopyTo(plain,0);BitConverter.GetBytes(questId).CopyTo(plain,2);return Offline70FlowCipherFG.Crypt(plain,keys,33%14,true);}
 byte[] GmFinishBodyFG201(ushort questId,ushort selection){byte[] plain=new byte[16];BitConverter.GetBytes((ushort)36).CopyTo(plain,0);BitConverter.GetBytes(questId).CopyTo(plain,2);BitConverter.GetBytes(selection).CopyTo(plain,4);BitConverter.GetBytes((ushort)1).CopyTo(plain,6);return Offline70FlowCipherFG.Crypt(plain,keys,36%14,true);}
 string GmQuestAcceptFG201(string receipt,XmlElement e){
  ushort id=(ushort)GmNumberFG201(e,"id",1,UInt16.MaxValue);
  if(GmLiveFG201&&!selected)throw new InvalidDataException("客户端已连接但还没有选择角色，请先进入城镇。");
  GmUseSavedTownFG201();
  Offline70QuestDefinitionFG q=catalog.Get(id);
  if(progress.Find(id)!=null)return "任务 "+id+"（"+q.Name+"）已经在该角色的任务日志里。";
  Quest(33,GmAcceptBodyFG201(id));
  Note("GM_QUEST_ACCEPTED receipt="+receipt+" id="+id+" name="+q.Name);
  return "已接受任务 "+id+"（"+q.Name+"）。";
 }
 string GmQuestCompleteFG201(string receipt,XmlElement e){
  ushort id=(ushort)GmNumberFG201(e,"id",1,UInt16.MaxValue);
  bool autoAccept=GmFlagFG201(e,"accept",false),topUp=GmFlagFG201(e,"topup",false);
  uint requested=GmOptionalNumberFG201(e,"selection",0,0,UInt16.MaxValue);
  if(GmLiveFG201&&!selected)throw new InvalidDataException("客户端已连接但还没有选择角色，请先进入城镇。");
  GmUseSavedTownFG201();
  Offline70QuestDefinitionFG q=catalog.Get(id);
  if(!q.Supported)throw new InvalidDataException("任务 "+id+"（"+q.Name+"）在服务端标记为未接入："+q.Reason);
  if(!AssetsKnown(q))throw new InvalidDataException("任务 "+id+" 的奖励物品还没有接入物品表。");
  if(progress.Find(id)==null){
   if(!autoAccept)throw new InvalidDataException("该角色没有接取任务 "+id+"（"+q.Name+"），请先接取或勾选自动接取。");
   GmQuestAcceptFG201(receipt+"-accept",e);
   if(progress.Find(id)==null)throw new InvalidDataException("任务 "+id+" 接取后仍然不在任务日志里。");
  }
  if(!catalog.ActiveVisible(q,progress))throw new InvalidDataException("任务 "+id+"（"+q.Name+"）要求等级 Lv."+q.MinLevel+"~"+q.MaxLevel+"，请先用“设置等级”把角色调到该区间。");
  ushort selection=GmSelectionFG201(q,requested);
  Dictionary<int,uint> missing;
  progress=quests.ForceObjectiveFG201(questPath,id,out missing);
  List<string> topped=new List<string>();
  if(missing.Count>0){
   if(!topUp)throw new InvalidDataException("任务 "+id+" 还缺少提交物："+GmMissingTextFG201(missing)+"（可勾选自动补齐）。");
   uint gold=missing.ContainsKey(0)?missing[0]:0;bool duplicate;
   assets=Offline70Economy.Apply(economyPath,"gm-"+receipt+"-topup","gm-quest-topup",gold+":"+GmPayloadFG201(missing),delegate(Offline70EconomyState s){
    if(gold!=0){if(gold>Offline70Economy.WalletLimit-s.Gold)throw new InvalidDataException("GM 补齐金币超出钱包上限");s.Gold=checked(s.Gold+gold);}
    foreach(KeyValuePair<int,uint> pair in missing)if(pair.Key!=0)Offline70Economy.AddAssetItem(s,(uint)pair.Key,pair.Value);
   },out duplicate);
   if(gold!=0)topped.Add("金币×"+gold);
   foreach(KeyValuePair<int,uint> pair in missing)if(pair.Key!=0)topped.Add(pair.Key+"×"+pair.Value);
   Sync();
   Note("GM_QUEST_TOPPED_UP receipt="+receipt+" quest="+id+" payload="+GmPayloadFG201(missing));
  }
  Quest(36,GmFinishBodyFG201(id,selection));
  Note("GM_QUEST_COMPLETED receipt="+receipt+" id="+id+" selection="+selection+" objectives=forced topped="+(topped.Count==0?"none":string.Join(",",topped.ToArray()))+" grow="+assets.GrowType+" level="+role.Level);
  return "任务 "+id+"（"+q.Name+"）已完成，奖励按任务表结算"+(q.RewardGrowType>0?"：一次转职 → "+GmJobNameFG201(role.Job)+"-"+GmGrowNameFG201(role.Job,Offline70RewardTypesFG183.FirstGrow(assets.GrowType)):"")+(q.RewardSecondGrow>0?"：一次觉醒（grow "+assets.GrowType+"）":"")+(topped.Count>0?"；已补齐提交物 "+string.Join("、",topped.ToArray()):"")+"。";
 }
 static ushort GmSelectionFG201(Offline70QuestDefinitionFG q,uint requested){
  if(q.Selection.Length==0)return ushort.MaxValue;
  int choices=q.Selection.Length/2;
  if(requested<(uint)choices&&q.Selection[2*(int)requested]!=0)return (ushort)requested;
  for(int i=0;i<choices;i++)if(q.Selection[2*i]!=0)return (ushort)i;
  throw new InvalidDataException("任务 "+q.Id+" 的可选奖励只有金币，服务端不支持金币选项。");
 }
 static string GmMissingTextFG201(Dictionary<int,uint> missing){List<string> parts=new List<string>();foreach(KeyValuePair<int,uint> pair in missing)parts.Add((pair.Key==0?"金币":"物品 "+pair.Key)+"×"+pair.Value);return string.Join("、",parts.ToArray());}
 static string GmPayloadFG201(Dictionary<int,uint> missing){StringBuilder b=new StringBuilder();foreach(KeyValuePair<int,uint> pair in missing)b.Append("-").Append(pair.Key).Append(":").Append(pair.Value);return b.ToString();}
 string GmKillAllFG201(string receipt,XmlElement e){
  uint limit=GmOptionalNumberFG201(e,"limit",20,1,40);
  if(!GmLiveFG201)throw new InvalidDataException("一键清图需要客户端已经连接并进入游戏。");
  if(!dungeon.Active||!dungeon.Loaded)throw new InvalidDataException("当前房间还没有进入或尚未加载完成。");
  if(gmKillSweepFG201.Count>0)throw new InvalidDataException("上一次一键清图还在结算中（剩 "+gmKillSweepFG201.Count+" 只），等它结束再按。");
  int planned=dungeon.TotalCount;
  for(int i=0;i<planned&&gmKillSweepFG201.Count<(int)limit;i++){
   ushort uid=(ushort)(100+i);
   try{if(!dungeon.IsDead(uid))gmKillSweepFG201.Add(uid);}
   catch(InvalidDataException){break;}
  }
  if(gmKillSweepFG201.Count==0)return "本房间计划怪物 "+planned+" 只，都已经登记死亡了。";
  gmKillScopeFG201=dungeon.RoomReceiptScope;gmKillDueFG201=DateTime.UtcNow.Ticks;gmKillDoneFG201=0;
  Note("GM_KILLALL_QUEUED receipt="+receipt+" dungeon="+dungeon.DungeonId+" difficulty="+dungeon.Difficulty+" room="+dungeon.X+","+dungeon.Y+" planned="+planned+" queued="+gmKillSweepFG201.Count+" stepMs="+KillSweepStepMs);
  return "已排队 "+gmKillSweepFG201.Count+" 只（本房间共 "+planned+" 只），每 "+KillSweepStepMs+"ms 结算一只；经验、掉落、任务计数都按正常击杀登记。";
 }
 void PumpGmSweepsFG201(){
  long now=DateTime.UtcNow.Ticks;
  if(gmKillSweepFG201.Count>0){
   if(!GmLiveFG201||!dungeon.Active||!dungeon.Loaded||gmKillScopeFG201!=dungeon.RoomReceiptScope||dungeon.ReadyToClear){
    Note("GM_KILLALL_ABORTED remaining="+gmKillSweepFG201.Count+" killed="+gmKillDoneFG201+" live="+GmLiveFG201+" scope="+dungeon.RoomReceiptScope+" readyToClear="+dungeon.ReadyToClear);
    gmKillSweepFG201.Clear();
   }
   else if(now>=gmKillDueFG201){
    gmKillDueFG201=now+(long)KillSweepStepMs*TimeSpan.TicksPerMillisecond;
    ushort uid=gmKillSweepFG201[0];gmKillSweepFG201.RemoveAt(0);
    try{KillMonster(uid);gmKillDoneFG201++;}
    catch(InvalidDataException ex){Note("GM_KILLALL_SKIPPED uid="+uid+" reason="+ex.Message);if(ex.Message.IndexOf("actor state",StringComparison.OrdinalIgnoreCase)>=0)gmKillSweepFG201.Clear();}
    catch(OverflowException ex){Note("GM_KILLALL_SKIPPED uid="+uid+" reason="+ex.Message);}
    if(gmKillSweepFG201.Count==0)Note("GM_KILLALL_DONE killed="+gmKillDoneFG201+" room="+dungeon.X+","+dungeon.Y);
   }
  }
  if(gmPickQueueFG201.Count>0&&now>=gmPickDueFG201){
   gmPickDueFG201=now+(long)AutoPickupStepMs*TimeSpan.TicksPerMillisecond;
   KeyValuePair<string,ushort> entry=gmPickQueueFG201[0];gmPickQueueFG201.RemoveAt(0);
   if(entry.Key!=dungeon.RoomReceiptScope)Note("AUTO_PICKUP_STALE ground="+entry.Value+" room changed");
   else try{if(PickupGroundFG201(entry.Value))Note("AUTO_PICKUP ground="+entry.Value);}
   catch(InvalidDataException ex){Note("AUTO_PICKUP_DEFERRED ground="+entry.Value+" reason="+ex.Message);}
  }
 }
 string GmPickAllFG201(string receipt){
  if(!GmLiveFG201)throw new InvalidDataException("自动拾取需要客户端已经进入副本。");
  if(!dungeon.Active||!dungeon.Loaded)throw new InvalidDataException("当前房间还没有进入或尚未加载完成。");
  if(ground.Count==0)return "当前房间地上没有可拾取物。";
  List<ushort> slots=new List<ushort>(ground.Keys);slots.Sort();
  int taken=0;List<string> rejected=new List<string>();
  foreach(ushort slot in slots){
   try{if(PickupGroundFG201(slot))taken++;}
   catch(InvalidDataException ex){if(rejected.Count<3)rejected.Add("slot"+slot+":"+ex.Message);}
  }
  Note("GM_PICKALL receipt="+receipt+" taken="+taken+" remaining="+ground.Count);
  return "已拾取 "+taken+" 个地上条目，剩余 "+ground.Count+" 个。"+(rejected.Count>0?" 拒绝："+string.Join("；",rejected.ToArray())+"。":"");
 }
 string GmAutoPickToggleFG201(XmlElement e){
  bool enable=GmFlagFG201(e,"enable",autoPickupFG201);
  WriteGmToggleFG201("autoPickup",enable);
  Note("GM_AUTOPICKUP "+(enable?"ENABLED":"DISABLED")+" toggle="+gmTogglePathFG201);
  return enable?"自动拾取已常开：之后每只怪掉落都会立即入库，背包满时该条留在地上并记 AUTO_PICKUP_DEFERRED。":"自动拾取已关闭：掉落留在地上，需要手动或点“拾取当前地上”。";
 }
 XmlElement GmSnapshotFG201(XmlDocument d){
  XmlElement s=d.CreateElement("Snapshot");
  s.SetAttribute("account",assets.AccountId.ToString());
  s.SetAttribute("roleId",role.Id.ToString());
  s.SetAttribute("roleName",role.Name);
  s.SetAttribute("job",role.Job.ToString());
  s.SetAttribute("level",role.Level.ToString());
  s.SetAttribute("experience",assets.Experience.ToString());
  s.SetAttribute("nextLevelExperience",role.Level<70?Offline70ProgressionFG.Threshold((byte)(role.Level+1)).ToString():"0");
  s.SetAttribute("gold",assets.Gold.ToString());
  s.SetAttribute("offlineCash",assets.OfflineCash.ToString());
  s.SetAttribute("boundCash",assets.BoundCash.ToString());
  s.SetAttribute("questPoints",assets.QuestPoints.ToString());
  s.SetAttribute("sp",RemainingSp().ToString());
  s.SetAttribute("grow",assets.GrowType.ToString());
  byte firstGrowFG201=Offline70RewardTypesFG183.FirstGrow(assets.GrowType),secondGrowFG201=Offline70RewardTypesFG183.SecondGrow(assets.GrowType);
  s.SetAttribute("firstGrow",firstGrowFG201.ToString());
  s.SetAttribute("firstGrowName",GmGrowNameFG201(role.Job,firstGrowFG201));
  s.SetAttribute("jobName",GmJobNameFG201(role.Job));
  s.SetAttribute("secondGrow",secondGrowFG201.ToString());
  s.SetAttribute("expertJob",assets.ExpertJobTypeFG183.ToString());
  s.SetAttribute("awakeningSkill",Offline70RewardTypesFG183.AwakeningSkillFor(role.Job,assets.GrowType).ToString());
  // 觉醒名只在真的觉醒过时给出：未觉醒的角色没有该分支的觉醒身份，给空串会让登录器显示一个空白职业。
  if(secondGrowFG201!=0){string awakeningNameFG201=GmAwakeningNameFG201(role.Job,firstGrowFG201);if(awakeningNameFG201.Length!=0)s.SetAttribute("awakeningName",awakeningNameFG201);}
  s.SetAttribute("fatigueRemaining",assets.FatigueRemaining.ToString());
  s.SetAttribute("savedTown",assets.SavedTown.ToString());
  s.SetAttribute("revision",assets.Revision.ToString());
  s.SetAttribute("live",GmLiveFG201?"1":"0");
  s.SetAttribute("selected",selected?"1":"0");
  s.SetAttribute("ready",GmLiveFG201&&selected&&!dungeon.Active?"1":"0");
  s.SetAttribute("town",town.ToString());
  s.SetAttribute("area",area.ToString());
  s.SetAttribute("dungeon",dungeon.Active?"1":"0");
  if(dungeon.Active){s.SetAttribute("dungeonId",dungeon.DungeonId.ToString());s.SetAttribute("difficulty",dungeon.Difficulty.ToString());s.SetAttribute("roomX",dungeon.X.ToString());s.SetAttribute("roomY",dungeon.Y.ToString());s.SetAttribute("roomLoaded",dungeon.Loaded?"1":"0");s.SetAttribute("roomMonsters",dungeon.TotalCount.ToString());s.SetAttribute("readyToClear",dungeon.ReadyToClear?"1":"0");}
  int rows=0;ulong items=0;foreach(Offline70EconomyEntry entry in assets.Inventory){rows++;if(entry.List!=3&&entry.List!=2)items+=entry.Quantity;}
  s.SetAttribute("inventoryRows",rows.ToString());
  s.SetAttribute("inventoryItems",items.ToString());
  s.SetAttribute("autoPickup",autoPickupFG201?"1":"0");
  s.SetAttribute("killSweepPending",gmKillSweepFG201.Count.ToString());
  s.SetAttribute("pickPending",gmPickQueueFG201.Count.ToString());
  s.SetAttribute("completedCount",progress.Completed.Count.ToString());
  s.SetAttribute("activeCount",progress.Active.Count.ToString());
  XmlElement roles=d.CreateElement("Roles");s.AppendChild(roles);
  try{foreach(Offline70Role saved in ProbeRoleCreateCandidate.Load(Path.Combine(folder,"roles-CX.xml")).Roles){XmlElement row=d.CreateElement("Role");row.SetAttribute("id",saved.Id.ToString());row.SetAttribute("name",saved.Name);row.SetAttribute("job",saved.Job.ToString());row.SetAttribute("storedLevel",saved.Level.ToString());row.SetAttribute("created",saved.CreatedUtc==null?"":saved.CreatedUtc);roles.AppendChild(row);}}
  catch(Exception e){roles.SetAttribute("error",e.GetType().Name+": "+e.Message);}
  XmlElement active=d.CreateElement("ActiveQuests");s.AppendChild(active);
  foreach(Offline70QuestActiveFG row in progress.Active){
   Offline70QuestDefinitionFG q=catalog.Get(row.Id);
   XmlElement quest=d.CreateElement("Quest");
   quest.SetAttribute("id",row.Id.ToString());
   quest.SetAttribute("name",q.Name==null?"":q.Name);
   quest.SetAttribute("grade",q.Grade==null?"":q.Grade);
   quest.SetAttribute("type",q.Type==null?"":q.Type);
   quest.SetAttribute("minLevel",q.MinLevel.ToString());
   quest.SetAttribute("maxLevel",q.MaxLevel.ToString());
   quest.SetAttribute("supported",q.Supported?"1":"0");
   quest.SetAttribute("reason",q.Reason==null?"":q.Reason);
   quest.SetAttribute("rewardGrow",q.RewardGrowType.ToString());
   quest.SetAttribute("rewardSecondGrow",q.RewardSecondGrow.ToString());
   quest.SetAttribute("rewardExpertJob",q.RewardExpertJob.ToString());
   try{quest.SetAttribute("remaining",quests.Remaining(q,row,progress).ToString());quest.SetAttribute("wired",quests.WireRemaining(q,row,progress).ToString());}
   catch(InvalidDataException ex){quest.SetAttribute("remaining","-1");quest.SetAttribute("reject",ex.Message);}
   active.AppendChild(quest);
  }
  XmlElement completed=d.CreateElement("CompletedQuests");
  StringBuilder ids=new StringBuilder();foreach(ushort id in progress.Completed){if(ids.Length!=0)ids.Append(',');if(ids.Length<60000)ids.Append(id);}
  completed.SetAttribute("ids",ids.ToString());
  s.AppendChild(completed);
  XmlElement inventory=d.CreateElement("Inventory");s.AppendChild(inventory);
  foreach(Offline70EconomyEntry entry in assets.Inventory){
   if(entry.List==3||entry.List==2)continue;
   XmlElement row=d.CreateElement("Entry");
   row.SetAttribute("list",entry.List.ToString());
   row.SetAttribute("slot",entry.Slot.ToString());
   row.SetAttribute("item",entry.ItemId.ToString());
   row.SetAttribute("quantity",entry.Quantity.ToString());
   // 强化/品质只对装备类有意义：非装备行不带这两个属性，登录器界面留空而不是显示 0 误导人。
   Offline70EconomyItemDefinition gearDefinition=Offline70Economy.Definition(entry.ItemId);
   if(gearDefinition.Kind==4||gearDefinition.Kind==8)row.SetAttribute("upgrade",entry.Upgrade.ToString());
   if(entry.EquipmentSeed>=0)row.SetAttribute("seed",entry.EquipmentSeed.ToString());
   if(entry.ExpiresUnix!=0)row.SetAttribute("expires",entry.ExpiresUnix.ToString());
   inventory.AppendChild(row);
  }
  XmlElement offers=d.CreateElement("Offers");
  StringBuilder offerIds=new StringBuilder();foreach(ushort offer in catalog.Offers(progress)){if(offerIds.Length>=60000)break;if(!AssetsKnown(catalog.Get(offer)))continue;if(offerIds.Length!=0)offerIds.Append(',');offerIds.Append(offer);}
  offers.SetAttribute("ids",offerIds.ToString());
  s.AppendChild(offers);
  if(ground.Count>0){
   XmlElement floor=d.CreateElement("Ground");s.AppendChild(floor);
   foreach(KeyValuePair<ushort,Offline70GroundFG> pair in ground){XmlElement row=d.CreateElement("Entry");row.SetAttribute("slot",pair.Key.ToString());row.SetAttribute("item",pair.Value.ItemId.ToString());row.SetAttribute("quantity",pair.Value.Quantity.ToString());floor.AppendChild(row);}
  }
  return s;
 }
}
partial class Offline70QuestEngineFG {
 // GM objective fill: raises this quest's own counters to the catalogue goals and reports what a
 // collection quest still needs, so the caller can top the inventory up first. The completion
 // itself still runs through opcode 36, so rewards stay computed, never asserted.
 internal Offline70ProgressionStateFG ForceObjectiveFG201(string path,ushort id,out Dictionary<int,uint> missing){
  Offline70QuestDefinitionFG q=catalog.Get(id);
  Dictionary<int,uint> report=new Dictionary<int,uint>();
  Offline70ProgressionStateFG result=Change(path,delegate(Offline70ProgressionStateFG s){
   Offline70QuestActiveFG a=s.Find(id);
   if(a==null)throw new InvalidDataException("GM 任务 "+id+" 还没有被该角色接取。");
   if(!catalog.ActiveVisible(q,s))throw new InvalidDataException("GM 任务 "+id+" 不在当前等级段（要求 Lv."+q.MinLevel+"~"+q.MaxLevel+"）。");
   int stride=HuntStride(q.Type);
   if(stride!=0){
    if(q.Condition.Length!=stride*a.Progress.Length)throw new InvalidDataException("GM 狩猎任务 "+id+" 目标宽度与条件表不匹配");
    for(int i=0;i<a.Progress.Length;i++){int goal=HuntGoal(q,i);if(goal<0)throw new InvalidDataException("GM 狩猎任务 "+id+" 目标数量为负");if(goal>a.Progress[i])a.Progress[i]=goal;}
    return;
   }
   if(q.Type=="[seeking]"){
    foreach(KeyValuePair<int,uint> requirement in Requirements(q)){
     int item=requirement.Key;uint have=item==0?s.Gold:s.Count(item);
     if(have<requirement.Value)report[item]=checked(requirement.Value-have);
    }
    return;
   }
   if(q.Id==4312)Receipt(s,"survivor-4312-"+(a.Instance??"legacy"));
   if(a.Progress.Length>0)a.Progress[0]=1;
  });
  missing=report;
  return result;
 }
}
