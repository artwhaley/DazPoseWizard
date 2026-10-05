using UnityEngine;
using DazPose.Motion;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Scene-local IMGUI controls for the isolated hand-grip acceptance scene.</summary>
    [DisallowMultipleComponent]
    public sealed class HandGripAcceptanceHarness : MonoBehaviour
    {
        [SerializeField] private PerformerHandGripController handGrip;
        [SerializeField] private GripContactRod rod;
        [SerializeField] private MotionDriver motionDriver;
        [SerializeField] private Transform rodRoot;
        [SerializeField] private bool visible = true;
        [SerializeField, Range(0.015f, 0.075f)] private float radius = 0.035f;
        [SerializeField, Range(-30f, 30f)] private float tiltDegrees;
        [SerializeField, Range(0f, 3f)] private float motionFrequencyHz = 1f;

        private Quaternion _baseRodRotation;

        public PerformerHandGripController HandGrip => handGrip;
        public GripContactRod Rod => rod;
        public MotionDriver MotionDriver => motionDriver;

        public void ConfigureForEditor(PerformerHandGripController controller, GripContactRod target,
            MotionDriver driver, Transform fixtureRoot)
        {
            handGrip = controller;
            rod = target;
            motionDriver = driver;
            rodRoot = fixtureRoot;
            radius = target != null ? target.Radius : radius;
            _baseRodRotation = fixtureRoot != null ? fixtureRoot.rotation : Quaternion.identity;
        }

        private void Start()
        {
            if (rodRoot != null) _baseRodRotation = rodRoot.rotation;
            if (motionDriver != null) motionFrequencyHz = motionDriver.FrequencyHz;
            ApplyRadius(radius);
        }

        private void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(12f, 12f, 380f, 540f), "Hand Grip Acceptance", GUI.skin.window);
            if (handGrip == null || rod == null || motionDriver == null)
            {
                GUILayout.Label("Harness references are incomplete.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label("Solver: " + handGrip.SolveResult.Status
                + "   GripWeight: " + handGrip.GripWeight.ToString("0.00")
                + "   Position01: " + handGrip.Position01.ToString("0.00"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(handGrip.IsGripRequested ? "RELEASE GRIP" : "GRIP ON"))
            {
                if (handGrip.IsGripRequested) handGrip.ReleaseGrip();
                else handGrip.Grip(handGrip.GripStrength01);
            }
            if (GUILayout.Button("MOTION START")) motionDriver.StartMotion();
            if (GUILayout.Button("MOTION STOP")) motionDriver.StopMotion();
            if (GUILayout.Button("RESTART")) motionDriver.RestartMotion();
            GUILayout.EndHorizontal();

            GUILayout.Label("Grip Strength: " + handGrip.GripStrength01.ToString("0.00"));
            handGrip.GripStrength01 = GUILayout.HorizontalSlider(handGrip.GripStrength01, 0f, 1f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Radius small 25 mm")) ApplyRadius(0.025f);
            if (GUILayout.Button("nominal 35 mm")) ApplyRadius(0.035f);
            if (GUILayout.Button("large 55 mm")) ApplyRadius(0.055f);
            GUILayout.EndHorizontal();
            GUILayout.Label("Rod radius: " + (rod.Radius * 1000f).ToString("0") + " mm");
            radius = GUILayout.HorizontalSlider(radius, 0.015f, 0.075f);
            if (!Mathf.Approximately(radius, rod.Radius)) ApplyRadius(radius);

            GUILayout.Label("Rod tilt: " + tiltDegrees.ToString("0") + "°");
            float newTilt = GUILayout.HorizontalSlider(tiltDegrees, -30f, 30f);
            if (!Mathf.Approximately(newTilt, tiltDegrees)) SetTilt(newTilt);

            GUILayout.Label("Motion frequency: " + motionFrequencyHz.ToString("0.00") + " Hz");
            motionFrequencyHz = GUILayout.HorizontalSlider(motionFrequencyHz, 0f, 3f);
            motionDriver.FrequencyHz = motionFrequencyHz;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Diagnostics on")) handGrip.DiagnosticsVisible = true;
            if (GUILayout.Button("Diagnostics off")) handGrip.DiagnosticsVisible = false;
            if (GUILayout.Button("Hide controls")) visible = false;
            GUILayout.EndHorizontal();

            HandGripSolveResult solve = handGrip.SolveResult;
            GUILayout.Label("Curl T/I/M/R/L: " + solve.ThumbCurl.ToString("0.00") + " / "
                + solve.IndexCurl.ToString("0.00") + " / " + solve.MiddleCurl.ToString("0.00") + " / "
                + solve.RingCurl.ToString("0.00") + " / " + solve.LittleCurl.ToString("0.00"));
            HandGripAlignmentDiagnostics alignment = handGrip.AlignmentDiagnostics;
            GUILayout.Label("Grip-center error: " + (alignment.PositionErrorMeters * 1000f).ToString("0")
                + " mm   orientation: " + alignment.OrientationErrorDegrees.ToString("0")
                + "°   roll: " + alignment.RollErrorDegrees.ToString("0") + "°");
            GUILayout.Label("MotionDriver is " + (motionDriver.IsRunning ? "running" : "paused")
                + "; grip follows its held Position01 while paused.");
            GUILayout.EndArea();
        }

        private void ApplyRadius(float value)
        {
            radius = Mathf.Clamp(value, 0.015f, 0.075f);
            if (rod != null) rod.Radius = radius;
        }

        private void SetTilt(float value)
        {
            tiltDegrees = Mathf.Clamp(value, -30f, 30f);
            if (rodRoot != null)
            {
                Vector3 axis = _baseRodRotation * Vector3.forward;
                rodRoot.rotation = Quaternion.AngleAxis(tiltDegrees, axis) * _baseRodRotation;
            }
        }
    }
}
