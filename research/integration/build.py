"""Explicit combined candidate: current src plus reinforcement, fatigue and world drops."""
from pathlib import Path
import subprocess,sys,shutil,json,hashlib,os
root=Path(__file__).resolve().parents[2];here=Path(__file__).parent;out=root/'build/integration-research';src=out/'source';src.mkdir(parents=True,exist_ok=True)
for name in ['reinforcement','fatigue','worlddrop']:
 subprocess.run([sys.executable,str(root/'research'/name/'build.py')],check=True,cwd=root)
base=root/'build/reinforcement-research'
for p in (base/'source').glob('*.cs'):shutil.copyfile(p,src/p.name)
shutil.copyfile(root/'build/fatigue-feedback-research/source/Fatigue-State-FG50.cs',src/'Fatigue-State-FG50.cs')
for name,old,new in [
 ('Batch-Service-FG.cs','Offline70ItemMetadataFG.Register(Path.Combine(baseDir,"item-metadata.xml"));','Offline70ItemMetadataFG.Register(Path.Combine(baseDir,"item-metadata.xml"));Offline70ItemMetadataFG.Register(Path.Combine(baseDir,"world-item-metadata-FG182.xml"));Offline70WorldDropsFG182.Load(Path.Combine(baseDir,"world-drops-FG182.xml"));'),
 ('Floor-Drops-FG119.cs','if(rewards.Count>MaximumRows)','Offline70WorldDropsFG182.Append(rewards,mon[1],kind,receipt);if(rewards.Count>MaximumRows)')]:
 p=src/name;s=p.read_text('utf-8-sig');assert s.count(old)==1;s=s.replace(old,new);p.write_text(s,encoding='utf-8-sig')
shutil.copyfile(root/'research/worlddrop/World-Drops-FG182.cs',src/'World-Drops-FG182.cs')
expected={p.name for p in (root/'src/Tools/VmTestKit').glob('*.cs')}|{'Ordinary-Reinforcement-FG176.cs','Break-Returns-FG177.cs','Retry-Projection-FG178.cs','Protection-FG180.cs','World-Drops-FG182.cs'}
assert len(expected)==155 and {p.name for p in src.glob('*.cs')}==expected,'Unexpected combined source set'
for folder in [base,root/'research/worlddrop/data']:
 for p in folder.iterdir():
  if p.is_file() and p.suffix in ('.xml','.dat'):shutil.copyfile(p,out/p.name)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework';compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None);assert compiler
env={k.upper():v for k,v in os.environ.items()};exe=out/'DNF70-Channel-FG.exe';r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:Offline70BatchServiceFG','/out:'+str(exe),*map(str,sorted(src.glob('*.cs')))],capture_output=True,env=env);(out/'compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,timeout=15,env=env);assert r.returncode==2
proof=dict(sourceCount=155,serviceSha256=hashlib.sha256(exe.read_bytes()).hexdigest(),sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in src.glob('*.cs')},releaseAllowed=False,deployed=False,smokePassed=True);(out/'build-proof.json').write_text(json.dumps(proof,indent=2));print(json.dumps(dict(compiled=155,serviceSha256=proof['serviceSha256'],releaseAllowed=False)))
