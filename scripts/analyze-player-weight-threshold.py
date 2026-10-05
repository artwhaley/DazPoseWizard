"""Quantify source FBX weights below Unity's minimum accepted 0.001 cutoff."""
import hashlib
import importlib.util
import json
import sys
import types
from collections import defaultdict
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
FBX_DIR = ROOT / "validation/DazPoseUnityValidation/Assets/TestCharacter"
OUT = ROOT / "TestOutput/G8MArtistErectionProof"
EXPECTED = {
    "playererect.fbx": "ba2bcca1dadb350a59e86e118a1b9b0195fef6033c00c472cc1523bf9c3481f1",
    "playerflacid.fbx": "7a8e9188ca04e205870f0ef7619cc0f10ab59af81dab2cff4ff3590ed6a0cda4",
}


def import_helper(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


f = import_helper("fbx_evidence", ROOT / "scripts/inspect-fbx.py")
pkg = types.ModuleType("io_scene_fbx")
pkg.__path__ = ["C:/Program Files/Blender Foundation/Blender 4.5/4.5/scripts/addons_core/io_scene_fbx"]
sys.modules["io_scene_fbx"] = pkg
from io_scene_fbx import parse_fbx


def fbx_meshes(path):
    root, _ = parse_fbx.parse(str(path))
    objects = f.child(root, b"Objects").elems
    by_id = {e.props[0]: e for e in objects}
    connections = f.child(root, b"Connections").elems
    parents, incoming = defaultdict(list), defaultdict(list)
    for conn in connections:
        if conn.props[0] == b"OO":
            parents[conn.props[1]].append(conn.props[2])
            incoming[conn.props[2]].append(conn.props[1])

    meshes = {}
    for geometry in objects:
        if geometry.id != b"Geometry" or geometry.props[-1] != b"Mesh":
            continue
        owner = next(i for i in parents[geometry.props[0]] if by_id[i].id == b"Model")
        layer = f.child(geometry, b"LayerElementMaterial")
        material_slots = f.values(layer, b"Materials")
        polygon_regions = defaultdict(set)
        polygon_indices = np.asarray(f.values(geometry, b"PolygonVertexIndex"), dtype=np.int64)
        polys, poly = [], []
        for raw in polygon_indices:
            poly.append(int(raw) if raw >= 0 else int(-raw - 1))
            if raw < 0:
                polys.append(poly)
                poly = []
        for face, slot in zip(polys, material_slots):
            polygon_regions[int(slot)].update(face)

        clusters = []
        for skin_id in incoming[geometry.props[0]]:
            skin = by_id[skin_id]
            if skin.id != b"Deformer" or skin.props[-1] != b"Skin":
                continue
            for cluster_id in incoming[skin_id]:
                cluster = by_id[cluster_id]
                if cluster.id != b"Deformer" or cluster.props[-1] != b"Cluster":
                    continue
                clusters.append({
                    "indices": np.asarray(f.values(cluster, b"Indexes"), dtype=np.int64),
                    "weights": np.asarray(f.values(cluster, b"Weights"), dtype=np.float64),
                })
        meshes[f.name(geometry)] = {
            "controlPointCount": len(f.values(geometry, b"Vertices")) // 3,
            "regions": polygon_regions,
            "clusters": clusters,
        }
    return meshes


def summarize(mesh, threshold):
    point_counts = np.zeros(mesh["controlPointCount"], dtype=np.int32)
    point_weights = np.zeros(mesh["controlPointCount"], dtype=np.float64)
    point_low_counts = np.zeros(mesh["controlPointCount"], dtype=np.int32)
    low_weight_mass = 0.0
    low_influences = 0
    positive_influences = 0
    for cluster in mesh["clusters"]:
        indices, weights = cluster["indices"], cluster["weights"]
        positive = weights > 0
        low = positive & (weights < threshold)
        np.add.at(point_counts, indices[positive], 1)
        np.add.at(point_weights, indices, weights)
        np.add.at(point_low_counts, indices[low], 1)
        low_influences += int(low.sum())
        positive_influences += int(positive.sum())
        low_weight_mass += float(weights[low].sum())
    low_points = np.flatnonzero(point_low_counts > 0)
    anatomy = set().union(*(mesh["regions"].get(slot, set()) for slot in range(16, 23)))
    anatomy_low = sorted(set(low_points.tolist()) & anatomy)
    return {
        "controlPoints": int(mesh["controlPointCount"]),
        "skinClusters": len(mesh["clusters"]),
        "maximumSourceInfluencesPerPoint": int(point_counts.max(initial=0)),
        "positiveSourceInfluences": positive_influences,
        "positiveInfluencesBelowThreshold": low_influences,
        "belowThresholdInfluenceShare": low_influences / positive_influences if positive_influences else 0,
        "sumOfWeightsBelowThreshold": low_weight_mass,
        "pointsWithAtLeastOneBelowThresholdInfluence": len(low_points),
        "anatomyPointsWithAtLeastOneBelowThresholdInfluence": len(anatomy_low),
        "anatomyPointCount": len(anatomy),
    }


def main():
    threshold = 0.001
    result = {"threshold": threshold, "endpoints": {}}
    for name, expected in EXPECTED.items():
        path = FBX_DIR / name
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest != expected:
            raise SystemExit(f"STOP: source hash changed for {path}: {digest}")
        meshes = fbx_meshes(path)
        result["endpoints"][name] = {
            "path": str(path.resolve()),
            "sha256": digest,
            "body": summarize(meshes["Genesis8Male"], threshold),
        }
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "WeightThresholdImpact.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
