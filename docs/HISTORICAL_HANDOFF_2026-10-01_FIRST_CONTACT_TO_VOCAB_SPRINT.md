# Historical Handoff — First Contact → Vocabulary Sprint

**Date:** October 1, 2026  
**Status:** Historical reference

This document records the project handoff created after **First Contact was accepted** and immediately before planning the **Vocabulary Sprint**.

It exists to preserve the exact project state, history, decisions, deferred work, and development philosophy known at that transition point. It is **historical context, not future control**: later accepted implementation decisions, source changes, and explicit user direction supersede anything in this handoff.

The original handoff follows below substantially as supplied on October 1, 2026.

---

PROJECT HANDOFF — UNITY “SUCCUBUS” CHARACTER PERFORMANCE PROTOTYPE
Status: FIRST CONTACT ACCEPTED
Date context: 2026-10-01


======================================================================
1. PROJECT IDENTITY
======================================================================

Repository:
https://github.com/artwhaley/DazPoseWizard

Primary branch:
main

Latest pushed baseline at this handoff:
4dd4abaf851e12dd5c66397baf7ddff59ed5e34d

Unity project:
validation/DazPoseUnityValidation/

Unity version:
6000.5.9f1

Render pipeline:
HDRP 17.5.0

IMPORTANT CURRENT MILESTONE:

FIRST CONTACT HAS BEEN IMPLEMENTED, RUN MANUALLY, TWEAKED, AND ACCEPTED.

The user has visually tested the first directed performance and is happy with
the current result.

Treat First Contact as an ACCEPTED milestone.

Some checked-in documentation was originally written before manual acceptance
and may still contain wording such as "acceptance pending." That wording is
stale. The user's current acceptance overrides it.

Do not reopen First Contact automatically.

Do not assume a specific next implementation step from this handoff. The point
of the new chat is to understand how we got here and then plan the next stage
interactively with the user.


======================================================================
2. HIGH-LEVEL GAME VISION
======================================================================

This is an adult character-performance prototype, conceptually a spiritual
successor to Virtual Succubus.

The important idea is not merely an animated character.

The target is a character who can deliver directed, intimate, responsive
performances inside a game/session structure.

The project has deliberately been developed bottom-up through small playable
slices.

The user has previously over-architected projects before reaching the point
where the experience could actually be felt. That is specifically what this
project is trying NOT to repeat.

Core development philosophy:

    build one thing that feels good
        ↓
    learn what the runtime actually needs
        ↓
    derive abstractions from demonstrated needs
        ↓
    only then build broader authoring/editor systems

Do NOT jump directly to:

    giant director frameworks
    generic behavior trees
    registries everywhere
    message buses
    universal command systems
    huge editor suites
    speculative abstractions

The editor should generally lag the runtime by one generation.

Implementation agents such as Codex/Astra do the mechanical implementation.
The user is the authority on subjective visual/performance quality.


======================================================================
3. CURRENT EXPERIENCE MILESTONE
======================================================================

The project has crossed an important threshold.

It is no longer only a collection of isolated animation tests.

The accepted First Contact sequence composes:

    environment
    player camera direction
    Lara gaze
    Lara locomotion
    mutual eye contact
    authored speech
    realtime SALSA lipsync
    seating
    cross-legged seated pose
    camera movement while tracking Lara
    breathing
    blinking
    attention life
    animation ownership between subsystems

into one continuous directed scene.

This was the first time the major runtime systems were judged together as a
performance rather than independently.

The result was accepted by the user after some body-ownership and timing
corrections.

That acceptance is significant evidence for future architectural planning.


======================================================================
4. CURRENT UNITY SCENE
======================================================================

Primary current performance scene:

Assets/Scenes/FirstPerformanceVoid.unity

This scene has been manually tuned.

PRESERVE IT.

Do not casually rerun old builders, migrations, or environment-generation
tools over it.

The current scene contains:

    FirstPerformanceVoid
    ├── Environment
    ├── Performer / Lara
    ├── PerformanceMarkers
    ├── Player
    │   └── ViewRig
    │       └── HeadPose
    │           └── MainCamera
    ├── lounge / PerformerSeat
    └── FirstContactPerformance

The scene on current pushed main contains exactly one:

    Player
    ViewRig
    HeadPose
    MainCamera
    FaceViewTarget
    ViewMark_Wide
    ViewMark_Lara
    ViewMark_Lounge
    LaraCloseMark
    ViewMark_Final

and one installed FirstContactPerformance component.


======================================================================
5. ENVIRONMENT — FIRST PERFORMANCE VOID
======================================================================

The environment is intentionally minimal.

Visual concept:

    dark infinite void
    glossy black floor
    bright magenta neon edge lighting
    low circular performance platform
    lounge/chair
    rising illuminated smoke around the perimeter

The environment is a placeholder with a real vibe, not production environment
art.

It exists so character performance can be evaluated in a context that actually
feels like the game.

Do not turn it into a full nightclub or detailed architectural room unless the
user later asks.

The platform is mostly visual scenery.

The current locomotion system does not implement vertical/stair navigation, so
the raised platform was deliberately not made a required walking destination.


======================================================================
6. ENVIRONMENT HISTORY / SMOKE
======================================================================

The first fog approach used HDRP global fog plus Local Volumetric Fog.

