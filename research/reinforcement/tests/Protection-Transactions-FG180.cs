using System;using System.IO;using System.Xml;using System.Xml.Serialization;
class ProtectionTransactionsFG180 {
    static string directory,path;static int cases,checks;static OrdinaryReinforcementCatalogFG176 catalog;
    static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
    static CouponRequestFG141 Request(uint id){return new CouponRequestFG141{Type=0,Target=10,Item=id,Ticket=113,Additional=65535};}
    static uint Count(Offline70EconomyState s,uint id){uint n=0;foreach(Offline70EconomyEntry e in s.Inventory)if(e.ItemId==id)n+=e.Quantity;return n;}
    static void Mutate(Action<Offline70EconomyState> action){bool duplicate;Offline70Economy.Apply(path,"fixture-"+Guid.NewGuid().ToString("N"),"fixture","setup",action,out duplicate);}
    static OrdinaryReinforcementTargetFG176 Target(int rarity){foreach(OrdinaryReinforcementTargetFG176 t in catalog.Targets)if(t.Rarity==rarity&&Offline70BreakReturnsFG177.CanReturn(t))return t;throw new Exception("No actual target");}
    static void Seed(OrdinaryReinforcementTargetFG176 t,byte old){path=Path.Combine(directory,"protected-"+cases+++".xml");Offline70Economy.Initialize(path,0,false);Mutate(delegate(Offline70EconomyState s){s.Level=70;s.Experience=Offline70ProgressionFG.Threshold(70);s.Gold=t.GoldCost+123;s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=10,ItemId=t.Id,Quantity=1,Upgrade=old,Durability=t.Durability});Offline70Economy.AddAssetItem(s,7804,2);s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=113,ItemId=3171,Quantity=1000});});}
    static void Invalid(Offline70EconomyReceipt receipt,byte[] b){receipt.ReinforcementOutcomeFG176=Convert.ToBase64String(b);try{Offline70OrdinaryReinforcementFG176.ValidateReceipt(receipt);throw new Exception("Expected malformed protected receipt refusal");}catch(InvalidDataException){checks++;}}
    static int Main(string[] a){try{
        if(a.Length!=2)return 2;directory=Path.GetFullPath(a[0]);if(directory.IndexOf("reinforcement-research",StringComparison.OrdinalIgnoreCase)<0&&directory.IndexOf(Path.Combine("build","integration-research"),StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Disposable scope");Directory.CreateDirectory(directory);
        Offline70WishBoxFG84.Register();Offline70BoosterRewardsFG47.Register();Offline70BoosterOpenFG46.Register();Offline70CashPurchaseFG45.Register();Offline70NpcPurchaseFG44.Register();Offline70ItemMetadataFG.Register(Path.Combine(a[1],"item-metadata.xml"));
        string data=Path.Combine(a[1],"ordinary-reinforcement-FG176.xml");Offline70OrdinaryReinforcementFG176.Load(data);using(FileStream f=File.OpenRead(data))catalog=(OrdinaryReinforcementCatalogFG176)new XmlSerializer(typeof(OrdinaryReinforcementCatalogFG176)).Deserialize(f);
        CouponResultFG141 result;bool duplicate;
        foreach(int rarity in new int[]{0,1,2,3,5})foreach(OrdinaryReinforcementStepFG176 step in catalog.Steps){
            if(!(step.Penalty==3||rarity<2&&step.Grade>=8))continue;
            OrdinaryReinforcementTargetFG176 t=Target(rarity);
            foreach(int roll in new int[]{0,100000}){
                Seed(t,step.Grade);Offline70EconomyState s=Offline70OrdinaryReinforcementFG176.Use(path,"protect",Request(t.Id),roll,0,0,out result,out duplicate);
                Check(!duplicate&&s.Gold==123&&Count(s,3171)==1000-step.Quantity,"Atomic gold/material debit");
                Check(Offline70Economy.Find(s,0,10)!=null&&result.TailFG177==null&&Count(s,3037)==0&&Count(s,3243)==0,"Protected/successful gear never becomes break materials");
                if(roll==0){Check(result.NoticeFG180==216&&result.New==0&&result.Result==2&&Count(s,7804)==1,"One actual protection consumes one ticket and clears upgrade");Check(Offline70Economy.Find(s,0,10).Upgrade==0&&result.ProtectionRemainingFG180==1,"Committed reset and protection count");}
                else Check(result.NoticeFG180==0&&result.New==step.Grade+1&&Count(s,7804)==2,"Successful upgrade never consumes protection");
                byte notice=result.NoticeFG180,grade=result.New;byte[] committed=File.ReadAllBytes(path);
                s=Offline70OrdinaryReinforcementFG176.Use(path,"protect",Request(t.Id),100000-roll,9999,9999,out result,out duplicate);
                Check(duplicate&&result.NoticeFG180==notice&&result.New==grade,"Receipt replays the committed protection decision");Check(Convert.ToBase64String(committed)==Convert.ToBase64String(File.ReadAllBytes(path)),"Retry charged or rewrote store");
                Check(Count(Offline70Economy.Load(path),7804)==(roll==0?1u:2u),"Protection survives reload exactly");
            }
        }
        OrdinaryReinforcementTargetFG176 rare=Target(2);
        foreach(byte old in new byte[]{4,7}){Seed(rare,old);Offline70EconomyState s=Offline70OrdinaryReinforcementFG176.Use(path,"downgrade",Request(rare.Id),0,0,0,out result,out duplicate);Check(result.NoticeFG180==0&&result.New<old&&Count(s,7804)==2,"Ordinary downgrade does not consume protection");}
        Seed(rare,10);Mutate(delegate(Offline70EconomyState s){Offline70EconomyEntry p=Offline70ReinforcementProtectionFG180.Find(s);p.Slot=114;s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=3,ItemId=7804,Quantity=1});});
        Offline70EconomyState after=Offline70OrdinaryReinforcementFG176.Use(path,"lowest-slot",Request(rare.Id),0,0,0,out result,out duplicate);
        Check(result.ProtectionSlotFG180==3&&result.ProtectionRemainingFG180==0&&Offline70Economy.Find(after,0,3)==null&&Offline70Economy.Find(after,0,114).Quantity==2,"Select lowest eligible slot and remove only exhausted stack");
        string good=after.Receipts[after.Receipts.Count-1].ReinforcementOutcomeFG176;byte[] envelope=Convert.FromBase64String(good);Check(envelope.Length==26&&envelope[0]==0&&envelope[1]==216&&envelope[2]==180,"Versioned envelope is separate from two-byte wire notice");
        Mutate(delegate(Offline70EconomyState s){Offline70Economy.Find(s,0,10).Upgrade=5;});byte[] later=File.ReadAllBytes(path);Offline70OrdinaryReinforcementFG176.Use(path,"lowest-slot",Request(rare.Id),100000,0,0,out result,out duplicate);Check(duplicate&&result.NoticeFG180==216&&result.New==0&&Offline70Economy.Load(path).Inventory.Exists(delegate(Offline70EconomyEntry e){return e.List==0&&e.Slot==10&&e.Upgrade==5;})&&Convert.ToBase64String(later)==Convert.ToBase64String(File.ReadAllBytes(path)),"Old protected result cannot roll back later grade");
        foreach(uint unknown in new uint[]{8050,8378}){Seed(rare,10);Mutate(delegate(Offline70EconomyState s){Offline70Economy.AddAssetItem(s,unknown,1);});byte[] before=File.ReadAllBytes(path);try{Offline70OrdinaryReinforcementFG176.Use(path,"mixed",Request(rare.Id),0,0,0,out result,out duplicate);throw new Exception("Expected unknown protection priority refusal");}catch(CouponEligibilityErrorFG141 e){Check(e.Code==19,"Unknown protection remains refused");}Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(path)),"Mixed protection lost assets");}
        Seed(rare,10);Mutate(delegate(Offline70EconomyState s){for(ushort i=105;i<137;i++)if(Offline70Economy.Find(s,0,i)==null)s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=i,ItemId=3015,Quantity=1000});for(ushort i=3;i<9;i++)s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=i,ItemId=3015,Quantity=1000});});
        byte[] full=File.ReadAllBytes(path);try{Offline70OrdinaryReinforcementFG176.Use(path,"full",Request(rare.Id),0,0,0,out result,out duplicate);throw new Exception("Expected unchanged original capacity guard");}catch(CouponEligibilityErrorFG141 e){Check(e.Code==21,"Original preflight capacity remains required");}Check(Convert.ToBase64String(full)==Convert.ToBase64String(File.ReadAllBytes(path)),"Full bag consumed gold/material/protection");
        foreach(int offset in new int[]{0,1,2,3,4,6,10,12,16,18,19,20,22}){byte[] bad=(byte[])envelope.Clone();if(offset==4||offset==10||offset==20)BitConverter.GetBytes((ushort)2).CopyTo(bad,offset);else if(offset==6)BitConverter.GetBytes((uint)3171).CopyTo(bad,offset);else if(offset==12||offset==22)BitConverter.GetBytes((uint)1000).CopyTo(bad,offset);else bad[offset]=(byte)(offset==18?31:offset==16?0:255);Invalid(new Offline70EconomyReceipt{Kind="npc-reinforce"},bad);}
        Invalid(new Offline70EconomyReceipt{Kind="npc-reinforce"},new byte[25]);Invalid(new Offline70EconomyReceipt{Kind="npc-reinforce"},new byte[27]);Invalid(new Offline70EconomyReceipt{Kind="other"},envelope);
        // An invalid protected envelope in an actual disposable XML must be
        // refused by Load and left intact, rather than silently normalized.
        Seed(rare,10);after=Offline70OrdinaryReinforcementFG176.Use(path,"xml",Request(rare.Id),0,0,0,out result,out duplicate);XmlDocument doc=new XmlDocument();doc.Load(path);XmlNode node=doc.SelectSingleNode("//ReinforcementOutcomeFG176");byte[] corrupt=Convert.FromBase64String(node.InnerText);corrupt[19]=1;node.InnerText=Convert.ToBase64String(corrupt);doc.Save(path);byte[] invalid=File.ReadAllBytes(path);try{Offline70Economy.Load(path);throw new Exception("Expected invalid protected XML refusal");}catch(InvalidDataException){checks++;}Check(Convert.ToBase64String(invalid)==Convert.ToBase64String(File.ReadAllBytes(path)),"Invalid XML was rewritten");
        Console.WriteLine("{\"allPassed\":true,\"protectionCases\":"+cases+",\"checks\":"+checks+",\"releaseAllowed\":false}");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
