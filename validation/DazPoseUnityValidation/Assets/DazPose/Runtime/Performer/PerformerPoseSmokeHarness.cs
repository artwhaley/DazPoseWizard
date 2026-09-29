using UnityEngine;

namespace DazPose.Performer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Development/Pose Smoke Harness")]
    public sealed class PerformerPoseSmokeHarness : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformerPose poseA = null;
        [SerializeField] private PerformerPose poseB = null;
        [SerializeField] private PerformerPose poseC = null;
        [SerializeField] private PoseTransition transition = PoseTransition.Default;
        [SerializeField] private PerformerPoseAcceptanceHarness acceptanceHarness;

        private void Reset()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
            if (acceptanceHarness == null) acceptanceHarness = GetComponent<PerformerPoseAcceptanceHarness>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) RequestPose(poseA);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RequestPose(poseB);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RequestPose(poseC);
            if (Input.GetKeyDown(KeyCode.F5)) RunAcceptanceChecks();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 360f, 470f), "Performer Pose Smoke Test", GUI.skin.window);
            GUILayout.Label("1 / 2 / 3 also selects these persistent poses.");
            DrawPoseButton("Pose A", poseA);
            DrawPoseButton("Pose B", poseB);
            DrawPoseButton("Pose C", poseC);
            if (GUILayout.Button("Run acceptance checks (F5)")) RunAcceptanceChecks();

            if (performer != null)
            {
                DrawBreathingControls();
                var desired = performer.DesiredPose == null ? "none" : performer.DesiredPose.name;
                GUILayout.Label("Desired: " + desired + (performer.IsTransitioning
                    ? "  " + (performer.TransitionProgress * 100f).ToString("F0") + "%"
                    : "  settled"));
            }
            if (acceptanceHarness != null) GUILayout.Label(acceptanceHarness.Status);
            GUILayout.EndArea();
        }

        private void DrawBreathingControls()
        {
            performer.BreathingEnabled = GUILayout.Toggle(performer.BreathingEnabled, "Breathing Enabled");
            DrawSlider("Breaths per minute", performer.BreathsPerMinute, 3f, 24f,
                value => performer.BreathsPerMinute = value);
            GUILayout.Label("Breath phase " + performer.BreathPhase.ToString("F2")
                            + "    value " + performer.BreathValue.ToString("F2"));

            GUILayout.Label("Morph Breathing");
            performer.MorphBreathingEnabled = GUILayout.Toggle(
                performer.MorphBreathingEnabled, "Morph breathing enabled");
            DrawSlider("Morph strength", performer.MorphBreathingStrength, 0f, 3f,
                value => performer.MorphBreathingStrength = value);
            DrawSlider("Breathe strength", performer.BreatheStrength, 0f, 2f,
                value => performer.BreatheStrength = value);
            DrawSlider("BreatheBelly strength", performer.BreatheBellyStrength, 0f, 2f,
                value => performer.BreatheBellyStrength = value);

            GUILayout.Label("Bone Breathing");
            performer.BoneBreathingEnabled = GUILayout.Toggle(
                performer.BoneBreathingEnabled, "Bone breathing enabled");
            DrawSlider("Bone strength", performer.BoneBreathingStrength, 0f, 2f,
                value => performer.BoneBreathingStrength = value);
        }

        private static void DrawSlider(string label, float current, float minimum, float maximum,
            System.Action<float> update)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            var value = GUILayout.HorizontalSlider(current, minimum, maximum);
            GUILayout.Label(value.ToString("F2"), GUILayout.Width(42f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(value - current) > 0.0001f) update(value);
        }

        private void DrawPoseButton(string label, PerformerPose pose)
        {
            var poseLabel = pose == null ? label + " (unassigned)" : label + ": " + pose.name;
            if (GUILayout.Button(poseLabel) && pose != null) RequestPose(pose);
        }

        private void RequestPose(PerformerPose pose)
        {
            if (performer == null)
            {
                Debug.LogError("Assign a SuccubusPerformer to the pose smoke harness.", this);
                return;
            }
            if (pose == null)
            {
                Debug.LogWarning("Assign all three PerformerPose assets before testing the pose smoke harness.", this);
                return;
            }

            performer.Pose(pose, transition);
        }

        private void RunAcceptanceChecks()
        {
            if (acceptanceHarness == null)
            {
                Debug.LogError("Add PerformerPoseAcceptanceHarness next to SuccubusPerformer to run F5 acceptance checks.", this);
                return;
            }
            acceptanceHarness.Run();
        }
    }
}
