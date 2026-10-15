"""Explicit world-drop research build, preserving stable sources and data."""
from pathlib import Path
import os,json,hashlib,shutil,subprocess
root=Path(__file__).resolve().parents[2];here=Path(__file__).parent;out=root/'build/worlddrop-research';src=out/'source';src.mkdir(parents=True,exist_ok=True)
original={}
for p in sorted((root/'src/Tools/VmTestKit').glob('*.cs')):
 original[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
 if p.name in ['Batch-Service-FG.cs','Floor-Drops-FG119.cs']:
  text=p.read_text('utf-8-sig')
  if p.name=='Batch-Service-FG.cs':
   old='Offline70ItemMetadataFG.Register(Path.Combine(baseDir,"item-metadata.xml"));';new=old+'Offline70ItemMetadataFG.Register(Path.Combine(baseDir,"world-item-metadata-FG182.xml"));Offline70WorldDropsFG182.Load(Path.Combine(baseDir,"world-drops-FG182.xml"));'
  else:
   old='if(rewards.Count>MaximumRows)';new='Offline70WorldDropsFG182.Append(rewards,mon[1],kind,receipt);'+old
  assert text.count(old)==1,'Shared source baseline changed';(src/p.name).write_text(text.replace(old,new),encoding='utf-8-sig')
 else:shutil.copyfile(p,src/p.name)
shutil.copyfile(here/'World-Drops-FG182.cs',src/'World-Drops-FG182.cs');assert len(original)==145 and {p.name for p in src.glob('*.cs')}==set(original)|{'World-Drops-FG182.cs'}
for folder in [root/'data',here/'data']:
 for p in folder.iterdir():
  if p.is_file():shutil.copyfile(p,out/p.name)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework';compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None);assert compiler
env={k.upper():v for k,v in os.environ.items()};exe=out/'DNF70-Channel-FG.exe';r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:Offline70BatchServiceFG','/out:'+str(exe),*map(str,sorted(src.glob('*.cs')))],capture_output=True,env=env);(out/'compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,timeout=15,env=env);assert r.returncode==2
proof=dict(sourceCount=146,stableBase=original,sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in src.glob('*.cs')},serviceSha256=hashlib.sha256(exe.read_bytes()).hexdigest(),sharedPatches=['Batch-Service-FG.cs','Floor-Drops-FG119.cs'],releaseAllowed=False,deployed=False,smokePassed=True)
(out/'build-proof.json').write_text(json.dumps(proof,indent=2));print(json.dumps(dict(compiled=146,serviceSha256=proof['serviceSha256'],releaseAllowed=False)))
