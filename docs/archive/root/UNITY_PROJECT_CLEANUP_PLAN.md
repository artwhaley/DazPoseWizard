# Unity project cleanup and reorganization plan

Recorded October 17, 2026.

Status: planning and recommendations only. This document records the full read-only
audit and proposed cleanup plan for working through together later. Saving this
document does not authorize implementation, asset deletion, scene changes, Unity
Play, automated playback, builds, or player launches. The manual-testing boundary
and preservation requirements in AGENTS.md remain authoritative.

I recommend a staged cleanup that leaves a small production project, one coherent example, and the authoring tools you actually use.

The main problem is **ownership and placement**: working production assets still live in folders named `TestData`, `Generated`, `PartnerProof`, and `Acceptance`. Deleting those folders wholesale would break current content.

The audit made no project changes and ran no Unity playback, tests, builds, or players.

The audit covered the active Unity project's 36 scenes, 3,719 asset files excluding `.meta` files, serialized GUID references, editor menus, import/configuration paths, assembly boundaries, and ignored review assets. Recommendations below distinguish direct retirement candidates from items that need dependencies disentangled first. Unity's imported dependency database should be checked before eventual deletion.

**The most important findings are these:**

- The clean `VoidEnvironment` still uses art under `FirstPerformanceVoid`.
- The prepared production male body still depends on `PartnerProof` and `TestCharacter/playererect.fbx`.
- Production wardrobe data still references `Remediation/R03/second-outfit`.
- One `WardrobeRuntime/DraftAttempts` folder is actively referenced.
- The draft and released wardrobe configurations contain **different layer assignments**. They are not interchangeable duplicates.
- `ClothingSetup` depends on a component named `ClothingSetupPreviewRunner` in the validation assembly.
- The normal editor assembly depends on validation and example assemblies.
- Several historical migration hooks still execute on editor reload or scene opening, despite their menus being hidden.
- The outfit review browser reads its published review-scene path from a `TestOutput` report. Some "test output" currently serves as authoring metadata.

Those are the dependencies I would resolve before large folder moves.

All asset paths below are relative to `validation/DazPoseUnityValidation/Assets/`.

The scene-by-scene recommendations follow. **"Remove" means remove from the active project after the stated prerequisite and a recoverable snapshot. Protected originals remain preserved.**

| Scene | Recommendation | Reason or prerequisite |
|---|---|---|
| `Production/Scenes/MainMenu.unity` | **Keep** | Actual application entry point. |
| `Production/Scenes/FirstContactD1D5.unity` | **Keep; eventually rename `FirstContact.unity`** | It now runs the full session. Update path consumers while preserving scene/actor identities and saved bindings. |
| `Production/Environments/VoidEnvironment.unity` | **Keep** | Canonical clean composition for new scenes. |
| `Scenes/ClothingSetup.unity` | **Keep; refactor into an authoring workspace** | This is a real tool, although its implementation still resembles a validation fixture. |
| `Production/Scenes/AuthoringShowcase.unity` | **Keep as the surviving example** | Move out of production scenes after removing its inherited fixture hierarchy. |
| `Scenes/FirstPerformanceVoid.unity` | **Keep frozen at its existing path during the first cleanup** | Protected artistic source; the template extraction fallback still names it. |
| `Production/Scenes/RemediationToeValidation.unity` | **Hold** | Preserve the pending user visual observation and its exact fixture. Retire afterward, retaining evidence. |

These scenes can be retired or merged:

