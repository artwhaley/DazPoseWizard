"""Extract effective saved-scene DAZ material evidence without converting assets.

Merges local material-library channels with scene overrides. Retains shader,
figure/geometry/surface identities, native values, UV references and texture
paths. Unknown/external material inheritance is reported, never guessed.
This is an audit/sidecar prototype, not a complete DSON or Iray renderer.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
from urllib.parse import unquote


def read(path):
    data = path.read_bytes()
    return json.loads(gzip.decompress(data) if data[:2] == b'\x1f\x8b' else data), hashlib.sha256(data).hexdigest()


def channels(material):
    result = {}
    for value in material.values():
        if isinstance(value, dict) and isinstance(value.get('channel'), dict):
            channel = value['channel']
            result[channel['id']] = dict(channel)
    for extra in material.get('extra', []):
        if extra.get('type') == 'studio_material_channels':
            for entry in extra.get('channels', []):
                channel = entry['channel']
                result.setdefault(channel['id'], {}).update(channel)
    return result


def inspect(source, roots):
    scene, digest = read(source)
    library = {m['id']: m for m in scene.get('material_library', [])}
    owners = {g['id']: (n, g) for n in scene.get('scene', {}).get('nodes', []) for g in n.get('geometries', [])}
    records, issues = [], []
    for override in scene.get('scene', {}).get('materials', []):
        reference = override.get('url', '')
        if not reference.startswith('#') or unquote(reference[1:]) not in library:
            issues.append({'material': override.get('id'), 'reason': 'Unresolved material inheritance', 'url': reference})
            continue
        base = library[unquote(reference[1:])]
        effective = channels(base)
        for key, value in channels(override).items():
            effective.setdefault(key, {}).update(value)
        geometry_ref = override.get('geometry', '')
        owner = owners.get(unquote(geometry_ref.lstrip('#')))
        if owner is None:
            issues.append({'material': override.get('id'), 'reason': 'Unresolved geometry owner', 'geometry': geometry_ref})
        node, geometry = owner if owner else ({}, {})
        shader_types = sorted({e['type'] for material in [base, override] for e in material.get('extra', [])
                               if e.get('type', '').startswith('studio/material/')})
        uv = override.get('uv_set', base.get('uv_set'))
        visible = next((entry['channel'].get('current_value', entry['channel'].get('value'))
                        for extra in node.get('extra', []) for entry in extra.get('channels', [])
                        if entry.get('channel', {}).get('id') == 'Visible'), None)
        normalized = []
        for key, channel in effective.items():
            texture = channel.get('image_file')
            resolved = []
            if texture:
                decoded = unquote(texture)
                candidates = [Path(decoded)] if Path(decoded).is_absolute() and not decoded.startswith('/') else []
                candidates += [root / decoded.lstrip('/\\') for root in roots]
                resolved = [str(p.resolve()) for p in candidates if p.is_file()]
            normalized.append({'id': key, 'name': channel.get('name', key), 'label': channel.get('label', channel.get('name', key)),
                               'type': channel.get('type'), 'value': channel.get('current_value', channel.get('value')),
                               'imageFile': texture, 'resolvedFiles': resolved, 'imageGamma': channel.get('default_image_gamma'),
                               'imageSettings': {k: v for k, v in channel.items() if k.startswith('image_') and k != 'image_file'}})
        records.append({'instanceId': override.get('id'), 'libraryId': base['id'], 'nodeId': node.get('id'),
                        'nodeLabel': node.get('label'), 'nodeAssetUrl': node.get('url'), 'geometryRef': geometry_ref,
                        'geometryAssetUrl': geometry.get('url'), 'surfaceGroups': override.get('groups', []),
                        'sourceVisible': visible, 'shaderTypes': shader_types, 'uvSet': uv, 'channels': normalized})
    textures = {c['imageFile'] for r in records for c in r['channels'] if c['imageFile']}
    missing = sorted({c['imageFile'] for r in records for c in r['channels'] if c['imageFile'] and not c['resolvedFiles']})
    return {'schemaVersion': 1, 'sourceDUF': str(source.resolve()), 'sourceSHA256': digest,
            'evidenceOnly': True, 'materialCount': len(records), 'uniqueTextureCount': len(textures),
            'missingTextures': missing, 'issues': issues, 'materials': records}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('duf', type=Path)
    parser.add_argument('--content-root', type=Path, action='append', default=[])
    parser.add_argument('--out', type=Path, required=True)
    options = parser.parse_args()
    report = inspect(options.duf, options.content_root)
    options.out.parent.mkdir(parents=True, exist_ok=True)
    options.out.write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(f'Materials: {report["materialCount"]}; textures: {report["uniqueTextureCount"]}; '
          f'missing: {len(report["missingTextures"])}; inheritance/owner issues: {len(report["issues"])}')


if __name__ == '__main__':
    main()
