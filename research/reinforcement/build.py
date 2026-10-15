"""Explicit isolated patches, never overwrite stable sources or run a service."""
from pathlib import Path
import hashlib,json,subprocess,shutil,os
root=Path(__file__).resolve().parents[2]
here=Path(__file__).parent
out=root/'build/reinforcement-research'
src=out/'source';src.mkdir(parents=True,exist_ok=True)
def replace(text,old,new):
    assert text.count(old)==1, old
    return text.replace(old,new)
origin={}
for p in sorted((root/'src/Tools/VmTestKit').glob('*.cs')):
    origin[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
    if p.name not in ('Economy-State-FG.cs','Coupon-Service-FG141.cs','Batch-Service-FG.cs'):
        shutil.copyfile(p,src/p.name);continue
    text=p.read_text('utf-8-sig')
    if p.name=='Economy-State-FG.cs':
        text=replace(text,'public string Kind;}','public string Kind;public string ReinforcementOutcomeFG176;}')
        text=replace(text,'foreach(char c in r.PayloadSha256)','Offline70OrdinaryReinforcementFG176.ValidateReceipt(r);foreach(char c in r.PayloadSha256)')
        # Keep existing Apply unchanged for every legacy transaction. New Apply
        # differs only by creating the receipt before the mutation so it can
        # persist the randomized outcome atomically.
        line=next(x for x in text.splitlines() if 'internal static Offline70EconomyState Apply(string path' in x)
        patched=line.replace('Apply(string path','ApplyWithReceiptFG176(string path').replace('Action<Offline70EconomyState> mutation','Action<Offline70EconomyState,Offline70EconomyReceipt> mutation')
        patched=patched.replace('mutation(after);after.Revision++;after.Receipts.Add(new Offline70EconomyReceipt{Id=receipt,Kind=kind,PayloadSha256=fingerprint});','Offline70EconomyReceipt saved=new Offline70EconomyReceipt{Id=receipt,Kind=kind,PayloadSha256=fingerprint};mutation(after,saved);after.Revision++;after.Receipts.Add(saved);')
        assert 'mutation(after,saved)' in patched
        text=replace(text,line,line+'\n'+patched)
    if p.name=='Coupon-Service-FG141.cs':
        text=replace(text,'internal byte Old,New,Result;','internal byte[] TailFG177;internal byte NoticeFG180;internal ushort ProtectionSlotFG180;internal uint ProtectionRemainingFG180;internal byte MechanismFG176=1;internal byte Old,New,Result;')
        text=replace(text,'if(error!=0)body=new byte[]{0,error};','if(error!=0||result!=null&&result.NoticeFG180!=0)body=new byte[]{0,error!=0?error:result.NoticeFG180};')
        text=replace(text,'w.Write((byte)1);w.Write(result.Old);','w.Write(result.MechanismFG176);w.Write(result.Old);')
        text=replace(text,'w.Write(r.Target);body=m.ToArray();','w.Write(r.Target);if(result.TailFG177!=null)w.Write(result.TailFG177);body=m.ToArray();')
        text=replace(text,'readonly Dictionary<ushort,string> couponRequestsFG141=', 'readonly Dictionary<ushort,Offline70ReinforcementRetryFG178.Context> ordinaryRetryFG178=new Dictionary<ushort,Offline70ReinforcementRetryFG178.Context>();readonly Dictionary<ushort,string> couponRequestsFG141=')
        text=replace(text,'Send("coupon-repeat83",couponRepliesFG141[sequence]);','Offline70ReinforcementRetryFG178.Context retryFG178;if(ordinaryRetryFG178.TryGetValue(sequence,out retryFG178)){Sync();if(retryFG178.CanReplay83(assets))Send("coupon-repeat83",couponRepliesFG141[sequence]);Send("reinforcement-recovery14",retryFG178.Reply(assets,keys));Note("REINFORCEMENT_RETRY_PROJECTED seq="+sequence);return;}Send("coupon-repeat83",couponRepliesFG141[sequence]);')
        text=replace(text,'couponRequestsFG141.Add(sequence,fingerprint);','if(error==0&&result.MechanismFG176==0)ordinaryRetryFG178.Add(sequence,new Offline70ReinforcementRetryFG178.Context(before,assets,request,result));couponRequestsFG141.Add(sequence,fingerprint);')
        old='assets=Offline70CouponFG141.Use(economyPath,session+"-coupon-"+sequence,request,Offline70CouponFG141.Roll(),now,out result,out duplicate);'
        new='Offline70EconomyEntry materialFG176=Offline70Economy.Find(assets,0,request.Ticket);if(materialFG176!=null&&materialFG176.ItemId==3171||request.Ticket>=105&&request.Ticket<137){if(dungeon.Active||!Offline70TownContentFG75.Npc(town,area,17))throw new CouponEligibilityErrorFG141(0x13,"Ordinary reinforcement requires current Kiri area");assets=Offline70OrdinaryReinforcementFG176.Use(economyPath,session+"-npc-reinforce-"+sequence,request,Offline70OrdinaryReinforcementFG176.Roll(),out result,out duplicate);}else{'+old+'}'
        text=replace(text,old,new)
    if p.name=='Batch-Service-FG.cs':
        text=replace(text,'Offline70CouponFG141.Load(Path.Combine(baseDir,"coupon-rules-FG141.xml"));','Offline70CouponFG141.Load(Path.Combine(baseDir,"coupon-rules-FG141.xml"));Offline70OrdinaryReinforcementFG176.Load(Path.Combine(baseDir,"ordinary-reinforcement-FG176.xml"));')
    (src/p.name).write_text(text,encoding='utf-8-sig')
for name in ['Ordinary-Reinforcement-FG176.cs','Break-Returns-FG177.cs','Retry-Projection-FG178.cs','Protection-FG180.cs']:shutil.copyfile(here/name,src/name)
expected=set(origin)|{'Ordinary-Reinforcement-FG176.cs','Break-Returns-FG177.cs','Retry-Projection-FG178.cs','Protection-FG180.cs'}
assert {p.name for p in src.glob('*.cs')}==expected, 'Unexpected stale source in isolated build'
for p in (root/'data').iterdir():
    if p.is_file():shutil.copyfile(p,out/p.name)
shutil.copyfile(here/'data/ordinary-reinforcement-FG176.xml',out/'ordinary-reinforcement-FG176.xml')
import xml.etree.ElementTree as ET
metadata=ET.parse(out/'item-metadata.xml');items=metadata.getroot().find('Items')
for ident in [3243,7804,8050,8378]:
    assert not any(x.findtext('ItemId')==str(ident) for x in items), 'Metadata extension now conflicts with baseline'
    n=ET.SubElement(items,'Offline70EconomyItemDefinition')
    for key,value in dict(ItemId=ident,Kind=2,StackLimit=1000,MinimumLevel=1,Job=255).items():ET.SubElement(n,key).text=str(value)
ET.indent(metadata);metadata.write(out/'item-metadata.xml',encoding='utf-8',xml_declaration=True)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework'
compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None)
assert compiler, 'A .NET Framework compiler is required'
result=subprocess.run([str(compiler),'/nologo','/target:exe','/main:Offline70BatchServiceFG','/out:'+str(out/'DNF70-Channel-FG.exe'),*map(str,sorted(src.glob('*.cs')))],capture_output=True)
(out/'compile.txt').write_bytes(result.stdout+result.stderr)
assert result.returncode==0,result.stdout+result.stderr
proof={'releaseAllowed':False,'deployed':False,'stableBase':origin,'serviceSha256':hashlib.sha256((out/'DNF70-Channel-FG.exe').read_bytes()).hexdigest(),'sharedPatches':['Batch-Service-FG.cs','Coupon-Service-FG141.cs','Economy-State-FG.cs'],'sources':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in src.glob('*.cs')}}
(out/'build-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps({'researchCompiled':len(proof['sources']),'serviceSha256':proof['serviceSha256'],'stableSourceChanged':False,'releaseAllowed':False}))
