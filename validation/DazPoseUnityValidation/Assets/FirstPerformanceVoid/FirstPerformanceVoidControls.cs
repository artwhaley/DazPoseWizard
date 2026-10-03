using System;
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
        [Header("P0.G2 Particle Body Acceptance")]
        [SerializeField] private PerformerParticleBody particleBody;
        [Header("P0.F Teleport Acceptance")]
        [SerializeField] private Transform teleportMarkA;
        [SerializeField] private Transform teleportMarkB;
        [SerializeField] private PerformerPose teleportArrivalPose;
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
        private bool dissolveTestRunning;
        private string dissolveTestStatus = "Run the P0.G3 DissolveTo acceptance checks in Play Mode.";
        private bool visibilityTestRunning;
        private string visibilityTestStatus = "DissolveOut leaves Lara hidden; use a matching IN button to restore her.";
        private const float DissolveShaderHalfCycleSeconds = 1f;

        public void ConfigureFirstContact(FirstContactPerformance performance) => firstContact = performance;

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
        }

        private void Update()
        {
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

        private void Request(Action command, string description)
        {
            try { command(); status = description; }
            catch (Exception exception) { status = exception.Message; Debug.LogException(exception, this); }
        }

        private void PlayerViewRequest(Action command, string description) => Request(command, description);

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
