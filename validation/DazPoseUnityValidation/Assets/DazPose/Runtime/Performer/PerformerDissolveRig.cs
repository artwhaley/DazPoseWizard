using System;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    /// <summary>Drives Lara's project-owned dissolve shaders without changing renderer materials.</summary>
    [DisallowMultipleComponent]
    public sealed class PerformerDissolveRig : MonoBehaviour
    {
        private static readonly int DissolveEnabledId = Shader.PropertyToID("_DissolveEnabled");
        private static readonly int DissolveProgressId = Shader.PropertyToID("_DissolveProgress");
        private static readonly int DissolveBoundsMinId = Shader.PropertyToID("_DissolveBoundsMin");
        private static readonly int DissolveBoundsSizeId = Shader.PropertyToID("_DissolveBoundsSize");

        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private VisualEffect visualEffect;

        private MaterialPropertyBlock _propertyBlock;
        private bool _originalForceRenderingOff;
        private bool _hasSavedForceRenderingOff;
        private bool _prepared;
        private bool _effectTailActive;
        private float _effectTailRemaining;
        private GameObject _visualEffectRoot;
        private bool _originalVisualEffectRootActive;
        private bool _hasVisualEffectRootState;
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

        public bool IsShaderReady(PerformerDissolveProfile profile, out string reason)
        {
            if (targetRenderer == null)
            {
                reason = "The dissolve rig needs Lara's SkinnedMeshRenderer.";
                return false;
            }
            if (targetRenderer.sharedMesh == null)
            {
                reason = "Lara's SkinnedMeshRenderer has no mesh.";
                return false;
            }
            if (profile == null)
            {
                reason = "Assign a PerformerDissolveProfile before using the dissolve shader.";
                return false;
            }
            if (!profile.IsShaderReady(out reason)) return false;

            Material[] rendererMaterials = targetRenderer.sharedMaterials;
            Material[] profileMaterials = profile.LaraRuntimeMaterials;
            if (rendererMaterials.Length != profileMaterials.Length)
            {
                reason = "Lara's renderer material slots do not match the 16 project-owned dissolve materials in the profile.";
                return false;
            }
            for (int i = 0; i < rendererMaterials.Length; i++)
            {
                if (rendererMaterials[i] == profileMaterials[i]) continue;
                reason = "Lara renderer material slot " + i + " is not assigned its permanent project-owned dissolve material.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool IsReady(PerformerDissolveProfile profile, out string reason)
        {
            if (!IsShaderReady(profile, out reason)) return false;
            if (visualEffect == null)
            {
                reason = "The existing P0.G DissolveTo sequence needs its VFX Graph component. Shader acceptance itself does not.";
                return false;
            }
            if (profile.DissolveVfx == null || profile.TransitPrefab == null)
            {
                reason = "The existing P0.G DissolveTo sequence needs its VFX Graph and transit prefab. Shader acceptance itself does not.";
                return false;
            }

            reason = null;
            return true;
        }

        internal void SetDissolveEnabled(bool enabled)
        {
            if (targetRenderer == null) return;
            MaterialPropertyBlock block = GetPropertyBlock();
            block.SetFloat(DissolveEnabledId, enabled ? 1f : 0f);
            SetBounds(block);
            targetRenderer.SetPropertyBlock(block);
        }

        internal void SetDissolveProgress(float progress)
        {
            if (targetRenderer == null) return;
            MaterialPropertyBlock block = GetPropertyBlock();
            block.SetFloat(DissolveProgressId, Mathf.Clamp01(progress));
            SetBounds(block);
            targetRenderer.SetPropertyBlock(block);
        }

        internal void Begin(PerformerDissolveProfile profile)
        {
            if (_prepared) throw new InvalidOperationException("The dissolve rig is already in use.");
            CleanupEffectTail();
            if (!IsReady(profile, out string reason)) throw new InvalidOperationException(reason);

            _originalForceRenderingOff = targetRenderer.forceRenderingOff;
            _hasSavedForceRenderingOff = true;
            _visualEffectRoot = visualEffect.transform.parent != null ? visualEffect.transform.parent.gameObject : null;
            _originalVisualEffectRootActive = _visualEffectRoot != null && _visualEffectRoot.activeSelf;
            _hasVisualEffectRootState = _visualEffectRoot != null;
            _prepared = true;
            try
            {
                visualEffect.visualEffectAsset = profile.DissolveVfx;
                if (_visualEffectRoot != null && !_visualEffectRoot.activeSelf)
                    _visualEffectRoot.SetActive(true);
                SetDissolveEnabled(true);
                SetDissolveProgress(0f);
            }
            catch
            {
                Restore(immediateTransitCleanup: true);
                throw;
            }
        }

        internal float EvaluateEffectCurve(float normalizedTime) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime));

        internal void StartDissolveVfx()
        {
            if (visualEffect == null) return;
            if (_visualEffectRoot != null && !_visualEffectRoot.activeSelf)
                _visualEffectRoot.SetActive(true);
            visualEffect.Play();
        }

        internal void StopDissolveVfx()
        {
            if (visualEffect != null) visualEffect.Stop();
        }

        internal void ForceBodyHidden()
        {
            if (targetRenderer != null) targetRenderer.forceRenderingOff = true;
        }

        internal void RevealBody()
        {
            if (targetRenderer != null && _hasSavedForceRenderingOff)
                targetRenderer.forceRenderingOff = _originalForceRenderingOff;
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
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (_transitObject != null)
                UnityEngine.Object.Destroy(_transitObject, destroyDelay);
        }

        internal void Finish(float effectTailLifetime)
        {
            RestoreRendererState();
            _prepared = false;
            _effectTailRemaining = Mathf.Max(0f, effectTailLifetime);
            _effectTailActive = true;
            if (_effectTailRemaining <= 0f) CleanupEffectTail();
        }

        internal void Restore(bool immediateTransitCleanup)
        {
            try
            {
                StopDissolveVfx();
            }
            finally
            {
                try { DestroyTransit(immediateTransitCleanup); }
                finally
                {
                    RestoreRendererState();
                    _prepared = false;
                    _effectTailActive = false;
                    _effectTailRemaining = 0f;
                    RestoreVisualEffectRootState();
                }
            }
        }

        private MaterialPropertyBlock GetPropertyBlock()
        {
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_propertyBlock);
            return _propertyBlock;
        }

        private void SetBounds(MaterialPropertyBlock block)
        {
            Bounds bounds = targetRenderer.localBounds;
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, 0.0001f);
            size.y = Mathf.Max(size.y, 0.0001f);
            size.z = Mathf.Max(size.z, 0.0001f);
            block.SetVector(DissolveBoundsMinId, bounds.min);
            block.SetVector(DissolveBoundsSizeId, size);
        }

        private void RestoreRendererState()
        {
            if (targetRenderer != null)
            {
                if (_hasSavedForceRenderingOff)
                    targetRenderer.forceRenderingOff = _originalForceRenderingOff;
                _hasSavedForceRenderingOff = false;
                MaterialPropertyBlock block = GetPropertyBlock();
                block.SetFloat(DissolveEnabledId, 0f);
                block.SetFloat(DissolveProgressId, 0f);
                SetBounds(block);
                targetRenderer.SetPropertyBlock(block);
            }
        }

        private void RestoreVisualEffectRootState()
        {
            if (_hasVisualEffectRootState && _visualEffectRoot != null
                && _visualEffectRoot.activeSelf != _originalVisualEffectRootActive)
                _visualEffectRoot.SetActive(_originalVisualEffectRootActive);
            _visualEffectRoot = null;
            _hasVisualEffectRootState = false;
        }

        private void CleanupEffectTail()
        {
            if (!_effectTailActive) return;
            try { StopDissolveVfx(); }
            finally
            {
                _effectTailActive = false;
                _effectTailRemaining = 0f;
                RestoreVisualEffectRootState();
            }
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
    }
}
