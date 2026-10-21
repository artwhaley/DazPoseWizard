# S02 — Spike: UMotion Pro as Lara's pose editor

**Repository:** `artwhaley/DazPoseWizard`, local working copy on Art's machine.
**Plan reference:** `docs/plan/ARCHITECTURE_SPRINT_PLAN.md` (decision D6, section 5.4, tickets T04 and T14).
**Runs after:** S01 has created the sandbox project. Can overlap the start of T02.
**Must conclude before:** T04.
**Who does what:** **Art** operates Unity and UMotion. **The agent** prepares, inspects clip files as text, and writes the report. The agent does not operate Unity in this ticket.

---

## Goal

Decide, with evidence, whether UMotion Pro can be the pose editor for Lara (decision D6), and learn what its edits do to a pose clip so T04 (pose metadata) and T14 (canonical edits from the bench) are designed correctly.

The UMotion store page lists compatibility with Unity 2019.4 only. This project is on 6000.5.9 with HDRP and a **Generic** Lara rig driven by a custom playable graph. Every one of those could break it, so this is a real go/no-go.

## Observable when done

Art sees a pose he edited in UMotion play correctly on Lara in the sandbox's First Contact, and `docs/plan/S02_UMOTION_SPIKE.md` gives a verdict: **Go**, **Go with constraints**, or **No-go** (fallback: a per-bone delta asset stored beside the pose).

---

## Boundaries

- **UMotion is installed in the sandbox only.** Never import it into the main project during this spike.
- Nothing in the main project changes, except the report and status files the agent writes.
- In the sandbox, edits go to **new** files. Never overwrite an original pose clip or `PerformerPose`, even in the sandbox, so original and edited can be compared.
- The agent does not launch or operate Unity (whatever S01 concluded, this spike is Art-operated because it is interactive UI work).
- Local commits only; no push.

---

## Known starting facts

- **Pose clips and their `PerformerPose` assets are gitignored.** They live under `Assets/Animations/DazPoses/` (`poseOutputRoot` in `DazPoseWizard.project.json`), generated from `Assets/DazPoseImports/`. Each `PerformerPose` asset sits beside its clip with the same name and an `.asset` extension. The story bindings (tracked) reference these by GUID.
- **Consequence:** any UMotion edit, and any offset T04 adds to a pose, currently lives outside version control. This spike must produce a recommendation on that (step 7).
- The DAZ regenerator (`DazPoseAnimationClipGenerator`) keeps the `PerformerPose` asset and GUID but **clears and rewrites the clip's curves** from the DAZ source on regeneration. An edited clip in the generated location would be lost on reimport.
- Asset serialization is Force Text, so `.anim` files are readable YAML. The agent can diff an original clip against UMotion's output without opening Unity.
- Pose clips are Generic. Lara's placement comes from marks, not from clip root motion; `PerformerBodyPose` has an `animateActorRoot` switch and preserves the actor root when sampling clips with root bindings. Confirm which applies to persistent poses before judging any root curves UMotion adds. Standing poses are sampled at 0.5 s (`PoseSamplingConventions.StandingSeconds`). Pose clips may carry non-bone curves (expression/morph channels from the typed pose pipeline).
- The six bound First Contact poses: SexyVamp, LeaningForward, CrossedArmsAmused, Main1, Sassy (standing) and Lounging (sitting). Their GUIDs are in `Assets/Production/Story/Programs/FirstContact.bindings.asset` and `Assets/Production/Story/Cards/FirstContactEngineering.bindings.asset`.
- Store page: <https://marketplace.unity.com/packages/tools/animation/umotion-pro-animation-editor-95991> ($70, version 1.29p04, March 2025).

---

## Steps

### Agent: prepare (before Art starts)

**P1.** Resolve the six bound pose GUIDs to file paths in the sandbox. For **LeaningForward** (the test subject) and **Lounging** (a seated control), record from the `.anim` YAML: clip length, sample rate, number of curves, the set of curve binding paths and types (transform rotation/position/scale, blendshape, custom float), keys per curve, and whether any curve targets the actor root.

