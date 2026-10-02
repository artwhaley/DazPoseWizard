using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Teleport Profile", fileName = "Performer Teleport Profile")]
    public sealed class PerformerTeleportProfile : ScriptableObject
    {
        [SerializeField] private GameObject effectPrefab;
        [SerializeField] private AudioClip departureAudio;
        [SerializeField] private AudioClip arrivalAudio;
        [SerializeField, Min(0f)] private float hideTime = 0.15f;
        [SerializeField, Min(0f)] private float revealDelay = 0.25f;
        [SerializeField, Min(0f)] private float completionTime = 0.45f;
        [SerializeField, Min(0.01f)] private float effectLifetime = 1.4f;
        [SerializeField, Range(0.1f, 3f)] private float departurePitch = 0.94f;
        [SerializeField, Range(0.1f, 3f)] private float arrivalPitch = 1.04f;

        public GameObject EffectPrefab => effectPrefab;
        public AudioClip DepartureAudio => departureAudio;
        public AudioClip ArrivalAudio => arrivalAudio;
        public float HideTime => hideTime;
        public float RevealDelay => revealDelay;
        public float CompletionTime => completionTime;
        public float EffectLifetime => effectLifetime;
        public float DeparturePitch => departurePitch;
        public float ArrivalPitch => arrivalPitch;

        public bool IsReady(out string reason)
        {
            if (effectPrefab == null)
            {
                reason = "Assign the HDRP magical teleport effect prefab in PerformerTeleportProfile.";
                return false;
            }
            if (hideTime < 0f || revealDelay < hideTime || completionTime < revealDelay || effectLifetime <= 0f)
            {
                reason = "Teleport timings must satisfy 0 <= Hide Time <= Reveal Delay <= Completion Time and Effect Lifetime > 0.";
                return false;
            }
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureIfMissing(GameObject effect, AudioClip departure, AudioClip arrival)
        {
            if (effectPrefab == null) effectPrefab = effect;
            if (departureAudio == null) departureAudio = departure;
            if (arrivalAudio == null) arrivalAudio = arrival;
        }
#endif
    }
}
