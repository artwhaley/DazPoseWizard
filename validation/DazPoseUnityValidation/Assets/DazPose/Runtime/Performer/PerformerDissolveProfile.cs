using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Dissolve Profile", fileName = "Performer Dissolve Profile")]
    public sealed class PerformerDissolveProfile : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float dissolveOutDuration = 0.45f;
        [SerializeField, Min(0.01f)] private float transitDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float materializeDuration = 0.45f;
        [SerializeField, Min(0f)] private float transitArcHeight = 0.45f;
        [SerializeField, Min(0f)] private float effectTailLifetime = 0.65f;

        [Header("Effects")]
        [SerializeField] private VisualEffectAsset dissolveVfx;
        [SerializeField] private GameObject transitPrefab;
        [SerializeField] private Material[] dissolveMaterialVariants;
        [SerializeField] private AudioClip departureAudio;
        [SerializeField] private AudioClip arrivalAudio;
        [SerializeField, Range(0.1f, 3f)] private float departurePitch = 0.92f;
        [SerializeField, Range(0.1f, 3f)] private float arrivalPitch = 1.08f;

        public float DissolveOutDuration => dissolveOutDuration;
        public float TransitDuration => transitDuration;
        public float MaterializeDuration => materializeDuration;
        public float TransitArcHeight => transitArcHeight;
        public float EffectTailLifetime => effectTailLifetime;
        public VisualEffectAsset DissolveVfx => dissolveVfx;
        public GameObject TransitPrefab => transitPrefab;
        public Material[] DissolveMaterialVariants => dissolveMaterialVariants;
        public AudioClip DepartureAudio => departureAudio;
        public AudioClip ArrivalAudio => arrivalAudio;
        public float DeparturePitch => departurePitch;
        public float ArrivalPitch => arrivalPitch;

        public bool IsReady(out string reason)
        {
            if (dissolveVfx == null)
            {
                reason = "Assign the INAB Attract Sparks dissolve VFX Graph in PerformerDissolveProfile.";
                return false;
            }
            if (transitPrefab == null)
            {
                reason = "Assign the project-owned magical transit particle prefab in PerformerDissolveProfile.";
                return false;
            }
            if (dissolveMaterialVariants == null || dissolveMaterialVariants.Length == 0)
            {
                reason = "Assign the generated Lara dissolve material variants in PerformerDissolveProfile.";
                return false;
            }
            for (int i = 0; i < dissolveMaterialVariants.Length; i++)
            {
                if (dissolveMaterialVariants[i] != null) continue;
                reason = "Dissolve material variant slot " + i + " is missing.";
                return false;
            }
            if (!IsFinite(dissolveOutDuration) || dissolveOutDuration <= 0f
                || !IsFinite(transitDuration) || transitDuration <= 0f
                || !IsFinite(materializeDuration) || materializeDuration <= 0f
                || !IsFinite(transitArcHeight) || transitArcHeight < 0f
                || !IsFinite(effectTailLifetime) || effectTailLifetime < 0f)
            {
                reason = "Dissolve timings must be finite, with positive phase durations and nonnegative arc/tail values.";
                return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureIfMissing(VisualEffectAsset graph, GameObject transit,
            Material[] materialVariants, AudioClip departure, AudioClip arrival)
        {
            if (dissolveVfx == null) dissolveVfx = graph;
            if (transitPrefab == null) transitPrefab = transit;
            if (dissolveMaterialVariants == null || dissolveMaterialVariants.Length == 0)
                dissolveMaterialVariants = materialVariants;
            if (departureAudio == null) departureAudio = departure;
            if (arrivalAudio == null) arrivalAudio = arrival;
        }
#endif

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
