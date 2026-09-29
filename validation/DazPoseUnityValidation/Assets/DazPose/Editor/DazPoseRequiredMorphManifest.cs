using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [Serializable]
    internal sealed class DazPoseRequiredMorphManifest
    {
        public int schemaVersion = 2;
        public DazPoseRequiredMorphManifestItem[] items = Array.Empty<DazPoseRequiredMorphManifestItem>();
    }

    [Serializable]
    internal sealed class DazPoseRequiredMorphManifestItem
    {
        public string rawControlId;
        public string name;
        public string state;
        public string firstSeenIn;
        public bool requiredByContent;
        public bool alwaysExport;
        public string category;
        public string purpose;
    }

    internal static class DazPoseRequiredMorphManifestStore
    {
        private static readonly object Gate = new object();

        public static int MarkConfirmed(string projectRoot, IEnumerable<ResolvedUnityMorphControl> controls)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("A Unity project root is required.", nameof(projectRoot));
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            var requiredNames = controls.Where(control => control != null && control.Bindings != null && control.Bindings.Count > 0)
                .Select(control => control.SourceControlName).Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
            if (requiredNames.Count == 0) return 0;

            var path = Path.Combine(projectRoot, ".dazposewizard", "required-morphs.json");
            lock (Gate)
            {
                if (!File.Exists(path)) return 0;
                DazPoseRequiredMorphManifest manifest;
                try { manifest = JsonUtility.FromJson<DazPoseRequiredMorphManifest>(File.ReadAllText(path)); }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
                {
                    throw new InvalidDataException("Could not read the project Required Morph Manifest at " + path + ".", exception);
                }
                if (manifest == null || (manifest.schemaVersion != 1 && manifest.schemaVersion != 2) || manifest.items == null)
                    throw new InvalidDataException("The project Required Morph Manifest has an unsupported schema: " + path);

                var changed = 0;
                var dirty = false;
                if (manifest.schemaVersion == 1)
                {
                    foreach (var item in manifest.items)
                        if (item != null) item.requiredByContent = true;
                    manifest.schemaVersion = 2;
                    dirty = true;
                }
                foreach (var item in manifest.items)
                {
                    if (item == null || !requiredNames.Contains(item.name)) continue;
                    if (string.Equals(item.state, "ConfirmedDirectMorph", StringComparison.Ordinal)) continue;
                    if (!string.Equals(item.state, "Candidate", StringComparison.Ordinal)
                        && !string.Equals(item.state, "Unresolved", StringComparison.Ordinal)) continue;
                    item.state = "ConfirmedDirectMorph";
                    changed++;
                    dirty = true;
                }
                if (!dirty) return 0;
                manifest.items = manifest.items.Where(item => item != null)
                    .OrderBy(item => item.name, StringComparer.Ordinal).ThenBy(item => item.rawControlId, StringComparer.Ordinal).ToArray();
                WriteAtomic(path, JsonUtility.ToJson(manifest, true));
                return changed;
            }
        }

        private static void WriteAtomic(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backupPath = path + "." + Guid.NewGuid().ToString("N") + ".bak";
            try
            {
                File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporaryPath, path, backupPath);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
        }
    }
}
