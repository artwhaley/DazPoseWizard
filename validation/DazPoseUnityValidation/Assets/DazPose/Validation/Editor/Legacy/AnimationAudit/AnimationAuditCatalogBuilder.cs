using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DazPose.AnimationAudit;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.AnimationAudit
{
    public static class AnimationAuditCatalogBuilder
    {
        public const string CatalogPath = "Assets/DazPose/AnimationAudit/AnimationAuditCatalog.asset";
        public const string KawaiiRoot = "Assets/KAWAII_ANIMATIOMS_100";
        public const string KuboldRoot = "Assets/FemaleMovementAnimsetPro";
        public const string ReportPath = "docs/animation-audit/AnimationAuditInventory.md";
        public const string LaraModelPath = "Assets/TestCharacter/lara.fbx";

        // Scene preparation rebuilds both catalog and playback states together.
        public static void BuildCatalogMenu()
        {
            AnimationAuditCatalog catalog = BuildCatalog();
            Debug.Log("Animation audit catalog built: " + catalog.entries.Count + " clips. Inventory: " + ReportPath);
        }

        public static AnimationAuditCatalog BuildCatalog()
        {
            EnsureProjectFolder("Assets/DazPose/AnimationAudit");
            string[] roots = { KawaiiRoot, KuboldRoot };
            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                if (!AssetDatabase.IsValidFolder(root))
                {
                    Debug.LogWarning("Animation audit source folder is missing: " + root);
                    continue;
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
                }
            }

            var entries = new List<AnimationAuditEntry>();
            foreach (string path in paths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .Where(IsAuditableClip)
                    .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Avatar sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                foreach (AnimationClip clip in clips)
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    string display = IsGenericTakeName(clip.name) ? stem : clip.name;
                    if (clips.Count(candidate => string.Equals(candidate.name, clip.name, StringComparison.OrdinalIgnoreCase)) > 1)
                        display += " [" + clip.name + "]";
                    bool humanoid = importer.animationType == ModelImporterAnimationType.Human;
                    entries.Add(new AnimationAuditEntry
                    {
                        pack = path.IndexOf(KawaiiRoot, StringComparison.OrdinalIgnoreCase) >= 0 ? AnimationAuditPack.Kawaii : AnimationAuditPack.FemaleMovementAnimsetPro,
                        category = Categorize(display),
                        displayName = display,
                        assetPath = path,
                        clipName = clip.name,
                        clip = clip,
                        durationSeconds = clip.length,
                        frameRate = clip.frameRate,
                        sourceHumanoid = humanoid,
                        loopTime = GetLoopTime(importer, clip),
                        hasRootMotionCurves = DetectRootMotionCurves(clip),
                        rootMotionNode = ReadImporterString(importer, "motionNodeName"),
                        sourceAvatarName = sourceAvatar != null ? sourceAvatar.name : string.Empty,
                        importWarnings = ReadImporterString(importer, "animationImportWarnings"),
                        importErrors = ReadImporterString(importer, "animationImportErrors"),
                        retargetStatus = humanoid
                            ? "Humanoid source; runtime playback on Lara not yet visually verified"
                            : "Generic source; transform-path playback on Lara not yet verified"
                    });
                }
            }

            AnimationAuditCatalog catalog = AssetDatabase.LoadAssetAtPath<AnimationAuditCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AnimationAuditCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.entries = entries;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            WriteInventory(catalog);
            return catalog;
        }

        public static string Categorize(string sourceName)
        {
            string name = NormalizeName(sourceName);
            if (name.Contains("sit") || name.Contains("seiza") || name.Contains("stool")) return "Sit / Seated";
            if (name.Contains("speak") || name.Contains("talk") || name.Contains("gesture") || name.Contains("point")) return "Speak / Body Gesture";
            if (name.Contains("sleep") || name.Contains("lie") || name.Contains("lying")) return "Sleep / Lie";
            if (name.Contains("turn") || name.Contains("pivot") || name.Contains("rotate") || name.Contains("suddenstop")) return "Turn / Pivot";
            if (name.Contains("idle") || name.Contains("wait") || name.Contains("breath")) return "Idle";
            if (name.Contains("run") || name.Contains("sprint")) return "Run";
            if (name.Contains("jump") || name.Contains("hop")) return "Jump";
            if (name.Contains("dance") || name.Contains("idol")) return "Dance";
            if (name.Contains("pair") || name.Contains("couple")) return "Pair";
            if (name.Contains("attack") || name.Contains("combat") || name.Contains("hit") || name.Contains("punch") || name.Contains("kick")) return "Combat";
            if (Regex.IsMatch(name, @"kawalk0[1-7](start|pivot|stop)?") || name.Contains("walkfwd") || name.Contains("slowwalk")) return "Forward Walk";
            if (name.Contains("walk") && (name.Contains("left") || name.Contains("right") || name.Contains("bwd") || name.Contains("backward"))) return "Directional Walk";
            if (name.Contains("walk")) return "Forward Walk";
            return "Other";
        }

        public static bool IsAuditableClip(AnimationClip clip)
        {
            if (clip == null || clip.length <= 0f || string.IsNullOrEmpty(clip.name)) return false;
            string name = clip.name.ToLowerInvariant();
            return !name.Contains("preview") && !name.Contains("bindpose") && !name.Contains("bind_pose");
        }

        private static bool IsGenericTakeName(string name)
        {
            return string.IsNullOrEmpty(name) || Regex.IsMatch(name, @"^(take|scene|animation)\s*\d*$", RegexOptions.IgnoreCase);
        }

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty).TrimStart('@').Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static bool GetLoopTime(ModelImporter importer, AnimationClip clip)
        {
            ModelImporterClipAnimation[] configured = importer.clipAnimations;
            if (configured == null || configured.Length == 0) configured = importer.defaultClipAnimations;
            ModelImporterClipAnimation match = configured?.FirstOrDefault(item => item != null && string.Equals(item.name, clip.name, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.loopTime;
            return clip.wrapMode == WrapMode.Loop;
        }

        private static bool DetectRootMotionCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                string property = binding.propertyName ?? string.Empty;
                string path = binding.path ?? string.Empty;
                if (property.StartsWith("RootT", StringComparison.OrdinalIgnoreCase) ||
                    property.StartsWith("RootQ", StringComparison.OrdinalIgnoreCase) ||
                    property.StartsWith("MotionT", StringComparison.OrdinalIgnoreCase) ||
                    property.StartsWith("MotionQ", StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(path, "Root", StringComparison.OrdinalIgnoreCase) && (property.Contains("Position") || property.Contains("Rotation"))))
                    return true;
            }
            return false;
        }

        private static string ReadImporterString(ModelImporter importer, string propertyName)
        {
            try
            {
                PropertyInfo property = importer.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null) return Convert.ToString(property.GetValue(importer, null));
                FieldInfo field = importer.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field != null ? Convert.ToString(field.GetValue(importer)) : string.Empty;
            }
            catch (Exception exception)
            {
                return "Could not read importer field " + propertyName + ": " + exception.Message;
            }
        }

        private static void WriteInventory(AnimationAuditCatalog catalog)
        {
            string report = BuildInventoryMarkdown(catalog);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, report, new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static string BuildInventoryMarkdown(AnimationAuditCatalog catalog)
        {
            var output = new StringBuilder();
            output.AppendLine("# Animation Audit Inventory");
            output.AppendLine();
            output.AppendLine("Generated from the installed project assets by `Tools > DAZ Pose > Animation Audit > Build Catalog and Inventory`. The package folders are scanned read-only. Import flags and curve names are inventory hints; they do not prove visual suitability or retarget quality.");
            output.AppendLine();
            output.AppendLine("Canonical Lara model: `" + LaraModelPath + "`. Its source FBX importer was not changed by the audit tool.");
            output.AppendLine();
            AppendPackSummary(output, catalog, AnimationAuditPack.Kawaii);
            AppendPackSummary(output, catalog, AnimationAuditPack.FemaleMovementAnimsetPro);
            AppendMissingPriority(output, catalog);
            AppendDuplicates(output, catalog);
            AppendImportIssues(output, catalog);
            output.AppendLine("## Retarget and runtime status");
            output.AppendLine();
            output.AppendLine("Catalog entries retain actual Unity `AnimationClip` objects, including multi-clip Kubold FBX sub-assets. Runtime retarget/playback is deliberately recorded as unverified until the audit scene evaluates each clip on Lara. Root motion and mirror measurements must be taken in Play Mode; this report does not infer them from filenames or import flags.");
            output.AppendLine();
            return output.ToString();
        }

        private static void AppendPackSummary(StringBuilder output, AnimationAuditCatalog catalog, AnimationAuditPack pack)
        {
            var entries = catalog.entries.Where(entry => entry.pack == pack).ToArray();
            string sourceRoot = pack == AnimationAuditPack.Kawaii ? KawaiiRoot : KuboldRoot;
            output.AppendLine("## " + (pack == AnimationAuditPack.Kawaii ? "KAWAII" : "Female Movement Animset Pro / Kubold"));
            output.AppendLine();
            output.AppendLine("Source folder: `" + sourceRoot + "` (" + (AssetDatabase.IsValidFolder(sourceRoot) ? "present" : "absent") + ")");
            output.AppendLine();
            output.AppendLine("Discovered AnimationClip assets/sub-assets: **" + entries.Length + "**");
            output.AppendLine();
            output.AppendLine("| Category | Count |");
            output.AppendLine("|---|---:|");
            foreach (var count in entries.GroupBy(entry => entry.category).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                output.AppendLine("| " + EscapeTable(count.Key) + " | " + count.Count() + " |");
            output.AppendLine();
        }

        private static void AppendMissingPriority(StringBuilder output, AnimationAuditCatalog catalog)
        {
            output.AppendLine("## Priority clips not found");
            output.AppendLine();
            AppendMissingList(output, catalog, "KAWAII", AnimationAuditPack.Kawaii, ExpectedKawaii());
            AppendMissingList(output, catalog, "Kubold", AnimationAuditPack.FemaleMovementAnimsetPro, ExpectedKubold());
        }

        private static void AppendMissingList(StringBuilder output, AnimationAuditCatalog catalog, string label, AnimationAuditPack pack, IEnumerable<string> expected)
        {
            string sourceRoot = pack == AnimationAuditPack.Kawaii ? KawaiiRoot : KuboldRoot;
            if (!AssetDatabase.IsValidFolder(sourceRoot))
            {
                output.AppendLine("**" + label + ":** source folder absent; scan skipped.");
                output.AppendLine();
                return;
            }
            var normalizedPresent = catalog.entries.Where(entry => entry.pack == pack)
                .SelectMany(entry => new[] { entry.IdentityName, entry.clipName, Path.GetFileNameWithoutExtension(entry.assetPath) })
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(AnimationAuditCatalog.Normalize)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] missing = expected.Where(name => !normalizedPresent.Contains(AnimationAuditCatalog.Normalize(name))).ToArray();
            output.AppendLine("**" + label + ":** " + (missing.Length == 0 ? "all expected priority stems found" : string.Join(", ", missing)));
            output.AppendLine();
        }

        private static void AppendDuplicates(StringBuilder output, AnimationAuditCatalog catalog)
        {
            output.AppendLine("## Duplicate clip display names");
            output.AppendLine();
            var duplicates = catalog.entries.GroupBy(entry => new { entry.pack, Name = AnimationAuditCatalog.Normalize(entry.IdentityName) })
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key.pack).ThenBy(group => group.Key.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (duplicates.Length == 0) output.AppendLine("None.");
            else
            {
                foreach (var group in duplicates)
                    output.AppendLine("- " + group.Key.pack + " / `" + group.First().IdentityName + "`: " + string.Join(", ", group.Select(entry => "`" + entry.assetPath + "`")));
            }
            output.AppendLine();
        }

        private static void AppendImportIssues(StringBuilder output, AnimationAuditCatalog catalog)
        {
            output.AppendLine("## Import errors and warnings");
            output.AppendLine();
            var paths = catalog.entries.Where(entry => !string.IsNullOrWhiteSpace(entry.importErrors) || !string.IsNullOrWhiteSpace(entry.importWarnings))
                .GroupBy(entry => entry.assetPath).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length == 0) output.AppendLine("No importer error or warning strings were exposed by the scanned ModelImporters.");
            else
            {
                foreach (var group in paths)
                {
                    AnimationAuditEntry entry = group.First();
                    output.AppendLine("- `" + entry.assetPath + "`");
                    if (!string.IsNullOrWhiteSpace(entry.importErrors)) output.AppendLine("  - Errors: " + EscapeInline(entry.importErrors));
                    if (!string.IsNullOrWhiteSpace(entry.importWarnings)) output.AppendLine("  - Warnings: " + EscapeInline(entry.importWarnings));
                }
            }
            output.AppendLine();
        }

        private static IEnumerable<string> ExpectedKawaii()
        {
            var names = new List<string>();
            for (int i = 1; i <= 7; i++)
            {
                string family = "KA_Walk" + i.ToString("00");
                names.AddRange(new[] { family + "_Start", family, family + "_Pivot", family + "_Stop" });
            }
            foreach (string direction in new[] { "Bwd", "Left", "Right" })
                names.AddRange(new[] { "KA_Walk_" + direction, "KA_Walk_" + direction + "_Start", "KA_Walk_" + direction + "_Pivot", "KA_Walk_" + direction + "_Stop" });
            names.AddRange(new[] { "KA_TurnLeft_90", "KA_TurnRight_90", "KA_TurnLeft_180", "KA_TurnRight_180", "KA_SuddenStop_Fwd", "KA_SuddenStop_Bwd", "KA_SuddenStop_Left", "KA_SuddenStop_Right" });
            foreach (string family in new[] { "KA_Sit", "KA_Idle10_Sit", "KA_Sit_CrossLegged", "KA_Sit_CrossLegs", "KA_Sit_LookAtToes", "KA_Sit_WithBothLegsUp", "KA_Sit_FeelListless", "KA_Sit_Sorrow", "KA_Sit_SwingingLegs" })
                names.AddRange(new[] { family + "_Start", family + "_Loop", family + "_End" });
            string[] speak = { "Normal", "Explaining", "Excited", "Calm", "PointingFingerScolding", "PointingFingerCalmly", "Chatty", "ArmsCrossedPouting", "ArmsCrossedCalm", "Shy" };
            for (int i = 0; i < speak.Length; i++)
                names.AddRange(new[] { "KA_Speak" + (i + 1).ToString("00") + "_" + speak[i] + "_Start", "KA_Speak" + (i + 1).ToString("00") + "_" + speak[i] + "_Loop", "KA_Speak" + (i + 1).ToString("00") + "_" + speak[i] + "_End" });
            names.AddRange(new[] { "Idle01_breathing", "Idle09_Waiting", "Idle11_LookingBack", "Idle12_LeaningForward", "Idle18_Shy", "Idle37_Tsundere", "Idle39_CuteArmUp", "Idle40_CrossLegs", "Idle41_CuteShyPose", "Idle43_HandOnHip", "Idle45_WaveHandSlightly", "Idle50_StandingTalk1_1", "Idle51_StandingTalk1_2", "Idle52_Curtsy", "Idle65_ThumbsUp", "Idle72_LeanForward", "Idle73_IdolPose", "Idle75_Pointing" });
            return names;
        }

        private static IEnumerable<string> ExpectedKubold()
        {
            return new[] { "Idle", "WalkFwdStart", "WalkFwd", "WalkFwdStop_LU", "WalkFwdStop_RU", "RotateLeft90", "RotateRight90", "RotateLeft180", "RotateRight180", "WalkFwdStart_L45", "WalkFwdStart_R45", "WalkFwdStart_L90", "WalkFwdStart_R90", "WalkFwdStart_L135", "WalkFwdStart_R135", "WalkFwdStart_L180", "WalkFwdStart_R180", "WalkTurnL135", "WalkTurnR135", "WalkFwdArchLeft180", "WalkFwdArchRight180", "SlowWalkStart", "SlowWalkLoop", "SlowWalkStop_LU", "SlowWalkStop_RU", "SlowWalkStart180", "SitStoolStart", "SitStoolLoop", "SitStoolStop" };
        }

        private static string EscapeTable(string value) => (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        private static string EscapeInline(string value) => (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

        private static void EnsureProjectFolder(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }

    }
}
