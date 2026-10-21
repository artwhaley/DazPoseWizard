# Art-operated native gates

**These are required future UI deliverables. Menus/buttons/scenes below do not exist merely because they are named here.** Executor implements, source-compiles and hands off the exact tested revision before Art follows a gate. Executor never starts/operates Unity. No screenshots required. Never ask Art to hunt for an unspecified host/component.

Main project: `C:\Users\artwh\OneDrive\Documents\DazPoseWizard\validation\DazPoseUnityValidation`.

Editor menu required by this packet: **DazPose > Wardrobe > Appearance Delivery**.

The window must default to production Lara/catalog/config paths and display them by readable name and asset path. It retains current staged candidate/released catalog/manifest by GUID across compilation/reload. A red input/error field gives the exact missing property/path and disables dependent actions; no silent fallback to another draft/catalog. Actions do not start Play, a build, a player or hardware.

## Handoff prerequisites, every gate

Before asking Art to act, executor supplies:

1. Gate letter and source commit/hash, current packet ticket status and actual source-check results.
2. Exact project, scene, menu/button and what will change on disk. Buttons must be implemented; no instructions to execute a C# method manually or use a runner that exists only in memory.
3. A brief list of expected visible results and what PASS means. Distinguish runner assertions from Art's observation.
4. Saved report location and how to report a failure using its case/error label.

Record gate evidence in progress.json and implementation report. Art can reply `A PASS`, `B FAIL — <case/observation>`, or BLOCKED with the missing operation. A generic positive response can accept the clearly requested gate scope; do not invent individually unperformed extended cases. Ask only about genuinely missing gate information, and continue independent permitted report/repair work meanwhile. Gate acceptance is never inferred from time, tool output alone, screenshots or an older approval.

If scripts fail to compile in Unity: Art reports the first Console error; executor repairs source and hands back that gate. Do not tell Art to enter Play with compile errors. If Art's current scene has unsaved changes, the delivery window uses normal Unity save prompts and never discards them or overwrites an existing authored diagnostic composition.

## Gate A - Accepted and retired

Art accepted the single Lara's clothes, independent hair and layers on October 20. Art explicitly waived the second-Lara/duplicate-owner requirement; do not request that check again. The historical native report is preserved with its fixture false failures, and Art's acceptance is recorded separately in progress.json. Gate A requires no more user action.

Its disposable scene, unused draft presets and one-use source runner/builders have been removed. The corrected released catalog/configuration retain their GUIDs under `Assets/Production/Wardrobe/Rigs/d0b34840249bc9e812337938d1214c5f5cfb79f958dbd764afe2b6bca12cce0a/`; the released fits, clothing/hair prefabs and manifest remain reusable production data. The Appearance Delivery window now only inspects/generates visual assets for any released catalog; adding outfits/hair does not involve diagnostic scenes.

Subsequent gates use one currently needed diagnostic composition at a time, then retire that scene and one-use setup after acceptance. Never require a second Lara. PlayerBody and Lara remain the actual two distinct scene participants. Preserve reusable production assets and immutable releases when removing temporary compositions.

## Gate B — Protected cards and smooth restoration

Entry: W04–W07 source-ready; Gate A accepted. Saved diagnostic scene and runner created by Art via **DazPose > Wardrobe > Appearance Delivery > Create / open Gate B scene**. Executor must not operate Unity. See `docs/wardrobe-directing/GATE_B_HANDOFF.md` for the short all-cases route: run the suite, one interactive Stop during Outgoing, then Write report.

### Art's exact actions