That produced useful atmospheric experiments but eventually created an
unacceptable rectangular/hedge-like upper boundary.

The user rejected that look.

The scene was subsequently changed to use rising perimeter smoke based on
native ParticleSystems and an HDRP lit transparent particle shader.

Current relevant assets include:

Assets/FirstPerformanceVoid/
    Materials/
    Settings/
    Textures/
    Meshes/
    Prefabs/
    Shaders/

Current smoke approach:

    four perimeter ParticleSystems
    world-space simulation
    randomized lifetimes
    upward drift
    lateral/noise motion
    size growth
    soft opacity birth/death
    HDRP lit transparent billboards
    generated billowy texture
    magenta illumination

The old Local Volumetric Fog objects may remain as inactive fallback/reference
objects. They are not the current visual solution.

The scene also contains scene-specific lighting and copied Lara skin materials
that were tuned after early tests showed:

    Lara too dark
    original neon too red
    skin too glossy
    fog/smoke initially insufficiently visible

Those corrections are part of the current accepted visual state.

Do not casually revert them to builder defaults.


======================================================================
7. CHARACTER: LARA
======================================================================

Canonical runtime character:

DAZ Genesis 8 Female “Lara”

Canonical FBX:
Assets/TestCharacter/lara.fbx

Critical rule:

THE CANONICAL RUNTIME LARA FBX IS GENERIC.

Do not convert canonical Lara to Humanoid.

It contains:

    one main SkinnedMeshRenderer
    16 material slots
    approximately 101 blendshape channels in the established canonical asset

A DAZ Bridge export was previously used as a material donor.

The canonical Lara importer has exact-name remaps to the Bridge materials.

Later, scene-specific skin material copies were created for the void room to
reduce the glossy/plastic appearance without rewriting the core imported
materials.


======================================================================
8. RETARGETING ARCHITECTURE
======================================================================

Animation-source FBXs such as Kawaii/Kubold are Humanoid.

Canonical Lara runtime remains Generic.

A project-owned Humanoid Lara proxy exists for editor-side retargeting:

    laraHumanoid.fbx

Correct pipeline:

    Kawaii / Kubold Humanoid source
        ↓
    validated Lara Humanoid proxy/avatar
        ↓
    optional Humanoid mirror
        ↓
    editor-time sample/bake
        ↓
    Lara-native Generic AnimationClip / motion data
        ↓
    canonical Generic Lara runtime

Do not create a hidden second runtime source skeleton.

Do not copy bones every frame.

Humanoid is an editor/baking bridge.

Generic Lara is the runtime authority.


======================================================================
9. INSTALLED ANIMATION PACKS
======================================================================

Two important third-party animation libraries are in the Unity project:

Assets/KAWAII_ANIMATIOMS_100/

and:

Assets/FemaleMovementAnimsetPro/

Note the Kawaii directory is actually misspelled:

    KAWAII_ANIMATIOMS_100

That is its real installed path.

IMPORTANT:

The user intentionally removed/disabled unwanted PlayMaker-related content
from FemaleMovementAnimsetPro because it introduced dependencies the project
does not want.

DO NOT RESTORE PLAYMAKER SUPPORT.

Do not treat the missing PlayMaker/sample support as package corruption.

Do not package-update these animation packs unless explicitly requested.


======================================================================
10. LOCOMOTION — CURRENT SHAPE
======================================================================

The primary locomotion family came from Kawaii.

Chosen baseline:

    KA_Walk01 family

Kubold/FemaleMovementAnimsetPro remains useful as fallback/reference but the
project intentionally avoided randomly mixing animation families.

Kawaii locomotion playback was tuned around:

    0.665x

There are authored:

    walk
    start
    stop
    turns
    pivots / directional content

The production WalkTo runtime lives primarily in:

Assets/DazPose/Runtime/Performer/PerformerLocomotion.cs
Assets/DazPose/Runtime/Performer/PerformerLocomotionMotion.cs
Assets/DazPose/Runtime/Performer/PerformerLocomotionProfile.cs

Editor bake support lives under the animation-audit tooling.

Public semantic API includes:

    WalkTo(Vector3)
    WalkTo(Transform)

    WalkToAsync(Vector3)
    WalkToAsync(Transform)

Transform destinations snapshot the requested target information when required
by the locomotion semantics.

Locomotion currently uses baked root trajectory data and a Lara-native Generic
body animation.

The project intentionally keeps deterministic final placement.

DO NOT "fix" the exact endpoint correction away.

The required behavior is:

    authored travel
        ↓
    final convergence
        ↓
    exact requested final root position/facing

The last roughly half-second can converge position while locomotion ownership
fades.

This was deliberate because exact world placement matters for interaction and
seating.


======================================================================
11. LOCOMOTION LIMITATIONS
======================================================================

Current locomotion is NOT:

    NavMesh navigation
    pathfinding
    obstacle avoidance
    stairs
    arbitrary vertical traversal

It is intended for clean direct movement through currently authored open
spaces.

The FirstPerformanceVoid layout was designed around that fact.

Do not assume Lara can automatically navigate complex environment geometry.


======================================================================
12. BODY SOURCE / ANIMATION OWNERSHIP
======================================================================

