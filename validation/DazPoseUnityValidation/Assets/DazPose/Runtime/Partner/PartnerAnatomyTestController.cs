using System;
using UnityEngine;

namespace DazPose.UnityValidation.Partner
{
    /// <summary>Small isolated acceptance harness for artist-authored G8M endpoints.</summary>
    public sealed class PartnerAnatomyTestController : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer structuralBody;
        [SerializeField] private SkinnedMeshRenderer shellRenderer;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform[] shaftBones = new Transform[7];
        [SerializeField] private Transform mainPelvis;
        [SerializeField] private Transform donorPelvis;
        [SerializeField] private Transform[] distributedBones = new Transform[4];
        [SerializeField] private Vector3[] flaccidCenterlineRoot = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] erectCenterlineRoot = Array.Empty<Vector3>();
        [SerializeField] private float flaccidRadius = 0.020f;
        [SerializeField] private float erectRadius = 0.023f;
        [SerializeField] private Vector3 contactTargetRoot;
        [SerializeField] private float erection01 = 1f;
        [SerializeField] private float position01 = 0.5f;
        [SerializeField] private bool shellVisible = true;

        private Quaternion[] erectBaseRotations;
        private Quaternion mainPelvisBaseRotation;
        private Vector3 mainPelvisBasePosition;
        private Vector3 donorMainLocalPosition;
        private Quaternion donorMainLocalRotation;
        private CameraView cameraView = CameraView.ThreeQuarter;
        private float shaft1X;
        private float shaft4X;
        private float distributedBend;
        private float pelvisTest;
        private bool showBones = true;
        private bool showSkeletalLine;
        private bool showGeometryLine = true;
        private bool showRadii = true;

        public float Erection01
        {
            get => erection01;
            set
            {
                erection01 = Mathf.Clamp01(value);
                ApplyErection();
                ApplyCompliance();
            }
        }

        public static float BlendShapeWeightForErection(float value) => 100f * (1f - Mathf.Clamp01(value));
        public static Quaternion ApplyComplianceOffset(Quaternion erectBase, Vector3 degrees) => erectBase * Quaternion.Euler(degrees);

        public bool ShellVisible
        {
            get => shellVisible;
            set
            {
                shellVisible = value;
                if (shellRenderer != null) shellRenderer.enabled = value;
                ApplyCompliance();
            }
        }

        public Vector3 ContactTargetRoot => contactTargetRoot;
        public float ContactRadius => Mathf.Lerp(flaccidRadius, erectRadius, Erection01);
        public float Position01
        {
            get => position01;
            set { position01 = Mathf.Clamp01(value); UpdateContactTarget(); }
        }

        private enum CameraView { Front, Side, ThreeQuarter, Root, Glans }

        private void Awake()
        {
            CaptureErectBase();
            if (mainPelvis != null)
            {
                mainPelvisBaseRotation = mainPelvis.localRotation;
                mainPelvisBasePosition = mainPelvis.localPosition;
            }
            CaptureDonorOffset();
            ApplyErection();
            ApplyCompliance();
            ShellVisible = shellVisible;
            UpdateCamera();
        }

        private void LateUpdate()
        {
            if (mainPelvis != null)
            {
                mainPelvis.localRotation = mainPelvisBaseRotation * Quaternion.Euler(0f, 0f, pelvisTest * 10f);
                mainPelvis.localPosition = mainPelvisBasePosition + new Vector3(0f, 0f, pelvisTest * 0.02f);
            }
            ApplyDonorFollow();
            ApplyCompliance();
            UpdateCamera();
        }

        public void Restore()
        {
            pelvisTest = shaft1X = shaft4X = distributedBend = 0f;
            if (mainPelvis != null)
            {
                mainPelvis.localRotation = mainPelvisBaseRotation;
                mainPelvis.localPosition = mainPelvisBasePosition;
            }
            Erection01 = 1f;
            ShellVisible = true;
            ApplyCompliance();
            ApplyDonorFollow();
        }

        public void SetShaft1X(float degrees) { shaft1X = Mathf.Clamp(degrees, -5f, 5f); ApplyCompliance(); }
        public void SetShaft4X(float degrees) { shaft4X = Mathf.Clamp(degrees, -5f, 5f); ApplyCompliance(); }
        public void SetDistributedBend(float degrees) { distributedBend = Mathf.Clamp(degrees, -5f, 5f); ApplyCompliance(); }
        public void SetPelvisTest(float value) { pelvisTest = Mathf.Clamp01(value); }

        private void CaptureErectBase()
        {
            erectBaseRotations = new Quaternion[shaftBones.Length];
            for (int i = 0; i < shaftBones.Length; i++)
                erectBaseRotations[i] = shaftBones[i] != null ? shaftBones[i].localRotation : Quaternion.identity;
        }

        private void ApplyErection()
        {
            if (structuralBody != null && structuralBody.sharedMesh != null)
            {
                int index = structuralBody.sharedMesh.GetBlendShapeIndex("ErectionToFlaccid");
                if (index >= 0) structuralBody.SetBlendShapeWeight(index, BlendShapeWeightForErection(erection01));
            }
            if (shellRenderer != null && shellRenderer.sharedMesh != null)
            {
                int index = shellRenderer.sharedMesh.GetBlendShapeIndex("ShellErectionToFlaccid");
                if (index >= 0) shellRenderer.SetBlendShapeWeight(index, BlendShapeWeightForErection(erection01));
            }
            UpdateContactTarget();
        }

        private void ApplyCompliance()
        {
            float[] distribution = { 0.15f, 0.30f, 0.40f, 0.15f };
            bool complianceEnabled = Mathf.Approximately(Erection01, 1f) && !ShellVisible;
            for (int i = 0; i < shaftBones.Length; i++)
            {
                Transform bone = shaftBones[i];
                if (bone == null) continue;
                float x = complianceEnabled ? (i == 0 ? shaft1X : i == 3 ? shaft4X : 0f) : 0f;
                int distributedIndex = Array.IndexOf(distributedBones, bone);
                if (complianceEnabled && distributedIndex >= 0) x += distributedBend * distribution[distributedIndex];
                bone.localRotation = ApplyComplianceOffset(erectBaseRotations[i], new Vector3(x, 0f, 0f));
            }
        }

        private void CaptureDonorOffset()
        {
            if (mainPelvis == null || donorPelvis == null) return;
            donorMainLocalPosition = mainPelvis.InverseTransformPoint(donorPelvis.position);
            donorMainLocalRotation = Quaternion.Inverse(mainPelvis.rotation) * donorPelvis.rotation;
        }

        private void ApplyDonorFollow()
        {
            if (mainPelvis == null || donorPelvis == null) return;
            Vector3 worldPosition = mainPelvis.TransformPoint(donorMainLocalPosition);
            Quaternion worldRotation = mainPelvis.rotation * donorMainLocalRotation;
            donorPelvis.position = worldPosition;
            donorPelvis.rotation = worldRotation;
        }

        private void UpdateContactTarget()
        {
            if (flaccidCenterlineRoot.Length == 0 || erectCenterlineRoot.Length != flaccidCenterlineRoot.Length) return;
            GetContactFrame(Position01, out contactTargetRoot, out _, out _);
        }

        public void GetContactFrame(float position, out Vector3 centerRoot, out Vector3 tangentRoot, out float radius)
        {
            int count = Mathf.Min(flaccidCenterlineRoot.Length, erectCenterlineRoot.Length);
            if (count < 2) { centerRoot = Vector3.zero; tangentRoot = Vector3.forward; radius = ContactRadius; return; }
            float scaled = Mathf.Clamp01(position) * (count - 1);
            int i = Mathf.Min(count - 2, Mathf.FloorToInt(scaled));
            float t = scaled - i;
            Vector3 a = Vector3.Lerp(flaccidCenterlineRoot[i], erectCenterlineRoot[i], Erection01);
            Vector3 b = Vector3.Lerp(flaccidCenterlineRoot[i + 1], erectCenterlineRoot[i + 1], Erection01);
            centerRoot = Vector3.Lerp(a, b, t);
            tangentRoot = (b - a).normalized;
            radius = ContactRadius;
        }

        private void UpdateCamera()
        {
            if (viewCamera == null || erectCenterlineRoot.Length < 2) return;
            Vector3 baseTarget = transform.TransformPoint(contactTargetRoot);
            if (cameraView == CameraView.Root) baseTarget = transform.TransformPoint(PointOnLine(.1f));
            if (cameraView == CameraView.Glans) baseTarget = transform.TransformPoint(PointOnLine(.9f));
            Vector3 offset = cameraView == CameraView.Side ? Vector3.right * 0.62f : cameraView == CameraView.ThreeQuarter ? new Vector3(0.46f, 0.18f, 0.46f) : Vector3.forward * 0.75f;
            if (cameraView == CameraView.Root || cameraView == CameraView.Glans) offset *= 0.34f;
            viewCamera.transform.position = baseTarget + offset;
            viewCamera.transform.rotation = Quaternion.LookRotation(baseTarget - viewCamera.transform.position, Vector3.up);
        }

        private Vector3 PointOnLine(float position)
        {
            int count = Mathf.Min(flaccidCenterlineRoot.Length, erectCenterlineRoot.Length);
            float scaled = Mathf.Clamp01(position) * (count - 1);
            int i = Mathf.Min(count - 2, Mathf.FloorToInt(scaled));
            float t = scaled - i;
            Vector3 a = Vector3.Lerp(flaccidCenterlineRoot[i], erectCenterlineRoot[i], Erection01);
            Vector3 b = Vector3.Lerp(flaccidCenterlineRoot[i + 1], erectCenterlineRoot[i + 1], Erection01);
            return Vector3.Lerp(a, b, t);
        }

        public void DrawSmokeTestControls()
        {
            GUILayout.Label("G8M Partner Anatomy Acceptance", GUI.skin.box);
            GUILayout.Label("Erection01: " + Erection01.ToString("0.00"));
            float next = GUILayout.HorizontalSlider(Erection01, 0f, 1f);
            if (!Mathf.Approximately(next, Erection01)) Erection01 = next;
            GUILayout.BeginHorizontal();
            foreach (float value in new[] { 0f, .25f, .5f, .75f, 1f }) if (GUILayout.Button(value.ToString("0.##"))) Erection01 = value;
            GUILayout.EndHorizontal();
            ShellVisible = GUILayout.Toggle(ShellVisible, "Shell visible (turn off for bone compliance)");
            GUILayout.Space(6);
            GUILayout.Label("Erect-state compliance (degrees)");
            GUILayout.Label("Active only at Erection01 = 1 with shell hidden.");
            DrawAngleControl("shaft1 X", shaft1X, SetShaft1X);
            DrawAngleControl("shaft4 X", shaft4X, SetShaft4X);
            DrawAngleControl("distributed bend", distributedBend, SetDistributedBend);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("shaft1 +3°")) SetShaft1X(3f);
            if (GUILayout.Button("shaft1 -3°")) SetShaft1X(-3f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("shaft1 +5°")) SetShaft1X(5f);
            if (GUILayout.Button("shaft1 -5°")) SetShaft1X(-5f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("shaft4 +3°")) SetShaft4X(3f);
            if (GUILayout.Button("shaft4 -3°")) SetShaft4X(-3f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("shaft4 +5°")) SetShaft4X(5f);
            if (GUILayout.Button("shaft4 -5°")) SetShaft4X(-5f);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("Contact preview: radius " + (ContactRadius * 100f).ToString("0.0") + " cm");
            GUILayout.Label("Position01: " + Position01.ToString("0.00"));
            Position01 = GUILayout.HorizontalSlider(Position01, 0f, 1f);
            showBones = GUILayout.Toggle(showBones, "Bone centers");
            showSkeletalLine = GUILayout.Toggle(showSkeletalLine, "Skeletal line (erect compliance only)");
            showGeometryLine = GUILayout.Toggle(showGeometryLine, "Geometry-derived contact line");
            showRadii = GUILayout.Toggle(showRadii, "Radius preview");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Front")) cameraView = CameraView.Front;
            if (GUILayout.Button("Side")) cameraView = CameraView.Side;
            if (GUILayout.Button("3/4")) cameraView = CameraView.ThreeQuarter;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Root close-up")) cameraView = CameraView.Root;
            if (GUILayout.Button("Glans close-up")) cameraView = CameraView.Glans;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("Donor pelvis follow diagnostic");
            pelvisTest = GUILayout.HorizontalSlider(pelvisTest, 0f, 1f);
            GUILayout.Label("main pelvis: small translation + 10° rotation; donor follows explicitly");
            if (GUILayout.Button("Restore canonical erect state")) Restore();
        }

        private void DrawAngleControl(string label, float current, Action<float> setter)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(116));
            setter(GUILayout.HorizontalSlider(current, -5f, 5f));
            GUILayout.Label(current.ToString("+0.0;-0.0;0.0") + "°", GUILayout.Width(48));
            GUILayout.EndHorizontal();
        }

        private void OnDrawGizmos()
        {
            if (shaftBones != null)
            {
                Gizmos.color = showSkeletalLine ? Color.yellow : new Color(1f, .8f, .1f, .45f);
                for (int i = 0; i < shaftBones.Length; i++) if (shaftBones[i] != null)
                {
                    if (showBones) Gizmos.DrawSphere(shaftBones[i].position, 0.009f);
                    if (showSkeletalLine && i > 0 && shaftBones[i - 1] != null) Gizmos.DrawLine(shaftBones[i - 1].position, shaftBones[i].position);
                }
                if (showSkeletalLine && erection01 >= .9999f && erectCenterlineRoot != null && erectCenterlineRoot.Length > 0 && shaftBones.Length > 0 && shaftBones[shaftBones.Length - 1] != null)
                {
                    Vector3 tip = transform.TransformPoint(erectCenterlineRoot[erectCenterlineRoot.Length - 1]);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(shaftBones[shaftBones.Length - 1].position, tip);
                    Gizmos.DrawSphere(tip, 0.009f);
                }
            }
            if (!showGeometryLine || erectCenterlineRoot == null || flaccidCenterlineRoot == null || erectCenterlineRoot.Length < 2 || erectCenterlineRoot.Length != flaccidCenterlineRoot.Length) return;
            Vector3 previous = transform.TransformPoint(Vector3.Lerp(flaccidCenterlineRoot[0], erectCenterlineRoot[0], Erection01));
            for (int i = 1; i < erectCenterlineRoot.Length; i++)
            {
                Vector3 current = transform.TransformPoint(Vector3.Lerp(flaccidCenterlineRoot[i], erectCenterlineRoot[i], Erection01));
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(previous, current);
                if (showRadii) Gizmos.DrawWireSphere(current, ContactRadius);
                previous = current;
            }
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(transform.TransformPoint(contactTargetRoot), 0.012f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(erectCenterlineRoot[erectCenterlineRoot.Length - 1]), 0.009f);
            GetContactFrame(Position01, out Vector3 center, out Vector3 tangent, out _);
            Vector3 worldCenter = transform.TransformPoint(center);
            Vector3 worldTangent = transform.TransformDirection(tangent).normalized;
            Gizmos.DrawLine(worldCenter - worldTangent * 0.06f, worldCenter + worldTangent * 0.06f);
        }
    }

}
