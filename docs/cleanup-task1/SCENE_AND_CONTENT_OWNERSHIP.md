# Current UI and Studio ownership

Source-first `.dazstory` import and source-owned companions are managed by SourceBindingReconciler. Scene Director Studio owns only disposable visual/rehearsal data unless the author explicitly changes a typed binding or commits a mark. Shared runtime UI prefabs/theme are under `Assets/Production/UI`; `Assets/Resources/DazPose/ProductionUi.asset` connects them. Settings and story use one profile presenter/transaction path. See [Studio guide](../story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md) and [current status](../p2-p3/PROGRESS.md). Native testing is pending; no live execution was performed.

# Current Unity scene and content ownership

The F-series follow-up retired all eleven obsolete feature scenes plus two generated-only preview scenes after caller extraction. Current asset inventory: fifteen scenes; no development-lab copies. See [active progress](../p2-p3/PROGRESS.md), [retirement report](../p2-p3/PHASE1_RETIREMENT_REPORT.md) and [complete scene matrix](../p2-p3/PHASE1_SCENE_DISPOSITION.csv). Source compiles/managed checks pass; new native Editor checks and human visual acceptance remain pending under the latest no-program/no-Unity-control boundary. Phase 2 UI and Phase 3 Studio implementation follow.

- **Gameplay:** `Assets/Production/Scenes/MainMenu.unity` is the application entry; `Assets/Production/Scenes/FirstContact.unity` is the full session. `Assets/Production/Application/SceneCatalog.asset` controls public navigation and future build paths. The original `FirstContactD1D5.unity` was renamed with its GUID and persistence identity preserved.
- **Authoring:** `Assets/Scenes/ClothingSetup.unity` is the persistent Clothing Setup scene and uses production `ClothingSetupStartup`. Mutable wardrobe drafts and immutable releases remain separate. `ProjectSettings/DazPose/WardrobeReviewMetadata.json` owns published review pointers by outfit/release/generation identity.
- **Examples:** `Assets/Examples/Scenes/AuthoringShowcase.unity` retains its saved host and runner. `Assets/Production/Scenes/AuthoringWelcome.unity` and `AuthoringCompanion.unity` were retired after their producers were detached and dependency checks passed. Their historic reports are evidence, not current scene instructions.
- **New-scene environment and frozen reference:** **DazPose > Scene > New Performance Scene...** copies the saved art-only `Assets/Production/Environments/VoidEnvironment.unity`; its ten shared art assets live in `Assets/Production/Environments/Void/`. `Assets/Scenes/FirstPerformanceVoid.unity` remains the untouched frozen original and is not a new-scene template.
- **Performer/player:** Lara speaks. `Assets/Production/Player/PlayerBody.prefab` uses the prepared male Partner and measured contact setup. Prepared mesh/shader support is under `Assets/Production/Player/Meshes` and `Assets/Production/Player/Shaders`. The prepared Partner prefab is `Assets/Production/Player/Support/PartnerCharacter.prefab`; its generated materials/textures now live under `Assets/Production/Player/Materials/Imported`. Lara core runtime data is `Assets/Production/Performers/Lara/{Rig,Surfaces,Configuration}`. Published magic, motion, gesture and audio content uses `Assets/Production/Content`.
- **Story:** local setup, Intro and sequence stay in `Assets/Production/Story/Programs/FirstContact.dazstory`; tagged global cards publish under `Assets/Production/Story/Cards/`. Each source uses its adjacent same-basename `.bindings.asset` companion. The source Inspector opens readable source or typed bindings. Typed companions bind source symbols to assets; named scene roles and locations resolve through the active scene bindings. Import discovers tagged cards and updates `Assets/Resources/DazPose/GlobalStoryCards.asset`; scenes query the shared pool by tags and requirements. A missing required scene resource rejects only that card. No per-scene registration is used.
- **Validation:** validation checks and validation-only helpers belong under `DazPose/Validation`. No validation scene appears in public navigation. Reload does not resume stale validation sessions. Use the saved First Contact or Showcase for explicit retained-host checks; validation-only request brokers do not inject Play.

## Protected and historical holdovers

