using System;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Dissolve Profile", fileName = "Performer Dissolve Profile")]
    public sealed class PerformerDissolveProfile : ScriptableObject
    {
        private const int MaterialSlotCount = 16;
        private const int EyeMoistureSlot = 10;
        private const int CorneaSlot = 14;

        private static readonly string[] RequiredDissolveProperties =
        {
            "_DissolveEnabled",
            "_DissolveProgress",
            "_DissolveBoundsMin",
            "_DissolveBoundsSize",
            "_DissolveFieldParams",
            "_DissolveEdgeWidth",
            "_DissolveEdgeColor",
            "_DissolveEdgeEmission"
        };

        [Header("Timing")]
        [Tooltip("Seconds for the source surface to dissolve and release its embers. Also sets the stagger between the first and last destination patches.")]
        [SerializeField, Min(0.01f)] private float dissolveOutDuration = 1f;
        [Tooltip("Additional travel time between the end of source dissolution and the first destination arrivals. Total streaming duration is twice Dissolve Out Duration plus Transit Duration plus Materialize Duration.")]
        [SerializeField, Min(0.01f)] private float transitDuration = 2f / 3f;
        [Tooltip("Local overlap in seconds between each arriving particle patch and the native mesh beneath it.")]
        [SerializeField, Min(0.01f)] private float materializeDuration = 1f / 3f;
        [SerializeField, Min(0f)] private float transitArcHeight = 0.45f;

        [Header("Native Dissolve Shaders")]
        [SerializeField] private Shader sssDissolveShader;
        [SerializeField] private Shader wetDissolveShader;
        [SerializeField] private Material[] laraRuntimeMaterials;
        [Tooltip("Optional explicit shader per material slot for converted bodies. Empty retains the original 16-slot Lara contract.")]
        [SerializeField] private Shader[] convertedSlotShaders;
        [SerializeField] private Vector4 dissolveFieldParams = new Vector4(3.5f, 0.85f, 17f, 1.15f);
        [SerializeField, Min(0.0001f)] private float dissolveEdgeWidth = 0.035f;
        [SerializeField] private Color dissolveEdgeColor = new Color(3f, 0.06f, 4f, 1f);
        [SerializeField, Min(0f)] private float dissolveEdgeEmission = 4f;

        [Header("Reusable Particle Body")]
        [SerializeField] private VisualEffectAsset particleBodyVfxAsset;
        [SerializeField] private PerformerSurfaceBindingAsset surfaceBindings;
        [SerializeField] private Color coreColor = new Color(3.2f, 3.2f, 3.2f, 0.85f);
        [SerializeField] private Color glowColor = new Color(2.8f, 0.003250774f, 2.8f, 0.5f);
        [SerializeField, Min(0.0001f)] private float coreSize = 0.003f;
        [SerializeField, Min(0.0001f)] private float glowSize = 0.009f;
        [SerializeField, Min(0.0001f)] private float cloudScale = 1.4f;
        [SerializeField, Min(0f)] private float swirlTurns = 3f;
        [SerializeField, Min(0f)] private float turbulenceStrength = 0.22f;

        [Header("Audio")]
        [SerializeField] private AudioClip departureAudio;
        [SerializeField] private AudioClip arrivalAudio;
        [SerializeField, Range(0.1f, 3f)] private float departurePitch = 0.92f;
        [SerializeField, Range(0.1f, 3f)] private float arrivalPitch = 1.08f;
        [SerializeField, Min(0f)] private float visibilityAudioStartOffsetSeconds = 0.85f;

        public float DefaultDuration => 2f * dissolveOutDuration + transitDuration + materializeDuration;
        public float DissolveOutDuration => dissolveOutDuration;
        public float TransitDuration => transitDuration;
        public float MaterializeDuration => materializeDuration;
        public float TransitArcHeight => transitArcHeight;
        public Shader SssDissolveShader => sssDissolveShader;
        public Shader WetDissolveShader => wetDissolveShader;
        public Material[] LaraRuntimeMaterials => laraRuntimeMaterials;
        public Vector4 DissolveFieldParams => dissolveFieldParams;
        public float DissolveEdgeWidth => dissolveEdgeWidth;
        public Color DissolveEdgeColor => dissolveEdgeColor;
        public float DissolveEdgeEmission => dissolveEdgeEmission;
        public VisualEffectAsset ParticleBodyVfxAsset => particleBodyVfxAsset;
        public PerformerSurfaceBindingAsset SurfaceBindings => surfaceBindings;
        public Color CoreColor => coreColor;
        public Color GlowColor => glowColor;
        public float CoreSize => coreSize;
        public float GlowSize => glowSize;
        public float CloudScale => cloudScale;
        public float SwirlTurns => swirlTurns;
        public float TurbulenceStrength => turbulenceStrength;
        public AudioClip DepartureAudio => departureAudio;
        public AudioClip ArrivalAudio => arrivalAudio;
        public float DeparturePitch => departurePitch;
        public float ArrivalPitch => arrivalPitch;
        public float VisibilityAudioStartOffsetSeconds => visibilityAudioStartOffsetSeconds;

        /// <summary>Rebind only a per-performer runtime clone; authored profiles stay immutable.</summary>
        internal void SetRuntimeSurfaceBindings(PerformerSurfaceBindingAsset bindings)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Wardrobe effect profiles may be rebound only on the runtime instance.");
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            surfaceBindings = bindings;
        }

        public bool IsShaderReady(out string reason)
        {
            if (sssDissolveShader == null || wetDissolveShader == null)
            {
                reason = "Assign both project-owned dissolve Shader Graph assets to the profile.";
                return false;
            }
            if (sssDissolveShader == wetDissolveShader)
            {
                reason = "The SSS and Wet dissolve shader references must be distinct.";
                return false;
            }
            bool converted = convertedSlotShaders != null && convertedSlotShaders.Length > 0;
            if (laraRuntimeMaterials == null || (converted ? laraRuntimeMaterials.Length != convertedSlotShaders.Length : laraRuntimeMaterials.Length != MaterialSlotCount))
            {
                reason = "The profile material count must match its explicit converted slots, or the original 16-slot Lara contract.";
                return false;
            }

            for (int i = 0; i < laraRuntimeMaterials.Length; i++)
            {
                Material material = laraRuntimeMaterials[i];
                if (material == null)
                {
                    reason = "Lara runtime material slot " + i + " is missing.";
                    return false;
                }

                Shader expectedShader = converted ? convertedSlotShaders[i] : IsWetSlot(i) ? wetDissolveShader : sssDissolveShader;
                if (expectedShader == null)
                {
                    reason = "Converted material slot " + i + " has no expected dissolve shader.";
                    return false;
                }
                if (material.shader != expectedShader)
                {
                    reason = "Lara runtime material slot " + i + " must use "
                        + expectedShader.name + ".";
                    return false;
                }
                for (int propertyIndex = 0; propertyIndex < RequiredDissolveProperties.Length; propertyIndex++)
                {
                    string property = RequiredDissolveProperties[propertyIndex];
                    if (material.HasProperty(property)) continue;
                    reason = "Lara runtime material slot " + i + " is missing shader property " + property + ".";
                    return false;
                }
            }

            if (!IsFinite(dissolveFieldParams) || !IsFinite(dissolveEdgeWidth) || dissolveEdgeWidth <= 0f
                || !IsFinite(dissolveEdgeColor) || !IsFinite(dissolveEdgeEmission) || dissolveEdgeEmission < 0f)
            {
                reason = "Dissolve field and edge parameters must contain finite values with positive width and nonnegative emission.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool IsReady(out string reason)
        {
            if (!IsShaderReady(out reason)) return false;
            if (particleBodyVfxAsset == null)
            {
                reason = "Assign the project-owned PerformerParticleBody VFX Graph in PerformerDissolveProfile. Run the Particle Body Acceptance Harness installer.";
                return false;
            }
            if (surfaceBindings == null || surfaceBindings.BindingCount != PerformerSurfaceBindingAsset.RequiredBindingCount)
            {
                reason = "Assign the current 32,768-entry PerformerSurfaceBindingAsset in PerformerDissolveProfile.";
                return false;
            }
            if (!IsFinite(dissolveOutDuration) || dissolveOutDuration <= 0f
                || !IsFinite(transitDuration) || transitDuration <= 0f
                || !IsFinite(materializeDuration) || materializeDuration <= 0f
                || !IsFinite(transitArcHeight) || transitArcHeight < 0f
                || !IsFinite(coreColor) || !IsFinite(glowColor)
                || !IsFinite(coreSize) || coreSize <= 0f || !IsFinite(glowSize) || glowSize <= 0f
                || !IsFinite(cloudScale) || cloudScale <= 0f
                || !IsFinite(swirlTurns) || swirlTurns < 0f
                || !IsFinite(turbulenceStrength) || turbulenceStrength < 0f
                || !IsFinite(visibilityAudioStartOffsetSeconds) || visibilityAudioStartOffsetSeconds < 0f
                || !IsFinite(departurePitch) || departurePitch <= 0f
                || !IsFinite(arrivalPitch) || arrivalPitch <= 0f)
            {
                reason = "Dissolve timing and particle parameters must be finite, with positive durations/sizes and nonnegative arc/swirl/turbulence values.";
                return false;
            }

            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureNativeMaterials(Shader sssShader, Shader wetShader, Material[] runtimeMaterials)
        {
            sssDissolveShader = sssShader;
            wetDissolveShader = wetShader;
            laraRuntimeMaterials = runtimeMaterials;
            convertedSlotShaders = null;
        }

        public void ConfigureConvertedMaterials(Shader sssShader, Shader wetShader, Material[] runtimeMaterials, Shader[] expectedShaders)
        {
            sssDissolveShader = sssShader;
            wetDissolveShader = wetShader;
            laraRuntimeMaterials = runtimeMaterials;
            convertedSlotShaders = expectedShaders;
        }

        public void ConfigureParticleBodyAssets(VisualEffectAsset graph, PerformerSurfaceBindingAsset bindings)
        {
            particleBodyVfxAsset = graph;
            surfaceBindings = bindings;
        }
#endif

        private static bool IsWetSlot(int index) => index == EyeMoistureSlot || index == CorneaSlot;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector4 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        private static bool IsFinite(Color value) => IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
    }
}
