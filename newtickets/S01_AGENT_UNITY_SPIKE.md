# S01 — Spike: can a local agent run Unity usefully?

**Repository:** `artwhaley/DazPoseWizard`, local working copy on Art's machine, branch `main`.
**Plan reference:** `docs/plan/ARCHITECTURE_SPRINT_PLAN.md` (decision D12, section 9).
**Runs after:** T01 is complete (the old fixture clones are deleted and disk is freed).
**Must conclude before:** T03.
**Owner of decisions:** Art.

---

## Goal

Answer one question with measurements: **can a local coding agent run Unity headless, reliably enough to verify its own work, without eating the disk?**

The answer decides whether the "agents never operate Unity" rule in `AGENTS.md` is relaxed or kept. Codex was poor at this in the past, so the bar is evidence, not a single lucky run.

This spike also creates the **one persistent sandbox Unity project** the rest of the sprint uses. S02 (UMotion) runs in the same sandbox.

## Observable when done

- Art sees an EditMode test result produced by an agent-launched, headless Unity run against the sandbox.
- Art sees the same test **fail** when the agent deliberately breaks it, proving the run is real and not a false green.
- `docs/plan/S01_UNITY_SPIKE.md` exists with measurements, a verdict, and proposed `AGENTS.md` rule text for Art to accept or reject.

---

## Boundaries

This ticket is the one exception to the no-Unity rule, and only in this form:

- **Allowed:** launching Unity in **batch mode** (`-batchmode -nographics`) against the **sandbox project only**.
- **Never:** open the main project (`validation/DazPoseUnityValidation/`) in Unity in any mode. Never launch the Editor GUI, Hub, Play mode, builds or players. Never touch hardware.
- Art may have the main project open in the Editor the whole time. The sandbox exists so that both can run.
- **Disk budget:** ask Art for a disk budget before creating the sandbox. Stop and report if any step would exceed it.
- **Exactly one sandbox.** Do not create additional clones. If the sandbox breaks, repair or recreate it in place.
- Kill any Unity process you started that is still running when you finish or abort. Never kill a Unity process you did not start.
- Code changes are made in the **main repo** and committed there. Nothing flows from the sandbox back into the main repo except by you copying a reviewed change into main and committing it there.
- Local commits only; no push.

---

## Known starting facts

Verified from source; re-check against the working copy.