The main body architecture now conceptually looks like:

    PerformerBodyPose
        ↓
    PerformerBodySourceMixer
        [persistent Pose / Locomotion]
        ↓
    PerformerSeatingLayer
        ↓
    PerformerBreathing
        ↓
    PerformerGaze + PerformerAttentionLife
        ↓
    PerformerExpressionLayer
        ↓
    PerformerBlink
        ↓
    Animator

SALSA speech articulation owns its explicitly assigned speech/viseme
blendshapes alongside this composition.

A key architectural lesson from First Contact was that technically correct
state changes can still expose ugly intermediate body poses.

The user noticed this during the continuous performance.

Post-First-Contact corrections therefore improved animation ownership through
movement transitions.


======================================================================
13. FIRST CONTACT BODY-OWNERSHIP CORRECTION
======================================================================

The accepted current behavior includes an explicit locomotion arrival-pose
hold.

During First Contact:

    Lara approaches player
        ↓
    locomotion reaches exact root destination
        ↓
    final locomotion body pose can remain owned
        ↓
    proximity beat
        ↓
    lounge movement begins
        ↓
    no intermediate flash of unrelated glamour idle

The FirstContact component uses:

    performer.SetHoldLocomotionArrivalPose(true)

before the close approach and releases the performance hold after seating has
taken ownership.

Seating independently protects its own approach pose until the seating layer
fully owns Lara.

These are separate ownership holds.

One owner must not accidentally release another owner's hold.


======================================================================
14. CURRENT BODY BLEND TIMING
======================================================================

After the First Contact visual test, body transition timing was also softened.

Current accepted policy includes:

    locomotion/seating body transitions
        minimum ~0.5 second weight overlap

and ordinary recovery back to standing idle takes at least:

    ~1.0 second

including cases where the authored stop/stand clip ends before that body fade
is complete.

A repeated locomotion clip was also corrected so the system does not blend a
Playable against itself with a zero input weight.

These are not theoretical future proposals.

They are part of the current accepted runtime after First Contact.


======================================================================
15. SEATING — CURRENT ARCHITECTURE
======================================================================

Environment seating uses:

    PerformerSeat

with two semantic transforms:

    ApproachAnchor
        = Lara's standing/root approach frame

    SeatAnchor
        = final pelvis/contact target

The SeatAnchor is NOT simply Lara's actor root.

The seating bake records pelvis/contact information and runtime solves the
actor root so the pelvis lands correctly.

This architecture is deliberate.

The current chair has been manually tuned for the actual Lara animation.


======================================================================
16. SEATING ANIMATION FAMILY
======================================================================

Current first seating family comes from Kawaii.

Main sequence:

    Standing
        ↓
    KA_Sit_Start
        ↓
    Basic seated
        ↓
    KA_Sit_CrossLegs_Start
        ↓
    KA_Sit_CrossLegs_Loop

Exit:

    CrossLegs
        ↓
    KA_Sit_CrossLegs_End
        ↓
    Basic
        ↓
    KA_Sit_End
        ↓
    Standing

Basic seated currently intentionally holds the final frame of Sit_Start rather
than forcing the Idle10 seated loop.

That was an explicit practical choice.

Do not replace it unless a future visual test motivates doing so.


======================================================================
17. RESPONSIVE CROSS-LEGS EXIT — P0.B1
======================================================================

An early implementation waited for the mathematically ideal exit phase near
the end of the 13-second CrossLegs loop.

That could make StandUp wait nearly an entire loop.

This was rejected.

Current behavior:

    arbitrary current CrossLegs loop frame
        ↓
    freeze current pose
        ↓
    blend for ~0.5 gameplay seconds
        ↓
    CrossLegs_End frame 0
        ↓
    play CrossLegs_End normally

The relevant state is now:

    PreparingUncross

not the old wait-for-seam behavior.

During the preparation blend:

    outgoing loop is frozen
    CrossLegs_End remains frozen at time 0
    actor root remains locked
    only mixer blend weight advances

After the blend:

    CrossLegs_End starts from time 0

Runtime no longer depends on CrossLegsExitLoopPhase.

The old measured seam metadata was retained as diagnostic/future information.


======================================================================
18. SEATING DEFERRED ITEMS
======================================================================

Known future issues intentionally NOT solved yet:

    foot/floor adaptation / IK
    heel-aware foot posture
    multiple seating profiles per performer runtime
    more seated styles
    potentially better Basic seated ambient motion
    smarter multi-exit motion matching if ever needed

The user manually rebuilt/tuned the current chair so Lara's feet contact the
floor well enough for current testing.

Do not reopen IK just because the system lacks it.

It is known debt, not an unnoticed bug.


======================================================================
19. PERSISTENT BODY POSE
======================================================================

`PerformerPose` is a ScriptableObject wrapper around a Lara-native animation
clip with transition metadata.

Pose semantics are persistent.

Conceptually:

    Pose(...)
        = choose Lara's persistent body state

A new pose supersedes the previous desired pose.

Pose transition supports:

    duration
    windup
    overshoot
    curve

This was intentionally richer than camera transitions because character body
performance can benefit from stylized motion.

Base-state capture prevents downstream layers from feeding their own output
back into future pose transitions.


======================================================================
20. BREATHING
======================================================================

Breathing is automatic ambient life.

It operates downstream of the body source.

It currently uses a small combination of DAZ morph/bone channels.

