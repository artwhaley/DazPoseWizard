"""Read-only S02 input inventory; never imports Unity or extracts the vendor package."""
import collections
import hashlib
import html
import json
from pathlib import Path
import re
import tarfile

REPO = Path(__file__).resolve().parents[3]
ROOT = Path(r'C:\Users\artwh\DazPoseWizard-Sandbox')
OUT = Path(__file__).parent
PACKAGE = REPO / 'newtickets/UMotion Pro - Animation Editor v1.29p04.unitypackage'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def clip_info(path):
    text = path.read_text(encoding='utf-8-sig')
    sections = list(re.finditer(r'^  (m_\w+):[^\n]*', text, re.M))
    curves = []
    section_counts = {}
    for i, section in enumerate(sections):
        name = section[1]
        if name not in ('m_RotationCurves', 'm_CompressedRotationCurves', 'm_EulerCurves',
                        'm_PositionCurves', 'm_ScaleCurves', 'm_FloatCurves',
                        'm_PPtrCurves', 'm_EditorCurves', 'm_EulerEditorCurves'):
            continue
        body = text[section.end():sections[i+1].start() if i+1 < len(sections) else len(text)]
        starts = list(re.finditer(r'^  - ', body, re.M))
        section_counts[name] = len(starts)
        for j, start in enumerate(starts):
            block = body[start.start():starts[j+1].start() if j+1 < len(starts) else len(body)]
            def field(key):
                m = re.search(r'^    '+key+r': *(.*)$', block, re.M)
                return m[1].strip() if m else None
            curves.append(dict(section=name, path=field('path'), attribute=field('attribute'),
                               classID=field('classID'), keys=len(re.findall(r'^        time:', block, re.M)),
                               times=[float(t) for t in re.findall(r'^        time: (\S+)', block, re.M)]))
    def scalar(key):
        m = re.search(r'^\s*'+key+r': (\S+)', text, re.M)
        return m[1] if m else None
    runtime = [c for c in curves if c['section'] not in ('m_EditorCurves','m_EulerEditorCurves')]
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path),
                startTime=scalar('m_StartTime'), stopTime=scalar('m_StopTime'),
                sampleRate=scalar('m_SampleRate'), sectionCounts=section_counts,
                runtimeSerializedCurveRecords=len(runtime),
                runtimeKeyCounts=dict(collections.Counter(str(c['keys']) for c in runtime)),
                actorRootCurves=[c for c in runtime if c['path'] in ('','""')],
                curves=curves)

guid_paths = {}
for meta in (ROOT/'Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig', errors='replace'), re.M)
    if match:
        guid_paths[match[1]] = Path(str(meta)[:-5])
bindings = (ROOT/'Assets/Production/Story/Programs/FirstContact.bindings.asset').read_text()
pose_block = bindings.split('  poses:\n',1)[1].split('  gestures:',1)[0]
rows = []
for alias, guid in re.findall(r'  - id: (\S+)\n    value: \{fileID: \d+, guid: (\w+)', pose_block):
    asset = guid_paths[guid]
    clip_guid = re.search(r'  clip: \{fileID: \d+, guid: (\w+)', asset.read_text())[1]
    clip = guid_paths[clip_guid]
    row = dict(alias=alias, poseGuid=guid, posePath=asset.relative_to(ROOT).as_posix(),
               clipGuid=clip_guid, clipPath=clip.relative_to(ROOT).as_posix(),
               poseSha256=digest(asset), clipSha256=digest(clip))
    if alias in ('LeaningForward','Lounging'):
        row['analysis'] = clip_info(clip)
    rows.append(row)
(OUT/'poses.json').write_text(json.dumps(rows,indent=2)+'\n')
with tarfile.open(PACKAGE) as archive:
    entries = []
    manuals = {}
    for member in archive.getmembers():
        if not member.name.endswith('/pathname'):
            continue
        path = archive.extractfile(member).read().decode('utf-8-sig').splitlines()[0].strip('\0')
        content_name = member.name.rsplit('/',1)[0]+'/asset'
        try:
            asset_member = archive.getmember(content_name)
        except KeyError:
            continue
        entries.append(dict(path=path,bytes=asset_member.size))
        if path.endswith('.html') and any(n in path for n in ('ReleaseNotes','UMotionAPI','Introduction','ImportExport','ProjectSettings','MenuBarFile')):
            text = archive.extractfile(asset_member).read().decode('utf-8-sig',errors='replace')
            text = html.unescape(re.sub('<[^>]+>',' ',text))
            manuals[path] = re.sub(r'\s+',' ',text)
    (OUT/'package.json').write_text(json.dumps(dict(file=PACKAGE.name,sha256=digest(PACKAGE),
         bytes=PACKAGE.stat().st_size,assetCount=len(entries),unpackedAssetBytes=sum(e['bytes'] for e in entries),
         entries=entries),indent=2)+'\n')
    # Vendor prose stays local to this read-only inspection; print small relevant context only.
    for path,text in manuals.items():
        if 'ReleaseNotes' in path:
            print(path, text[:700])
        elif 'UMotionAPI' in path:
            print(path, text[:700])
print(json.dumps([{k:v for k,v in r.items() if k!='analysis'} | {'summary':
    {k:v for k,v in r.get('analysis',{}).items() if k!='curves'}} for r in rows],indent=2))
