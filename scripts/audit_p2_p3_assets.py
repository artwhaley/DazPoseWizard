#!/usr/bin/env python3
"""Read-only P2-P3 source, YAML, GUID, and protected-baseline audit."""
from __future__ import annotations
import argparse, hashlib, json, re, subprocess, zipfile
from collections import defaultdict
from pathlib import Path
BASELINE="935fbd23a9aecf90a495176503ed8bd0337c22c4"
FC="Assets/Production/Story/Programs/FirstContact.dazstory"
UI="Assets/Production/UI"
LIB="Assets/Resources/DazPose/ProductionUi.asset"
FONT=("8f586378b4e144a9851e7b34d9b748ee","11400000")
SHADER=("fe393ace9b354375a9cb14cdbbc28be4","4800000")
def sha(b): return hashlib.sha256(b).hexdigest()
def load(p): return json.loads(p.read_text(encoding="utf-8"))
def get_guid(p):
    m=re.search(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$",p.read_text(encoding="utf-8",errors="replace"))
    return m.group(1).lower() if m else None
def git_blob(repo,rev,path):
    p=subprocess.run(["git","-c",f"safe.directory={repo.as_posix()}","show",f"{rev}:{path}"],cwd=repo,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    return p.stdout if p.returncode==0 else None
def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--project",type=Path,default=Path(__file__).resolve().parents[1]/"validation/DazPoseUnityValidation")
    ap.add_argument("--repo",type=Path,default=Path(__file__).resolve().parents[1])
    a=ap.parse_args(); project=a.project.resolve(); repo=a.repo.resolve(); assets=project/"Assets"; docs=repo/"docs/archive/docs/p2-p3"
    pb=load(docs/"PROTECTED_BASELINE.json")
    f00,f03,f04=[load(docs/x) for x in ("F00_SCENE_INVENTORY.json","F03_SCENE_RETIREMENT_PLAN.json","F04_SOURCE_AUDIT.json")]
    missing_allowed={}
    for r in f03.get("removed",[])+f03.get("files",[]): missing_allowed[r["path"]]={"evidence":"F03_SCENE_RETIREMENT_PLAN.json","sha256":r.get("sha256")}
    for r in f00.get("scenes",[]):
        if r.get("status")=="RETIRED" or r.get("classification")=="retired":
            if r.get("path"): missing_allowed[r["path"]]={"evidence":"F00_SCENE_INVENTORY.json","sha256":r.get("sha256")}
            if r.get("metaPath"): missing_allowed[r["metaPath"]]={"evidence":"F00_SCENE_INVENTORY.json","sha256":r.get("metaSha256")}
    for p in f04.get("expectedDisposableBaselineRemovals",[]): missing_allowed[p]={"evidence":"F04_SOURCE_AUDIT.json"}
    protected=[]; errors=[]
    ui_exc={"Assets/Production/Scenes/MainMenu.unity","Assets/Production/Application/SettingsPanel.prefab"}
    for r in pb.get("files",[]):
        path=r["path"]; p=assets/path.removeprefix("Assets/")
        if not p.is_file():
            ev=missing_allowed.get(path); protected.append({"path":path,"status":"explicitly_retired" if ev else "missing_unexpected","evidence":ev})
            if not ev: errors.append({"path":path,"issue":"protected baseline missing without retirement evidence"})
            continue
        b=p.read_bytes(); h=sha(b); same=h==r["sha256"] and len(b)==r["bytes"]; exception=path in ui_exc and not same
        protected.append({"path":path,"status":"unchanged" if same else "changed","baselineSha256":r["sha256"],"currentSha256":h,"baselineBytes":r["bytes"],"currentBytes":len(b),"authorizedUiException":exception})
        if not same and not exception: errors.append({"path":path,"issue":"protected file differs from baseline"})
    src=[]
    for p in sorted(assets.rglob("*")):
        if p.is_file() and (p.suffix==".cs" or p.suffix in {".asmdef",".asmref"}):
            b=p.read_bytes(); src.append({"path":"Assets/"+p.relative_to(assets).as_posix(),"bytes":len(b),"sha256":sha(b)})
    # Frozen FirstContact source, 19 dialogue rows, actual six poses/gesture/effects/motion/seven scene marks.
    fcdata=(assets/FC.removeprefix("Assets/")).read_bytes(); baseline_fc=git_blob(repo,BASELINE,"validation/DazPoseUnityValidation/"+FC)
    bind=(assets/"Production/Story/Programs/FirstContact.bindings.asset").read_text(encoding="utf-8")
    dm=load(assets/"Production/Story/Programs/FirstContact.dialogue-map.json"); dialogues=dm.get("clips",dm.get("dialogue",[]))
    fc=fcdata.decode("utf-8",errors="replace"); says=len(re.findall(r"(?m)^\s*await Lara\.Say\(",fc))
    requests={"poses":["SexyVamp","LeaningForward","CrossedArmsAmused","Lounging","Main1","Sassy"],"gestures":["Disapproval"],"shaders":["GlowyWireframe","Regular"],"motion":["Finisher1","FinishLoop"]}
    bindings={}
    for section,names in requests.items():
        sm=re.search(rf"(?ms)^  {section}:\s*\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:|\Z)",bind); block=sm.group(1) if sm else ""; rows=[]
        for name in names:
            m=re.search(rf"(?ms)^  - id: {re.escape(name)}\s*\n(.*?)(?=^  - id:|\Z)",block); body=m.group(1) if m else ""
            ref=re.search(r"value:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})",body)
            rows.append({"id":name,"present":bool(m),"fileID":ref.group(1) if ref else None,"guid":ref.group(2) if ref else None})
        bindings[section]=rows
    sm=re.search(r"(?ms)^  sceneSymbols:\s*\n(.*?)(?=^  [A-Za-z][A-Za-z0-9_]*:|\Z)",bind)
    symbols=re.findall(r"(?m)^  - id: (.+)$",sm.group(1) if sm else "")
    locs=["lara_start","player_start","camera_start","stepforward","Lounger","MainPosition1","Bedside"]
    bindings["sceneLocations"]=[{"id":x,"present":x in symbols} for x in locs]
    guidpaths={}
    for p in assets.rglob("*.meta"):
        g=get_guid(p)
        if g: guidpaths[g]=p.with_suffix("").relative_to(assets).as_posix()
    binding_guids=set()
    for rows in bindings.values():
        for r in rows:
            if r.get("guid"):
                r["assetPath"]=guidpaths.get(r["guid"]); r["assetExists"]=r["guid"] in guidpaths; binding_guids.add(r["guid"])
    fcok=baseline_fc is not None and fcdata==baseline_fc and says==19 and len(dialogues)==19 and all(x["present"] for x in bindings["sceneLocations"])
    fcok=fcok and all(x["present"] and x.get("assetExists") for k,rows in bindings.items() if k!="sceneLocations" for x in rows)
    if not fcok: errors.append({"issue":"FirstContact frozen source/transcript/binding check failed"})
    frozen_files={}
    for relative in (FC,"Assets/Production/Story/Programs/FirstContact.bindings.asset","Assets/Scenes/FirstContact.unity","Assets/Production/Story/Cards/FirstContactEngineering.dazstory","Assets/Production/Story/Cards/FirstContactEngineering.bindings.asset"):
        current=(project/relative).read_bytes()
        old=git_blob(repo,BASELINE,"validation/DazPoseUnityValidation/"+relative)
        frozen_files[relative]={"sha256":sha(current),"byteIdenticalToBaseline":current==old}
        if current!=old: errors.append({"path":relative,"issue":"protected FirstContact source/scene/content changed"})
    # Every Assets GUID and sidecar; current MonoScript refs against Assets/Packages/PackageCache and exact known F04 exceptions.
    files=[p for p in assets.rglob("*") if p.is_file()]; datafiles=[p for p in files if not p.name.endswith(".meta")]
    missing_meta=[p.relative_to(assets).as_posix() for p in datafiles if not Path(str(p)+".meta").is_file()]
    gm=defaultdict(list)
    for p in files:
        if p.name.endswith(".meta"):
            g=get_guid(p)
            if g: gm[g].append(p.with_suffix("").relative_to(assets).as_posix())
    duplicates={g:ps for g,ps in gm.items() if len(ps)>1}
    if missing_meta: errors.append({"issue":"missing metas","details":missing_meta})
    if duplicates: errors.append({"issue":"duplicate GUIDs","details":duplicates})
    scripts=set()
    for root in (assets,project/"Packages",project/"Library/PackageCache"):
        if root.exists():
            for p in root.rglob("*.cs.meta"):
                g=get_guid(p)
                if g: scripts.add(g)
    external_guids=set(gm)
    for pkgroot in (project/"Packages",project/"Library/PackageCache"):
        if pkgroot.exists():
            for sidecar in pkgroot.rglob("*.meta"):
                guid=get_guid(sidecar)
                if guid: external_guids.add(guid)
    knownset={(r["path"].removeprefix("Assets/"),r["guid"].lower()) for r in f04.get("baselineUnusedVendorPrefabScriptReferences",[])}
    known=[]; unresolved=[]
    vendor_baseline={}
    recovery=repo/".dazposewizard/recovery/18b268afe4df1ce11046a760b757002571a104c6/unity-inputs.zip"
    if recovery.is_file():
        with zipfile.ZipFile(recovery) as archive:
            for vendor_path,_ in knownset:
                entry="Assets/"+vendor_path
                try: vendor_baseline[vendor_path]=archive.read(entry)
                except KeyError: pass
    for p in datafiles:
        if p.suffix.lower() not in {".prefab",".unity",".asset"}: continue
        text=p.read_text(encoding="utf-8",errors="replace"); path=p.relative_to(assets).as_posix()
        for m in re.finditer(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})",text):
            g=m.group(1).lower()
            if g not in scripts:
                row={"path":path,"guid":g,"sha256":sha(p.read_bytes()),"baselineEvidence":"C00 recovery archive or starting git blob"}; (known if (path,g) in knownset and (p.read_bytes()==git_blob(repo,BASELINE,"validation/DazPoseUnityValidation/Assets/"+path) or p.read_bytes()==vendor_baseline.get(path)) else unresolved).append(row)
    if unresolved: errors.append({"issue":"unresolved MonoScript refs beyond known baseline","details":unresolved})
    # Actual production UI YAML references and authoritative library/font/shader targets.
    uirows=[]
    for p in sorted((assets/UI.removeprefix("Assets/")).rglob("*.prefab")) if (assets/UI.removeprefix("Assets/")).exists() else []:
        text=p.read_text(encoding="utf-8",errors="replace"); ids=set(re.findall(r"(?m)^--- !u!\d+ &(-?\d+)",text)); bad=[]
        for m in re.finditer(r"\{fileID:\s*(-?\d+)([^}]*)\}",text):
            fid,tail=m.group(1),m.group(2)
            if "guid:" not in tail and fid not in {"0","-0"} and fid not in ids: bad.append(fid)
        active=re.search(r"(?m)^  m_IsActive: (\d+)$",text)
        row={"path":p.relative_to(assets).as_posix(),"rootName":(re.search(r"(?m)^  m_Name: (.+)$",text) or [None,None])[1],
             "rootInactive":bool(active and active.group(1)=="0"),"localDocumentCount":len(ids),"unresolvedLocalFileIds":sorted(set(bad)),
             "canvasRendererDocuments":len(re.findall(r"!u!222 &",text)),"externalGuids":sorted(set(g.lower() for g in re.findall(r"guid:\s*([0-9a-fA-F]{32})",text)))}
        missing_external=[g for g in row["externalGuids"] if g not in external_guids and not g.startswith("0000000000000000")]
        row["unresolvedExternalGuids"]=missing_external
        if missing_external: errors.append({"path":row["path"],"issue":"unresolved external UI GUIDs","details":missing_external})
        uirows.append(row)
        if bad: errors.append({"path":row["path"],"issue":"unresolved local fileIDs","details":row["unresolvedLocalFileIds"]})
        if not row["rootInactive"]: errors.append({"path":row["path"],"issue":"template root active"})
    libtext=(assets/LIB.removeprefix("Assets/")).read_text(encoding="utf-8",errors="replace") if (assets/LIB.removeprefix("Assets/")).is_file() else ""; librefs=[]
    for m in re.finditer(r"\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32})",libtext):
        fid,g=m.group(1),m.group(2).lower(); target=next(iter(gm.get(g,[])),None); tp=assets/target.removeprefix("Assets/") if target else None
        content=tp.read_text(encoding="utf-8",errors="replace") if tp and tp.is_file() else ""
        found=bool(re.search(rf"(?m)^--- !u!\d+ &{re.escape(fid)}(?:\s|$)",content)) or fid=="0" or (tp and tp.suffix==".cs" and fid=="11500000")
        row={"fileID":fid,"guid":g,"target":target,"guidResolved":bool(target),"fileIDFound":found}; librefs.append(row)
        if not target or not found: errors.append({"issue":"ProductionUiLibrary ref unresolved","details":row})
    assets_meta={g for g in gm}; font_shader={}
    for name,(g,fid) in (("font",FONT),("shader",SHADER)):
        target=next(iter(gm.get(g,[])),None); tp=assets/target.removeprefix("Assets/") if target else None
        content=tp.read_text(encoding="utf-8",errors="replace") if tp and tp.is_file() else ""
        found=bool(re.search(rf"(?m)^--- !u!\d+ &{re.escape(fid)}(?:\s|$)",content)) or (tp and tp.suffix==".shader" and fid=="4800000" and "Shader " in content)
        row={"guid":g,"fileID":fid,"target":target,"assetExists":g in assets_meta,"fileIDFound":found}; font_shader[name]=row
        if not row["assetExists"] or not found: errors.append({"issue":f"UI {name} ref unresolved","details":row})
    baselineset={r["path"] for r in pb.get("files",[])}
    additions=sorted({"Assets/"+p.relative_to(assets).as_posix() for p in datafiles}-baselineset)
    head=subprocess.run(["git","-c",f"safe.directory={repo.as_posix()}","rev-parse","HEAD"],cwd=repo,capture_output=True,text=True).stdout.strip()
    result={"schema":"dazposewizard-p2-p3-final-asset-audit-v1","baselineCommit":BASELINE,"currentHead":head,"readOnly":True,"unityOrApplicationInvoked":False,
      "status":"PASS" if not errors else "FAIL","issues":errors,
      "counts":{"assets":len(datafiles),"guidCount":sum(map(len,gm.values())),"duplicateGuids":len(duplicates),"missingMetas":len(missing_meta),
        "protectedFiles":len(protected),"protectedUnchanged":sum(x["status"]=="unchanged" for x in protected),"explicitRetirementExceptions":len(missing_allowed),
        "currentSourceFilesHashed":len(src),"currentScenes":len(list(assets.rglob("*.unity"))),"productionUiPrefabs":len(uirows)},
      "protectedBaselineComparison":protected,"explicitRetirementInputs":[{"path":p,**v} for p,v in sorted(missing_allowed.items())],
      "currentSourceHashes":src,
      "firstContact":{"path":FC,"baselineSha256":sha(baseline_fc) if baseline_fc is not None else None,"currentSha256":sha(fcdata),
        "byteIdenticalToBaseline":baseline_fc is not None and fcdata==baseline_fc,"sayInstructionCount":says,"dialogueMapCount":len(dialogues),
        "frozenSourcesSceneAndCompanions":frozen_files,"dialogueIds":[x.get("id") for x in dialogues],"bindings":bindings,"bindingGuids":sorted(binding_guids),"frozenCheckPass":fcok},
      "guidAudit":{"duplicates":duplicates,"missingMetaAssets":missing_meta,"knownBaselineVendorScriptReferences":known,"newUnresolvedScriptReferences":unresolved},
      "productionUiStaticAudit":{"prefabs":uirows,"library":LIB,"libraryReferences":librefs,"fontAndShader":font_shader,
        "boundary":"Static YAML only; Unity import/fileID semantics not checked."},
      "newAssetPathsComparedWithProtectedBaseline":additions,
      "validationBoundary":"Python source/YAML/meta/hash checks only. Unity, Editor, Play, builds and runtime checks NOT RUN."}
    out=docs/"FINAL_ASSET_AUDIT.json"; out.write_text(json.dumps(result,indent=2,sort_keys=True)+"\n",encoding="utf-8",newline="\n")
    print(json.dumps({"status":result["status"],"issueCount":len(errors),"output":str(out),"counts":result["counts"]},indent=2))
    return 0 if not errors else 1
if __name__=="__main__": raise SystemExit(main())