| Scene | Recommendation | Prerequisite |
|---|---|---|
| `Production/Scenes/AuthoringWelcome.unity` | **Remove** | Preserve its minimal authoring example in documentation and initializer checks. Keep the `WelcomePerformance` fallback where initialization still uses it. |
| `Production/Scenes/AuthoringCompanion.unity` | **Merge into the surviving example; remove scene** | Retain movement, seating, action, magic, and wardrobe no-op coverage. |
| `Production/Scenes/Day2ActorValidation.unity` | **Remove after dependency repair** | `ClothingSetupBuilder` currently uses it as its environment source. |
| `Production/Scenes/Day3CommandValidation.unity` | **Remove after extracting current regressions** | Historical command fixture with an old performance controller and synthetic contact setup. |
| `Production/Scenes/Day4PartnerContactValidation.unity` | **Remove after extracting contact coverage** | Preserve measured contact behavior and meaningful solver checks. |
| `Production/Scenes/Day5WardrobeValidation.unity` | **Merge coverage into Clothing Setup/regression tests; remove scene** | Avoid maintaining another independent wardrobe stage. |
| `Production/Scenes/ProductionRuntimeSmoke.unity` | **Remove** | Historical smoke composition containing an expanded old actor and dedicated runner. |
| `Production/Scenes/RemediationValidation.unity` | **Remove after current-contract coverage is retained** | Historical remediation fixture. Keep its reports and failure history. |
| `Scenes/PoseValidation.unity` | **Remove after pose regressions are relocated** | Normal importing already operates without this scene. |
| `Scenes/HandGripAcceptance.unity` | **Merge into one development lab; remove scene** | Retain useful manual contact inspection and solver coverage. |
| `Scenes/PartnerRigAcceptance.unity` | **Merge into that lab; remove scene** | Preserve preparation inputs, measurements, and endpoint evidence separately. |
| `Scenes/LaraHumanoidKawaiiTest.unity` | **Remove** | One-purpose retargeting demonstration. Preserve the proxy and baking inputs still used by locomotion/seating tools. |
| `Scenes/AnimationAudit.unity` | **Merge browser/preview functionality into an authoring window; remove scene** | A permanent scene should not be required merely to browse animations. |

The ignored review scenes need more careful handling:

| Scene | Recommendation |
|---|---|
| `TestData/GraftPairProbe/GraftPairPreview.unity` | **Remove from active project** after preserving probe evidence and checking its recovered mesh's remaining consumers. |
| `TestData/LaraCandidate/LaraCandidatePreview.unity` | **Remove** once the supported inspection workflow replaces it. |
| `TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity` | **Keep for now; replace its importer role first.** Current wardrobe recipes explicitly use it as the canonical character scene. |
| `TestData/LaraCandidate/FirstOutfit/FirstPerformanceVoidLaraFirstOutfit.unity` | **Preserve as an accepted historical reference.** Eventually place in a recoverable reference archive, with dependencies. |
| `TestData/LaraCandidate/SecondOutfit/FirstPerformanceVoidLaraSecondOutfit.unity` | **Preserve as an accepted historical reference.** Same treatment. |
| `TestData/Wardrobe/first-outfit/Review.unity` | **Keep until shared Clothing Setup can review this package directly.** Then retire the generated scene. |
| `TestData/Wardrobe/second-outfit/Review.unity` | **Same consolidation.** |
| `TestData/Wardrobe/second-outfit/Review-20261004-155507.unity` | **Preserve published review history.** Update the review-browser pointer before removing it from the active project. |
| `TestData/Wardrobe/third-outfit/Review.unity` | **Keep until shared package review replaces it.** |
| `TestData/Wardrobe/third-outfit/Review-20261004-165635.unity` | **Preserve published/approved review history.** Update its browser pointer before archival. |

I would remove these six vendor demonstration scenes from the active project, retaining any shared assets needed by supported tools:

| Scene |
|---|
| `Crazy Minnow Studio/Examples/Scenes/SALSA-basic3D-boxhead.unity` |
| `FemaleMovementAnimsetPro/Scenes/FemaleDemo.unity` |
| `FemaleMovementAnimsetPro/Scenes/FemaleTopDownDemo.unity` |
| `FemaleMovementAnimsetPro/Standard Assets/Effects/TessellationShaders/Scenes/TessellationSample.unity` |
| `INab Studio/Demo Assets/StarterAssets/Environment/Art/Demo Scene.unity` |
| `KAWAII_ANIMATIOMS_100/Demo/KAWAII_ANIMATIOMS_100.unity` |

