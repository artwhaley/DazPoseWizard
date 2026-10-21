# DazPoseWizard — Deferred Work Backlog

**Status:** Deferred planning reference  
**Implementation authority:** None of the work in this document is authorized for implementation unless explicitly approved in a later phase/stage.  
**Current active next step:** Return primary development focus to the Unity game.  
**Current architecture baseline:** Phase 4 library browser + Unity drag/import pipeline on `main` after commit `1e5c9f7` (“Complete Phase 4 library browser and Unity drag pipeline”).

---

## Purpose

This document preserves architectural decisions and future directions for DazPoseWizard so they do not need to be rediscovered while development pivots back toward the Unity game.

These ideas are intentionally **deferred**.

Do not implement them opportunistically while working on Stage 5, game integration, test-scene work, or unrelated bug fixes.

Stage 5's minimum direct-blendshape pipeline is complete. No further DazPoseWizard feature work is currently scheduled before returning focus to the game.

---

# 1. Retargeting Other Genesis Generations to Canonical G8F

## Goal

Allow pose presets authored for additional Genesis generations to be converted into the same canonical Genesis 8 Female output already consumed by the Unity pipeline.

The important architectural rule is:

> Unity should continue consuming one canonical G8F pose format. Generation-specific retargeting belongs in DazPose.Core before canonical export.

Target shape:

```text
G8F pose ─────────────────────┐
                             │
G8.1F pose → retarget ───────┼→ canonical G8F .dazpose.json → existing Unity .anim pipeline
                             │
G9 pose → retarget ──────────┘
                             │
G8M / G8.1M → retarget later ┘
```

Unity should not gain separate G8.1, G9, or G8M animation conversion paths unless future evidence forces that design.

## G8.1F

Genesis 8.1 Female appears structurally extremely close to Genesis 8 Female.

The preferred implementation is still a formal retarget profile, but it may be almost an identity mapping:

```text
G81F_TO_G8F
```

Expected behavior:

1. Parse the G8.1F DUF against the G8.1F DSF.
2. Evaluate the pose in G8.1F source space.
3. Map corresponding G8.1F bones to G8F.
4. Re-express rest-relative transforms in the G8F target rest basis.
5. Emit ordinary canonical G8F `.dazpose.json`.

G8.1F should be the first retarget implementation because it can validate the abstraction before solving the harder Genesis 9 topology differences.

## Genesis 9

Genesis 9 is a real retargeting problem, not a rename-table problem.

Do **not** attempt to solve G9 by deriving a scalar multiplier per bone or by copying Euler values from similarly named bones.

The preferred model is:

```text
Source DSF + source DUF
        ↓
source-generation evaluator
        ↓
evaluated source pose relative to source rest pose
        ↓
semantic retarget profile
        ↓
G8F target pose
        ↓
canonical G8F exporter
```

### One-to-one anatomical mappings

Where anatomy corresponds directly, map semantic bones and transfer their rest-relative orientation.

Conceptually:

```text
G9 hip        → G8F hip
G9 pelvis     → G8F pelvis
G9 l_shin     → G8F lShin
G9 l_foot     → G8F lFoot
G9 l_hand     → G8F lHand
G9 head       → G8F head
```

The important transform is not:

```text
targetEuler = sourceEuler * factor
```

It is conceptually:

```text
source pose delta relative to source rest
        ↓
re-express delta in target rest basis
        ↓
apply delta to target rest pose
```

Use quaternion/matrix basis transforms.

### Chain mappings

Some G9 structures do not correspond one-to-one with G8.

Examples include twist-helper distribution and spine organization.

Treat these as anatomical chains rather than literal helper-bone renames.

Example concept:

```text
G9 thigh segment:
    main thigh
    twist helpers
        ↓
extract bend/swing + axial twist
        ↓
distribute over:
    G8 thigh bend
    G8 thigh twist
```

Do the same where needed for upper arms, forearms, torso, and neck.

### Translation policy

Do not force G8F bones to occupy literal G9 world positions.

Default policy should preserve G8F proportions and bone lengths while transferring pose orientation.

Root/hip translation may be transferred with an explicit scale policy such as relative figure height or leg length.

Translated child bones should be handled conservatively and only after real fixtures demonstrate the need.

