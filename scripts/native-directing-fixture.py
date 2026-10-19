"""Physical isolated Unity fixture and code-only synchronization. Never starts Unity."""
from __future__ import annotations
import argparse, hashlib, json, re, shutil, subprocess, uuid
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SOURCE = REPO / "validation/DazPoseUnityValidation"
FIXTURE = REPO / ".dazposewizard/native-directing/G1Project"
DOCS = REPO / "docs/native-directing"
MARKER = ".native-directing-fixture.json"
SCENE = "Assets/__NativeDirectingFixtures/G1_FirstContact.unity"
BASELINE = DOCS / "N00_BASELINE.json"
CODE = {".cs", ".asmdef", ".asmref"}

def digest(path):
    with path.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()

def code_file(path):
    return path.suffix in CODE or path.suffix == ".meta" and Path(path.stem).suffix in CODE

def files(root):
    return sorted(p for p in root.rglob("*") if p.is_file())

def revision():
    return subprocess.check_output(["git", "-c", f"safe.directory={REPO.as_posix()}", "rev-parse", "HEAD"], cwd=REPO, text=True).strip()

def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2)+"\n", encoding="utf-8")

def source_code():
    return {p.relative_to(SOURCE).as_posix(): digest(p) for p in files(SOURCE/"Assets") if code_file(p)}

def code_rows(values):
    return [{"path": path, "sha256": sha} for path, sha in sorted(values.items())]

def aggregate(values):
    return hashlib.sha256("".join(f"{p}|{h}\n" for p,h in sorted(values.items())).encode()).hexdigest()

def copy_physical(src, dst):
    if src.is_symlink():
        raise RuntimeError(f"Symbolic link input refused: {src}")
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)
    if src.samefile(dst):
        raise RuntimeError(f"Fixture aliases original: {src}")

def prepare():
    if FIXTURE.exists() or BASELINE.exists():
        raise RuntimeError("Baseline/fixture already exists; use verify or sync-code, never overwrite authored fixture")
    DOCS.mkdir(parents=True, exist_ok=True)
    protected = {}
    for name in ("Assets", "Packages", "ProjectSettings"):
        for p in files(SOURCE/name):
            if not code_file(p):
                protected[p.relative_to(REPO).as_posix()] = {"sha256":digest(p), "bytes":p.stat().st_size}
    historical = json.loads((REPO/"docs/studio-corrective/BASELINE.json").read_text())
    for row in historical["files"]:
        p=REPO/row["path"]
        if p.is_file() and not code_file(p):
            protected[row["path"]]={"sha256":digest(p),"bytes":p.stat().st_size}
    status=subprocess.check_output(["git","-c",f"safe.directory={REPO.as_posix()}","status","--porcelain=v1","--untracked-files=all"],cwd=REPO,text=True)
    write_json(BASELINE,{"head":revision(),"createdUtc":datetime.now(timezone.utc).isoformat(),"protectedFiles":[{"path":k,**v} for k,v in sorted(protected.items())],"preexistingWorktree":status.splitlines(),"fixtureProject":str(FIXTURE),"nativeEvidence":"NOT RUN; Art operates all Unity"})
    print(f"Baseline: {len(protected)} protected files",flush=True)
    for name in ("Assets", "Packages", "ProjectSettings", "Library/PackageCache"):
        count=0
        for p in files(SOURCE/name):
            copy_physical(p,FIXTURE/p.relative_to(SOURCE))
            count+=1
        print(f"Copied {name}: {count} files",flush=True)
    original=SOURCE/"Assets/Production/Scenes/FirstContact.unity"
    copy_physical(original,FIXTURE/SCENE)
    meta=f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    (FIXTURE/(SCENE+".meta")).write_text(meta,encoding="utf-8")
    folder=FIXTURE/"Assets/__NativeDirectingFixtures.meta"
    folder.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",encoding="utf-8")
    values=source_code()
    marker={"schemaVersion":1,"fixtureId":uuid.uuid4().hex,"sourceProject":str(SOURCE),"fixtureProject":str(FIXTURE),"scenePath":SCENE,"sourceRevision":revision(),"codeHash":aggregate(values),"codeFiles":code_rows(values),"createdUtc":datetime.now(timezone.utc).isoformat(),"sceneBaselineSha256":digest(original)}
    write_json(FIXTURE/MARKER,marker)
    # All content inputs must initially be byte-identical physical files, including ignored art.
    mismatches=[]
    for path,row in protected.items():
        p=REPO/path
        if p.is_relative_to(SOURCE):
            target=FIXTURE/p.relative_to(SOURCE)
            if not target.is_file() or digest(target)!=row["sha256"] or p.samefile(target):mismatches.append(path)
    if mismatches:raise RuntimeError(f"Fixture content mismatch/alias: {mismatches}")
    print(f"Independent fixture ready: {FIXTURE}; no Unity launched",flush=True)
    verify()

