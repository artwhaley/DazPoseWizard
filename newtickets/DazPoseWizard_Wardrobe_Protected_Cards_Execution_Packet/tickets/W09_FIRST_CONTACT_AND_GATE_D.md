# W09 — First Contact appearance and final production handoff

Depends Gate C PASS, A/B still valid. Read manual Gate D and CHECKS PR01–PR05.

## Files / bounded content edits

Production/Story/Programs/FirstContact.dazstory and owned FirstContact.bindings.asset; Production/Performers/Lara.prefab wardrobe mount/ref fields only; Production/Wardrobe/LaraCatalog.asset and LaraConfiguration.asset matching released generation refs/config only; necessary source-derived dependency declarations. Existing GUIDs and source objects stay. Do not copy diagnostic scene or any old native fixture art/bindings into production.

## Steps

1. Prepare delivery action's exact production property diff against W00 and current saved values; reject unexpected external changes and report them. Capture recovery refs. No broad prefab/scene rebuild.
2. Add awaited SetOutfit(OpeningOutfit), SetHair(OpeningHair) after hidden startup/idle, before first reveal. Preserve Intro sequence conversion and exact body/transcripts from W04. Root FirstContact stays explicit; preview cutoff remains empty for delivered full scene.
3. Reconcile/create typed slots without disturbing audio/pose/other assignments. Point to corrected released first-outfit and Emiko, derive reachable readiness. The proposed publication/rig references must be generation-compatible.
4. Art clicks Apply accepted appearance to First Contact in delivery window. Apply matching catalog/config/rig references transactionally; no partial advance. Mount nested reusable rig on Lara via normal prefab APIs without modifying skeleton/art/tuning. Pre-existing non-wardrobe prefab properties remain equal. Do not apply anything to PlayerBody beyond unchanged refs/declarations.
5. Art reimports relevant sources; existing global index rebuilds via normal editor path if required. Membership stays identical; fingerprint changes are recorded with reason. Executor does not execute Unity or manually forge generated compiled asset data/index.
6. Run source checks and inspect final on-disk diff after Art action. Update author guide with only commands/features actually delivered, recovery paths and exact main Studio/scene directions. Prepare Gate D and wait for Art's production observation.

## Exit

PR01–PR05, A–D PASS correlated to delivered revision and actual assets, timings captured, no unresolved full-state/preview ownership limitation. Update implementation report/progress to COMPLETE only after saved main scene observation. If a native issue remains, report ticket/gate incomplete without claiming production readiness. Local coherent commits only once execution authorized; no push.
