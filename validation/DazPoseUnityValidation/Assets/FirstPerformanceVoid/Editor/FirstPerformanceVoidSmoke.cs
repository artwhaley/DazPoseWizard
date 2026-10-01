using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using RenderingLayerMask = UnityEngine.Rendering.HighDefinition.RenderingLayerMask;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Authored native particle billows, replacing the rectangular perimeter fog banks.</summary>
    public static class FirstPerformanceVoidSmoke
    {
        private const string Root = "Assets/FirstPerformanceVoid";
        private const uint SmokeLayer = 4;

        public static void CopyFrontSettings(Transform room)
        {
            Transform group = room.Find("Environment/Perimeter Rising Smoke");
            if (group == null) throw new InvalidOperationException("Perimeter smoke group is missing.");
            ParticleSystem source = group.Find("Smoke Front").GetComponent<ParticleSystem>();
            foreach (string name in new[] { "Smoke Left", "Smoke Right", "Smoke Back" })
            {
                ParticleSystem target = group.Find(name).GetComponent<ParticleSystem>();
                Vector3 edgeSize = target.shape.scale;
                UnityEditorInternal.ComponentUtility.CopyComponent(source);
                if (!UnityEditorInternal.ComponentUtility.PasteComponentValues(target))
                    throw new InvalidOperationException("Could not copy Smoke Front settings to " + name);
                // Keep each source distributed along its own floor edge.
                var shape = target.shape;
                shape.scale = edgeSize;
                UnityEditorInternal.ComponentUtility.CopyComponent(source.GetComponent<ParticleSystemRenderer>());
                if (!UnityEditorInternal.ComponentUtility.PasteComponentValues(target.GetComponent<ParticleSystemRenderer>()))
                    throw new InvalidOperationException("Could not copy Smoke Front renderer to " + name);
            }
        }

        public static void Apply(Transform room)
        {
            Transform oldFog = room.Find("Environment/Volumetrics");
            if (oldFog != null) oldFog.gameObject.SetActive(false);
            Transform lights = room.Find("Environment/Lights");
            foreach (string side in new[] { "Left", "Right", "Front", "Back" })
            {
                Transform obsolete = lights.Find("Fog Only " + side);
                if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
            }
            Material material = SmokeMaterial();
            Transform group = Child(room.Find("Environment"), "Perimeter Rising Smoke");
            Emitter(group, "Smoke Left", new Vector3(-8.6f, 0.08f, 0f), new Vector3(0.4f, 0.1f, 14f), 42f, 3401, material);
            Emitter(group, "Smoke Right", new Vector3(8.6f, 0.08f, 0f), new Vector3(0.4f, 0.1f, 14f), 42f, 3402, material);
            Emitter(group, "Smoke Front", new Vector3(0f, 0.08f, -6.6f), new Vector3(18f, 0.1f, 0.4f), 54f, 3403, material);
            Emitter(group, "Smoke Back", new Vector3(0f, 0.08f, 6.6f), new Vector3(18f, 0.1f, 0.4f), 54f, 3404, material);
            SmokeLight(lights, "Smoke Edge Left", new Vector3(-8f, 0.6f, 0f));
            SmokeLight(lights, "Smoke Edge Right", new Vector3(8f, 0.6f, 0f));
            SmokeLight(lights, "Smoke Edge Front", new Vector3(0f, 0.6f, -6f));
            SmokeLight(lights, "Smoke Edge Back", new Vector3(0f, 0.6f, 6f));
        }

        private static void Emitter(Transform parent, string name, Vector3 position, Vector3 shapeSize,
            float rate, uint seed, Material material)
        {
            Transform transform = Child(parent, name);
            transform.localPosition = position;
            transform.localRotation = Quaternion.identity;
            ParticleSystem particles = transform.GetComponent<ParticleSystem>();
            if (particles == null) particles = transform.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = seed;
            var main = particles.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 650;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = shapeSize;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.9f)));
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            noise.strengthY = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.12f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.85f, 0.9f), 0f),
                new GradientColorKey(new Color(0.65f, 0.7f, 0.75f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.12f),
                    new GradientAlphaKey(0.4f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.normalDirection = 1f;
            renderer.maxParticleSize = 1f;
            renderer.renderingLayerMask = SmokeLayer;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                    ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
        }

        private static void SmokeLight(Transform parent, string name, Vector3 position)
        {
            Transform transform = Child(parent, name);
            transform.localPosition = position;
            Light light = transform.GetComponent<Light>();
            if (light == null) light = transform.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.12f, 0.85f);
            light.lightUnit = LightUnit.Candela;
            light.intensity = 4500f;
            light.range = 12f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            var hd = transform.GetComponent<HDAdditionalLightData>();
            if (hd == null) hd = transform.gameObject.AddComponent<HDAdditionalLightData>();
            hd.SetLightLayer((RenderingLayerMask)SmokeLayer, (RenderingLayerMask)1u);
            hd.affectDiffuse = true;
            hd.affectSpecular = false;
            hd.volumetricDimmer = 0f;
        }

        private static Material SmokeMaterial()
        {
            string path = Root + "/Materials/M_PerimeterSmoke.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/PerimeterSmokeLit.shadergraph");
                if (shader == null) throw new InvalidOperationException("Native HDRP smoke Shader Graph has not imported.");
                material = new Material(shader) { name = "M_PerimeterSmoke" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("Texture2D_23DD87FD", SmokeTexture());
            material.SetVector("Vector2_3782A8B8", new Vector4(0f, 0.5f, 0f, 0f));
            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D SmokeTexture()
        {
            string path = Root + "/Textures/T_SmokeBillow.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int resolution = 128;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true)
                { name = "T_SmokeBillow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / resolution * 2f - 1f, (y + 0.5f) / resolution * 2f - 1f);
                    float density = 0f;
                    for (int lobe = 0; lobe < 7; lobe++)
                    {
                        float angle = lobe * 2.39996f;
                        Vector2 center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (lobe == 0 ? 0f : 0.34f);
                        density += Mathf.Exp(-(p - center).sqrMagnitude / (lobe == 0 ? 0.22f : 0.1f)) * 0.35f;
                    }
                    float turbulence = 0.55f * Mathf.PerlinNoise(p.x * 3.1f + 17f, p.y * 3.1f + 43f)
                        + 0.3f * Mathf.PerlinNoise(p.x * 7f + 9f, p.y * 7f + 21f)
                        + 0.15f * Mathf.PerlinNoise(p.x * 15f + 36f, p.y * 15f + 8f);
                    float feather = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.96f, p.magnitude));
                    float alpha = Mathf.Clamp01(density * (0.35f + turbulence) * 1.7f) * feather;
                    float shade = 0.65f + turbulence * 0.35f;
                    pixels[y * resolution + x] = new Color(shade, shade, shade, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
