using System;using System.IO;using System.Collections.Generic;
partial class Offline70BatchServiceFG {
 readonly Dictionary<ushort,string> potRequestsFG93=new Dictionary<ushort,string>();
 void PotFG93(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;if(potRequestsFG93.TryGetValue(sequence,out prior)){if(prior!=fingerprint)throw new InvalidDataException("Pot sequence reused");Note("POT_DUPLICATE_IGNORED seq="+sequence);return;}
  byte[] body;Dictionary<ushort,uint> collection=null;Offline70EconomyState before=null;Offline70EconomyEntry reward=null;
  try{if(dungeon.Active)throw new InvalidDataException("Pot requires town");ushort slot=Offline70PotFG93.Parse(encrypted,keys);Sync();collection=CollectionSnapshot();before=assets;bool duplicate;
   int upgradeRoll=Offline70PotFG93.Draw(10000);uint[] band=Offline70PotFG93.UpgradeBand(upgradeRoll);
   assets=Offline70PotFG93.Open(economyPath,session+"-pot-"+sequence,slot,Offline70PotFG93.Draw(100001),upgradeRoll,Offline70PotFG93.Draw((int)(band[1]-band[0]+1)),Offline70PotFG93.Seed(),Offline70PotFG93.UnixNow(),out duplicate);
   if(duplicate){Note("POT_RECEIPT_DUPLICATE_IGNORED seq="+sequence);return;}reward=Offline70PotFG93.NewReward(before,assets);body=Offline70PotFG93.Body(slot,reward,true);Note("POT_COMMITTED sourceSlot="+slot+" reward="+reward.ItemId+" list="+reward.List+" slot="+reward.Slot+" upgrade="+reward.Upgrade+" expires="+reward.ExpiresUnix);
  }catch(InvalidDataException e){collection=null;body=Offline70PotFG93.Body(0,null,false);Note("POT_REJECTED "+e.Message);}
  potRequestsFG93.Add(sequence,fingerprint);Send(body[0]==1?"pot-open29":"pot-failed29",Offline70PotFG93.Reply(body,keys));
  // The original29 callback consumes the pot and adds the reward itself.
  // Do not rebuild inventories while the reward popup holds object references.
  if(body[0]==1){Sync();if(reward.List==1&&reward.ExpiresUnix!=0)Send("pot-avatar-period14",Offline70PotFG93.AvatarPeriodReply(reward,keys));if(before.Gold!=assets.Gold)Send("pot-wallet14",Offline70EconomyProtocol.WalletReply(assets,keys));CollectionChanged(collection);}
 }
}
