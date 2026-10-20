# Required implementation checks and evidence

These are execution requirements, not checks already run. This documentation-only packet pass does not add/run implementation tests. On explicit packet execution, add the checks with the corresponding tickets and record actual outputs. No Unity/native process may be run by the executor.

## Commands and evidence

From repository root, using already installed runtimes/cache (no network dependency installation just to run a check):

```powershell
dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore
dotnet test tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj --no-restore
# Created by W01; restore offline from existing cache if its assets file is absent:
dotnet test tests/DazPose.Wardrobe.Tests/DazPose.Wardrobe.Tests.csproj --no-restore
python scripts/check-unity-source.py
```

Use the available explicit Python/.NET paths if these commands are not on PATH. New test project targets net10.0 and reuses the repo's current xUnit/TestSdk package versions; no new test-framework/library purchase or download. If restore is required, use existing cache/offline configuration outside tracked paths. Missing cache/reference files are reported precisely, never replaced by fake passing output.

Standalone check-unity-source.py invokes Roslyn with existing reference assemblies and response inputs; it does not launch Unity. Read the script before running after any external modification. Missing stale native references are WAITING_ON_ART for compilation inputs, not authorization to launch Unity. Keep native rendering/fitting validation separate.

Write logs and a structured checks.json under docs/wardrobe-directing/evidence/, including command, revision/source hashes, exit status, counts, elapsed time and check IDs actually covered. Keep large transient compilation output under ignored .dazposewizard/. Do not call historical asset-import/wardrobe execution wrappers: several launch Unity or apps.

## AP — appearance selection, assets and transactions

| ID | Required case and expected result |
| --- | --- |
| AP01 | For populated masks 0..7 and ceilings -1..2, Remove skips empties/highest visible, Add chooses lowest hidden; masks never exceed preset. Boundary no-ops are successful. |
| AP02 | Remove to naked remembers preset; Add returns its first populated layer. Explicit unclothed clears remembered preset and Add is NoOutfitSelected. |
| AP03 | SetClothing fully dresses and retains selected hair; SetHair/ClearHair never change clothing/ceiling. Legacy Outfit applies preset Keep/Set/Clear exactly. |
| AP04 | Fully dressed legacy Outfit with changed current hair still applies required default hair. Equal complete target is AlreadyEquipped; part-dressed repeated outfit fully dresses. |
| AP05 | IDs/aliases resolve deterministically; missing/ambiguous alias, wrong signature, unpublished/mismatched generation return structured failures before mutation. |
| AP06 | Corrected first-outfit full -> panties-only -> naked garment sets match mask meaning; no relabelled dress-only intermediate. Native fit evidence required at A. |
| AP07 | Preparation allocates no native instance, starts no queue and changes no selection. Failed prepare leaves prior state and refs untouched. |
| AP08 | Commit failure stages (body/effect/footwear/attachments) restore prior selection/mesh/morphs/profile/visibility; cancellation never partially reveals. Pure transaction decision fault injection plus native A/B coverage. |
| AP09 | Manifest rejects duplicate/missing keys/role/signature/generation; separate hair exists independently of selected clothing; prefab mapping uses authoritative catalog metadata. |
| AP10 | Canonical bone resolution rejects ambiguity/missing bones; local bones in two attachments/actors never share instances; local cycles fail before attachment. Test real binder planning data, not a second test binder. |
| AP11 | MRU limits, current/rollback/snapshot pins, nested pin release and disposal do not evict live resources or leak previous-card instances. Active collection bounded by current owners. |
| AP12 | Deterministic asset plan has stable paths/content/meta reuse, mismatching existing outputs refused, no mutation outside allowlist. Unity builder behavior verified natively at A. |

## LN — language, source ownership and compatibility

