using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Magic/Style", fileName = "Magic Style")]
    public sealed class PerformerMagicStyle : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName;
        [SerializeField] private VisualEffectAsset visualEffectAsset;
        [SerializeField, Min(0)] private int styleId;

        [Header("HDR Palette")]
        [SerializeField, ColorUsage(true, true)] private Color primaryColor = Color.white;
        [SerializeField, ColorUsage(true, true)] private Color secondaryColor = Color.white;
        [SerializeField, ColorUsage(true, true)] private Color accentColor = Color.white;
        [SerializeField, ColorUsage(true, true)] private Color smokeColor = new Color(0.08f, 0.06f, 0.09f, 0.4f);

        [Header("Target Geometry")]
        [SerializeField, Min(0.01f)] private float targetScale = 1f;
        [SerializeField, Min(0f)] private float targetPadding;

        [Header("Spell Event")]
        [SerializeField, Min(0.1f)] private float spellDurationSeconds = 3f;
        [SerializeField, Range(1, 2048)] private int spellBurstCount = 500;
        [SerializeField, Min(0.1f)] private float spellParticleLifetimeSeconds = 3.15f;

        [Header("Persistent Aura State")]
        [SerializeField, Min(0f)] private float auraFadeInSeconds = 0.3f;
        [SerializeField, Min(0f)] private float auraFadeOutSeconds = 0.3f;
        [SerializeField, Range(0f, 2048f)] private float auraSpawnRate = 160f;
        [SerializeField, Min(0.1f)] private float auraParticleLifetimeSeconds = 2f;

        [Header("Shared Motion and Appearance")]
        [SerializeField, Range(0f, 4f)] private float intensity = 1f;
        [SerializeField, Min(0.001f)] private float particleSize = 0.035f;
        [SerializeField, Min(0f)] private float riseSpeed = 0.4f;
        [SerializeField, Min(0f)] private float swirlStrength = 1f;
        [SerializeField, Min(0f)] private float turbulence = 0.2f;
        [SerializeField, Min(0f)] private float pulseFrequency = 1f;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public VisualEffectAsset EffectGraph => visualEffectAsset;
        public int StyleId => styleId;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Color AccentColor => accentColor;
        public Color SmokeColor => smokeColor;
        public float TargetScale => targetScale;
        public float TargetPadding => targetPadding;
        public float SpellDurationSeconds => spellDurationSeconds;
        public int SpellBurstCount => spellBurstCount;
        public float SpellParticleLifetimeSeconds => spellParticleLifetimeSeconds;
        public float AuraFadeInSeconds => auraFadeInSeconds;
        public float AuraFadeOutSeconds => auraFadeOutSeconds;
        public float AuraSpawnRate => auraSpawnRate;
        public float AuraParticleLifetimeSeconds => auraParticleLifetimeSeconds;
        public float Intensity => intensity;
        public float ParticleSize => particleSize;
        public float RiseSpeed => riseSpeed;
        public float SwirlStrength => swirlStrength;
        public float Turbulence => turbulence;
        public float PulseFrequency => pulseFrequency;

        public bool IsReady(out string reason)
        {
            if (visualEffectAsset == null)
            {
                reason = "Magic Style '" + name + "' needs a VFX Graph asset.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                reason = "Magic Style '" + name + "' needs a display name.";
                return false;
            }
            if (targetScale <= 0f || targetPadding < 0f || spellDurationSeconds <= 0f
                || spellBurstCount < 1 || spellParticleLifetimeSeconds <= 0f
                || auraFadeInSeconds < 0f || auraFadeOutSeconds < 0f
                || auraSpawnRate <= 0f || auraParticleLifetimeSeconds <= 0f
                || intensity < 0f || particleSize <= 0f)
            {
                reason = "Magic Style '" + name + "' has invalid size, lifetime, rate, or fade settings.";
                return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(string label, VisualEffectAsset graph, int familyId,
            Color primary, Color secondary, Color accent, Color smoke,
            float geometryScale, float padding, float spellDuration, int burstCount,
            float spellParticleLifetime, float auraFadeIn, float auraFadeOut,
            float auraRate, float auraParticleLifetime, float effectIntensity,
            float size, float rise, float swirl, float turbulenceStrength, float pulse)
        {
            displayName = label;
            visualEffectAsset = graph;
            styleId = familyId;
            primaryColor = primary;
            secondaryColor = secondary;
            accentColor = accent;
            smokeColor = smoke;
            targetScale = geometryScale;
            targetPadding = padding;
            spellDurationSeconds = spellDuration;
            spellBurstCount = burstCount;
            spellParticleLifetimeSeconds = spellParticleLifetime;
            auraFadeInSeconds = auraFadeIn;
            auraFadeOutSeconds = auraFadeOut;
            auraSpawnRate = auraRate;
            auraParticleLifetimeSeconds = auraParticleLifetime;
            intensity = effectIntensity;
            particleSize = size;
            riseSpeed = rise;
            swirlStrength = swirl;
            turbulence = turbulenceStrength;
            pulseFrequency = pulse;
        }
#endif
    }
}