There is also a separate, ignored Unity project at `unity/SubtranceB`, containing `Assets/OutdoorsScene.unity`. I would archive that project's source composition and remove it from the working repository directory once its intended use is recorded. Its `Library` alone occupies approximately 3.3 GiB.

The desired everyday scene list would therefore be **Main Menu, First Contact, Clothing Setup, and one example**, with the clean Void template and protected references kept separately. A single development lab is justified if it supports manual inspection that ordinary scenes cannot provide.

The asset deletion candidates are more selective than the scene list.

| Asset or subtree | Recommended disposition |
|---|---|
| `Content/Remediation/Folder With Spaces/20261006-125650-623/FolderIndependentAction.anim` and `.asset` | **Delete generated fixture assets.** Retain the path-independence regression as a temporary test fixture. |
| Same folder: `FolderIndependentGesture.anim` and `.asset` | **Delete generated fixture assets.** Together, these four files occupy about 91.8 MiB. |
| `Production/ValidationData/Day3Face.anim` | **Delete with the retired Day 3 fixture**, after retaining relevant expression coverage. |
| `Production/ValidationData/Day3Expression.asset` | **Same disposition.** |
| `PartnerProof/PartnerCharacter.prefab` | **Retire the obsolete proof prefab** after the preparation tool stops targeting it. It has no serialized incoming references in the scanned assets. |
| `PartnerProof/Generated/PartnerProof_Body.mat` | **Delete obsolete proof material** after preparation-path cleanup. |
| `PartnerProof/Generated/PartnerProof_Shell.mat` | **Same disposition.** |
| `FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_Arms.mat` | **Delete obsolete generated skin material** after retiring its historical generator paths. |
| Same folder: `M_LaraSkin_Ears.mat` | **Same disposition.** |
| Same folder: `M_LaraSkin_EyeSocket.mat` | **Same disposition.** |
| Same folder: `M_LaraSkin_Face.mat` | **Same disposition.** |
| Same folder: `M_LaraSkin_Legs.mat` | **Same disposition.** |
| Same folder: `M_LaraSkin_Lips.mat` | **Same disposition.** |
| Same folder: `M_LaraSkin_Torso.mat` | **Same disposition.** These seven materials have no serialized incoming references in the scan. |
| `Production/Wardrobe/Candidate-first-outfit.asset` | **Delete abandoned candidate**, after snapshotting authoring state. |
| `Production/Wardrobe/Candidate-second-outfit.asset` | **Same disposition.** |
| `Production/Wardrobe/Candidate-third-outfit.asset` | **Same disposition.** These three have no serialized incoming references. |
| `Production/Wardrobe/Day5-20261006-001818/` | **Retire with its Day 5/remediation fixtures**, after checking the pending toe fixture's references. This includes its catalog, preview catalog/configuration, and three preview presets. |
| `Production/Wardrobe/Remediation/R03/ToeFixture/` | **Retire after the pending toe observation and fixture retirement.** |
| `Production/Wardrobe/Remediation/R03/third-outfit/` | **Retire fixture-only generated fit data after the same checks.** |
| `Generated/A.mp3`, `B.mp3`, `C.mp3` | **Remove after surviving examples have their own intentional audio bindings** and old speech fixtures are retired. |
| `Production/Story/Programs/FirstContactD1D5.story.json` | **Remove from active content.** Preserve the historical example/schema in documentation. The saved current sequence uses `.dazstory`. |

There are also several large abandoned working folders worth targeting individually:

