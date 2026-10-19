using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.UnityValidation.Partner;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation.Editor.Diagnostics
{
    /// <summary>Explicit, read-only checks for selected prepared actor prefabs and deterministic contact geometry.</summary>
    public static class PreparedActorDiagnostics
    {
        [MenuItem("DazPose/Diagnostics/Prepared Assets/Validate Selected Hand Grip")]
        public static void RunHandGrip()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Use these diagnostics in Edit Mode.");
            GameObject asset = SelectedPrefab();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(asset);
                instance.hideFlags = HideFlags.HideAndDontSave;
                SuccubusPerformer performer = instance.GetComponentInChildren<SuccubusPerformer>(true);
                Require(performer != null, "Selected prefab has no SuccubusPerformer.");
                Animator animator = performer.GetComponent<Animator>();
                Require(animator != null && animator.transform == performer.transform,
                    "Prepared Lara must have a Generic Animator on the SuccubusPerformer root.");
                PerformerHandGripController grip = performer.GetComponent<PerformerHandGripController>();
                Require(grip != null && grip.Profile != null, "Prepared Lara prefab has no assigned hand-grip profile.");
                Require(grip.Profile.IsReady(animator, out string reason), "Hand-grip profile is not ready: " + reason);
                string[] failures = HandGripRuntimeSelfTests.Run();
                Require(failures.Length == 0, "Hand-grip deterministic runtime checks failed: " + string.Join("; ", failures));
                SerializedObject performerState = new SerializedObject(performer);
                PerformerPose pose = performerState.FindProperty("initialPose")?.objectReferenceValue as PerformerPose;
                Require(pose != null && pose.Clip != null, "Selected prepared performer has no initial pose clip for the spatial proof.");
                string spatial = HandGripSpatialProof.Run(animator, grip.Profile, pose.Clip);
                Debug.Log("PREPARED_HANDGRIP_DIAGNOSTICS_PASS: " + AssetDatabase.GetAssetPath(asset)
                    + "; assertions=" + HandGripRuntimeSelfTests.AssertionCount + "; " + spatial);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [MenuItem("DazPose/Diagnostics/Prepared Assets/Validate Selected Partner")]
        public static void RunPartnerGeometry()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Use these diagnostics in Edit Mode.");
            // Exercise the actual runtime mapping and compliance functions with fixed values.
            Require(PartnerAnatomyTestController.BlendShapeWeightForErection(1f) == 0f,
                "Erection 1 must select the erect endpoint (zero blendshape weight).");
            Require(PartnerAnatomyTestController.BlendShapeWeightForErection(0f) == 100f,
                "Erection 0 must select the flaccid endpoint (full blendshape weight).");
            Require(PartnerAnatomyTestController.BlendShapeWeightForErection(-1f) == 100f
                && PartnerAnatomyTestController.BlendShapeWeightForErection(2f) == 0f,
                "Erection input must clamp to [0,1].");
            Quaternion basis = Quaternion.Euler(13f, -7f, 24f);
            Quaternion offset = PartnerAnatomyTestController.ApplyComplianceOffset(basis, new Vector3(5f, 0f, 0f));
            Require(Mathf.Abs(Quaternion.Angle(basis, offset) - 5f) < .001f,
                "Five degrees of compliance must offset the authored base rotation by five degrees.");
            Require(Quaternion.Angle(basis, PartnerAnatomyTestController.ApplyComplianceOffset(basis, Vector3.zero)) < 0.0001f,
                "Zero compliance must preserve the base rotation.");

            Vector3[] flaccid = { Vector3.zero, Vector3.up, Vector3.up * 2f };
            Vector3[] erect = { Vector3.right, Vector3.up + Vector3.right, Vector3.up * 2f + Vector3.right };
            Require(PartnerCenterlineGeometry.SegmentsNonzero(flaccid)
                && PartnerCenterlineGeometry.SegmentsNonzero(erect), "Centerline endpoints contain a zero-length segment.");
            Require(Mathf.Abs(PartnerCenterlineGeometry.PolylineLength(flaccid) - 2f) < 0.0001f,
                "Polyline arc length calculation failed on a deterministic straight centerline.");
            Require(Mathf.Abs(PartnerCenterlineGeometry.MinimumContinuousSegment(flaccid, erect) - 1f) < 0.0001f,
                "Continuous endpoint interpolation collapsed a centerline segment.");
            Require(Mathf.Abs(PartnerCenterlineGeometry.DistanceToPolyline(Vector3.right * 2f + Vector3.up, erect) - 1f) < 0.0001f,
                "Point-to-centerline distance calculation failed.");

            GameObject asset = SelectedPrefab();
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(asset);
                instance.hideFlags = HideFlags.HideAndDontSave;
                var contact = instance.GetComponentInChildren<PartnerContactTarget>(true);
                Require(contact != null, "Selected prefab has no prepared PartnerContactTarget.");
                Require(contact.IsReadyForContact(false, out string contactReason), "Prepared contact is invalid: " + contactReason);
                SkinnedMeshRenderer[] bodies = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Require(bodies.Any(body => body.sharedMesh != null && body.sharedMesh.vertexCount > 0),
                    "Selected prepared actor prefab has no populated skinned body mesh.");
                string[] shaftNames = { "shaft1", "shaft2", "shaft3", "shaft4", "shaft5", "shaft6", "shaft7" };
                Transform genesis = FindDeep(instance.transform, "Genesis8Male");
                Require(genesis != null && shaftNames.All(name => FindDeep(genesis, name) != null),
                    "Selected prepared partner prefab is missing one or more characterized shaft-chain bones.");
                Debug.Log("PREPARED_PARTNER_DIAGNOSTICS_PASS: " + AssetDatabase.GetAssetPath(asset)
                    + "; populated skinned body; seven shaft bones; parameter/compliance and centerline geometry checks passed.");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject SelectedPrefab()
        {
            GameObject asset = Selection.activeObject as GameObject;
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            Require(asset != null && !string.IsNullOrEmpty(path) && PrefabUtility.IsPartOfPrefabAsset(asset),
                "Select a prepared actor prefab asset in the Project window before running this read-only diagnostic.");
            return asset;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    /// <summary>Pure geometry helpers retained from the retired scene builder.</summary>
    internal static class PartnerCenterlineGeometry
    {
        internal static bool SegmentsNonzero(Vector3[] line)
        {
            if (line == null || line.Length < 2) return false;
            for (int i = 1; i < line.Length; i++)
                if ((line[i] - line[i - 1]).sqrMagnitude < 1e-10f) return false;
            return true;
        }

        internal static float PolylineLength(Vector3[] points)
        {
            if (points == null || points.Length < 2) return 0f;
            float result = 0f;
            for (int i = 1; i < points.Length; i++) result += Vector3.Distance(points[i - 1], points[i]);
            return result;
        }

        internal static float MinimumContinuousSegment(Vector3[] a, Vector3[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length < 2) return 0f;
            float minimum = float.PositiveInfinity;
            for (int i = 1; i < a.Length; i++)
            {
                Vector3 start = a[i] - a[i - 1];
                Vector3 change = (b[i] - b[i - 1]) - start;
                float t = change.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(-Vector3.Dot(start, change) / change.sqrMagnitude);
                minimum = Mathf.Min(minimum, (start + change * t).magnitude);
            }
            return minimum;
        }

        internal static float DistanceToPolyline(Vector3 point, Vector3[] line)
        {
            if (line == null || line.Length < 2) return float.PositiveInfinity;
            float min = float.PositiveInfinity;
            for (int i = 1; i < line.Length; i++)
            {
                Vector3 segment = line[i] - line[i - 1];
                float t = Mathf.Clamp01(Vector3.Dot(point - line[i - 1], segment) / Mathf.Max(segment.sqrMagnitude, 1e-12f));
                min = Mathf.Min(min, Vector3.Distance(point, line[i - 1] + t * segment));
            }
            return min;
        }
    }
}
