using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Deterministic lifetime and target-geometry checks run by the existing F5 acceptance harness.</summary>
    internal static class PerformerMagicRuntimeSelfTests
    {
        public static string[] Run()
        {
            var failures = new List<string>();
            var owner = new GameObject("MagicRuntimeSelfTestOwner");
            var target = new GameObject("MagicRuntimeSelfTestTarget");
            var otherTarget = new GameObject("MagicRuntimeSelfTestOtherTarget");
            var spellStyle = ScriptableObject.CreateInstance<PerformerMagicStyle>();
            var secondStyle = ScriptableObject.CreateInstance<PerformerMagicStyle>();
            var firstSpell = CreateAsset<PerformerSpell>(spellStyle);
            var secondSpell = CreateAsset<PerformerSpell>(spellStyle);
            var firstAura = CreateAsset<PerformerAura>(spellStyle);
            var secondAura = CreateAsset<PerformerAura>(secondStyle);
            var instances = new List<FakeMagicEffect>();
            var completions = new List<SpellCompletion>();
            int auraTargetLosses = 0;
            PerformerMagicRuntime runtime = null;

            try
            {
                TestFallbackGeometry(owner.transform, failures);
                TestRendererGeometry(owner.transform, failures);

                runtime = new PerformerMagicRuntime(owner.transform,
                    (aura, lostTarget) => auraTargetLosses++,
                    (style, effectTarget, mode, completion) =>
                    {
                        var instance = new FakeMagicEffect(style, effectTarget, mode, completion);
                        instances.Add(instance);
                        return instance;
                    }, validateAssets: false);

                runtime.Cast(firstSpell, owner.transform, result => completions.Add(result));
                Check(instances[0].Target == owner.transform, "self Cast targets the performer Transform", failures);
                runtime.Cast(secondSpell, target.transform, result => completions.Add(result));
                Check(runtime.ActiveSpellCount == 2, "multiple finite Cast instances coexist", failures);
                instances[1].CompleteNaturally();
                Check(completions.Count == 1 && completions[0] == SpellCompletion.Completed,
                    "finite Cast completion resolves independently", failures);
                runtime.Advance(0f);
                Check(runtime.ActiveSpellCount == 1, "completed Cast instance is removed promptly", failures);

                GameObject castTarget = new GameObject("MagicRuntimeSelfTestLostCastTarget");
                runtime.Cast(firstSpell, castTarget.transform, result => completions.Add(result));
                UnityEngine.Object.DestroyImmediate(castTarget);
                runtime.Advance(0f);
                Check(completions.Count == 2 && completions[1] == SpellCompletion.TargetLost,
                    "destroyed Cast target resolves TargetLost", failures);

                runtime.SetAura(firstAura, target.transform);
                FakeMagicEffect firstAuraInstance = instances[instances.Count - 1];
                int countBeforeIdempotent = instances.Count;
                runtime.SetAura(firstAura, target.transform);
                Check(instances.Count == countBeforeIdempotent && runtime.CurrentAura == firstAura,
                    "same Aura and target request is idempotent", failures);
                runtime.SetAura(secondAura, otherTarget.transform);
                Check(runtime.CurrentAura == secondAura && runtime.AuraTarget == otherTarget.transform
                    && firstAuraInstance.IsFadingOut,
                    "new Aura replaces and fades the previous slot", failures);
                runtime.ClearAura();
                Check(!runtime.HasAura && runtime.CurrentAura == null,
                    "ClearAura clears desired persistent state", failures);

                runtime.SetAura(firstAura, target.transform);
                UnityEngine.Object.DestroyImmediate(target);
                runtime.Advance(0f);
                Check(auraTargetLosses == 1 && !runtime.HasAura,
                    "destroyed Aura target clears the desired Aura", failures);

                runtime.Cast(firstSpell, owner.transform, result => completions.Add(result));
                int activeBeforeDispose = runtime.ActiveSpellCount;
                int beforeDispose = completions.Count;
                runtime.Dispose();
                runtime = null;
                bool allDisabled = completions.Count == beforeDispose + activeBeforeDispose;
                for (int i = beforeDispose; allDisabled && i < completions.Count; i++)
                    allDisabled &= completions[i] == SpellCompletion.PerformerDisabled;
                Check(allDisabled,
                    "active Cast resolves PerformerDisabled on runtime teardown", failures);
            }
            catch (Exception exception)
            {
                failures.Add("Magic runtime self-test threw " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                runtime?.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(otherTarget);
                UnityEngine.Object.DestroyImmediate(spellStyle);
                UnityEngine.Object.DestroyImmediate(secondStyle);
                UnityEngine.Object.DestroyImmediate(firstSpell);
                UnityEngine.Object.DestroyImmediate(secondSpell);
                UnityEngine.Object.DestroyImmediate(firstAura);
                UnityEngine.Object.DestroyImmediate(secondAura);
            }

            return failures.ToArray();
        }

        private static void TestFallbackGeometry(Transform target, List<string> failures)
        {
            target.position = new Vector3(1f, 2f, 3f);
            var geometry = new MagicTargetGeometry(target);
            MagicTargetSample first = geometry.Sample(1f, 0f);
            Check(Vector3.Distance(first.Center, target.position) < 0.001f
                && Mathf.Abs(first.Radius - 0.5f) < 0.001f
                && Mathf.Abs(first.Height - 1.8f) < 0.001f,
                "Renderer-free target uses centered 0.5 m radius and 1.8 m height fallback", failures);
            target.position += new Vector3(2f, 0.5f, -1f);
            MagicTargetSample moved = geometry.Sample(1f, 0f);
            Check(Vector3.Distance(moved.Center, target.position) < 0.001f,
                "cached target geometry follows live Transform movement", failures);

            var particleOnly = new GameObject("MagicRuntimeSelfTestParticleOnly");
            particleOnly.AddComponent<ParticleSystem>();
            MagicTargetSample particleFallback = new MagicTargetGeometry(particleOnly.transform).Sample(1f, 0f);
            Check(Mathf.Abs(particleFallback.Radius - 0.5f) < 0.001f,
                "particle-effect Renderer is excluded from target-bound discovery", failures);
            UnityEngine.Object.DestroyImmediate(particleOnly);
        }

        private static void TestRendererGeometry(Transform owner, List<string> failures)
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = "MagicRuntimeSelfTestRenderer";
            mesh.transform.SetParent(owner, false);
            MagicTargetSample sample = new MagicTargetGeometry(owner).Sample(1f, 0f);
            Check(sample.Height > 1.5f && sample.Radius > 0.2f,
                "renderer-backed target geometry is derived from child bounds", failures);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        private static T CreateAsset<T>(PerformerMagicStyle style) where T : ScriptableObject
        {
            T item = ScriptableObject.CreateInstance<T>();
            FieldInfo field = typeof(T).GetField("style", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(typeof(T).FullName, "style");
            field.SetValue(item, style);
            return item;
        }

        private static void Check(bool condition, string description, List<string> failures)
        {
            if (!condition) failures.Add("Magic: " + description + ".");
        }

        private sealed class FakeMagicEffect : IPerformerMagicEffectInstance
        {
            private readonly MagicEffectMode _mode;
            private readonly Action<SpellCompletion> _completion;
            private float _fadeElapsed;
            private float _fadeDuration;

            public PerformerMagicStyle Style { get; }
            public Transform Target { get; }
            public bool IsDisposed { get; private set; }
            public bool IsFadingOut { get; private set; }

            public FakeMagicEffect(PerformerMagicStyle style, Transform target, MagicEffectMode mode,
                Action<SpellCompletion> completion)
            {
                Style = style;
                Target = target;
                _mode = mode;
                _completion = completion;
            }

            public void Advance(float deltaTime)
            {
                if (IsDisposed) return;
                if (Target == null)
                {
                    Complete(SpellCompletion.TargetLost);
                    return;
                }
                if (IsFadingOut)
                {
                    _fadeElapsed += deltaTime;
                    if (_fadeElapsed >= _fadeDuration) Dispose();
                }
            }

            public void FadeOut(float seconds)
            {
                if (IsDisposed) return;
                IsFadingOut = true;
                _fadeDuration = Mathf.Max(0f, seconds);
                if (_fadeDuration <= 0f) Dispose();
            }

            public void Dispose()
            {
                if (IsDisposed) return;
                if (_mode == MagicEffectMode.Spell) Complete(SpellCompletion.PerformerDisabled);
                else IsDisposed = true;
            }

            public void CompleteNaturally() => Complete(SpellCompletion.Completed);

            private void Complete(SpellCompletion result)
            {
                if (IsDisposed) return;
                IsDisposed = true;
                _completion?.Invoke(result);
            }
        }
    }
}
