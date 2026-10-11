using System.Collections.Generic;
using DazPose.Daz3DConversion;
using UnityEditor;
using UnityEngine;

namespace Daz3D
{
    /// <summary>Adapts the vendor DTU types at the predefined editor assembly boundary.</summary>
    [InitializeOnLoad]
    internal static class DazPoseDtuConversionAdapter
    {
        static DazPoseDtuConversionAdapter()
        {
            DazPoseDtuConversionBridge.Register(Convert);
        }

        private static Material Convert(DtuConversionRequest request)
        {
            var converter = new DTU { DTUPath = request.DtuPath };
            var properties = new List<DTUMaterialProperty>(request.Properties.Count);
            foreach (DtuConversionProperty input in request.Properties)
            {
                var value = new DTUValue(input.Number);
                if (input.Kind == "color")
                {
                    value.Type = DTUValue.DataType.Color;
                    value.AsColor = input.Color;
                }
                if (input.Kind == "string")
                {
                    value.Type = DTUValue.DataType.String;
                    value.AsString = input.Text;
                }

                properties.Add(new DTUMaterialProperty
                {
                    Name = input.Name,
                    Value = value,
                    Texture = input.StagedTexturePath,
                    Exists = true
                });
            }

            var material = new DTUMaterial
            {
                AssetName = request.AssetName,
                MaterialName = request.MaterialName,
                MaterialType = request.MaterialType,
                Value = request.Value,
                ProductName = request.ProductName,
                ProductComponentName = request.ProductComponentName,
                Properties = properties
            };

            bool previousLegacyShaderSetting = Daz3DDTUImporter.UseLegacyShaders;
            try
            {
                Daz3DDTUImporter.UseLegacyShaders = false;
                return request.ConversionKind == DtuMaterialConversionKind.IrayUber
                    ? converter.ConvertToUnityIrayUber(material, request.TextureDirectory)
                    : converter.ConvertToUnityDazStudioDefault(material, request.TextureDirectory);
            }
            finally
            {
                Daz3DDTUImporter.UseLegacyShaders = previousLegacyShaderSetting;
            }
        }
    }
}
