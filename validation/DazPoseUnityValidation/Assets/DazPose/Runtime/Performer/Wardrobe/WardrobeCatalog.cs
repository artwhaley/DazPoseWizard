using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DazPose.Performer
{
    [CreateAssetMenu(menuName = "DAZ Pose/Wardrobe/Catalog")]
    public sealed class WardrobeCatalog : ScriptableObject
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string releasedGenerationId;
        [SerializeField] private WardrobePreset[] presets = Array.Empty<WardrobePreset>();
        public int SchemaVersion => schemaVersion;
        public string ReleasedGenerationId => releasedGenerationId;
        public WardrobePreset[] Presets => (WardrobePreset[])presets.Clone();
        public bool IsReleased => !string.IsNullOrWhiteSpace(releasedGenerationId);

#if UNITY_EDITOR
        public void ConfigureCandidate(WardrobePreset[] values)
        { releasedGenerationId = string.Empty; presets = values ?? Array.Empty<WardrobePreset>(); }
        public void SetReleasedGeneration(string generationId) { releasedGenerationId = generationId; }
#endif

        public bool TryResolve(string idOrAlias, out WardrobePreset preset, out string error)
        {
            preset = null;
            if (schemaVersion != 1) { error = "NotReleased"; return false; }
            if (!IsReleased) { error = "NotReleased"; return false; }
            string key = NormalizeAlias(idOrAlias);
            if (key.Length == 0) { error = "UnknownPreset"; return false; }
            foreach (var candidate in presets)
            {
                if (candidate == null || candidate.PresetId != key) continue;
                if (preset != null && preset != candidate) { preset = null; error = "AmbiguousAlias"; return false; }
                preset = candidate;
            }
            foreach (var candidate in presets)
            {
                if (candidate == null) continue;
                foreach (var alias in candidate.Aliases)
                    if (NormalizeAlias(alias) == key)
                    {
                        if (preset != null && preset != candidate) { preset = null; error = "AmbiguousAlias"; return false; }
                        preset = candidate;
                    }
            }
            error = preset == null ? "UnknownPreset" : null;
            return preset != null;
        }

        public bool Validate(out string error)
        {
            if (schemaVersion != 1) { error = "Unsupported catalog schema."; return false; }
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in presets)
            {
                if (item == null) { error = "Catalog contains a null preset."; return false; }
                if (!item.Validate(out error)) return false;
                if (!names.Add(NormalizeAlias(item.PresetId))) { error = "Duplicate preset ID."; return false; }
                foreach (var alias in item.Aliases)
                    if (!names.Add(NormalizeAlias(alias))) { error = "Duplicate normalized alias."; return false; }
            }
            error = null;
            return true;
        }

        public static bool IsValidId(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_') return false;
            return true;
        }

        public static string NormalizeAlias(string value)
        {
            if (value == null) return string.Empty;
            var result = new StringBuilder(value.Trim().Length);
            foreach (char c in value.Trim()) result.Append(char.ToLowerInvariant(c));
            return result.ToString();
        }
    }
}
