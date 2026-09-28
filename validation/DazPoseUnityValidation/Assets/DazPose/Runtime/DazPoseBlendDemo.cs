using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DazPoseBlendPlayer))]
    [AddComponentMenu("DAZ Pose/Validation Only/Runtime Blend Demo")]
    public sealed class DazPoseBlendDemo : MonoBehaviour
    {
        private const float ValidationBlendSeconds = 0.04f;
        private const float PositionToleranceMeters = 1e-5f;
        private const float RotationToleranceDegrees = 0.001f;
        private const float ScaleTolerance = 1e-5f;

        public DazPoseBlendPlayer player;
        public AnimationClip poseA;
        public AnimationClip poseB;
        public AnimationClip poseC;
        [Min(0f)] public float blendDurationSeconds = 0.7f;
        public DazPoseBlendEase blendEase = DazPoseBlendEase.SmoothStep;

        private LocalTransformState[] _startingPose;
        private Transform _outerPlacementRoot;
        private Vector3 _outerWorldPosition;
        private Quaternion _outerWorldRotation;
        private Vector3 _outerLossyScale;
        private bool _validationRunning;

        private void Start()
        {
            if (player == null) player = GetComponent<DazPoseBlendPlayer>();
            _outerPlacementRoot = transform.parent;
            CaptureOuterPlacement();
            _startingPose = CaptureLocalPose();

            if (poseA == null)
            {
                Debug.LogWarning("DAZ Pose Blend Demo needs Pose A and Pose B assigned in the Inspector. The demo is ready for assignments.", this);
                return;
            }

            player.BlendEase = blendEase;
            player.SetPose(poseA, 0f);
        }

        private void Update()
        {
            if (_validationRunning) return;
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) RequestPose(poseA);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) RequestPose(poseB);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RequestPose(poseC);
            if (Input.GetKeyDown(KeyCode.F5)) StartEndpointAndLeakValidation();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 330f, 250f), GUI.skin.box);
            GUILayout.Label("G8F Runtime Pose Blend Test");
            GUILayout.Label("Set Blend Duration and Ease in the Inspector during Play Mode.");
            GUILayout.Label("Current: " + DisplayName(player == null ? null : player.CurrentPose));
            GUILayout.Label("Target: " + DisplayName(player == null ? null : player.TargetPose)
                + " | progress " + (player == null ? 0f : player.BlendProgress).ToString("P0"));
            GUILayout.Label("Pending: " + DisplayName(player == null ? null : player.PendingPose));

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !_validationRunning;
            if (poseA != null && GUILayout.Button("1 - Pose A: " + poseA.name)) RequestPose(poseA);
            if (poseB != null && GUILayout.Button("2 - Pose B: " + poseB.name)) RequestPose(poseB);
            if (poseC != null && GUILayout.Button("3 - Pose C: " + poseC.name)) RequestPose(poseC);
            if (poseA != null && poseB != null && GUILayout.Button("F5 - Check endpoints and repeat A/B 20 times"))
                StartEndpointAndLeakValidation();
            GUI.enabled = previousEnabled;
            GUILayout.EndArea();
        }

        private void RequestPose(AnimationClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("Assign a clip to that pose slot on DazPoseBlendDemo before selecting it.", this);
                return;
            }
            if (player == null || !player.isActiveAndEnabled)
            {
                Debug.LogError("DazPoseBlendDemo needs an enabled DazPoseBlendPlayer on this animation root.", this);
                return;
            }

            player.BlendEase = blendEase;
            player.SetPose(clip, blendDurationSeconds);
        }

        private void StartEndpointAndLeakValidation()
        {
            if (poseA == null || poseB == null || player == null)
            {
                Debug.LogError("Assign Pose A, Pose B, and a DazPoseBlendPlayer before running the blend validation.", this);
                return;
            }
            if (_validationRunning || player.IsBlending)
            {
                Debug.LogWarning("Wait until the current pose transition finishes before starting the validation.", this);
                return;
            }

            StartCoroutine(ValidateEndpointsAndRepeatedTransitions());
        }

        private IEnumerator ValidateEndpointsAndRepeatedTransitions()
        {
            _validationRunning = true;
            var failures = new List<string>();
            var referenceA = SampleReferencePose(poseA);
            var referenceB = SampleReferencePose(poseB);
            RestorePose(_startingPose);

            player.BlendEase = blendEase;
            player.SetPose(poseA, 0f);
            yield return new WaitForEndOfFrame();
            CheckEndpoint("Pose A initial endpoint", referenceA, failures);
            CheckOuterPlacement("Pose A initial endpoint", failures);

            player.SetPose(poseB, ValidationBlendSeconds);
            var reached = false;
            yield return WaitForTransition(ok => reached = ok);
            if (!reached) failures.Add("A-to-B transition did not finish before the validation timeout.");
            else
            {
                CheckEndpoint("Pose B endpoint", referenceB, failures);
                CheckOuterPlacement("Pose B endpoint", failures);
            }

            if (reached)
            {
                player.SetPose(poseA, ValidationBlendSeconds);
                reached = false;
                yield return WaitForTransition(ok => reached = ok);
                if (!reached) failures.Add("B-to-A transition did not finish before the validation timeout.");
                else
                {
                    CheckEndpoint("Pose A return endpoint", referenceA, failures);
                    CheckOuterPlacement("Pose A return endpoint", failures);
                }
            }

            var steadyPlayableCount = player.GraphPlayableCount;
            if (reached)
            {
                for (var index = 0; index < 20; index++)
                {
                    var target = index % 2 == 0 ? poseB : poseA;
                    player.SetPose(target, ValidationBlendSeconds);
                    reached = false;
                    yield return WaitForTransition(ok => reached = ok);
                    if (!reached)
                    {
                        failures.Add("Repeated transition " + (index + 1) + " did not finish before the validation timeout.");
                        break;
                    }
                    if (player.GraphPlayableCount != steadyPlayableCount)
                    {
                        failures.Add("Playable count changed after repeated transition " + (index + 1) + " (expected "
                            + steadyPlayableCount + ", found " + player.GraphPlayableCount + ").");
                        break;
                    }
                    CheckOuterPlacement("Repeated transition " + (index + 1), failures);
                }
            }

            if (reached)
            {
                CheckEndpoint("Pose A endpoint after 20 transitions", referenceA, failures);
                CheckOuterPlacement("Pose A endpoint after 20 transitions", failures);
            }
            if (player.GraphPlayableCount != steadyPlayableCount)
                failures.Add("Final graph playable count changed from " + steadyPlayableCount + " to " + player.GraphPlayableCount + ".");

            _validationRunning = false;
            if (failures.Count == 0)
                Debug.Log("PASS DAZ Pose runtime blend validation: A/B endpoints match direct clip samples; 20 repeated transitions completed; steady playable count stayed at "
                    + steadyPlayableCount + "; Lara's outer placement stayed unchanged. Ease tested: " + blendEase + ".", this);
            else
                Debug.LogError("FAIL DAZ Pose runtime blend validation:\n- " + string.Join("\n- ", failures), this);
        }

        private IEnumerator WaitForTransition(Action<bool> result)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            yield return new WaitForEndOfFrame();
            while (player.IsBlending && Time.realtimeSinceStartup < deadline)
                yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            result(!player.IsBlending);
        }

        private LocalTransformState[] SampleReferencePose(AnimationClip clip)
        {
            RestorePose(_startingPose);
            clip.SampleAnimation(gameObject, 0.5f);
            var state = CaptureLocalPose();
            RestorePose(_startingPose);
            return state;
        }

        private LocalTransformState[] CaptureLocalPose()
        {
            return transform.GetComponentsInChildren<Transform>(true).Select(item => new LocalTransformState
            {
                target = item,
                position = item.localPosition,
                rotation = item.localRotation,
                scale = item.localScale
            }).ToArray();
        }

        private static void RestorePose(IEnumerable<LocalTransformState> state)
        {
            foreach (var item in state)
            {
                if (item.target == null) continue;
                item.target.localPosition = item.position;
                item.target.localRotation = item.rotation;
                item.target.localScale = item.scale;
            }
        }

        private static void CheckEndpoint(string label, IEnumerable<LocalTransformState> expected, List<string> failures)
        {
            var maxPosition = 0f;
            var maxRotation = 0f;
            var maxScale = 0f;
            var worstTransform = string.Empty;
            foreach (var item in expected)
            {
                if (item.target == null)
                {
                    failures.Add(label + " encountered a destroyed transform reference.");
                    return;
                }
                var positionError = Vector3.Distance(item.position, item.target.localPosition);
                var rotationError = Quaternion.Angle(item.rotation, item.target.localRotation);
                var scaleError = Vector3.Distance(item.scale, item.target.localScale);
                if (positionError > maxPosition || rotationError > maxRotation || scaleError > maxScale)
                    worstTransform = item.target.name;
                maxPosition = Mathf.Max(maxPosition, positionError);
                maxRotation = Mathf.Max(maxRotation, rotationError);
                maxScale = Mathf.Max(maxScale, scaleError);
            }

            if (maxPosition > PositionToleranceMeters || maxRotation > RotationToleranceDegrees || maxScale > ScaleTolerance)
                failures.Add(label + " differs from direct clip sampling (max position " + maxPosition.ToString("G6")
                    + " m, rotation " + maxRotation.ToString("G6") + " deg, scale " + maxScale.ToString("G6")
                    + "; worst transform " + worstTransform + "). This can reveal a sparse-binding interaction.");
        }

        private void CaptureOuterPlacement()
        {
            if (_outerPlacementRoot == null) return;
            _outerWorldPosition = _outerPlacementRoot.position;
            _outerWorldRotation = _outerPlacementRoot.rotation;
            _outerLossyScale = _outerPlacementRoot.lossyScale;
        }

        private void CheckOuterPlacement(string label, List<string> failures)
        {
            if (_outerPlacementRoot == null) return;
            var positionError = Vector3.Distance(_outerWorldPosition, _outerPlacementRoot.position);
            var rotationError = Quaternion.Angle(_outerWorldRotation, _outerPlacementRoot.rotation);
            var scaleError = Vector3.Distance(_outerLossyScale, _outerPlacementRoot.lossyScale);
            if (positionError > PositionToleranceMeters || rotationError > RotationToleranceDegrees || scaleError > ScaleTolerance)
                failures.Add(label + " changed Lara's outer placement (position " + positionError.ToString("G6")
                    + " m, rotation " + rotationError.ToString("G6") + " deg, scale " + scaleError.ToString("G6") + ").");
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _validationRunning = false;
        }

        private static string DisplayName(AnimationClip clip) => clip == null ? "<none>" : clip.name;

        private sealed class LocalTransformState
        {
            public Transform target;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
    }
}
