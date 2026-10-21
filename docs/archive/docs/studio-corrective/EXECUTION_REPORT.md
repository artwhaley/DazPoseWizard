> Historical report. Native Scene/Game Studio replacement and current manual instructions are in `docs/native-directing/IMPLEMENTATION_REPORT.md` and `G4_ART_HANDOFF.md`. Old viewport/orbit checks below are historical.

# Studio corrective execution — October 19

**Source implementation delivered; ready for user-controlled manual Unity testing. Native acceptance remains PARTIAL.**

Repository: artwhaley/DazPoseWizard, main. Starting/recovery SHA: `6ae1b817789f6115a78bd19d66ef5647f39158b0`. Final implementation SHA: `1b179ca`; the following documentation/evidence commit is identified by Git history. Local commits only, no push. Unrelated `.codex-remote-attachments/01a147d4-53ff-7391-a12b-b514ed1b1acf/` remains untracked and untouched. Unity project `validation/DazPoseUnityValidation`, version 6000.5.9f1.

No Unity/application launch, Play, computer use, player build/launch, hardware, native rendering or Undo execution occurred. The temporary automatic-review/sandbox infrastructure failures are resolved for root commands. Child agents retained the old helper failure and supplied bounded recommendations only; all applied edits were made and reviewed by the primary agent.

## Tickets

| Ticket | Source/check status | Evidence and remaining native gate |
|---|---|---|
| R00 | PASS | Fresh baseline 183 managed tests, seven source assemblies, read-only audit; 844 protected-file fingerprints. Existing pushed SHA is recovery point. |
| R01 | Implemented; native PARTIAL | CatalogViewportAnchor, ID/control-path focus and preserved edit viewport; Player tests 24. Actual item 73/80 pointer/keyboard focus and native Apply/Cancel remain unobserved. |
| R02 | Implemented; native PARTIAL | Owner migration independent of Undo, strict UTF-8, foreign/duplicate/corrupt typed claims refused before writes; actual owner dependency retained. Guarded native fixtures compiled, not run. Actual FirstContact companions are unchanged; one-time ownership backfill will occur during the user's native import. |
| R03 | PASS managed; native PARTIAL | One canonical visit computed once in StoryMachine and reused before/after/branch/wait/Director/effect. Atomic single-use responses, explicit unresolved waits, schema 2/fingerprint and legacy review. Story tests 90, Rehearsal 13; loop/RNG/reset coverage. Native buttons/reload remain pending. |
| R04 | Implemented; native PARTIAL | Event-driven source/content/global/dependency/scene-host stamp; targeted source timestamp signal; teardown/generation filtering; no per-frame Assets search. Changes cancel and require explicit rebuild. Guarded transient stamp/isolation checks compiled, not run; native import/Undo/host swap pending. |
| R05 | Implemented; native PARTIAL | Persistent mapped focus tracking after movement/sample/render, camera origin retained, StartAt ends tracking, orbit independent. Guarded native movement test compiled, not run; focus/pose/render appearance pending. |
| R06 | PASS managed; native PARTIAL | Linked owner/entry/PC/visit selection, bounded canonical replay through selected completion, main-thread effect flush, explicit before/after, unreachable-path diagnostic. Managed global pose-after-locomotion/owner and repeat tests pass; native position/pose/seat alignment pending. |
| R07 | PASS source/managed; native PARTIAL | 196 tests, seven assemblies, GUID/protected audit, docs and checklist. No native or visual acceptance inferred. |

## Executed commands and results

All from repository root:

```text
dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore --verbosity minimal    90 PASS
dotnet test tests/DazPose.Player.Tests/DazPose.Player.Tests.csproj --no-restore --verbosity minimal 24 PASS
dotnet test tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj --no-restore --verbosity minimal 13 PASS
dotnet test tests/DazPose.Tests/DazPose.Tests.csproj --no-restore --verbosity minimal               69 PASS
python scripts/check-unity-source.py                                                           7 assemblies PASS /warnaserror+
python scripts/audit_p2_p3_assets.py                                                            PASS, zero issues
git diff --check (owned changes; extracted ticket payload excluded)                             PASS
```

Total **196**, zero failed/skipped. Managed Unity shims still emit the pre-existing CatalogValidation.Header.version CS0649 warning; current Unity source compilation passes warnings-as-errors. Intermediate edits caught an async host signature error, explicit search callback errors, obsolete EntityId conversions and a synthetic-action regression; all were corrected and rechecked. Baseline source hashes were checked against `6ae1b817` including Windows newline normalization: no mismatch. Prior 183-test evidence is preserved separately.

Audit wrapper saves/restores the exact historical `docs/p2-p3/FINAL_ASSET_AUDIT.json` bytes and stores the new output here. Final counts: 4,456 GUIDs, zero duplicates/missing metas, 15 scenes, eight UI prefabs. `PROTECTED_FINAL.json`: **844/844 byte-identical**, including FirstContact source/companions/marks, all 19 transcripts, six assigned poses, global authored cards, MainMenu, Lara/prepared male/contact, art, wardrobe and release data. No saved scene or authored binding asset was changed. Historical vendor-reference exceptions remain historical and unchanged.

