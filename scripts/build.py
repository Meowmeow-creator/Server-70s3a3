from pathlib import Path
import argparse, os, shutil, subprocess, json, hashlib

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--research',action='store_true')
parser.add_argument('--smoke',action='store_true')
parser.add_argument('--compiler',type=Path)
args=parser.parse_args()
base=root/'research/tower' if args.research else root
sources=sorted((base/'src/Tools/VmTestKit').glob('*.cs'))
assert sources, 'Missing sources'
assert not any('research' in p.relative_to(root).parts for p in sources) or args.research
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework'
candidates=[args.compiler] if args.compiler else [framework/'v3.5/csc.exe',framework/'v4.0.30319/csc.exe']
compiler=next((p for p in candidates if p and p.is_file()),None)
if not compiler: raise SystemExit('Install a .NET Framework C# compiler or pass --compiler PATH')
out=root/'build'/('tower-research' if args.research else 'stable');out.mkdir(parents=True,exist_ok=True)
env={k.upper():v for k,v in os.environ.items()}
for filename,main in [('DNF70-Channel-FG.exe','Offline70BatchServiceFG'),('DNF70-Entrance-FG.exe','Offline70BatchEntranceFG')]:
    result=subprocess.run([str(compiler),'/nologo','/target:exe','/main:'+main,'/out:'+str(out/filename),*map(str,sources)],env=env,capture_output=True)
    (out/(filename+'.compile.txt')).write_bytes(result.stdout+result.stderr)
    if result.returncode: raise SystemExit((result.stdout+result.stderr).decode('utf-8','replace'))
    if args.smoke:
        result=subprocess.run([str(out/filename)],env=env,capture_output=True,timeout=15)
        assert result.returncode==2, 'No-argument mode must reject before opening a service/store'
for p in (base/'data').iterdir():
    if p.is_file(): shutil.copy2(p,out/p.name)
proof={'scope':'Compilation and no-argument rejection only; no gameplay/store/ports',
       'sourceCount':len(sources),'research':args.research,'releaseAllowed':False,'candidateOnly':True,
       'compiler':str(compiler),'smokePassed':args.smoke,
       'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.is_file() and not p.name.endswith('.txt') and p.name!='build-proof.json'}}
(out/'build-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps({'compiled':len(sources),'research':args.research,'smoke':args.smoke}))
