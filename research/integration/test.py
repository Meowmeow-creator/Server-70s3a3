from pathlib import Path
import subprocess,sys,os,json
root=Path(__file__).resolve().parents[2];here=Path(__file__).parent;out=root/'build/integration-research';proof=json.loads((out/'build-proof.json').read_text())
for name in ['reinforcement','worlddrop']:subprocess.run([sys.executable,str(root/'research'/name/'test.py'),'--integrated'],check=True,cwd=root)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework';compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None);assert compiler
env={k.upper():v for k,v in os.environ.items()};exe=out/'IntegrationBoundariesFG183.exe';r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:IntegrationBoundariesFG183','/out:'+str(exe),*map(str,sorted((out/'source').glob('*.cs'))),str(here/'tests/Boundaries-FG183.cs')],capture_output=True,env=env);(out/'boundaries-compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe),str(out)],capture_output=True,timeout=20,env=env);(out/'boundaries-result.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
j=json.loads(r.stdout);j['serviceSha256']=proof['serviceSha256'];(out/'boundaries-result.json').write_text(json.dumps(j,indent=2));print(json.dumps(j))
exe=out/'Act4MechanicsCheckFG.exe';r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:Act4MechanicsCheckFG','/out:'+str(exe),str(root/'src/Tools/VmTestKit/Act4-Mechanics-Check-FG.cs')],capture_output=True,env=env);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe),str(out/'DNF70-Channel-FG.exe')],capture_output=True,timeout=20,env=env);(out/'act4-mechanics.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr;print(json.dumps(dict(act4MechanicsPassed=True,checks=r.stdout.count(b'OK:'))))
