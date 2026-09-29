using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        private const float BlendShapeWeightTolerance = 1e-3f;
        private const float BlendShapeBlendTolerance = 0.05f;

        public DazPoseBlendPlayer player;
        public AnimationClip poseA;
        public AnimationClip poseB;
        public AnimationClip poseC;
        [Min(0f)] public float blendDurationSeconds = 0.7f;
        public DazPoseBlendEase blendEase = DazPoseBlendEase.SmoothStep;
        [Range(0f, 0.30f)] public float windupFraction;
        [Range(0f, 0.30f)] public float overshootFraction;

        private LocalTransformState[] _startingPose;
        private LocalBlendShapeState[] _startingBlendShapes;
        private Transform _outerPlacementRoot;
        private Vector3 _outerWorldPosition;
        private Quaternion _outerWorldRotation;
        private Vector3 _outerLossyScale;
        private bool _validationRunning;
        private string _blendDurationInput;
        private bool _blendDurationInputValid = true;

        private const string BlendDurationControlName = "DazPoseBlendDurationSeconds";
        private const string WindupControlName = "DazPoseWindupFraction";
        private const string OvershootControlName = "DazPoseOvershootFraction";

        private void Start()
        {
            if (player == null) player = GetComponent<DazPoseBlendPlayer>();
            _outerPlacementRoot = transform.parent;
            CaptureOuterPlacement();
            _startingPose = CaptureLocalPose();
            _startingBlendShapes = CaptureLocalBlendShapes();
            _blendDurationInput = blendDurationSeconds.ToString("0.##", CultureInfo.InvariantCulture);

            if (poseA == null)
            {
                Debug.LogWarning("DAZ Pose Blend Demo needs Pose A and Pose B assigned in the Inspector. The demo is ready for assignments.", this);
                return;
            }

            player.SetPose(poseA, new DazPoseTransitionOptions(0f, blendEase, 0f, 0f));
        }

        private void Update()
        {
            if (_validationRunning) return;
            var focusedControl = GUI.GetNameOfFocusedControl();
            if (IsTransitionControlFocused(focusedControl)) return;
            if (_blendDurationInputValid && (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))) RequestPose(poseA);
            if (_blendDurationInputValid && (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))) RequestPose(poseB);
            if (_blendDurationInputValid && (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))) RequestPose(poseC);
            if (Input.GetKeyDown(KeyCode.F5)) StartEndpointAndLeakValidation();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 430f, 390f), GUI.skin.box);
            GUILayout.Label("G8F Runtime Pose Blend Test");

            var previousEnabled = GUI.enabled;
            var canEditTransition = previousEnabled && !_validationRunning && (player == null || !player.IsBlending);
            GUI.enabled = canEditTransition;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blend time (seconds)", GUILayout.Width(140f));
            GUI.SetNextControlName(BlendDurationControlName);
            _blendDurationInput = GUILayout.TextField(_blendDurationInput ?? string.Empty, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
            _blendDurationInputValid = float.TryParse(_blendDurationInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDuration)
                && !float.IsNaN(parsedDuration) && !float.IsInfinity(parsedDuration) && parsedDuration >= 0f;
            if (_blendDurationInputValid) blendDurationSeconds = parsedDuration;
            GUILayout.Label(_blendDurationInputValid
                ? "Next pose change: " + blendDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " sec | Ease: " + blendEase
                : "Enter a valid number of seconds (0 or greater).", GUILayout.Width(400f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Windup", GUILayout.Width(140f));
            GUI.SetNextControlName(WindupControlName);
            windupFraction = RoundSliderFraction(GUILayout.HorizontalSlider(windupFraction, 0f, 0.30f));
            GUILayout.Label(windupFraction.ToString("P0", CultureInfo.InvariantCulture), GUILayout.Width(40f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Overshoot", GUILayout.Width(140f));
            GUI.SetNextControlName(OvershootControlName);
            overshootFraction = RoundSliderFraction(GUILayout.HorizontalSlider(overshootFraction, 0f, 0.30f));
            GUILayout.Label(overshootFraction.ToString("P0", CultureInfo.InvariantCulture), GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;

            GUILayout.Label("Next command: " + FormatTransition(blendDurationSeconds, blendEase,
                RoundSliderFraction(windupFraction), RoundSliderFraction(overshootFraction)), GUILayout.Width(410f));
            GUILayout.Label("Current: " + DisplayName(player == null ? null : player.CurrentPose));
            GUILayout.Label("Target: " + DisplayName(player == null ? null : player.TargetPose)
                + " | progress " + (player == null ? 0f : player.BlendProgress).ToString("P0"));
            GUILayout.Label(player != null && player.IsBlending
                ? "Active: " + FormatTransition(player.ActiveTransitionOptions)
                : "Active: <idle>", GUILayout.Width(410f));
            GUILayout.Label(player != null && player.HasPendingPose
                ? "Pending: " + DisplayName(player.PendingPose) + " | " + FormatTransition(player.PendingTransitionOptions)
                : "Pending: <none>", GUILayout.Width(410f));

            GUI.enabled = previousEnabled && !_validationRunning && _blendDurationInputValid;
            if (poseA != null && GUILayout.Button("1 - Pose A: " + poseA.name)) RequestPose(poseA);
            if (poseB != null && GUILayout.Button("2 - Pose B: " + poseB.name)) RequestPose(poseB);
            if (poseC != null && GUILayout.Button("3 - Pose C: " + poseC.name)) RequestPose(poseC);
            GUI.enabled = previousEnabled && !_validationRunning;
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

            if (!_blendDurationInputValid)
            {
                Debug.LogWarning("Enter a valid blend duration before requesting a pose.", this);
                return;
            }

            var options = new DazPoseTransitionOptions(blendDurationSeconds, blendEase,
                RoundSliderFraction(windupFraction), RoundSliderFraction(overshootFraction));
            player.SetPose(clip, options);
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
            ValidateTrajectoryMath(failures);
            var referenceA = SampleReferencePose(poseA);
            var referenceB = SampleReferencePose(poseB);
            var morphReferenceA = SampleReferenceBlendShapes(poseA);
            var morphReferenceB = SampleReferenceBlendShapes(poseB);
            var checksMorphBlend = MorphStatesDiffer(morphReferenceA, morphReferenceB);
            RestoreStartingPose();

            var validationOptions = new DazPoseTransitionOptions(ValidationBlendSeconds, blendEase, 0.08f, 0.10f);
            player.SetPose(poseA, new DazPoseTransitionOptions(0f, blendEase, 0f, 0f));
            yield return new WaitForEndOfFrame();
            CheckEndpoint("Pose A initial endpoint", referenceA, failures);
            CheckBlendShapeEndpoint("Pose A initial endpoint", morphReferenceA, failures);
            CheckOuterPlacement("Pose A initial endpoint", failures);

            var instantShapedOptions = new DazPoseTransitionOptions(0f, blendEase, 0.10f, 0.10f);
            player.SetPose(poseB, instantShapedOptions);
            yield return new WaitForEndOfFrame();
            CheckEndpoint("Zero-duration shaped Pose B endpoint", referenceB, failures);
            CheckBlendShapeEndpoint("Zero-duration shaped Pose B endpoint", morphReferenceB, failures);
            if (player.IsBlending || player.TargetPose != null)
                failures.Add("A zero-duration request entered a transition instead of snapping to its endpoint.");
            player.SetPose(poseA, instantShapedOptions);
            yield return new WaitForEndOfFrame();
            CheckEndpoint("Zero-duration shaped Pose A endpoint", referenceA, failures);
            CheckBlendShapeEndpoint("Zero-duration shaped Pose A endpoint", morphReferenceA, failures);

            player.SetPose(poseB, validationOptions);
            var reached = false;
            yield return WaitForTransition(ok => reached = ok, morphReferenceA, morphReferenceB, checksMorphBlend, failures);
            if (!reached) failures.Add("A-to-B transition did not finish before the validation timeout.");
            else
            {
                CheckEndpoint("Pose B endpoint", referenceB, failures);
                CheckBlendShapeEndpoint("Pose B endpoint", morphReferenceB, failures);
                CheckOuterPlacement("Pose B endpoint", failures);
            }

            if (reached)
            {
                player.SetPose(poseA, validationOptions);
                reached = false;
                yield return WaitForTransition(ok => reached = ok, morphReferenceB, morphReferenceA, checksMorphBlend, failures);
                if (!reached) failures.Add("B-to-A transition did not finish before the validation timeout.");
                else
                {
                    CheckEndpoint("Pose A return endpoint", referenceA, failures);
                    CheckBlendShapeEndpoint("Pose A return endpoint", morphReferenceA, failures);
                    CheckOuterPlacement("Pose A return endpoint", failures);
                }
            }

            var steadyPlayableCount = player.GraphPlayableCount;
            if (reached)
            {
                for (var index = 0; index < 20; index++)
                {
                    var target = index % 2 == 0 ? poseB : poseA;
                    player.SetPose(target, validationOptions);
                    reached = false;
                    var morphFrom = index % 2 == 0 ? morphReferenceA : morphReferenceB;
                    var morphTo = index % 2 == 0 ? morphReferenceB : morphReferenceA;
                    yield return WaitForTransition(ok => reached = ok, morphFrom, morphTo, checksMorphBlend, failures);
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
                CheckBlendShapeEndpoint("Pose A endpoint after 20 transitions", morphReferenceA, failures);
                CheckOuterPlacement("Pose A endpoint after 20 transitions", failures);
            }
            if (player.GraphPlayableCount != steadyPlayableCount)
                failures.Add("Final graph playable count changed from " + steadyPlayableCount + " to " + player.GraphPlayableCount + ".");
            if (!player.MixerWeightsStayedInRange)
                failures.Add("An AnimationMixerPlayable input weight left the documented 0-to-1 range during repeated transitions.");

            if (reached)
            {
                var activeOptions = new DazPoseTransitionOptions(0.12f, DazPoseBlendEase.Linear, 0.04f, 0.05f);
                var pendingOptions = new DazPoseTransitionOptions(0.18f, DazPoseBlendEase.SmoothStep, 0.12f, 0.08f);
                var latestPendingOptions = new DazPoseTransitionOptions(0.10f, DazPoseBlendEase.Linear, 0.03f, 0.14f);
                player.SetPose(poseB, activeOptions);
                player.SetPose(poseA, pendingOptions);
                player.SetPose(poseA, latestPendingOptions);

                if (player.ActiveTransitionOptions != activeOptions)
                    failures.Add("Submitting or replacing a pending command changed the active transition options.");
                if (!player.HasPendingPose || player.PendingPose != poseA || player.PendingTransitionOptions != latestPendingOptions)
                    failures.Add("The latest pending pose request did not replace the pending command's captured options.");

                reached = false;
                var queuedBecameActive = false;
                yield return WaitForActiveCommand(poseA, latestPendingOptions, ok => queuedBecameActive = ok);
                if (!queuedBecameActive)
                    failures.Add("The pending pose did not start with its captured options after the active transition finished.");
                else
                {
                    if (player.HasPendingPose || player.ActiveTransitionOptions != latestPendingOptions)
                        failures.Add("The queued command's options changed when it became active.");
                    reached = false;
                 yield return WaitForTransition(ok => reached = ok, morphReferenceB, morphReferenceA, checksMorphBlend, failures);
                    if (!reached) failures.Add("The queued transition did not finish before the validation timeout.");
                    else
                    {
                        CheckEndpoint("Latest queued Pose A endpoint", referenceA, failures);
                        CheckBlendShapeEndpoint("Latest queued Pose A endpoint", morphReferenceA, failures);
                        CheckOuterPlacement("Latest queued Pose A endpoint", failures);
                    }
                }
            }

            if (reached)
            {
                for (var cycle = 0; cycle < 2; cycle++)
                {
                    player.enabled = false;
                    if (player.GraphPlayableCount != 0)
                        failures.Add("Playable graph remained alive after disabling DazPoseBlendPlayer (cycle " + (cycle + 1) + ").");

                    player.enabled = true;
                    yield return new WaitForEndOfFrame();
                    if (player.GraphPlayableCount < 2)
                        failures.Add("Playable graph was not recreated after reenabling DazPoseBlendPlayer (cycle " + (cycle + 1) + ").");
                    player.SetPose(poseA, new DazPoseTransitionOptions(0f, blendEase, 0f, 0f));
                    yield return new WaitForEndOfFrame();
                    CheckEndpoint("Pose A after graph lifecycle cycle " + (cycle + 1), referenceA, failures);
                    CheckBlendShapeEndpoint("Pose A after graph lifecycle cycle " + (cycle + 1), morphReferenceA, failures);
                }
            }

            if (!player.MixerWeightsStayedInRange)
                failures.Add("An AnimationMixerPlayable input weight left the documented 0-to-1 range.");

            _validationRunning = false;
            if (failures.Count == 0)
                Debug.Log("PASS DAZ Pose runtime blend validation: shaped A/B Transform and blendshape endpoints match direct clip samples"
                    + (checksMorphBlend ? ", and blendshape weights interpolated through Playables" : string.Empty)
                    + "; trajectory math and immutable pending options passed; "
                    + "20 repeated transitions completed; graph enable/disable cycles were clean; mixer weights stayed in range; Lara's outer placement stayed unchanged. Ease tested: "
                    + blendEase + ".", this);
            else
                Debug.LogError("FAIL DAZ Pose runtime blend validation:\n- " + string.Join("\n- ", failures), this);
        }

        private static void ValidateTrajectoryMath(List<string> failures)
        {
            var shaped = new DazPoseTransitionOptions(1f, DazPoseBlendEase.SmoothStep, 0.10f, 0.12f);
            if (!Approximately(DazPoseTransitionTrajectory.Evaluate(0f, shaped), 0f))
                failures.Add("Shaped transition trajectory did not start at zero.");
            if (!(DazPoseTransitionTrajectory.Evaluate(0.075f, shaped) < 0f))
                failures.Add("Windup trajectory never moved behind the source pose.");
            if (!Approximately(DazPoseTransitionTrajectory.Evaluate(0.15f, shaped), -0.10f))
                failures.Add("Windup phase did not reach the requested negative fraction.");
            if (!Approximately(DazPoseTransitionTrajectory.Evaluate(0.80f, shaped), 1.12f))
                failures.Add("Main drive did not reach the requested overshoot fraction.");
            if (!(DazPoseTransitionTrajectory.Evaluate(0.90f, shaped) > 1f))
                failures.Add("Settle phase did not remain beyond the target before returning to it.");
            if (!Approximately(DazPoseTransitionTrajectory.Evaluate(1f, shaped), 1f))
                failures.Add("Shaped transition trajectory did not finish exactly at the target.");

            ExpectInvalidOptions(failures, -0.01f, DazPoseBlendEase.Linear, 0f, 0f, "negative duration");
            ExpectInvalidOptions(failures, float.NaN, DazPoseBlendEase.Linear, 0f, 0f, "non-finite duration");
            ExpectInvalidOptions(failures, 1f, DazPoseBlendEase.Linear, 0f, float.PositiveInfinity, "non-finite overshoot");
            ExpectInvalidOptions(failures, 1f, DazPoseBlendEase.Linear, 0f, 1.01f, "overshoot over 100 percent");

            foreach (var ease in new[] { DazPoseBlendEase.Linear, DazPoseBlendEase.SmoothStep })
            {
                var unshaped = new DazPoseTransitionOptions(1f, ease, 0f, 0f);
                foreach (var time in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    var expected = ease == DazPoseBlendEase.Linear
                        ? time
                        : time * time * (3f - 2f * time);
                    if (!Approximately(DazPoseTransitionTrajectory.Evaluate(time, unshaped), expected))
                    {
                        failures.Add("Zero-shaping trajectory changed the existing " + ease + " blend at t=" + time + ".");
                        break;
                    }
                }
            }
        }

        private static void ExpectInvalidOptions(List<string> failures, float duration, DazPoseBlendEase ease,
            float windup, float overshoot, string label)
        {
            try
            {
                new DazPoseTransitionOptions(duration, ease, windup, overshoot);
                failures.Add("Transition options accepted invalid " + label + ".");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private IEnumerator WaitForActiveCommand(AnimationClip pose, DazPoseTransitionOptions options, Action<bool> result)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            yield return new WaitForEndOfFrame();
            while (Time.realtimeSinceStartup < deadline)
            {
                if (player.IsBlending && player.TargetPose == pose && player.ActiveTransitionOptions == options)
                {
                    result(true);
                    yield break;
                }
                yield return new WaitForEndOfFrame();
            }
            result(false);
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) <= 1e-5f;

        private IEnumerator WaitForTransition(Action<bool> result, LocalBlendShapeState[] morphFrom = null,
            LocalBlendShapeState[] morphTo = null, bool validateMorphBlend = false, List<string> failures = null)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            var morphFailureReported = false;
            yield return new WaitForEndOfFrame();
            while (player.IsBlending && Time.realtimeSinceStartup < deadline)
            {
                if (validateMorphBlend && !morphFailureReported && morphFrom != null && morphTo != null && failures != null)
                {
                    var maxError = BlendShapeInterpolationError(morphFrom, morphTo, player.TrajectoryProgress);
                    if (maxError > BlendShapeBlendTolerance)
                    {
                        failures.Add("Playables blendshape interpolation exceeded " + BlendShapeBlendTolerance.ToString("G4")
                            + " weight units at trajectory progress " + player.TrajectoryProgress.ToString("G4") + " (error " + maxError.ToString("G6") + ").");
                        morphFailureReported = true;
                    }
                }
                yield return new WaitForEndOfFrame();
            }
            yield return new WaitForEndOfFrame();
            result(!player.IsBlending);
        }

        private LocalTransformState[] SampleReferencePose(AnimationClip clip)
        {
            RestoreStartingPose();
            clip.SampleAnimation(gameObject, 0.5f);
            var state = CaptureLocalPose();
            RestoreStartingPose();
            return state;
        }

        private LocalBlendShapeState[] SampleReferenceBlendShapes(AnimationClip clip)
        {
            RestoreStartingPose();
            clip.SampleAnimation(gameObject, 0.5f);
            var state = CaptureLocalBlendShapes();
            RestoreStartingPose();
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

        private LocalBlendShapeState[] CaptureLocalBlendShapes()
        {
            return transform.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer != null && renderer.sharedMesh != null)
                .SelectMany(renderer => Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                    .Select(index => new LocalBlendShapeState
                    {
                        renderer = renderer,
                        index = index,
                        name = renderer.sharedMesh.GetBlendShapeName(index),
                        weight = renderer.GetBlendShapeWeight(index)
                    })).ToArray();
        }

        private void RestoreStartingPose()
        {
            RestorePose(_startingPose);
            RestoreBlendShapes(_startingBlendShapes);
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

        private static void RestoreBlendShapes(IEnumerable<LocalBlendShapeState> state)
        {
            foreach (var item in state ?? Enumerable.Empty<LocalBlendShapeState>())
            {
                if (item.renderer == null || item.renderer.sharedMesh == null
                    || item.index < 0 || item.index >= item.renderer.sharedMesh.blendShapeCount) continue;
                item.renderer.SetBlendShapeWeight(item.index, item.weight);
            }
        }

        private static void CheckBlendShapeEndpoint(string label, IEnumerable<LocalBlendShapeState> expected, List<string> failures)
        {
            var maxError = 0f;
            var worstShape = string.Empty;
            foreach (var item in expected ?? Enumerable.Empty<LocalBlendShapeState>())
            {
                if (item.renderer == null || item.renderer.sharedMesh == null || item.index >= item.renderer.sharedMesh.blendShapeCount)
                {
                    failures.Add(label + " encountered a missing SkinnedMeshRenderer or imported blendshape.");
                    return;
                }
                var error = Mathf.Abs(item.weight - item.renderer.GetBlendShapeWeight(item.index));
                if (error > maxError) worstShape = item.name;
                maxError = Mathf.Max(maxError, error);
            }
            if (maxError > BlendShapeWeightTolerance)
                failures.Add(label + " differs from direct clip sampling (blendshape weight error " + maxError.ToString("G6") + "; worst shape " + worstShape + ").");
        }

        private static bool MorphStatesDiffer(LocalBlendShapeState[] left, LocalBlendShapeState[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return true;
            for (var index = 0; index < left.Length; index++)
                if (left[index].renderer != right[index].renderer || left[index].index != right[index].index
                    || Mathf.Abs(left[index].weight - right[index].weight) > BlendShapeWeightTolerance) return true;
            return false;
        }

        private static float BlendShapeInterpolationError(LocalBlendShapeState[] from, LocalBlendShapeState[] to, float progress)
        {
            if (from == null || to == null || from.Length != to.Length) return float.PositiveInfinity;
            var maxError = 0f;
            for (var index = 0; index < from.Length; index++)
            {
                var first = from[index];
                var last = to[index];
                if (first.renderer == null || last.renderer == null || first.renderer != last.renderer || first.index != last.index
                    || first.renderer.sharedMesh == null || first.index >= first.renderer.sharedMesh.blendShapeCount) return float.PositiveInfinity;
                // The Transform extrapolation job can move outside the endpoints for windup/overshoot,
                // while the AnimationMixerPlayable intentionally keeps blendshape weights bounded.
                var expected = Mathf.Lerp(first.weight, last.weight, Mathf.Clamp01(progress));
                maxError = Mathf.Max(maxError, Mathf.Abs(expected - first.renderer.GetBlendShapeWeight(first.index)));
            }
            return maxError;
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

        private static bool IsTransitionControlFocused(string controlName)
        {
            return string.Equals(controlName, BlendDurationControlName, StringComparison.Ordinal)
                || string.Equals(controlName, WindupControlName, StringComparison.Ordinal)
                || string.Equals(controlName, OvershootControlName, StringComparison.Ordinal);
        }

        private static float RoundSliderFraction(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Clamp(Mathf.Round(value * 100f) / 100f, 0f, 0.30f);
        }

        private static string FormatTransition(DazPoseTransitionOptions options)
        {
            return FormatTransition(options.DurationSeconds, options.Ease, options.WindupFraction, options.OvershootFraction);
        }

        private static string FormatTransition(float durationSeconds, DazPoseBlendEase ease,
            float windup, float overshoot)
        {
            return durationSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " sec | " + ease
                + " | windup " + windup.ToString("P0", CultureInfo.InvariantCulture)
                + " | overshoot " + overshoot.ToString("P0", CultureInfo.InvariantCulture);
        }

        private static string DisplayName(AnimationClip clip) => clip == null ? "<none>" : clip.name;

        private sealed class LocalTransformState
        {
            public Transform target;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        private sealed class LocalBlendShapeState
        {
            public SkinnedMeshRenderer renderer;
            public int index;
            public string name;
            public float weight;
        }
    }
}
