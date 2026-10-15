"""Compile a single fatigue-feedback patch in an isolated copy of FG175."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess
root=Path(__file__).resolve().parents[2]
out=root/'build/fatigue-feedback-research';src=out/'source';src.mkdir(parents=True,exist_ok=True)
old='new byte[]{0,19,0,0,0}'
new='new byte[]{0,22,0,0,0,0}'
original={}
for p in sorted((root/'src/Tools/VmTestKit').glob('*.cs')):
    original[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
    if p.name=='Fatigue-State-FG50.cs':
        text=p.read_text('utf-8-sig');assert (text.count(old),text.count(new)) in ((1,0),(0,1)),'Fatigue patch baseline changed'
        if text.count(new)==1:
            shutil.copyfile(p,src/p.name);continue
        text=text.replace('// C02080 reads a u32 error after the generic status byte. A JP60 u8 error would underflow.', '// Current C02080/C014B0: uint32 error22 plus exhausted party seat0.\n // The one-byte error used by other versions is not valid for this client.')
        (src/p.name).write_text(text.replace(old,new),encoding='utf-8-sig')
    else:shutil.copyfile(p,src/p.name)
assert len(original)==150 and {p.name for p in src.glob('*.cs')}==set(original)
for p in (root/'data').iterdir():
    if p.is_file():shutil.copyfile(p,out/p.name)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework'
compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None)
assert compiler,'A .NET Framework C# compiler is required'
env={k.upper():v for k,v in os.environ.items()}
exe=out/'DNF70-Channel-FG.exe'
r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:Offline70BatchServiceFG','/out:'+str(exe),*map(str,sorted(src.glob('*.cs')))],capture_output=True,env=env)
(out/'compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,env=env,timeout=15);assert r.returncode==2
proof=dict(sourceCount=len(original),sharedPatches=[] if (root/'src/Tools/VmTestKit/Fatigue-State-FG50.cs').read_text('utf-8-sig').count(new)==1 else ['Fatigue-State-FG50.cs'],stableBase=original,sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in src.glob('*.cs')},serviceSha256=hashlib.sha256(exe.read_bytes()).hexdigest(),smokePassed=True,releaseAllowed=False,deployed=False)
(out/'build-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps(dict(compiled=len(original),sharedPatches=proof['sharedPatches'],serviceSha256=proof['serviceSha256'],releaseAllowed=False)))
