> Historical D1–D5 handoff. The current D5.5–D8 source and checkpoint are documented in [D5_5_HANDOFF.md](D5_5_HANDOFF.md). First Contact now uses .dazstory; the former representative JSON/choreography is not its active source.

# D1–D5 implementation handoff

**SOURCE COMPLETE — AWAITING USER RETEST**

October 16, 2026. This implements T01–T12 from the D1–D5 packet. The packet's prohibition on automatic Play Mode governs this work.

Starting commit: `ca5acab4e6e161985c96710fb63c10a3cd9597fc`.
Delivery revision: see Git HEAD on `codex/d1-d5-story-runtime`. The user explicitly authorized commit and push to `https://github.com/artwhaley/DazPoseWizard.git` on October 17, 2026. Manual acceptance remains pending; older no-push statements below describe their dated handoffs.

- D1 complete in source: `dazpose.story` schema/version 1; strict symbolic bindings; recursive validation; saved `StoryProgramSequence` adapter.
- D2 complete in source: explicit awaited dispatcher through `PerformanceRun`, invocation-owned events, cancellable modal prompts, contextual failures and F8 snapshot.
- D3 complete in source: player body in/out vocabulary through `run.Player`; reuses the existing finite `PerformerDissolve` engine, authored style and particle family on the prepared male body. Lara is the speaking performer; the player body retains the original male meshes and measured contact-column setup.
- D4 complete in source: separate application profile service and atomic file; contract/session/world/local state; symmetric equipment tags; authoritative submission calculation.
- D5 complete in source: shared Settings/negotiation panels, persisted interests/equipment, deterministic prepared selection, session-only clothing and all three clothing branches.


## October 17 — production composition correction

The user rejected inherited test markers/controllers and the Lara player-body substitution. The current scene now removes the complete `FirstPerformanceVoid` wrapper/PerformanceMarkers tree and empty old ToyControl root. Four saved static marks remain because the story uses them; the two other bindings follow the live heads. Environment art, camera optics, volumes, Lara's authored lights and tuning are preserved.

The player body is now `Assets/Production/Player/PlayerBody.prefab`, a variant of the prepared male Partner. Original structural/shell meshes, skeleton, erection channels, materials and measured contact profile/surface are retained. The session's contact target is that body's prepared contact column. One existing finite dissolve owner drives all three original skinned surfaces and restores their original materials at stable visibility. No speaking or acceptance components are added to the player body.

Normal **New Performance Scene** now copies the saved clean `Assets/Production/Environments/VoidEnvironment.unity` and includes the same prepared male body. The template contains environment, volumes and player view only. The First Contact mark composition is a separate four-pose production prefab. New scenes do not copy old test markers or controllers. **Add Prepared Player Body** is available for an already initialized composition.

Console tracing is always enabled for story flow: English action start/finish, branch inputs/results, repeat iterations, event waits/receipt, prompts and confirmations, preference commits, score/equipment/clothing/state updates, checkpoint, cancellation and failure. Existing host logs cover session start/end; active Stop now reports cancellation explicitly. No per-frame logging or toy failure logging is added. D6/D7 Director/card behavior remains outside this packet.

Unity compilation and **34 bounded Edit Mode assertions** passed, including the exact saved scene and the normal new-production-scene path. This is source/setup evidence. The revised scene, Console tracing and male body effects await user manual playback/appearance acceptance. Executor Play Mode and player build/playback remain NOT RUN. Earlier positive feedback describes the previous revision only. Current evidence: `.dazposewizard/d1-d5-evidence/20261017-production-shape/`.

Delivery audit: all **203** changed/new Unity inputs match the compiled isolated project; **87** generated assets were copied with matching hashes. All five protected source assets are hash-identical, 66 retained component records and 44 numeric local poses are unchanged, and all 4,286 scanned asset GUIDs are unique.

See [production composition and tracing](PRODUCTION_SCENES.md) for asset ownership and authoring instructions. The targeted SALSA repair below is historical; replacing the incorrect body supersedes that visual-copy setup.
## October 17 — manual observation and targeted repair

The user manually ran the scene and reported “Otherwise, it looked just fine,” with an orphaned SALSA integration error. The player-body clone had retained PerformerSalsaLipSync/Salsa/QueueProcessor after its speaking performer was removed. Creation and refresh now remove those three player-only components, and the delivered saved scene is repaired. Speaking Lara and the source prefab are unchanged.

The updated check passed 22 Edit Mode assertions, including absence of orphan player-body speech components and valid speaking-Lara SALSA configuration. No executor Play Mode was run. User retest of the repair is pending. The initial 20-check evidence below is historical; current repair evidence is `.dazposewizard/d1-d5-evidence/20261017-player-body-lipsync/`. Positive visual feedback is recorded for the observed run; the full functional checklist and unrelated toe observation remain separate.

Review the story in `Assets/Production/Story/Programs/FirstContactD1D5.story.json`. The opcode dispatcher is `Assets/DazPose/Runtime/Story/Runtime/StoryRunner.cs`; the saved-host adapter is `StoryProgramSequence.cs` beside it.
## Delivered scene and bindings