It is not something authored performances need to manually pulse every few
seconds.

The design principle is:

    ambient life should happen automatically

rather than clutter every scene script.


======================================================================
21. GAZE
======================================================================

Lara supports semantic gaze:

    LookAt(Transform)
    LookAt(Vector3)

and async forms.

The system drives eyes/head procedurally.

Eyes respond more quickly than the head.

Attention-life logic adds small natural variation rather than maintaining a
perfect robotic stare.

Gaze is persistent until replaced or cleared.

The First Contact scene uses reciprocal gaze between:

    Lara
and
    Player.HeadTransform


======================================================================
22. BLINK / ATTENTION LIFE
======================================================================

Blinking and attention micro-movement are automatic ambient layers.

They remain downstream enough in the facial stack to compose with authored
expressions/gaze.

They were specifically intended to make silent holds feel alive.

First Contact's final silent seated hold successfully exercises this idea.


======================================================================
23. EXPRESSIONS
======================================================================

Persistent expression support is implemented.

Public API includes:

    Expression(...)
    ExpressionAsync(...)
    ClearExpression(...)

Expression semantics:

    latest desired expression persists

Expression assets can contain:

    blendshape channels
and
    eligible facial-bone channels

The facial-bone patch intentionally preserved DAZ facial bones beneath the
upper/lower face rigs while excluding body/head/eye-control ownership that
belongs to other systems.

Expression sanitation excludes controls owned by:

    breathing
    blinking
    speech/lipsync

so the layers do not mechanically fight over exact properties.

IMPORTANT CURRENT NOTE:

The runtime expression system exists and has been proven structurally, but the
accepted First Contact sequence does not depend on authored Expression A/B/C
beats.

Do not assume the next performance needs an expression framework rewrite just
because First Contact used mostly gaze, body language and speech.


======================================================================
24. SPEECH
======================================================================

Speech is finite and queue-based.

Public API:

    Say(AudioClip)
    SayAsync(AudioClip)
    StopSpeaking()

Speech semantics are strict FIFO.

Example:

    Say(A)
    Say(B)

means B waits behind A.

`SayAsync(B)` completes when B itself finishes, including any queued lines
ahead of it.

Repeated use of the same clip creates distinct requests.

Disabling the performer clears transient speech state.

Speech is NOT a persistent pose-like state.


======================================================================
25. AUTHORED AUDIO CLIPS
======================================================================

Three test speech clips already exist:

    A
    B
    C

They are wired into the existing performer smoke harness.

First Contact currently uses:

    Speech A
    Speech B

Clip C remains available.

First Contact owns explicit references to A and B after installation rather
than querying the development smoke harness at runtime.

Do not replace these clips merely to make another systems test unless the user
wants different content.


======================================================================
26. SALSA LIPSYNC
======================================================================

SALSA LipSync is integrated.

Installed version was:

    2.5.6.150

The project uses:

    SALSA
    QueueProcessor

and intentionally does NOT currently depend on:

    EmoteR
    Eyes

SALSA observes the existing speech AudioSource in real time.

It is waveform-driven and language-agnostic.

SALSA owns the exact speech/viseme blendshapes assigned to it.

Expressions must not own those same exact properties.

First Contact successfully uses SALSA in the full performance context,
including seated speech.


======================================================================
27. PLAYER / CAMERA ARCHITECTURE
======================================================================

The camera is now a first-class Player concept.

Current hierarchy:

    Player
    ├── PlayerController
    └── ViewRig
        └── HeadPose
            └── MainCamera

`PlayerController` is the semantic public facade.

`PlayerView` implements the camera/head motion.


======================================================================
28. WHY VIEWRIG AND HEADPOSE ARE SEPARATE
======================================================================

Current desktop behavior:

    ViewRig
        = game/director controlled player head orientation

    HeadPose
        = identity

    MainCamera
        = identity beneath HeadPose

Future XR intention:

    ViewRig
        = directed/world-space player position and broad orientation

    HeadPose
        = local tracked HMD movement

    MainCamera
        = actual rendered eyes

This seam exists specifically so future VR head tracking does not have to
fight the current director-controlled camera system.

Do not collapse ViewRig and HeadPose merely because HeadPose is identity today.


======================================================================
29. PLAYER VIEW API
======================================================================

Position and orientation are independent channels.

Main semantic API:

    MoveTo(...)
    MoveToAsync(...)

    LookAt(...)
    LookAtAsync(...)

    Track(...)
    StopTracking()

`MoveTo(Transform)` means:

    move player's head/world position to target.position

It intentionally does NOT silently adopt the target's rotation.

Orientation is separately controlled.


======================================================================
30. FINITE PLAYER LOOK
======================================================================

`LookAt(Transform)` means:

    smoothly turn toward a target
    continue sampling the target while acquiring it
    when the finite look completes:
        freeze the final orientation

If Lara then moves away:

    finite LookAt does NOT continue tracking her

This was a deliberate semantic distinction.


======================================================================
31. PLAYER TRACK
======================================================================

`Track(Transform)` means:

    smoothly acquire target
        ↓
    continue following indefinitely

Default tracking response is slightly damped rather than a perfect turret lock.

Current default follow response:

    ~0.20 seconds

This gave the Player viewpoint a little weight.


======================================================================
32. STOP TRACKING
======================================================================

