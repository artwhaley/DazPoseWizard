using System;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    internal enum MagicEffectMode
    {
        Spell = 0,
        Aura = 1
    }

    internal interface IPerformerMagicEffectInstance : IDisposable
    {
        PerformerMagicStyle Style { get; }
        Transform Target { get; }
        bool IsDisposed { get; }
        void Advance(float deltaTime);
        void FadeOut(float seconds);
    }

    /// <summary>One world-space VFX host following a live target transform.</summary>
    internal sealed class MagicEffectInstance : IPerformerMagicEffectInstance
    {
        private static readonly int EffectModeId = Shader.PropertyToID("EffectMode");
        private static readonly int EffectTimeId = Shader.PropertyToID("EffectTime");
        private static readonly int EffectProgressId = Shader.PropertyToID("EffectProgress");
        private static readonly int TargetCenterId = Shader.PropertyToID("TargetCenter");
        private static readonly int TargetRadiusId = Shader.PropertyToID("TargetRadius");
        private static readonly int TargetHeightId = Shader.PropertyToID("TargetHeight");
        private static readonly int StyleId = Shader.PropertyToID("StyleId");
        private static readonly int PrimaryColorId = Shader.PropertyToID("PrimaryColor");
        private static readonly int SecondaryColorId = Shader.PropertyToID("SecondaryColor");
        private static readonly int AccentColorId = Shader.PropertyToID("AccentColor");
        private static readonly int SmokeColorId = Shader.PropertyToID("SmokeColor");
        private static readonly int IntensityId = Shader.PropertyToID("Intensity");
        private static readonly int ParticleSizeId = Shader.PropertyToID("ParticleSize");
        private static readonly int RiseSpeedId = Shader.PropertyToID("RiseSpeed");
        private static readonly int SwirlStrengthId = Shader.PropertyToID("SwirlStrength");
        private static readonly int TurbulenceId = Shader.PropertyToID("Turbulence");
        private static readonly int PulseFrequencyId = Shader.PropertyToID("PulseFrequency");
        private static readonly int SeedId = Shader.PropertyToID("Seed");
        private static readonly int SpawnRateId = Shader.PropertyToID("SpawnRate");
        private static readonly int SpawnBurstCountId = Shader.PropertyToID("SpawnBurstCount");
        private static readonly int ParticleLifetimeId = Shader.PropertyToID("ParticleLifetime");

        private readonly MagicEffectMode _mode;
        private readonly Action<SpellCompletion> _completion;
        private readonly MagicTargetGeometry _geometry;
        private readonly GameObject _host;
        private readonly VisualEffect _effect;
        private float _elapsed;
        private float _effectTime;
        private float _fadeElapsed;
        private float _fadeDuration;
        private float _fadeStartIntensity;
        private float _currentIntensity;
        private readonly int _seed;
        private bool _isFadingOut;
        private bool _isDisposed;

        public PerformerMagicStyle Style { get; }
        public Transform Target => _geometry.Target;
        public bool IsDisposed => _isDisposed;

        public MagicEffectInstance(PerformerMagicStyle style, Transform target, MagicEffectMode mode,
            Action<SpellCompletion> completion = null)
        {
            Style = style != null ? style : throw new ArgumentNullException(nameof(style));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!style.IsReady(out string reason)) throw new InvalidOperationException(reason);

            _mode = mode;
            _completion = completion;
            _geometry = new MagicTargetGeometry(target);
            _seed = UnityEngine.Random.Range(1, int.MaxValue);
            _host = new GameObject("Performer Magic " + style.DisplayName + " " + mode);
            _effect = _host.AddComponent<VisualEffect>();
            _effect.visualEffectAsset = style.EffectGraph;
            _effect.startSeed = (uint)_seed;
            _effect.resetSeedOnPlay = false;
            _currentIntensity = mode == MagicEffectMode.Aura && style.AuraFadeInSeconds > 0f
                ? 0f : style.Intensity;

            try
            {
                RequireGraphContract();
                MagicTargetSample sample = _geometry.Sample(style.TargetScale, style.TargetPadding);
                _effect.Reinit();
                WriteStaticInputs(sample);
                _effect.SetFloat(SpawnRateId, mode == MagicEffectMode.Aura ? style.AuraSpawnRate : 0f);
                _effect.SetFloat(SpawnBurstCountId, mode == MagicEffectMode.Spell ? style.SpellBurstCount : 0f);
                _effect.SetFloat(ParticleLifetimeId, mode == MagicEffectMode.Spell
                    ? style.SpellParticleLifetimeSeconds : style.AuraParticleLifetimeSeconds);
                _effect.SetFloat(EffectProgressId, 0f);
                _effect.SetFloat(EffectTimeId, 0f);
                _effect.SetFloat(IntensityId, _currentIntensity);
                _effect.Play();
            }
            catch
            {
                DisposeHost();
                throw;
            }
        }

        public void Advance(float deltaTime)
        {
            if (_isDisposed) return;
            if (Target == null)
            {
                FinishSpell(SpellCompletion.TargetLost);
                return;
            }

            float delta = Mathf.Max(0f, deltaTime);
            _elapsed += delta;
            _effectTime += delta;
            MagicTargetSample sample = _geometry.Sample(Style.TargetScale, Style.TargetPadding);
            WriteStaticInputs(sample);
            _effect.SetFloat(EffectTimeId, _effectTime);

            if (_mode == MagicEffectMode.Spell)
            {
                float duration = Mathf.Max(0.01f, Style.SpellDurationSeconds);
                float progress = Mathf.Clamp01(_elapsed / duration);
                _effect.SetFloat(EffectProgressId, progress);
                if (_elapsed >= duration) FinishSpell(SpellCompletion.Completed);
                return;
            }

            _effect.SetFloat(EffectProgressId, Mathf.Repeat(_effectTime * Style.PulseFrequency * 0.1f, 1f));
            if (_isFadingOut)
            {
                _fadeElapsed += delta;
                float fade = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeElapsed / _fadeDuration);
                _currentIntensity = _fadeStartIntensity * (1f - fade);
                _effect.SetFloat(IntensityId, _currentIntensity);
                _effect.SetFloat(SpawnRateId, 0f);
                if (fade >= 1f) DisposeHost();
            }
            else if (Style.AuraFadeInSeconds > 0f && _elapsed < Style.AuraFadeInSeconds)
            {
                _currentIntensity = Style.Intensity * Mathf.Clamp01(_elapsed / Style.AuraFadeInSeconds);
                _effect.SetFloat(IntensityId, _currentIntensity);
            }
            else if (_currentIntensity != Style.Intensity)
            {
                _currentIntensity = Style.Intensity;
                _effect.SetFloat(IntensityId, _currentIntensity);
            }
        }

        public void FadeOut(float seconds)
        {
            if (_isDisposed || _isFadingOut) return;
            _isFadingOut = true;
            _fadeElapsed = 0f;
            _fadeDuration = Mathf.Max(0f, seconds);
            _fadeStartIntensity = _currentIntensity;
            if (_effect != null) _effect.SetFloat(SpawnRateId, 0f);
            if (_fadeDuration <= 0f) DisposeHost();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            if (_mode == MagicEffectMode.Spell) FinishSpell(SpellCompletion.PerformerDisabled);
            else DisposeHost();
        }

        private void WriteStaticInputs(MagicTargetSample sample)
        {
            _effect.SetInt(EffectModeId, (int)_mode);
            _effect.SetVector3(TargetCenterId, sample.Center);
            _effect.SetFloat(TargetRadiusId, sample.Radius);
            _effect.SetFloat(TargetHeightId, sample.Height);
            _effect.SetInt(StyleId, Style.StyleId);
            _effect.SetVector4(PrimaryColorId, ToVector4(Style.PrimaryColor));
            _effect.SetVector4(SecondaryColorId, ToVector4(Style.SecondaryColor));
            _effect.SetVector4(AccentColorId, ToVector4(Style.AccentColor));
            _effect.SetVector4(SmokeColorId, ToVector4(Style.SmokeColor));
            _effect.SetFloat(ParticleSizeId, Style.ParticleSize);
            _effect.SetFloat(RiseSpeedId, Style.RiseSpeed);
            _effect.SetFloat(SwirlStrengthId, Style.SwirlStrength);
            _effect.SetFloat(TurbulenceId, Style.Turbulence);
            _effect.SetFloat(PulseFrequencyId, Style.PulseFrequency);
            _effect.SetInt(SeedId, _seed);
        }

        private void RequireGraphContract()
        {
            Require(_effect.HasInt(EffectModeId), "EffectMode", "int");
            Require(_effect.HasFloat(EffectTimeId), "EffectTime", "float");
            Require(_effect.HasFloat(EffectProgressId), "EffectProgress", "float");
            Require(_effect.HasVector3(TargetCenterId), "TargetCenter", "Vector3");
            Require(_effect.HasFloat(TargetRadiusId), "TargetRadius", "float");
            Require(_effect.HasFloat(TargetHeightId), "TargetHeight", "float");
            Require(_effect.HasInt(StyleId), "StyleId", "int");
            Require(_effect.HasVector4(PrimaryColorId), "PrimaryColor", "Vector4");
            Require(_effect.HasVector4(SecondaryColorId), "SecondaryColor", "Vector4");
            Require(_effect.HasVector4(AccentColorId), "AccentColor", "Vector4");
            Require(_effect.HasVector4(SmokeColorId), "SmokeColor", "Vector4");
            Require(_effect.HasFloat(IntensityId), "Intensity", "float");
            Require(_effect.HasFloat(ParticleSizeId), "ParticleSize", "float");
            Require(_effect.HasFloat(RiseSpeedId), "RiseSpeed", "float");
            Require(_effect.HasFloat(SwirlStrengthId), "SwirlStrength", "float");
            Require(_effect.HasFloat(TurbulenceId), "Turbulence", "float");
            Require(_effect.HasFloat(PulseFrequencyId), "PulseFrequency", "float");
            Require(_effect.HasInt(SeedId), "Seed", "int");
            Require(_effect.HasFloat(SpawnRateId), "SpawnRate", "float");
            Require(_effect.HasFloat(SpawnBurstCountId), "SpawnBurstCount", "float");
            Require(_effect.HasFloat(ParticleLifetimeId), "ParticleLifetime", "float");
        }

        private void Require(bool present, string property, string type)
        {
            if (!present)
                throw new InvalidOperationException("Magic VFX Graph '" + Style.DisplayName + "' is missing exposed "
                    + type + " property '" + property + "'. Regenerate or repair the project-owned Magic Graph.");
        }

        private static Vector4 ToVector4(Color color) => new Vector4(color.r, color.g, color.b, color.a);

        private void FinishSpell(SpellCompletion result)
        {
            if (_isDisposed) return;
            DisposeHost();
            _completion?.Invoke(result);
        }

        private void DisposeHost()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            if (_effect != null) _effect.Stop();
            if (_host != null) UnityEngine.Object.Destroy(_host);
        }
    }
}
