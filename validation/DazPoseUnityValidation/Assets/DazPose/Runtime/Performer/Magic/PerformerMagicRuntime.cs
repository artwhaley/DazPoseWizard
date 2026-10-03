using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Owns finite Cast instances and one replaceable persistent Aura.</summary>
    internal sealed class PerformerMagicRuntime : IDisposable
    {
        private readonly Action<PerformerAura, Transform> _auraTargetLost;
        private readonly Func<PerformerMagicStyle, Transform, MagicEffectMode,
            Action<SpellCompletion>, IPerformerMagicEffectInstance> _createInstance;
        private readonly bool _validateAssets;
        private readonly List<IPerformerMagicEffectInstance> _spells = new List<IPerformerMagicEffectInstance>();
        private readonly List<IPerformerMagicEffectInstance> _retiringAuras = new List<IPerformerMagicEffectInstance>();
        private IPerformerMagicEffectInstance _aura;
        private PerformerAura _auraAsset;
        private Transform _auraTarget;
        private bool _disposed;

        public int ActiveSpellCount => _spells.Count;
        public PerformerAura CurrentAura => _auraAsset;
        public Transform AuraTarget => _auraTarget;
        public bool HasAura => _auraAsset != null;

        public PerformerMagicRuntime(Transform owner, Action<PerformerAura, Transform> auraTargetLost,
            Func<PerformerMagicStyle, Transform, MagicEffectMode, Action<SpellCompletion>,
                IPerformerMagicEffectInstance> createInstance = null, bool validateAssets = true)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            _auraTargetLost = auraTargetLost;
            _createInstance = createInstance ?? CreateVfxInstance;
            _validateAssets = validateAssets;
        }

        public void Cast(PerformerSpell spell, Transform target, Action<SpellCompletion> completion)
        {
            ThrowIfDisposed();
            if (spell == null) throw new ArgumentNullException(nameof(spell));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (_validateAssets && !spell.IsReady(out string reason)) throw new InvalidOperationException(reason);
            if (!_validateAssets && spell.Style == null) throw new InvalidOperationException("Spell has no Magic Style.");

            IPerformerMagicEffectInstance instance = _createInstance(spell.Style, target,
                MagicEffectMode.Spell, result => completion?.Invoke(result));
            _spells.Add(instance);
        }

        public void SetAura(PerformerAura aura, Transform target)
        {
            ThrowIfDisposed();
            if (aura == null) throw new ArgumentNullException(nameof(aura));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (_validateAssets && !aura.IsReady(out string reason)) throw new InvalidOperationException(reason);
            if (!_validateAssets && aura.Style == null) throw new InvalidOperationException("Aura has no Magic Style.");
            if (ReferenceEquals(_auraAsset, aura) && ReferenceEquals(_auraTarget, target)
                && _aura != null && !_aura.IsDisposed) return;

            IPerformerMagicEffectInstance replacement = _createInstance(aura.Style, target,
                MagicEffectMode.Aura, null);
            float previousFade = _aura != null ? _aura.Style.AuraFadeOutSeconds : 0f;
            RetireCurrentAura(previousFade);
            _auraAsset = aura;
            _auraTarget = target;
            _aura = replacement;
        }

        public void ClearAura()
        {
            if (_disposed) return;
            float fade = _aura != null ? _aura.Style.AuraFadeOutSeconds : 0f;
            RetireCurrentAura(fade);
            _auraAsset = null;
            _auraTarget = null;
        }

        public void Advance(float deltaTime)
        {
            if (_disposed) return;

            if (_aura != null && _auraTarget == null)
            {
                PerformerAura lostAura = _auraAsset;
                Transform lostTarget = _auraTarget;
                RetireCurrentAura(_aura.Style.AuraFadeOutSeconds);
                _auraAsset = null;
                _auraTarget = null;
                _auraTargetLost?.Invoke(lostAura, lostTarget);
            }

            for (int i = _spells.Count - 1; i >= 0; i--)
            {
                IPerformerMagicEffectInstance instance = _spells[i];
                if (instance == null || instance.IsDisposed)
                {
                    _spells.RemoveAt(i);
                    continue;
                }
                instance.Advance(deltaTime);
                if (instance.IsDisposed) _spells.RemoveAt(i);
            }

            AdvanceAura(_aura, deltaTime);
            for (int i = _retiringAuras.Count - 1; i >= 0; i--)
            {
                IPerformerMagicEffectInstance instance = _retiringAuras[i];
                AdvanceAura(instance, deltaTime);
                if (instance == null || instance.IsDisposed) _retiringAuras.RemoveAt(i);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _spells.Count - 1; i >= 0; i--) _spells[i]?.Dispose();
            _spells.Clear();
            _aura?.Dispose();
            _aura = null;
            foreach (IPerformerMagicEffectInstance instance in _retiringAuras) instance?.Dispose();
            _retiringAuras.Clear();
            _auraAsset = null;
            _auraTarget = null;
        }

        private void RetireCurrentAura(float fade)
        {
            if (_aura == null) return;
            IPerformerMagicEffectInstance old = _aura;
            _aura = null;
            if (fade <= 0f)
            {
                old.Dispose();
                return;
            }
            old.FadeOut(fade);
            if (!old.IsDisposed) _retiringAuras.Add(old);
        }

        private static void AdvanceAura(IPerformerMagicEffectInstance instance, float deltaTime)
        {
            if (instance != null && !instance.IsDisposed) instance.Advance(deltaTime);
        }

        private IPerformerMagicEffectInstance CreateVfxInstance(PerformerMagicStyle style,
            Transform target, MagicEffectMode mode, Action<SpellCompletion> completion) =>
            new MagicEffectInstance(style, target, mode, completion);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new InvalidOperationException("Performer magic runtime is not active.");
        }
    }
}
