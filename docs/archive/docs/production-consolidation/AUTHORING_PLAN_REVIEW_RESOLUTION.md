# Preauthoring plan review resolution

October 8, 2026. All three supplied reviews were read against the saved build plan, and their source-dependent claims were checked against the local checkout. Changes in this turn are documentation only. No application, build, test, device command or historical runner was executed.

Inputs, preserved unchanged:

- [DeepSeek review](<../preauthoring patch review/deepseek.txt>)
- [MiMo Flash review](<../preauthoring patch review/mimo flash.txt>)
- [Sol61 review](<../preauthoring patch review/sol61.txt>)

Outputs: [root build plan](../../root/AUTHORING_READINESS_BUILD_PLAN.md), [root API guide](../../root/CONTENT_AUTHORING_API.md), [exact menu inventory](AUTHORING_MENU_INVENTORY.md), updated command contract, progress and handoff.

## Disposition of every review finding

| Review finding | Assessment against source/plan | Resolution |
| --- | --- | --- |
| DeepSeek 1: existing runners assert rejected behavior | Confirmed. R07 requires acknowledged output, no-device failure and missing Toys exception; R08 requires tuning restoration. R06 has mandatory Begin preflight. | Plan now has per-runner dispositions and assertion changes in B01/B02/B04. Preserve historical reports; do not preserve obsolete runtime semantics or claim a new full-suite pass. |
| DeepSeek 2: HasToys / CanOutput undefined | Confirmed: FirstContact branches on both at line 111. | B01 deprecates them from authoring, migrates that caller immediately and requires zero production branches. Capability snapshots belong to app Settings. |
| DeepSeek 3: B01 requires B03's service | Valid plan dependency gap. | B01 always constructs a null-service-safe facade around the existing binding. B03 subsequently injects the app service. No temporary second service owner. |
| DeepSeek 4: Stop versus Dispose | Confirmed ambiguity: current Dispose calls Stop directly. | Explicit current-session Stop is broad; Dispose cleans only its captured run identity. Old Stop/Dispose cannot affect a new run. Host terminal state is latched before cleanup. |
| DeepSeek 5: menu inventory lacks measurable baseline | Valid; approximate count refined from source. | Exact baseline: 67 attributes / 46 files, comprising 66 commands + one validation attribute; 64 Tools/DAZ Pose attributes + three DazPose/Production. Every site now has a destination and completion gate. |
| DeepSeek 6: UI Toolkit introduces a new UI approach | Local manifest has uGUI; no first-party UXML/USS pattern was found. This was an unnecessary implementation choice, not an engine-compatibility finding. | B06 now uses saved uGUI Canvas prefabs and the installed package. No UI Toolkit addition. |
| DeepSeek 7: progress top level contradicts packet state | Confirmed: active root plan with old Day 6 / in_progress fields. | Top-level status/currentPacket now describe planned B01; currentDay is null. Prior day/status/approval metadata is retained in a historical bookkeeping object and existing evidence remains untouched. |
| DeepSeek 8a: cache/disk precedence unspecified | Valid; also Sol61 2. | B03 defines serialized → saved field overlay at first load, current cache winning on reload, partial-field fallback, live capture before teardown, and explicit Save scope/destination. |
| DeepSeek 8b: no rule for incompatible implementation shortcuts | Valid. | Plan explicitly forbids weakening the fixed contract to pass a packet; record the missing seam and proceed on independent work. |
| MiMo 1: B03 menu-switch gate requires B06 | Valid ordering flaw. | B03 owns navigation service and a bounded service/reload probe. Menu → A → menu → B belongs to B06/B08. |
| MiMo 2: WelcomePerformance unassigned; prefab origins unclear | Confirmed plan omission. Lara and Partner production prefabs already exist. | B04 explicitly adds WelcomePerformance. Initializer assigns Lara.InitialPose, verified to existing Vintage Glamour Genesis 8 Female 03 asset and recorded GUID; missing content reports an assignment need. |
| MiMo 3: B05/B06 controls installation unclear | Valid. | B05a produces a playable host without UI. B06 explicitly re-runs the idempotent initializer on those saved scenes to add one controls prefab. |
| MiMo 4: cancellation check says “if needed” | Valid weakness for a cancellation fix. | B01 requires a bounded fake-backend check with normal/zero time scale, delayed acknowledgement, timed-position routes, stale facade and replacement ownership. Ordinary output and explicit StopAll are counted separately. No hardware gate. |
| MiMo 5: obsolete adapters can mask unmigrated callers | Confirmed production caller in FirstContact. | B01 source gate requires no non-adapter production acknowledgement calls or availability branches. Compatibility result means immediate local submission/no-op, never invented device success. |
| MiMo 6: editor persistence was assumed | The mechanism exists, but its scope was overstated. QueueApply / ApplyQueued transfers selected supported fields through SessionState to a saved editor instance. It is not app persistence. | B02 documents and preserves that real mechanism; B03 adds separate app cache/disk behavior. No speculative fallback required. |
| MiMo 7: worker threading not specified | Valid. Current backend already uses ConfigureAwait(false) and service has frame-based ramps. | B01 pins Unity objects/frame loops to the main thread, confines transport workers to immutable data, synchronizes generations and observes every task silently. |
| MiMo 8: vendor logging could violate silence | First-party service/backend logs are confirmed. Emission from the pinned DLL itself is not established by this static review. | B01 explicitly removes first-party logs and inspects/disables any app-side vendor log sink; no exception to the user's silence rule and no global Unity-log suppression. External Intiface logs remain external. |
| MiMo 9: B05 is too broad as one delivery | Useful decomposition; no scope needs dropping. | B05a delivers session creation first; B05b performs menu migration. Both have explicit recorded states and are required to finish B05. |
| MiMo 10: built Unity player launch procedure missing | Confirmed: repository instructions describe Avalonia and Unity Editor, not this player. | B08 names exact exe/build/scene evidence plus signed-in-user identity, matching session ID, nonzero window handle and responding checks. No Avalonia window-title assumption. |
| MiMo minor: Settings Test can fail invisibly | Correct and deliberately required by the user. | Plan and API guide state that Test has no success/failure feedback; troubleshooting happens in Intiface. |
| Sol61 1: B02 forward-depends on B03 | Confirmed. | B02 only covers persistent values in the current instance and field mapping; B03 owns cache/disk/reload. Gates now match this order. |
| Sol61 2: persistence precedence and live capture | Valid. | Explicit field precedence, capture of latest live/Inspector values before Stop/unload, stable scene/actor IDs and named persistence files are in B03 and summarized in the guide. |
| Sol61 3: initializer claims playability despite ambiguity | Valid. | Ready to Play versus Needs author assignment are separate outcomes. Actor/camera/clock ambiguity never silently chooses. B04 owns WelcomePerformance and verified pose assignment. |
| Sol61 4: stale facades can acquire a fresh generation | Valid logical hole; service generations alone are insufficient. | Run lease is permanently revoked; toy calls become no-ops and actor/player calls cancel. Old cleanup cannot stop a new follower/clock or submit a late global StopAll. Ordered stop submission has no acknowledgement wait. |
| Sol61 5: acceptance outcomes too broad | Valid. | B08 now lists concrete outcomes for silent errors, cancelled dispatch, stale continuations, partial failure, terminal states, tuning and initialization. Evidence categories remain separate. |

