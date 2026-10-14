using System;using System.IO;using System.Collections.Generic;
class Offline70PetEquipmentFG103 {
 internal static void AddOffers(Dictionary<uint,Offline70CashPurchaseFG45.Offer> rows){
  rows.Add(101075,new Offline70CashPurchaseFG45.Offer{Commodity=101075,Category=3,Item=2747175,Cost=1580,Once=false,Rewards=new uint[][]{new uint[]{2747175,1}}});
  rows.Add(101074,new Offline70CashPurchaseFG45.Offer{Commodity=101074,Category=3,Item=2747174,Cost=1580,Once=false,Rewards=new uint[][]{new uint[]{2747174,1}}});
  rows.Add(101073,new Offline70CashPurchaseFG45.Offer{Commodity=101073,Category=3,Item=2747160,Cost=1280,Once=false,Rewards=new uint[][]{new uint[]{2747160,1}}});
  rows.Add(101072,new Offline70CashPurchaseFG45.Offer{Commodity=101072,Category=3,Item=2747159,Cost=1280,Once=false,Rewards=new uint[][]{new uint[]{2747159,1}}});
 }
 // Current CN/TW worn artifact slots23/24/25; A14 uses different offsets.
 // These four PVF files require character level30, no creature-minimum-level.
 internal static void RequirePet(Offline70EconomyState s,Offline70EconomyItemDefinition d){if(d.Kind==8&&Offline70PetEquipFG97.Equipped(s)==null)throw new InvalidDataException("Pet artifact requires equipped creature");}
}
