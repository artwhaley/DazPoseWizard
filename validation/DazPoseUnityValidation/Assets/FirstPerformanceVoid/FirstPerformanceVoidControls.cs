using System;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Player;
using UnityEngine;

namespace DazPose.FirstPerformanceVoid
{
    /// <summary>Compact development controls for the existing performer and directable Player APIs.</summary>
    public sealed class FirstPerformanceVoidControls : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private Transform acrossFloor;
        [SerializeField] private Transform nearPlatform;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform viewMarkWide;
        [SerializeField] private Transform viewMarkLara;
        [SerializeField] private Transform viewMarkLounge;
        [SerializeField] private Transform laraFaceViewTarget;
        [SerializeField] private FirstContactPerformance firstContact;
        [Header("Magic Vocabulary")]
        [SerializeField] private PerformerMagicCatalog magicCatalog;
        [Header("P0.G2 Particle Body Acceptance")]
        [SerializeField] private PerformerParticleBody particleBody;
        [Header("P0.F Teleport Acceptance")]
        [SerializeField] private Transform teleportMarkA;
        [SerializeField] private Transform teleportMarkB;
        [SerializeField] private PerformerPose teleportArrivalPose;
        [Header("P0.Gesture Acceptance")]
        [SerializeField] private PerformerGesture gestureAcceptanceWave;
        [Header("P0.Perform Acceptance")]
        [SerializeField] private PerformerAction jumpForJoyAction;
        [SerializeField] private PerformerAction displacedRecoveryTestAction;
        [Header("Dissolve Test Timing")]
        [Tooltip("Total seconds for the existing DISSOLVE A/B buttons. All effect phases scale together.")]
        [SerializeField, Min(0.01f)] private float dissolveDurationSeconds = 3f;
        [SerializeField, HideInInspector] private int lightingRevision;
        public int LightingRevision => lightingRevision;
        private ParticleSystem[] smokeEmitters;
        private string status = "Use the existing performer panel for seating, expressions and speech.";
        private string teleportStatus = "Run Install Teleport Acceptance Harness in Edit Mode.";
        private bool teleportTestRunning;
        private string dissolveShaderStatus = "Run the native dissolve shader acceptance controls in Play Mode.";
        private Vector2 panelScrollPosition;
        private bool dissolveShaderAnimating;
        private float dissolveShaderAnimationElapsed;
        private bool motionTrackingRoot;
        private float motionRunStartedRealtime;
        private float motionMeasuredRunSeconds;
        private float motionRootTranslationDrift;
        private float motionRootRotationDrift;
        private Vector3 motionRootStartPosition;
        private Quaternion motionRootStartRotation;
        private bool dissolveTestRunning;
        private string dissolveTestStatus = "Run the P0.G3 DissolveTo acceptance checks in Play Mode.";
        private bool visibilityTestRunning;
        private string visibilityTestStatus = "DissolveOut leaves Lara hidden; use a matching IN button to restore her.";
        private int selectedSpellIndex;
        private int selectedAuraIndex;
        private bool spellDropdownOpen;
        private bool auraDropdownOpen;
        private string magicStatus = "Generate the starter Magic catalog in Edit Mode.";
        private string gestureStatus = "Run Tools > DAZ Pose > Gesture > Generate Gesture Acceptance Assets in Edit Mode.";
        private string actionStatus = "Run Tools > DAZ Pose > Action > Generate Jump for Joy Acceptance Assets in Edit Mode.";
        private const float DissolveShaderHalfCycleSeconds = 1f;

        public PerformerGesture GestureAcceptanceWave => gestureAcceptanceWave;
        public PerformerAction JumpForJoyAction => jumpForJoyAction;
        public PerformerAction DisplacedRecoveryTestAction => displacedRecoveryTestAction;

        public void ConfigureFirstContact(FirstContactPerformance performance) => firstContact = performance;

        public void ConfigureMagicCatalog(PerformerMagicCatalog catalog)
        {
            magicCatalog = catalog;
            magicStatus = catalog != null ? "Ready; Cast events overlap and Aura follows its live target." : "No Magic catalog assigned.";
        }

        public void ConfigureTeleportAcceptance(Transform markA, Transform markB, PerformerPose arrivalPose)
        {
            if (teleportMarkA == null) teleportMarkA = markA;
            if (teleportMarkB == null) teleportMarkB = markB;
            if (teleportArrivalPose == null) teleportArrivalPose = arrivalPose;
        }

        public void Configure(SuccubusPerformer owner, Transform across, Transform platform, Transform camera)
        {
            performer = owner;
            acrossFloor = across;
            nearPlatform = platform;
            cameraTarget = camera;
        }

        public void ConfigurePlayerView(PlayerController controller, Transform wide, Transform lara,
            Transform lounge, Transform faceViewTarget)
        {
            playerController = controller;
            viewMarkWide = wide;
            viewMarkLara = lara;
            viewMarkLounge = lounge;
            laraFaceViewTarget = faceViewTarget;
            cameraTarget = controller != null ? controller.HeadTransform : cameraTarget;
        }

        private void Awake()
        {
            smokeEmitters = GetComponentsInChildren<ParticleSystem>();
            if (particleBody != null) particleBody.ConfigureDebugVisibilityOwner(performer);
            if (magicCatalog != null)
                magicStatus = "Ready; Cast events overlap and Aura follows its live target.";
        }

