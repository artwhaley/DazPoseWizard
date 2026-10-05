using System;
using System.Collections.Generic;
using System.IO;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.HandGrip
{
    public sealed class HandGripCalibrationWindow : EditorWindow
    {
        private const string ProfilePath = "Assets/DazPose/Generated/HandGrip/Profiles/LaraRightHandGrip.asset";
        private static readonly HandGripDigit[] Digits =
        {
            HandGripDigit.Thumb, HandGripDigit.Index, HandGripDigit.Middle,
            HandGripDigit.Ring, HandGripDigit.Little
        };

        private HandGripRigProfile _profile;
        private Animator _animator;
        private readonly float[] _previewCurls = new float[5];
        private readonly Dictionary<Transform, Quaternion> _originalRotations = new Dictionary<Transform, Quaternion>();
        private bool _previewActive;
        private Vector2 _scroll;

        [MenuItem("Tools/DAZ Pose/HandGrip/Open Calibration Window")]
        public static void Open()
        {
            HandGripCalibrationWindow window = GetWindow<HandGripCalibrationWindow>("Hand Grip Calibration");
            window.minSize = new Vector2(390f, 420f);
            window.TryResolveSelection();
            window.Show();
        }

        [MenuItem("Tools/DAZ Pose/HandGrip/Capture Lara Right Hand Profile From Selection")]
        public static void CaptureProfileFromSelection()
        {
            Animator animator = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<Animator>() : null;
            if (animator == null)
                throw new InvalidOperationException("Select Lara or one of her rig transforms before capturing the right-hand profile.");
            HandGripRigProfile profile = LoadOrCreateProfile(ProfilePath);
            ConfigureFromRig(animator, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            Debug.Log("HANDGRIP_PROFILE_CAPTURED: right hand at " + profile.HandPath
                + "; 3 joints and 4 probes for each digit; suggested closed rotations were inferred from the discovered palm/finger geometry. Review in the calibration window.", profile);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Preview and capture project-owned hand calibration. Closing this window restores every previewed transform.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _profile = (HandGripRigProfile)EditorGUILayout.ObjectField("Hand Grip Profile", _profile,
                typeof(HandGripRigProfile), false);
            _animator = (Animator)EditorGUILayout.ObjectField("Generic Animator", _animator,
                typeof(Animator), true);
            if (EditorGUI.EndChangeCheck())
            {
                RestorePreview();
                ClearOriginals();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture from selected rig")) CaptureSelectedRig();
                if (GUILayout.Button("Use open calibration")) ApplyEndpoint(open: true);
                if (GUILayout.Button("Use closed calibration")) ApplyEndpoint(open: false);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture all open")) CaptureAll(open: true);
                if (GUILayout.Button("Capture all closed")) CaptureAll(open: false);
            }
            if (_profile == null || _animator == null)
            {
                EditorGUILayout.HelpBox("Assign a profile and Lara's Generic Animator, or select a rig transform and capture the right hand.", MessageType.Warning);
                return;
            }
            if (!_profile.IsReady(_animator, out string reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Error);
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Hand: " + _profile.HandPath, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Blend: in " + _profile.GripInSeconds.ToString("0.00")
                + "s  release " + _profile.ReleaseSeconds.ToString("0.00") + "s"
                + "  iterations " + _profile.BinarySearchIterations);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (HandGripDigit digit in Digits)
            {
                HandGripDigitProfile digitProfile = FindDigit(digit);
                if (digitProfile == null) continue;
                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField(digit + "  (" + digitProfile.JointPaths.Length + " joints, "
                    + digitProfile.Probes.Length + " probes)", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                float curl = EditorGUILayout.Slider("Preview curl", _previewCurls[(int)digit], 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    _previewCurls[(int)digit] = curl;
                    BeginPreviewIfNeeded();
                    ApplyPreview();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Capture open")) CaptureDigit(digit, open: true);
                    if (GUILayout.Button("Capture closed")) CaptureDigit(digit, open: false);
                    if (GUILayout.Button("Preview this digit"))
                    {
                        Array.Clear(_previewCurls, 0, _previewCurls.Length);
                        _previewCurls[(int)digit] = 1f;
                        BeginPreviewIfNeeded();
                        ApplyPreview();
                    }
                }
                for (int joint = 0; joint < digitProfile.JointPaths.Length; joint++)
                {
                    string path = digitProfile.JointPaths[joint];
                    EditorGUILayout.LabelField(path, "open/closed local rotation calibrated");
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview all at zero"))
                {
                    Array.Clear(_previewCurls, 0, _previewCurls.Length);
                    BeginPreviewIfNeeded();
                    ApplyPreview();
                }
                if (GUILayout.Button("Stop preview and restore")) RestorePreview();
            }
        }

        private void OnDisable() => RestorePreview();

        private void TryResolveSelection()
        {
            if (_animator == null && Selection.activeGameObject != null)
                _animator = Selection.activeGameObject.GetComponentInParent<Animator>();
            if (_profile == null) _profile = AssetDatabase.LoadAssetAtPath<HandGripRigProfile>(ProfilePath);
        }

        private void CaptureSelectedRig()
        {
            if (_animator == null && Selection.activeGameObject != null)
                _animator = Selection.activeGameObject.GetComponentInParent<Animator>();
            if (_animator == null)
            {
                EditorUtility.DisplayDialog("Hand Grip Calibration", "Select a rig transform or assign a Generic Animator first.", "OK");
                return;
            }
            _profile = _profile != null ? _profile : LoadOrCreateProfile(ProfilePath);
            ConfigureFromRig(_animator, _profile);
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void CaptureDigit(HandGripDigit digit, bool open)
        {
            HandGripDigitProfile definition = FindDigit(digit);
            if (definition == null) return;
            var rotations = new Quaternion[definition.JointPaths.Length];
            for (int index = 0; index < rotations.Length; index++)
            {
                Transform bone = FindByPath(_animator.transform, definition.JointPaths[index]);
                if (bone == null) return;
                Undo.RecordObject(bone, "Capture Hand Grip Calibration");
                rotations[index] = bone.localRotation;
            }
            if (open) definition.CaptureOpenForEditor(rotations);
            else definition.CaptureClosedForEditor(rotations);
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void CaptureAll(bool open)
        {
            if (_profile == null || _animator == null) return;
            foreach (HandGripDigit digit in Digits) CaptureDigit(digit, open);
        }

        private void ApplyEndpoint(bool open)
        {
            BeginPreviewIfNeeded();
            foreach (HandGripDigit digit in Digits) _previewCurls[(int)digit] = open ? 0f : 1f;
            ApplyPreview();
        }

        private void BeginPreviewIfNeeded()
        {
            if (_previewActive) return;
            _originalRotations.Clear();
            if (_profile == null || _animator == null) return;
            foreach (HandGripDigitProfile digit in _profile.Digits)
            {
                if (digit == null) continue;
                foreach (string path in digit.JointPaths)
                {
                    Transform bone = FindByPath(_animator.transform, path);
                    if (bone != null && !_originalRotations.ContainsKey(bone))
                        _originalRotations.Add(bone, bone.localRotation);
                }
            }
            _previewActive = _originalRotations.Count > 0;
        }

        private void ApplyPreview()
        {
            if (!_previewActive || _profile == null || _animator == null) return;
            foreach (HandGripDigitProfile digit in _profile.Digits)
            {
                if (digit == null) continue;
                float amount = Mathf.Clamp01(_previewCurls[(int)digit.Digit]);
                for (int index = 0; index < digit.JointPaths.Length; index++)
                {
                    Transform bone = FindByPath(_animator.transform, digit.JointPaths[index]);
                    if (bone == null) continue;
                    bone.localRotation = Quaternion.Slerp(digit.OpenLocalRotations[index],
                        digit.ClosedLocalRotations[index], amount);
                }
            }
            SceneView.RepaintAll();
        }

        private void RestorePreview()
        {
            if (!_previewActive) return;
            foreach (KeyValuePair<Transform, Quaternion> item in _originalRotations)
            {
                if (item.Key == null) continue;
                item.Key.localRotation = item.Value;
            }
            _previewActive = false;
            SceneView.RepaintAll();
        }

        private void ClearOriginals() => _originalRotations.Clear();

        private HandGripDigitProfile FindDigit(HandGripDigit digit)
        {
            if (_profile == null) return null;
            foreach (HandGripDigitProfile item in _profile.Digits)
                if (item != null && item.Digit == digit) return item;
            return null;
        }

        public static void ConfigureFromRig(Animator animator, HandGripRigProfile profile)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!PerformerMotionMaskUtility.TryResolveRightArm(animator, out _, out _, out _,
                    out Transform hand, out _, out string reason))
                throw new InvalidOperationException("Could not resolve Lara's skinned right hand: " + reason);

            var digitProfiles = new HandGripDigitProfile[Digits.Length];
            string[] namePrefixes = { "rThumb", "rIndex", "rMid", "rRing", "rPinky" };
            Vector3[] rootPositions = new Vector3[Digits.Length];
            Transform[][] digitBones = new Transform[Digits.Length][];
            for (int digitIndex = 0; digitIndex < Digits.Length; digitIndex++)
            {
                var bones = new Transform[3];
                for (int jointIndex = 0; jointIndex < bones.Length; jointIndex++)
                {
                    string boneName = namePrefixes[digitIndex] + (jointIndex + 1);
                    Transform[] matches = FindNamed(animator.transform, boneName);
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Expected exactly one actual Lara hand bone named '"
                            + boneName + "'. Found " + matches.Length + ".");
                    bones[jointIndex] = matches[0];
                    if (jointIndex > 0 && !bones[jointIndex].IsChildOf(bones[jointIndex - 1]))
                        throw new InvalidOperationException("Discovered hand joint '" + boneName
                            + "' is not below its preceding joint in the imported hierarchy.");
                    if (!bones[jointIndex].IsChildOf(hand))
                        throw new InvalidOperationException("Discovered hand joint '" + boneName
                            + "' is outside the resolved rHand subtree.");
                }
                digitBones[digitIndex] = bones;
                rootPositions[digitIndex] = bones[0].position;
            }

            Vector3 acrossPalm = digitBones[(int)HandGripDigit.Index][0].position
                - digitBones[(int)HandGripDigit.Little][0].position;
            if (acrossPalm.sqrMagnitude < 1e-8f)
                throw new InvalidOperationException("The index-to-little-finger bases do not define a stable palm axis.");
            acrossPalm.Normalize();
            Vector3 palmCenter = (rootPositions[(int)HandGripDigit.Index]
                + rootPositions[(int)HandGripDigit.Little] + hand.position) / 3f;

            for (int digitIndex = 0; digitIndex < Digits.Length; digitIndex++)
            {
                Transform[] bones = digitBones[digitIndex];
                string[] paths = new string[bones.Length];
                Vector3[] localPositions = new Vector3[bones.Length];
                Quaternion[] open = new Quaternion[bones.Length];
                Quaternion[] positive = new Quaternion[bones.Length];
                Quaternion[] negative = new Quaternion[bones.Length];
                Vector3[] flexAxes = new Vector3[bones.Length];
                float[] flexDegrees = { 55f, 55f, 35f };
                for (int jointIndex = 0; jointIndex < bones.Length; jointIndex++)
                {
                    Transform bone = bones[jointIndex];
                    paths[jointIndex] = HierarchyPath(animator.transform, bone);
                    localPositions[jointIndex] = bone.localPosition;
                    open[jointIndex] = bone.localRotation;
                    flexAxes[jointIndex] = bone.InverseTransformDirection(acrossPalm).normalized;
                    positive[jointIndex] = open[jointIndex]
                        * Quaternion.AngleAxis(flexDegrees[jointIndex], flexAxes[jointIndex]);
                    negative[jointIndex] = open[jointIndex]
                        * Quaternion.AngleAxis(-flexDegrees[jointIndex], flexAxes[jointIndex]);
                }

                Vector3 terminalOffset = EstimateTerminalOffset(bones);
                float positiveDistance = EstimateTipDistance(hand, bones, localPositions,
                    positive, terminalOffset, palmCenter);
                float negativeDistance = EstimateTipDistance(hand, bones, localPositions,
                    negative, terminalOffset, palmCenter);
                Quaternion[] closed = positiveDistance <= negativeDistance ? positive : negative;

                var probes = new List<HandGripProbeDefinition>(4);
                for (int jointIndex = 0; jointIndex < bones.Length - 1; jointIndex++)
                {
                    Vector3 segment = bones[jointIndex + 1].localPosition;
                    probes.Add(new HandGripProbeDefinition(jointIndex, segment * 0.55f,
                        EstimateProbeRadius(bones[jointIndex], bones[jointIndex + 1], 0.16f)));
                }
                float terminalLength = terminalOffset.magnitude;
                probes.Add(new HandGripProbeDefinition(bones.Length - 1, terminalOffset * 0.55f,
                    Mathf.Clamp(terminalLength * 0.18f, 0.005f, 0.010f)));
                probes.Add(new HandGripProbeDefinition(bones.Length - 1, terminalOffset,
                    Mathf.Clamp(terminalLength * 0.24f, 0.007f, 0.012f)));

                digitProfiles[digitIndex] = new HandGripDigitProfile();
                digitProfiles[digitIndex].ConfigureForEditor(Digits[digitIndex], paths,
                    localPositions, open, closed, probes.ToArray());
            }

            Vector3 gripCenterWorld = palmCenter;
            Vector3 centerLocal = hand.InverseTransformPoint(gripCenterWorld);
            profile.ConfigureForEditor(HierarchyPath(animator.transform, hand), digitProfiles,
                centerLocal, Quaternion.identity, clearance: 0.003f, nearDistance: 0.006f,
                blendIn: 0.25f, blendOut: 0.25f, iterations: 7);
        }

        private static float EstimateProbeRadius(Transform from, Transform to, float fraction)
        {
            float length = Vector3.Distance(from.position, to.position);
            return Mathf.Clamp(length * fraction, 0.004f, 0.010f);
        }

        private static Vector3 EstimateTerminalOffset(Transform[] bones)
        {
            Transform terminal = bones[bones.Length - 1];
            Transform previous = bones[bones.Length - 2];
            Vector3 segmentWorld = terminal.position - previous.position;
            Vector3 local = terminal.InverseTransformDirection(segmentWorld);
            if (local.sqrMagnitude < 1e-8f) local = terminal.InverseTransformDirection(terminal.forward);
            return local.normalized * segmentWorld.magnitude * 0.8f;
        }

        private static float EstimateTipDistance(Transform hand, Transform[] bones,
            Vector3[] localPositions, Quaternion[] localRotations, Vector3 terminalOffset,
            Vector3 target)
        {
            Vector3 parentPosition = hand.position;
            Quaternion parentRotation = hand.rotation;
            for (int index = 0; index < bones.Length; index++)
            {
                parentPosition += parentRotation * localPositions[index];
                parentRotation *= localRotations[index];
            }
            return Vector3.Distance(parentPosition + parentRotation * terminalOffset, target);
        }

        private static Transform[] FindNamed(Transform root, string name)
        {
            var results = new List<Transform>();
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(candidate.name, name, StringComparison.Ordinal)) results.Add(candidate);
            return results.ToArray();
        }

        private static string HierarchyPath(Transform root, Transform target)
        {
            var segments = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Push(current.name);
                current = current.parent;
            }
            if (current != root) throw new InvalidOperationException("The transform is not below the selected Animator.");
            return string.Join("/", segments.ToArray());
        }

        private static Transform FindByPath(Transform root, string path)
        {
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                current = current.Find(segment);
                if (current == null) return null;
            }
            return current;
        }

        private static HandGripRigProfile LoadOrCreateProfile(string assetPath)
        {
            EnsureFolder("Assets/DazPose/Generated");
            EnsureFolder("Assets/DazPose/Generated/HandGrip");
            EnsureFolder("Assets/DazPose/Generated/HandGrip/Profiles");
            HandGripRigProfile profile = AssetDatabase.LoadAssetAtPath<HandGripRigProfile>(assetPath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<HandGripRigProfile>();
            profile.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(profile, assetPath);
            return profile;
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
