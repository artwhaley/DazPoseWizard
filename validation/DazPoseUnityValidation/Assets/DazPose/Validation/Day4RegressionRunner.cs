// This historical regression runner intentionally exercises the obsolete compatibility adapter.
#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DazPose.Motion;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.UnityValidation.Partner;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DazPose.UnityValidation
{
    /// <summary>Saved-scene integration regression for the production partner contact target.</summary>
    public sealed class Day4RegressionRunner : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PartnerBody partner;
        [SerializeField] private PartnerContactTarget target;
        [SerializeField] private MotionDriver driver;
        [SerializeField] private Camera view;
        [SerializeField] private Transform overviewCameraMark;
        [SerializeField] private Transform closeupCameraMark;
        [SerializeField] private Transform support;
        [SerializeField] private bool autoRun = true;

        private const float WatchdogSeconds = 90f;
        private const float GripWaitSeconds = 8f;
        private const float ReleaseWaitSeconds = 5f;
        private const float OutOfReachWaitSeconds = 5f;
        private readonly List<Assertion> assertions = new List<Assertion>();
        private readonly List<Day4ContactSurfaceWitness.Metrics> surfaces = new List<Day4ContactSurfaceWitness.Metrics>();
        private bool running;
        private bool stopping;
        private string status = "Ready";
        private string failure;
        private double runStarted;
        private long runnerStartedDriverRevision = -1;
        private bool runnerStartedDriver;
        private PerformanceExecutionScope gripScope;
        private PerformanceExecutionScope suiteOperationScope;
        private PerformanceExecutionScope previewScope;
        private Action previewCancellation;
        private bool previewRunning;
        private bool previewStartedDriver;
        private long previewStartedDriverRevision = -1;
        private TransformSnapshot previewPartnerStart;
        private TransformSnapshot previewActorStart;
        private string previewStatus = "Not started";
        private TransformSnapshot partnerStart;
        private TransformSnapshot actorStart;
        private Vector3 partnerScaleStart;
        private float erectionStart;
        private string surfaceFailure;

        [Serializable]
        private sealed class Assertion
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private sealed class Report
        {
            public string suite = "Day4RegressionRunner";
            public string generatedUtc;
            public string result;
            public string failure;
            public string manualVisualReview = "Not recorded by automation.";
            public List<Assertion> assertions;
            public List<Day4ContactSurfaceWitness.Metrics> surfaces;
        }

        private struct TransformSnapshot
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public TransformSnapshot(Transform value)
            { position = value.position; rotation = value.rotation; scale = value.localScale; }
            public void Restore(Transform value)
            { if (value != null) { value.SetPositionAndRotation(position, rotation); value.localScale = scale; } }
        }

        private void Start()
        {
            if (autoRun) BeginRun();
        }

        public void BeginRun()
        {
            if (running) return;
            EndPreviewAndRestore("Cancelled for new suite run");
            ShowOverview();
            running = true;
            stopping = false;
            failure = null;
            surfaceFailure = null;
            assertions.Clear();
            surfaces.Clear();
            runStarted = Time.realtimeSinceStartupAsDouble;
            status = "Running Day 4 regression";
            CaptureInitialState();
            RunSuite();
        }

        public void StopRun()
        {
            if (!running) return;
            stopping = true;
            gripScope?.Cancel();
            suiteOperationScope?.Cancel();
            status = "Stopping after current frame";
        }

        public void BeginPreview()
        {
            if (running || previewRunning || actor == null || target == null || driver == null)
                return;

            previewActorStart = new TransformSnapshot(actor.transform);
            previewPartnerStart = partner != null ? new TransformSnapshot(partner.transform) : default;
            previewRunning = true;
            previewStatus = "Acquiring held contact";
            previewStartedDriver = false;
            previewStartedDriverRevision = -1;
            if (!driver.IsRunning)
            {
                driver.StartMotion();
                previewStartedDriver = driver.IsRunning;
                previewStartedDriverRevision = driver.SourceRevision;
            }
            if (!driver.IsRunning)
            {
                previewStatus = "Failed: motion driver did not start";
                FinishPreview(false, "Driver unavailable");
                return;
            }

            previewScope = new PerformanceExecutionScope("Day 4 contact preview",
                () => actor != null && actor.IsRuntimeReady && target != null);
            Awaitable<GripCompletion> acquisition = actor.GripAsync(target);
            previewCancellation = actor.CaptureCancellation(PerformanceCommand.Grip);
            var operation = previewScope.Observe(acquisition, GripCompletion.Acquired,
                "preview held contact", previewCancellation, persistent: true);
            ObservePreviewAcquisition(previewScope, operation);
        }

        public void FollowPreviewMotion()
        {
            if (!previewRunning || previewScope == null || actor == null || !actor.IsGripping)
            {
                previewStatus = "Follow unavailable: preview contact is not active";
                return;
            }
            try
            {
                actor.FollowGripMotion();
                previewStatus = "Following shared motion";
            }
            catch (Exception ex) { previewStatus = "Follow failed: " + ex.Message; }
        }

        public void ReleasePreview()
        {
            if (!previewRunning) return;
            FinishPreview(true, "Released by user");
        }

        private async void ObservePreviewAcquisition(PerformanceExecutionScope scope,
            PerformanceExecutionScope.Operation operation)
        {
            try
            {
                await scope.Wait(operation);
                if (scope != previewScope || !previewRunning) return;
                actor.HoldGrip(.5f);
                previewStatus = "Holding contact at 0.5";
            }
            catch (Exception ex)
            {
                if (scope != previewScope) return;
                previewStatus = "Acquisition failed: " + ex.Message;
                FinishPreview(true, "Acquisition failed");
            }
        }

        private void EndPreviewAndRestore(string reason)
        {
            if (!previewRunning && previewScope == null) return;
            FinishPreview(true, reason);
        }

        private void FinishPreview(bool restoreTransforms, string reason)
        {
            if (previewCancellation != null)
            {
                try { previewCancellation(); }
                catch (Exception ex) { Debug.LogWarning("Preview release: " + ex.Message, this); }
            }
            previewScope?.Dispose();
            previewScope = null;
            previewCancellation = null;
            if (previewStartedDriver && driver != null && driver.IsRunning
                && driver.SourceRevision == previewStartedDriverRevision)
                driver.StopMotion();
            previewStartedDriver = false;
            previewStartedDriverRevision = -1;
            if (restoreTransforms)
            {
                previewPartnerStart.Restore(partner != null ? partner.transform : null);
                previewActorStart.Restore(actor != null ? actor.transform : null);
            }
            previewRunning = false;
            previewStatus = reason;
        }

        public void ShowOverview()
        {
            if (view != null && overviewCameraMark != null)
                view.transform.SetPositionAndRotation(overviewCameraMark.position, overviewCameraMark.rotation);
        }

        public void ShowCloseup()
        {
            if (view != null && closeupCameraMark != null)
                view.transform.SetPositionAndRotation(closeupCameraMark.position, closeupCameraMark.rotation);
        }

        private void CaptureInitialState()
        {
            partnerStart = partner != null ? new TransformSnapshot(partner.transform) : default;
            actorStart = actor != null ? new TransformSnapshot(actor.transform) : default;
            partnerScaleStart = partner != null ? partner.transform.localScale : Vector3.one;
            erectionStart = partner != null ? partner.Erection01 : 1f;
            runnerStartedDriver = false;
            runnerStartedDriverRevision = -1;
        }

        private async void RunSuite()
        {
            try
            {
                Require(actor != null && partner != null && target != null && driver != null && view != null
                    && overviewCameraMark != null && closeupCameraMark != null && support != null,
                    "saved-scene-bindings", "Actor, PartnerBody, PartnerContactTarget, MotionDriver, view, overview/close-up marks and support transform are required.");
                Require(target.Body == partner && partner.IsReady && partner.IsContactSupported,
                    "partner-runtime-ready", "Target must bind the ready partner at its supported rigid endpoint: " + partner.ValidationError);
                string profileReason = null;
                bool profileReady = target.Profile != null && target.Profile.IsReady(out profileReason);
                Require(profileReady, "contact-profile-ready", profileReason ?? "Profile accepted.");

                CheckProjectedActor(actor.transform, "actor-visible-in-overview");
                CheckProjectedPartner();
                Require(Vector3.Dot(partner.transform.forward, Vector3.up) > .95f,
                    "partner-lying-back", "Partner root.forward must point upward for the authored supine setup.");
                const float authoredFloorY = 0f;
                Require(TryGetEvaluatedStructuralMinimumWorldY(partner.StructuralBody, out float backY, out string bakeReason),
                    "partner-evaluated-mesh-available", bakeReason ?? "Evaluated structural mesh baked.");
                Require(Mathf.Abs(backY - authoredFloorY - 1f) <= .10f,
                    "partner-back-height", $"Evaluated structural-mesh vertex minimum Y is {backY - authoredFloorY:0.000} m above authored floor Y=0; expected 1.0 m within 0.1 m.");

                Vector2 span = target.Profile.SupportedArcSpan;
                for (int i = 0; i < 17; i++)
                {
                    // The adapter maps normalized author positions across its measured interior span.
                    float position = i / 16f;
                    Require(target.TryEvaluate(position, out GripFrame frame, out string reason) && frame.IsValid,
                        "interior-contact-frame-" + i, reason ?? $"Valid frame at {position:0.000}.");
                }

                EnsureDriverRunning();
                await Acquire(target, "initial-contact");
                var grip = actor.GetComponent<PerformerHandGripController>();
                Require(grip != null && actor.IsGripping && grip.State == GripInteractionState.Active,
                    "initial-contact-active", "Actor should own the arm in Active after acquisition.");

                actor.HoldGrip(.5f);
                Require(!actor.GripFollowsMotion && Mathf.Abs(actor.GripPosition01 - .5f) < .001f,
                    "hold-at-half", "HoldGrip(0.5) should pin contact position without stopping playback.");
                long heldRevision = driver.SourceRevision;
                double heldTime = driver.CurrentSample.TimeSeconds;
                await DelaySeconds(.35f);
                CheckSurface(grip, "held-contact-surface");
                Require(driver.SourceRevision == heldRevision && driver.CurrentSample.TimeSeconds > heldTime,
                    "hold-preserves-running-clock", "Holding contact must leave the same motion source advancing.");

                double beforeFollowTime = driver.CurrentSample.TimeSeconds;
                long beforeFollowRevision = driver.SourceRevision;
                for (int station = 0; station < 17; station++)
                {
                    actor.HoldGrip(station / 16f);
                    await DelaySeconds(.25f);
                    CheckSurface(grip, "held-station-" + station);
                }
                actor.FollowGripMotion();
                Require(actor.GripFollowsMotion, "follow-command-enabled", "FollowGripMotion should enable driver following.");
                bool sawFollowSample = false;
                double followDeadline = Time.realtimeSinceStartupAsDouble + .8d;
                while (Time.realtimeSinceStartupAsDouble < followDeadline)
                {
                    CheckRunState();
                    await Awaitable.NextFrameAsync();
                    CheckRunState();
                    if (Mathf.Abs(actor.GripPosition01 - driver.CurrentSample.Position01) <= .15f)
                        sawFollowSample = true;
                }
                Require(driver.SourceRevision == beforeFollowRevision && driver.CurrentSample.TimeSeconds > beforeFollowTime && sawFollowSample,
                    "follow-uses-live-clock", "Follow must consume current samples without resetting source or time.");
                CheckSurface(grip, "following-contact-surface");

                Require(target.TryEvaluate(.5f, out GripFrame beforeMove, out string beforeReason),
                    "frame-before-root-motion", beforeReason ?? "Initial frame valid.");
                Vector3 basePosition = partner.transform.position;
                Quaternion baseRotation = partner.transform.rotation;
                partner.transform.SetPositionAndRotation(basePosition + Vector3.right * .14f,
                    baseRotation * Quaternion.Euler(0f, 12f, 0f));
                await Awaitable.NextFrameAsync();
                CheckRunState();
                Require(target.TryEvaluate(.5f, out GripFrame movedFrame, out string movedReason)
                    && movedFrame.IsValid && Vector3.Distance(beforeMove.Center, movedFrame.Center) > .04f,
                    "frame-follows-root-motion", movedReason ?? "Live target frame follows root translation/yaw.");
                Require(actor.IsGripping && actor.GripState == GripInteractionState.Active
                    && driver.SourceRevision == beforeFollowRevision,
                    "grip-survives-small-root-motion", "Small root motion should preserve active grip and playback ownership.");
                await DelaySeconds(.2f);
                Require(grip.AlignmentDiagnostics.PositionErrorMeters <= .03f,
                    "grip-tracks-transformed-target", $"Palm remains within 30 mm after partner motion; observed {grip.AlignmentDiagnostics.PositionErrorMeters:0.000} m.");
                CheckSurface(grip, "transformed-contact-surface");

                double beforeReleaseTime = driver.CurrentSample.TimeSeconds;
                ReleaseSuiteGrip();
                await WaitForState(() => actor.GripState == GripInteractionState.Idle, ReleaseWaitSeconds, "release-to-idle");
                Require(grip.State == GripInteractionState.Idle && grip.GripWeight <= .001f && grip.MotionSuppression <= .001f,
                    "release-clears-ownership", "Release should clear grip weight and motion suppression.");
                await DelaySeconds(.2f);
                Require(driver.IsRunning && driver.CurrentSample.TimeSeconds > beforeReleaseTime,
                    "release-preserves-clock", "Releasing contact must not stop the shared clock.");
                DisposeGripScope();

                partner.Erection01 = .5f;
                Require(!target.TryEvaluate(.5f, out _, out string erectionReason) && !string.IsNullOrEmpty(erectionReason),
                    "reject-unsupported-erection", erectionReason ?? "Unsupported endpoint rejected.");
                partner.Erection01 = erectionStart;
                partner.transform.localScale = new Vector3(partnerScaleStart.x * 1.1f, partnerScaleStart.y, partnerScaleStart.z);
                Require(!target.TryEvaluate(.5f, out _, out string scaleReason) && !string.IsNullOrEmpty(scaleReason),
                    "reject-nonuniform-scale", scaleReason ?? "Nonuniform actor scale rejected.");
                partner.transform.localScale = new Vector3(-Mathf.Abs(partnerScaleStart.x), partnerScaleStart.y, partnerScaleStart.z);
                Require(!target.TryEvaluate(.5f, out _, out string negativeReason) && !string.IsNullOrEmpty(negativeReason),
                    "reject-negative-scale", negativeReason ?? "Negative actor scale rejected.");
                partnerStart.Restore(partner.transform);
                partner.Erection01 = erectionStart;
                partner.SetCompliance(2f, 0f, 0f);
                Require(!target.TryEvaluate(.5f, out _, out _), "reject-unproven-compliance", "Prepared surface supports only zero compliance.");
                partner.SetCompliance(0f, 0f, 0f);
                Vector3 donorScale = partner.DonorPelvis.localScale;
                partner.DonorPelvis.localScale = donorScale * 1.1f;
                bool rejectedDonorScale = !target.TryEvaluate(.5f, out _, out _);
                partner.DonorPelvis.localScale = donorScale;
                Require(rejectedDonorScale, "reject-donor-scale-change", "Prepared contact rejects independently scaled donor geometry.");
                var renderer = partner.StructuralBody;
                Vector3 rendererScale = renderer.transform.localScale;
                renderer.transform.localScale = rendererScale * 1.1f;
                Require(!target.TryEvaluate(.5f, out _, out _), "reject-renderer-transform-change", "Renderer-local scale changes invalidate prepared geometry.");
                renderer.transform.localScale = rendererScale;
                Transform[] boundBones = renderer.bones;
                var invalidBones = (Transform[])boundBones.Clone();
                invalidBones[0] = null;
                renderer.bones = invalidBones;
                bool rejectedBones = !target.TryEvaluate(.5f, out _, out _);
                renderer.bones = boundBones;
                Require(rejectedBones, "reject-lost-skinning-binding", "A missing bound skinning bone cannot retain successful contact eligibility.");
                Require(target.TryEvaluate(.5f, out _, out string restoredReason),
                    "target-restored-after-invalid-state", restoredReason ?? "Target valid after state restore.");

                await Acquire(target, "active-target-loss");
                partner.transform.position += Vector3.up * 3f;
                await WaitForState(() => actor.GripState == GripInteractionState.Failed
                    || actor.GripState == GripInteractionState.Idle && !actor.IsGripping,
                    OutOfReachWaitSeconds, "active-target-loss-bounded-terminal");
                var activeGrip = actor.GetComponent<PerformerHandGripController>();
                Require((actor.GripState == GripInteractionState.Failed || actor.GripState == GripInteractionState.Idle)
                    && !actor.IsGripping && activeGrip != null && activeGrip.GripWeight <= .001f
                    && activeGrip.MotionSuppression <= .001f,
                    "active-target-loss-releases-safely", "An active grip moved 3 m out of reach must reach Failed or Idle with arm weight and suppression cleared.");
                partnerStart.Restore(partner.transform);
                DisposeGripScope();
                await Awaitable.NextFrameAsync();
                CheckRunState();

                var outOfReachScope = new PerformanceExecutionScope("Day 4 out-of-reach grip", () => actor != null && actor.IsRuntimeReady);
                suiteOperationScope = outOfReachScope;
                try
                {
                    var outOfReachOperation = outOfReachScope.Observe(actor, PerformanceCommand.Grip,
                        () => actor.GripAsync(target), GripCompletion.Failed, "out-of-reach contact", persistent: true);
                    partner.transform.position += Vector3.up * 3f;
                    await WaitForState(() => actor.GripState == GripInteractionState.Failed,
                        OutOfReachWaitSeconds, "pending-target-loss-bounded-failure");
                    await outOfReachScope.Wait(outOfReachOperation);
                    Require(actor.GripState == GripInteractionState.Failed,
                        "pending-target-loss-failed", "A pending acquisition whose target moves 3 m out of reach must settle Failed.");
                }
                finally
                {
                    outOfReachScope.Dispose();
                    if (suiteOperationScope == outOfReachScope) suiteOperationScope = null;
                    partnerStart.Restore(partner != null ? partner.transform : null);
                }
                await Awaitable.NextFrameAsync();
                CheckRunState();

                GameObject cloneObject = Instantiate(partner.gameObject, partner.transform.position + Vector3.right * .03f,
                    partner.transform.rotation);
                cloneObject.name = partner.name + " Day4 pending clone";
                var cloneScope = new PerformanceExecutionScope("Day 4 destroyed-target grip", () => actor != null && actor.IsRuntimeReady);
                suiteOperationScope = cloneScope;
                try
                {
                    var cloneTarget = cloneObject.GetComponent<PartnerContactTarget>();
                    Require(cloneTarget != null && cloneTarget.Body == cloneObject.GetComponent<PartnerBody>(),
                        "clone-target-bindings", "Cloned target must bind its cloned PartnerBody.");
                    var cloneOperation = cloneScope.Observe(actor, PerformanceCommand.Grip,
                        () => actor.GripAsync(cloneTarget), GripCompletion.Failed, "destroy pending clone target", persistent: true);
                    Require(actor.GripState != GripInteractionState.Idle && actor.GripState != GripInteractionState.Failed
                        && !actor.IsGripping, "clone-acquisition-pending", "Cloned-target acquisition must still be pending before destruction.");
                    Destroy(cloneObject);
                    await WaitForState(() => actor.GripState == GripInteractionState.Failed,
                        GripWaitSeconds, "destroyed-target-failure");
                    await cloneScope.Wait(cloneOperation);
                    Require(cloneOperation.Result == GripCompletion.Failed.ToString() && actor.GripState == GripInteractionState.Failed,
                        "destroyed-target-settles-failed", "Destroying a pending clone should fail only that request.");
                }
                finally
                {
                    cloneScope.Dispose();
                    if (suiteOperationScope == cloneScope) suiteOperationScope = null;
                    if (cloneObject != null) Destroy(cloneObject);
                }
                string originalReason = null;
                bool originalReady = partner != null && partner.IsReady && target.TryEvaluate(.5f, out _, out originalReason);
                Require(originalReady, "original-target-survives-clone", originalReason ?? "Original remains ready after clone destruction.");
                await Awaitable.NextFrameAsync();
                CheckRunState();

                await Acquire(target, "final-reacquire");
                Require(actor.IsGripping && actor.GripState == GripInteractionState.Active,
                    "target-reusable-after-failures", "Original target can be acquired after invalid-state and destruction failures.");
                ReleaseSuiteGrip();
                await WaitForState(() => actor.GripState == GripInteractionState.Idle, ReleaseWaitSeconds, "final-release");
                DisposeGripScope();

                Require(surfaceFailure == null, "evaluated-surface-contact-gate", surfaceFailure ?? "All discrete surface witnesses passed.");
                status = "Passed";
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
                status = stopping ? "Stopped" : "Failed";
                if (gripScope != null) { gripScope.Cancel(); gripScope.Dispose(); gripScope = null; }
                suiteOperationScope?.Cancel();
                partnerStart.Restore(partner != null ? partner.transform : null);
                actorStart.Restore(actor != null ? actor.transform : null);
                if (partner != null) { partner.transform.localScale = partnerScaleStart; partner.Erection01 = erectionStart; }
            }
            finally
            {
                DisposeGripScope();
                suiteOperationScope?.Dispose();
                suiteOperationScope = null;
                StopOnlyRunnerOwnedDriver();
                partnerStart.Restore(partner != null ? partner.transform : null);
                actorStart.Restore(actor != null ? actor.transform : null);
                if (partner != null) partner.Erection01 = erectionStart;
                running = false;
                WriteReport();
                if (Application.isBatchMode)
                {
#if UNITY_EDITOR
                    EditorApplication.isPlaying = false;
#else
                    Application.Quit(string.Equals(status, "Passed", StringComparison.Ordinal) ? 0 : 1);
#endif
                }
            }
        }

        private async Awaitable Acquire(PartnerContactTarget requestedTarget, string name)
        {
            CheckRunState();
            gripScope = new PerformanceExecutionScope("Day 4 " + name, () => actor != null && actor.IsRuntimeReady);
            var operation = gripScope.Observe(actor, PerformanceCommand.Grip,
                () => actor.GripAsync(requestedTarget), GripCompletion.Acquired, name, persistent: true);
            double deadline = Time.realtimeSinceStartupAsDouble + GripWaitSeconds;
            while (!operation.Done)
            {
                CheckRunState();
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException(name + " timed out after " + GripWaitSeconds + " realtime seconds; state=" + actor.GripState);
                await Awaitable.NextFrameAsync();
                CheckRunState();
            }
            await gripScope.Wait(operation);
            Require(operation.Result == GripCompletion.Acquired.ToString() && actor.GripState == GripInteractionState.Active,
                name + "-acquired", "Completion=" + operation.Result + "; state=" + actor.GripState);
        }

        private void DisposeGripScope()
        {
            if (gripScope == null) return;
            gripScope.Dispose();
            gripScope = null;
        }

        private void ReleaseSuiteGrip()
        {
            actor.ReleaseGrip();
            if (gripScope != null)
                gripScope.Own("suite grip release", actor.CaptureCancellation(PerformanceCommand.Grip));
        }

        private async Awaitable WaitForState(Func<bool> done, float seconds, string name)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!done())
            {
                CheckRunState();
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException(name + " timed out after " + seconds.ToString("0.0") + " realtime seconds; actor state=" + actor.GripState);
                await Awaitable.NextFrameAsync();
                CheckRunState();
            }
            CheckRunState();
        }

        private async Awaitable DelaySeconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                CheckRunState();
                await Awaitable.NextFrameAsync();
                CheckRunState();
            }
        }

        private void EnsureDriverRunning()
        {
            if (driver.IsRunning) return;
            driver.StartMotion();
            runnerStartedDriver = driver.IsRunning;
            runnerStartedDriverRevision = driver.SourceRevision;
            Require(runnerStartedDriver, "runner-started-driver", "Scene MotionDriver must be running for shared playback tests.");
        }

        private void StopOnlyRunnerOwnedDriver()
        {
            if (runnerStartedDriver && driver != null && driver.IsRunning
                && driver.SourceRevision == runnerStartedDriverRevision)
                driver.StopMotion();
        }

        private void CheckProjectedActor(Transform root, string assertionName)
        {
            Require(IsBoundsInView(root.GetComponentsInChildren<Renderer>(), view), assertionName,
                "The actor renderer bounds should project fully inside the saved overview camera.");
        }

        private void CheckProjectedPartner()
        {
            var renderers = new List<Renderer>();
            if (partner.StructuralBody != null) renderers.Add(partner.StructuralBody);
            if (partner.ShellRenderer != null) renderers.Add(partner.ShellRenderer);
            Require(IsBoundsInView(renderers.ToArray(), view), "partner-visible-in-overview",
                "The prepared body and shell bounds should project fully inside the saved overview camera.");
        }

        private static bool IsBoundsInView(Renderer[] renderers, Camera camera)
        {
            if (renderers == null || renderers.Length == 0 || camera == null) return false;
            Bounds bounds = default;
            bool any = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds evaluated = renderer.bounds;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var mesh = new Mesh();
                    try
                    {
                        skinned.BakeMesh(mesh);
                        var vertices = mesh.vertices;
                        if (vertices.Length == 0) return false;
                        evaluated = new Bounds(skinned.transform.TransformPoint(vertices[0]), Vector3.zero);
                        foreach (var vertex in vertices) evaluated.Encapsulate(skinned.transform.TransformPoint(vertex));
                    }
                    finally { Destroy(mesh); }
                }
                if (!any) { bounds = evaluated; any = true; } else bounds.Encapsulate(evaluated);
            }
            if (!any) return false;
            Vector3 min = bounds.min, max = bounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 point = camera.WorldToViewportPoint(corner);
                if (point.z <= 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f) return false;
            }
            return true;
        }

        private static bool TryGetEvaluatedStructuralMinimumWorldY(SkinnedMeshRenderer renderer,
            out float minimumWorldY, out string reason)
        {
            minimumWorldY = float.PositiveInfinity;
            reason = null;
            if (renderer == null || renderer.sharedMesh == null)
            {
                reason = "Structural skinned renderer and source mesh are required for BakeMesh.";
                return false;
            }

            Mesh baked = new Mesh { name = "Day4 structural height sample", hideFlags = HideFlags.DontSave };
            try
            {
                renderer.BakeMesh(baked);
                Vector3[] vertices = baked.vertices;
                if (vertices == null || vertices.Length == 0)
                {
                    reason = "BakeMesh produced no evaluated structural vertices.";
                    return false;
                }

                for (int i = 0; i < vertices.Length; i++)
                {
                    float worldY = renderer.transform.TransformPoint(vertices[i]).y;
                    if (float.IsNaN(worldY) || float.IsInfinity(worldY))
                    {
                        reason = "An evaluated structural vertex produced a non-finite world Y.";
                        return false;
                    }
                    if (worldY < minimumWorldY) minimumWorldY = worldY;
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = "BakeMesh failed: " + ex.Message;
                return false;
            }
            finally
            {
                if (Application.isPlaying) Destroy(baked);
                else DestroyImmediate(baked);
            }
        }

        private void Require(bool passed, string name, string detail)
        {
            assertions.Add(new Assertion { name = name, passed = passed, detail = detail ?? string.Empty });
            if (!passed) throw new InvalidOperationException(name + " failed: " + detail);
        }

        private void CheckSurface(PerformerHandGripController grip, string name)
        {
            var sample = Day4ContactSurfaceWitness.Sample(partner, grip);
            surfaces.Add(sample);
            Require(sample.valid, name + "-measured", sample.error ?? "Every configured probe measured against evaluated body and shell triangles.");
            bool passed = sample.surfacePenetratingCount == 0 && sample.surfaceIntersectionCount == 0 && sample.surfaceNearCount >= 3;
            string detail = $"Measured {sample.surfaceNearCount} near probes, {sample.surfacePenetratingCount} penetrating; minimum clearance {sample.minimumClearanceMeters:F6} m. Numerical sign tolerance 0.1 mm; near threshold from unchanged hand profile.";
            assertions.Add(new Assertion { name = name + "-contact", passed = passed, detail = detail });
            if (!passed) surfaceFailure = name + ": " + detail;
        }

        private void CheckRunState()
        {
            if (stopping) throw new OperationCanceledException("Suite stopped from runner controls.");
            if (Time.realtimeSinceStartupAsDouble - runStarted > WatchdogSeconds)
                throw new TimeoutException("90-second realtime watchdog expired.");
        }

        private void WriteReport()
        {
            try
            {
                string outputDirectory;
#if UNITY_EDITOR
                string projectDirectory = Directory.GetParent(Application.dataPath).FullName;
                outputDirectory = Path.Combine(projectDirectory, "TestOutput", "day4");
                string reportName = "runtime.json";
#else
                outputDirectory = Path.GetDirectoryName(Application.dataPath);
                string reportName = "day4-runtime.json";
#endif
                Directory.CreateDirectory(outputDirectory);
                var report = new Report
                {
                    generatedUtc = DateTime.UtcNow.ToString("o"),
                    result = status.ToLowerInvariant(),
                    failure = failure,
                    assertions = assertions,
                    surfaces = surfaces
                };
                File.WriteAllText(Path.Combine(outputDirectory, reportName),
                    JsonUtility.ToJson(report, true), Encoding.UTF8);
            }
            catch (Exception ex) { Debug.LogError("Could not write Day 4 regression report: " + ex); }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 350, 230), GUI.skin.box);
            GUILayout.Label("Day 4 Contact Regression: " + status);
            if (!running && GUILayout.Button("Run suite")) BeginRun();
            if (running && GUILayout.Button("Stop suite")) StopRun();
            if (!running && status != "Ready")
            {
                GUILayout.BeginHorizontal();
                if (!previewRunning && GUILayout.Button("Preview held contact")) BeginPreview();
                if (previewRunning && GUILayout.Button("Follow motion")) FollowPreviewMotion();
                if (previewRunning && GUILayout.Button("Release preview")) ReleasePreview();
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Overview")) ShowOverview();
            if (GUILayout.Button("Close-up")) ShowCloseup();
            GUILayout.EndHorizontal();
            GUILayout.Label("Preview: " + previewStatus);
            GUILayout.Label(assertions.Count + " checks recorded" + (string.IsNullOrEmpty(failure) ? string.Empty : " — " + failure));
            GUILayout.EndArea();
        }
    }
}
#pragma warning restore CS0618