| Folder under `TestData/WardrobeRuntime/` | Size | Recommendation |
|---|---:|---|
| `DraftAttempts/02aa66ebd298461cb41043fd44d637e1/` | 136.3 MiB | Remove after checking retained historical references. |
| `DraftAttempts/94c58ce304554ea4bef67c908ad40e6c/` | 136.3 MiB | Same. |
| `DraftAttempts/ffc251e0c3f54ba5b4d2306f9d6a7c98/` | 204.8 MiB | Same. |
| `SetupCandidates/685f686eff13122c46493b5558faed839b468b0bf5bca4cf471f025637a21854/` | 136.3 MiB | Same. |
| `DraftAttempts/a6595a7b018d4a14937b9c70e9c3b8f7/` | 170.5 MiB | **Keep and promote its referenced data.** Fourteen files remain in the checked live dependency closure. |

The first four total approximately **614 MiB**. This is a candidate saving, contingent on preserved-reference checks.

I would also consolidate these related animation assets:

- `DazPose/Generated/Actions/Acceptance/JumpForJoy_Action.anim`
- `Production/Content/Assets/Celebration-Clip.anim`
- `Content/Remediation/.../FolderIndependentAction.anim`

Each animation is roughly 92 MiB. They are related generated copies; I have not established byte identity. Keep the intentional prepared content needed by the surviving example, remove the fixture copy, and avoid cloning large clips solely to demonstrate folder independence.

These asset groups must remain available and should be reorganized into production ownership:

| Current location | What to keep | Recommended destination or treatment |
|---|---|---|
| `FirstPerformanceVoid/Materials/` | `M_DarkAccent`, `M_NeonMagenta`, `M_PerimeterSmoke`, `M_VoidGlossBlack` | `Production/Environments/Void/Materials/` |
| `FirstPerformanceVoid/Meshes/` | `StageGlowRing.asset` | Same environment ownership. |
| `FirstPerformanceVoid/Prefabs/` | `ValidatedLoungeSeat.prefab` | Production environment/props; rename only after path consumers are updated. |
| `FirstPerformanceVoid/Settings/` | `V_FirstPerformanceVoid.asset` | Production Void volume profile. Preserve authored settings. |
| `FirstPerformanceVoid/Shaders/` | `PerimeterSmokeLit.shadergraph` and required license | Production Void shaders. |
| `FirstPerformanceVoid/Textures/` | `T_FogDensity32.asset`, `T_SmokeBillow.asset` | Production Void textures. |
| `PartnerProof/Generated/` | `G8M_ErectBody.asset`, `G8M_ErectShell.asset` | Production prepared male geometry. |
| `PartnerProof/` | `PartnerShell.shadergraph` | Production male materials/shaders. |
| `TestData/PartnerCharacter/` | Runtime materials, textures, preparation metadata | Production male support data plus clearly separated source/preparation records. |
| `TestData/LaraCandidate/` | Canonical body, surface bindings, dissolve profile, runtime materials/textures, required manifest | Production Lara support data; replace importer path assumptions before moving. |
| `Animations/Performer/Locomotion/` | Baked walking/turning clips and profiles | Production performer motion library. |
| `Animations/Performer/Seating/` | Baked seating clips and profiles | Same library. |
| `DazPose/Generated/Motion/Acceptance/` | `BasicStroke.anim`, `.asset`, `BasicStrokeSet.asset` | Retain their currently referenced functionality; make their production/library status explicit. |
| `DazPose/Generated/HandGrip/Profiles/` | `LaraRightHandGrip.asset` | Production Lara contact configuration. |
| `DazPose/Generated/Gestures/` and `Motion/` | Gesture and arm masks | Production performer configuration. |
| `DazPose/Generated/Magic/` | Reusable catalog, styles, spells, auras | Production content library. Current-story usage alone is insufficient reason to discard supported content. |
| `Generated/DazPoses/G8F/` | The currently used initial-pose wrapper and clip | Consolidate into the configured pose library, preserving GUIDs. |
| Root `motiondrive.funscript`, `teleportsound.mp3` | Referenced motion/audio | Give them clear content-library ownership. |

