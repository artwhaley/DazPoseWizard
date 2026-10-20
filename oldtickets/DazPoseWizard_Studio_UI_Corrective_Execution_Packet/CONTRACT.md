# Fixed product, implementation and safety contract

## Authority and scope
- Use current pushed implementation as the baseline, not an older ticket's hypothetical design.
- The full authored First Contact is a protected product oracle. The Scene Studio is a *tool over* source, typed content bindings and saved scene marks; it must not be a second source of scene choreography.
- Source-to-visual rehearsal shares the real `StoryMachine`, compiled program, `StoryDirector` and global card index. Never fork selection, state predicates, weighted randomness, cancellation or progress rules.
- One global pool of selectable tagged cards under the existing publication mechanism. A scene asks for `[Warmup]`, `[Main, Sitting]`, etc. Runtime filters by tags, physical predicates and resources. Scenes NEVER opt into libraries. Explicit local untagged narrative beats (`play Intro`) are separate.
- Existing `PlayerProfileService`, `PlayerProfileEditSession`, `StoryContentBindings`, `StorySceneBindings`, `SourceBindingReconciler`, `GlobalStoryCardCatalog`, `RehearsalSession`, `StoryRehearsalController`, `ScenePreviewSession` remain canonical; modify in place.

## No silent UX compromises
- A user editing item 73 of a 100-item catalog must not be returned to item 1. Focus cannot jump to arbitrary controls.
- A forced card is for the chosen visit to a draw instruction, not all future iterations of that line. Branch/event choices obey the same principle. A scenario may store per-visit decisions; site-wide repeat forcing is **not** the default.
- A source/binding/scene revision cannot continue a rehearsal on an out-of-date execution graph. Invalidation must be obvious and non-destructive.
- Selecting a performance instruction should stage the state **after** that instruction, not a raw pose floating at the wrong mark. Where the path depends on a decision or repeated encounter, expose the choice; never fake unseen state.
- Camera tracking should visibly follow Lara throughout preview actor movement and pose sampling, not aim once.

## Ownership and file changes
- Preserve Unity asset GUID/metas and production source assets. Prefer code/test changes; any serialized migration must preserve existing assigned references and source identity.
- A `.dazstory` should compile before all assets are bound, exposing typed requirements. Auto-reconciliation should add empty requirements, never destroy old assignments on source edits.
- Binding owner is the source asset GUID. Companion location is a useful default, not authoritative identity once moved.
- Distinguish **temporary rehearsal overrides** from **committed scene/binding edits**. User profiles, live sessions, toys and saved Lara/PlayerBody must not be mutated in Edit Mode preview.
- Preserve external/frozen Void and artistic files, immutable wardrobe releases, import/source FBXs, canonical pose files, user-assigned layers, measured contact and pending toe review. No magic changes to visuals or rig tuning.

## Permission and validation
- Allowed: read repo, edit source and tests, run .NET managed tests, installed-source compilation scripts, read-only asset/GUID audits, safe offline fixture generation, git diff checks.
- Not authorized by this packet alone: Unity Play, player/build launch, connected toy output, physical/device tests, changing the saved First Contact staging as a side effect, visual acceptance or a git push.
- Unity native import, click/keyboard/focus checks, Scene view handles, preview rendering and Undo/writeback remain **manual/native acceptance** until actually performed. List them explicitly; don't reinterpret a managed shim PASS as native success.

## Delivery quality
- Small coherent changes, zero stale event subscriptions/background workers, no per-frame asset scans, no new disposable saved scenes for testing.
- Use deterministic tests for loop/rehearsal correctness and guarded EditMode tests for Unity-specific behavior where possible. Do not require an unavailable window/GUI just to run managed tests.
- Document any unresolved issue with exact file, reproduction, risk, and corrective follow-up—not a vague HOLD.
