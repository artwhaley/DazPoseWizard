# F04 — Final cleanup validation, path stability and active-state audit

**Depends:** F03. **Goal:** No hidden architectural debt is concealed under a "cleanup finished" label.

1. Run full baseline pure managed/Unity EditMode tests; compile assemblies warnings-as-errors where existing CI does. Import, pose convert/reconcile, SALSA, ClothingSetup, review metadata, contact setup, script compile+link+global card index tests. Inspect exact saved scene references in EditMode; no automated Play.
2. Reassert seeded FirstContact selection/parity, 19 dialogues, six pose assignments, global selectable-card behavior; no FirstContact source/editor scene edits unless explicitly required to repair a path and verified equivalent.
3. Re-run project-wide `.unity`, `.asmdef`, `.meta`, GUID, referenced scene paths, importer settings and assembly dependency graph. No production editor dependency on Validation/Examples; no old scene paths beyond **historical docs/archived manifests**.
4. Update `docs/cleanup-task1/EXECUTION_REPORT.md` with clear follow-up status; create `docs/p2-p3/PHASE1_RETIREMENT_REPORT.md` containing each of the eleven before/after dispositions, deletion count/size and any legitimate keep exceptions; update current scene ownership and user-facing current instructions.
5. Do not overwrite stale Phase 1 historical evidence as if the original agent succeeded. Separate "Phase 1 initial partial" from "Phase 1 follow-up completed".

**Gate:** F-series clean; if any active nonprotected obsolete scene persists without replacement, F04 is PARTIAL/FAIL and Phase 1 cannot be labelled fully clean. Preserve independent UI/studio work readiness even if a genuine permission blocker exists; don't invent user consent obstacles.
