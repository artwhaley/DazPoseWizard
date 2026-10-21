# N00 — Native directing baseline

Starting main HEAD: `4656eb56fe1cfa2201104e960b4d7e8dc41cb766`, exactly the packet pin. Configured origin is `https://github.com/artwhaley/DazPoseWizard.git`. No remote mutations authorized or performed.

## Ownership and recovery

Pre-existing user changes are the three older packet directory moves from `newtickets/` into `oldtickets/`, the newly supplied Native Directing packet, and the unrelated Codex ZIP attachment. Archive moves and attachment are outside implementation ownership and remain untouched/uncommitted. The new execution packet is recorded with this baseline. No source/content edits preceded the baseline. The first implementation edit updates only AGENTS.md's top active execution block; its historical remainder stays intact.

`N00_BASELINE.json` records 7,211 original content/configuration fingerprints (all non-code project Assets/Packages/ProjectSettings plus existing protected inventory), recovery HEAD and exact working-tree status at capture. Bootstrap AGENTS/script changes are included in that status. `N00_SCENE_MAP.json` contains exact saved fileIDs, prefab definitions/overrides and renderer/Animator definitions. `N00_CONTENT_MAP.json` resolves all twelve pose assignments in the two source companions to their clips and records the seat/profile YAML. These are source facts, not native observations. Restore only a specifically identified accidentally modified owned file from its captured baseline; never blanket reset user changes.

## Real scene wiring

Authoritative scene: `Assets/Production/Scenes/FirstContact.unity`. Scene role host and PerformanceSceneBindings are on the saved story host. Saved PlayerController `685317201` references PlayerView `685317202`. Its player root Transform is `685317203`, viewRig `1111684059`, headPose `174019229`, MainCamera `1170267249` (Transform `1170267246`). Camera and all saved rig objects are enabled/active, targetDisplay 0, targetTexture null, FOV 58, near/far .08/70. Head/camera child offsets are zero. This establishes saved references only; HDRP Game rendering is NOT RUN until Art's G1.

Lara component `916334048` comes from `Assets/Production/Performers/Lara.prefab`. Source-owned bindings still lack ownerSourceGuid; importer reconciliation can write it automatically. Prepared body `233625191` comes from `Assets/Production/Player/PlayerBody.prefab`. Lara focus `801998613` is Story Face Target under a head-bone descendant, local offset (0,.08,.08). Preserve that local offset and resolve aim after pose sampling.

Lounger seat `8153883316773160518`, placement `8153883316773160521` and focus `8153883316773160520` belong to the ValidatedLoungeSeat prefab instance. Distinct ApproachAnchor and SeatAnchor definitions are retained in CONTENT_MAP. Profile is `Assets/Production/Content/Motions/Seating/KawaiiSeatingProfile.asset`; default CrossLegs and persistent sampled-hip correction are separate runtime paths. Native confirmation of all loaded objects is deferred to G1/G3.

## Edit Mode writers and later ledger obligations

| Owner | Callbacks / order | Properties and ownership consequence |
|---|---|---|
| LaraHeelReview | ExecuteAlways; OnEnable/OnValidate/LateUpdate; order 9000; OnDisable undoes prior correction | Body CapturedHeelFootPose/WardrobeFootFit blendshapes; rigRoot local Y; constrained toe local positions/rotations; foot/support correction. Disabling itself has restoration effects. N02 must snapshot before sampling and define coexistence; G1 touches none of these. |
| LaraFirstOutfit | ExecuteAlways; OnEnable/OnValidate/LateUpdate; order 10000 | Attachment enabled/updateWhenOffscreen/allowOcclusionWhenDynamic/localBounds/world bounds/forceRenderingOff; renderer property blocks and blendshape copies. |
| LaraWardrobe | ExecuteAlways; OnEnable/OnValidate/LateUpdate; order 10000 | Per-piece visibility, skinned bounds, forceRenderingOff, renderer and per-material property blocks (opacity), body-to-piece blendshapes. Equal-order wardrobe writers have no guaranteed ordering; don't assume deterministic preview precedence. |
| GripContactRod | ExecuteAlways; gizmo rendering and analytic frame queries | No recurring performer/camera/renderer writes; fixture contact gizmos are not a grip solver. |
| PerformerMagicShaderPresentation | Explicit Apply/RestoreRegular; OnDisable/OnDestroy cleanup | Replaces renderer sharedMaterials arrays with owned transient clones; tracks normal/effect refs and disposes clones. N04 must own references and tested restoration, not merely assume property blocks suffice. |
| SuccubusPerformer / PlayerView | Runtime initialization/LateUpdate (not ExecuteAlways) | Do not invoke runtime graph/session or async movement. Performer OnEnable guards Application.isPlaying; real sampled-bone behavior still needs Art's tests. |

N02 must establish property ownership before actual staging; this baseline is an audit, not an implemented lease or save-safety claim. Never disable Lara's root to hide the competing writers. Stage-induced focus world motion must not trigger logical invalidation.

## Isolation and code synchronization

Independent physical project: `.dazposewizard/native-directing/G1Project` under the repository root. Own Assets (including ignored generated/imported art), Packages, ProjectSettings and physical Library/PackageCache. No symlinks/hardlinks to source assets. Disposable saved scene: `Assets/__NativeDirectingFixtures/G1_FirstContact.unity`, byte-identical scene data with a fresh fixture-only GUID. Originals and their bindings/index remain separate physical files. Nothing launches Unity during preparation.

Reproduction: `python scripts/native-directing-fixture.py prepare` creates a fresh baseline/fixture and refuses overwrite; `sync-code` copies only .cs/.asmdef/.asmref plus their .meta, validates previous destination hashes before replacing anything and refuses independent fixture code edits; `verify` checks protected originals. Before every Art gate run sync-code. Fixture identity and per-file code hashes are in `.native-directing-fixture.json`; the G1 menu checks these before staging. Importer migrations/index changes in the fixture are expected and must never sync back.

## Baseline checks

196 managed tests passed: Story 90, Player 24, Rehearsal 13, Application 69; zero failures/skips. N00_MANAGED_RESULTS.json and individual logs preserve commands/results. Seven current Unity source assemblies compile through Roslyn/reference inputs outside Unity; see N00_SOURCE_COMPILE.json. Static asset audit PASS, zero issues; N00_ASSET_AUDIT.json preserves this run and the historical audit file was restored byte-for-byte.

N00_RENDERER_CALLERS.json lists fifty exact source lines for the obsolete renderer/Studio callers, including its existing guarded diagnostic. Removal remains N09 after prior Art gates. Native Inspector/import/Scene/Game/Undo/rendering were NOT RUN. No Unity executable, Hub, Play, player, build or hardware operation was started.
