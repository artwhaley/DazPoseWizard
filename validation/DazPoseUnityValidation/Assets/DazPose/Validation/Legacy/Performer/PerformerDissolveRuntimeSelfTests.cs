using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Deterministic sequencing checks for hidden relocation and serial dissolve requests.</summary>
    internal static class PerformerDissolveRuntimeSelfTests
    {
        public static string[] Run()
        {
            var failures = new List<string>();
            CheckNewPerformerDefaultsHidden(failures);
            var host = new GameObject("P0G3_DissolveRuntimeSelfTest");
            var target = new GameObject("P0G3_DissolveTargetSnapshot");
            var profile = ScriptableObject.CreateInstance<PerformerDissolveProfile>();
            var pose = ScriptableObject.CreateInstance<PerformerPose>();
            var clip = new AnimationClip { name = "P0G3_DissolveArrivalPose" };
            PerformerDissolve dissolve = null;

            try
            {
                FieldInfo clipField = typeof(PerformerPose).GetField("clip", BindingFlags.Instance | BindingFlags.NonPublic);
                if (clipField == null) throw new MissingFieldException(typeof(PerformerPose).FullName, "clip");
                clipField.SetValue(pose, clip);

                int frame = 10;
                var presentation = new FakePresentation { BodyCenter = new Vector3(0f, 1.2f, 0f) };
                dissolve = new PerformerDissolve(host.transform, profile, presentation,
                    arrivalPose =>
                    {
                        presentation.Events.Add("arrival-pose");
                        presentation.BodyCenter = host.transform.position + new Vector3(0f, 1.55f, 0f);
                    },
                    () => frame);

                Vector3 firstDestination = new Vector3(3f, 0f, -2f);
                dissolve.DissolveTo(firstDestination, pose, null);
                Check(presentation.DissolveEnabled && presentation.ActivePopulationCount == 1,
                    "one live particle body is active when dissolve begins", failures);
                dissolve.Advance(profile.DissolveOutDuration + 0.01f);
                Check(Vector3.Distance(host.transform.position, firstDestination) < 0.0001f
                    && presentation.DissolveProgress >= 0.999f,
                    "exact root relocation follows fully dissolved shader progress", failures);
                Check(presentation.Events.IndexOf("departure-complete") < presentation.Events.IndexOf("arrival-pose")
                    && presentation.Events.IndexOf("arrival-pose") >= 0,
                    "arrival Pose is asserted after complete departure", failures);

                dissolve.Advance(profile.TransitDuration + 0.1f);
                Check(presentation.TransitCount == 1 && presentation.MaterializeCount == 0,
                    "embers already travel while destination evaluation waits for the next frame", failures);
                frame++;
                Vector3 evaluatedDestinationCenter = presentation.BodyCenter;
                dissolve.Advance(0f);
                Check(presentation.TransitCount == 1
                    && Vector3.Distance(presentation.TransitDestination, evaluatedDestinationCenter) < 0.0001f,
                    "transit samples destination bounds only after a later evaluation frame", failures);
                int populationDuringTransit = presentation.CurrentPopulationId;
                dissolve.Advance(profile.TransitDuration + 0.01f);
                Check(presentation.MaterializeCount == 1
                    && presentation.CurrentPopulationId == populationDuringTransit
                    && presentation.ActivePopulationCount == 1,
                    "the same single particle population proceeds from transit into materialization", failures);
                dissolve.Advance(profile.DissolveOutDuration + profile.MaterializeDuration + 0.01f);
                Check(!dissolve.IsDissolving && presentation.FinishCount == 1
                    && !presentation.DissolveEnabled && presentation.ActivePopulationCount == 0,
                    "successful materialization restores shader state and disposes the particle body", failures);

                Vector3 secondDestination = new Vector3(-1f, 0f, 4f);
                Quaternion secondRotation = Quaternion.Euler(0f, 90f, 0f);
                target.transform.SetPositionAndRotation(secondDestination, secondRotation);
                dissolve.DissolveTo(target.transform, null, null);
                target.transform.SetPositionAndRotation(secondDestination + Vector3.right * 2f,
                    Quaternion.Euler(0f, 180f, 0f));
                dissolve.Advance(profile.DissolveOutDuration + 0.01f);
                Check(Vector3.Distance(host.transform.position, secondDestination) < 0.0001f
                    && Quaternion.Angle(host.transform.rotation, secondRotation) < 0.01f,
                    "Transform destination position and planar facing are snapshotted at request time", failures);
                frame++;
                dissolve.Advance(0f);
                int secondPopulation = presentation.CurrentPopulationId;
                dissolve.Advance(profile.TransitDuration + 0.01f);
                dissolve.Advance(profile.DissolveOutDuration + profile.MaterializeDuration + 0.01f);
                Check(presentation.FinishCount == 2 && !dissolve.IsDissolving
                    && presentation.LastFinishedPopulationId == secondPopulation
                    && presentation.ActivePopulationCount == 0,
                    "a second request completes serially using one freshly disposed population", failures);

                presentation.ThrowOnBegin = true;
                bool beginFailed = false;
                try { dissolve.DissolveTo(new Vector3(5f, 0f, 0f), null, null); }
                catch (InvalidOperationException) { beginFailed = true; }
                Check(beginFailed && !dissolve.IsDissolving && presentation.RestoreCount == 1
                    && !presentation.DissolveEnabled && presentation.ActivePopulationCount == 0,
                    "a failed particle setup restores shader state and releases any partially created body", failures);
                presentation.ThrowOnBegin = false;

                Vector3 thirdDestination = new Vector3(6f, 0f, 1f);
                dissolve.DissolveTo(thirdDestination, null, null);
                Check(dissolve.IsDissolving, "a new request can start after earlier requests finish", failures);
                dissolve.Dispose();
                Check(!dissolve.IsDissolving && presentation.RestoreCount == 2
                    && !presentation.DissolveEnabled && presentation.ActivePopulationCount == 0,
                    "disable/dispose restores shader state and releases an in-flight particle body", failures);

                RunPersistentVisibilityChecks(profile, failures);
            }
            catch (Exception exception)
            {
                failures.Add("Dissolve runtime self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                dissolve?.Dispose();
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(profile);
                UnityEngine.Object.Destroy(pose);
                UnityEngine.Object.Destroy(clip);
            }

            return failures.ToArray();
        }

        private static void Check(bool condition, string description, List<string> failures)
        {
            if (!condition) failures.Add("P0.G3: " + description + ".");
        }

        private static void RunPersistentVisibilityChecks(PerformerDissolveProfile profile, List<string> failures)
        {
            FieldInfo releaseField = typeof(PerformerDissolveProfile).GetField("dissolveOutDuration",
                BindingFlags.Instance | BindingFlags.NonPublic);
            float originalRelease = releaseField != null ? (float)releaseField.GetValue(profile) : 1f;
            var host = new GameObject("P0H_VisibilityRuntimeSelfTest");
            var presentation = new FakePresentation { BodyCenter = new Vector3(0f, 1f, 0f) };
            bool stableHidden = false;
            PerformerDissolve dissolve = null;
            try
            {
                dissolve = new PerformerDissolve(host.transform, profile, presentation, _ => { }, null,
                    initiallyHidden: false, stableVisibilityChanged: hidden => stableHidden = hidden);

                bool inWhileVisibleRejected = false;
                try { dissolve.DissolveIn(1f, null); }
                catch (InvalidOperationException) { inWhileVisibleRejected = true; }
                Check(inWhileVisibleRejected && dissolve.VisibilityState == PerformerVisibilityState.Visible,
                    "IN while Visible is rejected without changing state", failures, "P0.H");

                float[] durations = { 1f, 3f, 5f };
                foreach (float duration in durations)
                {
                    var outCompletion = new AwaitableCompletionSource<VisibilityCompletion>();
                    dissolve.DissolveOut(duration, outCompletion);
                    Check(dissolve.VisibilityState == PerformerVisibilityState.DissolvingOut,
                        duration + " second OUT enters DissolvingOut", failures, "P0.H");
                    bool secondOutRejected = false;
                    try { dissolve.DissolveOut(duration, null); }
                    catch (InvalidOperationException) { secondOutRejected = true; }
                    bool inDuringOutRejected = false;
                    try { dissolve.DissolveIn(duration, null); }
                    catch (InvalidOperationException) { inDuringOutRejected = true; }
                    Check(secondOutRejected && inDuringOutRejected
                        && dissolve.VisibilityState == PerformerVisibilityState.DissolvingOut,
                        "repeated or opposite visibility command during OUT is rejected", failures, "P0.H");
                    if (releaseField != null) releaseField.SetValue(profile, originalRelease * 4f);
                    dissolve.Advance(duration * 0.5f);
                    Check(Mathf.Abs(presentation.VisibilityClock - 0.5f) < 0.001f
                        && dissolve.VisibilityState == PerformerVisibilityState.DissolvingOut,
                        duration + " second OUT keeps one captured normalized clock after profile edits", failures, "P0.H");
                    dissolve.Advance(duration * 0.5f);
                    Check(dissolve.VisibilityState == PerformerVisibilityState.Hidden && stableHidden
                        && presentation.ForceRenderingOff && !presentation.DissolveEnabled
                        && presentation.DissolveProgress == 0f && presentation.ActivePopulationCount == 0,
                        duration + " second OUT ends in resource-free stable Hidden", failures, "P0.H");
                    Check(outCompletion.Awaitable.GetAwaiter().GetResult() == VisibilityCompletion.Hidden,
                        duration + " second OUT waiter resolves Hidden", failures, "P0.H");

                    bool repeatedOutRejected = false;
                    try { dissolve.DissolveOut(duration, null); }
                    catch (InvalidOperationException) { repeatedOutRejected = true; }
                    Check(repeatedOutRejected && dissolve.VisibilityState == PerformerVisibilityState.Hidden,
                        "OUT while Hidden is rejected without changing state", failures, "P0.H");

                    bool hiddenRelocationRejected = false;
                    try { dissolve.DissolveTo(host.transform.position, duration, null, null); }
                    catch (InvalidOperationException) { hiddenRelocationRejected = true; }
                    Check(hiddenRelocationRejected && dissolve.VisibilityState == PerformerVisibilityState.Hidden,
                        "DissolveTo while Hidden is rejected without changing state", failures, "P0.H");

                    var inCompletion = new AwaitableCompletionSource<VisibilityCompletion>();
                    dissolve.DissolveIn(duration, inCompletion);
                    Check(dissolve.VisibilityState == PerformerVisibilityState.DissolvingIn
                        && presentation.ForceRenderingOff,
                        duration + " second IN begins hidden and enters DissolvingIn", failures, "P0.H");
                    bool repeatedInRejected = false;
                    try { dissolve.DissolveIn(duration, null); }
                    catch (InvalidOperationException) { repeatedInRejected = true; }
                    bool outDuringInRejected = false;
                    try { dissolve.DissolveOut(duration, null); }
                    catch (InvalidOperationException) { outDuringInRejected = true; }
                    Check(repeatedInRejected && outDuringInRejected
                        && dissolve.VisibilityState == PerformerVisibilityState.DissolvingIn,
                        "repeated or opposite visibility command during IN is rejected", failures, "P0.H");
                    dissolve.Advance(duration * 0.5f);
                    Check(Mathf.Abs(presentation.VisibilityClock - 0.5f) < 0.001f,
                        duration + " second IN uses its own normalized clock", failures, "P0.H");
                    dissolve.Advance(duration * 0.5f);
                    Check(dissolve.VisibilityState == PerformerVisibilityState.Visible && !stableHidden
                        && !presentation.ForceRenderingOff && !presentation.DissolveEnabled
                        && presentation.DissolveProgress == 0f && presentation.ActivePopulationCount == 0,
                        duration + " second IN ends in resource-free stable Visible", failures, "P0.H");
                    Check(inCompletion.Awaitable.GetAwaiter().GetResult() == VisibilityCompletion.Visible,
                        duration + " second IN waiter resolves Visible", failures, "P0.H");
                }

                float[] invalidDurations = { float.NaN, float.PositiveInfinity, 0f, -1f };
                foreach (float duration in invalidDurations)
                {
                    bool rejected = false;
                    try { dissolve.DissolveOut(duration, null); }
                    catch (ArgumentOutOfRangeException) { rejected = true; }
                    Check(rejected && dissolve.VisibilityState == PerformerVisibilityState.Visible,
                        "invalid OUT duration is rejected without changing Visible state", failures, "P0.H");
                    bool inRejected = false;
                    try { dissolve.DissolveIn(duration, null); }
                    catch (ArgumentOutOfRangeException) { inRejected = true; }
                    Check(inRejected && dissolve.VisibilityState == PerformerVisibilityState.Visible,
                        "invalid IN duration is rejected without changing Visible state", failures, "P0.H");
                }

                var outAbort = new AwaitableCompletionSource<VisibilityCompletion>();
                dissolve.DissolveOut(3f, outAbort);
                dissolve.Advance(0.5f);
                dissolve.Dispose();
                dissolve = null;
                Check(stableHidden == false && presentation.ForceRenderingOff == false
                    && outAbort.Awaitable.GetAwaiter().GetResult() == VisibilityCompletion.PerformerDisabled,
                    "OUT interruption rolls back Visible and resolves waiter as PerformerDisabled", failures, "P0.H");

                var inPresentation = new FakePresentation { BodyCenter = new Vector3(0f, 1f, 0f), ForceRenderingOff = true };
                var inHost = new GameObject("P0H_HiddenInInterruptionSelfTest");
                var inDissolve = new PerformerDissolve(inHost.transform, profile, inPresentation, _ => { }, null,
                    initiallyHidden: true, stableVisibilityChanged: hidden => stableHidden = hidden);
                var inAbort = new AwaitableCompletionSource<VisibilityCompletion>();
                inDissolve.DissolveIn(3f, inAbort);
                inDissolve.Advance(0.5f);
                inDissolve.Dispose();
                Check(stableHidden && inDissolve.VisibilityState == PerformerVisibilityState.Hidden
                    && inPresentation.ForceRenderingOff && !inPresentation.DissolveEnabled
                    && inPresentation.ActivePopulationCount == 0
                    && inAbort.Awaitable.GetAwaiter().GetResult() == VisibilityCompletion.PerformerDisabled,
                    "IN interruption rolls back Hidden and resolves waiter as PerformerDisabled", failures, "P0.H");
                UnityEngine.Object.Destroy(inHost);

                // Mirror the facade's stable bool being fed back into a newly constructed
                // dissolve runtime after teardown; no effect body survives the boundary.
                var hiddenPresentation = new FakePresentation { ForceRenderingOff = true };
                var hiddenHost = new GameObject("P0H_StableHiddenRecreateSelfTest");
                var hiddenRuntime = new PerformerDissolve(hiddenHost.transform, profile, hiddenPresentation,
                    _ => { }, null, initiallyHidden: true, stableVisibilityChanged: hidden => stableHidden = hidden);
                hiddenRuntime.Dispose();
                var recreatedRuntime = new PerformerDissolve(hiddenHost.transform, profile, hiddenPresentation,
                    _ => { }, null, initiallyHidden: stableHidden, stableVisibilityChanged: hidden => stableHidden = hidden);
                Check(stableHidden && recreatedRuntime.VisibilityState == PerformerVisibilityState.Hidden
                    && hiddenPresentation.ForceRenderingOff && hiddenPresentation.ActivePopulationCount == 0
                    && !hiddenPresentation.DissolveEnabled,
                    "stable Hidden survives dissolve runtime teardown and recreation without particles", failures, "P0.H");
                recreatedRuntime.Dispose();
                UnityEngine.Object.Destroy(hiddenHost);
            }
            catch (Exception exception)
            {
                failures.Add("P0.H: visibility runtime self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                if (releaseField != null) releaseField.SetValue(profile, originalRelease);
                dissolve?.Dispose();
                UnityEngine.Object.Destroy(host);
            }
        }

        private static void CheckNewPerformerDefaultsHidden(List<string> failures)
        {
            var host = new GameObject("P0H_NewPerformerVisibilityDefaultSelfTest");
            host.SetActive(false);
            try
            {
                SuccubusPerformer performer = host.AddComponent<SuccubusPerformer>();
                FieldInfo startHidden = typeof(SuccubusPerformer).GetField("startHidden",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(startHidden != null && (bool)startHidden.GetValue(performer),
                    "new SuccubusPerformer instances default to startHidden", failures, "P0.H");
            }
            catch (Exception exception)
            {
                failures.Add("P0.H: new performer default self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        private static void Check(bool condition, string description, List<string> failures, string prefix)
        {
            if (!condition) failures.Add(prefix + ": " + description + ".");
        }

        private sealed class FakePresentation : IPerformerDissolvePresentation
        {
            public readonly List<string> Events = new List<string>();
            public Vector3 BodyCenter { get; set; }
            public Vector3 TransitDestination { get; private set; }
            public bool DissolveEnabled { get; private set; }
            public float DissolveProgress { get; private set; }
            public int ActivePopulationCount { get; private set; }
            public int CurrentPopulationId { get; private set; }
            public int LastFinishedPopulationId { get; private set; }
            public int TransitCount { get; private set; }
            public int MaterializeCount { get; private set; }
            public int FinishCount { get; private set; }
            public int RestoreCount { get; private set; }
            public bool ThrowOnBegin { get; set; }
            public bool ForceRenderingOff { get; set; }
            public float VisibilityClock { get; private set; }

            public float EvaluateEffectCurve(float normalizedTime) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime));

            public void Begin(PerformerDissolveProfile profile, PerformerDissolveTiming timing)
            {
                CurrentPopulationId++;
                ActivePopulationCount = 1;
                DissolveEnabled = true;
                DissolveProgress = 0f;
                ForceRenderingOff = false;
                Events.Add("begin");
                if (ThrowOnBegin) throw new InvalidOperationException("Synthetic particle setup failure.");
            }

            public void SetDissolveProgress(float progress) => DissolveProgress = Mathf.Clamp01(progress);

            public void CompleteDeparture() => Events.Add("departure-complete");

            public void BeginTransit(Vector3 destinationCenter)
            {
                TransitCount++;
                TransitDestination = destinationCenter;
                Events.Add("transit");
            }

            public void RetargetTransit(Vector3 destinationCenter) => TransitDestination = destinationCenter;

            public void SetTransitProgress(float progress) { }

            public void BeginMaterialize()
            {
                MaterializeCount++;
                Events.Add("materialize");
            }

            public void SetMaterializeProgress(float progress) => DissolveProgress = 1f - Mathf.Clamp01(progress);

            public void BeginVisibilityOut(PerformerDissolveProfile profile)
            {
                BeginVisibility(profile);
                ForceRenderingOff = false;
                Events.Add("visibility-out");
            }

            public void SetVisibilityOutProgress(float normalizedClock)
            {
                VisibilityClock = normalizedClock;
                DissolveProgress = Mathf.Clamp01(normalizedClock / 0.62f);
            }

            public void BeginVisibilityIn(PerformerDissolveProfile profile)
            {
                BeginVisibility(profile);
                ForceRenderingOff = true;
                DissolveProgress = 1f;
                Events.Add("visibility-in");
            }

            public void SetVisibilityInProgress(float normalizedClock)
            {
                VisibilityClock = normalizedClock;
                if (normalizedClock >= 0.17f) ForceRenderingOff = false;
                DissolveProgress = 1f - Mathf.Clamp01((normalizedClock - 0.17f) / 0.66f);
            }

            public void Finish(bool hidden)
            {
                LastFinishedPopulationId = CurrentPopulationId;
                FinishCount++;
                ActivePopulationCount = 0;
                DissolveEnabled = false;
                DissolveProgress = 0f;
                ForceRenderingOff = hidden;
                Events.Add(hidden ? "finish-hidden" : "finish-visible");
            }

            public void Restore(bool hidden)
            {
                RestoreCount++;
                ActivePopulationCount = 0;
                DissolveEnabled = false;
                DissolveProgress = 0f;
                ForceRenderingOff = hidden;
                Events.Add(hidden ? "restore-hidden" : "restore-visible");
            }

            private void BeginVisibility(PerformerDissolveProfile profile)
            {
                CurrentPopulationId++;
                ActivePopulationCount = 1;
                DissolveEnabled = true;
                DissolveProgress = 0f;
                VisibilityClock = 0f;
                if (ThrowOnBegin) throw new InvalidOperationException("Synthetic visibility particle setup failure.");
            }
        }
    }
}