**Do not remove `Production/Wardrobe/Remediation/R03/second-outfit/`.** Its body, fit-state, dissolve, and surface-binding assets remain live dependencies. The misleading folder name needs correction; the data is needed.

Similarly, keep both immutable wardrobe generations currently present. A generation with no current production-scene reference may still be required by protected reference content and recovery history.

The draft `Wardrobe/WardrobeConfiguration.asset` and released `Production/Wardrobe/LaraConfiguration.asset` should remain distinct. For example, their first-outfit garment layer assignments differ. Rename and organize them as **authoring draft** and **released configuration**, preserving the user's choices.

The current story assets also stay together:

- `FirstContact.dazstory`
- `FirstContact.bindings.asset`
- `FirstContact.dialogue-map.json`
- `FirstContactMarks.prefab`
- All 19 supplied dialogue recordings and their metadata
- The six assigned imported poses
- Engineering Disapproval gesture and both engineering funscripts
- Wireframe/regular presentation profiles

The `.dazstory` importer loads the companion bindings by filename. A plain GUID scan therefore understates that dependency. Its implementation is in [DazStoryImporter.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/DazStoryImporter.cs).

For licensed/source content, I recommend preservation and separation:

| Folder | Approximate size | Recommendation |
|---|---:|---|
| `TestCharacter/` | 3.33 GiB | Preserve source exports and sidecars. Eventually separate production-required imported models from import/preparation inputs. |
| `TestData/` | 2.73 GiB | Split live prepared assets, authoring working data, immutable releases, and disposable fixtures. |
| `Animations/` | 1.07 GiB | Keep the prepared library; consolidate duplicate roots and isolate baking intermediates. |
| `KAWAII_ANIMATIOMS_100/` | 368 MiB | Remove demonstration content; preserve the animation sources still named by baking tools. |
| `FemaleMovementAnimsetPro/` | 80 MiB | Candidate for removal from the active project after preserving the licensed package and confirming authoring-tool consumers. |
| `INab Studio/` | 345 MiB | Remove isolated demos selectively. Preserve actual dissolve shaders, graphs, textures, and runtime support. |
| `Daz3D/` | 54 MiB | Keep bridge/import support and referenced assets. |
| `Plugins/Crazy Minnow Studio/` | — | Keep SALSA runtime/editor integration; remove isolated example support where safe. |

`TestCharacter/lara.fbx` is still the configured pose-import reference model. `playererect.fbx` remains a production dependency. Neither should be classified as disposable because it sits under `TestCharacter`.

The menu audit found **22 normal DazPose commands**, plus **60 legacy menu attributes representing 59 commands and one validation attribute**. The legacy entries are already gated by `DAZPOSE_DEVELOPMENT_TOOLS`; the saved Standalone defines currently contain only `CMS_SALSA`. Thus, most historical commands are already hidden, although their supporting code still compiles.

For the normal menus, I recommend:

| Current command(s) | Disposition |
|---|---|
| `Scene/New Performance Scene...` | Keep. |
| `Scene/Initialize Game Session` | Keep. |
| `Scene/Add Lara Performer` | Keep. |
| `Scene/Add Prepared Player Body` | Keep. |
| `Scene/Add Partner` | Keep; make its additional-partner role clear. |
| `Scene/New Sequence Script...` | Keep as an advanced C# extension command; primary story authoring should point to `.dazstory`. |
| `Content/Reconcile Pending Pose Imports` | Keep. |
| `Content/Bake Selected Clip as Action` | Keep. |
| `Content/Bake Selected Clip as Gesture` | Keep. |
| `Settings/Pipeline Settings` | Keep. |
| `Game/Open Main Menu` | Keep. |
| `Game/Add Current Scene to Scene List` | Keep; eventually make catalog editing easier to discover. |
| `Game/Build Windows Player...` | Keep as an explicit user action. |
| `Help/Content Authoring API` | Keep; link the current story guide from the same help surface. |
| `Help/Authoring Build Plan` | Remove from the normal authoring menu; retain the historical document. |
| `Wardrobe/Imported Outfits` | Keep as the package/library entry point. |
| `Wardrobe/Open Clothing Setup` and `Wardrobe/Outfit Configuration` | Merge into one clear Clothing Setup workflow that opens its scene and editing window together. |
| `Story/Build or Refresh First Contact` | Merge with the following setup commands. |
| `Story/Prepare First Contact Authoring` | Merge; it currently shares its implementation with the preceding command. |
| `Story/Prepare Full First Contact Session` | Retain as one explicit advanced setup/repair operation. |
| `Story/Assign Missing First Contact Poses from Imports` | Remove the one-time random assignment command from normal authoring. Preserve existing assignments and any narrowly useful repair implementation. |

