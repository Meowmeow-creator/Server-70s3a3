from pathlib import Path
import json,subprocess,os,uuid
root=Path(__file__).resolve().parents[2];here=Path(__file__).parent
out=root/'build/reinforcement-research'
assert (out/'build-proof.json').is_file(), 'Build the isolated research first'
fixture=out/('transactions-'+uuid.uuid4().hex[:8]);fixture.mkdir()
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework'
compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None)
assert compiler, 'A .NET Framework compiler is required'
exe=out/'Transactions-FG176.exe'
r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:ReinforcementTransactionsFG176','/out:'+str(exe),*map(str,sorted((out/'source').glob('*.cs'))),str(here/'tests/Transactions-FG176.cs')],capture_output=True)
(fixture/'compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe),str(fixture),str(out)],capture_output=True,timeout=60)
(fixture/'result.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
report=json.loads(r.stdout);report['serviceSha256']=json.loads((out/'build-proof.json').read_text())['serviceSha256']
(fixture/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
exe=out/'BreakTransactions-FG177.exe'
r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:BreakTransactionsFG177','/out:'+str(exe),*map(str,sorted((out/'source').glob('*.cs'))),str(here/'tests/Break-Transactions-FG177.cs')],capture_output=True)
(fixture/'break-compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
breakDir=fixture/'break';breakDir.mkdir()
r=subprocess.run([str(exe),str(breakDir),str(out)],capture_output=True,timeout=60)
(fixture/'break-result.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
report=json.loads(r.stdout);report['serviceSha256']=json.loads((out/'build-proof.json').read_text())['serviceSha256']
(fixture/'break-result.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
