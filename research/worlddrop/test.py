from pathlib import Path
import subprocess,os,json,uuid,hashlib
root=Path(__file__).resolve().parents[2];here=Path(__file__).parent;out=root/'build/worlddrop-research';trial=out/('transactions-'+uuid.uuid4().hex[:8]);trial.mkdir(parents=True)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework';compiler=next((p for p in [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe'] if p.is_file()),None);assert compiler
env={k.upper():v for k,v in os.environ.items()};exe=trial/'WorldTransactions182.exe';r=subprocess.run([str(compiler),'/nologo','/target:exe','/main:WorldTransactions182','/out:'+str(exe),*map(str,sorted((out/'source').glob('*.cs'))),str(here/'tests/World-Transactions-FG182.cs')],capture_output=True,env=env);(trial/'compile.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
quotes=here/'tests/native-quotes.tsv';assert hashlib.sha256(quotes.read_bytes()).hexdigest()=='d1d24e7d007618e1ae2ede04e8d0289ce80c3857859a31f6cf3fbe4b7e00fe32'
r=subprocess.run([str(exe),str(trial),str(out),str(quotes)],capture_output=True,env=env,timeout=90);(trial/'result.txt').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
report=json.loads(r.stdout);report['serviceSha256']=json.loads((out/'build-proof.json').read_text())['serviceSha256'];(trial/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
