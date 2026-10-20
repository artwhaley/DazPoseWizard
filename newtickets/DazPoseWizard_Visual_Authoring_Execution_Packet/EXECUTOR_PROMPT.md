# Prompt for the execution agent

Execute `newtickets/DazPoseWizard_Visual_Authoring_Execution_Packet/START_HERE.md`, `BUILD_PLAN.md`, and `CHECKS_AND_ACCEPTANCE.md` in order, tickets A00–A06.

Art's product decision is authoritative: Scene Director Studio edits asset bindings, poses, and 3D transforms for Lara, the player body and the actual saved player camera. It must not execute story variables, choices, random state, waits, Director draws or loops to find a cue. Skip choice blocks for downstream visual accumulation; detect persistent visual writes as authoring-rule violations and report the affected uncertainty. Preserve correct native pose/seat/camera endpoint rules.

The existing implementation at inspected baseline `e36a6d2ee7255d91c61a690a264f37617596f7af` on main uses `NavigateSelected -> RehearsalSession -> StoryMachine`, accumulates `RehearsalVisualState.Effects`, and re-scans that history in `NativeSceneStage.Cumulative.cs`. Replace both navigation and presentation dependencies, not just one or their progress label. Do not optimize canonical replay with checkpoints, seeds, or cached story state.

Read the plan's explicit treatment of source structure, literal card calls, dynamic/global cards, skipped blocks, field-local uncertainty, source-owned aliases, neutral standing pose sampling, delta updates and save/resume. New type/file names in the plan are proposed; adapt names if necessary while preserving their responsibilities and acceptance criteria.

Keep the single left-aligned wrapped cue list and adaptive inspector. Clicking anywhere must select immediately. Show direct/inherited bindings, pose drag targets, mark create/bind/Select/Frame, and clear ambiguity. No runtime transport options belong in this authoring interface. Preserve independent editing even when some preview inputs are unresolved.

Work on main. Inspect HEAD/status first and preserve newer work and unrelated packet archival moves/attachments. Implementation permits coherent local commits, **no push or other remote write**. Preserve all production art, scenes, companions, global index, rig/wardrobe/toe data and GUIDs.

**Never launch or operate Unity in any form**: no GUI/Hub/computer-use, CLI/batch, Unity tests, executeMethod, Play, builds, players or hardware. Only Art performs native checks. Standalone Roslyn/source compilation and pure managed .NET checks are allowed. No subagents without explicit authorization.

Reuse `.dazposewizard/native-directing/G1Project` and its `Assets/__NativeDirectingFixtures/G1_FirstContact.unity`, preserving Art's edits. Synchronize source code only with the existing fixture script after managed/source checks. Never copy its content back to originals.

Record evidence and status in `docs/visual-authoring/IMPLEMENTATION_REPORT.md`; make the top AGENTS active reference unambiguous on beginning execution. Update old G4 status/handoff to explain supersession without erasing failures. Inspect all old Studio partials, diagnostics/reflection callers, lifecycle hooks and tests when retiring transport.

Proceed autonomously through source implementation and allowed checks. Then give Art the exact project/scene/menu/cue/action checklist from the delivered UI and stop at **WAITING ON ART**. No screenshots required. Do not mark G4/N11 complete from source compilation, synthetic performance, or old G1–G3 acceptance. Repair reported failures within this scope; finish A06 only after explicit native acceptance.