| ID | Required case and expected result |
| --- | --- |
| LN01 | Play local sequence persists its writes; play card dispatches card boundary; sequence inside card inherits context and stays inside outer protection. |
| LN02 | Local/public name collisions across kinds fail with both origins; Director cannot select a sequence; published files with sequence declarations rejected. |
| LN03 | Root setup once; helper play no implicit setup; explicit FirstContact root unchanged when Intro is another sequence. |
| LN04 | Sequence/card recursion bound enforced without host lockup; reachability/playback traversal finite and includes nested source-owned dependencies. |
| LN05 | Unit relocation/content ownership survives nested calls; no root alias fallback for global source. Dialogue owner/hash remains exact. |
| LN06 | Importer=3/program=2; stale version=1 cannot run/link as protected program. All filtered/linked views preserve new version. Existing enum 0–7 unchanged, additions 8/9 only. |
| LN07 | Intro declaration conversion preserves all fixed FirstContact dialogue bytes/transcripts, call order, marks, companions/GUIDs and explicit root. Update obsolete expectations, retain meaningful old tests. |
| LN08 | All five wardrobe commands compile with typed parameters, await required; unknown verbs/wrong arity/cross-kind slots rejected at source. |
| LN09 | Reconciliation adds only missing outfit/hair rows, preserves owner/assignments/audio/unused rows, rejects competing source owner or alias type collision. Native assignment checked A/C/D. |
| LN10 | Hair requires project prefab role/signature/generation; a scene object, arbitrary prefab, missing fit or wrong catalog fails before card entry/mutation. |
| LN11 | Mandatory resources produce actionable diagnostics; unusable drawn candidate excluded, explicit invalid call fails, correct source-unit aliases used. |
| LN12 | Multi-channel Body+Visual+Wardrobe conflicts reject without leaked locks; cancellation/failure releases locks; allowed tracking/speech overlap stays valid. |
| LN13 | Host success handling accepts no-op extremes but rejects Cancelled/Failed/Superseded outcomes as successful completed changes; new SetOutfit keeps hair. |

## RT — card boundary, ownership and return

Pure tests exercise the production protocol/decision records with controlled Tasks/clocks and recording adapters. They must not implement a second state machine whose success merely agrees with itself. Native services require separate Art gates.

| ID | Required case and expected result |
| --- | --- |
| RT01 | Enter capture after relevant finite pose/visibility settles; no snapshot midway through transition. |
| RT02 | Barrier excludes current/ancestor Director/play Tasks and stable tracking/idle; bounded controlled test detects self-wait. |
| RT03 | Accepted operation's delayed completion registers cleanup to its captured owner even after another child scope becomes current. |
| RT04 | Child cleanup cannot remove parent actions/signals/tracking; root context/phase cancellation remains root-owned. |
| RT05 | Restored persistent generation adopted into parent, old child/parent handles invalidated; eventual parent Stop cancels restored generation once. |
| RT06 | Finite completed operations leave active list; 1,000 sequential recording cards do not grow ownership/history resources except intentionally bounded Director history. |
| RT07 | Immutable snapshots include exact shader profile, separate base/seated pose, seat frames/style, gaze/aura/grip/follow targets and wardrobe generation/remembered state; no shallow mutable arrays. |
| RT08 | Prepare validates complete target before first mutation; missing seat/parent/hair/fit fails with domain/source and no partial return. |
| RT09 | Scope/pins release once for capture failure, body fault, restore fault, Stop, scene disposal and repeated Dispose; Dispose never animates. |
| RT10 | Parent live driver not rewound/replaced/stopped by child; child-owned driver stops on exit; restored grip reattaches only to same valid live source, no hardware replay. |
| RT11 | Direct play, Director.Execute and DrawAndExecute record identical Enter/body/settle/Return/verify ordering; no host interface fails before body. |
| RT12 | Nested card restores immediate parent's entry then parent restores outer entry; sequence writes persist outside card, are contained inside it. |
| RT13 | Pose-only diff uses 0.5-second blend and no dissolve; no-change card does not restart tracking/pose/build attachments. |
| RT14 | Combined wardrobe+relocation uses one conceal/commit/reveal, exact full rotation and correct fit/seat order; hidden entrance never reveals. Body restore keeps male source rig. |
| RT15 | Fixed camera full quaternion including roll returns; tracking restores exact target/settings through same endpoint math. No inferred focus substitution. |
| RT16 | Card completion/next cue/Director progress wait for verification; direct invocation gains no Director credit, unique invocation IDs advance independently. |
| RT17 | Stop during body/preparation/conceal/commit/blend/reveal/view adoption is terminal; no later writes/output/card/progress. Exercise cancellation at every awaited stage. |
| RT18 | Body/restore exception retains source/domain, cancels remaining work and grants no normal completion/progress. Presentation-derived posture mirror restored; mood/choices/RNG/user clothing/equipment stay changed. |

