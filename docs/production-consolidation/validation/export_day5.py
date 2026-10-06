"""Export only validated Day 5 dependency closures; refuse unplanned overwrites."""
import hashlib
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[3]
STAGING = ROOT / '.dazposewizard/production-consolidation'
MAIN = ROOT / 'validation/DazPoseUnityValidation'
EVIDENCE = ROOT / '.dazposewizard/day5-evidence'
AUTHORIZED = {
    'Assets/Scenes/ClothingSetup.unity',
    'Assets/Wardrobe/WardrobeCatalog.asset',
    'Assets/Wardrobe/WardrobeConfiguration.asset',
    *(f'Assets/Wardrobe/Presets/{name}-outfit.asset' for name in ('first', 'second', 'third')),
}

def native(path):
    value = str(path.resolve())
    return '\\\\?\\' + value if os.name == 'nt' else value

def read(path):
    with open(native(path), 'rb') as stream:
        return stream.read()

def digest(path):
    return hashlib.sha256(read(path)).hexdigest().upper()

def report(name):
    return json.loads(read(STAGING / 'TestOutput/day5' / name))

def main():
    assert report('transactions.json')['passed'], 'Transactions did not pass.'
    assert report('runtime.json')['result'] == 'passed', 'Saved scene did not pass.'
    assert report('build.json')['result'] == 'Succeeded', 'Exact scene build did not pass.'
    assert report('setup-runtime.json')['result'] == 'passed', 'Saved Clothing Setup did not pass.'
    assert report('setup-build.json')['result'] == 'Succeeded', 'Saved Clothing Setup build did not pass.'
    assert report('setup-preparation.json')['passed'], 'Setup repair did not pass.'
    paths = set(report('dependencies.json')['paths'])
    paths.update(report('setup-preparation.json')['dependencies'])
    paths.update(AUTHORIZED)
    files = set()
    for value in paths:
        if value.startswith('Packages/'):
            continue
        assert value.startswith('Assets/') and '..' not in Path(value).parts, value
        source = STAGING / value
        assert source.exists(), f'Missing dependency: {value}'
        if source.is_file():
            files.add(value)
        if Path(native(Path(str(source) + '.meta'))).is_file():
            files.add(value + '.meta')
        for parent in Path(value).parents:
            if str(parent) in ('.', 'Assets'):
                break
            meta = parent.as_posix() + '.meta'
            if Path(native(STAGING / meta)).is_file():
                files.add(meta)
    # Check the complete operation before copying anything.
    baseline = {item['path']: item['hash'].upper() for item in json.loads(read(EVIDENCE / 'input-assets.json'))}
    pending = []
    for value in sorted(files):
        source, target = STAGING / value, MAIN / value
        if target.exists() and digest(source) != digest(target):
            same_text = value.endswith('.cs') and read(source).replace(b'\r\n', b'\n') == read(target).replace(b'\r\n', b'\n')
            if same_text:
                continue
            assert value in AUTHORIZED, f'Unplanned overwrite refused: {value}'
            assert value in baseline and digest(target) == baseline[value], f'Concurrent main-project edit preserved: {value}'
            backup = EVIDENCE / 'pre-export-backup' / value
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                shutil.copyfile(native(target), native(backup))
        if not target.exists() or digest(source) != digest(target):
            pending.append(value)
    for value in pending:
        target = MAIN / value
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(native(STAGING / value), native(target))
    mismatches = []
    for value in sorted(files):
        a, b = read(STAGING / value), read(MAIN / value)
        if value.endswith('.cs'):
            a, b = a.replace(b'\r\n', b'\n'), b.replace(b'\r\n', b'\n')
        if a != b:
            mismatches.append(value)
    protected = json.loads(read(EVIDENCE / 'input-assets.json'))
    protected += json.loads(read(EVIDENCE / 'imported-source-manifest.json'))
    failures = []
    for item in protected:
        value = item['path']
        if value in AUTHORIZED:
            continue
        if digest(MAIN / value) != item['hash'].upper():
            failures.append(value)
    result = {'passed': not mismatches and not failures, 'dependencies': len(paths), 'files': len(files),
              'copied': pending, 'mismatches': mismatches, 'protectedFailures': failures,
              'comparison': 'Exact bytes; source CRLF/LF normalized only',
              'manifest': [{'path': value, 'sha256': digest(MAIN / value)} for value in sorted(files)]}
    (EVIDENCE / 'export-check.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    assert result['passed'], result
    print(f"DAY5_EXPORT_PASS: {len(paths)} dependencies / {len(files)} files; {len(pending)} copied")

if __name__ == '__main__':
    main()
