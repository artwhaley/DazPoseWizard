using System;
using System.Collections.Generic;
using DazPose.Motion;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.Performer
{
    /// <summary>Deterministic driver, consumer, frozen-clip and variant-mixer checks.</summary>
    public static class PerformerMotionRuntimeSelfTests
    {
        private const float Tolerance = 0.0002f;

        public static string[] Run()
        {
            var failures = new List<string>();
            var host = new GameObject("MotionRuntimeSelfTestHost");
            MotionDriver driver = host.AddComponent<MotionDriver>();
            CheckDriver(driver, failures);
#if UNITY_EDITOR
            CheckLayerAndConsumer(driver, failures);
            CheckFunscriptSourceFeedsConsumer(driver, failures);
#endif
            DestroyObject(host);
            return failures.ToArray();
        }

        private static void CheckDriver(MotionDriver driver, List<string> failures)
        {
            driver.ResetMotion();
            Check(Near(driver.CurrentSample.Position01, 0f), "reset starts at endpoint A", failures);

            driver.FrequencyHz = 1f;
            driver.StartMotion();
            driver.Advance(0.25f);
            Check(Near(driver.CurrentSample.Position01, 0.5f), "1 Hz reaches 0.5 at 0.25 seconds", failures);
            Check(driver.CurrentSample.Direction == MotionDirection.Increasing
                && Near(driver.CurrentSample.Velocity, Mathf.PI), "first half-cycle has analytic increasing velocity", failures);
            float heldPhase = driver.CurrentSample.Phase01;
            driver.FrequencyHz = 2f;
            Check(Near(driver.CurrentSample.Phase01, heldPhase)
                && Near(driver.CurrentSample.Position01, 0.5f), "frequency change preserves phase and position", failures);
            driver.Advance(0.125f);
            Check(Near(driver.CurrentSample.Phase01, 0.5f)
                && Near(driver.CurrentSample.Position01, 1f), "new frequency changes speed without a phase jump", failures);
            driver.ResetMotion();
            driver.FrequencyHz = 1f;
            driver.StartMotion();
            driver.Advance(0.25f);
            Check(Near(driver.CurrentSample.Position01, 1f)
                && driver.CurrentSample.Direction == MotionDirection.Stationary
                && Mathf.Abs(driver.CurrentSample.Velocity) < Tolerance,
                "1 Hz reaches endpoint B with approximately zero velocity at 0.50 seconds", failures);
            driver.Advance(0.25f);
            Check(Near(driver.CurrentSample.Position01, 0.5f)
                && driver.CurrentSample.Direction == MotionDirection.Decreasing,
                "second half-cycle decreases through 0.5 at 0.75 seconds", failures);
            driver.Advance(0.25f);
            Check(Near(driver.CurrentSample.Position01, 0f)
                && driver.CurrentSample.Direction == MotionDirection.Stationary,
                "1 Hz returns to endpoint A at 1.00 seconds", failures);

            MotionSample beforeStop = driver.CurrentSample;
            driver.StopMotion();
            driver.Advance(0.5f);
            Check(SameSample(beforeStop, driver.CurrentSample), "Stop freezes the published sample", failures);
            driver.StartMotion();
            driver.Advance(0.25f);
            Check(Near(driver.CurrentSample.Phase01, 0.25f), "Start resumes from the held phase", failures);
            driver.RestartMotion();
            Check(driver.IsRunning && Near(driver.CurrentSample.Phase01, 0f)
                && Near(driver.CurrentSample.TimeSeconds, 0d), "Restart returns to A and runs", failures);
            driver.Advance(0.25f);
            driver.ResetMotion();
            Check(!driver.IsRunning && Near(driver.CurrentSample.Position01, 0f)
                && Near(driver.CurrentSample.TimeSeconds, 0d), "Reset stops and restores endpoint A", failures);

            driver.FrequencyHz = 1f;
            driver.StartMotion();
            driver.Advance(0.1f);
            MotionSample current = driver.CurrentSample;
            var receivedA = new List<MotionSample>();
            var receivedB = new List<MotionSample>();
            driver.Sampled += receivedA.Add;
            driver.Sampled += receivedB.Add;
            long sequenceBefore = driver.CurrentSample.Sequence;
            driver.Advance(0.1f);
            Check(driver.CurrentSample.Sequence == sequenceBefore + 1
                && receivedA.Count == 1 && receivedB.Count == 1
                && SameSample(receivedA[0], receivedB[0])
                && SameSample(receivedA[0], driver.CurrentSample),
                "one authoritative increment is shared by multiple subscribers", failures);
            Check(Near(current.Phase01, 0.1f) && Near(receivedA[0].Phase01, 0.2f),
                "late subscription leaves a running driver at its current phase", failures);
            driver.Sampled -= receivedA.Add;
            driver.Sampled -= receivedB.Add;
            driver.StopMotion();
        }

#if UNITY_EDITOR
        private static void CheckLayerAndConsumer(MotionDriver driver, List<string> failures)
        {
            driver.ResetMotion();
            PlayableGraph graph = PlayableGraph.Create("Motion runtime self-test");
            var animatorObject = new GameObject("MotionSyntheticAnimator");
            Animator animator = animatorObject.AddComponent<Animator>();
            AnimationClip baseClip = MakeClip(1f, 0f, 0.1f);
            AnimationClip shortClip = MakeClip(1f, 0f, 0.5f);
            AnimationClip longClip = MakeClip(2f, 0f, 1f);
            PerformerMotionVariant shortVariant = MakeVariant(shortClip, "Short");
            PerformerMotionVariant longVariant = MakeVariant(longClip, "Long");
            PerformerMotionSet set = ScriptableObject.CreateInstance<PerformerMotionSet>();
            set.Configure(new[] { shortVariant, longVariant });
            var mask = new AvatarMask { transformCount = 1 };
            mask.SetTransformPath(0, "joint");
            mask.SetTransformActive(0, true);
            PerformerMotionLayer layer = null;
            PerformerMotionConsumer consumer = null;

            try
            {
                Playable basePlayable = AnimationClipPlayable.Create(graph, baseClip);
                layer = new PerformerMotionLayer(graph, basePlayable, animator, set, mask, 0.2f, validateMask: false);
                driver.FrequencyHz = 1f;
                driver.StartMotion();
                driver.Advance(0.25f);
                long bindSequence = driver.CurrentSample.Sequence;
                float bindPhase = driver.CurrentSample.Phase01;
                consumer = new PerformerMotionConsumer(driver, layer);
                Check(driver.IsRunning && driver.CurrentSample.Sequence == bindSequence
                    && Near(driver.CurrentSample.Phase01, bindPhase)
                    && Near(layer.SampledPosition01, driver.CurrentSample.Position01),
                    "mid-cycle binding samples CurrentSample without restarting MotionDriver", failures);
                consumer.Advance(0.2f);
                Check(Near(layer.MotionWeight, 1f) && Near(layer.SampledPosition01, 0.5f),
                    "endpoint position and motion ownership remain independent", failures);

                var sample = new MotionSample(bindSequence + 1, driver.CurrentSample.TimeSeconds,
                    0.91f, 0.25f, -1f, MotionDirection.Decreasing);
                layer.SetSample(sample);
                Check(Near(layer.GetVariantSampleTime(0), 0.25f)
                    && Near(layer.GetVariantSampleTime(1), 0.5f),
                    "normalized Position01 maps correctly across different clip lengths", failures);
                Check(Math.Abs(layer.GetVariantSampleTime(0) - sample.Phase01) > 0.1f
                    && Near((float)layer.GetVariantSampleTime(0), sample.Position01),
                    "clip time uses Position01 rather than Phase01", failures);
                Check(Near((float)layer.GetVariantSpeed(0), 0f)
                    && Near((float)layer.GetVariantSpeed(1), 0f), "every motion playable remains frozen", failures);

                MotionSample zero = new MotionSample(sample.Sequence + 1, sample.TimeSeconds,
                    0.76f, 0f, -1f, MotionDirection.Decreasing);
                layer.SetSample(zero);
                Check(Near(layer.GetVariantSampleTime(0), 0f)
                    && Near(layer.GetVariantSampleTime(1), 0f)
                    && Near(layer.MotionWeight, 1f),
                    "zero position is active endpoint A while the independent ownership weight stays engaged", failures);

                layer.SetSample(new MotionSample(zero.Sequence + 1, 1d, 0.8f, 0.8f, 1f, MotionDirection.Increasing));
                float forwardTime = layer.GetVariantSampleTime(0);
                layer.SetSample(new MotionSample(zero.Sequence + 2, 1.1d, 0.7f, 0.65f, -1f, MotionDirection.Decreasing));
                Check(layer.GetVariantSampleTime(0) < forwardTime
                    && Near(layer.GetVariantSampleTime(0), 0.65f),
                    "decreasing Position01 scrubs the frozen playable backward", failures);

                MotionSample driverSample = driver.CurrentSample;
                layer.SetSample(driverSample);
                float beforeVariantTimeA = layer.GetVariantSampleTime(0);
                float beforeVariantTimeB = layer.GetVariantSampleTime(1);
                driverSample = driver.CurrentSample;
                layer.SetVariant(1, 0.4f);
                consumer.Advance(0.2f);
                Check(Near(layer.GetVariantWeight(0), 0.5f) && Near(layer.GetVariantWeight(1), 0.5f)
                    && Near(layer.GetVariantSampleTime(0), beforeVariantTimeA)
                    && Near(layer.GetVariantSampleTime(1), beforeVariantTimeB)
                    && SameSample(driverSample, driver.CurrentSample),
                    "variant crossfade preserves the driver's phase and both normalized clip samples", failures);
                consumer.Advance(0.2f);
                Check(Near(layer.GetVariantWeight(0), 0f) && Near(layer.GetVariantWeight(1), 1f)
                    && layer.ActiveVariantName == "Long", "variant mixer completes its bounded crossfade", failures);

                driver.Advance(0.25f);
                MotionSample stoppingSample = driver.CurrentSample;
                float sampledAtStop = layer.SampledPosition01;
                driver.StopMotion();
                driver.Advance(0.5f);
                consumer.Advance(0.2f);
                Check(!driver.IsRunning && SameSample(stoppingSample, driver.CurrentSample)
                    && Near(layer.SampledPosition01, sampledAtStop) && Near(layer.MotionWeight, 0f),
                    "stopping holds the last scrub coordinate while motion ownership blends away", failures);

                driver.StartMotion();
                Check(Near(layer.SampledPosition01, driver.CurrentSample.Position01),
                    "resume samples the held position before re-engaging ownership", failures);
                consumer.Advance(0.2f);
                Check(Near(layer.MotionWeight, 1f), "resume blends motion ownership back in", failures);
                consumer.Dispose();
                consumer = null;
                float unboundTime = layer.GetVariantSampleTime(1);
                driver.Advance(0.1f);
                Check(Near(layer.GetVariantSampleTime(1), unboundTime),
                    "unbinding unsubscribes from future MotionSample events", failures);
            }
            catch (Exception exception)
            {
                failures.Add("Motion consumer/layer self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                consumer?.Dispose();
                layer?.Dispose();
                if (graph.IsValid()) graph.Destroy();
                DestroyObject(animatorObject);
                DestroyObject(baseClip);
                DestroyObject(shortClip);
                DestroyObject(longClip);
                DestroyObject(shortVariant);
                DestroyObject(longVariant);
                DestroyObject(set);
                DestroyObject(mask);
            }
            driver.StopMotion();
        }

        private static void CheckFunscriptSourceFeedsConsumer(MotionDriver driver, List<string> failures)
        {
            FunscriptMotionProgram program = null;
            var animatorObject = new GameObject("FunscriptMotionSyntheticAnimator");
            PlayableGraph graph = PlayableGraph.Create("Funscript source consumer self-test");
            AnimationClip baseClip = null;
            AnimationClip motionClip = null;
            PerformerMotionVariant variant = null;
            PerformerMotionSet set = null;
            AvatarMask mask = null;
            PerformerMotionLayer layer = null;
            PerformerMotionConsumer consumer = null;
            try
            {
                program = FunscriptJsonParser.Parse("{\"range\":100,\"metadata\":{\"duration\":4},"
                    + "\"actions\":[{\"at\":1000,\"pos\":10},{\"at\":2000,\"pos\":50},{\"at\":3000,\"pos\":0}]}");
                driver.StopMotion();
                driver.FunscriptProgram = program;
                driver.SourceMode = MotionSourceMode.Funscript;

                Animator animator = animatorObject.AddComponent<Animator>();
                baseClip = MakeClip(1f, 0f, 0.1f);
                motionClip = MakeClip(2f, 0f, 1f);
                variant = MakeVariant(motionClip, "FunscriptScrub");
                set = ScriptableObject.CreateInstance<PerformerMotionSet>();
                set.Configure(new[] { variant });
                mask = new AvatarMask { transformCount = 1 };
                mask.SetTransformPath(0, "joint");
                mask.SetTransformActive(0, true);

                Playable basePlayable = AnimationClipPlayable.Create(graph, baseClip);
                layer = new PerformerMotionLayer(graph, basePlayable, animator, set, mask,
                    ownershipBlendSeconds: 0f, validateMask: false);
                consumer = new PerformerMotionConsumer(driver, layer);
                driver.StartMotion();
                driver.Advance(1.5f);
                Check(Near(driver.CurrentSample.Position01, 0.3f)
                    && Near(layer.SampledPosition01, driver.CurrentSample.Position01)
                    && Near(layer.GetVariantSampleTime(0), 0.6f),
                    "Funscript Position01 reaches the existing frozen-clip consumer unchanged", failures);

                float forwardClipTime = layer.GetVariantSampleTime(0);
                driver.Advance(1f);
                Check(driver.CurrentSample.Direction == MotionDirection.Decreasing
                    && Near(driver.CurrentSample.Position01, 0.25f)
                    && Near(layer.SampledPosition01, 0.25f)
                    && layer.GetVariantSampleTime(0) < forwardClipTime
                    && Near(layer.GetVariantSampleTime(0), 0.5f),
                    "reverse Funscript motion scrubs the same clip backward without a source-specific consumer", failures);
                Check(Near(animator.transform.position.sqrMagnitude, 0f),
                    "Funscript sampling leaves the animator root untouched", failures);
            }
            catch (Exception exception)
            {
                failures.Add("Funscript consumer integration test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                driver.StopMotion();
                driver.FunscriptProgram = null;
                driver.SourceMode = MotionSourceMode.Sine;
                driver.ResetMotion();
                consumer?.Dispose();
                layer?.Dispose();
                if (graph.IsValid()) graph.Destroy();
                DestroyObject(animatorObject);
                DestroyObject(baseClip);
                DestroyObject(motionClip);
                DestroyObject(variant);
                DestroyObject(set);
                DestroyObject(mask);
                DestroyObject(program);
            }
        }

        private static PerformerMotionVariant MakeVariant(AnimationClip clip, string displayName)
        {
            var variant = ScriptableObject.CreateInstance<PerformerMotionVariant>();
            variant.Configure(clip, displayName);
            return variant;
        }

        private static AnimationClip MakeClip(float length, float start, float end)
        {
            var clip = new AnimationClip { frameRate = 60f, wrapMode = WrapMode.Once };
            clip.SetCurve("joint", typeof(Transform), "m_LocalPosition.y",
                new AnimationCurve(new Keyframe(0f, start), new Keyframe(length, end)));
            return clip;
        }
#endif

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static bool SameSample(MotionSample a, MotionSample b) =>
            a.Sequence == b.Sequence && Math.Abs(a.TimeSeconds - b.TimeSeconds) < 0.000001d
            && Near(a.Phase01, b.Phase01) && Near(a.Position01, b.Position01)
            && Near(a.Velocity, b.Velocity) && a.Direction == b.Direction;

        private static bool Near(float a, float b) => Mathf.Abs(a - b) <= Tolerance;

        private static bool Near(double a, double b) => Math.Abs(a - b) <= Tolerance;

        private static void Check(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add("Motion: " + message + ".");
        }
    }
}