## Validation fixtures

Retargeting should be validated with deliberately chosen poses, not fitted from them.

Useful validation cases:

- neutral/rest
- ordinary standing
- arms overhead
- deep torso twist
- crouch/kneel
- seated
- strong leg twist
- detailed hand pose
- asymmetric pose

Sample poses are tests, not the mathematical source of the retarget transform.

## Proposed abstraction

Conceptually:

```text
RetargetProfile
    SourceGeneration
    TargetGeneration

    BoneMappings[]
    ChainMappings[]
    TranslationPolicy
    TwistPolicy
```

Likely profiles:

```text
G8F_NATIVE
G81F_TO_G8F
G9_TO_G8F
G8M_TO_G8F
G81M_TO_G8F
```

## Deferred status

**DEFERRED.**

Recommended sequencing when eventually authorized:

1. G8.1F → G8F
2. high-quality in-app preview if not already implemented
3. G9 → G8F
4. G8M / G8.1M

Do not implement retargeting during Stage 5.

---

# 2. Embedded Unity 3D Preview in DazPoseWizard

## Goal

Provide a high-quality live pose preview inside the DazPoseWizard desktop app rather than relying only on low-resolution DAZ thumbnail images.

The final preview needs:

- orbit
- zoom
- useful default framing
- enough rendering fidelity to judge whether a pose actually works
- the same target skeleton/rendering environment used by the Unity game pipeline

A fixed still image is not sufficient as the final design because a single camera angle can make a good pose look bad or conceal a bad pose.

## Architectural decision

Do **not** rewrite the whole DazPoseWizard application in Unity.

The existing Avalonia application is the correct home for:

- library indexing
- SQLite metadata
- Windows-style folder trees
- search
- filtering
- thumbnail browsing
- conversion queues
- drag/drop
- Unity destination management

Instead, embed a Unity player/rendering surface inside the Avalonia application for the 3D preview portion.

Conceptually:

```text
Avalonia DazPoseWizard
┌──────────────────────────────────────────────┐
│ DAZ tree │ pose browser │ Unity destination │
│          │              │                   │
│          │   selected   │                   │
│          │     pose     │                   │
├──────────────────────────────────────────────┤
│        EMBEDDED UNITY PREVIEW                │
│                                              │
│        orbit / zoom / lighting               │
└──────────────────────────────────────────────┘
```

## Preferred behavior

On click or hover-selection:

```text
selected DUF
    ↓
DazPose.Core evaluation / retarget if needed
    ↓
temporary canonical G8F pose representation
    ↓
embedded Unity player
    ↓
pose applied to reference G8F
```

The preview should not require generation of a permanent `.anim` merely to inspect a pose.

The embedded Unity preview is particularly useful before/while implementing retargeting:

```text
click G9 pose
    ↓
run G9 → G8F retarget candidate
    ↓
preview immediately
    ↓
orbit / zoom / inspect
```

This avoids repeatedly:

```text
convert
write asset
switch to Unity
import
open scene
inspect
return to converter
repeat
```

## Integration preference

If Unity integration is introduced for preview, prefer embedding the actual Unity player rather than building a separate custom .NET/OpenGL skinning renderer.

Do not duplicate:

- Unity skinning
- material behavior
- blendshape deformation
- lighting
- camera behavior
- mesh rendering

inside Avalonia unless embedding Unity proves technically unacceptable.

## Preview character

Use a known G8F reference character specifically prepared for DazPoseWizard preview.

The Stage 5 proof used the prepared “fat reference Lara” as the master character because it contains the blendshape superset needed by the test poses.

The preview implementation itself remains deferred.

## Deferred status

**DEFERRED.**

Preferred timing:

> Implement this before serious Genesis 9 retarget tuning if retarget work begins.

Keep this feature deferred unless a future ticket explicitly authorizes it.

---

# 3. Per-Destination “Unconvert” / Removal Workflow

## Goal

Allow a source DAZ pose that has already been converted to be removed from one or more Unity destinations without touching the original DAZ source files.

DAZ source content remains read-only.

Never delete, rename, move, or modify the original:

```text
.duf
.png
```

## User interaction

A converted source pose may exist in multiple Unity destination folders.

When the user chooses to unconvert/remove it, show all existing destinations.

