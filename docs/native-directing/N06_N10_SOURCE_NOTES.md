# N06–N10 implementation notes

## Runtime parity contract

The integrated Studio uses canonical RehearsalSession / StoryMachine / StoryDirector with StoryLinkedContent’s global owner references. RehearsalVisualState consumes executed StoryCommands only. It contains typed semantic effects and cumulative camera, pose, visibility, material and seat intent; no source parsing, branch decisions, RNG or card eligibility. Reprojection resolves the same authored references against current matrices without advancing logic. Diagnostics’ fixed G2/G3 camera choices are absent from this product path.

`Camera.StartAt` resets actual PlayerRoot position and ViewRig full anchor rotation, cancels tracking; `Camera.Track(Lara)` persists and aims at the actual saved laraFocus after all pose/root operations. Actual camera hierarchy offsets/FOV/projection/HDRP are retained. Before any camera command, the saved baseline remains. Standing relocation uses actual InitialPose and yaw; full-rotation hidden/dissolve-in and locomotion retained height/pose follow the existing runtime rules. Default SitAt and live-hip persistent/seated-dissolve use the same G3-proven adapters. Material endpoint uses the runtime’s same enabled Mesh/SkinnedMesh renderer selection and replacement-slot semantics; Regular reuses the restored normal baseline. Body endpoint uses reversible forceRenderingOff and its real placement. No gameplay method runs.

Awaited gestures leave the underlying persistent pose. Non-awaited gestures show an explicitly labelled launch boundary without an invented terminal frame or unimplemented overlay audition. Speech retains composition and supplies audio/transcript binding inspection. Motion/toy/prompt/profile state remains virtual; grip is disclosed intent, not a solved animation. Unsupported visual intent throws with source/PC/visit.

## Transport / invalidation

Native effects are acknowledged through the virtual host before awaited/instant canonical calls complete. The editor context completes synchronous endpoint effects immediately, preserving virtual non-awaited pose/gesture channel completion. A queued fallback carries a generation token and cancellation. The first failure reaches canonical transport, cancels later execution and releases the lease; DrainRehearsal no longer catches and continues. Managed tests cover an invalid seat effect followed by movement and Director (neither executes), acknowledgement/reset, exact nth Before/After and unreachable visits.

Logical revision covers source/content/global index and explicit host/binding/profile configuration. It no longer hashes every scene Transform/component dirty count or a whole scene dependency hash. Bounded spatial watcher observes only referenced marks, seat/approach matrices (including parent chair changes), actor parent matrices and authored laraFocus LOCAL transform. The head’s animated world motion is not an invalidating input. laraFocus itself is excluded from preview property ownership because it is an authored local offset; clips targeting semantic anchors remain refused. Native save/Undo proof for this new authored overlap remains G4.

## UI / retirement

Studio is control chrome with auto-resolved saved host/source, searchable actual source rows, Before/After, exact visit, Next/Previous, bindings/native mark Selection/Frame and scenario/Director inspection. There is no product camera-mode toggle. Native Unity handles/Inspector/Undo author the marks. Explicit typed binding reconciliation remains an author command; importer/global-index metadata writes are tested only in the independent project. No source/content assets are changed by implementation.

ScenePreviewSession and meta are deleted after Art’s G1–G3 acceptance. Cloned scene, custom orbit, preview camera, draft ground handles, custom viewport/splitters and old renderer callers are removed. StudioCorrectiveChecks now checks logical-vs-spatial stamp separation instead of invoking the retired renderer. N09_RETIREMENT_AUDIT.json records zero remaining legacy renderer matches in production StoryStudio source; historical execution reports remain historical.

## G4 boundary

NativeDirectingG4Window records actual Studio visits and save/restoration comparisons. It has no fixed shot projection. Clean forced SaveScene compares narrowly normalized YAML; actor/bone/renderer/body/camera snapshots must match baseline while chair/mark and local-focus edits are allowed. Domain-reload state and incremental visit journal preserve evidence. Scratch global card creation/reindex/removal are explicit Art-clicked guarded operations in the independent fixture, with its own index hashes; original protection is independently hashed by the executor. Human checkboxes are evidence attestations, never auto-PASS from managed output.

G4 rendering, native import, contact, save/Undo/material/focus/lifecycle and all appearance remain WAITING ON ART. Source-only compiler and managed tests do not establish native acceptance. No Unity operation, program launch, hardware, push, PR or remote mutation by executor.
