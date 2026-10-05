using UnityEngine;
using DazPose.Motion;

namespace DazPose.Performer.HandGrip
{
    /// <summary>Scene-local hand-grip controls hosted by the existing runtime test panel.</summary>
    [DisallowMultipleComponent]
    public sealed class HandGripAcceptanceHarness : MonoBehaviour
    {
        [SerializeField] private PerformerHandGripController handGrip;
        [SerializeField] private GripContactRod rod;
        [SerializeField] private MotionDriver motionDriver;
        [SerializeField] private Transform rodRoot;
        [SerializeField, Range(0.015f, 0.075f)] private float radius = 0.035f;
        [SerializeField, Range(-30f, 30f)] private float tiltDegrees;

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
            ApplyRadius(radius);
        }

        /// <summary>Draws inside the existing First Performance Void test panel.</summary>
        public void DrawControls(bool guiEnabled)
        {
            GUILayout.Space(5f);
            GUILayout.Label("HAND GRIP ACCEPTANCE", GUI.skin.box);
            if (handGrip == null || rod == null || motionDriver == null)
            {
                GUILayout.Label("Harness references are incomplete.");
                return;
            }

            GUILayout.Label("State: " + handGrip.State + " Failure: " + handGrip.FailureReason);
            GUILayout.Label("Solver: " + handGrip.SolveResult.Status
                + "   GripWeight: " + handGrip.GripWeight.ToString("0.00")
                + "   Position01: " + handGrip.Position01.ToString("0.00"));

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && guiEnabled;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(handGrip.IsGripRequested ? "RELEASE GRIP" : "GRIP ON"))
            {
                if (handGrip.IsGripRequested) handGrip.ReleaseGrip();
                else handGrip.Grip(handGrip.GripStrength01);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Sine"))
            {
                motionDriver.SourceMode = MotionSourceMode.Sine;
                motionDriver.StartMotion();
            }
            if (GUILayout.Button("Stop / Hold")) motionDriver.StopMotion();
            if (GUILayout.Button("Restart")) motionDriver.RestartMotion();
            GUILayout.EndHorizontal();
            GUILayout.Label("Edit HandGripFixture / Start / End in Scene view to move the target or change its span.");

            handGrip.CalibrationMode = GUILayout.Toggle(handGrip.CalibrationMode,
                "RAW DIGIT CALIBRATION (contact disabled)");
            if (handGrip.CalibrationMode)
            {
                Vector4 curls = handGrip.CalibrationCurls;
                for (int digit = 0; digit < 4; digit++)
                {
                    GUILayout.Label(((HandGripDigit)digit) + " Curl: " + curls[digit].ToString("0.00"));
                    curls[digit] = GUILayout.HorizontalSlider(curls[digit], 0f, 1f);
                }
                handGrip.CalibrationCurls = curls;
                GUILayout.Label("Little Curl: " + handGrip.CalibrationLittleCurl.ToString("0.00"));
                handGrip.CalibrationLittleCurl = GUILayout.HorizontalSlider(handGrip.CalibrationLittleCurl, 0f, 1f);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Move Near")) MoveTarget(0.45f, 0f);
            if (GUILayout.Button("Move Far")) MoveTarget(3f, 0f);
            if (GUILayout.Button("Impossible Height")) MoveTarget(1f, 3f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int index = 0; index < 5; index++)
                if (GUILayout.Button("Position " + (index / 4f).ToString("0.##")))
                {
                    motionDriver.StopMotion();
                    motionDriver.SourceMode = MotionSourceMode.Sine;
                    motionDriver.Seek(Mathf.Acos(1f - 2f * (index / 4f)) / (2f * Mathf.PI * Mathf.Max(0.001f, motionDriver.FrequencyHz)));
                }
            GUILayout.EndHorizontal();
            GUILayout.Label("Wrist Twist " + handGrip.WristTwistDegrees.ToString("0") + "°");
            handGrip.WristTwistDegrees = GUILayout.HorizontalSlider(handGrip.WristTwistDegrees, -90f, 90f);
            if (GUILayout.Button("Reset Wrist Twist")) handGrip.WristTwistDegrees = 0f;
            GUILayout.Label("Grip Strength: " + handGrip.GripStrength01.ToString("0.00"));
            float strength = GUILayout.HorizontalSlider(handGrip.GripStrength01, 0f, 1f);
            if (!Mathf.Approximately(strength, handGrip.GripStrength01)) handGrip.GripStrength01 = strength;
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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("DIAGNOSTICS ON")) handGrip.DiagnosticsVisible = true;
            if (GUILayout.Button("DIAGNOSTICS OFF")) handGrip.DiagnosticsVisible = false;
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;

            HandGripSolveResult solve = handGrip.SolveResult;
            GUILayout.Label("Curl T/I/M/R/L: " + solve.ThumbCurl.ToString("0.00") + " / "
                + solve.IndexCurl.ToString("0.00") + " / " + solve.MiddleCurl.ToString("0.00") + " / "
                + solve.RingCurl.ToString("0.00") + " / " + solve.LittleCurl.ToString("0.00"));
            HandGripAlignmentDiagnostics alignment = handGrip.AlignmentDiagnostics;
            GUILayout.Label("Palm error: " + (alignment.PositionErrorMeters * 1000f).ToString("0.000")
                + " mm   orientation: " + alignment.OrientationErrorDegrees.ToString("0")
                + "°   reach: " + handGrip.ReachFraction.ToString("0.00")
                + "   walked: " + handGrip.RequiredLocomotion);
            GUILayout.Label("Digit status T/I/M/R/L: "
                + handGrip.GetDigitStatus(HandGripDigit.Thumb) + " / "
                + handGrip.GetDigitStatus(HandGripDigit.Index) + " / "
                + handGrip.GetDigitStatus(HandGripDigit.Middle) + " / "
                + handGrip.GetDigitStatus(HandGripDigit.Ring) + " / "
                + handGrip.GetDigitStatus(HandGripDigit.Little));
            GUILayout.Label("MotionDriver is " + (motionDriver.IsRunning ? "running" : "paused")
                + "; use the MOTION DRIVER section above to start, stop, or restart the stroke.");
        }

        private void MoveTarget(float distance, float height)
        {
            if (rodRoot == null || handGrip == null) return;
            Transform actor = handGrip.transform;
            rodRoot.position = actor.position + actor.forward * distance + actor.right * 0.15f + Vector3.up * (1.35f + height);
            rodRoot.rotation = Quaternion.identity;
            rod.StartPoint.localPosition = Vector3.down * 0.06f;
            rod.EndPoint.localPosition = Vector3.up * 0.06f;
            rod.ReferenceTransform = rodRoot;
            rod.ReferenceNormalLocal = -actor.forward;
            _baseRodRotation = rodRoot.rotation;
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