1. Wait for compile, open delivery window, create/open `Assets/__WardrobeProtectedCards/Scenes/GateB_ProtectedCards.unity`, enter Play. Saved **Card Return Gate** panel appears.
2. Leave **Entry posture: Standing**, **Entry camera: Fixed**. Click **Run direct card**. Runner establishes and shows the stable entrance for two seconds; disruptive body then visibly changes pose/location/clothing/hair/shader/view/body visibility and pauses two seconds. Observe the return: smooth pose blend, dissolve relocation, original look/body visibility and exact authored camera orientation. A trailing cue appears only after return verification.
3. Click **Run explicit Director card**, then **Run drawn card** after each result completes. Both must show the same protected return. The draw has one eligible diagnostic disruptive card, uses genuine selection/linking and reports its ID; it cannot pick an invisible no-op alternative.
4. Select **Entry posture: Default seated**, **Entry camera: Track Lara**, click **Run direct card**. Lara returns to the same seat/style and camera's original tracking target. Then select **Persistent seated** and repeat: custom pose/hip contact persists after return and for a two-second hold.
5. Click **Run nested cards**. Panel labels Outer entrance / Inner entrance / Inner returned / Outer returned. Both distinct entrance states must be restored at their respective boundaries.
6. Click **Run persistent sequence**: changes remain at its trailing cue. Click **Run sequence inside card**: sequence changes are returned by the enclosing card. Click **Run no-change card**: no gratuitous dissolve/camera restart or appearance rebuild.
7. Click **Run parent motion ownership**: local analytic driver runs without toy output, child cannot replace/stop it, captured grip/follow returns to its live clock without rewind. Panel names the driver/consumer generation and source restrictions.
8. Click **Run Stop checkpoints**. User-initiated diagnostic iterates labelled body/appearance/relocation/pose/view return checkpoints, invokes the real Stop, and asserts no subsequent commit/reveal/output/progress. Observe terminal stopped status; it does not animate recovery after Stop. Also click the panel's **Stop current run** once during a normal disruptive run to confirm interactive cancellation.
9. Click **Run fault cases**: missing target/fit and body/return exceptions are labelled intentional; no trailing normal cue/progress follows. No production assets are deleted to cause these cases.
10. Click **Run remaining combinations** to exercise any of the six posture/camera combinations not covered above, including hidden entrances. Then click **Write report**, exit Play, reply B PASS or exact failed case/observation. Path `docs/wardrobe-directing/evidence/gate-b-native.json`.

### Required runner evidence

Entrance/outgoing/returned snapshots, nonzero intended domain differences before return, method/phase trace, actual blend/dissolve durations, root/hip/camera residuals, assets/modes/target/settings, scope/generation handles, resource counts, progress-before/after, trailing-cue ordering and no-output assertions. Fixed camera case includes nonzero pitch/roll to detect yaw-only restore. Tests with hidden entrance and all six posture/camera combinations run as labelled assertions in **Write report** only if already exercised; provide **Run remaining combinations** to exercise them explicitly, never mark unrun assertions PASS.

Both visible smooth return and technical state/ownership behavior matter. On failure repair Gate B before W08.

## Gate C — Studio assignment, static summaries and save safety

Entry: Gate B PASS, W08 source-ready. Create/open `Assets/__WardrobeProtectedCards/Scenes/GateC_Studio.unity` via delivery window. **Remain in Edit Mode.**

GateC script is deliberately generated with unique cue text/binding names, local cards, helper sequences, explicit waits to reach 100+ source rows, sparse-layer cases and one ambiguous choice. Do not require Art to locate a numeric row after layout changes.

### Art's exact actions

