"""Prepare verified G8M shell correspondence and DUF material inputs for Unity.

Read-only against licensed DAZ/FBX sources. Run with Blender's bundled Python.
Unity uses this manifest to copy its already-imported body weights, including
UV seam duplicates, rather than copying or normalizing raw FBX clusters.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import types

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / 'validation/DazPoseUnityValidation'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def unity_point(point):
    return dict(x=-point[0] * .01, y=point[1] * .01, z=point[2] * .01)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--duf', type=Path, default=Path(
        'C:/Users/artwh/OneDrive/Documents/DAZ 3D/Studio/My Library/Scenes/playererect.duf'))
    parser.add_argument('--content-root', type=Path, default=Path('F:/Daz3D'))
    parser.add_argument('--blender-scripts', type=Path, default=Path(
        'C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core'))
    args = parser.parse_args()
    api = module('partner_fbx', ROOT / 'scripts/inspect-fbx.py')
    dson = module('partner_dson', ROOT / 'scripts/inspect-daz-materials.py')
    package = types.ModuleType('io_scene_fbx')
    package.__path__ = [str(args.blender_scripts / 'io_scene_fbx')]
    sys.modules['io_scene_fbx'] = package
    from io_scene_fbx import parse_fbx

    def geometry(path):
        root, _ = parse_fbx.parse(str(path))
        objects = api.child(root, b'Objects').elems
        by_id = {e.props[0]: e for e in objects}
        parents, incoming = defaultdict(list), defaultdict(list)
        for c in api.child(root, b'Connections').elems:
            if c.id == b'C' and c.props[0] == b'OO':
                parents[c.props[1]].append(c.props[2])
                incoming[c.props[2]].append(c.props[1])
        meshes = {}
        for e in objects:
            if e.id != b'Geometry' or e.props[-1] != b'Mesh':
                continue
            owner = next(i for i in parents[e.props[0]] if by_id[i].id == b'Model')
            flat = api.values(e, b'Vertices')
            points = [flat[i:i + 3] for i in range(0, len(flat), 3)]
            polygons, current = [], []
            for i in api.values(e, b'PolygonVertexIndex'):
                current.append(i if i >= 0 else -i - 1)
                if i < 0:
                    polygons.append(current)
                    current = []
            meshes[api.name(e)] = dict(points=points, polygons=polygons,
                materialIndices=api.values(api.child(e, b'LayerElementMaterial'), b'Materials'),
                slots=[api.name(by_id[i]) for i in incoming[owner] if by_id[i].id == b'Material'])
        return meshes

    erect_path = PROJECT / 'Assets/TestCharacter/playererect.fbx'
    flaccid_path = PROJECT / 'Assets/TestCharacter/playerflacid.fbx'
    endpoints = [geometry(p) for p in (erect_path, flaccid_path)]
    mappings = []
    for endpoint in endpoints:
        body, shell = endpoint['Genesis8Male'], endpoint['Dicktator Shell']
        anatomy = sorted({v for poly, slot in zip(body['polygons'], body['materialIndices'])
                          if slot >= 16 for v in poly})
        assert len(anatomy) == len(shell['points']) == 2205
        mapping = dict(enumerate(anatomy))
        for slot in range(16, 23):
            body_faces = Counter(tuple(sorted(poly)) for poly, s in
                                 zip(body['polygons'], body['materialIndices']) if s == slot)
            shell_faces = Counter(tuple(sorted(mapping[v] for v in poly)) for poly, s in
                                  zip(shell['polygons'], shell['materialIndices']) if s == slot)
            assert body_faces == shell_faces, f'Shell/body connectivity differs in slot {slot}'
        mappings.append(mapping)
    assert mappings[0] == mappings[1], 'Shell/body correspondence differs between endpoints'
    for name in ('Genesis8Male', 'Dicktator Shell'):
        a, b = (e[name] for e in endpoints)
        assert a['polygons'] == b['polygons'] and a['materialIndices'] == b['materialIndices']

    body, shell = endpoints[0]['Genesis8Male'], endpoints[0]['Dicktator Shell']
    points = [dict(shellIndex=i, bodyIndex=mappings[0][i],
                   shellPosition=unity_point(p), bodyPosition=unity_point(body['points'][mappings[0][i]]))
              for i, p in enumerate(shell['points'])]
    sources = dson.inspect(args.duf, [args.content_root])
    assert not sources['issues'] and not sources['missingTextures'], sources['issues'] + sources['missingTextures']
    # The body is a welded host/graft mesh; source ownership follows the
    # characterized material regions, not the renderer's host name.
    source_map = json.loads((ROOT / 'TestOutput/playernude-characterization/renderer-material-map.json').read_text())
    materials, bindings = [], []
    for mesh in ('Genesis8Male', 'Genesis8MaleEyelashes', 'Dicktator Shell'):
        entries = sorted((m for m in source_map if m['mesh'] == mesh and m['polygonCount'] > 0),
                         key=lambda m: m['slot'])
        for renderer_slot, entry in enumerate(entries):
            candidates = [m for m in sources['materials'] if m['nodeId'] == entry['sourceNode']
                          and m['surfaceGroups'] == [entry['sourceSurface']]]
            assert len(candidates) == 1, entry
            m = candidates[0]
            props = []
            for c in m['channels']:
                v = c['value']
                kind = 'color' if isinstance(v, list) and len(v) == 3 else 'string' if isinstance(v, str) else 'number'
                props.append(dict(name=c['name'], kind=kind,
                    number=float(v) if isinstance(v, (int, float, bool)) else 0,
                    color=dict(r=v[0], g=v[1], b=v[2], a=1) if kind == 'color' else {},
                    text=v if kind == 'string' else '',
                    texture=c['resolvedFiles'][0] if c['resolvedFiles'] else ''))
            index = len(materials)
            materials.append(dict(slot=index, node=m['nodeId'], surface=entry['sourceSurface'],
                                  assetType='Actor/Character', iray=True, properties=props))
            bindings.append(dict(mesh=mesh, slot=renderer_slot, material=index,
                                 fbxMaterial=entry['fbxMaterial'], surface=entry['sourceSurface']))
    manifest = dict(schemaVersion=1, erectAsset='Assets/TestCharacter/playererect.fbx',
        flaccidAsset='Assets/TestCharacter/playerflacid.fbx', erectSHA256=sha(erect_path),
        flaccidSHA256=sha(flaccid_path), sourceDUF=str(args.duf), dufSHA256=sources['sourceSHA256'],
        shellControlPoints=points, materials=materials, bindings=bindings,
        topologyProof='Bijective shell-to-body anatomy control points; all seven material-region polygon multisets match at both endpoints.',
        sourceTextureCount=sources['uniqueTextureCount'])
    destination = PROJECT / 'Assets/TestData/PartnerCharacter/manifest.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(manifest, separators=(',', ':')) + '\n', encoding='utf-8')
    print(f'PARTNER_MANIFEST_READY: {len(points)} verified vertex pairs; {len(materials)} material slots; '
          f'{sources["uniqueTextureCount"]} resolved source textures; {destination}')


if __name__ == '__main__':
    main()
