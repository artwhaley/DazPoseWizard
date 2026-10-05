"""Prepare local, licensed inputs for the Lara candidate Unity builder.

Uses evaluated FBX morphs on the identical closed base, strips the measured
graft-only facial export bias, and preserves the canonical facial channel order.
Run with Blender Python from the repository root. Does not modify source assets.
"""
import argparse
import importlib
import importlib.util
import hashlib
import json
import sys
import types
from pathlib import Path
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'validation/DazPoseUnityValidation'

def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts' / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

def main():
    options = argparse.ArgumentParser(description=__doc__)
    options.add_argument('--duf', type=Path, required=True)
    options.add_argument('--content-root', type=Path, default=Path('F:/Daz3D'))
    options.add_argument('--blender-scripts', default='C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core')
    args = options.parse_args()
    api = module('fbx_evidence', 'inspect-fbx.py')
    dson = module('dson_materials', 'inspect-daz-materials.py')
    package = types.ModuleType('io_scene_fbx')
    package.__path__ = [str(Path(args.blender_scripts) / 'io_scene_fbx')]
    sys.modules['io_scene_fbx'] = package
    parser = importlib.import_module('io_scene_fbx.parse_fbx')
    def load(filename):
        path = PROJECT / 'Assets/TestCharacter' / filename
        tree, _ = parser.parse(str(path))
        objects = api.child(tree, b'Objects').elems
        body = next(x for x in objects if x.id == b'Geometry' and x.props[-1] == b'Mesh' and api.name(x) == 'Genesis8Female')
        shapes = {api.name(x): x for x in objects if x.id == b'Geometry' and x.props[-1] == b'Shape' and api.name(x).startswith('Genesis8Female__')}
        return body, shapes, hashlib.sha256(path.read_bytes()).hexdigest()
    closed, _, closed_hash = load('l.aranudeclosed.fbx')
    first, shapes, first_hash = load('larafirstoutfit.fbx')
    original, canonical, original_hash = load('lara.fbx')
    assert api.values(closed, b'Vertices') == api.values(first, b'Vertices'), 'First outfit and closed geometry differ'
    assert api.values(closed, b'PolygonVertexIndex') == api.values(first, b'PolygonVertexIndex'), 'Topology differs'
    endpoint = json.loads((PROJECT / 'Assets/TestData/GraftPairProbe/endpoint-manifest.json').read_text())
    assert endpoint['closedSHA256'] == closed_hash
    def sparse(shape, field):
        flat = api.values(shape, field)
        return {i: flat[j*3:j*3+3] for j, i in enumerate(api.values(shape, b'Indexes'))} if flat else {}
    base_normals = api.values(api.child(first, b'LayerElementNormal'),b'Normals')
    closed_normals = api.values(api.child(closed, b'LayerElementNormal'),b'Normals')
    assert len(base_normals) == len(api.values(first,b'Vertices'))
    assert max(abs(a-b) for a,b in zip(base_normals,closed_normals)) < 1e-7, 'Closed/first base normals differ'
    def normal_deltas(shape):
        # FBX Shape.Normals stores target directions, not the Unity frame deltas.
        return {i: [normal[a]-base_normals[i*3+a] for a in range(3)]
                for i,normal in sparse(shape,b'Normals').items()}
    baseline = shapes['Genesis8Female__eCTRLvWJawOnly']
    bias = sparse(baseline, b'Vertices')
    normal_bias = normal_deltas(baseline)
    graft = {p['index'] for p in endpoint['points'] if p['graftOnly']}
    assert set(bias).issubset(graft), 'Expected jaw-only bias is not confined to graft'
    def vector(v, scale):
        return {'x': -v[0]*scale, 'y': v[1]*scale, 'z': v[2]*scale}
    # Original shape order is also the original Unity channel order (verified by builder).
    ordered = list(canonical)
    ordered += [n for n in shapes if 'Nipples' in n and n not in canonical]
    frames = []
    for name in ordered:
        if name not in shapes:
            raise ValueError('First outfit lacks canonical channel ' + name)
        positions = sparse(shapes[name], b'Vertices')
        normals = normal_deltas(shapes[name])
        facial = '__eCTRL' in name or 'FUNtasy Face' in name
        entries = []
        for index in sorted(set(positions) | set(bias) | set(normals) | set(normal_bias)):
            delta = [positions.get(index, [0]*3)[a] - bias.get(index, [0]*3)[a] for a in range(3)]
            normal = [normals.get(index, [0]*3)[a] - normal_bias.get(index, [0]*3)[a] for a in range(3)]
            if facial and index in graft:
                delta = normal = [0]*3
            if any(abs(v) > 1e-8 for v in delta + normal):
                entries.append({'index': index, 'delta': vector(delta, .01), 'normal': vector(normal, 1)})
        frames.append({'name': name, 'facial': facial, 'entries': entries})
    frames.append({'name': 'CapturedOpening', 'facial': False, 'entries': [
        {'index': p['index'], 'delta': p['delta'], 'normal': p['normalDelta']}
        for p in endpoint['points'] if any(abs(p['delta'][a]) > 1e-10 or abs(p['normalDelta'][a]) > 1e-8 for a in 'xyz')]})
    # Map native host indices through retained polygon correspondence for the breast DSF.
    def polygons(mesh):
        result, polygon = [], []
        for i in api.values(mesh, b'PolygonVertexIndex'):
            polygon.append(-i-1 if i < 0 else i)
            if i < 0: result.append(polygon); polygon = []
        return result
    native, _ = dson.read(args.content_root / 'data/SledgeHammer/SH_G8FG/SH_G8FG/SH_G8FemGen_2303.dsf')
    hidden = set(native['geometry_library'][0]['graft']['hidden_polys']['values'])
    host = [p for i, p in enumerate(polygons(original)) if i not in hidden]
    mapping = {}
    for a, b in zip(host, polygons(closed)[:len(host)]):
        assert len(a) == len(b)
        for s, t in zip(a, b):
            assert s not in mapping or mapping[s] == t
            mapping[s] = t
    breast_path = args.content_root / 'data/DAZ 3D/Genesis 8/Female/Morphs/Thorneworks/Lara/PBM Lara Breasts.dsf'
    breast, _ = dson.read(breast_path)
    breast_entries = []
    for i, x, y, z in breast['modifier_library'][0]['morph']['deltas']['values']:
        assert i in mapping, 'Breast delta maps to removed anatomy'
        breast_entries.append({'index': mapping[i], 'delta': vector([x,y,z], .01), 'normal': vector([0,0,0], 1)})
    frames.append({'name': 'LaraBreastsAdjustment', 'facial': False, 'entries': breast_entries})
    materials = dson.inspect(args.duf, [args.content_root])
    saved_scene, _ = dson.read(args.duf)
    breast_controls = [m['channel'] for m in saved_scene['scene'].get('modifiers',[]) if m.get('id') == 'PBM Lara Breasts']
    assert len(breast_controls) == 1, 'Ambiguous saved Lara breast value'
    breast_baked_value = breast_controls[0].get('current_value',breast_controls[0].get('value'))
    assert not materials['issues'] and not materials['missingTextures'], 'Unresolved DUF inputs'
    slots = ['Torso','Face','Lips','Teeth','Ears','Legs','EyeSocket','Mouth','Arms','Pupils','EyeMoisture','Fingernails','Cornea','Irises','Sclera','Toenails','Torso','Fluid','Vagina','Anus']
    bindings = []
    for index, surface in enumerate(slots):
        node = 'Genesis8Female' if index < 16 else 'SH_G8FemGen_2303'
        candidates = [m for m in materials['materials'] if m['nodeId'] == node and m['surfaceGroups'] == [surface]]
        assert len(candidates) == 1, (node, surface, 'ambiguous material')
        m = candidates[0]
        properties = []
        for c in m['channels']:
            v = c['value']
            properties.append({'name': c['name'], 'number': float(v) if isinstance(v, (int,float,bool)) else 0,
                'kind': 'color' if isinstance(v,list) and len(v)==3 else 'string' if isinstance(v,str) else 'number',
                'color': {'r':v[0],'g':v[1],'b':v[2],'a':1} if isinstance(v,list) and len(v)==3 else {},
                'text':v if isinstance(v,str) else '', 'texture': c['resolvedFiles'][0] if c['resolvedFiles'] else ''})
        bindings.append({'slot':index,'node':node,'surface':surface,'iray':'studio/material/uber_iray' in m['shaderTypes'],'properties':properties})
    manifest = {**endpoint, 'firstAsset':'Assets/TestCharacter/larafirstoutfit.fbx','firstSHA256':first_hash,
        'originalSHA256':original_hash,'sourceDUF':str(args.duf),'dufSHA256':materials['sourceSHA256'],
        'frames':frames,'materials':bindings,'breastsBakedValue':breast_baked_value,
        'normalRecipe':'Unity builder derives area-weighted normal changes from evaluated target positions, grouped by welded source point; imported rest normals are preserved',
        'warnings':['Lara breast adjustment is mesh-only; DAZ joint-center/end-point ERC is not applied.',
        'Captured opening is the evaluated endpoint pair; its exact DAZ dial combination is unconfirmed.',
        'Iray/legacy shader conversion approximates DAZ lighting; visual review is required.']}
    destination = PROJECT / 'Assets/TestData/LaraCandidate/manifest.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(manifest,separators=(',',':'))+'\n')
    print(f'Prepared {len(frames)} channels and {len(bindings)} body/graft material bindings; no source files changed.')

if __name__ == '__main__':
    main()
