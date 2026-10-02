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
        [SerializeField, HideInInspector] private int lightingRevision;
        public int LightingRevision => lightingRevision;
        private ParticleSystem[] smokeEmitters;
        private string status = "Use the existing performer panel for seating, expressions and speech.";

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
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Mathf.Max(454f, Screen.width - 286f), 12f, 274f, 480f),
                "First Performance Void", GUI.skin.window);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && performer != null && performer.IsRuntimeReady;
            if (GUILayout.Button("Walk across floor")) Request(() => performer.WalkTo(acrossFloor), "Walking across floor");
            if (GUILayout.Button("Walk near platform")) Request(() => performer.WalkTo(nearPlatform), "Walking beside platform");
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
            GUILayout.EndArea();
        }

        private void Request(Action command, string description)
        {
            try { command(); status = description; }
            catch (Exception exception) { status = exception.Message; Debug.LogException(exception, this); }
        }

        private void PlayerViewRequest(Action command, string description) => Request(command, description);
    }
}
