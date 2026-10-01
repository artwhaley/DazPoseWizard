using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.AnimationAudit
{
    public enum AnimationAuditPack
    {
        Kawaii,
        FemaleMovementAnimsetPro
    }

    [Serializable]
    public sealed class AnimationAuditEntry
    {
        public AnimationAuditPack pack;
        public string category;
        public string displayName;
        public string assetPath;
        public string clipName;
        public AnimationClip clip;
        public string animatorStateName;
        public float durationSeconds;
        public float frameRate;
        public bool sourceHumanoid;
        public bool loopTime;
        public bool hasRootMotionCurves;
        public string rootMotionNode;
        public string sourceAvatarName;
        public string importWarnings;
        public string importErrors;
        public string retargetStatus;

        public string IdentityName => string.IsNullOrEmpty(displayName) ? clipName : displayName;
    }

    [CreateAssetMenu(menuName = "DAZ Pose/Animation Audit Catalog", fileName = "AnimationAuditCatalog")]
    public sealed class AnimationAuditCatalog : ScriptableObject
    {
        public List<AnimationAuditEntry> entries = new List<AnimationAuditEntry>();

        public IEnumerable<string> GetCategories(AnimationAuditPack pack)
        {
            return entries
                .Where(entry => entry != null && entry.pack == pack)
                .Select(entry => string.IsNullOrEmpty(entry.category) ? "Other" : entry.category)
                .Distinct()
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<AnimationAuditEntry> Query(AnimationAuditPack pack, string category, string search)
        {
            IEnumerable<AnimationAuditEntry> result = entries.Where(entry => entry != null && entry.clip != null && entry.pack == pack);
            if (!string.IsNullOrEmpty(category) && category != "All")
                result = result.Where(entry => string.Equals(entry.category, category, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(search))
            {
                string needle = search.Trim();
                result = result.Where(entry =>
                    Contains(entry.IdentityName, needle) ||
                    Contains(entry.clipName, needle) ||
                    Contains(entry.assetPath, needle));
            }
            return result.OrderBy(entry => entry.IdentityName, StringComparer.OrdinalIgnoreCase);
        }

        public AnimationAuditEntry FindByStem(AnimationAuditPack pack, string stem)
        {
            if (string.IsNullOrWhiteSpace(stem)) return null;
            string normalized = Normalize(stem);
            return entries.FirstOrDefault(entry => entry != null && entry.clip != null && entry.pack == pack &&
                (Normalize(entry.IdentityName) == normalized || Normalize(System.IO.Path.GetFileNameWithoutExtension(entry.assetPath)) == normalized));
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
            return new string(chars);
        }

        private static bool Contains(string value, string needle)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