Open `validation/DazPoseUnityValidation/Assets/Production/Scenes/FirstContactD1D5.unity` in Unity and press Play manually. The scene saves its host, sequence, bindings, prompt canvas and player body. No helper injects runtime content.

The production main menu also lists **First Contact · session setup**. The existing Settings screen gains Interests and Equipment buttons using the same editors as the scene. F8 toggles the optional Editor/development snapshot. The top session control strip remains reachable during a prompt.

Saved symbolic targets: `lara_start`, `lara_face`, `step_forward`, `view_lara`, `view_lounge`, `player_head`.
Saved assets: `voice:first-contact-a` / `voice:first-contact-b` (AudioClip), `seat:lounge` (PerformerSeat).
The Resources configuration binds both versioned JSON catalogs.

**Known manual Inspector/setup items: none.**

The program has 45 authored steps including nested branches. It starts with Lara and the player body saved hidden, runs the representative intro and negotiation, selects at most one willing/owned item, handles clothing, sits at the authored lounge and ends at the explicit first Director-window checkpoint. Warmup progress remains zero. No card or Director behavior exists.

## Checks actually performed

Unity 6000.5.9f1 imported and compiled the final source and executed only Edit Mode setup/data checks in the established ignored `.dazposewizard/remediation-validation` project while the user's main editor remained open. The exact saved composition was refreshed, reopened and validated. The generated scene/configuration files were delivered to the main project with SHA-256 verification.

At the initial handoff the bounded source check passed **20 assertions** covering JSON round-trip and explicit deep-input rejection, preservation of the deepest clothing prompt argument, atomic profile persistence/failure recovery, corrupt-input retention, symmetric future-item conflicts, required saved bindings/body/host, repeated setup without duplicated objects or changed identities, original scene preservation, and no Play Mode.

Required static gates passed: normal JSON syntax for all three production JSON assets; diff/whitespace review; no new package dependency; no TODO/NotImplemented/reflection dispatcher or direct actor/player gameplay bypass in production Story source; no Director/card execution; separate profile/settings/tuning files and owners. All 4,238 scanned asset GUIDs were unique at delivery.

Preservation checks compare the original Void, Lara prefab, Partner prefab and package manifest hashes. Saved-environment comparison verified 33 retained visual component records and 54 retained transform poses. It separately records the three replaced original actor visual components; the replacement reusable Lara prefab and its authored visibility lights are unchanged.

Evidence:
- `.dazposewizard/d1-d5-evidence/final-import-setup.log`
- `.dazposewizard/d1-d5-evidence/source-checks-report.json`
- `.dazposewizard/d1-d5-evidence/delivery.json`
- `.dazposewizard/d1-d5-evidence/preservation-and-guids.json`
- `.dazposewizard/d1-d5-evidence/saved-environment-comparison.json`
- `.dazposewizard/d1-d5-evidence/static-gates.json`

## Limits and deviations

Unity's inline recursive DTO serializer emits its type-depth metadata warning and silently truncates sufficiently deep input. A diagnostic demonstrated truncation beyond its native limit; managed-reference JSON also changed the locked format. The implementation therefore adds a lexical guard that rejects more than eight nested step blocks before JsonUtility deserialization. DTOs and the canonical JSON shape remain unchanged. The delivered program round-trips all its branches and arguments. The warning can still appear when Unity first registers this DTO type; it does not indicate a lost branch in this validated program.

`lara.magicShader` is rejected because the existing scoped API has no corresponding typed shader verb. No bypass was introduced.

The two existing First Contact clips are retained by their known GUID bindings. Clip A's note uses the packet example transcript; clip B has no transcript in the supplied source, so its note records that limitation honestly. Negotiation/response lines without authored audio use prototype messages. No voice was synthesized.

A fresh Start uses current scene visibility. After stopping an already revealed intro, use **Restart scene** to rerun from the saved hidden configuration. Profile preferences survive; physical/session state starts fresh. Player-body in/out verbs are wired, but this checkpoint does not reach the later body-reveal beat.

**Play Mode: NOT RUN (required).**
At the initial source handoff no runtime or visual acceptance was claimed; the later user observation is recorded above. No standalone-player, hardware, independent import, or second-outfit toe acceptance is inferred. No new player build was made for this packet.

## Manual acceptance

Follow [the exact checklist](MANUAL_TEST_CHECKLIST.md). Test all clothing cases on separate runs, Stop during a prompt, profile persistence across leaving/re-entering Play, and Settings profile editing. Confirm the final visible checkpoint says:

> D1-D5 COMPLETE — First Director window begins here. Cards/Director are intentionally not implemented in this packet.

Record functional observations separately from appearance/audio preferences. Only the user can approve gameplay acceptance.

See [schema v1](SCHEMA_V1.md) for settled state keys, argument values and event behavior.

## Changed and new files

The implementation file inventory is in [D1_D5_FILES.md](D1_D5_FILES.md). The supplied D1–D5 ticket packet was already untracked at the starting commit; it is not implementation authored by this run.
