# W04 — Kind-aware play and Intro sequence migration

Depends Gate A PASS. Read API sections 2–4 and CHECKS LN01–LN07.

## Files

Language/CompiledStoryProgram, DazStoryCompiler, StoryProgramLinker, StoryMachine, StoryPlaybackWindow, StoryAuthoringStructure (if needed); Editor/Application/DazStoryImporter; Runtime/Story/Runtime/CompiledStoryAsset; relevant managed hosts/tests. FirstContact.dazstory declaration only. Update source-analysis/reference traversal helpers that assume Play means card. Do not yet change card exit behavior.

## Steps

1. Add ResolvePlayable and typed declaration result; retain ResolveCard for Director. Compile/link errors identify source and colliding kind/origin. Keep global files card-only and local helper sequences local.
2. Play dispatches sequence Entry directly or existing ExecuteCard for cards. Shared depth bound covers both; helper invocation does not create card context or implicit setup. Unique card invocation counter is run-local and independent from Director history.
3. Reachability and optional playback windows traverse both kinds under stable unit identity; finite traversal avoids cycles. Preserve source unit relocation, aliases, dialogue owner and exact transcript hashes.
4. Apply importer=3/program=2 schema boundary and reject stale program data everywhere it can enter runtime/linking. Preserve explicit version through filtered views. Update managed constructors/test fixtures accordingly.
5. Rename `card Intro` to `sequence Intro`; leave play Intro/body/transcripts unchanged. Verify root FirstContact remains the saved/configured root; do not pick the first sequence declaration.

## Checks / exit

LN01–LN07, existing story tests, source/rehearsal compatibility, exact transcript/alias/GUID comparison. Static source compile. Renaming Intro is required before W07 changes all card exits, not deferred into a broken main runtime interval.

Do not add run/execute-sequence syntax, global sequence catalog, special Intro dispatch or new interpreter. Next W05.
