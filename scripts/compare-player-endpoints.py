"""Read-only offline comparison of artist-authored player endpoints.

Run with Blender 4.5 bundled Python (NumPy) from repository root. Uses the
existing standalone FBX/DSON inspectors; never imports a Unity scene or writes
source assets. Matrix convention: column vectors, transposed FBX serialized
row-vector matrices. Coordinates and measurements in cm, reported errors mm.
"""
import csv, gzip, hashlib, importlib.util, json, math, sys, types
from collections import Counter, defaultdict
from pathlib import Path
from urllib.parse import unquote
import numpy as np

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'TestOutput/player-endpoints'
SCENES=Path('C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library/Scenes')
ASSETS=ROOT/'validation/DazPoseUnityValidation/Assets/TestCharacter'
def module(name,path):
    sp=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(sp);sp.loader.exec_module(m);return m
f=module('fbx_evidence',ROOT/'scripts/inspect-fbx.py')
d=module('dson_evidence',ROOT/'scripts/inspect-daz-materials.py')
pkg=types.ModuleType('io_scene_fbx');pkg.__path__=['C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core/io_scene_fbx'];sys.modules['io_scene_fbx']=pkg
from io_scene_fbx import parse_fbx
def save(name,value):
    (OUT/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def props(e):
    p=f.child(e,b'Properties70');return {x.props[0].decode():[q.decode(errors='replace') if isinstance(q,bytes) else q for q in x.props[4:]] for x in p.elems if x.id==b'P'} if p else {}
def sha_array(a):return hashlib.sha256(np.ascontiguousarray(a).tobytes()).hexdigest()
def stats(delta,points=None):
    n=np.linalg.norm(delta,axis=1)*10;changed=n>1e-4
    return {'count':len(n),'changedAbove00001mm':int(changed.sum()),'maxMm':float(n.max(initial=0)),'meanChangedMm':float(n[changed].mean()) if changed.any() else 0,'meanAllMm':float(n.mean()) if len(n) else 0,'rmsMm':float(np.sqrt(np.mean(n*n))) if len(n) else 0,'aboveMm':{str(t):int((n>t).sum()) for t in [.1,.5,1,2]},'changedBoundsCm':{'min':points[changed].min(axis=0).tolist(),'max':points[changed].max(axis=0).tolist()} if points is not None and changed.any() else None}
def mat(flat):return np.array(flat,dtype=float).reshape(4,4).T
def euler(v,order='XYZ'):
    r=np.eye(3)
    for axis in order:
        a=math.radians(v['XYZ'.index(axis)]);c,s=math.cos(a),math.sin(a)
        q={'X':np.array([[1,0,0],[0,c,-s],[0,s,c]]),'Y':np.array([[c,0,s],[0,1,0],[-s,0,c]]),'Z':np.array([[c,-s,0],[s,c,0],[0,0,1]])}[axis];r=q@r
    return r
def rotation_angle(a,b):return math.degrees(math.acos(float(np.clip((np.trace(b@a.T)-1)/2,-1,1))))
def transform(p):
    v=lambda n,default:p.get(n,default)
    # All measured endpoints have zero rotation/scaling pivots and identity
    # scale. Assert this before using the simplified FBX transform equation.
    for k in ['RotationPivot','ScalingPivot','ScalingOffset']:assert np.max(np.abs(v(k,[0,0,0])))<1e-10,(k,v(k,[0,0,0]))
    assert np.max(np.abs(np.array(v('Lcl Scaling',[1,1,1]))-1))<1e-10,'Nonidentity scale requires full FBX inheritance evaluation'
    order=['XYZ','XZY','YZX','YXZ','ZXY','ZYX'][int(v('RotationOrder',[0])[0])]
    r=euler(v('PreRotation',[0,0,0]))@euler(v('Lcl Rotation',[0,0,0]),order)@euler(v('PostRotation',[0,0,0])).T
    m=np.eye(4);m[:3,:3]=r;m[:3,3]=np.array(v('Lcl Translation',[0,0,0]))+np.array(v('RotationOffset',[0,0,0]));return m
def load(path):
    root,version=parse_fbx.parse(str(path));objects=f.child(root,b'Objects').elems;byid={e.props[0]:e for e in objects};connections=f.child(root,b'Connections').elems
    parents=defaultdict(list);incoming=defaultdict(list)
    for c in connections:
        if c.props[0]==b'OO':parents[c.props[1]].append(c.props[2]);incoming[c.props[2]].append(c.props[1])
    def nodepath(i):
        if i==0:return '__FBXRoot__'
        ps=[p for p in parents[i] if p in byid and byid[p].id==b'Model'];assert len(ps)<=1
        return (nodepath(ps[0])+'/' if ps else '')+f.name(byid[i])
    defaults={}
    for e in f.child(root,b'Definitions').elems:
        if e.props and e.props[0]==b'Model':defaults=props(f.child(e,b'PropertyTemplate'))
    models={nodepath(e.props[0]):{'id':e.props[0],'name':f.name(e),'type':e.props[-1].decode(),'properties':defaults|props(e)} for e in objects if e.id==b'Model'}
    globals={}
    def globalmat(path):
        if path not in globals:
            local=transform(models[path]['properties']);globals[path]=globalmat(path.rsplit('/',1)[0])@local if '/' in path else local
        return globals[path]
    for path in models:globalmat(path)
    meshes={}
    for e in objects:
        if e.id!=b'Geometry' or e.props[-1]!=b'Mesh':continue
        slots=[];owner=[p for p in parents[e.props[0]] if byid[p].id==b'Model'][0]
        slots=[f.name(byid[i]) for i in incoming[owner] if byid[i].id==b'Material']
        points=np.array(f.values(e,b'Vertices')).reshape(-1,3);indices=np.array(f.values(e,b'PolygonVertexIndex'),dtype=np.int64);polys=[];poly=[]
        for i in indices:
            poly.append(int(i) if i>=0 else int(-i-1))
            if i<0:polys.append(poly);poly=[]
        ml=f.child(e,b'LayerElementMaterial');materials=f.values(ml,b'Materials');regions=defaultdict(set)
        for polygon,slot in zip(polys,materials):regions[int(slot)].update(polygon)
        clusters=[]
        for sid in incoming[e.props[0]]:
            skin=byid[sid]
            if skin.id!=b'Deformer' or skin.props[-1]!=b'Skin':continue
            for cid in incoming[sid]:
                cl=byid[cid]
                if cl.id!=b'Deformer' or cl.props[-1]!=b'Cluster':continue
                bone=[nodepath(i) for i in incoming[cid] if byid[i].id==b'Model'];assert len(bone)==1
                clusters.append({'bone':bone[0],'name':f.name(cl),'indices':np.array(f.values(cl,b'Indexes'),dtype=int),'weights':np.array(f.values(cl,b'Weights')),'link':mat(f.values(cl,b'TransformLink')),'transform':mat(f.values(cl,b'Transform')),'mode':f.values(cl,b'Mode')})
        uv=[]
        for u in e.elems:
            if u.id==b'LayerElementUV':uv.append({'index':u.props[0],'properties':[(q.id.decode(),[str(x) for x in q.props]) for q in u.elems if q.id not in (b'UV',b'UVIndex')],'uv':np.array(f.values(u,b'UV')),'indices':np.array(f.values(u,b'UVIndex'),dtype=np.int64)})
        meshes[f.name(e)]={'points':points,'topology':indices,'polys':polys,'slots':slots,'materials':np.array(materials),'regions':regions,'clusters':clusters,'uv':uv,'path':nodepath(owner),'global':globals[nodepath(owner)]}
    poses=[]
    for e in objects:
        if e.id==b'Pose':
            nodes=[]
            for p in e.elems:
                if p.id==b'PoseNode':
                    i=f.child(p,b'Node').props[0];nodes.append({'path':nodepath(i),'matrix':f.values(p,b'Matrix')})
            poses.append({'name':f.name(e),'nodes':nodes})
    constraints=[]
    for e in objects:
        if e.id==b'Constraint':
            links=[]
            for c in connections:
                if c.props[0]==b'PO' and c.props[1]==e.props[0]:links.append({'role':c.props[2].decode(),'path':nodepath(c.props[3])})
                elif c.props[0]==b'OP' and c.props[2]==e.props[0]:links.append({'role':c.props[3].decode(),'path':nodepath(c.props[1])})
            constraints.append({'name':f.name(e),'properties':props(e),'links':links})
    materials=[{'name':f.name(e),'properties':props(e)} for e in objects if e.id==b'Material']
    videos=[]
    for e in objects:
        if e.id==b'Video':
            content=f.child(e,b'Content');filename=f.child(e,b'Filename') or f.child(e,b'FileName');data=content.props[0] if content and content.props else b''
            videos.append({'name':f.name(e),'filename':filename.props[0].decode() if filename and filename.props else None,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
    return {'source':str(path),'version':version,'models':models,'globals':globals,'meshes':meshes,'settings':props(f.child(root,b'GlobalSettings')),'poses':poses,'constraints':constraints,'materials':materials,'videos':videos,'objectCounts':dict(Counter(e.id.decode() for e in objects))}
def differences(a,b,path=''):
    if isinstance(a,dict) and isinstance(b,dict):
        return sum((differences(a.get(k),b.get(k),path+'/'+str(k)) for k in sorted(a.keys()|b.keys())),[])
    if a==b:return []
    return [{'path':path,'flaccid':a,'erect':b}]
def skin(source,target,mesh,limit=None):
    a=source['meshes'][mesh];b=target['meshes'][mesh];assert np.allclose(a['global'],np.eye(4)) and np.allclose(b['global'],np.eye(4))
    positions=a['points'];acc=np.zeros_like(positions);total=np.zeros(len(positions));target_clusters={c['bone']:c for c in b['clusters']}
    pairs=[]
    for c in a['clusters']:
        # Endpoint global bone matrices are validated against link matrices.
        change=target_clusters[c['bone']]['link']@np.linalg.inv(c['link'])
        moved=(change[:3,:3]@positions[c['indices']].T).T+change[:3,3]
        pairs.append((c['indices'],c['weights'],moved))
    if limit:
        weights=np.zeros((len(positions),len(pairs)))
        for j,(ids,ws,moved) in enumerate(pairs):weights[ids,j]=ws
        keep=np.argsort(-weights,axis=1,kind='stable')[:,:limit]
        mask=np.zeros_like(weights,dtype=bool);np.put_along_axis(mask,keep,True,axis=1)
        pairs=[(ids,ws*mask[ids,j],moved) for j,(ids,ws,moved) in enumerate(pairs)]
    for ids,ws,moved in pairs:acc[ids]+=moved*ws[:,None];total[ids]+=ws
    assert np.min(total)>0,'Unweighted point'
    return acc/total[:,None]
def bone_centers(endpoint):
    return [next(c['link'][:3,3] for c in endpoint['meshes']['Genesis8Male']['clusters'] if c['bone'].endswith('/shaft'+str(i))) for i in range(1,8)]
def geometry_tip(endpoint,centers):
    # FBX bone attributes export only display Size, not a tip endpoint. Define
    # reproducible evaluated glans support-plane tip, not an invented bone end.
    m=endpoint['meshes']['Genesis8Male'];ids=list(m['regions'][m['slots'].index('Glans')]);p=m['points'][ids];v=centers[-1]-centers[-2];v/=np.linalg.norm(v)
    distance=(p-centers[-1])@v;end=float(distance.max());return centers[-1]+v*end
def centerline(points):
    ps=np.array(points);d=np.diff(ps,axis=0);length=np.linalg.norm(d,axis=1);directions=d/length[:,None];angles=np.degrees(np.arccos(np.clip(np.sum(directions[1:]*directions[:-1],axis=1),-1,1)));overall=ps[-1]-ps[0]
    return {'pointsCm':ps.tolist(),'baseCm':ps[0].tolist(),'tipCm':ps[-1].tolist(),'lengthCm':float(length.sum()),'segmentLengthsCm':length.tolist(),'segmentDirections':directions.tolist(),'turnAnglesDegrees':angles.tolist(),'maxTurnDegrees':float(angles.max(initial=0)),'overallDirection':(overall/np.linalg.norm(overall)).tolist(),'minSegmentCm':float(length.min())}
def dimensions(endpoint,line):
    mesh=endpoint['meshes']['Genesis8Male'];ids=np.array(sorted(mesh['regions'][mesh['slots'].index('Shaft')]));p=mesh['points'][ids];cs=np.array(line['pointsCm']);segments=np.diff(cs,axis=0);lens=np.linalg.norm(segments,axis=1);arc=np.r_[0,np.cumsum(lens)];all_dist=[];all_arc=[]
    for i,(base,v) in enumerate(zip(cs[:-1],segments)):
        t=np.clip(((p-base)@v)/(v@v),0,1);proj=base+t[:,None]*v;all_dist.append(np.linalg.norm(p-proj,axis=1));all_arc.append(arc[i]+t*lens[i])
    dist=np.array(all_dist).T;which=np.argmin(dist,axis=1);radius=dist[np.arange(len(p)),which];position=np.array(all_arc).T[np.arange(len(p)),which];mask=(position>.2*arc[-1])&(position<.8*arc[-1]);r=radius[mask]
    return {'definition':'mid-shaft surface control points, nearest centerline segment, arc 20–80%; not exact circular radius','sampleCount':len(r),'radiusMedianCm':float(np.median(r)),'radiusP10Cm':float(np.percentile(r,10)),'radiusP90Cm':float(np.percentile(r,90)),'diameterMedianCm':float(2*np.median(r)),'cylinderVolumeProxyCm3':float(math.pi*np.median(r)**2*line['lengthCm'])}
OUT.mkdir(parents=True,exist_ok=True)
files={}
for state in ['playerflacid','playererect']:
    for folder,suffix in [(SCENES,'.duf'),(ASSETS,'.fbx')]:
        matches=[p for p in folder.iterdir() if p.name.lower()==state+suffix];assert len(matches)==1,matches
        p=matches[0];files[state+suffix]={'path':str(p.resolve()),'bytes':p.stat().st_size,'sha256':digest(p),'mtimeUtc':__import__('datetime').datetime.fromtimestamp(p.stat().st_mtime,__import__('datetime').timezone.utc).isoformat()}
    image=ASSETS/(state+'.images');files[state+'.images']={'path':str(image),'files':[{'name':p.name,'bytes':p.stat().st_size,'sha256':digest(p)} for p in sorted(image.iterdir()) if p.is_file() and p.suffix!='.meta']}
save('sources.json',files)
A=load(Path(files['playerflacid.fbx']['path']));B=load(Path(files['playererect.fbx']['path']))
comparison={'sources':files,'structural':{},'meshGeometry':{},'boneDifferences':[],'unexpectedNodeDifferences':[],'dufDifferences':{},'reconstruction':{},'centerlines':{},'dimensions':{}}
checks=comparison['structural'];checks['fbxVersionEqual']=A['version']==B['version'];checks['axisUnitsEqual']=A['settings']==B['settings'];checks['meshNamesEqual']=list(A['meshes'])==list(B['meshes']);checks['skeletonIdentityEqual']=[(p,n['type']) for p,n in A['models'].items()]==[(p,n['type']) for p,n in B['models'].items()];checks['meshChecks']={}
for name,a in A['meshes'].items():
    b=B['meshes'][name];check={'pointCountEqual':len(a['points'])==len(b['points']),'polygonIndicesExactlyEqual':np.array_equal(a['topology'],b['topology']),'slotsExactlyEqual':a['slots']==b['slots'],'polygonMaterialIndicesExactlyEqual':np.array_equal(a['materials'],b['materials']),'uvExactlyEqual':len(a['uv'])==len(b['uv']) and all(x['properties']==y['properties'] and np.array_equal(x['uv'],y['uv']) and np.array_equal(x['indices'],y['indices']) for x,y in zip(a['uv'],b['uv'])),'skinClusterPathsEqual':[c['bone'] for c in a['clusters']]==[c['bone'] for c in b['clusters']]}
    checks['meshChecks'][name]=check
    if not check['pointCountEqual'] or not check['polygonIndicesExactlyEqual']:
        save('comparison.json',comparison);raise SystemExit('STOP: topology mismatch in '+name)
    delta=b['points']-a['points'];comparison['meshGeometry'][name]={'points':len(a['points']),'polygons':len(a['polys']),'polygonIndexSha256':sha_array(a['topology']),'slotOrder':a['slots'],'all':stats(delta,a['points']),'regions':{a['slots'][slot]:stats(delta[sorted(ids)],a['points'][sorted(ids)]) for slot,ids in a['regions'].items()}}
    check['weightsExactlyEqual']=all(np.array_equal(c['indices'],d['indices']) and np.array_equal(c['weights'],d['weights']) and c['mode']==d['mode'] for c,d in zip(a['clusters'],b['clusters']))
    check['bindMatricesExactlyEqual']=all(np.array_equal(c['link'],d['link']) and np.array_equal(c['transform'],d['transform']) for c,d in zip(a['clusters'],b['clusters']))
prefix='Genesis8Male/Dicktator_Genitalia_G8M/'
for p in A['models']:
    a=A['models'][p];b=B['models'][p];la=transform(a['properties']);lb=transform(b['properties']);delta=lb@np.linalg.inv(la)
    rec={'path':p,'name':a['name'],'propertiesFlaccid':a['properties'],'propertiesErect':b['properties'],'localMatrixFlaccid':la.tolist(),'localMatrixErect':lb.tolist(),'deltaLocalMatrix':delta.tolist(),'rotationDifferenceDegrees':rotation_angle(la[:3,:3],lb[:3,:3]),'effectiveLocalTranslationDeltaCm':(lb[:3,3]-la[:3,3]).tolist(),'scaleRatio':[1,1,1],'globalMatrixFlaccid':A['globals'][p].tolist(),'globalMatrixErect':B['globals'][p].tolist(),'rawPropertyDifferences':differences(a['properties'],b['properties'])}
    if p.startswith(prefix):comparison['boneDifferences'].append(rec)
    elif not np.allclose(la,lb,atol=1e-8,rtol=0) or rec['rawPropertyDifferences']:comparison['unexpectedNodeDifferences'].append(rec)
# Validate transform evaluation using the file's bind/link matrices, not only
# a same-endpoint zero-residual experiment (which would be tautological).
comparison['matrixValidation']={}
for label,end in [('flaccid',A),('erect',B)]:
    errs=[];inverseerrs=[]
    for mesh in end['meshes'].values():
        for c in mesh['clusters']:
            errs.append(float(np.max(np.abs(end['globals'][c['bone']]-c['link']))));inverseerrs.append(float(np.max(np.abs(c['transform']@c['link']-np.eye(4)))))
    comparison['matrixValidation'][label]={'maxEvaluatedGlobalVsLinkElementError':max(errs),'maxClusterTransformTimesLinkVsIdentity':max(inverseerrs)}
    assert max(errs)<1e-4,'FBX transform/link evaluation mismatch'
for label,s,t in [('flaccidToErect',A,B),('erectToFlaccid',B,A),('selfFlaccid',A,A),('selfErect',B,B)]:
    actual=t['meshes']['Genesis8Male']['points'];reconstructed=skin(s,t,'Genesis8Male');residual=actual-reconstructed;source=s['meshes']['Genesis8Male'];comparison['reconstruction'][label]={'all':stats(residual,actual),'regions':{source['slots'][slot]:stats(residual[sorted(ids)],actual[sorted(ids)]) for slot,ids in source['regions'].items()}}
comparison['reconstruction']['flaccidToErectTopFour']={'all':stats(B['meshes']['Genesis8Male']['points']-skin(A,B,'Genesis8Male',4))}
body=A['meshes']['Genesis8Male'];weights=np.zeros(len(body['points']));cluster_risk=[]
for c in body['clusters']:
    weights[c['indices']]+=c['weights']
    if c['bone'].rsplit('/',1)[-1] in ['lThighBend','rThighBend','lThighTwist','rThighTwist']:
        anatomy=set().union(*(ids for slot,ids in body['regions'].items() if slot>=16));r=[w for i,w in zip(c['indices'],c['weights']) if i in anatomy and w>0]
        cluster_risk.append({'path':c['bone'],'anatomyPositivePoints':len(r),'anatomyWeightSum':sum(r),'anatomyMaxWeight':max(r,default=0)})
comparison['skinRisk']={'maxTotalWeight':float(weights.max()),'pointsAbove1001':int((weights>1.001).sum()),'thighClusters':cluster_risk}
comparison['bindPoses']={'identityEqual':[(p['name'],[n['path'] for n in p['nodes']]) for p in A['poses']]==[(p['name'],[n['path'] for n in p['nodes']]) for p in B['poses']],'flaccid':A['poses'],'erect':B['poses']}
comparison['constraints']={'identical':A['constraints']==B['constraints'],'flaccid':A['constraints'],'erect':B['constraints']}
comparison['materials']={'propertiesEqual':A['materials']==B['materials'],'videoContentsEqual':[(v['name'],v['bytes'],v['sha256']) for v in A['videos']]==[(v['name'],v['bytes'],v['sha256']) for v in B['videos']],'flaccid':A['materials'],'erect':B['materials'],'flaccidVideos':A['videos'],'erectVideos':B['videos']}
comparison['objectCounts']={'flaccid':A['objectCounts'],'erect':B['objectCounts']}
dufs=[]
for n in ['playerflacid','playererect']:
    scene,_=d.read(Path(files[n+'.duf']['path']));dufs.append(scene)
    material=d.inspect(Path(files[n+'.duf']['path']),[Path('F:/Daz3D')]);save(n+'-materials.json',material)
def scene_state(s):
    sc=s['scene'];return {'nodes':{n['id']:n for n in sc['nodes']},'modifiers':{m['id']:m for m in sc['modifiers']},'animations':{unquote(a['url']):a['keys'] for a in sc.get('animations',[])}}
ds=differences(scene_state(dufs[0]),scene_state(dufs[1]));comparison['dufDifferences']={'all':ds,'anatomy':[r for r in ds if any(x in json.dumps(r) for x in ['Dicktator','shaft','scrotum','Testicle','Flacid','Erection','Scale All','pJCM','hip-2','pelvis-2'])]}
ma=json.loads((OUT/'playerflacid-materials.json').read_text());mb=json.loads((OUT/'playererect-materials.json').read_text());comparison['dufMaterialDifferences']=differences(ma['materials'],mb['materials'])
ca=bone_centers(A);cb=bone_centers(B);ta=geometry_tip(A,ca);tb=geometry_tip(B,cb)
comparison['tipDefinition']='evaluated glans support-plane point along distal shaft6→shaft7 direction; FBX NodeAttribute supplies display Size only, no authored tip endpoint; geometric proxy explicitly used'
for t in [0,.25,.5,.75,1]:
    # Measured local rotations and scales are identity; interpolation reduces
    # to local translations. Hierarchy sums equal global center interpolation.
    centers=[(1-t)*x+t*y for x,y in zip(ca,cb)];tip=(1-t)*ta+t*tb;comparison['centerlines'][str(t)]=centerline(centers+[tip])
comparison['dimensions']={'flaccid':dimensions(A,comparison['centerlines']['0']),'erect':dimensions(B,comparison['centerlines']['1'])}
donor='Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis';main='Genesis8Male/hip/pelvis'
comparison['pelvis']={'relativeFlaccid':(np.linalg.inv(A['globals'][main])@A['globals'][donor]).tolist(),'relativeErect':(np.linalg.inv(B['globals'][main])@B['globals'][donor]).tolist(),'mainGlobalEqual':np.array_equal(A['globals'][main],B['globals'][main]),'donorGlobalEqual':np.array_equal(A['globals'][donor],B['globals'][donor])}
# Compare the known upward JCM without assuming source point indices equal FBX
# point indices. The exported static shell is in native graft vertex order only
# if its polygon stream matches the native stream; prove that first.
native_path=Path('F:/Daz3D/data/Meipex/Dicktator_Genitalia_G8M/Dicktator_Genitalia_G8M_v3/Dicktator_Genitalia_G8M.dsf');native,_=d.read(native_path);g=native['geometry_library'][0];native_polys=[row[2:] for row in g['polylist']['values']]
shell=A['meshes']['Dicktator Shell'];comparison['jcmEvidence']={'nativeShellPolygonOrderEqual':native_polys==shell['polys']}
if native_polys==shell['polys']:
    jpath=native_path.parent/'Morphs/Meipex/Base/pJCM_Shaft1_up.dsf';j,_=d.read(jpath);modifier=j['modifier_library'][0];jdelta=np.zeros_like(shell['points'])
    for row in modifier['morph']['deltas']['values']:jdelta[row[0]]=row[1:]
    shell_delta=B['meshes']['Dicktator Shell']['points']-shell['points'];active=np.linalg.norm(jdelta,axis=1)>0
    comparison['jcmEvidence'].update({'path':str(jpath),'jcmSupport':int(active.sum()),'shellChangedPointsOutsideJcmSupport':int((np.linalg.norm(shell_delta[~active],axis=1)*10>1e-4).sum()),'shellChangeOnJcmSupport':stats(shell_delta[active]),'warning':'pose, scale, shell offset and bulge effects prevent direct raw delta equivalence; no automatic source-to-host corrective mapping asserted'})
# Native corrective magnitude/support bound does not require an unproven index map.
jpath=native_path.parent/'Morphs/Meipex/Base/pJCM_Shaft1_up.dsf';j,_=d.read(jpath);jm=j['modifier_library'][0];jd=jm['morph']['deltas']['values']
comparison['jcmEvidence'].update({'path':str(jpath),'sha256':digest(jpath),'nativeSupportPoints':len(jd),'nativeMaxDeltaAtUnitMm':float(max(np.linalg.norm(row[1:]) for row in jd)*10),'channel':jm['channel'],'formulas':jm.get('formulas',[]),'mappingStatus':'native-to-FBX corrective indices not proven; support/magnitude bound only'})
# Every interpolated segment has an analytic minimum over t in [0,1].
da=np.diff(np.array(comparison['centerlines']['0']['pointsCm']),axis=0);db=np.diff(np.array(comparison['centerlines']['1']['pointsCm']),axis=0);dv=db-da
critical=np.clip(-np.sum(da*dv,axis=1)/np.sum(dv*dv,axis=1),0,1)
comparison['continuousCenterlineMinimum']={'segmentMinimaCm':np.linalg.norm(da+critical[:,None]*dv,axis=1).tolist(),'minimumParameters':critical.tolist(),'minimumCm':float(np.linalg.norm(da+critical[:,None]*dv,axis=1).min())}
save('comparison.json',comparison)
print('Topology safe:',all(c['polygonIndicesExactlyEqual'] and c['pointCountEqual'] for c in checks['meshChecks'].values()))
print('Geometry changes:',{n:v['all'] for n,v in comparison['meshGeometry'].items()})
print('Reconstruction:',{n:v['all'] for n,v in comparison['reconstruction'].items()})
print('Centerline lengths:',{n:v['lengthCm'] for n,v in comparison['centerlines'].items()})
print('Dimensions:',comparison['dimensions'])
