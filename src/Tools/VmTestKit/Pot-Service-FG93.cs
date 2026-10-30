using System;using System.IO;using System.Collections.Generic;
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> potRequestsFG93=new Dictionary<ushort,string>();
 // op29 is the container-open request (rednf DisPatcher_Lottery -> CUser::lottery_item): CN
 // clients send it for pocket jars and for every [legacy] / [stackable legacy] /
 // [upgradable legacy] item. The 11 hand-catalogued shop jars keep their dedicated path (it owns
 // the gold cost and the sealed/period decoration); every other container opens through the
 // PVF-driven FG373 table.
 void PotFG93(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;if(potRequestsFG93.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Pot sequence reused");Note("POT_DUPLICATE_IGNORED seq="+sequence);return;}
  byte[] body;Dictionary<ushort,uint> collection=null;Offline70EconomyState before=null;Offline70EconomyEntry reward=null;bool containerFG373=false;
  try{ushort slot=Offline70PotFG93.Parse(encrypted,keys);Sync();collection=CollectionSnapshot();before=assets;bool duplicate;
   Offline70EconomyEntry owned=Offline70Economy.Find(assets,0,slot);
   bool pot=owned!=null&&Offline70PotCatalogFG93.Pools.ContainsKey(owned.ItemId);
   if(!pot&&owned!=null&&Offline70ContainerLotteryFG373.Registered(owned.ItemId)){
    /* The native handler is a plain inventory use, so a container opens in a dungeon too. Every
       draw and the equipment seed/upgrade come from the receipt, which keeps the committed
       payload reproducible if the sequence ever replays. */
    containerFG373=true;string receipt=session+"-container-"+sequence;
    Offline70ContainerFG373 table=Offline70ContainerLotteryFG373.Get(owned.ItemId);
    uint count;uint granted=Offline70ContainerLotteryFG373.Roll(receipt,owned.ItemId,out count);
    int seed=-1;byte upgrade=0;
    Offline70EconomyItemDefinition grantedDef=Offline70Economy.Definition(granted);
    if(grantedDef.Kind==4){seed=Offline70ContainerLotteryFG373.Seed(receipt);if(table.UpgradeRoll){int roll=Offline70ContainerLotteryFG373.UpgradeRoll(receipt);uint[] band=Offline70PotFG93.UpgradeBand(roll);upgrade=Offline70PotFG93.Upgrade(roll,Offline70ContainerLotteryFG373.UpgradeValue(receipt,(int)(band[1]-band[0]+1)));}}
    assets=Offline70Economy.UseContainerFG373(economyPath,receipt,new Offline70EconomyUseRequest{List=0,Slot=slot,ItemId=owned.ItemId,Tick=sequence},DateTime.UtcNow.Ticks,granted,count,seed,upgrade,out duplicate);
    if(duplicate){Note("CONTAINER_RECEIPT_DUPLICATE_IGNORED seq="+sequence);return;}reward=Offline70PotFG93.NewReward(granted,before,assets);body=Offline70PotFG93.Body(slot,reward,true);
    Note("CONTAINER_OPENED sourceSlot="+slot+" item="+owned.ItemId+" reward="+reward.ItemId+" count="+reward.Quantity+" list="+reward.List+" slot="+reward.Slot+(seed>=0?" seed="+seed:"")+(upgrade!=0?" upgrade="+upgrade:""));
   }else{
    if(dungeon.Active)throw new InvalidDataException("Pot requires town");
    int upgradeRoll=Offline70PotFG93.Draw(10000);uint[] band=Offline70PotFG93.UpgradeBand(upgradeRoll);
    assets=Offline70PotFG93.Open(economyPath,session+"-pot-"+sequence,slot,Offline70PotFG93.Draw(100001),upgradeRoll,Offline70PotFG93.Draw((int)(band[1]-band[0]+1)),Offline70PotFG93.Seed(),Offline70PotFG93.UnixNow(),out duplicate);
    if(duplicate){Note("POT_RECEIPT_DUPLICATE_IGNORED seq="+sequence);return;}reward=Offline70PotFG93.NewReward(before,assets);body=Offline70PotFG93.Body(slot,reward,true);Note("POT_COMMITTED sourceSlot="+slot+" reward="+reward.ItemId+" list="+reward.List+" slot="+reward.Slot+" upgrade="+reward.Upgrade+" expires="+reward.ExpiresUnix);
   }
  }catch(InvalidDataException e){collection=null;body=Offline70PotFG93.Body(0,null,false);Note("POT_REJECTED "+e.Message);}
  potRequestsFG93.Add(sequence,fingerprint);Send(body[0]==1?"pot-open29":"pot-failed29",Offline70PotFG93.Reply(body,keys));
  // The original29 callback consumes the container and adds the reward itself.
  // Do not rebuild inventories while the reward popup holds object references.
  if(body[0]==1){
   Sync();
   /* A stackable reward can merge into a held stack, which the single reward row in the op29
      reply cannot express; the op14 delta carries the merged count. */
   if(containerFG373){byte[] delta=Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys);if(delta!=null)Send("container-open-assets",delta);}
   if(reward.List==1&&reward.ExpiresUnix!=0)Send("pot-avatar-period14",Offline70PotFG93.AvatarPeriodReply(reward,keys));if(before.Gold!=assets.Gold)Send("pot-wallet14",Offline70EconomyProtocol.WalletReply(assets,keys));CollectionChanged(collection);}
 }
}
