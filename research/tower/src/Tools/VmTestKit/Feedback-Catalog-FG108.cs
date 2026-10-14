using System;using System.Collections.Generic;
class Offline70FeedbackCatalogFG108 {
 internal class Avatar {internal uint Commodity,Item,Price;internal byte Options;}
 internal class Tag {internal uint Commodity,Item,Price,Days;}
 internal static readonly Dictionary<uint,Avatar> Avatars=new Dictionary<uint,Avatar>();
 internal static readonly Dictionary<uint,Tag> Tags=new Dictionary<uint,Tag>();
 static Offline70FeedbackCatalogFG108(){
 Avatars.Add(166031,new Avatar{Commodity=166031,Item=42059,Price=1980,Options=4});
 Avatars.Add(166030,new Avatar{Commodity=166030,Item=41593,Price=1980,Options=6});
 Avatars.Add(166029,new Avatar{Commodity=166029,Item=41282,Price=6000,Options=3});
 Avatars.Add(166028,new Avatar{Commodity=166028,Item=40890,Price=6000,Options=48});
 Avatars.Add(166027,new Avatar{Commodity=166027,Item=40411,Price=1980,Options=4});
 Avatars.Add(166026,new Avatar{Commodity=166026,Item=40002,Price=1980,Options=4});
 Avatars.Add(166025,new Avatar{Commodity=166025,Item=39664,Price=2640,Options=5});
 Avatars.Add(166024,new Avatar{Commodity=166024,Item=39254,Price=1980,Options=5});
 Tags.Add(100615,new Tag{Commodity=100615,Item=2681313,Price=300,Days=7});
 Tags.Add(100620,new Tag{Commodity=100620,Item=2681313,Price=600,Days=30});
 Tags.Add(100616,new Tag{Commodity=100616,Item=2681314,Price=300,Days=7});
 Tags.Add(100621,new Tag{Commodity=100621,Item=2681314,Price=600,Days=30});
 Tags.Add(100617,new Tag{Commodity=100617,Item=2681315,Price=300,Days=7});
 Tags.Add(100622,new Tag{Commodity=100622,Item=2681315,Price=600,Days=30});
 Tags.Add(100618,new Tag{Commodity=100618,Item=2681316,Price=300,Days=7});
 Tags.Add(100623,new Tag{Commodity=100623,Item=2681316,Price=600,Days=30});
 Tags.Add(100619,new Tag{Commodity=100619,Item=2681317,Price=300,Days=7});
 Tags.Add(100624,new Tag{Commodity=100624,Item=2681317,Price=600,Days=30});
 Tags.Add(100772,new Tag{Commodity=100772,Item=2681318,Price=300,Days=7});
 Tags.Add(100774,new Tag{Commodity=100774,Item=2681318,Price=600,Days=30});
 Tags.Add(100773,new Tag{Commodity=100773,Item=2681319,Price=300,Days=7});
 Tags.Add(100775,new Tag{Commodity=100775,Item=2681319,Price=600,Days=30});
 Tags.Add(100884,new Tag{Commodity=100884,Item=2681320,Price=300,Days=7});
 Tags.Add(100885,new Tag{Commodity=100885,Item=2681320,Price=600,Days=30});
 Tags.Add(100886,new Tag{Commodity=100886,Item=2681321,Price=300,Days=7});
 Tags.Add(100887,new Tag{Commodity=100887,Item=2681321,Price=600,Days=30});
 Tags.Add(100903,new Tag{Commodity=100903,Item=2747068,Price=300,Days=7});
 Tags.Add(100904,new Tag{Commodity=100904,Item=2747068,Price=600,Days=30});
 Tags.Add(100905,new Tag{Commodity=100905,Item=2747069,Price=300,Days=7});
 Tags.Add(100906,new Tag{Commodity=100906,Item=2747069,Price=600,Days=30});
 Tags.Add(101057,new Tag{Commodity=101057,Item=2747131,Price=300,Days=7});
 Tags.Add(101058,new Tag{Commodity=101058,Item=2747131,Price=600,Days=30});
 Tags.Add(101059,new Tag{Commodity=101059,Item=2747132,Price=300,Days=7});
 Tags.Add(101060,new Tag{Commodity=101060,Item=2747132,Price=600,Days=30});
 Tags.Add(101105,new Tag{Commodity=101105,Item=2747331,Price=300,Days=7});
 Tags.Add(101106,new Tag{Commodity=101106,Item=2747331,Price=600,Days=30});
 Tags.Add(101103,new Tag{Commodity=101103,Item=2747330,Price=300,Days=7});
 Tags.Add(101104,new Tag{Commodity=101104,Item=2747330,Price=600,Days=30});
 }
 internal static bool NameTag(uint item){if(item==0)return true;foreach(Tag t in Tags.Values)if(t.Item==item)return true;return false;}
 internal static void AddOffers(Dictionary<uint,Offline70CashPurchaseFG45.Offer> rows){
 rows.Add(101076,new Offline70CashPurchaseFG45.Offer{Commodity=101076,Category=3,Item=2682571,Cost=480,Rewards=new uint[][]{new uint[]{2682571,1}}});
 rows.Add(101077,new Offline70CashPurchaseFG45.Offer{Commodity=101077,Category=3,Item=2682571,Cost=2380,Rewards=new uint[][]{new uint[]{2682571,5}}});
 }
}