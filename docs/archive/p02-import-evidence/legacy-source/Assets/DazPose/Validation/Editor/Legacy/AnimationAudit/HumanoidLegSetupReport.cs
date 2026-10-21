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
        [MenuItem("Tools/DAZ Pose/Animation Audit/Report Humanoid Leg Setup")]
        public static void WriteReport()
        {
            var report = new StringBuilder("# Imported Humanoid leg setup\n\n");
            report.AppendLine("Mapping and reference transforms describe the imported Avatar, not the current animation pose. Validity alone does not establish anatomical correctness.\n");
            foreach (string path in new[] {
                LaraHumanoidKawaiiTestSetup.ModelPath,
                "Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01.FBX",
                "Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_1.fbx",
                "Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_2.fbx" })
            {
                report.AppendLine("## " + path + "\n");
                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null && AssetImporter.GetAtPath(path) is ModelImporter importer &&
                    importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther)
                {
                    avatar = importer.sourceAvatar;
                    report.AppendLine("Copies Avatar from: " + (avatar != null ? AssetDatabase.GetAssetPath(avatar) : "unassigned") + "\n");
                }
                if (avatar == null) { report.AppendLine("No imported Avatar found.\n"); continue; }
                report.AppendLine("Avatar: " + avatar.name + "; isHuman=" + avatar.isHuman + "; isValid=" + avatar.isValid + "\n");
                if (!avatar.isHuman) continue;
                HumanDescription description = avatar.humanDescription;
                HumanBone[] human = description.human ?? Array.Empty<HumanBone>();
                report.AppendLine("| Human bone | Assigned skeleton bone | Default limits | Min | Max | Center | Axis length |\n|---|---|---|---|---|---|---|");
                foreach (HumanBone bone in human)
                    report.AppendLine("| " + bone.humanName + " | " + bone.boneName + " | " + bone.limit.useDefaultValues + " | " + Vector(bone.limit.min) + " | " + Vector(bone.limit.max) + " | " + Vector(bone.limit.center) + " | " + Number(bone.limit.axisLength) + " |");
                if (human.Length == 0) report.AppendLine("Imported Avatar returned no mapping data; inspect Rig > Configure manually.");
                report.AppendLine("\nUpper leg twist: " + Number(description.upperLegTwist) + "; lower leg twist: " + Number(description.lowerLegTwist) + "; leg stretch: " + Number(description.legStretch) + "; feet spacing: " + Number(description.feetSpacing) + "; translation DoF: " + description.hasTranslationDoF + "\n");
                report.AppendLine("### Lower-body reference transforms (local to each bone's parent)\n");
                report.AppendLine("| Skeleton bone | Position | Rotation quaternion x,y,z,w | Scale |\n|---|---|---|---|");
                foreach (SkeletonBone bone in description.skeleton ?? Array.Empty<SkeletonBone>())
                {
                    string name = bone.name ?? "";
                    bool leg = human.Any(mapped => mapped.boneName == name &&
                        (mapped.humanName == "Hips" || mapped.humanName.Contains("Leg") || mapped.humanName.Contains("Foot") || mapped.humanName.Contains("Toes")));
                    if (!leg && !new[] { "hip", "pelvis", "thigh", "shin", "foot", "toe" }.Any(word => name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    Quaternion q = bone.rotation;
                    report.AppendLine("| " + name + " | " + Vector(bone.position) + " | " + Number(q.x) + ", " + Number(q.y) + ", " + Number(q.z) + ", " + Number(q.w) + " | " + Vector(bone.scale) + " |");
                }
                report.AppendLine();
            }
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "animation-audit", "HumanoidLegSetup.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, report.ToString());
            Debug.Log("Humanoid leg setup report saved: " + output + ". No importer or scene changes made.");
        }

        private static string Number(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => Number(value.x) + ", " + Number(value.y) + ", " + Number(value.z);
    }
}
