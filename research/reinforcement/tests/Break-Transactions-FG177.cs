using System;using System.IO;using System.Xml.Serialization;using System.Collections.Generic;
class BreakTransactionsFG177 {
    static string directory,path;static int cases,checks;static OrdinaryReinforcementCatalogFG176 catalog;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static CouponRequestFG141 Request(uint id){return new CouponRequestFG141{Type=0,Target=10,Item=id,Ticket=113,Additional=65535};}
    static OrdinaryReinforcementTargetFG176 Target(int rarity,bool bonus){foreach(OrdinaryReinforcementTargetFG176 t in catalog.Targets)if(t.Rarity==rarity&&Offline70BreakReturnsFG177.CanReturn(t)&&(!bonus||t.Grade>=50))return t;throw new Exception("No real supported target");}
    static uint Count(Offline70EconomyState s,uint item){uint n=0;foreach(Offline70EconomyEntry e in s.Inventory)if(e.ItemId==item)n+=e.Quantity;return n;}
    static void Mutate(Action<Offline70EconomyState> f){bool duplicate;Offline70Economy.Apply(path,"fixture-"+Guid.NewGuid().ToString("N"),"fixture","edge",f,out duplicate);}
    static void Seed(OrdinaryReinforcementTargetFG176 t,byte old){path=Path.Combine(directory,"break-"+cases+++".xml");Offline70Economy.Initialize(path,0,false);Mutate(delegate(Offline70EconomyState s){s.Level=70;s.Experience=Offline70ProgressionFG.Threshold(70);s.Gold=t.GoldCost+123;s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=10,ItemId=t.Id,Quantity=1,Upgrade=old,Durability=t.Durability});s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=113,ItemId=3171,Quantity=1000});});}
    static void Reject(CouponRequestFG141 r,byte error){byte[] before=File.ReadAllBytes(path);CouponResultFG141 result;bool duplicate;try{Offline70OrdinaryReinforcementFG176.Use(path,"reject",r,100000,0,0,out result,out duplicate);throw new Exception("Expected rejection");}catch(CouponEligibilityErrorFG141 e){Check(e.Code==error,"Original error code");}Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(path)),"Rejected operation wrote assets");}
    static int Main(string[] args){try{
        if(args.Length!=2)return 2;directory=Path.GetFullPath(args[0]);if(directory.IndexOf("reinforcement-research",StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Disposable scope");Directory.CreateDirectory(directory);
        Offline70WishBoxFG84.Register();Offline70BoosterRewardsFG47.Register();Offline70BoosterOpenFG46.Register();Offline70CashPurchaseFG45.Register();Offline70NpcPurchaseFG44.Register();Offline70ItemMetadataFG.Register(Path.Combine(args[1],"item-metadata.xml"));Offline70CouponFG141.Load(Path.Combine(args[1],"coupon-rules-FG141.xml"));
        string data=Path.Combine(args[1],"ordinary-reinforcement-FG176.xml");Offline70OrdinaryReinforcementFG176.Load(data);using(FileStream f=File.OpenRead(data))catalog=(OrdinaryReinforcementCatalogFG176)new XmlSerializer(typeof(OrdinaryReinforcementCatalogFG176)).Deserialize(f);
        foreach(int rarity in new int[]{0,1,2,3,5})foreach(OrdinaryReinforcementStepFG176 step in catalog.Steps){
            if(!(step.Penalty==3||rarity<2&&step.Grade>=8))continue;
            OrdinaryReinforcementTargetFG176 t=Target(rarity,true==false);
            foreach(int roll in new int[]{0,100000}){
                Seed(t,step.Grade);CouponResultFG141 result;bool duplicate;Offline70EconomyState after=Offline70OrdinaryReinforcementFG176.Use(path,"upgrade",Request(t.Id),roll,3,0,out result,out duplicate);
                Check(!duplicate&&after.Gold==123&&Count(after,3171)==1000-step.Quantity,"Atomic gold/material cost");
                if(roll==0){Check(result.Result==3&&result.New==0&&Offline70Economy.Find(after,0,10)==null&&result.TailFG177!=null,"Destroy target exactly once");Check(result.TailFG177.Length==1+10*result.TailFG177[0],"Current byte-count wire width");Check(Count(after,3037)>0,"Base return exists");}
                else Check(result.Result==0&&result.New==step.Grade+1&&Offline70Economy.Find(after,0,10)!=null&&result.TailFG177==null,"Successful high reinforcement does not destroy");
                byte[] committed=File.ReadAllBytes(path);string tail=result.TailFG177==null?null:Convert.ToBase64String(result.TailFG177);
                after=Offline70OrdinaryReinforcementFG176.Use(path,"upgrade",Request(t.Id),100000-roll,9999,9999,out result,out duplicate);
                Check(duplicate&&Convert.ToBase64String(committed)==Convert.ToBase64String(File.ReadAllBytes(path)),"Repeat rerolled or charged");Check(tail==(result.TailFG177==null?null:Convert.ToBase64String(result.TailFG177)),"Saved return lottery changed");
                Offline70EconomyState reload=Offline70Economy.Load(path);Check(reload.Gold==123&&Count(reload,3037)==Count(after,3037),"Reload returns");
            }
        }
        OrdinaryReinforcementTargetFG176 high=Target(3,true);
        Seed(high,11);Mutate(delegate(Offline70EconomyState s){s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=114,ItemId=3037,Quantity=999});});CouponResultFG141 o;bool dup;
        Offline70EconomyState state=Offline70OrdinaryReinforcementFG176.Use(path,"split",Request(high.Id),0,0,9999,out o,out dup);
        Check(Count(state,3243)==1,"+11 bonus truncates1.2 to1");Check(Offline70Economy.Find(state,0,114).Quantity==1000,"Existing material stack filled");
        bool deltaOne=false;for(int i=0;i<o.TailFG177[0];i++){int offset=1+10*i;if(BitConverter.ToUInt16(o.TailFG177,offset)==114){Check(BitConverter.ToUInt32(o.TailFG177,offset+6)==1,"Return must encode delta, not final1000");deltaOne=true;}}Check(deltaOne,"Existing stack delta missing");
        Check(Offline70BreakReturnsFG177.Bonus(high,10)==0&&Offline70BreakReturnsFG177.Bonus(high,30)==480,"Bonus grade boundary/formula");
        Seed(high,10);Mutate(delegate(Offline70EconomyState s){for(ushort slot=105;slot<137;slot++)if(slot!=113)s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=slot,ItemId=3015,Quantity=1000});for(ushort slot=3;slot<9;slot++)s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=slot,ItemId=3015,Quantity=1000});});Reject(Request(high.Id),0x15);
        // Equipment space cannot be substituted for material/quick-bar space.
        Check(Offline70Economy.Find(Offline70Economy.Load(path),0,11)==null,"Equipment page fixture should remain free");
        foreach(uint protector in new uint[]{8050,8378}){Seed(high,10);Mutate(delegate(Offline70EconomyState s){Offline70Economy.AddAssetItem(s,protector,1);});Reject(Request(high.Id),0x13);Check(Count(Offline70Economy.Load(path),protector)==1,"Unsupported protection consumed");}
        Seed(high,10);CouponRequestFG141 explicitTicket=Request(high.Id);explicitTicket.Additional=114;Reject(explicitTicket,0x13);
        Seed(high,31);Reject(Request(high.Id),0x13);
        Console.WriteLine("{\"allPassed\":true,\"breakCases\":"+cases+",\"checks\":"+checks+",\"scope\":\"compiled transaction, fixed rolls and saved return plans\",\"releaseAllowed\":false}");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
