# N11 — Hand off a usable directing workspace, not a new app

**Depends on:** N10. **Deliverable:** completed implementation report, new author guide, reviewer responses, clean git state and precise acceptance status.

## Execute

1. Create/update `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md` as an **actual novice workflow**, using visible Unity menu names and controls *as implemented*. It must begin with opening the FirstContact scene and standard Scene + Game tabs, then Studio; no hidden setup leaps. Include click-select, Next/Back, visit choice, Director forcing, pose, stand, seat and camera marks, Undo, Ctrl+S and Stop.
2. Describe how Studio distinguishes **temporary preview** from authored anchor edits, what works in Edit Mode (settled cue endpoints, standing/sitting poses, camera framing) and what remains a Play-only timed effect (exact dissolve dynamics, sound lip sync timing, hardware and physical grip solver). Do not say staging requires a Play second pass.
3. Provide `docs/native-directing/IMPLEMENTATION_REPORT.md` with before/after architecture, exact paths changed/deleted, actual final SHA, tests and native Game/Scene/save evidence, object/asset preservation audit, known edge cases and remaining manual observations.
4. Resolve each item in `REVIEWER_BRIEF.md` and `REVIEW_RESOLUTIONS.md` with code paths and **Art-supplied** evidence. Note unresolved technical/visual risk rather than inventing success. The executor never runs Unity.
5. Remove **executor-owned** scratch source files after Art confirms native evidence has been collected; do not claim to Stop an open Unity Preview or leave a particular Unity scene/window state yourself. Instead **ask Art** to click Stop and close the isolated fixture if needed. Verify source and protected asset/hash diff, `git diff --check`, non-Unity compilation; request Art for real assembly/package import observations.
6. Local commits allowed by current execution scope; **never push** without new authority. Do not claim visual approval without Art's explicit G4 evidence. Include commands/menus and five-minute smoke test. Preserve any pending diagnostic outputs until Art confirms safe cleanup.

## Gate

- A first-time author can follow the guide to stage FirstContact in the existing Scene and Game views, adjust one stand mark, one seated mark, one camera anchor and save/Undo safely—all Edit Mode.
- Completion report distinguishes code delivery, native technical validation, artistic/human approval, and deferred timed/physical behavior **truthfully**.
- No bespoke renderer survives; no architectural decision left to an executor's judgment; reviewer notes/hard blockers are answered or clearly labelled unresolved.
- N11 may finalize **PASS** only with G1–G4 Art-reported PASS plus preservation/safety proof; if Art has not run G4, provide `PARTIAL / WAITING ON ART`, exact handoff and no claim the workflow was visually accepted.
