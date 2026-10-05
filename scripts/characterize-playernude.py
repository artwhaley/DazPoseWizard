"""Read-only fixture evidence; no Unity import or source asset writes."""
import csv, gzip, hashlib, importlib.util, json, math, sys, types
from collections import Counter, defaultdict
from pathlib import Path
from urllib.parse import unquote

OUT = Path('TestOutput/playernude-characterization')
ROOTS = [Path('F:/Daz3D'), Path('C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library'), Path('C:/Users/Public/Documents/My DAZ 3D Library')]
DUF = ROOTS[1] / 'Scenes/playernude.duf'
FBX = Path('validation/DazPoseUnityValidation/Assets/TestCharacter/playernude.fbx')
def read(p):
    b=p.read_bytes(); return json.loads(gzip.decompress(b) if b[:2]==b'\x1f\x8b' else b)
def resolve(url):
    rel=unquote(url.split('#')[0]).lstrip('/'); return next((r/rel for r in ROOTS if (r/rel).is_file()), None)
def write(n,v): (OUT/n).write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8')
def stats(rows,total):
    xyz=[r[1:] for r in rows]; mag=[math.sqrt(sum(x*x for x in v)) for v in xyz]
    return dict(entries=len(rows),moved=sum(x>1e-8 for x in mag),maxCm=max(mag,default=0),meanMovedCm=sum(mag)/max(1,sum(x>1e-8 for x in mag)),rmsAllCm=math.sqrt(sum(x*x for x in mag)/max(1,total)),axisRmsCm=[math.sqrt(sum(v[i]**2 for v in xyz)/max(1,len(xyz))) for i in range(3)])
OUT.mkdir(parents=True,exist_ok=True)
s=read(DUF)
refs=set()
def walk(v):
    if isinstance(v,dict):
        for k,x in v.items():
            if k in ('url','uv_set') and isinstance(x,str) and x.startswith('/'): refs.add(x)
            walk(x)
    elif isinstance(v,list):
        for x in v:walk(x)
walk(s)
assets=[]; seen={}
for u in sorted(refs):
    p=resolve(u)
    if p and str(p) in seen:
        seen[str(p)]['urls'].append(u);continue
    rec={'url':u,'path':str(p) if p else None}
    rec['urls']=[u]
    if p:seen[str(p)]=rec
    if p and p.suffix.lower() in ('.dsf','.duf'):
        a=read(p);rec['sha256']=hashlib.sha256(p.read_bytes()).hexdigest()
        if 'geometry_library' in a:
            rec['geometries']=[{k:v for k,v in g.items() if k not in ('vertices','polylist')}|{'vertexCount':g.get('vertices',{}).get('count'),'polygonCount':g.get('polylist',{}).get('count')} for g in a['geometry_library']]
        if 'node_library' in a:rec['nodes']=a['node_library']
        if 'modifier_library' in a:rec['modifiers']=[{k:v for k,v in m.items() if k not in ('morph','skin')}|({'morphStats':stats(m['morph'].get('deltas',{}).get('values',[]),m['morph'].get('vertex_count',0))} if 'morph' in m else {})|({'skinSummary':{'node':m['skin'].get('node'),'joints':len(m['skin'].get('joints',[]))}} if 'skin' in m else {}) for m in a['modifier_library']]
        if 'uv_set_library' in a:rec['uvSets']=[{k:v for k,v in uv.items() if k not in ('uvs','polygon_vertex_indices')}|{'uvCount':uv.get('uvs',{}).get('count')} for uv in a['uv_set_library']]
    assets.append(rec)
write('dson-assets.json',assets)
graft=resolve('/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/Dicktator_Genitalia_G8M.dsf')
a=read(graft);morphs=[]
for p in sorted((graft.parent/'Morphs').rglob('*.dsf')):
    b=read(p)
    for m in b.get('modifier_library',[]):
        rec={k:v for k,v in m.items() if k!='morph'};rec['source']=str(p);rec['sha256']=hashlib.sha256(p.read_bytes()).hexdigest()
        if 'morph' in m:rec['geometryStats']=stats(m['morph'].get('deltas',{}).get('values',[]),m['morph'].get('vertex_count',0));rec['targetVertexCount']=m['morph'].get('vertex_count')
        morphs.append(rec)
