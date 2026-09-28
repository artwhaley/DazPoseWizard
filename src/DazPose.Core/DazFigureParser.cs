using System.Numerics;
using System.Text.Json;

namespace DazPose.Core;

public static class DazFigureParser
{
    private static readonly HashSet<string> RotationOrders = ["XYZ", "YZX", "ZYX", "ZXY", "XZY", "YXZ"];

    public static DazFigureDefinition Parse(string path, JsonDocument document)
    {
        try
        {
            var root = document.RootElement;
            var assetId = root.TryGetProperty("asset_info", out var assetInfo) && assetInfo.TryGetProperty("id", out var id)
                ? id.GetString() ?? string.Empty : string.Empty;
            if (!root.TryGetProperty("node_library", out var nodeLibrary) || nodeLibrary.ValueKind != JsonValueKind.Array)
                throw new DazConversionException("The file has no node_library array.");

            var nodes = new List<DazBoneDefinition>();
            foreach (var node in nodeLibrary.EnumerateArray())
            {
                var nodeId = RequiredString(node, "id");
                var name = node.TryGetProperty("name", out var nameJson) ? nameJson.GetString() ?? string.Empty : string.Empty;
                var type = node.TryGetProperty("type", out var typeJson) ? typeJson.GetString() ?? "node" : "node";
                var parentId = node.TryGetProperty("parent", out var parentJson) && parentJson.ValueKind == JsonValueKind.String
                    ? NormalizeParent(parentJson.GetString()) : null;
                var rotationOrder = node.TryGetProperty("rotation_order", out var orderJson)
                    ? orderJson.GetString() ?? "XYZ" : "XYZ";
                if (!RotationOrders.Contains(rotationOrder))
                    throw new DazConversionException($"Node '{name}' ({nodeId}) has unsupported rotation order '{rotationOrder}'.");

                nodes.Add(new DazBoneDefinition(
                    nodeId,
                    name,
                    node.TryGetProperty("label", out var labelJson) ? labelJson.GetString() ?? name : name,
                    type,
                    parentId,
                    rotationOrder,
                    !node.TryGetProperty("inherits_scale", out var inheritsJson) || inheritsJson.ValueKind != JsonValueKind.False,
                    ReadVector(node, "center_point", Vector3.Zero),
                    ReadVector(node, "end_point", Vector3.Zero),
                    ReadVector(node, "orientation", Vector3.Zero),
                    ReadVector(node, "rotation", Vector3.Zero),
                    ReadVector(node, "translation", Vector3.Zero),
                    ReadVector(node, "scale", Vector3.One),
                    ReadScalar(node, "general_scale", 1f)));
            }

            var byId = new Dictionary<string, DazBoneDefinition>(StringComparer.Ordinal);
            var byName = new Dictionary<string, DazBoneDefinition>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (!byId.TryAdd(node.Id, node))
                    throw new DazConversionException($"Duplicate node id '{node.Id}'.");
                if (!string.IsNullOrWhiteSpace(node.Name) && !byName.TryAdd(node.Name, node))
                    throw new DazConversionException($"Duplicate non-empty node name '{node.Name}'.");
            }

            var figures = nodes.Where(node => node.Type == "figure").ToArray();
            if (figures.Length != 1)
                throw new DazConversionException($"Expected exactly one figure root; found {figures.Length}.");

            foreach (var node in nodes.Where(node => node.Type == "bone"))
                if (node.ParentId is null || !byId.ContainsKey(node.ParentId))
                    throw new DazConversionException($"Bone '{node.Name}' ({node.Id}) has missing parent '{node.ParentId ?? "<none>"}'.");
            foreach (var node in nodes.Where(node => node.ParentId is not null))
                if (!byId.ContainsKey(node.ParentId!))
                    throw new DazConversionException($"Node '{node.Name}' ({node.Id}) references missing parent '{node.ParentId}'.");

            ValidateNoCycles(nodes, byId);
            return new DazFigureDefinition
            {
                FilePath = Path.GetFullPath(path), AssetId = assetId, Nodes = nodes,
                NodesById = byId, NodesByName = byName
            };
        }
        catch (DazConversionException ex)
        {
            throw new DazConversionException($"Figure '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new DazConversionException($"Figure '{Path.GetFileName(path)}' is malformed: {ex.Message}", ex);
        }
    }

    private static string RequiredString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new DazConversionException($"A node is missing required '{property}'.");
        return value.GetString()!;
    }

    private static string? NormalizeParent(string? parent)
    {
        if (string.IsNullOrWhiteSpace(parent)) return null;
        return parent.StartsWith('#') ? parent[1..] : parent;
    }

    private static Vector3 ReadVector(JsonElement node, string property, Vector3 fallback)
    {
        if (!node.TryGetProperty(property, out var channels) || channels.ValueKind != JsonValueKind.Array) return fallback;
        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var channel in channels.EnumerateArray())
        {
            if (!channel.TryGetProperty("id", out var axisJson) || axisJson.ValueKind != JsonValueKind.String) continue;
            if (!channel.TryGetProperty("value", out var valueJson)) continue;
            values[axisJson.GetString()!] = ReadNumber(valueJson);
        }
        return new Vector3(values.GetValueOrDefault("x", fallback.X), values.GetValueOrDefault("y", fallback.Y), values.GetValueOrDefault("z", fallback.Z));
    }

    private static float ReadScalar(JsonElement node, string property, float fallback)
    {
        if (!node.TryGetProperty(property, out var value)) return fallback;
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var scalar) ? ReadNumber(scalar) : fallback;
    }

    private static float ReadNumber(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetSingle(),
        JsonValueKind.String when float.TryParse(value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new FormatException("Expected a numeric channel value.")
    };

    private static void ValidateNoCycles(IEnumerable<DazBoneDefinition> nodes, IReadOnlyDictionary<string, DazBoneDefinition> byId)
    {
        var marks = new Dictionary<string, int>(StringComparer.Ordinal);
        void Visit(DazBoneDefinition node)
        {
            var mark = marks.GetValueOrDefault(node.Id);
            if (mark == 1) throw new DazConversionException($"Hierarchy cycle detected at node '{node.Name}' ({node.Id}).");
            if (mark == 2) return;
            marks[node.Id] = 1;
            if (node.ParentId is not null) Visit(byId[node.ParentId]);
            marks[node.Id] = 2;
        }
        foreach (var node in nodes) Visit(node);
    }
}
