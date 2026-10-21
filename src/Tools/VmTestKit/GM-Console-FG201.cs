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
   autoPickupFG201=stamp!=0&&Offline70TestItemsFG106.ReadSmall(gmTogglePathFG201).DocumentElement.GetAttribute("autoPickup")=="1";
  }
  catch(Exception e){autoPickupFG201=false;if(force)Note("GM_TOGGLE_UNREADABLE "+e.GetType().Name+" "+e.Message);}
 }
 void WriteGmToggleFG201(string name,bool value){
  XmlDocument d=new XmlDocument();XmlElement e=d.CreateElement("GMToggles");d.AppendChild(e);
  e.SetAttribute("version","FG201");e.SetAttribute("autoPickup",(name=="autoPickup"?value:autoPickupFG201)?"1":"0");
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
  if(raw.IndexOf("Quest not ready",StringComparison.Ordinal)>=0)return "操作失败：任务目标仍未满足，服务端没有完成该任务。";
  return "操作被服务端拒绝："+raw;
 }
 string GmExecuteFG201(string receipt,string op,XmlElement e){
  switch(op){
   case "status":return "已读取当前角色快照。";
   case "level":return GmLevelFG201(receipt,e);
   case "item":return GmItemFG201(receipt,e);
   case "promote":return GmGrowFG201(receipt,e);
   case "awaken":return GmAwakenFG201(receipt,e);
   case "quest-accept":return GmQuestAcceptFG201(receipt,e);
   case "quest-complete":return GmQuestCompleteFG201(receipt,e);
   case "dungeon-killall":return GmKillAllFG201(receipt,e);
   case "loot-pickall":return GmPickAllFG201(receipt);
   case "loot-autopick":return GmAutoPickToggleFG201(e);
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
  GmRequireTownFG201();
  Sync();
  ulong before=Offline70TestItemsFG106.Count(assets,item);Dictionary<ushort,uint> collection=selected?CollectionSnapshot():null;bool duplicate;
  assets=Offline70TestItemsFG106.Grant(economyPath,receipt,item,count,shortcut,out duplicate);
  Sync();
  ulong after=Offline70TestItemsFG106.Count(assets,item);
  if(selected){foreach(byte list in new byte[]{0,1,7})Send("gm-item14",Offline70TestItemsFG106.GrantedRows(assets,item,list,keys));CollectionChanged(collection);}
  Note("GM_ITEM_COMMITTED receipt="+receipt+" item="+item+" count="+count+" shortcut="+shortcut+" before="+before+" after="+after+" duplicate="+duplicate);
  return (duplicate?"已核对上次回执，本次没有重复发放。":selected?"发送完成，已保存并同步到当前角色。":"已写入存档，下次进入城镇即可看到。")+" 物品 "+item+" 持有量 "+before+" → "+after+"。";
 }
 string GmGrowFG201(string receipt,XmlElement e){
  uint first=GmNumberFG201(e,"grow",1,4);
  GmRequireTownFG201();
  if(role.Job!=0)throw new InvalidDataException("本服务端只接入鬼剑士（job 0）的转职表，当前角色 job="+role.Job+"。");
  if(Offline70RewardTypesFG183.FirstGrow(assets.GrowType)!=0)throw new InvalidDataException("该角色已转职为 "+GmGrowNameFG201(Offline70RewardTypesFG183.FirstGrow(assets.GrowType))+"（grow="+assets.GrowType+"）。");
  byte grow=Offline70RewardTypesFG183.EncodeGrow((byte)first,0);bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-grow",grow.ToString(),delegate(Offline70EconomyState s){s.GrowType=grow;},out duplicate);
  Sync();
  if(GmLiveFG201&&selected){Appearance();Send("gm-job-attributes",AdditionReplyFG129());Send("gm-job-skills",SkillsReplyFG129());Offers();}
  Note("GM_PROMOTE receipt="+receipt+" grow="+grow+" firstGrow="+first+" visibleSkills="+Offline70SkillSlots.VisibleCountFG139(skills)+" duplicate="+duplicate);
  return "转职已写入存档："+GmGrowNameFG201((byte)first)+"（grow 字节 "+grow+"），可见技能 "+Offline70SkillSlots.VisibleCountFG139(skills)+" 个"+(GmLiveFG201&&selected?"，已推送到客户端。":"，下次进入城镇生效。");
 }
 string GmAwakenFG201(string receipt,XmlElement e){
  GmRequireTownFG201();
  byte first=Offline70RewardTypesFG183.FirstGrow(assets.GrowType);
  if(first<1||first>4)throw new InvalidDataException("觉醒需要先完成一次转职（当前一次转职="+first+"）。");
  if(Offline70RewardTypesFG183.SecondGrow(assets.GrowType)!=0)throw new InvalidDataException("该角色已完成一次觉醒（grow="+assets.GrowType+"）。");
  byte grow=Offline70RewardTypesFG183.EncodeGrow(first,1);bool duplicate;
  assets=Offline70Economy.Apply(economyPath,"gm-"+receipt,"gm-awaken",grow.ToString(),delegate(Offline70EconomyState s){s.GrowType=grow;},out duplicate);
  Sync();
  byte skill=Offline70RewardTypesFG183.AwakeningSkillFor(role.Job,grow);
  if(GmLiveFG201&&selected){Appearance();Send("gm-awaken-attributes",AdditionReplyFG129());Send("gm-awaken-skills",SkillsReplyFG129());Offers();}
  Note("GM_AWAKEN receipt="+receipt+" grow="+grow+" awakeningSkill="+skill+" visibleSkills="+Offline70SkillSlots.VisibleCountFG139(skills)+" duplicate="+duplicate);
  return "一次觉醒已写入存档："+GmGrowNameFG201(first)+" → grow 字节 "+grow+"，觉醒技能 "+(skill==0?"（本版本鬼剑士脚本里该转职没有觉醒技能行）":skill+" Lv."+Offline70RewardTypesFG183.AwakeningSkillLevel)+"，可见技能 "+Offline70SkillSlots.VisibleCountFG139(skills)+" 个。";
 }
 static string GmGrowNameFG201(byte first){return first==1?"剑魂":first==2?"鬼泣":first==3?"狂战士":first==4?"阿修罗":"未转职";}
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
  return "任务 "+id+"（"+q.Name+"）已完成，奖励按任务表结算"+(q.RewardGrowType>0?"：一次转职 → "+GmGrowNameFG201(Offline70RewardTypesFG183.FirstGrow(assets.GrowType)):"")+(q.RewardSecondGrow>0?"：一次觉醒（grow "+assets.GrowType+"）":"")+(topped.Count>0?"；已补齐提交物 "+string.Join("、",topped.ToArray()):"")+"。";
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
  s.SetAttribute("questPoints",assets.QuestPoints.ToString());
  s.SetAttribute("sp",RemainingSp().ToString());
  s.SetAttribute("grow",assets.GrowType.ToString());
  s.SetAttribute("firstGrow",Offline70RewardTypesFG183.FirstGrow(assets.GrowType).ToString());
  s.SetAttribute("firstGrowName",GmGrowNameFG201(Offline70RewardTypesFG183.FirstGrow(assets.GrowType)));
  s.SetAttribute("secondGrow",Offline70RewardTypesFG183.SecondGrow(assets.GrowType).ToString());
  s.SetAttribute("expertJob",assets.ExpertJobTypeFG183.ToString());
  s.SetAttribute("awakeningSkill",Offline70RewardTypesFG183.AwakeningSkillFor(role.Job,assets.GrowType).ToString());
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