**P2.** Write `docs/plan/S02_UMOTION_SPIKE.md` with an empty results section and Art's checklist (A1–A7 below) filled in with the exact file paths from P1.

### Art: in the sandbox

**A1. Install.** Import UMotion Pro into the sandbox (Package Manager > My Assets). Record: compiles cleanly on 6000.5.9? Any console errors or warnings mentioning UMotion?

**A2. Set up Lara.** Create a UMotion project for Lara's **Generic** rig on a Lara instance in a scratch scene (not First Contact). Record: does UMotion accept the Generic rig and show the DAZ bone hierarchy? Does it complain about Lara's components (custom graph, wardrobe, dissolve rig)? If it needs a bare model, note that.

**A3. Import a pose.** Import the **LeaningForward** clip into UMotion. Record: does the pose look identical to how it looks in the Studio? Are facial/morph channels present?

**A4. Edit.** Make one clearly visible change (for example deepen the forward lean in the spine and move one hand). Try UMotion's IK tools on a hand if available. Record how it felt: could you get the pose you wanted?

**A5. Export to a new file.** Export as a **new** clip beside the original, for example `LeaningForward_UM.anim`. Duplicate the original `PerformerPose` as `LeaningForward_UM.asset` and point its clip at the new file. Record any export options UMotion offers (key reduction, root motion, curve types).

**A6. See it play.** In the sandbox only, open the sandbox copy of `FirstContact.unity`, change the LeaningForward binding to `LeaningForward_UM`, and:
- Check the cue in **DazPose > Story > Scene Director Studio**.
- Press Play and watch the intro where LeaningForward is used.

Record: looks right in both? Any Studio warning (for example clip validation)? Any difference between Studio preview and Play?

**A7. Coexistence.** With the UMotion window open on Lara, use the Studio projection on another cue, then return to UMotion. Record whether they fight over Lara (pose snapping, errors, UMotion losing its state). This tells T14 whether the bench can hand a pose to UMotion with both open.

### Agent: analyse

**N1. Diff the clips.** Compare `LeaningForward.anim` with `LeaningForward_UM.anim` as text. Report:
- Curves added, removed or retyped. In particular: were morph/custom float curves preserved? Were root curves added?
- Key counts and whether UMotion baked every frame.
- Clip length and sample rate changes.
- Anything that would matter at the 0.5 s standing sample time.

**N2. Answer the design questions** for T04 and T14:
1. **Where do edited poses live?** Recommend a location that is **not** under the regenerated output root and **is** version-controlled, for example `Assets/Production/Poses/`. Say whether the six bound poses (originals) should also move into version control.
2. **How is the edit tied to its source?** What T04's provenance field should record (original DAZ pose, original clip GUID, edited flag).
3. **How does the regenerator behave?** Confirm it must refuse to overwrite an edited pose and say so, rather than skip silently.
4. **Bench handoff:** from A7, can the bench open UMotion on a pose while its own preview is live, or must it close its preview first?
5. **Morph and root policy:** any rules T04 needs about which curves an edited pose may carry.

**N3. Verdict** in `docs/plan/S02_UMOTION_SPIKE.md`:
- **Go**: UMotion works as-is.
- **Go with constraints**: list the constraints (for example "close the bench preview first", "strip root curves on save").
- **No-go**: what failed, and a short sketch of the fallback (a per-bone delta asset beside the pose, edited with a small bench gizmo) so T04 can be designed around it.

**N4. Stop for Art.** Art accepts or changes the verdict. Record the decision in `docs/plan/STATUS.md`. Do not install UMotion in the main project; that happens in T14 if the verdict is Go.

---

## Deliverables

- `docs/plan/S02_UMOTION_SPIKE.md` with P1 data, Art's A1–A7 results, the clip diff, answers to the five design questions and the verdict
- `STATUS.md` updated after Art decides
- Main project otherwise unchanged

## Out of scope

- Editing any pose for real content.
- Building the bench handoff (T14).
- Moving poses into version control (T04, using this spike's recommendation).
