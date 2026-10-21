using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "Performer/Magic/Aura", fileName = "Aura")]
    public sealed class PerformerAura : ScriptableObject
    {
        [SerializeField] private PerformerMagicStyle style;

        public PerformerMagicStyle Style => style;
        public string DisplayName => style != null ? style.DisplayName : name;

        public bool IsReady(out string reason)
        {
            if (style == null)
            {
                reason = "PerformerAura '" + name + "' must reference a shared PerformerMagicStyle.";
                return false;
            }
            return style.IsReady(out reason);
        }

#if UNITY_EDITOR
        public void ConfigureInEditor(PerformerMagicStyle sharedStyle) => style = sharedStyle;
#endif
    }
}
