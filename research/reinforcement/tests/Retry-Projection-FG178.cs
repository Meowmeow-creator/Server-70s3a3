using System;using System.IO;using System.Collections.Generic;
class RetryProjectionTestsFG178 {
    static int checks,cases;
    static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
    static Offline70EconomyEntry Item(ushort slot,uint id,uint qty){return new Offline70EconomyEntry{List=0,Slot=slot,ItemId=id,Quantity=qty,Durability=Offline70Economy.Definition(id).MaxDurability};}
    static Dictionary<ushort,byte[]> Rows(byte[] body){Check(body[0]==0,"Inventory list");int n=BitConverter.ToUInt16(body,1);Check(body.Length==3+31*n,"Plain ordinary row widths");Dictionary<ushort,byte[]> r=new Dictionary<ushort,byte[]>();for(int i=0;i<n;i++){byte[] row=new byte[31];Buffer.BlockCopy(body,3+31*i,row,0,31);r[BitConverter.ToUInt16(row,0)]=row;}return r;}
    static void Reject(Offline70EconomyState current,ushort[] slots){try{Offline70ReinforcementRetryFG178.Body(current,slots);throw new Exception("Expected invalid projection");}catch(InvalidDataException){checks++;}}
    static int Main(string[] a){try{
        if(a.Length!=2)return 2;string dir=Path.GetFullPath(a[0]);if(dir.IndexOf("reinforcement-research",StringComparison.OrdinalIgnoreCase)<0&&dir.IndexOf(Path.Combine("build","integration-research"),StringComparison.OrdinalIgnoreCase)<0)throw new Exception("Disposable scope");Directory.CreateDirectory(dir);
        Offline70WishBoxFG84.Register();Offline70BoosterRewardsFG47.Register();Offline70BoosterOpenFG46.Register();Offline70CashPurchaseFG45.Register();Offline70NpcPurchaseFG44.Register();Offline70ItemMetadataFG.Register(Path.Combine(a[1],"item-metadata.xml"));
        string path=Path.Combine(dir,"projection.xml");Offline70Economy.Initialize(path,0,false);bool dupe;
        Offline70EconomyState before=Offline70Economy.Apply(path,"seed","fixture","before",delegate(Offline70EconomyState s){s.Gold=40000;s.Inventory.Add(Item(10,14126,1));s.Inventory.Add(Item(113,3171,1));s.Inventory.Add(Item(153,4183,5));},out dupe);
        Offline70EconomyState committed=Offline70Economy.Apply(path,"commit","fixture","destroy",delegate(Offline70EconomyState s){s.Gold=30000;s.Inventory.Clear();s.Inventory.Add(Item(114,3037,20));},out dupe);
        ushort[] slots=Offline70ReinforcementRetryFG178.Slots(before,committed);Check(slots.Length==4&&slots[0]==10&&slots[1]==113&&slots[2]==114&&slots[3]==153,"Union includes the task page and newly allocated slots");
        foreach(bool reuse in new bool[]{false,true}){
            Offline70EconomyState current=Offline70Economy.Apply(path,"later-"+reuse,"fixture","later",delegate(Offline70EconomyState s){s.Gold=20000;s.Inventory.Clear();s.Inventory.Add(Item(115,3037,7));if(reuse)s.Inventory.Add(Item(10,10050,1));},out dupe);
            byte[] bytes=File.ReadAllBytes(path),projection=Offline70ReinforcementRetryFG178.Body(current,slots);Dictionary<ushort,byte[]> rows=Rows(projection);
            Check(BitConverter.ToUInt32(rows[0],6)==20000,"Use latest wallet, not committed wallet");
            Check(BitConverter.ToUInt32(rows[10],2)==(reuse?10050u:UInt32.MaxValue),"Reused target slot survives, missing target is removed");
            Check(BitConverter.ToUInt32(rows[113],2)==UInt32.MaxValue&&BitConverter.ToUInt32(rows[114],2)==UInt32.MaxValue,"Consumed material and moved return tombstones");
            Check(BitConverter.ToUInt32(rows[115],2)==3037&&BitConverter.ToUInt32(rows[115],6)==7,"Current absolute material quantity");
            // Missing first reply and already-applied first reply converge to
            // the same authoritative rows; repeated projections never ADD.
            foreach(int repeats in new int[]{1,2,10}){
                Dictionary<ushort,byte[]> client=new Dictionary<ushort,byte[]>();
                foreach(Offline70EconomyEntry e in (repeats==1?before:committed).Inventory)client[e.Slot]=Offline70EconomyProtocol.InventoryRow(e);
                for(int j=0;j<repeats;j++)foreach(KeyValuePair<ushort,byte[]> row in rows){if(BitConverter.ToUInt32(row.Value,2)==UInt32.MaxValue)client.Remove(row.Key);else client[row.Key]=row.Value;}
                Check(!client.ContainsKey(113)&&!client.ContainsKey(114)&&client.ContainsKey(10)==reuse,"Projection clears ghosts without deleting reused slot");
                Check(BitConverter.ToUInt32(client[115],6)==7,"Repeated projection is absolute");cases++;
            }
            Check(Convert.ToBase64String(bytes)==Convert.ToBase64String(File.ReadAllBytes(path)),"Projection cannot mutate storage");
            Check(BitConverter.ToUInt32(rows[153],2)==UInt32.MaxValue,"Task-page tombstone uses current slot range");
            Reject(current,null);Reject(current,new ushort[]{2});Reject(current,new ushort[]{41});Reject(current,new ushort[]{89});Reject(current,new ushort[]{137});Reject(current,new ushort[]{185});Reject(current,new ushort[]{10,10});Reject(current,new ushort[]{114,10});Reject(current,new ushort[135]);
        }
        // Normal cached83 can describe an older grade; the following14 must
        // project the latest grade after subsequent successful operations.
        Offline70EconomyState upgraded=Offline70Economy.Apply(path,"upgraded","fixture","grade",delegate(Offline70EconomyState s){s.Inventory.Clear();Offline70EconomyEntry item=Item(10,10050,1);item.Upgrade=7;s.Inventory.Add(item);},out dupe);
        Dictionary<ushort,byte[]> final=Rows(Offline70ReinforcementRetryFG178.Body(upgraded,slots));Check((final[10][10]&31)==7,"Latest upgrade attributes");cases++;
        CouponRequestFG141 request=new CouponRequestFG141{Type=0,Target=10,Ticket=113,Item=10050,Additional=65535};
        Offline70ReinforcementRetryFG178.Context normal=new Offline70ReinforcementRetryFG178.Context(before,upgraded,request,new CouponResultFG141{MechanismFG176=0,New=7,Remaining=0});
        Check(normal.CanReplay83(upgraded),"Unchanged normal result can clear waiting with original83");
        Offline70EconomyState changed=Offline70Economy.Preview(upgraded,delegate(Offline70EconomyState s){Offline70Economy.Find(s,0,10).Upgrade=8;});Check(!normal.CanReplay83(changed),"Later grade suppresses stale83");
        changed=Offline70Economy.Preview(upgraded,delegate(Offline70EconomyState s){s.Inventory.Clear();});Check(!normal.CanReplay83(changed),"Missing target suppresses83");
        changed=Offline70Economy.Preview(upgraded,delegate(Offline70EconomyState s){s.Inventory.Add(Item(113,3037,1));});Check(!normal.CanReplay83(changed),"Reused material slot suppresses83");
        Offline70ReinforcementRetryFG178.Context broken=new Offline70ReinforcementRetryFG178.Context(before,committed,request,new CouponResultFG141{MechanismFG176=0,Result=3});Check(!broken.CanReplay83(upgraded),"Destructive result never replays even when target slot reused");
        byte[] replacement=broken.Body(upgraded);bool removedBeforeReplacement=false;uint last=0;
        for(int i=0;i<BitConverter.ToUInt16(replacement,1);i++){int off=3+31*i;if(BitConverter.ToUInt16(replacement,off)!=10)continue;uint id=BitConverter.ToUInt32(replacement,off+2);if(id==UInt32.MaxValue)removedBeforeReplacement=true;else{Check(removedBeforeReplacement,"Changed target ID needs removal before replacement");last=id;}}
        Check(last==10050&&BitConverter.ToUInt32(Rows(replacement)[10],2)==10050,"Current different item ID wins after tombstone");cases++;
        Offline70EconomyState full=Offline70Economy.Preview(upgraded,delegate(Offline70EconomyState s){
            s.Inventory.Clear();for(ushort i=3;i<41;i++)s.Inventory.Add(Item(i,10050,1));
            for(ushort i=57;i<89;i++)s.Inventory.Add(Item(i,15,1));
            for(ushort i=105;i<137;i++)s.Inventory.Add(Item(i,3171,1));
            for(ushort i=153;i<185;i++)s.Inventory.Add(Item(i,4183,1));
        });
        Offline70EconomyState empty=Offline70Economy.Preview(full,delegate(Offline70EconomyState s){s.Inventory.Clear();});
        ushort[] allSlots=Offline70ReinforcementRetryFG178.Slots(full,empty);Check(allSlots.Length==134,"Full ordinary bag capacity");
        Dictionary<ushort,byte[]> fullRows=Rows(Offline70ReinforcementRetryFG178.Body(empty,allSlots));Check(fullRows.Count==135,"Full tombstone list and wallet");
        foreach(ushort slot in allSlots)Check(BitConverter.ToUInt32(fullRows[slot],2)==UInt32.MaxValue,"Missing slot tombstone");cases++;
        Console.WriteLine("{\"allPassed\":true,\"projectionCases\":"+cases+",\"checks\":"+checks+",\"releaseAllowed\":false}");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
