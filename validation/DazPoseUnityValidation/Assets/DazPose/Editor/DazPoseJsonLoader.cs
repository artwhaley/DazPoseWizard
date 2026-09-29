using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public static class DazPoseJsonLoader
    {
        public static DazPoseDefinition Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("Choose an existing .dazpose.json file.", path);

            var json = File.ReadAllText(path);
            var definition = JsonUtility.FromJson<DazPoseDefinition>(json);
            if (definition == null || definition.format != "DazPoseTool"
                || (definition.version != 1 && definition.version != 2))
                throw new InvalidDataException("The selected file is not a supported DazPoseTool version 1 or 2 .dazpose.json.");
            var hasActiveFigureControl = definition.version == 2 && (definition.figureControls ?? Array.Empty<DazPoseFigureControl>())
                .Any(control => control != null && Mathf.Abs(control.value) > 1e-7f);
            if (definition.source == null || definition.bones == null || (definition.bones.Length == 0 && !hasActiveFigureControl))
                throw new InvalidDataException("The selected .dazpose.json has no source metadata or supported skeletal/figure-control data.");

            var unsupportedActiveChannel = (definition.poseChannels ?? Array.Empty<DazPoseChannel>())
                .FirstOrDefault(channel => channel != null && !channel.supported
                    && (channel.keys ?? Array.Empty<DazPoseKey>()).Any(key => key != null && Mathf.Abs(key.value) > 1e-7f));
            if (unsupportedActiveChannel != null)
                throw new InvalidDataException("Canonical pose contains an unsupported non-neutral property '" + unsupportedActiveChannel.url + "'. It cannot be applied or converted to a clip without an explicit mapping.");

            foreach (var bone in definition.bones)
            {
                if (string.IsNullOrEmpty(bone.id) || string.IsNullOrEmpty(bone.name)
                    || bone.restWorldPositionCm == null || bone.restWorldPositionCm.Length != 3
                    || bone.evaluatedWorldPositionCm == null || bone.evaluatedWorldPositionCm.Length != 3
                    || bone.restWorldRotation == null || bone.restWorldRotation.Length != 4
                    || bone.evaluatedWorldRotation == null || bone.evaluatedWorldRotation.Length != 4)
                {
                    throw new InvalidDataException("Bone '" + bone.id + "' is missing required rest/pose transform fields. Regenerate the JSON with the current converter.");
                }
            }

            var poseName = PoseName(definition.source.poseFile, definition.source.poseAssetId);
            Debug.Log("DAZ Pose loaded: " + poseName + " | figure " + definition.source.figureAssetId
                + " | bones " + definition.bones.Length + " | channels "
                + (definition.poseChannels == null ? 0 : definition.poseChannels.Length));
            return definition;
        }

        public static string PoseName(string sourcePath, string assetId)
        {
            if (!string.IsNullOrEmpty(sourcePath))
            {
                var normalized = sourcePath.Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(normalized);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return string.IsNullOrEmpty(assetId) ? "Unknown DAZ pose" : assetId;
        }

        public static Vector3 Vector(float[] values)
        {
            if (values == null || values.Length < 3) return Vector3.zero;
            return new Vector3(values[0], values[1], values[2]);
        }

        public static Quaternion Quaternion(float[] values)
        {
            if (values == null || values.Length < 4) return UnityEngine.Quaternion.identity;
            var result = new UnityEngine.Quaternion(values[0], values[1], values[2], values[3]);
            var normSquared = result.x * result.x + result.y * result.y + result.z * result.z + result.w * result.w;
            return normSquared < 1e-12f ? UnityEngine.Quaternion.identity : UnityEngine.Quaternion.Normalize(result);
        }
    }
}