## Before / after evidence

- `ProfileEditorPresenter` previously reset every edit through SetItems(default true). Data edits now retain ID/offset; explicit query/filter changes reset. Recycled rows capture `(catalog ID, control path)` and clear selection when that ID is outside the visible pool; rebinds clear old row listeners. Nearest surviving old ID is used when filtering removes the anchor.
- `SourceBindingReconciler` previously gated owner backfill on recordUndo. Ownership now converges regardless of Undo, while no-op exits before dirty/save. Existing references/orphan rows survive. Type collisions, duplicate GUID claims and foreign-source reuse are refused. Importer recovery uses the same reconciler rather than creating a shadow companion.
- `StoryMachine` creates a deterministic `(source, declaration, PC, encounter)` visit at BeforeInstruction and passes the same object to decision and completion hooks. Controller dictionaries consume by visit under lock. The second of three same-PC card draws can be forced without poisoning visits 1/3 or changing selection RNG position. Branch and event loops, reset/back and illegal forces are covered.
- `SceneDirectorStudioWindow.Revisions` checks on project/hierarchy/Undo/explicit refresh signals, plus targeted root-source mtime. Stamp includes compiled/raw source, source-owned content identity/dependency revisions, global index/published content, saved scene/host/staging and component dirty versions. Mismatch disposes preview/audio/owned AnimationMode, cancels transport, drops queued old-generation effects and clears stale selection. Host/scene/Play transition teardown is explicit. Preview transforms are excluded.
- `ScenePreviewSession` tracks mapped laraFocus continuously; external focus uses captured actor-relative offset, missing focus reports a bounds/root fallback. Sampling preserves actor placement and updates tracking. Seated staging uses authored pelvis anchor and canonical seating-profile offset; exact native seating transitions/solver visuals remain pending.
- Linked flow displays global card entries and source locations. A click resets/replays the same interpreter through the requested logical completion, then flushes its visual events before labelling after-state. Decisions pause for valid responses. Navigation uses a step budget and Stop cancellation; visual failures are reported rather than labelled successful staging.
- Global selection remains the single tag-discovered pool. Managed add/remove tests change published units without modifying the scene program. No per-scene registration/library or shadow interpreter was introduced.

## Native/manual checks NOT RUN

Unity native import, persistence of one-time legacy owner migration, source move/duplicate behavior, native keyboard recycling, drag/drop/Undo, scene close/domain reload, focus mapping/rendering, seat/pose aesthetics and user Play acceptance remain unexecuted. Optional diagnostic: **DazPose > Development > Validation > Studio Corrective Checks (Edit Mode)**. It is guarded against Play/existing AnimationMode and cleans its unique temporary assets and unsaved preview scene; compiled only during delivery.

Old site-key scenarios are rejected with a review notice. Use **New scenario** and recreate the intended decisions; no automatic site-wide conversion occurs. Changed compiled-program fingerprints require a reviewed new recipe. An instruction outside the chosen branch/card path is unreachable; choose a matching visit/decision/seed instead of assuming a pose preview proves execution.

The pre-existing limits remain: open saved scene required for its environment, no closed-scene loader; Volume post-processing, live grip/contact solver and continuous particle/effect timing are approximate or unsupported. Audio remains explicit. No asset reimport or toe/artistic approval campaign was attempted.

## Five-minute Editor checklist

1. Open Unity yourself and let scripts/assets import. Open `Assets/Production/Scenes/FirstContact.unity`, then **DazPose > Story > Scene Director Studio**. Select `Assets/Production/Story/Programs/FirstContact.dazstory` and its saved StorySceneBindings host.
2. In a large Interests/Equipment catalog, scroll near item 73/80. Change frequency/owned/reward/punishment with mouse and keyboard. Confirm the same ID/control stays visible and Apply/Cancel works. Query/filter changes should begin at the top.
3. In Studio click a pose after locomotion, or a global card pose. Confirm the **After** label, correct source owner, authored destination and sampled pose. Try visit 2 on a repeated line; use **Run to selected (before)** to compare. At an unresolved race choose its offered response.
4. Start at a camera shot, track Lara, then advance two movements and sample a pose. Confirm Player-camera aim follows, camera origin stays fixed and orbit still works independently.
5. While paused, edit a bound pose/audio or commit/Undo a mark. Confirm the rebuild notice and stopped transport. Start / Reset explicitly. Stop/close Studio and verify owned preview/audio/AnimationMode ends. Save authored edits only if intended. Play is entirely your decision.

For a full acceptance pass also exercise legacy/moved/duplicate bindings, global published card addition/removal, host swap/scene close/domain reload, both FirstContact climax races and all clothing branches; the short checklist does not claim these native gates complete. Current guide: `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md`.