Example:

```text
Vintage Glamour - Pose 03

Converted to:

[x] Sitting/Romantic
[ ] Standing/Reference
[x] Cutscene/Bedroom

[Cancel] [Remove Selected]
```

Removal is therefore **per destination**.

A separate explicit “remove all conversions” action may be added later, but it should still show the destination list before proceeding.

## Deletion ownership

The Avalonia application must **not** manually delete Unity `.anim` files and `.meta` files from disk.

Preferred flow:

```text
DazPoseWizard
    ↓
write removal request
    ↓
Unity notices request
    ↓
perform dependency/reference preflight
    ↓
Unity AssetDatabase deletion
    ↓
remove canonical JSON through Unity-safe asset handling
    ↓
status returned to DazPoseWizard
```

Let Unity own Unity asset deletion.

Do not manually manipulate `.meta` files.

## Assets associated with one conversion

Normally one destination conversion owns:

```text
Assets/DazPoseImports/<destination>/<name>.dazpose.json

Assets/<ConfiguredPoseRoot>/<destination>/<name>.anim
```

plus DazPoseWizard status/report data outside Assets.

A successful removal should clean the destination's generated artifacts and status/registry records.

It must not remove the original source DUF.

## Reference/dependency preflight

Before deleting a generated `.anim`, Unity should determine whether project assets refer to it.

At minimum inspect ordinary serialized project dependencies such as:

- scenes
- prefabs
- AnimatorControllers if any later exist
- ScriptableObjects
- other Unity assets

If no known references exist, deletion can proceed normally.

If references are found, default behavior is to block removal and display them.

Example:

```text
Cannot safely remove:

Vintage Glamour - Pose 03.anim

Referenced by:
Assets/Scenes/TestScene.unity
Assets/Characters/Lara.prefab
Assets/Performances/Intro.asset
```

## Delete Anyway

Reference discovery should block by default, but provide an explicit second-stage:

```text
Delete Anyway
```

after the user has seen the warnings.

This is intentionally destructive.

Do not hide the consequences behind a generic Yes/No dialog.

## Unity closed

If Unity is not running:

```text
Removal Requested
Awaiting Unity
```

When the project next opens, Unity processes pending removals.

If references prevent deletion, DazPoseWizard should receive/report the blocked state and reference list.

## Limit of dependency checking

A normal Unity dependency scan can detect serialized asset references.

It cannot guarantee discovery of every possible runtime lookup implemented through arbitrary strings, custom address systems, external databases, or dynamically constructed paths.

Warnings should describe what Unity found without claiming absolute proof that an asset is unused.

## Deferred status

**DEFERRED.**

Do not implement removal/unconvert during Stage 5.

---

# 4. Master “Fat Lara” vs Lean Runtime Character

## Goal

Avoid returning to DAZ and re-exporting the entire character every time the project discovers one more useful expression or body morph.

## Authoring/reference character

Maintain a deliberately large master G8F reference FBX containing a broad superset of direct morphs/blendshapes likely to be useful.

Conceptually:

```text
MasterG8F_AllUsefulMorphs.fbx
```

This asset prioritizes authoring convenience over shipping efficiency.

Useful categories may include:

- facial expressions
- eye/brow/mouth shapes
- body-shape morphs that may be used by the game
- other direct morphs likely to be animated
- specialty morphs that successfully export and are relevant

Do not equate every DAZ slider with a usable Unity blendshape.

DAZ may contain:

- direct morphs
- hidden correctives/JCMs
- high-level pose controls
- ERC/formula-driven controls
- morphs associated with grafts
- content that does not export as a normal FBX blendshape

The master reference should contain the broadest useful, successfully exported set rather than blindly assuming every installed DAZ slider should become a Unity shape.

## Near-term use

During Stage 5 and early game development, using the fat master directly is acceptable.

Do not prematurely optimize the runtime character simply because the reference FBX is large.

The immediate priority is:

> discover which morphs and expressions are useful without repeated DAZ export/setup work.

## Future lean runtime mesh

If the master becomes too large for shipping/runtime use, add a later optimization pipeline.

Conceptually:

```text
FAT MASTER FBX
    contains large blendshape superset
           ↓
DazPoseWizard / Unity tooling
    gathers blendshapes actually referenced
           ↓
generated lean runtime Mesh
    contains only required blendshape frames
```

