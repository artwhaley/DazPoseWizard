"""Resolve/cache wardrobe evidence and summarize completed Unity checks. No UI or Unity launch."""
import argparse, hashlib, importlib, importlib.util, json, subprocess, sys, types
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]

def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')

def calibration_path(recipe):
    # Supported footwear calibration is a tool default, not an operator memory task.
    return ROOT/(recipe.get('poseCalibration') or 'configs/wardrobe/paired-tiptoe-calibration.json') if recipe.get('shoes') else None

def inspect_source(recipe, project, dest):
    pkg=types.ModuleType('io_scene_fbx')
    pkg.__path__=['C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core/io_scene_fbx']
    sys.modules['io_scene_fbx']=pkg
    tree,_=importlib.import_module('io_scene_fbx.parse_fbx').parse(str(project/recipe['fbx']))
    def child(e,key): return next((c for c in e.elems if c.id==key),None)
    meshes=[]
    for mesh in child(tree,b'Objects').elems:
        if mesh.id!=b'Geometry' or mesh.props[-1]!=b'Mesh': continue
        vertices=child(mesh,b'Vertices'); polygons=child(mesh,b'PolygonVertexIndex')
        meshes.append({'node':mesh.props[1].split(b'\x00\x01')[0].decode(),
                       'points':len(vertices.props[0])//3 if vertices else 0,
                       'polygons':sum(v<0 for v in polygons.props[0]) if polygons else 0})
    errors=[]
    body=next((m for m in meshes if m['node']=='Genesis8Female'),None)
    if not body or not body['points'] or not body['polygons']:
        errors.append('BODY_GEOMETRY_REQUIRED: include the approved Lara body/graft in the direct FBX; a named empty node is insufficient. Retain supplied pose.')
    shoes=recipe.get('shoes')
    if shoes and (shoes.get('articulation') not in ('rigid-pair','skinned') or shoes.get('node') not in {m['node'] for m in meshes if m['points']}):
        errors.append('SHOE_POLICY_REQUIRED: select an existing shoe mesh and classify construction from its preview/product geometry; toe weights do not establish flexible footwear.')
    if not shoes and any(p.get('role')=='footwear' for p in recipe.get('policies',[])):
        errors.append('SHOE_POLICY_REQUIRED: a footwear piece requires shoes.node and an explicit articulation policy.')
    report={'fbx':recipe['fbx'],'meshes':meshes,'body':body,'footwear':shoes,
            'poseCalibration':str(calibration_path(recipe)) if shoes else None,'errors':errors}
    write(dest/'source-contract.json',report)
    if errors: raise ValueError('\n'.join(errors))
    print('SOURCE_CONTRACT_READY:',dest/'source-contract.json')

