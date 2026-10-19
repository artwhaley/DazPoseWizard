using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DazPose.Editor.Studio
{
    internal static class NativeDirectingFixture
    {
        [Serializable] internal sealed class CodeFile { public string path="",sha256=""; }
        [Serializable] internal sealed class FixtureIdentity
        {
            public int schemaVersion=0;
            public string fixtureId="",sourceProject="",fixtureProject="",scenePath="",sourceRevision="",codeHash="";
            public CodeFile[] codeFiles=Array.Empty<CodeFile>();
        }
        internal static string ProjectPath=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        static string Normalize(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        internal static string HashFile(string path)
        { using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
        internal static FixtureIdentity Require()
        {
            NativeStageContext.RequireEditMode();
            string marker = Path.Combine(ProjectPath, ".native-directing-fixture.json");
            if (!File.Exists(marker)) throw new InvalidOperationException("This is not the isolated native-directing project. Open the prepared G1Project; diagnostic refuses the production project.");
            var value = JsonUtility.FromJson<FixtureIdentity>(File.ReadAllText(marker));
            if (value == null || value.schemaVersion != 1 || string.IsNullOrEmpty(value.fixtureId)
                || !string.Equals(Normalize(value.fixtureProject), Normalize(ProjectPath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(Normalize(value.sourceProject), Normalize(ProjectPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid independent fixture identity. Recreate/repair the fixture through its source-only preparation tool.");
            if (value.scenePath != "Assets/__NativeDirectingFixtures/G1_FirstContact.unity"
                || SceneManager.GetActiveScene().path != value.scenePath)
                throw new InvalidOperationException("Open only the disposable scene Assets/__NativeDirectingFixtures/G1_FirstContact.unity for G1. Production scenes are refused.");
            if (value.codeFiles == null || value.codeFiles.Length == 0) throw new InvalidOperationException("Fixture code sync manifest is missing.");
            var codeText = new StringBuilder();
            foreach (var file in value.codeFiles.OrderBy(f => f.path, StringComparer.Ordinal))
            {
                string full = Path.GetFullPath(Path.Combine(ProjectPath, file.path));
                if (!file.path.StartsWith("Assets/", StringComparison.Ordinal)
                    || !full.StartsWith(Normalize(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(full) || HashFile(full) != file.sha256)
                    throw new InvalidOperationException("Fixture code/meta differs from synchronized implementation: " + file.path + ". Ask the executor to sync current source, then let Unity finish compiling.");
                codeText.Append(file.path).Append('|').Append(file.sha256).Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                string aggregate = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(codeText.ToString()))).Replace("-", "").ToLowerInvariant();
                if (aggregate != value.codeHash) throw new InvalidOperationException("Fixture code manifest hash is inconsistent.");
            }
            return value;
        }
    }
}