The required set can be derived from converted animation/expression assets and explicit project requirements.

Example:

```text
master: 1200 blendshapes
project currently uses: 53
        ↓
runtime mesh contains those 53
```

The generated runtime mesh should preserve the existing character setup wherever practical:

- skeleton
- bindposes
- renderer relationship
- materials
- textures
- submeshes

The goal is specifically to avoid repeating:

```text
go to DAZ
re-export FBX
re-import character
redo materials
redo references
```

for every newly discovered expression.

## Regeneration model

When a newly used blendshape appears:

```text
required set: 53 → 54
        ↓
regenerate lean runtime mesh
```

rather than returning to DAZ, provided that shape already exists in the fat master.

## Deferred status

The **fat master reference convention is active now** because it directly supports Stage 5.

The **automatic lean-runtime pruning/generation pipeline is DEFERRED**.

Do not build the pruning tool during Stage 5 unless separately authorized.

---

# 5. Explicitly NOT Deferred: Stage 5 Blendshape/Morph Proof

This document does **not** defer the next planned work.

Stage 5's minimum real direct-blendshape workflow is complete, and the user visually approved the FUNtasy direct application and generated clip. The implementation is limited to exact direct blendshape mappings.

The minimum purpose of Stage 5 is to prove one real mixed skeletal+morph pipeline.

Target proof:

```text
DAZ preset
    ↓
skeletal transform data
+
active morph/blendshape data
    ↓
canonical representation
    ↓
Unity native AnimationClip
    ↓
Transform curves
+
SkinnedMeshRenderer blendshape curve(s)
```

The proof used the real `!!FUNtasy Face.duf` fixture and reduced `lara.fbx` reference. DAZ export remains a manual checkpoint.

Do not expand Stage 5 into:

- all DAZ morph/control semantics
- geograft-specific support unless needed by the chosen fixture
- retargeting
- embedded preview
- runtime mesh pruning
- unconvert/delete workflow

Those remain separate concerns.

## Character refresh follow-up (deferred)

Future reference-character refresh tooling may enforce material remaps, known FBX import settings, reference prefab setup, and accessory setup. Stage 5 only preserved the existing asset identity and verified that the validation reference remained usable; it did not automate production material repair.

---

# 6. Deferred-Work Priority / Suggested Future Order

This is guidance only, not authorization.

After Stage 5 and the pivot back to game development, the likely useful order is:

1. **Embedded Unity 3D preview**
   - especially valuable before retargeting
   - orbit/zoom required
   - keep Avalonia as the application shell

2. **G8.1F → G8F retargeting**
   - low-risk proof of retarget architecture

3. **G9 → G8F retargeting**
   - semantic chain retarget
   - use embedded preview heavily during tuning

4. **Per-destination unconvert/removal**
   - Unity-owned deletion
   - reference preflight
   - explicit Delete Anyway

5. **G8M / G8.1M retargeting**
   - after female-generation paths are proven

6. **Lean runtime blendshape mesh generation**
   - only when fat-master size/import/runtime cost creates a real reason to optimize

This order can change based on actual game-development needs.

---

# 7. Deferred-Work Guardrail

Nothing in this document should be interpreted as permission for an agent to implement any deferred feature.

Agents working on DazPoseWizard should treat these sections as architectural context only.

Do not:

- start retargeting because generation filters already exist in the browser
- add Unity embedding while fixing thumbnails
- add delete APIs while adjusting conversion status
- build blendshape pruning while implementing Stage 5
- widen Stage 5 into all morph/ERC/geograft cases

Implementation begins only when the user explicitly authorizes a future stage/ticket covering that feature.

---

# 8. Current Cutoff Intent

The intended near-term sequence is:

```text
Phase 4 browser pipeline
        ↓
complete

post-Phase-3 parser compatibility correction
        ↓
complete / validate as appropriate

Stage 5 blendshape/morph proof
        ↓
complete minimum mixed-pose pipeline

write/update validation documentation
        ↓
STOP expanding DazPoseWizard

return primary focus to the Unity game/test scene
```

DazPoseWizard should now serve the game rather than becoming the main project indefinitely.
