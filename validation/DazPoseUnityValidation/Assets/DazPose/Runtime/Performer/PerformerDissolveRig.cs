using System;
using System.Collections.Generic;
using INab.Common;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    /// <summary>Contains the one-renderer INAB dissolve integration for the current Lara performer.</summary>
    [DisallowMultipleComponent]
    public sealed class PerformerDissolveRig : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private InteractiveEffect interactiveEffect;
        [SerializeField] private VisualEffect visualEffect;
        [SerializeField] private Transform mask;

        private Material[] _originalMaterials;
        private Material[] _runtimeMaterials;
        private bool _originalForceRenderingOff;
        private bool _originalEffectActive;
        private bool _prepared;
        private bool _effectTailActive;
        private float _effectTailRemaining;
        private GameObject _transitObject;
        private ParticleSystem[] _transitParticles;

        public Vector3 BodyCenter => targetRenderer != null ? targetRenderer.bounds.center : transform.position;

        private void Update()
        {
            if (!_effectTailActive) return;
            _effectTailRemaining -= Time.deltaTime;
            if (_effectTailRemaining <= 0f) CleanupEffectTail();
        }

        private void OnDisable()
        {
            Restore(immediateTransitCleanup: false);
        }

        public bool IsReady(PerformerDissolveProfile profile, out string reason)
        {
            if (targetRenderer == null || interactiveEffect == null || visualEffect == null || mask == null)
            {
                reason = "The dissolve rig needs Lara's SkinnedMeshRenderer, INAB InteractiveEffect, VisualEffect, and mask.";
                return false;
            }
            if (targetRenderer.sharedMesh == null || !targetRenderer.sharedMesh.isReadable)
            {
                reason = "Lara's mesh must be readable for INAB skinned-mesh sampling. Enable Read/Write on Assets/TestCharacter/lara.fbx.";
                return false;
            }
            if (profile == null)
            {
                reason = "Assign a PerformerDissolveProfile before using DissolveTo.";
                return false;
            }
            if (!profile.IsReady(out reason)) return false;
            if (profile.DissolveMaterialVariants.Length != targetRenderer.sharedMaterials.Length)
            {
                reason = "The dissolve material variant count must match Lara's renderer material slot count.";
                return false;
            }
            if (interactiveEffect.visualEffect != visualEffect || interactiveEffect.meshRenderer != targetRenderer
                || interactiveEffect.mask == null || interactiveEffect.mask.transform != mask)
            {
                reason = "INAB InteractiveEffect references must point to this Lara renderer, VisualEffect, and mask.";
                return false;
            }
            reason = null;
            return true;
        }

        internal void Begin(PerformerDissolveProfile profile)
        {
            if (_prepared) throw new InvalidOperationException("The dissolve rig is already in use.");
            CleanupEffectTail();
            if (!IsReady(profile, out string reason)) throw new InvalidOperationException(reason);

            _originalMaterials = targetRenderer.sharedMaterials;
            _originalForceRenderingOff = targetRenderer.forceRenderingOff;
            _originalEffectActive = interactiveEffect.gameObject.activeSelf;
            _runtimeMaterials = new Material[profile.DissolveMaterialVariants.Length];
            _prepared = true;

            try
            {
                for (int i = 0; i < _runtimeMaterials.Length; i++)
                {
                    _runtimeMaterials[i] = new Material(profile.DissolveMaterialVariants[i])
                    {
                        name = profile.DissolveMaterialVariants[i].name + " (Dissolve Runtime)"
                    };
                    ConfigureMagicalEdge(_runtimeMaterials[i]);
                }

                targetRenderer.sharedMaterials = _runtimeMaterials;
                targetRenderer.forceRenderingOff = _originalForceRenderingOff;
                visualEffect.visualEffectAsset = profile.DissolveVfx;
                interactiveEffect.visualEffect = visualEffect;
                interactiveEffect.meshRenderer = targetRenderer;
                interactiveEffect.meshTransform = targetRenderer.transform;
                interactiveEffect.materials.Clear();
                interactiveEffect.materials.AddRange(_runtimeMaterials);
                interactiveEffect.useVFXGraphEffect = true;
                interactiveEffect.useInstancedMaterials = false;

                Bounds bounds = targetRenderer.localBounds;
                Vector3 extents = bounds.extents;
                extents.x = Mathf.Max(extents.x, 0.02f);
                extents.y = Mathf.Max(extents.y, 0.02f);
                extents.z = Mathf.Max(extents.z, 0.02f);
                mask.localPosition = bounds.center;
                interactiveEffect.usePositionTransform = false;
                interactiveEffect.useScaleTransform = true;
                interactiveEffect.useRotationTransform = false;
                interactiveEffect.initialPosition = bounds.center;
                interactiveEffect.finalPosition = bounds.center;
                interactiveEffect.initialScale = extents * 0.025f;
                interactiveEffect.finalScale = extents * 2.4f;
                interactiveEffect.ChangeMaskType(InteractiveEffectMaskType.Ellipse);

                if (!interactiveEffect.gameObject.activeSelf)
                    interactiveEffect.gameObject.SetActive(true);
                interactiveEffect.UpdateAndSetupEffect();
                interactiveEffect.UpdateMaskTransform(0f);
            }
            catch
            {
                Restore(immediateTransitCleanup: true);
                throw;
            }
        }

        internal float EvaluateEffectCurve(float normalizedTime)
        {
            AnimationCurve curve = interactiveEffect != null ? interactiveEffect.effectCurve : null;
            float value = curve != null ? curve.Evaluate(Mathf.Clamp01(normalizedTime)) : Mathf.Clamp01(normalizedTime);
            return Mathf.Clamp01(value);
        }

        internal void SetDissolveProgress(float progress)
        {
            if (interactiveEffect == null) return;
            interactiveEffect.UpdateMaskTransform(Mathf.Clamp01(progress));
        }

        internal void StartDissolveVfx()
        {
            if (interactiveEffect != null) interactiveEffect.SendPlayEvent();
        }

        internal void StopDissolveVfx()
        {
            if (interactiveEffect != null) interactiveEffect.SendStopEvent();
        }

        internal void ForceBodyHidden()
        {
            if (targetRenderer != null) targetRenderer.forceRenderingOff = true;
        }

        internal void RevealBody()
        {
            if (targetRenderer != null) targetRenderer.forceRenderingOff = _originalForceRenderingOff;
        }

        internal void StartTransit(GameObject prefab, Vector3 worldPosition)
        {
            if (prefab == null) throw new InvalidOperationException("The dissolve transit prefab is missing.");
            DestroyTransit(immediate: true);
            _transitObject = UnityEngine.Object.Instantiate(prefab, worldPosition, Quaternion.identity);
            _transitObject.name = "Performer Dissolve Transit";
            _transitParticles = _transitObject.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particles in _transitParticles) particles.Play(true);
        }

        internal void MoveTransit(Vector3 worldPosition)
        {
            if (_transitObject != null) _transitObject.transform.position = worldPosition;
        }

        internal void StopTransit(float tailLifetime)
        {
            float destroyDelay = Mathf.Max(0f, tailLifetime);
            if (_transitParticles != null)
            {
                foreach (ParticleSystem particles in _transitParticles)
                {
                    if (particles == null) continue;
                    ParticleSystem.MainModule main = particles.main;
                    destroyDelay = Mathf.Max(destroyDelay, main.startLifetime.constantMax);
                    if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (_transitObject != null)
                UnityEngine.Object.Destroy(_transitObject, destroyDelay);
        }

        internal void Finish(float effectTailLifetime)
        {
            try
            {
                RestoreRendererAndMaterials();
            }
            finally
            {
                DestroyRuntimeMaterials();
                _prepared = false;
                _effectTailRemaining = Mathf.Max(0f, effectTailLifetime);
                // Run the same stop/active-state cleanup for a zero-length tail immediately.
                _effectTailActive = true;
                if (_effectTailRemaining <= 0f) CleanupEffectTail();
            }
        }

        internal void Restore(bool immediateTransitCleanup)
        {
            if (!_prepared)
            {
                try { CleanupEffectTail(); }
                finally { DestroyTransit(immediateTransitCleanup); }
                return;
            }

            try
            {
                StopDissolveVfx();
            }
            finally
            {
                try
                {
                    DestroyTransit(immediateTransitCleanup);
                }
                finally
                {
                    try
                    {
                        RestoreRendererAndMaterials();
                    }
                    finally
                    {
                        try { RestoreEffectActiveState(); }
                        finally
                        {
                            DestroyRuntimeMaterials();
                            _prepared = false;
                            _effectTailActive = false;
                            _effectTailRemaining = 0f;
                        }
                    }
                }
            }
        }

        private void RestoreRendererAndMaterials()
        {
            if (targetRenderer != null)
            {
                if (_originalMaterials != null) targetRenderer.sharedMaterials = _originalMaterials;
                targetRenderer.forceRenderingOff = _originalForceRenderingOff;
            }
            if (interactiveEffect != null)
            {
                if (interactiveEffect.materials == null) interactiveEffect.materials = new List<Material>();
                else interactiveEffect.materials.Clear();
                if (_originalMaterials != null) interactiveEffect.materials.AddRange(_originalMaterials);
            }
            _originalMaterials = null;
        }

        private void RestoreEffectActiveState()
        {
            if (interactiveEffect != null && interactiveEffect.gameObject.activeSelf != _originalEffectActive)
                interactiveEffect.gameObject.SetActive(_originalEffectActive);
        }

        private void CleanupEffectTail()
        {
            if (!_effectTailActive) return;
            try
            {
                StopDissolveVfx();
            }
            finally
            {
                try { RestoreEffectActiveState(); }
                finally
                {
                    _effectTailActive = false;
                    _effectTailRemaining = 0f;
                }
            }
        }

        private void DestroyRuntimeMaterials()
        {
            if (_runtimeMaterials != null)
            {
                foreach (Material material in _runtimeMaterials)
                {
                    if (material == null) continue;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(material);
                    else UnityEngine.Object.DestroyImmediate(material);
                }
            }
            _runtimeMaterials = null;
        }

        private void DestroyTransit(bool immediate)
        {
            if (_transitObject == null)
            {
                _transitParticles = null;
                return;
            }
            if (immediate && !Application.isPlaying) UnityEngine.Object.DestroyImmediate(_transitObject);
            else UnityEngine.Object.Destroy(_transitObject);
            _transitObject = null;
            _transitParticles = null;
        }

        private static void ConfigureMagicalEdge(Material material)
        {
            SetColor(material, "_EdgeColor", new Color(2.2f, 0.08f, 3.0f, 1f));
            SetColor(material, "_EmberColor", new Color(2.0f, 0.12f, 3.2f, 1f));
            SetColor(material, "_BurnColor", new Color(0.18f, 0.025f, 0.36f, 1f));
            SetFloat(material, "_EdgeWidth", 0.055f);
            SetFloat(material, "_EmberWidth", 0.075f);
            SetFloat(material, "_BurnHardness", 0.75f);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material != null && material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
