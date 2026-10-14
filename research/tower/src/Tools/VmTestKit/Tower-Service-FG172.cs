using System;using System.IO;using System.Collections.Generic;
// INTERNAL PRECURSOR. Entry/floors/typed death only. Do not ship before actual
// tower items, final awards, lifecycle and native transport are complete.
partial class Offline70BatchServiceFG {
 Offline70DeathTowerFloorsFG171 towerFG172;
 bool towerLoadedFG172,towerFinishingFG174,towerMenuReturnCommittedFG175;
 readonly Dictionary<string,string> towerRequestsFG172=new Dictionary<string,string>();
 bool TowerActiveFG172 {get{return towerFG172!=null&&towerFG172.Active;}}
 bool TowerSeenFG172(ushort op,ushort sequence,byte[] body){string key=op+":"+sequence,value=Convert.ToBase64String(body),old;if(towerRequestsFG172.TryGetValue(key,out old)){if(old!=value)throw new InvalidDataException("Tower sequence reused with different request");return true;}return false;}
 void TowerRememberFG172(ushort op,ushort sequence,byte[] body){if(towerRequestsFG172.Count>=10000)throw new InvalidDataException("Tower request bound");towerRequestsFG172[op+":"+sequence]=Convert.ToBase64String(body);}
 void TowerNotifyFG172(ushort opcode,byte[] body){Send("tower-"+opcode,Offline70RoleProtocol.BatchPacketFG(0,opcode,body,keys,opcode==154||opcode==155));}
 void TowerEntryFG172(byte[] body,ushort sequence){
  if(TowerSeenFG172(16,sequence,body)){Note("TOWER_ENTRY_DUPLICATE noDebit=True");return;}
  if(dungeon.Active||TowerActiveFG172||town!=3||area!=7||body==null||body.Length!=8)throw new InvalidDataException("Tower entry location/state");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,2,false);
  if(BitConverter.ToUInt16(plain,0)!=11000)throw new InvalidDataException("Tower original dungeon");
  for(int i=2;i<8;i++)if(plain[i]!=0)throw new InvalidDataException("Tower difficulty/reserved");
  Sync();byte failure=0;
  if(role.Level<50)failure=14;
  else {uint tickets=0;foreach(Offline70EconomyEntry item in assets.Inventory)if(item.List==0&&item.ItemId==4183)tickets=checked(tickets+item.Quantity);if(tickets<1)failure=17;}
  if(failure!=0){Send("tower-entry-failed",Offline70RoleProtocol.BatchPacketFG(1,16,failure==17?new byte[]{0,17,0}:new byte[]{0,failure},keys,false));Note("TOWER_ENTRY_REJECTED error="+failure+" noDebit=True");return;}
  Offline70DeathTowerFloorsFG171 next=new Offline70DeathTowerFloorsFG171();next.Begin(role.Level);
  Offline70EconomyState before=assets;bool duplicate;
  assets=Offline70Economy.Apply(economyPath,session+"-tower-entry-"+sequence,"tower-entry","11000:4183:1",delegate(Offline70EconomyState state){if(state.Level<50)throw new InvalidDataException("Tower minimum level");Offline70Economy.RemoveAssetItem(state,4183,1);state.TowerTemporaryItemsFG175=true;},out duplicate);
  if(duplicate)throw new InvalidDataException("Tower entry already committed");
  towerFG172=next;towerMenuReturnCommittedFG175=false;towerLoadedFG172=false;towerFinishingFG174=false;towerDisplayDueFG174=towerReturnDueFG174=0;towerRewardShownFG174=towerRewardCreditedFG174=false;entryTown=town;entryArea=area;selectionOpen=false;ResetDeathFG117();ResetRoomLoot();Sync();
  TowerRememberFG172(16,sequence,body);
  Send("tower-ticket-debit",Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys));
  TowerNotifyFG172(154,towerFG172.Info());TowerNotifyFG172(155,towerFG172.Room());
  Note("TOWER_ENTRY_COMMITTED run="+next.RunId+" dungeon=11000 ticket=4183 count=1 floor=1 fatigueUnchanged=True");
 }
 void TowerLoadFG172(byte[] body){
  Offline70RoleProtocol.BatchLoadRequestFG(body,keys);
  if(towerLoadedFG172){Note("TOWER_LOAD_DUPLICATE floor="+towerFG172.Floor);return;}
  towerLoadedFG172=true;
  // Original first-map load gets command40; subsequent floors only notify30.
  if(towerFG172.Floor==1)Send("tower-first-load-ack",Offline70RoleProtocol.DungeonEntryReply(40,Offline70RoleProtocol.BatchBaseRoleFG(role),keys));
  Send("tower-loaded",Offline70RoleProtocol.DungeonEntryReply(30,Offline70RoleProtocol.BatchBaseRoleFG(role),keys));
  Note("TOWER_LOADED floor="+towerFG172.Floor);
 }
 void TowerStageFG172(byte[] body,ushort sequence){
  if(TowerSeenFG172(174,sequence,body)){Note("TOWER_STAGE_DUPLICATE");return;}
  if(towerFinishingFG174||!towerLoadedFG172||body==null||(body.Length!=8&&body.Length!=16))throw new InvalidDataException("Tower stage load/size");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,6,false);int logical=plain[0]==1?1:plain[0]==2?9:0;
  if(logical==0||body.Length!=(logical+7)/8*8)throw new InvalidDataException("Tower stage original command");
  for(int i=logical;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Tower stage padding");
  if(plain[0]==1){towerFG172.Start();TowerRememberFG172(174,sequence,body);Note("TOWER_STAGE_START floor="+towerFG172.Floor);return;}
  towerFG172.FinishDialog();TowerRememberFG172(174,sequence,body);
  if(towerFG172.ReadyToFinish){Sync();Offline70TowerRewardFG174 offer=Offline70TowerRewardsFG174.Plan(towerFG172.RunId,role.Level,DateTime.UtcNow.Ticks);bool duplicate;assets=Offline70Economy.Apply(economyPath,towerFG172.RunId+"-tower-plan-174","tower-plan",offer.Level+":"+offer.Floor+":"+offer.Experience,delegate(Offline70EconomyState state){Offline70TowerRewardsFG174.Record(state,offer);},out duplicate);towerFinishingFG174=true;towerDisplayDueFG174=offer.CreditDueUtcTicks-5*TimeSpan.TicksPerSecond;Note("TOWER_FINAL_PLAN_PERSISTED run="+towerFG172.RunId+" cards=10 creditPhasePending=True");return;}
  Note("TOWER_DIALOG_COMPLETE awaitingOriginal48=True floor="+towerFG172.Floor);
 }
 void TowerProgressFG173(){foreach(Offline70QuestActiveFG a in progress.Active)if(catalog.ActiveVisible(catalog.Get(a.Id),progress))Send("tower-quest-progress",Offline70QuestWireFG.ProgressReply(a.Id,quests.WireRemaining(catalog.Get(a.Id),a,progress),true,keys));}
 void TowerMoveFG173(byte[] body,ushort sequence){if(TowerSeenFG172(48,sequence,body))return;Offline70DungeonFlowFG.MovePlain(body,keys);if(!towerLoadedFG172||!towerFG172.Cleared||towerFG172.Phase!=2||towerFG172.Floor>=45)throw new InvalidDataException("Tower move requires completed non-final stage");towerFG172.Advance();ResetRoomLoot();towerLoadedFG172=false;TowerRememberFG172(48,sequence,body);TowerNotifyFG172(155,towerFG172.Room());Note("TOWER_ORIGINAL_MOVE48 floor="+towerFG172.Floor);}
 void TowerKillFG172(byte[] body){
  if(!towerLoadedFG172||playerDeadFG117)throw new InvalidDataException("Tower death before loaded/live player");
  ushort uid=Offline70RoleProtocol.BatchDeathRequestFG(body,role,keys,delegate(ushort owner){return owner==role.Id;},delegate(ushort victim){try{towerFG172.Actor(victim);return true;}catch(InvalidDataException){return false;}});
  Offline70TowerActorFG171 actor=towerFG172.Actor(uid);
  if(towerFG172.IsDeadFG172(uid)){Note("TOWER_KILL_DUPLICATE uid="+uid);return;}
  if(towerFG172.Phase!=1)throw new InvalidDataException("Tower death before stage start");
  Sync();string receipt=towerFG172.RunId+"-tower-kill-"+uid;Offline70EconomyState before=assets;byte oldLevel=role.Level;uint award=Offline70TowerExperienceFG173.Kill(role.Level,actor.Type);List<Offline70QuestDropFG> direct=new List<Offline70QuestDropFG>();Offline70EconomyState preview=assets;
  foreach(Offline70QuestDropFG drop in Offline70TypedEnemyDropsFG171.Plan(catalog,progress,actor.Code,actor.Type==5?2:1,11000,0,receipt)){Offline70QuestDropFG plan=drop;Offline70EconomyItemDefinition def=Offline70Economy.Definition((uint)drop.ItemId);if(def.Kind!=5)throw new InvalidDataException("Tower typed quest drop requires original task inventory");try{preview=Offline70Economy.Preview(preview,delegate(Offline70EconomyState candidate){Offline70Economy.AddAssetItem(candidate,(uint)plan.ItemId,plan.Quantity);});direct.Add(drop);}catch(InvalidDataException e){Note("TOWER_QUEST_DROP_DEFERRED item="+drop.ItemId+" reason="+e.Message);}}
  List<Offline70GroundFG> towerDrops=TowerDropsFG175(actor,receipt);bool duplicate;assets=Offline70Economy.Apply(economyPath,receipt,"tower-kill",uid+":"+actor.Code+":"+actor.Type+":"+award,delegate(Offline70EconomyState state){state.Experience=Offline70ProgressionFG.AddExperience(state.Experience,award);state.Level=Offline70ProgressionFG.Level(state.Experience);foreach(Offline70QuestDropFG drop in direct)Offline70Economy.AddAssetItem(state,(uint)drop.ItemId,drop.Quantity);},out duplicate);Sync(false);List<Offline70ItemCountFG> counts=new List<Offline70ItemCountFG>();foreach(Offline70EconomyEntry item in assets.Inventory)if(item.List!=3&&item.List!=2)counts.Add(new Offline70ItemCountFG{Id=(int)item.ItemId,Quantity=item.Quantity});
  progress=quests.KillEnemyWithAssetsFG172(questPath,11000,0,actor.Code,actor.Type==5?2:1,receipt,assets.Gold,assets.Experience,counts);
  foreach(Offline70GroundFG drop in towerDrops)ground.Add(drop.Slot,drop);towerFG172.Kill(uid);
  if(towerFG172.Cleared)progress=quests.MapVisit(questPath,towerFG172.Map,towerFG172.RunId+"-map-clear-"+towerFG172.Floor);
  TowerNotifyFG172(38,TowerDeathDropsFG175(uid,towerDrops));
  if(direct.Count>0)Send("tower-task-inventory",Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys));Send("tower-kill-experience",Offline70RoleProtocol.BatchPacketFG(0,37,Experience(),keys,true));LevelChanged(oldLevel);TowerProgressFG173();
  Note("TOWER_KILL_COMMITTED floor="+towerFG172.Floor+" uid="+uid+" enemy="+actor.Code+" enemyType="+(actor.Type==5?2:1)+" award="+award+" taskDrops="+direct.Count+" finalRewardPending=True");
 }
 bool TowerRouteFG172(ushort op,byte[] body,ushort sequence){
  if(op==16&&(TowerActiveFG172||town==3&&area==7)){TowerEntryFG172(body,sequence);return true;}
  if(op==75&&towerFG172!=null){TowerMenuFG175(body,sequence);return true;}
  if(!TowerActiveFG172)return false;
  if(op==19){Offline70EconomyMoveRequest r=Offline70EconomyProtocol.ParseMove(body,keys);if(r.SourceList==0&&r.TargetList==0)return false;Send("tower-move-rejected",Offline70EconomyProtocol.MoveReply(r,false,keys));Note("TOWER_MOVE_REJECTED only bag rearrangement integrated");return true;}
  if(op==47){Offline70EconomyUseRequest r=Offline70EconomyProtocol.ParseUse(body,keys);if(towerLoadedFG172&&!playerDeadFG117&&!towerFinishingFG174&&r.List==0&&r.ItemId>=6520&&r.ItemId<=6524&&r.ItemId!=6522)return false;Send("tower-use-rejected",Offline70EconomyProtocol.UseReply(r,false,keys));Note("TOWER_USE_REJECTED state or unsupported tower item");return true;}
  if(op==40){TowerLoadFG172(body);return true;}
  if(op==174){TowerStageFG172(body,sequence);return true;}
  if(op==48){TowerMoveFG173(body,sequence);return true;}
  if(op==42){TowerKillFG172(body);return true;}
  if(op==46){TowerPickupFG175(body);return true;}
  if(op==43){TowerDieFG174(body,sequence);return true;}
  if(op==44){ReviveFG117(body,sequence);return true;}
  if(op==45){Offline70DungeonFlowFG.ValidateAbortRequest(body);towerFG172.Active=false;towerLoadedFG172=false;Send("tower-abort",Offline70DungeonFlowFG.Reply(1,45,new byte[]{1},keys));ReturnToEntry();Note("TOWER_ABORT noAward=True noTicketRefund=True");return true;}
  throw new InvalidDataException("Unintegrated tower operation "+op);
 }
 void TowerDieFG174(byte[] body,ushort sequence){try{byte[] plain=DeathPlainFG117(body,keys,4,1);if(TowerSeenFG172(43,sequence,body))return;if(playerDeadFG117){Send("tower-already-dead43",Offline70RoleProtocol.BatchPacketFG(1,43,new byte[]{0,18},keys,false));return;}if(!towerLoadedFG172||towerFG172.Phase<1||towerFinishingFG174)throw new InvalidDataException("Tower death before loaded stage or after reward plan");Sync();Offline70TowerRewardFG174 offer=Offline70TowerRewardsFG174.PlanFailure(towerFG172.RunId,role.Level,towerFG172.Floor,DateTime.UtcNow.Ticks);bool duplicate;assets=Offline70Economy.Apply(economyPath,towerFG172.RunId+"-tower-failure-plan-174","tower-plan",offer.Level+":"+offer.Floor+":"+offer.Experience,delegate(Offline70EconomyState state){Offline70TowerRewardsFG174.Record(state,offer);},out duplicate);progress=quests.MapVisit(questPath,towerFG172.Map,towerFG172.RunId+"-map-failure-finish-"+towerFG172.Floor);towerFinishingFG174=true;towerFG172.Phase=2;playerDeadFG117=true;towerDisplayDueFG174=offer.CreditDueUtcTicks-5*TimeSpan.TicksPerSecond;TowerRememberFG172(43,sequence,body);LifeFG117(0,"tower-character-dead32");TowerProgressFG173();Note("TOWER_FAILURE_PLAN_PERSISTED run="+towerFG172.RunId+" current="+towerFG172.Floor+" effective="+offer.Floor+" cards="+offer.Items.Count+" prepareSeconds=5 displaySeconds=1 creditSeconds=5 noRevival=True x="+BitConverter.ToUInt16(plain,0)+" y="+BitConverter.ToUInt16(plain,2));}catch(InvalidDataException e){Send("tower-death-invalid43",Offline70RoleProtocol.BatchPacketFG(1,43,new byte[]{0,19},keys,false));Note("TOWER_DEATH_REJECTED "+e.Message);}}
 void TowerMenuFG175(byte[] body,ushort sequence){byte choice=0;try{
  if(body==null||body.Length!=8)throw new InvalidDataException("Tower menu length");
  byte[] plain=Offline70RoleProtocol.BatchCryptFG(body,keys,5,false);byte confirm=plain[0];choice=plain[1];
  for(int i=2;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Tower menu padding");
  if(confirm>1||choice<1||choice>3)throw new InvalidDataException("Tower menu choice");
  if(TowerSeenFG172(75,sequence,body)){Note("TOWER_MENU_DUPLICATE noMutation=True");return;}
  if(!TowerActiveFG172||!towerFinishingFG174||!towerRewardCreditedFG174||towerMenuReturnCommittedFG175)throw new InvalidDataException("Tower menu state");
  TowerRememberFG172(75,sequence,body);
  if(confirm==1){if(choice==3)towerReturnDueFG174=Int64.MaxValue;else{towerReturnDueFG174=DateTime.UtcNow.Ticks+(choice==2?1:3)*TimeSpan.TicksPerSecond;towerMenuReturnCommittedFG175=true;}}
  Send("tower-menu75",Offline70RoleProtocol.BatchPacketFG(1,75,new byte[]{1,confirm,choice},keys,false));
  Note("TOWER_MENU_ACCEPTED choice="+choice+" confirm="+confirm+" returnCommitted="+towerMenuReturnCommittedFG175+" noRewardOrTicketMutation=True");
 }catch(InvalidDataException e){Send("tower-menu-rejected75",Offline70RoleProtocol.BatchPacketFG(1,75,new byte[]{0,choice,19},keys,false));Note("TOWER_MENU_REJECTED "+e.Message);}}
 long towerDisplayDueFG174,towerReturnDueFG174;bool towerRewardShownFG174,towerRewardCreditedFG174;
 void MaintainTowerRewardFG174(){if(!TowerActiveFG172||!towerFinishingFG174||towerDisplayDueFG174==0)return;long now=DateTime.UtcNow.Ticks;if(!towerRewardShownFG174&&now>=towerDisplayDueFG174){TowerNotifyFG172(157,Offline70TowerRewardsFG174.Display(Offline70TowerRewardsFG174.Find(assets,towerFG172.RunId)));towerRewardShownFG174=true;}
 if(!towerRewardCreditedFG174&&now>=Offline70TowerRewardsFG174.Find(assets,towerFG172.RunId).CreditDueUtcTicks){try{Offline70EconomyState before=assets;byte prior=role.Level;bool duplicate;assets=Offline70TowerRewardsFG174.Claim(economyPath,towerFG172.RunId,now,out duplicate);Sync();byte[] delta=Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys);if(delta!=null)Send("tower-final-inventory",delta);Send("tower-final-experience",Offline70RoleProtocol.BatchPacketFG(0,37,Experience(),keys,true));LevelChanged(prior);uint tickets=0;foreach(Offline70EconomyEntry e in assets.Inventory)if(e.ItemId==4183&&e.List==0)tickets+=e.Quantity;TowerNotifyFG172(158,new byte[]{(byte)(tickets>0?1:0)});towerRewardCreditedFG174=true;towerReturnDueFG174=now+Offline70DungeonFlowFG.AutoReturnSeconds*TimeSpan.TicksPerSecond;Note("TOWER_FINAL_CREDIT_COMMITTED run="+towerFG172.RunId+" duplicate="+duplicate);}catch(InvalidDataException e){Note("TOWER_FINAL_PENDING_CAPACITY run="+towerFG172.RunId+" reason="+e.Message);towerDisplayDueFG174=0;}}
 if(towerRewardCreditedFG174&&now>=towerReturnDueFG174){towerFG172.Active=false;towerLoadedFG172=false;towerDisplayDueFG174=0;if(playerDeadFG117)LifeFG117(1,"tower-return-life32");ReturnToEntry();Note("TOWER_FINAL_TIMEOUT_RETURN rewardAlreadyCommitted=True");}}

}
