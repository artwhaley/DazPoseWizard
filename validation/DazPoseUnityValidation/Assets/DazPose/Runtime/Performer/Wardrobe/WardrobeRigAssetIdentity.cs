using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DazPose.Performer
{
    /// <summary>Deterministic identity/path rules for editor-generated wardrobe prefabs, independently testable without Unity.</summary>
    public static class WardrobeRigAssetIdentity
    {
        public static string PrefabPath(string root, WardrobeAttachmentRole role, string stableKey)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(stableKey)) throw new ArgumentException("Rig output root and stable key are required.");
            string safe = stableKey.Replace('\\', '_').Replace('/', '_');
            if (role == WardrobeAttachmentRole.Clothing) return root.TrimEnd('/') + "/Clothing/" + safe + ".prefab";
            int slash = stableKey.IndexOf('/');
            if (slash <= 0 || slash == stableKey.Length - 1) throw new ArgumentException("Hair keys must be presetId/hairId.", nameof(stableKey));
            string preset = stableKey.Substring(0, slash).Replace('\\', '_');
            string hair = stableKey.Substring(slash + 1).Replace('\\', '_').Replace('/', '_');
            return root.TrimEnd('/') + "/Hair/" + preset + "/" + hair + ".prefab";
        }

        public static string ContentHash(string generation, string presetId, WardrobeAttachmentRole role,
            string stableKey, IEnumerable<string> orderedContent)
        {
            var text = new StringBuilder("wardrobe-rig-prefab-v1|").Append(generation).Append('|').Append(presetId).Append('|').Append(role).Append('|').Append(stableKey);
            foreach (var value in orderedContent ?? Array.Empty<string>()) text.Append('|').Append(value ?? string.Empty);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) result.Append(b.ToString("x2"));
                return result.ToString();
            }
        }

        public static bool MayReuseExisting(string existingContentHash, string plannedContentHash) =>
            !string.IsNullOrWhiteSpace(existingContentHash) && string.Equals(existingContentHash, plannedContentHash, StringComparison.Ordinal);
    }
}
