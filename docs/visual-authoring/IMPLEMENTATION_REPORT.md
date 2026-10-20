# Responsive visual authoring — source delivered, G4 waiting on Art

Art authorized A00–A06 on October 19, 2026. Implementation baseline: main `e36a6d2ee7255d91c61a690a264f37617596f7af`. Current source identity is recorded in the isolated fixture marker and G4_CODE_SYNC.json. No Unity operation or remote write occurred. A00–A05 source work is delivered; native G4 and A06/N11 acceptance remain pending.

## What changed

- The actual Scene Director Studio no longer constructs or runs RehearsalSession, RehearsalStoryHost, StoryMachine or StoryDirector. The old window rehearsal transport partial, cumulative native adapter, growing RehearsalVisualState/NativeProjectionPlan and their metas are removed. Canonical runtime/rehearsal classes and tests remain for their separate consumers.
- The existing language frontend exports immutable analysis-only structural nodes and exact statement text/spans. Compiled story storage and executable instructions retain their behavior. Read/function expressions are explicitly excluded from literal authoring arguments.
- AuthoringCueIndex builds compact Before/After visual states, memoized explicit-card summaries and bounded incoming-context joins. Choice/loop bodies are skipped for downstream accumulation. Persistent writes are field-local unknowns with source diagnostics. No variables, event responses, random draws, visits or elapsed story time are simulated.
- Worker analysis is cancellable and consumes captured text/DTOs only. Immediate row/inspector selection queues one latest native request. Sidebar layout/text/filtering is cached and only visible rows are drawn. Cue clicks perform direct lookup; no filesystem reads occur in OnGUI.
- Direct native projection resolves only the selected fields and dependencies. Existing stage, saved actor/body/camera, ownership ledger, sampler, seat math and camera endpoint math are reused. Identical shots skip native work; spatial edits retain the sampled pose. Pose/presentation deltas restore only their owned groups; complete save resume reapplies the newest desired shot.
- Standing sampling now shares the runtime 0.5-second convention and clears sparse-clip contributions to the pre-preview neutral baseline. Seated custom/default sampling remains governed by the existing seat frame resolver. Native graph behavior still needs Art's check.
- Preview context snapshots actual actor/body/rig/camera references, so role changes can dispose the previous owner correctly. Authoring permits unrelated missing marks; strict existing diagnostics retain full-map validation. Unknown/invalid selected fields use labelled baseline fallbacks.
- Import-path and dirty-object notifications replace whole-asset graph hashing/rehearsal resets. Companion-only imports do not rebuild source analysis. Existing companion references are reused on ordinary assignment instead of rescanning all binding assets. Only real Scene/Game views are repainted by the stage.
- Inspector pose slots, shared-alias/source ownership, direct mark creation/Hierarchy assignment, seat anchors and Select/Frame remain in one adaptive panel. No branch/seed/visit/transport controls remain. Mixed base/seated layer endpoints are explicitly unresolved rather than guessed.
- G4 diagnostics now record authoring identities, uncertainty, indexing/lookup/projection/request times, pose samples and spatial updates. Optional isolated scratch source exercises skipped-choice ambiguity directly; it requires Art's explicit UI action.

## Checks

- **183 managed tests PASS**, zero failures/skips: DazPose.Rehearsal.Tests 93 (25 new authoring cases; 12 obsolete history-model cases retired), DazPose.Story.Tests 90. Logs and MANAGED_RESULTS.json are beside this file. Meaningful canonical harness regressions from the old cumulative test file are retained in RuntimeRehearsalIntegrationTests; the separate runtime suite remains intact.
- **Seven standalone source assemblies compile PASS** with warnings as errors. SOURCE_COMPILE.json/log retain exact source counts and installed response-file reference inputs. This invokes Roslyn/dotnet only, never Unity.
- Static audit: zero duplicate GUIDs, zero missing source metas, zero Studio authoring traversal violations. Whitespace check passes.
- Actual FirstContact tests establish SexyVamp's inherited stepforward/tracking and direct late Sassy state without traversing Director loops. Synthetic 10,000-cue lookup checks prove no preceding statements are analyzed during lookup; timings in the test log are managed-only and are not native editor latency claims.
- Original protected non-code files are verified separately before delivery; original count 7,211. Existing packet archival deletions/moves, oldtickets additions and Codex attachment remain outside owned commits.

## Native limits and next action

Art alone imports/compiles and checks real AnimationMode/graph sampling, rendering, mark drag, sparse/nonconstant poses, seat contact, ownership callbacks, Save/Undo/Stop/close/reload, artist overrides and materials. Native performance targets are unmeasured. No source or synthetic result constitutes G4 PASS.

Use `docs/native-directing/G4_ART_HANDOFF.md` for exact project, saved scene, menu, cue and action directions. No screenshots required. G4 remains **WAITING ON ART**; repair user-reported failures before A06/N11 completion. Production art/assets/index and existing fixture edits are preserved; only code is synchronized.
