using System.Globalization;
using System.Text.Json;

namespace DazPose.Core;

public static class DazPropertyUrlParser
{
    public static DazPropertyUrl Parse(string url)
    {
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var propertyStart = url.IndexOf(":?", StringComparison.Ordinal);
        if (schemeEnd <= 0 || propertyStart <= schemeEnd + 3 || propertyStart + 2 >= url.Length)
            throw new DazConversionException($"Malformed DSON property URL '{url}'.");

        var scheme = url[..schemeEnd];
        var address = url[(schemeEnd + 3)..propertyStart];
        var propertyPath = url[(propertyStart + 2)..];
        var parts = propertyPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new DazConversionException($"DSON property URL '{url}' has no property path.");

        string? target = null;
        string? controlId = null;
        const string selectionPrefix = "@selection/";
        const string selectionControlPrefix = "@selection#";
        if (address.StartsWith(selectionPrefix, StringComparison.Ordinal))
            target = address[selectionPrefix.Length..];
        else if (address.StartsWith(selectionControlPrefix, StringComparison.Ordinal))
            controlId = address[selectionControlPrefix.Length..];

        if (target is not null && target.Length == 0)
            throw new DazConversionException($"DSON property URL '{url}' has an empty target node name.");

        return new DazPropertyUrl(
            scheme,
            address,
            target,
            controlId,
            parts[0],
            parts.Length > 1 ? parts[1] : null,
            parts[^1]);
    }
}

public static class DazPoseParser
{
    public static DazPose Parse(string path, JsonDocument document, DazFigureDefinition figure)
    {
        try
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("scene", out var scene)
                || !scene.TryGetProperty("animations", out var animations)
                || animations.ValueKind != JsonValueKind.Array)
                throw new DazConversionException("The pose file has no scene.animations array.");

            var assetId = root.TryGetProperty("asset_info", out var assetInfo) && assetInfo.TryGetProperty("id", out var id)
                ? id.GetString() ?? string.Empty : string.Empty;
            var channels = new List<DazPoseChannel>();
            var diagnostics = new List<string>();
            foreach (var animation in animations.EnumerateArray())
            {
                if (!animation.TryGetProperty("url", out var urlJson) || urlJson.ValueKind != JsonValueKind.String)
                    throw new DazConversionException("A pose animation channel is missing its URL.");
                var url = urlJson.GetString()!;
                var parsed = DazPropertyUrlParser.Parse(url);
                if (!animation.TryGetProperty("keys", out var keysJson) || keysJson.ValueKind != JsonValueKind.Array)
                    throw new DazConversionException($"Pose channel '{url}' is missing its keys array.");

                var keys = new List<DazPoseKey>();
                foreach (var key in keysJson.EnumerateArray())
                {
                    if (key.ValueKind != JsonValueKind.Array || key.GetArrayLength() < 2)
                        throw new DazConversionException($"Pose channel '{url}' has a malformed key; expected [time, value].");
                    keys.Add(new DazPoseKey(Number(key[0]), Number(key[1])));
                }
                if (keys.Count == 0)
                    throw new DazConversionException($"Pose channel '{url}' contains no key values.");

                DazBoneDefinition? targetBone = null;
                var usedIdFallback = false;
                if (parsed.Property is "rotation" or "translation")
                {
                    if (parsed.TargetNodeName is null)
                    {
                        if (!parsed.IsSelectedFigureRoot && !parsed.IsFigureControlAddress)
                            throw new DazConversionException($"Skeletal channel '{url}' does not address a target node or selected figure/control property.");
                    }
                    else
                    {
                        if (!string.Equals(parsed.AddressScheme, "name", StringComparison.Ordinal))
                            throw new DazConversionException($"Skeletal channel '{url}' uses unsupported address scheme '{parsed.AddressScheme}'. Expected name://.");
                        if (!figure.NodesByName.TryGetValue(parsed.TargetNodeName, out targetBone))
                        {
                            if (figure.NodesById.TryGetValue(parsed.TargetNodeName, out targetBone))
                            {
                                usedIdFallback = true;
                                diagnostics.Add($"Resolved '{parsed.TargetNodeName}' through node ID '{targetBone.Id}' after name lookup failed.");
                            }
                            else
                            {
                                throw new DazConversionException($"Unresolved skeletal target '{parsed.TargetNodeName}' in '{url}'.");
                            }
                        }
                        if (targetBone.Type != "bone")
                            throw new DazConversionException($"Skeletal target '{parsed.TargetNodeName}' resolves to a '{targetBone.Type}' node, not a bone.");
                    }
                }

                if (parsed.IsSelectedFigureRoot && (parsed.Property is "rotation" or "translation"))
                    diagnostics.Add($"Recognized targetless selected-figure {parsed.Property} channel '{url}' as an unsupported figure-root property.");

                channels.Add(new DazPoseChannel(url, parsed, keys, targetBone, usedIdFallback));
            }

            return new DazPose
            {
                FilePath = Path.GetFullPath(path), AssetId = assetId, Channels = channels, Diagnostics = diagnostics
            };
        }
        catch (DazConversionException ex)
        {
            throw new DazConversionException($"Pose '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new DazConversionException($"Pose '{Path.GetFileName(path)}' is malformed: {ex.Message}", ex);
        }
    }

    private static float Number(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetSingle(),
        JsonValueKind.String when float.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new FormatException("Expected a numeric key value.")
    };
}