def prepare(recipe_path, project, force=False, scene_only=False, inspect_fbx=False):
    recipe = read(recipe_path)
    dest = project / recipe['destination']
    command = [sys.executable, str(ROOT/'scripts/prepare-lara-wardrobe.py'), '--project', str(project),
               '--duf', recipe['duf'], '--fbx', recipe['fbx']]
    for root in recipe.get('contentRoots', []): command += ['--content-root', root]
    for name in recipe.get('include', []): command += ['--include', name]
    for mapping in recipe.get('ownership', []): command += ['--map', mapping['from']+'='+mapping['to']]
    if scene_only:
        subprocess.run(command + ['--scene-only', '--out', str(dest/'source-preflight.json')], check=True)
    spec = importlib.util.spec_from_file_location('dson', ROOT/'scripts/inspect-daz-materials.py')
    dson = importlib.util.module_from_spec(spec); spec.loader.exec_module(dson)
    roots = [Path('F:/Daz3D'), Path('C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library')]
    evidence = dson.inspect(Path(recipe['duf']), roots + list(map(Path, recipe.get('contentRoots', []))))
    if evidence['issues'] or evidence['missingTextures']:
        write(dest/'source-preflight.json', evidence)
        raise ValueError('DUF material ownership/textures unresolved; see source-preflight.json')
    scene, _duf_hash = dson.read(Path(recipe['duf']))
    parents = {n['id']:n.get('parent') for n in scene.get('scene', {}).get('nodes', [])}
    nodes = {}
    for material in evidence['materials']:
        node = nodes.setdefault(material['nodeId'], {'id':material['nodeId'],'label':material['nodeLabel'],
                   'assetUrl':material['nodeAssetUrl'],'parent':parents.get(material['nodeId']),
                   'visible':material['sourceVisible'],'surfaces':[]})
        node['surfaces'] += material['surfaceGroups']
    write(dest/'source-summary.json', {'nodes':list(nodes.values()),'materials':evidence['materialCount'],
          'textures':evidence['uniqueTextureCount'],'missingTextures':evidence['missingTextures']})
    if scene_only:
        if inspect_fbx: inspect_source(recipe,project,dest)
        return
    inspect_source(recipe,project,dest)
    textures = sorted({c['resolvedFiles'][0] for m in evidence['materials'] for c in m['channels'] if c['resolvedFiles']})
    inputs = {'recipe': digest(recipe_path), 'fbx': digest(project/recipe['fbx']), 'duf': digest(recipe['duf']),
              'reference': digest(project/recipe['referenceFbx']), 'textures': {p:digest(p) for p in textures},
              'tools': {p.name:digest(p) for p in (ROOT/'scripts').glob('*.py') if p.name in
                        ('prepare-lara-wardrobe.py','prepare-lara-heel-reference.py','inspect-daz-materials.py','inspect-fbx.py','wardrobe-recipe.py')}}
    cache = dest/'preparation-cache.json'
    if calibration_path(recipe):
        inputs['poseCalibration'] = digest(calibration_path(recipe))
    if (not force and cache.exists() and read(cache) == inputs and (dest/'wardrobe-manifest.json').exists() and
            (not recipe.get('shoes') or (dest/'heel-reference.json').exists())):
        print('PREPARATION_CACHED:', recipe['id']); return
    reference_output = dest/('heel-reference.json' if recipe.get('shoes') else 'body-reference.json')
    heel_command = [sys.executable,str(ROOT/'scripts/prepare-lara-heel-reference.py'),
                    '--fbx',str(project/recipe['fbx']),'--reference',str(project/recipe['referenceFbx']),
                    '--out',str(reference_output)]
    if calibration_path(recipe):
        heel_command += ['--duf',recipe['duf'],'--calibration',str(calibration_path(recipe))]
    subprocess.run(heel_command,check=True)
    if not recipe.get('shoes') and read(reference_output)['maximumMeters']['feet'] > .0015:
        raise ValueError('Foot reference changed without a footwear recipe; inspect the supplied pose.')
    subprocess.run(command + ['--out', str(dest/'wardrobe-manifest.json')], check=True)
    write(cache, inputs)
    print('PREPARATION_READY:', recipe['id'])

def summary(recipe_path, project):
    recipe = read(recipe_path); output = project/'TestOutput/wardrobe-import'/recipe['id']
    report = read(output/'import.json')
    # Require each current run's evidence. The wrapper removes stale reports before checks.
    report['checks'] = {key:read(output/folder/'validation.json') for key,folder in
                        [('assets','assets'),('render','render'),('motion','motion')]}
    # Reimport fixtures restore artist tuning after Build wrote its provisional report.
    # Use the current validated profile, not the temporary build-time height.
    if 'shoeHeightMeters' in report['checks']['assets']:
        report['shoeHeightMeters'] = report['checks']['assets']['shoeHeightMeters']
    report['status'] = 'technical-checks-passed-awaiting-artistic-review'
    write(output/'report.json', report)
    lines = [f"# {recipe['id']} import", '', report['status'], '',
             f"Pieces: {len(report['pieces'])}; character channels: {report['characterChannels']}; shoe height: {report['shoeHeightMeters']:.5f} m.",
             f"Outfit asset: {report['definition']}", f"Review scene: {report['reviewScene']}", '',
             'Assets, walking/culling and dissolve/restore passed. Inspect render/front.png, render/back.png, render/heels.png and motion/taa-walking.png.', '', 'Review items:']
    lines += ['- '+item for item in report.get('warnings', [])] or ['- Visual fit, sheen and coverage.']
    (output/'report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print('IMPORT_CHECKS_PASSED:', output/'report.md')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['prepare','preflight','inspect','summary'])
    parser.add_argument('--recipe',type=Path,required=True);parser.add_argument('--project',type=Path,required=True)
    parser.add_argument('--force',action='store_true');options=parser.parse_args()
    if options.action=='summary': summary(options.recipe,options.project)
    else: prepare(options.recipe,options.project,options.force,options.action in ('preflight','inspect'),options.action=='inspect')
if __name__=='__main__':main()