## Source evidence checked

All paths below are under `validation/DazPoseUnityValidation/Assets/`.

- `DazPose/Runtime/Performer/Authoring/ScopedPerformer.cs:16` hard-codes Snap; `PerformanceTuningOverride.cs` carries captured restore cleanup.
- `DazPose/Runtime/Performer/Authoring/PerformanceRun.cs:44` throws for missing Toys; `Dispose` calls Stop; `StopAfterFailure` skips the failing operation.
- `DazPose/Runtime/Performer/Authoring/PerformanceToyResult.cs:16` defines Succeeded using a positive successful-binding count. An immediate silent no-op adapter requires changing that contract, not returning a fabricated count.
- `DazPose/Runtime/Toys/ToyControlService.cs:540` filters cancellation routes by capability; tracking can choose timed hardware position. Service shutdown/event callbacks contain LogWarning/LogException.
- `DazPose/Runtime/Toys/Buttplug/ButtplugToyBackend.cs` has first-party LogException/LogWarning calls and a transport-order gate with an await between current-generation check and submission.
- `DazPose/Validation/RemediationPersistentOwnershipRunner.cs:280–318, 375–378` encodes availability/acknowledgement/failure and pending-toy-await expectations.
- `DazPose/Validation/RemediationAuthorSurfaceRunner.cs:120` requires restored tuning; its FirstContact checks use bespoke reset/replay APIs.
- `DazPose/Validation/RemediationScopedCommandRunner.cs:242–260` expects mandatory InvalidBindings for Begin. The two preflight runners explicitly call Validate and can remain optional diagnostics.
- `DazPose/Editor/Performer/PerformerDefaultsEditor.cs:41–60, 102–149` proves explicit editor instance persistence exists; supported fields are constrained, and application disk persistence does not exist there.
- `FirstPerformanceVoid/Runtime/FirstContactPerformance.cs:111–114` is the current production scoped-toy availability/Async caller.
- `Production/Performers/Lara.prefab:6847` points at GUID `c428715ecd67662458a22d5807df2fb4`, resolved through the existing generated pose's meta. `Production/Partners/Partner.prefab` already exists.
- Project `Packages/manifest.json` includes `com.unity.ugui`. Project `ProjectSettings/EditorBuildSettings.asset` has an empty scene list.
- Menu counts and exact sites are documented in [the inventory](AUTHORING_MENU_INVENTORY.md).

This is a documentation/source review. Runtime correctness, vendor logging behavior, delivered scene appearance and player playback remain unverified until implementation and the specified checks. All B01–B08 packets remain not started.
