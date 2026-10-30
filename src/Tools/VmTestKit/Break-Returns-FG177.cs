using System;using System.IO;using System.Collections.Generic;

// Break returns for a failed ordinary reinforcement at the destruction tier.
//
// FG398: the eligibility gate is gone. CItemUpgrade::_ProcPenalty (reference/rednf
// CItemUpgrade/_ProcPenalty.inc) deletes the item and then calls the *ordinary*
// decomposition result - G_CDataManager()->m_disJoint->GetResult(item->m_addInfo, djr)
// (reference/rednf ItemUpgradeVtableB2/GetResult.inc) - with no _checkDisjointableItem
// pre-check at all; the only requirement is that the item script exists (GetResult
// returns 0x11 for a null script and _ProcPenalty then reports a zero count). It then
// calls _getAdditionalDisjointItem, whose constants are the [disjoint] section of
// etc/upgrade.etc: [disjoint bonus item] 3243, [equip level const] 40, [upgrade const
// for bonus item count] 10, [upgrade failed bonus weight by rarity] and [correction
// grade by rarity]. Those values are what Bonus() already hardcodes.
//
// So the old 1310-row Disjoint-Catalog-FG92 gate ("only items whose decomposition we
// had proved may break") was our own whitelist, not the original rule. It now reads the
// full data/disjoint-params-FG92.dat row, which is the same [value]/[rarity]/[grade]
// triple the op28 decomposition uses.
class Offline70BreakReturnsFG177 {
    static Offline70DisjointParamsFG92.Params Row(OrdinaryReinforcementTargetFG176 t) {
        Offline70DisjointParamsFG92.Params p=Offline70DisjointParamsFG92.Get(t.Id);
        if(p==null)throw new InvalidDataException("Break-return item script missing item="+t.Id);
        return p;
    }
    internal static bool CanReturn(OrdinaryReinforcementTargetFG176 t) {
        return t!=null&&Offline70DisjointParamsFG92.Get(t.Id)!=null;
    }
    internal static void Preflight(Offline70EconomyState s,OrdinaryReinforcementTargetFG176 t) {
        if(!CanReturn(t))throw new CouponEligibilityErrorFG141(0x13,"Break-return item script missing");
        Offline70DisjointParamsFG92.Params p=Row(t);
        // CN7804 has its own user-confirmed policy. The reference8050/8378
        // candidates still have placeholder CN localizations and unknown
        // priority/effects; do not silently select through either of them.
        foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&(e.ItemId==8050||e.ItemId==8378))throw new CouponEligibilityErrorFG141(0x13,"Protection semantics not integrated");
        int empty=0;
        for(ushort slot=105;slot<137;slot++)if(Offline70Economy.Find(s,0,slot)==null)empty++;
        for(ushort slot=3;slot<9;slot++)if(Offline70Economy.Find(s,0,slot)==null)empty++;
        int need=Offline70DisjointCatalogFG92.Extras[p.Rarity].Length==0?2:3;
        if(empty<need)throw new CouponEligibilityErrorFG141(0x15,"Original break-return material/quick slots preflight");
    }
    internal static uint Bonus(OrdinaryReinforcementTargetFG176 t,byte old) {
        int[] correction=new int[]{1,1,2,4,5,3};
        float[] weights=new float[]{0,0,1f,1.2f,1.5f,1f};
        if(Math.Max(0,t.Grade-correction[t.Rarity])<40||old<=10)return 0;
        int span=old-10;
        // Integer square then the original float32 rarity coefficient, followed
        // by truncation. No invented +0.5 or equipment-grade squared formula.
        // Native shape: pow((unsigned)(up-[upgrade const for bonus item count]),2) *
        // [upgrade failed bonus weight by rarity], truncated once.
        return (uint)((double)(span*span)*(double)weights[t.Rarity]);
    }
    internal static byte[] Destroy(Offline70EconomyState s,Offline70EconomyEntry target,OrdinaryReinforcementTargetFG176 t,int choice,int chance) {
        if(choice<0||choice>=10000||chance<0||chance>=10000)throw new InvalidDataException("Break-return random range");
        Offline70DisjointParamsFG92.Params p=Row(t);
        Dictionary<ushort,Offline70EconomyEntry> before=new Dictionary<ushort,Offline70EconomyEntry>();
        foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0)before[e.Slot]=new Offline70EconomyEntry{ItemId=e.ItemId,Quantity=e.Quantity};
        byte old=target.Upgrade;s.Inventory.Remove(target);
        Offline70Economy.AddAssetItem(s,3037,Offline70DisjointFG92.BaseCount(p.Value,p.Rarity));
        uint[] pool=Offline70DisjointCatalogFG92.Extras[p.Rarity];
        if(pool.Length>0) {
            float divisor=chance<Offline70DisjointCatalogFG92.JackpotPercents[p.Rarity]*100?Offline70DisjointCatalogFG92.JackpotDivisors[p.Rarity]:Offline70DisjointCatalogFG92.NormalDivisors[p.Rarity];
            Offline70Economy.AddAssetItem(s,pool[choice%pool.Length],Math.Max(1u,(uint)((float)p.Grade/divisor)));
        }
        uint bonus=Bonus(t,old);if(bonus>0)Offline70Economy.AddAssetItem(s,3243,bonus);
        // CN C1A3D0 reads a byte count, then {ushort slot,uint item,uint delta}.
        // Existing material objects ADD the delta; these are not final totals.
        List<Offline70EconomyEntry> gains=new List<Offline70EconomyEntry>();
        foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0) {
            Offline70EconomyEntry prior;uint previous=before.TryGetValue(e.Slot,out prior)&&prior.ItemId==e.ItemId?prior.Quantity:0;
            if(e.Quantity>previous)gains.Add(new Offline70EconomyEntry{Slot=e.Slot,ItemId=e.ItemId,Quantity=e.Quantity-previous});
        }
        if(gains.Count<1||gains.Count>38)throw new InvalidDataException("Break-return projection bounds");
        using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)) {
            w.Write((byte)gains.Count);
            foreach(Offline70EconomyEntry e in gains){if(Offline70Economy.Definition(e.ItemId).Kind!=2)throw new InvalidDataException("Break-return material type");w.Write(e.Slot);w.Write(e.ItemId);w.Write(e.Quantity);}
            return m.ToArray();
        }
    }
}
