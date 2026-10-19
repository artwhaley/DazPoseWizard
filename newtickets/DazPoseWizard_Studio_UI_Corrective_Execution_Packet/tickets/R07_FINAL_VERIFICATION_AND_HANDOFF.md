# R07 — Integrated regression and delivery

**Depends on:** R00–R06.

1. Run all managed tests and source-only Unity compile/audit commands from `TEST_MATRIX.md`. Add a regression for each fixed defect. Report counts and exact command results; do not recycle old Phase 2/3 numbers.
2. Compile the saved First Contact oracle with global published engineering cards and 19 fixed dialogue transcripts; verify source declarations, six pose assignments, contact setup, no unintentional scene staging edits, and 0/2/6 clothing + both climax races using detached rehearsal.
3. Prove global-card invariant by publishing a disposable tagged card *without modifying FirstContact.dazstory or its scene*, confirming appearance in Director pool, then retiring it and confirming absence. Use safe temporary scratch content and preserve all GUIDs.
4. Audit binding owner GUID migrations. Existing assigned Unity references must be unchanged except the intended ownerSourceGuid metadata; no missing assets, duplicated GUIDs or importer loops.
5. Test legacy program/site-scenario migration behavior; ensure old saved scenarios either convert safely when unambiguous or show a useful incompatible-version diagnostic.
6. Run source cleanliness (`git diff --check`) and a diff of protected files. If the test suite runs native EditMode checks only by launching Unity, **leave it for the user's later authorized walkthrough**; write manual script rather than imply a pass.
7. Update `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md` only for the new navigation/one-off overrides/ownership behavior. Update `docs/p2-p3/MANUAL_ACCEPTANCE.md` with a short **post-correction validation appendix**. Do not alter historical evidence reports to claim old tests tested new code.
8. Complete `templates/EXECUTION_REPORT.md`: checkpoint SHA(s), changed files, pass/fail/native pending, regressions and remaining known preview limitations, actual menu paths and a 5-minute Editor smoke checklist. Do not give the full interactive walkthrough now.
9. Leave the working tree in an intelligible state; commit only work owned by this sprint if authorized. Do not push without explicit instruction.

**Exit condition:** six documented correction gates pass managed/source tests, preserved content confirmed, and the only outstanding gates are truthful native/visual user acceptance or explicitly reported environment barriers.