- Unity version: `6000.5.9f1` (`ProjectSettings/ProjectVersion.txt`). Editor path on Art's machine, per the old `AGENTS.md`: `C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe`.
- **The Unity Test Framework package is not installed** (`com.unity.test-framework` is absent from `Packages/manifest.json`), and no test assemblies exist under `Assets/`. You will add both.
- Asset serialization is Force Text (`m_SerializationMode: 2`).
- **Large parts of the working project are gitignored local inputs**, so a plain `git clone` or `git worktree` of the repo produces an incomplete project. Notably:
  - `Assets/Animations/` — generated pose clips **and their `PerformerPose` assets** (`poseOutputRoot` in `DazPoseWizard.project.json`).
  - `Assets/DazPoseImports/`, `Assets/DazExpressionImports/` — DAZ import inputs.
  - `Assets/Generated/`, `Assets/TestData/`.
  - All `*.fbx` files, including the Lara and player character models.
  - `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- The existing managed tests (`dotnet test` over `tests/`) run outside Unity and stay as they are.
- Candidate pure functions for the first test, all editor-side and deterministic: `NativeSeatingMath.BakedRoot`, `NativeSeatingMath.AlignLiveHip`, `NativeStandingRules.Facing` (in `Assets/DazPose/Editor/StoryStudio/`).

---

## Steps

**1. Measure before creating anything.** On the main project, report sizes of: `Library/`, `Assets/` total, each gitignored folder under `Assets/` listed above, and the `*.fbx` files. Report free disk space on the drive. Ask Art for a disk budget and a location for the sandbox. Suggest a sibling of the repo folder (for example `..\DazPoseWizard-Sandbox\`), **not** inside the repo and not under `.dazposewizard/`.

**2. Choose the sandbox layout.** Pick one approach and justify it with step 1's numbers:
- **Copy** `validation/DazPoseUnityValidation/` excluding `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Build*/`, `TestOutput/`. Unity rebuilds `Library/` on first open.
- **Worktree plus local inputs**: a `git worktree` for tracked files, plus a one-time copy of the gitignored inputs.

Whichever you pick, write **one** sync script (for example `scripts/sandbox-sync.ps1`) that brings tracked code from the main repo into the sandbox without touching the sandbox's `Library/`, and never writes to the main project. Retire nothing else in `scripts/` in this ticket.

**3. Add the test framework and a test assembly (in main).**
- Add `com.unity.test-framework` to the main project's `Packages/manifest.json`. Use the version the 6000.5 editor ships with by default; record which.
- Create an EditMode test assembly under `Assets/DazPose/Tests/EditMode/` (asmdef with `includePlatforms: ["Editor"]`, referencing the test runner and the editor assembly that contains `NativeSeatingMath`).
- Write a small, deterministic test class covering `NativeSeatingMath.BakedRoot`, `AlignLiveHip` and `NativeStandingRules.Facing` with hand-computed expected values.
- Commit in main. Tell Art the main project will import the new package next time Unity opens it.

**4. Create the sandbox** per step 2 and sync.

**5. First headless run.** Launch, roughly:

```
Unity.exe -batchmode -nographics -projectPath <sandbox> -runTests -testPlatform EditMode -testResults <sandbox>\TestOutput\editmode.xml -logFile <sandbox>\Logs\s01-run.log
```

Do not pass `-quit` together with `-runTests`. Record wall time (this one includes the full first import), exit code, and the result XML summary. If licensing blocks batch mode, stop and report exactly what the log says; Art handles licensing.

**6. Reliability runs.** Run the same command **five more times** with no changes. Record wall time, exit code and pass/fail per run. Then:
- Make one trivial code change in main, sync, run again (incremental compile time).
- Deliberately break one assertion, sync, run: it must report a failure. Revert, sync, run: it must pass. Show Art both results.

**7. One scene-level probe (stretch).** Write an editor method that opens the sandbox copy of `Assets/Scenes/FirstContact.unity` in batch mode and prints, without modifying or saving anything: the bound locations and whether each resolves, and the result of the existing binding diagnostics (`StoryContentBindings.Diagnostics` / `PerformanceSceneBindings.Validate`). Run it once with `-executeMethod ... -quit`. Record whether HDRP and the scene load cleanly headless. If this is painful, stop and record why; it is a stretch goal.

**8. Measure the cost.** Sandbox disk usage after all runs. Main project untouched (confirm `git status` in main shows only your intended commits).

**9. Write `docs/plan/S01_UNITY_SPIKE.md`** containing:
- Sandbox location, layout chosen and why, disk used.
- A table of every run: wall time, exit code, result.
- Failure modes seen (hangs, license, import errors, lock files) and how each was resolved.
- Whether the scene probe worked.
- **Verdict**: one of
  - **Adopt**: agents may run headless EditMode tests and `-executeMethod` probes against the sandbox. Include the exact proposed `AGENTS.md` boundary text.
  - **Adopt narrowly**: tests only, no scene probes. Include proposed text.
  - **Keep the rule**: Art tests everything. Say why.
- What T03 should therefore include for verification.

**10. Stop for Art.** Art reads the verdict and decides. Only after Art accepts a rule change, update the boundary section of `AGENTS.md` and record the decision in `docs/plan/STATUS.md`.

---

## Deliverables

- One sandbox project at the agreed location, plus its sync script
- `com.unity.test-framework` and `Assets/DazPose/Tests/EditMode/` in main, committed
- `docs/plan/S01_UNITY_SPIKE.md`
- `AGENTS.md` and `STATUS.md` updated only after Art decides

## Out of scope

- Play mode tests, rendering checks, screenshots.
- Testing anything beyond the placement math and the optional scene probe.
- Installing UMotion (that is S02, in this same sandbox).
- Version-controlling the gitignored pose assets. Note it in the report if it affected sandbox creation; S02 and T04 deal with it.
