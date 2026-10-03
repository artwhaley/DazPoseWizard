using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    /// <summary>Owns one persistent GPU particle population sampled from a live skinned surface.</summary>
    [DisallowMultipleComponent]
    public sealed class PerformerParticleBody : MonoBehaviour, IDisposable
    {
        private enum BodyPhase
        {
            Follow = 0,
            Departure = 1,
            Detached = 2,
            Transit = 3,
            Reform = 4,
            Hidden = 5,
            StreamDepart = 6,
            StreamArrive = 7,
            StreamOut = 8,
            StreamIn = 9
        }

        private enum Transition
        {
            None,
            Departure,
            Transit,
            Reform
        }

        private const float DepartureSeconds = 4f;
        private const float TransitSeconds = 0.9f;
        private const float ReformSeconds = 1.1f;
        private const float DefaultTransitArcHeight = 0.45f;
        private const float DefaultCloudScale = 0.60f;
        private const float DefaultSwirlTurns = 1.25f;
        private const float DefaultTurbulenceStrength = 0.025f;

        private static readonly int TargetRendererId = Shader.PropertyToID("TargetRenderer");
        private static readonly int SurfaceBindingsId = Shader.PropertyToID("SurfaceBindings");
        private static readonly int BindingCountId = Shader.PropertyToID("BindingCount");
        private static readonly int PhaseId = Shader.PropertyToID("Phase");
        private static readonly int DissolveProgressId = Shader.PropertyToID("DissolveProgress");
        private static readonly int DepartureProgressId = Shader.PropertyToID("DepartureProgress");
        private static readonly int MaterializeProgressId = Shader.PropertyToID("MaterializeProgress");
        private static readonly int TransitProgressId = Shader.PropertyToID("TransitProgress");
        private static readonly int SourceCenterId = Shader.PropertyToID("SourceCenter");
        private static readonly int DestinationCenterId = Shader.PropertyToID("DestinationCenter");
        private static readonly int TransitArcHeightId = Shader.PropertyToID("TransitArcHeight");
        private static readonly int CloudScaleId = Shader.PropertyToID("CloudScale");
        private static readonly int SwirlTurnsId = Shader.PropertyToID("SwirlTurns");
        private static readonly int TurbulenceStrengthId = Shader.PropertyToID("TurbulenceStrength");
        private static readonly int DissolveFieldParamsId = Shader.PropertyToID("DissolveFieldParams");
        private static readonly int DissolveBoundsMinId = Shader.PropertyToID("DissolveBoundsMin");
        private static readonly int DissolveBoundsSizeId = Shader.PropertyToID("DissolveBoundsSize");
        private static readonly int WorldToLocalMatrixId = Shader.PropertyToID("WorldToLocalMatrix");
        private static readonly int CoreColorId = Shader.PropertyToID("CoreColor");
        private static readonly int GlowColorId = Shader.PropertyToID("GlowColor");
        private static readonly int CoreSizeId = Shader.PropertyToID("CoreSize");
        private static readonly int GlowSizeId = Shader.PropertyToID("GlowSize");

        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private PerformerSurfaceBindingAsset surfaceBindings;
        [SerializeField] private VisualEffectAsset visualEffectAsset;
        [SerializeField] private Color coreColor = new Color(3.2f, 3.2f, 3.2f, 0.85f);
        [SerializeField] private Color glowColor = new Color(2.8f, 0.003250774f, 2.8f, 0.5f);
        [SerializeField, Min(0.0001f)] private float coreSize = 0.003f;
        [SerializeField, Min(0.0001f)] private float glowSize = 0.009f;
        [SerializeField, HideInInspector] private SuccubusPerformer debugVisibilityOwner;

        private GraphicsBuffer _surfaceBuffer;
        private GameObject _effectObject;
        private VisualEffect _effect;
        private bool _savedUpdateWhenOffscreen;
        private bool _hasSavedRendererState;
        private bool _disposed;
        private BodyPhase _phase = BodyPhase.Follow;
        private Transition _transition;
        private float _transitionElapsed;
        private float _streamDepartureSeconds;
        private float _streamFlightBaseSeconds;
        private float _streamFadeSeconds;
        private Vector3 _sourceCenter;
        private Vector3 _destinationCenter;
        private Vector4 _dissolveFieldParams = new Vector4(FieldScale, FieldVerticalBlend, FieldSeed, FieldContrast);
        private const float FieldScale = 3.5f;
        private const float FieldVerticalBlend = 0.2f;
        private const float FieldSeed = 17f;
        private const float FieldContrast = 1.15f;

        public bool IsActive => _effect != null && _surfaceBuffer != null;
        public int BindingCount => surfaceBindings != null ? surfaceBindings.BindingCount : 0;
        public VisualEffectAsset VisualEffectAsset => visualEffectAsset;
        public PerformerSurfaceBindingAsset SurfaceBindings => surfaceBindings;
        public string Status { get; private set; } = "Particle body is stopped.";

        internal void ConfigureDebugVisibilityOwner(SuccubusPerformer owner) => debugVisibilityOwner = owner;

        public bool ValidateConfiguration(out string reason)
        {
            if (targetRenderer == null)
            {
                reason = "Assign Lara's SkinnedMeshRenderer to PerformerParticleBody.";
                return false;
            }
            if (surfaceBindings == null)
            {
                reason = "Assign a baked PerformerSurfaceBindingAsset.";
                return false;
            }
            if (!surfaceBindings.IsValidFor(targetRenderer, out reason)) return false;
            if (visualEffectAsset == null)
            {
                reason = "Assign the project-owned PerformerParticleBody VFX Graph.";
                return false;
            }
            if (Marshal.SizeOf<PerformerSurfaceBinding>() != PerformerSurfaceBinding.Stride)
            {
                reason = "PerformerSurfaceBinding must remain exactly 16 bytes for the VFX GraphicsBuffer.";
                return false;
            }

            reason = null;
            return true;
        }

        internal bool ValidateConfiguration(PerformerDissolveProfile profile, out string reason)
        {
            if (!ValidateConfiguration(out reason)) return false;
            if (profile == null)
            {
                reason = "Assign a PerformerDissolveProfile before using the particle body for DissolveTo.";
                return false;
            }
            if (targetRenderer.sharedMesh == null)
            {
                reason = "The particle body target SkinnedMeshRenderer has no mesh.";
                return false;
            }
            if (profile.ParticleBodyVfxAsset != visualEffectAsset)
            {
                reason = "PerformerParticleBody and PerformerDissolveProfile must reference the same generated VFX Graph.";
                return false;
            }
            if (profile.SurfaceBindings != surfaceBindings)
            {
                reason = "PerformerParticleBody and PerformerDissolveProfile must reference the same 32,768-entry surface binding asset.";
                return false;
            }
            if (surfaceBindings.BindingCount != PerformerSurfaceBindingAsset.RequiredBindingCount)
            {
                reason = "DissolveTo requires exactly 32,768 stable surface bindings.";
                return false;
            }

            reason = null;
            return true;
        }

        internal void BeginDissolve(PerformerDissolveProfile profile, PerformerDissolveTiming timing)
        {
            if (!ValidateConfiguration(profile, out string reason))
                throw new InvalidOperationException(reason);

            _transition = Transition.None;
            _streamDepartureSeconds = timing.DepartureDuration;
            _streamFlightBaseSeconds = timing.FlightBaseDuration;
            _streamFadeSeconds = timing.FadeDuration;
            _dissolveFieldParams = profile.DissolveFieldParams;
            coreColor = profile.CoreColor;
            glowColor = profile.GlowColor;
            coreSize = profile.CoreSize;
            glowSize = profile.GlowSize;
            EnsureCreated(BodyPhase.Departure);
            ApplyAppearance();

            _sourceCenter = targetRenderer.bounds.center;
            _destinationCenter = _sourceCenter;
            SetVector3(SourceCenterId, _sourceCenter);
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(DepartureProgressId, 0f);
            SetFloat(DissolveProgressId, 0f);
            SetFloat(MaterializeProgressId, 0f);
            SetFloat(TransitProgressId, 0f);
            SetFloat(TransitArcHeightId, profile.TransitArcHeight);
            SetFloat(CloudScaleId, profile.CloudScale);
            SetFloat(SwirlTurnsId, profile.SwirlTurns);
            SetFloat(TurbulenceStrengthId, profile.TurbulenceStrength);
            _effect.SetVector4(DissolveFieldParamsId, _dissolveFieldParams);
            SetPhase(BodyPhase.Departure);
            Status = "DissolveTo owns one live 32,768-particle body.";
        }

        internal void BeginVisibilityOut(PerformerDissolveProfile profile)
        {
            BeginOneSidedVisibility(profile, BodyPhase.StreamOut,
                "DissolveOut streams the bound surface embers upward individually.");
        }

        internal void BeginVisibilityIn(PerformerDissolveProfile profile)
        {
            BeginOneSidedVisibility(profile, BodyPhase.StreamIn,
                "DissolveIn descends particles onto Lara's current skinned surface.");
        }

        internal void SetVisibilityClock(float normalizedClock)
        {
            if (!IsActive || (_phase != BodyPhase.StreamOut && _phase != BodyPhase.StreamIn))
                throw new InvalidOperationException("The particle body is not in a one-sided visibility phase.");
            SetFloat(TransitProgressId, Mathf.Clamp01(normalizedClock));
        }

        private void BeginOneSidedVisibility(PerformerDissolveProfile profile, BodyPhase phase, string status)
        {
            if (!ValidateConfiguration(profile, out string reason))
                throw new InvalidOperationException(reason);

            _transition = Transition.None;
            _dissolveFieldParams = profile.DissolveFieldParams;
            coreColor = profile.CoreColor;
            glowColor = profile.GlowColor;
            coreSize = profile.CoreSize;
            glowSize = profile.GlowSize;
            EnsureCreated(phase);
            ApplyAppearance();

            _sourceCenter = targetRenderer.bounds.center;
            _destinationCenter = _sourceCenter;
            SetVector3(SourceCenterId, _sourceCenter);
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(DepartureProgressId, 0f);
            SetFloat(DissolveProgressId, 0f);
            SetFloat(MaterializeProgressId, 0f);
            SetFloat(TransitProgressId, 0f);
            SetFloat(TransitArcHeightId, profile.TransitArcHeight);
            SetFloat(CloudScaleId, profile.CloudScale);
            SetFloat(SwirlTurnsId, profile.SwirlTurns);
            SetFloat(TurbulenceStrengthId, profile.TurbulenceStrength);
            _effect.SetVector4(DissolveFieldParamsId, _dissolveFieldParams);
            SetPhase(phase);
            // Refresh the stable bindings and frozen per-particle source offset before
            // the selected one-sided update phase begins evaluating.
            _effect.Reinit();
            Status = status;
        }

        internal void SetDissolveProgress(float progress)
        {
            if (!IsActive) throw new InvalidOperationException("The particle body must be active before setting dissolve progress.");
            progress = Mathf.Clamp01(progress);
            if (_phase == BodyPhase.StreamDepart || _phase == BodyPhase.StreamArrive) return;
            SetFloat(DepartureProgressId, progress);
            SetFloat(DissolveProgressId, DepartureDissolveProgress(progress));
        }

        internal void CompleteDeparture()
        {
            if (!IsActive) throw new InvalidOperationException("The particle body is not active.");
            if (_effect.aliveParticleCount == 0)
                throw new InvalidOperationException("DissolveTo cannot relocate Lara: the particle body has no live particles after dissolve-out. Resolve the PerformerParticleBody VFX shader/import errors in the Console.");
            if (_phase == BodyPhase.StreamDepart || _phase == BodyPhase.StreamArrive) return;
            SetDissolveProgress(1f);
            _transition = Transition.None;
            SetPhase(BodyPhase.Detached);
            Status = "The fully dissolved body remains in the same particle population.";
        }

        internal void BeginStreaming(Vector3 destinationCenter)
        {
            if (!IsActive) throw new InvalidOperationException("The particle body must be active before streaming.");
            _destinationCenter = destinationCenter;
            SetVector3(DestinationCenterId, destinationCenter);
            // In streaming mode these existing graph inputs carry clock ratios, rather
            // than independent phase progress. No particle population is replaced.
            SetFloat(DepartureProgressId, _streamDepartureSeconds / _streamFlightBaseSeconds);
            SetFloat(MaterializeProgressId, _streamFadeSeconds / _streamFlightBaseSeconds);
            SetFloat(TransitProgressId, 0f);
            SetPhase(BodyPhase.StreamDepart);
            // Refresh the frozen source positions even when the acceptance preview already
            // had this reusable effect active. Reinit keeps the same buffer and bindings.
            _effect.Reinit();
            Status = "Surface embers are leaving individually along curved paths.";
        }

        internal void RetargetStreaming(Vector3 destinationCenter)
        {
            if (!IsActive || _phase != BodyPhase.StreamDepart)
                throw new InvalidOperationException("Streaming departure must start before destination retargeting.");
            _destinationCenter = destinationCenter;
            SetVector3(DestinationCenterId, destinationCenter);
            SetPhase(BodyPhase.StreamArrive);
            Status = "The same embers are streaming onto Lara's evaluated destination surface.";
        }

        internal void BeginTransit(Vector3 destinationCenter)
        {
            if (!IsActive || _phase != BodyPhase.Detached)
                throw new InvalidOperationException("Particle transit requires a fully detached particle body.");
            _destinationCenter = destinationCenter;
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(TransitProgressId, 0f);
            SetPhase(BodyPhase.Transit);
            Status = "The detached particle body is travelling to the evaluated destination pose.";
        }

        internal void SetTransitProgress(float progress)
        {
            if (IsActive && (_phase == BodyPhase.StreamDepart || _phase == BodyPhase.StreamArrive))
            {
                SetFloat(TransitProgressId, Mathf.Max(0f, progress));
                return;
            }
            if (!IsActive || _phase != BodyPhase.Transit)
                throw new InvalidOperationException("Particle transit has not started.");
            SetFloat(TransitProgressId, Mathf.Clamp01(progress));
        }

        internal void BeginMaterialize()
        {
            if (IsActive && _phase == BodyPhase.StreamArrive) return;
            if (!IsActive || _phase != BodyPhase.Transit)
                throw new InvalidOperationException("Particle materialization requires the transit phase.");
            SetFloat(TransitProgressId, 1f);
            SetFloat(MaterializeProgressId, 0f);
            SetPhase(BodyPhase.Reform);
            Status = "The same particles are converging onto Lara's current destination pose.";
        }

        internal void SetMaterializeProgress(float progress)
        {
            if (!IsActive || _phase != BodyPhase.Reform)
                throw new InvalidOperationException("Particle materialization has not started.");
            SetFloat(MaterializeProgressId, Mathf.Clamp01(progress));
        }

        internal void FinishDissolve()
        {
            if (IsActive)
            {
                SetFloat(DepartureProgressId, 0f);
                SetFloat(DissolveProgressId, 0f);
                SetFloat(MaterializeProgressId, 0f);
                SetFloat(TransitProgressId, 0f);
            }
            Dispose();
        }

        private void Update()
        {
            if (_transition == Transition.None || !IsActive) return;

            _transitionElapsed += Mathf.Max(0f, Time.deltaTime);
            switch (_transition)
            {
                case Transition.Departure:
                {
                    float progress = Mathf.Clamp01(_transitionElapsed / DepartureSeconds);
                    SetDepartureProgress(progress);
                    if (progress >= 1f)
                    {
                        _phase = BodyPhase.Detached;
                        _transition = Transition.None;
                        SetPhase(_phase);
                        Status = "Detached cloud; move Lara to Pose/Position B, then TRANSIT A → B.";
                    }
                    break;
                }
                case Transition.Transit:
                {
                    float progress = Mathf.Clamp01(_transitionElapsed / TransitSeconds);
                    SetFloat(TransitProgressId, progress);
                    if (progress >= 1f)
                    {
                        _sourceCenter = _destinationCenter;
                        _phase = BodyPhase.Detached;
                        _transition = Transition.None;
                        SetVector3(SourceCenterId, _sourceCenter);
                        SetPhase(_phase);
                        Status = "At destination cloud; click REFORM to attach to Lara's current pose.";
                    }
                    break;
                }
                case Transition.Reform:
                {
                    float progress = Mathf.Clamp01(_transitionElapsed / ReformSeconds);
                    SetFloat(MaterializeProgressId, progress);
                    if (progress >= 1f)
                    {
                        _phase = BodyPhase.Follow;
                        _transition = Transition.None;
                        SetPhase(_phase);
                        Status = "Following Lara's live skinned surface.";
                    }
                    break;
                }
            }
        }

        private void OnEnable() => _disposed = false;
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();

        public void Show()
        {
            RequireDebugVisibilityOwnerVisible();
            EnsureCreated();
            _transition = Transition.None;
            _phase = BodyPhase.Follow;
            _sourceCenter = targetRenderer.bounds.center;
            _destinationCenter = _sourceCenter;
            SetVector3(SourceCenterId, _sourceCenter);
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(DepartureProgressId, 0f);
            SetFloat(DissolveProgressId, 0f);
            SetFloat(MaterializeProgressId, 0f);
            SetFloat(TransitProgressId, 0f);
            SetPhase(_phase);
            if (!_effect.enabled) _effect.enabled = true;
            Status = "Showing " + surfaceBindings.BindingCount + " bindings on Lara's live skinned surface.";
        }

        public void Hide()
        {
            RequireDebugVisibilityOwnerVisible();
            EnsureCreated();
            _transition = Transition.None;
            _phase = BodyPhase.Hidden;
            SetPhase(_phase);
            Status = "Particle body hidden; GPU population and buffer remain allocated.";
        }

        public void Detach()
        {
            RequireDebugVisibilityOwnerVisible();
            EnsureCreated();
            if (_phase != BodyPhase.Follow && _phase != BodyPhase.Hidden)
                throw new InvalidOperationException("DETACH requires Lara's particle body to be following. Click RESET first if it is already detached.");

            _sourceCenter = targetRenderer.bounds.center;
            _destinationCenter = _sourceCenter;
            SetVector3(SourceCenterId, _sourceCenter);
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(DepartureProgressId, 0f);
            SetFloat(DissolveProgressId, 0f);
            SetFloat(MaterializeProgressId, 0f);
            _phase = BodyPhase.Departure;
            _transition = Transition.Departure;
            _transitionElapsed = 0f;
            SetPhase(_phase);
            Status = "Detaching particles from their surface addresses.";
        }

        public void TransitToCurrentPerformer()
        {
            RequireDebugVisibilityOwnerVisible();
            EnsureCreated();
            if (_phase != BodyPhase.Detached)
                throw new InvalidOperationException("TRANSIT A → B requires a detached cloud. Click DETACH and move Lara to B first.");

            _destinationCenter = targetRenderer.bounds.center;
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(TransitProgressId, 0f);
            _phase = BodyPhase.Transit;
            _transition = Transition.Transit;
            _transitionElapsed = 0f;
            SetPhase(_phase);
            Status = "Moving the same particle population from A to Lara's current B position.";
        }

        public void Reform()
        {
            RequireDebugVisibilityOwnerVisible();
            EnsureCreated();
            if (_phase != BodyPhase.Detached)
                throw new InvalidOperationException("REFORM requires the cloud to be detached or to finish transit first.");

            _destinationCenter = targetRenderer.bounds.center;
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(MaterializeProgressId, 0f);
            _phase = BodyPhase.Reform;
            _transition = Transition.Reform;
            _transitionElapsed = 0f;
            SetPhase(_phase);
            Status = "Converging the same bindings onto Lara's current pose.";
        }

        public void ResetBody()
        {
            RequireDebugVisibilityOwnerVisible();
            EnsureCreated();
            _transition = Transition.None;
            _phase = BodyPhase.Follow;
            _sourceCenter = targetRenderer.bounds.center;
            _destinationCenter = _sourceCenter;
            SetVector3(SourceCenterId, _sourceCenter);
            SetVector3(DestinationCenterId, _destinationCenter);
            SetFloat(DepartureProgressId, 0f);
            SetFloat(DissolveProgressId, 0f);
            SetFloat(MaterializeProgressId, 0f);
            SetFloat(TransitProgressId, 0f);
            SetPhase(_phase);
            Status = "Reset; the population follows Lara's current live surface.";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _transition = Transition.None;
            try
            {
                if (_effectObject != null)
                {
                    if (_effect != null)
                    {
                        _effect.Stop();
                        _effect.enabled = false;
                    }
                    _effect = null;
                    if (Application.isPlaying) Destroy(_effectObject);
                    else DestroyImmediate(_effectObject);
                }
            }
            finally
            {
                _effectObject = null;
                if (_surfaceBuffer != null)
                {
                    _surfaceBuffer.Dispose();
                    _surfaceBuffer = null;
                }
                if (_hasSavedRendererState && targetRenderer != null)
                    targetRenderer.updateWhenOffscreen = _savedUpdateWhenOffscreen;
                _hasSavedRendererState = false;
                Status = "Particle body disposed.";
            }
        }

        private void EnsureCreated(BodyPhase initialPhase = BodyPhase.Follow)
        {
            if (_disposed) _disposed = false;
            if (IsActive) return;
            if (!ValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);

            try
            {
                _savedUpdateWhenOffscreen = targetRenderer.updateWhenOffscreen;
                _hasSavedRendererState = true;
                targetRenderer.updateWhenOffscreen = true;

                _surfaceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    surfaceBindings.BindingCount, PerformerSurfaceBinding.Stride);
                _surfaceBuffer.SetData(surfaceBindings.CopyBindings());

                _effectObject = new GameObject("Performer Particle Body (World Space)");
                _effectObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _effectObject.transform.localScale = Vector3.one;
                _effect = _effectObject.AddComponent<VisualEffect>();
                _effect.visualEffectAsset = visualEffectAsset;
                _effect.startSeed = 1;
                _effect.resetSeedOnPlay = false;
                BindRequiredProperties();
                RefreshSharedFieldInputs(true);
                _sourceCenter = targetRenderer.bounds.center;
                _destinationCenter = _sourceCenter;
                SetVector3(SourceCenterId, _sourceCenter);
                SetVector3(DestinationCenterId, _destinationCenter);
                SetPhase(initialPhase);
                _effect.Play();
                Status = "GPU particle body created with " + surfaceBindings.BindingCount + " bindings.";
            }
            catch
            {
                DisposeAfterFailedCreate();
                throw;
            }
        }

        private void BindRequiredProperties()
        {
            Require(_effect.HasSkinnedMeshRenderer(TargetRendererId), "TargetRenderer", "SkinnedMeshRenderer");
            Require(_effect.HasGraphicsBuffer(SurfaceBindingsId), "SurfaceBindings", "GraphicsBuffer");
            Require(_effect.HasUInt(BindingCountId), "BindingCount", "uint");
            Require(_effect.HasInt(PhaseId), "Phase", "int");
            Require(_effect.HasFloat(DissolveProgressId), "DissolveProgress", "float");
            Require(_effect.HasFloat(DepartureProgressId), "DepartureProgress", "float");
            Require(_effect.HasFloat(MaterializeProgressId), "MaterializeProgress", "float");
            Require(_effect.HasFloat(TransitProgressId), "TransitProgress", "float");
            Require(_effect.HasVector3(SourceCenterId), "SourceCenter", "Vector3");
            Require(_effect.HasVector3(DestinationCenterId), "DestinationCenter", "Vector3");
            Require(_effect.HasFloat(TransitArcHeightId), "TransitArcHeight", "float");
            Require(_effect.HasFloat(CloudScaleId), "CloudScale", "float");
            Require(_effect.HasFloat(SwirlTurnsId), "SwirlTurns", "float");
            Require(_effect.HasFloat(TurbulenceStrengthId), "TurbulenceStrength", "float");
            Require(_effect.HasVector4(DissolveFieldParamsId), "DissolveFieldParams", "Vector4");
            Require(_effect.HasVector3(DissolveBoundsMinId), "DissolveBoundsMin", "Vector3");
            Require(_effect.HasVector3(DissolveBoundsSizeId), "DissolveBoundsSize", "Vector3");
            Require(_effect.HasMatrix4x4(WorldToLocalMatrixId), "WorldToLocalMatrix", "Matrix4x4");
            Require(_effect.HasVector4(CoreColorId), "CoreColor", "Color/Vector4");
            Require(_effect.HasVector4(GlowColorId), "GlowColor", "Color/Vector4");
            Require(_effect.HasFloat(CoreSizeId), "CoreSize", "float");
            Require(_effect.HasFloat(GlowSizeId), "GlowSize", "float");

            _effect.SetSkinnedMeshRenderer(TargetRendererId, targetRenderer);
            _effect.SetGraphicsBuffer(SurfaceBindingsId, _surfaceBuffer);
            _effect.SetUInt(BindingCountId, (uint)surfaceBindings.BindingCount);
            _effect.SetFloat(TransitArcHeightId, DefaultTransitArcHeight);
            _effect.SetFloat(CloudScaleId, DefaultCloudScale);
            _effect.SetFloat(SwirlTurnsId, DefaultSwirlTurns);
            _effect.SetFloat(TurbulenceStrengthId, DefaultTurbulenceStrength);
            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            _effect.SetVector4(CoreColorId, coreColor);
            _effect.SetVector4(GlowColorId, glowColor);
            _effect.SetFloat(CoreSizeId, coreSize);
            _effect.SetFloat(GlowSizeId, glowSize);
        }

        private void RefreshSharedFieldInputs(bool readMaterialParameters)
        {
            if (readMaterialParameters)
            {
                Material[] materials = targetRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].HasProperty("_DissolveFieldParams"))
                    {
                        _dissolveFieldParams = materials[i].GetVector("_DissolveFieldParams");
                        break;
                    }
                }
            }

            Bounds localBounds = targetRenderer.localBounds;
            Vector3 size = localBounds.size;
            size.x = Mathf.Max(size.x, 0.0001f);
            size.y = Mathf.Max(size.y, 0.0001f);
            size.z = Mathf.Max(size.z, 0.0001f);
            _effect.SetVector4(DissolveFieldParamsId, _dissolveFieldParams);
            _effect.SetVector3(DissolveBoundsMinId, localBounds.min);
            _effect.SetVector3(DissolveBoundsSizeId, size);
            _effect.SetMatrix4x4(WorldToLocalMatrixId, targetRenderer.transform.worldToLocalMatrix);
        }

        private void LateUpdate()
        {
            if (!IsActive) return;
            RefreshSharedFieldInputs(false);
        }

        // Match the mesh replacement to the first 40% of departure. The next 30%
        // holds a complete particle body; scattering occupies the final 30%.
        internal static float DepartureDissolveProgress(float progress)
        {
            return Mathf.Clamp01(progress / 0.4f);
        }

        private void SetDepartureProgress(float progress)
        {
            SetFloat(DepartureProgressId, progress);
            SetFloat(DissolveProgressId, DepartureDissolveProgress(progress));
        }

        private void SetPhase(BodyPhase phase)
        {
            _phase = phase;
            if (_effect != null) _effect.SetInt(PhaseId, (int)phase);
        }

        private void SetFloat(int propertyId, float value)
        {
            if (_effect != null) _effect.SetFloat(propertyId, value);
        }

        private void SetVector3(int propertyId, Vector3 value)
        {
            if (_effect != null) _effect.SetVector3(propertyId, value);
        }

        private void RequireDebugVisibilityOwnerVisible()
        {
            if (debugVisibilityOwner == null) return;
            if (debugVisibilityOwner.VisibilityState != PerformerVisibilityState.Visible
                || debugVisibilityOwner.IsDissolving)
                throw new InvalidOperationException("Particle-body acceptance controls require the performer to be in stable Visible state with no active dissolve.");
        }

        private static void Require(bool condition, string property, string type)
        {
            if (!condition)
                throw new InvalidOperationException("PerformerParticleBody VFX Graph is missing the exposed " + type + " input '" + property + "'. Regenerate the P0.G2 graph asset.");
        }

        private void DisposeAfterFailedCreate()
        {
            try
            {
                if (_effectObject != null)
                {
                    if (_effect != null)
                    {
                        _effect.Stop();
                        _effect.enabled = false;
                    }
                    if (Application.isPlaying) Destroy(_effectObject);
                    else DestroyImmediate(_effectObject);
                }
            }
            finally
            {
                _effect = null;
                _effectObject = null;
                if (_surfaceBuffer != null)
                {
                    _surfaceBuffer.Dispose();
                    _surfaceBuffer = null;
                }
                if (_hasSavedRendererState && targetRenderer != null)
                    targetRenderer.updateWhenOffscreen = _savedUpdateWhenOffscreen;
                _hasSavedRendererState = false;
            }
        }
    }
}
