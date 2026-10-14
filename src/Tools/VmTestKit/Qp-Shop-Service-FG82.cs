using System;using System.IO;using System.Collections.Generic;
partial class Offline70BatchServiceFG {
 readonly Dictionary<string,string> qpRequests=new Dictionary<string,string>();readonly Dictionary<string,byte[]> qpReplies=new Dictionary<string,byte[]>();
 void QpShop(ushort op,byte[] body,ushort sequence){
  string key=op+":"+sequence,fingerprint=Convert.ToBase64String(body),prior;
  if(qpRequests.TryGetValue(key,out prior)){if(prior!=fingerprint)throw new InvalidDataException("QP sequence reused");Send("qp-repeat"+op,qpReplies[key]);Sync();Send("qp-repeat-balance",Offline70QuestPointsFG81.Reply(assets,keys));return;}
  bool success=false;byte id=0;ushort level=0;Offline70EconomyState before=assets;
  try{if(dungeon.Active)throw new InvalidDataException("QP changes require town");Sync();before=assets;bool duplicate;
   if(op==407){byte[] r=Offline70QpShopFG82.ParseBuy(body,keys);id=r[0];assets=Offline70QpShopFG82.Buy(economyPath,session+"-qp-buy-"+sequence,id,r[1],out duplicate);level=Offline70QpShopFG82.Level(assets,id);Note("QP_STAT_BOUGHT id="+id+" levels="+r[1]+" total="+level+" remaining="+assets.QuestPoints);}
   else{if(body.Length!=0)throw new InvalidDataException("QP408 empty request required");assets=Offline70QpShopFG82.Reset(economyPath,session+"-qp-reset-"+sequence,0,false,out duplicate);Note("QP_RESET_GOLD refund="+(assets.QuestPoints-before.QuestPoints)+" cost="+(before.Gold-assets.Gold)+" resetCount="+assets.QuestPointResetCount);}
   success=true;
  }catch(InvalidDataException e){Note("QP_REJECTED opcode="+op+" reason="+e.Message);}
  byte[] reply=op==407?Offline70QpShopFG82.BuyReply(success,id,level,keys):Offline70QpShopFG82.ResetReply(success,assets,keys);qpRequests.Add(key,fingerprint);qpReplies.Add(key,reply);Send("qp-result"+op,reply);
  if(success){Sync();Send("qp-current-balance",Offline70QuestPointsFG81.Reply(assets,keys));if(before.Gold!=assets.Gold)Send("qp-reset-wallet",Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys));}
 }
 bool UseQpResetItem(byte[] body,ushort sequence){
  string key="32:"+sequence,fingerprint=Convert.ToBase64String(body),prior;
  if(qpRequests.TryGetValue(key,out prior)){if(prior!=fingerprint)throw new InvalidDataException("QP reset-item sequence reused");Send("qp-item-repeat32",qpReplies[key]);Sync();Send("qp-item-repeat-inventory",Offline70EconomyProtocol.InventoryRestoreReply(assets,keys));Send("qp-item-repeat-balance",Offline70QuestPointsFG81.Reply(assets,keys));return true;}
  ushort slot;try{slot=Offline70EconomyProtocol.SkillBookSlot(body,keys);}catch(InvalidDataException){return false;}
  Sync();Offline70EconomyEntry item=Offline70Economy.Find(assets,0,slot);if(item==null||item.ItemId!=2682275)return false;
  bool success=false;Dictionary<ushort,uint> collection=CollectionSnapshot();
  try{if(dungeon.Active)throw new InvalidDataException("QP reset item requires town");bool duplicate;assets=Offline70QpShopFG82.Reset(economyPath,session+"-qp-item-"+sequence,slot,true,out duplicate);success=true;Note("QP_RESET_ITEM slot="+slot+" remaining="+assets.QuestPoints+" resetCount="+assets.QuestPointResetCount);}
  catch(InvalidDataException e){Note("QP_ITEM_REJECTED "+e.Message);}
  byte[] reply=Offline70QpShopFG82.ItemReply(slot,success,keys);qpRequests.Add(key,fingerprint);qpReplies.Add(key,reply);Send("qp-reset-item32",reply);
  if(success){Sync();Send("qp-item-inventory",Offline70EconomyProtocol.InventoryRestoreReply(assets,keys));Send("qp-item-balance",Offline70QuestPointsFG81.Reply(assets,keys));CollectionChanged(collection);}return true;
 }
}
