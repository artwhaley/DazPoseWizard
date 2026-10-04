using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Project-owned light, combustion and turbulent vapor masks for Magic.</summary>
    internal static class PerformerMagicTextureBuilder
    {
        private const string Root = "Assets/DazPose/Effects/Magic/Shared";
        private const int Resolution = 256;

        public static void Generate(bool overwriteExisting = true)
        {
            Write("Mote", (x, y) =>
            {
                float radius = x * x + y * y;
                return Mask((Mathf.Exp(-radius * 35f) + Mathf.Exp(-radius * 8f) * 0.08f) * EdgeFade(radius));
            }, overwriteExisting);
            Write("Glow", (x, y) =>
            {
                float radius = x * x + y * y;
                return Mask(Mathf.Exp(-radius * 5f) * 0.55f * EdgeFade(radius));
            }, overwriteExisting);
            Write("Flame", (x, y) =>
            {
                float t = (y + 1f) * 0.5f;
                float noise = WarpedNoise(x * 3.0f + 7.1f, t * 4.0f + 13.4f);
                float center = (Fractal(x * 1.4f + 5f, t * 3f) - 0.5f) * 0.45f;
                float width = Mathf.Lerp(0.45f, 0.16f, t);
                float body = Mathf.Exp(-Mathf.Pow((x - center) / width, 2f) * 1.4f);
                float pockets = Smooth(0.28f, 0.68f, noise);
                float fringe = Mathf.Exp(-Mathf.Pow((noise - 0.45f) * 15f, 2f)) * 0.22f;
                float taper = Smooth(0f, 0.14f, t) * (1f - Smooth(0.66f, 1f, t));
                return Mask(body * (pockets + fringe) * taper * EdgeFade(x * x + y * y));
            }, overwriteExisting);
            Write("Wisp", (x, y) =>
            {
                float radius = x * x + y * y;
                float noise = WarpedNoise(x * 3.4f + 17.3f, y * 3.4f + 11.6f);
                float density = Smooth(0.34f, 0.68f, noise);
                float rim = Mathf.Exp(-Mathf.Pow((noise - 0.48f) * 14f, 2f));
                float detail = 0.60f + Fractal(x * 13f, y * 13f) * 0.40f;
                float falloff = Mathf.Exp(-radius * 3.2f) * EdgeFade(radius);
                return Mask((density * 0.70f + rim * 0.30f) * detail * falloff);
            }, overwriteExisting);
            Write("Smoke", (x, y) =>
            {
                float radius = x * x + y * y;
                float billow = WarpedNoise(x * 2.6f + 31.7f, y * 2.6f + 6.1f);
                float detail = 0.65f + Fractal(x * 11f + 3f, y * 11f) * 0.35f;
                return Mask(Mathf.Exp(-radius * 3.6f) * Smooth(0.25f, 0.73f, billow) * detail * EdgeFade(radius));
            }, overwriteExisting);
        }

        public static Texture2D Load(string name)
        {
            string path = Root + "/Magic" + name + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Magic layer texture is missing at " + path + ". Run Generate Starter Magic Assets.");
            return texture;
        }

        private static Color Mask(float alpha)
        {
            // Dim fringes leave a bright center without broad white bloom obscuring the color.
            float value = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(alpha));
            return new Color(value, value, value, Mathf.Clamp01(alpha));
        }

        private static float EdgeFade(float squaredRadius) => 1f - Smooth(0.55f, 0.98f, squaredRadius);

        private static float Fractal(float x, float y)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            for (int octave = 0; octave < 4; octave++)
            {
                sum += Mathf.PerlinNoise(x + 41.3f, y + 87.1f) * amplitude;
                x = x * 2.03f + 3.19f;
                y = y * 2.03f + 7.43f;
                amplitude *= 0.5f;
            }
            return sum / 0.9375f;
        }

        private static float WarpedNoise(float x, float y)
        {
            float warpX = Fractal(x + 12.1f, y + 7.8f) - 0.5f;
            float warpY = Fractal(x + 3.7f, y + 25.4f) - 0.5f;
            return Fractal(x + warpX * 1.8f, y + warpY * 1.8f);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static void Write(string name, Func<float, float, Color> sample, bool overwriteExisting)
        {
            string path = Root + "/Magic" + name + ".png";
            if (!overwriteExisting && File.Exists(path)) return;
            var pixels = new Color[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++)
                for (int x = 0; x < Resolution; x++)
                    pixels[y * Resolution + x] = sample((x + 0.5f) / Resolution * 2f - 1f, (y + 0.5f) / Resolution * 2f - 1f);
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Resolution;
            importer.SaveAndReimport();
        }
    }
}