        private void Update()
        {
            UpdateMotionRootDrift();
            if (!dissolveShaderAnimating) return;
            if (performer == null || !performer.DissolveShaderAcceptanceAvailable)
            {
                dissolveShaderAnimating = false;
                return;
            }

            dissolveShaderAnimationElapsed += Time.deltaTime;
            float elapsed = dissolveShaderAnimationElapsed;
            float duration = DissolveShaderHalfCycleSeconds;
            if (elapsed < duration)
            {
                ApplyDissolveShaderState(true, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                dissolveShaderStatus = "Animating shader dissolve out.";
            }
            else if (elapsed < duration * 2f)
            {
                float progress = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - duration) / duration);
                ApplyDissolveShaderState(true, progress);
                dissolveShaderStatus = "Animating shader dissolve in.";
            }
            else
            {
                dissolveShaderAnimating = false;
                ApplyDissolveShaderState(false, 0f);
                dissolveShaderStatus = "Animation complete; dissolve disabled and Lara restored.";
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Mathf.Max(454f, Screen.width - 286f), 12f, 274f, Mathf.Min(900f, Screen.height - 24f)),
                "First Performance Void", GUI.skin.window);
            panelScrollPosition = GUILayout.BeginScrollView(panelScrollPosition,
                GUILayout.Width(274f), GUILayout.Height(Mathf.Max(160f, Mathf.Min(900f, Screen.height - 54f))));
            bool enabled = GUI.enabled;
            GUILayout.Label("FIRST CONTACT", GUI.skin.box);
            GUI.enabled = enabled && firstContact != null && firstContact.CanRun;
            if (GUILayout.Button("RUN FIRST CONTACT")) firstContact.Run();
            GUI.enabled = enabled;
            GUILayout.Label("Status: " + (firstContact != null ? firstContact.Status : "Run Install First Contact Performance"));
            if (firstContact != null) GUILayout.Label("Elapsed: " + firstContact.Elapsed.ToString("F1") + "s");
            DrawMagicControls(enabled);
            DrawMotionControls(enabled);
            GUILayout.Space(5f);
            GUILayout.Label("P0.F TELEPORT", GUI.skin.box);
            bool teleportEnabled = enabled && !teleportTestRunning && performer != null
                && performer.TeleportAvailable && performer.VisibilityState == PerformerVisibilityState.Visible
                && !performer.IsDissolving;
            GUI.enabled = teleportEnabled && teleportMarkA != null && teleportMarkB != null;
            if (GUILayout.Button("TELEPORT A → B")) RunTeleportTest(teleportMarkB, null);
            GUI.enabled = teleportEnabled && teleportMarkA != null && teleportMarkB != null;
            if (GUILayout.Button("TELEPORT B → A")) RunTeleportTest(teleportMarkA, null);
            GUI.enabled = teleportEnabled && teleportMarkB != null && teleportArrivalPose != null;
            if (GUILayout.Button("TELEPORT B + ARRIVAL POSE")) RunTeleportTest(teleportMarkB, teleportArrivalPose);
            GUI.enabled = enabled;
            GUILayout.Label("Teleport: " + teleportStatus);

            GUILayout.Space(5f);
            GUILayout.Label("P0.G1 DISSOLVE SHADER", GUI.skin.box);
            bool shaderAcceptanceEnabled = enabled && !dissolveShaderAnimating && performer != null
                && performer.DissolveShaderAcceptanceAvailable
                && performer.VisibilityState == PerformerVisibilityState.Visible && !performer.IsDissolving;
            GUI.enabled = shaderAcceptanceEnabled;
            if (GUILayout.Button("DISSOLVE SHADER 0%")) SetDissolveShaderAcceptanceState(false, 0f);
            if (GUILayout.Button("DISSOLVE SHADER 25%")) SetDissolveShaderAcceptanceState(true, 0.25f);
            if (GUILayout.Button("DISSOLVE SHADER 50%")) SetDissolveShaderAcceptanceState(true, 0.5f);
            if (GUILayout.Button("DISSOLVE SHADER 75%")) SetDissolveShaderAcceptanceState(true, 0.75f);
            if (GUILayout.Button("DISSOLVE SHADER 100%")) SetDissolveShaderAcceptanceState(true, 1f);
            if (GUILayout.Button("DISSOLVE SHADER ANIMATE OUT/IN"))
            {
                dissolveShaderAnimationElapsed = 0f;
                dissolveShaderAnimating = true;
                dissolveShaderStatus = "Starting shader-only dissolve acceptance animation.";
            }
            GUI.enabled = enabled;
            GUILayout.Label("Shader: " + dissolveShaderStatus);

            GUILayout.Space(5f);
            GUILayout.Label("P0.G3 DISSOLVE TO", GUI.skin.box);
            bool atDissolveA = teleportMarkA != null
                && Vector3.Distance(performer != null ? performer.transform.position : Vector3.zero, teleportMarkA.position) < 0.25f;
            bool atDissolveB = teleportMarkB != null
                && Vector3.Distance(performer != null ? performer.transform.position : Vector3.zero, teleportMarkB.position) < 0.25f;
            PerformerPoseSmokeHarness poseHarness = performer != null
                ? performer.GetComponent<PerformerPoseSmokeHarness>()
                : null;
            PerformerPose dissolveArrivalPose = poseHarness != null ? poseHarness.PoseB : null;
            bool dissolveEnabled = enabled && !dissolveTestRunning && !dissolveShaderAnimating
                && performer != null && performer.DissolveAvailable
                && performer.VisibilityState == PerformerVisibilityState.Visible && !performer.IsDissolving
                && !performer.IsDissolveShaderAcceptanceActive
                && !performer.IsTeleporting && !performer.IsLocomoting
                && performer.SeatingState == PerformerSeatingState.Standing;
            GUI.enabled = dissolveEnabled && atDissolveA && teleportMarkB != null;
            if (GUILayout.Button("DISSOLVE A → B")) RunDissolveTest(teleportMarkB, null);
            GUI.enabled = dissolveEnabled && atDissolveB && teleportMarkA != null;
            if (GUILayout.Button("DISSOLVE B → A")) RunDissolveTest(teleportMarkA, null);
            GUI.enabled = dissolveEnabled && atDissolveA && teleportMarkB != null && dissolveArrivalPose != null;
            if (GUILayout.Button("DISSOLVE A → B + POSE B")) RunDissolveTest(teleportMarkB, dissolveArrivalPose);
            GUI.enabled = enabled;
            GUILayout.Label("DissolveTo: " + dissolveTestStatus);
            GUILayout.Label("A/B dissolves enable within 0.25 m of the matching TeleportMark. Use P0.F teleport controls to reset position.");

            GUILayout.Space(5f);
            GUILayout.Label("P0.H PERSISTENT VISIBILITY", GUI.skin.box);
            GUILayout.Label("Visibility: " + (performer != null ? performer.VisibilityState.ToString() : "No performer"), GUI.skin.box);
            bool visibilityAvailable = enabled && !visibilityTestRunning && !dissolveShaderAnimating && performer != null
                && performer.DissolveAvailable && !performer.IsDissolving && !performer.IsTeleporting
                && !performer.IsLocomoting && !performer.IsDissolveShaderAcceptanceActive;
            bool canDissolveOut = visibilityAvailable && performer.VisibilityState == PerformerVisibilityState.Visible;
            bool canDissolveIn = visibilityAvailable && performer.VisibilityState == PerformerVisibilityState.Hidden;
            GUI.enabled = canDissolveOut;
            if (GUILayout.Button("OUT 1s")) RunVisibilityTest(true, 1f);
            GUI.enabled = canDissolveIn;
            if (GUILayout.Button("IN 1s")) RunVisibilityTest(false, 1f);
            GUI.enabled = canDissolveOut;
            if (GUILayout.Button("OUT 3s")) RunVisibilityTest(true, 3f);
            GUI.enabled = canDissolveIn;
            if (GUILayout.Button("IN 3s")) RunVisibilityTest(false, 3f);
            GUI.enabled = canDissolveOut;
            if (GUILayout.Button("OUT 5s")) RunVisibilityTest(true, 5f);
            GUI.enabled = canDissolveIn;
            if (GUILayout.Button("IN 5s")) RunVisibilityTest(false, 5f);
            GUI.enabled = enabled;
            GUILayout.Label("Visibility: " + visibilityTestStatus);

            GUILayout.Space(5f);
            GUILayout.Label("P0.G2 PARTICLE BODY", GUI.skin.box);
            GUI.enabled = enabled && particleBody != null && !dissolveTestRunning
                && (performer == null || (performer.VisibilityState == PerformerVisibilityState.Visible
                    && !performer.IsDissolving && !performer.IsDissolveShaderAcceptanceActive));
            if (GUILayout.Button("PARTICLE BODY — SHOW")) ParticleBodyRequest(particleBody.Show);
            if (GUILayout.Button("PARTICLE BODY — HIDE")) ParticleBodyRequest(particleBody.Hide);
            if (GUILayout.Button("PARTICLE BODY — DETACH")) ParticleBodyRequest(particleBody.Detach);
            if (GUILayout.Button("PARTICLE BODY — TRANSIT A → B")) ParticleBodyRequest(particleBody.TransitToCurrentPerformer);
            if (GUILayout.Button("PARTICLE BODY — REFORM")) ParticleBodyRequest(particleBody.Reform);
            if (GUILayout.Button("PARTICLE BODY — RESET")) ParticleBodyRequest(particleBody.ResetBody);
            GUI.enabled = enabled;
            GUILayout.Label("Particle body: " + (particleBody != null ? particleBody.Status : "Run Install Particle Body Acceptance Harness in Edit Mode."));
            if (particleBody != null) GUILayout.Label("Bindings: " + particleBody.BindingCount);

            GUILayout.Space(5f);
            GUI.enabled = enabled && performer != null && performer.IsRuntimeReady
                && performer.VisibilityState == PerformerVisibilityState.Visible && !performer.IsDissolving;
            if (GUILayout.Button("Walk across floor")) Request(() => performer.WalkTo(acrossFloor), "Walking across floor");
            if (GUILayout.Button("Walk near platform")) Request(() => performer.WalkTo(nearPlatform), "Walking beside platform");
            GUI.enabled = enabled && performer != null && performer.IsRuntimeReady;
            if (GUILayout.Button("Look at camera")) Request(() => performer.LookAt(cameraTarget), "Looking at camera");
            GUI.enabled = enabled;

            GUILayout.Space(5f);
            GUILayout.Label("P0 TURN TO — BODY FACING", GUI.skin.box);
            bool turnToEnabled = enabled && performer != null && performer.IsRuntimeReady
                && performer.LocomotionAvailable && performer.VisibilityState == PerformerVisibilityState.Visible
                && performer.SeatingState == PerformerSeatingState.Standing && !performer.IsLocomoting
                && !performer.IsTeleporting && !performer.IsDissolving
                && !performer.IsDissolveShaderAcceptanceActive;
            DrawTurnToPair("0° Forward", 0f, "10° Right", 10f, turnToEnabled);
            DrawTurnToPair("25° Left", -25f, "35° Right", 35f, turnToEnabled);
            DrawTurnToPair("60° Left", -60f, "60° Right", 60f, turnToEnabled);
            DrawTurnToPair("90° Left", -90f, "90° Right", 90f, turnToEnabled);
            DrawTurnToPair("120° Left", -120f, "120° Right", 120f, turnToEnabled);
            DrawTurnToPair("170° Left", -170f, "170° Right", 170f, turnToEnabled);
            GUI.enabled = turnToEnabled && playerController != null;
            if (GUILayout.Button("TURN TO PLAYER"))
                Request(() => performer.TurnTo(playerController.transform), "Turning toward Player position snapshot");
            GUI.enabled = enabled;
            if (performer != null)
            {
                GUILayout.Label("Locomotion: " + performer.LocomotionState
                    + " / " + performer.LocomotionCurrentMotion);
                GUILayout.Label("Heading error: " + performer.LocomotionHeadingError.ToString("0.0") + "°");
                GUILayout.Label("Actor position: " + performer.transform.position.ToString("F4"));
                GUILayout.Label("Actor yaw: " + performer.transform.eulerAngles.y.ToString("0.0") + "°");
            }

            GUILayout.Space(5f);
            GUILayout.Label("P0.GESTURE — ADDITIVE UPPER BODY", GUI.skin.box);
            bool gestureVisible = enabled && performer != null && performer.IsRuntimeReady
                && performer.GestureAvailable && gestureAcceptanceWave != null
                && performer.VisibilityState == PerformerVisibilityState.Visible;
            GUI.enabled = gestureVisible;
            if (GUILayout.Button("GESTURE: WAVE"))
                Request(() => performer.Gesture(gestureAcceptanceWave), "Playing RightHandWave");
            if (GUILayout.Button("GESTURE: WAVE ASYNC")) RunGestureAsync();
            if (GUILayout.Button("WAVE TWICE / SUPERSEDE")) RunGestureSupersession();
            GUI.enabled = gestureVisible && acrossFloor != null && performer.LocomotionAvailable;
            if (GUILayout.Button("WALK + WAVE"))
                Request(() => { performer.WalkTo(acrossFloor); performer.Gesture(gestureAcceptanceWave); }, "WalkTo + Gesture");
            GUI.enabled = gestureVisible && turnToEnabled;
            if (GUILayout.Button("TURN + WAVE"))
                Request(() => { performer.TurnTo(CreateTurnTarget(90f)); performer.Gesture(gestureAcceptanceWave); }, "TurnTo + Gesture");
            GUI.enabled = gestureVisible && poseHarness != null && poseHarness.SeatingTestSeatForAcceptance != null;
            if (GUILayout.Button("SEATED + WAVE"))
                Request(() =>
                {
                    if (performer.SeatingState == PerformerSeatingState.Standing)
                        performer.SitAt(poseHarness.SeatingTestSeatForAcceptance);
                    performer.Gesture(gestureAcceptanceWave);
                }, "SitAt + Gesture");
            GUI.enabled = gestureVisible && cameraTarget != null;
            if (GUILayout.Button("LOOK + WAVE"))
                Request(() => { performer.LookAt(cameraTarget); performer.Gesture(gestureAcceptanceWave); }, "LookAt + Gesture");
            GUI.enabled = gestureVisible && poseHarness != null && poseHarness.ExpressionAForAcceptance != null;
            if (GUILayout.Button("EXPRESSION + WAVE"))
                Request(() =>
                {
                    performer.Expression(poseHarness.ExpressionAForAcceptance);
                    performer.Gesture(gestureAcceptanceWave);
                }, "Expression + Gesture");
            GUI.enabled = gestureVisible && poseHarness != null && poseHarness.SpeechClipA != null;
            if (GUILayout.Button("SAY + WAVE"))
                Request(() =>
                {
                    performer.Say(poseHarness.SpeechClipA);
                    performer.Gesture(gestureAcceptanceWave);
                }, "Say + Gesture");
            Transform dissolveTarget = GetOppositeDissolveMark();
            bool dissolveGestureAvailable = gestureVisible && dissolveTarget != null
                && performer.DissolveAvailable && !performer.IsLocomoting && !performer.IsTeleporting
                && performer.SeatingState == PerformerSeatingState.Standing && !performer.IsDissolving;
            GUI.enabled = dissolveGestureAvailable;
            if (GUILayout.Button("DISSOLVE TO + WAVE"))
                Request(() =>
                {
                    performer.Gesture(gestureAcceptanceWave);
                    performer.DissolveTo(dissolveTarget, dissolveDurationSeconds);
                }, "DissolveTo + Gesture");
            GUI.enabled = dissolveGestureAvailable;
            if (GUILayout.Button("OUT WHILE WAVING"))
                Request(() =>
                {
                    performer.Gesture(gestureAcceptanceWave);
                    performer.DissolveOut(dissolveDurationSeconds);
                }, "DissolveOut while Gesture continues");
            GUI.enabled = enabled;
            GUILayout.Label("Status: " + gestureStatus);
            if (performer != null)
            {
                GUILayout.Label("IsGesturing: " + performer.IsGesturing
                    + "  CurrentGesture: " + (performer.CurrentGesture != null ? performer.CurrentGesture.name : "null")
                    + "  Progress: " + (performer.GestureProgress * 100f).ToString("F0") + "%"
                    + "  Available: " + performer.GestureAvailable);
                GUILayout.Label("Visibility: " + performer.VisibilityState
                    + "  Locomotion: " + performer.LocomotionState
                    + "  Seating: " + performer.SeatingState);
            }
            if (gestureAcceptanceWave == null)
                GUILayout.Label("Run Tools > DAZ Pose > Gesture > Generate Gesture Acceptance Assets in Edit Mode.");
            else if (performer != null && performer.VisibilityState != PerformerVisibilityState.Visible
                && performer.IsRuntimeReady && performer.GestureAvailable)
            {
                GUI.enabled = enabled;
                if (GUILayout.Button("TRY WAVE WHILE HIDDEN / DISSOLVING (SHOULD REJECT)"))
                    Request(() => performer.Gesture(gestureAcceptanceWave), "Unexpectedly accepted Gesture outside Visible state");
            }

            GUILayout.Space(5f);
            GUILayout.Label("P0.PERFORM — FULL BODY + RETURN HOME", GUI.skin.box);
            bool actionCanStart = enabled && performer != null && performer.IsRuntimeReady
                && performer.ActionAvailable && performer.VisibilityState == PerformerVisibilityState.Visible
                && performer.SeatingState == PerformerSeatingState.Standing && !performer.IsLocomoting
                && !performer.IsTeleporting && !performer.IsDissolving && !performer.IsPerforming;
            GUI.enabled = actionCanStart && jumpForJoyAction != null;
            if (GUILayout.Button("PERFORM: JUMP FOR JOY")) StartPerform(jumpForJoyAction);
            GUI.enabled = actionCanStart && jumpForJoyAction != null;
            if (GUILayout.Button("PERFORM ASYNC: JUMP FOR JOY")) RunPerformAsync(jumpForJoyAction);
            GUI.enabled = actionCanStart && displacedRecoveryTestAction != null;
            if (GUILayout.Button("PERFORM: DISPLACED RECOVERY TEST")) StartPerform(displacedRecoveryTestAction);
            GUI.enabled = enabled;
            GUILayout.Label("Status: " + actionStatus);
            if (performer != null)
            {
                GUILayout.Label("IsPerforming: " + performer.IsPerforming
                    + "  CurrentAction: " + (performer.CurrentAction != null ? performer.CurrentAction.name : "none")
                    + "  State: " + performer.ActionState
                    + "  Progress: " + (performer.ActionProgress * 100f).ToString("F0") + "%");
                if (performer.ActionHasAnchor)
                {
                    GUILayout.Label("Anchor: " + performer.ActionAnchorPosition.ToString("F3")
                        + "  Current: " + performer.transform.position.ToString("F3")
                        + "  Position error: " + performer.ActionPositionError.ToString("F3") + " m");
                    Vector3 anchorForward = Vector3.ProjectOnPlane(performer.ActionAnchorForward, Vector3.up);
                    Vector3 currentForward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up);
                    float anchorYaw = anchorForward.sqrMagnitude > 0.000001f
                        ? Quaternion.LookRotation(anchorForward.normalized, Vector3.up).eulerAngles.y : 0f;
                    float currentYaw = currentForward.sqrMagnitude > 0.000001f
                        ? Quaternion.LookRotation(currentForward.normalized, Vector3.up).eulerAngles.y : performer.transform.eulerAngles.y;
                    GUILayout.Label("Anchor facing: " + anchorYaw.ToString("0.0") + "°"
                        + "  Current facing: " + currentYaw.ToString("0.0") + "°"
                        + "  Heading error: " + performer.ActionHeadingError.ToString("0.0") + "°");
                }
                else GUILayout.Label("Anchor: none yet");
                GUILayout.Label("Locomotion: " + performer.LocomotionState
                    + "  Visibility: " + performer.VisibilityState);
            }
            if (jumpForJoyAction == null || displacedRecoveryTestAction == null)
                GUILayout.Label("Run Tools > DAZ Pose > Action > Generate Jump for Joy Acceptance Assets in Edit Mode.");

            GUILayout.Space(5f);
            GUILayout.Label("Player View", GUI.skin.box);
            GUI.enabled = enabled && playerController != null && viewMarkWide != null;
            if (GUILayout.Button("Move Wide — 2 sec")) PlayerViewRequest(() => playerController.MoveTo(viewMarkWide, 2f), "Moving to wide view");
            GUI.enabled = enabled && playerController != null && viewMarkLara != null;
            if (GUILayout.Button("Move Lara — 2 sec")) PlayerViewRequest(() => playerController.MoveTo(viewMarkLara, 2f), "Moving to Lara view");
            GUI.enabled = enabled && playerController != null && viewMarkLounge != null;
            if (GUILayout.Button("Move Lounge — 3 sec")) PlayerViewRequest(() => playerController.MoveTo(viewMarkLounge, 3f), "Moving to lounge view");
            GUI.enabled = enabled && playerController != null && laraFaceViewTarget != null;
            if (GUILayout.Button("Look At Lara — 1 sec")) PlayerViewRequest(() => playerController.LookAt(laraFaceViewTarget, 1f), "Looking at Lara");
            if (GUILayout.Button("Track Lara — 1 sec")) PlayerViewRequest(() => playerController.Track(laraFaceViewTarget, 1f), "Tracking Lara");
            GUI.enabled = enabled && playerController != null && playerController.View != null
                && (playerController.View.IsTracking || playerController.View.IsAcquiringTrack);
            if (GUILayout.Button("Stop Tracking")) PlayerViewRequest(playerController.StopTracking, "Holding current view direction");
            GUI.enabled = enabled;

            if (playerController != null && playerController.View != null)
            {
                PlayerView view = playerController.View;
                string moveState = view.IsMoving
                    ? "moving " + (view.MoveProgress * 100f).ToString("F0") + "% → " + view.CurrentMoveDestination.ToString("F2")
                    : "idle";
                string orientationState = view.IsLooking ? "looking " + (view.LookProgress * 100f).ToString("F0") + "%"
                    : view.IsAcquiringTrack ? "acquiring track " + (view.LookProgress * 100f).ToString("F0") + "%"
                    : view.IsTracking ? "tracking " + (view.CurrentTrackTarget == laraFaceViewTarget ? "Lara FaceViewTarget" : "target")
                    : "idle";
                GUILayout.Label("Move: " + moveState);
                GUILayout.Label("Orientation: " + orientationState);
                GUILayout.Label("Player: " + view.PlayerPosition.ToString("F2"));
            }

            GUILayout.Label(status);
            GUILayout.Label("The raised stage is scenery. Walk targets stay on the floor.");
            if (smokeEmitters != null && smokeEmitters.Length > 0)
            {
                int count = 0, running = 0;
                foreach (ParticleSystem emitter in smokeEmitters)
                {
                    if (emitter == null) continue;
                    count += emitter.particleCount;
                    if (emitter.isPlaying) running++;
                }
                GUILayout.Label("Smoke: " + count + " particles; " + running + "/" + smokeEmitters.Length + " emitters running.");
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawMotionControls(bool guiEnabled)
        {
            GUILayout.Space(5f);
            GUILayout.Label("MOTION DRIVER", GUI.skin.box);
            MotionDriver driver = performer != null ? performer.MotionSource : null;
            if (driver == null)
            {
                GUI.enabled = false;
                GUILayout.Button("START");
                GUI.enabled = guiEnabled;
                GUILayout.Label("Run Tools > DAZ Pose > Motion > Generate BasicStroke and Install Acceptance Harness in Edit Mode.");
                return;
            }

            MotionSample sample = driver.CurrentSample;
            GUILayout.Label("Running: " + (driver.IsRunning ? "YES" : "NO"));
            GUILayout.Label("Frequency: " + driver.FrequencyHz.ToString("F2") + " Hz");
            GUI.enabled = guiEnabled && driver.IsRunning;
            float frequency = GUILayout.HorizontalSlider(driver.FrequencyHz, 0.25f, 3f);
            GUI.enabled = guiEnabled;
            if (!Mathf.Approximately(frequency, driver.FrequencyHz)) driver.FrequencyHz = frequency;

            GUILayout.Label("Sequence: " + sample.Sequence + "    Phase: " + sample.Phase01.ToString("F3"));
            GUILayout.Label("Position: " + sample.Position01.ToString("F3")
                + "    Velocity: " + sample.Velocity.ToString("+0.00;-0.00;0.00"));
            GUILayout.Label("Direction: " + sample.Direction + "    Time: " + sample.TimeSeconds.ToString("F2") + " s");
            GUI.enabled = false;
            GUILayout.HorizontalSlider(sample.Position01, 0f, 1f);
            GUI.enabled = guiEnabled;

            GUILayout.BeginHorizontal();
            GUI.enabled = guiEnabled && !driver.IsRunning;
            if (GUILayout.Button("START"))
            {
                BeginMotionRootTracking();
                driver.StartMotion();
            }
            GUI.enabled = guiEnabled && driver.IsRunning;
            if (GUILayout.Button("STOP")) driver.StopMotion();
            GUI.enabled = guiEnabled;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = guiEnabled && driver.IsRunning;
            if (GUILayout.Button("RESTART"))
            {
                BeginMotionRootTracking();
                driver.RestartMotion();
            }
            GUI.enabled = guiEnabled;
            if (GUILayout.Button("RESET"))
            {
                driver.ResetMotion();
                motionTrackingRoot = false;
            }
            GUI.enabled = guiEnabled;
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            GUILayout.Label("Consumer bound: " + (performer != null && performer.MotionConsumerBound ? "YES" : "NO"));
            GUILayout.Label("Motion ownership: " + (performer != null ? performer.MotionOwnershipWeight : 0f).ToString("F2"));
            GUILayout.Label("Active variant: " + (performer != null ? performer.MotionActiveVariantName : "none"));
            GUILayout.Label("Scrubbed Position01: " + (performer != null ? performer.MotionSampledPosition01 : 0f).ToString("F3"));
            GUILayout.Space(5f);
            GUILayout.Label("Root drift from run start");
            GUILayout.Label("Run duration: " + motionMeasuredRunSeconds.ToString("F1") + " s"
                + (motionTrackingRoot ? " (live)" : string.Empty));
            GUILayout.Label("Translation: " + motionRootTranslationDrift.ToString("F5") + " m"
                + "    Rotation: " + motionRootRotationDrift.ToString("F4") + "°");
            GUI.enabled = guiEnabled;
        }

        private void UpdateMotionRootDrift()
        {
            MotionDriver driver = performer != null ? performer.MotionSource : null;
            if (driver == null) return;
            if (driver.IsRunning && !motionTrackingRoot) BeginMotionRootTracking();
            if (!motionTrackingRoot) return;

            motionMeasuredRunSeconds = Time.realtimeSinceStartup - motionRunStartedRealtime;
            motionRootTranslationDrift = Vector3.Distance(motionRootStartPosition, performer.transform.position);
            motionRootRotationDrift = Quaternion.Angle(motionRootStartRotation, performer.transform.rotation);
            if (!driver.IsRunning) motionTrackingRoot = false;
        }

        private void BeginMotionRootTracking()
        {
            if (performer == null) return;
            motionTrackingRoot = true;
            motionRunStartedRealtime = Time.realtimeSinceStartup;
            motionMeasuredRunSeconds = 0f;
            motionRootTranslationDrift = 0f;
            motionRootRotationDrift = 0f;
            motionRootStartPosition = performer.transform.position;
            motionRootStartRotation = performer.transform.rotation;
        }

        private void DrawMagicControls(bool guiEnabled)
        {
            GUILayout.Space(5f);
            GUILayout.Label("CAST + AURA", GUI.skin.box);
            if (magicCatalog == null)
            {
                GUI.enabled = false;
                GUILayout.Button("SPELL  [catalog not assigned]");
                GUILayout.Button("AURA  [catalog not assigned]");
                GUILayout.Button("CAST ON LARA");
                GUILayout.Button("SET ON LARA");
                GUILayout.Button("CLEAR AURA");
                GUI.enabled = guiEnabled;
                GUILayout.Label(magicStatus);
                return;
            }

            var spells = magicCatalog.Spells;
            var auras = magicCatalog.Auras;
            if (spells == null || spells.Count == 0 || auras == null || auras.Count == 0)
            {
                GUI.enabled = false;
                GUILayout.Button("SPELL  [empty catalog]");
                GUILayout.Button("AURA  [empty catalog]");
                GUI.enabled = guiEnabled;
                GUILayout.Label("Add at least one Spell and Aura to the assigned catalog.");
                return;
            }

            selectedSpellIndex = Mathf.Clamp(selectedSpellIndex, 0, spells.Count - 1);
            selectedAuraIndex = Mathf.Clamp(selectedAuraIndex, 0, auras.Count - 1);
            PerformerSpell selectedSpell = spells[selectedSpellIndex];
            PerformerAura selectedAura = auras[selectedAuraIndex];

            GUI.enabled = guiEnabled;
            if (GUILayout.Button("SPELL  [" + (selectedSpell != null ? selectedSpell.DisplayName : "Missing Spell")
                + (spellDropdownOpen ? " ▲]" : " ▼]"))) spellDropdownOpen = !spellDropdownOpen;
            if (spellDropdownOpen)
            {
                for (int i = 0; i < spells.Count; i++)
                {
                    PerformerSpell option = spells[i];
                    GUI.enabled = guiEnabled && option != null;
                    if (GUILayout.Button((i == selectedSpellIndex ? "• " : "  ")
                        + (option != null ? option.DisplayName : "Missing Spell")))
                    {
                        selectedSpellIndex = i;
                        spellDropdownOpen = false;
                    }
                }
            }

            selectedSpell = spells[selectedSpellIndex];
            bool magicReady = performer != null && performer.MagicAvailable && selectedSpell != null
                && selectedSpell.IsReady(out _);
            GUI.enabled = guiEnabled && magicReady;
            if (GUILayout.Button("CAST ON LARA"))
                Request(() => performer.Cast(selectedSpell), "Casting " + selectedSpell.DisplayName + " on Lara");
            GUI.enabled = guiEnabled && magicReady && playerController != null;
            if (GUILayout.Button("CAST ON PLAYER"))
                Request(() => performer.Cast(selectedSpell, playerController.transform),
                    "Casting " + selectedSpell.DisplayName + " on Player");

            GUILayout.Space(3f);
            selectedAura = auras[selectedAuraIndex];
            GUI.enabled = guiEnabled;
            if (GUILayout.Button("AURA  [" + (selectedAura != null ? selectedAura.DisplayName : "Missing Aura")
                + (auraDropdownOpen ? " ▲]" : " ▼]"))) auraDropdownOpen = !auraDropdownOpen;
            if (auraDropdownOpen)
            {
                for (int i = 0; i < auras.Count; i++)
                {
                    PerformerAura option = auras[i];
                    GUI.enabled = guiEnabled && option != null;
                    if (GUILayout.Button((i == selectedAuraIndex ? "• " : "  ")
                        + (option != null ? option.DisplayName : "Missing Aura")))
                    {
                        selectedAuraIndex = i;
                        auraDropdownOpen = false;
                    }
                }
            }

            selectedAura = auras[selectedAuraIndex];
            bool auraReady = performer != null && performer.MagicAvailable && selectedAura != null
                && selectedAura.IsReady(out _);
            GUI.enabled = guiEnabled && auraReady;
            if (GUILayout.Button("SET ON LARA"))
                Request(() => performer.Aura(selectedAura), "Setting " + selectedAura.DisplayName + " Aura on Lara");
            GUI.enabled = guiEnabled && auraReady && playerController != null;
            if (GUILayout.Button("SET ON PLAYER"))
                Request(() => performer.Aura(selectedAura, playerController.transform),
                    "Setting " + selectedAura.DisplayName + " Aura on Player");
            GUI.enabled = guiEnabled && performer != null && performer.MagicAvailable;
            if (GUILayout.Button("CLEAR AURA")) Request(performer.ClearAura, "Cleared Lara's desired Aura");
            GUI.enabled = guiEnabled;

            string currentAura = performer != null && performer.HasAura && performer.CurrentAura != null
                ? performer.CurrentAura.DisplayName + " on "
                    + (performer.AuraTarget == (performer != null ? performer.transform : null) ? "Lara" : "external target")
                : "none";
            GUILayout.Label("Current Aura: " + currentAura);
            GUILayout.Label("Active Spells: " + (performer != null ? performer.ActiveSpellCount : 0));
            GUILayout.Label("Magic: " + magicStatus);
        }

        private void Request(Action command, string description)
        {
            try { command(); status = description; }
            catch (Exception exception) { status = exception.Message; Debug.LogException(exception, this); }
        }

        private void PlayerViewRequest(Action command, string description) => Request(command, description);

        private void DrawTurnToPair(string firstLabel, float firstYaw, string secondLabel, float secondYaw,
            bool turnEnabled)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = turnEnabled;
            if (GUILayout.Button(firstLabel)) Request(() => performer.TurnTo(CreateTurnTarget(firstYaw)), "TurnTo " + firstLabel);
            if (GUILayout.Button(secondLabel)) Request(() => performer.TurnTo(CreateTurnTarget(secondYaw)), "TurnTo " + secondLabel);
            GUILayout.EndHorizontal();
        }

        public void ConfigureGestureAcceptance(PerformerGesture wave) => gestureAcceptanceWave = wave;

        public void ConfigureActionAcceptance(PerformerAction jumpForJoy, PerformerAction displacedRecoveryTest)
        {
            jumpForJoyAction = jumpForJoy;
            displacedRecoveryTestAction = displacedRecoveryTest;
        }

        private Vector3 CreateTurnTarget(float signedYaw)
        {
            Vector3 forward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.000001f)
                throw new InvalidOperationException("The performer has no usable planar forward direction.");
            Vector3 facing = Quaternion.AngleAxis(signedYaw, Vector3.up) * forward.normalized;
            return performer.transform.position + facing * 2f;
        }

        private Transform GetOppositeDissolveMark()
        {
            if (teleportMarkA == null || teleportMarkB == null || performer == null) return null;
            bool atA = Vector3.Distance(performer.transform.position, teleportMarkA.position) < 0.25f;
            bool atB = Vector3.Distance(performer.transform.position, teleportMarkB.position) < 0.25f;
            return atA ? teleportMarkB : atB ? teleportMarkA : null;
        }

        private async void RunGestureAsync()
        {
            gestureStatus = "Waiting for RightHandWave…";
            try
            {
                GestureCompletion result = await performer.GestureAsync(gestureAcceptanceWave);
                gestureStatus = "WAVE ASYNC completed: " + result + ".";
            }
            catch (Exception exception)
            {
                gestureStatus = "WAVE ASYNC rejected — " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private async void RunGestureSupersession()
        {
            gestureStatus = "Starting first Wave; second request will arrive at 40%.";
            try
            {
                Awaitable<GestureCompletion> first = performer.GestureAsync(gestureAcceptanceWave);
                float delay = Mathf.Max(0.1f, gestureAcceptanceWave.Clip.length * 0.4f);
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    await Awaitable.NextFrameAsync();
                    elapsed += Time.deltaTime;
                }
                Awaitable<GestureCompletion> second = performer.GestureAsync(gestureAcceptanceWave);
                GestureCompletion firstResult = await first;
                GestureCompletion secondResult = await second;
                gestureStatus = "First: " + firstResult + "; restarted Wave: " + secondResult + ".";
            }
            catch (Exception exception)
            {
                gestureStatus = "SUPERSEDE test failed — " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void StartPerform(PerformerAction action)
        {
            try
            {
                performer.Perform(action);
                actionStatus = "Performing " + action.name + "; completion includes any physical return-home recovery.";
            }
            catch (Exception exception)
            {
                actionStatus = "REJECTED — " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private async void RunPerformAsync(PerformerAction action)
        {
            actionStatus = "Awaiting " + action.name + " and return-home recovery…";
            try
            {
                ActionCompletion result = await performer.PerformAsync(action);
                actionStatus = result == ActionCompletion.Completed
                    ? action.name + " completed after recovery."
                    : "Performer was disabled during " + action.name + ".";
            }
            catch (Exception exception)
            {
                actionStatus = "FAILED — " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void ParticleBodyRequest(Action command)
        {
            try { command(); }
            catch (Exception exception)
            {
                status = "Particle body: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void SetDissolveShaderAcceptanceState(bool dissolveEnabled, float progress)
        {
            try
            {
                performer.SetDissolveShaderAcceptanceState(dissolveEnabled, progress);
                dissolveShaderStatus = "Shader progress " + (progress * 100f).ToString("F0") + "%";
            }
            catch (Exception exception)
            {
                dissolveShaderStatus = "FAILED — " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void ApplyDissolveShaderState(bool dissolveEnabled, float progress)
        {
            try { performer.SetDissolveShaderAcceptanceState(dissolveEnabled, progress); }
            catch (Exception exception)
            {
                dissolveShaderAnimating = false;
                dissolveShaderStatus = "FAILED — " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private async void RunTeleportTest(Transform target, PerformerPose arrivalPose)
        {
            teleportTestRunning = true;
            teleportStatus = "Teleporting…";
            string targetName = target != null ? target.name : "destination";
            try
            {
                TeleportCompletion result = await performer.TeleportToAsync(target, arrivalPose);
                teleportStatus = result == TeleportCompletion.Arrived
                    ? "Arrived at " + targetName + (arrivalPose != null ? " in " + arrivalPose.name : ".")
                    : "Performer disabled during teleport.";
            }
            catch (Exception exception)
            {
                teleportStatus = "ABORTED — " + exception.Message;
                Debug.LogException(exception, this);
            }
            finally { teleportTestRunning = false; }
        }

        private async void RunDissolveTest(Transform target, PerformerPose arrivalPose)
        {
            dissolveTestRunning = true;
            string targetName = target != null ? target.name : "destination";
            dissolveTestStatus = "Dissolving to " + targetName + (arrivalPose != null ? " in " + arrivalPose.name + "…" : "…");
            try
            {
                DissolveCompletion result = await performer.DissolveToAsync(target, dissolveDurationSeconds, arrivalPose);
                dissolveTestStatus = result == DissolveCompletion.Arrived
                    ? "Arrived at " + targetName + (arrivalPose != null ? " in " + arrivalPose.name + "." : ".")
                    : "Performer disabled during dissolve.";
            }
            catch (Exception exception)
            {
                dissolveTestStatus = "ABORTED — " + exception.Message;
                Debug.LogException(exception, this);
            }
            finally { dissolveTestRunning = false; }
        }

        private async void RunVisibilityTest(bool dissolveOut, float durationSeconds)
        {
            visibilityTestRunning = true;
            visibilityTestStatus = (dissolveOut ? "Dissolving out over " : "Dissolving in over ")
                + durationSeconds.ToString("F0") + " seconds…";
            try
            {
                VisibilityCompletion result = dissolveOut
                    ? await performer.DissolveOutAsync(durationSeconds)
                    : await performer.DissolveInAsync(durationSeconds);
                visibilityTestStatus = result == VisibilityCompletion.PerformerDisabled
                    ? "Performer disabled; transition rolled back to its starting visibility."
                    : "Completed in stable " + result + " state. You can wait, change Pose/Expression/Gaze, then press IN.";
            }
            catch (Exception exception)
            {
                visibilityTestStatus = "REJECTED — " + exception.Message;
                Debug.LogException(exception, this);
            }
            finally { visibilityTestRunning = false; }
        }

    }
}