Canonical source FBXs, configured pose-import roots, published review scenes, immutable wardrobe releases and pending second-outfit toe fixtures remain at their existing paths while live consumers or user decisions require them. Imported packages may temporarily remain under `Assets/TestData/Wardrobe/<id>`; the path name is historical, while release identity and user-edited materials/layers remain protected. The original frozen FirstPerformanceVoid scene, reference-loader scripts, remaining source data, and archived failed-expression recovery inputs also remain protected; the ten shared art assets already moved to Production/Environments/Void are tracked in completed-moves.json.

The `DraftAttempts` and `SetupCandidates` names remain as implementation roots while their current producer or serialized consumers exist. Two abandoned `DraftAttempts` generations (`02aa66ebd298461cb41043fd44d637e1` and `94c58ce304554ea4bef67c908ad40e6c`) were retired after recovery and consumer checks. `DraftAttempts/ffc251e0c3f54ba5b4d2306f9d6a7c98` remains protected because three Day5 previews retain distinct layer choices and reference its FitState assets; root independent GUID scanning confirmed these consumers. `SetupCandidates/685f686eff13122c46493b5558faed839b468b0bf5bca4cf471f025637a21854` remains tied to three preserved configurations by generation inputHashes. Superseded default-layer Candidate-first/third presets were retired; pending Candidate-second remains protected. See the follow-up F03 orphan audit for exact field differences. The live `DraftAttempts/a6595a7b018d4a14937b9c70e9c3b8f7` generation is retained. See [per-file recovery and reference audit](abandoned-draft-audit.json).

## Publish a selectable card

Use **DazPose > Story > New Global Card Source**. It creates a readable `.dazstory` source and adjacent typed binding companion below `Assets/Production/Story/Cards/`. For example:

```text
card QuietBreathing
tags [Warmup, Calm]
requires World.Posture == Sitting
progress 10
{
    wait 1s
}
```

Import discovers sources and refreshes the shared `Assets/Resources/DazPose/GlobalStoryCards.asset` index. Public tagged IDs must be globally unique; untagged helper IDs remain local to their source and are not random draws. A local `play Intro` beat is an explicit step in First Contact, while a public tagged card is selected by the Director from matching tags and runtime requirements. Source-local helpers, source assets and distinct typed bindings stay with each source; two sources may bind the same pose alias differently.

The source Inspector opens source text or selects typed bindings. Invalid published code or owned assets report source diagnostics and invalidate publication. Missing scene resources reject only the affected card. Fix source/bindings and reimport, or use **DazPose > Content > Advanced > Reindex Published Cards**. Removing a card declaration/source removes it from the index. Indexing notices binding edits, moves and removal and leaves unchanged output alone. The runtime keeps one machine, Director, history, random stream and cancellation scope per invocation.

First Contact full human visual acceptance, the second-outfit toe appearance review, player UI modernization, Scene Director Studio binding/placement/rehearsal, and three new authored scenes remain pending or deferred. The user performs manual Play in Unity; cleanup has not run Play, builds or players.

## Verified move reconciliation

The merged [completed move map](completed-moves.json) contains 163 unique original asset paths: the earlier 50 records plus 113 successfully executed records in `final-production-move-plan.json`, with no source overlap. GUIDs, byte counts, source/meta hashes and preservation flags are retained per asset. Use destination paths in that map as current locations; old roots in dependency snapshots are historical evidence and were not rewritten.

The 32 Partner runtime materials, 32 source materials and 21 textures now live under `Assets/Production/Player/Materials/Imported`; preserve canonical partner manifests and conversion inputs at their source roots. Three Lara core assets moved to `Assets/Production/Performers/Lara/Rig`, `Surfaces` and `Configuration`. Lara imported/source materials, textures and manifests remain HOLD because `WardrobeCatalogBuilder.CharacterSignature` hashes material asset paths. Keep canonical source FBXs, configured pose-import roots, mutable wardrobe candidates/catalog/configuration, immutable releases and the second-outfit toe fixtures protected.
## Remaining scenes after follow-up

The five normal scenes are MainMenu, FirstContact, ClothingSetup, AuthoringShowcase and VoidEnvironment. Frozen FirstPerformanceVoid, pending RemediationToeValidation, the three original accepted candidate/first/second outfit references, and five current/timestamped imported-outfit reviews are exceptional artistic/release evidence. `LaraCandidatePreview` and `GraftPairPreview` generated-only displays are retired; their mesh/morph diagnostics use the isolated Animation Asset Preview and retained source checks. No mesh generation, material rebake or imported FBX rewrite was performed.
