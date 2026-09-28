using System.Numerics;

namespace DazPose.Core;

public sealed class DazFigureDefinition
{
    public required string FilePath { get; init; }
    public required string AssetId { get; init; }
    public required IReadOnlyList<DazBoneDefinition> Nodes { get; init; }
    public required IReadOnlyDictionary<string, DazBoneDefinition> NodesById { get; init; }
    public required IReadOnlyDictionary<string, DazBoneDefinition> NodesByName { get; init; }
    public IReadOnlyList<DazBoneDefinition> Bones => Nodes.Where(node => node.Type == "bone").ToArray();
    public IReadOnlyList<DazBoneDefinition> FigureRoots => Nodes.Where(node => node.Type == "figure").ToArray();
    public string FigureLabel => FigureRoots.SingleOrDefault()?.Label ?? Path.GetFileNameWithoutExtension(FilePath);
}

public sealed record DazBoneDefinition(
    string Id,
    string Name,
    string Label,
    string Type,
    string? ParentId,
    string RotationOrder,
    bool InheritsScale,
    Vector3 CenterPoint,
    Vector3 EndPoint,
    Vector3 Orientation,
    Vector3 Rotation,
    Vector3 Translation,
    Vector3 Scale,
    float GeneralScale);

public sealed record DazPoseKey(float Time, float Value);

public sealed record DazPropertyUrl(
    string AddressScheme,
    string Address,
    string? TargetNodeName,
    string? ControlId,
    string Property,
    string? Axis,
    string LeafProperty);

public sealed record DazPoseChannel(
    string Url,
    DazPropertyUrl ParsedUrl,
    IReadOnlyList<DazPoseKey> Keys,
    DazBoneDefinition? TargetBone,
    bool UsedIdFallback)
{
    public bool IsSupportedSkeletalChannel => TargetBone is not null
        && ParsedUrl.Property is "rotation" or "translation"
        && ParsedUrl.Axis is "x" or "y" or "z"
        && ParsedUrl.LeafProperty == "value";
    public bool IsSkeletalProperty => ParsedUrl.Property is "rotation" or "translation";
    public string TargetDescription => TargetBone is null ? ParsedUrl.Address : $"{TargetBone.Name} ({TargetBone.Id})";
}

public sealed class DazPose
{
    public required string FilePath { get; init; }
    public required string AssetId { get; init; }
    public required IReadOnlyList<DazPoseChannel> Channels { get; init; }
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    public string PoseName => Path.GetFileNameWithoutExtension(FilePath);
    public int SkeletalTargetCount => Channels.Where(channel => channel.IsSkeletalProperty && channel.TargetBone is not null)
        .Select(channel => channel.TargetBone!.Id).Distinct(StringComparer.Ordinal).Count();
    public int ResolvedSkeletalTargetCount => SkeletalTargetCount;
    public IReadOnlyList<DazPoseChannel> UnsupportedChannels => Channels.Where(channel => !channel.IsSupportedSkeletalChannel).ToArray();
    public IReadOnlyList<DazPoseChannel> NeutralUnsupportedChannels => UnsupportedChannels
        .Where(channel => channel.Keys.All(key => Math.Abs(key.Value) <= 1e-7f)).ToArray();
    public IReadOnlyList<DazPoseChannel> NonNeutralUnsupportedChannels => UnsupportedChannels
        .Where(channel => channel.Keys.Any(key => Math.Abs(key.Value) > 1e-7f)).ToArray();
}

public sealed record EvaluatedBonePose(
    DazBoneDefinition Bone,
    Vector3 PoseRotationDegrees,
    Vector3 PoseTranslationCm,
    Quaternion EvaluatedLocalRotation,
    Quaternion EvaluatedWorldRotation,
    Vector3 EvaluatedWorldPositionCm,
    Vector3 EvaluatedGlobalScale,
    Matrix4x4 EvaluatedGlobalScaleMatrix);

public sealed class DazPoseEvaluation
{
    public required IReadOnlyList<EvaluatedBonePose> Bones { get; init; }
    public required IReadOnlyDictionary<string, EvaluatedBonePose> BonesById { get; init; }
}

public sealed record ConversionDiagnostic(string Severity, string Message);

public sealed class ConversionResult
{
    public required DazFigureDefinition Figure { get; init; }
    public required DazPose Pose { get; init; }
    public required DazPoseEvaluation Evaluation { get; init; }
    public required string JsonPath { get; init; }
    public required string BvhPath { get; init; }
    public required string ReportPath { get; init; }
    public required IReadOnlyList<ConversionDiagnostic> Diagnostics { get; init; }
}