1. Open **DazPose > Story > Scene Director Studio**. Click **Scene / script**, then **Find open scene** if the gate scene is not selected. Connection shows the saved diagnostic host and script by name; no hierarchy hunting.
2. Search `SetOutfit(OpeningOutfit)` and click that cue. In the right inspector, drag an existing compatible released WardrobePreset from Project into **OpeningOutfit**. Preview shows that clothing without moving actor/camera to saved defaults. Undo restores assignment. Search `SetHair(OpeningHair)` and use its Hair prefab slot similarly; clothing stays.
3. Search `RemoveOneLayer` and click successive layer cues with Before/After. Inspector names visible garments, skips empty layers and explains no-op extremes; hair stays. No separate bindings tab is needed.
4. Search `play ProtectedDemo`, click its caller cue and compare Before/After: caller presentation is the entrance state. Open its internal cues to inspect authored disruptions, then click **Return to card entrance** under that declaration and choose After: entrance endpoint. Search `play PersistentDemo`: its sequence output persists. A labelled drawn call preserves caller state without selecting/running a card.
5. Search `LatePose` (a unique source cue after row 91) and click it. Selection is immediate. Use the right inspector's **Select** button on the displayed performer-position mark; in Scene view drag Move/Rotate handles. Then select the displayed camera mark and rotate it. Live drag off updates shot on release; handle editing remains smooth, correct pose/look remains, no wardrobe rebuild on each mark change. Native timings/counters are visible in a compact diagnostics foldout.
6. Drag a different pose into LatePose's pose slot. Actor remains at authored location and camera does not jump through defaults. Inspect seated appearance changes using the clearly named SeatedDemo cues.
7. Search `AmbiguousDemo`: only affected fields show uncertainty after its direct skipped sequence writes. The card-only loop immediately above the late cue does not make its caller state ambiguous. No `finding cue` runtime wait appears.
8. Save scene with preview active, then click **Stop preview**. No proxy objects/mesh/material/visibility changes are saved; intentionally edited marks/slots remain. Close/reopen scene and Studio, repeat preview/Stop. Use Undo/Redo on a slot and a mark. Art can enter/exit Play to verify cleanup; executor cannot.
9. In delivery window click **Write Gate C report**. It reads recorded preview counters/timings, saved-scene/asset references and lifecycle observations; it does not execute runtime story to find cues. Reply C PASS or exact failed action. Path `docs/wardrobe-directing/evidence/gate-c-native.json`.

### Extended native safety checks

Delivery window provides **Run Gate C lifecycle checks** (Art clicks in Edit Mode) to exercise domain-reload-safe save/close ownership bookkeeping and inject an external source MPB property edit. It may request normal reload; it never starts Play automatically. Source MPB edit survives Stop even for an unknown property ID, since appearance adapter never writes source blocks. Assertions not actually exercised remain NOT_RUN. Gate C requires native lifecycle checks plus Art's visible editing/timing approval; no screenshots.

## Gate D — Main saved First Contact

Entry: A–C PASS valid for current code/assets; W09 source/action ready. Art's action advances production appearance refs only now.

### Art's exact actions

1. Exit Play/Stop preview. Open delivery window, **Inspect inputs**. Proposed production diff lists only Lara's wardrobe mount/refs, catalog/config generation binding, First Contact source-owned appearance slots and the already prepared source startup commands.
2. Click **Apply accepted appearance to First Contact**. The window applies exactly those references transactionally and reports before/after GUIDs. If external authored values differ from expected baseline, stop with the property names; do not overwrite them to force the patch.
3. Open `Assets/Production/Scenes/FirstContact.unity` through the delivery window's **Open First Contact** button. Open Studio, select the opening outfit/hair cues and first visible Intro cue. Outfit 1/Emiko appears at the authored marks with the same fit as Gate A/C. Named slots remain easy to replace.
4. Save, reopen the scene, then enter Play yourself. Lara appears dressed before the first reveal; original camera/body/environment stay correct. Intro leaves her intended position/pose because it is a sequence. First drawn cards restore their phase entrance look/position/camera before continuing.
5. Exit Play. Click **Write Gate D report** and reply D PASS or precise observation. Path `docs/wardrobe-directing/evidence/gate-d-native.json`.

The executor then checks actual saved property diffs, source transcripts/GUIDs, release binding and source membership against W00, completes report/checks and commits owned implementation changes locally. No remote push. A missing native observation stays pending and prevents COMPLETE.

## Report ownership

Runners write technical data only; they cannot set Art's approval fields. Executor records Art's message/date/revision verbatim or faithfully summarized. Reports include statuses PASS/FAIL/BLOCKED/NOT_RUN, never silently treating missing cases as PASS. If a gate is broader than what Art exercised, preserve that limitation explicitly and finish the remaining named checks before claiming the whole gate accepted.
