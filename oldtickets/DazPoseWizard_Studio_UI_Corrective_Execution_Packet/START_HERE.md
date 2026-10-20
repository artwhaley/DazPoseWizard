# DazPoseWizard — Phase 2/3 Corrective Sprint

**Repository:** https://github.com/artwhaley/DazPoseWizard  
**Reviewed `main`:** `6ae1b817789f6115a78bd19d66ef5647f39158b0` (Oct 19, 2026 00:54 UTC; Oct 18 in US Central).  
**Purpose:** Repair six specific production UI/Scene Director Studio deficiencies found by independent source review. **No architectural choices are deferred to the executor.**  
**Status:** Execution packet; no changes have been made to the repo by this packet.

## Execute

1. Read `EXECUTOR_PROMPT.md`, `CONTRACT.md`, and `TEST_MATRIX.md`.
2. At checkout verify branch, HEAD, clean/dirty state, Unity version, and authoritative paths. If HEAD differs, inspect differences and adapt narrowly; do not silently revert newer work.
3. Execute `tickets/R00` through `tickets/R07` in order. Commit logical checkpoints if your environment and user authorization permit. **Do not push without explicit permission.**
4. Record results and unresolved native/manual checks in `templates/EXECUTION_REPORT.md`.
5. Leave Unity in Edit Mode. The user will perform an assisted hands-on walkthrough of the Studio after this sprint. Provide clear setup and UI entry points in the handoff, but do not attempt the user's Play/visual acceptance.

## Ticket summary

| Ticket | Goal |
|---|---|
| R00 | Recovery baseline, source and asset fingerprints, test baseline |
| R01 | Large catalog edit scroll anchoring and keyboard focus |
| R02 | Reliable source GUID ownership migration and binding moves |
| R03 | Per-occurrence, single-use rehearsal choices (Director / branches / events) |
| R04 | Stale-source, binding, catalog, host and scene-change invalidation |
| R05 | Persistent camera target tracking in isolated rehearsal |
| R06 | Direct source-to-visual navigation, including global cards |
| R07 | Integrated regression, native gates clearly distinguished, documentation and handoff |

The **one global tag-discovered card pool** is non-negotiable. Scenes never declare card libraries. A new published tagged card becomes eligible in preexisting scenes by matching tags and runtime conditions, with no scene edit.

## What this packet is not

It is not a new script language; a new Director; a second story VM; a UI rewrite; a new collection of feature-test scenes; a wardrobe regeneration; a hardware test; or a delivery of the three new authored scenes. All six fixes are surgical against existing code.
