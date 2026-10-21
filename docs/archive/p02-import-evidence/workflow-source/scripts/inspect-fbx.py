"""Read binary FBX evidence without importing/rebinding/editing the asset.

Uses Blender's installed binary reader (not Blender's scene conversion). Run with
Blender's bundled Python; pass --blender-scripts pointing at scripts/addons_core.
Raw positions/morph deltas and cluster matrices remain in FBX coordinates/units.
Unity's companion inspector measures the actual imported meshes/materials.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import importlib
import json
import math
from pathlib import Path
import sys
import struct
import types


def child(element, key):
    return next((e for e in element.elems if e.id == key), None)


def values(element, key):
    e = child(element, key)
    return list(e.props[0]) if e and e.props else []


def name(element):
    return element.props[1].split(b'\x00\x01')[0].decode('utf-8', errors='replace')


def displacement(flat, vertex_count, epsilon):
    lengths = [math.sqrt(sum(v * v for v in flat[i:i + 3])) for i in range(0, len(flat), 3)]
    moved = [v for v in lengths if v > epsilon]
    return {'storedDeltaCount': len(lengths), 'movedVertexCount': len(moved),
            'maximum': max(lengths, default=0),
            'rmsAllVertices': math.sqrt(sum(v * v for v in lengths) / vertex_count) if vertex_count else 0,
            'rmsMovedVertices': math.sqrt(sum(v * v for v in moved) / len(moved)) if moved else 0}


def inspect(source, parser, epsilon):
    root, version = parser.parse(str(source))
    objects = child(root, b'Objects')
    connections = child(root, b'Connections')
    if objects is None or connections is None:
        raise ValueError('FBX lacks Objects or Connections')
    by_id = {e.props[0]: e for e in objects.elems}
    incoming, parents = defaultdict(list), defaultdict(list)
    for c in connections.elems:
        if c.id == b'C' and c.props[0] == b'OO':
            incoming[c.props[2]].append(c.props[1])
            parents[c.props[1]].append(c.props[2])

    def linked(ids, kind, subtype=None):
        return [by_id[i] for i in ids if i in by_id and by_id[i].id == kind
                and (subtype is None or by_id[i].props[-1] == subtype)]

    def path(model, visited=None):
        visited = set() if visited is None else visited
        if model.props[0] in visited:
            raise ValueError('Cyclic model hierarchy')
        visited.add(model.props[0])
        parent = linked(parents[model.props[0]], b'Model')
        if len(parent) > 1:
            raise ValueError('Multiple model parents: ' + name(model))
        return (path(parent[0], visited) + '/' if parent else '') + name(model)

    def properties(element):
        p = child(element, b'Properties70')
        return {e.props[0].decode('utf-8'): [v.decode('utf-8', errors='replace') if isinstance(v, bytes) else v
                for v in e.props[4:]] for e in p.elems if e.id == b'P'} if p else {}

    meshes = []
    for mesh in linked(by_id, b'Geometry', b'Mesh'):
        vertices = values(mesh, b'Vertices')
        vertex_count = len(vertices) // 3
        models = linked(parents[mesh.props[0]], b'Model')
        polygon_indices = values(mesh, b'PolygonVertexIndex')
        polygon_count = sum(v < 0 for v in polygon_indices)
        material_layers = []
        for layer in mesh.elems:
            if layer.id == b'LayerElementMaterial':
                material_layers.append({'mapping': child(layer, b'MappingInformationType').props[0].decode(),
                                        'materialIndices': dict(sorted(Counter(values(layer, b'Materials')).items()))})
        skins = []
        for skin in linked(incoming[mesh.props[0]], b'Deformer', b'Skin'):
            clusters = []
            influence_counts = Counter()
            for cluster in linked(incoming[skin.props[0]], b'Deformer', b'Cluster'):
                bones = linked(incoming[cluster.props[0]], b'Model')
                weights = values(cluster, b'Weights')
                indices = values(cluster, b'Indexes')
                if len(indices) != len(weights):
                    raise ValueError('Cluster weights/indices disagree: ' + name(cluster))
                for index, weight in zip(indices, weights):
                    if index < 0 or index >= vertex_count:
                        raise ValueError('Cluster index outside mesh: ' + name(cluster))
                    if weight > 0:
                        influence_counts[index] += 1
                clusters.append({'name': name(cluster), 'bones': [path(b) for b in bones],
                                 'indexCount': len(indices),
                                 'positiveWeightCount': sum(w > 0 for w in weights),
                                 'transform': values(cluster, b'Transform'),
                                 'transformLink': values(cluster, b'TransformLink'),
                                 'transformAssociateModel': values(cluster, b'TransformAssociateModel')})
            skins.append({'name': name(skin), 'clusters': clusters,
                          'unweightedControlPoints': vertex_count - len(influence_counts),
                          'maximumInfluencesPerControlPoint': max(influence_counts.values(), default=0),
                          'controlPointsOverFourInfluences': sum(v > 4 for v in influence_counts.values())})
        shapes = []
        for blend in linked(incoming[mesh.props[0]], b'Deformer', b'BlendShape'):
            for channel in linked(incoming[blend.props[0]], b'Deformer', b'BlendShapeChannel'):
                frames = []
                for shape in linked(incoming[channel.props[0]], b'Geometry', b'Shape'):
                    indices, deltas = values(shape, b'Indexes'), values(shape, b'Vertices')
                    if len(deltas) != len(indices) * 3 or len(set(indices)) != len(indices):
                        raise ValueError('Malformed shape indices/deltas: ' + name(shape))
                    if any(i < 0 or i >= vertex_count for i in indices):
                        raise ValueError('Shape index outside base mesh: ' + name(shape))
                    moved_indices = [index for i, index in enumerate(indices)
                                     if sum(v * v for v in deltas[i * 3:i * 3 + 3]) > epsilon * epsilon]
                    bounds = {'min': [min(vertices[i * 3 + axis] for i in moved_indices) for axis in range(3)],
                              'max': [max(vertices[i * 3 + axis] for i in moved_indices) for axis in range(3)]} if moved_indices else None
                    fingerprint = hashlib.sha256()
                    for index, xyz in zip(indices, zip(deltas[0::3], deltas[1::3], deltas[2::3])):
                        fingerprint.update(struct.pack('<qddd', index, *xyz))
                    frames.append({'name': name(shape), 'deltaSha256': fingerprint.hexdigest(),
                                   'movedVertexBounds': bounds, **displacement(deltas, vertex_count, epsilon)})
                deform = child(channel, b'DeformPercent')
                shapes.append({'name': name(channel), 'defaultDeformPercent': deform.props[0] if deform else None,
                               'fullWeights': values(channel, b'FullWeights'),
                               'frameCount': len(frames), 'frames': frames})
        meshes.append({'name': name(mesh), 'modelPaths': [path(m) for m in models],
                       'controlPointCount': vertex_count, 'polygonCount': polygon_count,
                       'polygonCornerCount': len(polygon_indices), 'materialLayers': material_layers,
                       'materialSlots': [[name(x) for x in linked(incoming[m.props[0]], b'Material')] for m in models],
                       'skins': skins, 'blendShapes': shapes})
    models = linked(by_id, b'Model')
    globals_element = child(root, b'GlobalSettings')
    return {'schemaVersion': 1, 'source': str(source.resolve()),
            'sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'fbxVersion': version,
            'measurementSpace': 'raw FBX geometry units; not Unity meters', 'movedThreshold': epsilon,
            'globalSettings': properties(globals_element) if globals_element else {},
            'hierarchy': [{'name': name(m), 'path': path(m), 'type': m.props[-1].decode(),
                           'properties': properties(m)} for m in sorted(models, key=path)],
            'materials': [{'id': m.props[0], 'name': name(m), 'properties': properties(m),
                           'textureConnections': [{'property': c.props[3].decode() if len(c.props) > 3 else '',
                                                   'textureId': c.props[1], 'texture': name(by_id[c.props[1]])}
                                                  for c in connections.elems if c.id == b'C'
                                                  and c.props[2] == m.props[0] and c.props[1] in by_id
                                                  and by_id[c.props[1]].id == b'Texture']}
                          for m in linked(by_id, b'Material')],
            'textures': [{'id': t.props[0], 'name': name(t), 'file': child(t, b'FileName').props[0].decode(errors='replace')
                          if child(t, b'FileName') else ''} for t in linked(by_id, b'Texture')],
            'meshes': meshes}


def main():
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument('fbx', type=Path)
    args.add_argument('--blender-scripts', type=Path, required=True)
    args.add_argument('--out', type=Path, required=True)
    args.add_argument('--epsilon', type=float, default=1e-6, help='Moved-vertex threshold in raw FBX units')
    options = args.parse_args()
    if options.epsilon < 0 or not math.isfinite(options.epsilon):
        args.error('--epsilon must be finite and nonnegative')
    # Load only the standalone reader; package __init__ imports bpy unnecessarily.
    package = types.ModuleType('io_scene_fbx')
    package.__path__ = [str(options.blender_scripts / 'io_scene_fbx')]
    sys.modules['io_scene_fbx'] = package
    parser = importlib.import_module('io_scene_fbx.parse_fbx')
    report = inspect(options.fbx, parser, options.epsilon)
    options.out.parent.mkdir(parents=True, exist_ok=True)
    options.out.write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(f'Wrote {options.out}: {len(report["meshes"])} meshes, '
          f'{sum(len(m["blendShapes"]) for m in report["meshes"])} shape channels')


if __name__ == '__main__':
    main()
