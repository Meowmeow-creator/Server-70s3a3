using System;
partial class Offline70BatchServiceFG {
 bool nameCardAppearancePendingFG108;
 void MaintainNameCardFG108(){
  if(!selected||assets==null)return;
  if(assets.NameTagItemFG108!=0&&assets.NameTagExpiryFG108<=Offline70PotFG93.UnixNow()){
   Offline70FeedbackCashFG108.Expire(economyPath);Sync();nameCardAppearancePendingFG108=true;Note("NAME_CARD_EXPIRED");
  }
  // Appearance2 rebuilds the town actor. During a dungeon keep the current
  // room state intact and apply the expired appearance on the next town loop.
  if(nameCardAppearancePendingFG108&&!dungeon.Active){nameCardAppearancePendingFG108=false;Appearance();}
 }
}
