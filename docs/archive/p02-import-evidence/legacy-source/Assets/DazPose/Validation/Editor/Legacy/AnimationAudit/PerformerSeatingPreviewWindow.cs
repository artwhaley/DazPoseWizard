using System;
using System.Collections.Generic;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.AnimationAudit
{
    /// <summary>Samples baked seating clips on a disposable scene copy and exposes editable seat anchors.</summary>
    public sealed class PerformerSeatingPreviewWindow : EditorWindow
    {
        private enum MotionKind
        {
            SitStart,
            CrossLegsStart,
            CrossLegsLoop,
            CrossLegsEnd,
            SitEnd,
            BasicIdleCandidate
        }

        private static readonly MotionKind[] MotionKinds = (MotionKind[])Enum.GetValues(typeof(MotionKind));
        private static readonly string[] MotionLabels =
        {
            "Sit Start", "Cross Legs Start", "Cross Legs Loop", "Cross Legs End", "Sit End", "Basic Idle Candidate"
        };

        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformerSeat seat;
        [SerializeField] private MotionKind motionKind = MotionKind.SitStart;
        [SerializeField] private bool showAnchorHandles = true;
        [SerializeField] private bool previewSitToCrossBlend = true;
        [SerializeField] private bool applyFinalAnchorCorrection = true;

        private GameObject previewActor;
        private Transform[] previewTransforms;
        private Vector3[] firstPosePositions;
        private Quaternion[] firstPoseRotations;
        private Vector3[] firstPoseScales;
        private Vector3[] secondPosePositions;
        private Quaternion[] secondPoseRotations;
        private Vector3[] secondPoseScales;
        private bool ownsAnimationMode;
        private bool playing;
        private bool sampleDirty = true;
        private int frame;
        private float playbackTime;
        private double lastUpdateTime;
        private Vector3 lastSeatPosition;
        private Quaternion lastSeatRotation;
        private Vector3 lastApproachPosition;
        private Quaternion lastApproachRotation;
        private Vector3 lastPerformerScale;
        private string status;

        [MenuItem("Tools/DAZ Pose/Seating/Edit Mode Preview")]
        public static void ShowWindow()
        {
            var window = GetWindow<PerformerSeatingPreviewWindow>();
            window.titleContent = new GUIContent("Seating Preview");
            window.minSize = new Vector2(360f, 300f);
            window.ResolveSelection();
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Seating Preview");
            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged += MarkSampleDirty;
            SceneView.duringSceneGui += OnSceneGUI;
            ResolveSelection();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.projectChanged -= MarkSampleDirty;
            SceneView.duringSceneGui -= OnSceneGUI;
            StopPreview();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            performer = (SuccubusPerformer)EditorGUILayout.ObjectField(
                "Performer", performer, typeof(SuccubusPerformer), true);
            seat = (PerformerSeat)EditorGUILayout.ObjectField("Seat", seat, typeof(PerformerSeat), true);
            if (EditorGUI.EndChangeCheck())
            {
                StopPreview();
                frame = 0;
                playing = false;
            }

            DrawAnchorButtons();
            EditorGUILayout.Space(4f);

            PerformerSeatingProfile profile = seat != null ? seat.SeatingProfile : null;
            if (performer == null || seat == null)
            {
                EditorGUILayout.HelpBox("Assign the scene's SuccubusPerformer and PerformerSeat. Selecting either component before opening this window fills its field automatically.", MessageType.Info);
                DrawPreviewToggle(profile);
                return;
            }
            if (seat.SeatAnchor == null || seat.ApproachAnchor == null)
            {
                EditorGUILayout.HelpBox("Assign both ApproachAnchor and SeatAnchor before previewing.", MessageType.Error);
                DrawPreviewToggle(profile);
                return;
            }
            if (profile == null)
            {
                EditorGUILayout.HelpBox("The selected PerformerSeat has no seating profile assigned.", MessageType.Error);
                DrawPreviewToggle(profile);
                return;
            }
            if (!profile.IsReady(out string reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.Error);
                EditorGUILayout.HelpBox("After updating the seating scripts, run Tools > DAZ Pose > Seating > Bake KAWAII Seating for Generic Lara. The current generated assets predate the seat-contact rebase.", MessageType.Warning);
                DrawPreviewToggle(profile);
                return;
            }

            DrawPreviewToggle(profile);
            if (previewActor == null) return;

            EditorGUILayout.Space(5f);
            if (GetMotion(profile, motionKind) == null)
                motionKind = MotionKind.CrossLegsStart;
            int selectedIndex = Array.IndexOf(MotionKinds, motionKind);
            int availableIndex = GetAvailableMotionIndex(profile, selectedIndex);
            string[] labels = GetAvailableMotionLabels(profile);
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("Motion", availableIndex, labels);
            if (EditorGUI.EndChangeCheck())
            {
                motionKind = GetAvailableMotionKind(profile, nextIndex);
                frame = 0;
                playing = false;
                SampleCurrentFrame(profile);
            }

            PerformerSeatingMotion motion = GetMotion(profile, motionKind);
            if (motion == null || motion.BodyClip == null)
            {
                EditorGUILayout.HelpBox("The selected motion has no baked body clip.", MessageType.Warning);
                return;
            }

            int frameCount = Mathf.Max(1, Mathf.CeilToInt(motion.DurationSeconds * Mathf.Max(1f, motion.BodyClip.frameRate)));
            int maxFrame = motion.Looping ? frameCount - 1 : frameCount;
            int minFrame = Mathf.FloorToInt(GetEntryTime(profile, motion) * Mathf.Max(1f, motion.BodyClip.frameRate));
            frame = Mathf.Clamp(frame, minFrame, maxFrame);
            EditorGUI.BeginChangeCheck();
            frame = EditorGUILayout.IntSlider("Frame", frame, minFrame, maxFrame);
            if (EditorGUI.EndChangeCheck())
            {
                playing = false;
                SampleCurrentFrame(profile);
            }

            float sampleTime = GetSampleTime(profile, motion);
            EditorGUILayout.LabelField("Time", sampleTime.ToString("0.000") + " / " + motion.DurationSeconds.ToString("0.000") + " s");
            if (motionKind == MotionKind.CrossLegsStart)
            {
                EditorGUI.BeginChangeCheck();
                previewSitToCrossBlend = EditorGUILayout.Toggle("Blend from held Sit Start", previewSitToCrossBlend);
                if (EditorGUI.EndChangeCheck()) SampleCurrentFrame(profile);
                if (previewSitToCrossBlend && profile.BodyBlendSeconds > 0f)
                {
                    float blendProgress = Mathf.Clamp01(sampleTime
                        / Mathf.Max(0.0001f, profile.BodyBlendSeconds * profile.PlaybackSpeed));
                    EditorGUILayout.LabelField("Sit → Cross Legs blend", (blendProgress * 100f).ToString("0") + "%");
                }
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "Pause" : "Play"))
            {
                playbackTime = GetSampleTime(profile, motion);
                if (!playing && playbackTime >= motion.DurationSeconds)
                {
                    playbackTime = GetEntryTime(profile, motion);
                    frame = Mathf.RoundToInt(playbackTime * motion.BodyClip.frameRate);
                }
                playing = !playing;
                lastUpdateTime = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("First Frame"))
            {
                playing = false;
                frame = minFrame;
                SampleCurrentFrame(profile);
            }
            if (!motion.Looping && GUILayout.Button("Last Frame"))
            {
                playing = false;
                frame = maxFrame;
                SampleCurrentFrame(profile);
            }
            EditorGUILayout.EndHorizontal();

            Vector3 pelvisOffset = motion.PelvisOffsetAt(motion.DurationSeconds <= 0f
                ? 0f : sampleTime / motion.DurationSeconds);
            Vector3 pelvisWorld = previewActor.transform.position + previewActor.transform.rotation
                * Vector3.Scale(previewActor.transform.lossyScale, pelvisOffset);
            Vector3 contactDelta = pelvisWorld - seat.SeatAnchor.position;
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Actor root", motionKind == MotionKind.SitStart
                ? "Approach → Seat, using playback root motion"
                : motionKind == MotionKind.SitEnd ? "Seat → Approach, using playback root motion"
                : "Held at seated root, as in playback");
            EditorGUI.BeginChangeCheck();
            applyFinalAnchorCorrection = EditorGUILayout.Toggle("Apply playback final correction", applyFinalAnchorCorrection);
            if (EditorGUI.EndChangeCheck()) SampleCurrentFrame(profile);
            GetNaturalSitLanding(profile, out Vector3 landingPosition, out Quaternion landingRotation);
            Vector3 naturalContact = landingPosition + landingRotation
                * Vector3.Scale(previewActor.transform.lossyScale, profile.SitStart.PelvisOffsetAt(1f));
            Vector3 naturalError = naturalContact - seat.SeatAnchor.position;
            EditorGUILayout.LabelField("Natural Sit Start landing error", naturalError.ToString("F3") + " m");
            EditorGUILayout.LabelField("Natural landing facing error", Quaternion.Angle(landingRotation,
                YawRotation(seat.SeatAnchor.forward)).ToString("F1") + "°");
            EditorGUILayout.LabelField("Baked pelvis offset", pelvisOffset.ToString("F3") + " m");
            EditorGUILayout.LabelField("Pelvis − SeatAnchor", contactDelta.ToString("F3") + " m");
            EditorGUILayout.LabelField("Skeleton rebase", profile.CrossLegsBodyRootOffset.ToString("F3") + " m");
            showAnchorHandles = EditorGUILayout.ToggleLeft("Show draggable Approach and Seat anchor handles in Scene view", showAnchorHandles);
            EditorGUILayout.HelpBox("Sit Start begins at ApproachAnchor and moves to SeatAnchor using the same root trajectory and final correction as playback. Disable final correction to inspect the natural landing. Drag either anchor or use Fit Approach to Sit Start to align the animation. Seated clips hold the actor root fixed.", MessageType.Info);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Warning);
        }

        private void DrawPreviewToggle(PerformerSeatingProfile profile)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(previewActor != null || performer == null || seat == null
                || seat.SeatAnchor == null || seat.ApproachAnchor == null
                || profile == null || !profile.IsReady(out _)))
            {
                if (GUILayout.Button("Enable Preview")) StartPreview(profile);
            }
            using (new EditorGUI.DisabledScope(previewActor == null))
            {
                if (GUILayout.Button("Disable Preview")) StopPreview();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAnchorButtons()
        {
            using (new EditorGUI.DisabledScope(seat == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select Approach Anchor") && seat.ApproachAnchor != null)
                    SelectAndFrame(seat.ApproachAnchor);
                if (GUILayout.Button("Select Seat Anchor") && seat.SeatAnchor != null)
                    SelectAndFrame(seat.SeatAnchor);
                EditorGUILayout.EndHorizontal();
            }
            PerformerSeatingProfile profile = seat != null ? seat.SeatingProfile : null;
            using (new EditorGUI.DisabledScope(performer == null || seat == null
                || seat.ApproachAnchor == null || seat.SeatAnchor == null
                || profile == null || !profile.IsReady(out _)))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Play Sit Down from Approach"))
                    PreviewSitStart(profile, false, true);
                if (GUILayout.Button("Show Seated End Pose"))
                    PreviewSitStart(profile, true, false);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("Fit Approach to Sit Start")) FitApproachToSitStart(profile);
            }
        }

        private void PreviewSitStart(PerformerSeatingProfile profile, bool lastFrame, bool play)
        {
            motionKind = MotionKind.SitStart;
            if (previewActor == null) StartPreview(profile);
            if (previewActor == null) return;
            playing = false;
            frame = lastFrame ? Mathf.CeilToInt(profile.SitStart.DurationSeconds
                * profile.SitStart.BodyClip.frameRate) : 0;
            SampleCurrentFrame(profile);
            playbackTime = GetSampleTime(profile, profile.SitStart);
            playing = play;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void FitApproachToSitStart(PerformerSeatingProfile profile)
        {
            Quaternion seatRotation = YawRotation(seat.SeatAnchor.forward);
            Quaternion approachRotation = seatRotation * Quaternion.AngleAxis(
                -(profile.SitStart.RootYawAt(1f) - profile.SitStart.RootYawAt(0f)), Vector3.up);
            Vector3 seatedRoot = seat.SeatAnchor.position - seatRotation
                * Vector3.Scale(performer.transform.lossyScale, profile.SitStart.PelvisOffsetAt(1f));
            Vector3 approachPosition = seatedRoot - approachRotation
                * (profile.SitStart.RootPositionAt(1f) - profile.SitStart.RootPositionAt(0f));
            Undo.RecordObject(seat.ApproachAnchor, "Fit Approach to Sit Start");
            seat.ApproachAnchor.SetPositionAndRotation(approachPosition, approachRotation);
            EditorUtility.SetDirty(seat.ApproachAnchor);
            EditorSceneManager.MarkSceneDirty(seat.ApproachAnchor.gameObject.scene);
            sampleDirty = true;
            if (previewActor != null) SampleCurrentFrame(profile);
        }

        private void GetNaturalSitLanding(PerformerSeatingProfile profile,
            out Vector3 position, out Quaternion rotation)
        {
            Quaternion approachRotation = YawRotation(seat.ApproachAnchor.forward);
            position = seat.ApproachAnchor.position + approachRotation
                * (profile.SitStart.RootPositionAt(1f) - profile.SitStart.RootPositionAt(0f));
            rotation = approachRotation * Quaternion.AngleAxis(
                profile.SitStart.RootYawAt(1f) - profile.SitStart.RootYawAt(0f), Vector3.up);
        }

        private float GetEntryTime(PerformerSeatingProfile profile, PerformerSeatingMotion motion)
        {
            return motionKind == MotionKind.SitEnd ? profile.SitEndEntryPhase * motion.DurationSeconds : 0f;
        }

        private float GetSampleTime(PerformerSeatingProfile profile, PerformerSeatingMotion motion)
        {
            return Mathf.Clamp(playing ? playbackTime : frame / Mathf.Max(1f, motion.BodyClip.frameRate),
                GetEntryTime(profile, motion), motion.DurationSeconds);
        }

        private void StartPreview(PerformerSeatingProfile profile)
        {
            if (performer == null || seat == null || seat.SeatAnchor == null
                || seat.ApproachAnchor == null || profile == null)
                return;
            if (!profile.IsReady(out string reason))
            {
                status = reason;
                Repaint();
                return;
            }
            if (AnimationMode.InAnimationMode())
            {
                status = "Another editor animation preview is active. Stop that preview before enabling Seating Preview.";
                Repaint();
                return;
            }

            try
            {
                previewActor = UnityEngine.Object.Instantiate(performer.gameObject, (Transform)null);
                previewActor.name = performer.name + " [Seating Preview]";
                if (performer.gameObject.scene.IsValid()
                    && previewActor.scene != performer.gameObject.scene)
                    SceneManager.MoveGameObjectToScene(previewActor, performer.gameObject.scene);
                foreach (Transform item in previewActor.GetComponentsInChildren<Transform>(true))
                    item.gameObject.hideFlags = HideFlags.HideAndDontSave;
                previewActor.transform.localScale = performer.transform.lossyScale;
                previewActor.SetActive(true);
                previewTransforms = previewActor.GetComponentsInChildren<Transform>(true);
                firstPosePositions = new Vector3[previewTransforms.Length];
                firstPoseRotations = new Quaternion[previewTransforms.Length];
                firstPoseScales = new Vector3[previewTransforms.Length];
                secondPosePositions = new Vector3[previewTransforms.Length];
                secondPoseRotations = new Quaternion[previewTransforms.Length];
                secondPoseScales = new Vector3[previewTransforms.Length];
                foreach (Behaviour behaviour in previewActor.GetComponentsInChildren<Behaviour>(true))
                    behaviour.enabled = false;

                AnimationMode.StartAnimationMode();
                ownsAnimationMode = true;
                frame = 0;
                playing = false;
                status = null;
                lastUpdateTime = EditorApplication.timeSinceStartup;
                SampleCurrentFrame(profile);
            }
            catch (Exception exception)
            {
                status = "Could not start the seating preview: " + exception.Message;
                StopPreview();
            }
        }

        private void StopPreview()
        {
            playing = false;
            if (ownsAnimationMode && AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            ownsAnimationMode = false;
            if (previewActor != null)
            {
                DestroyImmediate(previewActor);
                previewActor = null;
            }
            previewTransforms = null;
            firstPosePositions = null;
            firstPoseRotations = null;
            firstPoseScales = null;
            secondPosePositions = null;
            secondPoseRotations = null;
            secondPoseScales = null;
            sampleDirty = true;
            SceneView.RepaintAll();
            Repaint();
        }

        private void SampleCurrentFrame(PerformerSeatingProfile profile)
        {
            if (previewActor == null || seat == null || seat.SeatAnchor == null
                || seat.ApproachAnchor == null || profile == null) return;
            PerformerSeatingMotion motion = GetMotion(profile, motionKind);
            if (motion == null || motion.BodyClip == null) return;

            float time = GetSampleTime(profile, motion);
            Vector3 scale = performer != null ? performer.transform.lossyScale : previewActor.transform.localScale;
            previewActor.transform.localScale = scale;
            Quaternion seatRotation = YawRotation(seat.SeatAnchor.forward);
            Vector3 seatedOffset = Vector3.Scale(scale, profile.SitStart.PelvisOffsetAt(1f));
            Vector3 rootPosition = seat.SeatAnchor.position - seatRotation * seatedOffset;
            Quaternion rootRotation = seatRotation;
            if (motionKind == MotionKind.SitStart || motionKind == MotionKind.SitEnd)
            {
                bool sittingDown = motionKind == MotionKind.SitStart;
                Vector3 origin = sittingDown ? seat.ApproachAnchor.position : rootPosition;
                Quaternion originRotation = sittingDown ? YawRotation(seat.ApproachAnchor.forward) : seatRotation;
                Vector3 target = sittingDown ? rootPosition : seat.ApproachAnchor.position;
                Quaternion targetRotation = sittingDown ? seatRotation : YawRotation(seat.ApproachAnchor.forward);
                float entryTime = GetEntryTime(profile, motion);
                if (applyFinalAnchorCorrection)
                    motion.EvaluateAnchoredRoot(time, entryTime, origin, originRotation, target, targetRotation,
                        profile.FinalBlendSeconds, profile.PlaybackSpeed, out rootPosition, out rootRotation);
                else
                {
                    float phase = time / motion.DurationSeconds;
                    float entryPhase = entryTime / motion.DurationSeconds;
                    rootPosition = origin + originRotation * (motion.RootPositionAt(phase) - motion.RootPositionAt(entryPhase));
                    rootRotation = originRotation * Quaternion.AngleAxis(
                        motion.RootYawAt(phase) - motion.RootYawAt(entryPhase), Vector3.up);
                }
            }
            previewActor.transform.SetPositionAndRotation(rootPosition, rootRotation);
            try
            {
                bool blendFromSit = motionKind == MotionKind.CrossLegsStart && previewSitToCrossBlend
                    && profile.BodyBlendSeconds > 0f
                    && time < profile.BodyBlendSeconds * profile.PlaybackSpeed;
                if (blendFromSit)
                {
                    SampleClip(profile.SitStart.BodyClip,
                        profile.SitStart.DurationSeconds);
                    CapturePreviewPose(firstPosePositions, firstPoseRotations, firstPoseScales);
                    SampleClip(motion.BodyClip, time);
                    CapturePreviewPose(secondPosePositions, secondPoseRotations, secondPoseScales);
                    float blend = Mathf.Clamp01(time / Mathf.Max(0.0001f,
                        profile.BodyBlendSeconds * profile.PlaybackSpeed));
                    ApplyPoseBlend(blend);
                }
                else
                {
                    SampleClip(motion.BodyClip, time);
                }
            }
            catch (Exception exception)
            {
                status = "Clip sampling failed: " + exception.Message;
                playing = false;
                return;
            }
            // Body sampling must not overwrite the actor trajectory evaluated above.
            previewActor.transform.SetPositionAndRotation(rootPosition, rootRotation);
            lastSeatPosition = seat.SeatAnchor.position;
            lastSeatRotation = seatRotation;
            lastApproachPosition = seat.ApproachAnchor.position;
            lastApproachRotation = YawRotation(seat.ApproachAnchor.forward);
            lastPerformerScale = scale;
            sampleDirty = false;
            SceneView.RepaintAll();
            Repaint();
        }

        private void SampleClip(AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            try
            {
                AnimationMode.SampleAnimationClip(previewActor, clip, time);
            }
            finally
            {
                AnimationMode.EndSampling();
            }
        }

        private void CapturePreviewPose(Vector3[] positions, Quaternion[] rotations, Vector3[] scales)
        {
            for (int i = 0; i < previewTransforms.Length; i++)
            {
                positions[i] = previewTransforms[i].localPosition;
                rotations[i] = previewTransforms[i].localRotation;
                scales[i] = previewTransforms[i].localScale;
            }
        }

        private void ApplyPoseBlend(float weight)
        {
            for (int i = 0; i < previewTransforms.Length; i++)
            {
                previewTransforms[i].localPosition = Vector3.Lerp(
                    firstPosePositions[i], secondPosePositions[i], weight);
                previewTransforms[i].localRotation = Quaternion.Slerp(
                    firstPoseRotations[i], secondPoseRotations[i], weight);
                previewTransforms[i].localScale = Vector3.Lerp(
                    firstPoseScales[i], secondPoseScales[i], weight);
            }
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)Math.Max(0d, now - lastUpdateTime);
            lastUpdateTime = now;
            if (previewActor == null) return;
            if (performer == null || seat == null || seat.SeatAnchor == null || seat.ApproachAnchor == null)
            {
                StopPreview();
                return;
            }

            PerformerSeatingProfile profile = seat.SeatingProfile;
            string reason = null;
            if (profile == null || !profile.IsReady(out reason))
            {
                status = reason ?? "The selected seat no longer has a ready seating profile.";
                StopPreview();
                return;
            }
            PerformerSeatingMotion motion = GetMotion(profile, motionKind);
            if (motion == null || motion.BodyClip == null)
            {
                status = "The selected motion is no longer available in the seating profile.";
                StopPreview();
                return;
            }

            if (playing)
            {
                float frameRate = Mathf.Max(1f, motion.BodyClip.frameRate);
                float time = playbackTime + deltaTime * profile.PlaybackSpeed;
                if (motion.Looping)
                    time = Mathf.Repeat(time, motion.DurationSeconds);
                else if (time >= motion.DurationSeconds)
                {
                    time = motion.DurationSeconds;
                    playing = false;
                }
                playbackTime = time;
                int nextFrame = !motion.Looping && time >= motion.DurationSeconds
                    ? Mathf.CeilToInt(motion.DurationSeconds * frameRate) : Mathf.RoundToInt(time * frameRate);
                frame = Mathf.Clamp(nextFrame, 0,
                    motion.Looping ? Mathf.CeilToInt(motion.DurationSeconds * frameRate) - 1
                        : Mathf.CeilToInt(motion.DurationSeconds * frameRate));
                sampleDirty = true;
            }

            Quaternion seatRotation = YawRotation(seat.SeatAnchor.forward);
            if (sampleDirty || (seat.SeatAnchor.position - lastSeatPosition).sqrMagnitude > 0.0000001f
                || Quaternion.Angle(seatRotation, lastSeatRotation) > 0.001f
                || (seat.ApproachAnchor.position - lastApproachPosition).sqrMagnitude > 0.0000001f
                || Quaternion.Angle(YawRotation(seat.ApproachAnchor.forward), lastApproachRotation) > 0.001f
                || (performer.transform.lossyScale - lastPerformerScale).sqrMagnitude > 0.0000001f)
                SampleCurrentFrame(profile);
            if (playing) Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (previewActor == null || !showAnchorHandles || seat == null) return;
            DrawAnchorHandle(seat.ApproachAnchor, new Color(1f, 0.68f, 0.12f, 1f), "Move Approach Anchor");
            DrawAnchorHandle(seat.SeatAnchor, new Color(0.1f, 0.8f, 1f, 1f), "Move Seat Anchor");
        }

        private void DrawAnchorHandle(Transform anchor, Color color, string undoName)
        {
            if (anchor == null) return;
            using (new Handles.DrawingScope(color))
            {
                float size = HandleUtility.GetHandleSize(anchor.position);
                Handles.SphereHandleCap(0, anchor.position, Quaternion.identity, size * 0.09f, EventType.Repaint);
                Handles.Label(anchor.position + Vector3.up * size * 0.12f, anchor.name);

                EditorGUI.BeginChangeCheck();
                Vector3 position = Handles.PositionHandle(anchor.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(anchor, undoName);
                    anchor.position = position;
                    EditorUtility.SetDirty(anchor);
                    EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                    sampleDirty = true;
                }

                EditorGUI.BeginChangeCheck();
                Quaternion rotation = Handles.RotationHandle(anchor.rotation, anchor.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(anchor, undoName);
                    anchor.rotation = rotation;
                    EditorUtility.SetDirty(anchor);
                    EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
                    sampleDirty = true;
                }
            }
        }

        private void ResolveSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;
            if (performer == null)
                performer = selected.GetComponentInParent<SuccubusPerformer>()
                    ?? selected.GetComponentInChildren<SuccubusPerformer>(true);
            if (seat == null)
                seat = selected.GetComponentInParent<PerformerSeat>()
                    ?? selected.GetComponentInChildren<PerformerSeat>(true);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                StopPreview();
        }

        private void MarkSampleDirty()
        {
            sampleDirty = true;
        }

        private static void SelectAndFrame(Transform target)
        {
            Selection.activeTransform = target;
            EditorGUIUtility.PingObject(target);
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.Frame(new Bounds(target.position, Vector3.one), false);
                view.Focus();
            }
        }

        private static string[] GetAvailableMotionLabels(PerformerSeatingProfile profile)
        {
            var labels = new List<string>();
            for (int i = 0; i < MotionKinds.Length; i++)
                if (GetMotion(profile, MotionKinds[i]) != null)
                    labels.Add(MotionLabels[i]);
            return labels.ToArray();
        }

        private static int GetAvailableMotionIndex(PerformerSeatingProfile profile, int preferredIndex)
        {
            int selected = 0;
            int visible = 0;
            for (int i = 0; i < MotionKinds.Length; i++)
            {
                if (GetMotion(profile, MotionKinds[i]) == null) continue;
                if (i == preferredIndex) selected = visible;
                visible++;
            }
            return selected;
        }

        private static MotionKind GetAvailableMotionKind(PerformerSeatingProfile profile, int visibleIndex)
        {
            int visible = 0;
            for (int i = 0; i < MotionKinds.Length; i++)
            {
                if (GetMotion(profile, MotionKinds[i]) == null) continue;
                if (visible == visibleIndex) return MotionKinds[i];
                visible++;
            }
            return MotionKind.CrossLegsStart;
        }

        private static PerformerSeatingMotion GetMotion(PerformerSeatingProfile profile, MotionKind kind)
        {
            if (profile == null) return null;
            switch (kind)
            {
                case MotionKind.SitStart: return profile.SitStart;
                case MotionKind.CrossLegsStart: return profile.CrossLegsStart;
                case MotionKind.CrossLegsLoop: return profile.CrossLegsLoop;
                case MotionKind.CrossLegsEnd: return profile.CrossLegsEnd;
                case MotionKind.SitEnd: return profile.SitEnd;
                case MotionKind.BasicIdleCandidate: return profile.BasicIdleLoopCandidate;
                default: return null;
            }
        }

        private static Quaternion YawRotation(Vector3 forward)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return forward.sqrMagnitude < 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