`StopTracking()` means:

    stop calculating new target orientation
    hold EXACTLY the current heading

It does not restore an earlier/default rotation.


======================================================================
33. PLAYER VIEW TRANSITIONS
======================================================================

Camera movement uses:

    ViewTransition
        Duration
        AnimationCurve

Standard duration overloads use smooth ease-in/ease-out.

Camera interpolation is deterministic.

It does NOT use:

    position = Lerp(position, target, deltaTime * speed)

which has vague arrival timing.

Finite movement instead captures:

    start
    destination
    duration

and samples normalized progress.

Camera transition curves are clamped to 0..1.

Unlike character poses, camera transitions intentionally do NOT support
windup/overshoot.

Do not add character-like overshoot to the player's head casually.


======================================================================
34. INDEPENDENT PLAYER CHANNELS
======================================================================

Player position and orientation can run simultaneously.

Example:

    Player MoveTo lounge
    WHILE
    Player Track Lara

is valid and is used by First Contact.

A new movement command supersedes the old movement command.

A new orientation command supersedes the old orientation command.

Movement does NOT supersede orientation and vice versa.


======================================================================
35. PLAYER COMMAND REVISIONS
======================================================================

PlayerView now exposes command revisions:

    PositionCommandRevision
    OrientationCommandRevision

These were added so an authored performance can detect when an external debug
or user command has replaced an action it thought it owned.

First Contact uses these revisions to abort safely if its camera direction is
interfered with.

This is intentionally small ownership machinery, not a general director bus.


======================================================================
36. PLAYER FACE TARGET
======================================================================

Lara contains:

    FaceViewTarget

under her animated head.

The player camera tracks/looks at this semantic face target rather than Lara's
actor root.

This matters because the actor root would aim the camera at her feet/pelvis
depending on pose.

Conversely, Lara's gaze targets:

    Player.HeadTransform

rather than an arbitrary bare camera object.


======================================================================
37. FIRST CONTACT — CURRENT STATUS
======================================================================

Milestone name:

    P0.E — First Contact

Runtime component:

Assets/FirstPerformanceVoid/FirstContactPerformance.cs

Editor installer:

Assets/FirstPerformanceVoid/Editor/FirstContactPerformanceSetup.cs

Documentation:

docs/FirstContactPerformance.md

Current state:

    INSTALLED
    RUN
    TWEAKED
    ACCEPTED BY USER

This is the first accepted continuous directed Lara performance.


======================================================================
38. FIRST CONTACT — DESIGN INTENT
======================================================================

First Contact was deliberately NOT built as a general director system.

It is one authored sequence.

That was intentional.

The point was to discover:

    what timing feels good
    how player camera movement feels
    how locomotion composes with gaze
    how seating fits into a performance
    whether SALSA survives full-context performance
    where animation ownership breaks illusion
    whether the environment is sufficient to judge Lara

Only after seeing a real scene work should larger performance-authoring
abstractions be considered.


======================================================================
39. FIRST CONTACT — SEQUENCE
======================================================================

The accepted script conceptually does:

OPENING

    clear Lara gaze
    hold existing composition for ~2.25 sec

PLAYER NOTICES LARA

    Player finite LookAt Lara.FaceViewTarget over ~1.2 sec

RECIPROCAL GAZE

    after ~0.45 sec Lara begins looking at Player.HeadTransform
    complete player look
    hold mutual gaze ~0.7 sec

SPEECH A

    Lara plays authored Speech A
    wait for actual speech completion
    brief pause

APPROACH

    Player begins tracking Lara's face
        acquire ~0.75 sec
        follow response ~0.20 sec

    Lara WalkTo close mark

    ~0.3 sec later Player begins a ~3.5 sec MoveTo toward Lara composition

    wait for BOTH actual Lara arrival and Player movement completion

PROXIMITY

    hold ~0.9 sec

LOUNGE

    Lara SitAt lounge in CrossLegs style
        this composite action performs:
            lounge approach
            alignment
            Sit_Start
            CrossLegs_Start
            CrossLegs_Loop

    ~0.6 sec after SitAt begins:
        Player moves toward lounge composition over ~4.5 sec

    face tracking continues during the move

    wait for both:
        SeatingCompletion.Seated
        Player movement Completed

SEATED HOLD

    ~0.8 sec

FINAL PUSH

    Player moves toward final closer view over ~4 sec

    ~0.5 sec later:
        Lara plays Speech B

    wait for:
        Player movement Completed
        Speech B Finished

FINAL HOLD

    remain tracking ~1 sec
    StopTracking
    hold the final composition ~2 sec

END

    Lara remains seated
    Lara continues gaze toward Player
    breathing/blinking/ambient life continue


======================================================================
40. FIRST CONTACT TIMING MODEL
======================================================================

The sequence is NOT forced to exactly 30.00 seconds.

That was intentional.

Authored pauses use scaled gameplay seconds.

Actual long-running operations use real completion semantics:

    WalkTo waits for Arrived
    SitAt waits for Seated
    Say waits for Finished
    camera move waits for Completed

The original timing target was approximately:

    28–38 seconds

depending on actual movement/speech duration.

The accepted visual result matters more than a stopwatch reading.


======================================================================
41. FIRST CONTACT INTERRUPTION POLICY
======================================================================

First Contact is intentionally defensive.

