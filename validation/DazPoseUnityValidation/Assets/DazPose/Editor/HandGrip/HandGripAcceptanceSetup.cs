using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.HandGrip
{
    public static class HandGripAcceptanceSetup
    {
        public const string SourceScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        public const string AcceptanceScenePath = "Assets/Scenes/HandGripAcceptance.unity";
        public const string ProfilePath = "Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset";

        [MenuItem("Tools/DAZ Pose/HandGrip/Create Isolated Acceptance Scene")]
        public static void CreateAcceptanceScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create the HandGrip acceptance scene in Edit Mode.");
            if (!File.Exists(SourceScenePath))
                throw new FileNotFoundException("The functioning FirstPerformanceVoid source scene is missing.", SourceScenePath);

            if (!File.Exists(AcceptanceScenePath))
            {
                if (!AssetDatabase.CopyAsset(SourceScenePath, AcceptanceScenePath))
                    throw new IOException("Could not duplicate FirstPerformanceVoid into the isolated HandGrip acceptance scene.");
                AssetDatabase.Refresh();
            }
            Scene scene = EditorSceneManager.OpenScene(AcceptanceScenePath, OpenSceneMode.Single);
            SuccubusPerformer performer = FindPerformer(scene);
            Animator animator = performer != null ? performer.GetComponent<Animator>() : null;
            if (performer == null || animator == null)
                throw new InvalidOperationException("The duplicated scene must contain one Lara SuccubusPerformer with a Generic Animator.");
            if (animator.transform != performer.transform)
                throw new InvalidOperationException("HandGrip acceptance expects Lara's Generic Animator on her performer root.");

            MotionDriver driver = performer.MotionSource;
            PerformerMotionSet motionSet = ReadReference<PerformerMotionSet>(performer, "motionSet");
            string motionReason = null;
            bool motionReady = motionSet != null && motionSet.IsReady(out motionReason);
            if (driver == null || motionSet == null || !motionReady)
                throw new InvalidOperationException("The source scene must have its existing MotionDriver acceptance setup. "
                    + (driver == null ? "MotionDriver is missing."
                        : motionSet == null ? "PerformerMotionSet is missing." : motionReason));
            if (!PerformerMotionMaskUtility.TryResolveRightArm(animator, out _, out _, out _,
                    out Transform hand, out _, out string handReason))
                throw new InvalidOperationException(handReason);

            HandGripRigProfile profile = AssetDatabase.LoadAssetAtPath<HandGripRigProfile>(ProfilePath);
            if (profile == null) profile = CreateProfileAsset(ProfilePath);
            if (!profile.IsReady(animator, out _))
            {
                HandGripCalibrationWindow.ConfigureFromRig(animator, profile);
                EditorUtility.SetDirty(profile);
            }

            PerformerHandGripController controller = performer.GetComponent<PerformerHandGripController>();
            if (controller == null) controller = Undo.AddComponent<PerformerHandGripController>(performer.gameObject);
            GripContactRod rod = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GripContactRod>(true)).FirstOrDefault();
            Transform fixtureRoot;
            if (rod == null)
            {
                fixtureRoot = new GameObject("HandGripFixture").transform;
                SceneManager.MoveGameObjectToScene(fixtureRoot.gameObject, scene);
                Undo.RegisterCreatedObjectUndo(fixtureRoot.gameObject, "Create HandGrip acceptance rod");
                Vector3[] handCenters = SampleMotionGripCenters(motionSet.Variants[0].Clip,
                    performer.gameObject, hand, profile.GripCenterLocalPosition);
                Vector3 stroke = handCenters[1] - handCenters[0];
                if (stroke.sqrMagnitude < 1e-8f)
                    stroke = animator.transform.up;
                Vector3 tangent = stroke.normalized;
                Vector3 center = (handCenters[0] + handCenters[1]) * 0.5f;
                fixtureRoot.position = center;
                fixtureRoot.rotation = Quaternion.FromToRotation(Vector3.up, tangent);
                float halfLength = Mathf.Max(0.45f, stroke.magnitude * 0.5f + 0.15f);
                Transform start = CreateChild(fixtureRoot, "Start", Vector3.down * halfLength);
                Transform end = CreateChild(fixtureRoot, "End", Vector3.up * halfLength);
                rod = Undo.AddComponent<GripContactRod>(fixtureRoot.gameObject);
                rod.StartPoint = start;
                rod.EndPoint = end;
                rod.Radius = 0.035f;
                Vector3 normal = Vector3.ProjectOnPlane(animator.transform.right, tangent);
                if (normal.sqrMagnitude < 1e-8f)
                    normal = Vector3.ProjectOnPlane(animator.transform.forward, tangent);
                if (normal.sqrMagnitude < 1e-8f)
                    normal = Vector3.ProjectOnPlane(animator.transform.up, tangent);
                if (normal.sqrMagnitude < 1e-8f)
                    throw new InvalidOperationException("The acceptance rod could not derive a stable roll reference from Lara's rig.");
                rod.ReferenceTransform = fixtureRoot;
                rod.ReferenceNormalLocal = fixtureRoot.InverseTransformDirection(normal.normalized);
            }
            else fixtureRoot = rod.transform;

            SetReference(controller, "profile", profile);
            SetReference(controller, "targetSource", rod);
            SetBool(controller, "gripOnEnable", true);
            SetFloat(controller, "gripStrength01", 1f);
            SetBool(controller, "drawDiagnostics", true);
            SetBool(performer, "startHidden", false);

            HandGripAcceptanceHarness harness = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HandGripAcceptanceHarness>(true)).FirstOrDefault();
            if (harness == null) harness = Undo.AddComponent<HandGripAcceptanceHarness>(fixtureRoot.gameObject);
            harness.ConfigureForEditor(controller, rod, driver, fixtureRoot);

            EditorUtility.SetDirty(performer);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(rod);
            EditorUtility.SetDirty(harness);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save the isolated HandGripAcceptance scene.");

            string hierarchy = string.Join("; ", profile.Digits.Select(digit =>
                digit.Digit + "=[" + string.Join(",", digit.JointPaths.Select(PathLeaf)) + "] probes=" + digit.Probes.Length));
            Debug.Log("HANDGRIP_ACCEPTANCE_READY: scene=" + AcceptanceScenePath
                + "; hand=rHand (right); hierarchy=" + hierarchy
                + "; clearance=" + profile.ContactClearance.ToString("0.000") + "m"
                + "; blend=" + profile.GripInSeconds.ToString("0.00") + "/" + profile.ReleaseSeconds.ToString("0.00")
                + "s; solver iterations=" + profile.BinarySearchIterations
                + "; graph=Motion → Gesture → HandGrip AnimationScriptPlayable → Breathing; Position01 comes from MotionDriver.CurrentSample.", controller);
        }

        [MenuItem("Tools/DAZ Pose/HandGrip/Run Geometry and Solver Tests")]
        public static void RunSolverTests()
        {
            string[] failures = HandGripRuntimeSelfTests.Run();
            if (failures.Length != 0)
                throw new InvalidOperationException("HandGrip self-tests failed:\n - " + string.Join("\n - ", failures));
            Debug.Log("HANDGRIP_SELF_TESTS_PASSED: geometry, clamping, invalid rod/frame, probe classification, adaptive radius/strength solve, base-penetration reporting, digit independence, and ownership blending.");
        }

        private static SuccubusPerformer FindPerformer(Scene scene)
        {
            SuccubusPerformer[] performers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SuccubusPerformer>(true)).ToArray();
            if (performers.Length != 1)
                throw new InvalidOperationException("Expected one Lara SuccubusPerformer in the acceptance scene; found " + performers.Length + ".");
            return performers[0];
        }

        private static Vector3[] SampleMotionGripCenters(AnimationClip clip, GameObject root,
            Transform hand, Vector3 gripCenterLocal)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            var positions = new Vector3[transforms.Length];
            var rotations = new Quaternion[transforms.Length];
            var scales = new Vector3[transforms.Length];
            for (int index = 0; index < transforms.Length; index++)
            {
                positions[index] = transforms[index].localPosition;
                rotations[index] = transforms[index].localRotation;
                scales[index] = transforms[index].localScale;
            }

            var centers = new Vector3[2];
            try
            {
                clip.SampleAnimation(root, 0f);
                centers[0] = hand.TransformPoint(gripCenterLocal);
                clip.SampleAnimation(root, clip.length);
                centers[1] = hand.TransformPoint(gripCenterLocal);
            }
            finally
            {
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index] == null) continue;
                    transforms[index].localPosition = positions[index];
                    transforms[index].localRotation = rotations[index];
                    transforms[index].localScale = scales[index];
                }
            }
            return centers;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Create HandGrip rod endpoint");
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            return child;
        }

        private static HandGripRigProfile CreateProfileAsset(string path)
        {
            EnsureFolder("Assets/DazPose/Generated");
            EnsureFolder("Assets/DazPose/Generated/HandGrip");
            EnsureFolder("Assets/DazPose/Generated/HandGrip/Profiles");
            var profile = ScriptableObject.CreateInstance<HandGripRigProfile>();
            profile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static T ReadReference<T>(UnityEngine.Object target, string name) where T : UnityEngine.Object
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(name);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void SetReference(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string name, bool value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string name, float value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(name).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string PathLeaf(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator < 0 ? path : path.Substring(separator + 1);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
