using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace DazPose.Editor.AnimationAudit
{
    // Reads imported Avatar data; does not instantiate, animate, or modify any model.
    public static class HumanoidLegSetupReport
    {
        [MenuItem("DazPose/Diagnostics/Report Humanoid Leg Setup")]
        public static void WriteReport()
        {
            var report = new StringBuilder("# Imported Humanoid leg setup\n\n");
            report.AppendLine("Mapping and reference transforms describe the imported Avatar, not the current animation pose. Validity alone does not establish anatomical correctness.\n");
            foreach (string path in new[] { AnimationAuditSceneSetup.HumanoidModelPath,
                "Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01.FBX",
                "Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_1.fbx",
                "Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_2.fbx" })
            {
                report.AppendLine("## " + path + "\n");
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null && AssetImporter.GetAtPath(path) is ModelImporter importer && importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
                { avatar = importer.sourceAvatar; report.AppendLine("Copies Avatar from: " + (avatar != null ? AssetDatabase.GetAssetPath(avatar) : "unassigned") + "\n"); }
                if (avatar == null) { report.AppendLine("No imported Avatar found.\n"); continue; }
                report.AppendLine("Avatar: " + avatar.name + "; isHuman=" + avatar.isHuman + "; isValid=" + avatar.isValid + "\n");
                if (!avatar.isHuman) continue;
                var description = avatar.humanDescription;
                var human = description.human ?? Array.Empty<HumanBone>();
                report.AppendLine("| Human bone | Assigned skeleton bone | Default limits | Min | Max | Center | Axis length |\n|---|---|---|---|---|---|---|");
                foreach (HumanBone bone in human)
                    report.AppendLine("| " + bone.humanName + " | " + bone.boneName + " | " + bone.limit.useDefaultValues + " | " + Vec(bone.limit.min) + " | " + Vec(bone.limit.max) + " | " + Vec(bone.limit.center) + " | " + Num(bone.limit.axisLength) + " |");
                if (human.Length == 0) report.AppendLine("Imported Avatar returned no mapping data; inspect Rig > Configure manually.");
                report.AppendLine("\nUpper leg twist: " + Num(description.upperLegTwist) + "; lower leg twist: " + Num(description.lowerLegTwist) + "; leg stretch: " + Num(description.legStretch) + "; feet spacing: " + Num(description.feetSpacing) + "; translation DoF: " + description.hasTranslationDoF + "\n");
                report.AppendLine("### Lower-body reference transforms (local to each bone's parent)\n");
                report.AppendLine("| Skeleton bone | Position | Rotation quaternion x,y,z,w | Scale |\n|---|---|---|---|");
                foreach (SkeletonBone bone in description.skeleton ?? Array.Empty<SkeletonBone>())
                {
                    string name = bone.name ?? "";
                    bool leg = human.Any(m => m.boneName == name && (m.humanName == "Hips" || m.humanName.Contains("Leg") || m.humanName.Contains("Foot") || m.humanName.Contains("Toes")));
                    if (!leg && !new[] { "hip", "pelvis", "thigh", "shin", "foot", "toe" }.Any(w => name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    Quaternion q = bone.rotation;
                    report.AppendLine("| " + name + " | " + Vec(bone.position) + " | " + Num(q.x) + ", " + Num(q.y) + ", " + Num(q.z) + ", " + Num(q.w) + " | " + Vec(bone.scale) + " |");
                }
                report.AppendLine();
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "animation-audit", "HumanoidLegSetup.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, report.ToString());
            Debug.Log("Humanoid leg setup report saved: " + output + ". No importer or scene changes made.");
        }
        private static string Num(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Vec(Vector3 v) => Num(v.x) + ", " + Num(v.y) + ", " + Num(v.z);
    }
}
