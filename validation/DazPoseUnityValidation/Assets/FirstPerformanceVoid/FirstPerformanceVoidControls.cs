using System;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.FirstPerformanceVoid
{
    /// <summary>Room destinations wired to the existing performer API. No movement or camera system.</summary>
    public sealed class FirstPerformanceVoidControls : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private Transform acrossFloor;
        [SerializeField] private Transform nearPlatform;
        [SerializeField] private Transform cameraTarget;
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

        private void Awake()
        {
            smokeEmitters = GetComponentsInChildren<ParticleSystem>();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Mathf.Max(454f, Screen.width - 286f), 12f, 274f, 210f),
                "First Performance Void", GUI.skin.window);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && performer != null && performer.IsRuntimeReady;
            if (GUILayout.Button("Walk across floor")) Request(() => performer.WalkTo(acrossFloor), "Walking across floor");
            if (GUILayout.Button("Walk near platform")) Request(() => performer.WalkTo(nearPlatform), "Walking beside platform");
            if (GUILayout.Button("Look at camera")) Request(() => performer.LookAt(cameraTarget), "Looking at camera");
            GUI.enabled = enabled;
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
    }
}