The four overlapping story commands are the clearest remaining ordinary-menu clutter. Their relationship is visible in [StorySceneBuilder.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/StorySceneBuilder.cs) and [StoryFullSessionSetup.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/StoryFullSessionSetup.cs).

For completeness, these are the legacy menu commands I would remove entirely as commands. Every entry below currently begins with `DazPose/Development/Legacy/`.

| Submenu | Commands to retire |
|---|---|
| Root | `Setup or Refresh Validation Scene`; `Open Pose Validation Scene`; `Setup or Refresh Performer Pose Acceptance Harness` |
| Root | `Open Lara Candidate Preview`; `Copy Accepted Lara Skin Response`; `Lara Candidate Controls` |
| Root | `Open First Performance Void Lara Candidate`; `Replace Lara In First Performance Void` |
| Root | `Open Lara First Outfit`; `Open Lara Second Outfit`; `Lara Outfit Review Controls` |
| Root | `Copy Bridge Materials to Selected Lara`; `Open Graft Pair Preview` |
| `First Performance Void` | `Install First Contact Performance`; `Build or Open Lounge`; `Refresh Generated Materials and Volume Defaults` |
| `First Performance Void` | `Apply Lara Lighting and Fog Corrections`; `Migrate to Directable Player View`; `Copy Current Smoke Front to Other Edges` |
| `First Performance Void` | `Install Dissolve Acceptance Harness`; `Install Particle Body Acceptance Harness`; `Install Teleport Acceptance Harness` |
| `First Performance Void` | `Rebuild Particle Body VFX Graph` |
| `Magic` | `Generate Starter Magic Assets` |
| `Motion` | `Generate BasicStroke and Install Acceptance Harness` |
| `Action` | `Generate Jump for Joy Acceptance Assets` |
| `Gesture` | `Generate Gesture Acceptance Assets` |
| `Animation Audit` | `Prepare and Open Audit Scene`; `Open AnimationAudit Scene`; `Create or Refresh Simple Humanoid Test` |
| `Handjob` | `Create Acceptance Scene` |
| `Partner Proof` | `Build Acceptance Scene` |
| `Production` | `Create Lara performer assets (new paths only)`; `Rebuild actor validation in authored void`; `Bind player and toy references in selected scope` |
| `Wardrobe Setup` | `Prepare and Open Clothing Setup` |

Removing a command does **not** automatically mean deleting every method in its source file. Several historical builders contain helpers used by the current importer.

These legacy commands should lose their individual menu buttons while their useful checks move into the maintained regression suite:

| Submenu | Commands |
|---|---|
| `First Performance Void` | `Validate Native Dissolve Shader Setup`; `Run Dissolve Material Stability Check`; `Validate Particle Body Acceptance Harness` |
| `Handjob` | `Run Geometry and Solver Tests`; `Validate Target Driven Grip` |
| `Motion` | `Run Funscript Acceptance Checks` |
| `Partner Proof` | `Recheck Generated Endpoint Data` |
| `Toys` | `Run BT.1 Offline Registry Tests` |

