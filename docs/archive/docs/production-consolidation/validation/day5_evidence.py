"""Record delivered-source parity, authored-environment preservation and evidence hashes."""
import hashlib
import json
import re
from pathlib import Path
from export_day5 import ROOT, MAIN, STAGING, EVIDENCE, read, digest

def write(name, value):
    (EVIDENCE / name).write_text(json.dumps(value, indent=2), encoding='utf-8')

def records(path):
    text = read(path).decode('utf-8-sig').replace('\r\n', '\n')
    blocks = re.split(r'(?m)(?=^--- !u!)', text)[1:]
    return {re.match(r'--- !u!(\d+) &(-?\d+)', block).group(2): block for block in blocks}

def main():
    sources, differences = [], []
    for tree in ('DazPose', 'Player', 'FirstPerformanceVoid'):
        for path in sorted((MAIN / 'Assets' / tree).rglob('*')):
            if not path.is_file() or not (path.name.endswith('.cs') or path.name.endswith('.cs.meta')):
                continue
            relative = path.relative_to(MAIN).as_posix()
            data = read(path)
            peer = STAGING / relative
            same = peer.exists() and data.replace(b'\r\n', b'\n') == read(peer).replace(b'\r\n', b'\n')
            sources.append({'path': relative, 'sha256': hashlib.sha256(data).hexdigest(), 'matches': same})
            if not same:
                differences.append(relative)
    write('source-manifest.json', {'passed': not differences, 'files': sources, 'differences': differences,
                                   'comparison': 'Source CRLF/LF only; meta bytes preserved'})
    before = records(MAIN / 'Assets/Production/Scenes/Day2ActorValidation.unity')
    allowed = {'323584076', '883108039', '883108040', '883108041', '883108042', '883108043',
               '1170267246', '1170267249', '9223372036854775807'}
    scenes, unexpected = [], []
    for scene in ('Assets/Production/Scenes/Day5WardrobeValidation.unity', 'Assets/Scenes/ClothingSetup.unity'):
        after = records(MAIN / scene)
        changed = [{'id': key, 'type': re.match(r'--- !u!(\d+)', value).group(1),
                    'result': 'missing' if key not in after else 'changed'}
                   for key, value in before.items() if after.get(key) != value]
        problems = [value for value in changed if value['id'] not in allowed]
        unexpected.extend(problems)
        scenes.append({'scene': scene, 'baseRecords': len(before), 'unchangedBaseRecords': len(before) - len(changed),
                       'changedExistingRecords': changed, 'unexpectedChanges': problems})
    write('environment-preservation.json', {'passed': not unexpected, 'scenes': scenes,
          'unexpectedChanges': unexpected, 'originalVoidSha256': digest(MAIN / 'Assets/Scenes/FirstPerformanceVoid.unity'),
          'laraPrefabSha256': digest(MAIN / 'Assets/Production/Performers/Lara.prefab'),
          'allowedChanges': ['Primary actor placement and candidate binding', 'Remove second actor',
                             'Authorized camera framing', 'Scene root list'],
          'method': 'Exact YAML record comparison against accepted Day 2 authored Void composition'})
    files = sorted(p for p in EVIDENCE.rglob('*') if p.is_file() and p.name != 'evidence-ledger.json')
    write('evidence-ledger.json', {'files': [{'path': p.relative_to(ROOT).as_posix(), 'sha256': digest(p)} for p in files],
          'humanWorkflowApproval': 'pending', 'day4VisualApproval': 'passed_user_reports_manual_validation',
          'independentCleanContextImportAcceptance': 'unchanged_separate_task'})
    assert not differences, differences
    assert not unexpected, unexpected
    print(f'DAY5_EVIDENCE_PASS: {len(sources)} source files; {len(scenes)} authored environment comparisons')

if __name__ == '__main__':
    main()
