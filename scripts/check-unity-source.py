"""Compile current DazPose sources using installed Unity reference inputs; never launches Unity or Play.
Requires the existing project Library compiler response files from a prior Editor refresh.
Outputs only ignored .dazposewizard/p2-p3/compile artifacts. This is not a native Editor test.
"""
from pathlib import Path
import json,re,subprocess,sys
repo=Path(__file__).resolve().parents[1]; project=repo/'validation/DazPoseUnityValidation'
output=repo/'.dazposewizard/p2-p3/compile';output.mkdir(parents=True,exist_ok=True)
unity=Path('C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data')
compiler=unity/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
definitions={}
for f in (project/'Assets').rglob('*.asmdef'):
 data=json.loads(f.read_text());name=data['name']
 if name.startswith('DazPose.'):definitions[name]=(f,data)
guid_names={}
for name,(f,data) in definitions.items():
 m=re.search(r'^guid:\s*(\w+)',Path(str(f)+'.meta').read_text(),re.M)
 if m:guid_names['GUID:'+m[1]]=name
order=[];pending=set(definitions)
while pending:
 ready=sorted(n for n in pending if not any(guid_names.get(r,r) in pending for r in definitions[n][1]['references']))
 if not ready:raise RuntimeError('Assembly cycle: '+str(pending))
 order.extend(ready);pending.difference_update(ready)
def owner(path):
 for folder in path.parents:
  candidates=list(folder.glob('*.asmdef'))
  if candidates:return json.loads(candidates[0].read_text())['name']
  refs=list(folder.glob('*.asmref'))
  if refs:return guid_names.get(json.loads(refs[0].read_text())['reference'],json.loads(refs[0].read_text())['reference'])
  if folder==project/'Assets':return None
sources={n:[] for n in definitions}
for f in (project/'Assets').rglob('*.cs'):
 n=owner(f)
 if n in sources:sources[n].append(f.relative_to(project).as_posix())
results=[]
for name in order:
 responses=sorted((project/'Library/Bee/artifacts').rglob(name+'.rsp'),key=lambda f:f.stat().st_mtime,reverse=True)
 if not responses:raise RuntimeError('Unity compiler input missing: '+name)
 lines=responses[0].read_text(encoding='utf-8-sig').splitlines()
 if name=='DazPose.Validation.Editor':
  extra=sorted((project/'Library/Bee/artifacts').rglob('DazPose.Editor.rsp'),key=lambda f:f.stat().st_mtime,reverse=True)[0]
  lines+= [line for line in extra.read_text(encoding='utf-8-sig').splitlines() if line.startswith('-r:') and line not in lines]
 filtered=[]
 wanted={guid_names.get(r,r) for r in definitions[name][1]['references']}
 for line in lines:
  if line.strip('"').endswith('.cs'):continue
  if line.startswith(('-out:','-refout:')):continue
  m=re.match(r'-r:"?[^"\r\n]*[/\\](DazPose\.[^/\\"]+?)(?:\.ref)?\.dll"?$',line)
  if m:
   dependency=m[1]
   if dependency not in wanted:continue
   line='-r:"'+(output/(dependency+'.ref.dll')).as_posix()+'"'
  filtered.append(line)
 for dependency in sorted(wanted.intersection(definitions)):
  reference='-r:"'+(output/(dependency+'.ref.dll')).as_posix()+'"'
  if reference not in filtered:filtered.append(reference)
 for dependency in sorted(wanted.difference(definitions)):
  if dependency.startswith('GUID:'):continue
  dll=project/'Library/ScriptAssemblies'/(dependency+'.dll')
  if dll.exists():
   reference='-r:"'+dll.as_posix()+'"'
   if reference not in filtered:filtered.append(reference)
 for symbol in ['UNITY_EDITOR','UNITY_EDITOR_64','UNITY_EDITOR_WIN']:
  if '-define:'+symbol not in filtered:filtered.append('-define:'+symbol)
 filtered.extend(['-out:"'+(output/(name+'.dll')).as_posix()+'"','-refout:"'+(output/(name+'.ref.dll')).as_posix()+'"','/warnaserror+'])
 filtered.extend('"'+s+'"' for s in sorted(sources[name]))
 response=output/(name+'.rsp');response.write_text('\n'.join(filtered)+'\n',encoding='utf-8')
 run=subprocess.run([str(unity/'NetCoreRuntime/dotnet.exe'),str(compiler),'@'+str(response)],cwd=project,text=True,capture_output=True)
 (output/(name+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8')
 results.append({'assembly':name,'exitCode':run.returncode,'sources':len(sources[name]),'input':str(responses[0].relative_to(project))})
 print(name+': '+('PASS' if run.returncode==0 else 'FAIL'),flush=True)
 if run.returncode:
  print((run.stdout+run.stderr)[-16000:]);break
(output/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
raise SystemExit(any(r['exitCode'] for r in results))
