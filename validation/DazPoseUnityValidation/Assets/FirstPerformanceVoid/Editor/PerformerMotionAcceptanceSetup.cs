using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Motion;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Generates BasicStroke assets and installs the isolated MotionDriver acceptance harness.</summary>
    public static class PerformerMotionAcceptanceSetup
    {
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string MotionFolder = "Assets/DazPose/Generated/Motion";
        private const string AcceptanceFolder = MotionFolder + "/Acceptance";
        private const string MaskPath = MotionFolder + "/RightArmMotion.mask";
        private const string ClipPath = AcceptanceFolder + "/BasicStroke.anim";
        private const string VariantPath = AcceptanceFolder + "/BasicStroke.asset";
        private const string SetPath = AcceptanceFolder + "/BasicStrokeSet.asset";

        [MenuItem("Tools/DAZ Pose/Motion/Generate BasicStroke and Install Acceptance Harness")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generate Motion acceptance assets in Edit Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before installing Motion acceptance.");

            FirstPerformanceVoidControls[] controlsFound = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).ToArray();
            if (controlsFound.Length != 1)
                throw new InvalidOperationException("Expected exactly one FirstPerformanceVoidControls in FirstPerformanceVoid. Found "
                    + controlsFound.Length + ".");
            FirstPerformanceVoidControls controls = controlsFound[0];
            var controlsData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlsData, "performer");
            if (performer == null || performer.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara reference on FirstPerformanceVoidControls is required.");
            Animator animator = performer.GetComponent<Animator>();
            if (animator == null || animator.transform != performer.transform)
                throw new InvalidOperationException("Motion acceptance requires Lara's Generic Animator on her actor root.");
            if (!PerformerMotionMaskUtility.TryResolveRightArm(animator, out Transform collar,
                    out Transform shoulder, out Transform forearm, out Transform hand,
                    out string[] rightArmPaths, out string resolveReason))
                throw new InvalidOperationException(resolveReason);
            if (!PerformerMotionMaskUtility.TryCreateMask(animator, out AvatarMask generatedMask, out resolveReason))
                throw new InvalidOperationException(resolveReason);

            EnsureFolder("Assets/DazPose/Generated");
            EnsureFolder(MotionFolder);
            EnsureFolder(AcceptanceFolder);
            AvatarMask mask = SaveMask(generatedMask);
            if (mask != generatedMask) UnityEngine.Object.DestroyImmediate(generatedMask);
            AnimationClip clip = SaveAcceptanceClip(animator, collar, shoulder, forearm, hand, rightArmPaths);
            PerformerMotionVariant variant = LoadOrCreate<PerformerMotionVariant>(VariantPath);
            variant.ConfigureInEditor(clip, "BasicStroke", AssetDatabase.GetAssetPath(clip),
                "Project-owned one-way right-arm acceptance motion. Endpoint 0 is lowered and forward; endpoint 1 is raised. MotionDriver supplies all reversal by scrubbing.");
            if (!variant.IsReady(out string variantReason))
                throw new InvalidOperationException("Generated BasicStroke is invalid: " + variantReason);

            PerformerMotionSet set = LoadOrCreate<PerformerMotionSet>(SetPath);
            set.ConfigureInEditor(new[] { variant });
            if (!set.IsReady(out string setReason)) throw new InvalidOperationException(setReason);

            MotionDriver driver = FindOrCreateDriver(scene);
            var driverData = new SerializedObject(driver);
            driverData.FindProperty("frequencyHz").floatValue = 1f;
            driverData.FindProperty("playOnEnable").boolValue = false;
            driverData.ApplyModifiedProperties();

            var performerData = new SerializedObject(performer);
            performerData.FindProperty("motionDriver").objectReferenceValue = driver;
            performerData.FindProperty("motionSet").objectReferenceValue = set;
            performerData.FindProperty("rightArmMotionMask").objectReferenceValue = mask;
            performerData.FindProperty("motionOwnershipBlendSeconds").floatValue = 0.18f;
            performerData.ApplyModifiedProperties();

            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(performer);
            EditorUtility.SetDirty(mask);
            EditorUtility.SetDirty(variant);
            EditorUtility.SetDirty(set);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save Motion references to FirstPerformanceVoid.");

            Debug.Log("MOTION_ACCEPTANCE_INSTALLED: driver=scene-level MotionDriver at 1 Hz; consumer=Lara right-collar Generic mask ("
                + rightArmPaths.Length + " transforms); set=" + SetPath + "; variant=" + VariantPath
                + "; clip=" + ClipPath + "; mask=" + MaskPath
                + ". Motion controls are integrated into the existing First Performance Void test window; the driver remains stopped until START is pressed.", controls);
        }

        private static AnimationClip SaveAcceptanceClip(Animator animator, Transform collar,
            Transform shoulder, Transform forearm, Transform hand, string[] maskPaths)
        {
            AnimationClip clip = LoadOrCreateClip(ClipPath);
            ClearClip(clip);

            Vector3 upperDirection = forearm.position - shoulder.position;
            Vector3 lowerDirection = hand.position - forearm.position;
            Vector3 raiseAxisWorld = Vector3.Cross(upperDirection.normalized, animator.transform.up);
            if (raiseAxisWorld.sqrMagnitude < 0.0001f)
                raiseAxisWorld = Vector3.Cross(upperDirection.normalized, animator.transform.forward);
            raiseAxisWorld.Normalize();
            Vector3 elbowAxisWorld = Vector3.Cross(lowerDirection.normalized, animator.transform.up);
            if (elbowAxisWorld.sqrMagnitude < 0.0001f)
                elbowAxisWorld = Vector3.Cross(lowerDirection.normalized, animator.transform.forward);
            elbowAxisWorld.Normalize();

            foreach (string path in maskPaths)
            {
                Transform bone = FindByPath(animator.transform, path);
                if (bone == null) throw new InvalidOperationException("Could not resolve right-arm mask path '" + path + "' in Lara's Generic hierarchy.");
                Quaternion start = bone.localRotation;
                Quaternion end = start;
                if (bone == collar)
                {
                    Vector3 frontAxis = LocalAxis(collar, animator.transform.up);
                    start = start * Quaternion.AngleAxis(-4f, frontAxis);
                    end = start;
                }
                else if (bone == shoulder)
                {
                    Vector3 frontAxis = LocalAxis(shoulder, animator.transform.up);
                    Vector3 raiseAxis = LocalAxis(shoulder, raiseAxisWorld);
                    start = start * Quaternion.AngleAxis(-18f, frontAxis)
                        * Quaternion.AngleAxis(8f, raiseAxis);
                    end = bone.localRotation * Quaternion.AngleAxis(-18f, frontAxis)
                        * Quaternion.AngleAxis(58f, raiseAxis);
                }
                else if (bone == forearm)
                {
                    Vector3 elbowAxis = LocalAxis(forearm, elbowAxisWorld);
                    start = start * Quaternion.AngleAxis(18f, elbowAxis);
                    end = start;
                }
                else if (bone == hand)
                {
                    Vector3 wristAxis = LocalAxis(hand, animator.transform.forward);
                    start = start * Quaternion.AngleAxis(4f, wristAxis);
                    end = start;
                }
                AddRotationCurves(clip, path, start, end);
            }

            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.Once;
            clip.EnsureQuaternionContinuity();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlend = false;
            settings.cycleOffset = 0f;
            settings.hasAdditiveReferencePose = false;
            settings.additiveReferencePoseTime = 0f;
            settings.additiveReferencePoseClip = null;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            ValidateAcceptanceClip(clip, maskPaths);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void ValidateAcceptanceClip(AnimationClip clip, string[] expectedPaths)
        {
            var expected = new HashSet<string>(expectedPaths, StringComparer.Ordinal);
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length != expected.Count * 4)
                throw new InvalidOperationException("BasicStroke must contain exactly four local-rotation curves for each right-arm mask transform.");
            foreach (EditorCurveBinding binding in bindings)
                if (binding.type != typeof(Transform) || !expected.Contains(binding.path)
                    || !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    throw new InvalidOperationException("BasicStroke contains an unexpected curve binding: "
                        + binding.path + " / " + binding.propertyName + ".");
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0
                || AnimationUtility.GetAnimationEvents(clip).Length != 0
                || AnimationUtility.GetAnimationClipSettings(clip).loopTime)
                throw new InvalidOperationException("BasicStroke must be a finite, non-looping skeletal clip without object curves or events.");
        }

        private static void AddRotationCurves(AnimationClip clip, string path,
            Quaternion start, Quaternion end)
        {
            SetRotationCurve(clip, path, "m_LocalRotation.x", start.x, end.x);
            SetRotationCurve(clip, path, "m_LocalRotation.y", start.y, end.y);
            SetRotationCurve(clip, path, "m_LocalRotation.z", start.z, end.z);
            SetRotationCurve(clip, path, "m_LocalRotation.w", start.w, end.w);
        }

        private static void SetRotationCurve(AnimationClip clip, string path, string property,
            float start, float end)
        {
            var curve = new AnimationCurve(new Keyframe(0f, start), new Keyframe(1f, end));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void ClearClip(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        }

        private static AvatarMask SaveMask(AvatarMask generated)
        {
            AvatarMask asset = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(generated, MaskPath);
                asset = generated;
            }
            else
            {
                for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                    asset.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
                asset.transformCount = generated.transformCount;
                for (int i = 0; i < generated.transformCount; i++)
                {
                    asset.SetTransformPath(i, generated.GetTransformPath(i));
                    asset.SetTransformActive(i, generated.GetTransformActive(i));
                }
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }

        private static MotionDriver FindOrCreateDriver(Scene scene)
        {
            MotionDriver[] drivers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MotionDriver>(true)).ToArray();
            if (drivers.Length > 1)
                throw new InvalidOperationException("Expected at most one scene-level MotionDriver. Found " + drivers.Length + ".");
            if (drivers.Length == 1)
            {
                if (drivers[0].transform.parent != null)
                    throw new InvalidOperationException("MotionDriver must be a scene-level object, not a child of Lara or Player.");
                return drivers[0];
            }
            var gameObject = new GameObject("MotionDriver");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create MotionDriver");
            return Undo.AddComponent<MotionDriver>(gameObject);
        }

        private static AnimationClip LoadOrCreateClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T created = AssetDatabase.LoadAssetAtPath<T>(path);
            if (created != null) return created;
            created = ScriptableObject.CreateInstance<T>();
            created.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static T Read<T>(SerializedObject data, string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = data.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static Transform FindByPath(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                current = current.Find(segment);
                if (current == null) return null;
            }
            return current;
        }

        private static Vector3 LocalAxis(Transform bone, Vector3 worldAxis)
        {
            Vector3 local = bone.InverseTransformDirection(worldAxis);
            return local.sqrMagnitude > 0.000001f ? local.normalized : Vector3.forward;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