These capabilities are worth retaining behind a compact advanced diagnostics/preparation surface:

| Current legacy command(s) | Treatment |
|---|---|
| `Validate Always-Export Morphs` | Keep import diagnostic. |
| `Diagnostics/Inspect Selected Character` | Consolidate into diagnostics. |
| `Diagnostics/Inspect Pose Bone Mapping` | Consolidate into diagnostics. |
| `Diagnostics/Compare Pose Clip Bindings` | Consolidate into diagnostics. |
| `Inspect Selected FBX Evidence` | Consolidate into diagnostics. |
| `Animation Audit/Report Humanoid Leg Setup` | Consolidate into animation diagnostics. |
| `Setup or Refresh Performer SALSA Lip Sync`; `Audit Performer SALSA Lip Sync` | Keep targeted preparation/audit functionality. |
| `Handjob/Open Calibration Window`; `Handjob/Capture Lara Right Hand Profile From Selection` | Keep calibration tools; use consistent contact terminology. |
| `Locomotion/Bake KAWAII Walk01 for Generic Lara` | Preserve baker; remove its dependence on an acceptance-scene workflow. |
| `Seating/Bake KAWAII Seating for Generic Lara`; `Seating/Edit Mode Preview` | Preserve baking/preview functionality in the animation authoring tools. |
| `First Performance Void/Bake Performer Surface Bindings` | Keep the reusable baker under production content preparation. |
| `Partner Proof/Prepare Skinned Character and Materials` | Keep preparation functionality after separating it from the proof prefab and runner. |

The Assets/Create menus also need naming consolidation: they currently spread across `DazPose`, `DAZ Pose`, and `Performer`. Keep legitimate content types under one `DazPose` hierarchy. Remove the development-only `Animation Audit Catalog`, `Pose Smoke Harness`, and `Pose Acceptance Checks` entries from ordinary creation menus when their fixtures leave the project.

The source refactors I would prioritize are narrowly tied to this cleanup:

1. **Make Clothing Setup a supported authoring workspace.** Separate preview startup from assertions and report writing. Its normal scene startup should belong to product code. Replace its `Day2ActorValidation` construction dependency with the clean saved environment and production Lara. See [ClothingSetupBuilder.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs).

2. **Extract shared importer helpers from historical builders.** `WardrobeImporter`, geometry utilities, and partner preparation still call methods in `LaraCandidateBuilder`, `LaraFirstOutfitBuilder`, and `LaraSecondOutfitBuilder`. Move reusable geometry/material/preparation functions first; retire their scene-building shells afterward.

3. **Remove normal editor dependencies on validation/examples.** `DazPose.Editor.asmdef` currently references both. Production tools should depend on runtime/content services; validation should depend on those tools. Preserve script GUIDs and assembly/type identities during the initial folder moves.

4. **Retire automatic historical migrations.** Audit and remove the old reload/scene-opening hooks in `FirstPerformanceVoidLighting`, `SmokeFrontSettingsCopy`, `LaraFirstOutfitBuilder`, `LaraSecondOutfitRepair`, and the partner `.once` request handlers once their supported responsibilities are extracted. Keep current pose reconciliation, wardrobe synchronization, and tuning persistence. Hiding a menu does not disable these hooks.

5. **Consolidate the surviving example's hierarchy.** Welcome, Showcase, and Companion still contain inherited `FirstPerformanceVoid`, `TeleportAcceptance`, and `PerformanceMarkers` objects. Retain only marks and objects used by the surviving example. Also extract `StoryFoundationBuilder.ContactStaging()`'s dependency on the Showcase scene before moving it.

6. **Remove examples from normal game navigation.** `SceneCatalog.asset` and Editor Build Settings currently include Welcome, Showcase, and Companion. The build helper constructs its scene list from the catalog, so changing Editor Build Settings alone is insufficient. See [GamePlayerBuild.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Editor/Application/GamePlayerBuild.cs).

