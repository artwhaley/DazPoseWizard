using System;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Drives Lara's permanent dissolve shaders and the one reusable skinned particle body.</summary>
    [DisallowMultipleComponent]
    public sealed class PerformerDissolveRig : MonoBehaviour
    {
        private const float MeshRevealStartProgress = 0.75f;
        private bool _streaming;
        private bool _oneSidedVisibility;
        private bool _visibilityRendererRevealed;
        private float _streamDepartureSeconds;
        private float _streamFadeSeconds;

        private static readonly int DissolveEnabledId = Shader.PropertyToID("_DissolveEnabled");
        private static readonly int DissolveProgressId = Shader.PropertyToID("_DissolveProgress");
        private static readonly int DissolveBoundsMinId = Shader.PropertyToID("_DissolveBoundsMin");
        private static readonly int DissolveBoundsSizeId = Shader.PropertyToID("_DissolveBoundsSize");
        private static readonly int DissolveFieldParamsId = Shader.PropertyToID("_DissolveFieldParams");
        private static readonly int DissolveEdgeWidthId = Shader.PropertyToID("_DissolveEdgeWidth");
        private static readonly int DissolveEdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
        private static readonly int DissolveEdgeEmissionId = Shader.PropertyToID("_DissolveEdgeEmission");

        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private PerformerParticleBody particleBody;

        private MaterialPropertyBlock _propertyBlock;
        private bool _prepared;

        public Vector3 BodyCenter => targetRenderer != null ? targetRenderer.bounds.center : transform.position;
        public PerformerParticleBody ParticleBody => particleBody;
        internal SkinnedMeshRenderer TargetRenderer => targetRenderer;

        internal bool CanConfigureWardrobe(PerformerDissolveProfile profile, out string reason)
        {
            if (_prepared) { reason = "The dissolve rig is owned by an active effect."; return false; }
            if (!IsShaderReady(profile, out reason)) return false;
            if (particleBody == null) { reason = "Particle body component is missing."; return false; }
            if (particleBody.TargetRenderer != targetRenderer)
            { reason = "Dissolve rig and particle body must reference the same performer renderer."; return false; }
            if (profile.SurfaceBindings == null || !profile.SurfaceBindings.IsValidFor(targetRenderer, out reason)) return false;
            if (profile.ParticleBodyVfxAsset != particleBody.VisualEffectAsset)
            { reason = "Wardrobe profile and particle body use different VFX graphs."; return false; }
            reason = null;
            return true;
        }

        internal void ConfigureWardrobeRuntime(PerformerDissolveProfile profile)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Dissolve wardrobe bindings are runtime instance state.");
            if (!CanConfigureWardrobe(profile, out string reason)) throw new InvalidOperationException(reason);
            particleBody.ConfigureWardrobeRuntime(profile);
            if (!particleBody.ValidateConfiguration(profile, out reason)) throw new InvalidOperationException(reason);
        }

        private void OnDisable()
        {
            // Component cleanup releases effect resources but preserves the renderer's
            // current visibility decision. The performer facade owns stable visibility.
            RestorePreservingVisibility();
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
                reason = "Lara's renderer material slots do not match the project-owned dissolve materials in the profile.";
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
            if (!profile.IsReady(out reason)) return false;
            if (particleBody == null)
            {
                reason = "Assign the generated PerformerParticleBody component to the dissolve rig. Run the Particle Body Acceptance Harness installer.";
                return false;
            }
            if (!particleBody.ValidateConfiguration(profile, out reason)) return false;

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
            float value = Mathf.Clamp01(progress);
            // Streaming releases embers as the matching source surface dissolves.
            SetTargetDissolveProgress(_prepared && !_streaming ? PerformerParticleBody.DepartureDissolveProgress(value) : value);
            if (_prepared) particleBody.SetDissolveProgress(value);
        }

        private void SetTargetDissolveProgress(float progress)
        {
            if (targetRenderer == null) return;
            float value = Mathf.Clamp01(progress);
            MaterialPropertyBlock block = GetPropertyBlock();
            block.SetFloat(DissolveProgressId, value);
            SetBounds(block);
            targetRenderer.SetPropertyBlock(block);
        }

        internal void Begin(PerformerDissolveProfile profile, PerformerDissolveTiming timing)
        {
            if (_prepared) throw new InvalidOperationException("The dissolve rig is already in use.");
            if (!IsReady(profile, out string reason)) throw new InvalidOperationException(reason);

            _prepared = true;
            _streaming = false;
            _oneSidedVisibility = false;
            _visibilityRendererRevealed = false;
            _streamDepartureSeconds = timing.DepartureDuration;
            _streamFadeSeconds = timing.FadeDuration;
            try
            {
                particleBody.BeginDissolve(profile, timing);
                InitializeDissolveProperties(profile, 0f);
                targetRenderer.forceRenderingOff = false;
            }
            catch
            {
                Restore(false);
                throw;
            }
        }

        internal void BeginVisibilityOut(PerformerDissolveProfile profile)
        {
            BeginOneSidedVisibility(profile, hiddenAtStart: false, phaseOut: true);
        }

        internal void BeginVisibilityIn(PerformerDissolveProfile profile)
        {
            BeginOneSidedVisibility(profile, hiddenAtStart: true, phaseOut: false);
        }

        internal void SetVisibilityOutProgress(float normalizedClock)
        {
            RequireOneSidedPrepared();
            float clock = Mathf.Clamp01(normalizedClock);
            SetTargetDissolveProgress(Mathf.Clamp01(clock / 0.62f));
            particleBody.SetVisibilityClock(clock);
        }

        internal void SetVisibilityInProgress(float normalizedClock)
        {
            RequireOneSidedPrepared();
            float clock = Mathf.Clamp01(normalizedClock);
            if (!_visibilityRendererRevealed && clock >= 0.17f)
            {
                // Reveal the renderer while the shader still clips the complete mesh.
                targetRenderer.forceRenderingOff = false;
                _visibilityRendererRevealed = true;
            }
            float reveal = Mathf.Clamp01((clock - 0.17f) / 0.66f);
            SetTargetDissolveProgress(1f - reveal);
            particleBody.SetVisibilityClock(clock);
        }

        private void BeginOneSidedVisibility(PerformerDissolveProfile profile, bool hiddenAtStart, bool phaseOut)
        {
            if (_prepared) throw new InvalidOperationException("The dissolve rig is already in use.");
            if (!IsReady(profile, out string reason)) throw new InvalidOperationException(reason);

            _prepared = true;
            _streaming = false;
            _oneSidedVisibility = true;
            _visibilityRendererRevealed = false;
            try
            {
                // Keep Lara hidden until the phase-9 mesh reveal actually begins.
                targetRenderer.forceRenderingOff = hiddenAtStart;
                if (phaseOut) particleBody.BeginVisibilityOut(profile);
                else particleBody.BeginVisibilityIn(profile);
                InitializeDissolveProperties(profile, phaseOut ? 0f : 1f);
            }
            catch
            {
                Restore(hiddenAtStart);
                throw;
            }
        }

        private void InitializeDissolveProperties(PerformerDissolveProfile profile, float progress)
        {
            MaterialPropertyBlock block = GetPropertyBlock();
            block.SetVector(DissolveFieldParamsId, profile.DissolveFieldParams);
            block.SetFloat(DissolveEdgeWidthId, profile.DissolveEdgeWidth);
            block.SetColor(DissolveEdgeColorId, profile.DissolveEdgeColor);
            block.SetFloat(DissolveEdgeEmissionId, profile.DissolveEdgeEmission);
            block.SetFloat(DissolveEnabledId, 1f);
            block.SetFloat(DissolveProgressId, progress);
            SetBounds(block);
            targetRenderer.SetPropertyBlock(block);
        }

        internal float EvaluateEffectCurve(float normalizedTime) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime));

        internal void CompleteDeparture()
        {
            RequirePrepared();
            particleBody.CompleteDeparture();
        }

        internal void BeginTransit(Vector3 destinationCenter)
        {
            RequirePrepared();
            particleBody.BeginStreaming(destinationCenter);
            _streaming = true;
        }

        internal void RetargetTransit(Vector3 destinationCenter)
        {
            RequirePrepared();
            particleBody.RetargetStreaming(destinationCenter);
        }

        internal void SetTransitProgress(float progress)
        {
            RequirePrepared();
            particleBody.SetTransitProgress(progress);
        }

        internal void BeginMaterialize()
        {
            RequirePrepared();
            particleBody.BeginMaterialize();
        }

        internal void SetMaterializeProgress(float progress)
        {
            RequirePrepared();
            float value = Mathf.Clamp01(progress);
            if (_streaming)
            {
                // A destination patch appears only after its own embers have reached the
                // surface. The particle glow then fades over the local overlap interval.
                float arrivalElapsed = value * (_streamDepartureSeconds + _streamFadeSeconds);
                float reveal = Mathf.Clamp01((arrivalElapsed - _streamFadeSeconds * 0.2f) / _streamDepartureSeconds);
                SetTargetDissolveProgress(1f - reveal);
                return;
            }
            // The particle body completes its surface convergence by 60%; keep Lara fully
            // dissolved until 75%, then reveal her through the assembled particle cloud.
            particleBody.SetMaterializeProgress(value);
            float meshRevealProgress = Mathf.InverseLerp(MeshRevealStartProgress, 1f, value);
            SetTargetDissolveProgress(1f - meshRevealProgress);
        }

        internal void Finish(bool hidden)
        {
            if (hidden && targetRenderer != null) targetRenderer.forceRenderingOff = true;
            if (targetRenderer != null)
            {
                MaterialPropertyBlock block = GetPropertyBlock();
                block.SetFloat(DissolveEnabledId, 0f);
                block.SetFloat(DissolveProgressId, 0f);
                SetBounds(block);
                targetRenderer.SetPropertyBlock(block);
            }
            try
            {
                if (particleBody != null) particleBody.FinishDissolve(hidden);
            }
            finally
            {
                _prepared = false;
                _streaming = false;
                _oneSidedVisibility = false;
                _visibilityRendererRevealed = false;
                if (!hidden && targetRenderer != null) targetRenderer.forceRenderingOff = false;
            }
        }

        internal void Restore(bool hidden)
        {
            // Hide first so shader reset cannot flash an outgoing/incomplete mesh.
            if (hidden && targetRenderer != null) targetRenderer.forceRenderingOff = true;
            try
            {
                if (targetRenderer != null)
                {
                    MaterialPropertyBlock block = GetPropertyBlock();
                    block.SetFloat(DissolveEnabledId, 0f);
                    block.SetFloat(DissolveProgressId, 0f);
                    SetBounds(block);
                    targetRenderer.SetPropertyBlock(block);
                }
            }
            finally
            {
                try
                {
                    if (particleBody != null) particleBody.FinishDissolve(hidden);
                }
                finally
                {
                    _prepared = false;
                    _streaming = false;
                    _oneSidedVisibility = false;
                    _visibilityRendererRevealed = false;
                    if (targetRenderer != null) targetRenderer.forceRenderingOff = hidden;
                }
            }
        }

        internal void ApplyStableVisibility(bool hidden)
        {
            Restore(hidden);
        }

        private void RestorePreservingVisibility()
        {
            if (targetRenderer == null)
            {
                Restore(false);
                return;
            }
            Restore(targetRenderer.forceRenderingOff);
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

        private void RequirePrepared()
        {
            if (!_prepared || particleBody == null)
                throw new InvalidOperationException("The dissolve rig has not begun a particle-backed dissolve.");
        }

        private void RequireOneSidedPrepared()
        {
            if (!_prepared || !_oneSidedVisibility || particleBody == null)
                throw new InvalidOperationException("A one-sided visibility dissolve has not begun.");
        }
    }
}
