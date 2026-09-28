using System.Numerics;

namespace DazPose.Core;

public static class DazTransformEvaluator
{
    public static DazPoseEvaluation Evaluate(DazFigureDefinition figure, DazPose pose)
    {
        var poseValues = new Dictionary<string, (Vector3 Rotation, Vector3 Translation)>(StringComparer.Ordinal);
        foreach (var bone in figure.Bones)
            poseValues[bone.Id] = (bone.Rotation, bone.Translation);
        foreach (var channel in pose.Channels.Where(channel => channel.IsSupportedSkeletalChannel))
        {
            var current = poseValues[channel.TargetBone!.Id];
            var value = channel.Keys[0].Value;
            var axis = channel.ParsedUrl.Axis;
            if (channel.ParsedUrl.Property == "rotation") current.Rotation = SetAxis(current.Rotation, axis!, value);
            else current.Translation = SetAxis(current.Translation, axis!, value);
            poseValues[channel.TargetBone.Id] = current;
        }

        var byId = new Dictionary<string, EvaluatedBonePose>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        EvaluatedBonePose EvaluateNode(DazBoneDefinition node)
        {
            if (byId.TryGetValue(node.Id, out var known)) return known;
            if (!visiting.Add(node.Id)) throw new DazConversionException($"Hierarchy cycle encountered while evaluating '{node.Name}'.");

            var values = poseValues.GetValueOrDefault(node.Id, (node.Rotation, node.Translation));
            var parent = node.ParentId is null ? null : EvaluateNode(figure.NodesById[node.ParentId]);
            var orientation = EulerToQuaternion(node.Orientation, "XYZ");
            var animatedRotation = EulerToQuaternion(values.Rotation, node.RotationOrder);
            var localRotation = Quaternion.Normalize(orientation * animatedRotation * Quaternion.Inverse(orientation));
            var worldRotation = parent is null
                ? localRotation
                : Quaternion.Normalize(parent.EvaluatedWorldRotation * localRotation);

            var localScaleMatrix = LocalScaleMatrix(node, orientation);
            Matrix4x4 worldScaleMatrix;
            if (parent is null) worldScaleMatrix = localScaleMatrix;
            else if (node.InheritsScale) worldScaleMatrix = localScaleMatrix * parent.EvaluatedGlobalScaleMatrix;
            else
            {
                var parentDefinition = figure.NodesById[parent.Bone.Id];
                var parentOrientation = EulerToQuaternion(parentDefinition.Orientation, "XYZ");
                if (!Matrix4x4.Invert(LocalScaleMatrix(parentDefinition, parentOrientation), out var inverseParentLocalScale))
                    throw new DazConversionException($"Cannot compensate the non-inherited scale of parent '{parentDefinition.Name}' because its local scale is singular.");
                worldScaleMatrix = localScaleMatrix * inverseParentLocalScale * parent.EvaluatedGlobalScaleMatrix;
            }

            var centerOffset = parent is null ? node.CenterPoint : node.CenterPoint - figure.NodesById[parent.Bone.Id].CenterPoint;
            var localOffset = centerOffset + values.Translation;
            if (parent is not null)
                localOffset = Vector3.TransformNormal(localOffset, parent.EvaluatedGlobalScaleMatrix);
            var worldPosition = parent is null
                ? localOffset
                : parent.EvaluatedWorldPositionCm + Vector3.Transform(localOffset, parent.EvaluatedWorldRotation);

            var evaluated = new EvaluatedBonePose(node, values.Rotation, values.Translation, localRotation, worldRotation, worldPosition,
                new Vector3(worldScaleMatrix.M11, worldScaleMatrix.M22, worldScaleMatrix.M33), worldScaleMatrix);
            byId[node.Id] = evaluated;
            visiting.Remove(node.Id);
            return evaluated;
        }

        var results = figure.Bones.Select(EvaluateNode).ToArray();
        return new DazPoseEvaluation { Bones = results, BonesById = byId };
    }

