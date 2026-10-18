# Phase 1 follow-up retirement report

The initial C00–C10 delivery was partial. This follow-up now retires all eleven targeted obsolete scenes rather than relocating them to a lab. Controlled versioned deletion was used under F03; no Unity/application launch or computer control occurred.

| Scene | Original GUID | Disposition | Replacement |
|---|---|---|---|
| `Assets/Production/Scenes/Day2ActorValidation.unity` | `6147181d1c5f60e458a955b04fbd0627` | RETIRED | ProductionSceneEnvironment + GameSessionSceneSetup with saved template/prefabs |
| `Assets/Production/Scenes/Day3CommandValidation.unity` | `8ca719727de320f4ea5de8dd2370e555` | RETIRED | MaintainedRegressionSuite; existing command/adapter runtime tests |
| `Assets/Production/Scenes/Day4PartnerContactValidation.unity` | `2e0bf1a76e1ceb448af92d79f7655bfd` | RETIRED | PreparedActorDiagnostics; HandGripRuntimeSelfTests; retained Day4ContactSurfaceWitness |
| `Assets/Production/Scenes/Day5WardrobeValidation.unity` | `2eb7586bcdaa7c547a376aa0e22c2ff1` | RETIRED | Existing wardrobe transaction/release tests, ClothingSetup and protected reviews |
| `Assets/Production/Scenes/ProductionRuntimeSmoke.unity` | `64a54fb1efde6cf4c866b3f5351559bd` | RETIRED | Current FirstContact/MainMenu saved reference checks and managed full-session tests |
| `Assets/Production/Scenes/RemediationValidation.unity` | `d5a830258f9b59b4c80583e6a82d53d9` | RETIRED | Extracted negative-content + maintained runtime regression suites |
| `Assets/Scenes/PoseValidation.unity` | `1f3c2d21783d2d442b3802aa2c542ca8` | RETIRED | Selected-character/resolver/adapter diagnostics; asset animation preview; selected SALSA config/audit |
| `Assets/Scenes/HandGripAcceptance.unity` | `89a9289125c8278428bceb315fb7482a` | RETIRED | PreparedActorDiagnostics; hand-grip geometry and spatial proof |
| `Assets/Scenes/PartnerRigAcceptance.unity` | `25267b7dc8b0b264d98b96d5840d38cc` | RETIRED | Prepared male/contact validation + preserved source preparation and pure centerline tests |
| `Assets/Scenes/LaraHumanoidKawaiiTest.unity` | `374d21527f8ea434a8b0a6bb7f2078eb` | RETIRED | Humanoid source asset/avatar/clip diagnostics and generic animation preview |
| `Assets/Scenes/AnimationAudit.unity` | `e6dea64a9da3e3749b7d9f7394375dbe` | RETIRED | Animation catalog/controller asset checks and generic animation preview |

Actually deleted scene/meta files: **22**, **5,482,787 bytes**. No moves or prior Task 1 savings are counted. `F03_SCENE_RETIREMENT_PLAN.json` records exact hashes/bytes/recovery. Source-only wrappers/orchestrators are measured separately in F01_RETIRED_ORCHESTRATION.json and F03_RETIRED_WRAPPERS.json.

Guards passed: original scene/meta recovery hashes; tracked-version recovery; zero incoming current Assets GUID references (including metas); zero current .cs/.ps1/.py target-path consumers. Source compile after retirement: all seven assemblies PASS with warnings as errors. All 669 current protected files remain byte-identical; GUID/meta audit has no duplicates/missing metas.

Native Editor diagnostics, preview rendering, AssetDatabase disappearance and open/close/Undo checks: **NOT RUN under the latest user no-program/no-Unity-control boundary**. No source inspection or compilation is described as native runtime success. Existing historical Editor results are not reused as new passes.

The complete remaining-scene pass additionally retired generated `Assets/TestData/LaraCandidate/LaraCandidatePreview.unity` and `Assets/TestData/GraftPairProbe/GraftPairPreview.unity` plus metas (4 files, 439,246 bytes). Their source-validated mesh generation and morph inspection now use the safe generic asset preview; neither Open command rebuilds or saves a scene. Accepted original candidate/first/second outfit scenes and published review captures remain protected by the import status/user approval record.

Five disposable fixture groups were retired: folder-independent action/gesture copies, Day3 face/expression test pair, unused proof materials, and superseded default-layer first/third Candidate presets. Exact original/meta and actual ZIP payload hashes were verified. These are 20 files/metas and 96,336,016 bytes. Current differing user layer choices in Day5 and R03 configurations remain intact.

**Follow-up total: 46 asset files/metas, 102,258,049 bytes; 13 scenes retired, 15 remain.** Source orchestration deletions are measured separately and are not added to this asset savings number. No generated wardrobe tree was removed.

The initial subagent recommendation for `ffc251` was rejected by independent all-Assets GUID verification: three current Day5 preview presets still reference its fit outputs. Its complete closure is retained with those distinct layer records. `SetupCandidates/685f` remains linked by inputHashes in three actual configurations; live a659, immutable releases, pending toe, audio A/B/C (frozen source refs), exact-path legacy skin material consumers and canonical imports remain. Each reason and property difference is recorded in F03_ORPHAN_AUDIT.json; these are concrete protected data/consumer contracts, not an excuse to retain obsolete scenes.

Inventory paths/classifications from the first F00 draft were corrected after root review. PHASE1_SCENE_DISPOSITION.csv and PHASE1_DEPENDENCY_GRAPH.json now use verified `Assets/...` identities. There are no per-feature lab copies. Native Editor integration remains NOT RUN by the user's instruction; source checks and preserved/ported assertions are separate evidence.

Final F04 source/managed result: 7 assemblies compile, 60 story + 69 application tests pass, 661 protected files byte-identical plus eight explicitly audited disposable baseline records removed, 4,371 GUIDs with no duplicate/missing metas, and no new unresolved scripts in retained scenes/prefabs. Twelve script references in unused INab vendor demo camera prefabs are unchanged from C00 and belong to absent optional camera packages; preserve vendor content/provenance rather than silently install packages. Native Editor checks remain NOT RUN, so full native/visual acceptance is pending. Cleanup source implementation is complete; UI/Studio execution proceeds independently of prohibited native testing.
