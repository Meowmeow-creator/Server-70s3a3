from pathlib import Path
import hashlib,json,re,subprocess,xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[1]
try:
    result=subprocess.run(['git','ls-files','-z'],cwd=root,capture_output=True,check=True)
    paths=[root/p for p in result.stdout.decode('utf-8').split('\0') if p]
except (subprocess.SubprocessError,FileNotFoundError): paths=[]
if not paths: paths=[p for p in root.rglob('*') if p.is_file() and not any(x in p.relative_to(root).parts for x in ('.git','build','local','__pycache__'))]
bad=[]
blocked_suffix={'.exe','.dll','.pvf','.db','.sqlite','.log','.dmp','.pcap','.pcapng','.pem','.key','.zip'}
for p in paths:
    name=p.relative_to(root).as_posix()
    if p.suffix.lower() in blocked_suffix or any(x.lower() in ('stores','runtime','captures','local','logs') for x in p.relative_to(root).parts) or p.name.startswith('.env'):
        bad.append(name+': prohibited runtime/player/binary file')
    if p.suffix.lower() in ('.cs','.py','.xml','.md','.json','.yml'):
        t=p.read_text('utf-8-sig')
        if re.search(r'^(?:<{7}|>{7}|={7})(?: |$)',t,re.M): bad.append(name+': unresolved merge marker')
        # Credential shapes, not game cipher tables or protocol terminology.
        if re.search(r'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|-----BEGIN (?:RSA |OPENSSH )?PRIVATE KEY-----',t):
            bad.append(name+': possible credential')
        if p.suffix=='.xml':
            if '<!DOCTYPE' in t.upper() or '<!ENTITY' in t.upper(): bad.append(name+': XML DTD/entity')
            doc=ET.fromstring(t)
            for parent in doc.iter():
                ids=[]
                for child in parent:
                    ident=child.findtext('Id') or child.findtext('ItemId')
                    if ident is not None: ids.append((child.tag,child.findtext('Shop'),ident))
                if len(ids)!=len(set(ids)): bad.append(name+': duplicate sibling record IDs')
stable=list((root/'src/Tools/VmTestKit').glob('*.cs'))
for p in stable:
    if p.name.startswith(('Tower-Service','Death-Tower','Tower-Rewards','Tower-Experience','Tower-Items','Typed-Enemy')):
        bad.append(str(p.relative_to(root))+': unreleased tower implementation in stable sources')
manifest=json.loads((root/'baseline/manifest.json').read_text('utf-8'))
for name in manifest['files']:
    if not (root/name).is_file(): bad.append(name+': missing baseline path (rename/removal needs explicit migration record)')
if bad: raise SystemExit('\n'.join(bad))
print(json.dumps({'repositoryChecksPassed':True,'checkedFiles':len(paths),'stableSources':len(stable)}))
