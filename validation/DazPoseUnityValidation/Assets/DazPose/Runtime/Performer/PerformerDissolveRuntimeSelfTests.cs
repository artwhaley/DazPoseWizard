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
                Check(presentation.TransitCount == 0 && presentation.MaterializeCount == 0,
                    "transit waits while the relocation frame is still current", failures);
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
                dissolve.Advance(profile.MaterializeDuration + 0.01f);
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
                dissolve.Advance(profile.MaterializeDuration + 0.01f);
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

            public float EvaluateEffectCurve(float normalizedTime) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime));

            public void Begin(PerformerDissolveProfile profile)
            {
                CurrentPopulationId++;
                ActivePopulationCount = 1;
                DissolveEnabled = true;
                DissolveProgress = 0f;
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

            public void SetTransitProgress(float progress) { }

            public void BeginMaterialize()
            {
                MaterializeCount++;
                Events.Add("materialize");
            }

            public void SetMaterializeProgress(float progress) => DissolveProgress = 1f - Mathf.Clamp01(progress);

            public void Finish()
            {
                LastFinishedPopulationId = CurrentPopulationId;
                FinishCount++;
                ActivePopulationCount = 0;
                DissolveEnabled = false;
                DissolveProgress = 0f;
                Events.Add("finish");
            }

            public void Restore()
            {
                RestoreCount++;
                ActivePopulationCount = 0;
                DissolveEnabled = false;
                DissolveProgress = 0f;
                Events.Add("restore");
            }
        }
    }
}
