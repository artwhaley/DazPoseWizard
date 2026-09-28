using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace DazPose.Core;

public static class DazPoseExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void WriteJson(string path, DazFigureDefinition figure, DazPose pose, DazPoseEvaluation evaluation,
        IReadOnlyList<string> warnings)
    {
        var data = new
        {
            format = "DazPoseTool",
            version = 1,
            source = new
            {
                figureFile = figure.FilePath,
                poseFile = pose.FilePath,
                figureAssetId = figure.AssetId,
                poseAssetId = pose.AssetId
            },
            coordinateSystem = new { handedness = "right", upAxis = "Y", lengthUnit = "centimeter", angleUnit = "degree" },
            poseChannels = pose.Channels.Select(channel => new
            {
                url = channel.Url,
                targetId = channel.TargetBone?.Id,
                targetName = channel.TargetBone?.Name,
                property = channel.ParsedUrl.Property,
                axis = channel.ParsedUrl.Axis,
                leafProperty = channel.ParsedUrl.LeafProperty,
                keys = channel.Keys.Select(key => new { timeSeconds = key.Time, value = key.Value }).ToArray(),
                supported = channel.IsSupportedSkeletalChannel,
                usedIdFallback = channel.UsedIdFallback
            }).ToArray(),
            bones = evaluation.Bones.OrderBy(bone => bone.Bone.Id, StringComparer.Ordinal).Select(item => new
            {
                id = item.Bone.Id,
                name = item.Bone.Name,
                parentId = item.Bone.ParentId,
                rotationOrder = item.Bone.RotationOrder,
                inheritsScale = item.Bone.InheritsScale,
                center = V(item.Bone.CenterPoint),
                end = V(item.Bone.EndPoint),
                orientationDegrees = V(item.Bone.Orientation),
                sourceRotationDegrees = V(item.Bone.Rotation),
                sourceTranslationCm = V(item.Bone.Translation),
                sourceScale = V(item.Bone.Scale),
                sourceGeneralScale = item.Bone.GeneralScale,
                poseRotationDegrees = V(item.PoseRotationDegrees),
                poseTranslationCm = V(item.PoseTranslationCm),
                evaluatedLocalRotation = Q(item.EvaluatedLocalRotation),
                evaluatedWorldRotation = Q(item.EvaluatedWorldRotation),
                evaluatedWorldPositionCm = V(item.EvaluatedWorldPositionCm),
                evaluatedGlobalScale = V(item.EvaluatedGlobalScale),
                evaluatedGlobalScaleMatrix = M(item.EvaluatedGlobalScaleMatrix)
            }).ToArray(),
            ignoredNeutralChannels = pose.NeutralUnsupportedChannels.Select(channel => channel.Url).OrderBy(url => url, StringComparer.Ordinal).ToArray(),
            warnings
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions), new UTF8Encoding(false));
    }

    public static void WriteBvh(string path, DazFigureDefinition figure, DazPoseEvaluation evaluation)
    {
        if (!figure.NodesById.TryGetValue("hip", out var hip) || hip.Type != "bone")
            throw new DazConversionException("The figure has no bone with ID 'hip', required as the BVH root.");
        var children = figure.Bones.Where(node => node.ParentId is not null)
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var ordered = new List<DazBoneDefinition>();
        void Add(DazBoneDefinition node)
        {
            ordered.Add(node);
            if (children.TryGetValue(node.Id, out var descendants))
                foreach (var child in descendants) Add(child);
        }
        Add(hip);

        var builder = new StringBuilder();
        builder.AppendLine("HIERARCHY");
        builder.AppendLine("ROOT hip");
        builder.AppendLine("{");
        var rootOffset = hip.CenterPoint - figure.NodesById[hip.ParentId!].CenterPoint;
        builder.Append("  OFFSET ").AppendLine(VText(rootOffset));
        builder.AppendLine("  CHANNELS 6 Xposition Yposition Zposition Xrotation Yrotation Zrotation");
        if (children.TryGetValue(hip.Id, out var rootChildren))
            foreach (var child in rootChildren) WriteJoint(builder, child, hip, children);
        builder.AppendLine("}");
        builder.AppendLine("MOTION");
        builder.AppendLine("Frames: 1");
        builder.AppendLine("Frame Time: 0.0333333333");

        var outputValues = new List<float>();
        var rootPose = evaluation.BonesById[hip.Id];
        outputValues.Add(rootPose.PoseTranslationCm.X);
        outputValues.Add(rootPose.PoseTranslationCm.Y);
        outputValues.Add(rootPose.PoseTranslationCm.Z);
        AddEuler(outputValues, rootPose.EvaluatedLocalRotation);
        foreach (var node in ordered.Skip(1)) AddEuler(outputValues, evaluation.BonesById[node.Id].EvaluatedLocalRotation);
        builder.AppendLine(string.Join(' ', outputValues.Select(Number)));
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }

    private static void WriteJoint(StringBuilder builder, DazBoneDefinition node, DazBoneDefinition parent,
        IReadOnlyDictionary<string, DazBoneDefinition[]> children)
    {
        builder.Append("  JOINT ").AppendLine(SafeBvhName(node.Name));
        builder.AppendLine("  {");
        builder.Append("    OFFSET ").AppendLine(VText(node.CenterPoint - parent.CenterPoint));
        builder.AppendLine("    CHANNELS 3 Xrotation Yrotation Zrotation");
        if (children.TryGetValue(node.Id, out var descendants) && descendants.Length > 0)
        {
            foreach (var child in descendants) WriteJoint(builder, child, node, children);
        }
        else
        {
            builder.AppendLine("    End Site");
            builder.AppendLine("    {");
            builder.Append("      OFFSET ").AppendLine(VText(node.EndPoint - node.CenterPoint));
            builder.AppendLine("    }");
        }
        builder.AppendLine("  }");
    }

    private static void AddEuler(List<float> values, Quaternion rotation)
    {
        var euler = DazTransformEvaluator.QuaternionToEuler(rotation, "XYZ");
        values.Add(euler.X); values.Add(euler.Y); values.Add(euler.Z);
    }

    private static float[] V(Vector3 value) => [Clean(value.X), Clean(value.Y), Clean(value.Z)];
    private static float[] Q(Quaternion value) => [Clean(value.X), Clean(value.Y), Clean(value.Z), Clean(value.W)];
    private static float[] M(Matrix4x4 value) =>
    [
        Clean(value.M11), Clean(value.M12), Clean(value.M13),
        Clean(value.M21), Clean(value.M22), Clean(value.M23),
        Clean(value.M31), Clean(value.M32), Clean(value.M33)
    ];
    private static string VText(Vector3 value) => $"{Number(value.X)} {Number(value.Y)} {Number(value.Z)}";
    private static string Number(float value) => Clean(value).ToString("0.######", CultureInfo.InvariantCulture);
    private static float Clean(float value) => Math.Abs(value) < 1e-8f ? 0f : value;
    private static string SafeBvhName(string name) => string.IsNullOrWhiteSpace(name) ? "unnamed" : name.Replace(' ', '_').Replace('\t', '_');
}