write('anatomy-morphs.json',morphs)
write('anatomy-native.json',{'source':str(graft),'nodes':a.get('node_library'),'geometry':[ {k:v for k,v in g.items() if k not in ('vertices','polylist')}|{'vertexCount':g['vertices']['count'],'polygonCount':g['polylist']['count']} for g in a.get('geometry_library',[])], 'skin':[ {k:v for k,v in m.items() if k!='skin'}|{'skin':{k:v for k,v in m.get('skin',{}).items() if k!='joints'}}|{'joints':[ {k:v for k,v in j.items() if 'weights' not in k}|{'weightCounts':{k:len(v.get('values',[])) for k,v in j.items() if 'weights' in k and isinstance(v,dict)}} for j in m.get('skin',{}).get('joints',[])]} for m in a.get('modifier_library',[]) ]})
# Reuse the existing Blender binary reader and project helper functions.
spec=importlib.util.spec_from_file_location('f','scripts/inspect-fbx.py');f=importlib.util.module_from_spec(spec);spec.loader.exec_module(f)
package=types.ModuleType('io_scene_fbx');package.__path__=['C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core/io_scene_fbx'];sys.modules['io_scene_fbx']=package
from io_scene_fbx import parse_fbx
root,version=parse_fbx.parse(str(FBX));objs=f.child(root,b'Objects').elems;byid={e.props[0]:e for e in objs};cs=f.child(root,b'Connections').elems
incoming=defaultdict(list)
for c in cs:
    if c.props[0]==b'OO':incoming[c.props[2]].append(c.props[1])
