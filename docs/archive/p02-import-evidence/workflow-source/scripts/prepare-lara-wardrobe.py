"""Prepare saved DUF + direct FBX wardrobe inputs; --scene-only runs preflight.

Operator instructions: validation/DazPoseUnityValidation/docs/LaraWardrobeImportPlaybook.md
"""
import argparse,hashlib,importlib,importlib.util,json,sys,types
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT/'validation/DazPoseUnityValidation'
def module(name,file):
 s=importlib.util.spec_from_file_location(name,ROOT/'scripts'/file);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def main():
 parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('--fbx',type=Path,default=Path('Assets/TestCharacter/larafirstoutfit.fbx'))
 parser.add_argument('--duf',type=Path,default=Path('C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library/Scenes/larafirstoutfit.duf'))
 parser.add_argument('--out',type=Path,default=PROJECT/'Assets/TestData/LaraCandidate/wardrobe-manifest.json')
 parser.add_argument('--content-root',type=Path,action='append',default=[Path('F:/Daz3D'),Path('C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library')])
 parser.add_argument('--include',action='append',help='Exact FBX geometry name; default discovers visible followers.')
 parser.add_argument('--map',action='append',default=[],help='Explicit FBX geometry name=DUF node id mapping.')
 parser.add_argument('--scene-only',action='store_true',help='Preflight materials, shells, transforms and modifiers before geometry export.')
 parser.add_argument('--project',type=Path,default=PROJECT,help='Unity project containing the FBX (isolated import project supported).')
 options=parser.parse_args()
 api=module('fbx','inspect-fbx.py');dson=module('dson','inspect-daz-materials.py')
 duf=options.duf;scene,digest=dson.read(duf);materials=dson.inspect(duf,options.content_root)
 if materials['issues'] or materials['missingTextures']:raise ValueError(f"Material preflight failed: {materials['issues']}; missing {materials['missingTextures']}")
 evidence={'schemaVersion':2,'duf':str(duf.resolve()),'dufSHA256':digest,'sceneOnly':options.scene_only,'sourceNodes':scene.get('scene',{}).get('nodes',[]),'sourceNodeLibrary':scene.get('node_library',[]),'sourceModifiers':scene.get('scene',{}).get('modifiers',[]),'materialEvidence':materials}
 options.out.parent.mkdir(parents=True,exist_ok=True)
 if options.scene_only:
  options.out.write_text(json.dumps(evidence,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8')
  print('DUF preflight:',materials['materialCount'],'materials;',materials['uniqueTextureCount'],'textures; saved',options.out);return
 pkg=types.ModuleType('io_scene_fbx');pkg.__path__=['C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core/io_scene_fbx'];sys.modules['io_scene_fbx']=pkg
 path=options.fbx if options.fbx.is_absolute() else options.project/options.fbx
 asset=path.resolve().relative_to(options.project.resolve()).as_posix()
 tree,_=importlib.import_module('io_scene_fbx.parse_fbx').parse(str(path))
 objects=api.child(tree,b'Objects').elems;byid={o.props[0]:o for o in objects};incoming={}
 for c in api.child(tree,b'Connections').elems:
  if c.id==b'C' and c.props[0]==b'OO':incoming.setdefault(c.props[2],[]).append(c.props[1])
 def linked(id,kind,subtype=None):return [byid[i] for i in incoming.get(id,[]) if byid[i].id==kind and (subtype is None or byid[i].props[-1]==subtype)]
 nodes={n['id']:n for n in evidence['sourceNodes']};mapping=dict(item.split('=',1) for item in options.map);parts=[];selected=set()
 (options.out.parent/'geometry-inventory.json').write_text(json.dumps({'fbxGeometry':[api.name(o) for o in objects if o.id==b'Geometry' and o.props[-1]==b'Mesh']},indent=2)+'\n',encoding='utf-8')
 for mesh in [o for o in objects if o.id==b'Geometry' and o.props[-1]==b'Mesh']:
  node=api.name(mesh);source=mapping.get(node,node);records=[m for m in materials['materials'] if m['nodeId']==source]
  if options.include and node not in options.include:continue
  if not options.include and (source=='Genesis8Female' or 'SH_G8FemGen' in source or records and records[0]['sourceVisible'] is False):continue
  if not records:raise ValueError(f'No exact DUF geometry owner for {node!r}; inspect then use --map.')
  info=nodes[source];description=(source+' '+info.get('label','')+' '+info.get('url','')).lower()
  kind='hair' if 'hair' in description else 'footwear' if any(w in description for w in ('shoe','boot','heel','sandal','pump','sneaker')) else 'garment'
  key=node
  vertices=api.values(mesh,b'Vertices');frames=[]
  for blend in linked(mesh.props[0],b'Deformer',b'BlendShape'):
   for channel in linked(blend.props[0],b'Deformer',b'BlendShapeChannel'):
    shapes=linked(channel.props[0],b'Geometry',b'Shape')
    if len(shapes)!=1:raise ValueError(f'Unsupported multi-frame channel: {node}/{api.name(channel)}')
    shape=shapes[0];indices=api.values(shape,b'Indexes');deltas=api.values(shape,b'Vertices')
    entries=[{'index':i,'delta':{'x':-deltas[j*3]*.01,'y':deltas[j*3+1]*.01,'z':deltas[j*3+2]*.01}} for j,i in enumerate(indices) if sum(x*x for x in deltas[j*3:j*3+3])>1e-12]
    if entries:frames.append({'name':api.name(channel),'entries':entries})
  model=next(o for o in objects if o.id==b'Model' and mesh.props[0] in incoming.get(o.props[0],[]))
  surfaces=[]
  for slot,material in enumerate(linked(model.props[0],b'Material')):
   name=api.name(material);match=[m for m in records if m['surfaceGroups']==[name]]
   if len(match)!=1:raise ValueError(f'Ambiguous/missing surface owner: {source}/{name}')
   m=match[0];props=[]
   for c in m['channels']:
    v=c['value'];props.append({'name':c['name'],'number':float(v) if isinstance(v,(int,float,bool)) else 0,'kind':'color' if isinstance(v,list) and len(v)==3 else 'string' if isinstance(v,str) else 'number','color':dict(zip('rgba',v+[1])) if isinstance(v,list) and len(v)==3 else {},'text':v if isinstance(v,str) else '', 'texture':c['resolvedFiles'][0] if c['resolvedFiles'] else ''})
   surfaces.append({'slot':slot,'node':node,'surface':name,'iray':'studio/material/uber_iray' in m['shaderTypes'],'assetType':'Follower/Hair' if kind=='hair' else 'Follower/Wardrobe','properties':props})
  opaque=True
  for r in records:
   for c in r['channels']:
    name=c['name'].lower();v=c['value']
    if name in ('cutout opacity','opacity strength') and (c['imageFile'] or isinstance(v,(int,float)) and v<1):opaque=False
    if 'refraction weight' in name and (c['imageFile'] or isinstance(v,(int,float)) and v>0):opaque=False
  parts.append({'node':node,'sourceNode':source,'mesh':key,'kind':kind,'shell':any(e.get('type')=='studio/node/shell' for e in info.get('extra',[])),'coveragePolicy':'opaque-candidate' if opaque else 'review-transparency','points':[{'index':i,'position':{'x':-vertices[i*3]*.01,'y':vertices[i*3+1]*.01,'z':vertices[i*3+2]*.01}} for i in range(len(vertices)//3)],'frames':frames,'materials':surfaces})
  selected.add(node)
  print(node,'points',len(vertices)//3,'nonempty frames',len(frames),'materials',len(surfaces))
 if options.include and set(options.include)!=selected:raise ValueError(f'Selected geometry not found: {set(options.include)-selected}')
 if not parts:raise ValueError('No visible follower geometry found.')
 evidence.update({'asset':asset,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'parts':parts})
 options.out.write_text(json.dumps(evidence,separators=(',',':'),allow_nan=False)+'\n',encoding='utf-8')
 print('Saved',options.out)
if __name__=='__main__':main()
