# Luna execution entry point: wardrobe runtime and setup

Status: implementation is in progress. Runner, Contract, Migration, Setup and
Persistence pass. Runtime and Layers assertions pass but their required stress,
walk and culling evidence is incomplete; Integration is currently unimplemented.
See [progress.json](progress.json) for the current hashes and exact remaining work.
The ClothingSetup scene is validated in the isolated project and has not been
published into the main project. Continue from the first incomplete gate.

## Assignment

Implement the shared runtime outfit/layer controller, authoring facade and ONE
persistent ClothingSetup scene. Follow the stages below in order. The user has
authorized implementation through the execution assignment; ordinary source,
fixture, diagnostic and reversible configuration work needs no further permission.
Human visual approval is a separate result, requested with a concrete preview.
This is runtime implementation, not the separate recipe-only clean-context import
trial. Editing shared C# is expected here.

Read root AGENTS.md, this file and [CONTRACT.md](CONTRACT.md). Then read only the
current stage file and its named code seams. The larger
[product specification](../WardrobeRuntimeAndSetupSpec.md) is an exception reference
for omitted rationale; do not repeatedly reread its full history.

## Stages and gates

| Stage | Instructions | Gate to record in progress.json |
| --- | --- | --- |
| E0 | [01-runtime.md](01-runtime.md): runner and validation entry points | runner |
| E1 | same file: contract types and seven-mask layer behavior | contract |
| E2 | same file: known-import migration and naked/fully clothed fit states | migration |
| E3 | same file: atomic runtime binder, preserving rebind and vocabulary | runtime |
| E4 | [02-layer-fitting.md](02-layer-fitting.md): layer states, coverage and footwear | layers |
| E5 | [03-setup-and-save.md](03-setup-and-save.md): single setup scene and editor | setup |
| E6 | same file: persistence, variants and protected release/reimport boundary | persistence |
| E7 | [04-integration.md](04-integration.md): scene bindings, contention/stress and player build | integration |

An earlier gate must pass before a dependent stage is marked complete. Continue
routine implementation/repair autonomously. Do not run later tests against fake
or placeholder data and declare success. A bounded unsupported fit case must be
reported with exact meshes/state and evidence; it cannot be hidden by relaxing a
tolerance, scaling shoes, forcing every mesh into Base or disabling normal commands.

## Workspace and source protection

Main project: `validation/DazPoseUnityValidation`.
Isolated project: `.dazposewizard/p0c-native-generation` (already populated).
Unity: `C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe`.
Blender Python: `C:/Program Files/Blender Foundation/Blender 4.5/4.5/python/bin/python.exe`.

The three imported source packages already exist at
`Assets/TestData/Wardrobe/{first-outfit,second-outfit,third-outfit}/Outfit.asset`.
Canonical scene:
`Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity`.
Canonical point manifest: `Assets/TestData/LaraCandidate/manifest.json`.

Create runtime source under `Assets/DazPose/Runtime/Performer/Wardrobe/` and editor
source under `Assets/DazPose/Editor/Wardrobe/`. Keep existing namespaces
`DazPose.Performer` and `DazPose.UnityValidation` for facade/editor entry points.
Authored catalog/presets/configs go under `Assets/Wardrobe/`; generated fitting and
test assets go under `Assets/TestData/WardrobeRuntime/`.

Keep source package GUIDs, meshes, materials and original accepted scenes intact.
Do not rebuild or open the user's main scene from a batch process. Run generation
and checks in the isolated project. Do not kill Unity to obtain the project lock.
Do not publish, move or delete old scenes wholesale. Do not import FinalIK, build
a new scene language/director or add independent shoe-mixing gameplay.

Generate `.meta` files once and preserve them. If isolation created source metas
before the main editor did, copy those matching metas back before publishing
assets that reference their script GUIDs. Keep generated captures/builds ignored.
Do not stage large licensed source assets in Git or commit unrelated working-tree
changes. No commit/PR is required by this assignment.

## Resume protocol

Update [progress.json](progress.json) after each passed gate and before yielding:
current stage, completed gates, exact files, test/report path and remaining work.
Record a blocker only for an evidenced missing input/capability, not normal compile
errors or work taking more than one turn. Do not mark visual approval yourself.

On a fresh context, inspect progress and confirm the latest report still matches
the current source/package hashes. Resume at the first incomplete gate. Re-run
only gates affected by new changes. Keep commentary updates under roughly a
minute apart, explaining findings and what the next check resolves.

## Commands after E0 creates the runner

In a signed-in-user/escalated shell, from repository root:

```powershell
.\scripts\run-wardrobe-execution.ps1 -Stage Contract
.\scripts\run-wardrobe-execution.ps1 -Stage Migration
.\scripts\run-wardrobe-execution.ps1 -Stage Runtime
.\scripts\run-wardrobe-execution.ps1 -Stage Layers
.\scripts\run-wardrobe-execution.ps1 -Stage Setup
.\scripts\run-wardrobe-execution.ps1 -Stage Persistence
.\scripts\run-wardrobe-execution.ps1 -Stage Integration
```

These are the mandatory runner contract, not existing commands today. Create and
validate the runner in E0 before using them. Read
[VALIDATION.md](VALIDATION.md) for entry points/report requirements and publication.

## Finished means

All eight gates have current evidence; the common setup scene selects existing
and future candidate imports, saves configuration without scene-only tuning,
and exercises the same controller as gameplay. Outfit/layer vocabulary and query
state work in authored fixtures and a player build. Layer assignments and artist
tuning survive reimport. Normal future publication adds data/catalog entries and
does not create another production scene. Deliver the setup scene, controls,
captures and a concise known-limits report for the user's artistic review.

Start prompt to give Luna:

> Implement the wardrobe runtime and setup system by following
> validation/DazPoseUnityValidation/docs/wardrobe-execution/START.md.
> Execute the stages in order, keep progress.json current, and continue until the
> implementation and technical checks are complete or an evidenced external input
> is required. Give me frequent updates and a concrete preview for artistic review.