def sync_code(gate="G1"):
    marker=json.loads((FIXTURE/MARKER).read_text())
    if Path(marker["fixtureProject"]).resolve()!=FIXTURE.resolve() or Path(marker["sourceProject"]).resolve()!=SOURCE.resolve():
        raise RuntimeError("Fixture identity differs")
    previous={r["path"]:r["sha256"] for r in marker["codeFiles"]}
    values=source_code()
    # Detect independently added fixture scripts as well as edits to known ones.
    existing={p.relative_to(FIXTURE).as_posix() for p in files(FIXTURE/"Assets") if code_file(p)}
    extra=existing.difference(previous)
    if extra:raise RuntimeError(f"Fixture contains independently added code; refuse synchronization: {sorted(extra)}")
    # Validate all destinations before making any changes.
    for path in sorted(set(previous)|set(values)):
        target=FIXTURE/path
        expected=previous.get(path)
        if target.exists() and (expected is None or digest(target)!=expected):
            raise RuntimeError(f"Fixture code was changed independently; refuse overwrite: {path}")
        if expected is not None and not target.exists():raise RuntimeError(f"Fixture code missing: {path}")
    for path in sorted(set(previous)|set(values)):
        target=FIXTURE/path
        if path not in values:target.unlink()
        elif previous.get(path)!=values[path]:copy_physical(SOURCE/path,target)
    marker.update(sourceRevision=revision(),codeHash=aggregate(values),codeFiles=code_rows(values),syncedUtc=datetime.now(timezone.utc).isoformat())
    for path,sha in values.items():
        if digest(FIXTURE/path)!=sha:raise RuntimeError(f"Code sync verification failed: {path}")
    write_json(FIXTURE/MARKER,marker)
    write_json(DOCS/(gate+"_CODE_SYNC.json"),{"baseRevision":json.loads(BASELINE.read_text())["head"],"codeHash":marker["codeHash"],"files":len(values),"fixtureProject":str(FIXTURE),"unityLaunched":False})
    print(f"Code and meta sync PASS: {len(values)} files, {marker['codeHash']}")

def verify():
    data=json.loads(BASELINE.read_text())
    mismatches=[r["path"] for r in data["protectedFiles"] if not (REPO/r["path"]).is_file() or digest(REPO/r["path"])!=r["sha256"]]
    result={"head":revision(),"protectedFiles":len(data["protectedFiles"]),"mismatches":mismatches,"pass":not mismatches,"checkedUtc":datetime.now(timezone.utc).isoformat()}
    write_json(DOCS/"PROTECTED_CHECK.json",result)
    print(f"Original protected files: {len(data['protectedFiles'])}, mismatches: {len(mismatches)}")
    if mismatches:raise RuntimeError(str(mismatches))

if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action",choices=["prepare","sync-code","verify"])
    parser.add_argument("--gate",choices=["G1","G2","G3","G4"],default="G1",help="Evidence filename for code-only synchronization")
    args=parser.parse_args()
    {"prepare":prepare,"sync-code":lambda:sync_code(args.gate),"verify":verify}[args.action]()