    public static Quaternion EulerToQuaternion(Vector3 degrees, string rotationOrder)
    {
        if (rotationOrder.Length != 3 || rotationOrder.Distinct().Count() != 3 || rotationOrder.Any(axis => axis is not ('X' or 'Y' or 'Z')))
            throw new DazConversionException($"Invalid Euler rotation order '{rotationOrder}'.");
        var result = Quaternion.Identity;
        foreach (var axis in rotationOrder)
        {
            var radians = Degrees(axis switch { 'X' => degrees.X, 'Y' => degrees.Y, _ => degrees.Z }) * 0.5f;
            var component = MathF.Sin(radians);
            var axisRotation = axis switch
            {
                'X' => new Quaternion(component, 0, 0, MathF.Cos(radians)),
                'Y' => new Quaternion(0, component, 0, MathF.Cos(radians)),
                _ => new Quaternion(0, 0, component, MathF.Cos(radians))
            };
            // The order string names the fixed axes applied in sequence (for XYZ: X, then Y, then Z).
            // With Hamilton quaternions, each later fixed-axis rotation is pre-multiplied.
            result = Quaternion.Normalize(axisRotation * result);
        }
        return result;
    }

    public static Vector3 QuaternionToEuler(Quaternion quaternion, string rotationOrder)
    {
        if (rotationOrder.Length != 3 || rotationOrder.Distinct().Count() != 3 || rotationOrder.Any(axis => axis is not ('X' or 'Y' or 'Z')))
            throw new DazConversionException($"Invalid Euler rotation order '{rotationOrder}'.");
        quaternion = Quaternion.Normalize(quaternion);
        var rowMatrix = Matrix4x4.CreateFromQuaternion(quaternion);
        var m = Matrix4x4.Transpose(rowMatrix); // Convert System.Numerics row-vector matrix to the column-vector convention used by the decomposition.
        var (firstAxis, parity) = rotationOrder switch
        {
            "XYZ" => (0, 0), "XZY" => (0, 1), "YZX" => (1, 0),
            "YXZ" => (1, 1), "ZXY" => (2, 0), "ZYX" => (2, 1),
            _ => throw new DazConversionException($"Invalid Euler rotation order '{rotationOrder}'.")
        };
        var next = new[] { 1, 2, 0, 1 };
        var i = firstAxis;
        var j = next[i + parity];
        var k = next[i - parity + 1];
        var matrix = new double[,]
        {
            { m.M11, m.M12, m.M13 },
            { m.M21, m.M22, m.M23 },
            { m.M31, m.M32, m.M33 }
        };
        var cy = Math.Sqrt(matrix[i, i] * matrix[i, i] + matrix[j, i] * matrix[j, i]);
        double first, middle, last;
        if (cy > 1e-7)
        {
            first = Math.Atan2(matrix[k, j], matrix[k, k]);
            middle = Math.Atan2(-matrix[k, i], cy);
            last = Math.Atan2(matrix[j, i], matrix[i, i]);
        }
        else
        {
            first = Math.Atan2(-matrix[j, k], matrix[j, j]);
            middle = Math.Atan2(-matrix[k, i], cy);
            last = 0;
        }
        if (parity != 0) { first = -first; middle = -middle; last = -last; }

        var angles = new Dictionary<char, float>
        {
            [rotationOrder[0]] = RadiansToDegrees((float)first),
            [rotationOrder[1]] = RadiansToDegrees((float)middle),
            [rotationOrder[2]] = RadiansToDegrees((float)last)
        };
        return new Vector3(angles['X'], angles['Y'], angles['Z']);
    }

    private static Vector3 SetAxis(Vector3 vector, string axis, float value) => axis switch
    {
        "x" => vector with { X = value },
        "y" => vector with { Y = value },
        "z" => vector with { Z = value },
        _ => vector
    };

    private static Matrix4x4 LocalScaleMatrix(DazBoneDefinition node, Quaternion orientation)
    {
        var orientedScale = Matrix4x4.CreateScale(node.Scale * node.GeneralScale);
        var orientationMatrix = Matrix4x4.CreateFromQuaternion(orientation);
        var inverseOrientationMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(orientation));
        // System.Numerics transforms row vectors, so this is the transposed form of DSON's O*S*G*O^-1.
        return inverseOrientationMatrix * orientedScale * orientationMatrix;
    }

    private static float Degrees(float value) => value * (MathF.PI / 180f);
    private static float RadiansToDegrees(float value) => value * (180f / MathF.PI);
}
