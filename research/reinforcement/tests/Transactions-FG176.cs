using System;using System.IO;using System.Collections.Generic;using System.Xml;using System.Xml.Serialization;
class ReinforcementTransactionsFG176 {
    static string directory,path;static int checks,cases;
    static OrdinaryReinforcementCatalogFG176 catalog;
    static void Check(bool condition,string text){checks++;if(!condition)throw new Exception(text);}
    static OrdinaryReinforcementTargetFG176 Target(byte rarity){foreach(OrdinaryReinforcementTargetFG176 t in catalog.Targets)if(t.Rarity==rarity&&t.Durability>0)return t;throw new Exception("Missing rarity fixture");}
    static CouponRequestFG141 Request(uint id){return new CouponRequestFG141{Type=0,Target=10,Item=id,Ticket=113,Additional=65535};}
    static void Seed(OrdinaryReinforcementTargetFG176 target,byte grade,uint gold,uint material){
        path=Path.Combine(directory,"case-"+cases+++".xml");Offline70Economy.Initialize(path,0,false);bool duplicate;
        Offline70Economy.Apply(path,"fixture","fixture","owned",delegate(Offline70EconomyState s){
            s.Gold=gold;s.Level=70;s.Experience=Offline70ProgressionFG.Threshold(70);
            s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=10,ItemId=target.Id,Quantity=1,Durability=target.Durability,Upgrade=grade});
            if(material>0)s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=113,ItemId=3171,Quantity=material});
        },out duplicate);
    }
    static void Mutate(Action<Offline70EconomyState> mutation){bool duplicate;Offline70Economy.Apply(path,"fixture-"+Guid.NewGuid().ToString("N"),"fixture","edge",mutation,out duplicate);}
    static void Reject(CouponRequestFG141 r,byte error){byte[] before=File.ReadAllBytes(path);CouponResultFG141 o;bool dupe;
        try{Offline70OrdinaryReinforcementFG176.Use(path,"reject",r,100000,out o,out dupe);throw new Exception("Expected rejection "+error);}catch(CouponEligibilityErrorFG141 e){Check(e.Code==error,"Wrong original error");}
        Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(path)),"Rejection wrote state");
    }
    static int Main(string[] args){try{
        if(args.Length!=2)return 2;directory=Path.GetFullPath(args[0]);
        if(directory.IndexOf("reinforcement-research",StringComparison.OrdinalIgnoreCase)<0&&directory.IndexOf(Path.Combine("build","integration-research"),StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Disposable fixture scope");
        Directory.CreateDirectory(directory);
        Offline70WishBoxFG84.Register();Offline70BoosterRewardsFG47.Register();Offline70BoosterOpenFG46.Register();Offline70CashPurchaseFG45.Register();Offline70NpcPurchaseFG44.Register();
        Offline70ItemMetadataFG.Register(Path.Combine(args[1],"item-metadata.xml"));Offline70CouponFG141.Load(Path.Combine(args[1],"coupon-rules-FG141.xml"));
        string data=Path.Combine(args[1],"ordinary-reinforcement-FG176.xml");Offline70OrdinaryReinforcementFG176.Load(data);
        using(FileStream f=File.OpenRead(data))catalog=(OrdinaryReinforcementCatalogFG176)new XmlSerializer(typeof(OrdinaryReinforcementCatalogFG176)).Deserialize(f);
        // Exhaust every allowed grade/rarity at both sides of its exact threshold.
        foreach(byte rarity in new byte[]{0,1,2,3,5})foreach(OrdinaryReinforcementStepFG176 step in catalog.Steps){
            if(step.Grade>9||rarity<2&&step.Grade>=8)continue;OrdinaryReinforcementTargetFG176 t=Target(rarity);
            foreach(int roll in new int[]{0,Math.Max(0,(int)step.FailureThreshold-1),(int)step.FailureThreshold,100000}){
                Seed(t,step.Grade,t.GoldCost+123,100);CouponResultFG141 o;bool dupe;
                Offline70EconomyState after=Offline70OrdinaryReinforcementFG176.Use(path,"transaction",Request(t.Id),roll,out o,out dupe);
                bool fail=roll<step.FailureThreshold;byte expected=fail?(byte)Math.Max(0,step.Grade-step.Drop):(byte)(step.Grade+1);
                Check(!dupe&&o.Old==step.Grade&&o.New==expected&&o.Result==(fail?step.Penalty:0)&&o.MechanismFG176==0,"Boundary outcome");
                Check(after.Gold==123&&Offline70Economy.Find(after,0,113).Quantity==100-step.Quantity&&Offline70Economy.Find(after,0,10).Upgrade==expected,"Real atomic debits/grade");
                byte[] committed=File.ReadAllBytes(path);
                after=Offline70OrdinaryReinforcementFG176.Use(path,"transaction",Request(t.Id),100000-roll,out o,out dupe);
                Check(dupe&&o.New==expected&&Convert.ToBase64String(committed)==Convert.ToBase64String(File.ReadAllBytes(path)),"Duplicate consumed or rerolled");
                after=Offline70Economy.Load(path);Check(after.Gold==123&&Offline70Economy.Find(after,0,10).Upgrade==expected,"Reload outcome");
            }
        }
        OrdinaryReinforcementTargetFG176 rare=Target(2);CouponRequestFG141 req=Request(rare.Id);
        Seed(rare,0,rare.GoldCost*3,3);CouponResultFG141 result;bool duplicate;
        Offline70OrdinaryReinforcementFG176.Use(path,"first",req,100000,out result,out duplicate);
        Offline70OrdinaryReinforcementFG176.Use(path,"second",req,100000,out result,out duplicate);
        Check(result.New==2&&result.Remaining==0,"Last material removed");byte[] twice=File.ReadAllBytes(path);
        Offline70OrdinaryReinforcementFG176.Use(path,"first",req,0,out result,out duplicate);
        Check(duplicate&&result.Old==0&&result.New==1&&result.Remaining==2&&Convert.ToBase64String(twice)==Convert.ToBase64String(File.ReadAllBytes(path)),"Saved old outcome after later upgrade");
        req.Item++;
        try{Offline70OrdinaryReinforcementFG176.Use(path,"first",req,0,out result,out duplicate);throw new Exception("Receipt collision accepted");}catch(InvalidDataException){Check(Convert.ToBase64String(twice)==Convert.ToBase64String(File.ReadAllBytes(path)),"Receipt collision altered state");}
        req=Request(rare.Id);
        Seed(rare,0,rare.GoldCost-1,1);Reject(req,10);
        Seed(rare,3,rare.GoldCost,3);Reject(req,0x16);
        Seed(rare,0,rare.GoldCost,0);Reject(req,0x16);
        Seed(rare,0,rare.GoldCost,1);Mutate(delegate(Offline70EconomyState s){Offline70Economy.Find(s,0,10).Durability--;});Reject(req,7);
        Seed(rare,0,rare.GoldCost,1);Mutate(delegate(Offline70EconomyState s){Offline70Economy.Find(s,0,10).Sealed=true;});Reject(req,0x13);
        Seed(rare,31,rare.GoldCost,100);Reject(req,0x13);
        Seed(Target(0),8,100000,100);Mutate(delegate(Offline70EconomyState s){Offline70Economy.AddAssetItem(s,8050,1);});Reject(Request(Target(0).Id),0x13);
        Seed(Target(1),8,100000,100);Mutate(delegate(Offline70EconomyState s){Offline70Economy.AddAssetItem(s,8378,1);});Reject(Request(Target(1).Id),0x13);
        Seed(rare,0,rare.GoldCost,1);CouponRequestFG141 wrong=Request(rare.Id);wrong.Additional=66;Reject(wrong,0x13);
        wrong=Request(rare.Id);wrong.Type=1;Reject(wrong,0x13);
        wrong=Request(rare.Id);wrong.Item++;Reject(wrong,4);
        // Full equipment page does not force fake capacity errors for branches
        // which return no item; destruction remains rejected before any debit.
        Seed(rare,0,rare.GoldCost,1);Mutate(delegate(Offline70EconomyState s){for(ushort slot=3;slot<=40;slot++)if(slot!=10)s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=slot,ItemId=rare.Id,Quantity=1,Durability=rare.Durability});});
        Offline70EconomyState full=Offline70OrdinaryReinforcementFG176.Use(path,"full",req,100000,out result,out duplicate);
        Check(full.Gold==0&&Offline70Economy.Find(full,0,113)==null&&full.Inventory.Count==38,"Full bag non-destroy debit");
        // Old no-outcome receipts and zero defaults survive serialization.
        foreach(Offline70EconomyReceipt receipt in full.Receipts)Offline70OrdinaryReinforcementFG176.ValidateReceipt(receipt);
        Check(File.ReadAllText(path).IndexOf("<ReinforcementOutcomeFG176 />",StringComparison.Ordinal)<0,"Absent legacy outcome serialized");
        for(int i=0;i<2000;i++){int roll=Offline70OrdinaryReinforcementFG176.Roll();Check(roll>=0&&roll<=100000,"RNG range");}
        Console.WriteLine("{\"allPassed\":true,\"cases\":"+cases+",\"checks\":"+checks+",\"scope\":\"compiled atomic transactions and saved outcomes; no TCP/game\",\"releaseAllowed\":false}");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