7. **Move review identity out of `TestOutput`.** Store the published review pointer with package/authoring metadata. Then reports become safe to archive independently. The current dependency is explicit in [WardrobeImportReviewWindow.cs](../../../validation/DazPoseUnityValidation/Assets/DazPose/Editor/WardrobeImportReviewWindow.cs).

8. **Retire compatibility content separately from runtime APIs.** Remove the inactive JSON story asset and old `FirstContactPerformance` example after their historical consumers leave. Keep `PerformanceSequence`, `PerformanceHost`, scoped APIs, and the flat story interpreter. Any eventual removal of JSON compatibility code or obsolete adapters should be a separate, bounded change.

I would aim for this folder ownership:

```text
Assets/
  DazPose/
    Runtime/                 Application, performer, player, story, motion, toys
    Presentation/
    Editor/                  Supported authoring/import tools
    Tests/                   Maintained regression code and small fixtures

  Production/
    Application/
    Scenes/                  MainMenu, FirstContact
    Environments/Void/
    Performers/Lara/
    Player/
    Partners/
    Content/                 Reusable poses, actions, gestures, motion, effects
    Story/

  Authoring/
    Scenes/                  ClothingSetup
    Wardrobe/
      Drafts/
    Preparation/

  Examples/
    Scenes/
    Content/

  ThirdParty/                Where vendor packages tolerate relocation
  Resources/DazPose/         Existing application/catalog load points
```

I would leave existing immutable release paths in place during the first cleanup. New releases can adopt the final structure after publication and recovery tooling support it. Vendor folders can also remain where their importers expect them.

At repository level, keep one active Unity project. Moving `validation/DazPoseUnityValidation` itself should be a final naming change because scripts, test-project links, documentation, and tooling embed that path.

The documentation also needs one current entry point. The Unity README still directs users to retired `Tools > DAZ Pose` paths and old harness workflows. Preserve old plans and ticket packets as history, but clearly separate them from the current `.dazstory` guide, production scene instructions, and import manuals.

I would execute the cleanup in this order:

| Pass | Work | Completion condition |
|---|---|---|
| 1 | Capture the current saved scenes, source assets/metas, ignored inputs, authored settings, and dependency manifest | Current recovery snapshot exists alongside the protected October 6 baseline. |
| 2 | Remove redundant public menus and isolate old automatic migration hooks | Ordinary authoring exposes supported operations; historical hooks cannot silently alter content. |
| 3 | Extract importer helpers and Clothing Setup startup; repair editor assembly dependencies | Supported workflows no longer require historical fixture implementations. |
| 4 | Move live art, character, contact, motion, and wardrobe support assets with GUIDs preserved | Production content has clear ownership; code/configuration path consumers are updated. |
| 5 | Consolidate examples and regression coverage; retire obsolete scenes | One example and, if needed, one saved development lab replace the feature-by-feature scene stack. |
| 6 | Delete individually verified disposable assets and abandoned working folders | No retained serialized, importer, configuration, report-pointer, or source-path consumers remain. |
| 7 | Archive historical workspaces/review history and update documentation | Recovery remains possible; current instructions describe the delivered structure. |

The active `Assets` tree occupies approximately **8.43 GiB**. Separate storage includes roughly **7.6 GiB of Unity Library cache**, **1.5 GiB of builds**, and **77.8 GiB under `.dazposewizard`**. That last directory contains recovery, evidence, and isolated workspaces, so it requires its own retention inventory. I would never delete it wholesale. Likewise, `TestOutput` needs its review-pointer dependency fixed before broad pruning.

For eventual acceptance, first check compilation, saved references, GUID preservation, importer reconciliation, catalog paths, and protected artistic/release data. Then the user manually tests the exact saved First Contact scene, Clothing Setup, and surviving example in Unity. The pending toe observation and current First Contact visual approval remain separate from cleanup's technical checks.
