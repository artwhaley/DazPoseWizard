using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Development/Pose Acceptance Checks")]
    public sealed class PerformerPoseAcceptanceHarness : MonoBehaviour
    {
        private const float PositionTolerance = 1e-4f;
        private const float RotationToleranceDegrees = 0.01f;
        private const float ScaleTolerance = 1e-4f;
        private const float BlendShapeTolerance = 0.05f;

        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformerPose poseA = null;
        [SerializeField] private PerformerPose poseB = null;
        [SerializeField] private PerformerPose poseC = null;
        [SerializeField] private PerformerExpression expressionA = null;
        [SerializeField] private PerformerExpression expressionB = null;
        [SerializeField] private PerformerExpression expressionC = null;
        [SerializeField] private PerformerGesture gestureAcceptanceWave;
        [SerializeField] private PerformerAction actionAcceptanceJumpForJoy;
        [SerializeField] private PerformerAction actionAcceptanceDisplacedTest;
        [SerializeField] private Transform gazeTarget;

        private bool _running;
        private bool _awaitableChecksComplete;
        private bool _expressionAwaitableChecksComplete;
        private bool _originalExpressionTested;
        private GazeHarnessSettings _originalGazeSettings;
        private AttentionLifeHarnessSettings _originalAttentionLifeSettings;
        private BreathingHarnessSettings _originalBreathingSettings;
        private BlinkHarnessSettings _originalBlinkSettings;
        private PerformerExpression _originalDesiredExpression;
        private float _originalDesiredExpressionIntensity;
        private Vector3? _gazeTargetPositionBeforeTests;
        public string Status { get; private set; } = "Press F5 to run acceptance checks.";

        private void Reset()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
        }

        public void Run()
        {
            if (_running) return;
            if (performer == null || poseA == null || poseB == null || poseC == null)
            {
                Debug.LogError("Assign SuccubusPerformer and all three PerformerPose assets before running acceptance checks.", this);
                return;
            }

            StartCoroutine(RunChecks());
        }

        private IEnumerator RunChecks()
        {
            _running = true;
            Status = "Running acceptance checks…";
            var failures = new List<string>();
            var animator = performer.GetComponent<Animator>();
            if (animator == null)
            {
                Finish(failures, "Animator is missing.");
                yield break;
            }

            var placementRoot = transform;
            while (placementRoot.parent != null) placementRoot = placementRoot.parent;
            var startPlacement = CapturePlacement(placementRoot);
            _gazeTargetPositionBeforeTests = gazeTarget == null ? (Vector3?)null : gazeTarget.position;
            var playableCount = performer.RuntimePlayableCount;
            var originalBreathing = CaptureBreathingSettings();
            var originalGaze = CaptureGazeSettings();
            var originalAttentionLife = CaptureAttentionLifeSettings();
            var originalBlink = CaptureBlinkSettings();
            _originalBreathingSettings = originalBreathing;
            _originalGazeSettings = originalGaze;
            _originalAttentionLifeSettings = originalAttentionLife;
            _originalBlinkSettings = originalBlink;
            _originalDesiredExpression = performer.DesiredExpression;
            _originalDesiredExpressionIntensity = performer.DesiredExpressionIntensity;
            performer.BreathingEnabled = false;
            performer.MorphBreathingStrength = 0f;
            performer.BoneBreathingStrength = 0f;
            performer.GazeEnabled = false;
            performer.AttentionLifeEnabled = false;
            performer.BlinkEnabled = false;
            performer.ClearGaze();
            performer.SetBreathPhaseForAcceptance(originalBreathing.Phase);

            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            var stateA = CapturePose(animator);
            performer.Pose(poseB, PoseTransition.Snap);
            yield return null;
            var stateB = CapturePose(animator);
            performer.Pose(poseC, PoseTransition.Snap);
            yield return null;
            var stateC = CapturePose(animator);
            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            CheckPose("Zero-duration snap to A", stateA, CapturePose(animator), failures);
            if (performer.IsTransitioning) failures.Add("A zero-duration request remained in transition.");
            ValidateTrajectoryMath(failures);

            var smooth = PoseTransition.Smooth(0.65f);
            var before = CapturePose(animator);
            performer.Pose(poseB, smooth);
            if (!performer.IsTransitioning) failures.Add("A nonzero A-to-B transition completed immediately.");
            if (performer.DesiredPose != poseB || performer.SettledPose != poseA)
                failures.Add("A-to-B did not publish DesiredPose and SettledPose correctly.");
            CheckPose("A-to-B initial continuity", before, CapturePose(animator), failures);
            yield return WaitForSettlement(6f, failures, "A-to-B");
            CheckPose("A-to-B endpoint", stateB, CapturePose(animator), failures);
            if (performer.SettledPose != poseB) failures.Add("B did not become the settled pose.");

            performer.Pose(poseB, PoseTransition.Smooth(1f));
            if (performer.IsTransitioning) failures.Add("A repeated request for the settled desired pose restarted a transition.");
            performer.Pose(poseA, smooth);
            yield return WaitForSettlement(6f, failures, "B-to-A");
            CheckPose("B-to-A endpoint", stateA, CapturePose(animator), failures);

            var shaped = new PoseTransition(0.55f, 0.10f, 0.12f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            performer.Pose(poseB, shaped);
            yield return WaitForSettlement(6f, failures, "windup/overshoot A-to-B");
            CheckPose("Shaped transition endpoint", stateB, CapturePose(animator), failures);

            yield return InterruptAtProgress(poseA, poseB, poseC, 0.25f, stateC, failures);
            yield return InterruptAtProgress(poseA, poseB, poseC, 0.70f, stateC, failures);
            yield return CheckRapidRetargets(poseA, poseB, poseC, stateC, failures);
            yield return CheckLifecycle(animator, poseC, stateC, playableCount, failures);

            _awaitableChecksComplete = false;
            RunAwaitableChecks(failures);
            var awaitableDeadline = Time.realtimeSinceStartup + 30f;
            while (!_awaitableChecksComplete && Time.realtimeSinceStartup < awaitableDeadline)
                yield return null;
            if (!_awaitableChecksComplete)
            {
                failures.Add("PoseAsync acceptance checks did not finish before the timeout.");
                performer.Pose(poseA, PoseTransition.Snap);
                yield return null;
            }

            yield return CheckBreathingAcceptance(animator, stateA, stateB, stateC,
                startPlacement, failures);

            _awaitableChecksComplete = false;
            RunBreathingAwaitableChecks(failures);
            awaitableDeadline = Time.realtimeSinceStartup + 30f;
            while (!_awaitableChecksComplete && Time.realtimeSinceStartup < awaitableDeadline)
                yield return null;
            if (!_awaitableChecksComplete)
                failures.Add("Breathing PoseAsync acceptance checks did not finish before the timeout.");

            RestoreBreathingSettings(originalBreathing);
            performer.SetBreathPhaseForAcceptance(originalBreathing.Phase);

            yield return CheckGazeAcceptance(animator, startPlacement, failures);

            _awaitableChecksComplete = false;
            RunGazeAwaitableChecks(failures);
            awaitableDeadline = Time.realtimeSinceStartup + 45f;
            while (!_awaitableChecksComplete && Time.realtimeSinceStartup < awaitableDeadline)
                yield return null;
            if (!_awaitableChecksComplete)
                failures.Add("Gaze awaitable acceptance checks did not finish before the timeout.");

            yield return CheckAttentionAndBlinkAcceptance(failures);
            yield return CheckExpressionAcceptance(failures);
            yield return CheckSpeechAcceptance(failures);
            yield return CheckGestureAcceptance(animator, failures);
            yield return CheckActionAcceptance(failures);

            var dissolveRuntimeSelfTestFailures = PerformerDissolveRuntimeSelfTests.Run();
            failures.AddRange(dissolveRuntimeSelfTestFailures);
            if (dissolveRuntimeSelfTestFailures.Length == 0)
                Debug.Log("P0.G3 synthetic dissolve phase, hidden relocation, particle lifecycle, cleanup, and serial-request checks passed.", this);
            var locomotionRuntimeSelfTestFailures = PerformerLocomotionRuntimeSelfTests.Run(performer.LocomotionProfile);
            failures.AddRange(locomotionRuntimeSelfTestFailures);
            if (locomotionRuntimeSelfTestFailures.Length == 0)
                Debug.Log("P0.TurnTo synthetic locomotion checks passed for target snapshots, safe authored-turn selection, in-place alignment, and action ownership.", this);
            yield return CheckTurnToFacadeAcceptance(failures);
            yield return CheckDissolveIntegrationAcceptance(failures);

            performer.ClearGaze();
            yield return WaitForGazeRelease(2f);
            var lifeReleaseDeadline = Time.realtimeSinceStartup + 5f;
            while (performer.GazeRuntime != null
                   && Quaternion.Angle(performer.GazeRuntime.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f
                   && Time.realtimeSinceStartup < lifeReleaseDeadline)
                yield return null;
            if (performer.GazeRuntime != null
                && Quaternion.Angle(performer.GazeRuntime.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f)
                failures.Add("Final P0.7 teardown did not return preferred head bias to identity.");
            RestoreGazeSettings(originalGaze);
            RestoreBreathingSettings(originalBreathing);
            RestoreAttentionLifeSettings(originalAttentionLife);
            RestoreBlinkSettings(originalBlink);
            if (_originalExpressionTested)
            {
                if (_originalDesiredExpression == null) performer.ClearExpression(0f);
                else performer.Expression(_originalDesiredExpression, _originalDesiredExpressionIntensity, 0f);
                yield return null;
            }
            if (_gazeTargetPositionBeforeTests.HasValue && gazeTarget != null)
                gazeTarget.position = _gazeTargetPositionBeforeTests.Value;

            CheckPlacement("Outer Lara placement", startPlacement, CapturePlacement(placementRoot), failures);
            if (performer.RuntimePlayableCount != playableCount)
                failures.Add("Playable count changed from " + playableCount + " to " + performer.RuntimePlayableCount + ".");
            Finish(failures);
        }

        private IEnumerator CheckExpressionBypassAndAsyncClear(List<string> failures)
        {
            var runtime = performer.ExpressionRuntime;
            if (runtime == null) { failures.Add("PerformerExpressionLayer runtime is missing."); yield break; }
            runtime.SetBypassedForAcceptance(true);
            performer.Expression(expressionA, 1f, 0f);
            yield return null;
            foreach (var channel in expressionA.Channels)
                if (TryGetBlendShapeWeight(channel, out var actual)
                    && Math.Abs(actual - channel.TargetWeight) > BlendShapeTolerance)
                    failures.Add("Expression bypass altered renderer channel " + channel.BlendShapeName + ".");
            runtime.SetBypassedForAcceptance(false);
            performer.Expression(expressionA, 1f, 0f);
            yield return null;
            CheckExpressionTargets("Expression bypass restored", expressionA, 1f, failures);
        }

        private IEnumerator CheckExpressionRuntimeIndependence(List<string> failures)
        {
            var gaze = performer.GazeRuntime;
            var breathing = performer.BreathingRuntime;
            var life = performer.AttentionLifeRuntime;
            if (gaze == null || breathing == null || life == null)
            { failures.Add("Expression independence requires live gaze, breathing, and attention-life runtimes."); yield break; }
            performer.BreathingEnabled = true;
            performer.AttentionLifeEnabled = true;
            performer.EyeFixationLifeEnabled = true;
            performer.HeadAttentionLifeEnabled = true;
            performer.GazeEnabled = true;
            performer.BlinkEnabled = false;
            performer.BreathsPerMinute = 12f;
            performer.SetBreathPhaseForAcceptance(0.31f);
            var phaseBefore = performer.BreathPhase;
            var generationBefore = gaze.IntentionGeneration;
            var rawTargetBefore = performer.RawGazeTargetPosition;
            var eyeCountdownBefore = performer.EyeFixationEventCountdown;
            var headCountdownBefore = performer.HeadAttentionEventCountdown;
            var breathBindings = breathing.MorphBindings.Select(binding => binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex)).ToArray();
            var expressionJob = StartExpressionRequest(expressionB, 0.65f, 0.1f);
            var deadline = Time.realtimeSinceStartup + 3f;
            while (!expressionJob.Completed && Time.realtimeSinceStartup < deadline) yield return null;
            if (!expressionJob.Completed || expressionJob.Result != ExpressionCompletion.Settled)
                failures.Add("ExpressionAsync failed while autonomous life systems were running.");
            if (gaze.IntentionGeneration != generationBefore || (performer.HasGazeTarget && Vector3.Distance(performer.RawGazeTargetPosition, rawTargetBefore) > 1e-5f))
                failures.Add("Expression changed semantic gaze acquisition or target state.");
            if (Mathf.Abs(performer.BreathPhase - phaseBefore) > 0.2f || performer.BreathPhase == phaseBefore)
                failures.Add("Expression reset or stalled autonomous breathing phase.");
            if (eyeCountdownBefore <= 0f || headCountdownBefore <= 0f)
                failures.Add("Attention-life countdown diagnostics were not available at Expression test start.");
            if (life.EyeEventCountdown > eyeCountdownBefore + 0.2f || life.HeadEventCountdown > headCountdownBefore + 0.2f)
                failures.Add("Expression reseeded or reset attention-life event timers.");
            if (breathBindings.Length != breathing.MorphBindings.Count)
                failures.Add("Breathing binding set changed while Expression was active.");
            for (var index = 0; index < breathing.MorphBindings.Count; index++)
                if (Mathf.Abs(breathBindings[index] - breathing.MorphBindings[index].Renderer
                        .GetBlendShapeWeight(breathing.MorphBindings[index].BlendShapeIndex)) > BlendShapeTolerance)
                    failures.Add("Expression changed breathing-owned morph " + breathing.MorphBindings[index].SemanticName + ".");
            performer.BreathingEnabled = false;
            performer.SetBreathPhaseForAcceptance(_originalBreathingSettings.Phase);
        }

        private IEnumerator CheckExpressionBaseAndPoseIsolation(List<string> failures)
        {
            performer.BlinkEnabled = false;
            performer.BreathingEnabled = false;
            performer.GazeEnabled = false;
            performer.AttentionLifeEnabled = false;
            performer.Pose(poseA, PoseTransition.Snap);
            performer.Expression(expressionA, 1f, 0f);
            yield return null;
            var exaggerated = expressionA.Channels.ToDictionary(channel => channel.RendererPath + "|" + channel.BlendShapeName,
                channel => channel.TargetWeight, StringComparer.Ordinal);
            performer.Pose(poseB, PoseTransition.Smooth(0.3f));
            var deadline = Time.realtimeSinceStartup + 3f;
            while (performer.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            if (performer.DesiredExpression != expressionA || performer.SettledExpression != expressionA)
                failures.Add("Pose replacement cleared or superseded persistent Expression state.");
            var baseAfterPose = performer.CaptureEvaluatedBasePoseState();
            foreach (var channel in expressionA.Channels)
            {
                var key = channel.RendererPath + "|" + channel.BlendShapeName;
                if (exaggerated.TryGetValue(key, out var target) && TryGetBlendShapeWeight(channel, out var actual))
                {
                    var baseIndex = FindBaseBlendShapeIndex(baseAfterPose, channel);
                    if (baseIndex < 0) continue;
                    var incoming = baseAfterPose.BlendShapes[baseIndex];
                    if (Mathf.Abs(actual - Mathf.Lerp(incoming, target, 1f)) > BlendShapeTolerance)
                        failures.Add("Full Expression stopped applying over the changing incoming Pose value for " + key + ".");
                }
            }
            var transitionSource = performer.CaptureTransitionSourcePoseState();
            foreach (var channel in expressionA.Channels)
            {
                var sourceIndex = FindBaseBlendShapeIndex(transitionSource, channel);
                var baseIndex = FindBaseBlendShapeIndex(baseAfterPose, channel);
                if (sourceIndex >= 0 && baseIndex >= 0
                    && Mathf.Abs(transitionSource.BlendShapes[sourceIndex] - baseAfterPose.BlendShapes[baseIndex]) > BlendShapeTolerance)
                    failures.Add("Expression contaminated the base-state pose transition source for " + channel.BlendShapeName + ".");
            }
            performer.ClearExpression(0.2f);
            deadline = Time.realtimeSinceStartup + 3f;
            while (performer.IsExpressionTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            var clearedBase = performer.CaptureEvaluatedBasePoseState();
            foreach (var channel in expressionA.Channels)
            {
                var baseIndex = FindBaseBlendShapeIndex(clearedBase, channel);
                if (baseIndex >= 0 && TryGetBlendShapeWeight(channel, out var actual)
                    && Mathf.Abs(actual - clearedBase.BlendShapes[baseIndex]) > BlendShapeTolerance)
                    failures.Add("ClearExpression did not reveal the current Pose-authored channel " + channel.BlendShapeName + ".");
            }
        }

        private IEnumerator CheckExpressionBlinkComposition(List<string> failures)
        {
            var blink = performer.BlinkRuntime;
            if (blink == null || !blink.IsAvailable) { failures.Add("Blink composition requires resolved autonomous eyelid bindings."); yield break; }
            performer.BreathingEnabled = false;
            performer.GazeEnabled = false;
            performer.AttentionLifeEnabled = false;
            performer.BlinkEnabled = false;
            performer.ClearExpression(0f);
            yield return null;
            foreach (var binding in blink.Bindings) binding.Renderer.SetBlendShapeWeight(binding.BlendShapeIndex, 12f);
            var narrowings = expressionA.Channels.Where(channel =>
                channel.BlendShapeName.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0
                || channel.BlendShapeName.IndexOf("Lid", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            var blinkBefore = blink.Bindings.ToDictionary(binding => binding.ImportedBlendShapeName,
                binding => binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex), StringComparer.Ordinal);
            performer.Expression(expressionA, 1f, 0f);
            yield return null;
            foreach (var binding in blink.Bindings)
                if (Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex) - blinkBefore[binding.ImportedBlendShapeName]) > BlendShapeTolerance)
                    failures.Add("Facial Expression directly owned an autonomous full-blink control.");
            if (narrowings.Length == 0)
                Debug.Log("P0.8 Blink smoke: selected Expression has no eye/lid narrowing channel; composition is verified by the active blink-only stream below.", this);
            blink.SetClosureForAcceptance(1f);
            yield return null;
            foreach (var binding in blink.Bindings)
                if (Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex) - binding.PositiveMaximumWeight) > BlendShapeTolerance)
                    failures.Add("Blink did not close fully over the current Expression face.");
            blink.SetClosureForAcceptance(0f);
            yield return null;
            foreach (var binding in blink.Bindings)
                if (Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex) - blinkBefore[binding.ImportedBlendShapeName]) > BlendShapeTolerance)
                    failures.Add("Opening Blink did not reveal the underlying Expression eyelid state.");
            blink.ClearClosureOverrideForAcceptance();
            performer.ClearExpression(0f);
            yield return null;
        }

        private int FindBaseBlendShapeIndex(PerformerPoseSnapshot state, PerformerExpressionChannel channel)
        {
            var renderer = performer.GetComponent<Animator>().transform.Find(channel.RendererPath)?.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null || renderer.sharedMesh == null) return -1;
            var shape = renderer.sharedMesh.GetBlendShapeIndex(channel.BlendShapeName);
            if (shape < 0) return -1;
            var index = 0;
            foreach (var candidate in performer.GetComponent<Animator>().GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (candidate.sharedMesh == null) continue;
                if (candidate == renderer) return index + shape < state.BlendShapes.Length ? index + shape : -1;
                index += candidate.sharedMesh.blendShapeCount;
            }
            return -1;
        }

        private IEnumerator CheckExpressionAcceptance(List<string> failures)
        {
            var runtimeSelfTestFailures = PerformerExpressionRuntimeSelfTests.Run();
            failures.AddRange(runtimeSelfTestFailures);
            if (runtimeSelfTestFailures.Length == 0)
                Debug.Log("P0.8.1 synthetic facial Expression runtime checks passed.", this);

            if (expressionA == null || expressionB == null || expressionC == null)
            {
                failures.Add("Assign Expression A, B, and C to run the P0.8 acceptance checks.");
                yield break;
            }

            _originalExpressionTested = true;
            performer.BreathingEnabled = true;
            performer.MorphBreathingEnabled = true;
            performer.BoneBreathingEnabled = true;
            performer.GazeEnabled = true;
            performer.AttentionLifeEnabled = true;
            performer.EyeFixationLifeEnabled = true;
            performer.HeadAttentionLifeEnabled = true;
            performer.BlinkEnabled = false;
            performer.SetBreathPhaseForAcceptance(0.22f);
            performer.ClearExpression(0f);
            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            var baseA = CaptureExpressionChannels(expressionA);
            performer.Expression(expressionA, 1f, 0f);
            yield return null;
            CheckExpressionTargets("Expression A snap", expressionA, 1f, failures);
            if (performer.DesiredExpression != expressionA || performer.SettledExpression != expressionA)
                failures.Add("Expression A snap did not publish desired and settled state.");

            performer.Expression(expressionA, 0.5f, 0.25f);
            if (!performer.IsExpressionTransitioning) failures.Add("Same expression with changed intensity did not retarget.");
            var deadline = Time.realtimeSinceStartup + 3f;
            while (performer.IsExpressionTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            foreach (var channel in expressionA.Channels)
            {
                var key = channel.RendererPath + "|" + channel.BlendShapeName;
                if (baseA.TryGetValue(key, out var incoming) && TryGetBlendShapeWeight(channel, out var actual))
                {
                    var expected = Mathf.Lerp(incoming, channel.TargetWeight, 0.5f);
                    if (Mathf.Abs(actual - expected) > BlendShapeTolerance)
                        failures.Add("Expression A partial intensity did not use Lerp(incoming, target, intensity) for " + key + ".");
                }
            }

            yield return CheckExpressionBypassAndAsyncClear(failures);
            yield return CheckExpressionRuntimeIndependence(failures);
            yield return CheckExpressionBaseAndPoseIsolation(failures);
            yield return CheckExpressionBlinkComposition(failures);
            _expressionAwaitableChecksComplete = false;
            RunExpressionAwaitableChecks(failures);
            deadline = Time.realtimeSinceStartup + 20f;
            while (!_expressionAwaitableChecksComplete && Time.realtimeSinceStartup < deadline) yield return null;
            if (!_expressionAwaitableChecksComplete) failures.Add("ExpressionAsync checks timed out.");

        }

        private IEnumerator CheckSpeechAcceptance(List<string> failures)
        {
            var runtimeFailures = PerformerSpeechRuntimeSelfTests.Run();
            failures.AddRange(runtimeFailures);
            if (runtimeFailures.Length == 0)
                Debug.Log("P0.9A deterministic speech queue checks passed. Actual end-of-clip playback remains a Play Mode audio check.", this);

            var smoke = GetComponent<PerformerPoseSmokeHarness>();
            var clipA = smoke == null ? null : smoke.SpeechClipA;
            var clipB = smoke == null ? null : smoke.SpeechClipB;
            var source = performer.SpeechAudioSource;
            if (source == null || clipA == null || clipB == null)
            {
                failures.Add("Assign the generated Speech AudioSource and Assets/generated/A.mp3 and B.mp3 in the performer smoke harness before running P0.9A integration checks.");
                yield break;
            }

            var head = performer.GetComponent<Animator>().GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "head");
            if (head == null || source.transform == head || !source.transform.IsChildOf(head))
                failures.Add("The dedicated speech AudioSource is not parented beneath the animated head bone.");

            var previousPose = performer.DesiredPose;
            var previousExpression = performer.DesiredExpression;
            var previousExpressionIntensity = performer.DesiredExpressionIntensity;
            var speechTestExpression = previousExpression == expressionA ? expressionB : expressionA;
            if (speechTestExpression != null) performer.Expression(speechTestExpression, 0.73f, 0.5f);
            var expectedExpression = performer.DesiredExpression;
            var expectedExpressionIntensity = performer.DesiredExpressionIntensity;
            var breathPhase = performer.BreathPhase;
            var hasGazeTarget = performer.HasGazeTarget;
            var gazeTarget = performer.RawGazeTargetPosition;
            var eyeCountdown = performer.EyeFixationEventCountdown;
            var headCountdown = performer.HeadAttentionEventCountdown;

            performer.Say(clipA);
            if (!performer.IsSpeaking || performer.CurrentSpeechClip != clipA
                || performer.PendingSpeechCount != 0 || source.clip != clipA)
                failures.Add("Say did not immediately publish the current clip on the dedicated AudioSource.");
            if (performer.DesiredExpression != expectedExpression
                || Mathf.Abs(performer.DesiredExpressionIntensity - expectedExpressionIntensity) > 0.0001f
                || performer.HasGazeTarget != hasGazeTarget
                || (hasGazeTarget && Vector3.Distance(performer.RawGazeTargetPosition, gazeTarget) > 0.0001f)
                || Mathf.Abs(performer.BreathPhase - breathPhase) > 0.0001f
                || Mathf.Abs(performer.EyeFixationEventCountdown - eyeCountdown) > 0.0001f
                || Mathf.Abs(performer.HeadAttentionEventCountdown - headCountdown) > 0.0001f)
                failures.Add("Starting speech changed Expression, gaze, breathing, or attention-life state.");

            var speechTestPose = previousPose == poseB ? poseC : poseB;
            var poseDuringSpeech = StartPoseRequest(speechTestPose, PoseTransition.Smooth(0.25f));
            if (poseDuringSpeech.Completed || !performer.IsTransitioning)
                failures.Add("PoseAsync did not remain active while a speech request was playing.");

            var speechResult = SpeechCompletion.Finished;
            var speechCompleted = false;
            var speechAwaiter = performer.SayAsync(clipB).GetAwaiter();
            speechAwaiter.OnCompleted(() =>
            {
                speechResult = speechAwaiter.GetResult();
                speechCompleted = true;
            });
            if (performer.CurrentSpeechClip != clipA || performer.PendingSpeechCount != 1
                || source.clip != clipA || speechCompleted)
                failures.Add("SayAsync did not remain queued behind the current line while Pose/Expression were transitioning.");

            performer.Pose(speechTestPose, PoseTransition.Snap);
            if (!poseDuringSpeech.Completed || poseDuringSpeech.Error != null
                || poseDuringSpeech.Result != PoseCompletion.Settled
                || !performer.IsSpeaking || performer.CurrentSpeechClip != clipA || speechCompleted)
                failures.Add("PoseAsync settlement was coupled to queued speech completion.");
            performer.StopSpeaking();
            if (!speechCompleted || speechResult != SpeechCompletion.Cancelled
                || performer.IsSpeaking || performer.PendingSpeechCount != 0 || source.isPlaying || source.clip != null)
                failures.Add("StopSpeaking did not stop current audio, clear the queue, and cancel the queued SayAsync request.");

            performer.Say(clipA);
            if (!performer.IsSpeaking || performer.CurrentSpeechClip != clipA)
                failures.Add("Speech did not accept a new request after StopSpeaking.");
            performer.StopSpeaking();

            if (previousPose != null) performer.Pose(previousPose, PoseTransition.Snap);
            if (previousExpression == null) performer.ClearExpression(0f);
            else performer.Expression(previousExpression, previousExpressionIntensity, 0f);
            yield break;
        }

        private IEnumerator CheckTurnToFacadeAcceptance(List<string> failures)
        {
            Vector3 origin = performer.transform.position;
            Quaternion originalRotation = performer.transform.rotation;
            if (performer.VisibilityState != PerformerVisibilityState.Visible)
            {
                bool hiddenRejected = false;
                try { performer.TurnTo(origin + Vector3.forward * 3f); }
                catch (InvalidOperationException) { hiddenRejected = true; }
                CheckTurnTo(hiddenRejected && performer.IsHidden,
                    "TurnTo rejects while persistently Hidden without revealing Lara", failures);
                yield break;
            }

            if (performer.SeatingState != PerformerSeatingState.Standing)
            {
                bool seatedRejected = false;
                try { performer.TurnTo(origin + Vector3.forward * 3f); }
                catch (InvalidOperationException) { seatedRejected = true; }
                CheckTurnTo(seatedRejected && performer.SeatingState != PerformerSeatingState.Standing,
                    "TurnTo rejects while seated or in a seating transition without auto-standing", failures);
                yield break;
            }

            if (!performer.IsRuntimeReady || !performer.LocomotionAvailable
                || performer.IsLocomoting || performer.IsTeleporting || performer.IsDissolving)
            {
                failures.Add("P0.TurnTo: facade composition checks require a visible, standing performer with idle locomotion and no active teleport or dissolve.");
                yield break;
            }

            PerformerPose desiredPose = performer.DesiredPose;
            PerformerPose settledPose = performer.SettledPose;
            bool hadGazeTarget = performer.HasGazeTarget;
            Vector3 originalGazeTarget = performer.RawGazeTargetPosition;
            var target = new GameObject("P0TurnTo_FacadeTargetSnapshot");
            var gazeTargetObject = new GameObject("P0TurnTo_PersistentGazeTarget");
            try
            {
                Vector3 planarForward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up).normalized;
                Vector3 capturedDirection = Quaternion.AngleAxis(70f, Vector3.up) * planarForward;
                target.transform.SetPositionAndRotation(origin + capturedDirection * 4f,
                    Quaternion.LookRotation(-capturedDirection, Vector3.up));
                gazeTargetObject.transform.position = origin + Quaternion.AngleAxis(-25f, Vector3.up)
                    * planarForward * 2f + Vector3.up * 1.4f;
                performer.LookAt(gazeTargetObject.transform);
                string gazeDescription = performer.GazeTargetDescription;

                var observation = new TurnToCompletionObservation();
                ObserveTurnToCompletion(observation, target.transform);
                // Move and rotate the Transform after the call. The action must keep its
                // original position snapshot and must ignore the target's forward vector.
                target.transform.SetPositionAndRotation(origin - capturedDirection * 4f,
                    Quaternion.LookRotation(capturedDirection, Vector3.up));

                float maximumDrift = 0f;
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!observation.Completed && Time.realtimeSinceStartup < deadline)
                {
                    maximumDrift = Mathf.Max(maximumDrift,
                        Vector3.Distance(origin, performer.transform.position));
                    yield return null;
                }

                CheckTurnTo(observation.Completed && observation.Error == null
                    && observation.Result == LocomotionCompletion.Arrived,
                    "public TurnToAsync(Transform) resolves Arrived", failures);
                CheckTurnTo(maximumDrift < 0.001f
                    && Vector3.Distance(origin, performer.transform.position) < 0.001f,
                    "public TurnTo preserves actor position during and after the turn", failures);
                Vector3 finalForward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up).normalized;
                CheckTurnTo(Vector3.Angle(finalForward, capturedDirection)
                        <= performer.LocomotionProfile.ArrivalHeadingTolerance + 0.01f,
                    "public TurnTo faces the captured target position instead of target.forward or its later position", failures);
                CheckTurnTo(performer.DesiredPose == desiredPose && performer.SettledPose == settledPose,
                    "TurnTo leaves persistent DesiredPose and SettledPose unchanged", failures);
                CheckTurnTo(performer.GazeTargetDescription == gazeDescription
                    && Vector3.Distance(performer.RawGazeTargetPosition, gazeTargetObject.transform.position) < 0.001f,
                    "TurnTo preserves the independent LookAt target", failures);

                Quaternion turnedRotation = performer.transform.rotation;
                performer.ClearGaze();
                CheckTurnTo(Quaternion.Angle(performer.transform.rotation, turnedRotation) < 0.001f,
                    "ClearGaze after TurnTo does not alter body orientation", failures);
            }
            finally
            {
                if (hadGazeTarget) performer.LookAt(originalGazeTarget);
                else performer.ClearGaze();
                performer.transform.SetPositionAndRotation(origin, originalRotation);
                Destroy(target);
                Destroy(gazeTargetObject);
            }
        }

        private sealed class TurnToCompletionObservation
        {
            public bool Completed;
            public LocomotionCompletion Result;
            public Exception Error;
        }

        private sealed class GestureCompletionObservation
        {
            public bool Completed;
            public GestureCompletion Result;
            public Exception Error;
        }

        private IEnumerator CheckGestureAcceptance(Animator animator, List<string> failures)
        {
            if (gestureAcceptanceWave == null)
            {
                if (performer.gameObject.scene.name == "FirstPerformanceVoid")
                    failures.Add("P0.Gesture acceptance Wave is missing. Run Tools > DAZ Pose > Gesture > Generate Gesture Acceptance Assets.");
                else
                    Debug.Log("P0.Gesture scene checks skipped because no acceptance Wave is assigned.", this);
                yield break;
            }
            if (!gestureAcceptanceWave.IsReady(out string reason))
            {
                failures.Add("P0.Gesture acceptance asset is invalid: " + reason);
                yield break;
            }
            if (!performer.GestureAvailable)
            {
                failures.Add("P0.Gesture is unavailable. Run Tools > DAZ Pose > Gesture > Generate Gesture Acceptance Assets to assign a valid generated chestLower mask.");
                yield break;
            }
            if (performer.VisibilityState != PerformerVisibilityState.Visible
                || performer.IsLocomoting || performer.IsTeleporting || performer.IsDissolving)
            {
                failures.Add("P0.Gesture automated checks require a visible, stationary performer with no teleport or dissolve in progress.");
                yield break;
            }

            Vector3 rootPosition = performer.transform.position;
            Quaternion rootRotation = performer.transform.rotation;
            PerformerPose desiredPose = performer.DesiredPose;
            PerformerPose settledPose = performer.SettledPose;
            PerformerExpression desiredExpression = performer.DesiredExpression;
            bool hadGaze = performer.HasGazeTarget;
            Vector3 gazeTargetPosition = performer.RawGazeTargetPosition;
            Transform[] lowerBody = FindGestureProtectedBones(animator.transform, failures);
            Vector3[] lowerPositions = lowerBody.Select(bone => bone.localPosition).ToArray();
            Quaternion[] lowerRotations = lowerBody.Select(bone => bone.localRotation).ToArray();

            PerformerPoseSmokeHarness smokeHarness = GetComponent<PerformerPoseSmokeHarness>();
            AudioClip speechClip = smokeHarness != null ? smokeHarness.SpeechClipA : null;
            if (speechClip != null) performer.Say(speechClip);
            var completion = new GestureCompletionObservation();
            ObserveGestureCompletion(completion, gestureAcceptanceWave);
            if (speechClip != null && (!performer.IsSpeaking || performer.CurrentSpeechClip != speechClip))
                failures.Add("P0.Gesture interrupted or replaced speech when GestureAsync started.");
            if (speechClip != null) performer.StopSpeaking();
            yield return null;
            if (!performer.IsGesturing || performer.CurrentGesture != gestureAcceptanceWave)
                failures.Add("P0.Gesture did not publish IsGesturing and CurrentGesture immediately after GestureAsync.");
            if (performer.GestureProgress < 0f || performer.GestureProgress > 1f)
                failures.Add("P0.GestureProgress left the normalized 0..1 range.");

            float maximumProgress = performer.GestureProgress;
            float deadline = Time.realtimeSinceStartup + gestureAcceptanceWave.Clip.length + 4f;
            while (!completion.Completed && Time.realtimeSinceStartup < deadline)
            {
                maximumProgress = Mathf.Max(maximumProgress, performer.GestureProgress);
                yield return null;
            }
            if (!completion.Completed)
                failures.Add("P0.GestureAsync did not complete before the clip-duration timeout.");
            else if (completion.Error != null || completion.Result != GestureCompletion.Completed)
                failures.Add("P0.GestureAsync returned " + completion.Result + (completion.Error != null ? ": " + completion.Error.Message : "."));
            if (maximumProgress < 0.95f)
                failures.Add("P0.GestureProgress did not advance through the finite clip.");
            if (performer.IsGesturing || performer.CurrentGesture != null || performer.GestureProgress != 0f)
                failures.Add("P0.Gesture did not return to the idle status after its final blend-out.");

            CheckGestureRootAndBaseState(rootPosition, rootRotation, desiredPose, settledPose,
                desiredExpression, hadGaze, gazeTargetPosition, lowerBody, lowerPositions, lowerRotations, failures,
                "standalone completion");

            var first = new GestureCompletionObservation();
            ObserveGestureCompletion(first, gestureAcceptanceWave);
            float supersedeDeadline = Time.realtimeSinceStartup + gestureAcceptanceWave.Clip.length;
            while (performer.GestureProgress < 0.4f && Time.realtimeSinceStartup < supersedeDeadline)
                yield return null;
            var second = new GestureCompletionObservation();
            ObserveGestureCompletion(second, gestureAcceptanceWave);
            yield return null;
            if (!performer.IsGesturing || performer.CurrentGesture != gestureAcceptanceWave
                || performer.GestureProgress > 0.25f)
                failures.Add("P0.Gesture repeated same-asset request did not restart the Wave from its beginning.");
            deadline = Time.realtimeSinceStartup + gestureAcceptanceWave.Clip.length * 2f + 4f;
            while ((!first.Completed || !second.Completed) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!first.Completed || first.Error != null || first.Result != GestureCompletion.Superseded)
                failures.Add("P0.Gesture first waiter was not resolved as Superseded by the restart.");
            if (!second.Completed || second.Error != null || second.Result != GestureCompletion.Completed)
                failures.Add("P0.Gesture restarted action did not resolve its waiter as Completed.");
            CheckGestureRootAndBaseState(rootPosition, rootRotation, desiredPose, settledPose,
                desiredExpression, hadGaze, gazeTargetPosition, lowerBody, lowerPositions, lowerRotations, failures,
                "same-gesture supersession");

            var disabled = new GestureCompletionObservation();
            ObserveGestureCompletion(disabled, gestureAcceptanceWave);
            performer.enabled = false;
            float disableDeadline = Time.realtimeSinceStartup + 2f;
            while (!disabled.Completed && Time.realtimeSinceStartup < disableDeadline)
                yield return null;
            if (!disabled.Completed || disabled.Error != null
                || disabled.Result != GestureCompletion.PerformerDisabled)
                failures.Add("P0.Gesture active waiter did not resolve as PerformerDisabled when SuccubusPerformer was disabled.");
            if (performer.IsGesturing || performer.CurrentGesture != null || performer.GestureProgress != 0f)
                failures.Add("P0.Gesture state was not cleared when SuccubusPerformer was disabled.");

            performer.enabled = true;
            yield return null;
            if (!performer.GestureAvailable || performer.IsGesturing
                || performer.CurrentGesture != null || performer.GestureProgress != 0f)
                failures.Add("P0.Gesture finite action was restored or the runtime was unavailable after performer re-enable.");
        }

        private sealed class ActionCompletionObservation
        {
            public bool Completed;
            public ActionCompletion Result;
            public Exception Error;
        }

        private sealed class LocomotionCompletionObservation
        {
            public bool Completed;
            public LocomotionCompletion Result;
            public Exception Error;
        }

        private IEnumerator CheckActionAcceptance(List<string> failures)
        {
            if (actionAcceptanceJumpForJoy == null)
            {
                if (performer.gameObject.scene.name == "FirstPerformanceVoid")
                    failures.Add("P0.Perform JumpForJoy acceptance Action is missing. Run Tools > DAZ Pose > Action > Generate Jump for Joy Acceptance Assets.");
                else Debug.Log("P0.Perform scene checks skipped because no acceptance Action is assigned.", this);
                yield break;
            }
            if (!actionAcceptanceJumpForJoy.IsReady(out string reason))
            {
                failures.Add("P0.Perform JumpForJoy acceptance asset is invalid: " + reason);
                yield break;
            }
            if (!performer.ActionAvailable)
            {
                failures.Add("P0.Perform is unavailable. Assign a ready locomotion profile and rebuild the Action acceptance assets.");
                yield break;
            }
            if (performer.VisibilityState != PerformerVisibilityState.Visible
                || performer.SeatingState != PerformerSeatingState.Standing || performer.IsLocomoting
                || performer.IsTeleporting || performer.IsDissolving || performer.IsPerforming)
            {
                failures.Add("P0.Perform checks require a visible, standing performer with no active locomotion, seating, teleport, dissolve, or Action request.");
                yield break;
            }

            PerformerPose restorePose = performer.DesiredPose != null ? performer.DesiredPose : poseA;
            PerformerExpression restoreExpression = performer.DesiredExpression;
            float restoreExpressionIntensity = performer.DesiredExpressionIntensity;
            bool restoreGaze = performer.HasGazeTarget;
            Vector3 restoreGazePosition = performer.RawGazeTargetPosition;
            PerformerPoseSmokeHarness smoke = GetComponent<PerformerPoseSmokeHarness>();
            AudioClip speechClip = smoke != null ? smoke.SpeechClipA : null;

            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            Vector3 anchor = performer.transform.position;
            Vector3 anchorForward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up).normalized;
            PerformerPose poseBeforeAction = performer.DesiredPose;
            PerformerPose settledBeforeAction = performer.SettledPose;
            PerformerExpression expressionBeforeAction = performer.DesiredExpression;
            bool gazeBeforeAction = performer.HasGazeTarget;
            Vector3 gazePositionBeforeAction = performer.RawGazeTargetPosition;

            var basic = new ActionCompletionObservation();
            ObserveActionCompletion(basic, actionAcceptanceJumpForJoy);
            if (!performer.IsPerforming || performer.CurrentAction != actionAcceptanceJumpForJoy
                || performer.ActionState != PerformerActionState.Performing)
                failures.Add("P0.PerformAsync did not immediately publish Performing and CurrentAction.");
            if (performer.ActionProgress < 0f || performer.ActionProgress > 1f)
                failures.Add("P0.Perform ActionProgress left the normalized 0..1 range.");

            CheckActionCommandRejected(() => performer.Perform(actionAcceptanceJumpForJoy), "second Perform", failures);
            CheckActionCommandRejected(() => performer.WalkTo(performer.transform.position + performer.transform.forward * 2f), "WalkTo", failures);
            CheckActionCommandRejected(() => performer.TurnTo(performer.transform.position + performer.transform.forward * 2f), "TurnTo", failures);
            CheckActionCommandRejected(() => performer.SitAt(null), "SitAt", failures);
            CheckActionCommandRejected(performer.StandUp, "StandUp", failures);
            CheckActionCommandRejected(() => performer.TeleportTo(performer.transform.position), "TeleportTo", failures);
            CheckActionCommandRejected(() => performer.DissolveTo(performer.transform.position), "DissolveTo", failures);
            CheckActionCommandRejected(() => performer.DissolveOut(0.2f), "DissolveOut", failures);
            CheckActionCommandRejected(() => performer.DissolveIn(0.2f), "DissolveIn", failures);
            if (!performer.IsPerforming || performer.CurrentAction != actionAcceptanceJumpForJoy)
                failures.Add("A rejected spatial command damaged the active PerformerAction.");

            float peakWorldY = performer.transform.position.y;
            float maximumProgress = performer.ActionProgress;
            bool sawRecovery = false;
            float deadline = Time.realtimeSinceStartup + actionAcceptanceJumpForJoy.DurationSeconds + 45f;
            while (!basic.Completed && Time.realtimeSinceStartup < deadline)
            {
                peakWorldY = Mathf.Max(peakWorldY, performer.transform.position.y);
                maximumProgress = Mathf.Max(maximumProgress, performer.ActionProgress);
                sawRecovery |= performer.ActionState == PerformerActionState.Recovering;
                if (performer.CurrentAction != null && performer.CurrentAction != actionAcceptanceJumpForJoy)
                    failures.Add("P0.Perform changed CurrentAction before the authored action completed.");
                yield return null;
            }
            if (!basic.Completed)
                failures.Add("P0.PerformAsync did not complete after the action and return-home timeout.");
            else if (basic.Error != null || basic.Result != ActionCompletion.Completed)
                failures.Add("P0.PerformAsync returned " + basic.Result + (basic.Error != null ? ": " + basic.Error.Message : "."));
            if (maximumProgress < 0.95f)
                failures.Add("P0.Perform ActionProgress did not advance through the finite clip.");
            if (peakWorldY - anchor.y < 0.02f)
                failures.Add("KAWAII JumpForJoy did not produce measurable actor-root vertical movement during Perform.");
            if (performer.IsPerforming || performer.CurrentAction != null || performer.ActionState != PerformerActionState.Idle)
                failures.Add("P0.Perform did not publish its idle state after completion.");
            if (performer.DesiredPose != poseBeforeAction || performer.SettledPose != settledBeforeAction)
                failures.Add("P0.Perform changed the persistent DesiredPose or SettledPose.");
            if (performer.DesiredExpression != expressionBeforeAction)
                failures.Add("P0.Perform changed the independent expression.");
            if (performer.HasGazeTarget != gazeBeforeAction
                || (gazeBeforeAction && Vector3.Distance(performer.RawGazeTargetPosition, gazePositionBeforeAction) > PositionTolerance))
                failures.Add("P0.Perform changed the independent gaze target.");
            if (performer.ActionPositionError > performer.LocomotionProfile.ArrivalPositionTolerance + 0.005f
                || performer.ActionHeadingError > performer.LocomotionProfile.ArrivalHeadingTolerance + 0.1f)
                failures.Add("P0.Perform returned outside the configured anchor/facing tolerances.");
            Vector2 basicPlanarDisplacement = new Vector2(actionAcceptanceJumpForJoy.NominalDisplacement.x,
                actionAcceptanceJumpForJoy.NominalDisplacement.z);
            bool basicNeedsRecovery = basicPlanarDisplacement.magnitude > performer.LocomotionProfile.ArrivalPositionTolerance
                || Mathf.Abs(actionAcceptanceJumpForJoy.NominalYawDegrees) > performer.LocomotionProfile.ArrivalHeadingTolerance;
            if (basicNeedsRecovery && !sawRecovery)
                failures.Add("P0.Perform did not publish Recovering for a JumpForJoy trajectory that ended outside locomotion tolerances.");

            if (gestureAcceptanceWave != null && gestureAcceptanceWave.IsReady(out _)
                && !performer.IsGesturing)
            {
                performer.Pose(poseA, PoseTransition.Snap);
                yield return null;
                performer.Gesture(gestureAcceptanceWave);
                if (!performer.IsGesturing)
                    failures.Add("P0.Perform composition test could not start Gesture before the next Action.");
                if (expressionA != null) performer.Expression(expressionA, 1f, 0f);
                if (gazeTarget != null) performer.LookAt(gazeTarget);
                if (speechClip != null)
                {
                    performer.StopSpeaking();
                    performer.Say(speechClip);
                }
                performer.Perform(actionAcceptanceJumpForJoy);
                bool changedPose = false;
                deadline = Time.realtimeSinceStartup + actionAcceptanceJumpForJoy.DurationSeconds + 45f;
                while (performer.IsPerforming && Time.realtimeSinceStartup < deadline)
                {
                    if (!changedPose && performer.ActionState == PerformerActionState.Performing
                        && performer.ActionProgress >= 0.2f)
                    {
                        performer.Pose(poseC, PoseTransition.Smooth(0.2f));
                        changedPose = true;
                    }
                    if (performer.CurrentAction != actionAcceptanceJumpForJoy)
                        failures.Add("A Pose/Gesture/life-layer command interrupted the active PerformerAction.");
                    yield return null;
                }
                if (!changedPose) failures.Add("P0.Perform did not remain active long enough to test a mid-action persistent Pose change.");
                if (performer.DesiredPose != poseC)
                    failures.Add("The most recently requested Pose did not remain desired after Perform.");
                if (expressionA != null && performer.DesiredExpression != expressionA)
                    failures.Add("P0.Perform cleared or replaced the independent expression.");
                if (gazeTarget != null && (!performer.HasGazeTarget
                    || Vector3.Distance(performer.RawGazeTargetPosition, gazeTarget.position) > PositionTolerance))
                    failures.Add("P0.Perform cleared or replaced the independent gaze target.");
                if (speechClip != null && !performer.IsSpeaking && performer.CurrentSpeechClip != speechClip)
                    failures.Add("P0.Perform interrupted the independent speech request.");
                performer.StopSpeaking();
            }

            PerformerAction recoveryAction = actionAcceptanceDisplacedTest != null
                && actionAcceptanceDisplacedTest.IsReady(out _) ? actionAcceptanceDisplacedTest : actionAcceptanceJumpForJoy;
            if (performer.IsPerforming)
            {
                float recoveryWaitDeadline = Time.realtimeSinceStartup + recoveryAction.DurationSeconds + 45f;
                while (performer.IsPerforming && Time.realtimeSinceStartup < recoveryWaitDeadline) yield return null;
            }
            if (!performer.IsPerforming && performer.DesiredPose != restorePose)
            {
                performer.Pose(restorePose, PoseTransition.Snap);
                yield return null;
            }
            if (restoreExpression == null) performer.ClearExpression(0f);
            else performer.Expression(restoreExpression, restoreExpressionIntensity, 0f);
            if (restoreGaze) performer.LookAt(restoreGazePosition);
            else performer.ClearGaze();
            performer.StopSpeaking();
            yield return null;

            yield return CheckDisplacedActionRecovery(recoveryAction, failures);
            yield return CheckActionDisableSemantics(actionAcceptanceJumpForJoy, failures);

            if (performer.IsPerforming)
                failures.Add("P0.Perform left an action active after its acceptance checks.");
            if (Vector3.Distance(anchor, performer.transform.position) > performer.LocomotionProfile.ArrivalPositionTolerance + 0.005f
                || Vector3.Angle(Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up), anchorForward)
                    > performer.LocomotionProfile.ArrivalHeadingTolerance + 0.1f)
                failures.Add("P0.Perform acceptance cleanup did not physically restore the original anchor/facing.");
        }

        private IEnumerator CheckDisplacedActionRecovery(PerformerAction action, List<string> failures)
        {
            if (action == null || !action.IsReady(out _)) yield break;
            Vector3 anchor = performer.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up).normalized;
            var observation = new ActionCompletionObservation();
            ObserveActionCompletion(observation, action);
            float maximumDisplacement = 0f;
            bool sawRecovery = false;
            bool checkedRecoveryGuards = false;
            float deadline = Time.realtimeSinceStartup + action.DurationSeconds + 60f;
            while (!observation.Completed && Time.realtimeSinceStartup < deadline)
            {
                maximumDisplacement = Mathf.Max(maximumDisplacement,
                    Vector3.Distance(anchor, performer.transform.position));
                if (performer.ActionState == PerformerActionState.Recovering)
                {
                    sawRecovery = true;
                    if (performer.IsPerforming && performer.CurrentAction == action)
                    {
                        CheckActionCommandRejected(() => performer.WalkTo(performer.transform.position + performer.transform.forward * 2f), "WalkTo during recovery", failures);
                        CheckActionCommandRejected(() => performer.TurnTo(performer.transform.position + performer.transform.forward * 2f), "TurnTo during recovery", failures);
                        CheckActionCommandRejected(() => performer.Perform(action), "Perform during recovery", failures);
                        checkedRecoveryGuards = true;
                    }
                }
                yield return null;
            }
            if (maximumDisplacement < 0.2f)
                failures.Add("The displaced recovery acceptance Action did not visibly displace Lara during its authored phase.");
            if (!sawRecovery)
                failures.Add("The displaced recovery acceptance Action did not enter ActionState.Recovering.");
            if (!checkedRecoveryGuards)
                failures.Add("The action's recovery phase was not observed with spatial-command guards active.");
            if (!observation.Completed)
                failures.Add("The displaced PerformAsync did not complete before its recovery timeout.");
            else if (observation.Error != null || observation.Result != ActionCompletion.Completed)
                failures.Add("Displaced PerformAsync returned " + observation.Result
                    + (observation.Error != null ? ": " + observation.Error.Message : "."));
            if (performer.IsPerforming || performer.CurrentAction != null)
                failures.Add("The displaced action remained active after recovery completed.");
            if (Vector3.Distance(anchor, performer.transform.position) > performer.LocomotionProfile.ArrivalPositionTolerance + 0.005f)
                failures.Add("Existing locomotion did not return the displaced Action to its captured position.");
            if (Vector3.Angle(Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up), forward)
                > performer.LocomotionProfile.ArrivalHeadingTolerance + 0.1f)
                failures.Add("Existing locomotion did not restore the displaced Action's captured facing.");
        }

        private IEnumerator CheckActionDisableSemantics(PerformerAction action, List<string> failures)
        {
            var duringAction = new ActionCompletionObservation();
            ObserveActionCompletion(duringAction, action);
            if (performer.ActionState != PerformerActionState.Performing)
                failures.Add("The disable-during-Perform acceptance Action did not enter Performing.");
            performer.enabled = false;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!duringAction.Completed && Time.realtimeSinceStartup < deadline) yield return null;
            if (!duringAction.Completed || duringAction.Error != null
                || duringAction.Result != ActionCompletion.PerformerDisabled)
                failures.Add("PerformAsync during the authored action did not resolve as PerformerDisabled.");
            performer.enabled = true;
            yield return null;
            if (performer.IsPerforming || performer.CurrentAction != null
                || performer.ActionState != PerformerActionState.Idle || !performer.ActionAvailable)
                failures.Add("Re-enabling Lara restarted or retained the finite Action.");

            PerformerAction displaced = actionAcceptanceDisplacedTest;
            if (displaced == null || !displaced.IsReady(out _)) yield break;
            Vector3 anchor = performer.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up).normalized;
            var duringRecovery = new ActionCompletionObservation();
            ObserveActionCompletion(duringRecovery, displaced);
            deadline = Time.realtimeSinceStartup + displaced.DurationSeconds + 30f;
            while (performer.ActionState != PerformerActionState.Recovering
                && !duringRecovery.Completed && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (performer.ActionState != PerformerActionState.Recovering)
            {
                failures.Add("The disable-during-recovery test did not reach ActionState.Recovering.");
                while (!duringRecovery.Completed && Time.realtimeSinceStartup < deadline) yield return null;
                yield break;
            }
            Vector3 positionAtDisable = performer.transform.position;
            performer.enabled = false;
            deadline = Time.realtimeSinceStartup + 2f;
            while (!duringRecovery.Completed && Time.realtimeSinceStartup < deadline) yield return null;
            if (!duringRecovery.Completed || duringRecovery.Error != null
                || duringRecovery.Result != ActionCompletion.PerformerDisabled)
                failures.Add("PerformAsync during return-home recovery did not resolve as PerformerDisabled.");
            if (Vector3.Distance(positionAtDisable, anchor) < 0.1f
                || Vector3.Distance(performer.transform.position, anchor) < 0.1f)
                failures.Add("Disabling during recovery teleported Lara to her captured anchor.");

            performer.enabled = true;
            yield return null;
            var returnTarget = new GameObject("ActionAcceptanceReturnHome");
            returnTarget.transform.SetPositionAndRotation(anchor, Quaternion.LookRotation(forward, Vector3.up));
            var locomotion = new LocomotionCompletionObservation();
            ObserveLocomotionCompletion(locomotion, returnTarget.transform);
            deadline = Time.realtimeSinceStartup + 45f;
            while (!locomotion.Completed && Time.realtimeSinceStartup < deadline) yield return null;
            Destroy(returnTarget);
            if (!locomotion.Completed || locomotion.Error != null || locomotion.Result != LocomotionCompletion.Arrived)
                failures.Add("Acceptance cleanup could not use WalkTo to restore Lara after the disable-during-recovery test."
                    + (locomotion.Error != null ? " " + locomotion.Error.Message : string.Empty));
            if (Vector3.Distance(anchor, performer.transform.position) > performer.LocomotionProfile.ArrivalPositionTolerance + 0.005f
                || Vector3.Angle(Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up), forward)
                    > performer.LocomotionProfile.ArrivalHeadingTolerance + 0.1f)
                failures.Add("Acceptance cleanup did not return Lara to the pre-disable location after the recovery teardown test.");
        }

        private static void CheckActionCommandRejected(Action command, string name, List<string> failures)
        {
            try
            {
                command();
                failures.Add("P0.Perform did not reject " + name + " while the Action owned Lara.");
            }
            catch (InvalidOperationException) { }
            catch (Exception exception)
            {
                failures.Add("P0.Perform rejected " + name + " with the wrong exception: " + exception.Message);
            }
        }

        private async void ObserveActionCompletion(ActionCompletionObservation observation, PerformerAction action)
        {
            try { observation.Result = await performer.PerformAsync(action); }
            catch (Exception exception) { observation.Error = exception; }
            finally { observation.Completed = true; }
        }

        private async void ObserveLocomotionCompletion(LocomotionCompletionObservation observation, Transform target)
        {
            try { observation.Result = await performer.WalkToAsync(target); }
            catch (Exception exception) { observation.Error = exception; }
            finally { observation.Completed = true; }
        }

        private Transform[] FindGestureProtectedBones(Transform animatorRoot, List<string> failures)
        {
            var result = new List<Transform>();
            foreach (string boneName in new[] { "pelvis", "lThighBend", "rThighBend", "lShin", "rShin", "lFoot", "rFoot" })
            {
                Transform[] matches = animatorRoot.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone.name == boneName).ToArray();
                if (matches.Length != 1)
                    failures.Add("P0.Gesture could not uniquely resolve protected lower-body bone '" + boneName + "'.");
                else result.Add(matches[0]);
            }
            return result.ToArray();
        }

        private void CheckGestureRootAndBaseState(Vector3 rootPosition, Quaternion rootRotation,
            PerformerPose desiredPose, PerformerPose settledPose, PerformerExpression desiredExpression,
            bool hadGaze, Vector3 gazeTargetPosition, Transform[] lowerBody, Vector3[] lowerPositions,
            Quaternion[] lowerRotations, List<string> failures, string phase)
        {
            if (Vector3.Distance(rootPosition, performer.transform.position) > PositionTolerance
                || Quaternion.Angle(rootRotation, performer.transform.rotation) > RotationToleranceDegrees)
                failures.Add("P0.Gesture changed actor-root position or rotation during " + phase + ".");
            if (performer.DesiredPose != desiredPose || performer.SettledPose != settledPose)
                failures.Add("P0.Gesture changed persistent DesiredPose or SettledPose during " + phase + ".");
            if (performer.DesiredExpression != desiredExpression)
                failures.Add("P0.Gesture changed the independent expression during " + phase + ".");
            if (performer.HasGazeTarget != hadGaze
                || (hadGaze && Vector3.Distance(performer.RawGazeTargetPosition, gazeTargetPosition) > PositionTolerance))
                failures.Add("P0.Gesture changed the independent gaze target during " + phase + ".");
            for (int i = 0; i < lowerBody.Length; i++)
                if (Vector3.Distance(lowerBody[i].localPosition, lowerPositions[i]) > PositionTolerance
                    || Quaternion.Angle(lowerBody[i].localRotation, lowerRotations[i]) > RotationToleranceDegrees)
                    failures.Add("P0.Gesture changed protected lower-body bone '" + lowerBody[i].name + "' during " + phase + ".");
        }

        private async void ObserveGestureCompletion(GestureCompletionObservation observation, PerformerGesture gesture)
        {
            try { observation.Result = await performer.GestureAsync(gesture); }
            catch (Exception exception) { observation.Error = exception; }
            finally { observation.Completed = true; }
        }

        private static void CheckTurnTo(bool condition, string description, List<string> failures)
        {
            if (!condition) failures.Add("P0.TurnTo: " + description + ".");
        }

        private async void ObserveTurnToCompletion(TurnToCompletionObservation observation, Transform target)
        {
            try { observation.Result = await performer.TurnToAsync(target); }
            catch (Exception exception) { observation.Error = exception; }
            finally { observation.Completed = true; }
        }

        private IEnumerator CheckDissolveIntegrationAcceptance(List<string> failures)
        {
            if (!performer.DissolveAvailable)
            {
                if (performer.gameObject.scene.name == "FirstPerformanceVoid")
                    failures.Add("P0.G3 DissolveTo is unavailable in FirstPerformanceVoid. Install/validate the native dissolve shaders and particle-body harness first.");
                else
                    Debug.Log("P0.G3 scene integration checks skipped because this scene has no installed dissolve particle body.", this);
                yield break;
            }

            PerformerDissolveRig rig = performer.GetComponent<PerformerDissolveRig>();
            PerformerParticleBody body = rig != null ? rig.ParticleBody : null;
            SkinnedMeshRenderer renderer = performer.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (rig == null || body == null || renderer == null)
            {
                failures.Add("P0.G3 integration checks require the dissolve rig, its particle body, and Lara's skinned renderer.");
                yield break;
            }
            if (performer.gameObject.scene.name == "FirstPerformanceVoid"
                && (performer.VisibilityState != PerformerVisibilityState.Visible || performer.IsHidden
                    || renderer.forceRenderingOff))
                failures.Add("P0.H FirstPerformanceVoid must explicitly start with stable Visible visibility.");
            if (body.BindingCount != PerformerSurfaceBindingAsset.RequiredBindingCount)
            {
                failures.Add("P0.G3 integration check found " + body.BindingCount + " particle bindings instead of 32,768.");
                yield break;
            }
            if (body.IsActive) body.Dispose();

            Vector3 initialPosition = performer.transform.position;
            Quaternion initialRotation = performer.transform.rotation;
            PerformerPose originalPose = performer.DesiredPose;
            PerformerExpression originalExpression = performer.DesiredExpression;
            float originalExpressionIntensity = performer.DesiredExpressionIntensity;
            bool originalGaze = performer.HasGazeTarget;
            Vector3 originalGazeTarget = performer.RawGazeTargetPosition;
            bool originalUpdateWhenOffscreen = renderer.updateWhenOffscreen;
            Material[] materialsBefore = renderer.sharedMaterials;
            PerformerPose[] arrivalPoses = { poseA, poseB };

            for (int cycle = 0; cycle < arrivalPoses.Length; cycle++)
            {
                PerformerPose arrivalPose = arrivalPoses[cycle];
                DissolveCompletionObservation observation = StartDissolveRequest(initialPosition, arrivalPose);
                if (!performer.IsDissolving || !body.IsActive)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not allocate its live particle body synchronously.");
                if (Mathf.Abs(ReadDissolveProperty(renderer, "_DissolveEnabled") - 1f) > 0.0001f)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not enable the permanent dissolve shader through its property block.");
                if (!renderer.updateWhenOffscreen)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not enable offscreen skinned-mesh updates for the live particle sampler.");

                bool reportedInactiveDuringRun = false;
                bool materialReferenceFailureReported = false;
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!observation.Completed && Time.realtimeSinceStartup < deadline)
                {
                    if (performer.IsDissolving && !body.IsActive && !reportedInactiveDuringRun)
                    {
                        failures.Add("P0.G3 cycle " + (cycle + 1) + " lost its particle body before materialization completed.");
                        reportedInactiveDuringRun = true;
                    }
                    if (!materialReferenceFailureReported && !SameMaterialReferences(materialsBefore, renderer.sharedMaterials))
                    {
                        CheckMaterialReferences("P0.G3 cycle " + (cycle + 1), materialsBefore, renderer.sharedMaterials, failures);
                        materialReferenceFailureReported = true;
                    }
                    yield return null;
                }

                if (!observation.Completed)
                {
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not complete within eight seconds.");
                    performer.enabled = false;
                    yield return null;
                    performer.enabled = true;
                    yield return null;
                    yield break;
                }
                if (observation.Error != null)
                {
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " failed: " + observation.Error.GetType().Name + ": " + observation.Error.Message);
                    yield break;
                }
                if (observation.Result != DissolveCompletion.Arrived)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " completed as " + observation.Result + " instead of Arrived.");
                if (performer.IsDissolving || body.IsActive)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not dispose the particle body at completion.");
                if (Mathf.Abs(ReadDissolveProperty(renderer, "_DissolveEnabled")) > 0.0001f
                    || Mathf.Abs(ReadDissolveProperty(renderer, "_DissolveProgress")) > 0.0001f)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " left nonzero dissolve shader state after completion.");
                if (renderer.updateWhenOffscreen != originalUpdateWhenOffscreen)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not restore SkinnedMeshRenderer.updateWhenOffscreen.");
                if (!materialReferenceFailureReported)
                    CheckMaterialReferences("P0.G3 cycle " + (cycle + 1) + " completion", materialsBefore, renderer.sharedMaterials, failures);
                if (performer.DesiredPose != arrivalPose || performer.SettledPose != arrivalPose)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " did not keep the requested arrival pose as ordinary persistent pose state.");
                if (performer.DesiredExpression != originalExpression
                    || Mathf.Abs(performer.DesiredExpressionIntensity - originalExpressionIntensity) > 0.0001f
                    || performer.HasGazeTarget != originalGaze
                    || (originalGaze && Vector3.Distance(performer.RawGazeTargetPosition, originalGazeTarget) > 0.0001f))
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " changed expression or gaze state.");
                if (Vector3.Distance(performer.transform.position, initialPosition) > PositionTolerance
                    || Quaternion.Angle(performer.transform.rotation, initialRotation) > RotationToleranceDegrees)
                    failures.Add("P0.G3 cycle " + (cycle + 1) + " changed the requested root position or preserved facing.");
            }

            if (originalPose != null && performer.DesiredPose != originalPose)
            {
                performer.Pose(originalPose, PoseTransition.Snap);
                yield return null;
            }
        }

        private DissolveCompletionObservation StartDissolveRequest(Vector3 position, PerformerPose arrivalPose)
        {
            var observation = new DissolveCompletionObservation();
            try
            {
                var awaiter = performer.DissolveToAsync(position, arrivalPose).GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    try { observation.Result = awaiter.GetResult(); }
                    catch (Exception exception) { observation.Error = exception; }
                    finally { observation.Completed = true; }
                });
            }
            catch (Exception exception)
            {
                observation.Error = exception;
                observation.Completed = true;
            }
            return observation;
        }

        private static void CheckMaterialReferences(string description, Material[] expected, Material[] actual,
            List<string> failures)
        {
            if (expected == null || actual == null || expected.Length != actual.Length)
            {
                failures.Add(description + " changed Lara's renderer material slot count.");
                return;
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] == actual[i]) continue;
                failures.Add(description + " replaced renderer material reference in slot " + i + ".");
                return;
            }
        }

        private static bool SameMaterialReferences(Material[] expected, Material[] actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (expected[i] != actual[i]) return false;
            return true;
        }

        private static float ReadDissolveProperty(SkinnedMeshRenderer renderer, string propertyName)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetFloat(Shader.PropertyToID(propertyName));
        }

        private sealed class DissolveCompletionObservation
        {
            public bool Completed;
            public DissolveCompletion Result;
            public Exception Error;
        }

        private async void RunExpressionAwaitableChecks(List<string> failures)
        {
            try
            {
                var first = StartExpressionRequest(expressionA, 1f, 0.4f);
                var joined = StartExpressionRequest(expressionA, 1f, 1f);
                await Awaitable.NextFrameAsync();
                var replacement = StartExpressionRequest(expressionB, 0.75f, 0.2f);
                await WaitForExpressionCompletion(first, 4f, failures, "superseded Expression waiter");
                await WaitForExpressionCompletion(joined, 4f, failures, "joined Expression waiter");
                await WaitForExpressionCompletion(replacement, 4f, failures, "replacement Expression waiter");
                if (first.Result != ExpressionCompletion.Superseded || joined.Result != ExpressionCompletion.Superseded
                    || replacement.Result != ExpressionCompletion.Settled)
                    failures.Add("ExpressionAsync latest-intention completion results were incorrect.");

                var immediate = await performer.ExpressionAsync(expressionB, 0.75f, 1f);
                if (immediate != ExpressionCompletion.Settled) failures.Add("Settled same-state ExpressionAsync was not immediate Settled.");

                var pendingForClear = StartExpressionRequest(expressionA, 1f, 0.5f);
                await Awaitable.NextFrameAsync();
                var clear = StartExpressionRequest(null, 0f, 0.2f);
                await WaitForExpressionCompletion(pendingForClear, 4f, failures, "Expression superseded by ClearExpression");
                await WaitForExpressionCompletion(clear, 4f, failures, "ClearExpression waiter");
                if (pendingForClear.Result != ExpressionCompletion.Superseded || clear.Result != ExpressionCompletion.Settled)
                    failures.Add("ClearExpression did not supersede the previous waiter and settle its own waiter.");

                var disabled = StartExpressionRequest(expressionC, 1f, 1f);
                performer.enabled = false;
                if (!disabled.Completed || disabled.Result != ExpressionCompletion.PerformerDisabled)
                    failures.Add("Disabling did not complete ExpressionAsync as PerformerDisabled.");
                performer.enabled = true;
                await Awaitable.NextFrameAsync();
                if (performer.DesiredExpression != expressionC || performer.IsExpressionTransitioning)
                    failures.Add("Re-enable did not restore the last desired Expression snapped.");
            }
            catch (Exception exception) { failures.Add("ExpressionAsync checks threw " + exception.GetType().Name + ": " + exception.Message); }
            finally { _expressionAwaitableChecksComplete = true; }
        }

        private ExpressionCompletionObservation StartExpressionRequest(PerformerExpression expression, float intensity, float blendTime)
        {
            var observation = new ExpressionCompletionObservation(); ObserveExpressionRequest(observation, expression, intensity, blendTime); return observation;
        }

        private async void ObserveExpressionRequest(ExpressionCompletionObservation observation, PerformerExpression expression, float intensity, float blendTime)
        {
            try { observation.Result = await performer.ExpressionAsync(expression, intensity, blendTime); }
            catch (Exception exception) { observation.Error = exception; }
            finally { observation.Completed = true; }
        }

        private static async Awaitable WaitForExpressionCompletion(ExpressionCompletionObservation observation,
            float timeout, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!observation.Completed && Time.realtimeSinceStartup < deadline) await Awaitable.NextFrameAsync();
            if (!observation.Completed) failures.Add(description + " did not resolve before timeout.");
            else if (observation.Error != null) failures.Add(description + " threw " + observation.Error.Message);
        }

        private void CheckExpressionTargets(string label, PerformerExpression expression, float intensity, List<string> failures)
        {
            foreach (var channel in expression.Channels)
            {
                if (!TryGetBlendShapeWeight(channel, out var actual)) { failures.Add(label + " could not resolve " + channel.RendererPath + "|" + channel.BlendShapeName + "."); continue; }
                if (intensity >= 0.999f && Mathf.Abs(actual - channel.TargetWeight) > BlendShapeTolerance)
                    failures.Add(label + " expected " + channel.TargetWeight + " but found " + actual + " for " + channel.BlendShapeName + ".");
            }
        }

        private Dictionary<string, float> CaptureExpressionChannels(params PerformerExpression[] expressions)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var channel in expressions.SelectMany(expression => expression.Channels))
                if (TryGetBlendShapeWeight(channel, out var value)) result[channel.RendererPath + "|" + channel.BlendShapeName] = value;
            return result;
        }

        private bool TryGetBlendShapeWeight(PerformerExpressionChannel channel, out float value)
        {
            value = 0f;
            var renderer = performer.GetComponent<Animator>().transform.Find(channel.RendererPath)?.GetComponent<SkinnedMeshRenderer>();
            var index = renderer == null || renderer.sharedMesh == null ? -1 : renderer.sharedMesh.GetBlendShapeIndex(channel.BlendShapeName);
            if (index < 0) return false;
            value = renderer.GetBlendShapeWeight(index); return true;
        }

        private static void CheckFloatMap(string label, Dictionary<string, float> expected, Dictionary<string, float> actual, List<string> failures)
        {
            foreach (var pair in expected)
                if (!actual.TryGetValue(pair.Key, out var value) || Mathf.Abs(value - pair.Value) > BlendShapeTolerance)
                    failures.Add(label + " changed " + pair.Key + " at retarget.");
        }

        private IEnumerator CheckBreathingAcceptance(Animator animator, PoseSnapshot stateA,
            PoseSnapshot stateB, PoseSnapshot stateC, PlacementSnapshot startPlacement,
            List<string> failures)
        {
            var breathing = performer.BreathingRuntime;
            if (breathing == null)
            {
                failures.Add("PerformerBreathing runtime is missing.");
                yield break;
            }

            if (breathing.BreatheBindingCount == 0)
                failures.Add("Canonical Lara is missing the Breathe morph binding. " + string.Join(" ", breathing.Diagnostics));
            if (breathing.BreatheBellyBindingCount == 0)
                failures.Add("Canonical Lara is missing the BreatheBelly morph binding. " + string.Join(" ", breathing.Diagnostics));
            if (breathing.BoneBindings.Count == 0)
                failures.Add("No configured torso breathing bones resolved. " + string.Join(" ", breathing.Diagnostics));

            performer.BreathingEnabled = true;
            performer.MorphBreathingEnabled = true;
            performer.MorphBreathingStrength = 1f;
            performer.BreatheStrength = 1f;
            performer.BreatheBellyStrength = 0.7f;
            performer.BoneBreathingEnabled = false;
            performer.BoneBreathingStrength = 1f;
            performer.Pose(poseA, PoseTransition.Snap);

            performer.SetBreathPhaseForAcceptance(0f);
            var restBase = performer.CaptureEvaluatedBasePoseState();
            CheckPose("Breathing at rest must equal the base pose", ToPoseSnapshot(restBase),
                CapturePose(animator), failures);

            performer.SetBreathPhaseForAcceptance(0.32f);
            var morphInhaleBase = performer.CaptureEvaluatedBasePoseState();
            var inhaleContributions = CaptureMorphContributions(breathing.MorphBindings, morphInhaleBase);
            CheckMorphContributions("Morph-only inhale", breathing.MorphBindings, inhaleContributions,
                true, failures);
            CheckBreathingBonesEqualBase("Morph-only mode", breathing.BoneBindings,
                morphInhaleBase, failures);

            performer.SetBreathPhaseForAcceptance(0.70f);
            var morphExhaleBase = performer.CaptureEvaluatedBasePoseState();
            var exhaleContributions = CaptureMorphContributions(breathing.MorphBindings, morphExhaleBase);
            CheckMorphContributions("Morph-only exhale", breathing.MorphBindings, exhaleContributions,
                true, failures);
            if (inhaleContributions.Length != exhaleContributions.Length)
                failures.Add("Morph bindings changed between breath phases.");
            else
                for (var index = 0; index < inhaleContributions.Length; index++)
                    if (Mathf.Abs(inhaleContributions[index] - exhaleContributions[index]) < 0.05f)
                    {
                        failures.Add("Morph binding " + index + " did not change between two breath phases.");
                        break;
                    }

            performer.MorphBreathingEnabled = false;
            performer.BoneBreathingEnabled = true;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var boneOnlyBase = performer.CaptureEvaluatedBasePoseState();
            CheckMorphsEqualBase("Bone-only mode", breathing.MorphBindings, boneOnlyBase, failures);
            if (!CheckBreathingBonesDifferFromBase("Bone-only mode", breathing.BoneBindings,
                    boneOnlyBase, failures))
                failures.Add("Bone-only breathing did not move any configured torso bone.");

            performer.MorphBreathingEnabled = true;
            performer.BoneBreathingEnabled = true;
            performer.MorphBreathingStrength = 1f;
            performer.BoneBreathingStrength = 1f;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var combinedBase = performer.CaptureEvaluatedBasePoseState();
            CheckExpectedMorphContributions("Combined mode", breathing.MorphBindings,
                combinedBase, performer, failures);
            CheckExpectedBoneContributions("Combined mode", breathing.BoneBindings,
                combinedBase, performer, failures);
            var expectedBreathValue = Mathf.Clamp01(performer.BreathingCurve.Evaluate(performer.BreathPhase));
            if (Mathf.Abs(expectedBreathValue - performer.BreathValue) > 0.0001f)
                failures.Add("Morph and bone breathing do not share the evaluated value for the current phase.");

            yield return CheckBreathingRetargetIsolation(animator, stateA, stateB, stateC, failures);

            performer.BreathingEnabled = false;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var disabledBase = performer.CaptureEvaluatedBasePoseState();
            CheckPose("Master breathing switch bypass", ToPoseSnapshot(disabledBase),
                CapturePose(animator), failures);

            performer.BreathingEnabled = true;
            performer.MorphBreathingStrength = 0f;
            performer.BoneBreathingStrength = 0f;
            performer.SetBreathPhaseForAcceptance(0.32f);
            var zeroStrengthBase = performer.CaptureEvaluatedBasePoseState();
            CheckPose("Zero breathing strengths bypass", ToPoseSnapshot(zeroStrengthBase),
                CapturePose(animator), failures);

            if (performer.DesiredPose != performer.SettledPose || performer.IsTransitioning)
                failures.Add("Continuous breathing changed the persistent pose state or transition status.");
            CheckPlacement("Breathing root placement", startPlacement, CapturePlacement(transform.root), failures);

            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
        }

        private IEnumerator CheckBreathingRetargetIsolation(Animator animator, PoseSnapshot stateA,
            PoseSnapshot stateB, PoseSnapshot stateC, List<string> failures)
        {
            var breathing = performer.BreathingRuntime;
            var midPoses = new[] { poseB, poseA, poseC };
            var nextPoses = new[] { poseC, poseB, poseA };
            var nextPoseStates = new[] { stateC, stateB, stateA };

            performer.Pose(poseA, PoseTransition.Snap);
            performer.SetBreathPhaseForAcceptance(0.32f);
            yield return null;

            for (var index = 0; index < midPoses.Length; index++)
            {
                performer.Pose(midPoses[index], PoseTransition.Smooth(1f));
                var deadline = Time.realtimeSinceStartup + 4f;
                while (performer.IsTransitioning && performer.TransitionProgress < 0.38f
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!performer.IsTransitioning || performer.TransitionProgress < 0.38f)
                {
                    failures.Add("Breathing retarget setup did not reach its interruption point.");
                    yield break;
                }

                var phaseBeforeRetarget = performer.BreathPhase;
                var evaluatedBase = performer.CaptureEvaluatedBasePoseState();
                var renderedBefore = CapturePose(animator);
                if (CheckPoseDifference(ToPoseSnapshot(evaluatedBase), renderedBefore) < 0.0001f)
                    failures.Add("Breathing was not visible during the base-state isolation check.");

                performer.Pose(nextPoses[index], new PoseTransition(0.45f, 0.06f, 0.08f,
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
                CheckPose("Retarget source must be evaluated base only", ToPoseSnapshot(evaluatedBase),
                    ToPoseSnapshot(performer.CaptureTransitionSourcePoseState()), failures);
                CheckPose("Retarget continuity with downstream breathing", renderedBefore,
                    CapturePose(animator), failures);
                if (Mathf.Abs(phaseBeforeRetarget - performer.BreathPhase) > 0.0001f)
                    failures.Add("A pose retarget reset or advanced the breath phase synchronously.");

                yield return null;
                var phaseDelta = Mathf.Repeat(performer.BreathPhase - phaseBeforeRetarget, 1f);
                if (phaseDelta <= 0.0001f || phaseDelta > 0.2f)
                    failures.Add("The breath phase did not continue smoothly after a pose retarget.");
                yield return WaitForSettlement(6f, failures, "breathing retarget to " + nextPoses[index].name);
                CheckPose("Retarget base pose must not accumulate breathing offsets",
                    nextPoseStates[index], ToPoseSnapshot(performer.CaptureEvaluatedBasePoseState()), failures);
            }

            if (breathing.BreatheBindingCount == 0 || breathing.BreatheBellyBindingCount == 0)
                failures.Add("Retarget isolation did not exercise both requested morph channels.");
        }

        private IEnumerator CheckGazeAcceptance(Animator animator, PlacementSnapshot startPlacement,
            List<string> failures)
        {
            var gaze = performer.GazeRuntime;
            if (gaze == null)
            {
                failures.Add("PerformerGaze runtime is missing.");
                yield break;
            }
            if (!gaze.IsAvailable)
            {
                failures.Add("Canonical Lara gaze bindings failed: " + string.Join(" ", gaze.Diagnostics));
                yield break;
            }
            if (gazeTarget == null)
            {
                failures.Add("Assign the scene Gaze Target to the acceptance harness.");
                yield break;
            }

            CheckGazeCalibration("head", gaze.HeadCalibration, "head", failures);
            CheckGazeCalibration("left eye", gaze.LeftEyeCalibration, "lEye", failures);
            CheckGazeCalibration("right eye", gaze.RightEyeCalibration, "rEye", failures);

            performer.Pose(poseA, PoseTransition.Snap);
            performer.ClearGaze();
            performer.GazeEnabled = false;
            yield return null;
            var baseGazePose = CaptureGazePose(gaze);
            var firstTarget = gaze.CreateValidationTarget(18f, 7f, 2.5f);
            performer.LookAt(firstTarget);
            for (var frame = 0; frame < 3; frame++) yield return null;
            CheckGazePose("Master gaze bypass", baseGazePose, CaptureGazePose(gaze), false, false, false, failures);
            if (!performer.HasGazeTarget)
                failures.Add("Disabling the gaze development switch erased the semantic target.");

            performer.GazeEnabled = true;
            performer.HeadGazeEnabled = false;
            performer.HeadGazeWeight = 0f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 1f;
            yield return WaitForGazeAcquisition(6f, failures, "eye-only target");
            CheckGazePose("Eye-only mode", baseGazePose, CaptureGazePose(gaze), false, true, true, failures);
            yield return ReleaseGazeToBase(baseGazePose, gaze, failures, "eye-only release");

            performer.HeadGazeEnabled = true;
            performer.HeadGazeWeight = 1f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 0f;
            performer.LookAt(gaze.CreateValidationTarget(-27f, 8f, 2.5f));
            yield return WaitForGazeAcquisition(6f, failures, "head-only target");
            CheckGazePose("Head-only mode", baseGazePose, CaptureGazePose(gaze), true, false, false, failures);
            yield return ReleaseGazeToBase(baseGazePose, gaze, failures, "head-only release");

            performer.HeadGazeEnabled = true;
            performer.HeadGazeWeight = 0.7f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 1f;
            performer.LookAt(gaze.CreateValidationTarget(25f, -9f, 2.5f));
            yield return WaitForGazeAcquisition(6f, failures, "combined head and eye target");
            CheckGazePose("Combined gaze", baseGazePose, CaptureGazePose(gaze), true, true, true, failures);

            var movingTargetStart = gaze.CreateValidationTarget(-20f, 2f, 2.5f);
            gazeTarget.position = movingTargetStart;
            performer.LookAt(gazeTarget);
            yield return WaitForGazeAcquisition(6f, failures, "moving Transform target");
            var previousSmoothedDirection = gaze.SmoothedHeadAimDirection;
            var movingTargetEnd = gaze.CreateValidationTarget(38f, 14f, 2.5f);
            gazeTarget.position = movingTargetEnd;
            var errorBeforeMove = Vector3.Angle(previousSmoothedDirection,
                (movingTargetEnd - gaze.HeadCalibration.Bone.position).normalized);
            for (var frame = 0; frame < 18; frame++) yield return null;
            var errorAfterMove = Vector3.Angle(gaze.SmoothedHeadAimDirection,
                (movingTargetEnd - gaze.HeadCalibration.Bone.position).normalized);
            if (Vector3.Distance(performer.RawGazeTargetPosition, movingTargetEnd) > 1e-4f
                || errorAfterMove >= errorBeforeMove - 0.5f)
                failures.Add("The persistent Transform gaze did not follow its moved target through the smoothed head aim.");

            var fixedPoint = gaze.CreateValidationTarget(-12f, 5f, 2.2f);
            performer.LookAt(fixedPoint);
            yield return WaitForGazeAcquisition(6f, failures, "fixed world target");
            gazeTarget.position = gaze.CreateValidationTarget(45f, -10f, 2.5f);
            for (var frame = 0; frame < 4; frame++) yield return null;
            if (Vector3.Distance(performer.RawGazeTargetPosition, fixedPoint) > 1e-5f)
                failures.Add("LookAt(Vector3) changed when an unrelated Transform moved.");

            performer.GazeEnabled = false;
            yield return null;
            CheckGazePose("Master bypass with an active target", baseGazePose,
                CaptureGazePose(gaze), false, false, false, failures);
            if (!performer.HasGazeTarget)
                failures.Add("The master gaze switch cleared a persistent target.");

            performer.GazeEnabled = true;
            performer.ClearGaze();
            yield return WaitForGazeRelease(3f);
            if (performer.GazeWeight > 0.01f)
                failures.Add("ClearGaze did not release gaze influence to zero.");
            CheckGazePose("ClearGaze returns to authored pose", baseGazePose,
                CaptureGazePose(gaze), false, false, false, failures);

            var farTarget = gaze.CreateValidationTarget(165f, 18f, 3f);
            var requestedHeadDirection = (farTarget - gaze.HeadCalibration.Bone.position).normalized;
            var headAim = (gaze.HeadCalibration.Bone.rotation * gaze.HeadCalibration.LocalAim).normalized;
            var headUp = (gaze.HeadCalibration.Bone.rotation * gaze.HeadCalibration.LocalUp).normalized;
            var headRight = Vector3.Cross(headUp, headAim).normalized;
            headUp = Vector3.Cross(headAim, headRight).normalized;
            var rawHeadYaw = Mathf.Atan2(Vector3.Dot(requestedHeadDirection, headRight),
                Vector3.Dot(requestedHeadDirection, headAim)) * Mathf.Rad2Deg;
            var clampedHeadAngles = PerformerGazeJob.ClampAimAngles(headAim, headRight, headUp,
                requestedHeadDirection, performer.HeadGazeMaxYaw, performer.HeadGazeMaxPitch);
            if (Mathf.Abs(rawHeadYaw) <= performer.HeadGazeMaxYaw
                || Mathf.Abs(clampedHeadAngles.x) > performer.HeadGazeMaxYaw + 0.01f
                || Mathf.Abs(clampedHeadAngles.y) > performer.HeadGazeMaxPitch + 0.01f)
                failures.Add("Behind-target head yaw/pitch did not clamp to the configured anatomical limits.");
            performer.LookAt(farTarget);
            yield return WaitForGazeAcquisition(8f, failures, "behind-the-performer target");
            CheckPlacement("Gaze limits must preserve outer performer placement", startPlacement,
                CapturePlacement(transform.root), failures);

            var beforePhase = performer.BreathPhase;
            var breathingWasEnabled = performer.BreathingEnabled;
            performer.BreathingEnabled = true;
            performer.LookAt(gaze.CreateValidationTarget(8f, 2f, 2.5f));
            for (var frame = 0; frame < 12; frame++) yield return null;
            if (Mathf.Repeat(performer.BreathPhase - beforePhase, 1f) <= 0.001f)
                failures.Add("Breathing phase did not continue while gaze was active.");
            performer.BreathingEnabled = breathingWasEnabled;

            performer.Pose(poseA, PoseTransition.Snap);
            performer.LookAt(gaze.CreateValidationTarget(-10f, 4f, 2.5f));
            yield return WaitForGazeAcquisition(6f, failures, "gaze before pose retarget");
            performer.Pose(poseB, PoseTransition.Smooth(1.2f));
            var deadline = Time.realtimeSinceStartup + 4f;
            while (performer.IsTransitioning && performer.TransitionProgress < 0.38f
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!performer.IsTransitioning || performer.TransitionProgress < 0.38f)
                failures.Add("Gaze/base-state isolation setup did not reach the pose interruption point.");
            else
            {
                var capturedBase = performer.CaptureEvaluatedBasePoseState();
                var targetBeforeRetarget = performer.GazeTargetDescription;
                performer.Pose(poseC, new PoseTransition(0.45f, 0.06f, 0.08f,
                    AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
                CheckPose("Gaze must not contaminate the base transition source",
                    ToPoseSnapshot(capturedBase), ToPoseSnapshot(performer.CaptureTransitionSourcePoseState()), failures);
                if (!performer.HasGazeTarget || performer.GazeTargetDescription != targetBeforeRetarget)
                    failures.Add("Pose retargeting replaced the persistent gaze intention.");
                yield return WaitForSettlement(6f, failures, "gaze-active pose retarget");
            }

            performer.ClearGaze();
            yield return WaitForGazeRelease(3f);
            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
        }

        private IEnumerator CheckAttentionAndBlinkAcceptance(List<string> failures)
        {
            var gaze = performer.GazeRuntime;
            var life = performer.AttentionLifeRuntime;
            var blink = performer.BlinkRuntime;
            if (gaze == null || !gaze.IsAvailable || life == null || blink == null)
            {
                failures.Add("P0.7 acceptance needs active PerformerGaze, PerformerAttentionLife, and PerformerBlink runtimes.");
                yield break;
            }

            if (!blink.IsAvailable)
                failures.Add("Canonical Lara blink bindings are missing: " + string.Join(" ", blink.Diagnostics));
            foreach (var binding in blink.Bindings)
            {
                if (binding.PositiveMaximumWeight <= 0f)
                    failures.Add("Blink morph '" + binding.ImportedBlendShapeName + "' has no positive full-closure weight.");
                Debug.Log("P0.7 blink binding: " + binding.SemanticName + " = "
                          + binding.ImportedBlendShapeName + " @ " + binding.RendererPath
                          + ", positive frame max " + binding.PositiveMaximumWeight.ToString("F3") + ".", this);
            }

            var arbitraryBase = 23.75f;
            if (PerformerBlink.ComposeWeight(arbitraryBase, 100f, 0f) != arbitraryBase)
                failures.Add("Blink closure zero did not exactly preserve an incoming eyelid weight.");
            if (Mathf.Abs(PerformerBlink.ComposeWeight(20f, 100f, 0.5f) - 60f) > 0.0001f
                || PerformerBlink.ComposeWeight(20f, 100f, 1f) != 100f)
                failures.Add("Blink composition did not move the incoming eyelid weight toward full closure.");

            CheckDeterministicAttentionEvents(failures);

            performer.AttentionLifeEnabled = false;
            performer.BlinkEnabled = false;
            performer.GazeEnabled = true;
            performer.HeadGazeEnabled = true;
            performer.HeadGazeWeight = 0.7f;
            performer.EyeGazeEnabled = true;
            performer.EyeGazeWeight = 1f;
            performer.HeadGazeResponse = 20f;
            performer.EyeGazeResponse = 20f;
            performer.GazeAcquireToleranceDegrees = 5f;
            var target = new GameObject("P0.7 Attention Acceptance Target");
            target.transform.position = gaze.CreateValidationTarget(12f, 4f, 3f);
            performer.LookAt(target.transform);
            yield return null;
            var rawTarget = performer.RawGazeTargetPosition;
            if (Vector3.Distance(performer.EffectiveHeadTargetPosition, rawTarget) > 1e-5f
                || Vector3.Distance(performer.EffectiveEyeTargetPosition, rawTarget) > 1e-5f
                || Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.001f)
                failures.Add("Attention Life OFF did not reproduce P0.6 raw targets and identity preferred-head bias.");

            performer.AttentionLifeEnabled = true;
            performer.EyeFixationLifeEnabled = true;
            performer.EyeFixationMaxHorizontalDegrees = 4f;
            performer.EyeFixationMaxVerticalDegrees = 3f;
            performer.EyeFixationCenterBias = 1f;
            performer.EyeFixationMinimumHoldSeconds = 0.1f;
            performer.EyeFixationMaximumHoldSeconds = 0.1f;
            performer.HeadAttentionLifeEnabled = false;
            performer.AttentionLifeSeed = 8181;
            performer.HeadGazeResponse = 0.1f;
            performer.EyeGazeResponse = 0.1f;
            performer.GazeAcquireToleranceDegrees = 0.1f;
            var fixationWaiter = StartGazeRequest(target.transform);
            var fixationGeneration = gaze.IntentionGeneration;
            var pendingWaiters = gaze.PendingWaiterCount;
            var fixationRaw = performer.RawGazeTargetPosition;
            if (pendingWaiters != 1)
                failures.Add("The fixation acceptance setup did not retain exactly one semantic gaze waiter.");
            var sawFixation = false;
            for (var frame = 0; frame < 30 && !sawFixation; frame++)
            {
                yield return null;
                sawFixation = Mathf.Abs(performer.EyeFixationHorizontalOffset) > 0.01f
                              || Mathf.Abs(performer.EyeFixationVerticalOffset) > 0.01f;
                if (gaze.IntentionGeneration != fixationGeneration
                    || gaze.PendingWaiterCount != pendingWaiters
                    || Vector3.Distance(performer.RawGazeTargetPosition, fixationRaw) > 1e-5f)
                    failures.Add("An eye fixation event changed semantic gaze generation, waiter count, or raw target.");
            }
            if (!sawFixation)
                failures.Add("The short-interval fixation acceptance setup did not generate a nonzero held event.");
            if (Vector3.Distance(performer.EffectiveHeadTargetPosition, fixationRaw) > 1e-5f
                || Vector3.Distance(performer.EffectiveEyeTargetPosition, fixationRaw) < 1e-4f)
                failures.Add("Fixation must move only the effective eye target while the head target stays raw.");
            performer.ClearGaze();
            if (!fixationWaiter.Completed || fixationWaiter.Result != GazeCompletion.Superseded)
                failures.Add("ClearGaze did not supersede the pending semantic waiter used by fixation acceptance.");
            yield return null;

            performer.EyeFixationLifeEnabled = false;
            performer.HeadAttentionLifeEnabled = true;
            performer.HeadAttentionMaxTiltDegrees = 10f;
            performer.HeadAttentionMaxChinDegrees = 8f;
            performer.HeadAttentionMinimumHoldSeconds = 0.1f;
            performer.HeadAttentionMaximumHoldSeconds = 0.1f;
            performer.HeadAttentionTransitionResponse = 3f;
            performer.AttentionLifeSeed = 9191;
            target.transform.position = gaze.CreateValidationTarget(0f, 0f, 3f);
            performer.LookAt(target.transform);
            var headGeneration = gaze.IntentionGeneration;
            var sawHeadBias = false;
            for (var frame = 0; frame < 120 && !sawHeadBias; frame++)
            {
                yield return null;
                sawHeadBias = Quaternion.Angle(gaze.EffectivePreferredHeadBias,
                    Quaternion.identity) > 0.1f;
            }
            if (!sawHeadBias)
                failures.Add("Head Attention Life did not produce a non-neutral preferred-head bias.");
            if (Vector3.Distance(performer.EffectiveHeadTargetPosition, performer.RawGazeTargetPosition) > 1e-5f
                || Vector3.Distance(performer.EffectiveEyeTargetPosition, performer.RawGazeTargetPosition) > 1e-5f)
                failures.Add("Head Attention Life changed raw semantic target positions.");
            if (sawHeadBias)
            {
                var leftAim = (gaze.LeftEyeCalibration.Bone.rotation * gaze.LeftEyeCalibration.LocalAim).normalized;
                var leftTarget = (performer.RawGazeTargetPosition - gaze.LeftEyeCalibration.Bone.position).normalized;
                if (Vector3.Angle(leftAim, leftTarget) > 8f)
                    failures.Add("Eyes did not compensate toward the semantic target while preferred head bias was active.");
            }

            var beforeAttentionDisableGeneration = gaze.IntentionGeneration;
            var beforeAttentionDisableTarget = performer.RawGazeTargetPosition;
            performer.AttentionLifeEnabled = false;
            var biasReleaseDeadline = Time.realtimeSinceStartup + 5f;
            while (Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f
                   && Time.realtimeSinceStartup < biasReleaseDeadline)
                yield return null;
            if (Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f)
                failures.Add("Disabling Attention Life did not fade preferred head bias back to identity.");
            if (!performer.HasGazeTarget || gaze.IntentionGeneration != beforeAttentionDisableGeneration
                || Vector3.Distance(performer.RawGazeTargetPosition, beforeAttentionDisableTarget) > 1e-5f)
                failures.Add("Disabling Attention Life changed the active semantic gaze intention.");

            performer.ClearGaze();
            var clearBiasDeadline = Time.realtimeSinceStartup + 5f;
            while (Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f
                   && Time.realtimeSinceStartup < clearBiasDeadline)
                yield return null;
            if (Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f)
                failures.Add("ClearGaze did not release preferred head bias to identity.");

            performer.AttentionLifeEnabled = true;
            performer.HeadAttentionLifeEnabled = true;
            performer.HeadAttentionMinimumHoldSeconds = 0.1f;
            performer.HeadAttentionMaximumHoldSeconds = 0.1f;
            performer.HeadAttentionTransitionResponse = 3f;
            performer.AttentionLifeSeed = 10101;
            var lostTarget = new GameObject("P0.7 Lost Target Acceptance Target");
            lostTarget.transform.position = gaze.CreateValidationTarget(0f, 0f, 3f);
            performer.LookAt(lostTarget.transform);
            sawHeadBias = false;
            for (var frame = 0; frame < 120 && !sawHeadBias; frame++)
            {
                yield return null;
                sawHeadBias = Quaternion.Angle(gaze.EffectivePreferredHeadBias,
                    Quaternion.identity) > 0.1f;
            }
            Destroy(lostTarget);
            for (var frame = 0; frame < 3 && performer.HasGazeTarget; frame++) yield return null;
            if (performer.HasGazeTarget)
                failures.Add("Destroying the active Transform did not lose semantic gaze while head life was active.");
            clearBiasDeadline = Time.realtimeSinceStartup + 5f;
            while (Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f
                   && Time.realtimeSinceStartup < clearBiasDeadline)
                yield return null;
            if (Quaternion.Angle(gaze.EffectivePreferredHeadBias, Quaternion.identity) > 0.01f)
                failures.Add("Target loss did not release preferred head bias to identity.");

            if (blink.IsAvailable)
            {
                var retarget = new GameObject("P0.7 Blink Retarget Acceptance Target");
                performer.AttentionLifeEnabled = false;
                performer.BlinkEnabled = true;
                performer.BlinkStrength = 1f;
                performer.GazeEnabled = true;
                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 20f;
                target.transform.position = gaze.CreateValidationTarget(0f, 0f, 3f);
                performer.LookAt(target.transform);
                var acquisitionDeadline = Time.realtimeSinceStartup + 6f;
                while (!performer.IsGazeAcquired && Time.realtimeSinceStartup < acquisitionDeadline)
                    yield return null;
                if (!performer.IsGazeAcquired)
                    failures.Add("Blink/gaze independence setup could not acquire its semantic target.");

                var baseState = performer.CaptureEvaluatedBasePoseState();
                blink.SetClosureForAcceptance(0f);
                yield return null;
                foreach (var binding in blink.Bindings)
                {
                    var incoming = baseState.BlendShapes[binding.BaseBlendShapeIndex];
                    var actual = binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex);
                    if (Mathf.Abs(actual - incoming) > BlendShapeTolerance)
                        failures.Add("Zero blink closure changed incoming eyelid state for '"
                                     + binding.ImportedBlendShapeName + "'.");
                }

                var generationBeforeBlink = gaze.IntentionGeneration;
                var acquiredBeforeBlink = performer.IsGazeAcquired;
                blink.SetClosureForAcceptance(1f);
                yield return null;
                foreach (var binding in blink.Bindings)
                {
                    var actual = binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex);
                    if (Mathf.Abs(actual - binding.PositiveMaximumWeight) > BlendShapeTolerance)
                        failures.Add("Full blink did not reach useful maximum weight for '"
                                     + binding.ImportedBlendShapeName + "'.");
                }
                if (performer.BlinkClosure < 0.999f || gaze.IntentionGeneration != generationBeforeBlink
                    || performer.IsGazeAcquired != acquiredBeforeBlink)
                    failures.Add("Blink changed gaze closure, intention generation, or acquisition semantics.");

                var generationBeforeClosedRetarget = gaze.IntentionGeneration;
                retarget.transform.position = gaze.CreateValidationTarget(-18f, 5f, 3f);
                performer.LookAt(retarget.transform);
                if (gaze.IntentionGeneration != generationBeforeClosedRetarget + 1
                    || performer.BlinkClosure < 0.999f)
                    failures.Add("Gaze could not retarget independently while blink was held closed.");
                acquisitionDeadline = Time.realtimeSinceStartup + 6f;
                while (!performer.IsGazeAcquired && Time.realtimeSinceStartup < acquisitionDeadline)
                    yield return null;
                if (!performer.IsGazeAcquired || performer.BlinkClosure < 0.999f)
                    failures.Add("Gaze did not reacquire under a closed blink contribution.");

                performer.ClearGaze();
                yield return null;
                if (performer.BlinkClosure < 0.999f)
                    failures.Add("ClearGaze canceled or altered an independent closed blink contribution.");

                performer.Pose(poseA, PoseTransition.Snap);
                yield return null;
                blink.SetClosureForAcceptance(1f);
                var poseDuringBlink = StartPoseRequest(poseB, PoseTransition.Smooth(0.15f));
                var poseDeadline = Time.realtimeSinceStartup + 5f;
                while (!poseDuringBlink.Completed && Time.realtimeSinceStartup < poseDeadline)
                    yield return null;
                if (!poseDuringBlink.Completed || poseDuringBlink.Error != null
                    || poseDuringBlink.Result != PoseCompletion.Settled || performer.SettledPose != poseB)
                    failures.Add("PoseAsync did not settle independently while blink was closed.");
                if (performer.BlinkClosure < 0.999f)
                    failures.Add("A base-pose transition restarted or altered blink closure.");

                blink.ClearClosureOverrideForAcceptance();
                performer.BlinkEnabled = false;
                var blinkOpenDeadline = Time.realtimeSinceStartup + 1f;
                while (performer.BlinkClosure > 0.001f && Time.realtimeSinceStartup < blinkOpenDeadline)
                    yield return null;
                if (performer.BlinkClosure > 0.001f)
                    failures.Add("Disabling Blink mid-closure left the eyelids closed.");
                Destroy(retarget);
            }

            yield return CheckP07PoseIsolation(target.transform, blink, failures);
            performer.ClearGaze();
            Destroy(target);
        }

        private IEnumerator CheckP07PoseIsolation(Transform target, PerformerBlink blink,
            List<string> failures)
        {
            performer.BreathingEnabled = true;
            performer.MorphBreathingEnabled = true;
            performer.MorphBreathingStrength = 3f;
            performer.BoneBreathingEnabled = true;
            performer.BoneBreathingStrength = 2f;
            performer.AttentionLifeEnabled = true;
            performer.EyeFixationLifeEnabled = true;
            performer.EyeFixationMaxHorizontalDegrees = 4f;
            performer.EyeFixationMaxVerticalDegrees = 3f;
            performer.EyeFixationMinimumHoldSeconds = 0.1f;
            performer.EyeFixationMaximumHoldSeconds = 0.1f;
            performer.HeadAttentionLifeEnabled = true;
            performer.HeadAttentionMaxTiltDegrees = 10f;
            performer.HeadAttentionMaxChinDegrees = 8f;
            performer.HeadAttentionMinimumHoldSeconds = 0.1f;
            performer.HeadAttentionMaximumHoldSeconds = 0.1f;
            performer.HeadAttentionTransitionResponse = 3f;
            performer.AttentionLifeSeed = 14711;
            performer.GazeEnabled = true;
            performer.BlinkEnabled = blink.IsAvailable;
            performer.Pose(poseA, PoseTransition.Snap);
            yield return null;
            performer.LookAt(target);
            for (var frame = 0; frame < 30; frame++) yield return null;
            if (blink.IsAvailable) blink.SetClosureForAcceptance(1f);

            performer.Pose(poseB, PoseTransition.Smooth(1f));
            var deadline = Time.realtimeSinceStartup + 3f;
            while (performer.IsTransitioning && performer.TransitionProgress < 0.35f
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!performer.IsTransitioning || performer.TransitionProgress < 0.35f)
            {
                failures.Add("P0.7 pose-contamination setup did not reach the interruption point.");
            }
            else
            {
                var evaluatedBase = performer.CaptureEvaluatedBasePoseState();
                performer.Pose(poseC, PoseTransition.Smooth(0.45f));
                CheckPose("Breathing, gaze, fixation, head bias, and blink must stay downstream of pose source",
                    ToPoseSnapshot(evaluatedBase),
                    ToPoseSnapshot(performer.CaptureTransitionSourcePoseState()), failures);
            }

            yield return WaitForSettlement(6f, failures, "P0.7 pose-contamination retarget");
            blink.ClearClosureOverrideForAcceptance();
            performer.BlinkEnabled = false;
            var blinkOpenDeadline = Time.realtimeSinceStartup + 1f;
            while (performer.BlinkClosure > 0.001f && Time.realtimeSinceStartup < blinkOpenDeadline)
                yield return null;
            performer.Pose(poseA, PoseTransition.Snap);
            performer.ClearGaze();
        }

        private static void CheckDeterministicAttentionEvents(List<string> failures)
        {
            var settings = new PerformerAttentionLifeSettings
            {
                Enabled = true,
                Seed = 246813,
                EyeFixationEnabled = true,
                EyeMaxHorizontalDegrees = 2.5f,
                EyeMaxVerticalDegrees = 1.5f,
                EyeMinimumHoldSeconds = 0.4f,
                EyeMaximumHoldSeconds = 0.8f,
                EyeCenterBias = 2.2f,
                HeadEnabled = true,
                HeadMaxTiltDegrees = 3f,
                HeadMaxChinDegrees = 2f,
                HeadMinimumHoldSeconds = 1f,
                HeadMaximumHoldSeconds = 2f,
                HeadTransitionResponse = 1.5f
            };
            var first = new PerformerAttentionLife(Vector3.forward, Vector3.right, settings);
            var second = new PerformerAttentionLife(Vector3.forward, Vector3.right, settings);
            first.SetGazeActive(true);
            second.SetGazeActive(true);
            var sawEyeEvent = false;
            var heldCheckPassed = true;
            var previousHorizontal = 0f;
            var previousVertical = 0f;
            var previousCountdown = 0f;
            for (var frame = 0; frame < 500; frame++)
            {
                first.Advance(0.05f);
                second.Advance(0.05f);
                var a = first.CurrentOutput;
                var b = second.CurrentOutput;
                if (Mathf.Abs(a.EyeHorizontalOffsetDegrees - b.EyeHorizontalOffsetDegrees) > 1e-6f
                    || Mathf.Abs(a.EyeVerticalOffsetDegrees - b.EyeVerticalOffsetDegrees) > 1e-6f
                    || Mathf.Abs(first.EyeEventCountdown - second.EyeEventCountdown) > 1e-6f
                    || Mathf.Abs(first.HeadEventCountdown - second.HeadEventCountdown) > 1e-6f)
                {
                    failures.Add("Attention-life event offsets or event times were not deterministic for a fixed seed.");
                    break;
                }

                if (previousCountdown > 0.051f
                    && (Mathf.Abs(a.EyeHorizontalOffsetDegrees - previousHorizontal) > 1e-6f
                        || Mathf.Abs(a.EyeVerticalOffsetDegrees - previousVertical) > 1e-6f))
                    heldCheckPassed = false;
                if (Mathf.Abs(a.EyeHorizontalOffsetDegrees) > 0.001f
                    || Mathf.Abs(a.EyeVerticalOffsetDegrees) > 0.001f)
                    sawEyeEvent = true;
                previousHorizontal = a.EyeHorizontalOffsetDegrees;
                previousVertical = a.EyeVerticalOffsetDegrees;
                previousCountdown = first.EyeEventCountdown;
            }
            if (!sawEyeEvent) failures.Add("Deterministic attention-life progression did not produce a nonzero fixation event.");
            if (!heldCheckPassed) failures.Add("Eye fixation changed between held events instead of remaining stable.");

            var authoredHead = Quaternion.Euler(7f, -11f, 3f);
            var gazeCorrection = Quaternion.Euler(-2f, 9f, 0f);
            var sideTiltBias = Quaternion.AngleAxis(10f, Vector3.forward);
            var gazeOnlyHead = PerformerGazeJob.ComposeHeadRotation(authoredHead, gazeCorrection,
                Quaternion.identity);
            var attendedHead = PerformerGazeJob.ComposeHeadRotation(authoredHead, gazeCorrection,
                sideTiltBias);
            if (Quaternion.Angle(gazeOnlyHead, attendedHead) < 9.9f)
                failures.Add("Head Attention Life side tilt was canceled by semantic gaze composition.");

            var firstBlinkIntervals = PerformerBlink.SampleIntervalsForAcceptance(246813, 3.5f, 6.5f, 12);
            var secondBlinkIntervals = PerformerBlink.SampleIntervalsForAcceptance(246813, 3.5f, 6.5f, 12);
            for (var index = 0; index < firstBlinkIntervals.Length; index++)
                if (firstBlinkIntervals[index] != secondBlinkIntervals[index]
                    || firstBlinkIntervals[index] < 3.5f || firstBlinkIntervals[index] > 6.5f)
                {
                    failures.Add("Private blink event intervals did not repeat for the same seed and settings.");
                    break;
                }
        }

        private IEnumerator ReleaseGazeToBase(GazePoseSnapshot basePose, PerformerGaze gaze,
            List<string> failures, string description)
        {
            performer.ClearGaze();
            yield return WaitForGazeRelease(3f);
            if (performer.GazeWeight > 0.01f)
                failures.Add(description + " did not reach zero gaze weight.");
            CheckGazePose(description, basePose, CaptureGazePose(gaze), false, false, false, failures);
        }

        private IEnumerator WaitForGazeAcquisition(float timeoutSeconds, List<string> failures,
            string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.HasGazeTarget && !performer.IsGazeAcquired
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!performer.HasGazeTarget || !performer.IsGazeAcquired)
                failures.Add(description + " did not acquire before the timeout.");
        }

        private IEnumerator WaitForGazeRelease(float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.GazeWeight > 0.01f && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static GazePoseSnapshot CaptureGazePose(PerformerGaze gaze)
        {
            return new GazePoseSnapshot(gaze.HeadCalibration.Bone.localRotation,
                gaze.LeftEyeCalibration.Bone.localRotation, gaze.RightEyeCalibration.Bone.localRotation);
        }

        private static void CheckGazePose(string description, GazePoseSnapshot expected,
            GazePoseSnapshot actual, bool headShouldMove, bool leftEyeShouldMove,
            bool rightEyeShouldMove, List<string> failures)
        {
            CheckGazeRotation(description, "head", expected.Head, actual.Head, headShouldMove, failures);
            CheckGazeRotation(description, "left eye", expected.LeftEye, actual.LeftEye, leftEyeShouldMove, failures);
            CheckGazeRotation(description, "right eye", expected.RightEye, actual.RightEye, rightEyeShouldMove, failures);
        }

        private static void CheckGazeRotation(string description, string semanticName,
            Quaternion expected, Quaternion actual, bool shouldMove, List<string> failures)
        {
            var difference = Quaternion.Angle(expected, actual);
            if (shouldMove && difference <= RotationToleranceDegrees)
                failures.Add(description + " did not rotate the " + semanticName + ".");
            if (!shouldMove && difference > RotationToleranceDegrees)
                failures.Add(description + " changed the " + semanticName + " without a gaze contribution.");
        }

        private static void CheckGazeCalibration(string semanticName,
            PerformerGazeBoneCalibration calibration, string expectedBoneName, List<string> failures)
        {
            if (calibration.Bone == null || calibration.Bone.name != expectedBoneName)
            {
                failures.Add("The " + semanticName + " gaze bone did not resolve to exact transform '"
                             + expectedBoneName + "'.");
                return;
            }

            if (!IsFinite(calibration.LocalAim) || !IsFinite(calibration.LocalUp)
                || !IsFinite(calibration.LocalRight) || Mathf.Abs(calibration.LocalAim.magnitude - 1f) > 0.001f
                || Mathf.Abs(calibration.LocalUp.magnitude - 1f) > 0.001f
                || Mathf.Abs(calibration.LocalRight.magnitude - 1f) > 0.001f
                || Mathf.Abs(Vector3.Dot(calibration.LocalAim, calibration.LocalUp)) > 0.001f
                || Mathf.Abs(Vector3.Dot(calibration.LocalAim, calibration.LocalRight)) > 0.001f
                || Mathf.Abs(Vector3.Dot(calibration.LocalUp, calibration.LocalRight)) > 0.001f)
                failures.Add("The " + semanticName + " calibrated anatomical aim basis is invalid.");
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                   && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private async void RunBreathingAwaitableChecks(List<string> failures)
        {
            try
            {
                performer.BreathingEnabled = true;
                performer.MorphBreathingEnabled = true;
                performer.BoneBreathingEnabled = true;
                performer.MorphBreathingStrength = 1f;
                performer.BoneBreathingStrength = 1f;
                performer.Pose(poseA, PoseTransition.Snap);
                performer.SetBreathPhaseForAcceptance(0.20f);
                var phaseAtStart = performer.BreathPhase;
                var settledRequest = StartPoseRequest(poseB, PoseTransition.Smooth(0.30f));
                await WaitForCompletionObservation(settledRequest, 5f, failures,
                    "PoseAsync while breathing is active");
                if (settledRequest.Result != PoseCompletion.Settled || performer.SettledPose != poseB
                    || performer.IsTransitioning)
                    failures.Add("PoseAsync did not complete at base-pose settlement while breathing was active.");
                var phaseAtSettlement = performer.BreathPhase;
                await Awaitable.NextFrameAsync();
                if (Mathf.Repeat(phaseAtSettlement - phaseAtStart, 1f) <= 0.01f
                    || Mathf.Repeat(performer.BreathPhase - phaseAtSettlement, 1f) <= 0.0001f)
                    failures.Add("Breathing did not continue independently through and after PoseAsync settlement.");

                performer.Pose(poseA, PoseTransition.Snap);
                performer.SetBreathPhaseForAcceptance(0.25f);
                var superseded = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                var deadline = Time.realtimeSinceStartup + 4f;
                while (performer.IsTransitioning && performer.TransitionProgress < 0.35f
                       && Time.realtimeSinceStartup < deadline)
                    await Awaitable.NextFrameAsync();
                if (!performer.IsTransitioning || performer.TransitionProgress < 0.35f)
                    failures.Add("Breathing-active supersession setup did not reach the requested transition point.");
                var phaseBeforeSupersession = performer.BreathPhase;
                performer.Pose(poseC, PoseTransition.Smooth(0.25f));
                if (!superseded.Completed || superseded.Result != PoseCompletion.Superseded
                    || performer.DesiredPose != poseC)
                    failures.Add("Breathing-active PoseAsync supersession did not preserve latest-wins behavior.");
                if (Mathf.Abs(phaseBeforeSupersession - performer.BreathPhase) > 0.0001f)
                    failures.Add("Superseding an awaited pose reset the breath phase.");
                await WaitForSettlementAsync(5f, failures, "breathing-active superseding pose");
                if (performer.SettledPose != poseC)
                    failures.Add("The breathing-active superseding pose did not settle at the latest target.");
            }
            catch (Exception exception)
            {
                failures.Add("Breathing PoseAsync checks threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                _awaitableChecksComplete = true;
            }
        }

        private async void RunGazeAwaitableChecks(List<string> failures)
        {
            var originalSettings = CaptureGazeSettings();
            GameObject targetA = null;
            GameObject targetB = null;
            GameObject lostTarget = null;
            try
            {
                var gaze = performer.GazeRuntime;
                if (gaze == null || !gaze.IsAvailable)
                {
                    failures.Add("Gaze awaitable checks require a successfully initialized PerformerGaze runtime.");
                    return;
                }

                performer.GazeEnabled = true;
                performer.HeadGazeEnabled = true;
                performer.EyeGazeEnabled = true;
                performer.HeadGazeWeight = 0.7f;
                performer.EyeGazeWeight = 1f;
                performer.HeadGazeResponse = 4f;
                performer.EyeGazeResponse = 12f;
                performer.GazeAcquireToleranceDegrees = 3f;
                performer.GazeReleaseResponse = 20f;
                targetA = new GameObject("Gaze Acceptance Target A");
                targetB = new GameObject("Gaze Acceptance Target B");
                targetA.transform.position = gaze.CreateValidationTarget(15f, 5f, 2.5f);
                targetB.transform.position = gaze.CreateValidationTarget(-35f, 10f, 2.5f);

                performer.Pose(poseA, PoseTransition.Snap);
                var firstSameTarget = StartGazeRequest(targetA.transform);
                await Awaitable.NextFrameAsync();
                var generationBeforeDuplicate = gaze.IntentionGeneration;
                var secondSameTarget = StartGazeRequest(targetA.transform);
                if (gaze.IntentionGeneration != generationBeforeDuplicate)
                    failures.Add("A second same-Transform waiter restarted gaze acquisition.");
                await WaitForGazeCompletionObservation(firstSameTarget, 8f, failures,
                    "first same-target gaze waiter");
                await WaitForGazeCompletionObservation(secondSameTarget, 8f, failures,
                    "second same-target gaze waiter");
                if (firstSameTarget.Result != GazeCompletion.Acquired
                    || secondSameTarget.Result != GazeCompletion.Acquired)
                    failures.Add("Same-target gaze waiters did not both complete as Acquired.");

                var generationBeforeMovedDuplicate = gaze.IntentionGeneration;
                targetA.transform.position = gaze.CreateValidationTarget(-48f, 12f, 2.5f);
                var movedSameTarget = StartGazeRequest(targetA.transform);
                if (movedSameTarget.Completed || gaze.IntentionGeneration != generationBeforeMovedDuplicate)
                    failures.Add("A moved but semantically identical target restarted gaze or reported stale acquisition.");
                await WaitForGazeCompletionObservation(movedSameTarget, 8f, failures,
                    "same target after its Transform moved");
                if (movedSameTarget.Result != GazeCompletion.Acquired)
                    failures.Add("A same-Transform waiter did not reacquire after the target moved.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                performer.GazeAcquireToleranceDegrees = 0.1f;
                targetA.transform.position = gaze.CreateValidationTarget(78f, -12f, 3f);
                targetB.transform.position = gaze.CreateValidationTarget(-70f, 16f, 3f);
                var superseded = StartGazeRequest(targetA.transform);
                await Awaitable.NextFrameAsync();
                var headBeforeRetarget = gaze.HeadCalibration.Bone.localRotation;
                var leftEyeBeforeRetarget = gaze.LeftEyeCalibration.Bone.localRotation;
                var rightEyeBeforeRetarget = gaze.RightEyeCalibration.Bone.localRotation;
                performer.LookAt(targetB.transform);
                if (!superseded.Completed || superseded.Result != GazeCompletion.Superseded)
                    failures.Add("A replaced pending gaze request did not complete as Superseded immediately.");
                CheckGazeRotation("Gaze retarget continuity", "head", headBeforeRetarget,
                    gaze.HeadCalibration.Bone.localRotation, false, failures);
                CheckGazeRotation("Gaze retarget continuity", "left eye", leftEyeBeforeRetarget,
                    gaze.LeftEyeCalibration.Bone.localRotation, false, failures);
                CheckGazeRotation("Gaze retarget continuity", "right eye", rightEyeBeforeRetarget,
                    gaze.RightEyeCalibration.Bone.localRotation, false, failures);
                var retargetWaiter = StartGazeRequest(targetB.transform);
                performer.HeadGazeResponse = 12f;
                performer.EyeGazeResponse = 20f;
                await WaitForGazeCompletionObservation(retargetWaiter, 8f, failures,
                    "latest gaze target after supersession");
                if (retargetWaiter.Result != GazeCompletion.Acquired)
                    failures.Add("The latest gaze target did not acquire after superseding the first target.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                targetA.transform.position = gaze.CreateValidationTarget(100f, 0f, 3f);
                var clearedDuringAcquisition = StartGazeRequest(targetA.transform);
                await Awaitable.NextFrameAsync();
                performer.ClearGaze();
                if (!clearedDuringAcquisition.Completed
                    || clearedDuringAcquisition.Result != GazeCompletion.Superseded)
                    failures.Add("ClearGaze during acquisition did not supersede the pending waiter.");
                await WaitForGazeReleaseAsync(3f);

                lostTarget = new GameObject("Gaze Acceptance Lost Target");
                lostTarget.transform.position = gaze.CreateValidationTarget(-100f, 0f, 3f);
                var targetLost = StartGazeRequest(lostTarget.transform);
                await Awaitable.NextFrameAsync();
                Destroy(lostTarget);
                await WaitForGazeCompletionObservation(targetLost, 4f, failures,
                    "destroyed Transform gaze target");
                if (targetLost.Result != GazeCompletion.TargetLost || performer.HasGazeTarget)
                    failures.Add("A destroyed gaze Transform did not resolve as TargetLost and release the intention.");
                await WaitForGazeReleaseAsync(3f);
                lostTarget = null;

                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 30f;
                performer.GazeAcquireToleranceDegrees = 8f;
                var fixedPoint = gaze.CreateValidationTarget(5f, 2f, 2.5f);
                var fixedAwaiter = StartGazeRequest(fixedPoint);
                await WaitForGazeCompletionObservation(fixedAwaiter, 5f, failures,
                    "fixed world point gaze waiter");
                if (fixedAwaiter.Result != GazeCompletion.Acquired)
                    failures.Add("LookAtAsync(Vector3) did not complete as Acquired.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 30f;
                performer.GazeAcquireToleranceDegrees = 8f;
                var constrainedWaiter = StartGazeRequest(gaze.CreateValidationTarget(145f, 12f, 2.5f));
                await WaitForGazeCompletionObservation(constrainedWaiter, 5f, failures,
                    "async gaze toward a target beyond anatomical limits");
                if (constrainedWaiter.Result != GazeCompletion.Acquired)
                    failures.Add("LookAtAsync did not complete Acquired for a target beyond anatomical limits.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                performer.GazeAcquireToleranceDegrees = 0.1f;
                var slowGaze = StartGazeRequest(gaze.CreateValidationTarget(145f, 10f, 3f));
                await Awaitable.NextFrameAsync();
                var poseWhileGazeAcquires = StartPoseRequest(poseB, PoseTransition.Smooth(0.15f));
                await WaitForCompletionObservation(poseWhileGazeAcquires, 4f, failures,
                    "PoseAsync while gaze is still acquiring");
                if (poseWhileGazeAcquires.Result != PoseCompletion.Settled || performer.SettledPose != poseB)
                    failures.Add("PoseAsync did not settle independently while gaze was acquiring.");
                if (slowGaze.Completed)
                    failures.Add("LookAtAsync completed before its slow, out-of-range gaze solution settled.");
                performer.ClearGaze();
                if (!slowGaze.Completed || slowGaze.Result != GazeCompletion.Superseded)
                    failures.Add("Clearing a slow pending gaze did not complete its waiter as Superseded.");
                await WaitForGazeReleaseAsync(3f);

                performer.HeadGazeResponse = 20f;
                performer.EyeGazeResponse = 30f;
                performer.GazeAcquireToleranceDegrees = 10f;
                performer.Pose(poseA, PoseTransition.Snap);
                var poseInProgress = StartPoseRequest(poseB, PoseTransition.Smooth(2f));
                await Awaitable.NextFrameAsync();
                targetA.transform.position = gaze.CreateValidationTarget(4f, 1f, 2.5f);
                targetB.transform.position = gaze.CreateValidationTarget(-28f, 8f, 2.8f);
                var gazeDuringPose = StartGazeRequest(targetA.transform);
                await WaitForGazeCompletionObservation(gazeDuringPose, 3f, failures,
                    "LookAtAsync while a body pose transition is active");
                if (gazeDuringPose.Result != GazeCompletion.Acquired || !performer.IsTransitioning)
                    failures.Add("LookAtAsync was coupled to pose settlement instead of gaze acquisition.");
                var gazeRetargetedDuringPose = StartGazeRequest(targetB.transform);
                if (!performer.IsTransitioning)
                    failures.Add("The body pose transition settled before a second gaze target could be issued.");
                await WaitForGazeCompletionObservation(gazeRetargetedDuringPose, 3f, failures,
                    "gaze retarget during a body pose transition");
                if (gazeRetargetedDuringPose.Result != GazeCompletion.Acquired || !performer.IsTransitioning)
                    failures.Add("Gaze could not retarget and acquire independently while the body pose transition continued.");
                await WaitForCompletionObservation(poseInProgress, 5f, failures,
                    "PoseAsync while gaze remains active");
                if (poseInProgress.Result != PoseCompletion.Settled)
                    failures.Add("PoseAsync did not settle while persistent gaze remained active.");

                performer.ClearGaze();
                await WaitForGazeReleaseAsync(3f);
                performer.HeadGazeResponse = 0.1f;
                performer.EyeGazeResponse = 0.1f;
                performer.GazeAcquireToleranceDegrees = 0.1f;
                var pendingOnDisable = StartGazeRequest(gaze.CreateValidationTarget(130f, -15f, 3f));
                await Awaitable.NextFrameAsync();
                if (pendingOnDisable.Completed)
                    failures.Add("The lifecycle test target acquired before the performer-disable check.");
                performer.enabled = false;
                if (!pendingOnDisable.Completed || pendingOnDisable.Result != GazeCompletion.PerformerDisabled)
                    failures.Add("Disabling the performer did not complete the pending gaze waiter as PerformerDisabled.");
                performer.enabled = true;
                await Awaitable.NextFrameAsync();
            }
            catch (Exception exception)
            {
                failures.Add("Gaze awaitable checks threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (!performer.enabled) performer.enabled = true;
                if (targetA != null) Destroy(targetA);
                if (targetB != null) Destroy(targetB);
                if (lostTarget != null) Destroy(lostTarget);
                RestoreGazeSettings(originalSettings);
                _awaitableChecksComplete = true;
            }
        }

        private GazeCompletionObservation StartGazeRequest(Transform target)
        {
            var observation = new GazeCompletionObservation();
            ObserveGazeRequest(observation, target);
            return observation;
        }

        private GazeCompletionObservation StartGazeRequest(Vector3 worldPosition)
        {
            var observation = new GazeCompletionObservation();
            ObserveGazeRequest(observation, worldPosition);
            return observation;
        }

        private async void ObserveGazeRequest(GazeCompletionObservation observation, Transform target)
        {
            try
            {
                observation.Result = await performer.LookAtAsync(target);
            }
            catch (Exception exception)
            {
                observation.Error = exception;
            }
            finally
            {
                observation.Completed = true;
            }
        }

        private async void ObserveGazeRequest(GazeCompletionObservation observation, Vector3 worldPosition)
        {
            try
            {
                observation.Result = await performer.LookAtAsync(worldPosition);
            }
            catch (Exception exception)
            {
                observation.Error = exception;
            }
            finally
            {
                observation.Completed = true;
            }
        }

        private static async Awaitable WaitForGazeCompletionObservation(
            GazeCompletionObservation observation, float timeoutSeconds, List<string> failures,
            string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!observation.Completed && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (!observation.Completed)
            {
                failures.Add(description + " did not resolve before the timeout.");
                return;
            }
            if (observation.Error != null)
                failures.Add(description + " threw " + observation.Error.GetType().Name + ": " + observation.Error.Message);
        }

        private async Awaitable WaitForGazeReleaseAsync(float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.GazeWeight > 0.01f && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (performer.GazeWeight > 0.01f)
                throw new TimeoutException("Gaze influence did not release before the timeout.");
        }

        private static float[] CaptureMorphContributions(
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, PerformerPoseSnapshot baseState)
        {
            var result = new float[bindings.Count];
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                result[index] = binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex)
                                - baseState.BlendShapes[binding.BaseBlendShapeIndex];
            }
            return result;
        }

        private static void CheckMorphContributions(string description,
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, IReadOnlyList<float> contributions,
            bool shouldMove, List<string> failures)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                if (shouldMove && contributions[index] <= 0.05f)
                    failures.Add(description + " did not add weight to '" + bindings[index].SemanticName
                                 + "' on " + bindings[index].RendererPath + ".");
                if (!shouldMove && Mathf.Abs(contributions[index]) > BlendShapeTolerance)
                    failures.Add(description + " changed base morph '" + bindings[index].SemanticName + "'.");
            }
        }

        private static void CheckMorphsEqualBase(string description,
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, PerformerPoseSnapshot baseState,
            List<string> failures)
        {
            CheckMorphContributions(description, bindings, CaptureMorphContributions(bindings, baseState),
                false, failures);
        }

        private static void CheckBreathingBonesEqualBase(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            List<string> failures)
        {
            CheckBreathingBones(description, bindings, baseState, false, failures);
        }

        private static bool CheckBreathingBonesDifferFromBase(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            List<string> failures)
        {
            return CheckBreathingBones(description, bindings, baseState, true, failures);
        }

        private static bool CheckBreathingBones(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            bool shouldMove, List<string> failures)
        {
            var anyMoved = false;
            foreach (var binding in bindings)
            {
                var expected = baseState.Transforms[binding.BaseTransformIndex];
                var positionDelta = Vector3.Distance(binding.Bone.localPosition, expected.LocalPosition);
                var rotationDelta = Quaternion.Angle(binding.Bone.localRotation, expected.LocalRotation);
                var moved = positionDelta > PositionTolerance || rotationDelta > RotationToleranceDegrees;
                anyMoved |= moved;
                if (shouldMove && !moved)
                    failures.Add(description + " did not move configured bone '" + binding.Bone.name + "'.");
                if (!shouldMove && moved)
                    failures.Add(description + " applied a breathing delta to bone '" + binding.Bone.name + "'.");
            }
            return anyMoved;
        }

        private static void CheckExpectedMorphContributions(string description,
            IReadOnlyList<PerformerBreathingMorphBindingInfo> bindings, PerformerPoseSnapshot baseState,
            SuccubusPerformer currentPerformer, List<string> failures)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                var relativeStrength = binding.SemanticName == "Breathe"
                    ? currentPerformer.BreatheStrength : currentPerformer.BreatheBellyStrength;
                var baseWeight = baseState.BlendShapes[binding.BaseBlendShapeIndex];
                var room = Mathf.Max(0f, binding.PositiveMaximumWeight - baseWeight);
                var expected = Mathf.Min(PerformerBreathing.MorphWeightAmplitude * currentPerformer.BreathValue
                                         * currentPerformer.MorphBreathingStrength * relativeStrength, room);
                var actual = binding.Renderer.GetBlendShapeWeight(binding.BlendShapeIndex) - baseWeight;
                if (Mathf.Abs(actual - expected) > BlendShapeTolerance)
                    failures.Add(description + " did not use the shared breath value for '"
                                 + binding.SemanticName + "' on " + binding.RendererPath + ".");
            }
        }

        private static void CheckExpectedBoneContributions(string description,
            IReadOnlyList<PerformerBreathingBoneBindingInfo> bindings, PerformerPoseSnapshot baseState,
            SuccubusPerformer currentPerformer, List<string> failures)
        {
            var inhale = currentPerformer.BreathValue * currentPerformer.BoneBreathingStrength;
            foreach (var binding in bindings)
            {
                var expected = baseState.Transforms[binding.BaseTransformIndex];
                var expectedPosition = expected.LocalPosition + binding.FullInhaleLocalPositionDelta * inhale;
                var expectedRotation = expected.LocalRotation
                                      * Quaternion.Euler(binding.FullInhaleLocalRotationDelta * inhale);
                if (Vector3.Distance(binding.Bone.localPosition, expectedPosition) > PositionTolerance
                    || Quaternion.Angle(binding.Bone.localRotation, expectedRotation) > RotationToleranceDegrees)
                    failures.Add(description + " did not derive bone '" + binding.Bone.name
                                 + "' from the same breath value as the morph channels.");
            }
        }

        private static PoseSnapshot ToPoseSnapshot(PerformerPoseSnapshot source)
        {
            var transforms = new TransformSnapshot[source.Transforms.Length];
            for (var index = 0; index < transforms.Length; index++)
            {
                var state = source.Transforms[index];
                transforms[index] = new TransformSnapshot(state.LocalPosition, state.LocalRotation, state.LocalScale);
            }
            return new PoseSnapshot(transforms, (float[])source.BlendShapes.Clone());
        }

        private static float CheckPoseDifference(PoseSnapshot left, PoseSnapshot right)
        {
            var difference = 0f;
            var transformCount = Mathf.Min(left.Transforms.Length, right.Transforms.Length);
            for (var index = 0; index < transformCount; index++)
            {
                difference = Mathf.Max(difference,
                    Vector3.Distance(left.Transforms[index].Position, right.Transforms[index].Position));
                difference = Mathf.Max(difference,
                    Quaternion.Angle(left.Transforms[index].Rotation, right.Transforms[index].Rotation) / 180f);
            }
            var blendShapeCount = Mathf.Min(left.BlendShapes.Length, right.BlendShapes.Length);
            for (var index = 0; index < blendShapeCount; index++)
                difference = Mathf.Max(difference, Mathf.Abs(left.BlendShapes[index] - right.BlendShapes[index]));
            return difference;
        }

        private BreathingHarnessSettings CaptureBreathingSettings()
        {
            return new BreathingHarnessSettings(performer.BreathingEnabled,
                performer.MorphBreathingEnabled, performer.MorphBreathingStrength,
                performer.BreatheStrength, performer.BreatheBellyStrength,
                performer.BoneBreathingEnabled, performer.BoneBreathingStrength, performer.BreathPhase);
        }

        private GazeHarnessSettings CaptureGazeSettings()
        {
            return new GazeHarnessSettings(performer.GazeEnabled,
                performer.GazeAcquireToleranceDegrees, performer.HeadGazeEnabled,
                performer.HeadGazeWeight, performer.HeadGazeResponse, performer.HeadGazeMaxYaw,
                performer.HeadGazeMaxPitch, performer.EyeGazeEnabled, performer.EyeGazeWeight,
                performer.EyeGazeResponse, performer.EyeGazeMaxYaw, performer.EyeGazeMaxPitch,
                performer.GazeReleaseResponse);
        }

        private AttentionLifeHarnessSettings CaptureAttentionLifeSettings()
        {
            return new AttentionLifeHarnessSettings(performer.AttentionLifeEnabled,
                performer.AttentionLifeSeed, performer.EyeFixationLifeEnabled,
                performer.EyeFixationMaxHorizontalDegrees, performer.EyeFixationMaxVerticalDegrees,
                performer.EyeFixationMinimumHoldSeconds, performer.EyeFixationMaximumHoldSeconds,
                performer.EyeFixationCenterBias, performer.HeadAttentionLifeEnabled,
                performer.HeadAttentionMaxTiltDegrees, performer.HeadAttentionMaxChinDegrees,
                performer.HeadAttentionMinimumHoldSeconds, performer.HeadAttentionMaximumHoldSeconds,
                performer.HeadAttentionTransitionResponse);
        }

        private BlinkHarnessSettings CaptureBlinkSettings()
        {
            return new BlinkHarnessSettings(performer.BlinkEnabled, performer.BlinkStrength,
                performer.BlinkMinimumIntervalSeconds, performer.BlinkMaximumIntervalSeconds,
                performer.BlinkCloseDurationSeconds, performer.BlinkClosedDurationSeconds,
                performer.BlinkOpenDurationSeconds);
        }

        private void RestoreGazeSettings(GazeHarnessSettings settings)
        {
            performer.GazeEnabled = settings.GazeEnabled;
            performer.GazeAcquireToleranceDegrees = settings.AcquireToleranceDegrees;
            performer.HeadGazeEnabled = settings.HeadEnabled;
            performer.HeadGazeWeight = settings.HeadWeight;
            performer.HeadGazeResponse = settings.HeadResponse;
            performer.HeadGazeMaxYaw = settings.HeadMaxYaw;
            performer.HeadGazeMaxPitch = settings.HeadMaxPitch;
            performer.EyeGazeEnabled = settings.EyesEnabled;
            performer.EyeGazeWeight = settings.EyeWeight;
            performer.EyeGazeResponse = settings.EyeResponse;
            performer.EyeGazeMaxYaw = settings.EyeMaxYaw;
            performer.EyeGazeMaxPitch = settings.EyeMaxPitch;
            performer.GazeReleaseResponse = settings.ReleaseResponse;
        }

        private void RestoreAttentionLifeSettings(AttentionLifeHarnessSettings settings)
        {
            performer.AttentionLifeEnabled = settings.Enabled;
            performer.EyeFixationLifeEnabled = settings.EyeEnabled;
            performer.EyeFixationMaxHorizontalDegrees = settings.EyeMaxHorizontal;
            performer.EyeFixationMaxVerticalDegrees = settings.EyeMaxVertical;
            performer.EyeFixationMinimumHoldSeconds = settings.EyeMinimumHold;
            performer.EyeFixationMaximumHoldSeconds = settings.EyeMaximumHold;
            performer.EyeFixationCenterBias = settings.EyeCenterBias;
            performer.HeadAttentionLifeEnabled = settings.HeadEnabled;
            performer.HeadAttentionMaxTiltDegrees = settings.HeadMaxTilt;
            performer.HeadAttentionMaxChinDegrees = settings.HeadMaxChin;
            performer.HeadAttentionMinimumHoldSeconds = settings.HeadMinimumHold;
            performer.HeadAttentionMaximumHoldSeconds = settings.HeadMaximumHold;
            performer.HeadAttentionTransitionResponse = settings.HeadResponse;
            performer.AttentionLifeSeed = settings.Seed;
        }

        private void RestoreBlinkSettings(BlinkHarnessSettings settings)
        {
            var blink = performer.BlinkRuntime;
            blink?.ClearClosureOverrideForAcceptance();
            performer.BlinkEnabled = false;
            performer.BlinkStrength = settings.Strength;
            performer.BlinkMinimumIntervalSeconds = settings.MinimumInterval;
            performer.BlinkMaximumIntervalSeconds = settings.MaximumInterval;
            performer.BlinkCloseDurationSeconds = settings.CloseDuration;
            performer.BlinkClosedDurationSeconds = settings.ClosedDuration;
            performer.BlinkOpenDurationSeconds = settings.OpenDuration;
            performer.BlinkEnabled = settings.Enabled;
        }

        private void RestoreBreathingSettings(BreathingHarnessSettings settings)
        {
            performer.BreathingEnabled = settings.BreathingEnabled;
            performer.MorphBreathingEnabled = settings.MorphBreathingEnabled;
            performer.MorphBreathingStrength = settings.MorphBreathingStrength;
            performer.BreatheStrength = settings.BreatheStrength;
            performer.BreatheBellyStrength = settings.BreatheBellyStrength;
            performer.BoneBreathingEnabled = settings.BoneBreathingEnabled;
            performer.BoneBreathingStrength = settings.BoneBreathingStrength;
        }

        private async void RunAwaitableChecks(List<string> failures)
        {
            try
            {
                var alreadySettled = await performer.PoseAsync(poseC, PoseTransition.Smooth(0.5f));
                if (alreadySettled != PoseCompletion.Settled || performer.IsTransitioning)
                    failures.Add("PoseAsync on the already settled pose did not complete immediately as Settled.");

                var snap = StartPoseRequest(poseA, PoseTransition.Snap);
                if (!snap.Completed || snap.Error != null || snap.Result != PoseCompletion.Settled
                    || performer.DesiredPose != poseA || performer.SettledPose != poseA || performer.IsTransitioning)
                    failures.Add("PoseAsync with Snap did not settle synchronously before another frame.");

                var ordinary = StartPoseRequest(poseB, PoseTransition.Smooth(0.20f));
                if (ordinary.Completed || !performer.IsTransitioning || performer.DesiredPose != poseB)
                    failures.Add("PoseAsync did not suspend while an ordinary transition was still running.");
                await WaitForCompletionObservation(ordinary, 5f, failures, "ordinary PoseAsync transition");
                if (ordinary.Result != PoseCompletion.Settled || performer.SettledPose != poseB)
                    failures.Add("An ordinary PoseAsync transition did not complete with Settled at its target.");

                performer.Pose(poseA, PoseTransition.Snap);
                var reentrant = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                reentrant.OnCompleted = result =>
                {
                    if (result == PoseCompletion.Superseded)
                        performer.Pose(poseA, PoseTransition.Smooth(0.15f));
                };
                await Awaitable.NextFrameAsync();
                performer.Pose(poseC, PoseTransition.Smooth(0.25f));
                if (!reentrant.Completed || reentrant.Result != PoseCompletion.Superseded
                    || performer.DesiredPose != poseA || !performer.IsTransitioning)
                    failures.Add("A superseded PoseAsync continuation could not safely issue a reentrant pose command.");
                await WaitForSettlementAsync(5f, failures, "reentrant superseding pose");

                performer.Pose(poseA, PoseTransition.Snap);
                var first = StartPoseRequest(poseB, PoseTransition.Smooth(0.25f));
                var replacement = StartPoseRequest(poseC, PoseTransition.Smooth(0.20f));
                await WaitForCompletionObservation(first, 5f, failures, "PoseAsync supersession by PoseAsync");
                await WaitForCompletionObservation(replacement, 5f, failures, "latest PoseAsync target");
                if (first.Result != PoseCompletion.Superseded || replacement.Result != PoseCompletion.Settled
                    || performer.SettledPose != poseC)
                    failures.Add("A newer PoseAsync did not supersede the old waiter and settle the latest target.");

                performer.Pose(poseA, PoseTransition.Snap);
                var waiterOne = StartPoseRequest(poseB, PoseTransition.Smooth(0.30f));
                var waiterTwo = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                if (performer.ActiveTransition.Duration < 0.299f || performer.ActiveTransition.Duration > 0.301f)
                    failures.Add("A second waiter for the same desired pose restarted its transition.");
                await WaitForCompletionObservation(waiterOne, 5f, failures, "first same-target waiter");
                await WaitForCompletionObservation(waiterTwo, 5f, failures, "second same-target waiter");
                if (waiterOne.Result != PoseCompletion.Settled || waiterTwo.Result != PoseCompletion.Settled)
                    failures.Add("Independent same-target PoseAsync waiters did not both settle.");

                performer.Pose(poseA, PoseTransition.Snap);
                var disabled = StartPoseRequest(poseB, PoseTransition.Smooth(1f));
                performer.enabled = false;
                if (!disabled.Completed || disabled.Result != PoseCompletion.PerformerDisabled)
                    failures.Add("Disabling the performer did not complete its outstanding PoseAsync waiter as PerformerDisabled.");
                performer.enabled = true;
                await Awaitable.NextFrameAsync();
            }
            catch (Exception exception)
            {
                failures.Add("PoseAsync acceptance checks threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                _awaitableChecksComplete = true;
            }
        }

        private CompletionObservation StartPoseRequest(PerformerPose pose, PoseTransition transition)
        {
            var observation = new CompletionObservation();
            ObservePoseRequest(observation, pose, transition);
            return observation;
        }

        private async void ObservePoseRequest(CompletionObservation observation,
            PerformerPose pose, PoseTransition transition)
        {
            try
            {
                observation.Result = await performer.PoseAsync(pose, transition);
            }
            catch (Exception exception)
            {
                observation.Error = exception;
            }
            finally
            {
                observation.Completed = true;
            }

            observation.OnCompleted?.Invoke(observation.Result);
        }

        private static async Awaitable WaitForCompletionObservation(CompletionObservation observation,
            float timeoutSeconds, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!observation.Completed && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (!observation.Completed)
            {
                failures.Add(description + " did not resolve before the timeout.");
                return;
            }
            if (observation.Error != null)
                failures.Add(description + " threw " + observation.Error.GetType().Name + ": " + observation.Error.Message);
        }

        private async Awaitable WaitForSettlementAsync(float timeoutSeconds, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.IsTransitioning && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            if (performer.IsTransitioning) failures.Add(description + " did not settle before the timeout.");
        }

        private sealed class CompletionObservation
        {
            public bool Completed;
            public PoseCompletion Result;
            public Exception Error;
            public Action<PoseCompletion> OnCompleted;
        }

        private sealed class ExpressionCompletionObservation
        {
            public bool Completed;
            public ExpressionCompletion Result;
            public Exception Error;
        }

        private sealed class GazeCompletionObservation
        {
            public bool Completed;
            public GazeCompletion Result;
            public Exception Error;
        }

        private IEnumerator InterruptAtProgress(PerformerPose start, PerformerPose middle, PerformerPose target,
            float progress, PoseSnapshot targetState, List<string> failures)
        {
            performer.Pose(start, PoseTransition.Snap);
            yield return null;
            performer.Pose(middle, PoseTransition.Smooth(1.2f));
            var deadline = Time.realtimeSinceStartup + 4f;
            while (performer.IsTransitioning && performer.TransitionProgress < progress
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!performer.IsTransitioning || performer.TransitionProgress < progress)
            {
                failures.Add("Transition did not reach the " + (progress * 100f).ToString("F0") + "% interruption point.");
                yield break;
            }

            var visibleBefore = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(target, new PoseTransition(0.45f, 0.08f, 0.10f,
                AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
            CheckPose("Interruption continuity at " + (progress * 100f).ToString("F0") + "%",
                visibleBefore, CapturePose(performer.GetComponent<Animator>()), failures);
            if (performer.DesiredPose != target || performer.SettledPose != start || !performer.IsTransitioning)
                failures.Add("The " + (progress * 100f).ToString("F0") + "% interruption did not replace the desired pose immediately.");

            yield return WaitForSettlement(6f, failures, "interruption to " + target.name);
            if (performer.DesiredPose != target || performer.SettledPose != target || performer.IsTransitioning)
                failures.Add("The interrupted transition did not settle on its latest target.");
            CheckPose("Interrupted target endpoint", targetState,
                CapturePose(performer.GetComponent<Animator>()), failures);
        }

        private IEnumerator CheckRapidRetargets(PerformerPose start, PerformerPose first,
            PerformerPose latest, PoseSnapshot latestState, List<string> failures)
        {
            performer.Pose(start, PoseTransition.Snap);
            yield return null;
            performer.Pose(first, PoseTransition.Smooth(1f));
            yield return null;

            var before = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(latest, PoseTransition.Smooth(0.25f));
            CheckPose("Rapid retarget continuity 1", before,
                CapturePose(performer.GetComponent<Animator>()), failures);
            before = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(start, PoseTransition.Smooth(0.25f));
            CheckPose("Rapid retarget continuity 2", before,
                CapturePose(performer.GetComponent<Animator>()), failures);
            before = CapturePose(performer.GetComponent<Animator>());
            performer.Pose(latest, PoseTransition.Smooth(0.25f));
            CheckPose("Rapid retarget continuity 3", before,
                CapturePose(performer.GetComponent<Animator>()), failures);
            if (performer.DesiredPose != latest) failures.Add("Rapid retargeting did not keep the latest desired pose.");

            yield return WaitForSettlement(6f, failures, "rapid latest-wins retarget");
            if (performer.DesiredPose != latest || performer.SettledPose != latest || performer.IsTransitioning)
                failures.Add("Rapid retargeting queued an obsolete pose instead of settling on the latest target.");
            CheckPose("Rapid retarget latest endpoint", latestState,
                CapturePose(performer.GetComponent<Animator>()), failures);
        }

        private IEnumerator CheckLifecycle(Animator animator, PerformerPose pose,
            PoseSnapshot expected, int expectedPlayableCount, List<string> failures)
        {
            for (var index = 0; index < 3; index++)
            {
                performer.enabled = false;
                yield return null;
                if (performer.RuntimePlayableCount != 0)
                    failures.Add("Disabling the performer left a live playable graph.");

                performer.enabled = true;
                yield return null;
                if (performer.DesiredPose != pose || performer.SettledPose != pose || performer.IsTransitioning)
                    failures.Add("Performer re-enable did not restore its last desired pose.");
                CheckPose("Lifecycle re-enable " + (index + 1), expected, CapturePose(animator), failures);
                if (performer.RuntimePlayableCount != expectedPlayableCount)
                    failures.Add("Playable count after re-enable was " + performer.RuntimePlayableCount
                        + "; expected " + expectedPlayableCount + ".");
            }
        }

        private IEnumerator WaitForSettlement(float timeoutSeconds, List<string> failures, string description)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (performer.IsTransitioning && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (performer.IsTransitioning) failures.Add(description + " did not settle before the timeout.");
        }

        private static void ValidateTrajectoryMath(List<string> failures)
        {
            var linear = new PoseTransition(1f, 0f, 0f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var slowStartCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.75f, 0.18f),
                new Keyframe(1f, 1f, 0f, 0f));
            var slowStart = new PoseTransition(1f, 0f, 0f, slowStartCurve);
            if (Mathf.Abs(PoseTransitionTrajectory.Evaluate(0.35f, linear)
                          - PoseTransitionTrajectory.Evaluate(0.35f, slowStart)) < 0.1f)
                failures.Add("The custom curve did not change main-drive progression.");
            if (PoseTransitionTrajectory.Evaluate(0f, slowStart) != 0f
                || PoseTransitionTrajectory.Evaluate(1f, slowStart) != 1f)
                failures.Add("Custom curve endpoints were not exact.");

            var shaped = new PoseTransition(1f, 0.10f, 0.12f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            if (PoseTransitionTrajectory.Evaluate(0.075f, shaped) >= 0f)
                failures.Add("Windup did not travel below the source.");
            if (PoseTransitionTrajectory.Evaluate(0.90f, shaped) <= 1f)
                failures.Add("Overshoot did not travel past the target.");
            if (PoseTransitionTrajectory.Evaluate(1f, shaped) != 1f)
                failures.Add("Windup/overshoot did not finish at the exact target.");
        }

        private static PoseSnapshot CapturePose(Animator animator)
        {
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            var renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var transformValues = new TransformSnapshot[transforms.Length];
            for (var index = 0; index < transforms.Length; index++)
            {
                var item = transforms[index];
                transformValues[index] = new TransformSnapshot(item.localPosition, item.localRotation, item.localScale);
            }

            var blendShapeCount = 0;
            foreach (var renderer in renderers)
                if (renderer != null && renderer.sharedMesh != null) blendShapeCount += renderer.sharedMesh.blendShapeCount;
            var blendShapeValues = new float[blendShapeCount];
            var blendShapeIndex = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;
                for (var index = 0; index < renderer.sharedMesh.blendShapeCount; index++)
                    blendShapeValues[blendShapeIndex++] = renderer.GetBlendShapeWeight(index);
            }
            return new PoseSnapshot(transformValues, blendShapeValues);
        }

        private static void CheckPose(string description, PoseSnapshot expected, PoseSnapshot actual,
            List<string> failures)
        {
            if (expected.Transforms.Length != actual.Transforms.Length
                || expected.BlendShapes.Length != actual.BlendShapes.Length)
            {
                failures.Add(description + " changed the transform or blendshape count.");
                return;
            }

            for (var index = 0; index < expected.Transforms.Length; index++)
            {
                var a = expected.Transforms[index];
                var b = actual.Transforms[index];
                if (Vector3.Distance(a.Position, b.Position) > PositionTolerance
                    || Quaternion.Angle(a.Rotation, b.Rotation) > RotationToleranceDegrees
                    || Vector3.Distance(a.Scale, b.Scale) > ScaleTolerance)
                {
                    failures.Add(description + " differed at transform index " + index + ".");
                    break;
                }
            }

            for (var index = 0; index < expected.BlendShapes.Length; index++)
            {
                if (Mathf.Abs(expected.BlendShapes[index] - actual.BlendShapes[index]) > BlendShapeTolerance)
                {
                    failures.Add(description + " differed at blendshape index " + index + ".");
                    break;
                }
            }
        }

        private static PlacementSnapshot CapturePlacement(Transform root)
        {
            return new PlacementSnapshot(root.position, root.rotation, root.lossyScale);
        }

        private static void CheckPlacement(string description, PlacementSnapshot expected,
            PlacementSnapshot actual, List<string> failures)
        {
            if (Vector3.Distance(expected.Position, actual.Position) > PositionTolerance
                || Quaternion.Angle(expected.Rotation, actual.Rotation) > RotationToleranceDegrees
                || Vector3.Distance(expected.Scale, actual.Scale) > ScaleTolerance)
                failures.Add(description + " changed during pose tests.");
        }

        private void Finish(List<string> failures, string additionalFailure = null)
        {
            if (additionalFailure != null) failures.Add(additionalFailure);
            _running = false;
            Status = failures.Count == 0 ? "Acceptance checks passed."
                : "Acceptance checks found " + failures.Count + " issue(s). See Console.";
            if (failures.Count == 0)
                Debug.Log("Performer checks passed: pose endpoints and awaitables, breathing, P0.6 gaze, P0.7 attention-life determinism and release, exact blink bindings and composition, blink/gaze/pose independence, downstream pose-state isolation, graph lifecycle, root placement, and P0.G3 dissolve phase/cleanup contracts.", this);
            else
                Debug.LogError("Performer pose checks failed:\n- " + string.Join("\n- ", failures), this);
        }

        private readonly struct BreathingHarnessSettings
        {
            public readonly bool BreathingEnabled;
            public readonly bool MorphBreathingEnabled;
            public readonly float MorphBreathingStrength;
            public readonly float BreatheStrength;
            public readonly float BreatheBellyStrength;
            public readonly bool BoneBreathingEnabled;
            public readonly float BoneBreathingStrength;
            public readonly float Phase;

            public BreathingHarnessSettings(bool breathingEnabled, bool morphBreathingEnabled,
                float morphBreathingStrength, float breatheStrength, float breatheBellyStrength,
                bool boneBreathingEnabled, float boneBreathingStrength, float phase)
            {
                BreathingEnabled = breathingEnabled;
                MorphBreathingEnabled = morphBreathingEnabled;
                MorphBreathingStrength = morphBreathingStrength;
                BreatheStrength = breatheStrength;
                BreatheBellyStrength = breatheBellyStrength;
                BoneBreathingEnabled = boneBreathingEnabled;
                BoneBreathingStrength = boneBreathingStrength;
                Phase = phase;
            }
        }

        private readonly struct AttentionLifeHarnessSettings
        {
            public readonly bool Enabled;
            public readonly int Seed;
            public readonly bool EyeEnabled;
            public readonly float EyeMaxHorizontal;
            public readonly float EyeMaxVertical;
            public readonly float EyeMinimumHold;
            public readonly float EyeMaximumHold;
            public readonly float EyeCenterBias;
            public readonly bool HeadEnabled;
            public readonly float HeadMaxTilt;
            public readonly float HeadMaxChin;
            public readonly float HeadMinimumHold;
            public readonly float HeadMaximumHold;
            public readonly float HeadResponse;

            public AttentionLifeHarnessSettings(bool enabled, int seed, bool eyeEnabled,
                float eyeMaxHorizontal, float eyeMaxVertical, float eyeMinimumHold,
                float eyeMaximumHold, float eyeCenterBias, bool headEnabled,
                float headMaxTilt, float headMaxChin, float headMinimumHold,
                float headMaximumHold, float headResponse)
            {
                Enabled = enabled;
                Seed = seed;
                EyeEnabled = eyeEnabled;
                EyeMaxHorizontal = eyeMaxHorizontal;
                EyeMaxVertical = eyeMaxVertical;
                EyeMinimumHold = eyeMinimumHold;
                EyeMaximumHold = eyeMaximumHold;
                EyeCenterBias = eyeCenterBias;
                HeadEnabled = headEnabled;
                HeadMaxTilt = headMaxTilt;
                HeadMaxChin = headMaxChin;
                HeadMinimumHold = headMinimumHold;
                HeadMaximumHold = headMaximumHold;
                HeadResponse = headResponse;
            }
        }

        private readonly struct BlinkHarnessSettings
        {
            public readonly bool Enabled;
            public readonly float Strength;
            public readonly float MinimumInterval;
            public readonly float MaximumInterval;
            public readonly float CloseDuration;
            public readonly float ClosedDuration;
            public readonly float OpenDuration;

            public BlinkHarnessSettings(bool enabled, float strength, float minimumInterval,
                float maximumInterval, float closeDuration, float closedDuration, float openDuration)
            {
                Enabled = enabled;
                Strength = strength;
                MinimumInterval = minimumInterval;
                MaximumInterval = maximumInterval;
                CloseDuration = closeDuration;
                ClosedDuration = closedDuration;
                OpenDuration = openDuration;
            }
        }

        private readonly struct GazeHarnessSettings
        {
            public readonly bool GazeEnabled;
            public readonly float AcquireToleranceDegrees;
            public readonly bool HeadEnabled;
            public readonly float HeadWeight;
            public readonly float HeadResponse;
            public readonly float HeadMaxYaw;
            public readonly float HeadMaxPitch;
            public readonly bool EyesEnabled;
            public readonly float EyeWeight;
            public readonly float EyeResponse;
            public readonly float EyeMaxYaw;
            public readonly float EyeMaxPitch;
            public readonly float ReleaseResponse;

            public GazeHarnessSettings(bool gazeEnabled, float acquireToleranceDegrees,
                bool headEnabled, float headWeight, float headResponse, float headMaxYaw,
                float headMaxPitch, bool eyesEnabled, float eyeWeight, float eyeResponse,
                float eyeMaxYaw, float eyeMaxPitch, float releaseResponse)
            {
                GazeEnabled = gazeEnabled;
                AcquireToleranceDegrees = acquireToleranceDegrees;
                HeadEnabled = headEnabled;
                HeadWeight = headWeight;
                HeadResponse = headResponse;
                HeadMaxYaw = headMaxYaw;
                HeadMaxPitch = headMaxPitch;
                EyesEnabled = eyesEnabled;
                EyeWeight = eyeWeight;
                EyeResponse = eyeResponse;
                EyeMaxYaw = eyeMaxYaw;
                EyeMaxPitch = eyeMaxPitch;
                ReleaseResponse = releaseResponse;
            }
        }

        private readonly struct GazePoseSnapshot
        {
            public readonly Quaternion Head;
            public readonly Quaternion LeftEye;
            public readonly Quaternion RightEye;

            public GazePoseSnapshot(Quaternion head, Quaternion leftEye, Quaternion rightEye)
            {
                Head = head;
                LeftEye = leftEye;
                RightEye = rightEye;
            }
        }

        private readonly struct PoseSnapshot
        {
            public readonly TransformSnapshot[] Transforms;
            public readonly float[] BlendShapes;

            public PoseSnapshot(TransformSnapshot[] transforms, float[] blendShapes)
            {
                Transforms = transforms;
                BlendShapes = blendShapes;
            }
        }

        private readonly struct TransformSnapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            public TransformSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }

        private readonly struct PlacementSnapshot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;

            public PlacementSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }
        }
    }
}