If another control replaces one of its camera actions:

    performance aborts

If a required operation returns an unexpected completion:

    performance aborts

It does not continue firing later beats after losing ownership.

Cleanup attempts to release only state First Contact itself still owns.

The performer currently does not expose a universal semantic cancellation API
for every locomotion/seating action, so an already-started character action may
finish after performance abort.

That limitation is known.

Do not invent a giant cancellation framework merely because of it unless later
experience demands one.


======================================================================
42. FIRST CONTACT RUN MODEL
======================================================================

First Contact is manually started from the room development UI.

Button:

    RUN FIRST CONTACT

It does not autoplay when entering Play Mode.

Only one run is allowed per Play Mode session.

Re-enter Play Mode to replay from the clean authored starting state.

This was useful for evaluation and prevents ambiguous partial reruns.


======================================================================
43. WHAT FIRST CONTACT TAUGHT US
======================================================================

The most important demonstrated lessons are:

1. The existing semantic APIs are sufficient to compose a coherent short scene.

2. Player movement + Player face tracking can run concurrently and feel useful.

3. Lara locomotion + gaze + camera motion can coexist.

4. SitAt can function as a real performance beat, not merely an isolated test.

5. Speech/SALSA works while the rest of the character stack is active.

6. Silent holds are viable because breathing/blinking/attention keep Lara alive.

7. Animation ownership between large body systems matters enormously.

8. A one-frame or brief exposure of the wrong persistent body pose can damage
   the illusion even when all individual subsystems are technically correct.

9. It was useful to fix those ownership seams AFTER experiencing them in a
   real performance rather than predicting dozens of hypothetical seams first.

Preserve that development philosophy.


======================================================================
44. DAZ POSE / EXPRESSION AUTHORING TOOL
======================================================================

The repository also contains the standalone DazPoseWizard tooling.

Solution:

DazPoseTool.sln

Major projects:

src/DazPose.Core
src/DazPose.App

DazPose.App is an Avalonia desktop application.

DazPose.Core owns:

    DSON reading
    Genesis parsing
    pose-channel parsing
    static transform evaluation
    canonical export

Canonical interchange:

    .dazpose.json

Conceptual pipeline:

    DAZ library / .duf
        ↓
    DazPoseWizard
        ↓
    DazPose.Core
        ↓
    canonical .dazpose.json
        ↓
    Unity importer/resolver
        ↓
    Lara-native clips / wrappers

Unity does NOT parse DSON directly.


======================================================================
45. DAZ STATIC POSE / MORPH PIPELINE
======================================================================

The tool already proved:

    DAZ skeletal pose
    +
    direct morph/blendshape data
        ↓
    canonical representation
        ↓
    Unity native AnimationClip
        ↓
    Transform curves
    +
    SkinnedMeshRenderer blendshape curves

Stage 5's minimum direct blendshape proof is complete.

Do not expand that proof into every possible DAZ control semantics unless the
game actually needs it.


======================================================================
46. EXPRESSION / FACIAL-BONE IMPORT HISTORY
======================================================================

P0.8 introduced typed Pose vs Expression assets.

Conceptual asset kinds:

    Pose
    Expression

Expressions are relative overlays against the incoming live base rather than
whole-character replacements.

P0.8.1 added eligible DAZ facial-bone animation to expressions in addition to
blendshapes.

Eligible facial-bone descendants are limited so expression clips do not steal:

    body ownership
    head-look ownership
    eye-look ownership

Speech later composed with this by owning explicit viseme properties rather
than deleting the facial-bone expression support.


======================================================================
47. MATERIAL / CHARACTER PIPELINE HISTORY
======================================================================

Canonical Lara and DAZ Bridge Lara both had 16 corresponding material names.

Exact-name remaps were added to the canonical FBX importer metadata.

The Bridge materials provided a practical PBR donor setup.

This was explicitly considered "good enough" for the prototype.

Future toon/anime shading has been discussed but not implemented as the final
character shader.

Do not derail gameplay development into a full shader rewrite without a
demonstrated reason.


======================================================================
48. DAZ GEOGRAFT / ANATOMY PIPELINE — NOT SOLVED
======================================================================

A known future character-pipeline issue is DAZ geografts and additional morphs.

Earlier Bridge exports successfully brought expected facial morphs but not all
desired geograft-associated controls.

Future investigation areas discussed:

    straight DAZ → FBX export
    Blender bridge
    geograft mesh integration / mesh surgery where required
    preserving additional morph controls in Unity

This is known future work.

It did NOT block First Contact.

Do not assume it has already been solved.


======================================================================
49. DFORCE / CLOTHING — FUTURE CONCERN
======================================================================

Many DAZ outfits use dForce cloth simulation.

How best to migrate those clothes into Unity has been discussed/researched,
but there is no universal production dForce-to-Unity solution currently baked
into this project.

This is separate from the core performer architecture.

Do not conflate clothing simulation work with character motion or pose
retargeting.


======================================================================
50. WARDROBE / FOOTWEAR POSTURE — FUTURE DESIGN
======================================================================

A future equipment-driven posture concept has been discussed for heels and
footwear.

Possible semantic data:

    heel rise
    foot pitch
    toe pitch
    local offsets
    sole/contact offset

The important conceptual distinction is:

    footwear pose conformation
