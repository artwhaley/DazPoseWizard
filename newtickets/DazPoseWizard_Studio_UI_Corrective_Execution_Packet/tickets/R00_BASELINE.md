# R00 — Baseline and recovery

**Dependencies:** none. **Deliverable:** verified starting snapshot and reproducible baseline.

1. Resolve `main` HEAD and compare to reviewed `6ae1b817...`; inspect recent diffs if ahead. Record dirty/untracked and user work. No blanket reset/stash/add.
2. Read `AGENTS.md`, current `docs/p2-p3/IMPLEMENTATION_REPORT.md`, `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md` and `CONTRACT.md`.
3. Record source hashes and Unity GUIDs for First Contact source/companions, published global cards, MainMenu/FirstContact scenes, core Lara/PlayerBody and relevant saved mark references. Preserve any untracked artist files.
4. Run baseline managed checks/source-only Unity compile/audit as available (commands in `TEST_MATRIX.md`). Capture commands, counts and failures before modifications.
5. Verify relevant real code paths; attach current path/line notes to report. Do not treat previous report's 183 passes as current automatic proof.
6. Create a protected recovery commit/branch if local workflow permits. Do not push. If currently uncommitted unrelated edits exist, isolate only the corrective files.

**Acceptance:** reproducible baseline; no user assets modified; executor can attribute each change to a ticket.
