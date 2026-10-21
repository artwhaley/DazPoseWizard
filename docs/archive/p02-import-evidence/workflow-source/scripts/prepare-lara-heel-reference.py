"""Capture an exported Daz heel foot shape against a matching approved body.

This preserves baked foot geometry, not a general locomotion solution. Unity
also retains the exported toe joint centers and recalculates rest bind poses.
"""
import argparse, gzip, hashlib, importlib, json, sys, types
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT/'validation/DazPoseUnityValidation'
def main():
 args=argparse.ArgumentParser(description=__doc__)
 args.add_argument('--fbx',type=Path,default=PROJECT/'Assets/TestCharacter/larasecondoutfit.fbx')
 args.add_argument('--reference',type=Path,default=PROJECT/'Assets/TestCharacter/larafirstoutfit.fbx')
 args.add_argument('--out',type=Path,default=PROJECT/'Assets/TestData/LaraCandidate/SecondOutfit/heel-reference.json')
 args.add_argument('--duf',type=Path)
 args.add_argument('--calibration',type=Path)
 opt=args.parse_args()
 pkg=types.ModuleType('io_scene_fbx');pkg.__path__=['C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core/io_scene_fbx'];sys.modules['io_scene_fbx']=pkg
 parser=importlib.import_module('io_scene_fbx.parse_fbx')
 def child(e,key):return next((c for c in e.elems if c.id==key),None)
 def body(file):
  root,_=parser.parse(str(file))
  mesh=next(e for e in child(root,b'Objects').elems if e.id==b'Geometry' and e.props[-1]==b'Mesh' and e.props[1].split(b'\x00\x01')[0]==b'Genesis8Female')
  return list(child(mesh,b'Vertices').props[0]),list(child(mesh,b'PolygonVertexIndex').props[0])
 base,topology=body(opt.reference);posed,posed_topology=body(opt.fbx)
 if topology!=posed_topology or len(base)!=len(posed):raise ValueError('Heel reference body topology does not match the approved body.')
 calibrated={};calibration_report=None
 if opt.calibration:
  calibration=json.loads(opt.calibration.read_text(encoding='utf-8-sig'))
  if calibration['schemaVersion']!=1 or calibration['referenceSHA256']!=hashlib.sha256(opt.reference.read_bytes()).hexdigest():raise ValueError('Tiptoe calibration does not match the approved reference.')
  if not opt.duf:raise ValueError('A saved DUF is required to identify the supplied tiptoe pose.')
  data=opt.duf.read_bytes();scene=json.loads(gzip.decompress(data) if data[:2]==b'\x1f\x8b' else data)
  controls={m['id']:float(m.get('channel',{}).get('current_value',0)) for m in scene.get('scene',{}).get('modifiers',[]) if m.get('parent')=='#Genesis8Female'}
  left=controls.get('pCTRLlFootTipToes',0);right=controls.get('pCTRLrFootTipToes',0)
  if abs(left-right)>1e-8 or not calibration['minimumControl']<=left<=calibration['maximumControl']:raise ValueError('Tiptoe calibration only supports paired equal controls in its calibrated range; inspect this pose.')
  for entry in calibration['entries']:
   i=entry['index']
   if not 0<=i<len(base)//3 or not 40<=base[i*3+1]<=60:raise ValueError('Calibration entry is outside the approved lower-leg region.')
   predicted=[v*left for v in entry['deltaPerUnit']]
   observed=[(posed[i*3+a]-base[i*3+a])*.01 for a in range(3)]
   residual=sum((a-b)**2 for a,b in zip(predicted,observed))**.5
   if residual>calibration['maximumResidualMeters']:raise ValueError(f'Lower-leg point {i} differs from the known tiptoe correction by {residual*1000:.6f} mm; inspect the supplied shape.')
   calibrated[i]=residual
  calibration_report={'control':left,'classifiedPoints':len(calibrated),'maximumResidualMeters':max(calibrated.values(),default=0),'calibrationSHA256':hashlib.sha256(opt.calibration.read_bytes()).hexdigest()}
 entries=[];regions={'feet':0,'upper':0};maxima={'feet':0.,'upper':0.};unexplained=0.
 for i in range(len(base)//3):
  delta=[(posed[i*3+a]-base[i*3+a])*.01 for a in range(3)]
  magnitude=sum(v*v for v in delta)**.5;region='feet' if base[i*3+1]<40 else 'upper'
  if magnitude>2e-6:regions[region]+=1;maxima[region]=max(maxima[region],magnitude)
  if region=='upper' and i not in calibrated:unexplained=max(unexplained,magnitude)
  if (region=='feet' or i in calibrated) and magnitude>1e-8:entries.append({'index':i,'delta':{'x':-delta[0],'y':delta[1],'z':delta[2]}})
 if unexplained>.0015:raise ValueError('Unexplained body change above the feet exceeds 1.5 mm; inspect the shape before transferring the heel reference.')
 report={'matchingTopology':True,'sourceSHA256':hashlib.sha256(opt.fbx.read_bytes()).hexdigest(),'referenceSHA256':hashlib.sha256(opt.reference.read_bytes()).hexdigest(),'movedPoints':regions,'maximumMeters':maxima,'entries':entries}
 report['poseCalibration']=calibration_report;report['maximumUnexplainedUpperMeters']=unexplained
 opt.out.parent.mkdir(parents=True,exist_ok=True);opt.out.write_text(json.dumps(report,separators=(',',':'))+'\n',encoding='utf-8')
 print(json.dumps({k:v for k,v in report.items() if k!='entries'}))
if __name__=='__main__':main()
