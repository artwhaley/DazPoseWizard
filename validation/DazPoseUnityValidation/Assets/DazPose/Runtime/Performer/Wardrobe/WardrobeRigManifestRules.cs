using System;
using System.Collections.Generic;

namespace DazPose.Performer
{
    /// <summary>Pure exact-key and identity checks shared by manifest adapters and managed validation.</summary>
    public static class WardrobeRigManifestRules
    {
        public static bool HasExactUniqueKeys(IEnumerable<string> expectedKeys, IEnumerable<string> actualKeys, out string error)
        {
            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in expectedKeys ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(key) || !expected.Add(key))
                { error = "Expected rig key is missing or duplicated: " + (key ?? "<null>"); return false; }
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in actualKeys ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(key) || !actual.Add(key))
                { error = "Actual rig key is missing or duplicated: " + (key ?? "<null>"); return false; }
            if (!expected.SetEquals(actual)) { error = "Rig keys are incomplete or contain unexpected entries."; return false; }
            error = null; return true;
        }

        public static bool MatchesIdentity(string expectedGeneration, string expectedSignature,
            string actualGeneration, string actualSignature, out string error)
        {
            if (string.IsNullOrWhiteSpace(expectedGeneration) || string.IsNullOrWhiteSpace(expectedSignature) ||
                !string.Equals(expectedGeneration, actualGeneration, StringComparison.Ordinal) ||
                !string.Equals(expectedSignature, actualSignature, StringComparison.Ordinal))
            { error = "Rig entry generation or character signature does not match its manifest."; return false; }
            error = null; return true;
        }
    }
}
