using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private PerformerExpression expressionA = null;
        [SerializeField] private PerformerExpression expressionB = null;
        [SerializeField] private PerformerExpression expressionC = null;
        [SerializeField] private AudioClip speechClipA;
        [SerializeField] private AudioClip speechClipB;
        [SerializeField] private AudioClip speechClipC;
        [SerializeField, Range(0f, 1f)] private float expressionIntensity = 1f;
        [SerializeField, Min(0f)] private float expressionBlendTime = 0.25f;
        [SerializeField] private PoseTransition transition = PoseTransition.Default;
        [SerializeField] private PerformerPoseAcceptanceHarness acceptanceHarness;
        [SerializeField] private Transform gazeTarget;
        [Header("P0.A1 Locomotion")]
        [SerializeField] private Transform locomotionTarget;
        [SerializeField, Min(0.5f)] private float locomotionTestDistance = 5f;
        [Header("P0.B Anchored Seating")]
        [SerializeField] private PerformerSeat seatingTestSeat;
        private Vector2 _scrollPosition;
        private string _seedText = string.Empty;
        private bool _lipSyncCheckRunning;
        private string _lipSyncCheckStatus = "Not run. Use Play Mode with a non-silent Clip A.";
        private string _locomotionCommandStatus = "No locomotion command yet.";
        private string _seatingCommandStatus = "No seating command yet.";
        private Vector3 _locomotionTestOrigin;
        private int _nextLocomotionTestId;
        private readonly List<string> _locomotionResults = new List<string>();

        internal AudioClip SpeechClipA => speechClipA;
        internal AudioClip SpeechClipB => speechClipB;
        internal PerformerPose PoseA => poseA;
        internal PerformerPose PoseB => poseB;
        internal PerformerPose PoseC => poseC;
        internal PerformerSeat SeatingTestSeatForAcceptance => seatingTestSeat;
        internal PerformerExpression ExpressionAForAcceptance => expressionA;

        private void Reset()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
            if (acceptanceHarness == null) acceptanceHarness = GetComponent<PerformerPoseAcceptanceHarness>();
        }

        private void Awake()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
            if (performer != null) _locomotionTestOrigin = performer.transform.position;
        }

        private void Update()
        {
            if (performer == null || !performer.IsRuntimeReady) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) RequestPose(poseA);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RequestPose(poseB);
            if (Input.GetKeyDown(KeyCode.Alpha3)) RequestPose(poseC);
            if (Input.GetKeyDown(KeyCode.F5)) RunAcceptanceChecks();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 430f, 900f), "Performer Pose Smoke Test", GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            var previousGuiEnabled = GUI.enabled;
            var runtimeReady = performer != null && performer.IsRuntimeReady;
            GUI.enabled = previousGuiEnabled && runtimeReady;
            if (!runtimeReady)
                GUILayout.Label("Performer runtime is unavailable. Speech setup is attempted automatically at Play Mode startup; inspect the Console if startup failed.");
            GUILayout.Label("1 / 2 / 3 also selects these persistent poses.");
            DrawPoseButton("Pose A", poseA);
            DrawPoseButton("Pose B", poseB);
            DrawPoseButton("Pose C", poseC);
            if (GUILayout.Button("Run acceptance checks (F5)")) RunAcceptanceChecks();

            if (performer != null)
            {
                DrawBreathingControls();
                DrawExpressionControls();
                DrawSpeechControls();
                DrawLocomotionControls();
                DrawSeatingControls();
                DrawGazeControls();
                DrawAttentionLifeControls();
                var desired = performer.DesiredPose == null ? "none" : performer.DesiredPose.name;
                GUILayout.Label("Desired: " + desired + (performer.IsTransitioning
                    ? "  " + (performer.TransitionProgress * 100f).ToString("F0") + "%"
                    : "  settled"));
            }
            GUI.enabled = previousGuiEnabled;
            if (acceptanceHarness != null) GUILayout.Label(acceptanceHarness.Status);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            if (locomotionTarget != null)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.15f, 1f);
                Gizmos.DrawWireSphere(locomotionTarget.position, 0.12f);
                Gizmos.DrawLine(locomotionTarget.position,
                    locomotionTarget.position + locomotionTarget.forward * 0.45f);
            }

            if (performer == null || !Application.isPlaying || !performer.IsLocomoting) return;
            Vector3 actorPosition = performer.transform.position + Vector3.up * 0.05f;
            Vector3 targetPosition = performer.LocomotionCurrentTarget;
            targetPosition.y = actorPosition.y;
            Vector3 predictedEndpoint = performer.LocomotionPredictedStopEndpoint;
            predictedEndpoint.y = actorPosition.y;

            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            Gizmos.DrawLine(actorPosition, targetPosition);
            Gizmos.DrawLine(actorPosition, actorPosition + performer.transform.forward * 0.6f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(predictedEndpoint, 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(predictedEndpoint, targetPosition);
        }

        private void DrawLocomotionControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("P0.A1 Production WalkTo");
            if (!performer.LocomotionAvailable)
            {
                GUILayout.Label(performer.LocomotionProfile == null
                    ? "No locomotion profile assigned. Bake Walk01, then assign KawaiiWalk01Profile on SuccubusPerformer."
                    : "The locomotion profile did not initialize. Check the Console for its configuration error.");
                return;
            }

            DrawSlider("Test distance (m)", locomotionTestDistance, 0.5f, 8f,
                value => locomotionTestDistance = Mathf.Max(0.5f, value));
            GUILayout.Label("Full Start/Stop distance threshold: "
                + performer.LocomotionProfile.MinimumWalkDistance.ToString("F2") + " m");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ahead")) RequestWalkToDirection(0f);
            if (GUILayout.Button("30° L")) RequestWalkToDirection(-30f);
            if (GUILayout.Button("30° R")) RequestWalkToDirection(30f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("60° L")) RequestWalkToDirection(-60f);
            if (GUILayout.Button("60° R")) RequestWalkToDirection(60f);
            if (GUILayout.Button("90° L")) RequestWalkToDirection(-90f);
            if (GUILayout.Button("90° R")) RequestWalkToDirection(90f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("120° L")) RequestWalkToDirection(-120f);
            if (GUILayout.Button("120° R")) RequestWalkToDirection(120f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Behind-Left")) RequestWalkToDirection(-165f);
            if (GUILayout.Button("Directly Behind")) RequestWalkToDirection(180f);
            if (GUILayout.Button("Behind-Right")) RequestWalkToDirection(165f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Return to start")) RequestWalkTo(_locomotionTestOrigin);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(locomotionTarget == null
                    ? "WalkTo Transform (assign target)"
                    : "WalkTo Transform: " + locomotionTarget.name))
            {
                if (locomotionTarget == null)
                    Debug.LogWarning("Assign a target Transform to the P0.A1 Locomotion Target field.", this);
                else RequestWalkTo(locomotionTarget);
            }

            GUILayout.Label("Request: " + _locomotionCommandStatus);
            GUILayout.Label("State: " + performer.LocomotionState + "    active: " + performer.IsLocomoting);
            GUILayout.Label("Actor position: " + performer.transform.position.ToString("F3"));
            GUILayout.Label("Motion: " + performer.LocomotionCurrentMotion
                + "    clip time: " + performer.LocomotionPlaybackTime.ToString("F2") + " s"
                + "    playables: " + performer.RuntimePlayableCount);
            GUILayout.Label("Turn: " + performer.LocomotionSelectedTurn + "    stop: " + performer.LocomotionSelectedStop);
            GUILayout.Label("Gait phase: " + performer.LocomotionGaitPhase.ToString("F3")
                + "    remaining: " + performer.LocomotionRemainingDistance.ToString("F2") + " m"
                + "    heading: " + performer.LocomotionHeadingError.ToString("F1") + "°");
            GUILayout.Label("Predicted stop distance: " + performer.LocomotionPredictedStopDistance.ToString("F2") + " m");
            GUILayout.Label("Predicted endpoint: " + performer.LocomotionPredictedStopEndpoint.ToString("F2"));
            GUILayout.Label("Endpoint correction: " + performer.LocomotionEndpointCorrection.ToString("F3")
                + " (" + performer.LocomotionEndpointCorrection.magnitude.ToString("F3") + " m)");
            GUILayout.Label("Arrival blend: " + (performer.LocomotionArrivalBlendProgress * 100f).ToString("F0")
                + "%    target: " + performer.LocomotionCurrentTarget.ToString("F3")
                + "    actual: " + performer.transform.position.ToString("F3"));
            for (int i = _locomotionResults.Count - 1; i >= 0; i--)
                GUILayout.Label("Result: " + _locomotionResults[i]);
        }

        private void RequestWalkToDirection(float angleDegrees)
        {
            if (performer == null) return;
            Vector3 direction = Quaternion.AngleAxis(angleDegrees, Vector3.up) * performer.transform.forward;
            direction.y = 0f;
            RequestWalkTo(performer.transform.position + direction.normalized * locomotionTestDistance);
        }

        private async void RequestWalkTo(Vector3 destination)
        {
            if (performer == null) return;
            int id = ++_nextLocomotionTestId;
            _locomotionCommandStatus = "#" + id + " walking to " + destination.ToString("F2");
            try
            {
                LocomotionCompletion result = await performer.WalkToAsync(destination);
                RecordLocomotionResult(id, result);
            }
            catch (Exception exception)
            {
                _locomotionCommandStatus = "#" + id + " rejected: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private async void RequestWalkTo(Transform target)
        {
            if (performer == null || target == null) return;
            int id = ++_nextLocomotionTestId;
            _locomotionCommandStatus = "#" + id + " walking to Transform " + target.name;
            try
            {
                LocomotionCompletion result = await performer.WalkToAsync(target);
                RecordLocomotionResult(id, result);
            }
            catch (Exception exception)
            {
                _locomotionCommandStatus = "#" + id + " rejected: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void RecordLocomotionResult(int id, LocomotionCompletion result)
        {
            _locomotionCommandStatus = "#" + id + " " + result;
            _locomotionResults.Add("#" + id + " " + result);
            if (_locomotionResults.Count > 5) _locomotionResults.RemoveAt(0);
        }

        private void DrawSeatingControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("P0.B Anchored Seating");
            if (seatingTestSeat == null)
            {
                GUILayout.Label("No chair fixture is assigned. Run Tools > DAZ Pose > Development > Setup or Refresh Performer Pose Acceptance Harness.");
                return;
            }
            if (seatingTestSeat.SeatingProfile == null)
                GUILayout.Label("Bake first: Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara. Then refresh this harness to assign the profile.");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sit Default")) RequestSitDefault();
            if (GUILayout.Button("Sit Basic")) RequestSit(PerformerSeatedStyle.Basic);
            if (GUILayout.Button("Sit CrossLegs")) RequestSit(PerformerSeatedStyle.CrossLegs);
            if (GUILayout.Button("Stand Up")) RequestStandUp();
            GUILayout.EndHorizontal();
            if (performer.SeatingState == PerformerSeatingState.BasicSeated)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Hold Sit_Start frame")) SetBasicIdleCandidate(false);
                if (GUILayout.Button("Preview Idle10 Sit Loop")) SetBasicIdleCandidate(true);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Request: " + _seatingCommandStatus);
            GUILayout.Label("State: " + performer.SeatingState + "    seat: "
                + (performer.CurrentSeat == null ? "none" : performer.CurrentSeat.name)
                + "    style: " + performer.CurrentSeatedStyle);
            GUILayout.Label("Motion: " + performer.SeatingCurrentMotion
                + "    time: " + performer.SeatingMotionTime.ToString("F2") + " s"
                + "    seating weight: " + performer.SeatingOwnershipWeight.ToString("F2"));
            GUILayout.Label("Seat contact error: " + performer.SeatingContactError.ToString("F3")
                + " (" + performer.SeatingContactError.magnitude.ToString("F3") + " m)");
            GUILayout.Label("CrossLegs exit blend: " + performer.SeatingCrossLegsExitBlendProgress.ToString("F2")
                + "    duration: " + performer.SeatingCrossLegsExitBlendDuration.ToString("F2") + " s");
            GUILayout.Label("Use the expression, gaze, breathing, blink, and speech controls while she is seated to verify those layers remain active.");
        }

        private async void RequestSit(PerformerSeatedStyle style)
        {
            if (performer == null || seatingTestSeat == null) return;
            _seatingCommandStatus = "Walking to " + seatingTestSeat.name + " as " + style;
            try
            {
                SeatingCompletion result = await performer.SitAtAsync(seatingTestSeat, style);
                _seatingCommandStatus = "SitAt " + result;
            }
            catch (Exception exception)
            {
                _seatingCommandStatus = "SitAt rejected: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private async void RequestSitDefault()
        {
            if (performer == null || seatingTestSeat == null) return;
            _seatingCommandStatus = "Walking to " + seatingTestSeat.name + " using its default style";
            try
            {
                SeatingCompletion result = await performer.SitAtAsync(seatingTestSeat);
                _seatingCommandStatus = "SitAt default " + result;
            }
            catch (Exception exception)
            {
                _seatingCommandStatus = "SitAt default rejected: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private async void RequestStandUp()
        {
            if (performer == null) return;
            _seatingCommandStatus = "Standing up";
            try
            {
                SeatingCompletion result = await performer.StandUpAsync();
                _seatingCommandStatus = "StandUp " + result;
            }
            catch (Exception exception)
            {
                _seatingCommandStatus = "StandUp rejected: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void SetBasicIdleCandidate(bool enabled)
        {
            try
            {
                performer.SetBasicIdleLoopCandidateForAcceptance(enabled);
                _seatingCommandStatus = enabled
                    ? "Auditioning KA_Idle10_Sit_Loop; click Hold Sit_Start frame to restore the selected Basic state."
                    : "Using the selected Basic hold: final frame of KA_Sit_Start.";
            }
            catch (Exception exception)
            {
                _seatingCommandStatus = "Basic seated loop rejected: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void DrawExpressionControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("P0.8 Persistent Expression");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Expression A")) RequestExpression(expressionA);
            if (GUILayout.Button("Expression B")) RequestExpression(expressionB);
            if (GUILayout.Button("Expression C")) RequestExpression(expressionC);
            if (GUILayout.Button("Clear")) performer.ClearExpression(expressionBlendTime);
            GUILayout.EndHorizontal();
            DrawSlider("Expression intensity", expressionIntensity, 0f, 1f, value => expressionIntensity = value);
            DrawSlider("Expression blend (s)", expressionBlendTime, 0f, 2f, value => expressionBlendTime = value);
            var desired = performer.DesiredExpression == null ? "none" : performer.DesiredExpression.name;
            var settled = performer.SettledExpression == null ? "none" : performer.SettledExpression.name;
            GUILayout.Label("Desired: " + desired + "  Settled: " + settled + "  Progress: "
                            + (performer.ExpressionTransitionProgress * 100f).ToString("F0") + "%");
        }

        private void RequestExpression(PerformerExpression expression)
        {
            if (expression != null) performer.Expression(expression, expressionIntensity, expressionBlendTime);
            else Debug.LogWarning("Assign this PerformerExpression on the smoke harness first.", this);
        }

        private void DrawSpeechControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("P0.9A Queued Speech");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Say A")) SaySpeech(speechClipA, "A");
            if (GUILayout.Button("Say B")) SaySpeech(speechClipB, "B");
            if (GUILayout.Button("Say C")) SaySpeech(speechClipC, "C");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Queue A+B+C"))
            {
                if (speechClipA == null || speechClipB == null || speechClipC == null)
                    Debug.LogWarning("Assign Speech Clip A, B, and C before queueing the three-line sequence.", this);
                else
                {
                    performer.Say(speechClipA);
                    performer.Say(speechClipB);
                    performer.Say(speechClipC);
                }
            }
            if (GUILayout.Button("Stop Speaking")) performer.StopSpeaking();
            GUILayout.EndHorizontal();

            var current = performer.CurrentSpeechClip == null ? "none" : performer.CurrentSpeechClip.name;
            var source = performer.SpeechAudioSource;
            GUILayout.Label("Speaking: " + performer.IsSpeaking + "  Current: " + current
                            + "  Pending: " + performer.PendingSpeechCount);
            GUILayout.Label("Speech AudioSource playing: " + (source != null && source.isPlaying));

            var lipSync = performer.GetComponent<PerformerSalsaLipSync>();
            GUILayout.Label("P0.9B SALSA: " + (lipSync == null ? "not configured" : lipSync.Status));
            if (lipSync != null)
            {
                GUILayout.Label("Ready: " + lipSync.IsReady + "  IsSALSAing: " + lipSync.IsSALSAing
                    + "  Analysis: " + lipSync.AnalysisValue.ToString("F3")
                    + "  Trigger: " + lipSync.TriggeredIndex + " / " + lipSync.CurrentViseme
                    + "  Visemes: " + lipSync.VisemeCount
                    + "  AudioSource matches: " + lipSync.UsesSpeechAudioSource);
                GUILayout.Label("Bindings: " + lipSync.BindingSummary);
                GUILayout.Label("Speech weights: " + lipSync.WeightSummary);
            }
            if (GUILayout.Button(_lipSyncCheckRunning ? "Checking speech articulation…" : "Check Clip A articulation"))
                StartLipSyncCheck();
            GUILayout.Label("Lip-sync check: " + _lipSyncCheckStatus);
        }

        private void StartLipSyncCheck()
        {
            if (_lipSyncCheckRunning) return;
            if (speechClipA == null)
            {
                _lipSyncCheckStatus = "Assign Speech Clip A on the smoke harness first.";
                return;
            }
            var lipSync = performer == null ? null : performer.GetComponent<PerformerSalsaLipSync>();
            if (performer == null || lipSync == null || !lipSync.IsReady)
            {
                _lipSyncCheckStatus = "P0.9B SALSA is not ready. Run the editor setup command and inspect the Console.";
                return;
            }
            if (performer.IsSpeaking || performer.PendingSpeechCount != 0)
            {
                _lipSyncCheckStatus = "Wait until the existing FIFO speech queue is idle, then run this check.";
                return;
            }
            StartCoroutine(CheckClipArticulation(speechClipA, lipSync));
        }

        private IEnumerator CheckClipArticulation(AudioClip clip, PerformerSalsaLipSync lipSync)
        {
            _lipSyncCheckRunning = true;
            _lipSyncCheckStatus = "Playing Clip A and sampling analysis, triggers, and all catalog weights.";
            if (!PerformerLipSyncMorphCatalog.TryResolveBindings(performer.GetComponent<Animator>(),
                    out var bindings, out var bindingFailure))
            {
                _lipSyncCheckStatus = "FAIL: " + bindingFailure;
                _lipSyncCheckRunning = false;
                yield break;
            }

            var analysisPeak = 0f;
            var speechWeightPeak = 0f;
            var maximumAllowedWeight = 0f;
            var speechWeightOverLimit = false;
            foreach (var binding in bindings)
                maximumAllowedWeight = Mathf.Max(maximumAllowedWeight, binding.Definition.MaximumUnityWeight);
            var speechAudioSource = performer.SpeechAudioSource;
            var audioSourceStable = speechAudioSource != null && lipSync.UsesSpeechAudioSource;
            var sawPlayback = false;
            var sawSalsaing = false;
            var sawTrigger = false;
            var triggerCount = new HashSet<int>();
            var deadline = Time.realtimeSinceStartup + Mathf.Max(2f, clip.length + 2f);
            performer.Say(clip);
            while (performer.CurrentSpeechClip == clip && Time.realtimeSinceStartup < deadline)
            {
                var source = performer.SpeechAudioSource;
                audioSourceStable &= source == speechAudioSource && lipSync.UsesSpeechAudioSource;
                sawPlayback |= source != null && source.isPlaying;
                analysisPeak = Mathf.Max(analysisPeak, lipSync.AnalysisValue);
                sawSalsaing |= lipSync.IsSALSAing;
                var trigger = lipSync.TriggeredIndex;
                if (trigger >= 0)
                {
                    sawTrigger = true;
                    triggerCount.Add(trigger);
                }
                foreach (var binding in bindings)
                {
                    var mesh = binding.Renderer == null ? null : binding.Renderer.sharedMesh;
                    if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount
                        || mesh.GetBlendShapeName(binding.Index) != binding.Definition.BlendShapeName) continue;
                    var currentWeight = Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.Index)
                        - binding.Definition.RestUnityWeight);
                    speechWeightPeak = Mathf.Max(speechWeightPeak, currentWeight);
                    speechWeightOverLimit |= currentWeight > binding.Definition.MaximumUnityWeight + 1f;
                }
                yield return null;
            }

            var speechEnded = !performer.IsSpeaking && performer.PendingSpeechCount == 0;
            if (!speechEnded) performer.StopSpeaking();

            var maxOff = 0f;
            foreach (var binding in bindings) maxOff = Mathf.Max(maxOff, binding.Definition.DurationOff);
            var releaseTimeout = Mathf.Max(0.35f, (lipSync.AudioUpdateDelay + maxOff) * 3f);
            var releaseDeadline = Time.realtimeSinceStartup + releaseTimeout;
            var released = false;
            var residualPeak = 0f;
            while (Time.realtimeSinceStartup < releaseDeadline)
            {
                audioSourceStable &= performer.SpeechAudioSource == speechAudioSource && lipSync.UsesSpeechAudioSource;
                residualPeak = 0f;
                foreach (var binding in bindings)
                {
                    var mesh = binding.Renderer == null ? null : binding.Renderer.sharedMesh;
                    if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount
                        || mesh.GetBlendShapeName(binding.Index) != binding.Definition.BlendShapeName) continue;
                    var currentWeight = Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.Index)
                        - binding.Definition.RestUnityWeight);
                    residualPeak = Mathf.Max(residualPeak, currentWeight);
                    speechWeightOverLimit |= currentWeight > binding.Definition.MaximumUnityWeight + 1f;
                }
                if (residualPeak <= 0.5f) { released = true; break; }
                yield return null;
            }

            var passed = sawPlayback && analysisPeak > 0.01f && sawSalsaing && sawTrigger
                && speechWeightPeak > 0.5f && !speechWeightOverLimit && speechEnded && released && audioSourceStable;
            _lipSyncCheckStatus = (passed ? "PASS" : "FAIL")
                + " playback=" + sawPlayback + " analysisPeak=" + analysisPeak.ToString("F3")
                + " SALSAing=" + sawSalsaing + " triggers=" + triggerCount.Count
                + " speechWeightPeak=" + speechWeightPeak.ToString("F2")
                + " maxAllowedUnityWeight=" + maximumAllowedWeight.ToString("F2")
                + " overLimit=" + speechWeightOverLimit
                + " audioSourceStable=" + audioSourceStable
                + " ended=" + speechEnded + " released=" + released
                + " residual=" + residualPeak.ToString("F2") + " releaseTimeout=" + releaseTimeout.ToString("F2") + "s";
            Debug.Log("P0.9B audio/articulation check " + _lipSyncCheckStatus, this);
            _lipSyncCheckRunning = false;
        }

        private void SaySpeech(AudioClip clip, string label)
        {
            if (clip == null)
            {
                Debug.LogWarning("Assign Speech Clip " + label + " on the Performer Pose Smoke Harness first.", this);
                return;
            }
            performer.Say(clip);
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

        private void DrawGazeControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Gaze");
            performer.GazeEnabled = GUILayout.Toggle(performer.GazeEnabled, "Gaze enabled");
            DrawSlider("Acquire tolerance", performer.GazeAcquireToleranceDegrees, 0.1f, 10f,
                value => performer.GazeAcquireToleranceDegrees = value);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Look At Gaze Target"))
            {
                if (gazeTarget != null) performer.LookAt(gazeTarget);
                else Debug.LogWarning("Assign the scene's Gaze Target to the smoke harness first.", this);
            }
            if (GUILayout.Button("Look At Camera"))
            {
                var camera = Camera.main;
                if (camera != null) performer.LookAt(camera.transform);
                else Debug.LogWarning("No MainCamera is tagged in the validation scene.", this);
            }
            if (GUILayout.Button("Clear Gaze")) performer.ClearGaze();
            GUILayout.EndHorizontal();

            var gazeState = !performer.GazeEnabled ? "Bypassed"
                : !performer.HasGazeTarget ? (performer.GazeWeight > 0.01f ? "Releasing" : "Released")
                : performer.IsGazeAcquired ? "Acquired" : "Acquiring";
            GUILayout.Label("Target: " + performer.GazeTargetDescription + "    " + gazeState);
            GUILayout.Label("Weight " + performer.GazeWeight.ToString("F2"));
            if (performer.HasGazeTarget)
            {
                GUILayout.Label("Raw: " + performer.RawGazeTargetPosition.ToString("F2"));
                GUILayout.Label("Effective head: " + performer.EffectiveHeadTargetPosition.ToString("F2"));
                GUILayout.Label("Effective eyes: " + performer.EffectiveEyeTargetPosition.ToString("F2"));
            }

            GUILayout.Label("Head");
            performer.HeadGazeEnabled = GUILayout.Toggle(performer.HeadGazeEnabled, "Head gaze enabled");
            DrawSlider("Head weight", performer.HeadGazeWeight, 0f, 1f,
                value => performer.HeadGazeWeight = value);
            DrawSlider("Head response", performer.HeadGazeResponse, 0f, 20f,
                value => performer.HeadGazeResponse = value);
            DrawSlider("Head max yaw", performer.HeadGazeMaxYaw, 0f, 89f,
                value => performer.HeadGazeMaxYaw = value);
            DrawSlider("Head max pitch", performer.HeadGazeMaxPitch, 0f, 89f,
                value => performer.HeadGazeMaxPitch = value);

            GUILayout.Label("Eyes");
            performer.EyeGazeEnabled = GUILayout.Toggle(performer.EyeGazeEnabled, "Eye gaze enabled");
            DrawSlider("Eye weight", performer.EyeGazeWeight, 0f, 1f,
                value => performer.EyeGazeWeight = value);
            DrawSlider("Eye response", performer.EyeGazeResponse, 0f, 30f,
                value => performer.EyeGazeResponse = value);
            DrawSlider("Eye max yaw", performer.EyeGazeMaxYaw, 0f, 89f,
                value => performer.EyeGazeMaxYaw = value);
            DrawSlider("Eye max pitch", performer.EyeGazeMaxPitch, 0f, 89f,
                value => performer.EyeGazeMaxPitch = value);
            DrawSlider("Release response", performer.GazeReleaseResponse, 0f, 20f,
                value => performer.GazeReleaseResponse = value);
        }

        private void DrawAttentionLifeControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("P0.7 Attention Life");
            performer.AttentionLifeEnabled = GUILayout.Toggle(
                performer.AttentionLifeEnabled, "Attention life enabled");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", GUILayout.Width(150f));
            var seedValue = GUILayout.TextField(performer.AttentionLifeSeed.ToString(), GUILayout.Width(100f));
            if (seedValue != _seedText) _seedText = seedValue;
            if (int.TryParse(_seedText, out var seed) && seed != performer.AttentionLifeSeed)
                performer.AttentionLifeSeed = seed;
            if (GUILayout.Button("Reset seed", GUILayout.Width(85f)))
            {
                performer.AttentionLifeSeed = 12345;
                _seedText = "12345";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("P0.6 baseline")) SetComparisonMode(false, false, false, false);
            if (GUILayout.Button("Fixation only")) SetComparisonMode(true, true, false, false);
            if (GUILayout.Button("Head only")) SetComparisonMode(true, false, true, false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Blink only")) SetComparisonMode(false, false, false, true);
            if (GUILayout.Button("Full P0.7")) SetComparisonMode(true, true, true, true);
            if (GUILayout.Button("Exaggerate")) SetExaggeratedSettings();
            GUILayout.EndHorizontal();

            GUILayout.Label("Eye Fixation");
            performer.EyeFixationLifeEnabled = GUILayout.Toggle(
                performer.EyeFixationLifeEnabled, "Eye fixation enabled");
            DrawSlider("Max horizontal (deg)", performer.EyeFixationMaxHorizontalDegrees, 0f, 8f,
                value => performer.EyeFixationMaxHorizontalDegrees = value);
            DrawSlider("Max vertical (deg)", performer.EyeFixationMaxVerticalDegrees, 0f, 6f,
                value => performer.EyeFixationMaxVerticalDegrees = value);
            DrawSlider("Hold min (s)", performer.EyeFixationMinimumHoldSeconds, 0.1f, 10f,
                value => performer.EyeFixationMinimumHoldSeconds = value);
            DrawSlider("Hold max (s)", performer.EyeFixationMaximumHoldSeconds, 0.1f, 15f,
                value => performer.EyeFixationMaximumHoldSeconds = value);
            DrawSlider("Center bias", performer.EyeFixationCenterBias, 1f, 4f,
                value => performer.EyeFixationCenterBias = value);
            GUILayout.Label("Offset H " + performer.EyeFixationHorizontalOffset.ToString("F2")
                            + "°  V " + performer.EyeFixationVerticalOffset.ToString("F2")
                            + "°    next event " + performer.EyeFixationEventCountdown.ToString("F1") + " s");

            GUILayout.Label("Head Attention Life");
            performer.HeadAttentionLifeEnabled = GUILayout.Toggle(
                performer.HeadAttentionLifeEnabled, "Head attention enabled");
            DrawSlider("Max side tilt (deg)", performer.HeadAttentionMaxTiltDegrees, 0f, 15f,
                value => performer.HeadAttentionMaxTiltDegrees = value);
            DrawSlider("Max chin nod (deg)", performer.HeadAttentionMaxChinDegrees, 0f, 12f,
                value => performer.HeadAttentionMaxChinDegrees = value);
            DrawSlider("Hold min (s)", performer.HeadAttentionMinimumHoldSeconds, 0.1f, 30f,
                value => performer.HeadAttentionMinimumHoldSeconds = value);
            DrawSlider("Hold max (s)", performer.HeadAttentionMaximumHoldSeconds, 0.1f, 45f,
                value => performer.HeadAttentionMaximumHoldSeconds = value);
            DrawSlider("Transition response", performer.HeadAttentionTransitionResponse, 0f, 2f,
                value => performer.HeadAttentionTransitionResponse = value);
            GUILayout.Label("Current side tilt " + performer.HeadAttentionTilt.ToString("F2")
                            + "°  chin nod " + performer.HeadAttentionChin.ToString("F2")
                            + "°    next event " + performer.HeadAttentionEventCountdown.ToString("F1") + " s");

            GUILayout.Label("Autonomous Blink");
            performer.BlinkEnabled = GUILayout.Toggle(performer.BlinkEnabled, "Blink enabled");
            DrawSlider("Strength", performer.BlinkStrength, 0f, 1f,
                value => performer.BlinkStrength = value);
            DrawSlider("Interval min (s)", performer.BlinkMinimumIntervalSeconds, 0.5f, 15f,
                value => performer.BlinkMinimumIntervalSeconds = value);
            DrawSlider("Interval max (s)", performer.BlinkMaximumIntervalSeconds, 0.5f, 20f,
                value => performer.BlinkMaximumIntervalSeconds = value);
            DrawSlider("Close (s)", performer.BlinkCloseDurationSeconds, 0.02f, 0.4f,
                value => performer.BlinkCloseDurationSeconds = value);
            DrawSlider("Closed (s)", performer.BlinkClosedDurationSeconds, 0.01f, 0.25f,
                value => performer.BlinkClosedDurationSeconds = value);
            DrawSlider("Open (s)", performer.BlinkOpenDurationSeconds, 0.02f, 0.5f,
                value => performer.BlinkOpenDurationSeconds = value);
            GUILayout.Label("State " + performer.BlinkState + "    closure "
                            + performer.BlinkClosure.ToString("F2") + "    next blink "
                            + performer.BlinkCountdown.ToString("F1") + " s");
            var blink = performer.BlinkRuntime;
            GUILayout.Label("Morph binding " + (performer.BlinkResolutionAvailable ? "resolved" : "MISSING"));
            if (blink != null)
            {
                foreach (var binding in blink.Bindings)
                    GUILayout.Label(binding.SemanticName + ": " + binding.ImportedBlendShapeName
                                    + " @ " + binding.RendererPath + " (max "
                                    + binding.PositiveMaximumWeight.ToString("F2") + ")");
            }
        }

        private void SetComparisonMode(bool attention, bool fixation, bool head, bool blink)
        {
            performer.AttentionLifeEnabled = attention;
            performer.EyeFixationLifeEnabled = fixation;
            performer.HeadAttentionLifeEnabled = head;
            performer.BlinkEnabled = blink;
        }

        private void SetExaggeratedSettings()
        {
            performer.EyeFixationMaxHorizontalDegrees = 4f;
            performer.EyeFixationMaxVerticalDegrees = 3f;
            performer.EyeFixationMinimumHoldSeconds = 0.8f;
            performer.EyeFixationMaximumHoldSeconds = 1.8f;
            performer.HeadAttentionMaxTiltDegrees = 10f;
            performer.HeadAttentionMaxChinDegrees = 8f;
            performer.HeadAttentionMinimumHoldSeconds = 2f;
            performer.HeadAttentionMaximumHoldSeconds = 4f;
            performer.BlinkMinimumIntervalSeconds = 2f;
            performer.BlinkMaximumIntervalSeconds = 4f;
            performer.BlinkCloseDurationSeconds = 0.22f;
            performer.BlinkClosedDurationSeconds = 0.12f;
            performer.BlinkOpenDurationSeconds = 0.4f;
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