versus
    environment grounding / IK

Do not solve high heels merely by raising Lara's global root.

That fails for sitting and lying.

No production version of this system currently exists.


======================================================================
51. GESTURES — DEFERRED
======================================================================

A semantic gesture layer is still desired.

Examples may eventually include:

    gesture(...)
    Emphasize()

The long-term idea is to allow short performance punctuation layered onto a
persistent body pose.

This has NOT yet been needed to make First Contact work.

Do not assume its final API from this handoff.


======================================================================
52. HIGHER-LEVEL PERFORMANCE DIRECTION — OPEN DESIGN SPACE
======================================================================

FirstContactPerformance is intentionally a hand-authored one-off component.

The project has NOT yet committed to a final general-purpose:

    scene director
    timeline system
    session scripting system
    behavior tree
    dialogue graph
    authoring language
    editor UI

This is now a legitimate topic for discussion because there is real evidence
from an accepted authored performance.

But no particular abstraction has been chosen yet.

A new chat should discuss this with the user rather than assuming that the
obvious next move is to generalize FirstContactPerformance.


======================================================================
53. FUTURE GAME CONCEPTS ALREADY DISCUSSED
======================================================================

Longer-term game/session ideas include:

    directed character performances
    teasing / JOI-style sessions
    toy interaction
    Bluetooth toy controls
    funscript-driven/procedural motion
    broader animation/gesture library
    more character poses
    more environments
    more session logic

These are direction-of-travel ideas, not authorization to implement them now.


======================================================================
54. PLAYER FUTURE POSSIBILITIES
======================================================================

The Player object currently owns almost entirely viewpoint.

Future Player responsibilities might include:

    input
    VR head tracking
    optional visible player body/model
    interaction state
    perhaps hands/controllers
    other session-level player representation

None of those systems exist merely because the `Player` hierarchy exists.

Do not pre-build them.


======================================================================
55. CAMERA FUTURE POSSIBILITIES
======================================================================

Current camera system intentionally stops at:

    MoveTo
    LookAt
    Track
    StopTracking

Possible future camera/view features could include:

    FOV control
    shake
    paths / splines
    camera marks with richer metadata
    first-person input
    VR / OpenXR
    collision / obstruction behavior

None is currently required by the accepted First Contact milestone.

Do not add Cinemachine or another camera framework without a concrete need.


======================================================================
56. VR CONTEXT
======================================================================

The current scene/project is HDRP.

Future PCVR has been considered.

The Player/ViewRig/HeadPose hierarchy was explicitly shaped so an XR tracked
head can later layer local pose below world/director pose.

Standalone/mobile VR may eventually force rendering/performance tradeoffs.

No render-pipeline migration decision has been made.

Do not change HDRP preemptively.


======================================================================
57. ENVIRONMENT PERFORMANCE / VR CONTEXT
======================================================================

The environment is intentionally simple:

    primitive geometry
    modest material count
    bounded smoke
    modular effects

The smoke/fog layer can be reduced/replaced later if VR performance requires
it.

Current visual quality was prioritized enough to make First Contact feel real.

Do not optimize the atmosphere away before profiling a real target.


======================================================================
58. DAZPOSEWIZARD DEFERRED BACKLOG
======================================================================

The root file:

    DazPoseWizard_Deferred_Work.md

contains deliberately deferred authoring-tool ideas.

Important examples:

A. Other Genesis-generation retargeting

    G8.1F → canonical G8F
    G9 → canonical G8F
    later G8M / G8.1M

Architectural rule:

    generation-specific retargeting belongs in DazPose.Core

Unity should continue consuming the canonical G8F representation rather than
gaining a separate runtime path per DAZ generation.


B. Embedded Unity preview inside DazPoseWizard

Potential future architecture:

    Avalonia remains main application shell
        +
    embedded Unity rendering surface

The preview should eventually support:

    orbit
    zoom
    real Unity character/material rendering


C. Per-destination "unconvert" workflow

If implemented later:

    DAZ source remains read-only
    Unity owns Unity asset deletion
    dependency/reference preflight
    explicit destructive override


D. Fat reference Lara vs lean runtime mesh

Current philosophy:

    authoring/reference FBX may contain a broad morph superset

Possible later optimization:

    determine actually used morphs
        ↓
    generate lean runtime mesh

Do not prematurely optimize this.


======================================================================
59. CURRENT ARCHITECTURAL PHILOSOPHY
======================================================================

Keep public runtime concepts semantic and small.

Current examples:

    SuccubusPerformer
        Pose
        Expression
        LookAt
        WalkTo
        SitAt
        Say

    PlayerController
        MoveTo
        LookAt
        Track
        StopTracking

Behind those facades, focused components are acceptable.

Avoid forcing gameplay/directing code to understand:

    Playables
    retarget bones
    mixer weights
    SALSA internals
    pelvis offsets
    root trajectory bake format

Those are implementation details.


======================================================================
60. PERSISTENT STATE VS FINITE ACTIONS
======================================================================

This distinction has been useful and should be preserved unless evidence says
otherwise.

Persistent state:

    Pose
    Expression
    Lara gaze/look target
    Player tracking

Finite actions:

    WalkTo
    SitAt / StandUp
    Say
    finite Player MoveTo
    finite Player LookAt

