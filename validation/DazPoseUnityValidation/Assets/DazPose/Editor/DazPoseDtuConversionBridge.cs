using System;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DazPose.Daz3DConversion
{
    internal enum DtuMaterialConversionKind
    {
        IrayUber,
        DazStudioDefault
    }

    internal sealed class DtuConversionProperty
    {
        internal string Name { get; }
        internal string Kind { get; }
        internal double Number { get; }
        internal Color Color { get; }
        internal string Text { get; }
        internal string StagedTexturePath { get; }

        internal DtuConversionProperty(string name, string kind, double number, Color color, string text, string stagedTexturePath)
        {
            Name = name ?? string.Empty;
            Kind = kind ?? string.Empty;
            Number = number;
            Color = color;
            Text = text ?? string.Empty;
            StagedTexturePath = stagedTexturePath ?? string.Empty;
        }
    }

    internal sealed class DtuConversionRequest
    {
        internal string DtuPath { get; }
        internal string TextureDirectory { get; }
        internal string AssetName { get; }
        internal string MaterialName { get; }
        internal string MaterialType { get; }
        internal string Value { get; }
        internal string ProductName { get; }
        internal string ProductComponentName { get; }
        internal DtuMaterialConversionKind ConversionKind { get; }
        internal ReadOnlyCollection<DtuConversionProperty> Properties { get; }

        internal DtuConversionRequest(string dtuPath, string textureDirectory, string assetName, string materialName,
            string materialType, string value, string productName, string productComponentName,
            DtuMaterialConversionKind conversionKind, DtuConversionProperty[] properties)
        {
            DtuPath = dtuPath ?? string.Empty;
            TextureDirectory = textureDirectory ?? string.Empty;
            AssetName = assetName ?? string.Empty;
            MaterialName = materialName ?? string.Empty;
            MaterialType = materialType ?? string.Empty;
            Value = value ?? string.Empty;
            ProductName = productName ?? string.Empty;
            ProductComponentName = productComponentName ?? string.Empty;
            ConversionKind = conversionKind;
            Properties = Array.AsReadOnly(properties == null ? Array.Empty<DtuConversionProperty>() : (DtuConversionProperty[])properties.Clone());
        }
    }

    /// <summary>Editor-only seam for converting authored DTU surface data without a first-party vendor dependency.</summary>
    internal static class DazPoseDtuConversionBridge
    {
        private static Func<DtuConversionRequest, Material> _converter;

        internal static void Register(Func<DtuConversionRequest, Material> converter)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        internal static Material Convert(DtuConversionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var converter = _converter;
            if (converter == null)
                throw new InvalidOperationException("The DAZ DTU conversion adapter has not registered in Assembly-CSharp-Editor.");
            return converter(request);
        }
    }
}