## AU — Studio and preview ownership

| ID | Required case and expected result |
| --- | --- |
| AU01 | Count/AllMask derived across all new fields; no lost uncertainty due 8/255 assumptions. Bounded worklist converges at joins. |
| AU02 | Every card call identity, every sequence composes; a skipped choice/loop containing only card calls does not contaminate caller fields. A persistent sequence write still marks affected fields unknown. |
| AU03 | Card bodies under multiple entrances join field-locally; unknown drawn entrance is labelled. No candidate execution/random/variable resolution to find cue. |
| AU04 | Appearance reduction matches AP sparse/no-op/remembered/hair semantics; recipes bounded rather than command-history chains. |
| AU05 | Virtual exit row stable ID and Before/After semantics; real source IDs survive row insertion/search, no duplicate lookup keys. |
| AU06 | Latest selection wins; cue 91+ direct lookup; unchanged-appearance cues/mark updates do not prepare fit/build attachments. Counters assert zero unexpected work. |
| AU07 | Appearance proxies share real skeleton/marks/camera, no duplicate actor/graph; blendshapes copied by name against prepared mesh. Original MPBs/material/mesh unaffected by appearance projection. |
| AU08 | Original renderer visibility ledger preserves external authored changes; no restoring old MPB over unknown external property. Owned proxies/materials/local bones disposed even on partial allocation failure. |
| AU09 | Save suspension restores source visibility/removes transient appearance before serialization and resumes newest request, not old queued appearance. |
| AU10 | Stop, scene close, reload, Play entry, Undo/Redo and prefab apply leave no transient refs/overrides; slot/mark edits persist. Native Art gate required. |
| AU11 | Pose drag while dressed preserves root/camera, seat/footwear contact/fit update once; separate spatial deltas remain retained. |
| AU12 | Typed direct/inherited binding UI has correct source owner, compatible drag targets and missing-slot action; no runtime/scenario tabs added. |
| AU13 | Native counters/timings distinguish lookup, warm spatial/projection and cold prepare. Targets lookup<10ms/warm<50ms, no cue-index growth. Record Art project measurements, not only synthetic. |

## PR — final production and preservation

| ID | Required case and expected result |
| --- | --- |
| PR01 | Before first reveal, OpeningOutfit/OpeningHair resolve correct accepted generation/prefabs, all outfit 1 visible; camera/body/idle authored startup unchanged. |
| PR02 | Intro is a sequence and leaves intended state; all drawn/direct cards return phase entrance; saved cutoff empty/root FirstContact explicit. |
| PR03 | Final production property diff exactly allowlisted, protected hashes/transcripts/GUIDs/source membership intact; report intentional fingerprint/new-generation changes. |
| PR04 | Saved main scene Play and Studio use same assets/fit endpoints; save/reload keeps authored appearance slots and mark edits, no diagnostic runner/proxy contamination. |
| PR05 | A–D Art responses correlated with code/assets, report lists source vs native vs visual evidence, no pending full-state coverage failure or performance target failure at completion. |

## Native evidence schema

Each runner writes `docs/wardrobe-directing/evidence/gate-<letter>-native.json` with schema=1, code/build fingerprint, scene GUID/path, old/new generation, case IDs and PASS/FAIL/BLOCKED plus measured values/errors. Assertions name the actual production service/method and observed values. A check cannot pass merely because a button was clicked or a counter increased. Runner report is technical evidence; manual gate response separately records Art's visual observation.

Outside Unity, the executor checks paths/GUID uniqueness, protected hashes, allowed property diff, transcript identities, catalog membership and packet progress links. Never fabricate missing native evidence, copy old gate approval to a new feature, or run Unity to fill it in.