Ambient automatic behavior:

    breathing
    blinking
    attention variation

This separation has helped keep authored performance understandable.


======================================================================
61. IMPORTANT USER WORKFLOW PREFERENCE
======================================================================

The user generally does NOT want ChatGPT itself making unsolicited repository
changes.

Preferred workflow:

    ChatGPT:
        inspect source
        reason about architecture
        identify actual problems
        write execution tickets/packets

    Codex/Astra:
        apply implementation

    User:
        manually evaluate subjective result
        commit/push when happy

Only modify the repo directly if the user explicitly asks.

When the user says changes are committed/pushed:

    re-ground against actual GitHub main before reviewing them

Do not review from remembered architecture alone.


======================================================================
62. IMPORTANT COMMUNICATION PREFERENCE
======================================================================

The user is highly technical and direct.

They do not need vague descriptions of what should conceptually be done when
they ask how to operate a tool or implement something.

Prefer:

    concrete
    code-grounded
    exact
    execution-ready

Do not make a pile of assumptions and then build a huge ticket on top of them.

When source matters:

    inspect it first

The user may swear when frustrated. Do not become patronizing or defensive.


======================================================================
63. EXECUTION-PACKET STYLE
======================================================================

The user likes implementation packets that make the execution agent as dumb as
possible.

A good packet should specify:

    baseline
    exact goal
    architecture decision
    files likely involved
    invariants
    behavior semantics
    non-goals
    acceptance tests
    stop condition
    completion report

For artistic tasks, a more capable reasoning model can still help tune visual
details.

The user previously selected Sol 6.1 Medium for the environment task because
the specification was constrained but some artistic judgment was valuable.


======================================================================
64. AVOID RE-LITIGATING ACCEPTED DECISIONS
======================================================================

Unless a new observed problem forces reconsideration, do not reopen:

    Generic canonical Lara
    Humanoid editor retarget proxy
    exact locomotion endpoint correction
    Kawaii Walk01 baseline
    current seating anchor model
    0.5s simple CrossLegs exit blend
    no foot IK yet
    current Player/ViewRig/HeadPose split
    independent Player movement/orientation channels
    current First Contact body ownership corrections
    FirstPerformanceVoid as adequate first-test environment
    First Contact itself

These have enough evidence behind them for now.


======================================================================
65. KNOWN CURRENT TECHNICAL DEBT
======================================================================

Known but non-blocking items include:

    no seated foot/floor IK
    no generalized heel/footwear posture system
    one seating profile per performer runtime
    limited seating styles
    Basic seated hold is static final Sit_Start frame
    no general gesture layer
    no generalized director/session authoring system
    no locomotion obstacle avoidance / NavMesh
    no vertical/stair locomotion
    no VR integration
    no player input controller
    no player avatar/body
    no finalized toon/anime character shader
    geograft/anatomy morph export unresolved
    dForce clothing conversion unresolved
    broad DAZ generation retargeting deferred

These are a backlog, not instructions for what to implement next.


======================================================================
66. HISTORICAL MILESTONE SUMMARY
======================================================================

The rough progression was:

DAZ POSE TOOLING
    prove DSON → canonical G8F pose data
    prove Unity direct pose application
    prove native animation clip generation
    prove direct blendshape/morph support

P0.4-ish BODY PERFORMANCE FOUNDATION
    persistent pose API
    transition semantics

P0.5
    base-state capture / stop overlay feedback

P0.6
    procedural gaze

P0.7
    attention variation + blinking

P0.8
    typed Pose vs Expression pipeline

P0.8.1
    facial-bone expression support

P0.9A
    queued finite speech API

P0.9B
    SALSA realtime lipsync

P0.A
    animation-library audit
    establish Kawaii/Kubold content and retarget/bake strategy

P0.A1
    production WalkTo locomotion

P0.B
    anchored SitAt / StandUp
    Basic and CrossLegs seated styles

P0.B1
    responsive CrossLegs exit

P0.C
    FirstPerformanceVoid environment

P0.D
    directable Player/View camera

P0.E
    FIRST CONTACT
        first complete authored performance
        subsequent body-ownership/timing correction
        manually accepted by user


======================================================================
67. CURRENT SUCCESS CRITERION
======================================================================

The project has now proved that this stack can produce a short scene the user
is happy to watch.

That is more important than whether every hypothetical runtime feature exists.

The next planning conversation should therefore ask questions such as:

    What did First Contact make us want more of?
    What broke immersion despite being mechanically correct?
    What kinds of scenes should the game eventually support?
    Which missing capability would unlock the most interesting next experiment?
    Which current one-off implementation has earned generalization?
    Which apparent gaps can continue to wait?

Those are planning questions, NOT predetermined implementation steps.


======================================================================
68. HANDOFF STOP POINT
======================================================================

CURRENT STATE:

    FirstPerformanceVoid:
        accepted as sufficient performance-test environment

    Lara performer stack:
        working

    locomotion:
        working

    seating:
        working

    gaze / attention / blink / breathing:
        working

    speech / SALSA:
        working

    Player camera:
        working and visually accepted

    First Contact:
        WORKING, TWEAKED, AND ACCEPTED

There is intentionally NO mandated "next ticket" in this handoff.

The next chat should first orient itself to this history and current source,
then plan future direction interactively with the user rather than assuming
the roadmap from here.