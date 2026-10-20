# S03 — Generic Scene Director Studio EditorWindow

**Depends:** S02. **Goal:** professional visible authoring tool, not another prototype Inspector.

Add `DazPose > Story > Scene Director Studio` dockable EditorWindow. Discover selected `CompiledStoryAsset`/`.dazstory` and active scene runner, choose sequence, show left searchable flow tree with setup/cards/sequence/phases/source instructions/branches/loops/Director requests; center preview placeholder owner from S04; right contextual binding/mark details; bottom transport/state/errors. All panels resizable, visible focus/selection and responsive narrow inspector behavior. Source lines clickable to open original editor file with correct line/column. Show parse errors and missing requirements by source location, filter Unbound/Warnings/Locations/Audio/Poses. Ability to choose `.dazstory` with no current scene (content binding only), or choose saved scene with scene placements. No code generation of a second choreography editor. Changing source reimports/reconciles safely; opening Studio alone doesn't dirty scene or binding assets.

Tests: source selection invalid type, source move, reimport with active line, zero/100 requirements, FirstContact all 19 speech uses and current pose symbols represented, global public and local narrative cards distinguished.

**Gate:** Generic source-level UI visible; binding navigation and original-line locations correct; no First Contact naming or hardcoded symbolic list in generalized UI.