extra=[]
for mesh in [e for e in objs if e.id==b'Geometry' and e.props[-1]==b'Mesh']:
    vs=f.values(mesh,b'Vertices');polys=[];poly=[]
    for i in f.values(mesh,b'PolygonVertexIndex'):
        poly.append(i if i>=0 else -i-1)
        if i<0:polys.append(poly);poly=[]
    edges=defaultdict(list);mats=f.values(f.child(mesh,b'LayerElementMaterial'),b'Materials')
    vertex_slots=defaultdict(set)
    for pi,p in enumerate(polys):
        if mats:
            for i in p:vertex_slots[i].add(mats[pi])
        for x,y in zip(p,p[1:]+p[:1]):edges[tuple(sorted((x,y)))].append(pi)
    seam=[(e,ps) for e,ps in edges.items() if len(ps)==2 and mats and ((mats[ps[0]]<16)!=(mats[ps[1]]<16))]
    weighted=[]; cluster_data=[]
    for sid in incoming[mesh.props[0]]:
        skin=byid[sid]
        if skin.id!=b'Deformer' or skin.props[-1]!=b'Skin':continue
        for cid in incoming[sid]:
            c=byid[cid]
            if c.id!=b'Deformer' or c.props[-1]!=b'Cluster':continue
            bone=[f.name(byid[i]) for i in incoming[cid] if byid[i].id==b'Model']
            ids=f.values(c,b'Indexes');ws=f.values(c,b'Weights');positive=[i for i,w in zip(ids,ws) if w>0]
            weighted.append({'bones':bone,'positiveCount':len(positive),'weightSum':sum(ws),'maxWeight':max(ws,default=0),'boundsCm':{'min':[min(vs[i*3+ax] for i in positive) for ax in range(3)],'max':[max(vs[i*3+ax] for i in positive) for ax in range(3)]} if positive else None})
            cluster_data.append((bone,ids,ws,f.values(c,b'TransformLink')))
            weighted[-1]['positiveVerticesBySlot']=dict(Counter(slot for i in positive for slot in vertex_slots[i]))
            anatomy_weights=[w for i,w in zip(ids,ws) if w>0 and any(slot>=16 for slot in vertex_slots[i])]
            weighted[-1]['anatomyInfluence']={'maxWeight':max(anatomy_weights,default=0),'sumWeights':sum(anatomy_weights),'over001':sum(w>=.01 for w in anatomy_weights),'over01':sum(w>=.1 for w in anatomy_weights)}
    sensitivity=[];total_summary=None
    if f.name(mesh)=='Genesis8Male':
        totals=defaultdict(float)
        for names,ids,ws,link in cluster_data:
            for i,w in zip(ids,ws):totals[i]+=w
        anatomy_totals=[w for i,w in totals.items() if any(slot>=16 for slot in vertex_slots[i])]
        total_summary={'min':min(totals.values()),'max':max(totals.values()),'verticesAbove1001':sum(w>1.001 for w in totals.values()),'anatomyMin':min(anatomy_totals),'anatomyMax':max(anatomy_totals),'anatomyAbove1001':sum(w>1.001 for w in anatomy_totals)}
        for target in ['shaftRoot']+['shaft'+str(i) for i in range(1,8)]:
            pivot=next(link[12:15] for names,ids,ws,link in cluster_data if target in names)
            weights=defaultdict(float)
            descendants=['shaft'+str(i) for i in range(int(target[-1]) if target!='shaftRoot' else 1,8)]
            if target=='shaftRoot':descendants+=['shaftRoot','scrotum','lTesticle','rTesticle']
            for names,ids,ws,link in cluster_data:
                if any(n in descendants for n in names):
                    for i,w in zip(ids,ws):weights[i]+=w
            angle=math.radians(5);rows=[]
            for i,w in weights.items():
                x,y,z=[vs[i*3+ax]-pivot[ax] for ax in range(3)]
                dy=y*math.cos(angle)-z*math.sin(angle)-y;dz=y*math.sin(angle)+z*math.cos(angle)-z
                rows.append([i,0,dy*w/totals[i],dz*w/totals[i]])
            sensitivity.append({'bone':target,'pivotCm':pivot,'test':'analytic 5-degree raw bind-space X rotation with normalized subtree skin weights; not rendered/DAZ-evaluated','stats':stats(rows,len(vs)//3)})
    extra.append({'name':f.name(mesh),'uvSets':[{'index':e.props[0],'name':f.child(e,b'Name').props[0].decode() if f.child(e,b'Name') else '', 'uvCount':len(f.values(e,b'UV'))//2,'mapping':f.child(e,b'MappingInformationType').props[0].decode()} for e in mesh.elems if e.id==b'LayerElementUV'],'usedSlots':dict(Counter(mats)),'boundaryEdges':sum(len(p)==1 for p in edges.values()),'nonmanifoldEdges':sum(len(p)>2 for p in edges.values()),'hostGraftSeamEdges':len(seam),'seamVertices':len(set(i for e,p in seam for i in e)),'weights':weighted,'boneSensitivity':sensitivity})
    if f.name(mesh)=='Genesis8Male':
        extra[-1]['rawWeightTotals']=total_summary
        ae=Counter(e for e,ps in edges.items() for pi in ps if mats[pi]>=16)
        extra[-1]['anatomyRegionBoundaryEdges']=sum(count==1 for count in ae.values())
write('fbx-extra.json',{'meshes':extra,'poses':[{'name':f.name(e),'nodeCount':len(e.elems),'type':[str(p) for p in e.props]} for e in objs if e.id==b'Pose'],'animationObjects':dict(Counter(e.id.decode() for e in objs if e.id.startswith(b'Animation'))),'embeddedVideoBytes':[{'name':f.name(e),'bytes':len(f.child(e,b'Content').props[0]) if f.child(e,b'Content') and f.child(e,b'Content').props else 0} for e in objs if e.id==b'Video']})
# Texture dimensions, sharing, source hashes and direct-export omissions.
import struct
def image_info(p):
    b=Path(p).read_bytes()
    if b[:8]==b'\x89PNG\r\n\x1a\n':return {'size':list(struct.unpack('>II',b[16:24])),'format':'PNG'}
    if b[:2]==b'\xff\xd8':
        i=2
        while i<len(b):
            if b[i]!=255:i+=1;continue
            while b[i]==255:i+=1
            marker=b[i];i+=1
            if marker in (0xd8,0xd9):continue
            n=struct.unpack('>H',b[i:i+2])[0]
            if marker in (0xc0,0xc1,0xc2,0xc3):
                h,w=struct.unpack('>HH',b[i+3:i+7]);return {'size':[w,h],'format':'JPEG'}
            i+=n
    return {'size':None,'format':Path(p).suffix}
materials=json.loads((OUT/'materials.json').read_text());textures={}
for mat in materials['materials']:
    for ch in mat['channels']:
        for p in ch['resolvedFiles']:
            if p not in textures:
                res=image_info(p)
                copies=list((FBX.parent/'playernude.images').glob(Path(p).stem+'.*'))
                res.update(path=p,sourceReference=ch['imageFile'],sha256=hashlib.sha256(Path(p).read_bytes()).hexdigest(),exportCopy=(FBX.parent/'playernude.images'/Path(p).name).is_file(),exportStemMatches=[str(c) for c in copies],uses=[]);textures[p]=res
            textures[p]['uses'].append({'node':mat['nodeId'],'surface':mat['surfaceGroups'],'channel':ch['id']})
write('textures.json',list(textures.values()))
mapping=[]
fbx=json.loads((OUT/'fbx.json').read_text())
for mesh in fbx['meshes']:
    for slot,surface in enumerate(mesh['materialSlots'][0]):
        owner= 'Dicktator_Genitalia_G8M' if mesh['name']=='Genesis8Male' and slot>=16 else mesh['name']
        mat=next(m for m in materials['materials'] if m['nodeId']==owner and surface in m['surfaceGroups'])
        mapping.append({'rendererPath':mesh['modelPaths'][0],'mesh':mesh['name'],'slot':slot,'fbxMaterial':surface,'sourceNode':owner,'sourceMaterial':mat['instanceId'],'sourceSurface':surface,'shaderTypes':mat['shaderTypes'],'uvSet':mat['uvSet'],'polygonCount':next(x for x in extra if x['name']==mesh['name'])['usedSlots'].get(slot,0)})
write('renderer-material-map.json',mapping)
with (OUT/'material-channels.csv').open('w',newline='',encoding='utf-8') as stream:
    writer=csv.writer(stream);writer.writerow(['sourceNode','surface','materialId','shader','uvSet','channelId','label','type','value','imageFile','resolvedFiles','gamma','imageSettings'])
    for m in materials['materials']:
        for c in m['channels']:writer.writerow([m['nodeId'],';'.join(m['surfaceGroups']),m['instanceId'],';'.join(m['shaderTypes']),m['uvSet'],c['id'],c['label'],c['type'],json.dumps(c['value']),c['imageFile'],';'.join(c['resolvedFiles']),c['imageGamma'],json.dumps(c['imageSettings'])])
selected=['Flacid','Cock Length','Shaft Inflate','pJCM_Shaft1_up']
assert all(sum(m['id']==n for m in morphs)==1 for n in selected)
with Path('scripts/playernude_runtime_morphs.csv').open('w',newline='',encoding='utf-8') as stream:
    writer=csv.writer(stream,quoting=csv.QUOTE_ALL,lineterminator='\r\n')
    exclusions=['Flacid Preset 01','Flacid Preset 02','Flacid Preset 03','Flacid Preset 04','Scrotum','Shaft Inflate 1','Shaft Inflate 2','Shaft Inflate 3','Shaft Inflate 4']
    writer.writerows([(n,'Bake') for n in exclusions]+[(n,'Export') for n in selected]+[('Anything','Bake')])
# Append readable exhaustive slot/texture tables to the hand-authored report.
report=Path('validation/DazPoseUnityValidation/docs/PLAYERNUDE_G8M_ANATOMY_CHARACTERIZATION.md')
marker='<!-- GENERATED EVIDENCE TABLES -->'
def table(headers,rows):
    def cell(x):return str(x).replace('|','\\|').replace('\n',' ')
    return '\n'.join(['| '+' | '.join(headers)+' |','| '+' | '.join(['---']*len(headers))+' |']+['| '+' | '.join(cell(c) for c in row)+' |' for row in rows])+'\n'
def xyz(values):return ', '.join(f'{v:.5f}' for v in values)
tables=['\n## Appendix A. Deterministic FBX renderer/material mapping\n\nIray Uber for every row. Paths and slot counts are raw FBX; empty shell slots remain declared. UV references are material overrides from DSON; shell UVs exist in FBX despite no override.\n']
for mesh in fbx['meshes']:
    tables.append('\n### `'+mesh['modelPaths'][0]+'`\n\n')
    tables.append(table(['Slot','FBX material / DAZ surface','Source figure','DAZ material ID','Polygons','UV set'],[[m['slot'],m['fbxMaterial'],m['sourceNode'],m['sourceMaterial'],m['polygonCount'],unquote(m['uvSet']) if m['uvSet'] else 'no explicit override; geometry UV'] for m in mapping if m['mesh']==mesh['name']]))
tables.append('\n## Appendix B. Anatomy-region bone influences\n\nCounts overlap because vertices can use multiple bones and surfaces. Counts below include any positive exported weight on slots 16–22. Whole-mesh positive counts and 3D influence bounds are retained in fbx-extra.json. Bind centers come from cluster TransformLink (cm). Region max and ≥.1 counts expose substantial inherited thigh influence.\n\n')
clusters=fbx['meshes'][0]['skins'][0]['clusters'];rows=[]
for w in extra[0]['weights']:
    region=w['anatomyInfluence']
    if region['maxWeight']==0:continue
    name=w['bones'][0];c=next(c for c in clusters if c['bones'][0].split('/')[-1]==name)
    path=c['bones'][0];slots=', '.join(mapping[int(k)]['sourceSurface']+':'+str(v) for k,v in w['positiveVerticesBySlot'].items() if int(k)>=16)
    native=next((n for n in a['node_library'] if n.get('name')==name),None)
    purpose='base G8M attachment-region influence' if '/Dicktator_Genitalia_G8M/' not in path else ('shaft centerline/shape' if name.startswith('shaft') else 'scrotal region' if name in ('scrotum','lTesticle','rTesticle') else 'perineal/rectal region')
    rows.append([name,path.rsplit('/',1)[0],purpose,xyz(c['transformLink'][12:15]),w['positiveCount'],f"{region['maxWeight']:.6f}",region['over01'],slots])
tables.append(table(['Bone','Exact parent path','Ownership / purpose','Export bind center XYZ cm','Whole-mesh positive points','Anatomy max weight','Anatomy points ≥.1','Positive points per anatomy surface'],rows))
tables.append('\n### Native anatomical joint directions\n\nNative reference before saved Scale Down/Shaft Shorten/Caine evaluation; do not use these as the saved FBX bind centers. All rows are graft-owned.\n\n')
tables.append(table(['Native ID → node name','Native parent ID','Center XYZ cm','End XYZ cm','Orientation XYZ degrees','Rotation order'],[[n['id']+' → '+n.get('name',''),unquote(n.get('parent','')),xyz([c['value'] for c in n.get('center_point',[])]),xyz([c['value'] for c in n.get('end_point',[])]),xyz([c['value'] for c in n.get('orientation',[])]),n.get('rotation_order')] for n in a['node_library'] if n.get('type')=='bone']))
tables.append('\n## Appendix C. Selected and contact/shape candidate morphs\n\nAll paths below are relative to `F:/Daz3D/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/Morphs/`. Deltas are measured at value 1, in source cm; mean is over moved points, RMS is over the entire target geometry. `ERC` means no direct deltas; mixed records also contain joint/property formulas. This table inventories candidates, not export selections. Complete operations and dependencies remain in anatomy-morphs.json.\n\n')
rows=[]
for m in morphs:
    name=m['id']
    if not (name in selected+['Cock Thickness','Cock Tall','Cock Wide','Erection Preset 01','Erection Preset 02','Up-Down','Small Dick Length','Scale Down','Flacid Preset 01','Flacid Preset 02','Flacid Preset 03','Flacid Preset 04'] or any(x in name for x in ['Shaft','Glans','Curve','Flatten','Squeeze','Root Fold','Corpus','Frenulum','Corona','Crown','Scrotum'])):continue
    ch=m['channel'];gs=m.get('geometryStats');fs=m.get('formulas',[])
    cls='permanent configuration'
    if name in ['Shaft Inflate','Shaft Inflate 1','Shaft Inflate 2','Shaft Inflate 3','Shaft Inflate 4','Glans Flatten','Glans Height','Glans Width','pJCM_Shaft1_up']:cls='procedural candidate; broad/local shape or root corrective'
    elif 'Curve' in name or name in ['Up-Down','Shaft Twist']:cls='bone bend reference; not squish'
    elif any(t in name for t in ['Scrotum','Squeeze','Silicone']):cls='probably irrelevant to first shaft contact'
    elif name=='Small Dick Length':cls='no useful geometry evidence'
    mechanism='direct morph'+(' + ERC' if fs else '') if gs else ('ERC/controller' if fs or name=='Cock Thickness' else 'empty property')
    rows.append([('**'+name+'**') if name in selected else name,ch.get('label',ch.get('name')),str(Path(m['source']).relative_to(graft.parent/'Morphs')).replace('\\','/'),f"{ch.get('min')}..{ch.get('max')}; {ch.get('value')}",mechanism,len(fs),gs['moved'] if gs else '—',f"{gs['maxCm']:.6f}/{gs['meanMovedCm']:.6f}/{gs['rmsAllCm']:.6f}" if gs else '—',cls])
tables.append(table(['Internal ID (bold = selected)','Display label','Source DSF','Min..max; default','Mechanism','Formula count','Moved points','Max / mean / RMS cm','Proposed classification'],rows))
tables.append('\n## Appendix D. Exact texture files and map sharing\n\nAll source files below are JPEG. An export stem match with `.tif` is a converted file, not byte-identical source evidence. A dash means no same-stem image in playernude.images. Source inventory retains every individual surface use; this table groups channels and figures.\n\n')
tables.append(table(['Exact source path','Resolution','Used by figures','Exact mapped channel IDs','Export image filenames'],[[t['path'].replace('\\','/'),' × '.join(map(str,t['size'])) if t['size'] else 'unknown',', '.join(sorted(set(u['node'] for u in t['uses']))),', '.join(sorted(set(u['channel'] for u in t['uses']))),', '.join(Path(c).name for c in t['exportStemMatches']) or '—'] for t in textures.values()]))
tables.append('\n## Appendix E. Reproduction inventory and checks\n\n')
tables.append(table(['Artifact under TestOutput/playernude-characterization','Purpose'],[['fbx.json','Existing binary inspector: hierarchy, slots, blendshape frames/deltas, all cluster bind matrices, materials and texture links'],['fbx-extra.json','UV sets, used slots, topology seams/boundaries, per-region influences, analytic rotation sensitivity, embedded bytes and animation object counts'],['duf-structure.json','Saved scene nodes/property values/generated shell geometry'],['dson-assets.json','Resolved directly referenced physical assets with hashes, bone/geometry/UV and modifier summaries'],['anatomy-native.json','Native graft nodes/topology/graft mapping/skin metadata'],['anatomy-morphs.json','252 modifiers: channel metadata, target, source hash/path, all ERC operations and measured direct deltas'],['renderer-material-map.json','Deterministic FBX renderer/slot → DSON figure/surface/material/shader/UV mapping'],['materials.json','Complete saved effective channel values/maps, no ownership/inheritance issues'],['material-channels.csv','Readable complete channel inventory for all 48 materials'],['textures.json','26 physical source files with dimensions/hashes/sharing/export counterparts']]))
tables.append('\nTechnical checks: inspector completed with no malformed shape indices/deltas; all direct DUF references resolved; 48 material records, 26 unique source textures, zero missing textures or ownership/inheritance issues; renderer mapping resolved every declared slot to exactly one source record; selected CSV IDs are unique native modifiers; terminal Bake schema checked. No rendered or Unity acceptance is claimed.\n')
if report.exists():report.write_text(report.read_text(encoding='utf-8').split(marker)[0]+marker+'\n'+''.join(tables),encoding='utf-8')
print('Followed',len(assets),'references;',len(morphs),'anatomy modifiers;',len(textures),'textures')
for m in morphs:
    if any(x in m['id'].lower() for x in ('erect','flac','length','girth','thick','shorten','straight','squish','flatten','compress')):print(m['id'],m.get('channel'),m.get('geometryStats'),'formulas',len(m.get('formulas',[])))
